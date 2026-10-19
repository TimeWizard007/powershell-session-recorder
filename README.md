# PowerShell Session Recorder

[Polski](README.pl.md)

PowerShell Session Recorder is a lightweight, offline desktop application for IT administrators and MSP or helpdesk teams. It will let an administrator open an independent PowerShell session for a customer, tag that session, and keep a transcript on the local machine.

There is no backend server, database server, cloud account, or Windows service.

## Status

**Checkpoint 3 — persistent session history.** Start Session still opens an independent GNOME Terminal running `pwsh` with `Start-Transcript` already active. A confirmed session can be listed in History. The application can also ask GitHub whether a newer stable release exists. It does not download or install updates. Version 1.0.0 has not been released. See [CHANGELOG.md](CHANGELOG.md) and [Checkpoint 3](docs/en/checkpoint-3.md).

## Supported operating systems

The application is intended for:

- Windows 10 and Windows 11
- Ubuntu Linux
- macOS

Checkpoint 2 records a PowerShell 7 session on Ubuntu through GNOME Terminal. Windows PowerShell 5.1 can be selected only when the application is running on Windows, and it is not launched.

## Technology

- C# and .NET 10 (LTS)
- Avalonia UI 12 with the Fluent theme
- MVVM with CommunityToolkit.Mvvm
- PowerShell 7. Recording is implemented for Ubuntu with GNOME Terminal
- Windows PowerShell 5.1 on Windows only (not launched yet)
- JSON for local settings and session history

## What works now

- Session configuration: customer TAG, session name, log directory, PowerShell version, privilege choice, timestamp option, and history option
- Live validation and a transcript filename preview
- Windows-safe filename sanitization and collision-aware unique names
- Local folder selection
- English (`en-US`) and Polish (`pl-PL`), with the choice saved for the current user
- Local JSON settings
- A History page for sessions recorded with **Save session in history** checked. Older transcript files are not imported
- **Start Session**, when the form is valid and `pwsh` and GNOME Terminal are available
- A notify-only check for a newer stable GitHub release. Automatic checks run at most once every 24 hours and can be turned off. Nothing is downloaded or installed

## Limitations

- Windows PowerShell 5.1 is not launched
- Privilege elevation is not performed. The administrator checkbox is not saved, and a session cannot start while it is checked
- History does not show a live Active state. A session is marked Finished only when the transcript footer is present
- Transcripts recorded with **Save session in history** unchecked, and transcripts created before this feature, are not added to History
- `Start-Transcript` records host output. Secrets typed or displayed in the console can be stored in the transcript
- Abrupt termination can omit the transcript footer
- Only GNOME Terminal 3.52 on Ubuntu 24.04 was tested. Other terminal emulators are not claimed to work
- The filename shown in the preview uses an example identifier. Starting a session reserves a new file and does not overwrite an existing transcript

## Prerequisites

- [.NET SDK 10](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu-2404) (LTS)
- A desktop session for the graphical interface

On a fresh Ubuntu machine, Avalonia also needs the usual native libraries. Ubuntu 24.04 package names:

```bash
sudo apt-get update
sudo apt-get install -y libgtk-3-0t64 libice6 libsm6 libfontconfig1 libx11-6
```

The folder picker uses the desktop portal. A normal Ubuntu desktop already provides it:

```bash
sudo apt-get install -y xdg-desktop-portal xdg-desktop-portal-gtk
```

PowerShell 7 and GNOME Terminal are required to start a recorded session. Installation instructions: [Install PowerShell on Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).

## Build

From the repository root:

```bash
dotnet build PowerShellSessionRecorder.sln
```

## Run on Ubuntu

From the repository root, with a display available:

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

Settings are stored in `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json`, or in `~/.config/powershell-session-recorder/settings.json` when `XDG_CONFIG_HOME` is not set.

## Tests

```bash
dotnet test PowerShellSessionRecorder.sln
```

## Planned functionality

Later checkpoints can launch Windows PowerShell 5.1 and support elevation where the operating system allows it.

## Settings locations

| Operating system | Settings file |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` or `~/.config/powershell-session-recorder/settings.json` |
| Windows | `%APPDATA%\PowerShellSessionRecorder\settings.json` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder/settings.json` |

The settings file stores the interface language, the last log directory, the last PowerShell version, the timestamp and history options, the theme, and the update-check preference. It does not store passwords or customer secrets. A file that cannot be parsed is left in place, copied once to `settings.invalid.json`, and replaced in memory by defaults.

Session history is a separate `history.json` file in that same directory. It stores metadata for sessions that were confirmed with **Save session in history** checked. It does not store transcript contents. See [Checkpoint 3](docs/en/checkpoint-3.md).

## Documentation

- [Checkpoint 3](docs/en/checkpoint-3.md)
- [Checkpoint 2](docs/en/checkpoint-2.md)
- [Checkpoint 1](docs/en/checkpoint-1.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)

## License

[MIT License](LICENSE). Copyright (c) 2026 TimeWizard.
