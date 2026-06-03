# RebornSaga.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Portrait-only, Immersive Fullscreen** — kein Banner-Ad (Vollbild-SkiaSharp-Spiel).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier einmal pro Prozess. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring vor `base.OnCreate`, Immersive-Fullscreen-Lifecycle, Back-Button, AdMob-Init. |
| `AndroidAudioService.cs` | Android-Implementierung von `AudioService`: SoundPool für SFX (27 Sounds), MediaPlayer für BGM (10 Tracks), Vibrator für Haptik. SFX-Assets: `Assets/Sounds/*.ogg`, BGM: `Assets/Music/*.ogg`. |
| `AndroidManifest.xml` | Package `org.rsdigital.rebornsaga`, `Theme.Material.Light.NoActionBar`, Portrait-only, `largeHeap="true"`, `enableOnBackInvokedCallback="false"`. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories müssen vor DI-Build registriert sein):
- `App.RewardedAdServiceFactory` → `AndroidRewardedAdService(_rewardedAdHelper, purchaseService, "RebornSaga")`.
  Rewarded-Placements (6) → [RebornSaga/CLAUDE.md](../CLAUDE.md#premium--ads).
- `App.PurchaseServiceFactory` → `AndroidPurchaseService(this, preferencesService, adService)`.
- `App.AudioServiceFactory` → `AndroidAudioService(this, preferencesService)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` — StatusBar + NavigationBar ausblenden.
  API 30+: `InsetsController.Hide(SystemBars)` + `ShowTransientBarsBySwipe`.
  Fallback (< API 30): `SystemUiFlags.ImmersiveSticky | …` via `DecorView.SystemUiVisibility`.
- `MainViewModel` aus DI holen, `ExitHintRequested` → Toast via `RunOnUiThread`.
- `AdMobHelper.Initialize()` → nach SDK-Callback: `RewardedAdHelper.Load()` + `AdMobHelper.RequestConsent()`.

**Immersive wird dreifach gesetzt** (Lifecycle-Robustheit): `OnCreate` → `OnResume` →
`OnWindowFocusChanged(hasFocus=true)`. Verhindert, dass System-UI nach Benachrichtigungen
wieder sichtbar wird.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`:
Overlays schließen → Szene poppen → Double-Back-to-Exit mit Toast.

**`OnDestroy`:** `App.DisposeServices()` → `_rewardedAdHelper?.Dispose()` → `_adMobHelper?.Dispose()`.

### Gotcha: `_adMobHelper` bleibt `null`

`_adMobHelper` wird in `MainActivity` deklariert, aber nie initialisiert — RebornSaga verwendet kein Banner-Ad.
`OnResume`/`OnPause`/`OnDestroy` rufen `_adMobHelper?.Pause/Resume/Dispose()` auf, was bei `null` lautlos no-ops.
Sollte in Zukunft ein Banner hinzukommen: `_adMobHelper = new AdMobHelper(...)` im `OnCreate`-Callback ergänzen.

### Gotcha: AdMob App-ID (TODO offen)

`AndroidManifest.xml` enthält noch die Platzhalter-App-ID `ca-app-pub-2588160251469436~8809763733`.
Vor dem produktiven Release eine eigene RebornSaga-App-ID in der AdMob-Konsole anlegen und dort eintragen.

## AndroidAudioService — Besonderheiten

- Erbt von `AudioService` (abstrakte Basisklasse in `RebornSaga.Services`).
- `SoundPool` mit maximal 8 gleichzeitigen Streams; SFX werden beim Konstruktor-Aufruf per `TryLoadSfx` vorgeladen — fehlt die Asset-Datei, wird die Exception still ignoriert.
- `MediaPlayer` (BGM) läuft geloopt; `Prepare()` synchron, weil `PrepareAsync()` im Java-Binding `void` zurückgibt.
- Vibrator-Initialisierung: API 31+ über `VibratorManager`, darunter Legacy `VibratorService`. In eigene Methoden ausgelagert, damit der JIT auf älteren Geräten den API-31-Typ nicht auflöst.
- `Dispose()` ist idempotent (`_disposed`-Guard); gibt `SoundPool`, `MediaPlayer` und läuft `StopBgmInternal()` innerhalb des `_musicLock` frei.

## Premium Linked-Files

`AndroidRewardedAdService.cs`, `AndroidPurchaseService.cs`, `AdMobHelper.cs`,
`RewardedAdHelper.cs` aus `MeineApps.Core.Premium.Ava/Android/` werden per
`<Compile Include="…" Link="…" />` eingebunden.
Details → [MeineApps.Core.Premium.Ava/CLAUDE.md](../../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

## Build

```bash
dotnet build   src/Apps/RebornSaga/RebornSaga.Android
dotnet publish src/Apps/RebornSaga/RebornSaga.Android -c Release   # AAB, nur auf Anfrage
```
