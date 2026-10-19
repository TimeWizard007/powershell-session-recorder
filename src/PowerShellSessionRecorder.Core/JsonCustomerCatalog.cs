using System.Text;
using System.Text.Json;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Versioned customer TAG catalog stored beside the user settings file.
/// Writes are atomic. The file does not contain transcripts or credentials.
/// </summary>
public sealed class JsonCustomerCatalog : ICustomerCatalog
{
    public const int CurrentSchemaVersion = 1;

    public const string FileName = "customers.json";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Lock _gate = new();

    public JsonCustomerCatalog(string filePath)
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

    public CustomerCatalog Load()
    {
        lock (_gate)
        {
            try
            {
                if (!TryPrepareForRead())
                    return CustomerCatalog.Unreadable();

                if (!File.Exists(FilePath))
                    return CustomerCatalog.Missing();

                using var fileLock = AcquireFileLock();
                return ReadUnlocked();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CustomerCatalog.Unreadable();
            }
        }
    }

    public CustomerEditResult Add(string tag, string? displayName, DateTimeOffset createdUtc)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return CustomerEditResult.Fail(CustomerEditError.Required);
        if (!CustomerTagRules.IsValid(tag))
            return CustomerEditResult.Fail(CustomerEditError.Invalid);
        if (!CustomerTagRules.IsDisplayNameValid(displayName ?? ""))
            return CustomerEditResult.Fail(CustomerEditError.DisplayNameInvalid);
        if (createdUtc == default)
            throw new ArgumentException("Creation time is required.", nameof(createdUtc));

