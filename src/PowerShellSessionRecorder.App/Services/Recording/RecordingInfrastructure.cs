using System.Diagnostics;
using System.Text;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public interface IExecutableProbe
{
    string? FindExecutable(string name);

    void Invalidate();
}

public interface IProcessRunner
{
    Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

public interface ITranscriptWatcher
{
    Task<bool> WaitForHeaderAsync(string transcriptPath, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class SystemExecutableProbe : IExecutableProbe
{
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);
    private readonly Func<string, bool> _isExecutable;

    public SystemExecutableProbe()
        : this(IsExecutableFile)
    {
    }

    internal SystemExecutableProbe(Func<string, bool> isExecutable)
    {
        _isExecutable = isExecutable;
    }

    public string? FindExecutable(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var found = ExecutableLocator.Find(name, Environment.GetEnvironmentVariable("PATH"), _isExecutable);
        _cache[name] = found;
        return found;
    }

    public void Invalidate() => _cache.Clear();

    private static bool IsExecutableFile(string path)
    {
        if (!File.Exists(path))
            return false;

        if (OperatingSystem.IsWindows())
            return true;

        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            return -1;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }

            return -1;
        }
    }
}

public sealed class FileTranscriptWatcher : ITranscriptWatcher
{
    public async Task<bool> WaitForHeaderAsync(string transcriptPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (HasHeader(transcriptPath))
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static bool HasHeader(string transcriptPath)
    {
        try
        {
            if (!File.Exists(transcriptPath))
                return false;

            using var stream = new FileStream(transcriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return RecordingBootstrap.ContainsHeader(reader.ReadToEnd());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed class BootstrapFileStore
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Encoding _encoding;

    public BootstrapFileStore(string directory, bool? emitUtf8Bom = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        DirectoryPath = directory;
        _encoding = CreateEncoding(emitUtf8Bom ?? OperatingSystem.IsWindows());
    }

    public string DirectoryPath { get; }

    public static Encoding CreateEncoding(bool emitUtf8Bom)
        => emitUtf8Bom ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) : Utf8WithoutBom;

    public string WriteScript()
    {
        Directory.CreateDirectory(DirectoryPath);
        TryRestrictDirectory(DirectoryPath);
        DeleteExpiredScripts();

        var path = Path.Combine(DirectoryPath, "session-" + Guid.NewGuid().ToString("N") + ".ps1");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, RecordingBootstrap.Script, _encoding);
        TryRestrictFile(temporary);
        File.Move(temporary, path, overwrite: true);
        return path;
    }

    private void DeleteExpiredScripts()
    {
        if (!Directory.Exists(DirectoryPath))
            return;

        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.ps1"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromHours(24))
                    File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static void TryRestrictDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    private static void TryRestrictFile(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }
}

public static class BootstrapPaths
{
    public static string GetDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local))
                return Path.Combine(local, "PowerShellSessionRecorder", "bootstrap");
        }

        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtime))
            return Path.Combine(runtime.Trim(), "powershell-session-recorder");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "powershell-session-recorder", "bootstrap");
    }
}
