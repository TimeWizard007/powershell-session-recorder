# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Version 1.0.0 has not been released.

## [Unreleased]

Prepared `0.1.0-beta.2`. It is not published. The published GitHub release remains `0.1.0-beta.1`.

### Added

- macOS bundles for Intel (`osx-x64`) and Apple Silicon (`osx-arm64`): a self-contained `.app`, a `.dmg`, and SHA-256 files whose names match those artifacts. On macOS the completed bundle is ad-hoc signed after `Info.plist` and the native libraries are in place, then checked with `codesign --verify --deep --strict`. The Apple Silicon runner then starts the signed `osx-arm64` bundle with `--smoke`, which initializes the .NET host and Avalonia without opening a window. That signature is not a Developer ID signature and the packages are not notarized. Gatekeeper stays enabled. The release workflow does not publish them yet.
- GitHub Actions CI for pull requests and `main`, and a manual release workflow that builds the Ubuntu `.deb`, Linux `.tar.gz`, and Windows installer from one tag. Publishing a GitHub Release stays behind an environment approval and does not run on push. The CI workflow also builds and tests on `macos-15`.
- Stable and Beta update channels. Stable stays the default and still reads GitHub’s latest stable release. Beta compares published stable and prerelease tags, skips drafts, and looks at most three pages of 100 releases. A lower version is not offered. Changing the channel clears the cached result. Nothing is downloaded or installed.
- A self-contained Linux x64 `.tar.gz` with `install-user.sh` and `uninstall-user.sh`. The default directory is `~/.local/opt/powershell-session-recorder`. The desktop entry is separate from the system `.deb`. User settings, customers, history, and transcripts are not removed.
- Inno Setup choices for a per-user install, the default, and an all-users install. The script was not compiled on Ubuntu.

### Fixed

