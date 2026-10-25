using System.Diagnostics;

namespace PowerShellSessionRecorder.Tests;

public class CheckpointReleaseTests
{
    [Fact]
    public void Workflows_ParseAndKeepReleasePublishingGated()
    {
        var root = RepositoryRoot();
        var ci = LoadWorkflow(Path.Combine(root, ".github/workflows/ci.yml"));
        var release = LoadWorkflow(Path.Combine(root, ".github/workflows/release.yml"));

        Assert.True(ci.Contains("pull_request", StringComparison.Ordinal));
        Assert.Contains("branches:\n      - main", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: ubuntu-24.04", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: windows-2022", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: macos-15", ci, StringComparison.Ordinal);
        Assert.Contains("sh packaging/macos/build-dmg.sh all", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: macos-15", release, StringComparison.Ordinal);
        Assert.Contains("sh packaging/macos/build-dmg.sh all", release, StringComparison.Ordinal);
        Assert.Contains("release-macos", release, StringComparison.Ordinal);
        Assert.Contains("PowerShellSessionRecorder-${{ needs.resolve.outputs.version }}-osx-x64.dmg", release, StringComparison.Ordinal);
        Assert.Contains("PowerShellSessionRecorder-${{ needs.resolve.outputs.version }}-osx-arm64.zip", release, StringComparison.Ordinal);
        Assert.Contains("verify-checksum-files.sh release-assets 7", release, StringComparison.Ordinal);
        Assert.DoesNotContain("hdiutil", release, StringComparison.Ordinal);
        Assert.DoesNotContain("notarytool", release, StringComparison.Ordinal);
        Assert.Contains("dotnet-version: 10.0.x", ci, StringComparison.Ordinal);
        Assert.Contains("dotnet test -c Debug --no-build --nologo", ci, StringComparison.Ordinal);
        Assert.Contains("dotnet test -c Release --no-build --nologo", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_dispatch", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release", ci, StringComparison.Ordinal);

        Assert.Contains("workflow_dispatch", release, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", release, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  push:", release, StringComparison.Ordinal);
        Assert.Contains("default: review-only", release, StringComparison.Ordinal);
        Assert.Contains("environment: release", release, StringComparison.Ordinal);
        Assert.Contains("if: inputs.mode == 'publish'", release, StringComparison.Ordinal);
        Assert.Contains("contents: write", release, StringComparison.Ordinal);
        Assert.Contains("GH_TOKEN: ${{ github.token }}", release, StringComparison.Ordinal);
        Assert.Contains("needs.resolve.outputs.sha", release, StringComparison.Ordinal);
        Assert.Contains("SOURCE_REVISION_ID:", release, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/install-inno-setup.ps1", release, StringComparison.Ordinal);
        Assert.Contains("packaging/ci/validate-linux-release.sh", release, StringComparison.Ordinal);
        Assert.Contains("dotnet test -c Release --nologo", release, StringComparison.Ordinal);
        Assert.Contains("FA73BF47A4DA250D185D07561C2BFDA387E5E20DB77E4570004CF6A133CC10B1", File.ReadAllText(Path.Combine(root, "packaging/windows/install-inno-setup.ps1")), StringComparison.Ordinal);
        var publisher = File.ReadAllText(Path.Combine(root, "packaging/ci/publish-github-release.sh"));
        Assert.Contains("RELEASE_APPROVED", publisher, StringComparison.Ordinal);
        Assert.Contains("Refusing to overwrite", publisher, StringComparison.Ordinal);
        Assert.Contains("PowerShellSessionRecorder-${version}-osx-arm64.dmg", publisher, StringComparison.Ordinal);
        Assert.Contains("PowerShellSessionRecorder-${version}-osx-x64.zip", publisher, StringComparison.Ordinal);
        Assert.Contains("*-*) prerelease=\"--prerelease\"", publisher, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionFiles_AgreeOnTheStableRelease()
    {
        var output = RunShell(RepositoryRoot(), "packaging/ci/assert-version-files.sh", "1.0.0");
        Assert.Equal(0, output.ExitCode);
        var lines = output.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("1.0.0", lines[0]);
        Assert.Equal("1.0.0-1", lines[1]);
    }

    [Fact]
    public void ReleaseTagResolver_RefusesTheCurrentBranchCheckout()
    {
        var root = RepositoryRoot();
        var branch = Run(root, "git", "symbolic-ref", "-q", "HEAD");
        if (branch.ExitCode != 0)
            return;

        var resolved = RunShell(root, "packaging/ci/resolve-release-tag.sh", "v0.1.0-beta.2");
        Assert.NotEqual(0, resolved.ExitCode);
        Assert.Contains("Refusing to build from a branch", resolved.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseValidators_AcceptTheApprovedIconAndRejectSampleData()
    {
        var root = RepositoryRoot();
        var icon = Path.Combine(root, "assets/branding/app-icon.ico");
        var iconCheck = Run(root, "python3", "packaging/ci/assert-icon-resource.py", icon, icon);
        Assert.Equal(0, iconCheck.ExitCode);

        var empty = Path.Combine(Path.GetTempPath(), "psr-empty-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(empty, [1, 2, 3, 4]);
        try
        {
            var rejected = Run(root, "python3", "packaging/ci/assert-icon-resource.py", icon, empty);
            Assert.NotEqual(0, rejected.ExitCode);
        }
        finally
        {
            File.Delete(empty);
        }

        var marked = Path.Combine(Path.GetTempPath(), "psr-version-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(marked, System.Text.Encoding.ASCII.GetBytes("0.1.0-beta.2+" + new string('a', 40)));
        try
        {
            var accepted = Run(root, "python3", "packaging/ci/assert-informational-version.py", marked, "0.1.0-beta.2", new string('a', 40));
            Assert.Equal(0, accepted.ExitCode);
            var wrong = Run(root, "python3", "packaging/ci/assert-informational-version.py", marked, "0.1.0-beta.2", new string('b', 40));
            Assert.NotEqual(0, wrong.ExitCode);
        }
        finally
        {
            File.Delete(marked);
        }

        var executable = Path.Combine(root, "artifacts/win-x64/PowerShellSessionRecorder.exe");
        if (File.Exists(executable))
        {
            var embedded = Run(root, "python3", "packaging/ci/assert-icon-resource.py", icon, executable);
            Assert.Equal(0, embedded.ExitCode);
        }

        foreach (var script in new[]
        {
            "packaging/ci/assert-version-files.sh",
            "packaging/ci/resolve-release-tag.sh",
            "packaging/ci/validate-linux-release.sh",
            "packaging/ci/verify-checksum-files.sh",
            "packaging/ci/publish-github-release.sh",
            "packaging/ubuntu/build-deb.sh",
            "packaging/linux/build-tarball.sh",
            "packaging/windows/publish-win-x64.sh"
        })
        {
            var syntax = Run(root, RequireBash(), "-n", Path.Combine(root, script));
            Assert.True(syntax.ExitCode == 0, script + Environment.NewLine + syntax.Text);
        }
    }

    private static string LoadWorkflow(string path)
    {
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("\t", text, StringComparison.Ordinal);
        Assert.StartsWith("name:", text, StringComparison.Ordinal);
        Assert.Contains("\njobs:\n", text, StringComparison.Ordinal);
        return text;
    }

    private static (int ExitCode, string Text) RunShell(string root, string script, params string[] args)
    {
        var arguments = new string[args.Length + 1];
        arguments[0] = Path.Combine(root, script);
        args.CopyTo(arguments, 1);
        return Run(root, RequireBash(), arguments);
    }

    private static string RequireBash()
    {
        if (!OperatingSystem.IsWindows())
            return "bash";

        var gitBash = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Git",
            "bin",
            "bash.exe");
        if (File.Exists(gitBash))
            return gitBash;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), "bash.exe");
            if (!File.Exists(candidate))
                continue;
            if (candidate.Contains("System32", StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        throw new InvalidOperationException("Bash was not found. Git Bash is required to check the release scripts on Windows.");
    }

    private static (int ExitCode, string Text) Run(string root, string fileName, params string[] args)
    {
        var executable = Path.Combine(root, fileName);
        if (!File.Exists(executable))
            executable = fileName;

        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = root,
            UseShellExecute = false,
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
