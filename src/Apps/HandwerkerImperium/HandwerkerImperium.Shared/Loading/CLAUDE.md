# Loading — Startup-Pipeline

Startup-Sequenz die blockierende Initialisierung vom App-Start entkoppelt und
dem Splash-Screen Progress-Updates liefert.

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `HandwerkerImperiumLoadingPipeline.cs` | Führt alle Startup-Schritte sequential/parallel aus. Meldet Fortschritt via `ProgressChanged(double progress, string text)` an Splash |

---

## Pipeline-Schritte (parallel wo möglich)

Die Pipeline wird in `App.axaml.cs → RunLoadingAsync()` ausgeführt:

1. **GameAssetService Preload** — häufig benötigte WebP-Bitmaps vorladen (Workshop-Karten, Meister Hans, Worker-Avatare)
2. **SkSL-Shader kompilieren** — `GameJuiceEngine`-Shader vorkompilieren (verhindert Hitch beim ersten Render)
3. **Icons initialisieren** — `GameIconRenderer`-Cache aufbauen
4. **Purchases initialisieren** — `IPurchaseService.InitializeAsync()` (Google Play Billing)
5. **RemoteConfig laden** — `IRemoteConfigService.InitializeAsync()` (App funktioniert mit Defaults auch ohne Netz)
6. **DailyBundle prüfen** — `IDailyBundleService.CheckAsync()`

---

## Gotcha — Splash-Mindestanzeigedauer

`RunLoadingAsync` wartet nach der Pipeline auf `GameBalanceConstants.SplashMinimumDisplayMs`
(800ms). Dies stellt sicher dass die Splash-Animation sichtbar ist auch wenn die Pipeline
schneller als 800ms abgeschlossen ist. Der Wert liegt in `GameBalanceConstants` — nicht hardcoded.

## Gotcha — Kein ViewModel im Pipeline-Code

Die Pipeline darf KEINEN `MainViewModel` oder andere ViewModels auflösen. Diese werden
erst nach der Pipeline in `RunLoadingAsync` aufgelöst und als DataContext gesetzt.
Services (Singleton) können in der Pipeline frei genutzt werden.
