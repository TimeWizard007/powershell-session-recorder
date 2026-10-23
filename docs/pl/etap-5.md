# Etap 5 — konsola Windows

[English](../en/checkpoint-5.md)

Ten etap dodaje rejestrowanie w Windows 10/11 x64 obok przepływu Ubuntu. To nie jest wydanie. Opublikowana beta Ubuntu pozostaje `0.1.0-beta.1`. Ta kopia robocza podaje `0.1.0-checkpoint.5`.

## Co potrafi Windows

- Wykrywa program PowerShell 7 w `%ProgramFiles%\PowerShell\7\pwsh.exe`, a potem we wpisach bezwzględnych PATH.
- Wykrywa Windows PowerShell 5.1 tylko w `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`.
- Pomija bieżący katalog, względne wpisy PATH i punkty ponownej analizy, takie jak aliasy wykonania aplikacji Windows.
- Preferuje program PowerShell 7, gdy oba silniki istnieją. Zapisany wybór 5.1 zostaje, gdy ten silnik jest obecny.
- Otwiera osobną widoczną konsolę. Proces nie jest wewnątrz okna Avalonia i nie jest ukryty. Nie jest zatrzymywany przy zamknięciu rejestratora.
- Przekazuje ścieżkę transkrypcji i ścieżkę skryptu jako argumenty. Skrypt startowy nie zawiera TAG-u klienta ani nazwy sesji.
- Pyta o Kontrolę konta użytkownika tylko wtedy, gdy zaznaczono **Uruchom jako administrator**. Okno rejestratora nie jest podnoszone. Anulowanie pytania nie uruchamia rejestrowania i nie zapisuje Historii. Przyjęte pytanie jest pokazywane jako żądane, a nie jako potwierdzone podniesienie uprawnień.
- Przechowuje konfigurację w `%LOCALAPPDATA%\PowerShellSessionRecorder`. Transkrypcje zostają w wybranym folderze.
- Kasuje transkrypcję tylko wtedy, gdy jej własna stopka jest obecna, a otwarty uchwyt jest zwykłym plikiem. Katalogi, dowiązania symboliczne, punkty połączenia i inne punkty ponownej analizy są odrzucane. Ścieżki nie są przekazywane do powłoki.

Ubuntu nadal używa GNOME Terminal i programu PowerShell 7. Pole administratora jest tam ukryte.

## Pakiet

`packaging/windows/publish-win-x64.sh` publikuje samodzielny folder `win-x64` do `artifacts/win-x64/`. Użytkownik końcowy nie potrzebuje środowiska .NET. `packaging/windows/PowerShellSessionRecorder.iss` to skrypt Inno Setup dla pozycji w menu Start i dezinstalatora.

Instalator nie został skompilowany w Ubuntu i nie był sprawdzany w Windows. Opublikowanie folderu w Ubuntu nie dowodzi, że konsola, pytanie Kontroli konta użytkownika albo instalator działa.

## Nie sprawdzono w Windows

Wykrywanie silników, widoczna konsola, podniesienie uprawnień i jego anulowanie, listy kontroli dostępu tylko dla właściciela, kasowanie punktów ponownej analizy oraz instalator nadal wymagają komputera z Windows 10 albo Windows 11 x64.
