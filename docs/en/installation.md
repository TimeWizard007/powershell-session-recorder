# Install on Ubuntu 24.04

[Polski](../pl/instalacja.md)

This package is for Ubuntu 24.04 LTS, amd64 only. It was not tested on other distributions or architectures. The published GitHub release is `0.1.0-beta.1`, Debian package `0.1.0~beta.1-1`. This working tree prepares `0.1.0-beta.2`, Debian package `0.1.0~beta.2-1`. Notes are in [Release notes](release-notes-0.1.0-beta.2.md).

A recorded session requires PowerShell 7 and GNOME Terminal. Neither program is inside the package. PowerShell 7 is not in the default Ubuntu 24.04 repositories.

The `.deb` contains the Avalonia application and its .NET runtime. It does not contain PowerShell 7, GNOME Terminal, settings, the customer catalog, history, or transcripts. The program runs as the user who starts it. The launcher does not use `sudo`.

About shows the informational version without the `+` commit suffix. The update checker reports that same version. Stable, the default, requests GitHub’s latest stable release. Beta also considers published prereleases and does not offer an older tag. The request does not include customer TAGs, history, or transcripts. Nothing is downloaded.

## Build

From a clean checkout, with the .NET SDK 10 and `dpkg-deb`:

```bash
packaging/ubuntu/build-deb.sh
```

The script publishes a self-contained `linux-x64` build and writes `artifacts/powershell-session-recorder_<debian-version>_amd64.deb` plus a SHA-256 file with the same name and a `.sha256` suffix. Both files are gitignored. The informational version in `PowerShellSessionRecorder.App.csproj` is the source of truth. `0.1.0-beta.2` becomes the Debian version `0.1.0~beta.2-1`. A stable `1.0.0` would become `1.0.0-1`. The `~` makes the beta package sort before the stable package with the same core version. `<AssemblyVersion>` and `<FileVersion>` stay `0.1.0.0` because those fields are numeric. The Git commit is kept after `+` in the assembly informational version and is omitted from the package version and from About.

Check the package after the build:

```bash
cd artifacts
sha256sum -c powershell-session-recorder_0.1.0~beta.2-1_amd64.deb.sha256
```

To publish a later build, update `<InformationalVersion>` in the application project, then run the script again.

## Install

```bash
sudo apt install ./artifacts/powershell-session-recorder_0.1.0~beta.2-1_amd64.deb
```

`apt` installs the runtime libraries named in the package. It does not install PowerShell 7. GNOME Terminal, `xdg-desktop-portal`, and `xdg-desktop-portal-gtk` are suggestions. On a normal Ubuntu desktop they are already present. If they are missing, the application still opens and explains how to install them. It does not run those commands.

PowerShell 7 comes from Microsoft’s repository, not from Ubuntu’s default repositories. Follow [Install PowerShell on Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu) before starting a recorded session.

Icons are documented in [Icons](icons.md). The approved source is `assets/branding/app-icon-source.png`.

## Per-user Linux archive

`packaging/linux/build-tarball.sh` writes `artifacts/powershell-session-recorder-0.1.0-beta.2-linux-x64.tar.gz` and a `.sha256` file. It needs Bash and GNU find. It was checked on Ubuntu 24.04, not on every Debian release or desktop.

```bash
tar -xzf powershell-session-recorder-0.1.0-beta.2-linux-x64.tar.gz
cd powershell-session-recorder-0.1.0-beta.2-linux-x64
./install-user.sh
```

Files go to `~/.local/opt/powershell-session-recorder`. The menu entry is **PowerShell Session Recorder (per user)** and does not replace `/usr/bin/powershell-session-recorder`. `./uninstall-user.sh` removes only the files listed for that installation. Settings, customers, history, and transcripts stay. The installer refuses a symbolic link in the archive or at the destination.

## Windows 10/11 x64

This checkout can also publish a self-contained Windows build. It does not require a .NET runtime on the destination computer. PowerShell 7 or Windows PowerShell 5.1 must already be installed. Windows Terminal is not required.

From the repository root, on Ubuntu or Windows:

```bash
packaging/windows/publish-win-x64.sh
```

The output is `artifacts/win-x64/`. That directory is gitignored. On Windows, from the repository root or any other directory, with Inno Setup 6 installed:

```powershell
packaging\windows\build-setup.ps1
```

The expected installer name is `artifacts\PowerShellSessionRecorder-0.1.0-beta.2-win-x64-setup.exe`. It was not compiled on Ubuntu. The default choice is a per-user install in `%LOCALAPPDATA%\Programs\PowerShellSessionRecorder`, without administrator rights. Choosing all users asks for User Account Control and installs in `%ProgramFiles%\PowerShellSessionRecorder`. An upgrade stays in the scope already registered for the same AppId. The installer does not change `PATH`, does not bundle PowerShell 7, and does not remove `%LOCALAPPDATA%\PowerShellSessionRecorder`.

Configuration on Windows is `%LOCALAPPDATA%\PowerShellSessionRecorder`. Transcripts stay in the folder selected in the application. The program does not need administrator rights for ordinary use. Run as administrator applies only to the PowerShell console.

A new Windows account starts with an empty customer catalog, no selected customer, empty history, and the ordinary setting defaults. Per-user and all-users installs share that same profile folder. They do not create sample customers, TAGs, sessions, or transcripts. A customer that is already in `customers.json`, including one saved during earlier testing, stays there after an upgrade or reinstall. Remove it in the application with **Remove from catalog** and confirm. History entries and transcript files remain until you remove those entries from History.

The informational version in this working tree is `0.1.0-beta.2`. The published GitHub release remains `0.1.0-beta.1`. GitHub Actions can build the `.deb`, the Linux archive, and the Windows installer from one tag. The steps and the manual approval are in [CI and release builds](release-pipeline.md).

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

`sudo apt purge powershell-session-recorder` removes the same package files. Neither command deletes transcripts, `settings.json`, `history.json`, or `customers.json`. Those stay in the log directory you chose and in `$XDG_CONFIG_HOME/powershell-session-recorder` or `~/.config/powershell-session-recorder`. A PowerShell process that is already running keeps running. Remove that data yourself only when you intend to.

The `.deb` and the per-user `.tar.gz` use that same configuration directory. A new Linux account starts with an empty catalog and empty history. Neither package installs a sample customer. To remove a customer that is already saved, open it in the application, choose **Remove from catalog**, and confirm. That leaves History and transcript files in place.

## What is installed

- `/opt/powershell-session-recorder/` — application files
- `/usr/bin/powershell-session-recorder` — link to the application
- `/usr/share/applications/powershell-session-recorder.desktop`
- icons under `/usr/share/icons/hicolor/`
- `/usr/share/doc/powershell-session-recorder/copyright`
