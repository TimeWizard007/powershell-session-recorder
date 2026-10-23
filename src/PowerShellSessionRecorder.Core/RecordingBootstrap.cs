namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Fixed PowerShell 7 bootstrap. Customer text and paths are process arguments,
/// never interpolated into this script.
/// </summary>
public static class RecordingBootstrap
{
    public const string HeaderMarker = "transcript start";

    public static string Script { get; } =
        """
        #Requires -Version 5.1
        param(
            [Parameter(Mandatory = $true)]
            [string] $TranscriptPath,

            [Parameter(Mandatory = $true)]
            [string] $BootstrapPath,

            [ValidateSet('en-US', 'pl-PL')]
            [string] $Language = 'en-US',

            [string] $WindowTitle = 'PowerShell Session Recorder',

            [string] $WorkingDirectory = ''
        )

        $ErrorActionPreference = 'Stop'

        $messages = @{
            'en-US' = @{
                FailedTitle = 'RECORDING DID NOT START'
                Active = 'Recording is active.'
                TranscriptLabel = 'Transcript:'
                Close = 'An unrecorded shell is not allowed.'
                PressEnter = 'Press Enter to close this window.'
            }
            'pl-PL' = @{
                FailedTitle = 'REJESTROWANIE NIE ZOSTAŁO URUCHOMIONE'
                Active = 'Rejestrowanie jest aktywne.'
                TranscriptLabel = 'Transkrypcja:'
                Close = 'Powłoka bez rejestrowania nie jest dozwolona.'
                PressEnter = 'Naciśnij Enter, aby zamknąć to okno.'
            }
        }

        if (-not $messages.ContainsKey($Language)) {
            $Language = 'en-US'
        }

        $text = $messages[$Language]

        function Remove-EmptyTranscriptFile {
            if (-not (Test-Path -LiteralPath $TranscriptPath -PathType Leaf)) {
                return
            }

            $item = Get-Item -LiteralPath $TranscriptPath
            if ($item.Length -eq 0) {
                Remove-Item -LiteralPath $TranscriptPath -Force -ErrorAction SilentlyContinue
            }
        }

        function Remove-BootstrapFile {
            if ([string]::IsNullOrWhiteSpace($BootstrapPath)) {
                return
            }

            Remove-Item -LiteralPath $BootstrapPath -Force -ErrorAction SilentlyContinue
        }

        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            try { Set-Location -LiteralPath $WorkingDirectory } catch { }
        }

        if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) {
            try { $Host.UI.RawUI.WindowTitle = $WindowTitle } catch { }
        }

        try {
            Start-Transcript -LiteralPath $TranscriptPath -Append -IncludeInvocationHeader | Out-Null
            if ($PSVersionTable.PSVersion.Major -ge 6 -and -not $IsWindows) {
                [System.IO.File]::SetUnixFileMode(
                    $TranscriptPath,
                    [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
            }
        }
        catch {
            Write-Host $text.FailedTitle -ForegroundColor Red
            Write-Host $_.Exception.Message -ForegroundColor Red
            Write-Host $text.Close
            Write-Host $text.PressEnter
            Remove-EmptyTranscriptFile
            Remove-BootstrapFile
            try { $null = Read-Host } catch { }
            # exit only leaves the script file. -NoExit would then keep an unrecorded shell.
            [Environment]::Exit(1)
        }

        Write-Host $text.Active
        Write-Host ("{0} {1}" -f $text.TranscriptLabel, $TranscriptPath)

        $global:PSRBootstrapPath = $BootstrapPath
        Register-EngineEvent -SourceIdentifier PowerShell.Exiting -SupportEvent -Action {
            try { Stop-Transcript | Out-Null } catch { }
            try {
                if (-not [string]::IsNullOrWhiteSpace($global:PSRBootstrapPath)) {
                    Remove-Item -LiteralPath $global:PSRBootstrapPath -Force -ErrorAction SilentlyContinue
                }
            } catch { }
        } | Out-Null
        """;

    public static string NormalizeLanguage(string? language)
    {
        var canonical = ApplicationSettings.CanonicalLanguage(language);
        return canonical == "pl-PL" ? "pl-PL" : "en-US";
    }

    public static IReadOnlyList<string> BuildPowerShellArguments(
        string workingDirectory,
        string bootstrapPath,
        string transcriptPath,
        string? language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptPath);

        return
        [
            "-NoLogo",
            "-NoProfile",
            "-NoExit",
            "-WorkingDirectory",
            workingDirectory,
            "-File",
            bootstrapPath,
            "-TranscriptPath",
            transcriptPath,
            "-Language",
            NormalizeLanguage(language),
            "-BootstrapPath",
            bootstrapPath
        ];
    }

    public static IReadOnlyList<string> BuildWindowsArguments(
        PowerShellVersion version,
        string workingDirectory,
        string bootstrapPath,
        string transcriptPath,
        string? language,
        string windowTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(windowTitle);

        var arguments = new List<string>
        {
            "-NoLogo",
            "-NoProfile",
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass"
        };

        if (version == PowerShellVersion.PowerShell7)
        {
            arguments.Add("-WorkingDirectory");
            arguments.Add(workingDirectory);
        }

        arguments.Add("-File");
        arguments.Add(bootstrapPath);
        arguments.Add("-TranscriptPath");
        arguments.Add(transcriptPath);
        arguments.Add("-Language");
        arguments.Add(NormalizeLanguage(language));
        arguments.Add("-BootstrapPath");
        arguments.Add(bootstrapPath);
        arguments.Add("-WindowTitle");
        arguments.Add(windowTitle);
        arguments.Add("-WorkingDirectory");
        arguments.Add(workingDirectory);
        return arguments;
    }

    public static bool ContainsHeader(string? content)
        => content?.Contains(HeaderMarker, StringComparison.OrdinalIgnoreCase) == true;
}
