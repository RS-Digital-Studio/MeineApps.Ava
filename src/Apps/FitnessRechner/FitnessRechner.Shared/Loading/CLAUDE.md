# Loading — Startup-Pipeline

Startup-Logik für Shader-Preload, ViewModel-Initialisierung und Premium-Setup.
Generische Loading-Patterns → [MeineApps.UI](../../../../../UI/MeineApps.UI/CLAUDE.md)
(`LoadingPipelineBase`, `SkiaLoadingSplash`).

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
voneinander unabhängig. `PurchaseService.InitializeAsync()` prüft Premium-Status gegen
Google Play — ohne diesen Schritt könnten Geräte-/Datenwechsel dazu führen, dass
Premium-Status erst beim nächsten Start wiederhergestellt wird.

**Mindest-Splash:** 800 ms in `App.axaml.cs` — damit die EKG-Herzschlag-Animation sichtbar ist.

**Fehler-Resilienz:** Pipeline-Fehler werden in `RunLoadingAsync` gefangen → `FadeOut` ohne
Crash (kein Leerbildschirm).
