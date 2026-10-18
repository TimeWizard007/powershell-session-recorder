# Etap 1

[English](../en/checkpoint-1.md)

Etap 1 jest fundamentem Rejestratora sesji PowerShell. Rozwiązanie się kompiluje, okno Avalonia się otwiera, a administrator może przygotować opis sesji. Aplikacja nie rejestruje jeszcze programu PowerShell.

## W zakresie

- Układ rozwiązania: Core, aplikacja Avalonia i testy
- Interfejs po angielsku i po polsku
- Formularz sesji, walidacja i podgląd nazwy pliku
- Wybór lokalnego folderu i ustawienia JSON bieżącego użytkownika
- Reguły nazw plików zgodne z systemem Windows, które przy żądaniu unikalnej nazwy nie nadpisują istniejącego pliku

## Poza zakresem

- Uruchamianie `pwsh` albo `powershell.exe`
- Zapis transkrypcji
- Podnoszenie uprawnień i mechanizm UAC
- Rzeczywista historia sesji

Przycisk **Uruchom sesję** pozostaje wyłączony celowo. Historia pozostaje pusta celowo.

## Sprawdzenie

```bash
dotnet test PowerShellSessionRecorder.sln
dotnet run --project src/PowerShellSessionRecorder.App/PowerShellSessionRecorder.App.csproj
```

Polecenie uruchomienia wymaga sesji graficznej. Instrukcja kompilacji i testów jest w [polskim README](../../README.pl.md).
