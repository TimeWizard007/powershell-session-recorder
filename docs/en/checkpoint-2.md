# Checkpoint 2

[Polski](../pl/etap-2.md)

Checkpoint 2 starts a real PowerShell 7 recording session from the Avalonia window on Ubuntu. Checkpoint 2.1 keeps that recording flow and refreshes the interface: vertical navigation (New Session, History, Settings, About), System/Light/Dark themes, and a dependency check for `pwsh` and GNOME Terminal. The customer TAG is typed for each session. Recent TAGs saved in an older settings file are kept and are not selected automatically.

## What happens when Start Session is pressed

1. The form must be valid, PowerShell 7 must be `pwsh` on `PATH`, and GNOME Terminal must be installed. Elevation stays disabled: the button does not start a session while **Run as administrator** is checked.
2. The application creates the log directory when it is missing.
3. It reserves a new `.txt` file with `FileMode.CreateNew`. An existing transcript is never overwritten. Simultaneous starts receive different names.
4. It writes a fixed bootstrap script outside the log directory. Customer text and paths are process arguments, not text inserted into the script.
5. GNOME Terminal is launched without `--wait`. The terminal client returns immediately. The PowerShell process belongs to `gnome-terminal-server`, so closing the Avalonia window does not stop the recording.
6. The bootstrap runs `Start-Transcript` before the prompt. The terminal prints that recording is active and shows the transcript path.
7. If `Start-Transcript` fails, the terminal prints the error and waits for Enter. It then exits. It does not leave an unrecorded shell.
8. The Avalonia status line says recording started only after the transcript header is visible in the file. Otherwise it says the terminal opened but recording was not confirmed.
9. When the user exits PowerShell normally, the bootstrap calls `Stop-Transcript` and deletes the bootstrap script.

The **Save session in history** checkbox is stored for a later history list. The transcript file is created either way. This checkpoint does not add a history list or a Stop Session button.

## Terminal support

On the development machine the only installed terminal emulator is GNOME Terminal 3.52 (`/usr/bin/gnome-terminal`). The launcher builds arguments for that program and was checked there. `x-terminal-emulator` is not used, because the Ubuntu wrapper adds `--wait` and can pass the command through a shell.

Other terminal emulators are not claimed to work.

## Reproduce the recording test

Requirements: Ubuntu with a desktop session, PowerShell 7 (`pwsh`), and GNOME Terminal.

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

1. Select customer TAG `HOWE`.
2. Enter session name `Diagnostyka VPN`.
3. Select `/tmp` as the log directory.
4. Leave PowerShell 7 selected and leave **Run as administrator** unchecked.
5. Press **Start Session**.
6. In the new terminal, confirm the recording message and the transcript path before typing.
7. Run `Get-Date` and `Get-Location`.
8. Exit with `exit`.
9. Open the `.txt` file in `/tmp` and confirm both commands and their output.
10. Start two sessions and confirm they use different files.
11. Start another session, close the Avalonia window, and confirm the terminal is still running.

## Limitations

- Windows PowerShell 5.1 is not launched.
- Elevation and UAC are not implemented.
- `Start-Transcript` records what the PowerShell host displays. A password or token typed or printed in the session can be stored in the transcript.
- Closing the terminal window, or killing the process, can skip the transcript footer. The file may still contain the commands that were already written.
- The application does not load or change the PowerShell profile. The session uses `-NoProfile`.
- The filename preview uses an example identifier. The started session reserves a different file.
