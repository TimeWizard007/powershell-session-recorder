# Instalacja w Ubuntu 24.04

[English](../en/installation.md)

Ten pakiet jest przeznaczony tylko dla Ubuntu 24.04 LTS w wersji amd64. Nie był sprawdzany na innych dystrybucjach ani architekturach. Bieżąca publiczna beta ma wersję aplikacji `0.1.0-beta.1` i wersję pakietu Debiana `0.1.0~beta.1-1`. Informacje o tej becie są w [opisie wydania](../en/release-notes-0.1.0-beta.1.md).

Rejestrowana sesja wymaga programu PowerShell 7 i GNOME Terminal. Żaden z tych programów nie jest w pakiecie. PowerShell 7 nie znajduje się w domyślnych repozytoriach Ubuntu 24.04.

Plik `.deb` zawiera aplikację Avalonia i jej środowisko .NET. Nie zawiera programu PowerShell 7, GNOME Terminal, ustawień, katalogu klientów, historii ani transkrypcji. Program działa jako użytkownik, który go uruchamia. Program uruchamiający nie używa `sudo`.

Strona O programie pokazuje wersję informacyjną. Sprawdzanie aktualizacji podaje tę samą wersję i pyta GitHub o najnowsze stabilne wydanie. Etykieta beta nie jest proponowana jako aktualizacja. Zapytanie nie zawiera TAG-ów klientów, historii ani transkrypcji.

## Kompilacja pakietu

W czystej kopii repozytorium, przy zainstalowanym .NET SDK 10 i narzędziu `dpkg-deb`:

```bash
packaging/ubuntu/build-deb.sh
```

Skrypt publikuje samodzielną kompilację `linux-x64` i zapisuje `artifacts/powershell-session-recorder_<wersja-debiana>_amd64.deb` oraz plik SHA-256 o tej samej nazwie z przyrostkiem `.sha256`. Oba pliki są ignorowane przez Git. Wersja informacyjna w `PowerShellSessionRecorder.App.csproj` jest źródłem prawdy. `0.1.0-beta.1` staje się wersją Debiana `0.1.0~beta.1-1`. Stabilna wersja `1.0.0` stałaby się `1.0.0-1`. Znak `~` sprawia, że pakiet bety sortuje się przed stabilnym pakietem o tym samym rdzeniu wersji. `<AssemblyVersion>` i `<FileVersion>` pozostają `0.1.0.0`, bo te pola są liczbowe.

Po kompilacji sprawdź pakiet:

```bash
cd artifacts
sha256sum -c powershell-session-recorder_0.1.0~beta.1-1_amd64.deb.sha256
```

Przy kolejnej kompilacji zaktualizuj `<InformationalVersion>` w projekcie aplikacji, a potem uruchom skrypt ponownie.

## Instalacja

```bash
sudo apt install ./artifacts/powershell-session-recorder_0.1.0~beta.1-1_amd64.deb
```

`apt` instaluje biblioteki nazwane w pakiecie. Nie instaluje programu PowerShell 7. GNOME Terminal, `xdg-desktop-portal` i `xdg-desktop-portal-gtk` są tylko sugerowane. Na zwykłym pulpicie Ubuntu już są. Gdy ich brakuje, aplikacja i tak się otwiera i wyjaśnia, jak je zainstalować. Sama tych poleceń nie uruchamia.

PowerShell 7 pochodzi z repozytorium firmy Microsoft, a nie z domyślnych repozytoriów Ubuntu. Przed rejestrowaną sesją skorzystaj z [instalacji programu PowerShell w Ubuntu](https://learn.microsoft.com/powershell/scripting/install/install-ubuntu).

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

## Co jest instalowane

- `/opt/powershell-session-recorder/` — pliki aplikacji
- `/usr/bin/powershell-session-recorder` — dowiązanie do aplikacji
- `/usr/share/applications/powershell-session-recorder.desktop`
- ikony w `/usr/share/icons/hicolor/`
- `/usr/share/doc/powershell-session-recorder/copyright`
