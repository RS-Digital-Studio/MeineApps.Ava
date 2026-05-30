# SmartMeasure.Android — Android-Host

Android-Einstiegsprojekt (`net10.0-android`). Hostet das Shared-Projekt via Avalonia-12-Lifecycle.
**Kein AdMob/IAP** — privates Projekt, keine Werbung.
Generische Android-Patterns → [Haupt-CLAUDE.md](../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `AndroidApp.cs` | `AvaloniaAndroidApplication<App>` — Avalonia-Bootstrap einmal pro Prozess. `CustomizeAppBuilder().WithInterFont()`. |
| `MainActivity.cs` | `AvaloniaMainActivity` (kein `<App>`-Generic in Avalonia 12). Factory-Wiring + Permissions + Lifecycle. |
| `Ar/ArCaptureActivity.cs` | Native `AppCompatActivity` für ARCore-Session (kein Avalonia). GL + Canvas + Toolbar. |
| `Ar/ArCaptureActivity.Dialogs.cs` | Partial: Bestätigungs-Dialoge, Kontur-Typ-Dialog, Coach-Marks. |
| `Ar/ArCaptureActivity.Recovery.cs` | Partial: State-Persistenz, Restore-Dialog, Earth-Anchor-Re-Attach-Queue. |
| `Ar/ArBackgroundRenderer.cs` | OpenGL ES 3.0 Kamera-Preview (Vertex+Fragment-Shader für Camera-Textur). |
| `Ar/ArPointOverlayView.cs` | Transparenter Canvas (Punkte, Linien, Toolbar-Overlay). `partial sealed`. |
| `Ar/ArAnchorManager.cs` | Drift-Kompensation via Earth-Anchors. Enthält auch `ArStabilityMonitor`. |
| `Ar/ArPrecisionHelpers.cs` | Depth-Sanity, Ground-Plane, Semantic-Label, Sky-Check. Delegiert Math an `ArMathHelpers` (Shared). |
| `Ar/ArOverlayState.cs` | Snapshot-Record: alle Render-Parameter + lokalisierte Labels + System-Banner. |
| `Services/AndroidBleService.cs` | BLE GATT-Kommunikation zum RTK-Stab. MTU 247, Write-Queue, Reconnect-Backoff. |
| `Services/AndroidAppPaths.cs` | `IAppPaths`-Impl: `Context.FilesDir` → sandbox-sichere Pfade. |
| `Services/MeasurementForegroundService.cs` | Android Foreground-Service (Notification), verhindert Doze-Kill während BLE aktiv. |

---

## MainActivity — Reihenfolge in `OnCreate`

**VOR `base.OnCreate`** (Factories werden beim DI-Build ausgewertet, der in `base.OnCreate` passiert):

```
App.AppPathsFactory = () => new AndroidAppPaths(this)
App.BleServiceFactory = sp => new AndroidBleService(this, sp.GetRequiredService<IGeoidService>())
App.ArCaptureServiceFactory = _ => _arCaptureService   (AndroidArCaptureService)
App.VoiceAnnotationServiceFactory = _ => new AndroidVoiceAnnotationService(this)
UriLauncher.PlatformShareFile = ShareFileViaIntent
UriLauncher.PlatformOpenFile  = OpenFileViaIntent
```

**NACH `base.OnCreate`:**

```
_mainVm aus App.Services holen
ExitHintRequested    → Toast (double-back)
MessageRequested     → Toast (lange Toasts für Fehler)
ForegroundServiceRequested → MeasurementForegroundService.Start/Stop
RequestBlePermissionsIfNeeded()
```

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst `base.OnBackPressed()`.

**`OnDestroy`:** `MeasurementForegroundService.Stop(this)` — FG-Service sterben lassen wenn Activity endet.

---

## Permissions (Manifest + Runtime)

| Permission | Wann | Grund |
|-----------|------|-------|
| `BLUETOOTH_SCAN` | Runtime, API 31+ | BLE-Scan |
| `BLUETOOTH_CONNECT` | Runtime, API 31+ | BLE GATT-Verbindung |
| `ACCESS_FINE_LOCATION` | Runtime, alle Level | Mapsui + AR-Georeferenzierung |
| `CAMERA` | Runtime, von `ArCaptureActivity` selbst angefragt | ARCore |

`OperatingSystem.IsAndroidVersionAtLeast(31)` statt `Build.VERSION.SdkInt` (Static-Analyzer-konform).

---

## AR-Capture — Activity-Brücke

`ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia). Kommunikation via
`TaskCompletionSource<ArCaptureResult?>` in `AndroidArCaptureService`:

```
SurveyViewModel.StartArCaptureAsync()
  → IArCaptureService.CaptureAsync()   // wartet auf TCS
  → AndroidArCaptureService startet ArCaptureActivity (Intent)
  → ArCaptureActivity.FinishCapture()  → TCS.SetResult()
  → ConsumeLastResult() gibt ArCaptureResult zurück
```

`HandleActivityResult` / `HandlePermissionResult` in `AndroidArCaptureService` müssen von
`MainActivity.OnActivityResult` / `OnRequestPermissionsResult` delegiert werden.

---

## FileProvider (Export + Share)

Authority: `{packageId}.fileprovider` → `AndroidX.Core.Content.FileProvider.GetUriForFile(this, PackageName + ".fileprovider", file)`.
Manifest: `<provider android:authorities="${applicationId}.fileprovider" android:grantUriPermissions="true" .../>`.
Pfade: `Resources/xml/provider_paths.xml`.
`UriLauncher.PlatformShareFile` → `Intent.ActionSend`, `PlatformOpenFile` → `Intent.ActionView`.

---

## AndroidBleService — Wichtige Details

- `RequestMtu(247)` in `OnConnected` VOR `DiscoverServices` (BLE 5.3 DLE, 48-Byte-Pakete brauchen mehr als MTU 23).
- Write-Queue via `SemaphoreSlim` + `OnCharacteristicWrite`-Acknowledgment (BLE-Writes nicht parallel!).
- `BinaryPrimitives.ReadDoubleLittleEndian` statt `BitConverter` (ESP32 = little-endian, explizit sicherer).
- Exponential-Backoff-Reconnect: 1 s → 2 s → 4 s → 10 s, max 5 Versuche.

---

## Build

```bash
dotnet build   src/Apps/SmartMeasure/SmartMeasure.Android
dotnet publish src/Apps/SmartMeasure/SmartMeasure.Android -c Release   # AAB, nur auf Anfrage
```
