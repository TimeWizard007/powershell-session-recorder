using System.Text.Json;

namespace PowerShellSessionRecorder.Core.Updates;

public enum GitHubDocumentStatus
{
    Parsed,
    Malformed,
    MissingTag
}

public readonly record struct GitHubReleaseDocument(
    GitHubDocumentStatus Status,
    string? TagName,
    string? HtmlUrl,
    bool Draft = false,
    bool Prerelease = false)
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

            return ParseObject(document.RootElement);
        }
        catch (JsonException)
        {
            return new GitHubReleaseDocument(GitHubDocumentStatus.Malformed, null, null);
        }
    }

    public static bool TryParseList(string? json, out IReadOnlyList<PublishedRelease> releases)
    {
        releases = [];
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var parsed = new List<PublishedRelease>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var release = ParseObject(item);
                parsed.Add(new PublishedRelease(release.TagName, release.HtmlUrl, release.Draft, release.Prerelease));
            }

            releases = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static GitHubReleaseDocument ParseObject(JsonElement element)
    {
        if (!element.TryGetProperty("tag_name", out var tagProperty)
            || tagProperty.ValueKind != JsonValueKind.String)
            return new GitHubReleaseDocument(GitHubDocumentStatus.MissingTag, null, null);

        var tag = tagProperty.GetString();
        if (string.IsNullOrWhiteSpace(tag))
            return new GitHubReleaseDocument(GitHubDocumentStatus.MissingTag, null, null);

        string? htmlUrl = null;
        if (element.TryGetProperty("html_url", out var urlProperty)
            && urlProperty.ValueKind == JsonValueKind.String)
            htmlUrl = urlProperty.GetString();

        return new GitHubReleaseDocument(
            GitHubDocumentStatus.Parsed,
            tag,
            htmlUrl,
            Flag(element, "draft"),
            Flag(element, "prerelease"));
    }

    private static bool Flag(JsonElement element, string name)
        => element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.True;
}
