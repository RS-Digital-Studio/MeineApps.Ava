# FitnessRechner.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`FitnessRechner.Shared/App.axaml.cs`). Keine plattformspezifischen Service-Implementierungen —
Haptic und Sound fallen auf No-Op zurück; Barcode-Scanner zeigt manuelle Eingabe
(`DesktopBarcodeService` gibt `null` zurück); Reminder sind No-Op.

---

## Build / Run

```bash
dotnet run     --project src/Apps/FitnessRechner/FitnessRechner.Desktop
dotnet publish src/Apps/FitnessRechner/FitnessRechner.Desktop -c Release -r win-x64    # bzw. linux-x64
```
