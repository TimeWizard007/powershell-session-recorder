using System.Buffers.Binary;
using System.Diagnostics;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint521Tests
{
    private const string SourceSha256 = "7d31efaa578022dc2f75d4e1e38a4f41bb1443ad89c8931dfe34f613aea13841";

    [Fact]
    public void ApprovedIconSource_IsUnchangedAndDerivedIconsMatchItsSizes()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllBytes(Path.Combine(root, "assets/branding/app-icon-source.png"));
        Assert.Equal(SourceSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)).ToLowerInvariant());
        Assert.Equal(1254, PngWidth(source));

        Assert.Equal(256, PngWidth(File.ReadAllBytes(Path.Combine(root, "src/PowerShellSessionRecorder.App/Assets/app-icon.png"))));
        Assert.Equal(1024, PngWidth(File.ReadAllBytes(Path.Combine(root, "assets/branding/macos/app-icon-1024.png"))));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x01, 0x00 }, File.ReadAllBytes(Path.Combine(root, "assets/branding/app-icon.ico")).Take(4).ToArray());

        var ico = File.ReadAllBytes(Path.Combine(root, "assets/branding/app-icon.ico"));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4, 2));
        Assert.Equal(7, count);
        var widths = new List<int>();
        for (var index = 0; index < count; index++)
        {
            var width = ico[6 + index * 16];
            widths.Add(width == 0 ? 256 : width);
        }

        Assert.Equal(new[] { 16, 24, 32, 48, 64, 128, 256 }, widths);
    }

    [Fact]
    public void WindowsAndLinuxProjects_ReferenceTheDerivedIcon()
    {
        var root = RepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj"));
        var window = File.ReadAllText(Path.Combine(root, "src/PowerShellSessionRecorder.App/Views/MainWindow.axaml"));
        var desktop = File.ReadAllText(Path.Combine(root, "packaging/ubuntu/powershell-session-recorder.desktop"));
        var setup = File.ReadAllText(Path.Combine(root, "packaging/windows/build-setup.ps1"));

        Assert.Contains("<ApplicationIcon>..\\..\\assets\\branding\\app-icon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("Icon=\"/Assets/app-icon.png\"", window, StringComparison.Ordinal);
        Assert.Contains("Icon=powershell-session-recorder", desktop, StringComparison.Ordinal);
        Assert.Contains("Program Files (x86)\\Inno Setup 6\\ISCC.exe", setup, StringComparison.Ordinal);
        Assert.Contains("PSScriptRoot", setup, StringComparison.Ordinal);
        Assert.Contains("INNO_SETUP_COMPILER", setup, StringComparison.Ordinal);
        Assert.Contains("PowerShellSessionRecorder.exe", setup, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSetupScript_ParsesWhenPowerShellIsAvailable()
    {
        var pwsh = ExecutableLocator.Find("pwsh", Environment.GetEnvironmentVariable("PATH"), File.Exists);
        if (pwsh is null)
            return;

        var start = new ProcessStartInfo
        {
            FileName = pwsh,
            UseShellExecute = false,
            RedirectStandardError = true
        };
        start.Environment["PSR_SCRIPT"] = Path.Combine(RepositoryRoot(), "packaging/windows/build-setup.ps1");
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$errors = $null; $tokens = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($env:PSR_SCRIPT, [ref]$tokens, [ref]$errors); if (@($errors).Count -gt 0) { exit 1 }; exit 0");
        using var process = Process.Start(start);
        Assert.NotNull(process);
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private static int PngWidth(byte[] png)
        => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));

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
