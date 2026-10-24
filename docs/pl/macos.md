# Pakiety macOS

[English](../en/macos.md)

Te pakiety to niepodpisana kompilacja `0.1.0-beta.2`. Nie są poświadczone (notarized) i nie są zaufane produkcyjnie. Opublikowane wydanie GitHub `v0.1.0-beta.2` pozostaje bez zmian, a przepływ wydania jeszcze nie wysyła plików macOS.

## Budowa

Z kopii roboczej, przy zainstalowanym SDK .NET 10:

```bash
sh packaging/macos/build-dmg.sh all
```

`osx-x64` albo `osx-arm64` buduje jedną architekturę. Skrypt publikuje samodzielną aplikację .NET 10, razem z natywnymi bibliotekami Avalonia, i usuwa tymczasowy katalog publikacji po zakończeniu. Nie czyta ani nie zapisuje `~/.config/powershell-session-recorder` i nie kopiuje istniejącej aplikacji z `/Applications`.

Wyniki w `artifacts/`, katalogu ignorowanym przez Git:

- `PowerShellSessionRecorder-0.1.0-beta.2-osx-x64.dmg` oraz plik o tej samej nazwie z końcówką `.sha256`
- `PowerShellSessionRecorder-0.1.0-beta.2-osx-arm64.dmg` oraz plik o tej samej nazwie z końcówką `.sha256`
- archiwum `.zip` pakietu `PowerShell Session Recorder.app` dla każdej architektury, z własnym plikiem `.sha256`
- sprawdzony katalog `.app` w `artifacts/osx-x64/` i `artifacts/osx-arm64/`

Wiersz sumy kontrolnej podaje nazwę pliku, który naprawdę powstał. W macOS obraz tworzy `hdiutil`, a `hdiutil verify` go sprawdza. Na komputerze bez `hdiutil` narzędzie `xorriso` buduje obraz ISO z Rock Ridge, a skrypt owija go w nieskompresowany plik UDIF `.dmg`. Ten wariant sprawdzono w Ubuntu przez odczyt obrazu. Nie został zamontowany na komputerze Mac z tej kopii roboczej.

Identyfikator pakietu to `com.timewizard.powershellsessionrecorder`. Nazwa wyświetlana to PowerShell Session Recorder. `CFBundleShortVersionString` ma wartość `0.1.0`, a tekst informacji zawiera `0.1.0-beta.2`. Ikona to `assets/branding/macos/AppIcon.icns`, utworzona z `assets/branding/app-icon-source.png`.

Program PowerShell 7 nie jest w pakiecie. Terminal.app należy do macOS i ten pakiet go nie instaluje.

## Gatekeeper

Aplikacja nie jest podpisana. Gatekeeper ostrzeże przy pierwszym otwarciu. Zostaw Gatekeepera włączonego.

1. Otwórz plik `.dmg` i przeciągnij **PowerShell Session Recorder** do Applications albo rozpakuj `.app` z archiwum zip. Zastąpienie istniejącego pakietu dotyczy tylko tego pakietu. Ustawienia, historia i transkrypcje zostają w profilu użytkownika oraz w katalogu transkrypcji.
2. Kliknij aplikację z wciśniętym klawiszem Control, wybierz **Otwórz**, a potem ponownie **Otwórz**. To dopuszcza tę jedną aplikację. Nie wyłącza Gatekeepera.
3. Aby usunąć atrybut kwarantanny tylko z tej kopii:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Nie uruchamiaj `sudo spctl --master-disable`. To wyłączyłoby Gatekeepera dla każdej aplikacji.

Te same wskazówki są w pliku `GATEKEEPER.txt` na obrazie dysku. Mac z procesorem Intel, także maszyna wirtualna Sequoia 15.7.9, potrzebuje obrazu `osx-x64`. Obraz `osx-arm64` jest osobną kompilacją i nie był uruchamiany na sprzęcie Apple Silicon.

## Co pozostaje ręczne

Otwarcie okna, wykrycie programu PowerShell 7.6.8, rejestrowanie w Terminal.app i skasowanie ukończonej transkrypcji nadal wymaga maszyny wirtualnej Intel. Udane sprawdzenie pakietu nie uruchamia aplikacji.