- macOS bundles are ad-hoc signed after the `.app` is assembled. Managed assemblies live in `Contents/Resources` and Mach-O files stay in `Contents/MacOS`, because `codesign --verify --deep --strict` treats every file under `Contents/MacOS` as nested code. The host executable is bound to `../Resources/PowerShellSessionRecorder.dll`. Nested Mach-O files are signed first, then the bundle identifier `com.timewizard.powershellsessionrecorder` is sealed. This is internal testing only and is not notarization.
- Checkpoint 5.2.2 does not ship sample customers, TAGs, history, or transcripts. A new profile starts empty, and an existing catalog is left unchanged on upgrade. Package builds refuse `settings.json`, `customers.json`, and `history.json`.
- Checkpoint 5.2.1 uses the approved icon from `assets/branding/app-icon-source.png` for the window, the Windows executable, the Inno Setup installer, and the Linux hicolor icons. The Windows build script looks for Inno Setup 6 under Program Files, including `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.
- Checkpoint 5.1 writes the Windows bootstrap script as UTF-8 with a BOM so Windows PowerShell 5.1 can parse Polish text. Linux scripts stay UTF-8 without a BOM.
- Checkpoint 5.1 sizes the window to the monitor working area, starting at 960×640 device-independent pixels when that fits, and lets action buttons wrap instead of using a fixed 1120×780 window.

### Added

- Checkpoint 5 Windows console: PowerShell 7 and Windows PowerShell 5.1 can be selected on Windows 10/11 x64. Start Session opens a separate visible console that is not embedded in the application. Optional elevation uses the standard User Account Control prompt for that console only. Canceling the prompt does not start a recording and does not write History.
- Windows configuration is `%LOCALAPPDATA%\PowerShellSessionRecorder`. Transcript deletion on Windows refuses reparse points, directories, and unfinished transcripts, and it does not use a command shell.
- A self-contained `win-x64` publish script and an Inno Setup script. The installer was not built or tested on Windows. The published Ubuntu beta remains `0.1.0-beta.1`. The prepared informational version is `0.1.0-beta.2`.

## [0.1.0-beta.1] - 2026-10-19

First public Ubuntu 24.04 amd64 beta. About and the GitHub update checker report `0.1.0-beta.1`. The Debian package version is `0.1.0~beta.1-1`. The checker reads the latest stable GitHub release and does not offer this beta, or any later beta, as an update. Release notes: [docs/en/release-notes-0.1.0-beta.1.md](docs/en/release-notes-0.1.0-beta.1.md).

### Added

- Checkpoint 4.1 customer catalog: saved TAGs in `customers.json`, with add, edit, search, and removal that leaves History and transcript files unchanged. New Session does not select a customer until one is chosen or created.
- Checkpoint 4.1 history cleanup: remove entries from History only, or also delete a transcript file after its footer has been read from that file. Bulk actions apply to the entries currently shown.
- Checkpoint 4 Ubuntu package: a self-contained `linux-x64` `.deb` for Ubuntu 24.04, with a desktop entry and icons. PowerShell 7 and GNOME Terminal are not bundled.
- Checkpoint 4 history label: a confirmed start is shown as “Recording started (current state unknown)”, with the same meaning in Polish. The stored value remains `RecordingConfirmed`.
- Checkpoint 3 session history: a local `history.json` index of confirmed sessions, with search, filters, and actions to open the transcript, open its folder, or copy its path.
- Checkpoint 2.2 update check: a manual or once-daily request to the official GitHub Releases API. A newer stable release is shown in the window. The application does not download, install, or restart itself.
- Checkpoint 2.1 interface: vertical navigation, System/Light/Dark themes, an About page, and dependency checks for PowerShell 7 and GNOME Terminal.
- Checkpoint 2 recording flow: Start Session launches an independent GNOME Terminal running PowerShell 7 with `Start-Transcript`.
- Exclusive transcript allocation, a fixed bootstrap script, and English and Polish launch status messages.
- Checkpoint 1 application foundation for .NET 10 and Avalonia UI.
- Core models for session configuration, application settings, and PowerShell version selection.
- Windows-safe transcript filename sanitization and unique filename generation.
- English and Polish interface resources.
- Per-user JSON settings for language, log directory, PowerShell version, filename options, and recent customer TAGs.
- Automated tests for filenames, validation, and settings.
- Bilingual project documentation.

### Limitations

- Windows PowerShell 5.1 and privilege elevation are not implemented.
- History lists only sessions recorded with **Save session in history** checked after this feature. It does not import older transcripts and does not show a live Active state.
- The `.deb` was not tested on distributions other than Ubuntu 24.04 LTS x64.
- Only GNOME Terminal on Ubuntu was tested.
- Abrupt termination can leave a transcript without its footer. That file can be dropped from History and is not deleted. A footer confirms transcript completion, not that every related process has exited.

## [Niewydane]

Przygotowano `0.1.0-beta.2`. To wydanie nie jest opublikowane. Opublikowane wydanie GitHub pozostaje `0.1.0-beta.1`.

### Dodano

- GitHub Actions CI dla żądań ściągnięcia i gałęzi `main` oraz ręczny przepływ wydania, który buduje pakiet Ubuntu `.deb`, archiwum Linux `.tar.gz` i instalator Windows z jednego znacznika. Publikacja wydania GitHub wymaga zatwierdzenia środowiska i nie uruchamia się po wypchnięciu.
- Kanały aktualizacji: stabilny i testowy. Stabilny pozostaje domyślny i nadal czyta najnowsze stabilne wydanie GitHub. Kanał testowy porównuje opublikowane wersje stabilne i przedpremierowe, pomija szkice i przegląda najwyżej trzy strony po 100 wydań. Niższa wersja nie jest proponowana. Zmiana kanału czyści zapisany wynik. Nic nie jest pobierane ani instalowane.
- Samodzielne archiwum Linux x64 `.tar.gz` ze skryptami `install-user.sh` i `uninstall-user.sh`. Domyślny katalog to `~/.local/opt/powershell-session-recorder`. Wpis pulpitu jest oddzielny od systemowego pakietu `.deb`. Ustawienia, klienci, historia i transkrypcje nie są usuwane.
- Wybór Inno Setup: instalacja dla bieżącego użytkownika, która jest domyślna, oraz instalacja dla wszystkich użytkowników. Skryptu nie skompilowano w Ubuntu.

### Naprawiono

- Etap 5.2.2 nie dołącza przykładowych klientów, TAG-ów, historii ani transkrypcji. Nowy profil startuje pusty, a istniejący katalog zostaje bez zmian przy aktualizacji. Kompilacja pakietów odrzuca `settings.json`, `customers.json` i `history.json`.
- Etap 5.2.1 używa zatwierdzonej ikony z `assets/branding/app-icon-source.png` dla okna, pliku wykonywalnego Windows, instalatora Inno Setup i ikon hicolor w Linuksie. Skrypt kompilacji Windows szuka Inno Setup 6 w Program Files, także w `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.
- Etap 5.1 zapisuje skrypt startowy Windows jako UTF-8 ze znacznikiem BOM, żeby Windows PowerShell 5.1 poprawnie odczytał polski tekst. Skrypty w Linuksie pozostają UTF-8 bez znacznika BOM.
- Etap 5.1 dopasowuje okno do obszaru roboczego monitora. Gdy się mieści, startuje w 960×640 pikselach niezależnych od urządzenia, a przyciski akcji zawijają się zamiast wymuszać okno 1120×780.

### Dodano

