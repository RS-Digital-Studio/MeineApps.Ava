# FinanzRechner.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Werbe-App** → referenziert `MeineApps.Core.Premium.Ava` (AdMob + Banner + Rewarded + Billing).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring + AdMob-Init + Lifecycle + Immersive. |
| `AndroidManifest.xml` | Package `com.meineapps.finanzrechner` (aus `Directory.Build.targets`), `MyTheme.NoActionBar`, Internet + Network-State + Billing-Permissions, AdMob-Application-ID, `FileProvider` für `AndroidFileShareService`. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |
| `Resources/xml/file_paths.xml` | FileProvider-Pfade für Datei-Sharing via `AndroidFileShareService`. |

---

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories müssen vor DI-Start registriert sein):
- `App.FileShareServiceFactory = () => new AndroidFileShareService(this)`.
- `_rewardedAdHelper = new RewardedAdHelper()`.
- `App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(_rewardedAdHelper!, sp.GetRequiredService<IPurchaseService>(), "FinanzRechner")`.
- `App.PurchaseServiceFactory = sp => new AndroidPurchaseService(this, sp.GetRequiredService<IPreferencesService>(), sp.GetRequiredService<IAdService>())`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden).
- `_mainVm = App.Services.GetService<MainViewModel>()` → `ExitHintRequested` → `Toast`.
- `AdMobHelper.Initialize(this, callback)` — im Callback:
  - `_adMobHelper = new AdMobHelper()` wird hier instanziiert (erst nach SDK-Init gültig).
  - `_adMobHelper.AttachToActivity(this, AdConfig.GetBannerAdUnitId("FinanzRechner"), adService, purchaseService, 56)`.
  - `_rewardedAdHelper.Load(this, AdConfig.GetRewardedAdUnitId("FinanzRechner"))`.
  - `AdMobHelper.RequestConsent(this)` (GDPR Consent-Form EU).

**Lifecycle-Weitergabe:**
- `OnResume` → `_adMobHelper?.Resume()` + `EnableImmersiveMode()`.
- `OnPause` → `_adMobHelper?.Pause()`.
- `OnWindowFocusChanged(hasFocus)` → `EnableImmersiveMode()` wenn `hasFocus`.
- `OnDestroy` → `_rewardedAdHelper?.Dispose()` + `_adMobHelper?.Dispose()`.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst
`MoveTaskToBack(true)`. `enableOnBackInvokedCallback=false` im Manifest hält das deprecated
`OnBackPressed`-Pattern aktiv (API 33+ Opt-out).

---

## Immersive Mode

`EnableImmersiveMode()` deckt beide API-Level ab:
- **API 30+**: `Window.SetDecorFitsSystemWindows(false)` + `InsetsController.Hide(SystemBars)` +
  `SystemBarsBehavior = ShowTransientBarsBySwipe`.
- **API < 30**: `SystemUiVisibility`-Fallback mit `ImmersiveSticky`-Flags (deprecated, aber
  notwendig für ältere Geräte).

---

## Premium-Linked-Files

`AndroidRewardedAdService`, `AndroidPurchaseService`, `AndroidFileShareService` leben in
`MeineApps.Core.Premium.Ava/Android/` und werden per `<Compile Include … Link="…" />` in dieses
Projekt eingebunden. Details → [MeineApps.Core.Premium.Ava](../../../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## Build

```bash
dotnet build   src/Apps/FinanzRechner/FinanzRechner.Android
dotnet publish src/Apps/FinanzRechner/FinanzRechner.Android -c Release   # AAB, nur auf Anfrage
```
