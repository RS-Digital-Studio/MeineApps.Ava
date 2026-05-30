# RechnerPlus.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`RechnerPlus.Shared/App.axaml.cs`). Keine plattformspezifischen Service-Implementierungen —
`IHapticService` fällt auf `NoOpHapticService` zurück, kein Android-Factory.

## Build / Run

```bash
dotnet run     --project src/Apps/RechnerPlus/RechnerPlus.Desktop
dotnet publish src/Apps/RechnerPlus/RechnerPlus.Desktop -c Release -r win-x64    # bzw. linux-x64
```
