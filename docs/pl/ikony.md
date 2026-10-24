# Ikony

Zatwierdzona grafika to `assets/branding/app-icon-source.png`. Nie zastępuj tego pliku innym znakiem. Pliki pochodne powstają z niego:

```bash
python3 packaging/icons/generate-icons.py
```

Skrypt używa ponownego próbkowania Lanczos i zachowuje kanał alfa. Nie zmienia pliku źródłowego.

## Pliki pochodne

- `assets/branding/app-icon.ico` — ikona Windows z obrazami 16, 24, 32, 48, 64, 128 i 256 px.
- `packaging/ubuntu/icons/hicolor/<rozmiar>/apps/powershell-session-recorder.png` — 16, 22, 24, 32, 48, 64, 128, 256 i 512 px.
- `src/PowerShellSessionRecorder.App/Assets/app-icon.png` — obraz 256 px używany przez okno Avalonia.
- `assets/branding/macos/app-icon-1024.png` — obraz 1024 px używany przy budowie ikony macOS.
- `assets/branding/macos/AppIcon.icns` — ikona macOS utworzona z tego samego źródła, ze standardowymi obrazami PNG od 16 px do 1024 px.

Nie ma skalowalnego pliku SVG. Poprzedni SVG był innym znakiem zastępczym, a środowiska pulpitu wybierały go przed plikami PNG.

## Windows

`ApplicationIcon` w projekcie aplikacji osadza `app-icon.ico` w `PowerShellSessionRecorder.exe`. Eksplorator, pasek zadań oraz skróty menu Start i pulpitu używają ikony tego pliku. Instalacja dla użytkownika i dla wszystkich użytkowników wskazuje skróty na ten sam plik. `SetupIconFile` nadaje instalatorowi Inno Setup tę samą grafikę.

Przebuduj instalator w Windows, z dowolnego katalogu roboczego:

```powershell
packaging\windows\publish-win-x64.sh
packaging\windows\build-setup.ps1
```

Skrypt znajduje `ISCC.exe` w `PATH` oraz w Program Files i Program Files (x86), w tym `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`. Inną kopię wskaż przez `-CompilerPath` albo `INNO_SETUP_COMPILER`. Wynik pozostaje `artifacts\PowerShellSessionRecorder-0.1.0-beta.2-win-x64-setup.exe`.

Po instalacji skrót menu Start, opcjonalny skrót pulpitu i uruchomione okno powinny pokazywać niebieski kafelek rejestratora. Z Ubuntu tego nie sprawdzono wizualnie.

## Linux

Pakiet `.deb` instaluje pliki PNG hicolor w `/usr/share/icons/hicolor`, a wpis pulpitu używa `Icon=powershell-session-recorder`. Skrypt `postinst` odświeża pamięć podręczną ikon hicolor, gdy dostępne jest `gtk-update-icon-cache`.

Archiwum dla użytkownika kopiuje te pliki PNG do `~/.local/share/icons/hicolor` jako `powershell-session-recorder-user.png`. Jego wpis pulpitu używa tej nazwy, więc nie zastępuje systemowej nazwy ikony. Dezinstalacja usuwa tylko te nazwane pliki.

Ikona okna to zasób Avalonia. `StartupWMClass=PowerShellSessionRecorder` pozwala dokowi powiązać okno z wpisem pulpitu. Uruchomienia aplikacji i sprawdzenia ikony w doku nie wykonano w tym etapie.
