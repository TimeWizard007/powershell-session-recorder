# Pakiety macOS

[English](../en/macos.md)

Te pakiety to kompilacja `0.1.0-beta.2` z podpisem ad hoc do testów wewnętrznych. Podpis nie jest podpisem Developer ID. Pakiety nie są poświadczone (notarized) i nie są zaufane produkcyjnie. Opublikowane wydanie GitHub `v0.1.0-beta.2` pozostaje bez zmian, a przepływ wydania jeszcze nie wysyła plików macOS.

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

Wiersz sumy kontrolnej podaje nazwę pliku, który naprawdę powstał. W macOS skrypt podpisuje gotowy pakiet `.app` dopiero wtedy, gdy są już plik wykonywalny, `Info.plist`, ikona i natywne biblioteki. Najpierw podpisywane są zagnieżdżone pliki Mach-O, a potem pakiet jako `com.timewizard.powershellsessionrecorder`. Polecenie `codesign --verify --deep --strict --verbose=2` musi się powieść dla pakietu, archiwum zip i zamontowanego obrazu, zanim powstaną sumy kontrolne. Na Apple Silicon podpisany pakiet `osx-arm64` jest potem uruchamiany z `--smoke`. Ten proces nie otwiera okna. Potem `hdiutil` tworzy prywatny obraz do zapisu, kopiuje pakiet na jego własny punkt montowania i konwertuje go na skompresowany obraz. `hdiutil verify` sprawdza wynik. Wspólna ścieżka `-srcfolder` nie jest używana, bo po podpisaniu pakietu kończyła się błędem `Resource busy`. Na komputerze bez `codesign` skrypt nie tworzy tego podpisu. `xorriso` nadal może owinąć obraz ISO z Rock Ridge w nieskompresowany plik UDIF `.dmg` do lokalnego obejrzenia. Ten wariant sprawdzono w Ubuntu przez odczyt obrazu. Nie został zamontowany na komputerze Mac z tej kopii roboczej.

Identyfikator pakietu to `com.timewizard.powershellsessionrecorder`. Nazwa wyświetlana to PowerShell Session Recorder. `CFBundleShortVersionString` ma wartość `0.1.0`, a tekst informacji zawiera `0.1.0-beta.2`. Ikona to `assets/branding/macos/AppIcon.icns`, utworzona z `assets/branding/app-icon-source.png`.

`Contents/MacOS` zawiera natywny program hosta i biblioteki Mach-O. Zestawy zarządzane, `PowerShellSessionRecorder.deps.json`, `PowerShellSessionRecorder.runtimeconfig.json` oraz katalogi językowe są w `Contents/Resources`. Host wskazuje `../Resources/PowerShellSessionRecorder.dll`, a plik deps kieruje biblioteki natywne z powrotem do `../MacOS`. Dzięki temu podziałowi `codesign --verify --deep --strict` akceptuje pakiet: zarządzana biblioteka DLL pozostawiona w `Contents/MacOS` jest traktowana jako niepodpisany kod zagnieżdżony.

Ta sama publikacja zapisuje w hoście położenie środowiska `../MacOS` względem aplikacji i tylko to położenie. Względna ścieżka zestawu wejściowego sprawia, że katalogiem aplikacji jest `Contents/Resources`. Nie ma w nim `libhostfxr.dylib`, więc domyślne wyszukiwanie hosta przechodzi do `DOTNET_ROOT`, a potem do instalacji .NET dla całego komputera. W aplikacji samodzielnej hostpolicy jest wtedy wymagany w tej instalacji, a nie obok `libcoreclr.dylib`. Ścieżka `../MacOS` jest liczona od `Contents/Resources`, czyli wskazuje `Contents/MacOS`, gdzie już są `libhostfxr.dylib`, `libhostpolicy.dylib` i `libcoreclr.dylib`. Aplikacja pozostaje samodzielna. Ta kopia robocza nie uruchamiała tego hosta; nadal musi go uruchomić runner Apple Silicon.

Program PowerShell 7 nie jest w pakiecie. Terminal.app należy do macOS i ten pakiet go nie instaluje.

## Gatekeeper

Podpis ad hoc pozwala systemowi macOS sprawdzić pakiet. Nie czyni aplikacji zaufaną aplikacją Developer ID. Gatekeeper ostrzeże przy pierwszym otwarciu. Zostaw Gatekeepera włączonego.

1. Otwórz plik `.dmg` i przeciągnij **PowerShell Session Recorder** do Applications albo rozpakuj `.app` z archiwum zip. Zastąpienie istniejącego pakietu dotyczy tylko tego pakietu. Ustawienia, historia i transkrypcje zostają w profilu użytkownika oraz w katalogu transkrypcji.
2. Kliknij aplikację z wciśniętym klawiszem Control, wybierz **Otwórz**, a potem ponownie **Otwórz**. To dopuszcza tę jedną aplikację. Nie wyłącza Gatekeepera.
3. Aby usunąć atrybut kwarantanny tylko z tej kopii:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Nie uruchamiaj `sudo spctl --master-disable`. To wyłączyłoby Gatekeepera dla każdej aplikacji.

Te same wskazówki są w pliku `GATEKEEPER.txt` na obrazie dysku. Mac z procesorem Intel, także maszyna wirtualna Sequoia 15.7.9, potrzebuje obrazu `osx-x64`. Obraz `osx-arm64` jest osobną kompilacją i nie był uruchamiany na sprzęcie Apple Silicon.

## Co pozostaje ręczne

Test dymny CI na Apple Silicon sprawdza, że podpisany host `osx-arm64` odnajduje `PowerShellSessionRecorder.dll` w `Contents/Resources`, czyta konfigurację środowiska `net10.0`, ładuje `libcoreclr`, `libhostfxr`, `libhostpolicy`, Avalonię, Skia i HarfBuzz z `Contents/MacOS` oraz że Avalonia zarejestrowała `Avalonia.Native.AvaloniaNativePlatform`. Avalonia 12 zostawia `WindowingSubsystemName` puste dla tego backendu; nazwy publikują Skia i HarfBuzz. Test nie otwiera okna i nie czyta `~/.config/powershell-session-recorder`. Pakiet Intel `osx-x64` jest podpisywany i sprawdzany przez `codesign`, ale ten runner nie może go uruchomić. Otwarcie okna, wykrycie programu PowerShell 7.6.8, rejestrowanie w Terminal.app i skasowanie ukończonej transkrypcji nadal wymaga maszyny wirtualnej Intel.
