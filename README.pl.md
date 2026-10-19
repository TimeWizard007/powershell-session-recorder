# Rejestrator sesji PowerShell

[English](README.md)

Rejestrator sesji PowerShell to lekka, lokalna aplikacja pulpitowa dla administratorów IT oraz zespołów MSP i helpdesku. Ma umożliwiać uruchomienie niezależnej sesji programu PowerShell dla klienta, oznaczenie jej TAG-iem i zapisanie transkrypcji na komputerze lokalnym.

Aplikacja nie wymaga serwera zaplecza, serwera bazy danych, konta w chmurze ani usługi systemu Windows.

## Stan projektu

**Beta Ubuntu 0.1.0-beta.1.** Przycisk Uruchom sesję otwiera niezależny GNOME Terminal z programem `pwsh` i już działającym `Start-Transcript`. Potwierdzona sesja może pojawić się w Historii. Zapisany TAG klienta jest wybierany przed rozpoczęciem rejestrowania. Z tej kopii repozytorium można zbudować pakiet `.deb` dla Ubuntu 24.04 amd64. Strona O programie i sprawdzanie aktualizacji podają wersję `0.1.0-beta.1`. Sprawdzanie szuka tylko nowszego stabilnego wydania GitHub, więc ta beta i kolejne bety nie są proponowane jako aktualizacja. Nic nie jest pobierane ani instalowane. Wersja 1.0.0 nie została wydana. Szczegóły zawierają [informacje o wydaniu](docs/en/release-notes-0.1.0-beta.1.md), [CHANGELOG.md](CHANGELOG.md) i [instalację](docs/pl/instalacja.md).

## Obsługiwane systemy operacyjne

Aplikacja jest przeznaczona dla:

- Windows 10 i Windows 11
- Ubuntu Linux
- macOS

Etap 2 rejestruje sesję programu PowerShell 7 w Ubuntu przez GNOME Terminal. Windows PowerShell 5.1 można wybrać tylko wtedy, gdy aplikacja działa w systemie Windows, i nie jest uruchamiany.

## Technologia

- C# i .NET 10 (LTS)
- Avalonia UI 12 z motywem Fluent
- MVVM z pakietem CommunityToolkit.Mvvm
- PowerShell 7. Rejestrowanie jest zaimplementowane dla Ubuntu z GNOME Terminal
- Windows PowerShell 5.1 tylko w systemie Windows (jeszcze nie jest uruchamiany)
- JSON dla ustawień lokalnych i historii sesji

## Co działa teraz

- Konfiguracja sesji: TAG klienta, nazwa sesji, katalog dziennika, wersja programu PowerShell, wybór uprawnień, opcja daty i godziny oraz opcja historii
- Bieżąca walidacja i podgląd nazwy pliku transkrypcji
- Bezpieczne dla systemu Windows oczyszczanie nazw plików oraz unikalne nazwy, które nie nadpisują istniejących plików
- Wybór lokalnego folderu
- Język angielski (`en-US`) i polski (`pl-PL`); wybór jest zapisywany dla bieżącego użytkownika
- Lokalne ustawienia JSON
- Zapisany katalog klientów. Nowa sesja nie ma wybranego TAG-u. TAG wybiera się z katalogu albo dodaje w formularzu sesji. Zmiana nazwy albo usunięcie klienta nie zmienia Historii ani plików transkrypcji
- Strona Historia dla sesji zarejestrowanych z zaznaczoną opcją **Zapisz sesję w historii**. Starsze pliki transkrypcji nie są importowane. Wpis historii można usunąć osobno albo razem z plikiem transkrypcji, który ma stopkę zakończenia
- Przycisk **Uruchom sesję**, gdy formularz jest poprawny oraz dostępne są `pwsh` i GNOME Terminal
- Samo powiadomienie o nowszym stabilnym wydaniu w serwisie GitHub. Automatyczne sprawdzenie działa najwyżej raz na 24 godziny i można je wyłączyć. Nic nie jest pobierane ani instalowane. Etykiety beta, w tym to wydanie `0.1.0-beta.1`, nie są traktowane jako dostępna aktualizacja

## Ograniczenia

- Windows PowerShell 5.1 nie jest uruchamiany
- Uprawnienia nie są podnoszone. Pole administratora nie jest zapisywane, a sesji nie można uruchomić, gdy jest zaznaczone
- Historia nie pokazuje stanu Aktywna. Etykieta to „Nagrywanie uruchomione (obecny stan nieznany)”, dopóki transkrypcja nie ma stopki. Stan Zakończona nie wymyśla czasu zakończenia
- Transkrypcje zarejestrowane bez zaznaczonej opcji **Zapisz sesję w historii** oraz transkrypcje sprzed tej funkcji nie trafiają do Historii
- `Start-Transcript` zapisuje wyjście hosta. Dane poufne wpisane lub wyświetlone w konsoli mogą trafić do transkrypcji
- Nagłe przerwanie procesu może pominąć stopkę transkrypcji. Transkrypcję bez tej stopki można usunąć z Historii, ale plik zostaje
- Stopka transkrypcji oznacza, że transkrypcja się zakończyła. Nie dowodzi, że każdy powiązany proces został zakończony. Aplikacja nie śledzi działającego procesu programu PowerShell, więc plik jest kasowany tylko wtedy, gdy ta stopka została odczytana z samego pliku
- Sprawdzono tylko GNOME Terminal 3.52 w Ubuntu 24.04. Nie twierdzimy, że inne emulatory terminala działają
- Pakiet `.deb` jest przeznaczony tylko dla Ubuntu 24.04 LTS amd64. Do rejestrowania sesji potrzebne są PowerShell 7 i GNOME Terminal, a pakiet ich nie zawiera. Innych dystrybucji i architektur nie sprawdzano
- Sprawdzanie aktualizacji pokazuje zainstalowaną wersję i porównuje ją z najnowszym stabilnym wydaniem GitHub. Nie śledzi wydań beta
- Nazwa pliku w podglądzie używa przykładowego identyfikatora. Uruchomienie sesji rezerwuje nowy plik i nie nadpisuje istniejącej transkrypcji

