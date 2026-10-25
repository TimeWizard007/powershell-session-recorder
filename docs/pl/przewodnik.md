# Przewodnik

[English](../en/user-guide.md)

Rejestrator sesji PowerShell to lokalna aplikacja pulpitu dla administratorów i zespołów wsparcia. Otwiera osobną konsolę programu PowerShell dla klienta, oznacza sesję tagiem i zostawia transkrypcję na tym samym komputerze. Nie ma serwera, konta ani automatycznego pobierania.

To drzewo źródeł ma wersję **1.0.0**. Wydanie GitHub dla `v1.0.0` powstaje dopiero po utworzeniu znacznika i ręcznej publikacji. Starsze bety zostają na GitHub.

Ikona okna to `assets/branding/app-icon-source.png`. To repozytorium nie zawiera zrzutów ekranu okna. Interfejs ma cztery obszary: formularz sesji, Historię, Ustawienia i O programie.

## Co robi

- Zapisuje klientów jako TAG-i i uruchamia sesję z jednym wybranym TAG-iem.
- Otwiera program PowerShell poza oknem aplikacji i rejestruje go przez `Start-Transcript`.
- Zapisuje wpis historii, gdy ta opcja jest zaznaczona.
- Trzyma na tym komputerze polski i angielski tekst interfejsu, motyw oraz preferencję aktualizacji.
- Może porównać zainstalowaną wersję z wydaniami GitHub. Nie pobiera i nie instaluje aktualizacji.

## Obsługiwane systemy

| System | Architektura | Co sprawdzono |
| --- | --- | --- |
| Windows 10 i Windows 11 | x64 | Zainstalowana aplikacja: start, wykrycie programu PowerShell, rejestrowanie, transkrypcje i historia |
| Ubuntu 24.04 LTS | amd64 | Ten sam przebieg przez GNOME Terminal i program PowerShell 7 |
| macOS 14 lub nowszy, sprawdzony na Intel Sequoia | x64 | Zainstalowany `.app`: start, wykrycie programu PowerShell, rejestrowanie w Terminal.app, transkrypcje i historia |
| macOS 14 lub nowszy | Apple Silicon | Zapakowany host przechodzi test dymny CI. Okno nie było częścią tego testu |

Pakiet macOS deklaruje minimum macOS 14.0. Rejestrowanie sprawdzono na macOS Intel, nie na każdym wydaniu macOS i nie jako widoczną aplikację na Apple Silicon. Innych dystrybucji i pulpitów Linuksa nie sprawdzano. Aplikacja nie szuka pliku programu PowerShell w bieżącym katalogu.

## Program PowerShell

Żaden pakiet nie zawiera programu PowerShell.

- Linux: program PowerShell 7 (`pwsh`) i GNOME Terminal. Działania innych terminali nie deklarujemy.
- macOS: program PowerShell 7 (`pwsh`) i Terminal.app, który należy do macOS.
- Windows: program PowerShell 7 (`pwsh.exe`) albo Windows PowerShell 5.1 (`powershell.exe`). Gdy oba są zainstalowane, preferowany jest program PowerShell 7. Windows PowerShell 5.1 nie wymaga programu PowerShell 7.

Ustawienia pokazują wykrytą zainstalowaną wersję i najnowsze stabilne wydanie programu PowerShell 7 od Microsoftu. Ten panel jest informacyjny. Nie instaluje programu PowerShell.

## Instalacja

Pakiety i sumy kontrolne są w [informacjach o wydaniu 1.0.0](informacje-1.0.0.md). Kroki budowy są w [instalacji](instalacja.md). Wybór pakietu macOS, obraz dysku i Gatekeeper są w [macOS](macos.md).

