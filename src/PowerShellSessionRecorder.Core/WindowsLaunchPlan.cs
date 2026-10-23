namespace PowerShellSessionRecorder.Core;

public sealed record WindowsLaunchPlan(
    string ExecutablePath,
    PowerShellVersion Version,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    bool Elevate,
    string QuotedArguments);

public static class WindowsLaunchPlanFactory
{
    public static bool TryCreate(
        PowerShellVersion version,
        string? executablePath,
        string? workingDirectory,
        string? bootstrapPath,
        string? transcriptPath,
        string? language,
        string? windowTitle,
        bool elevate,
        bool executableIsReparsePoint,
        out WindowsLaunchPlan? plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(executablePath)
            || string.IsNullOrWhiteSpace(workingDirectory)
            || string.IsNullOrWhiteSpace(bootstrapPath)
            || string.IsNullOrWhiteSpace(transcriptPath)
            || string.IsNullOrWhiteSpace(windowTitle)
            || executableIsReparsePoint
            || !Path.IsPathRooted(executablePath)
            || !PowerShellEngineDiscovery.IsEngineFileName(executablePath, version))
            return false;

        try
        {
            var executable = Path.GetFullPath(executablePath);
            if (!string.Equals(executable, executablePath, StringComparison.OrdinalIgnoreCase))
                return false;

            var arguments = RecordingBootstrap.BuildWindowsArguments(
                version,
                workingDirectory,
                bootstrapPath,
                transcriptPath,
                language,
                windowTitle);
            if (arguments.Any(argument => argument.Contains('\0') || argument.Contains('\r') || argument.Contains('\n')))
                return false;

            plan = new WindowsLaunchPlan(
                executable,
                version,
                arguments,
                workingDirectory,
                elevate,
                WindowsCommandLine.Join(arguments));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
