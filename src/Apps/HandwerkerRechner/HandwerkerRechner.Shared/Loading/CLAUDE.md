# Loading — Startup-Pipeline

Namespace: `HandwerkerRechner.Loading`. Verwaltet die Initialisierungsschritte beim App-Start,
die auf dem `SkiaLoadingSplash` als Fortschrittsbalken sichtbar sind.

## Dateien

| Datei | Zweck |
|-------|-------|
| `HandwerkerRechnerLoadingPipeline.cs` | Erbt von `LoadingPipelineBase` (MeineApps.UI). Registriert Lade-Schritte als `LoadingStep`-Objekte. |

## Pipeline-Schritte

Aktuell ein Schritt (Schritt 1 — Gewicht 45):
- **Shader+ViewModel** parallel via `Task.WhenAll`:
  1. `ShaderPreloader.PreloadAll()` auf `Task.Run` (GPU-Shader kompilieren).
  2. `services.GetRequiredService<MainViewModel>()` auf `Task.Run` (VM-Graph instanziieren —
     löst alle Singleton-Services transitiv auf).
  3. `IPurchaseService.InitializeAsync()` (Google Play Billing abgleichen → Premium-Status
     wiederherstellen bei Geräte-/Datenwechsel).

## Wichtig: VM-Instanziierung auf Background-Thread

`MainViewModel` wird hier auf einem `Task.Run`-Thread erzeugt. Das ist erlaubt, weil
`MainViewModel` **keine** Avalonia-UI-Objekte im Konstruktor erstellt (keine `SolidColorBrush`,
keine `IBrush`-`[ObservableProperty]`). Würde das VM UI-Objekte anlegen, müsste die Erzeugung
auf `Dispatcher.UIThread.InvokeAsync(...)` verlagert werden (Fehler-Muster in Haupt-CLAUDE.md
Troubleshooting: "Render-Crash calling thread cannot access").

## App.axaml.cs — Splash-Mindestdauer

`RunLoadingAsync` erzwingt mindestens 800 ms Splash-Anzeige damit die Maßband-Animation sichtbar
ist — unabhängig davon wie schnell die Pipeline tatsächlich abschließt.
