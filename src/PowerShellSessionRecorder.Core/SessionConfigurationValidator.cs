namespace PowerShellSessionRecorder.Core;

public static class SessionValidationError
{
    public const string CustomerTagRequired = nameof(CustomerTagRequired);

    public const string SessionNameRequired = nameof(SessionNameRequired);

    public const string CustomerTagNotFileSafe = nameof(CustomerTagNotFileSafe);

    public const string SessionNameNotFileSafe = nameof(SessionNameNotFileSafe);
}

public sealed class SessionValidationResult
{
    public SessionValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public bool HasError(string code) => Errors.Contains(code, StringComparer.Ordinal);
}

public static class SessionConfigurationValidator
{
    public static SessionValidationResult Validate(SessionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = new List<string>(capacity: 2);
        AddNameErrors(
            configuration.CustomerTag,
            SessionValidationError.CustomerTagRequired,
            SessionValidationError.CustomerTagNotFileSafe,
            errors);
        AddNameErrors(
            configuration.SessionName,
            SessionValidationError.SessionNameRequired,
            SessionValidationError.SessionNameNotFileSafe,
            errors);

        return new SessionValidationResult(errors);
    }

    private static void AddNameErrors(string? value, string requiredCode, string unsafeCode, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(requiredCode);
            return;
        }

        if (TranscriptFilenameGenerator.SanitizeSegment(value).Length == 0)
            errors.Add(unsafeCode);
    }
}
