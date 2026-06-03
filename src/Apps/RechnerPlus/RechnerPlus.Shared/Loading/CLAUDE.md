# Loading — Startup-Pipeline

> Pipeline-Framework (`LoadingPipelineBase`, `LoadingStep`, `ShaderPreloader`) →
> [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md). Aufruf aus `App.axaml.cs` →
> [RechnerPlus.Shared](../CLAUDE.md).

| Datei | Zweck |
|-------|-------|
| `RechnerPlusLoadingPipeline.cs` | Erbt `LoadingPipelineBase`. Registriert die Lade-Schritte mit Gewichtung für den Fortschrittsbalken. |

## Schritte

Ein einziger gewichteter Schritt (Weight 40):

**Shader + ViewModel** — `ShaderPreloader.PreloadAll()` und `MainViewModel`-Auflösung aus
dem DI-Container laufen **parallel** (`Task.WhenAll`). Reihenfolge ist egal, weil beide
voneinander unabhängig sind. Der Shader-Preload verhindert Jank beim ersten Rendern.

`App.axaml.cs` hält die Splash mindestens **800 ms** sichtbar, damit die Sweep-Wellen-Animation
vollständig abläuft (Details → [RechnerPlus.Shared](../CLAUDE.md)).
