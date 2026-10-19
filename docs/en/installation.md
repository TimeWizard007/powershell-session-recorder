# Install on Ubuntu 24.04

[Polski](../pl/instalacja.md)

This package is for Ubuntu 24.04 LTS, x64 only. It was not tested on other distributions or architectures.

The `.deb` contains the Avalonia application and its .NET runtime. It does not contain PowerShell 7, GNOME Terminal, customer settings, history, or transcripts. The program runs as the user who starts it. The launcher does not use `sudo`.

## Build

From a clean checkout, with the .NET SDK 10 and `dpkg-deb`:

```bash
packaging/ubuntu/build-deb.sh
```

The script publishes a self-contained `linux-x64` build and writes `artifacts/powershell-session-recorder_<debian-version>_amd64.deb`. The application informational version is read from `PowerShellSessionRecorder.App.csproj`. A prerelease `0.1.0-checkpoint.4.1` becomes the Debian version `0.1.0~checkpoint.4.1-1`. The earlier checkpoint `0.1.0-checkpoint.4` mapped to `0.1.0~checkpoint.4-1`. A stable `1.0.0` would become `1.0.0-1`. The `~` makes the checkpoint package sort before the stable package with the same core version.

To publish a later build, update `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, and `<InformationalVersion>` in the application project, then run the script again.

## Install

```bash
sudo apt install ./artifacts/powershell-session-recorder_0.1.0~checkpoint.4.1-1_amd64.deb
```

`apt` installs the runtime libraries named in the package. It does not install PowerShell 7. GNOME Terminal, `xdg-desktop-portal`, and `xdg-desktop-portal-gtk` are suggestions. On a normal Ubuntu desktop they are already present. If they are missing, the application still opens and explains how to install them. It does not run those commands.

PowerShell 7 comes from Microsoft’s repository, not from Ubuntu’s default repositories. Follow [Install PowerShell on Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu) before starting a recorded session.

## Launch

The application menu entry is **PowerShell Session Recorder**. From a terminal:

```bash
powershell-session-recorder
```

Both start `/opt/powershell-session-recorder/PowerShellSessionRecorder`. No terminal window is required for the application itself. A recorded session still opens GNOME Terminal.

## Upgrade

Install the newer `.deb` the same way:

```bash
sudo apt install ./artifacts/powershell-session-recorder_<newer-version>_amd64.deb
```

## Remove

```bash
sudo apt remove powershell-session-recorder
```

`sudo apt purge powershell-session-recorder` removes the same package files. Neither command deletes transcripts, `settings.json`, or `history.json`. Those stay in the log directory you chose and in `$XDG_CONFIG_HOME/powershell-session-recorder` or `~/.config/powershell-session-recorder`. A PowerShell process that is already running keeps running. Remove that data yourself only when you intend to.

## What is installed

- `/opt/powershell-session-recorder/` — application files
- `/usr/bin/powershell-session-recorder` — link to the application
- `/usr/share/applications/powershell-session-recorder.desktop`
- icons under `/usr/share/icons/hicolor/`
- `/usr/share/doc/powershell-session-recorder/copyright`
