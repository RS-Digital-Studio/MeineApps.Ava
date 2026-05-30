# HandwerkerImperium.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Werbe-App** → referenziert `MeineApps.Core.Premium.Ava` (AdMob Banner + Rewarded + Google Play Billing).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring, AdMob-Setup, Lifecycle, Back-Button. |
| `AndroidAudioService.cs` | `IAudioService`-Impl via SoundPool (SFX) + MediaPlayer (Musik + Crossfade). AudioFocus-Listener für Telefonanrufe. Erhält `IGameStateService` für Lautstärke-Einstellungen. |
| `AndroidNotificationService.cs` | Lokale Push-Benachrichtigungen (8 Trigger), AlarmManager, NotificationChannel. |
| `AndroidPlayGamesService.cs` | Google Play Games Services v2 — Leaderboards, Achievements, Cloud Save. |
| `BootReceiver.cs` | `BroadcastReceiver` — stellt nach Geräte-Neustart geplante Benachrichtigungen wieder her. |
| `NotificationReceiver.cs` | `BroadcastReceiver` — empfängt AlarmManager-PendingIntents und postet Notifications. |

---

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories brauchen `this` oder müssen vor DI-Build stehen):
- `App.RewardedAdServiceFactory` → `AndroidRewardedAdService(_rewardedAdHelper, IPurchaseService, "HandwerkerImperium")`
- `App.PurchaseServiceFactory` → `AndroidPurchaseService(this, IPreferencesService, IAdService)`
- `App.AudioServiceFactory` → `AndroidAudioService(this, IGameStateService)`
- `App.NotificationServiceFactory` → `AndroidNotificationService(this)`
- `PlayGamesSdk.Initialize(this)` — **muss vor erstem Client-Aufruf** stehen
- `App.PlayGamesServiceFactory` → `AndroidPlayGamesService(this)`
- `App.ReviewPromptRequested` → `LaunchReviewFlow()`
- `UriLauncher.PlatformShareText` → `Intent.ActionSend` (natives Share-Sheet)
- `GameAssetService.PlatformAssetLoader` → `Assets.Open("visuals/{path}")`

**Nach `base.OnCreate`:**
- POST_NOTIFICATIONS Runtime-Permission anfordern (Android 13+ / API 33)
- `_mainVm = App.Services.GetService<MainViewModel>()` → `ExitHintRequested` → Toast
- `EnableImmersiveMode()`
- `AdMobHelper.Initialize(this, callback)`:
  - Banner-Ad: `_adMobHelper.AttachToActivity(this, BannerAdUnitId, adService, purchaseService, 64)`
  - Rewarded-Ad vorladen: `golden_screws` (häufigster Daily-Trigger) + `offline_double` (App-Start-Trigger)
  - `AdMobHelper.RequestConsent(this)` (GDPR EU-Consent-Form)

---

## Lifecycle

| Methode | Was passiert |
|---------|-------------|
| `OnResume` | `_adMobHelper?.Resume()`, `_mainVm?.ResumeGameLoop()`, `EnableImmersiveMode()` |
| `OnPause` | `_mainVm?.PauseGameLoopAsync().GetAwaiter().GetResult()` (synchron — verhindert Datenverlust bei OS-Kill), `_adMobHelper?.Pause()` |
| `OnDestroy` | `App.DisposeServices()`, `_rewardedAdHelper?.Dispose()`, `_adMobHelper?.Dispose()` |
| `OnWindowFocusChanged` | `EnableImmersiveMode()` bei `hasFocus` (stellt Fullscreen nach Overlay-Fenstern wieder her) |

**PauseGameLoopAsync synchron blockieren** ist bewusst: Android kann die App nach OnPause sofort
killen. `ConfigureAwait(false)` in der Methode verhindert UI-Thread-Deadlock.

---

## Immersive Mode

`EnableImmersiveMode()` blendet StatusBar + NavigationBar aus. Bars erscheinen bei Swipe kurz
und verschwinden automatisch wieder.

- **API 30+**: `Window.InsetsController.Hide(SystemBars)` + `ShowTransientBarsBySwipe`
- **API < 30**: `SystemUiFlags.ImmersiveSticky` via `DecorView.SystemUiVisibility` (deprecated, Pragma-Suppress)

---

## Back-Button

```csharp
public override void OnBackPressed()
{
    if (_mainVm != null && _mainVm.HandleBackPressed()) return;
    base.OnBackPressed();
}
```

`HandleBackPressed()` schließt Overlays/Sub-Navigation, Double-Back-to-Exit über `BackPressHelper`.
Kein `MoveTaskToBack` — Default-Back (`base.OnBackPressed()`) beendet die Activity normal.

---

## In-App Review (Google Play Review API)

`LaunchReviewFlow()` → `ReviewManagerFactory.Create(this)` → `manager.RequestReviewFlow()` →
innere Klasse `ReviewRequestListener` (implementiert `Android.Gms.Tasks.IOnCompleteListener`) →
`manager.LaunchReviewFlow(activity, reviewInfo)`. Fehler still gefangen (kein Play Store → kein Crash).

---

## Manifest & Package

| Eigenschaft | Wert |
|-------------|------|
| Package-ID | `com.meineapps.handwerkerimperium` |
| Theme | `@style/MyTheme.NoActionBar` |
| ConfigChanges | `Orientation | ScreenSize | UiMode` |
| Permissions | `INTERNET`, `RECEIVE_BOOT_COMPLETED`, `POST_NOTIFICATIONS`, `VIBRATE`, AdMob/Billing |
| Ad-Banner-Höhe | 64 dp (sicherer Spacer für Adaptive Banner) |

---

## Premium-Linked-Files (MeineApps.Core.Premium.Ava)

Android-spezifische Klassen werden per `<Compile Include="..." Link="..." />` ins Android-Projekt eingebunden:
- `AndroidRewardedAdService.cs` — Rewarded Ads, 13 Placements
- `AndroidPurchaseService.cs` — Google Play Billing v8
- `AdMobHelper.cs` — Banner-Ad-Verwaltung
- `RewardedAdHelper.cs` — Rewarded-Ad-Loading + Callback

Details → [MeineApps.Core.Premium.Ava](../../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## Build

```bash
dotnet build   src/Apps/HandwerkerImperium/HandwerkerImperium.Android
dotnet publish src/Apps/HandwerkerImperium/HandwerkerImperium.Android -c Release   # AAB, nur auf Anfrage
```
