# Etap 3

[English](../en/checkpoint-3.md)

Etap 3 dodaje lokalną, przeszukiwalną historię potwierdzonych sesji rejestrowania. Silnik rejestrowania pozostaje bez zmian: Uruchom sesję nadal otwiera niezależny GNOME Terminal z programem PowerShell 7 i `Start-Transcript`. Historia jest indeksem metadanych. Nie jest potrzebna do zapisania transkrypcji.

## Co jest zapisywane

Historia to plik `history.json` w tym samym katalogu użytkownika co `settings.json`. Plik używa schematu w wersji 1 i tablicy `sessions`. Każdy wpis zawiera:

- unikalny identyfikator sesji
- TAG klienta i nazwę sesji
- nazwę pliku transkrypcji i jej bezwzględną ścieżkę
- czas rozpoczęcia w UTC; strona Historia wylicza z niego lokalną datę i godzinę
- wybraną wersję programu PowerShell
- zweryfikowany stan rejestrowania
- opcjonalny czas zakończenia, dopiero po odczytaniu stopki transkrypcji

Treść transkrypcji nie jest zapisywana w `history.json`. W systemach uniksowych uprawnienia pliku to odczyt i zapis właściciela (`0600`). Zapis trafia do pliku tymczasowego w tym samym katalogu, a potem zastępuje `history.json`. W Linuksie i Windows plik blokady szereguje procesy zapisujące. Awaria przed podmianą zostawia poprzedni plik `history.json`. Etap 4 ogranicza też katalog konfiguracji do właściciela.

Plik, który nie jest poprawnym JSON-em albo zawiera niepoprawną sesję, jest zmieniany na `history.invalid-<znacznik-czasu>-<n>.json`. Ta kopia nie jest nadpisywana. `schemaVersion` nowszy niż 1 pozostaje niezmieniony i nie jest zapisywany od nowa. Aplikacja nie usuwa plików transkrypcji, gdy odczytuje albo aktualizuje historię.

Istniejące pliki ustawień zachowują język, motyw, katalog dziennika, wybór programu PowerShell i wartości sprawdzania aktualizacji. Nowa sesja nadal zaczyna się od pustego TAG-u klienta. Podpowiedzi ostatnich TAG-ów nie są pokazywane.

## Kiedy sesja trafia do indeksu

Opcja **Zapisz sesję w historii** steruje tylko indeksem. Transkrypcja powstaje w obu przypadkach.

Gdy opcja jest zaznaczona, wpis jest dodawany dopiero po zobaczeniu nagłówka transkrypcji (komunikat o uruchomieniu rejestrowania). Ten sam TAG i ta sama nazwa sesji mogą być użyte ponownie; każde potwierdzone uruchomienie dostaje własny identyfikator i własny plik transkrypcji. Jeśli pliku historii nie da się zapisać, wiersz stanu nadal informuje, że rejestrowanie wystartowało, i dodaje ostrzeżenie, że Historia nie została zaktualizowana. Terminal pozostaje uruchomiony.

Gdy opcja jest odznaczona, nic nie jest dodawane. Aplikacja nie przegląda później katalogu dziennika i nie importuje tej transkrypcji. Transkrypcje utworzone przed tą funkcją, także pliki pozostawione w `/tmp`, nie są importowane. Pusta strona Historia o tym mówi.

Uruchomienie, które nie potwierdzi nagłówka transkrypcji, nie jest indeksowane.

## Etykiety stanu

Strona nie pokazuje stanu Aktywna i nie traktuje braku czasu zakończenia jako działającej sesji. GNOME Terminal może działać po zamknięciu tej aplikacji, a zabity proces może zostawić transkrypcję bez stopki.

| Etykieta | Znaczenie |
| --- | --- |
| Nagrywanie uruchomione (obecny stan nieznany) | Nagłówek transkrypcji został sprawdzony przy uruchomieniu. Zapisana wartość to nadal `RecordingConfirmed`. Nie oznacza to, że proces nadal działa. |
| Zakończona | Koniec pliku transkrypcji zawiera `PowerShell transcript end`. `End time:` jest odczytywany jako czas lokalny i zapisywany w UTC, gdy da się go odczytać. Stopka bez użytecznego czasu i tak oznacza stan Zakończona. |
| Nieznany | Ścieżka jest niepoprawna albo pliku nie udało się odczytać. |
| Brak transkrypcji | Pliku transkrypcji nie ma już pod zapisaną ścieżką. Wpis historii zostaje. |

Aplikacja nie śledzi procesu programu PowerShell po uruchomieniu. Odświeżenie czyta tylko ostatnie 8 KB transkrypcji, szukając stopki. Nie wczytuje transkrypcji do pliku historii.

## Strona Historia

Sesje są ułożone od najnowszej. Strona może szukać w TAG-u, nazwie sesji i nazwie pliku transkrypcji, filtrować po dokładnym TAG-u klienta i filtrować po włącznym lokalnym zakresie dat. **Wyczyść filtry** zeruje te kontrolki. **Odśwież** ponownie czyta indeks.

Gdy indeks nie ma sesji, strona informuje, że widoczne są tylko sesje zarejestrowane z włączoną historią po wprowadzeniu tej funkcji. Gdy sesje istnieją, ale bieżące wyszukiwanie albo filtry wykluczają je wszystkie, strona informuje, że nic nie pasuje.

Każdy wiersz może otworzyć transkrypcję, otworzyć folder, w którym się znajduje, albo skopiować bezwzględną ścieżkę. W Linuksie plik i folder są przekazywane jako jeden argument do `xdg-open`. Ścieżka nie jest wstawiana do polecenia powłoki, a transkrypcja nie jest wykonywana. Brak pliku, niepoprawna ścieżka albo odmowa odczytu są zgłaszane w wierszu stanu po angielsku albo po polsku.

## Poza zakresem

Windows PowerShell 5.1, podnoszenie uprawnień, rejestrowanie w macOS, inne emulatory terminala, usuwanie wpisów historii i import starszych transkrypcji nie należą do tego etapu. Rejestrowanie sprawdzono tylko z GNOME Terminal 3.52 w Ubuntu 24.04.
