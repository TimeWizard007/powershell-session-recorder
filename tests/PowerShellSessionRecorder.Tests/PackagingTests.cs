using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class PackagingTests
{
    [Theory]
    [InlineData("0.1.0-checkpoint.4", "0.1.0~checkpoint.4-1")]
    [InlineData("0.1.0-checkpoint.4.1", "0.1.0~checkpoint.4.1-1")]
    [InlineData("0.1.0-beta.1", "0.1.0~beta.1-1")]
    [InlineData("0.1.0-checkpoint.4+local", "0.1.0~checkpoint.4-1")]
    [InlineData("1.0.0", "1.0.0-1")]
    [InlineData("1.2.3-rc.1", "1.2.3~rc.1-1")]
    public void DebianVersion_SortsAPrereleaseBeforeTheStablePackage(string informational, string debian)
        => Assert.Equal(debian, DebianPackageVersion.FromInformational(informational));

    [Fact]
    public void ApplicationInformationalVersion_MatchesTheDebianPackageVersion()
    {
        var informational = XDocument.Load(Path.Combine(RepositoryRoot(), "src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"))
            .Descendants("InformationalVersion")
            .Single()
            .Value;

        Assert.Equal("1.0.0", informational);
        Assert.Equal("1.0.0-1", DebianPackageVersion.FromInformational(informational));
        var assemblyVersion = typeof(ProductVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+', 2)[0];
        Assert.Equal(informational, assemblyVersion);
        Assert.Equal(informational, ProductVersion.Current);
    }

    [Fact]
    public void DebianVersionScript_MatchesTheMapper()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var script = Path.Combine(RepositoryRoot(), "packaging/ubuntu/debian-version.sh");
        foreach (var sample in new[] { "0.1.0-checkpoint.4", "0.1.0-checkpoint.4.1", "0.1.0-beta.1", "0.1.0-checkpoint.4+sha", "1.0.0", "1.2.3-rc.1" })
        {
            var start = new ProcessStartInfo
            {
                FileName = "sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(". \"$1\" && debian_version_from_informational \"$2\"");
            start.ArgumentList.Add("sh");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(sample);
            using var process = Process.Start(start);
            Assert.NotNull(process);
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(DebianPackageVersion.FromInformational(sample), output);
        }
    }

    [Fact]
    public void DesktopEntry_LaunchesWithoutATerminalOrShell()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "packaging/ubuntu/powershell-session-recorder.desktop"));
        var values = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#') && line.Contains('=', StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        Assert.Equal("Application", values["Type"]);
        Assert.Equal("PowerShell Session Recorder", values["Name"]);
        Assert.Equal("Rejestrator sesji PowerShell", values["Name[pl]"]);
        Assert.Equal("/usr/bin/powershell-session-recorder", values["Exec"]);
        Assert.Equal("powershell-session-recorder", values["Icon"]);
        Assert.Equal("false", values["Terminal"]);
        Assert.Equal("Utility;", values["Categories"]);
        Assert.Equal("PowerShellSessionRecorder", values["StartupWMClass"]);
        Assert.DoesNotContain("sudo", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sh -c", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageScript_DoesNotInstallPowerShellOrUseSudo()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "packaging/ubuntu/build-deb.sh"));

        Assert.Contains("--self-contained true", text, StringComparison.Ordinal);
        Assert.Contains("-r linux-x64", text, StringComparison.Ordinal);
        Assert.Contains("Suggests: gnome-terminal", text, StringComparison.Ordinal);
        Assert.Contains("libssl3t64", text, StringComparison.Ordinal);
        Assert.DoesNotContain("libssl3,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("apt-get", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", text.Split('\n').First(line => line.Contains("Depends:", StringComparison.Ordinal)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usr/share/icons/hicolor", text, StringComparison.Ordinal);
        Assert.Contains("sha256sum", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(22)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    public void HicolorIcons_UseTheExpectedSize(int size)
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "packaging/ubuntu/icons/hicolor",
            $"{size}x{size}",
            "apps",
            "powershell-session-recorder.png");

        Assert.Equal(size, PngWidth(path));
        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot(),
            "packaging/ubuntu/icons/hicolor/scalable/apps/powershell-session-recorder.svg")));
    }

    private static int PngWidth(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[24];
        Assert.Equal(24, stream.Read(header));
        return BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PowerShellSessionRecorder.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