## Wymagania

- [.NET SDK 10](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu-2404) (LTS)
- Sesja pulpitu dla interfejsu graficznego

Na świeżej instalacji Ubuntu Avalonia potrzebuje typowych bibliotek systemowych. Nazwy pakietów dla Ubuntu 24.04:

```bash
sudo apt-get update
sudo apt-get install -y libgtk-3-0t64 libice6 libsm6 libfontconfig1 libx11-6
```

Okno wyboru folderu korzysta z portalu pulpitu. Zwykły pulpit Ubuntu już go zawiera:

```bash
sudo apt-get install -y xdg-desktop-portal xdg-desktop-portal-gtk
```

Do uruchomienia rejestrowanej sesji potrzebne są PowerShell 7 i GNOME Terminal. Instrukcja instalacji: [Instalacja programu PowerShell w Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).

## Kompilacja

W katalogu głównym repozytorium:

```bash
dotnet build PowerShellSessionRecorder.sln
```

## Pakiet Ubuntu

Instrukcja zbudowania i instalacji pakietu Ubuntu 24.04 amd64 jest w [instalacji](docs/pl/instalacja.md). Wersja pakietu tej bety to `0.1.0~beta.1-1`. Pakiet nie zawiera programu PowerShell 7 ani GNOME Terminal. Jego usunięcie nie kasuje transkrypcji, ustawień, historii ani katalogu klientów. Plik `.deb` i jego suma SHA-256 powstają w katalogu `artifacts/` i nie należą do drzewa źródeł.

## Uruchomienie w Ubuntu ze źródeł

W katalogu głównym repozytorium, gdy dostępny jest ekran:

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

Ustawienia są zapisywane w `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` albo w `~/.config/powershell-session-recorder/settings.json`, gdy zmienna `XDG_CONFIG_HOME` nie jest ustawiona.

## Testy

```bash
dotnet test PowerShellSessionRecorder.sln
```

## Planowane funkcje

Kolejne etapy mogą uruchomić Windows PowerShell 5.1 i obsłużyć podnoszenie uprawnień tam, gdzie pozwala na to system operacyjny.

## Położenie ustawień

| System operacyjny | Plik ustawień |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` lub `~/.config/powershell-session-recorder/settings.json` |
| Windows | `%APPDATA%\PowerShellSessionRecorder\settings.json` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder/settings.json` |

Plik ustawień przechowuje język interfejsu, ostatni katalog dziennika, ostatnią wersję programu PowerShell, opcje daty i historii, motyw oraz preferencję sprawdzania aktualizacji. Nie przechowuje haseł ani poufnych danych klientów. W Linuksie katalog konfiguracji należy tylko do właściciela, a plik ustawień ma odczyt i zapis właściciela. Plik, którego nie można odczytać, pozostaje na miejscu, jest jednorazowo kopiowany do `settings.invalid.json`, a aplikacja używa ustawień domyślnych.

Historia sesji to osobny plik `history.json` w tym samym katalogu. Zawiera metadane sesji potwierdzonych przy zaznaczonej opcji **Zapisz sesję w historii**. Nie zawiera treści transkrypcji. Zapisani klienci to osobny katalog `customers.json` w tym samym katalogu: stały identyfikator, TAG, opcjonalna nazwa wyświetlana i czas utworzenia. Katalog nie jest wypełniany z Historii, z ostatnich TAG-ów ani z nazw plików transkrypcji. Szczegóły opisują [etap 3](docs/pl/etap-3.md) i [etap 4.1](docs/pl/etap-4.1.md).

## Dokumentacja

- [Informacje o wydaniu 0.1.0-beta.1](docs/en/release-notes-0.1.0-beta.1.md)
- [Instalacja](docs/pl/instalacja.md)
- [Etap 4.1](docs/pl/etap-4.1.md)
- [Etap 4](docs/pl/etap-4.md)
- [Etap 3](docs/pl/etap-3.md)
- [Etap 2](docs/pl/etap-2.md)
- [Etap 1](docs/pl/etap-1.md)
- [Współtworzenie](CONTRIBUTING.md)
- [Bezpieczeństwo](SECURITY.md)
- [Dziennik zmian](CHANGELOG.md)

## Licencja

[Licencja MIT](LICENSE). Copyright (c) 2026 TimeWizard.
