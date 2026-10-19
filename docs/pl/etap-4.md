# Etap 4 — pakiet Ubuntu, etykiety stanu i uprawnienia metadanych

Ten etap dodaje pakiet `.deb` dla Ubuntu 24.04 amd64 i doprecyzowuje dwa zachowania historii. Nie dodaje śledzenia procesu i nie zmienia schematu `history.json` w wersji 1.

Pakiet przygotowano dla Ubuntu 24.04 LTS na architekturze x64. Innych dystrybucji i architektur nie sprawdzano.

## Etykiety stanu

Zapisane wartości stanu to nadal `RecordingConfirmed` i `Finished`. Strona Historia pokazuje:

| Etykieta | Znaczenie |
| --- | --- |
| Nagrywanie uruchomione (obecny stan nieznany) | Nagłówek transkrypcji został sprawdzony przy uruchomieniu. Podpowiedź wiersza mówi, że obecny stan procesu nie jest znany. To nie jest stan Aktywna, Trwa ani Nagrywa teraz. |
| Zakończona | Koniec transkrypcji zawiera `PowerShell transcript end`. `End time:` jest zapisywany tylko wtedy, gdy da się odczytać te 14 cyfr. Stopka bez użytecznego czasu nadal jest pokazywana jako Zakończona i nie jest wymyślany czas zakończenia. |
| Nieznany | Ścieżka jest niepoprawna albo pliku nie udało się odczytać. |
| Brak transkrypcji | Pliku nie ma już pod zapisaną ścieżką. Wpis historii zostaje. |

Angielskie etykiety to „Recording started (current state unknown)”, „Finished”, „Unknown” i „Transcript missing”.

## Uprawnienia metadanych

W Linuksie katalog konfiguracji należy tylko do właściciela (`0700`). Pliki `settings.json`, `history.json`, `history.json.lock`, kwarantanna `history.invalid-*.json` oraz `settings.invalid.json` mają odczyt i zapis właściciela (`0600`). Kolejne zapisy i odczyty utrzymują te uprawnienia. Istniejący katalog albo plik metadanych czytelny dla grupy lub pozostałych użytkowników jest zawężany do tych uprawnień. Sama zmiana uprawnień nie podmienia treści i nie usuwa transkrypcji.

Dowiązanie symboliczne użyte jako katalog konfiguracji, plik ustawień, plik historii, blokada albo miejsce kwarantanny nie jest śledzone. Aplikacja go nie usuwa. Zapis historii wtedy kończy się dotychczasowym ostrzeżeniem, a transkrypcja nadal może powstać.

Windows nie używa trybów uniksowych. macOS nadal używa trybu tylko dla właściciela, gdy system to obsługuje.

## Pakiet

Opisuje to [instalacja](instalacja.md). Źródłem wersji jest wersja informacyjna w projekcie aplikacji. `0.1.0-checkpoint.4` staje się wersją Debiana `0.1.0~checkpoint.4-1`, która sortuje się przed późniejszym stabilnym pakietem `0.1.0-1`. Przy przyszłej kompilacji zmień `<Version>`, `<AssemblyVersion>`, `<FileVersion>` i `<InformationalVersion>` w `PowerShellSessionRecorder.App.csproj`, a potem uruchom skrypt pakietu ponownie. Nie poprawiaj ręcznie wygenerowanego pliku `control`.

Pakiet zależy od bibliotek Ubuntu 24.04, które ładuje program, w tym `libssl3t64`, `libicu74` i `libfontconfig1`. PowerShell 7 nie jest zależnością pakietu, bo nie ma go w domyślnych repozytoriach Ubuntu 24.04. GNOME Terminal jest tylko sugerowany, więc okno może się otworzyć i pokazać dotychczasową wskazówkę instalacji, gdy brakuje któregoś polecenia. Pakiet nie dodaje repozytoriów i sam niczego nie instaluje.

Usunięcie albo wyczyszczenie pakietu nie kasuje transkrypcji, ustawień ani historii. Nie ma skryptu opiekuna, który zatrzymywałby program PowerShell.
