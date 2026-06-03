# SmartMeasure — 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt (nicht im Play Store). Zwei Erfassungsmodi: **AR-Kamera** (±5–50 cm, primär,
ohne Hardware) und optional **RTK-GPS-Stab** (±2 cm, DIY-Hardware). Man geht durch den Garten,
setzt Punkte, zeichnet Konturen → 3D-Geländemodell + 2D-Gartenplan. Export nach Blender,
GeoJSON, DXF, KMZ, CSV, PDF.

| Aspekt | Wert |
|--------|------|
| Plattformen | Desktop (Entwicklung/Mock) + Android (Samsung Galaxy S25 Ultra) |
| Min SDK | 26 (Android 8.0) |
| ARCore-Paket | Vapolia.Google.ARCore 1.47.1 |
| BLE-Paket | InTheHand.BluetoothLE |

Generische Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator. Kein AdMob/IAP (privates Projekt).

```
SmartMeasure.Android ┐
                     ├─> SmartMeasure.Shared ──> MeineApps.Core.Ava  (Preferences, Localization, ViewLocator)
SmartMeasure.Desktop ┘                       └─> MeineApps.UI        (SkiaSharp-Helpers, Behaviors)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `SmartMeasure.Shared/App.axaml.cs`
(Factory-Properties → DI-Build → `MainViewModel`) → ViewLocator löst die 9 Views.
Desktop nutzt `MockBleService` + `MockArCaptureService` statt echter Hardware.

### Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Doku |
|---------|------|
| Composition Root, DI, Factory-Reihenfolge, Lifecycle | [SmartMeasure.Shared](SmartMeasure.Shared/CLAUDE.md) |
| Android-Host, AR-Brücke, BLE-Service, Permissions, FileProvider | [SmartMeasure.Android](SmartMeasure.Android/CLAUDE.md) |
| Desktop-Host, Mock-Modus | [SmartMeasure.Desktop](SmartMeasure.Desktop/CLAUDE.md) |
| ViewModels (Navigation, Messung, Terrain, ...) | [Shared/ViewModels](SmartMeasure.Shared/ViewModels/CLAUDE.md) |
| Views (AXAML, Touch, Lazy-Map, SKCanvasView-Pattern) | [Shared/Views](SmartMeasure.Shared/Views/CLAUDE.md) |
| Services (Geo-Algorithmen, BLE-Mock, Export, AR-Math) + Gotchas | [Shared/Services](SmartMeasure.Shared/Services/CLAUDE.md) |
| Models (SQLite-Entities, TerrainMesh, AR-Typen) | [Shared/Models](SmartMeasure.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (Terrain, GardenPlan, Kompass, Stakeout) + Farbpalette | [Shared/Graphics](SmartMeasure.Shared/Graphics/CLAUDE.md) |
| DIY-Hardware-Detail (Stückliste, Firmware, Verkabelung) | Memory `smartmeasure.md` |

Diese Datei trägt nur, was **app-übergreifend** ist: den AR-First-Betriebsmodus (spannt über
alle VMs/Views), die übergreifenden Datenflüsse, die ARCore-Capture-Activity (UX + AR-Features)
und die Hardware-BOM-Eckdaten. Service-/Renderer-/Algorithmus-Detail und die Gotcha-Tabellen
leben in den jeweiligen Unterordner-Dateien (siehe Doku-Karte) — hier nicht wiederholt.

---

## Adaptiver Betriebsmodus (AR-First) — Kern-Architektur

Die App startet im reinen AR-Modus: die gesamte RTK-Hardware-UI (Live-Kompass, BLE-Tab,
Stab-Einstellungen, Stakeout) ist ausgeblendet, bis erstmals ein Stab verbunden wird. Danach
merkt sich die App das (Preference) und zeigt die Hardware-UI dauerhaft.

`IHardwareModeService` (Singleton) ist die zentrale Quelle für `ShowRtkUi`. Er hört auf
`IBleService.StateChanged`, persistiert die Erst-Verbindung (Preference `sm.has_ever_connected_ble`)
und feuert `Changed` (vom BLE-Background-Thread → Konsumenten marshallen via `Dispatcher.UIThread.Post`).

```
ShowRtkUi = IsConnected || HasEverConnectedBle   // sonst reiner AR-Modus
```

`MainViewModel`, `SurveyViewModel`, `SettingsViewModel` injizieren den Service und binden
gegen `ShowRtkUi` / `!ShowRtkUi`:

| View | AR-Modus (`!ShowRtkUi`) | RTK-Modus (`ShowRtkUi`) |
|------|-------------------------|--------------------------|
| MainView Status-Bar | schlanke Marken-Leiste + AR-Chip | volle Hardware-Bar (BLE/Fix/Sat/Akku) |
| MainView Tab-Bar (`UniformGrid Rows="1"`) | 6 Tabs (BLE + Abstecken aus) | 8 Tabs |
| SurveyView | AR-Hero-CTA + ehrlicher ±5–50 cm-Hinweis + Live-Statistik | Kompass/Position/PUNKT-Button |
| SettingsView | "RTK-Stab verbinden"-Einstieg | Stab-Optionen + "Zurück zum AR-Modus" (`ResetToArMode`) |

Wichtig: `UniformGrid Rows="1"` verteilt nur **sichtbare** Kinder — versteckte Tabs (BLE,
Abstecken via `IsVisible="{Binding ShowRtkUi}"`) hinterlassen keine Lücke. Der Connect-Screen
bleibt per `Navigate("Connect")` erreichbar, auch wenn sein Tab-Button ausgeblendet ist
(Settings → `ConnectRtkStickCommand`). Der `PUNKT`-Button ist `IsEnabled="{Binding IsBleConnected}"`;
die Punkte-Liste + Statistik wird aus `IMeasurementService.PointAdded/PointsReset` gespeist,
damit AR-Punkte ebenso erscheinen.

---

## Übergreifende Datenflüsse

### RTK-GPS Datenfluss

```
ESP32-Rover → BLE GATT → AndroidBleService.OnCharacteristicChanged
  → ParsePointData (BinaryPrimitives.ReadDoubleLittleEndian, little-endian!)
  → Geoid-Korrektur (IGeoidService.EllipsoidToGeoid)
  → Tilt-Korrektur (vertikal immer, horizontal nur bei MagAccuracy ≥ 2)
  → PointReceived-Event (Background-Thread)
  → SurveyViewModel.OnPointReceived (via Dispatcher.UIThread.Post)
  → MeasurementService.AddPoint → PointAdded-Event → Terrain/Map/GardenPlan-VMs
