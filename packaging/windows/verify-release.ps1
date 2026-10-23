# Check the Windows publish directory, installer, icon resource, and commit metadata.
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$python = $null
foreach ($candidate in @("py", "python", "python3")) {
    $command = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($command) {
        $python = $command.Source
        break
    }
}
if (-not $python) {
    Write-Error "Python is required to validate the Windows release artifacts."
}

function Invoke-Python {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$ArgumentList)
    if ((Split-Path -Leaf $python) -eq "py") {
        & $python -3 @ArgumentList
    } else {
        & $python @ArgumentList
    }
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$bash = Get-Command bash -ErrorAction SilentlyContinue
if (-not $bash) {
    Write-Error "bash is required to read the project version."
}
$versionLines = & bash (Join-Path $root "packaging/ci/assert-version-files.sh")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
$version = ($versionLines | Select-Object -First 1).Trim()
$sha = (& git -C $root rev-parse HEAD).Trim()
if ($sha -notmatch '^[0-9a-f]{40}$') {
    Write-Error "HEAD is not a full commit SHA."
}

$publish = Join-Path $root "artifacts\win-x64"
$application = Join-Path $publish "PowerShellSessionRecorder.exe"
$library = Join-Path $publish "PowerShellSessionRecorder.dll"
$icon = Join-Path $root "assets\branding\app-icon.ico"
$setup = Join-Path $root "artifacts\PowerShellSessionRecorder-$version-win-x64-setup.exe"
$checksum = "$setup.sha256"
foreach ($required in @($application, $library, $icon, $setup, $checksum)) {
    if (-not (Test-Path -LiteralPath $required)) {
        Write-Error "Missing $required."
    }
}

$forbidden = @("settings.json", "settings.invalid.json", "customers.json", "history.json", "customers.json.lock", "history.json.lock")
$found = Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object {
    $forbidden -contains $_.Name -or $_.Extension -eq ".pdb"
}
if ($found) {
    Write-Error ("The Windows publish directory contains user data or debug symbols:`n" + ($found.FullName -join "`n"))
}

Invoke-Python (Join-Path $root "packaging\ci\assert-informational-version.py") $library $version $sha
Invoke-Python (Join-Path $root "packaging\ci\assert-icon-resource.py") $icon $application $setup

$hashLine = (Get-Content -LiteralPath $checksum -Raw).Trim()
$expected = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash.ToLowerInvariant()
if ($hashLine -notlike "$expected  $(Split-Path -Leaf $setup)") {
    Write-Error "Installer checksum does not match $setup."
}

$iss = Get-Content -LiteralPath (Join-Path $root "packaging\windows\PowerShellSessionRecorder.iss") -Raw
if ($iss -notlike "*AppId={{A7B3C1E2-4F58-4D1A-9C6E-2B8F0A1D5E77}*") {
    Write-Error "Inno Setup AppId changed."
}
if ($iss -notlike "*PrivilegesRequired=lowest*") {
    Write-Error "Per-user installation default changed."
}

Write-Output "Windows release artifacts match $version ($sha)."
