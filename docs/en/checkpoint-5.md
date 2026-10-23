# Checkpoint 5 — Windows console

[Polski](../pl/etap-5.md)

This checkpoint adds Windows 10/11 x64 recording beside the Ubuntu flow. It is not a release. The published Ubuntu beta remains `0.1.0-beta.1`. This working tree reports `0.1.0-checkpoint.5`.

## What Windows can do

- Discover PowerShell 7 at `%ProgramFiles%\PowerShell\7\pwsh.exe`, then on absolute PATH entries.
- Discover Windows PowerShell 5.1 only at `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`.
- Ignore the current directory, relative PATH entries, and reparse points such as Windows app-execution aliases.
- Prefer PowerShell 7 when both engines exist. A saved choice of 5.1 is kept when that engine is present.
- Open a separate visible console. The process is not inside the Avalonia window and is not hidden. It is not stopped when the recorder closes.
- Pass the transcript path and bootstrap path as arguments. The bootstrap script does not contain the customer TAG or session name.
- Ask for User Account Control only when **Run as administrator** is checked. The recorder window is not elevated. Canceling the prompt does not start a recording and does not write History. An accepted prompt is shown as requested, not as confirmed elevation.
- Store configuration in `%LOCALAPPDATA%\PowerShellSessionRecorder`. Transcripts stay in the selected folder.
- Delete a transcript only when its own footer is present and the opened handle is a regular file. Directories, symbolic links, junctions, and other reparse points are refused. Paths are not passed to a shell.

Ubuntu still uses GNOME Terminal and PowerShell 7. The administrator checkbox is hidden there.

## Package

`packaging/windows/publish-win-x64.sh` publishes a self-contained `win-x64` folder to `artifacts/win-x64/`. The end user does not need a .NET runtime. `packaging/windows/PowerShellSessionRecorder.iss` is the Inno Setup script for a Start Menu entry and an uninstaller.

The installer was not compiled on Ubuntu and was not tested on Windows. Publishing the folder on Ubuntu does not prove that the console, the User Account Control prompt, or the installer works.

## Not verified on Windows

Engine discovery, the visible console, elevation and its cancellation, owner-only ACLs, reparse-point deletion, and the installer still need a Windows 10 or Windows 11 x64 machine.