```

### AR → Terrain Transfer

```
ArCaptureActivity → ConsumeLastResult → AndroidArCaptureService → TCS
  → SurveyViewModel.ArCaptureCompleted-Event
  → MainViewModel: ArTransferService.TransferToProjectAsync
    → RotateAndProject (ARCore +Z = hinten — Rotations-Formel siehe Services-CLAUDE.md)
    → IGeoidService für Höhen-Korrektur
    → ProjectService.AddPointAsync + AddGardenElementAsync
```

### Projekt-Load (Batch, NICHT iterativ)

```
ProjectsView.OpenProject → MainViewModel lädt aus DB
  → MeasurementService.ReplacePoints (EIN PointsReset-Event!)
  → TerrainViewModel.RecalculateMesh (1× für N Punkte, nicht N×)
  → GardenPlanViewModel.LoadElementsFromProjectAsync
```

`ReplacePoints` + `PointsReset`-Event verhindert O(N²)-Triangulation beim Load.

### Export-Pattern

```
ProjectsViewModel.ExportXxxAsync
  → Datei in IAppPaths.ExportFolder schreiben
  → FileExportReady-Event mit Pfad
  → MainViewModel → MessageRequested + ExportBanner-State
  → MainActivity: Share-Intent (FileProvider) oder Open-Intent (MIME-Type)
