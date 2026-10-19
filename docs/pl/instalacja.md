# Instalacja w Ubuntu 24.04

[English](../en/installation.md)

Ten pakiet jest przeznaczony tylko dla Ubuntu 24.04 LTS w wersji x64. Nie był sprawdzany na innych dystrybucjach ani architekturach.

Plik `.deb` zawiera aplikację Avalonia i jej środowisko .NET. Nie zawiera programu PowerShell 7, GNOME Terminal, ustawień klienta, historii ani transkrypcji. Program działa jako użytkownik, który go uruchamia. Program uruchamiający nie używa `sudo`.

## Kompilacja pakietu

W czystej kopii repozytorium, przy zainstalowanym .NET SDK 10 i narzędziu `dpkg-deb`:

```bash
packaging/ubuntu/build-deb.sh
```

Skrypt publikuje samodzielną kompilację `linux-x64` i zapisuje `artifacts/powershell-session-recorder_<wersja-debiana>_amd64.deb`. Wersja informacyjna aplikacji jest czytana z `PowerShellSessionRecorder.App.csproj`. Wersja przedpremierowa `0.1.0-checkpoint.4.1` staje się wersją Debiana `0.1.0~checkpoint.4.1-1`. Wcześniejszy etap `0.1.0-checkpoint.4` mapował się na `0.1.0~checkpoint.4-1`. Stabilna wersja `1.0.0` stałaby się `1.0.0-1`. Znak `~` sprawia, że pakiet etapu sortuje się przed stabilnym pakietem o tym samym rdzeniu wersji.

Przy kolejnej kompilacji zaktualizuj `<Version>`, `<AssemblyVersion>`, `<FileVersion>` i `<InformationalVersion>` w projekcie aplikacji, a potem uruchom skrypt ponownie.

## Instalacja

```bash
sudo apt install ./artifacts/powershell-session-recorder_0.1.0~checkpoint.4.1-1_amd64.deb
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

Polecenie `sudo apt purge powershell-session-recorder` usuwa te same pliki pakietu. Żadne z nich nie kasuje transkrypcji, `settings.json` ani `history.json`. Zostają one w wybranym katalogu dziennika oraz w `$XDG_CONFIG_HOME/powershell-session-recorder` albo `~/.config/powershell-session-recorder`. Działający już proces programu PowerShell działa dalej. Te dane usuń samodzielnie tylko wtedy, gdy tego chcesz.

## Co jest instalowane

- `/opt/powershell-session-recorder/` — pliki aplikacji
- `/usr/bin/powershell-session-recorder` — dowiązanie do aplikacji
- `/usr/share/applications/powershell-session-recorder.desktop`
- ikony w `/usr/share/icons/hicolor/`
- `/usr/share/doc/powershell-session-recorder/copyright`
