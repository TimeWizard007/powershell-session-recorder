# PowerShell Session Recorder

[Polski](README.pl.md)

PowerShell Session Recorder is a lightweight, offline desktop application for IT administrators and MSP or helpdesk teams. It will let an administrator open an independent PowerShell session for a customer, tag that session, and keep a transcript on the local machine.

There is no backend server, database server, cloud account, or Windows service.

## Status

**Checkpoint 1 — application foundation.** This version does not launch PowerShell and does not record sessions. Version 1.0.0 has not been released. See [CHANGELOG.md](CHANGELOG.md).

## Supported operating systems

The application is intended for:

- Windows 10 and Windows 11
- Ubuntu Linux
- macOS

Checkpoint 1 builds and runs as a desktop application on Ubuntu. Windows PowerShell 5.1 can be selected only when the application is running on Windows.

## Technology

- C# and .NET 10 (LTS)
- Avalonia UI 12 with the Fluent theme
- MVVM with CommunityToolkit.Mvvm
- PowerShell 7 on Windows, Linux, and macOS (used by later checkpoints)
- Windows PowerShell 5.1 on Windows only (used by later checkpoints)
- JSON for local settings and, later, session history

## What works now

- Session configuration: customer TAG, session name, log directory, PowerShell version, privilege choice, timestamp option, and history option
- Live validation and a transcript filename preview
- Windows-safe filename sanitization and collision-aware unique names
- Local folder selection
- English (`en-US`) and Polish (`pl-PL`), with the choice saved for the current user
- Local JSON settings
- An empty History page. It does not show sample sessions
- A disabled **Start Session** action, because the recording engine is not part of this checkpoint

## Limitations

- PowerShell is not started
- Transcripts are not created
- Privilege elevation is not performed
- The administrator checkbox is not saved
- History is not recorded
- The filename shown in the preview is a proposal. Uniqueness is enforced again when a session is created in a later checkpoint

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

PowerShell 7 is not required to build or open this checkpoint. It will be required when recording is implemented. Installation instructions: [Install PowerShell on Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).

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

Later checkpoints will launch a recorded PowerShell session, write a transcript into the selected folder, honor the saved history option, and support elevation where the operating system allows it.

## Settings locations

| Operating system | Settings file |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` or `~/.config/powershell-session-recorder/settings.json` |
| Windows | `%APPDATA%\PowerShellSessionRecorder\settings.json` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder/settings.json` |

The file stores the interface language, the last log directory, the last PowerShell version, the timestamp and history options, and recent customer TAGs. It does not store passwords or customer secrets. A file that cannot be parsed is left in place, copied once to `settings.invalid.json`, and replaced in memory by defaults.

## Documentation

- [Development status](docs/en/checkpoint-1.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)

## License

[MIT License](LICENSE). Copyright (c) 2026 TimeWizard.
