# WorkTimePro.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Werbe-App mit Premium** → referenziert `MeineApps.Core.Premium.Ava` (AdMob + Billing + Linked-Files).
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring + AdMob-Init + Lifecycle + Back-Button-Delegation. |
| `Services/AndroidHapticService.cs` | `IHapticService`-Impl über `Vibrator`: `Tick`/`Click`/`HeavyClick` nutzen `VibrationEffect` (API Q+) mit ms-Vibrate-Fallback. |
| `Services/AndroidNotificationService.cs` | `INotificationService`-Impl: `worktimepro_reminder` NotificationChannel, `SetExactAndAllowWhileIdle`-Planung mit inexaktem Fallback (`SetAndAllowWhileIdle`) wenn Exact-Alarm-Permission fehlt, stabiler Hash für Notification-IDs. |
| `Services/ReminderReceiver.cs` | `BroadcastReceiver` — wird von `AlarmManager` auch bei geschlossener App gefeuert. Zeigt Notification via `NotificationManagerCompat`. |
| `Services/BootReceiver.cs` | `BroadcastReceiver` für `BOOT_COMPLETED` + `MY_PACKAGE_REPLACED` — plant AlarmManager-Alarme nach Gerät-Neustart neu (AlarmManager-Alarme werden beim Reboot verworfen). Baut minimalen Service-Graph ohne App.Services. |
| `AndroidManifest.xml` | Package `com.meineapps.worktimepro`, `MyTheme.NoActionBar`, Permissions: `POST_NOTIFICATIONS`, `SCHEDULE_EXACT_ALARM`, `RECEIVE_BOOT_COMPLETED`, FileProvider für Export-Share. |
| `Resources/mipmap-*` | App-Icon (`appicon`, `appicon_round`). |
| `Resources/values/styles.xml` | `MyTheme.NoActionBar`. |

## Premium — Linked-File-Pattern

`AndroidRewardedAdService.cs`, `AndroidPurchaseService.cs`, `AdMobHelper.cs`, `RewardedAdHelper.cs`
und `AndroidFileShareService.cs` werden per `<Compile Include="..." Link="..." />` aus
`MeineApps.Core.Premium.Ava/Android/` in dieses Projekt eingebunden (nicht dupliziert).

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Factories müssen vor DI-Build stehen):
- `App.FileShareServiceFactory = () => new AndroidFileShareService(this)`.
- `App.NotificationServiceFactory = () => new AndroidNotificationService()`.
- `App.HapticServiceFactory = () => new AndroidHapticService()`.
- `_rewardedAdHelper = new RewardedAdHelper()`.
- `App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(_rewardedAdHelper, …)`.
- `App.PurchaseServiceFactory = sp => new AndroidPurchaseService(this, …)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden; API 30+ via `InsetsController`,
  sonst `SystemUiVisibility`-Fallback).
- `POST_NOTIFICATIONS` Permission anfragen (Android 13+, `BuildVersionCodes.Tiramisu`).
- `AdMobHelper.Initialize()` — Ad-Banner (`AdConfig.GetBannerAdUnitId("WorkTimePro")`, 56dp),
  Rewarded-Ad vorladen, GDPR Consent-Form via `AdMobHelper.RequestConsent()`.

**Lifecycle:**
- `OnResume` → `_adMobHelper?.Resume()` + `EnableImmersiveMode()`.
- `OnPause` → `_adMobHelper?.Pause()`.
- `OnWindowFocusChanged(true)` → `EnableImmersiveMode()` (damit Bars nach Benachrichtigungen wieder verschwinden).
- `OnDestroy` → `_rewardedAdHelper?.Dispose()` + `_adMobHelper?.Dispose()`.

**Back-Button:** `OnBackPressed()` → `mainVm.HandleBackPressed()`; bei `false` →
`MoveTaskToBack(true)` (App in Hintergrund, nicht beenden).

## AndroidHapticService

Liegt in `Services/AndroidHapticService.cs`. `IHapticService`-Impl über `Vibrator`:
`VibrationEffect.EffectTick`/`EffectClick`/`EffectHeavyClick` (API Q+), ms-Fallback für ältere
Geräte. Wird per Factory ins Shared-DI injiziert (kein direkter Konstruktor-Aufruf in Views).

## AndroidNotificationService + ReminderReceiver + BootReceiver

- `worktimepro_reminder` Channel mit `NotificationImportance.High`.
- Notification-IDs per `StableHash(id)` (deterministisch zwischen Neustarts).
- Exact-Alarm-Fallback: Fehlt `SCHEDULE_EXACT_ALARM`-Permission (Android 12+, vom Nutzer
  entziehbar), fällt die Planung auf `SetAndAllowWhileIdle` zurück — Reminder feuert etwas
  verzögert, aber feuert überhaupt (statt stumm abzubrechen).
- `ReminderReceiver`: `[BroadcastReceiver(Exported = false)]` — reagiert auf AlarmManager-Intents,
  auch wenn App nicht läuft.
- `BootReceiver`: `[BroadcastReceiver(Exported = true)]` für `BOOT_COMPLETED` +
  `MY_PACKAGE_REPLACED` — plant Alarme nach Reboot neu. Baut eigenständig einen minimalen
  Service-Graph (`DatabaseService`, `ReminderService`, …), weil `App.Services` beim
  Boot-Broadcast nicht verfügbar ist.

## Build

```bash
dotnet build   src/Apps/WorkTimePro/WorkTimePro.Android
dotnet publish src/Apps/WorkTimePro/WorkTimePro.Android -c Release   # AAB, nur auf Anfrage
```
