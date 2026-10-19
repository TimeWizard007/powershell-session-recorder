using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Versioned JSON history stored beside the user settings file.
/// Writes replace a temporary file and do not store transcript contents.
/// </summary>
public sealed class JsonHistoryStore : ISessionHistoryStore
{
    public const int CurrentSchemaVersion = 1;

    public const string FileName = "history.json";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Lock _gate = new();

    public JsonHistoryStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string? LastQuarantinePath { get; private set; }

    public static string GetFilePath(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Settings file path does not include a directory.");

        return Path.Combine(directory, FileName);
    }

    public HistoryCatalog Load()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
                return HistoryCatalog.Missing();

            using var fileLock = AcquireFileLock();
            return ReadUnlocked();
        }
    }

    public void Append(SessionHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!SessionHistoryEntry.IsPersistable(entry))
            throw new ArgumentException("History entry is incomplete.", nameof(entry));

        lock (_gate)
        {
            using var fileLock = AcquireFileLock();
            var catalog = ReadUnlocked();
            if (catalog.Kind is HistoryLoadKind.Unreadable or HistoryLoadKind.UnsupportedSchema)
                throw new IOException("History file could not be updated.");

            var sessions = catalog.Sessions.ToList();
            if (sessions.Any(existing => string.Equals(existing.Id, entry.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A history entry with this id already exists.");

            sessions.Add(entry);
            WriteDocument(new SessionHistoryDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Sessions = sessions
            });
        }
    }

    public void RecordCompletion(string id, DateTimeOffset endedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (_gate)
        {
            using var fileLock = AcquireFileLock();
            var catalog = ReadUnlocked();
            if (catalog.Kind != HistoryLoadKind.Loaded)
                return;

            var entry = catalog.Sessions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return;

            if (entry.RecordingStatus == SessionHistoryStatus.Finished && entry.EndedUtc == endedUtc)
                return;

            entry.RecordingStatus = SessionHistoryStatus.Finished;
            entry.EndedUtc = endedUtc;
            WriteDocument(new SessionHistoryDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Sessions = catalog.Sessions.ToList()
            });
        }
    }

    private HistoryCatalog ReadUnlocked()
    {
        if (!File.Exists(FilePath))
            return HistoryCatalog.Missing();

        string json;
        try
        {
            json = File.ReadAllText(FilePath, Utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return HistoryCatalog.Unreadable();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("History file is not an object.");

            var version = CurrentSchemaVersion;
            if (document.RootElement.TryGetProperty("schemaVersion", out var versionElement))
            {
                if (!versionElement.TryGetInt32(out version))
                    throw new JsonException("History schema version is not an integer.");
            }

            if (version > CurrentSchemaVersion)
                return HistoryCatalog.UnsupportedSchema();

            if (version < 1)
                throw new JsonException("History schema version is not supported.");

            var history = JsonSerializer.Deserialize<SessionHistoryDocument>(json, SerializerOptions)
                ?? throw new JsonException("History file did not contain an object.");

            if (history.Sessions is null)
                throw new JsonException("History file has no session list.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in history.Sessions)
            {
                if (!SessionHistoryEntry.IsPersistable(entry) || !ids.Add(entry.Id))
                    throw new JsonException("History file contains an invalid session.");
            }

            return HistoryCatalog.Loaded(history.Sessions);
        }
        catch (JsonException)
        {
            try
            {
                Quarantine();
                return HistoryCatalog.Quarantined();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return HistoryCatalog.Unreadable();
            }
        }
    }

    private void Quarantine()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("History file path does not include a directory.");

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var destination = Path.Combine(
                directory,
                $"history.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{attempt}.json");
            if (File.Exists(destination))
                continue;

            File.Move(FilePath, destination);
            RestrictToCurrentUser(destination);
            LastQuarantinePath = destination;
            return;
        }

        throw new IOException("The unreadable history file could not be preserved.");
    }

    private void WriteDocument(SessionHistoryDocument document)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("History file path does not include a directory.");

        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        if (Directory.Exists(temporary))
            throw new IOException("The history temporary path is not a file.");

        var json = JsonSerializer.Serialize(document, SerializerOptions) + Environment.NewLine;
        File.WriteAllText(temporary, json, Utf8);
        RestrictToCurrentUser(temporary);
        try
        {
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }

        RestrictToCurrentUser(FilePath);
    }

    private FileStream AcquireFileLock()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("History file path does not include a directory.");

        Directory.CreateDirectory(directory);
        var lockPath = FilePath + ".lock";
        var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (stream.Length == 0)
            {
                stream.WriteByte(0);
                stream.Flush();
            }

            RestrictToCurrentUser(lockPath);
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                stream.Lock(0, 1);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void RestrictToCurrentUser(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }
}
