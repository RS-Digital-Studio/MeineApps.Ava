# RebornSaga.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first (Portrait-Spiel). Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow.Content = new MainView()`,
`MainWindow.DataContext = Services.GetRequiredService<MainViewModel>()`, `ShutdownRequested` →
`App.DisposeServices()` (siehe `RebornSaga.Shared/App.axaml.cs`). Keine plattformspezifischen
Service-Implementierungen: `IAudioService` → Desktop-Stub `AudioService` (kein `AudioServiceFactory`
gesetzt). Rewarded Ads → Desktop-Simulator (kein echter AdMob).

## Build / Run

```bash
dotnet run     --project src/Apps/RebornSaga/RebornSaga.Desktop
dotnet publish src/Apps/RebornSaga/RebornSaga.Desktop -c Release -r win-x64    # bzw. linux-x64
```
