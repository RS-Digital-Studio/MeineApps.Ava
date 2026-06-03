# GardenControl.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). Dient als **Entwicklungs-/Test-Host**
und als **Kiosk-App auf dem Raspberry Pi** (Fullscreen, kein Fensterrahmen, Auto-Login).
Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Architektur, Pi-Kiosk-Modus, Hardware und GPIO-Belegung → [GardenControl/CLAUDE.md](../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`GardenControl.Shared/App.axaml.cs`). Pi-Erkennung in `App.axaml.cs` entscheidet zwischen
Fullscreen-Kiosk und normalem 1200×800-Entwicklungsfenster — kein Plattform-Code hier.

## Build / Run

```bash
# Entwicklung (Mock-Hardware, kein Pi nötig)
dotnet run --project src/Apps/GardenControl/GardenControl.Desktop

# Pi cross-compile (linux-arm64, self-contained)
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r linux-arm64 --self-contained

# Windows
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r win-x64
```
