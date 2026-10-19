# Etap 4.1 — TAG-i klientów i czyszczenie transkrypcji

Etap 4.1 dodaje zapisany katalog klientów i potwierdzone usuwanie wpisów historii. Rejestrowanie, wyszukiwanie historii, sprawdzanie aktualizacji, motywy i pakiet Ubuntu z wcześniejszych etapów pozostają.

Wersja informacyjna to `0.1.0-checkpoint.4.1`. Wersja pakietu Debiana to `0.1.0~checkpoint.4.1-1`.

## Katalog klientów

Klienci są zapisywani w pliku `customers.json` obok `settings.json`. W Linuksie jest to `$XDG_CONFIG_HOME/powershell-session-recorder/customers.json` albo `~/.config/powershell-session-recorder/customers.json`.

Każdy rekord zawiera:

- stały identyfikator
- TAG
- opcjonalną nazwę wyświetlaną
- czas utworzenia w UTC

Plik nie zawiera haseł ani treści transkrypcji. Wersja schematu to 1. Zapis używa unikalnego pliku tymczasowego w tym samym katalogu, a potem atomowej podmiany. W Linuksie katalog ma tryb `0700`, a katalog klientów, jego blokada i plik kwarantanny mają tryb `0600`. Dowiązanie symboliczne w miejscu katalogu albo pliku katalogu klientów nie jest śledzone.

`settings.json` i `history.json` zostają przy schemacie 1. Otwarcie aplikacji nie tworzy `customers.json`, dopóki klient nie zostanie zapisany. Ostatnie TAG-i, TAG-i z historii i nazwy plików transkrypcji nie są importowane.

Nowa sesja nie ma wybranego klienta. Przycisk Uruchom sesję pozostaje wyłączony, dopóki nie zostanie wybrany zapisany TAG albo zapisany nowy, poprawny TAG. Silnik rejestrowania nadal dostaje ten TAG jako TAG klienta sesji. Zdublowany TAG jest odrzucany bez względu na wielkość liter. Pusty TAG albo TAG, z którego nie można utworzyć nazwy pliku transkrypcji, jest odrzucany.

Zmiana zapisanego TAG-u aktualizuje katalog, a jeśli ten klient jest wybrany, także TAG pokazywany dla nowej sesji. Nie zmienia nazw plików transkrypcji i nie przepisuje historii. Historia zachowuje TAG użyty w chwili potwierdzenia nagrywania.

Usunięcie klienta wymaga potwierdzenia i wyjaśnia, że Historia oraz pliki transkrypcji zostają. Nie zatrzymuje programu PowerShell. Jeśli ten klient był wybrany, wybór jest czyszczony, a przycisk Uruchom sesję znów jest wyłączony.

## Czyszczenie historii

Historia ma dwie osobne operacje:

- **Usuń z historii** usuwa wpis indeksu i zostawia plik transkrypcji.
- **Usuń i skasuj transkrypcję** kasuje plik tylko wtedy, gdy jest to bezpieczne, a potem usuwa wpis indeksu.

Nie ma jednego przycisku Usuń. Obie operacje pytają o potwierdzenie. Potwierdzenie podaje liczbę zaznaczonych wpisów historii, liczbę plików transkrypcji do skasowania i to, która z dwóch operacji zostanie wykonana.

Można zaznaczyć wiele wierszy. Zaznaczenie wszystkich obejmuje wiersze pasujące do bieżącego wyszukiwania i filtrów. Zmiana wyszukiwania albo filtrów zdejmuje zaznaczenie z wierszy, które przestały być widoczne, więc ukryty wiersz nie jest usuwany. Wyczyszczenie zaznaczenia zdejmuje wszystkie zaznaczenia.

Dla operacji plik i historia kolejność jest taka:

1. Brane są pod uwagę tylko wpisy, które nadal są widoczne.
2. Dla każdego próba kasowania dotyczy dokładnie bezwzględnej ścieżki zapisanej w tym wpisie.
3. `history.json` jest przepisywany jeden raz. Usuwane są tylko wpisy, których pliki zostały skasowane albo których już nie było.
4. Każdy inny wpis zostaje w katalogu.

Jeśli przepisanie katalogu nie uda się po skasowaniu pliku, okno informuje, że Historia nie została zaktualizowana, a wpis zostaje na liście. Późniejsze usunięcie tylko z historii może ten wpis zdjąć. Przerwane czyszczenie nie wybiera innego pliku.

Brak transkrypcji nie blokuje usunięcia wpisu historii. Błąd uprawnień, dowiązanie symboliczne, katalog albo ścieżka inna niż zapisana ścieżka bezwzględna są odrzucane, a wpis historii zostaje.

## Kiedy plik transkrypcji może zostać skasowany

`RecordingConfirmed` oznacza, że nagłówek transkrypcji był widoczny przy uruchomieniu. Nie oznacza, że proces się zakończył, i nie jest zgodą na skasowanie pliku.

Kasowanie pliku jest dozwolone tylko wtedy, gdy otwarty plik jest zwykłym plikiem, a jego koniec zawiera `PowerShell transcript end`. Stopka potwierdza, że transkrypcja się zakończyła. Nie dowodzi, że każdy powiązany proces został zakończony. Aplikacja nadal nie śledzi działającego procesu `pwsh`.

W Linuksie kasowanie otwiera katalog nadrzędny bez śledzenia końcowego dowiązania, tak samo otwiera plik, sprawdza, że deskryptor wskazuje zwykły plik, czyta stopkę z tego deskryptora, sprawdza, że wpis katalogu nadal nazywa to samo urządzenie i i-węzeł, a potem odłącza tę nazwę. Nigdy nie kasuje katalogu, nie działa rekurencyjnie i nie buduje polecenia powłoki. W innych systemach obowiązują te same odmowy przez interfejs plików, bez sprawdzenia i-węzła z Linuksa.

Wpis bez potwierdzonego zakończenia nadal można usunąć z Historii. Jego plik zostaje.

## Pakiet

Pakiet `.deb` buduje `packaging/ubuntu/build-deb.sh`. Nie instaluj go na miejsce istniejącego pakietu systemowego, dopóki taka podmiana nie zostanie zatwierdzona. Pakiet nie zawiera `customers.json`, `history.json`, `settings.json` ani transkrypcji. Usunięcie pakietu nadal nie kasuje plików w profilu użytkownika.