        var record = new CustomerRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            Tag = tag.Trim(),
            DisplayName = CustomerTagRules.NormalizeDisplayName(displayName),
            CreatedUtc = createdUtc
        };

        return Mutate(customers =>
        {
            if (customers.Any(existing => CustomerTagRules.TagsConflict(existing.Tag, record.Tag)))
                return CustomerEditResult.Fail(CustomerEditError.Duplicate);

            customers.Add(record);
            return CustomerEditResult.Ok(record);
        });
    }

    public CustomerEditResult Update(string id, string tag, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (string.IsNullOrWhiteSpace(tag))
            return CustomerEditResult.Fail(CustomerEditError.Required);
        if (!CustomerTagRules.IsValid(tag))
            return CustomerEditResult.Fail(CustomerEditError.Invalid);
        if (!CustomerTagRules.IsDisplayNameValid(displayName ?? ""))
            return CustomerEditResult.Fail(CustomerEditError.DisplayNameInvalid);

        var normalizedTag = tag.Trim();
        var normalizedName = CustomerTagRules.NormalizeDisplayName(displayName);

        return Mutate(customers =>
        {
            var record = customers.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                return CustomerEditResult.Fail(CustomerEditError.Missing);

            if (customers.Any(existing =>
                    !string.Equals(existing.Id, record.Id, StringComparison.OrdinalIgnoreCase)
                    && CustomerTagRules.TagsConflict(existing.Tag, normalizedTag)))
                return CustomerEditResult.Fail(CustomerEditError.Duplicate);

            record.Tag = normalizedTag;
            record.DisplayName = normalizedName;
            return CustomerEditResult.Ok(record);
        });
    }

    public CustomerEditResult Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Mutate(customers =>
        {
            var record = customers.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                return CustomerEditResult.Fail(CustomerEditError.Missing);

            customers.Remove(record);
            return CustomerEditResult.Ok(record);
        });
    }

    private CustomerEditResult Mutate(Func<List<CustomerRecord>, CustomerEditResult> change)
    {
        lock (_gate)
        {
            try
            {
                PrepareForWrite();
                using var fileLock = AcquireFileLock();
                var catalog = ReadUnlocked();
                if (catalog.Kind is CustomerCatalogKind.Unreadable or CustomerCatalogKind.UnsupportedSchema)
                    return CustomerEditResult.Fail(CustomerEditError.CatalogUnavailable);

                var customers = catalog.Customers.ToList();
                var result = change(customers);
                if (result.Error != CustomerEditError.None)
                    return result;

                WriteDocument(customers);
                return result;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CustomerEditResult.Fail(CustomerEditError.CatalogUnavailable);
            }
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

    private void PrepareForWrite()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Customer catalog path does not include a directory.");

        UnixPrivateMetadata.PrepareOwnerDirectory(directory);
        UnixPrivateMetadata.RejectSymbolicLink(FilePath);
    }

    private CustomerCatalog ReadUnlocked()
    {
        if (UnixPrivateMetadata.IsSymbolicLink(FilePath))
            return CustomerCatalog.Unreadable();

        if (!File.Exists(FilePath))
            return CustomerCatalog.Missing();

        string json;
        try
        {
            json = File.ReadAllText(FilePath, Utf8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CustomerCatalog.Unreadable();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Customer catalog is not an object.");

            var version = CurrentSchemaVersion;
            if (document.RootElement.TryGetProperty("schemaVersion", out var versionElement))
            {
                if (!versionElement.TryGetInt32(out version))
                    throw new JsonException("Customer catalog schema version is not an integer.");
            }

            if (version > CurrentSchemaVersion)
                return CustomerCatalog.UnsupportedSchema();

            if (version < 1)
                throw new JsonException("Customer catalog schema version is not supported.");

            var catalog = JsonSerializer.Deserialize<CustomerCatalogDocument>(json, SerializerOptions)
                ?? throw new JsonException("Customer catalog did not contain an object.");

            if (catalog.Customers is null)
                throw new JsonException("Customer catalog has no customer list.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var customer in catalog.Customers)
            {
                if (!CustomerRecord.IsPersistable(customer) || !ids.Add(customer.Id) || !tags.Add(customer.Tag))
                    throw new JsonException("Customer catalog contains an invalid customer.");
            }

            return CustomerCatalog.Loaded(catalog.Customers);
        }
        catch (JsonException)
        {
            try
            {
                Quarantine();
                return CustomerCatalog.Quarantined();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CustomerCatalog.Unreadable();
            }
        }
    }

    private void Quarantine()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Customer catalog path does not include a directory.");

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var destination = Path.Combine(
                directory,
                $"customers.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{attempt}.json");
            if (File.Exists(destination) || UnixPrivateMetadata.IsSymbolicLink(destination))
                continue;

            UnixPrivateMetadata.RejectSymbolicLink(FilePath);
            File.Move(FilePath, destination);
            UnixPrivateMetadata.TryRestrictRegularFile(destination);
            LastQuarantinePath = destination;
            return;
        }

        throw new IOException("The unreadable customer catalog could not be preserved.");
    }

    private void WriteDocument(List<CustomerRecord> customers)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Customer catalog path does not include a directory.");

        UnixPrivateMetadata.PrepareOwnerDirectory(directory);
        UnixPrivateMetadata.RejectSymbolicLink(FilePath);
        var legacyTemporary = FilePath + ".tmp";
        if (Directory.Exists(legacyTemporary))
            throw new IOException("The customer catalog temporary path is not a file.");

        var temporary = UnixPrivateMetadata.CreateUniqueTemporaryPath(FilePath);
        var json = JsonSerializer.Serialize(new CustomerCatalogDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Customers = customers
        }, SerializerOptions) + Environment.NewLine;
        try
        {
            UnixPrivateMetadata.WriteNewPrivateFile(temporary, Utf8.GetBytes(json));
            File.Move(temporary, FilePath, overwrite: true);
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

        UnixPrivateMetadata.TryRestrictRegularFile(FilePath);
    }

    private FileStream AcquireFileLock()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Customer catalog path does not include a directory.");

        UnixPrivateMetadata.PrepareOwnerDirectory(directory);
        var lockPath = FilePath + ".lock";
        UnixPrivateMetadata.RejectSymbolicLink(lockPath);
        var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (stream.Length == 0)
            {
                stream.WriteByte(0);
                stream.Flush();
            }

            UnixPrivateMetadata.TryRestrictRegularFile(lockPath);
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
}
