# macOS packages

[Polski](../pl/macos.md)

These packages are an ad-hoc signed `0.1.0-beta.2` build for internal testing. The signature is not a Developer ID signature. They are not notarized and they are not production-trusted. The published `v0.1.0-beta.2` GitHub Release is not changed by this packaging, and the release workflow does not upload the macOS files yet.

## Build

From a checkout with the .NET SDK 10:

```bash
sh packaging/macos/build-dmg.sh all
```

`osx-x64` or `osx-arm64` builds one architecture. The script publishes a self-contained .NET 10 application, including the Avalonia native libraries, and removes the temporary publish directory when it finishes. It does not read or write `~/.config/powershell-session-recorder`, and it does not copy an existing application out of `/Applications`.

Outputs under `artifacts/`, which is gitignored:

- `PowerShellSessionRecorder-0.1.0-beta.2-osx-x64.dmg` and the same name with `.sha256`
- `PowerShellSessionRecorder-0.1.0-beta.2-osx-arm64.dmg` and the same name with `.sha256`
- a `.zip` of `PowerShell Session Recorder.app` for each architecture, with a matching `.sha256`
- the validated `.app` directory under `artifacts/osx-x64/` and `artifacts/osx-arm64/`

The checksum line names the file that is actually written. On macOS, the script signs the completed `.app` only after the executable, `Info.plist`, icon, and native libraries are in place. Nested Mach-O files are signed first, then the bundle is signed as `com.timewizard.powershellsessionrecorder`. `codesign --verify --deep --strict --verbose=2` must succeed for the bundle, the zip, and the mounted disk image before the checksums are written. `hdiutil` then creates a compressed disk image and `hdiutil verify` checks it. On a machine without `codesign`, the script does not produce that signature. `xorriso` can still wrap a Rock Ridge ISO in an uncompressed UDIF `.dmg` for local inspection. That fallback was checked on Ubuntu by reading the image back. It was not mounted on a Mac from this checkout.

The bundle identifier is `com.timewizard.powershellsessionrecorder`. The display name is PowerShell Session Recorder. `CFBundleShortVersionString` is `0.1.0`, and the Get Info string includes `0.1.0-beta.2`. The icon is `assets/branding/macos/AppIcon.icns`, generated from `assets/branding/app-icon-source.png`.

PowerShell 7 is not inside the bundle. Terminal.app is part of macOS and is not installed by this package.

## Gatekeeper

The ad-hoc signature lets macOS check the bundle. It does not make the app a trusted Developer ID application. Gatekeeper will warn the first time it is opened. Leave Gatekeeper enabled.

1. Open the `.dmg` and drag **PowerShell Session Recorder** to Applications, or copy the `.app` out of the zip. Replacing an existing bundle replaces only that bundle. Settings, history, and transcripts stay in the user profile and in the transcript directory.
2. Control-click the app, choose **Open**, then choose **Open** again. This allows this one app. It does not disable Gatekeeper.
3. To clear the quarantine attribute on this copy only:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Do not run `sudo spctl --master-disable`. That would turn Gatekeeper off for every application.

The same instructions are in `GATEKEEPER.txt` on the disk image. An Intel Mac, including the Sequoia 15.7.9 virtual machine, needs the `osx-x64` image. The `osx-arm64` image is a separate build and has not been run on Apple Silicon hardware.

## What remains manual

Opening the window, detecting PowerShell 7.6.8, recording in Terminal.app, and deleting a finished transcript still need the Intel virtual machine. A passing package check does not launch the application.
