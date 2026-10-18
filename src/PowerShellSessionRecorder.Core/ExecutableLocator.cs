namespace PowerShellSessionRecorder.Core;

public static class ExecutableLocator
{
    public static string? Find(string executableName, string? pathVariable, Func<string, bool> isExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentNullException.ThrowIfNull(isExecutable);

        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (directory.Length == 0)
                continue;

            var candidate = Path.Combine(directory, executableName);
            if (isExecutable(candidate))
                return candidate;
        }

        return null;
    }
}
