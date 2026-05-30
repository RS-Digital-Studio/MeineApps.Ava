# Loading — Startup-Pipeline

| Datei | Zweck |
|-------|-------|
| `RechnerPlusLoadingPipeline.cs` | Erbt `LoadingPipelineBase` ([MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md)). Wird in `App.axaml.cs` → `RunLoadingAsync` ausgeführt. |

Schritte (sequentiell, gewichteter Fortschritt):

1. **CalcLib-Warm-Up** (im `MainViewModel`-Ctor) — JIT/Parser aufwärmen.
2. **History-Persistenz laden** (`IHistoryService` aus `IPreferencesService`-JSON).
3. **Memory-Persistenz laden** (M-Register aus Preferences).

`App.axaml.cs` hält die Splash mindestens **800 ms** sichtbar, damit die Sweep-Wellen-Animation
abläuft.