```

### Tilt-Korrektur (Antenne → Stabspitze)

```
h_tip     = h_antenne - stabHeight * cos(tilt)          // immer
east_off  = stabHeight * sin(tilt) * sin(azimuth)       // nur MagAccuracy ≥ 2
north_off = stabHeight * sin(tilt) * cos(azimuth)       // nur MagAccuracy ≥ 2
```

Bei 5°/1,8 m Stab: 15,7 cm horizontaler Versatz. `SetStabHeightAsync` setzt Wert im BLE-Service.

---

## ARCore-Capture-Activity (Android)

`ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia), als `partial class`
über drei Files verteilt (Datei-/Verantwortungs-Trennung). Brücke ins Shared-Projekt via
`TaskCompletionSource<ArCaptureResult?>` in `AndroidArCaptureService` (Factory-Wiring →
[Android-CLAUDE.md](SmartMeasure.Android/CLAUDE.md)). TCS-Lock-Pattern + Status-Enum
`IArCaptureService.LastCompletionStatus` (`Success | UserCancelled | Error`) + `LastError`
erlauben dem UI-Layer, User-Abbruch von echten Fehlern zu trennen (`SurveyViewModel` zeigt
unterschiedliche Meldungen je Status).

### Layout (3 Schichten)

```
FrameLayout
├── GLSurfaceView          OpenGL ES 3.0 Kamera-Preview (ArBackgroundRenderer)
├── ArPointOverlayView     Transparenter Canvas (Punkte, Linien, Auswahl)
└── Native Toolbar          7 Icon+Label-Buttons (VectorDrawables Resources/drawable/ic_ar_*):
                            Punkt · Fläche · Schließen · Zurück · Vor · Mehr · Fertig.
                            "Mehr" = PopupMenu (Maßband, Tachymeter, Abstecken, Löschen,
                            Screenshot, Aufnahme, Hilfe). KEINE Emojis/Unicode als UI-Text.
```

### Lokalisierung (`ArOverlayLabels`)

Die Activity hat keine Avalonia-DI. Lokalisierte Strings werden einmalig in `OnCreate` via
`LoadLocalizedLabels()` aus `AppStrings.*` gelesen und als `ArOverlayLabels`-Record in jedem
`ArOverlayState`-Snapshot mitgegeben. Sprachwechsel mid-AR-Session passiert nicht
(Modal-Fullscreen) → ein Snapshot pro Session genügt.

### Capture-Modi (`CaptureMode`)

| Mode | Verhalten |
|------|-----------|
| `Point` | Einzelne Messpunkte ins Projekt + Undo-Stack + Foto-Annotation. |
| `Contour` | Aktive Kontur (Weg/Beet/Mauer/...) — Mehrfach-Tap + `CloseActiveContour` mit Bowditch-Correction + Foto-Annotation pro Punkt. |
| `TapeMeasure` | Ad-hoc-Distanz. Eigener Buffer `_tapeMeasurePoints`, kein Projekt-Save, kein Undo, kein Foto. Long-Press auf Maß-Button = Reset. Footer zeigt Σ Strecken-Summe. |
| `Stakeout` | Pfeil + Distanz + Target-Label zum nächsten unerreichten Ziel. Targets via `IArCaptureService.SetStakeoutTargets`. Hysterese-Reached bei ≤ 10 cm (von > 30 cm kommend). |
| `TotalStation` | Stationierung + Radial-Projektion (Distanz + Bearing + Pitch → Lat/Lon) via `ITotalStationService`. |

### Marker-Overlays

- **Site-Marker** (`IArCaptureService.SetSitePoints`): bestehende Projekt-`SurveyPoints` werden
  vor Session-Start übergeben. Sobald Geospatial-Tracking aktiv ist, erzeugt
  `CreatePendingSiteAnchors` Earth-Anchors (max 2/Frame). Render als dezente graue Kreise — neue
  Punkte landen im selben Koordinatensystem.
- **RTK-Stab Live-Marker**: bei verbundenem BLE-Stab + Geospatial-Tracking refresht
  `UpdateRtkStabAnchor` 1×/s den Earth-Anchor an der aktuellen Stab-Position. Render:
  pulsierender Marker (1 Hz Sinus) + Fix-Quality-Farbe (Grün = RTK-Fix, Gelb = Float,
  Orange = DGPS, Rot = GPS-only). `PostInvalidateDelayed(33)` hält die Pulse-Animation.

