# macOS packages

[Polski](../pl/macos.md)

These packages are the ad-hoc signed `1.0.0` builds. The signature is not a Developer ID signature. They are not notarized. Previously published beta releases are not changed by this packaging. The manual release workflow uploads these macOS files only when it builds tag `v1.0.0`.

## Build

From a checkout with the .NET SDK 10:

```bash
sh packaging/macos/build-dmg.sh all
```

`osx-x64` or `osx-arm64` builds one architecture. The script publishes a self-contained .NET 10 application, including the Avalonia native libraries, and removes the temporary publish directory when it finishes. It does not read or write `~/.config/powershell-session-recorder`, and it does not copy an existing application out of `/Applications`.

Outputs under `artifacts/`, which is gitignored:

- `PowerShellSessionRecorder-1.0.0-osx-x64.dmg` and the same name with `.sha256`
- `PowerShellSessionRecorder-1.0.0-osx-arm64.dmg` and the same name with `.sha256`
- `PowerShellSessionRecorder-1.0.0-osx-x64.zip` and `PowerShellSessionRecorder-1.0.0-osx-arm64.zip`, each with a matching `.sha256`
- the validated `.app` directory under `artifacts/osx-x64/` and `artifacts/osx-arm64/`

The checksum line names the file that is actually written. On macOS, the script signs the completed `.app` only after the executable, `Info.plist`, icon, and native libraries are in place. Nested Mach-O files are signed first, then the bundle is signed as `com.timewizard.powershellsessionrecorder`. `codesign --verify --deep --strict --verbose=2` must succeed for the bundle, the zip, and the mounted disk image before the checksums are written. On Apple Silicon the signed `osx-arm64` bundle is then started with `--smoke`. That process does not open a window. `hdiutil` then creates a private read-write image, copies the bundle onto that image's own mount, and converts it to a compressed disk image. `hdiutil verify` checks the result. The shared `-srcfolder` path is not used, because it failed with `Resource busy` after the bundle was signed. On a machine without `codesign`, the script does not produce that signature. `xorriso` can still wrap a Rock Ridge ISO in an uncompressed UDIF `.dmg` for local inspection. That fallback was checked on Ubuntu by reading the image back. It was not mounted on a Mac from this checkout.

The bundle identifier is `com.timewizard.powershellsessionrecorder`. The display name is PowerShell Session Recorder. `CFBundleShortVersionString` is `1.0.0`, and the Get Info string includes `1.0.0`. The icon is `assets/branding/macos/AppIcon.icns`, generated from `assets/branding/app-icon-source.png`. Use the `osx-x64` image or zip on Intel. Use the `osx-arm64` image or zip on Apple Silicon. Open the disk image and drag **PowerShell Session Recorder** to Applications, or copy the `.app` out of the zip.

`Contents/MacOS` contains the native host and Mach-O libraries. Managed assemblies, `PowerShellSessionRecorder.deps.json`, `PowerShellSessionRecorder.runtimeconfig.json`, and the localization catalogs are in `Contents/Resources`. The host is bound to `../Resources/PowerShellSessionRecorder.dll`, and the deps file points native libraries back at `../MacOS`. That split is what lets `codesign --verify --deep --strict` accept the bundle: a managed DLL left in `Contents/MacOS` is treated as unsigned nested code.

The same publish also records an app-relative runtime location of `../MacOS` in the host, and only that location. A relative entry-assembly path makes the application directory `Contents/Resources`. `libhostfxr.dylib` is not in that directory, so the default host search continues with `DOTNET_ROOT` and then a machine-wide .NET install. For a self-contained app, hostpolicy is then required in that install instead of beside `libcoreclr.dylib`. `../MacOS` is resolved from `Contents/Resources`, which is `Contents/MacOS`, where `libhostfxr.dylib`, `libhostpolicy.dylib`, and `libcoreclr.dylib` already are. The application stays self-contained. An Ubuntu checkout cannot execute that Mach-O host. The Apple Silicon CI smoke test has started the signed `osx-arm64` bundle and does not open a window.

PowerShell 7 is not inside the bundle. Terminal.app is part of macOS and is not installed by this package.

## Gatekeeper

The ad-hoc signature lets macOS check the bundle. It does not make the app a trusted Developer ID application. Gatekeeper will warn the first time it is opened. Leave Gatekeeper enabled.

1. Open the `.dmg` and drag **PowerShell Session Recorder** to Applications, or copy the `.app` out of the zip. Replacing an existing bundle replaces only that bundle. Settings, history, and transcripts stay in the user profile and in the transcript directory.
2. Control-click the app, choose **Open**, then choose **Open** again. This allows this one app. It does not disable Gatekeeper.
3. After the SHA-256 file for this download has been checked, an advanced option is to clear the quarantine attribute on this verified copy only:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Do not run `sudo spctl --master-disable`. That would turn Gatekeeper off for every application.

The same instructions are in `GATEKEEPER.txt` on the disk image. An Intel Mac, including the Sequoia 15.7.9 virtual machine, needs the `osx-x64` image. The `osx-arm64` image is a separate build. Its packaged host has been started by the Apple Silicon CI smoke test. That test does not open a window, and the Apple Silicon application window was not functionally tested.

## What remains manual

The Apple Silicon CI smoke test has passed for the signed `osx-arm64` host: it resolves `PowerShellSessionRecorder.dll` from `Contents/Resources`, reads the `net10.0` runtime configuration, loads `libcoreclr`, `libhostfxr`, `libhostpolicy`, Avalonia, Skia, and HarfBuzz from `Contents/MacOS`, and Avalonia registers `Avalonia.Native.AvaloniaNativePlatform`. Avalonia 12 leaves `WindowingSubsystemName` empty for that backend; Skia and HarfBuzz are the subsystems that publish names. The test does not open a window. Intel macOS was functionally tested from the installed `.app`, including PowerShell detection, recording in Terminal.app, transcripts, and history. The Apple Silicon window was not part of that functional test. The `osx-x64` and `osx-arm64` packages are separate builds.
