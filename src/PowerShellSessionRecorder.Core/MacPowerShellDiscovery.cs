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
                if (!Path.IsPathRooted(directory))
                    continue;

                Consider(Path.Combine(directory, ExecutableName), currentDirectory, inspect, readLink, found, seen);
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
        if (!Path.IsPathRooted(candidate) || ContainsControl(candidate))
            return false;

        string current;
        try
        {
            current = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

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
                if (!string.Equals(Path.GetFileName(current), ExecutableName, StringComparison.Ordinal))
                    return false;

                executablePath = current;
                return true;
            }

            var link = readLink(current);
            if (string.IsNullOrWhiteSpace(link) || ContainsControl(link))
                return false;

            try
            {
                current = Path.IsPathRooted(link)
                    ? Path.GetFullPath(link)
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(current) ?? "", link));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool InsideCurrentDirectory(string path, string? currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(currentDirectory))
            return false;

        try
        {
            var current = Path.GetFullPath(currentDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directory = (Path.GetDirectoryName(path) ?? "")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(directory, current, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool ContainsControl(string value)
        => value.Contains('\0') || value.Contains('\r') || value.Contains('\n');
}