### ARCore-Features aktiv

| Feature | Zweck |
|---------|-------|
| `ArAnchorManager` | Drift-Kompensation: Anchor pro gesetztem Punkt, RefreshAnchors pro Frame |
| `ArPoseSampler` (Shared.Services) | Multi-Frame-Averaging (15 Samples / 800 ms), Median + ±3σ-Outlier-Filter |
| `ArStabilityMonitor` (in `ArAnchorManager.cs`) | EMA über Gyro + Accel, StabilityScore 0..1, Block bei < 0,6 |
| `ArPrecisionHelpers` | Depth-Sanity, Depth-Fallback (Instant-Placement), Ground-Plane, Heading-Extraktion, Semantic-Label, Sky-Check. Math-Helfer delegiert an `ArMathHelpers` (Shared) |
| `ArSnapEngine` (Shared.Services) | Vertex (15 cm), Right-Angle (5°), Parallel (3°), Extension (10 cm) |
| Geospatial API (VPS) | `earth.CameraGeospatialPose` → Heading ±5° statt ±15–30° (Metall-immun) |
| Earth-Anchors | Persistent über Session-Ende via VPS re-lokalisierbar — Recovery-Restore queued Punkte für Re-Attach sobald Earth-Tracking aktiv |
| Raw Depth + Confidence | Pixel mit Confidence > 0,3 (Random-Noise-Filter) |
| Scene Semantics | `SemanticMode.Enabled` — Sky + Instant-Placement-Kombi wird abgelehnt, sonst Label in `ArPoint.SemanticLabel` |
| Light-Estimation | `LightEstimate.PixelIntensity` — Helligkeits-Sprung > 40 % bricht laufendes Sampling ab (2 s Cooldown) |
| RTK-AR-Fusion | `IBleService`-Snapshot via `App.Services` — RTK-Position als GPS-Anker (±2 cm) statt Android-LocationManager (±5 m). `ArGpsSource`-Enum trackt die Quelle bis in `ArTransferService` (kein 50 cm-Min, kein 100×-Faktor für RTK) |
| Augmented Images (ArUco) | AugmentedImageDatabase + Erkennungs-Loop + Auto-Anchor an eingemessener Position |
| Session Recovery | State in SharedPreferences nach jedem Punkt, max 30 Min alt |
| Recording API | MP4 in `ExternalFilesDir/Recordings/`, `SetAutoStopOnPause(true)` |

**Bewusst NICHT aktiviert:** Cloud Anchors (kostenpflichtig — Earth-Anchor-Cache ist Default),
Shared Camera/Camera2 (Vapolia-Binding unvollständig).

### Bowditch-Korrektur

Bei Kontur-Close: Schlussfehler-Vektor proportional zur Distanz auf alle Zwischenpunkte
verteilen. Nur aktiv bei 1 cm–2 m Schlussfehler (kleiner: unnötig, größer: Fehler-Detection).

### Foto-Annotation pro Punkt

Bei jedem AR-Punkt (Point + Contour, NICHT TapeMeasure) macht `CapturePhotoForPoint` via
`PixelCopy.Request` einen JPEG-Snapshot des reinen Kamera-Frames (ohne Overlay) und legt ihn in
`IAppPaths.PhotosFolder` ab (`pt_<timestamp>_<guid>.jpg`, Quality 80, ~200 KB). `ArPoint.PhotoPath`
wird sofort gesetzt, der Disk-Write läuft asynchron → PDF-Bericht muss `File.Exists` prüfen.
Pfad wandert durch `ArTransferService` in `SurveyPoint.PhotoPath`.

### Confidence-Formel

```
confidence =
    Hit-Quality     (0.1 Instant / 0.2 Point / 0.3 Plane)
  + StdDev          (0.3 wenn σ=0, linear auf 0 bei σ=5 cm)
  + Stability       (0.2 × StabilityScore)
  + Anchor-Bonus    (+0.2 wenn Anchor erstellt)
→ max 1.0
```

