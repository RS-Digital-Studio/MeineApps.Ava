# FinanzRechner.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`FinanzRechner.Shared/App.axaml.cs`). Keine plattformspezifischen Service-Implementierungen —
`IFileShareService` fällt auf `DesktopFileShareService` zurück (kein Android-FileShare-Intent),
kein AdMob, kein Billing.

---

## Build / Run

```bash
dotnet run     --project src/Apps/FinanzRechner/FinanzRechner.Desktop
dotnet publish src/Apps/FinanzRechner/FinanzRechner.Desktop -c Release -r win-x64    # bzw. linux-x64
```
