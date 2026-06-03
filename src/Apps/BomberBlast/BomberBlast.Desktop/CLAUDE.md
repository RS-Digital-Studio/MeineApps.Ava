# BomberBlast.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Landscape, Touch-Input). Generische Desktop-Publishing-Befehle →
[Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`BomberBlast.Shared/App.axaml.cs`, Desktop-Branch). Keine Platform-Factories gesetzt —
plattformspezifische Services fallen auf ihre Fallback-Implementierungen zurück:

| Interface | Desktop-Fallback |
|-----------|-----------------|
| `ISoundService` | `NullSoundService` |
| `IVibrationService` | `NullVibrationService` |
| `IPushNotificationService` | `NullPushNotificationService` |
| `IPlayGamesService` | `NullPlayGamesService` |
| `ICloudSaveService` | `NullCloudSaveService` |
| `IRemoteConfigService` | `DefaultsRemoteConfigService` (liest `remote_config_defaults.json`) |

## Build / Run

```bash
dotnet run     --project src/Apps/BomberBlast/BomberBlast.Desktop
dotnet publish src/Apps/BomberBlast/BomberBlast.Desktop -c Release -r win-x64    # bzw. linux-x64
```
