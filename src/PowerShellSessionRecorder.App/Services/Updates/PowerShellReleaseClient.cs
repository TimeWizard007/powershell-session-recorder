using System.Net;
using System.Net.Http.Headers;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.Services.Updates;

public interface IPowerShellReleaseLookup
{
    Task<GitHubFetchResult> GetLatestStableAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the latest stable PowerShell release. This is not the application update check.
/// </summary>
public sealed class PowerShellReleaseClient : IPowerShellReleaseLookup
{
    public static readonly Uri LatestStableUri = new("https://api.github.com/repos/PowerShell/PowerShell/releases/latest");

    private readonly HttpClient _http;

    public PowerShellReleaseClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static HttpClient CreateClient(string applicationVersion)
    {
        var version = string.IsNullOrWhiteSpace(applicationVersion) ? "0" : applicationVersion.Trim();
        var client = new HttpClient { Timeout = GitHubReleaseClient.DefaultTimeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            $"PowerShellSessionRecorder/{version} (+https://github.com/TimeWizard007/powershell-session-recorder)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async Task<GitHubFetchResult> GetLatestStableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestStableUri);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null);

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new GitHubFetchResult(GitHubFetchStatus.RateLimited, null, null);

            if (!response.IsSuccessStatusCode)
                return new GitHubFetchResult(GitHubFetchStatus.Malformed, null, null);

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var document = GitHubReleaseDocument.Parse(json);
            if (document.Draft || document.Prerelease)
                return new GitHubFetchResult(GitHubFetchStatus.NotFound, null, null);

            return document.Status switch
            {
                GitHubDocumentStatus.Parsed => new GitHubFetchResult(GitHubFetchStatus.Ok, document.TagName, document.HtmlUrl),
                GitHubDocumentStatus.MissingTag => new GitHubFetchResult(GitHubFetchStatus.MissingTag, null, null),
                _ => new GitHubFetchResult(GitHubFetchStatus.Malformed, null, null)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GitHubFetchResult(GitHubFetchStatus.Canceled, null, null);
        }
        catch (OperationCanceledException)
        {
            return new GitHubFetchResult(GitHubFetchStatus.TimedOut, null, null);
        }
        catch (HttpRequestException)
        {
            return new GitHubFetchResult(GitHubFetchStatus.Offline, null, null);
        }
    }
}
