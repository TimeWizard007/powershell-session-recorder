using System.Diagnostics;
using System.Text;
using PowerShellSessionRecorder.App.Services.Recording;
using PowerShellSessionRecorder.App.Views;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.Tests;

public class Checkpoint51Tests
{
    private const string PolishSentence = "Powłoka bez rejestrowania nie jest dozwolona.";

    [Fact]
    public void WindowsBootstrap_StartsWithUtf8BomAndKeepsPolishText()
    {
        using var root = new TempDirectory();
        var store = new BootstrapFileStore(root.Path, emitUtf8Bom: true);
        var first = store.WriteScript();
        var second = store.WriteScript();
        var firstBytes = File.ReadAllBytes(first);
        var secondBytes = File.ReadAllBytes(second);

        Assert.Equal(firstBytes, secondBytes);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, firstBytes.Take(3).ToArray());
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(firstBytes.AsSpan(3));
        Assert.Contains(PolishSentence, text, StringComparison.Ordinal);
        Assert.Contains("ł", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PowĹ", text, StringComparison.Ordinal);
        Assert.Equal(RecordingBootstrap.Script, text);
        Assert.DoesNotContain("HOWE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LinuxBootstrap_StaysUtf8WithoutBom()
    {
        using var root = new TempDirectory();
        var store = new BootstrapFileStore(root.Path, emitUtf8Bom: false);
        var bytes = File.ReadAllBytes(store.WriteScript());

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains(PolishSentence, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            var native = File.ReadAllBytes(new BootstrapFileStore(root.Path).WriteScript());
            Assert.NotEqual(0xEF, native[0]);
        }
    }

    [Fact]
    public void PowerShell7_ParsesTheUtf8BomBootstrap()
    {
        var pwsh = ExecutableLocator.Find("pwsh", Environment.GetEnvironmentVariable("PATH"), File.Exists);
        if (pwsh is null)
            return;

        using var root = new TempDirectory();
        var path = new BootstrapFileStore(root.Path, emitUtf8Bom: true).WriteScript();
        var exitCode = ParseWith(pwsh, path);
        Assert.Equal(0, exitCode);
    }

#if WINDOWS_HOST
    [Fact]
#else
    [Fact(Skip = "Windows PowerShell 5.1 parser was not executed on this host.")]
#endif
    public void WindowsPowerShell51_ParsesTheUtf8BomBootstrapWhenHostedOnWindows()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot")
            ?? throw new InvalidOperationException("SystemRoot is not set.");
        var powershell = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        Assert.True(File.Exists(powershell));

        using var root = new TempDirectory();
        var path = new BootstrapFileStore(root.Path, emitUtf8Bom: true).WriteScript();
        Assert.Equal(0, ParseWith(powershell, path));
    }

    [Theory]
    [InlineData(1920, 1080, 960, 640)]
    [InlineData(1280, 800, 960, 640)]
    [InlineData(1024, 768, 960, 640)]
    [InlineData(800, 600, 776, 576)]
    public void StartupWindow_FitsTheLogicalWorkingArea(double width, double height, double expectedWidth, double expectedHeight)
    {
        var fit = WindowStartupBounds.Fit(width, height);

        Assert.Equal(expectedWidth, fit.Width);
        Assert.Equal(expectedHeight, fit.Height);
        Assert.True(fit.Width <= width - WindowStartupBounds.Margin);
        Assert.True(fit.Height <= height - WindowStartupBounds.Margin);
        Assert.True(fit.MinWidth <= fit.Width);
        Assert.True(fit.MinHeight <= fit.Height);
        Assert.True(fit.Width < 1120);
        Assert.True(fit.Height < 780);
    }

    [Theory]
    [InlineData(1920, 1200, 1.5, 48)]
    [InlineData(1920, 1080, 1.0, 40)]
    [InlineData(1920, 1080, 1.25, 40)]
    [InlineData(1920, 1080, 2.0, 48)]
    public void StartupWindow_FitsScaledWorkingArea(double pixelsWide, double pixelsHigh, double scale, double taskbarPixels)
    {
        var working = WindowStartupBounds.WorkingAreaDips(pixelsWide, pixelsHigh - taskbarPixels, scale);
        var fit = WindowStartupBounds.Fit(working.Width, working.Height);

        Assert.True(fit.Width <= working.Width - WindowStartupBounds.Margin + 0.01);
        Assert.True(fit.Height <= working.Height - WindowStartupBounds.Margin + 0.01);
        Assert.True(fit.Width <= WindowStartupBounds.PreferredWidth);
        Assert.True(fit.Height <= WindowStartupBounds.PreferredHeight);
    }

    private static int ParseWith(string executable, string scriptPath)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["PSR_BOOTSTRAP"] = scriptPath;
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$errors = $null; $tokens = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($env:PSR_BOOTSTRAP, [ref]$tokens, [ref]$errors); if (@($errors).Count -gt 0) { exit 1 }; exit 0");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(20_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException(error.IsCompletedSuccessfully ? error.Result : "");
        }

        _ = output.GetAwaiter().GetResult();
        _ = error.GetAwaiter().GetResult();
        return process.ExitCode;
    }
}
