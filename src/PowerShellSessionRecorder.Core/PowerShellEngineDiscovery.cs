namespace PowerShellSessionRecorder.Core;

public sealed record PowerShellEngineLocation(PowerShellVersion Version, string Path);

public readonly record struct EngineFileInspection(bool Exists, bool ReparsePoint);

/// <summary>
/// Finds PowerShell engines in trusted Windows locations and absolute PATH entries.
/// The current directory and relative PATH entries are ignored.
/// </summary>
public static class PowerShellEngineDiscovery
{
    public const string PowerShell7FileName = "pwsh.exe";

    public const string WindowsPowerShellFileName = "powershell.exe";

    public static IReadOnlyList<PowerShellEngineLocation> DiscoverWindows(
        string? pathVariable,
        string? programFiles,
        string? systemRoot,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect)
    {
        ArgumentNullException.ThrowIfNull(inspect);

        var found = new List<PowerShellEngineLocation>(2);
        var powerShell7 = FindPowerShell7(pathVariable, programFiles, currentDirectory, inspect);
        if (powerShell7 is not null)
            found.Add(new PowerShellEngineLocation(PowerShellVersion.PowerShell7, powerShell7));

        var windowsPowerShell = FindWindowsPowerShell51(systemRoot, currentDirectory, inspect);
        if (windowsPowerShell is not null)
            found.Add(new PowerShellEngineLocation(PowerShellVersion.WindowsPowerShell51, windowsPowerShell));

        return found;
    }

    public static string? FindPowerShell7(
        string? pathVariable,
        string? programFiles,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect)
    {
        if (!string.IsNullOrWhiteSpace(programFiles) && Path.IsPathRooted(programFiles))
        {
            var installed = Path.Combine(programFiles.Trim(), "PowerShell", "7", PowerShell7FileName);
            if (Accept(installed, currentDirectory, inspect))
                return Path.GetFullPath(installed);
        }

        return FirstOnPath(pathVariable, PowerShell7FileName, currentDirectory, inspect);
    }

    public static string? FindWindowsPowerShell51(
        string? systemRoot,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect)
    {
        if (string.IsNullOrWhiteSpace(systemRoot) || !Path.IsPathRooted(systemRoot))
            return null;

        var installed = Path.Combine(
            systemRoot.Trim(),
            "System32",
            "WindowsPowerShell",
            "v1.0",
            WindowsPowerShellFileName);
        return Accept(installed, currentDirectory, inspect) ? Path.GetFullPath(installed) : null;
    }

    public static bool IsEngineFileName(string? path, PowerShellVersion version)
    {
        var name = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);
        return version switch
        {
            PowerShellVersion.PowerShell7 => name.Equals(PowerShell7FileName, StringComparison.OrdinalIgnoreCase),
            PowerShellVersion.WindowsPowerShell51 => name.Equals(WindowsPowerShellFileName, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string? FirstOnPath(
        string? pathVariable,
        string fileName,
        string? currentDirectory,
        Func<string, EngineFileInspection> inspect)
    {
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Path.IsPathRooted(directory))
                continue;

            var candidate = Path.Combine(directory, fileName);
            if (Accept(candidate, currentDirectory, inspect))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    private static bool Accept(string candidate, string? currentDirectory, Func<string, EngineFileInspection> inspect)
    {
        if (!Path.IsPathRooted(candidate))
            return false;

        string fullPath;
        string fullDirectory;
        try
        {
            fullPath = Path.GetFullPath(candidate);
            fullDirectory = Path.GetDirectoryName(fullPath) ?? "";
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (SameDirectory(fullDirectory, currentDirectory))
            return false;

        var inspection = inspect(fullPath);
        return inspection.Exists && !inspection.ReparsePoint;
    }

    private static bool SameDirectory(string directory, string? currentDirectory)
    {
        if (string.IsNullOrWhiteSpace(currentDirectory))
            return false;

        try
        {
            var current = Path.GetFullPath(currentDirectory);
            return string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
