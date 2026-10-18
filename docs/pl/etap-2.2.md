# Etap 2.2

[English](../en/checkpoint-2.2.md)

Etap 2.2 dodaje samo powiadomienie o nowszym stabilnym wydaniu w serwisie GitHub. Rejestrowanie, nawigacja, motywy i strona O programie pozostają takie jak w etapie 2.1.

## Co robi sprawdzenie

Aplikacja odczytuje przez HTTPS adres `https://api.github.com/repos/TimeWizard007/powershell-session-recorder/releases/latest`. Żądanie podaje wersję aplikacji w nagłówku User-Agent. Nie zawiera TAG-ów klientów, nazw sesji, transkrypcji, ścieżek dziennika ani innych ustawień.

Etykieta wydania jest porównywana z informacyjną wersją zestawu jako wersja semantyczna. Prefiks `v` jest akceptowany. Metadane po znaku `+` są pomijane. Etykieta taka jak `0.1.0-checkpoint.2.1` jest wersją wstępną wydania `0.1.0`, więc stabilne wydanie `0.1.0` jest nowsze od tej kompilacji etapu, a `0.2.0` również. Zdalna wersja wstępna nigdy nie jest traktowana jako nowsze stabilne wydanie.

Gdy GitHub zwróci HTTP 404, aplikacja informuje, że nie opublikowano jeszcze żadnego wydania. Nie opisuje tego wyniku jako aktualności.

Nowsze stabilne wydanie pojawia się w powiadomieniu u góry głównej treści, ze zainstalowaną wersją, dostępną wersją, przyciskiem **Zobacz wydanie** i przyciskiem **Później**. **Zobacz wydanie** otwiera wyłącznie stronę `https://github.com/TimeWizard007/powershell-session-recorder/releases/tag/...`. **Później** ukrywa tę wersję do następnego uruchomienia aplikacji. Nie ma okna modalnego.

Aplikacja nie pobiera plików wydania, nie instaluje pakietów, nie uruchamia pobranych plików i nie restartuje się.

## Kiedy działa

Automatyczne sprawdzanie jest domyślnie włączone i można je wyłączyć w Ustawieniach. Gdy jest włączone, sprawdzenie zaczyna się po pokazaniu okna i nie opóźnia otwarcia. Udane sprawdzenie nie jest powtarzane przez 24 godziny. Ręczne **Sprawdź aktualizacje teraz** pomija ten odstęp. W danej chwili działa tylko jedno sprawdzenie.

Plik ustawień przechowuje preferencję, czas ostatniego udanego sprawdzenia, ostatnią stabilną etykietę i zaufany adres strony wydania. Nie przechowuje treści odpowiedzi API. Nieudane sprawdzenie nie zmienia tego czasu i nie blokuje przycisku Uruchom sesję.

Brak sieci, przekroczenie czasu, HTTP 403, HTTP 404, niepoprawny JSON i niezdatny numer wersji mają osobne komunikaty.
