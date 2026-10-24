using System.Buffers.Binary;
using System.Diagnostics;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint62Tests
{
    [Fact]
    public void AppIcon_IsAnIcnsWithTheStandardPngSizes()
    {
        var data = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "assets/branding/macos/AppIcon.icns"));
        Assert.Equal("icns"u8.ToArray(), data.Take(4).ToArray());
        var total = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
        Assert.Equal(data.Length, total);

        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        var offset = 8;
        while (offset + 8 <= total)
        {
            var kind = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 4, 4));
            Assert.True(length >= 8 && offset + length <= total);
            var png = data.AsSpan(offset + 8, length - 8);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4].ToArray());
            sizes[kind] = BinaryPrimitives.ReadInt32BigEndian(png[16..20]);
            offset += length;
        }

        Assert.Equal(total, offset);
        Assert.Equal(16, sizes["icp4"]);
        Assert.Equal(32, sizes["icp5"]);
        Assert.Equal(128, sizes["ic07"]);
        Assert.Equal(256, sizes["ic08"]);
        Assert.Equal(512, sizes["ic09"]);
        Assert.Equal(1024, sizes["ic10"]);
        Assert.Equal(512, sizes["ic14"]);
    }

    [Fact]
    public void MacPackageScript_PublishesBothArchitecturesWithoutWeakeningGatekeeper()
    {
        var root = RepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "packaging/macos/build-dmg.sh"));
        var notice = File.ReadAllText(Path.Combine(root, "packaging/macos/GATEKEEPER.txt"));
        var release = File.ReadAllText(Path.Combine(root, ".github/workflows/release.yml"));

        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("-r \"$rid\"", script, StringComparison.Ordinal);
        Assert.Contains("osx-x64", script, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil create", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil verify", script, StringComparison.Ordinal);
        Assert.Contains("validate-bundle.py", script, StringComparison.Ordinal);
        Assert.Contains("checksum.py", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", script, StringComparison.Ordinal);
        Assert.DoesNotContain("spctl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("osascript", script, StringComparison.Ordinal);
        Assert.DoesNotContain("codesign", script, StringComparison.Ordinal);
        Assert.Contains("Do not run \"sudo spctl --master-disable\"", notice, StringComparison.Ordinal);
        Assert.Contains("nie została poświadczona", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("hdiutil", release, StringComparison.Ordinal);
        Assert.DoesNotContain("build-dmg.sh", release, StringComparison.Ordinal);

        if (!OperatingSystem.IsWindows())
        {
            var syntax = Run(root, "sh", "-n", "packaging/macos/build-dmg.sh");
            Assert.Equal(0, syntax.ExitCode);
        }
    }

    [Fact]
    public void BundleValidator_AcceptsAThinExecutableAndRejectsUserData()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = RepositoryRoot();
        using var directory = new TempDirectory();
        var app = Path.Combine(directory.Path, "PowerShell Session Recorder.app");
        var macos = Path.Combine(app, "Contents", "MacOS");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));
        Directory.CreateDirectory(Path.Combine(macos, "Localization"));
        File.Copy(
            Path.Combine(root, "assets/branding/macos/AppIcon.icns"),
            Path.Combine(app, "Contents", "Resources", "AppIcon.icns"));
        var executable = Path.Combine(macos, "PowerShellSessionRecorder");
        File.WriteAllBytes(executable, Thin(0x01000007));
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        foreach (var library in new[] { "libAvaloniaNative.dylib", "libSkiaSharp.dylib", "libHarfBuzzSharp.dylib" })
            File.WriteAllBytes(Path.Combine(macos, library), Universal());
        File.WriteAllText(Path.Combine(macos, "Localization", "en-US.json"), "{}");
        File.WriteAllText(Path.Combine(macos, "Localization", "pl-PL.json"), "{}");
        var plist = Run(
            root,
            "python3",
            "packaging/macos/write-plist.py",
            Path.Combine(app, "Contents", "Info.plist"),
            "0.1.0-beta.2",
            "0.1.0",
            "Copyright (c) 2026 TimeWizard");
        Assert.Equal(0, plist.ExitCode);

        var accepted = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-x64", "0.1.0-beta.2");
        Assert.Equal(0, accepted.ExitCode);

        var wrongArchitecture = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-arm64", "0.1.0-beta.2");
        Assert.NotEqual(0, wrongArchitecture.ExitCode);

        File.WriteAllText(Path.Combine(macos, "settings.json"), "{}");
        var userData = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-x64", "0.1.0-beta.2");
        Assert.NotEqual(0, userData.ExitCode);
        Assert.Contains("per-user data", userData.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxDiskImage_PreservesTheExecutableAndNamesTheChecksum()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/usr/bin/xorriso"))
            return;

        var root = RepositoryRoot();
        using var directory = new TempDirectory();
        var stage = Path.Combine(directory.Path, "stage");
        var executable = Path.Combine(stage, "PowerShell Session Recorder.app", "Contents", "MacOS", "PowerShellSessionRecorder");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateSymbolicLink(Path.Combine(stage, "Applications"), "/Applications");
        var image = Path.Combine(directory.Path, "PowerShellSessionRecorder-0.1.0-beta.2-osx-x64.dmg");

        var written = Run(root, "python3", "packaging/macos/write-dmg.py", stage, image);
        Assert.Equal(0, written.ExitCode);
        var checkedImage = Run(root, "python3", "packaging/macos/write-dmg.py", "--check", image);
        Assert.Equal(0, checkedImage.ExitCode);
        var checksum = Run(root, "python3", "packaging/macos/checksum.py", image);
        Assert.Equal(0, checksum.ExitCode);
        var sum = File.ReadAllText(image + ".sha256");
        Assert.Contains("  PowerShellSessionRecorder-0.1.0-beta.2-osx-x64.dmg\n", sum, StringComparison.Ordinal);

        var extracted = Path.Combine(directory.Path, "extracted");
        Directory.CreateDirectory(extracted);
        var iso = Path.Combine(directory.Path, "volume.iso");
        var dmg = File.ReadAllBytes(image);
        var dataLength = BinaryPrimitives.ReadInt64BigEndian(dmg.AsSpan(dmg.Length - 512 + 32, 8));
        File.WriteAllBytes(iso, dmg.AsSpan(0, (int)dataLength));
        var restore = Run(root, "xorriso", "-osirrox", "on", "-indev", iso, "-extract", "/", extracted);
        Assert.Equal(0, restore.ExitCode);
        var restored = Path.Combine(extracted, "PowerShell Session Recorder.app", "Contents", "MacOS", "PowerShellSessionRecorder");
        Assert.True(File.GetUnixFileMode(restored).HasFlag(UnixFileMode.UserExecute));
        Assert.Equal("/Applications", new FileInfo(Path.Combine(extracted, "Applications")).LinkTarget);
    }

    private static byte[] Thin(uint cpu)
    {
        var bytes = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), cpu);
        return bytes;
    }

    private static byte[] Universal()
    {
        var bytes = new byte[48];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 0xCAFEBABE);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 0x01000007);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(28), 0x0100000C);
        return bytes;
    }

    private static (int ExitCode, string Text) Run(string root, string file, params string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = file,
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
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

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
