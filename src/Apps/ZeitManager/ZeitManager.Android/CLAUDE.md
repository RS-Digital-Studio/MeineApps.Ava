# ZeitManager.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-
Lifecycle. **Werbefrei** → referenziert `MeineApps.Core.Premium.Ava` NICHT (keine AdMob-/
Billing-/Linked-Files). Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity`. Platform-Service-Registrierung, Lifecycle, Ringtone-Picker-Flow, Back-Button-Delegation. |
| `Services/TimerForegroundService.cs` | Foreground Service mit Notification für laufende Timer-Countdowns. |
| `Services/AlarmReceiver.cs` | `BroadcastReceiver` — löst Alarm aus, prüft `IsAppInForeground`. |
| `Services/BootReceiver.cs` | `BOOT_COMPLETED` → Wecker nach Neustart neu planen. |
| `Services/AlarmActivity.cs` | Dedizierte Fullscreen-Activity über Lockscreen (`ShowWhenLocked`, `TurnScreenOn`). Buttons lokalisiert via `App.Services`. |
| `Services/AndroidAudioService.cs` | `IAudioService` via `MediaPlayer` + `RingtoneManager`. Unterstützt eingebaute Töne, System-Ringtones, benutzerdefinierte URIs. |
| `Services/AndroidNotificationService.cs` | `INotificationService` mit zwei Kanälen: `zeitmanager_timer` (leise) und `zeitmanager_alarm_v2` (High + System-Alarm-Sound). |
| `Services/AndroidShakeDetectionService.cs` | `IShakeDetectionService` via Accelerometer — Shake-Challenge für Wecker. |
| `Services/AndroidHapticService.cs` | `IHapticService` via `Vibrator` / `PerformHapticFeedback`. |

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (DI-Build passiert in `base.OnCreate` → Factories müssen vorher stehen):

```csharp
App.ConfigurePlatformServices = services =>
{
    services.AddSingleton<INotificationService, AndroidNotificationService>();
    services.AddSingleton<IAudioService, AndroidAudioService>();
    services.AddSingleton<IShakeDetectionService, AndroidShakeDetectionService>();
    services.AddSingleton<IHapticService, AndroidHapticService>();
};
```

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden; API 30+ via `InsetsController`,
  sonst `SystemUiVisibility`-Fallback).
- `TimerService.ForegroundNotificationCallback` + `StopForegroundCallback` verdrahten
  (DI ist jetzt verfügbar → `App.Services.GetService<ITimerService>()`).
- `AndroidAudioService.PickRingtoneCallback = PickRingtoneAsync` — startet
  `RingtoneManager.ActionRingtonePicker` per `StartActivityForResult`.
- `MainViewModel` aus DI holen, `ExitHintRequested` → `Toast`.

**Immersive Mode:** `OnResume` + `OnWindowFocusChanged` rufen ebenfalls `EnableImmersiveMode()`
auf, damit Bars nach System-Dialogen wieder verschwinden.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst
`MoveTaskToBack(true)`.

## Ringtone-Picker Flow

`MainActivity` hält eine statische `_ringtonePickerTcs`. `PickRingtoneAsync()` startet
`StartActivityForResult` mit `RingtoneManager.ActionRingtonePicker` (Request-Code 9001) und
gibt den Task zurück. `OnActivityResult` löst den TCS auf — URI als String oder null bei Abbruch.
`AndroidAudioService` ruft `PickRingtoneCallback` auf und wartet darauf.

## IsAppInForeground

Statisches Flag `MainActivity.IsAppInForeground` — wird in `OnResume` auf `true`, in `OnPause`
auf `false` gesetzt. `AlarmReceiver` prüft es vor dem Starten der `AlarmActivity`, um Doppel-
Auslösung (AlarmActivity + In-App Overlay) zu vermeiden.

## AndroidManifest Permissions

`FOREGROUND_SERVICE`, `SCHEDULE_EXACT_ALARM`, `RECEIVE_BOOT_COMPLETED`, `POST_NOTIFICATIONS`,
`VIBRATE`, `USE_FULL_SCREEN_INTENT`, `WAKE_LOCK`. Package: `com.meineapps.zeitmanager`,
Theme: `MyTheme.NoActionBar`.

## Notification-Kanäle

| Kanal | ID | Wichtigkeit | Zweck |
|-------|----|------------|-------|
| Timer | `zeitmanager_timer` | Low | Laufende Foreground-Timer-Notification |
| Alarm | `zeitmanager_alarm_v2` | High | Alarm-Sound + Vibration |

`zeitmanager_alarm` (alter lautloser Kanal) wird beim App-Start gelöscht. Channel-IDs sind
stabil, da sie ins Android-System eingetragen werden und nicht mehr änderbar sind.

## Build

```bash
dotnet build   src/Apps/ZeitManager/ZeitManager.Android
dotnet publish src/Apps/ZeitManager/ZeitManager.Android -c Release   # AAB, nur auf Anfrage
```
