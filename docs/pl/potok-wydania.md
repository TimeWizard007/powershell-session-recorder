# CI i kompilacje wydania

[English](../en/release-pipeline.md)

To drzewo źródeł ma wersję `1.0.0`. GitHub Actions może zbudować znacznik `v1.0.0`, gdy ten znacznik już istnieje. Wypchnięcie commita albo znacznika nie publikuje wydania GitHub. Wcześniej opublikowane bety zostają na miejscu.

## Ciągła integracja

`.github/workflows/ci.yml` uruchamia się dla żądań ściągnięcia i dla wypchnięć do `main`.

- Ubuntu 24.04 buduje konfiguracje Debug i Release oraz uruchamia testy automatyczne.
- Windows Server 2022 robi to samo. Test składni Windows PowerShell 5.1 jest kompilowany tylko w Windows, więc wykonuje go właśnie to zadanie. W Ubuntu ten test jest pomijany.
- macOS 15 buduje Debug i Release, uruchamia testy i pakuje `osx-x64` oraz `osx-arm64`. Podpisuje pakiety ad hoc, sprawdza `codesign --verify --deep --strict` i wykonuje test dymny hosta Apple Silicon. Te pakiety są artefaktami CI o nazwie `powershell-session-recorder-macos-ci`. Nie są zasobami wydania.
- Zadania używają SDK .NET 10.
- Przepływ ma odczyt repozytorium. Nie tworzy znaczników ani wydań GitHub.

## Kompilacja wydania

`.github/workflows/release.yml` startuje wyłącznie z karty Actions poleceniem **Run workflow**. Nie uruchamia się po wypchnięciu.

Podaj istniejący znacznik, na przykład `v1.0.0`. Tryb zostaw jako `review-only`.

Przepływ pobiera ten znacznik, odrzuca gałąź i brudne drzewo oraz wymaga, aby commit znacznika zgadzał się z:

- wersją aplikacji `1.0.0`
- wersją Debiana `1.0.0-1`
- `MyAppVersion` w Inno Setup

Zadania Linux, Windows i macOS pobierają potem ten sam commit po jego SHA. Nie używają wcześniej przesłanego artefaktu CI ani pliku skopiowanego z komputera programisty. `SOURCE_REVISION_ID` jest tym SHA, więc wersja informacyjna w każdej kompilacji to `1.0.0+` i 40-znakowy commit. Ekran O programie pokazuje `1.0.0` bez tego przyrostka.

Ubuntu 24.04 buduje `.deb` i archiwum dla użytkownika, zapisuje pliki SHA-256 i odrzuca ustawienia, klientów, historię, symbole debugowania oraz starą ikonę SVG. Wymaga też zatwierdzonych plików PNG hicolor oraz wersji informacyjnej z pełnym SHA commita.

Windows x64 uruchamia testy Release, w tym test składni Windows PowerShell 5.1. Pobiera Inno Setup 6.5.4 z oficjalnego wydania `jrsoftware/issrc` i sprawdza SHA-256 `fa73bf47a4da250d185d07561c2bfda387e5e20db77e4570004cf6a133cc10b1` przed instalacją. Publikuje aplikację, kompiluje `packaging/windows/PowerShellSessionRecorder.iss` i sprawdza sumę instalatora, stały AppId oraz obecność zatwierdzonych obrazów ikony w pliku wykonywalnym i instalatorze. Nie ma osobnego archiwum ZIP dla Windows.

macOS 15 uruchamia testy Release i `sh packaging/macos/build-dmg.sh all`. Powstają obrazy i archiwa Intel oraz Apple Silicon, podpis ad hoc, weryfikacja podpisów i test dymny Apple Silicon. Aplikacje nie są poświadczane.

Ostatnie zadanie Ubuntu pobiera te pliki, ponownie sprawdza wszystkie siedem sum i ikonę instalatora, a następnie wysyła jeden pakiet do przeglądu. To zadanie nie tworzy wydania. Publikacja zatrzymuje się, gdy brakuje wymaganego pliku, więc sam zestaw Linuksa albo sam zestaw Windows nie zostanie opublikowany.

## Zatwierdzenie przed publikacją

Publikacja jest osobnym uruchomieniem, a nie częścią zwykłej kompilacji.

