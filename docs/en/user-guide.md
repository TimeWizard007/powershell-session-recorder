# User guide

[Polski](../pl/przewodnik.md)

PowerShell Session Recorder is a local desktop application for administrators and support teams. It opens a separate PowerShell console for a customer, tags that session, and keeps a transcript on the same computer. There is no server, account, or automatic download.

This source tree is version **1.0.0**. The GitHub Release for `v1.0.0` exists only after the tag is created and the manual release workflow publishes it. Older beta releases stay on GitHub.

The window icon is `assets/branding/app-icon-source.png`. This repository does not include screenshots of the window. The interface has four areas: the session form, History, Settings, and About.

## What it does

- Saves customers as TAGs and starts a session with one selected TAG.
- Opens PowerShell outside the application window and records with `Start-Transcript`.
- Stores a history entry when that option is checked.
- Keeps English and Polish interface text, a theme, and an update preference on this computer.
- Can compare the installed version with GitHub releases. It does not download or install an update.

## Supported systems

| System | Architecture | What was verified |
| --- | --- | --- |
| Windows 10 and Windows 11 | x64 | Installed application: startup, PowerShell detection, recording, transcripts, and history |
| Ubuntu 24.04 LTS | amd64 | The same flow through GNOME Terminal and PowerShell 7 |
| macOS 14 or later, tested on Intel Sequoia | x64 | Installed `.app`: startup, PowerShell detection, recording in Terminal.app, transcripts, and history |
| macOS 14 or later | Apple Silicon | Packaged apphost passes the CI startup smoke test. The window was not part of that test |

The macOS bundle declares a minimum of macOS 14.0. Functional recording was checked on Intel macOS, not on every macOS release and not on Apple Silicon as a visible application. Other Linux distributions and desktops were not tested. The application does not search the current directory for a PowerShell executable.

## PowerShell

PowerShell is not included in any package.

- Linux: PowerShell 7 (`pwsh`) and GNOME Terminal. Other terminals are not claimed to work.
- macOS: PowerShell 7 (`pwsh`) and Terminal.app, which is part of macOS.
- Windows: PowerShell 7 (`pwsh.exe`) or Windows PowerShell 5.1 (`powershell.exe`). When both are installed, PowerShell 7 is preferred. Windows PowerShell 5.1 does not require PowerShell 7.

Settings shows the detected installed version and the newest stable PowerShell 7 release from Microsoft. That panel is informational. It does not install PowerShell.

## Install

Packages and checksums are listed in [Release notes 1.0.0](release-notes-1.0.0.md). Build steps are in [Installation](installation.md). macOS package choice, the disk image, and Gatekeeper are in [macOS](macos.md).

- Windows x64: run `PowerShellSessionRecorder-1.0.0-win-x64-setup.exe`. The default is a per-user install under `%LOCALAPPDATA%\Programs\PowerShellSessionRecorder` and does not require administrator rights. All users asks for User Account Control and installs under Program Files. An upgrade stays in the scope already registered for this application. Uninstall removes the application files only. It does not remove `%LOCALAPPDATA%\PowerShellSessionRecorder`.
- Ubuntu 24.04 amd64: install `powershell-session-recorder_1.0.0-1_amd64.deb`, or unpack `powershell-session-recorder-1.0.0-linux-x64.tar.gz` and run its `install-user.sh`. Removing either install does not delete settings, customers, history, or transcripts.
- Intel Mac: use the `osx-x64` disk image or zip. Apple Silicon: use the `osx-arm64` disk image or zip. Open the image and drag **PowerShell Session Recorder** to Applications, or copy the `.app` out of the zip. Replacing an existing bundle replaces that bundle only.

There is no Windows zip in the release, and there is no macOS `.pkg`.

### macOS Gatekeeper

The macOS builds are ad-hoc signed. They are not Developer ID signed and they are not notarized. Gatekeeper can warn the first time the app is opened. Leave Gatekeeper enabled.

Control-click the app, choose **Open**, then choose **Open** again. That allows this one app.

For a downloaded copy that you have already verified with its SHA-256 file, an advanced option is to clear quarantine on that copy only:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Do not run `sudo spctl --master-disable`. That would turn Gatekeeper off for every application.

## Customers

A customer is a TAG plus an optional display name. New Session starts with no TAG selected. Choose a saved TAG or add one in the session form. Renaming or removing a saved customer does not rewrite History and does not rename transcript files. The catalog is `customers.json` in the settings directory. It is not filled from History or from old transcript names.

