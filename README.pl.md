# Rejestrator sesji PowerShell

[English](README.md)

Rejestrator sesji PowerShell to lekka, lokalna aplikacja pulpitowa dla administratorów IT oraz zespołów MSP i helpdesku. Ma umożliwiać uruchomienie niezależnej sesji programu PowerShell dla klienta, oznaczenie jej TAG-iem i zapisanie transkrypcji na komputerze lokalnym.

Aplikacja nie wymaga serwera zaplecza, serwera bazy danych, konta w chmurze ani usługi systemu Windows.

## Stan projektu

**Wersja 0.1.0-beta.2 jest przygotowana i nieopublikowana.** Opublikowane wydanie GitHub pozostaje [0.1.0-beta.1](docs/en/release-notes-0.1.0-beta.1.md). Ta kopia repozytorium podaje `0.1.0-beta.2`. Ubuntu zachowuje GNOME Terminal i program PowerShell 7. Windows 10/11 x64 może otworzyć program PowerShell 7 albo Windows PowerShell 5.1, z opcjonalną Kontrolą konta użytkownika tylko dla tej konsoli. Ustawienia mogą używać kanału stabilnego albo testowego. Nic nie jest pobierane ani instalowane automatycznie. Skrypt instalatora Windows jest gotowy dla Inno Setup 6 i nie został tu skompilowany. Szczegóły zawierają [etap 5.2](docs/pl/etap-5.2.md), [informacje o wydaniu](docs/en/release-notes-0.1.0-beta.2.md) i [instalację](docs/pl/instalacja.md).

## Obsługiwane systemy operacyjne

Aplikacja jest przeznaczona dla:

- Windows 10 i Windows 11
- Ubuntu Linux
- macOS

Ubuntu rejestruje sesję programu PowerShell 7 przez GNOME Terminal. Windows 10/11 x64 może uruchomić program PowerShell 7 (`pwsh.exe`) albo Windows PowerShell 5.1 (`powershell.exe`) w osobnej konsoli. macOS otwiera program PowerShell 7 w Terminal.app. Pakiety dla procesorów Intel i Apple Silicon z podpisem ad hoc opisuje [macOS](docs/pl/macos.md). Nie są poświadczone.

## Technologia

- C# i .NET 10 (LTS)
- Avalonia UI 12 z motywem Fluent
- MVVM z pakietem CommunityToolkit.Mvvm
- PowerShell 7. Rejestrowanie jest zaimplementowane dla Ubuntu z GNOME Terminal
- Windows PowerShell 5.1 tylko w systemie Windows. Program PowerShell 7 nie jest wtedy wymagany
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
- Przycisk **Uruchom sesję** w Ubuntu, gdy formularz jest poprawny oraz dostępne są `pwsh` i GNOME Terminal
- W Windows 10/11 x64 wybór programu PowerShell 7 albo Windows PowerShell 5.1 i osobna widoczna konsola. PowerShell 7 jest preferowany, gdy oba są zainstalowane. Bieżący katalog nie jest przeszukiwany w poszukiwaniu silnika
- Samo powiadomienie GitHub z kanałem stabilnym albo testowym. Domyślny jest kanał stabilny. Automatyczne sprawdzenie działa najwyżej raz na 24 godziny i można je wyłączyć. Nic nie jest pobierane ani instalowane. Żądanie nie wysyła TAG-ów klientów, nazw sesji, transkrypcji, historii ani ustawień

## Ograniczenia

- Rejestrowania, instalacji i układu okna w Windows nie sprawdzono na komputerze z Windows dla tej bety. Instalatora Inno Setup nie skompilowano w Ubuntu
- W Ubuntu opcja administratora jest ukryta i sesji nie można uruchomić, gdy podniesienie uprawnień jest ustawione. W Windows podniesienie dotyczy tylko konsoli programu PowerShell. Anulowanie pytania nie uruchamia rejestrowania, a przyjęte pytanie nie jest zapisywane jako potwierdzone podniesienie uprawnień
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

Rejestrowanie w macOS nie jest zaimplementowane. Działanie konsoli Windows nadal trzeba sprawdzić w systemie Windows.

## Położenie ustawień

| System operacyjny | Plik ustawień |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` lub `~/.config/powershell-session-recorder/settings.json` |
| Windows | `%LOCALAPPDATA%\PowerShellSessionRecorder\settings.json` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder/settings.json` |

Plik ustawień przechowuje język interfejsu, ostatni katalog dziennika, ostatnią wersję programu PowerShell, opcje daty i historii, motyw oraz preferencję sprawdzania aktualizacji. Nie przechowuje haseł ani poufnych danych klientów. W Linuksie katalog konfiguracji należy tylko do właściciela, a plik ustawień ma odczyt i zapis właściciela. Plik, którego nie można odczytać, pozostaje na miejscu, jest jednorazowo kopiowany do `settings.invalid.json`, a aplikacja używa ustawień domyślnych.

Historia sesji to osobny plik `history.json` w tym samym katalogu. Zawiera metadane sesji potwierdzonych przy zaznaczonej opcji **Zapisz sesję w historii**. Nie zawiera treści transkrypcji. Zapisani klienci to osobny katalog `customers.json` w tym samym katalogu: stały identyfikator, TAG, opcjonalna nazwa wyświetlana i czas utworzenia. Katalog nie jest wypełniany z Historii, z ostatnich TAG-ów ani z nazw plików transkrypcji. Szczegóły opisują [etap 3](docs/pl/etap-3.md) i [etap 4.1](docs/pl/etap-4.1.md).

## Dokumentacja

- [Etap 5](docs/pl/etap-5.md)
- [Informacje o wydaniu 0.1.0-beta.1](docs/en/release-notes-0.1.0-beta.1.md)
- [Informacje o wydaniu 0.1.0-beta.2](docs/en/release-notes-0.1.0-beta.2.md)
- [CI i kompilacje wydania](docs/pl/potok-wydania.md)
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