- Windows x64: uruchom `PowerShellSessionRecorder-1.0.0-win-x64-setup.exe`. Domyślna jest instalacja dla bieżącego użytkownika w `%LOCALAPPDATA%\Programs\PowerShellSessionRecorder` i nie wymaga uprawnień administratora. Wybór dla wszystkich użytkowników pyta o Kontrolę konta użytkownika i instaluje w Program Files. Aktualizacja zostaje w zakresie już zapisanym dla tej aplikacji. Odinstalowanie usuwa tylko pliki aplikacji. Nie usuwa `%LOCALAPPDATA%\PowerShellSessionRecorder`.
- Ubuntu 24.04 amd64: zainstaluj `powershell-session-recorder_1.0.0-1_amd64.deb` albo rozpakuj `powershell-session-recorder-1.0.0-linux-x64.tar.gz` i uruchom jego `install-user.sh`. Usunięcie którejkolwiek instalacji nie kasuje ustawień, klientów, historii ani transkrypcji.
- Mac z procesorem Intel: użyj obrazu albo archiwum `osx-x64`. Apple Silicon: użyj obrazu albo archiwum `osx-arm64`. Otwórz obraz i przeciągnij **PowerShell Session Recorder** do Applications albo skopiuj `.app` z archiwum zip. Zastąpienie istniejącego pakietu dotyczy tylko tego pakietu.

Wydanie nie zawiera archiwum ZIP dla Windows ani pliku `.pkg` dla macOS.

### Gatekeeper w macOS

Kompilacje macOS mają podpis ad hoc. Nie są podpisane identyfikatorem Developer ID i nie są poświadczone. Gatekeeper może ostrzec przy pierwszym otwarciu. Zostaw Gatekeepera włączonego.

Kliknij aplikację z wciśniętym klawiszem Control, wybierz **Otwórz**, a potem ponownie **Otwórz**. To dopuszcza tę jedną aplikację.

Dla pobranej kopii, której sumę SHA-256 już sprawdziłeś, zaawansowaną możliwością jest zdjęcie kwarantanny tylko z tej kopii:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Nie uruchamiaj `sudo spctl --master-disable`. To wyłączyłoby Gatekeepera dla każdej aplikacji.

## Klienci

Klient to TAG i opcjonalna nazwa wyświetlana. Nowa sesja startuje bez wybranego TAG-u. Wybierz zapisany TAG albo dodaj go w formularzu sesji. Zmiana nazwy albo usunięcie zapisanego klienta nie przepisuje Historii i nie zmienia nazw plików transkrypcji. Katalog to `customers.json` w katalogu ustawień. Nie jest wypełniany z Historii ani ze starych nazw transkrypcji.

## Rejestrowanie sesji

W formularzu ustaw TAG klienta, nazwę sesji, katalog dziennika, wersję programu PowerShell, to, czy nazwa pliku zawiera datę i godzinę, oraz to, czy sesja trafia do Historii. Formularz pokazuje podgląd nazwy pliku. Uruchomienie sesji rezerwuje nowy plik i nie nadpisuje istniejącej transkrypcji.

**Uruchom sesję** otwiera osobną konsolę. Okno programu PowerShell nie jest osadzone w tej aplikacji. W Windows wybór uprawnień może zapytać o Kontrolę konta użytkownika tylko dla tej konsoli. Anulowanie pytania nie uruchamia rejestrowania i nie zapisuje Historii. W Ubuntu opcja administratora nie służy do uruchomienia sesji.

Zwykłe `exit` uruchamia `Stop-Transcript`. Zamknięcie terminala w inny sposób może pominąć stopkę transkrypcji.

## Transkrypcje i historia

Transkrypcje powstają w katalogu dziennika wybranym dla sesji. Nazwa pliku składa się z czasu, TAG-u i nazwy sesji oraz, gdy trzeba, z krótkiego unikalnego przyrostka. `Start-Transcript` zapisuje tekst hosta. Tajemnica wpisana albo wyświetlona w tej konsoli może trafić do pliku. Traktuj transkrypcje jako dane wrażliwe.

Historia pokazuje sesje uruchomione z zaznaczonym **Zapisz sesję w historii**. Starsze transkrypcje nie są importowane. Wiersz historii można usunąć osobno. Usunięcie także pliku transkrypcji jest możliwe tylko wtedy, gdy plik zawiera stopkę `PowerShell transcript end`. Ta stopka oznacza, że transkrypcja się zakończyła. Nie dowodzi, że każdy powiązany proces już wyszedł. Plik bez stopki może zniknąć z Historii i nie jest kasowany. Historia nie pokazuje stanu „trwa”. Zanim pojawi się stopka, etykieta brzmi „Nagrywanie uruchomione (obecny stan nieznany)”.

