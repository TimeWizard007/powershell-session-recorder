# PowerShell Session Recorder 0.1.0-beta.1

First public beta for Ubuntu 24.04 LTS, amd64.

The application version is `0.1.0-beta.1`. About shows that version. The Debian package is `powershell-session-recorder_0.1.0~beta.1-1_amd64.deb`. Version 1.0.0 has not been released. Windows is not part of this beta.

## Support scope

This package is built for Ubuntu 24.04 LTS on amd64. It was not tested on other Ubuntu releases, other distributions, or other architectures.

A recorded session needs two programs that are not inside the package:

- PowerShell 7 (`pwsh`), installed separately. It is not in the default Ubuntu 24.04 repositories. See [Install PowerShell on Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).
- GNOME Terminal. The package suggests it. A normal Ubuntu desktop already has it. Other terminal emulators are not claimed to work.

The `.deb` contains the Avalonia application and its .NET runtime. It does not contain PowerShell 7, GNOME Terminal, settings, the customer catalog, history, or transcripts.

## This beta

- **Independent PowerShell 7 recording sessions.** Start Session opens its own GNOME Terminal running PowerShell 7 with `Start-Transcript` already active. The transcript is a new file. An existing transcript is not overwritten.
- **Persistent searchable History.** A confirmed session can be saved in a local `history.json` index and found later by search and filters. History is optional for each session.
- **Customer TAG management.** Saved customers live in a local `customers.json` catalog. New Session does not select a TAG until one is chosen or created. Renaming or removing a saved customer leaves existing recordings unchanged.
- **Safe transcript cleanup.** A history entry can be removed on its own, or together with its transcript file. The file is deleted only when that file’s own completion footer has been read and the path passes the safety checks. A transcript without that footer can leave History, and the file stays.
- **English and Polish.** The interface language is saved for the current user.
- **Light and dark themes.** System, Light, and Dark are available, and the choice is saved for the current user.
- **GitHub update checking.** The application can ask whether a newer stable release exists. The check can run at most once every 24 hours, and it can be turned off. Nothing is downloaded or installed.
- **Ubuntu desktop integration.** The package adds an application menu entry, a launcher command, and icons. It runs as the user who starts it.

## Update checks and this beta

The installed version reported in About and in Settings is `0.1.0-beta.1`. The same version is the User-Agent on the update request.

The checker reads only the latest stable GitHub release, `https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest`. GitHub does not return a prerelease from that endpoint. A beta tag, including a later beta such as `0.1.0-beta.2`, is not offered as an update. A newer stable release is. Stable `0.1.0` is newer than this beta under Semantic Versioning, so it would be shown when it exists.

The request does not send customer TAGs, session names, transcripts, history, or settings.

## Install

Build and checksum instructions are in [Installation](installation.md). Removing the package does not delete transcripts or the files under `$XDG_CONFIG_HOME/powershell-session-recorder`.

## Limits that still apply

- Windows PowerShell 5.1 is not launched, and privilege elevation is not performed.
- History does not follow the live PowerShell process. A footer means the transcript finished. It does not prove that every related process has exited.
- Transcripts can contain anything typed or printed in the session.
