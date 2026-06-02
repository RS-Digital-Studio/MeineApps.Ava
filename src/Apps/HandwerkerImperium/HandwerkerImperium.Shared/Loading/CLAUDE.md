# Loading — Startup-Pipeline

Startup-Sequenz die blockierende Initialisierung vom App-Start entkoppelt und
dem Splash-Screen Progress-Updates liefert.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `HandwerkerImperiumLoadingPipeline.cs` | Führt alle Startup-Schritte sequential/parallel aus. Meldet Fortschritt via `ProgressChanged(double progress, string text)` an Splash |

---

## Pipeline-Schritte (3 Schritte, parallel wo möglich)

Die Pipeline wird in `App.axaml.cs → RunLoadingAsync()` ausgeführt:

1. **Shader + ViewModel + Icons + Portraits** (Gewicht 40, parallel) — `ShaderPreloader.PreloadAll()`
   (Task.Run), MainViewModel-Graph-Konstruktion (`Dispatcher.UIThread.InvokeAsync` — UI-Thread!),
   alle 224 Bitmap-Icons (`GameIcon.PreloadAllAsync`), 20 Worker-Portraits (`IGameAssetService.PreloadAsync`).
2. **GameInit** (Gewicht 35) — `MainViewModel.InitializeAsync()` (Spielstand laden, Orders, Rewards),
   danach `IPurchaseService.InitializeAsync()` (Google Play Billing — nach SanitizeState, damit
   RestorePurchases den echten Premium-Status wiederherstellt).
3. **RemoteConfig + DailyBundle** (Gewicht 5) — `IRemoteConfigService.InitializeAsync()` mit 5s-Timeout
   (App läuft mit Defaults auch ohne Netz), danach `IDailyBundleService.InitializeAsync()`; bei Timeout
   übernimmt ein deferred `ContinueWith`-Hook die Bundle-Init nach erfolgreichem Fetch.

---

## Gotcha — Splash-Mindestanzeigedauer

`RunLoadingAsync` wartet nach der Pipeline auf `GameBalanceConstants.SplashMinimumDisplayMs`
(800ms). Dies stellt sicher dass die Splash-Animation sichtbar ist auch wenn die Pipeline
schneller als 800ms abgeschlossen ist. Der Wert liegt in `GameBalanceConstants` — nicht hardcoded.

## Gotcha — MainViewModel-Konstruktion nur auf dem UI-Thread

Die Pipeline löst den `MainViewModel` **by-design** auf (Schritt 1 baut den Graphen, Schritt 2 ruft
`InitializeAsync()` — übernimmt was früher in `MainViewModel.InitializeAsync()` lag). Die Auflösung
MUSS über `Dispatcher.UIThread.InvokeAsync(...)` laufen, NICHT über `Task.Run`: VM-Ctors erzeugen
UI-thread-affine Objekte (`SolidColorBrush`), `DispatcherTimer` und Event-Abos. Da `MainViewModel`
Singleton ist und `MainActivity` ihn ebenfalls früh auflöst, stellt der UI-Thread-Resolve sicher,
dass der Graph deterministisch auf dem UI-Thread entsteht (kein latenter Background-Thread-Crash).
