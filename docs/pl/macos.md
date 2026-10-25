# Pakiety macOS

[English](../en/macos.md)

Te pakiety to kompilacje `1.0.0` z podpisem ad hoc. Podpis nie jest podpisem Developer ID. Pakiety nie są poświadczone (notarized). Wcześniej opublikowane bety pozostają bez zmian. Ręczny przepływ wydania wysyła te pliki macOS tylko wtedy, gdy buduje znacznik `v1.0.0`.

## Budowa

Z kopii roboczej, przy zainstalowanym SDK .NET 10:

```bash
sh packaging/macos/build-dmg.sh all
```

`osx-x64` albo `osx-arm64` buduje jedną architekturę. Skrypt publikuje samodzielną aplikację .NET 10, razem z natywnymi bibliotekami Avalonia, i usuwa tymczasowy katalog publikacji po zakończeniu. Nie czyta ani nie zapisuje `~/.config/powershell-session-recorder` i nie kopiuje istniejącej aplikacji z `/Applications`.

Wyniki w `artifacts/`, katalogu ignorowanym przez Git:

- `PowerShellSessionRecorder-1.0.0-osx-x64.dmg` oraz plik o tej samej nazwie z końcówką `.sha256`
- `PowerShellSessionRecorder-1.0.0-osx-arm64.dmg` oraz plik o tej samej nazwie z końcówką `.sha256`
- `PowerShellSessionRecorder-1.0.0-osx-x64.zip` i `PowerShellSessionRecorder-1.0.0-osx-arm64.zip`, każde z własnym plikiem `.sha256`
- sprawdzony katalog `.app` w `artifacts/osx-x64/` i `artifacts/osx-arm64/`

Wiersz sumy kontrolnej podaje nazwę pliku, który naprawdę powstał. W macOS skrypt podpisuje gotowy pakiet `.app` dopiero wtedy, gdy są już plik wykonywalny, `Info.plist`, ikona i natywne biblioteki. Najpierw podpisywane są zagnieżdżone pliki Mach-O, a potem pakiet jako `com.timewizard.powershellsessionrecorder`. Polecenie `codesign --verify --deep --strict --verbose=2` musi się powieść dla pakietu, archiwum zip i zamontowanego obrazu, zanim powstaną sumy kontrolne. Na Apple Silicon podpisany pakiet `osx-arm64` jest potem uruchamiany z `--smoke`. Ten proces nie otwiera okna. Potem `hdiutil` tworzy prywatny obraz do zapisu, kopiuje pakiet na jego własny punkt montowania i konwertuje go na skompresowany obraz. `hdiutil verify` sprawdza wynik. Wspólna ścieżka `-srcfolder` nie jest używana, bo po podpisaniu pakietu kończyła się błędem `Resource busy`. Na komputerze bez `codesign` skrypt nie tworzy tego podpisu. `xorriso` nadal może owinąć obraz ISO z Rock Ridge w nieskompresowany plik UDIF `.dmg` do lokalnego obejrzenia. Ten wariant sprawdzono w Ubuntu przez odczyt obrazu. Nie został zamontowany na komputerze Mac z tej kopii roboczej.

Identyfikator pakietu to `com.timewizard.powershellsessionrecorder`. Nazwa wyświetlana to PowerShell Session Recorder. `CFBundleShortVersionString` ma wartość `1.0.0`, a tekst informacji zawiera `1.0.0`. Ikona to `assets/branding/macos/AppIcon.icns`, utworzona z `assets/branding/app-icon-source.png`. Na Intelu użyj obrazu albo archiwum `osx-x64`. Na Apple Silicon użyj obrazu albo archiwum `osx-arm64`. Otwórz obraz dysku i przeciągnij **PowerShell Session Recorder** do Applications albo skopiuj `.app` z archiwum zip.

`Contents/MacOS` zawiera natywny program hosta i biblioteki Mach-O. Zestawy zarządzane, `PowerShellSessionRecorder.deps.json`, `PowerShellSessionRecorder.runtimeconfig.json` oraz katalogi językowe są w `Contents/Resources`. Host wskazuje `../Resources/PowerShellSessionRecorder.dll`, a plik deps kieruje biblioteki natywne z powrotem do `../MacOS`. Dzięki temu podziałowi `codesign --verify --deep --strict` akceptuje pakiet: zarządzana biblioteka DLL pozostawiona w `Contents/MacOS` jest traktowana jako niepodpisany kod zagnieżdżony.

