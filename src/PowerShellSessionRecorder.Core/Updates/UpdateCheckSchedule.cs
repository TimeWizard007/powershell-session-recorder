namespace PowerShellSessionRecorder.Core.Updates;

public static class UpdateCheckSchedule
{
    public static readonly TimeSpan AutomaticInterval = TimeSpan.FromHours(24);

    public static bool AutomaticCheckIsDue(DateTimeOffset? lastSuccessfulCheckUtc, DateTimeOffset utcNow)
    {
        if (lastSuccessfulCheckUtc is null)
            return true;

        return utcNow - lastSuccessfulCheckUtc.Value >= AutomaticInterval;
    }
}
