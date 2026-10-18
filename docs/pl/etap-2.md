# Etap 2

[English](../en/checkpoint-2.md)

Etap 2 uruchamia prawdziwe rejestrowanie sesji programu PowerShell 7 z okna Avalonia w systemie Ubuntu. Etap 2.1 zachowuje ten przebieg i odświeża interfejs: pionowa nawigacja (Nowa sesja, Historia, Ustawienia, O programie), motywy systemowy, jasny i ciemny oraz sprawdzenie zależności `pwsh` i GNOME Terminal. TAG klienta jest wpisywany przy każdej sesji. TAG-i zapisane w starszym pliku ustawień pozostają i nie są wybierane automatycznie.

## Co się dzieje po naciśnięciu Uruchom sesję

1. Formularz musi być poprawny, program PowerShell 7 musi być dostępny jako `pwsh` w zmiennej `PATH`, a na komputerze musi być zainstalowany GNOME Terminal. Podnoszenie uprawnień pozostaje wyłączone: przycisk nie uruchamia sesji, gdy zaznaczono **Uruchom jako administrator**.
2. Aplikacja tworzy katalog dziennika, jeśli go nie ma.
3. Rezerwuje nowy plik `.txt` przez `FileMode.CreateNew`. Istniejąca transkrypcja nie jest nadpisywana. Jednoczesne uruchomienia dostają różne nazwy.
4. Zapisuje stały skrypt startowy poza katalogiem dziennika. Tekst klienta i ścieżki są argumentami procesu, a nie fragmentem wklejonym do skryptu.
5. GNOME Terminal jest uruchamiany bez `--wait`. Klient terminala kończy pracę od razu. Proces programu PowerShell należy do `gnome-terminal-server`, więc zamknięcie okna Avalonia nie przerywa rejestrowania.
6. Skrypt wywołuje `Start-Transcript` przed znakiem zachęty. Terminal wypisuje, że rejestrowanie jest aktywne, i pokazuje ścieżkę transkrypcji.
7. Jeśli `Start-Transcript` się nie powiedzie, terminal pokazuje błąd i czeka na Enter. Potem się zamyka. Nie zostawia powłoki bez rejestrowania.
8. Pasek stanu Avalonia mówi, że rejestrowanie wystartowało, dopiero gdy nagłówek transkrypcji jest widoczny w pliku. W przeciwnym razie informuje, że terminal został otwarty, ale rejestrowanie nie zostało potwierdzone.
9. Gdy użytkownik normalnie kończy pracę programu PowerShell, skrypt wywołuje `Stop-Transcript` i usuwa skrypt startowy.

Pole **Zapisz sesję w historii** jest przechowywane na potrzeby późniejszej listy. Plik transkrypcji powstaje niezależnie od tego pola. Ten etap nie dodaje listy historii ani przycisku zatrzymania sesji.

## Obsługa terminala

Na komputerze użytym do tej pracy jedynym zainstalowanym emulatorem terminala jest GNOME Terminal 3.52 (`/usr/bin/gnome-terminal`). Program buduje argumenty dla tego narzędzia i został tam sprawdzony. `x-terminal-emulator` nie jest używany, ponieważ opakowanie w Ubuntu dodaje `--wait` i może przekazać polecenie przez powłokę.

Nie twierdzimy, że inne emulatory terminala działają.

## Powtórzenie testu rejestrowania

Wymagania: Ubuntu z sesją pulpitu, PowerShell 7 (`pwsh`) i GNOME Terminal.

```bash
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

1. Wybierz TAG klienta `HOWE`.
2. Wpisz nazwę sesji `Diagnostyka VPN`.
3. Wybierz `/tmp` jako katalog dziennika.
4. Zostaw PowerShell 7 i nie zaznaczaj **Uruchom jako administrator**.
5. Naciśnij **Uruchom sesję**.
6. W nowym terminalu potwierdź komunikat o rejestrowaniu i ścieżkę transkrypcji, zanim wpiszesz polecenia.
7. Uruchom `Get-Date` i `Get-Location`.
8. Zakończ poleceniem `exit`.
9. Otwórz plik `.txt` w `/tmp` i potwierdź oba polecenia oraz ich wynik.
10. Uruchom dwie sesje i potwierdź, że używają różnych plików.
11. Uruchom kolejną sesję, zamknij okno Avalonia i potwierdź, że terminal nadal działa.

## Ograniczenia

- Windows PowerShell 5.1 nie jest uruchamiany.
- Podnoszenie uprawnień i mechanizm UAC nie są zaimplementowane.
- `Start-Transcript` zapisuje to, co host programu PowerShell wyświetla. Hasło albo token wpisany lub wypisany w sesji może trafić do transkrypcji.
- Zamknięcie okna terminala albo zabicie procesu może pominąć stopkę transkrypcji. Plik może nadal zawierać polecenia, które zostały już zapisane.
- Aplikacja nie wczytuje ani nie zmienia profilu programu PowerShell. Sesja używa `-NoProfile`.
- Podgląd nazwy pliku używa przykładowego identyfikatora. Uruchomiona sesja rezerwuje inny plik.
