# WorkTimePro.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App ist
Android-first. Build-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (in
`WorkTimePro.Shared/Views/MainWindow.axaml`). Keine plattformspezifischen Service-Implementierungen
außer `DesktopNotificationService` (in `WorkTimePro.Shared/Services/` — PowerShell-Toast auf
Windows, `notify-send` auf Linux). `DesktopFileShareService` (Clipboard-Fallback) und
`NoOpHapticService` (Desktop ohne Vibrations-Hardware) kommen aus `MeineApps.Core.Ava`.
Keine AdMob-/Billing-Factories.