- Konsola Windows etapu 5: w Windows 10/11 x64 można wybrać program PowerShell 7 albo Windows PowerShell 5.1. Uruchom sesję otwiera osobną widoczną konsolę, która nie jest osadzona w aplikacji. Opcjonalne podniesienie uprawnień używa standardowego pytania Kontroli konta użytkownika tylko dla tej konsoli. Anulowanie pytania nie uruchamia rejestrowania i nie zapisuje Historii.
- Konfiguracja Windows to `%LOCALAPPDATA%\PowerShellSessionRecorder`. Kasowanie transkrypcji w Windows odrzuca punkty ponownej analizy, katalogi i niedokończone transkrypcje oraz nie używa powłoki poleceń.
- Skrypt samodzielnej publikacji `win-x64` i skrypt Inno Setup. Instalator nie został zbudowany ani sprawdzony w Windows. Opublikowana beta Ubuntu pozostaje `0.1.0-beta.1`. Przygotowana wersja informacyjna to `0.1.0-beta.2`.

## [0.1.0-beta.1] - 2026-10-19

Pierwsza publiczna beta dla Ubuntu 24.04 amd64. Strona O programie i sprawdzanie aktualizacji GitHub podają `0.1.0-beta.1`. Wersja pakietu Debiana to `0.1.0~beta.1-1`. Sprawdzanie czyta najnowsze stabilne wydanie GitHub i nie proponuje tej bety ani późniejszej bety jako aktualizacji. Informacje o wydaniu: [docs/en/release-notes-0.1.0-beta.1.md](docs/en/release-notes-0.1.0-beta.1.md).

### Dodano

- Katalog klientów etapu 4.1: zapisane TAG-i w pliku `customers.json`, z dodawaniem, edycją, wyszukiwaniem i usuwaniem, które nie zmienia Historii ani plików transkrypcji. Nowa sesja nie wybiera klienta, dopóki użytkownik go nie wskaże albo nie utworzy.
- Czyszczenie historii etapu 4.1: wpis można usunąć tylko z Historii albo także skasować plik transkrypcji po odczytaniu jego stopki. Działania zbiorcze obejmują wpisy aktualnie widoczne.
- Pakiet Ubuntu etapu 4: samodzielny plik `.deb` `linux-x64` dla Ubuntu 24.04, z wpisem pulpitu i ikonami. PowerShell 7 i GNOME Terminal nie są dołączone.
- Etykieta historii etapu 4: potwierdzony start jest pokazywany jako „Nagrywanie uruchomione (obecny stan nieznany)”. Zapisana wartość to nadal `RecordingConfirmed`.
- Historia sesji etapu 3: lokalny indeks `history.json` potwierdzonych sesji, z wyszukiwaniem, filtrami oraz otwarciem transkrypcji, otwarciem folderu i kopiowaniem ścieżki.
- Sprawdzanie aktualizacji etapu 2.2: ręczne albo codzienne zapytanie do oficjalnego API wydań GitHub. Nowsze stabilne wydanie jest pokazywane w oknie. Aplikacja nie pobiera, nie instaluje i nie uruchamia się ponownie.
- Interfejs etapu 2.1: pionowa nawigacja, motywy systemowy, jasny i ciemny, strona O programie oraz sprawdzenie zależności programu PowerShell 7 i GNOME Terminal.
- Przepływ rejestrowania etapu 2: Uruchom sesję otwiera niezależny GNOME Terminal z programem PowerShell 7 i `Start-Transcript`.
- Wyłączną rezerwację pliku transkrypcji, stały skrypt startowy oraz angielskie i polskie komunikaty uruchomienia.
- Fundament etapu 1 dla .NET 10 i Avalonia UI.
- Modele konfiguracji sesji, ustawień aplikacji i wyboru wersji programu PowerShell.
- Oczyszczanie nazw plików transkrypcji zgodne z systemem Windows oraz generowanie unikalnych nazw.
- Zasoby interfejsu w języku angielskim i polskim.
- Ustawienia JSON bieżącego użytkownika: język, katalog dziennika, wersja programu PowerShell, opcje nazwy pliku i ostatnie TAG-i klientów.
- Testy automatyczne nazw plików, walidacji i ustawień.
- Dwujęzyczną dokumentację projektu.

### Ograniczenia

- Windows PowerShell 5.1 i podnoszenie uprawnień nie są zaimplementowane.
- Historia pokazuje tylko sesje zarejestrowane z zaznaczoną opcją **Zapisz sesję w historii** po wprowadzeniu tej funkcji. Nie importuje starszych transkrypcji i nie pokazuje stanu Aktywna.
- Pakietu `.deb` nie sprawdzano na dystrybucjach innych niż Ubuntu 24.04 LTS x64.
- Sprawdzono tylko GNOME Terminal w systemie Ubuntu.
- Nagłe przerwanie procesu może zostawić transkrypcję bez stopki. Taki plik można usunąć z Historii, ale nie jest kasowany. Stopka potwierdza zakończenie transkrypcji, a nie zakończenie każdego powiązanego procesu.
