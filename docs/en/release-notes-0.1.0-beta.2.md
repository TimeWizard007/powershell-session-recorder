# PowerShell Session Recorder 0.1.0-beta.2

This beta is prepared from one source revision for Ubuntu 24.04 amd64 and Windows 10/11 x64. It is not published until a GitHub Release is created. The published release remains `0.1.0-beta.1`.

The application version is `0.1.0-beta.2`. About shows that version. The Debian package is `powershell-session-recorder_0.1.0~beta.2-1_amd64.deb`. The Linux archive is `powershell-session-recorder-0.1.0-beta.2-linux-x64.tar.gz`. The Windows installer name, when compiled with Inno Setup 6, is `PowerShellSessionRecorder-0.1.0-beta.2-win-x64-setup.exe`. Version 1.0.0 has not been released. macOS is not included.

## Install

Ubuntu system package: install the `.deb` with `apt`. It does not install PowerShell 7 or GNOME Terminal. Upgrading or removing the package leaves settings, the customer catalog, history, and transcripts in place.

Linux per-user archive: extract the `.tar.gz` and run `install-user.sh`. It does not use sudo. The menu entry is **PowerShell Session Recorder (per user)** and does not replace a system-wide `/usr/bin` launcher. `uninstall-user.sh` removes only that installation's files.

Windows: the release workflow compiles `packaging/windows/PowerShellSessionRecorder.iss` with Inno Setup 6.5.4 after publishing the self-contained application. The default is a per-user install without administrator rights. All users is an explicit choice and asks for User Account Control. The installer does not bundle PowerShell 7. Windows PowerShell 5.1 can record without PowerShell 7. That workflow has not run on GitHub yet.

## What changed since 0.1.0-beta.1

- Windows 10/11 x64 can launch PowerShell 7 or Windows PowerShell 5.1 in a separate console. Optional elevation applies only to that console.
- Windows bootstrap scripts are UTF-8 with a BOM so Windows PowerShell 5.1 can read Polish text. Linux scripts stay UTF-8 without a BOM.
- The window opens at 960×640 device-independent pixels when the working area allows it, and action buttons wrap.
- Settings has an update channel. Stable is the default and only offers a newer stable release. Beta can offer a newer stable or prerelease version. Drafts are ignored. A lower version is never offered. The check does not download or install anything and does not send customer data.
- A per-user Linux archive is available beside the `.deb`.

## Update checker

The installed version reported in About and in Settings is `0.1.0-beta.2`. The same version is the User-Agent on the update request. Stable does not offer this beta as an update. Beta will offer a later published release, such as `0.1.0-beta.3` or `0.1.0`, when one exists, and will not offer `0.1.0-beta.1`.

## Limits

Tested packaging on Ubuntu 24.04 amd64 covers the `.deb` inspection and an isolated per-user install, upgrade, and uninstall. An earlier Windows installer was exercised manually, but the release installer must come from the tagged GitHub Actions run, which has not happened yet. Not every Debian version or desktop environment was tested. macOS is not supported.
