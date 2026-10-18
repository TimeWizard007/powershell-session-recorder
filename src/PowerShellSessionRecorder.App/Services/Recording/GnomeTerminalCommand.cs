using System.Text;

namespace PowerShellSessionRecorder.App.Services.Recording;

/// <summary>
/// Argument builder for GNOME Terminal 3.52 on Ubuntu 24.04.
/// Other terminal emulators are not claimed to work.
/// </summary>
public static class GnomeTerminalCommand
{
    public const string ExecutableName = "gnome-terminal";

    public static IReadOnlyList<string> BuildArguments(string? title, IReadOnlyList<string> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0 || command.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("The terminal command is empty.", nameof(command));

        var arguments = new List<string>(command.Count + 3)
        {
            "--title",
            SanitizeTitle(title),
            "--"
        };
        arguments.AddRange(command);
        return arguments;
    }

    public static string SanitizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "PowerShell Session Recorder";

        var builder = new StringBuilder(title.Length);
        foreach (var character in title.Trim())
        {
            if (character < 32 || character == 127)
                continue;

            builder.Append(character);
            if (builder.Length >= 120)
                break;
        }

        var value = builder.ToString().Trim();
        if (value.Length == 0)
            return "PowerShell Session Recorder";

        if (value.StartsWith('-'))
            value = "PowerShell Session Recorder " + value;

        return value;
    }
}
