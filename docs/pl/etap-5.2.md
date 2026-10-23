# Etap 5.2

Przygotowana wersja `0.1.0-beta.2`. To nie jest opublikowane wydanie GitHub. Opublikowane wydanie pozostaje `0.1.0-beta.1`.

Wersja pakietu Debiana: `0.1.0~beta.2-1`.
Wersja instalatora Windows: `0.1.0-beta.2`.
Wersja archiwum Linuksa: `0.1.0-beta.2`.
Wersje zestawu i pliku pozostają `0.1.0.0`. Commit Gita jest zapisany po znaku `+` w wersji informacyjnej zestawu. Strona O programie i nagłówek User-Agent pomijają ten przyrostek.

## Pakiety

Wszystkie trzy pakiety powstają z tej samej rewizji źródeł.

- Pakiet `.deb` Ubuntu 24.04 amd64 ze skryptu `packaging/ubuntu/build-deb.sh`. Zachowuje integrację z pulpitem, program uruchamiający w `/usr/bin`, ikony i położenie danych użytkownika.
- Archiwum Linux x64 `.tar.gz` ze skryptu `packaging/linux/build-tarball.sh`. `install-user.sh` instaluje do `~/.local/opt/powershell-session-recorder` bez `sudo`. Nazwa na pulpicie to **Rejestrator sesji PowerShell (użytkownik)**, więc nie jest to program uruchamiający pakietu `.deb`. `uninstall-user.sh` kasuje tylko pliki z manifestu. Ustawienia, klienci, historia i transkrypcje zostają.
- Skrypt instalatora Windows `packaging/windows/PowerShellSessionRecorder.iss`. Instalacja dla bieżącego użytkownika jest domyślna, bo `PrivilegesRequired=lowest`. Okno dialogowe pozwala wybrać instalację dla wszystkich użytkowników, która pyta o Kontrolę konta użytkownika. `UsePreviousPrivileges=yes` zostawia aktualizację w dotychczasowym zakresie. `{autopf}` oznacza `%LOCALAPPDATA%\Programs` albo `%ProgramFiles%` zależnie od wyboru. Skryptu nie skompilowano w Ubuntu.

Instalator dla bieżącego użytkownika Linuksa sprawdzono w odizolowanym `HOME` na Ubuntu 24.04. Nie sprawdzono każdego wydania Debiana ani każdego środowiska pulpitu.

## Kanały aktualizacji

Schemat ustawień pozostaje w wersji 1. Brak pola `updateChannel` oznacza kanał stabilny i nie powoduje przepisania pliku. Kanał stabilny używa `/releases/latest` i pomija wydania przedpremierowe. Kanał testowy używa `/releases?per_page=100` dla najwyżej trzech stron, pomija szkice i wybiera najwyższą wersję semantyczną, a nie pierwszy wiersz. Starsza etykieta nie jest proponowana. Zmiana kanału czyści zapamiętaną wersję, adres i czas ostatniego sprawdzenia. Sprawdzenie pozostaje ręczne albo najwyżej raz na 24 godziny. Wydanie nie jest pobierane.

## Czego tu nie zrobiono

Instalatora Windows nie skompilowano ani nie uruchomiono. Parsowania Windows PowerShell 5.1 i okna w Windows nie sprawdzono ponownie w Windows. Na stanowisku deweloperskim nie zainstalowano żadnego pakietu. Nic nie zostało zatwierdzone, otagowane ani opublikowane.