1. Utwórz środowisko GitHub o nazwie `release`.
2. Dodaj wymaganych recenzentów. Zadanie publikacji czeka, aż jeden z nich je zatwierdzi.
3. Dodaj zmienną środowiska `RELEASE_APPROVED` o wartości `yes` dopiero wtedy, gdy publikacja jest dozwolona. Przepływ nie może sam ustawić tej wartości.
4. Uruchom przepływ ponownie z tym samym znacznikiem i trybem `publish`.

Zadanie publikacji sprawdza przejrzane sumy, sprawdza, że znacznik nadal wskazuje zbudowany commit, i zatrzymuje się, gdy wydanie o tym znaczniku już istnieje. Nie zastępuje ani nie kasuje istniejącego wydania. Znacznik, którego wersja zawiera łącznik, jest oznaczany jako przedpremierowy. `v1.0.0` nie ma łącznika, więc kanał stabilny może potraktować go jako wydanie stabilne. Istniejące bety pozostają przedpremierowe.

Dopóki nie ma środowiska i zmiennej, tryb `publish` nie może się zakończyć, nawet jeśli ktoś go wybierze.

## Uprawnienia

| Przepływ | Uprawnienie |
|---|---|
| CI | Zawartość: odczyt. Zadanie macOS może wysłać własne artefakty CI. |
| Kompilacja i przegląd wydania | Zawartość: odczyt. Zadanie wydania macOS może wysłać swoje artefakty wydania. |
| Zadanie publikacji | Zawartość: zapis, i tylko po zatwierdzeniu środowiska `release` |

Domyślny `GITHUB_TOKEN` wystarcza. Publiczne repozytorium nie wymaga dodatkowego sekretu. Token nie dostaje zapisu przy kompilacjach żądań ściągnięcia.

## Artefakty i sumy kontrolne

Otwórz przebieg przepływu, a potem **Artifacts**. `review-only` tworzy `release-linux`, `release-windows`, `release-macos` i `release-review-<tag>`. Przy każdym pakiecie jest plik `.sha256`. Pakiet przeglądu jest trzymany przez 14 dni.

Pobierz pakiet i w katalogu, który zawiera pakiet oraz jego plik `.sha256`, uruchom:

```bash
sha256sum -c powershell-session-recorder_1.0.0-1_amd64.deb.sha256
sha256sum -c powershell-session-recorder-1.0.0-linux-x64.tar.gz.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-win-x64-setup.exe.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-x64.dmg.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-x64.zip.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-arm64.dmg.sha256
sha256sum -c PowerShellSessionRecorder-1.0.0-osx-arm64.zip.sha256
```

W Windows PowerShell `Get-FileHash -Algorithm SHA256` powinien pokazać ten sam skrót co pierwsze pole pliku `.sha256`. W macOS te same pliki sprawdza `shasum -a 256 -c`.

## Publikacja zatwierdzonego wydania

Po przejrzeniu artefaktów `review-only`:

```text
Actions → Release → Run workflow
Tag: v1.0.0
Mode: publish
```

Zatwierdź oczekujące środowisko `release`. Zadanie tworzy wydanie GitHub ze znacznika i dołącza siedem pakietów oraz ich sumy. Plik informacji o wydaniu to `docs/en/release-notes-1.0.0.md` z tego znacznika.

Utwórz znacznik z przejrzanego commita przed pierwszym uruchomieniem. Nie wskazuj nim nazwy gałęzi ani niezacommitowanych plików:

```bash
git tag v1.0.0 <przejrzany-commit>
git push origin v1.0.0
```

Wypchnięcie znacznika nie uruchamia przepływu wydania. Robi to tylko **Run workflow**.

## Nieudane kompilacje

Nieudane zadanie platformy nie wysyła swoich plików, a zadanie publikacji się nie uruchamia. Po przejściowej awarii użyj **Re-run failed jobs**. Przy błędzie kodu lub pakowania popraw go nowym commitem, przesuń albo wymień znacznik tylko wtedy, gdy tego chcesz, i uruchom nowy przebieg `review-only`. Nie publikuj niepełnego zestawu. Jeśli próba publikacji zatrzyma się, zanim GitHub utworzy wydanie, nie ma czego kasować. Gdy wydanie już istnieje, przepływ zostawia je bez zmian.
