# GardenControl.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). Dient als **Entwicklungs-/Test-Host**
und als **Kiosk-App auf dem Raspberry Pi** (Fullscreen, kein Fensterrahmen, Auto-Login).
Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`GardenControl.Shared/App.axaml.cs`). Pi-Erkennung in `App.axaml.cs` entscheidet zwischen
Fullscreen-Kiosk und normalem 1200×800-Entwicklungsfenster — kein Plattform-Code hier.

## Pi-Kiosk-Deployment

Auf dem Pi läuft dieser Desktop-Host als Kiosk (Auto-Login + Autostart via `install-pi5.sh`).
Der `GardenControl.Server` läuft parallel als systemd-Service. Die App verbindet sich
automatisch auf `http://localhost:5000` wenn `App.IsRunningOnPi == true`.

## Build / Run

```bash
# Entwicklung
dotnet run --project src/Apps/GardenControl/GardenControl.Desktop

# Pi cross-compile (linux-arm64)
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r linux-arm64 --self-contained

# Windows
dotnet publish src/Apps/GardenControl/GardenControl.Desktop -c Release -r win-x64
```
