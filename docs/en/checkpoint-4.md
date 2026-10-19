# Checkpoint 4 — Ubuntu package, status labels, and metadata permissions

This checkpoint adds an Ubuntu 24.04 amd64 `.deb` and tightens two history behaviors. It does not add process monitoring, and it does not change `history.json` schema version 1.

The package was prepared for Ubuntu 24.04 LTS on x64. Other distributions and architectures were not tested.

## Status labels

The stored status values are still `RecordingConfirmed` and `Finished`. The History page shows:

| Label | Meaning |
| --- | --- |
| Recording started (current state unknown) | The transcript header was verified at launch. The row tooltip says the current process state is not known. This is not an Active, Running, or Recording Now state. |
| Finished | The end of the transcript contains `PowerShell transcript end`. An `End time:` is stored only when those 14 digits can be parsed. A footer without a usable time is still shown as Finished, and no end time is invented. |
| Unknown | The path is not valid, or the file could not be read. |
| Transcript missing | The file is no longer at the stored path. The history entry is kept. |

Polish uses “Nagrywanie uruchomione (obecny stan nieznany)”, “Zakończona”, “Nieznany”, and “Brak transkrypcji”.

## Metadata permissions

On Linux the configuration directory is owner-only (`0700`). `settings.json`, `history.json`, `history.json.lock`, quarantine files `history.invalid-*.json`, and `settings.invalid.json` are owner read and write (`0600`). A later save or load keeps those modes. An existing directory or metadata file that is group- or world-readable is tightened to those modes. The bytes are not replaced just to change the mode, and transcript files are not deleted.

A symbolic link used as the configuration directory, settings file, history file, lock, or quarantine destination is not followed. The application does not delete that link. A history write then fails with the existing warning, while the transcript can still be recorded.

Windows does not use Unix modes. macOS continues to use the same owner-only mode when the operating system supports it.

## Package

See [Installation](installation.md). The informational version in the application project is the source of truth. `0.1.0-checkpoint.4` becomes the Debian version `0.1.0~checkpoint.4-1`, which sorts before a later stable `0.1.0-1`. For a future build, change `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, and `<InformationalVersion>` in `PowerShellSessionRecorder.App.csproj`, then run the package script again. Do not hand-edit the generated `control` file.

The package depends on the Ubuntu 24.04 libraries the binary loads, including `libssl3t64`, `libicu74`, and `libfontconfig1`. PowerShell 7 is not a package dependency because it is not in the default Ubuntu 24.04 repositories. GNOME Terminal is suggested, not required, so the window can open and show the existing install guidance when either command is missing. The package does not add repositories or install software by itself.

Removing or purging the package does not delete transcripts, settings, or history. There is no maintainer script that stops PowerShell.
