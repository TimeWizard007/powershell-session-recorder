# Checkpoint 4.1 — Customer TAGs and transcript cleanup

Checkpoint 4.1 adds a saved customer catalog and a confirmed way to remove history entries. Recording, history search, the update check, themes, and the Ubuntu package from earlier checkpoints stay in place.

The informational version is `0.1.0-checkpoint.4.1`. The Debian package version is `0.1.0~checkpoint.4.1-1`.

## Customer catalog

Customers are stored in `customers.json` next to `settings.json`. On Linux that is `$XDG_CONFIG_HOME/powershell-session-recorder/customers.json`, or `~/.config/powershell-session-recorder/customers.json`.

Each record has:

- a stable id
- the TAG
- an optional display name
- the creation time in UTC

The file does not contain passwords or transcript text. Schema version is 1. Writes use a unique temporary file in the same directory, then an atomic replace. On Linux the directory is mode `0700` and the catalog, its lock, and any quarantine file are mode `0600`. A symbolic link in place of the directory or the catalog is not followed.

`settings.json` and `history.json` keep schema version 1. Opening the application does not create `customers.json` until a customer is saved. Recent TAGs, history TAGs, and transcript file names are not imported.

New Session shows no selected customer. Start Session stays disabled until a saved TAG is chosen or a new valid TAG is saved. The recording engine still receives that TAG as the session customer tag. A duplicate TAG is rejected by case-insensitive comparison. A blank TAG, or a TAG that cannot form a transcript file name, is rejected.

Renaming a saved TAG updates the catalog and, if that customer is selected, the TAG shown for the new session. It does not rename transcript files or rewrite history. History keeps the TAG that was used when the recording was confirmed.

Removing a customer asks for confirmation and explains that History and transcript files remain. It does not stop PowerShell. If that customer was selected, the selection is cleared and Start Session is disabled again.

## History cleanup

History has two separate actions:

- **Remove from history** drops the index entry and leaves the transcript file.
- **Remove and delete transcript** deletes the file only when it is safe, then drops the index entry.

There is no single Delete button. Both actions ask for confirmation. The confirmation states how many history entries are selected, how many transcript files would be deleted, and which of the two operations is about to run.

Several rows can be selected. Select all applies to the rows that match the current search and filters. Changing the search or filters drops selections that are no longer visible, so a hidden row is not deleted. Clear selection removes the ticks.

For a file-and-history action the sequence is:

1. Consider only the entries that are still visible.
2. For each one, try to delete exactly the absolute path stored on that entry.
3. Rewrite `history.json` once, removing only the entries whose files were deleted or were already missing.
4. Leave every other entry in the catalog.

If the catalog rewrite fails after a file was deleted, the window says that History was not updated and the entry remains listed. A later history-only removal can drop that entry. An interrupted cleanup does not choose a different file.

A missing transcript does not block history removal. A permission error, a symbolic link, a directory, or any path that is not the stored absolute path is refused, and that history entry stays.

## When a transcript file may be deleted

`RecordingConfirmed` means the transcript header was seen at launch. It does not mean the process has stopped, and it is not permission to delete the file.

File deletion is allowed only when the opened file is a regular file and its tail contains `PowerShell transcript end`. The footer confirms that the transcript finished. It does not prove that every related process has exited. The application still does not track the live `pwsh` process.

On Linux the deleter opens the parent directory without following a final symlink, opens the file the same way, checks that the descriptor is a regular file, reads the footer from that descriptor, checks that the directory entry still names the same device and inode, and then unlinks that name. It never deletes a directory, never recurses, and never builds a shell command. On other operating systems the same refusals apply through file APIs, without the Linux inode check.

An entry that is not confirmed finished can still be removed from History. Its file is kept.

## Package

Build the `.deb` with `packaging/ubuntu/build-deb.sh`. Do not install it over an existing system package unless that replacement has been approved. The package does not contain `customers.json`, `history.json`, `settings.json`, or transcripts. Removing the package still does not delete files in the user profile.
