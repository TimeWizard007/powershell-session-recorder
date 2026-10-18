using System.Text.Json;

namespace PowerShellSessionRecorder.Core.Updates;

public enum GitHubDocumentStatus
{
    Parsed,
    Malformed,
    MissingTag
}

public readonly record struct GitHubReleaseDocument(GitHubDocumentStatus Status, string? TagName, string? HtmlUrl)
{
    public static GitHubReleaseDocument Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new GitHubReleaseDocument(GitHubDocumentStatus.Malformed, null, null);

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new GitHubReleaseDocument(GitHubDocumentStatus.Malformed, null, null);

            if (!document.RootElement.TryGetProperty("tag_name", out var tagProperty)
                || tagProperty.ValueKind != JsonValueKind.String)
                return new GitHubReleaseDocument(GitHubDocumentStatus.MissingTag, null, null);

            var tag = tagProperty.GetString();
            if (string.IsNullOrWhiteSpace(tag))
                return new GitHubReleaseDocument(GitHubDocumentStatus.MissingTag, null, null);

            string? htmlUrl = null;
            if (document.RootElement.TryGetProperty("html_url", out var urlProperty)
                && urlProperty.ValueKind == JsonValueKind.String)
                htmlUrl = urlProperty.GetString();

            return new GitHubReleaseDocument(GitHubDocumentStatus.Parsed, tag, htmlUrl);
        }
        catch (JsonException)
        {
            return new GitHubReleaseDocument(GitHubDocumentStatus.Malformed, null, null);
        }
    }
}
