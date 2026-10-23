# CI i kompilacje wydania

Opublikowane wydanie GitHub pozostaje `v0.1.0-beta.1`. Wersja `0.1.0-beta.2` jest przygotowana w tym repozytorium. GitHub Actions może ją zbudować ze znacznika. Samo przepchnięcie kodu nie publikuje wydania.

## Ciągła integracja

`.github/workflows/ci.yml` uruchamia się dla żądań ściągnięcia i dla wypchnięć do `main`.

- Ubuntu 24.04 buduje konfiguracje Debug i Release oraz uruchamia testy automatyczne.
- Windows Server 2022 robi to samo. Test składni Windows PowerShell 5.1 jest kompilowany tylko w Windows, więc wykonuje go właśnie to zadanie. W Ubuntu ten test jest pomijany.
- Oba zadania używają SDK .NET 10.
- Przepływ ma tylko odczyt repozytorium. Nie tworzy znaczników, pakietów ani wydań GitHub.

## Kompilacja wydania

`.github/workflows/release.yml` startuje wyłącznie z karty Actions poleceniem **Run workflow**. Nie uruchamia się po wypchnięciu.

Podaj istniejący znacznik, na przykład `v0.1.0-beta.2`. Tryb zostaw jako `review-only`.

Przepływ pobiera ten znacznik, odrzuca gałąź i brudne drzewo oraz wymaga, aby commit znacznika zgadzał się z:

- wersją aplikacji `0.1.0-beta.2`
- wersją Debiana `0.1.0~beta.2-1`
- `MyAppVersion` w Inno Setup

Zadania Linux i Windows pobierają potem ten sam commit po jego SHA. Nie używają wcześniej przesłanego artefaktu ani pliku skopiowanego z komputera programisty.

Ubuntu 24.04 buduje `.deb` i archiwum dla użytkownika, zapisuje pliki SHA-256 i odrzuca ustawienia, klientów, historię, symbole debugowania oraz starą ikonę SVG. Wymaga też zatwierdzonych plików PNG hicolor oraz wersji informacyjnej `0.1.0-beta.2` z pełnym SHA commita.

Windows x64 uruchamia testy Release, w tym test składni Windows PowerShell 5.1. Pobiera Inno Setup 6.5.4 z oficjalnego wydania `jrsoftware/issrc` i sprawdza SHA-256 `fa73bf47a4da250d185d07561c2bfda387e5e20db77e4570004cf6a133cc10b1` przed instalacją. Publikuje aplikację, kompiluje `packaging/windows/PowerShellSessionRecorder.iss` i sprawdza sumę instalatora, stały AppId oraz obecność zatwierdzonych obrazów ikony w pliku wykonywalnym i instalatorze.

Ostatnie zadanie Ubuntu pobiera te pliki, ponownie sprawdza sumy i ikonę instalatora, a następnie wysyła jeden pakiet do przeglądu. To zadanie nie tworzy wydania.

## Zatwierdzenie przed publikacją

Publikacja jest osobnym uruchomieniem, a nie częścią zwykłej kompilacji.

1. Utwórz środowisko GitHub o nazwie `release`.
2. Dodaj wymaganych recenzentów. Zadanie publikacji czeka, aż jeden z nich je zatwierdzi.
3. Dodaj zmienną środowiska `RELEASE_APPROVED` o wartości `yes` dopiero wtedy, gdy publikacja jest dozwolona. Przepływ nie może sam ustawić tej wartości.
4. Uruchom przepływ ponownie z tym samym znacznikiem i trybem `publish`.

Zadanie publikacji sprawdza przejrzane sumy, sprawdza, że znacznik nadal wskazuje zbudowany commit, i zatrzymuje się, gdy wydanie o tym znaczniku już istnieje. Nie zastępuje ani nie kasuje istniejącego wydania. Znacznik beta jest oznaczany jako wydanie przedpremierowe, żeby kanał stabilny nie uznał go za najnowsze wydanie stabilne.

Dopóki nie ma środowiska i zmiennej, tryb `publish` nie może się zakończyć, nawet jeśli ktoś go wybierze.

## Uprawnienia

| Przepływ | Uprawnienie |
|---|---|
| CI | Zawartość: odczyt |
| Kompilacja i przegląd wydania | Zawartość: odczyt |
| Zadanie publikacji | Zawartość: zapis, i tylko po zatwierdzeniu środowiska `release` |

Wystarcza domyślny `GITHUB_TOKEN`. Publiczne repozytorium nie potrzebuje dodatkowego sekretu. Przy żądaniu ściągnięcia token nie dostaje prawa zapisu.

## Artefakty i sumy kontrolne

Otwórz uruchomienie przepływu, a potem **Artifacts**. Tryb `review-only` tworzy `release-linux`, `release-windows` i `release-review-<znacznik>`. Przy każdym pakiecie jest plik `.sha256`. Pakiet do przeglądu pozostaje przez 14 dni.

Pobierz pakiet i w katalogu, w którym leży plik oraz jego `.sha256`, uruchom:

```bash
sha256sum -c powershell-session-recorder_0.1.0~beta.2-1_amd64.deb.sha256
sha256sum -c powershell-session-recorder-0.1.0-beta.2-linux-x64.tar.gz.sha256
sha256sum -c PowerShellSessionRecorder-0.1.0-beta.2-win-x64-setup.exe.sha256
```

W Windows PowerShell `Get-FileHash -Algorithm SHA256` powinien pokazać ten sam skrót, co pierwsze pole pliku `.sha256`.

Wersja informacyjna zestawu w kompilacjach Linux i Windows to `0.1.0-beta.2`, potem `+` i 40-znakowy SHA commita. Ekran O programie nadal pokazuje `0.1.0-beta.2` bez tego przyrostka.

## Publikacja zatwierdzonego wydania

Po przejrzeniu artefaktów `review-only`:

```text
Actions → Release → Run workflow
Tag: v0.1.0-beta.2
Mode: publish
```

Zatwierdź oczekujące środowisko `release`. Zadanie tworzy wydanie GitHub ze znacznika i dołącza trzy pakiety oraz ich sumy. Plik informacji o wydaniu to `docs/en/release-notes-0.1.0-beta.2.md` z tego znacznika.

Znacznik utwórz z przejrzanego commita przed pierwszym uruchomieniem. Nie wskazuj nim nazwy gałęzi ani niezatwierdzonych plików:

```bash
git tag v0.1.0-beta.2 <przejrzany-commit>
git push origin v0.1.0-beta.2
```

Wypchnięcie znacznika nie uruchamia przepływu wydania. Robi to tylko **Run workflow**.

## Nieudane kompilacje

Nieudane zadanie platformy nie wysyła swoich plików, a zadanie publikacji się nie uruchamia. Po przejściowej awarii użyj **Re-run failed jobs**. Przy błędzie kodu lub pakowania popraw go nowym commitem, przesuń albo wymień znacznik tylko wtedy, gdy tego chcesz, i uruchom nowy przebieg `review-only`. Nie publikuj niepełnego zestawu. Jeśli próba publikacji zatrzyma się, zanim GitHub utworzy wydanie, nie ma czego kasować. Gdy wydanie już istnieje, przepływ zostawia je bez zmian.
