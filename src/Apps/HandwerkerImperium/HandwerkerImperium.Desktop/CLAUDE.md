# HandwerkerImperium.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. Registriert `DesktopAudioService` als `App.AudioServiceFactory` (NAudio auf Windows, ffplay-Fallback auf Linux/macOS), dann `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`HandwerkerImperium.Shared/App.axaml.cs`). Keine AdMob-/Billing-Factories — Premium-Services
fallen auf Desktop-Stubs zurück (`NullAdService`, `StubPurchaseService`).

## Build / Run

```bash
dotnet run     --project src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop
dotnet publish src/Apps/HandwerkerImperium/HandwerkerImperium.Desktop -c Release -r win-x64    # bzw. linux-x64
```
