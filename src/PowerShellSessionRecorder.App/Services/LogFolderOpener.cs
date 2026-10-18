using System.Diagnostics;

namespace PowerShellSessionRecorder.App.Services;

public enum LogFolderOpenResult
{
    Opened,
    EmptyPath,
    Missing,
    Failed
}

public static class LogFolderOpener
{
    public static LogFolderOpenResult TryOpen(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return LogFolderOpenResult.EmptyPath;

        if (!Directory.Exists(path))
            return LogFolderOpenResult.Missing;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = OperatingSystem.IsWindows()
            };

            if (OperatingSystem.IsWindows())
                startInfo.FileName = "explorer.exe";
            else if (OperatingSystem.IsMacOS())
                startInfo.FileName = "open";
            else
                startInfo.FileName = "xdg-open";

            startInfo.ArgumentList.Add(path);
            return Process.Start(startInfo) is null
                ? LogFolderOpenResult.Failed
                : LogFolderOpenResult.Opened;
        }
        catch (Exception)
        {
            return LogFolderOpenResult.Failed;
        }
    }
}
