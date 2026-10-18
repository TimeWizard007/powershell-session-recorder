# Security

[Polski](#bezpieczeństwo)

PowerShell Session Recorder is a local desktop application. Checkpoint 1 does not open network connections of its own, does not launch PowerShell, and does not elevate privileges.

## Data stored on the computer

Settings are written as JSON in the current user's application directory:

- Linux: `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` or `~/.config/powershell-session-recorder/settings.json`
- Windows: `%APPDATA%\PowerShellSessionRecorder\settings.json`
- macOS: `~/Library/Application Support/PowerShellSessionRecorder/settings.json`

The file may contain a log directory and customer TAGs. It must not contain passwords, tokens, or customer secrets. On Unix systems the application attempts to limit the settings file to the current user.

An unreadable settings file is preserved and copied once to `settings.invalid.json` in the same directory.

## Reporting a vulnerability

Please report a security issue through GitHub private vulnerability reporting on [TimeWizard007/powershell-session-recorder](https://github.com/TimeWizard007/powershell-session-recorder). Do not open a public issue for an exploitable vulnerability.

Include the affected version or commit, the operating system, and a description of the impact. Please do not include secrets or customer data in the report.

## Bezpieczeństwo

Rejestrator sesji PowerShell jest lokalną aplikacją pulpitową. Etap 1 nie otwiera własnych połączeń sieciowych, nie uruchamia programu PowerShell i nie podnosi uprawnień.

### Dane przechowywane na komputerze

Ustawienia są zapisywane jako JSON w katalogu aplikacji bieżącego użytkownika:

- Linux: `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` lub `~/.config/powershell-session-recorder/settings.json`
- Windows: `%APPDATA%\PowerShellSessionRecorder\settings.json`
- macOS: `~/Library/Application Support/PowerShellSessionRecorder/settings.json`

Plik może zawierać katalog dziennika i TAG-i klientów. Nie może zawierać haseł, tokenów ani poufnych danych klientów. W systemach uniksowych aplikacja próbuje ograniczyć dostęp do pliku ustawień do bieżącego użytkownika.

Nieczytelny plik ustawień zostaje zachowany i jest jednorazowo kopiowany do `settings.invalid.json` w tym samym katalogu.

### Zgłaszanie podatności

Zgłoś problem bezpieczeństwa przez prywatne zgłaszanie podatności w serwisie GitHub dla repozytorium [TimeWizard007/powershell-session-recorder](https://github.com/TimeWizard007/powershell-session-recorder). Nie otwieraj publicznego zgłoszenia dla podatności, którą można wykorzystać.

Podaj wersję lub commit, system operacyjny i opis skutków. Nie dołączaj do zgłoszenia danych poufnych ani danych klientów.