### UX-Features (AR-Modus)

| Feature | Beschreibung |
|---------|-------------|
| Bestätigungs-Dialoge | Löschen + Fertig fragen vor destruktiver Aktion (`ConfirmDeleteSelectedPoint`, `ConfirmFinishCapture`) |
| Sound beim Punkt-Setzen | `MediaActionSound.SHUTTER_CLICK` zusätzlich zur Vibration. SharedPreferences-Key `ar.sound.enabled` (Default an). Toggle im Help-Dialog. |
| Pop-Animation neuer Punkte | 250 ms Scale-Easing in `ArPointOverlayView.DrawPoints` — junge Punkte (< 250 ms alt) starten 2.2× groß, schrumpfen mit Ease-Out-Quadratic |
| Tooltips auf Toolbar-Buttons | Long-Press zeigt `Button.TooltipText` (API 26+) |
| Coach-Marks beim 1. AR-Start | Show-once Dialog (Crosshair/Workflow/Toolbar). Key `ar.coachmarks.shown`. "Später nochmal" lässt Pref unverändert → nächster Start zeigt erneut |
| Persistente System-Banner | `ArOverlayState.ThermalWarning` + `BatteryWarning` als persistente Top-Banner unter dem Tracking-Banner (vs. TransientHint-Fade) |
| Live-Footer-Bar | Über der Toolbar mit Punkte/Länge/Fläche in großer Schrift (`ArPointOverlayView.DrawLiveFooter`) |
| Live-Segment ("Gummiband") | Beim Punkt-/Kontur-Zeichnen: gestrichelte Linie vom zuletzt gesetzten Punkt zum Crosshair + schwebende Pille mit **Horizontaldistanz** (groß), **ΔH** + **Steigung %** (klein), HitQuality-gefärbt. Reticle-Weltpos wird in `BuildHitInfo` gespeichert → `BuildOverlayState` rechnet `Distance2DTo` (horizontal) / `DistanceTo` (schräg) / Y-Delta. Felder: `ArOverlayState.ShowLiveSegment` + `LiveSegment{FromScreen,Horizontal,Slope,HeightDelta}`. Render: `ArPointOverlayView.DrawRubberBand`/`DrawValuePill`. Frustum-geclippt (kein Springen). Distanzen < 1 m in cm (`FormatMeters`) |
| Kontur-Segment-Labels | Gesetzte aktive-Kontur-Segmente zeigen ihre horizontale Welt-Distanz zwischen den Punkten (`ActiveContourSegmentMeters` vom GL-Thread, gerendert in `DrawInterPointDistances` — früher leerer Stub) |
| Readiness-Badge Tap | Badge oben links klickbar (`ReadinessBadgeBounds`). Detail-Dialog mit Checkliste je Condition (Stabilität / Kompass / Planes / GPS / Geospatial / Tracking-Continuity) |
| Recovery-Bestätigungs-Dialog | "X Punkte aus letzter Sitzung wiederherstellen?" mit Wiederherstellen/Verwerfen — statt Auto-Restore. Earth-Anchors parallel re-attached |

### S25-Ultra-Spezifika

- `LightEstimationMode.EnvironmentalHdr` wenn RAM ≥ 8 GB
- Multi-Sample-Count: 15 (High-End) / 10 (Normal) / 5 (Thermal Severe)
- `PowerManager.CurrentThermalStatus` alle 60 Frames prüfen
- `OnApplyWindowInsets` liest Punch-Hole-Cutout → `ArOverlayState.TopInsetPixels`

---

## Hardware (nicht Teil der Solution)

Eckdaten; vollständige Stückliste, Firmware, Verkabelung → Memory `smartmeasure.md`.

### Rover-Stab (~285–375 EUR)

- ESP32-S3-WROOM + ZED-F9P (RTK, ±2 cm) + L1/L2 Multi-Band-Antenne
- BNO085 (9-Achsen IMU, Sensor Fusion, Tilt + Kompass)
- **AP2112K-3.3 LDO** — NIEMALS AMS1117 (1,1 V Dropout, stirbt bei halbem Akku!)
- SSD1306 OLED, Piezo, WS2812B RGB LED, 2× Taster
- 2× 18650 parallel (6000 mAh, ~10 h), TP4056 USB-C
- Alu-Rohr 1,5 m + Edelstahl-Spitze + **PETG**-Gehäuse (kein PLA — UV-spröde!)

