# Checkpoint 5.2

Prepared version `0.1.0-beta.2`. This is not a published GitHub release. The published release remains `0.1.0-beta.1`.

Debian package version: `0.1.0~beta.2-1`.
Windows installer version: `0.1.0-beta.2`.
Linux archive version: `0.1.0-beta.2`.
Assembly and file versions stay `0.1.0.0`. The Git commit is stored after `+` in the assembly informational version. About and the User-Agent omit that suffix.

## Packages

All three packages are built from this source revision.

- Ubuntu 24.04 amd64 `.deb` from `packaging/ubuntu/build-deb.sh`. It keeps the existing desktop integration, `/usr/bin` launcher, icons, and user-data locations.
- Linux x64 `.tar.gz` from `packaging/linux/build-tarball.sh`. `install-user.sh` installs into `~/.local/opt/powershell-session-recorder` without sudo. The desktop name is **PowerShell Session Recorder (per user)** so it is not the system `.deb` launcher. `uninstall-user.sh` deletes only manifest files. Settings, customers, history, and transcripts stay.
- Windows installer script `packaging/windows/PowerShellSessionRecorder.iss`. Per-user is the default because `PrivilegesRequired=lowest`. The dialog can select an all-users install, which asks for UAC. `UsePreviousPrivileges=yes` keeps an upgrade in the existing scope. `{autopf}` maps to `%LOCALAPPDATA%\Programs` or `%ProgramFiles%` according to that choice. The script was not compiled on Ubuntu.

The per-user Linux installer was exercised in an isolated `HOME` on Ubuntu 24.04. It was not claimed to cover every Debian version or desktop environment.

## Update channels

Settings schema stays at 1. A missing `updateChannel` loads as Stable and does not rewrite the file. Stable uses `/releases/latest` and ignores prereleases. Beta uses `/releases?per_page=100` for at most three pages, skips drafts, and chooses the highest semantic version rather than the first row. An older tag is not offered. Changing the channel clears the cached version, URL, and last-check time. Checks stay manual or at most once every 24 hours. No release is downloaded.

## Not done here

The Windows installer was not compiled or run. Windows PowerShell 5.1 parsing and the Windows window were not rechecked on Windows. No package was installed on the developer workstation. Nothing was committed, tagged, or published.
