namespace PowerShellSessionRecorder.Core;

public sealed record MacPowerShellInstall(string DisplayPath, string ExecutablePath);

/// <summary>
/// Finds PowerShell 7 on macOS. Homebrew and the official installer publish a symlink,
/// so the launch path is the resolved regular file named pwsh. Relative PATH entries
/// and the current directory are ignored.
/// </summary>
public static class MacPowerShellDiscovery
{
    public const string ExecutableName = "pwsh";

    public static readonly string[] PreferredPaths =
    [
        "/usr/local/microsoft/powershell/7/pwsh",
        "/opt/homebrew/bin/pwsh",
        "/usr/local/bin/pwsh"
    ];

    public static IReadOnlyList<MacPowerShellInstall> Discover(
        string? pathVariable,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect,
        Func<string, string?> readLink)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        ArgumentNullException.ThrowIfNull(readLink);

        var found = new List<MacPowerShellInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preferred in PreferredPaths)
            Consider(preferred, currentDirectory, inspect, readLink, found, seen);

        if (!string.IsNullOrWhiteSpace(pathVariable))
        {
            foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!MacPath.IsRooted(directory))
                    continue;

                Consider(MacPath.Combine(directory, ExecutableName), currentDirectory, inspect, readLink, found, seen);
            }
        }

        return found;
    }

    public static EngineFileInspection InspectFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return new EngineFileInspection(true, (attributes & FileAttributes.ReparsePoint) != 0);
        }
        catch (Exception exception) when (exception is FileNotFoundException
                     or DirectoryNotFoundException
                     or IOException
                     or UnauthorizedAccessException
                     or ArgumentException
                     or NotSupportedException)
        {
            return new EngineFileInspection(false, false);
        }
    }

    public static string? ReadLink(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
        }
        catch (Exception exception) when (exception is FileNotFoundException
                     or DirectoryNotFoundException
                     or IOException
                     or UnauthorizedAccessException
                     or ArgumentException
                     or NotSupportedException)
        {
            return null;
        }
    }

    private static void Consider(
        string candidate,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect,
        Func<string, string?> readLink,
        List<MacPowerShellInstall> found,
        HashSet<string> seen)
    {
        if (!TryResolve(candidate, currentDirectory, inspect, readLink, out var display, out var executable))
            return;

        if (!seen.Add(executable))
            return;

        found.Add(new MacPowerShellInstall(display, executable));
    }

    private static bool TryResolve(
        string candidate,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect,
        Func<string, string?> readLink,
        out string displayPath,
        out string executablePath)
    {
        displayPath = "";
        executablePath = "";
        if (!MacPath.TryNormalize(candidate, out var current))
            return false;

        if (InsideCurrentDirectory(current, currentDirectory))
            return false;

        displayPath = current;
        for (var hop = 0; hop < 8; hop++)
        {
            var inspection = inspect(current);
            if (!inspection.Exists)
                return false;

            if (!inspection.ReparsePoint)
            {
                if (!string.Equals(MacPath.FileName(current), ExecutableName, StringComparison.Ordinal))
                    return false;

                executablePath = current;
                return true;
            }

            var link = readLink(current);
            if (string.IsNullOrWhiteSpace(link) || !MacPath.TryNormalizeLink(current, link, out current))
                return false;
        }

        return false;
    }

    private static bool InsideCurrentDirectory(string path, string? currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(currentDirectory) || !MacPath.TryNormalize(currentDirectory, out var current))
            return false;

        var directory = MacPath.DirectoryName(path) ?? "";
        return string.Equals(directory, current, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// POSIX paths for macOS discovery. System.IO.Path rewrites a leading slash on Windows,
    /// which would hide the same installs the macOS runner finds. A backslash is a legal
    /// filename character and is not a separator.
    /// </summary>
    private static class MacPath
    {
        public static bool IsRooted(string path)
            => path.Length > 0 && path[0] == '/';

        public static string Combine(string directory, string name)
        {
            if (IsRooted(name))
                return name;

            if (directory.Length == 0)
                return name;

            return directory[^1] == '/' ? directory + name : directory + "/" + name;
        }

        public static string? DirectoryName(string path)
        {
            var slash = path.LastIndexOf('/');
            if (slash < 0)
                return null;

            return slash == 0 ? "/" : path[..slash];
        }

        public static string FileName(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path[(slash + 1)..];
        }

        public static bool TryNormalize(string path, out string normalized)
        {
            normalized = "";
            if (!IsRooted(path) || ContainsControl(path) || !TryCollapse(path, out normalized))
                return false;

            return true;
        }

        public static bool TryNormalizeLink(string current, string link, out string normalized)
        {
            normalized = "";
            if (ContainsControl(link))
                return false;

            var combined = IsRooted(link)
                ? link
                : Combine(DirectoryName(current) ?? "/", link);
            return TryNormalize(combined, out normalized);
        }

        private static bool TryCollapse(string path, out string normalized)
        {
            normalized = "";
            var parts = new List<string>();
            foreach (var part in path.Split('/', StringSplitOptions.None))
            {
                if (part.Length == 0 || part == ".")
                    continue;

                if (part == "..")
                {
                    if (parts.Count == 0)
                        continue;

                    parts.RemoveAt(parts.Count - 1);
                    continue;
                }

                parts.Add(part);
            }

            normalized = "/" + string.Join('/', parts);
            return normalized.Length > 1;
        }
    }

    private static bool ContainsControl(string value)
        => value.Contains('\0') || value.Contains('\r') || value.Contains('\n');
}
