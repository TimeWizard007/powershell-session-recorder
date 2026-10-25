# Rejestrator sesji PowerShell 1.0.0

[English](../en/release-notes-1.0.0.md)

Pierwsze stabilne wydanie. Aplikacja podaje `1.0.0`. Nazwy pakietów używają `1.0.0` i nie mają przyrostka przedpremierowego. Wcześniej opublikowane bety pozostają dostępne i nie są zastępowane.

Ten plik opisuje wydanie. Sam plik nie publikuje wydania GitHub. Wydanie dla znacznika `v1.0.0` tworzy ręczny przepływ po zatwierdzeniu.

## Sprawdzone

- Windows 10/11 x64: aplikacja startuje, wykrywa program PowerShell, rejestruje sesję, zapisuje transkrypcję i obsługuje historię.
- Ubuntu 24.04 amd64: ten sam przebieg przez GNOME Terminal i program PowerShell 7.
- macOS Intel: zainstalowany pakiet `.app` startuje, wykrywa program PowerShell, rejestruje w Terminal.app, zapisuje transkrypcje i obsługuje historię.
- Apple Silicon: zapakowany host `osx-arm64` przechodzi test dymny CI na macOS. Ten test nie otwiera okna. Graficzny interfejs Apple Silicon nie był częścią testu funkcjonalnego.

## Pakiety

Każdy plik powstaje z commita oznaczonego znacznikiem. Przy każdym pakiecie jest plik `.sha256`.

- `powershell-session-recorder_1.0.0-1_amd64.deb` oraz `.sha256` — Ubuntu 24.04 amd64.
- `powershell-session-recorder-1.0.0-linux-x64.tar.gz` oraz `.sha256` — archiwum Linuksa x64 dla użytkownika.
- `PowerShellSessionRecorder-1.0.0-win-x64-setup.exe` oraz `.sha256` — instalator Windows x64.
- `PowerShellSessionRecorder-1.0.0-osx-x64.dmg` oraz `.sha256` — obraz dysku macOS Intel.
- `PowerShellSessionRecorder-1.0.0-osx-x64.zip` oraz `.sha256` — pakiet aplikacji macOS Intel.
- `PowerShellSessionRecorder-1.0.0-osx-arm64.dmg` oraz `.sha256` — obraz dysku Apple Silicon.
- `PowerShellSessionRecorder-1.0.0-osx-arm64.zip` oraz `.sha256` — pakiet aplikacji Apple Silicon.

Nie ma osobnego przenośnego archiwum ZIP dla Windows. Wydaniem Windows jest instalator. Linux nie ma pakietu RPM, Snap ani Flatpak. macOS nie ma pliku `.pkg` i nie jest poświadczony.

## Podpis macOS

Pakiety macOS mają podpis ad hoc, żeby `codesign --verify --deep --strict` mógł je sprawdzić. Nie są podpisane identyfikatorem Developer ID i nie są poświadczone. Gatekeeper może ostrzec przy pierwszym otwarciu. Zostaw Gatekeepera włączonego. Instalację i zaawansowane usuwanie kwarantanny opisuje [macOS](macos.md).

## Aktualizacje

`v1.0.0` jest stabilnym wydaniem GitHub. Kanał stabilny może je zaproponować. Znaczniki z łącznikiem, w tym istniejące bety, są oznaczane jako przedpremierowe i nie są proponowane na kanale stabilnym. Sprawdzanie niczego nie pobiera i nie instaluje.

Przewodnik: [Przewodnik](przewodnik.md).
