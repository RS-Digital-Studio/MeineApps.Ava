# HandwerkerRechner.Desktop — Desktop-Host

Desktop-Einstiegsprojekt (`net10.0`, Windows/Linux). **Nur für Entwicklung/Test** — die App
ist Android-first. Generische Desktop-Publishing-Befehle → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `Program.cs` | Entry Point. `BuildAvaloniaApp().UsePlatformDetect().WithInterFont().LogToTrace()` → `StartWithClassicDesktopLifetime(args)`. |

App läuft über `IClassicDesktopStyleApplicationLifetime` → `MainWindow` (siehe
`HandwerkerRechner.Shared/App.axaml.cs`). Keine plattformspezifischen Service-Implementierungen —
`IRewardedAdService` und `IPurchaseService` fallen auf die Desktop-Stubs aus `AddMeineAppsPremium()`
zurück (kein Android-Factory gesetzt). `IFileShareService` → `DesktopFileShareService`.
`IPhotoPickerService` → `DesktopPhotoPickerService`.

## Build / Run

```bash
dotnet run     --project src/Apps/HandwerkerRechner/HandwerkerRechner.Desktop
dotnet publish src/Apps/HandwerkerRechner/HandwerkerRechner.Desktop -c Release -r win-x64    # bzw. linux-x64
```
