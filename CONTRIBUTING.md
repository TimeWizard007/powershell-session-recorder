# Contributing

[Polski](#współtworzenie)

Thank you for helping with PowerShell Session Recorder. This source tree is version 1.0.0. Please keep changes focused.

## Development setup

1. Install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/).
2. Clone the repository.
3. Build and test from the repository root:

```bash
dotnet build PowerShellSessionRecorder.sln
dotnet test PowerShellSessionRecorder.sln
```

4. Run the application on a machine with a desktop session:

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

## Project layout

- `src/PowerShellSessionRecorder.Core` contains platform-independent models and rules.
- `src/PowerShellSessionRecorder.App` contains the Avalonia application.
- `tests/PowerShellSessionRecorder.Tests` contains automated tests.
- `packaging/` contains the Ubuntu `.deb`, Linux archive, Windows installer, and macOS bundle scripts. Package outputs belong in `artifacts/` and are not committed.
- Visible interface text belongs in `src/PowerShellSessionRecorder.App/Localization/en-US.json` and `pl-PL.json`.

## Localization

Add a key to `Localization/L.cs` and to both JSON catalogs. Polish text must use Polish characters. The localization tests fail when a key is missing, unused, or when the Polish catalog drops its diacritics.

## Settings and secrets

Do not store passwords, tokens, or customer secrets in settings or in the repository. Runtime settings belong in the per-user application directory, not in the source tree.

## Pull requests

- Describe the user-visible behavior.
- Add or update tests for Core changes.
- Do not commit `bin/`, `obj/`, local settings, transcripts, or secrets.
- Do not claim that a released version exists unless the changelog has a version heading.

## Współtworzenie

Dziękujemy za pomoc przy Rejestratorze sesji PowerShell. To drzewo źródeł ma wersję 1.0.0. Zmiany powinny pozostać skupione.

### Środowisko

1. Zainstaluj [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/).
2. Sklonuj repozytorium.
3. Zbuduj i uruchom testy w katalogu głównym:

```bash
dotnet build PowerShellSessionRecorder.sln
dotnet test PowerShellSessionRecorder.sln
```

4. Uruchom aplikację na komputerze z sesją pulpitu:

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

### Układ projektu

- `src/PowerShellSessionRecorder.Core` zawiera niezależne od platformy modele i reguły.
- `src/PowerShellSessionRecorder.App` zawiera aplikację Avalonia.
- `tests/PowerShellSessionRecorder.Tests` zawiera testy automatyczne.
- `packaging/` zawiera skrypty pakietu Ubuntu `.deb`, archiwum Linuksa, instalatora Windows i pakietów macOS. Wyniki należą do `artifacts/` i nie wchodzą do commita.
- Widoczny tekst interfejsu należy do plików `src/PowerShellSessionRecorder.App/Localization/en-US.json` i `pl-PL.json`.

### Lokalizacja

Dodaj klucz do `Localization/L.cs` i do obu katalogów JSON. Polski tekst musi zawierać polskie znaki. Testy lokalizacji kończą się niepowodzeniem, gdy klucza brakuje, gdy nie jest używany albo gdy polski katalog traci znaki diakrytyczne.

### Ustawienia i dane poufne

Nie zapisuj haseł, tokenów ani poufnych danych klientów w ustawieniach ani w repozytorium. Ustawienia środowiska wykonawczego należą do katalogu danych użytkownika, a nie do drzewa źródeł.

### Wnioski o scalenie

- Opisz zachowanie widoczne dla użytkownika.
- Dodaj lub zaktualizuj testy przy zmianach w projekcie Core.
- Nie commituj katalogów `bin/` i `obj/`, lokalnych ustawień, transkrypcji ani danych poufnych.
- Nie twierdź, że wersja została wydana, jeśli dziennik zmian nie ma nagłówka tej wersji.
