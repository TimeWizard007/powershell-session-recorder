using System.Buffers.Binary;
using System.Diagnostics;
using PowerShellSessionRecorder.App;

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
        var signer = File.ReadAllText(Path.Combine(root, "packaging/macos/sign-bundle.sh"));
        var notice = File.ReadAllText(Path.Combine(root, "packaging/macos/GATEKEEPER.txt"));
        var release = File.ReadAllText(Path.Combine(root, ".github/workflows/release.yml"));

        Assert.Contains("--self-contained true", script, StringComparison.Ordinal);
        Assert.Contains("-p:AppHostDotNetSearch=AppRelative", script, StringComparison.Ordinal);
        Assert.Contains("-p:AppHostRelativeDotNet=../MacOS", script, StringComparison.Ordinal);
        var project = File.ReadAllText(Path.Combine(root, "src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"));
        Assert.DoesNotContain("AppHostRelativeDotNet", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AppHostDotNetSearch", project, StringComparison.Ordinal);
        Assert.Contains("-r \"$rid\"", script, StringComparison.Ordinal);
        Assert.Contains("osx-x64", script, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil create", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil verify", script, StringComparison.Ordinal);
        Assert.Contains("scratch=\"$stage/$rid-rw.dmg\"", script, StringComparison.Ordinal);
        Assert.Contains("mount=\"$stage/mnt\"", script, StringComparison.Ordinal);
        Assert.Contains("-mountpoint \"$mount\"", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil convert", script, StringComparison.Ordinal);
        Assert.Contains("hdiutil detach", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-srcfolder", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sleep ", script, StringComparison.Ordinal);
        Assert.Contains("layout-bundle.py", script, StringComparison.Ordinal);
        Assert.Contains("validate-bundle.py", script, StringComparison.Ordinal);
        Assert.True(script.IndexOf("layout-bundle.py", StringComparison.Ordinal)
            < script.IndexOf("validate-bundle.py", StringComparison.Ordinal));
        Assert.Contains("checksum.py", script, StringComparison.Ordinal);
        Assert.Contains("sign-bundle.sh", script, StringComparison.Ordinal);
        Assert.Contains("if [ \"$rid\" = \"osx-arm64\" ]; then\n        sh \"$macos/smoke-bundle.sh\" \"$app\"", script, StringComparison.Ordinal);
        var smoke = File.ReadAllText(Path.Combine(root, "packaging/macos/smoke-bundle.sh"));
        Assert.Contains("--smoke", smoke, StringComparison.Ordinal);
        Assert.Contains("COREHOST_TRACE=1 COREHOST_TRACEFILE=\"$trace\"", smoke, StringComparison.Ordinal);
        Assert.Contains("SMOKE_OK ", smoke, StringComparison.Ordinal);
        Assert.Contains("Apphost could not locate the managed entry assembly.", smoke, StringComparison.Ordinal);
        Assert.Contains("Hostfxr or hostpolicy could not initialize.", smoke, StringComparison.Ordinal);
        Assert.Contains("Native dependency loading failed.", smoke, StringComparison.Ordinal);
        Assert.Contains("Avalonia backend initialization failed.", smoke, StringComparison.Ordinal);
        Assert.Contains("Packaging or signing failed:", smoke, StringComparison.Ordinal);
        Assert.Contains("libAvaloniaNative.dylib", smoke, StringComparison.Ordinal);
        Assert.Contains("platform=Avalonia.Native.AvaloniaNativePlatform", smoke, StringComparison.Ordinal);
        Assert.Contains("runtimeconfig=net10.0", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("xattr", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("spctl", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("smoke-bundle.sh", release, StringComparison.Ordinal);
        var zipAt = script.IndexOf("zip -r -X -y", StringComparison.Ordinal);
        var signAt = script.IndexOf("sign-bundle.sh\" \"$app\"", StringComparison.Ordinal);
        var smokeAt = script.IndexOf("smoke-bundle.sh\" \"$app\"", StringComparison.Ordinal);
        var verifyZipAt = script.IndexOf("verify_zipped_bundle \"$root/artifacts/$zip_name\"", StringComparison.Ordinal);
        var verifyDmgAt = script.IndexOf("verify_mounted_bundle \"$root/artifacts/$dmg_name\"", StringComparison.Ordinal);
        var imageAt = script.IndexOf("hdiutil create", StringComparison.Ordinal);
        Assert.True(signAt >= 0 && smokeAt > signAt && zipAt > smokeAt);
        Assert.True(verifyZipAt > zipAt);
        Assert.True(verifyDmgAt > imageAt);
        Assert.Contains("codesign --force --sign -", signer, StringComparison.Ordinal);
        Assert.Contains("--identifier \"$identifier\"", signer, StringComparison.Ordinal);
        Assert.Contains("com.timewizard.powershellsessionrecorder", signer, StringComparison.Ordinal);
        Assert.Contains("codesign --verify --deep --strict --verbose=2", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("--preserve-metadata", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("Developer ID Application", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("notarytool", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("xattr", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("--options runtime", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", script, StringComparison.Ordinal);
        Assert.DoesNotContain("spctl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("osascript", script, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", signer, StringComparison.Ordinal);
        Assert.DoesNotContain("spctl", signer, StringComparison.Ordinal);
        Assert.Contains("Do not run \"sudo spctl --master-disable\"", notice, StringComparison.Ordinal);
        Assert.Contains("nie została poświadczona", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("AppHostRelativeDotNet", release, StringComparison.Ordinal);
        Assert.Contains("sh packaging/macos/build-dmg.sh all", release, StringComparison.Ordinal);
        Assert.DoesNotContain("hdiutil", release, StringComparison.Ordinal);
        Assert.DoesNotContain("codesign", release, StringComparison.Ordinal);

        if (!OperatingSystem.IsWindows())
        {
            var syntax = Run(root, "sh", "-n", "packaging/macos/build-dmg.sh");
            Assert.Equal(0, syntax.ExitCode);
            var signerSyntax = Run(root, "sh", "-n", "packaging/macos/sign-bundle.sh");
            Assert.Equal(0, signerSyntax.ExitCode);
            var smokeSyntax = Run(root, "sh", "-n", "packaging/macos/smoke-bundle.sh");
            Assert.Equal(0, smokeSyntax.ExitCode);
            var smokeMissing = Run(root, "sh", "packaging/macos/smoke-bundle.sh");
            Assert.NotEqual(0, smokeMissing.ExitCode);
            var missing = Run(root, "sh", "packaging/macos/sign-bundle.sh");
            Assert.NotEqual(0, missing.ExitCode);
            var refused = Run(root, "sh", "packaging/macos/sign-bundle.sh", "--verify", Path.Combine(root, "packaging"));
            Assert.NotEqual(0, refused.ExitCode);
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
        var resources = Path.Combine(app, "Contents", "Resources");
        Directory.CreateDirectory(macos);
        Directory.CreateDirectory(resources);
        Directory.CreateDirectory(Path.Combine(resources, "Localization"));
        File.Copy(
            Path.Combine(root, "assets/branding/macos/AppIcon.icns"),
            Path.Combine(resources, "AppIcon.icns"));
        var executable = Path.Combine(macos, "PowerShellSessionRecorder");
        var host = Thin(0x01000007)
            .Concat(System.Text.Encoding.ASCII.GetBytes("../Resources/PowerShellSessionRecorder.dll\0"))
            .Concat(new byte[] { 0x02, 0x00 })
            .Concat(System.Text.Encoding.ASCII.GetBytes("../MacOS\0"))
            .ToArray();
        File.WriteAllBytes(executable, host);
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        foreach (var library in new[] { "libAvaloniaNative.dylib", "libSkiaSharp.dylib", "libHarfBuzzSharp.dylib" })
            File.WriteAllBytes(Path.Combine(macos, library), Universal());
        File.WriteAllBytes(Path.Combine(resources, "PowerShellSessionRecorder.dll"), [0x4D, 0x5A]);
        File.WriteAllText(Path.Combine(resources, "PowerShellSessionRecorder.deps.json"), "{}");
        File.WriteAllText(Path.Combine(resources, "PowerShellSessionRecorder.runtimeconfig.json"), "{}");
        File.WriteAllText(Path.Combine(resources, "Localization", "en-US.json"), "{}");
        File.WriteAllText(Path.Combine(resources, "Localization", "pl-PL.json"), "{}");
        var plist = Run(
            root,
            "python3",
            "packaging/macos/write-plist.py",
            Path.Combine(app, "Contents", "Info.plist"),
            "1.0.0",
            "1.0.0",
            "Copyright (c) 2026 TimeWizard");
        Assert.Equal(0, plist.ExitCode);

        var accepted = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-x64", "1.0.0");
        Assert.Equal(0, accepted.ExitCode);

        var wrongArchitecture = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-arm64", "1.0.0");
        Assert.NotEqual(0, wrongArchitecture.ExitCode);

        File.WriteAllText(Path.Combine(macos, "settings.json"), "{}");
        var userData = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-x64", "1.0.0");
        Assert.NotEqual(0, userData.ExitCode);
        Assert.Contains("per-user data", userData.Text, StringComparison.Ordinal);
        File.Delete(Path.Combine(macos, "settings.json"));
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllBytes(
            executable,
            Thin(0x01000007).Concat(System.Text.Encoding.ASCII.GetBytes("../Resources/PowerShellSessionRecorder.dll\0")).ToArray());
        var unboundRuntime = Run(root, "python3", "packaging/macos/validate-bundle.py", app, "osx-x64", "1.0.0");
        Assert.NotEqual(0, unboundRuntime.ExitCode);
        Assert.Contains("self-contained runtime in ../MacOS", unboundRuntime.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BundleLayout_MovesManagedFilesAndKeepsNativeLibraries()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = RepositoryRoot();
        using var directory = new TempDirectory();
        var app = Path.Combine(directory.Path, "PowerShell Session Recorder.app");
        var macos = Path.Combine(app, "Contents", "MacOS");
        Directory.CreateDirectory(Path.Combine(macos, "Localization"));
        var host = Thin(0x01000007).Concat(new byte[64]).ToArray();
        var published = System.Text.Encoding.ASCII.GetBytes("PowerShellSessionRecorder.dll\0");
        published.CopyTo(host, 32);
        File.WriteAllBytes(Path.Combine(macos, "PowerShellSessionRecorder"), host);
        File.WriteAllBytes(Path.Combine(macos, "libcoreclr.dylib"), Universal());
        File.WriteAllBytes(Path.Combine(macos, "PowerShellSessionRecorder.dll"), [0x4D, 0x5A, 1, 2]);
        File.WriteAllText(Path.Combine(macos, "PowerShellSessionRecorder.runtimeconfig.json"), "{}");
        File.WriteAllText(Path.Combine(macos, "Localization", "en-US.json"), "{}");
        File.WriteAllText(Path.Combine(macos, "PowerShellSessionRecorder.deps.json"), """
            {
              "targets": {
                ".NETCoreApp,Version=v10.0/osx-x64": {
                  "runtimepack.Microsoft.NETCore.App.Runtime.osx-x64/10.0.0": {
                    "native": {
                      "libcoreclr.dylib": {},
                      "createdump": {}
                    }
                  },
                  "SkiaSharp.NativeAssets.macOS/3.0.0": {
                    "native": {
                      "runtimes/osx/native/libSkiaSharp.dylib": {}
                    }
                  }
                }
              }
            }
            """);
        File.WriteAllBytes(Path.Combine(macos, "libSkiaSharp.dylib"), Universal());

        var laidOut = Run(root, "python3", "packaging/macos/layout-bundle.py", app);
        Assert.Equal(0, laidOut.ExitCode);

        var resources = Path.Combine(app, "Contents", "Resources");
        Assert.False(File.Exists(Path.Combine(macos, "PowerShellSessionRecorder.dll")));
        Assert.False(File.Exists(Path.Combine(macos, "Localization", "en-US.json")));
        Assert.True(File.Exists(Path.Combine(resources, "PowerShellSessionRecorder.dll")));
        Assert.True(File.Exists(Path.Combine(resources, "Localization", "en-US.json")));
        Assert.True(File.Exists(Path.Combine(macos, "libcoreclr.dylib")));
        Assert.True(File.Exists(Path.Combine(macos, "libSkiaSharp.dylib")));
        var bound = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(macos, "PowerShellSessionRecorder")));
        Assert.Contains("../Resources/PowerShellSessionRecorder.dll", bound, StringComparison.Ordinal);
        var deps = File.ReadAllText(Path.Combine(resources, "PowerShellSessionRecorder.deps.json"));
        Assert.Contains("../MacOS/libcoreclr.dylib", deps, StringComparison.Ordinal);
        Assert.Contains("../MacOS/libSkiaSharp.dylib", deps, StringComparison.Ordinal);
        Assert.Contains("\"createdump\"", deps, StringComparison.Ordinal);
        Assert.DoesNotContain("../MacOS/createdump", deps, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupSmoke_ChecksTheResourcesLayoutAndRejectsOtherArguments()
    {
        Assert.True(StartupSmoke.IsRequested(["--smoke"]));
        Assert.False(StartupSmoke.IsRequested([]));
        Assert.False(StartupSmoke.IsRequested(["--smoke", "--extra"]));
        Assert.False(StartupSmoke.IsRequested(["smoke"]));

        using var directory = new TempDirectory();
        var resources = Path.Combine(directory.Path, "Contents", "Resources");
        var macos = Path.Combine(directory.Path, "Contents", "MacOS");
        Directory.CreateDirectory(Path.Combine(resources, "Localization"));
        Directory.CreateDirectory(macos);
        File.WriteAllText(Path.Combine(macos, "PowerShellSessionRecorder"), "host");
        File.WriteAllBytes(Path.Combine(resources, "PowerShellSessionRecorder.dll"), [0x4D, 0x5A]);
        File.WriteAllText(Path.Combine(resources, "PowerShellSessionRecorder.deps.json"), "{}");
        File.WriteAllText(
            Path.Combine(resources, "PowerShellSessionRecorder.runtimeconfig.json"),
            """
            {
              "runtimeOptions": {
                "tfm": "net10.0"
              }
            }
            """);
        File.WriteAllText(Path.Combine(resources, "Localization", "en-US.json"), "{}");
        File.WriteAllText(Path.Combine(resources, "Localization", "pl-PL.json"), "{}");

        StartupSmoke.RequireBundleFiles(resources);
        StartupSmoke.RequireMacWindowingPlatform(StartupSmoke.MacWindowingPlatform);
        Assert.Throws<InvalidOperationException>(() => StartupSmoke.RequireMacWindowingPlatform(""));
        Assert.Throws<InvalidOperationException>(() => StartupSmoke.RequireMacWindowingPlatform("Avalonia.X11.X11Platform"));
        var smokeSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "src/PowerShellSessionRecorder.App/StartupSmoke.cs"));
        Assert.Contains("typeof(IWindowingPlatform)", smokeSource, StringComparison.Ordinal);
        Assert.Contains("GetProperty(\n            \"Current\"", smokeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowingSubsystemName, \"Native\"", smokeSource, StringComparison.Ordinal);
        File.Delete(Path.Combine(resources, "PowerShellSessionRecorder.runtimeconfig.json"));
        Assert.Throws<FileNotFoundException>(() => StartupSmoke.RequireBundleFiles(resources));
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
