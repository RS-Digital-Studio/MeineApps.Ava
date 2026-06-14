# Loading — Startup-Pipeline

Namespace: `HandwerkerRechner.Loading`. Verwaltet die Initialisierungsschritte beim App-Start,
die auf dem `SkiaLoadingSplash` als Fortschrittsbalken sichtbar sind.
Composition Root + `RunLoadingAsync`-Aufruf → [HandwerkerRechner.Shared/CLAUDE.md](../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `HandwerkerRechnerLoadingPipeline.cs` | Erbt von `LoadingPipelineBase` (MeineApps.UI). Registriert Lade-Schritte als `LoadingStep`-Objekte. |

## Pipeline-Schritte

Ein Schritt (Gewicht 45):

1. `IPurchaseService.InitializeAsync()` direkt — Google Play Billing abgleichen,
   Premium-Status bei Geräte-/Datenwechsel wiederherstellen (parallel).
2. `services.GetRequiredService<MainViewModel>()` via `Dispatcher.UIThread.InvokeAsync` —
   VM-Graph auf dem **UI-Thread** instanziieren, löst alle Singleton-Services transitiv auf.

**Kein Shader-Preload:** HandwerkerRechner rendert KEINEN der 12 SkSL-Effekte — weder direkt
noch über ein MeineApps.UI-Control. Alle Blueprint-Visualisierungen (`SkiaBlueprintCanvas` etc.)
nutzen klassische SkiaSharp-Gradienten/Pfade ohne SkSL. `PreloadAll()` hätte hier 12 nie genutzte
Shader (bis 2,4s auf Android) kompiliert.

## Gotcha: VM-Instanziierung NIE auf Background-Thread

`MainViewModel` wird bewusst via `Dispatcher.UIThread.InvokeAsync(...)` erzeugt — **nicht**
auf `Task.Run`. ViewModels haben UI-Thread-Affinität (Avalonia-UI-Objekte wie
`SolidColorBrush` im Objektgraphen), eine Background-Erzeugung riskiert Render-Crashes
("calling thread cannot access"). Beim Erweitern der Pipeline: nur echte Background-Arbeit
(Shader-Preload, I/O, Service-`InitializeAsync`) in `Task.Run` legen.
Detailliertes Thread-Safety-Pattern → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
