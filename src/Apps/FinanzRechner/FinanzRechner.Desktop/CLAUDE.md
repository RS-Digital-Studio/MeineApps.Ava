# FinanzRechner.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Build-Befehle → [App-Root-CLAUDE.md](../CLAUDE.md) bzw.
[Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (in
`FinanzRechner.Shared/Views/MainWindow.axaml`). Keine plattformspezifischen Service-Implementierungen —
`IFileShareService` fällt auf `DesktopFileShareService` zurück (kein Android-FileShare-Intent),
kein AdMob, kein Billing.
