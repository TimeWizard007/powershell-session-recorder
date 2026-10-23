# Etap 5.1

Wersja kopii roboczej `0.1.0-checkpoint.5.1`. To nie jest wydanie. Opublikowana beta Ubuntu pozostaje `0.1.0-beta.1`.

## Kodowanie

Windows PowerShell 5.1 czyta plik `.ps1` bez znacznika kolejności bajtów stroną kodową ANSI systemu. Skrypt startowy jest w UTF-8, więc znak `ł` (`C5 82`) pojawiał się jako `Ĺ‚`, a parser odrzucał polski napis. W Windows plik startowy jest teraz UTF-8 z bajtami `EF BB BF`. Pliki w Linuksie pozostają UTF-8 bez znacznika BOM. Kodowanie konsoli, profile i zasady wykonywania pozostają bez zmian.

## Okno

Okno nie startuje już w stałym rozmiarze 1120×780. Gdy obszar roboczy monitora jest wystarczający, otwiera się w 960×640 pikselach niezależnych od urządzenia, wyśrodkowane w tym obszarze, i można je zmieniać. Minimum to 640×480, chyba że obszar roboczy jest mniejszy. Wiersze akcji zawijają się, żeby polskie etykiety pozostawały dostępne.

Wyglądu w Windows i parsowania przez Windows PowerShell 5.1 nie sprawdzono z Ubuntu.
