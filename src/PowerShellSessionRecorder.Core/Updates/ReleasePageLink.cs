namespace PowerShellSessionRecorder.Core.Updates;

/// <summary>
/// Accepts only HTTPS pages for this repository's GitHub releases.
/// </summary>
public static class ReleasePageLink
{
    public const string Owner = "TimeWizard007";

    public const string Repository = "powershell-session-recorder";

    public static bool IsTrusted(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.Port is not (-1 or 443))
            return false;

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        var path = uri.AbsolutePath.TrimEnd('/');
        var prefix = $"/{Owner}/{Repository}/releases/tag/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var tag = path[prefix.Length..];
        if (tag.Length == 0 || tag.Contains('/', StringComparison.Ordinal))
            return false;

        foreach (var character in tag)
        {
            if (character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-' or '~' or '%' or '+')
                continue;

            return false;
        }

        return true;
    }

    public static bool TryCreateTagPage(string? tag, out string url)
    {
        url = "";
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var trimmed = tag.Trim();
        if (trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains('?', StringComparison.Ordinal)
            || trimmed.Contains('#', StringComparison.Ordinal)
            || trimmed.Any(char.IsWhiteSpace))
            return false;

        url = $"https://github.com/{Owner}/{Repository}/releases/tag/{Uri.EscapeDataString(trimmed)}";
        return IsTrusted(url);
    }

    public static string? SelectTrustedPage(string? htmlUrl, string? tag)
    {
        if (IsTrusted(htmlUrl))
            return htmlUrl;

        return TryCreateTagPage(tag, out var created) ? created : null;
    }
}
