# Checkpoint 3

[Polski](../pl/etap-3.md)

Checkpoint 3 adds a local, searchable history of confirmed recording sessions. The recording engine is unchanged: Start Session still launches an independent GNOME Terminal running PowerShell 7 with `Start-Transcript`. History is an index of metadata. It is not required for a transcript to be written.

## What is stored

History is `history.json` in the same per-user directory as `settings.json`. The file uses schema version 1 and a `sessions` array. Each entry stores:

- a unique session id
- the customer TAG and session name
- the transcript file name and its absolute path
- the start time in UTC; the History page derives the local date and time from that value
- the selected PowerShell version
- a verified recording status
- an optional end time, only after a transcript footer has been read

The transcript text is not stored in `history.json`. On Unix the file mode is owner read and write (`0600`). A write goes to a temporary file in the same directory and then replaces `history.json`. On Linux and Windows a lock file serializes writers. A crash before the replace leaves the previous `history.json` in place. Checkpoint 4 also limits the configuration directory to the owner.

A file that is not valid JSON, or that contains an invalid session, is renamed to `history.invalid-<timestamp>-<n>.json`. That copy is not overwritten. A `schemaVersion` newer than 1 is left unchanged and is not rewritten. The application does not delete transcript files when it reads or updates history.

Existing settings files keep their language, theme, log directory, PowerShell selection, and update-check values. A new session still starts with an empty customer TAG. Recent TAG suggestions are not shown.

## When a session is indexed

**Save session in history** controls the index only. The transcript is created either way.

When the option is checked, an entry is added only after the transcript header has been seen (`Recording started`). The same TAG and session name can be used again; each confirmed launch gets its own id and its own transcript file. If the history file cannot be written, the status line still says that recording started and adds a warning that History was not updated. The terminal is left running.

When the option is unchecked, nothing is added. The application does not scan the log directory later and does not import that transcript. Transcripts created before this feature, including files left in `/tmp`, are not imported. The empty History page says that.

A launch that does not confirm the transcript header is not indexed.

## Status labels

The page does not show an Active state, and it does not treat a missing end time as a running session. GNOME Terminal can keep running after this application closes, and a killed process can leave a transcript without a footer.

| Label | Meaning |
| --- | --- |
| Recording started (current state unknown) | The transcript header was verified when the session started. The stored value remains `RecordingConfirmed`. This does not mean the process is still running. |
| Finished | The end of the transcript file contains `PowerShell transcript end`. `End time:` is read as local time and stored in UTC when it can be parsed. A footer without a usable time is still Finished. |
| Unknown | The path is not valid, or the file could not be read. |
| Transcript missing | The transcript file is no longer at the stored path. The history entry is kept. |

The application does not track the PowerShell process after launch. Refresh reads only the last 8 KB of a transcript when checking for the footer. It does not load the transcript into the history file.

## History page

Sessions are listed newest first. The page can search the TAG, session name, and transcript file name, filter by an exact customer TAG, and filter by an inclusive local date range. **Clear filters** resets those controls. **Refresh** reads the index again.

When the index has no sessions, the page says that only sessions recorded with history enabled after this feature are listed. When sessions exist but the current search or filters exclude all of them, the page says that nothing matches.

Each row can open the transcript, open the folder that contains it, or copy the absolute path. On Linux the file and folder are passed as one argument to `xdg-open`. The path is not interpolated into a shell command, and the transcript is not executed. A missing file, an invalid path, or a denied read is reported in the status line in English or Polish.

## What remains out of scope

Windows PowerShell 5.1, elevation, macOS recording, other terminal emulators, deleting history entries, and importing older transcripts are not part of this checkpoint. Only GNOME Terminal 3.52 on Ubuntu 24.04 was tested for recording.
