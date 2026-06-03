# Loading — Startup-Pipeline

`HandwerkerImperiumLoadingPipeline` erbt von `LoadingPipelineBase` (aus `MeineApps.UI.Loading`).
Entkoppelt blockierende Initialisierung vom App-Start und meldet Fortschritt über die
Basisklassen-Infrastruktur an den Splash-Screen.

Aufruf: `App.axaml.cs → RunLoadingAsync()` — dort wird auch die Mindest-Anzeigedauer
(`GameBalanceConstants.SplashMinimumDisplayMs`, 800ms) abgewartet.

---

## Pipeline-Schritte (3 Schritte, parallel wo möglich)

1. **Shader + ViewModel + Icons + Portraits** (Gewicht 40, parallel) — `ShaderPreloader.PreloadAll()`
   (Task.Run), MainViewModel-Graph-Konstruktion (`Dispatcher.UIThread.InvokeAsync` — UI-Thread!),
   alle 224 Bitmap-Icons (`GameIcon.PreloadAllAsync`), 20 Worker-Portraits (`IGameAssetService.PreloadAsync`).
2. **GameInit** (Gewicht 35) — `MainViewModel.InitializeAsync()` (Spielstand laden, Orders, Rewards),
   danach `IPurchaseService.InitializeAsync()` (Google Play Billing — nach SanitizeState, damit
   RestorePurchases den echten Premium-Status wiederherstellt).
3. **RemoteConfig** (Gewicht 5) — `IRemoteConfigService.InitializeAsync()` mit 5s-Timeout
   (App läuft mit Defaults auch ohne Netz), danach `IDailyBundleService.InitializeAsync()`; bei Timeout
   übernimmt ein deferred `ContinueWith`-Hook die Bundle-Init nach erfolgreichem Fetch.

---

## Gotcha — MainViewModel-Konstruktion nur auf dem UI-Thread

Die Pipeline löst den `MainViewModel` **by-design** auf (Schritt 1 baut den Graphen, Schritt 2 ruft
`InitializeAsync()` — übernimmt was früher in `MainViewModel.InitializeAsync()` lag). Die Auflösung
MUSS über `Dispatcher.UIThread.InvokeAsync(...)` laufen, NICHT über `Task.Run`: VM-Ctors erzeugen
UI-thread-affine Objekte (`SolidColorBrush`), `DispatcherTimer` und Event-Abos. Da `MainViewModel`
Singleton ist und `MainActivity` ihn ebenfalls früh auflöst, stellt der UI-Thread-Resolve sicher,
dass der Graph deterministisch auf dem UI-Thread entsteht (kein latenter Background-Thread-Crash).
