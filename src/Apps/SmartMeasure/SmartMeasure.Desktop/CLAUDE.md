# SmartMeasure.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Samsung Galaxy S25 Ultra als Zielgerät).
Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` 450×900 (Portrait-Simulation).
Keine plattformspezifischen Service-Implementierungen:
- `IBleService` → `MockBleService` (simuliert RTK-Daten + Edge-Cases).
- `IArCaptureService` → `MockArCaptureService`.
- `IAppPaths` → `AppPaths` (verwendet `Environment.SpecialFolder.LocalApplicationData`).

Mock-Modus schaltet Debug-Panel in `SurveyView` frei: `IsMockMode = true` zeigt Buttons für
`CycleFixDegradation`, `SimulatePacketLoss`, `SimulateBatteryDrain`, `SimulateMagLoss`, `SimulateSpuriousDisconnect`.

---

## Build / Run

```bash
dotnet run     --project src/Apps/SmartMeasure/SmartMeasure.Desktop
dotnet publish src/Apps/SmartMeasure/SmartMeasure.Desktop -c Release -r win-x64
dotnet publish src/Apps/SmartMeasure/SmartMeasure.Desktop -c Release -r linux-x64
```
