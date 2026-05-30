# ZeitManager.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`ZeitManager.Shared/App.axaml.cs`). `ConfigurePlatformServices` wird NICHT gesetzt →
Fallback auf `DesktopNotificationService`. `IShakeDetectionService` → `DesktopShakeDetectionService`
(simuliert Shake per Button, kein Accelerometer). `IHapticService` → `NoOpHapticService`.

## Build / Run

```bash
dotnet run     --project src/Apps/ZeitManager/ZeitManager.Desktop
dotnet publish src/Apps/ZeitManager/ZeitManager.Desktop -c Release -r win-x64    # bzw. linux-x64
```
