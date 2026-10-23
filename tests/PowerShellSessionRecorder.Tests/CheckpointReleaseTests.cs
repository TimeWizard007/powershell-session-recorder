using System.Diagnostics;

namespace PowerShellSessionRecorder.Tests;

public class CheckpointReleaseTests
{
    [Fact]
    public void Workflows_ParseAndKeepReleasePublishingGated()
    {
        var root = RepositoryRoot();
        var ci = LoadYaml(Path.Combine(root, ".github/workflows/ci.yml"));
        var release = LoadYaml(Path.Combine(root, ".github/workflows/release.yml"));

        Assert.True(ci.Contains("pull_request", StringComparison.Ordinal));
        Assert.Contains("branches:\n      - main", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: ubuntu-24.04", ci, StringComparison.Ordinal);
        Assert.Contains("runs-on: windows-2022", ci, StringComparison.Ordinal);
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
        Assert.Contains("needs.resolve.outputs.sha", release, StringComparison.Ordinal);
        Assert.Contains("SOURCE_REVISION_ID:", release, StringComparison.Ordinal);
        Assert.Contains("packaging/windows/install-inno-setup.ps1", release, StringComparison.Ordinal);
        Assert.Contains("packaging/ci/validate-linux-release.sh", release, StringComparison.Ordinal);
        Assert.Contains("dotnet test -c Release --nologo", release, StringComparison.Ordinal);
        Assert.Contains("FA73BF47A4DA250D185D07561C2BFDA387E5E20DB77E4570004CF6A133CC10B1", File.ReadAllText(Path.Combine(root, "packaging/windows/install-inno-setup.ps1")), StringComparison.Ordinal);
        Assert.Contains("RELEASE_APPROVED", File.ReadAllText(Path.Combine(root, "packaging/ci/publish-github-release.sh")), StringComparison.Ordinal);
        Assert.Contains("Refusing to overwrite", File.ReadAllText(Path.Combine(root, "packaging/ci/publish-github-release.sh")), StringComparison.Ordinal);
    }

    [Fact]
    public void VersionFiles_AgreeOnBeta2()
    {
        var output = Run(RepositoryRoot(), "packaging/ci/assert-version-files.sh", "0.1.0-beta.2");
        Assert.Equal(0, output.ExitCode);
        var lines = output.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("0.1.0-beta.2", lines[0]);
        Assert.Equal("0.1.0~beta.2-1", lines[1]);
    }

    [Fact]
    public void ReleaseTagResolver_RefusesTheCurrentBranchCheckout()
    {
        var root = RepositoryRoot();
        var branch = Run(root, "git", "symbolic-ref", "-q", "HEAD");
        if (branch.ExitCode != 0)
            return;

        var resolved = Run(root, "packaging/ci/resolve-release-tag.sh", "v0.1.0-beta.2");
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
            var syntax = Run(root, "bash", "-n", script);
            Assert.Equal(0, syntax.ExitCode);
        }
    }

    private static string LoadYaml(string path)
    {
        var parsed = Run(Path.GetDirectoryName(path)!, "python3", "-c", "import pathlib, yaml, sys; yaml.safe_load(pathlib.Path(sys.argv[1]).read_text()); print('ok')", path);
        Assert.Equal(0, parsed.ExitCode);
        return File.ReadAllText(path);
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
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, text);
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
