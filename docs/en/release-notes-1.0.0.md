# PowerShell Session Recorder 1.0.0

First stable release. The application reports `1.0.0`. Package names use `1.0.0` and have no prerelease suffix. Previously published beta releases remain available and are not replaced.

This file is the note attached when the manual release workflow creates the GitHub Release for tag `v1.0.0`. Creating this file does not publish that release.

## Verified

- Windows 10/11 x64: the application starts, detects PowerShell, records a session, stores the transcript, and supports history.
- Ubuntu 24.04 amd64: the same recording flow through GNOME Terminal and PowerShell 7.
- Intel macOS: the installed `.app` starts, detects PowerShell, records in Terminal.app, stores transcripts, and supports history.
- Apple Silicon: the packaged `osx-arm64` apphost passes the macOS CI startup smoke test. That test does not open a window. The Apple Silicon graphical interface was not part of the functional test.

## Packages

Every file is built from the tagged commit. Each package has a sibling `.sha256` file.

- `powershell-session-recorder_1.0.0-1_amd64.deb` and `.sha256` — Ubuntu 24.04 amd64.
- `powershell-session-recorder-1.0.0-linux-x64.tar.gz` and `.sha256` — per-user Linux x64 archive.
- `PowerShellSessionRecorder-1.0.0-win-x64-setup.exe` and `.sha256` — Windows x64 installer.
- `PowerShellSessionRecorder-1.0.0-osx-x64.dmg` and `.sha256` — Intel macOS disk image.
- `PowerShellSessionRecorder-1.0.0-osx-x64.zip` and `.sha256` — Intel macOS application bundle.
- `PowerShellSessionRecorder-1.0.0-osx-arm64.dmg` and `.sha256` — Apple Silicon disk image.
- `PowerShellSessionRecorder-1.0.0-osx-arm64.zip` and `.sha256` — Apple Silicon application bundle.

There is no separate Windows portable zip. The Windows release asset is the installer. Linux has no RPM, Snap, or Flatpak package. macOS has no `.pkg` and is not notarized.

## macOS signature

The macOS bundles are ad-hoc signed so `codesign --verify --deep --strict` can check them. They are not Developer ID signed and they are not notarized. Gatekeeper can warn on first open. Leave Gatekeeper enabled. Installation and the quarantine troubleshooting command are in [macOS](macos.md).

## Updates

`v1.0.0` is a stable GitHub Release. The Stable update channel can offer it. Tags that contain a hyphen, including the existing betas, are marked prerelease and are not offered on the Stable channel. The checker does not download or install anything.

User guide: [User guide](user-guide.md).