**ESP32-S3 Pin-Belegung:** GPIO 8/9 I2C (BNO085 + SSD1306) · GPIO 17/18 UART1 (ZED-F9P) ·
GPIO 38 WS2812B (**NICHT GPIO 48** — oft reserviert auf DevKits!) · GPIO 1 (ADC1_CH0) Akku-Spannung.

### BLE GATT-Profil

- Position @2 Hz: 3× float64 (Lat, Lon, Alt Ellipsoid — App korrigiert zu NN)
- Fix Quality: uint8 (0=NoFix, 4=RTK-Fix, 5=Float) · Accuracy: 2× float32 (H-cm, V-cm)
- Orientation @5 Hz: 3× float32 (Pitch, Roll, Yaw) · Battery @0,1 Hz: uint8
- Point Trigger: SurveyPoint komplett bei Knopfdruck (inkl. TiltAngle + TiltAzimuth)
- Write: StabHeight, NTRIP-Config, WiFi-Config
- ESP-IDF: `esp_coex_preference_set(ESP_COEX_PREFER_WIFI)` — NTRIP hat Priorität

### Basisstation (~250–340 EUR)

ESP32-S3 + ZED-F9P + NTRIP-Server auf Port 2101. Handy-Hotspot verbindet Basis + Rover
(kein öffentliches Internet nötig).

---

## App-spezifische Conventions

### Mock-Modus (Desktop-Entwicklung)

- `MockBleService` + `MockArCaptureService` ersetzen Hardware. `MockBleService` startet
  disconnected → Desktop zeigt AR-First-Modus (gewollt; RTK-UI via Settings testbar).
- Debug-Panel in SurveyView nur bei `IsMockMode=true`. Edge-Cases: `CycleFixDegradation`,
  `SimulatePacketLoss`, `SimulateBatteryDrain`, `SimulateMagLoss`, `SimulateSpuriousDisconnect`.

### Thread-Safety (AR-Activity-spezifisch)

- Alle BLE-Events via `Dispatcher.UIThread.Post` marshallen.
- `_dataLock` in `ArCaptureActivity` für alle Zugriffe auf `_points`, `_contours`, `_activeContour`.
  Undo/Redo-Actions halten Lock-Reference + setzen Lock bei Mutation.
- `_frameLock` für `_lastFrame` (GL-Thread schreibt, UI-Thread liest).
- `RunOnUiThread` für alle Overlay-State-Updates.

### Android-Build

- `OperatingSystem.IsAndroidVersionAtLeast(31)` statt `Build.VERSION.SdkInt` (Static-Analyzer).
- `SupportedOSPlatformVersion=26` im csproj.
- `RunAOTCompilation=false` + `AndroidEnableProguard=false` (Mapsui/NTS brauchen Reflection).
- `ArCaptureActivity` Theme: `@style/MyTheme.Fullscreen` (AppCompat, NICHT `android:Theme.Black`).
- `global::Android.Content.Res.…` voll qualifizieren (Namespace-Kollision App vs. Android-SDK → CS0234).

---

## Build

```bash
dotnet build src/Apps/SmartMeasure/SmartMeasure.Shared
dotnet run   --project src/Apps/SmartMeasure/SmartMeasure.Desktop
dotnet build src/Apps/SmartMeasure/SmartMeasure.Android
```

---

## Verweise

- Hardware-Detail: Memory `smartmeasure.md`
- DI/MVVM/DateTime/Thread-Safety, Naming, Localization: [Haupt-CLAUDE.md](../../../CLAUDE.md)
- SkiaSharp/Rendering-Gotchas: [MeineApps.UI](../../../UI/MeineApps.UI/CLAUDE.md)
- Avalonia/MVVM/Android-Framework-Fallstricke: [MeineApps.Core.Ava](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md)
