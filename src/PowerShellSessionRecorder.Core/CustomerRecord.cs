namespace PowerShellSessionRecorder.Core;

/// <summary>
/// A saved customer TAG. This is catalog metadata only. It is not a history entry
/// and it does not contain transcript text or credentials.
/// </summary>
public sealed class CustomerRecord
{
    public string Id { get; set; } = "";

    public string Tag { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; }

    public static bool IsPersistable(CustomerRecord? record)
    {
        if (record is null)
            return false;

        return Guid.TryParseExact(record.Id, "D", out _)
            && CustomerTagRules.IsValid(record.Tag)
            && string.Equals(record.Tag, record.Tag.Trim(), StringComparison.Ordinal)
            && CustomerTagRules.IsDisplayNameValid(record.DisplayName)
            && string.Equals(record.DisplayName, record.DisplayName.Trim(), StringComparison.Ordinal)
            && record.CreatedUtc != default;
    }
}
