# RebornSaga.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Portrait-only, Immersive Fullscreen** — keine Tab-Leiste, kein Banner-Ad
(Vollbild-SkiaSharp-Spiel). Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring vor `base.OnCreate`, Immersive-Fullscreen-Lifecycle, Back-Button, AdMob-Init. |
| `AndroidManifest.xml` | Package `org.rsdigital.rebornsaga`, `Theme.Material.Light.NoActionBar`, Portrait-only. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories müssen vor DI-Build registriert sein):
- `App.RewardedAdServiceFactory` → `AndroidRewardedAdService(_rewardedAdHelper, purchaseService, "RebornSaga")`.
  6 Placements: `gold_bonus`, `time_rift`, `bonus_exp`, `revive`, `daily_prophecy`, `kodex_hint`.
- `App.PurchaseServiceFactory` → `AndroidPurchaseService(this, preferencesService, adService)`.
- `App.AudioServiceFactory` → `AndroidAudioService(this, preferencesService)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` — StatusBar + NavigationBar ausblenden.
  API 30+: `InsetsController.Hide(SystemBars)` + `ShowTransientBarsBySwipe`.
  Fallback (< API 30): `SystemUiFlags.ImmersiveSticky | …` via `DecorView.SystemUiVisibility`.
- `MainViewModel` aus DI holen, `ExitHintRequested` → `Toast`.
- `AdMobHelper.Initialize()` → nach SDK-Callback: `RewardedAdHelper.Load()` + `AdMobHelper.RequestConsent()`.
  **Kein Banner-Ad** — RebornSaga ist Vollbild-SkiaSharp-Spiel.

**Immersive wird dreifach gesetzt** (Lifecycle-Robustheit): `OnCreate` → `OnResume` →
`OnWindowFocusChanged(hasFocus=true)`. Verhindert, dass System-UI nach Benachrichtigungen
wieder sichtbar wird.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`:
Overlays schließen → Szene poppen → Double-Back-to-Exit mit Toast.

**`OnDestroy`:** `App.DisposeServices()` → `_rewardedAdHelper?.Dispose()` → `_adMobHelper?.Dispose()`.

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
