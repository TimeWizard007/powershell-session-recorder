# Instalacja w Ubuntu 24.04

[English](../en/installation.md)

Ten pakiet jest przeznaczony tylko dla Ubuntu 24.04 LTS w wersji amd64. Nie był sprawdzany na innych dystrybucjach ani architekturach. Ta kopia robocza to `1.0.0`, pakiet Debiana `1.0.0-1`. Informacje są w [opisie wydania](informacje-1.0.0.md). Wcześniej opublikowane bety pozostają dostępne.

Rejestrowana sesja wymaga programu PowerShell 7 i GNOME Terminal. Żaden z tych programów nie jest w pakiecie. PowerShell 7 nie znajduje się w domyślnych repozytoriach Ubuntu 24.04.

Plik `.deb` zawiera aplikację Avalonia i jej środowisko .NET. Nie zawiera programu PowerShell 7, GNOME Terminal, ustawień, katalogu klientów, historii ani transkrypcji. Program działa jako użytkownik, który go uruchamia. Program uruchamiający nie używa `sudo`.

Strona O programie pokazuje wersję informacyjną bez przyrostka commita po znaku `+`. Sprawdzanie aktualizacji podaje tę samą wersję. Kanał stabilny, który jest domyślny, pyta GitHub o najnowsze stabilne wydanie. Kanał testowy uwzględnia też opublikowane wydania przedpremierowe i nie proponuje starszej etykiety. Zapytanie nie zawiera TAG-ów klientów, historii ani transkrypcji. Nic nie jest pobierane.

## Kompilacja pakietu

W czystej kopii repozytorium, przy zainstalowanym .NET SDK 10 i narzędziu `dpkg-deb`:

```bash
packaging/ubuntu/build-deb.sh
```

Skrypt publikuje samodzielną kompilację `linux-x64` i zapisuje `artifacts/powershell-session-recorder_<wersja-debiana>_amd64.deb` oraz plik SHA-256 o tej samej nazwie z przyrostkiem `.sha256`. Oba pliki są ignorowane przez Git. Wersja informacyjna w `PowerShellSessionRecorder.App.csproj` jest źródłem prawdy. `1.0.0` staje się wersją Debiana `1.0.0-1`. Wersja przedpremierowa, na przykład `0.1.0-beta.1`, staje się `0.1.0~beta.1-1`. Znak `~` sprawia, że pakiet bety sortuje się przed stabilnym pakietem o tym samym rdzeniu wersji. `<AssemblyVersion>` i `<FileVersion>` mają wartość `1.0.0.0`, bo te pola są liczbowe. Commit Gita zostaje po znaku `+` w wersji informacyjnej zestawu i jest pomijany w wersji pakietu oraz na stronie O programie.

Po kompilacji sprawdź pakiet:

```bash
cd artifacts
sha256sum -c powershell-session-recorder_1.0.0-1_amd64.deb.sha256
```

Przy kolejnej kompilacji zaktualizuj `<InformationalVersion>` w projekcie aplikacji, a potem uruchom skrypt ponownie.

## Instalacja

```bash
sudo apt install ./artifacts/powershell-session-recorder_1.0.0-1_amd64.deb
```

`apt` instaluje biblioteki nazwane w pakiecie. Nie instaluje programu PowerShell 7. GNOME Terminal, `xdg-desktop-portal` i `xdg-desktop-portal-gtk` są tylko sugerowane. Na zwykłym pulpicie Ubuntu już są. Gdy ich brakuje, aplikacja i tak się otwiera i wyjaśnia, jak je zainstalować. Sama tych poleceń nie uruchamia.

PowerShell 7 pochodzi z repozytorium firmy Microsoft, a nie z domyślnych repozytoriów Ubuntu. Przed rejestrowaną sesją skorzystaj z [instalacji programu PowerShell w Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).

Ikony opisuje [ikony](ikony.md). Zatwierdzone źródło to `assets/branding/app-icon-source.png`.

## Archiwum dla bieżącego użytkownika Linuksa

`packaging/linux/build-tarball.sh` zapisuje `artifacts/powershell-session-recorder-1.0.0-linux-x64.tar.gz` i plik `.sha256`. Wymaga Basha i GNU find. Sprawdzono je w Ubuntu 24.04, nie na każdym wydaniu Debiana i nie na każdym pulpicie.

```bash
tar -xzf powershell-session-recorder-1.0.0-linux-x64.tar.gz
cd powershell-session-recorder-1.0.0-linux-x64
./install-user.sh
```

Pliki trafiają do `~/.local/opt/powershell-session-recorder`. Pozycja w menu to **Rejestrator sesji PowerShell (użytkownik)** i nie zastępuje `/usr/bin/powershell-session-recorder`. `./uninstall-user.sh` usuwa tylko pliki wymienione dla tej instalacji. Ustawienia, klienci, historia i transkrypcje zostają. Instalator odrzuca dowiązanie symboliczne w archiwum albo w miejscu docelowym.

## Windows 10/11 x64

Ta kopia repozytorium może też opublikować samodzielną kompilację Windows. Na komputerze docelowym nie jest wymagane środowisko .NET. Program PowerShell 7 albo Windows PowerShell 5.1 musi już być zainstalowany. Windows Terminal nie jest wymagany.

