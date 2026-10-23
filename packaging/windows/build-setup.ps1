# Compile the Inno Setup installer on Windows.
# Requires Inno Setup 6 and a populated artifacts\win-x64 directory.
# The compiler path can be passed as -CompilerPath or INNO_SETUP_COMPILER.
param(
    [string]$CompilerPath
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$iss = Join-Path $root "packaging\windows\PowerShellSessionRecorder.iss"
$publish = Join-Path $root "artifacts\win-x64"
$application = Join-Path $publish "PowerShellSessionRecorder.exe"
$library = Join-Path $publish "PowerShellSessionRecorder.dll"
$icon = Join-Path $root "assets\branding\app-icon.ico"

function Find-InnoCompiler {
    param([string]$Requested)

    $candidates = @()
    if ($Requested) {
        $candidates += $Requested
    }
    if ($env:INNO_SETUP_COMPILER) {
        $candidates += $env:INNO_SETUP_COMPILER
    }
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramW6432)) {
        if ($base) {
            $candidates += (Join-Path $base "Inno Setup 6\ISCC.exe")
        }
    }

    $onPath = Get-Command iscc -ErrorAction SilentlyContinue
    if ($onPath) {
        $candidates += $onPath.Source
    }

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

$missing = @()
foreach ($required in @($iss, $application, $library, $icon)) {
    if (-not (Test-Path -LiteralPath $required)) {
        $missing += $required
    }
}
if ($missing.Count -gt 0) {
    Write-Error ("Missing required files:`n" + ($missing -join "`n") + "`nPublish the Windows app with packaging/windows/publish-win-x64.sh, then run this script again.")
}

$compiler = Find-InnoCompiler -Requested $CompilerPath
if (-not $compiler) {
    Write-Error "Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6, or pass -CompilerPath or set INNO_SETUP_COMPILER to ISCC.exe. A standard install is 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'."
}

$issText = Get-Content -LiteralPath $iss -Raw
if ($issText -notmatch '#define MyAppVersion "([^"]+)"') {
    Write-Error "MyAppVersion was not found in $iss."
}
$appVersion = $Matches[1]
$projectPath = Join-Path $root "src\PowerShellSessionRecorder.App\PowerShellSessionRecorder.App.csproj"
$project = Get-Content -LiteralPath $projectPath -Raw
if ($project -notmatch '<InformationalVersion>([^<]+)</InformationalVersion>') {
    Write-Error "InformationalVersion was not found in $projectPath."
}
$informational = ($Matches[1] -split '\+', 2)[0]
if ($informational -ne $appVersion) {
    Write-Error "Inno Setup version $appVersion does not match InformationalVersion $informational."
}

& $compiler $iss
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$setupName = "PowerShellSessionRecorder-$appVersion-win-x64-setup.exe"
$setup = Join-Path $root "artifacts\$setupName"
if (-not (Test-Path -LiteralPath $setup)) {
    Write-Error "Inno Setup finished without writing $setup."
}

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash.ToLowerInvariant()
$checksum = Join-Path $root "artifacts\$setupName.sha256"
[System.IO.File]::WriteAllBytes($checksum, [System.Text.Encoding]::ASCII.GetBytes("$hash  $setupName`n"))
Write-Output $setup
