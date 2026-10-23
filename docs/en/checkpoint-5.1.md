# Checkpoint 5.1

Working-tree version `0.1.0-checkpoint.5.1`. This is not a release. The published Ubuntu beta remains `0.1.0-beta.1`.

## Encoding

Windows PowerShell 5.1 reads a `.ps1` file that has no byte-order mark with the system ANSI code page. The bootstrap is UTF-8, so `ł` (`C5 82`) was shown as `Ĺ‚` and the parser rejected the Polish string. On Windows the bootstrap file is now UTF-8 with the bytes `EF BB BF`. Linux files stay UTF-8 without a BOM. Console encoding, profiles, and execution policy are unchanged.

## Window

The window no longer starts at a fixed 1120×780. When the monitor working area is large enough it opens at 960×640 device-independent pixels, centered in that working area, and it can be resized. The minimum is 640×480 unless the working area is smaller. Action rows wrap so Polish labels stay reachable.

Windows visual checks and Windows PowerShell 5.1 parsing were not run from Ubuntu.
