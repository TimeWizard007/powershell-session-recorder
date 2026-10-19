# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Version 1.0.0 has not been released.

## [Unreleased]

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
