# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Version 1.0.0 has not been released.

## [Unreleased]

### Added

- Checkpoint 1 application foundation for .NET 10 and Avalonia UI.
- Core models for session configuration, application settings, and PowerShell version selection.
- Windows-safe transcript filename sanitization and unique filename generation.
- English and Polish interface resources.
- Per-user JSON settings for language, log directory, PowerShell version, filename options, and recent customer TAGs.
- Automated tests for filenames, validation, and settings.
- Bilingual project documentation.

### Limitations

- PowerShell sessions are not launched or recorded.
- Privilege elevation and transcript writing are not implemented.
- The Start Session action is disabled.

## [Niewydane]

### Dodano

- Fundament etapu 1 dla .NET 10 i Avalonia UI.
- Modele konfiguracji sesji, ustawień aplikacji i wyboru wersji programu PowerShell.
- Oczyszczanie nazw plików transkrypcji zgodne z systemem Windows oraz generowanie unikalnych nazw.
- Zasoby interfejsu w języku angielskim i polskim.
- Ustawienia JSON bieżącego użytkownika: język, katalog dziennika, wersja programu PowerShell, opcje nazwy pliku i ostatnie TAG-i klientów.
- Testy automatyczne nazw plików, walidacji i ustawień.
- Dwujęzyczną dokumentację projektu.

### Ograniczenia

- Sesje programu PowerShell nie są uruchamiane ani rejestrowane.
- Podnoszenie uprawnień i zapis transkrypcji nie są zaimplementowane.
- Akcja Uruchom sesję jest wyłączona.
