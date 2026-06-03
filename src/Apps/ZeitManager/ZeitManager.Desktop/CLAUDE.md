# ZeitManager.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`ZeitManager.Shared/App.axaml.cs`). `ConfigurePlatformServices` wird NICHT gesetzt:

| Service | Desktop-Impl | Anmerkung |
|---------|-------------|-----------|
| `INotificationService` | `DesktopNotificationService` | Vollwertige OS-Benachrichtigungen: PowerShell-Toast (Windows), `notify-send` (Linux). Scheduling per `ConcurrentDictionary`+`Task.Delay`. |
| `IShakeDetectionService` | `DesktopShakeDetectionService` | Kein physischer Sensor (`HasPhysicalSensor = false`). `SimulateShake()` feuert `ShakeDetected`-Event — für manuelle Tests. |
| `IHapticService` | `NoOpHapticService` | Keine Vibrations-Hardware auf Desktop (definiert in `MeineApps.Core.Ava`). |

## Build / Run

```bash
dotnet run     --project src/Apps/ZeitManager/ZeitManager.Desktop
dotnet publish src/Apps/ZeitManager/ZeitManager.Desktop -c Release -r win-x64    # bzw. linux-x64
```