Ta sama publikacja zapisuje w hoście położenie środowiska `../MacOS` względem aplikacji i tylko to położenie. Względna ścieżka zestawu wejściowego sprawia, że katalogiem aplikacji jest `Contents/Resources`. Nie ma w nim `libhostfxr.dylib`, więc domyślne wyszukiwanie hosta przechodzi do `DOTNET_ROOT`, a potem do instalacji .NET dla całego komputera. W aplikacji samodzielnej hostpolicy jest wtedy wymagany w tej instalacji, a nie obok `libcoreclr.dylib`. Ścieżka `../MacOS` jest liczona od `Contents/Resources`, czyli wskazuje `Contents/MacOS`, gdzie już są `libhostfxr.dylib`, `libhostpolicy.dylib` i `libcoreclr.dylib`. Aplikacja pozostaje samodzielna. Kopia robocza w Ubuntu nie może wykonać tego hosta Mach-O. Test dymny CI na Apple Silicon uruchomił podpisany pakiet `osx-arm64` i nie otwiera okna.

Program PowerShell 7 nie jest w pakiecie. Terminal.app należy do macOS i ten pakiet go nie instaluje.

## Gatekeeper

Podpis ad hoc pozwala systemowi macOS sprawdzić pakiet. Nie czyni aplikacji zaufaną aplikacją Developer ID. Gatekeeper ostrzeże przy pierwszym otwarciu. Zostaw Gatekeepera włączonego.

1. Otwórz plik `.dmg` i przeciągnij **PowerShell Session Recorder** do Applications albo rozpakuj `.app` z archiwum zip. Zastąpienie istniejącego pakietu dotyczy tylko tego pakietu. Ustawienia, historia i transkrypcje zostają w profilu użytkownika oraz w katalogu transkrypcji.
2. Kliknij aplikację z wciśniętym klawiszem Control, wybierz **Otwórz**, a potem ponownie **Otwórz**. To dopuszcza tę jedną aplikację. Nie wyłącza Gatekeepera.
3. Po sprawdzeniu pliku SHA-256 tego pobrania zaawansowaną możliwością jest usunięcie atrybutu kwarantanny tylko z tej zweryfikowanej kopii:

```bash
xattr -dr com.apple.quarantine "/Applications/PowerShell Session Recorder.app"
```

Nie uruchamiaj `sudo spctl --master-disable`. To wyłączyłoby Gatekeepera dla każdej aplikacji.

Te same wskazówki są w pliku `GATEKEEPER.txt` na obrazie dysku. Mac z procesorem Intel, także maszyna wirtualna Sequoia 15.7.9, potrzebuje obrazu `osx-x64`. Obraz `osx-arm64` jest osobną kompilacją. Jego host uruchomił test dymny CI na Apple Silicon. Ten test nie otwiera okna, a okna aplikacji na Apple Silicon nie sprawdzono funkcjonalnie.

## Co pozostaje ręczne

Test dymny CI na Apple Silicon przeszedł dla podpisanego hosta `osx-arm64`: odnajduje `PowerShellSessionRecorder.dll` w `Contents/Resources`, czyta konfigurację środowiska `net10.0`, ładuje `libcoreclr`, `libhostfxr`, `libhostpolicy`, Avalonię, Skia i HarfBuzz z `Contents/MacOS`, a Avalonia rejestruje `Avalonia.Native.AvaloniaNativePlatform`. Avalonia 12 zostawia `WindowingSubsystemName` puste dla tego backendu; nazwy publikują Skia i HarfBuzz. Test nie otwiera okna. macOS Intel sprawdzono na zainstalowanym `.app`, w tym wykrycie programu PowerShell, rejestrowanie w Terminal.app, transkrypcje i historię. Okno Apple Silicon nie było częścią tego testu funkcjonalnego. Pakiety `osx-x64` i `osx-arm64` są osobnymi kompilacjami.
