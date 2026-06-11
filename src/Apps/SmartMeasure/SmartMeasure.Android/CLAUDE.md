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
| `Ar/AndroidArCaptureService.cs` | `IArCaptureService`-Impl: startet `ArCaptureActivity` via Intent, TCS-Bridge, `LastCompletionStatus` + `LastError`. |
| `Ar/AndroidArSession.cs` | `IArSessionLike`-Wrapper um `Google.AR.Core.Session` — entkoppelt Pose/HitTest-Logik von ARCore. |
| `Ar/ArBackgroundRenderer.cs` | OpenGL ES 3.0 Kamera-Preview (Vertex+Fragment-Shader für Camera-Textur). |
| `Ar/ArPointOverlayView.cs` | Transparenter Canvas: gesamtes HUD (3D-Punkte, Linien, Flächen, Boden-Raster, Banner, Pillen, Footer, Modus-Chip, Stats, Readiness-Badge). `partial sealed`. |
| `Ar/ArPointOverlayView.Design.cs` | Design-System des Overlays: semantische Farb-Tokens (Klasse `C`), Typo-Schnitte, `DrawPanel`/`DrawStatusDot` — das EINE Glas-Panel-Primitiv für alle HUD-Container. |
| `Ar/ArAnchorManager.cs` | Drift-Kompensation via Earth-Anchors. Enthält auch `ArStabilityMonitor`. |
| `Ar/ArPrecisionHelpers.cs` | Depth-Sanity, Ground-Plane, Semantic-Label, Sky-Check. Delegiert Math an `ArMathHelpers` (Shared). |
| `Ar/ArOverlayState.cs` | Snapshot-Record: alle Render-Parameter + lokalisierte Labels + System-Banner. |
| `Ar/MediaStoreGallery.cs` | Speichert Screenshots (Bild) + Aufnahmen (Video) via MediaStore in `Pictures/SmartMeasure` / `Movies/SmartMeasure` (IS_PENDING-geschuetzt, Cleanup bei Fehler). Sichtbar in der Galerie, ueberlebt Deinstall, keine Permission ab API 29. |
| `Services/AndroidAppPaths.cs` | `IAppPaths`-Impl: `Context.FilesDir` + `GetExternalFilesDir("Exports")` → sandbox-sichere Pfade. |
| `Services/AndroidVoiceAnnotationService.cs` | `IVoiceAnnotationService`-Impl: Android `SpeechRecognizer`, 5 s Aufnahme, liefert Transkript. |

---

## MainActivity — Reihenfolge in `OnCreate`

**VOR `base.OnCreate`** (DI-Build läuft in `AvaloniaAndroidApplication.OnCreate` auf Application-Ebene — also noch vor diesem `MainActivity.OnCreate`. `base.OnCreate` hier ruft `InitializeAvaloniaView` auf, das `App.MainViewFactory` auslöst und erst dann das `MainViewModel` auflöst. Factories müssen daher VOR `base.OnCreate` gesetzt sein):

```
App.AppPathsFactory = () => new AndroidAppPaths(this)
App.ArCaptureServiceFactory = _ => _arCaptureService   (STATISCHE AndroidArCaptureService-Instanz)
App.VoiceAnnotationServiceFactory = _ => new AndroidVoiceAnnotationService(this)
UriLauncher.PlatformShareFile = ShareFileViaIntent
UriLauncher.PlatformOpenFile  = OpenFileViaIntent
```

**AR-Service ist STATISCH + wiederangehängt:** `AndroidArCaptureService` lebt als
DI-Singleton über MainActivity-Recreates hinweg (laufende TCS!). Pro `OnCreate` wird nur die
Activity-Referenz via `AttachActivity(this)` erneuert — eine NEUE Instanz pro Activity würde
nach einem Recreate die `OnActivityResult`-Zustellung von der awaitenden TCS trennen
(CaptureAsync hinge für immer, AR-Button dauerhaft tot).

**NACH `base.OnCreate`:**

```
_mainVm aus App.Services holen
ExitHintRequested    → Toast (double-back)
MessageRequested     → Toast (lange Toasts für Fehler)
RequestLocationPermissionIfNeeded()
```

**Back-Button:** `OnBackPressed()` delegiert an `_mainVm.HandleBackPressed()`; sonst `base.OnBackPressed()`.

---

## Permissions (Manifest + Runtime)

| Permission | Wann | Grund |
|-----------|------|-------|
| `ACCESS_FINE_LOCATION` | Runtime, alle Level | Mapsui + AR-Georeferenzierung (Geospatial API) — KEINE harte AR-Voraussetzung |
| `CAMERA` | Runtime, vom AR-Service angefragt | ARCore (einzige harte Voraussetzung) |
| `VIBRATE` | Manifest | Haptik (Punkt-Setzen, Warnungen) — ohne sie verschluckt Android Vibrator-Calls still |

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
  → nach erfolgreichem Transfer: ConfirmResultPersisted() → ClearRecoveryState
```

`HandleActivityResult` / `HandlePermissionResult` in `AndroidArCaptureService` müssen von
`MainActivity.OnActivityResult` / `OnRequestPermissionsResult` delegiert werden.
Permissions: nur CAMERA ist harte AR-Voraussetzung (Location degradiert sauber zur
Relativ-Messung); bei dauerhaftem Camera-Deny öffnet der Service die App-Einstellungen.

---

## FileProvider (Export + Share)

Authority: `{packageId}.fileprovider` → `AndroidX.Core.Content.FileProvider.GetUriForFile(this, PackageName + ".fileprovider", file)`.
Manifest: `<provider android:authorities="${applicationId}.fileprovider" android:grantUriPermissions="true" .../>`.
Pfade: `Resources/xml/provider_paths.xml`.
`UriLauncher.PlatformShareFile` → `Intent.ActionSend`, `PlatformOpenFile` → `Intent.ActionView`.

---

## Build

```bash
dotnet build   src/Apps/SmartMeasure/SmartMeasure.Android
dotnet publish src/Apps/SmartMeasure/SmartMeasure.Android -c Release   # AAB, nur auf Anfrage
```
