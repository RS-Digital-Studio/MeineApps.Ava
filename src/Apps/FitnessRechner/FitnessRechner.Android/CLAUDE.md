# FitnessRechner.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-
Lifecycle. Referenziert `MeineApps.Core.Premium.Ava` (AdMob Linked-Files, Rewarded Ads,
Google Play Billing, FileShareService). Generische Android-Patterns →
[Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia initialisiert sich hier **einmal pro Prozess**. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Alle Platform-Factories + AdMob + Lifecycle. |
| `AndroidBarcodeService.cs` | `IBarcodeService`-Impl: startet `BarcodeScannerActivity` via `StartActivityForResult`, gibt Ergebnis per `TaskCompletionSource<string?>` zurück. |
| `BarcodeScannerActivity.cs` | `AppCompatActivity` mit CameraX Preview + ML Kit ImageAnalysis. Erkennt EAN-13/EAN-8/UPC-A/UPC-E. Semi-transparentes Scan-Overlay mit Ecken-Akzenten. |
| `AndroidHapticService.cs` | `IHapticService`-Impl: Tick/Click/HeavyClick via `VibrationEffect` (API Q+) mit `PerformHapticFeedback`-Fallback. |
| `AndroidFitnessSoundService.cs` | `IFitnessSoundService`-Impl: `PlaySuccess` via MediaPlayer (System-Notification-Sound). |
| `AndroidReminderService.cs` | `IReminderService`-Impl: erbt von `ReminderService`. 3 Reminder-Typen (Wasser alle 2h, Gewicht täglich, Abend-Zusammenfassung) via `AlarmManager` + `NotificationChannel`. Channel-ID `fitness_reminders`. |

---

## MainActivity — Reihenfolge in `OnCreate`

**Vor `base.OnCreate`** (Platform-Hooks/Factories, die `this` brauchen):
- Unhandled-Exception-Handler registrieren (Android + AppDomain + TaskScheduler).
- `App.RewardedAdServiceFactory` → `AndroidRewardedAdService(_rewardedAdHelper, purchaseService, "FitnessRechner")`.
- `App.PurchaseServiceFactory` → `AndroidPurchaseService(this, preferencesService, adService)`.
- `App.FileShareServiceFactory` → `AndroidFileShareService(this)`.
- `App.BarcodeServiceFactory` → `AndroidBarcodeService(this)` (Instanz in `_barcodeService` für Activity-Result-Delegation).
- `App.HapticServiceFactory` → `AndroidHapticService(this)`.
- `App.SoundServiceFactory` → `AndroidFitnessSoundService(this)`.
- `App.ReminderServiceFactory` → `AndroidReminderService(this, preferencesService)`.

**Nach `base.OnCreate`:**
- `EnableImmersiveMode()` (StatusBar + NavigationBar ausblenden).
- `MainViewModel` aus DI holen, `ExitHintRequested` → `Toast`.
- `AdMobHelper.Initialize` → Callback: Banner-Ad (`56dp`) attachen + Rewarded Ad vorladen + GDPR-Consent.

**Activity-Result-Delegation:**
- `OnActivityResult` → `_barcodeService?.HandleActivityResult(...)`.
- `OnRequestPermissionsResult` → `_barcodeService?.HandlePermissionResult(...)`.

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst
`MoveTaskToBack(true)`.

**Lifecycle:**
- `OnResume` → `_adMobHelper?.Resume()` + `EnableImmersiveMode()`.
- `OnPause` → `_adMobHelper?.Pause()`.
- `OnWindowFocusChanged` → `EnableImmersiveMode()` bei `hasFocus`.
- `OnDestroy` → `_rewardedAdHelper?.Dispose()` + `_adMobHelper?.Dispose()`.

### ImmersiveMode

API 30+: `InsetsController.Hide(SystemBars())` + `ShowTransientBarsBySwipe`.
API < 30: `SystemUiVisibility`-Fallback (`ImmersiveSticky | LayoutStable | LayoutHideNavigation | ...`).

---

## Barcode-Permission-Flow

```
ScanBarcodeAsync()
  ├─ Kamera-Permission vorhanden → StartScannerActivity()
  └─ Permission fehlt → RequestPermissions(CAMERA_PERMISSION_CODE)
       → OnRequestPermissionsResult:
           ├─ Granted → PostDelayed(500ms) → StartScannerActivity()   // 500ms Delay: Permission-Aktivierung braucht Zeit auf älteren Geräten
           └─ Denied → TrySetResult(null)
```

**Gotcha:** 500 ms Delay nach Permission-Grant ist notwendig — CameraX crasht ohne diesen
Puffer auf älteren Geräten, weil das System die Kamera-Permission nicht sofort aktiviert.

---

## Manifest & Resources

- Package: `com.meineapps.fitnessrechner`
- Theme: `@style/MyTheme.NoActionBar`
- Permissions: `CAMERA` (Barcode-Scanner), `RECEIVE_BOOT_COMPLETED` + `USE_EXACT_ALARM`
  (Reminder-AlarmManager), `POST_NOTIFICATIONS` (Android 13+), `INTERNET` (Open Food Facts API),
  `VIBRATE` (Haptic)
- `Resources/mipmap-*`: App-Icon (`appicon`, `appicon_round`)
- `Resources/values/styles.xml`: `MyTheme.NoActionBar`

---

## Build

```bash
dotnet build   src/Apps/FitnessRechner/FitnessRechner.Android
dotnet publish src/Apps/FitnessRechner/FitnessRechner.Android -c Release   # AAB, nur auf Anfrage
```
