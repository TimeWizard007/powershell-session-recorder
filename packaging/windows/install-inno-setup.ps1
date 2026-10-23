# Download and install a pinned Inno Setup 6 compiler.
# The SHA-256 is the GitHub release asset digest for jrsoftware/issrc tag is-6_5_4.
$ErrorActionPreference = "Stop"

$version = "6.5.4"
$expectedHash = "FA73BF47A4DA250D185D07561C2BFDA387E5E20DB77E4570004CF6A133CC10B1"
$url = "https://github.com/jrsoftware/issrc/releases/download/is-6_5_4/innosetup-$version.exe"
$installer = Join-Path $env:TEMP "innosetup-$version.exe"
$target = Join-Path $env:TEMP "powershell-session-recorder-inno-$version"
$compiler = Join-Path $target "ISCC.exe"

if (Test-Path -LiteralPath $compiler) {
    Write-Output $compiler
    exit 0
}

$ProgressPreference = "SilentlyContinue"
Invoke-WebRequest -Uri $url -OutFile $installer
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash.ToUpperInvariant()
if ($actual -ne $expectedHash) {
    Write-Error "Inno Setup $version hash mismatch. Expected $expectedHash, got $actual."
}

$process = Start-Process -FilePath $installer -ArgumentList @(
    "/VERYSILENT",
    "/NORESTART",
    "/SUPPRESSMSGBOXES",
    "/SP-",
    "/DIR=$target"
) -Wait -PassThru
if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}
if (-not (Test-Path -LiteralPath $compiler)) {
    Write-Error "Inno Setup $version finished without writing $compiler."
}

Write-Output $compiler
