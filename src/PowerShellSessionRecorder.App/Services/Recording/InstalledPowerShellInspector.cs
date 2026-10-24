using System.Diagnostics;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services.Recording;

public interface IInstalledPowerShellInspector
{
    IReadOnlyList<PowerShellInstallInspection> Inspect();
}

/// <summary>
/// Reads installed PowerShell product versions by executing the discovered binaries.
/// The command text is fixed and contains no customer or session data.
/// </summary>
public sealed class InstalledPowerShellInspector : IInstalledPowerShellInspector
{
    public static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(5);

    public IReadOnlyList<PowerShellInstallInspection> Inspect()
    {
        var installs = Discover();
        var results = new List<PowerShellInstallInspection>(installs.Count);
        var powerShell7Selected = false;
        foreach (var install in installs)
        {
            var selected = install.Family == PowerShellVersion.PowerShell7 && !powerShell7Selected;
            if (selected)
                powerShell7Selected = true;

            results.Add(install with
            {
                ProductVersion = ReadVersion(install.Path),
                UsedForRecording = selected || install.Family == PowerShellVersion.WindowsPowerShell51
            });
        }

        return results;
    }

    private static IReadOnlyList<PowerShellInstallInspection> Discover()
    {
        if (OperatingSystem.IsWindows())
        {
            var found = new List<PowerShellInstallInspection>();
            foreach (var path in PowerShellEngineDiscovery.FindAllPowerShell7(
                         Environment.GetEnvironmentVariable("PATH"),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.CurrentDirectory,
                         WindowsPrivateMetadata.Inspect))
            {
                found.Add(new PowerShellInstallInspection(PowerShellVersion.PowerShell7, path, null, false));
            }

            var windowsPowerShell = PowerShellEngineDiscovery.FindWindowsPowerShell51(
                Environment.GetEnvironmentVariable("SystemRoot") ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.CurrentDirectory,
                WindowsPrivateMetadata.Inspect);
            if (windowsPowerShell is not null)
                found.Add(new PowerShellInstallInspection(PowerShellVersion.WindowsPowerShell51, windowsPowerShell, null, false));

            return found;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacPowerShellDiscovery.Discover(
                    Environment.GetEnvironmentVariable("PATH"),
                    Environment.CurrentDirectory,
                    MacPowerShellDiscovery.InspectFile,
                    MacPowerShellDiscovery.ReadLink)
                .Select(install => new PowerShellInstallInspection(
                    PowerShellVersion.PowerShell7,
                    install.DisplayPath,
                    null,
                    false,
                    string.Equals(install.DisplayPath, install.ExecutablePath, StringComparison.Ordinal)
                        ? null
                        : install.ExecutablePath))
                .ToArray();
        }

        if (!OperatingSystem.IsLinux())
            return [];

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        var selected = ExecutableLocator.Find(RecordingPlatform.PowerShellExecutable, pathVariable, File.Exists);
        var linux = new List<PowerShellInstallInspection>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            linux.Add(new PowerShellInstallInspection(PowerShellVersion.PowerShell7, selected, null, false));
            seen.Add(Path.GetFullPath(selected));
        }

        if (!string.IsNullOrWhiteSpace(pathVariable))
        {
            foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Path.IsPathRooted(directory))
                    continue;

                var candidate = Path.Combine(directory, RecordingPlatform.PowerShellExecutable);
                if (!File.Exists(candidate))
                    continue;

                var fullPath = Path.GetFullPath(candidate);
                if (!seen.Add(fullPath))
                    continue;

                linux.Add(new PowerShellInstallInspection(PowerShellVersion.PowerShell7, fullPath, null, false));
            }
        }

        return linux;
    }

    private static string? ReadVersion(string executable)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in PowerShellVersionCommand.Arguments)
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start);
            if (process is null)
                return null;

            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(VersionTimeout))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            _ = error.GetAwaiter().GetResult();
            var text = output.GetAwaiter().GetResult();
            return PowerShellProductVersion.TryParse(text, out var version) ? version.Text : null;
        }
        catch (Exception exception) when (exception is IOException
                     or UnauthorizedAccessException
                     or InvalidOperationException
                     or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