`history.json` przechowuje TAG, nazwę sesji, ścieżkę, czas rozpoczęcia, wersję programu PowerShell i status. Nie przechowuje tekstu transkrypcji.

## Ustawienia i język

Ustawienia są zapisywane dla bieżącego użytkownika:

| System | Katalog |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder` albo `~/.config/powershell-session-recorder` |
| Windows | `%LOCALAPPDATA%\PowerShellSessionRecorder` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder` |

`settings.json` przechowuje język (`en-US` albo `pl-PL`), motyw, ostatni katalog dziennika, ostatni wybór programu PowerShell, opcje znacznika czasu i historii oraz preferencję aktualizacji. Nie przechowuje haseł. Plik, którego nie da się odczytać, zostaje na miejscu, jest raz kopiowany do `settings.invalid.json` i w pamięci zastępowany wartościami domyślnymi. Motyw podąża za systemem albo za jasnym lub ciemnym wyborem zapisanym w tym pliku.

## Aktualizacje

Kanał stabilny jest domyślny. Czyta najnowsze stabilne wydanie GitHub. `v1.0.0` jest stabilne, więc ten kanał może je zaproponować. Znacznik przedpremierowy, w tym `v0.1.0-beta.1` i każda późniejsza beta, jest pomijany na kanale stabilnym. Te wydania pozostają dostępne na GitHub.

Kanał testowy porównuje opublikowane wersje stabilne i przedpremierowe, pomija szkice i przegląda najwyżej trzy strony po 100 wydań. Niższa wersja nie jest proponowana. Zmiana kanału czyści zapisany wynik.

Automatyczne sprawdzanie odbywa się najwyżej raz na 24 godziny i można je wyłączyć. **Sprawdź aktualizacje** w Ustawieniach uruchamia sprawdzenie na żądanie. Żądanie wysyła wersję aplikacji i nie wysyła TAG-ów, nazw sesji, transkrypcji, historii ani ustawień. Nic nie jest pobierane ani instalowane. O programie pokazuje `1.0.0` bez przyrostka commita, który pakowanie dokleja po znaku `+`.

## Rozwiązywanie problemów

- Przycisk sesji pozostaje niedostępny, gdy formularz jest niekompletny albo brakuje wymaganego hosta programu PowerShell lub terminala.
- W Windows anulowanie Kontroli konta użytkownika wraca do formularza i nie tworzy wpisu historii.
- Transkrypcja bez `PowerShell transcript end` jest niedokończona. Historia może usunąć wpis i nie skasuje pliku.
- W macOS ostrzeżenie Gatekeepera jest oczekiwane przy tym podpisie ad hoc. Użyj Control-klik **Otwórz**. Nie wyłączaj Gatekeepera.
- W macOS zainstaluj pakiet `osx-x64` na Intelu i pakiet `osx-arm64` na Apple Silicon. Te pakiety nie są wymienne.
- Sprawdzanie aktualizacji zgłasza limit zapytań, brak sieci albo brak nowszej stabilnej wersji i nie zmienia plików lokalnych.

## Budowa

Współtwórcy potrzebują [.NET SDK 10](https://dotnet.microsoft.com/download). Z kopii roboczej:

```bash
dotnet build PowerShellSessionRecorder.sln
dotnet test PowerShellSessionRecorder.sln
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

Polecenia pakowania są w [instalacji](instalacja.md) i [macOS](macos.md). Nie commituj `artifacts/`, ustawień, klientów, historii ani transkrypcji. Zobacz [współtworzenie](../../CONTRIBUTING.md).

## Pliki do pobrania i sumy kontrolne

Zasoby wydania i przykłady `sha256sum -c` są w [informacjach o wydaniu 1.0.0](informacje-1.0.0.md) oraz w [CI i kompilacjach wydania](potok-wydania.md). Sprawdź sumę, zanim otworzysz pobrany instalator albo obraz dysku. Przepływ wydania buduje każdy plik z commita znacznika i odmawia publikacji, gdy któregoś brakuje.

## Historia wersji

[Dziennik zmian](../../CHANGELOG.md). Informacje o tej wersji: [1.0.0](informacje-1.0.0.md). Informacje o betach pozostają: [0.1.0-beta.1](../en/release-notes-0.1.0-beta.1.md) i [0.1.0-beta.2](../en/release-notes-0.1.0-beta.2.md).
