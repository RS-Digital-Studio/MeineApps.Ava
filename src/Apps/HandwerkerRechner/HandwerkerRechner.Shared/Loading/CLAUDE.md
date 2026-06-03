# Loading — Startup-Pipeline

Namespace: `HandwerkerRechner.Loading`. Verwaltet die Initialisierungsschritte beim App-Start,
die auf dem `SkiaLoadingSplash` als Fortschrittsbalken sichtbar sind.
Composition Root + `RunLoadingAsync`-Aufruf → [HandwerkerRechner.Shared/CLAUDE.md](../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `HandwerkerRechnerLoadingPipeline.cs` | Erbt von `LoadingPipelineBase` (MeineApps.UI). Registriert Lade-Schritte als `LoadingStep`-Objekte. |

## Pipeline-Schritte

Ein Schritt (Gewicht 45), alle drei Tasks parallel via `Task.WhenAll`:

1. `ShaderPreloader.PreloadAll()` auf `Task.Run` — GPU-Shader kompilieren.
2. `services.GetRequiredService<MainViewModel>()` auf `Task.Run` — VM-Graph instanziieren,
   löst alle Singleton-Services transitiv auf.
3. `IPurchaseService.InitializeAsync()` direkt — Google Play Billing abgleichen,
   Premium-Status bei Geräte-/Datenwechsel wiederherstellen.

## Gotcha: VM-Instanziierung auf Background-Thread

`MainViewModel` wird auf einem `Task.Run`-Thread erzeugt. Das ist nur erlaubt, weil
`MainViewModel` **keine** Avalonia-UI-Objekte im Konstruktor erstellt (keine `SolidColorBrush`,
keine `IBrush`-`[ObservableProperty]`). Würde das VM UI-Objekte anlegen, müsste die Erzeugung
auf `Dispatcher.UIThread.InvokeAsync(...)` verlagert werden — andernfalls Render-Crash
("calling thread cannot access"). Detailliertes Thread-Safety-Pattern → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).
