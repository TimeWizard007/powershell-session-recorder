namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Maps an application informational version to a Debian package version.
/// A prerelease such as 0.1.0-checkpoint.4 becomes 0.1.0~checkpoint.4-1 so it
/// sorts before the stable package 0.1.0-1. Build metadata after '+' is omitted.
/// </summary>
public static class DebianPackageVersion
{
    public static string FromInformational(string informationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(informationalVersion);
        var version = informationalVersion.Trim();
        var metadata = version.IndexOf('+', StringComparison.Ordinal);
        if (metadata >= 0)
            version = version[..metadata];

        if (version.Length == 0 || version.Contains('/', StringComparison.Ordinal) || version.Contains(' ', StringComparison.Ordinal))
            throw new ArgumentException("Informational version cannot be mapped to a Debian version.", nameof(informationalVersion));

        var separator = version.IndexOf('-', StringComparison.Ordinal);
        if (separator < 0)
            return version + "-1";

        var upstream = version[..separator];
        var prerelease = version[(separator + 1)..];
        if (upstream.Length == 0 || prerelease.Length == 0 || prerelease.Contains('-', StringComparison.Ordinal))
            throw new ArgumentException("Informational version cannot be mapped to a Debian version.", nameof(informationalVersion));

        return upstream + "~" + prerelease + "-1";
    }
}
