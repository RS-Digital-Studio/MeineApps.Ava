# BomberBlast.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Landscape, Touch-Input). Generische Desktop-Publishing-Befehle →
[Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`BomberBlast.Shared/App.axaml.cs`, Desktop-Branch). Keine plattformspezifischen Service-Implementierungen —
`ISoundService`/`IVibrationService`/`IPushNotificationService`/`IPlayGamesService`/`ICloudSaveService`
fallen auf ihre jeweiligen Null-Implementierungen zurück. Keine Platform-Factories gesetzt.

## Build / Run

```bash
dotnet run     --project src/Apps/BomberBlast/BomberBlast.Desktop
dotnet publish src/Apps/BomberBlast/BomberBlast.Desktop -c Release -r win-x64    # bzw. linux-x64
```
