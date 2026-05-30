# Loading — Startup-Pipeline

Startup-Sequenz die zwischen Splash-Anzeige und erstem UI-Render ausgeführt wird.
Generische Loading-Patterns → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `WorkTimeProLoadingPipeline.cs` | 2-stufige Startup-Sequenz: DB+Shader parallel, dann ViewModel. |

## Pipeline-Schritte

| Schritt | Name | Gewicht | Inhalt |
|---------|------|---------|--------|
| 1 | Init (45%) | 45 | `IDatabaseService.InitializeAsync()` + `ShaderPreloader.PreloadAll()` + `IPurchaseService.InitializeAsync()` **parallel** via `Task.WhenAll`. Danach (sequenziell): `IReminderService.InitializeAsync()` (hängt von DB ab). |
| 2 | ViewModel (20%) | 20 | `MainViewModel` aus DI auflösen + `WaitForInitializationAsync()`. |

**Warum Reminder sequenziell nach DB?** `ReminderService.InitializeAsync()` liest Settings aus
der DB — die DB muss vollständig initialisiert sein. In derselben Stage belassen (kein extra
`ProgressChanged`-Event-Roundtrip).

**Splash-Mindestdauer:** `App.axaml.cs` wartet mindestens 800ms damit die Stechuhr-Animation
in `WorkTimeProSplashRenderer` sichtbar ist.

**Fehler-Handling:** Pipeline-Fehler werden in `App.RunLoadingAsync` gefangen → `splash.FadeOut()`
läuft trotzdem (kein Leerbildschirm).
