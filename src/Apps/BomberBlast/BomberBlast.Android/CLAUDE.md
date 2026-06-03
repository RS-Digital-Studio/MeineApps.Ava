# BomberBlast.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-
Lifecycle. Landscape-only, Fullscreen-Immersive. Premium-fähig (AdMob + Billing).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity`. Factory-Wiring vor `base.OnCreate`, Lifecycle, Gamepad-Dispatch. |
| `AndroidSoundService.cs` | `ISoundService`-Impl via `SoundPool` (SFX) + `MediaPlayer` (Musik). |
| `AndroidVibrationService.cs` | `IVibrationService`-Impl via `VibrationEffect.CreateWaveform`. |
| `AndroidPushNotificationService.cs` | FCM + `AlarmManager`-basierte lokale Notifications. POST_NOTIFICATIONS-Permission (Android 13+). |
| `BomberBlastMessagingService.cs` | `FirebaseMessagingService`-Subclass: `OnNewToken` → `RaiseTokenRefresh()`, Data-Payload → eigene Notification.Builder. Manifest: `org.rsdigital.bomberblast.BomberBlastMessagingService`. |
| `NotificationReceiver.cs` | `BroadcastReceiver` — postet Local-Notifications aus AlarmManager-PendingIntent-Extras. |
| `FirebaseRemoteConfigService.cs` | Erbt von `DefaultsRemoteConfigService`. Cloud-Fetch überschreibt Keys via `ApplyRawRemoteValue(key, raw)`. |
| `AndroidManifest.xml` | Package `org.rsdigital.bomberblast`, `MyTheme.NoActionBar`, `ScreenOrientation.Landscape`, AdMob + Billing-Permissions, Notification-Channels. |
| `Resources/mipmap-*/` | App-Icons (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |

---

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Platform-Factories, die `this` brauchen):

```csharp
App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(_rewardedAdHelper, sp.GetRequiredService<IPurchaseService>(), "BomberBlast");
App.PurchaseServiceFactory   = sp => new AndroidPurchaseService(this, prefs, adService);
App.SoundServiceFactory      = _ => new AndroidSoundService(this);
App.VibrationServiceFactory  = _ => new AndroidVibrationService(this);
App.PlayGamesServiceFactory  = sp => new AndroidPlayGamesService(this, prefs);
App.PushNotificationServiceFactory = _ => new AndroidPushNotificationService(this);
App.RemoteConfigServiceFactory     = sp => new FirebaseRemoteConfigService(logger, isDebug);
GameAssetService.PlatformAssetLoader = path => Assets?.Open($"visuals/{path}");
```

**Nach `base.OnCreate`:**

- `EnableImmersiveMode()` — StatusBar + NavigationBar ausblenden (API 30+:
  `InsetsController`, Fallback `SystemUiFlags.ImmersiveSticky` für API < 30).
- `_mainVm` aus DI holen, `ExitHintRequested` → Toast.
- `AndroidPlayGamesService.InitializeSdk()` + `SignInAsync()`.
- `AdMobHelper.Initialize(this, callback)` → Banner-Ad (kein Banner — BomberBlast ist
  Landscape-only, nutzt nur Rewarded), Rewarded-Ad vorladen, GDPR-Consent anfordern.

**WICHTIG**: `GameViewModel` wird **nicht** eager aus DI gezogen — er ist lazy-resolved
(`MainViewModel.GameVm`). Direktes `GetService<GameViewModel>()` in `OnCreate` würde den
~100-200ms Startup-Vorteil des Lazy-Patterns zunichte machen.

---

## Gamepad-Dispatch

```csharp
// DispatchKeyEvent: Face-Buttons (A/B/X/Y/Start/Select/Menu)
// Nur wenn IsGameActive && GameVm != null
gameVm.OnGamepadButtonDown/Up(gamepadButton)

// DispatchGenericMotionEvent: Analog-Stick (Joystick-Source, Deadzone im InputManager)
gameVm.SetAnalogStick(x, y)
```

Events werden konsumiert (`return true`) und nicht an Avalonia weitergeleitet.

---

## Lifecycle

| Callback | Aktion |
|----------|--------|
| `OnResume` | `EnableImmersiveMode()`, `_mainVm.OnAppResumed()`, `AdMobHelper.Resume()`, `ReEngagementScheduler.CancelAll()`, `RemoteConfig.FetchAndActivateAsync()` |
| `OnPause` | `_mainVm.OnAppPaused()`, `AdMobHelper.Pause()`, `PreferencesService.FlushPending()` (Persistenz-Flush bei Hintergrundwechsel), `ReEngagementScheduler.ScheduleAll()` |
| `OnWindowFocusChanged(true)` | `EnableImmersiveMode()` (Wiederherstellung nach kurzer System-UI-Sichtbarkeit) |
| `OnBackPressed` | `_mainVm.HandleBackPressed()` → bei false: `base.OnBackPressed()` |
| `OnRequestPermissionsResult` | `AndroidPushNotificationService.OnPermissionResult()` (POST_NOTIFICATIONS TCS-Auflösung) |
| `OnDestroy` | `App.DisposeServices()`, `RewardedAdHelper.Dispose()`, `AdMobHelper.Dispose()` |

---

## Premium-Linked-Files

Folgende Dateien aus `MeineApps.Core.Premium.Ava/Android/` sind per
`<Compile Include="..." Link="..." />` eingebunden:
- `AdMobHelper.cs`, `RewardedAdHelper.cs`
- `AndroidRewardedAdService.cs`, `AndroidPurchaseService.cs`
- `AndroidPlayGamesService.cs`

Details + UMP-Namespace-Typo + Java-Generics-Erasure-Fix →
[MeineApps.Core.Premium.Ava](../../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## Notification-Channels

| Channel-ID | Priorität | Zweck |
|-----------|-----------|-------|
| `bomberblast_daily` | Low | Tägliche Rewards |
| `bomberblast_liveops` | Default (Manifest-Default) | Live-Ops-Events |
| `bomberblast_important` | High + Vibration | Saison-Ende, Liga-Abstieg |

---

## Build

```bash
dotnet build   src/Apps/BomberBlast/BomberBlast.Android
dotnet publish src/Apps/BomberBlast/BomberBlast.Android -c Release   # AAB, nur auf Anfrage
```
