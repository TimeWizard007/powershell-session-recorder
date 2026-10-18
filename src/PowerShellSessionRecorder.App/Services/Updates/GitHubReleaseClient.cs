using System.Net;
using System.Net.Http.Headers;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.Services.Updates;

public interface IGitHubReleaseSource
{
    Task<GitHubFetchResult> GetLatestAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the latest stable GitHub release. The request carries no local configuration.
/// </summary>
public sealed class GitHubReleaseClient : IGitHubReleaseSource
{
    public static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest");

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient _http;

    public GitHubReleaseClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public static HttpClient CreateClient(string applicationVersion)
    {
        var version = string.IsNullOrWhiteSpace(applicationVersion) ? "0" : applicationVersion.Trim();
        var client = new HttpClient { Timeout = DefaultTimeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            $"PowerShellSessionRecorder/{version} (+https://github.com/TimeWizard007/powershell-session-recorder)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async Task<GitHubFetchResult> GetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
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