## Record a session

On the session form set the customer TAG, session name, log directory, PowerShell version, whether the file name includes the date and time, and whether the session is saved in History. The form shows a filename preview. Starting a session reserves a new file and does not overwrite an existing transcript.

**Start Session** opens a separate console. The PowerShell window is not embedded in this application. On Windows, the privilege choice can ask User Account Control for that console only. Canceling the prompt does not start a recording and does not write History. On Ubuntu the administrator option is not used to start a session.

A normal `exit` runs `Stop-Transcript`. Closing the terminal another way can omit the transcript footer.

## Transcripts and history

Transcripts are written in the log directory selected for the session. The file name is built from the time, TAG, and session name, plus a short unique suffix when needed. `Start-Transcript` records host output. A secret typed or displayed in that console can be stored in the file. Treat transcripts as sensitive.

History lists sessions that were started with **Save session in history** checked. Older transcripts are not imported. A history row can be removed by itself. Removing the transcript file as well is allowed only when the file contains the footer `PowerShell transcript end`. That footer means the transcript finished. It does not prove that every related process has exited. A file without the footer can leave History and is not deleted. History does not show a live running state. Until the footer is present, the label is “Recording started (current state unknown)”.

`history.json` stores the TAG, session name, path, start time, PowerShell version, and status. It does not store the transcript text.

## Settings and language

Settings are saved for the current user:

| Operating system | Directory |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder` or `~/.config/powershell-session-recorder` |
| Windows | `%LOCALAPPDATA%\PowerShellSessionRecorder` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder` |

`settings.json` stores the language (`en-US` or `pl-PL`), theme, last log directory, last PowerShell choice, timestamp and history options, and the update preference. It does not store passwords. A file that cannot be parsed is left in place, copied once to `settings.invalid.json`, and replaced in memory by defaults. The theme follows the system, or a light or dark choice saved in that file.

## Updates

Stable is the default channel. It reads GitHub’s latest stable release. `v1.0.0` is stable, so this channel can offer it. A prerelease tag, including `v0.1.0-beta.1` and any later beta, is ignored on the Stable channel. Those releases remain available on GitHub.

The Beta channel compares published stable and prerelease tags, skips drafts, and looks at most three pages of 100 releases. A lower version is not offered. Changing the channel clears the cached result.

Automatic checks run at most once every 24 hours and can be turned off. **Check for updates** in Settings runs a check on demand. The request sends the application version and does not send customer TAGs, session names, transcripts, history, or settings. Nothing is downloaded or installed. About shows `1.0.0` without the commit suffix that packaging appends after `+`.

## Troubleshooting

- The session button stays unavailable when the form is incomplete or the required PowerShell host or terminal is missing.
- On Windows, canceling User Account Control returns to the form and does not create a history entry.
- A transcript without `PowerShell transcript end` is unfinished. History can drop the entry and will not delete the file.
- On macOS, a Gatekeeper warning is expected for this ad-hoc signature. Use Control-click **Open**. Do not disable Gatekeeper.
- On macOS, install the `osx-x64` package on Intel and the `osx-arm64` package on Apple Silicon. The two packages are not interchangeable.
- The update check reports a rate limit, an offline state, or no newer stable release without changing local files.

## Build

Contributors need the [.NET SDK 10](https://dotnet.microsoft.com/download). From a checkout:

```bash
dotnet build PowerShellSessionRecorder.sln
dotnet test PowerShellSessionRecorder.sln
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

Package commands are in [Installation](installation.md) and [macOS](macos.md). Do not commit `artifacts/`, settings, customers, history, or transcripts. See [Contributing](../../CONTRIBUTING.md).

## Downloads and checksums

Release assets and the `sha256sum -c` examples are in [Release notes 1.0.0](release-notes-1.0.0.md) and [CI and release builds](release-pipeline.md). Check the checksum before opening a downloaded installer or disk image. The release workflow builds every asset from the tagged commit and refuses to publish when one of them is missing.

## Version history

[Changelog](../../CHANGELOG.md). Notes for this version: [Release notes 1.0.0](release-notes-1.0.0.md). The beta notes remain: [0.1.0-beta.1](release-notes-0.1.0-beta.1.md) and [0.1.0-beta.2](release-notes-0.1.0-beta.2.md).
