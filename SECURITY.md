# Security

[Polski](#bezpieczeństwo)

PowerShell Session Recorder is a local desktop application. It does not elevate privileges. The only network request it makes for itself is an optional HTTPS read of the latest stable GitHub release for this repository. That request sends the application version in the User-Agent and does not send customer TAGs, session names, transcripts, history entries, log paths, or other settings. The response is not executed, and release files are not downloaded.

## Transcripts

`Start-Transcript` records the text the PowerShell host displays, including commands and their output. A password, token, or other secret typed or printed in the recorded console can be written to the transcript file. Treat transcript files as sensitive.

The bootstrap script is stored outside the log directory, in `$XDG_RUNTIME_DIR/powershell-session-recorder` when that directory is available, otherwise under `~/.local/share/powershell-session-recorder/bootstrap`. On Unix the application limits that directory to the current user and each script to owner read and write. The script is a fixed file. Customer TAGs, session names, and paths are passed as separate process arguments. The application does not use `Invoke-Expression` to build the launch command, and it does not change the PowerShell profile, the registry, or system policy.

A normal `exit` runs `Stop-Transcript`. If the terminal or process is killed, PowerShell may not write the transcript footer. Commands that were already flushed can still be present.

The reserved transcript file is limited to the current user on Unix when it is created. The bootstrap sets that mode again after `Start-Transcript` starts, so a transcript created under the process umask is not left group-readable. The selected log directory itself is not chmod'd.

## Data stored on the computer

Settings are written as JSON in the current user's application directory:

- Linux: `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` or `~/.config/powershell-session-recorder/settings.json`
- Windows: `%APPDATA%\PowerShellSessionRecorder\settings.json`
- macOS: `~/Library/Application Support/PowerShellSessionRecorder/settings.json`

The file may contain a log directory and customer TAGs. It must not contain passwords, tokens, or customer secrets. On Unix systems the application attempts to limit the settings file to the current user.

An unreadable settings file is preserved and copied once to `settings.invalid.json` in the same directory.

Session history is `history.json` in the same directory. It records the session id, customer TAG, session name, transcript file name, absolute transcript path, start time in UTC, PowerShell version, and a verified recording status. An end time is stored only after the transcript footer has been read. The file does not contain the transcript text, passwords, or other console output. On Unix the application limits `history.json` to the current user. Removing or changing a history entry is not part of this version, and the application does not delete transcript files when it updates history metadata.

A history file that cannot be parsed is renamed to `history.invalid-<timestamp>-<n>.json` and left for inspection. A file written by a newer schema is left unchanged. History is not sent to GitHub or any other service. The application does not scan log directories and does not import transcripts that were recorded with history disabled or before this feature.

## Reporting a vulnerability

Please report a security issue through GitHub private vulnerability reporting on [TimeWizard007/powershell-session-recorder](https://github.com/TimeWizard007/powershell-session-recorder). Do not open a public issue for an exploitable vulnerability.

Include the affected version or commit, the operating system, and a description of the impact. Please do not include secrets or customer data in the report.

## Bezpieczeństwo

Rejestrator sesji PowerShell jest lokalną aplikacją pulpitową. Nie podnosi uprawnień. Jedynym własnym żądaniem sieciowym jest opcjonalny odczyt HTTPS najnowszego stabilnego wydania tego repozytorium w serwisie GitHub. Żądanie wysyła wersję aplikacji w nagłówku User-Agent i nie wysyła TAG-ów klientów, nazw sesji, transkrypcji, wpisów historii, ścieżek dziennika ani innych ustawień. Odpowiedź nie jest wykonywana, a pliki wydania nie są pobierane.

### Transkrypcje

`Start-Transcript` zapisuje tekst wyświetlany przez host programu PowerShell, w tym polecenia i ich wyniki. Hasło, token albo inna dana poufna wpisana lub wypisana w rejestrowanej konsoli może trafić do pliku transkrypcji. Pliki transkrypcji należy traktować jako dane wrażliwe.

Skrypt startowy jest przechowywany poza katalogiem dziennika, w `$XDG_RUNTIME_DIR/powershell-session-recorder`, gdy ten katalog jest dostępny, a w przeciwnym razie w `~/.local/share/powershell-session-recorder/bootstrap`. W systemach uniksowych aplikacja ogranicza ten katalog do bieżącego użytkownika, a każdy skrypt do odczytu i zapisu właściciela. Skrypt jest stałym plikiem. TAG-i klientów, nazwy sesji i ścieżki są przekazywane jako osobne argumenty procesu. Aplikacja nie używa `Invoke-Expression` do zbudowania polecenia uruchomienia i nie zmienia profilu programu PowerShell, rejestru ani zasad systemu.

Zwykłe polecenie `exit` wywołuje `Stop-Transcript`. Jeśli terminal albo proces zostanie zabity, program PowerShell może nie zapisać stopki transkrypcji. Polecenia, które zostały już zrzucone na dysk, mogą nadal być w pliku.

Zarezerwowany plik transkrypcji jest w systemach uniksowych ograniczony do bieżącego użytkownika w chwili utworzenia. Skrypt startowy ustawia te uprawnienia ponownie po uruchomieniu `Start-Transcript`, więc transkrypcja utworzona zgodnie z maską procesu nie pozostaje czytelna dla grupy. Wybrany katalog dziennika nie ma zmienianych uprawnień.

### Dane przechowywane na komputerze

Ustawienia są zapisywane jako JSON w katalogu aplikacji bieżącego użytkownika:

- Linux: `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` lub `~/.config/powershell-session-recorder/settings.json`
- Windows: `%APPDATA%\PowerShellSessionRecorder\settings.json`
- macOS: `~/Library/Application Support/PowerShellSessionRecorder/settings.json`

Plik może zawierać katalog dziennika i TAG-i klientów. Nie może zawierać haseł, tokenów ani poufnych danych klientów. W systemach uniksowych aplikacja próbuje ograniczyć dostęp do pliku ustawień do bieżącego użytkownika.

Nieczytelny plik ustawień zostaje zachowany i jest jednorazowo kopiowany do `settings.invalid.json` w tym samym katalogu.

Historia sesji to plik `history.json` w tym samym katalogu. Zapisuje identyfikator sesji, TAG klienta, nazwę sesji, nazwę pliku transkrypcji, bezwzględną ścieżkę transkrypcji, czas rozpoczęcia w UTC, wersję programu PowerShell i zweryfikowany stan rejestrowania. Czas zakończenia jest zapisywany dopiero po odczytaniu stopki transkrypcji. Plik nie zawiera treści transkrypcji, haseł ani innego wyjścia konsoli. W systemach uniksowych aplikacja ogranicza `history.json` do bieżącego użytkownika. Ta wersja nie usuwa wpisów historii i nie usuwa plików transkrypcji przy zmianie metadanych historii.

Plik historii, którego nie można odczytać, jest zmieniany na `history.invalid-<znacznik-czasu>-<n>.json` i pozostaje do wglądu. Plik zapisany nowszym schematem pozostaje niezmieniony. Historia nie jest wysyłana do serwisu GitHub ani do żadnej innej usługi. Aplikacja nie przegląda katalogów dziennika i nie importuje transkrypcji zarejestrowanych bez historii albo przed tą funkcją.

### Zgłaszanie podatności

Zgłoś problem bezpieczeństwa przez prywatne zgłaszanie podatności w serwisie GitHub dla repozytorium [TimeWizard007/powershell-session-recorder](https://github.com/TimeWizard007/powershell-session-recorder). Nie otwieraj publicznego zgłoszenia dla podatności, którą można wykorzystać.

Podaj wersję lub commit, system operacyjny i opis skutków. Nie dołączaj do zgłoszenia danych poufnych ani danych klientów.
