# Rejestrator sesji PowerShell

[English](README.md)

Rejestrator sesji PowerShell to lekka, lokalna aplikacja pulpitowa dla administratorów IT oraz zespołów MSP i helpdesku. Ma umożliwiać uruchomienie niezależnej sesji programu PowerShell dla klienta, oznaczenie jej TAG-iem i zapisanie transkrypcji na komputerze lokalnym.

Aplikacja nie wymaga serwera zaplecza, serwera bazy danych, konta w chmurze ani usługi systemu Windows.

## Stan projektu

**Etap 2.2 — rejestrowanie w Ubuntu oraz sprawdzenie wydań w serwisie GitHub.** Przycisk Uruchom sesję otwiera niezależny GNOME Terminal z programem `pwsh` i już działającym `Start-Transcript`. Aplikacja może zapytać GitHub, czy jest nowsze stabilne wydanie. Nie pobiera ani nie instaluje aktualizacji. Wersja 1.0.0 nie została wydana. Szczegóły zawierają [CHANGELOG.md](CHANGELOG.md) i [etap 2.2](docs/pl/etap-2.2.md).

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
- JSON dla ustawień lokalnych i, w przyszłości, historii sesji

## Co działa teraz

- Konfiguracja sesji: TAG klienta, nazwa sesji, katalog dziennika, wersja programu PowerShell, wybór uprawnień, opcja daty i godziny oraz opcja historii
- Bieżąca walidacja i podgląd nazwy pliku transkrypcji
- Bezpieczne dla systemu Windows oczyszczanie nazw plików oraz unikalne nazwy, które nie nadpisują istniejących plików
- Wybór lokalnego folderu
- Język angielski (`en-US`) i polski (`pl-PL`); wybór jest zapisywany dla bieżącego użytkownika
- Lokalne ustawienia JSON
- Pusta strona Historia. Pliki transkrypcji i tak są zapisywane w katalogu dziennika
- Przycisk **Uruchom sesję**, gdy formularz jest poprawny oraz dostępne są `pwsh` i GNOME Terminal
- Samo powiadomienie o nowszym stabilnym wydaniu w serwisie GitHub. Automatyczne sprawdzenie działa najwyżej raz na 24 godziny i można je wyłączyć. Nic nie jest pobierane ani instalowane

## Ograniczenia

- Windows PowerShell 5.1 nie jest uruchamiany
- Uprawnienia nie są podnoszone. Pole administratora nie jest zapisywane, a sesji nie można uruchomić, gdy jest zaznaczone
- Strona Historia nie wyświetla sesji
- `Start-Transcript` zapisuje wyjście hosta. Dane poufne wpisane lub wyświetlone w konsoli mogą trafić do transkrypcji
- Nagłe przerwanie procesu może pominąć stopkę transkrypcji
- Sprawdzono tylko GNOME Terminal 3.52 w Ubuntu 24.04. Nie twierdzimy, że inne emulatory terminala działają
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

## Uruchomienie w Ubuntu

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

Kolejne etapy mogą wyświetlić listę zarejestrowanych sesji, uruchomić Windows PowerShell 5.1 i obsłużyć podnoszenie uprawnień tam, gdzie pozwala na to system operacyjny.

## Położenie ustawień

| System operacyjny | Plik ustawień |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/powershell-session-recorder/settings.json` lub `~/.config/powershell-session-recorder/settings.json` |
| Windows | `%APPDATA%\PowerShellSessionRecorder\settings.json` |
| macOS | `~/Library/Application Support/PowerShellSessionRecorder/settings.json` |

Plik przechowuje język interfejsu, ostatni katalog dziennika, ostatnią wersję programu PowerShell, opcje daty i historii oraz ostatnie TAG-i klientów. Nie przechowuje haseł ani poufnych danych klientów. Plik, którego nie można odczytać, pozostaje na miejscu, jest jednorazowo kopiowany do `settings.invalid.json`, a aplikacja używa ustawień domyślnych.

## Dokumentacja

- [Etap 2](docs/pl/etap-2.md)
- [Etap 1](docs/pl/etap-1.md)
- [Współtworzenie](CONTRIBUTING.md)
- [Bezpieczeństwo](SECURITY.md)
- [Dziennik zmian](CHANGELOG.md)

## Licencja

[Licencja MIT](LICENSE). Copyright (c) 2026 TimeWizard.
