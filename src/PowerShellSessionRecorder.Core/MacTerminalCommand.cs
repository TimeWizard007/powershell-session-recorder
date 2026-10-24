using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Builds a Terminal.app launch that does not pass the session through a shell string.
/// /usr/bin/open receives the application name and a command file as separate arguments.
/// The command file is a fixed exec script whose paths are single-quoted. AppleScript
/// is not used, because do script would concatenate a shell command.
/// </summary>
public static class MacTerminalCommand
{
    public const string OpenExecutable = "/usr/bin/open";

    public const string TerminalApplicationName = "Terminal";

    public const string TerminalBundlePath = "/System/Applications/Utilities/Terminal.app";

    public static IReadOnlyList<string> OpenArguments(string scriptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        return [ "-a", TerminalApplicationName, scriptPath ];
    }

    public static bool TryBuildScript(
        string executablePath,
        IReadOnlyList<string> arguments,
        string scriptPath,
        out string script)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        script = "";
        if (!AcceptablePath(executablePath)
            || !AcceptablePath(scriptPath)
            || !string.Equals(Path.GetFileName(executablePath), MacPowerShellDiscovery.ExecutableName, StringComparison.Ordinal)
            || arguments.Any(argument => !AcceptableArgument(argument)))
            return false;

        var builder = new StringBuilder();
        builder.Append("#!/bin/sh\n");
        builder.Append("rm -f -- ").Append(Quote(scriptPath)).Append('\n');
        builder.Append("exec ").Append(Quote(executablePath));
        foreach (var argument in arguments)
            builder.Append(' ').Append(Quote(argument));

        builder.Append('\n');
        script = builder.ToString();
        return true;
    }

    public static string Quote(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static bool AcceptablePath(string? value)
        => AcceptableArgument(value) && Path.IsPathRooted(value);

    private static bool AcceptableArgument(string? value)
        => !string.IsNullOrEmpty(value)
            && !value.Contains('\0')
            && !value.Contains('\r')
            && !value.Contains('\n');
}