Z katalogu głównego repozytorium, w Ubuntu albo Windows:

```bash
packaging/windows/publish-win-x64.sh
```

Wynik to `artifacts/win-x64/`. Ten katalog jest ignorowany przez Git. W Windows, z katalogu repozytorium albo z dowolnego innego katalogu, gdy zainstalowano Inno Setup 6:

```powershell
packaging\windows\build-setup.ps1
```

Oczekiwana nazwa instalatora to `artifacts\PowerShellSessionRecorder-1.0.0-win-x64-setup.exe`. Kompiluje go zadanie wydania w Windows, nie Ubuntu. Domyślny wybór to instalacja dla bieżącego użytkownika w `%LOCALAPPDATA%\Programs\PowerShellSessionRecorder`, bez uprawnień administratora. Wybór dla wszystkich użytkowników pyta o Kontrolę konta użytkownika i instaluje w `%ProgramFiles%\PowerShellSessionRecorder`. Aktualizacja zostaje w zakresie już zapisanym dla tego samego AppId. Instalator nie zmienia `PATH`, nie dołącza programu PowerShell 7 i nie usuwa `%LOCALAPPDATA%\PowerShellSessionRecorder`.

Konfiguracja w Windows to `%LOCALAPPDATA%\PowerShellSessionRecorder`. Transkrypcje zostają w folderze wybranym w aplikacji. Do zwykłego użycia program nie potrzebuje uprawnień administratora. Uruchom jako administrator dotyczy tylko konsoli programu PowerShell.

Nowe konto Windows startuje z pustym katalogiem klientów, bez wybranego klienta, z pustą historią i ze zwykłymi ustawieniami domyślnymi. Instalacja dla użytkownika i instalacja dla wszystkich korzystają z tego samego folderu profilu. Nie tworzą przykładowych klientów, TAG-ów, sesji ani transkrypcji. Klient już zapisany w `customers.json`, także dodany podczas wcześniejszego testu, zostaje po aktualizacji i ponownej instalacji. Usuń go w aplikacji poleceniem **Usuń z katalogu** i potwierdź. Wpisy historii i pliki transkrypcji zostają, dopóki nie usuniesz tych wpisów z Historii.

Wersja informacyjna w tej kopii roboczej to `1.0.0`. GitHub Actions buduje `.deb`, archiwum Linuksa, instalator Windows oraz obrazy i archiwa macOS z jednego znacznika. Kroki i ręczne zatwierdzenie opisuje [CI i kompilacje wydania](potok-wydania.md). Codzienny opis jest w [przewodniku](przewodnik.md).

## Uruchomienie

Pozycja w menu aplikacji to **PowerShell Session Recorder**. Z terminala:

```bash
powershell-session-recorder
```

Oba sposoby uruchamiają `/opt/powershell-session-recorder/PowerShellSessionRecorder`. Sama aplikacja nie potrzebuje okna terminala. Rejestrowana sesja nadal otwiera GNOME Terminal.

## Aktualizacja pakietu

Nowszy plik `.deb` instaluje się tak samo:

```bash
sudo apt install ./artifacts/powershell-session-recorder_<nowsza-wersja>_amd64.deb
```

## Usuwanie

```bash
sudo apt remove powershell-session-recorder
```

Polecenie `sudo apt purge powershell-session-recorder` usuwa te same pliki pakietu. Żadne z nich nie kasuje transkrypcji, `settings.json`, `history.json` ani `customers.json`. Zostają one w wybranym katalogu dziennika oraz w `$XDG_CONFIG_HOME/powershell-session-recorder` albo `~/.config/powershell-session-recorder`. Działający już proces programu PowerShell działa dalej. Te dane usuń samodzielnie tylko wtedy, gdy tego chcesz.

Pakiet `.deb` i archiwum `.tar.gz` dla użytkownika korzystają z tego samego katalogu konfiguracji. Nowe konto Linux startuje z pustym katalogiem i pustą historią. Żaden z pakietów nie instaluje przykładowego klienta. Aby usunąć klienta, który jest już zapisany, otwórz go w aplikacji, wybierz **Usuń z katalogu** i potwierdź. Historia i pliki transkrypcji zostają.

## Co jest instalowane

- `/opt/powershell-session-recorder/` — pliki aplikacji
- `/usr/bin/powershell-session-recorder` — dowiązanie do aplikacji
- `/usr/share/applications/powershell-session-recorder.desktop`
- ikony w `/usr/share/icons/hicolor/`
- `/usr/share/doc/powershell-session-recorder/copyright`

## macOS

Obrazy dysku i archiwa ZIP dla procesorów Intel i Apple Silicon powstają poleceniem `sh packaging/macos/build-dmg.sh all`. Nazwy to `PowerShellSessionRecorder-1.0.0-osx-x64.dmg`, `PowerShellSessionRecorder-1.0.0-osx-x64.zip` oraz te same dwa pliki dla `osx-arm64`, każdy z plikiem `.sha256`. W macOS gotowy pakiet dostaje podpis ad hoc. Nie jest to podpis Developer ID i pakiet nie jest poświadczony. Gatekeeper pozostaje włączony. Instalację, w tym czego nie zmieniać w systemie, opisuje [macOS](macos.md).
