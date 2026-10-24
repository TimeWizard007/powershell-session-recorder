# Icons

The approved artwork is `assets/branding/app-icon-source.png`. Do not replace that file with a different design. Derived files are produced from it:

```bash
python3 packaging/icons/generate-icons.py
```

The script uses Lanczos resampling and keeps the alpha channel. It does not modify the source.

## Generated files

- `assets/branding/app-icon.ico` — Windows icon with 16, 24, 32, 48, 64, 128, and 256 px images.
- `packaging/ubuntu/icons/hicolor/<size>/apps/powershell-session-recorder.png` — 16, 22, 24, 32, 48, 64, 128, 256, and 512 px.
- `src/PowerShellSessionRecorder.App/Assets/app-icon.png` — 256 px image used by the Avalonia window.
- `assets/branding/macos/app-icon-1024.png` — 1024 px image used while building the macOS icon.
- `assets/branding/macos/AppIcon.icns` — macOS icon generated from the same source, with the standard PNG sizes from 16 px through 1024 px.

There is no scalable SVG. The previous SVG was a different placeholder, and desktop environments preferred it over the PNG sizes.

## Windows

`ApplicationIcon` in the application project embeds `app-icon.ico` in `PowerShellSessionRecorder.exe`. Explorer, the taskbar, and both Start Menu and desktop shortcuts use that executable icon. Per-user and all-users installs point their shortcuts at the same executable. `SetupIconFile` gives the Inno Setup installer the same artwork.

Rebuild the installer on Windows, from any working directory:

```powershell
packaging\windows\publish-win-x64.sh
packaging\windows\build-setup.ps1
```

The script finds `ISCC.exe` on `PATH` and under Program Files and Program Files (x86), including `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`. Pass `-CompilerPath` or set `INNO_SETUP_COMPILER` to use another copy. The output remains `artifacts\PowerShellSessionRecorder-0.1.0-beta.2-win-x64-setup.exe`.

After installation, the Start Menu shortcut, an optional desktop shortcut, and the running window should show the blue recorder tile. This was not visually checked from Ubuntu.

## Linux

The `.deb` installs the hicolor PNGs to `/usr/share/icons/hicolor` and the desktop entry uses `Icon=powershell-session-recorder`. A package `postinst` refreshes the hicolor icon cache when `gtk-update-icon-cache` is available.

The per-user archive copies those PNGs into `~/.local/share/icons/hicolor` as `powershell-session-recorder-user.png`. Its desktop entry uses that name, so it does not replace the system icon name. Uninstall removes only those named files.

The window icon is the Avalonia asset. `StartupWMClass=PowerShellSessionRecorder` lets the dock match the desktop entry. Opening the application and confirming the dock icon was not done in this checkpoint.
