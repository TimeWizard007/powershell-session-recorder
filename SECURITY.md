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

The file may contain a log directory and recent customer TAGs kept for compatibility. It must not contain passwords, tokens, or customer secrets. On Linux the configuration directory is owner-only (`0700`). On Unix the settings file, invalid-settings copy, history file, history lock, history quarantine, customer catalog, customer lock, and customer quarantine are owner read and write (`0600`). A directory or metadata file that is readable by the group or by others is tightened to those modes without replacing its contents or deleting transcripts. A symbolic link in place of that directory or those files is not followed and is not deleted. Windows does not use these Unix modes.

An unreadable settings file is preserved and copied once to `settings.invalid.json` in the same directory.

Session history is `history.json` in the same directory. It records the session id, customer TAG, session name, transcript file name, absolute transcript path, start time in UTC, PowerShell version, and a verified recording status. `RecordingConfirmed` means the transcript header was seen at launch. The History page does not present that state as active or running. An end time is stored only when the transcript footer contains a parseable time. The file does not contain the transcript text, passwords, or other console output. Removing a history entry rewrites this catalog once. It does not delete a transcript unless the user confirms a separate action and the file itself contains the transcript footer `PowerShell transcript end`. That footer means the transcript finished. It does not prove that every related process has exited. The application does not track the live PowerShell process, so a file without that footer is not deleted. A missing file does not block removal of its history entry. If a requested deletion is refused or fails, that history entry stays. Deletion opens the recorded path, rejects symbolic links and directories, and does not scan the log directory or derive a path from the TAG.

A history file that cannot be parsed is renamed to `history.invalid-<timestamp>-<n>.json`, limited to the owner on Unix, and left for inspection. A file written by a newer schema is left unchanged. History is not sent to GitHub or any other service. The application does not scan log directories and does not import transcripts that were recorded with history disabled or before this feature.

Saved customers are `customers.json` in the same directory. Each record is an id, a TAG, an optional display name, and a creation time. The file does not contain credentials or transcript text. It is not filled from History, from recent TAGs, or from file names. Renaming a saved TAG does not rename transcript files or rewrite history. Removing a saved customer does not delete history, transcript files, or processes. A customer catalog that cannot be parsed is renamed to `customers.invalid-<timestamp>-<n>.json`. A newer schema is left unchanged. Customer TAGs are not sent to GitHub.

The Ubuntu `.deb` installs under `/opt` and `/usr`. It does not contain customer settings, history, or transcripts. Removing or purging the package does not delete files in the user profile or the chosen log directory, and it does not stop a PowerShell process that is already running.

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

Plik może zawierać katalog dziennika i ostatnie TAG-i klientów zachowane dla zgodności. Nie może zawierać haseł, tokenów ani poufnych danych klientów. W Linuksie katalog konfiguracji należy tylko do właściciela (`0700`). W systemach uniksowych plik ustawień, kopia niepoprawnych ustawień, plik historii, blokada historii, kwarantanna historii, katalog klientów, blokada katalogu i kwarantanna katalogu mają odczyt i zapis właściciela (`0600`). Katalog albo plik metadanych czytelny dla grupy lub pozostałych użytkowników jest zawężany do tych uprawnień bez podmiany treści i bez usuwania transkrypcji. Dowiązanie symboliczne w miejscu tego katalogu albo tych plików nie jest śledzone i nie jest usuwane. Windows nie używa tych trybów uniksowych.

Nieczytelny plik ustawień zostaje zachowany i jest jednorazowo kopiowany do `settings.invalid.json` w tym samym katalogu.

Historia sesji to plik `history.json` w tym samym katalogu. Zapisuje identyfikator sesji, TAG klienta, nazwę sesji, nazwę pliku transkrypcji, bezwzględną ścieżkę transkrypcji, czas rozpoczęcia w UTC, wersję programu PowerShell i zweryfikowany stan rejestrowania. `RecordingConfirmed` oznacza, że nagłówek transkrypcji był widoczny przy uruchomieniu. Strona Historia nie pokazuje tego stanu jako aktywnego ani trwającego. Czas zakończenia jest zapisywany tylko wtedy, gdy stopka transkrypcji zawiera możliwy do odczytania czas. Plik nie zawiera treści transkrypcji, haseł ani innego wyjścia konsoli. Usunięcie wpisu historii przepisuje ten katalog jeden raz. Nie kasuje transkrypcji, chyba że użytkownik potwierdzi osobną operację, a sam plik zawiera stopkę `PowerShell transcript end`. Ta stopka oznacza, że transkrypcja się zakończyła. Nie dowodzi, że każdy powiązany proces został zakończony. Aplikacja nie śledzi działającego procesu programu PowerShell, więc plik bez tej stopki nie jest kasowany. Brak pliku nie blokuje usunięcia wpisu historii. Jeśli żądane kasowanie zostanie odrzucone albo się nie uda, wpis historii zostaje. Kasowanie otwiera zapisaną ścieżkę, odrzuca dowiązania symboliczne i katalogi oraz nie przegląda katalogu dziennika i nie wyprowadza ścieżki z TAG-u.

Plik historii, którego nie można odczytać, jest zmieniany na `history.invalid-<znacznik-czasu>-<n>.json`, w systemach uniksowych ograniczany do właściciela i pozostaje do wglądu. Plik zapisany nowszym schematem pozostaje niezmieniony. Historia nie jest wysyłana do serwisu GitHub ani do żadnej innej usługi. Aplikacja nie przegląda katalogów dziennika i nie importuje transkrypcji zarejestrowanych bez historii albo przed tą funkcją.

Zapisani klienci to plik `customers.json` w tym samym katalogu. Każdy rekord zawiera identyfikator, TAG, opcjonalną nazwę wyświetlaną i czas utworzenia. Plik nie zawiera poświadczeń ani treści transkrypcji. Nie jest wypełniany z Historii, z ostatnich TAG-ów ani z nazw plików. Zmiana zapisanego TAG-u nie zmienia nazw plików transkrypcji ani historii. Usunięcie klienta nie kasuje historii, plików transkrypcji ani procesów. Katalog klientów, którego nie można odczytać, jest zmieniany na `customers.invalid-<znacznik-czasu>-<n>.json`. Nowszy schemat pozostaje niezmieniony. TAG-i klientów nie są wysyłane do serwisu GitHub.

Pakiet `.deb` dla Ubuntu instaluje pliki w `/opt` i `/usr`. Nie zawiera ustawień klienta, historii ani transkrypcji. Usunięcie albo wyczyszczenie pakietu nie kasuje plików w profilu użytkownika ani w wybranym katalogu dziennika i nie zatrzymuje już działającego procesu programu PowerShell.

### Zgłaszanie podatności

Zgłoś problem bezpieczeństwa przez prywatne zgłaszanie podatności w serwisie GitHub dla repozytorium [TimeWizard007/powershell-session-recorder](https://github.com/TimeWizard007/powershell-session-recorder). Nie otwieraj publicznego zgłoszenia dla podatności, którą można wykorzystać.

Podaj wersję lub commit, system operacyjny i opis skutków. Nie dołączaj do zgłoszenia danych poufnych ani danych klientów.
