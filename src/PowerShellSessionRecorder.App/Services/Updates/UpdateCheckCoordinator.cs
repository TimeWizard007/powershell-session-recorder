using PowerShellSessionRecorder.Core;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.Services.Updates;

/// <summary>
/// Decides when to call GitHub and how to remember the last successful result.
/// The current version is supplied by the caller. It is not invented here.
/// </summary>
public sealed class UpdateCheckCoordinator
{
    private readonly IGitHubReleaseSource _source;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly string _currentVersion;

    public UpdateCheckCoordinator(IGitHubReleaseSource source, Func<DateTimeOffset> utcNow, string currentVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(utcNow);
        _source = source;
        _utcNow = utcNow;
        _currentVersion = currentVersion ?? "";
    }

    public string CurrentVersion => _currentVersion;

    public async Task<UpdateCheckOutcome> CheckAsync(
        ApplicationSettings settings,
        bool manual,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!manual && !UpdateCheckSchedule.AutomaticCheckIsDue(settings.LastUpdateCheckUtc, _utcNow()))
        {
            return new UpdateCheckOutcome(
                UpdateCheckKind.NotDue,
                _currentVersion,
                string.IsNullOrWhiteSpace(settings.LastKnownStableVersion) ? null : settings.LastKnownStableVersion,
                string.IsNullOrWhiteSpace(settings.LastKnownReleaseUrl) ? null : settings.LastKnownReleaseUrl);
        }

        GitHubFetchResult fetch;
        try
        {
            fetch = await _source.GetLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckOutcome(UpdateCheckKind.Canceled, _currentVersion, null, null);
        }
        catch (OperationCanceledException)
        {
            return new UpdateCheckOutcome(UpdateCheckKind.TimedOut, _currentVersion, null, null);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckOutcome(UpdateCheckKind.Offline, _currentVersion, null, null);
        }

        var outcome = UpdateCheckOutcome.Interpret(_currentVersion, fetch);
        UpdateCheckOutcome.Remember(settings, outcome, _utcNow());
        return outcome;
    }
}
