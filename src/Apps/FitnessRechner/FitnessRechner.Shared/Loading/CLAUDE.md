# Loading — Startup-Pipeline

Startup-Logik für Shader-Preload, ViewModel-Initialisierung und Premium-Setup.
Generische Loading-Patterns → [MeineApps.UI](../../../../../UI/MeineApps.UI/CLAUDE.md)
(`LoadingPipelineBase`, `SkiaLoadingSplash`).
Mindest-Splash-Dauer, Fehler-Resilienz und `RunLoadingAsync` → [FitnessRechner.Shared](../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `FitnessRechnerLoadingPipeline.cs` | Einziger Startup-Schritt: Shader + ViewModel + Purchases parallel |

---

## Pipeline-Schritte

| Schritt | Gewicht | Was passiert |
|---------|---------|--------------|
| `Shader+ViewModel` | 45 | `ShaderPreloader.PreloadAll()` (Task.Run) + `MainViewModel`-Auflösung (Task.Run) + `IPurchaseService.InitializeAsync()` parallel via `Task.WhenAll` |

**Warum parallel?** Shader-Compilation auf der GPU und ViewModel-Auflösung (DI-Graph) sind
voneinander unabhängig. `IPurchaseService.InitializeAsync()` prüft den Premium-Status gegen
Google Play — ohne diesen Schritt könnten Geräte-/Datenwechsel dazu führen, dass der
Premium-Status erst beim nächsten Start wiederhergestellt wird.
