# HandwerkerRechner.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Werbefinanziert** → referenziert `MeineApps.Core.Premium.Ava` (AdMob-Banner + Rewarded + Billing).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic mehr in Avalonia 12). Factory-Wiring + AdMob-Setup + Lifecycle + Back-Navigation. |
| `AndroidManifest.xml` | Package `com.meineapps.handwerkerrechner`, `MyTheme.NoActionBar`, AdMob/Billing-Permissions. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |

## Linked Premium-Files

Android-Projekt bindet via `<Compile Include … Link="…" />` aus `MeineApps.Core.Premium.Ava/Android/`:
`AdMobHelper.cs`, `RewardedAdHelper.cs`, `AndroidRewardedAdService.cs`,
`AndroidFileShareService.cs`, `AndroidPurchaseService.cs`.

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories die `this` brauchen oder DI vorbereiten):
- `UriLauncher.PlatformOpenUri` → `Intent.ActionView` (URI-Launcher).
- `App.FileShareServiceFactory = () => new AndroidFileShareService(this)`.
- `_rewardedAdHelper = new RewardedAdHelper()`.
- `App.RewardedAdServiceFactory` → `AndroidRewardedAdService(_rewardedAdHelper, IPurchaseService, "HandwerkerRechner")`.
- `App.PurchaseServiceFactory` → `AndroidPurchaseService(this, IPreferencesService, IAdService)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden; API 30+ via `InsetsController`,
  sonst `SystemUiVisibility`-Fallback). Auch in `OnResume` + `OnWindowFocusChanged`.
- `MainViewModel` aus DI holen, `ExitHintRequested` → `Toast`.
- `AdMobHelper.Initialize(this, callback)` → im Callback: `_adMobHelper.AttachToActivity(…, 56)` (Tab-Bar-Höhe 56),
  `_rewardedAdHelper.Load(…)`, `AdMobHelper.RequestConsent` (GDPR EU).

**Ad-Placement:** `AdConfig.GetBannerAdUnitId("HandwerkerRechner")` + `GetRewardedAdUnitId("HandwerkerRechner")`.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst `MoveTaskToBack(true)`.

**Lifecycle-Cleanup:** `OnDestroy` → `_rewardedAdHelper?.Dispose()` + `_adMobHelper?.Dispose()`.

## Build

```bash
dotnet build   src/Apps/HandwerkerRechner/HandwerkerRechner.Android
dotnet publish src/Apps/HandwerkerRechner/HandwerkerRechner.Android -c Release   # AAB, nur auf Anfrage
```
