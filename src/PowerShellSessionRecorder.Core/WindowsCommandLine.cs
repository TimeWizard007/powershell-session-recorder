using System.Text;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Quotes process arguments with the Windows CommandLineToArgvW rules.
/// The result is one argument vector, not a shell command.
/// </summary>
public static class WindowsCommandLine
{
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Contains('\0'))
            throw new ArgumentException("An argument cannot contain a null character.", nameof(argument));

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (slashes * 2) + 1);
                builder.Append('"');
                slashes = 0;
                continue;
            }

            if (slashes > 0)
            {
                builder.Append('\\', slashes);
                slashes = 0;
            }

            builder.Append(character);
        }

        if (slashes > 0)
            builder.Append('\\', slashes * 2);

        builder.Append('"');
        return builder.ToString();
    }

    public static string Join(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Join(' ', arguments.Select(Quote));
    }
}
