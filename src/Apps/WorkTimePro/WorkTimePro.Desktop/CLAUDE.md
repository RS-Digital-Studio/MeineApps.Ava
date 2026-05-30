# WorkTimePro.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App ist
Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (in
`WorkTimePro.Shared/Views/MainWindow.axaml`). Keine plattformspezifischen Service-Implementierungen
außer `DesktopNotificationService` (PowerShell-Toast auf Windows, `notify-send` auf Linux) und
`DesktopFileShareService` (Clipboard-Fallback). `IHapticService` fällt auf `NoOpHapticService`
zurück, keine AdMob-/Billing-Factories.

## Build / Run

```bash
dotnet run     --project src/Apps/WorkTimePro/WorkTimePro.Desktop
dotnet publish src/Apps/WorkTimePro/WorkTimePro.Desktop -c Release -r win-x64    # bzw. linux-x64
```
