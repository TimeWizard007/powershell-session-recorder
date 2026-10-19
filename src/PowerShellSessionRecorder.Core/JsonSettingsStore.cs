using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerShellSessionRecorder.Core;

public enum SettingsLoadStatus
{
    Missing,
    Loaded,
    Invalid
}

public sealed class SettingsLoadResult
{
    private SettingsLoadResult(SettingsLoadStatus status, ApplicationSettings settings)
    {
        Status = status;
        Settings = settings;
    }

    public SettingsLoadStatus Status { get; }

    public ApplicationSettings Settings { get; }

    public static SettingsLoadResult Missing() => new(SettingsLoadStatus.Missing, new ApplicationSettings());

    public static SettingsLoadResult Loaded(ApplicationSettings settings) => new(SettingsLoadStatus.Loaded, settings);

    public static SettingsLoadResult Invalid() => new(SettingsLoadStatus.Invalid, new ApplicationSettings());
}

/// <summary>
/// Reads and writes <see cref="ApplicationSettings"/> as UTF-8 JSON.
/// A missing file and a file that cannot be parsed both yield defaults.
/// An invalid file is left in place and copied once to settings.invalid.json.
/// </summary>
public sealed class JsonSettingsStore
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly Lock _gate = new();

    public JsonSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string InvalidBackupPath => Path.Combine(
        Path.GetDirectoryName(FilePath) ?? "",
        "settings.invalid.json");

    public static JsonSerializerOptions CreateSerializerOptions()
        => new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

    public SettingsLoadResult LoadDetailed()
    {
        lock (_gate)
        {
            if (!TryPrepareForRead())
                return SettingsLoadResult.Invalid();

            if (!File.Exists(FilePath))
                return SettingsLoadResult.Missing();

            ApplicationSettings settings;
            try
            {
                var json = File.ReadAllText(FilePath, Utf8);
                if (string.IsNullOrWhiteSpace(json))
                    throw new JsonException("Settings file is empty.");

                settings = JsonSerializer.Deserialize<ApplicationSettings>(json, SerializerOptions)
                    ?? throw new JsonException("Settings file did not contain an object.");
            }
            catch (JsonException)
            {
                PreserveInvalidCopy();
                return SettingsLoadResult.Invalid();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return SettingsLoadResult.Invalid();
            }

            try
            {
                if (settings.Normalize())
                    WriteUnlocked(settings);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Keep the repaired settings in memory when the file cannot be rewritten.
            }

            return SettingsLoadResult.Loaded(settings);
        }
    }

    public ApplicationSettings Load() => LoadDetailed().Settings;

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_gate)
            WriteUnlocked(settings);
    }

    private void WriteUnlocked(ApplicationSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Settings file path does not include a directory.");

        UnixPrivateMetadata.PrepareOwnerDirectory(directory);
        UnixPrivateMetadata.RejectSymbolicLink(FilePath);
        var json = JsonSerializer.Serialize(settings, SerializerOptions) + Environment.NewLine;
        var temporary = UnixPrivateMetadata.CreateUniqueTemporaryPath(FilePath);
        try
        {
            UnixPrivateMetadata.WriteNewPrivateFile(temporary, Utf8.GetBytes(json));
            File.Move(temporary, FilePath, overwrite: true);
            UnixPrivateMetadata.TryRestrictRegularFile(FilePath);
        }
        catch
        {
            try
            {
                if (File.Exists(temporary) && !UnixPrivateMetadata.IsSymbolicLink(temporary))
                    File.Delete(temporary);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private bool TryPrepareForRead()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                UnixPrivateMetadata.RejectSymbolicLink(directory);
                UnixPrivateMetadata.TryRestrictDirectory(directory);
            }

            UnixPrivateMetadata.RejectSymbolicLink(FilePath);
            if (File.Exists(FilePath))
                UnixPrivateMetadata.TryRestrictRegularFile(FilePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void PreserveInvalidCopy()
    {
        try
        {
            if (UnixPrivateMetadata.IsSymbolicLink(InvalidBackupPath)
                || UnixPrivateMetadata.IsSymbolicLink(FilePath)
                || File.Exists(InvalidBackupPath)
                || !File.Exists(FilePath))
                return;

            File.Copy(FilePath, InvalidBackupPath, overwrite: false);
            UnixPrivateMetadata.TryRestrictRegularFile(InvalidBackupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the original file for the operator to inspect.
        }
    }
}
