# SmartMeasure — 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt. Zwei Erfassungsmodi: **RTK-GPS-Stab** (±2 cm, DIY-Hardware) und
**AR-Kamera** (±5–50 cm, Samsung Galaxy S25 Ultra). Man geht durch den Garten, setzt
Punkte, zeichnet Konturen → 3D-Geländemodell + 2D-Gartenplan. Export nach Blender,
GeoJSON, DXF, KMZ, CSV, PDF. Nicht im Play Store.

| Aspekt | Wert |
|--------|------|
| Aktuelle Version | v1.2.0 (UX-Refactoring AR-Modus) |
| Modus | Desktop (Entwicklung/Mock) + Android (Samsung Galaxy S25 Ultra) |
| Min SDK | 26 (Android 8.0) |
| ARCore-Paket | Vapolia.Google.ARCore 1.47.1 |
| BLE-Paket | InTheHand.BluetoothLE |

Für generische Build-Befehle, Conventions und Troubleshooting → [Haupt-CLAUDE.md](../../../CLAUDE.md).

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `SmartMeasure.Shared` | `net10.0` | `dotnet build src/Apps/SmartMeasure/SmartMeasure.Shared` |
| `SmartMeasure.Desktop` | `net10.0` | `dotnet run --project src/Apps/SmartMeasure/SmartMeasure.Desktop` |
| `SmartMeasure.Android` | `net10.0-android` | `dotnet build src/Apps/SmartMeasure/SmartMeasure.Android` |

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `SmartMeasure.Shared/ViewModels/` | `SmartMeasure.ViewModels` |
| `SmartMeasure.Shared/Views/` | `SmartMeasure.Views` |
| `SmartMeasure.Shared/Services/` | `SmartMeasure.Services` |
| `SmartMeasure.Shared/Models/` | `SmartMeasure.Models` |
| `SmartMeasure.Shared/Graphics/` | `SmartMeasure.Graphics` |

---

## Projekt-Struktur

```
src/Apps/SmartMeasure/
├── SmartMeasure.Shared/
│   ├── ViewModels/        # 9 ViewModels (Connect, Survey, Terrain, GardenPlan, Map, Projects, Stakeout, Settings, Main)
│   ├── Views/             # 9 Views mit x:CompileBindings="True" (inkl. StakeoutView + MainView)
│   ├── Services/          # Interfaces + Shared-Impls (AppPaths, MockBleService, MockArCaptureService, ...)
│   ├── Models/            # SurveyPoint, SurveyProject, NtripConfig, MaterialEstimate, StickState,
│   │                      # GardenElement, StakeoutTarget, TerrainMesh, ArPoint, ArContour, ArCaptureResult
│   └── Graphics/          # 5 SkiaSharp-Renderer (Terrain, GardenPlan, SurveyLive, Stakeout, Thumbnail)
├── SmartMeasure.Desktop/
│   └── Program.cs         # Einstiegspunkt. Kein eigener Service-Ordner — Desktop nutzt Shared-Impls
└── SmartMeasure.Android/
    ├── Ar/                # ArCaptureActivity, ArBackgroundRenderer, ArPointOverlayView,
    │                      # AndroidArCaptureService, ArAnchorManager, ArPrecisionHelpers, ArOverlayState
    └── Services/          # AndroidBleService, AndroidAppPaths, MeasurementForegroundService
```

---

## Services

| Service | Aufgabe |
|---------|---------|
| `IAppPaths` | Plattform-abstrahierte Pfade (Android: `Context.FilesDir`, Desktop: `ApplicationData`) |
| `IBleService` | BLE-Kommunikation zum Rover-Stab (plattform-spezifisch) |
| `MockBleService` | Simuliert RTK-Daten + Edge-Cases für Desktop-Entwicklung |
| `IArCaptureService` | AR-Kamera-Erfassung (Android: ARCore, Desktop: Mock) |
| `IArTransferService` | AR-Punkte → SurveyPoints (GPS-Fusion, Heading-Rotation, Geoid-Korrektur) |
| `IMeasurementService` | Punkt-Verwaltung, Abstände, Flächen. `ReplacePoints` + `PointsReset`-Event für Batch-Load |
| `ICoordinateService` | WGS84 ↔ UTM (Transverse-Mercator). `ToUtmFixedZone` für konsistente Zone über Grenzen |
| `IGeoidService` | EGM96 Ellipsoid→NN, hardcoded 2°-Grid für DE (±0,5–1 m). Toggle bei Firmware-MSL |
| `ITerrainService` | Bowyer-Watson Delaunay, Konturlinien, Volumen, Convex Hull |
| `IGardenPlanService` | Gartenelemente CRUD, PointsJson v2 (WGS84 absolut), v1-Legacy-Fallback |
| `IProjectService` | SQLite-Persistenz (Projekte, Punkte, Elemente). `DeleteProject` atomar in Transaktion |
| `IExportService` | CSV, GeoJSON, DXF, KMZ, PDF |
| `IBlenderExportService` | OBJ + MTL (Y/Z kein Swap — UTM-Koords sind bereits Blender-Standard Z-up) |
| `ArSnapEngine` | Geometrische Snap-Hilfen: Vertex (15cm), Right-Angle (5°), Parallel (3°), Extension (10cm zur Verlaengerung, min 5cm jenseits Edge-Ende). Stateless, in Shared damit testbar. |
| `ArPoseSampler` | Multi-Frame-HitTest-Averaging: Median + ±3σ-Outlier-Filter + Mittel auf bereinigten Samples. In Shared. |
| `ArMathHelpers` | `ApplyBowditchCorrection` + `ExtractHeadingFromQuaternion` + `ExtractPitchFromQuaternion`. Pure Mathematik, in Shared. `ArPrecisionHelpers` delegiert dorthin. |
| `IDifferentialSnapshotService` | Vergleicht zwei Vermessungs-Snapshots desselben Grundstuecks (Greedy-Nearest-Neighbor, 3D-Distanz inkl. Hoehe). Liefert Moved/Unchanged/Added/Removed. |

---

## Architektur-Patterns

### IAppPaths-Pattern (Android-Sandbox-Fix)

`Environment.SpecialFolder.LocalApplicationData` crasht auf Android im DI-Kontext.
Immer `IAppPaths` per Constructor-Injection verwenden:

```csharp
// App.axaml.cs
public static Func<IAppPaths>? AppPathsFactory { get; set; }

// MainActivity.cs — VOR DI-Build registrieren
App.AppPathsFactory = () => new AndroidAppPaths(this);
```

Betroffen: `ProjectService`, `SettingsViewModel`, `ProjectsViewModel`, `ExportService` — alle
müssen `IAppPaths` per DI bekommen, NIEMALS `Environment.GetFolderPath` direkt aufrufen.

### AR-Capture-Pattern (Separate Activity)

`ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia).
Brücke via `TaskCompletionSource<ArCaptureResult?>` in `AndroidArCaptureService`.
Factory-Pattern analog zu BleService und AppPaths:

```csharp
// App.axaml.cs
public static Func<IServiceProvider, IArCaptureService>? ArCaptureServiceFactory { get; set; }
```

TCS-Lock-Pattern + Status-Enum: `IArCaptureService.LastCompletionStatus`
(`Success | UserCancelled | Error`) + `LastError`-Klartext erlauben dem UI-Layer
User-Abbruch von echten Fehlern zu trennen. Der `SurveyViewModel` zeigt
unterschiedliche Meldungen je nach Status, statt pauschal "AR abgebrochen".

### AR-Overlay-Lokalisierung (`ArOverlayLabels`)

`ArCaptureActivity` ist eine native Activity ohne Avalonia-DI. Lokalisierte
Strings werden einmalig in `OnCreate` via `LoadLocalizedLabels()` aus
`AppStrings.*` gelesen und als `ArOverlayLabels`-Record in jedem `ArOverlayState`-
Snapshot mitgegeben. Sprachwechsel mid-AR-Session passieren nicht (Modal-Fullscreen),
daher reicht ein Snapshot pro Session.

### RTK-GPS Datenfluss

```
ESP32-Rover → BLE GATT → AndroidBleService.OnCharacteristicChanged
  → ParsePointData (BinaryPrimitives.ReadDoubleLittleEndian, little-endian!)
  → Geoid-Korrektur (IGeoidService.EllipsoidToGeoid)
  → Tilt-Korrektur (vertikal immer, horizontal nur bei MagAccuracy ≥ 2)
  → PointReceived-Event (Background-Thread)
  → SurveyViewModel.OnPointReceived (via Dispatcher.UIThread.Post)
  → MeasurementService.AddPoint
  → PointAdded-Event → Terrain/Map/GardenPlan-VMs
```

### AR → Terrain Transfer

```
ArCaptureActivity → ConsumeLastResult → AndroidArCaptureService → TCS
  → SurveyViewModel.ArCaptureCompleted-Event
  → MainViewModel: ArTransferService.TransferToProjectAsync
    → RotateAndProject (ARCore +Z = hinten — Rotation-Formel korrekt)
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

FileProvider-Authority: `{packageId}.fileprovider` + `Resources/xml/provider_paths.xml`.

### PointsJson v2 (GardenElement)

```json
{"v":2,"points":[[lat,lon],...]}
```

v1-Legacy (lokale UTM-Meter) wird gelesen aber nicht geschrieben.
`GardenElement.LocalPoints` ist `[SQLite.Ignore]` — transient, wird bei Projekt-Load
oder Schwerpunkt-Änderung aus `GardenPlanService.GetLocalPoints` neu projiziert.
ArTransferService persistiert direkt v2 (kein UTM-Zwischenschritt).

---

## Präzisions-Pipeline

### Koordinaten (CoordinateService)

`ToLocalMetric` nutzt UTM-Projektion (statt 111320-Approximation) — spart ~8 cm auf 100 m.
`ToUtmFixedZone(lat, lon, zone)` projiziert in erzwungene Zone — konsistent über Zonengrenzen.
Zonen-Abweichungs-Warnung bei >3° Longitude-Distanz vom Schwerpunkt.

### Geoid-Korrektur (EGM96, ~-48 m in DE)

`IGeoidService.EllipsoidToGeoid(lat, lon, altEllipsoid)` → NN-Höhe.
Hardcoded 2°-Grid 46–56°N, 4–16°E mit bilinearer Interpolation.
Außerhalb: 48 m Pauschal-Fallback + Debug-Warnung.
`IsClientCorrectionEnabled=false` wenn Firmware bereits MSL sendet.

### Tilt-Korrektur (Antenne → Stabspitze)

```
h_tip     = h_antenne - stabHeight * cos(tilt)          // immer
east_off  = stabHeight * sin(tilt) * sin(azimuth)       // nur MagAccuracy ≥ 2
north_off = stabHeight * sin(tilt) * cos(azimuth)       // nur MagAccuracy ≥ 2
```

Bei 5°/1,8 m Stab: 15,7 cm horizontaler Versatz. `SetStabHeightAsync` setzt Wert im Service.

### ARCore Rotations-Formel (KRITISCH — typische Fehlerquelle)

ARCore: +X = rechts, +Y = oben, **+Z = hinten** (vom Gerät weg).
Bei heading=0 zeigt -Z nach Norden. Korrekte Rotation:

```csharp
eastOffset  = arX * cosH - arZ * sinH
nordOffset  = -arX * sinH - arZ * cosH
```

Naive Formel `arX*cosH + arZ*sinH` bricht bei heading ≠ 0°.

### Bowyer-Watson Delaunay (TerrainService)

Härtung für RTK-Genauigkeit (±2 cm):

| Maßnahme | Grund |
|----------|-------|
| Punkt-Dedup bei 1 mm | RTK-Streuung → numerisch instabile Circumcircle-Determinante |
| CCW-Winding (`NormalizeWinding`) | Circumcircle-Test setzt CCW voraus |
| Epsilon `1e-12` in `IsInCircumcircle` | Endless-Loop bei quasi-kollinearen Punkten |
| Super-Triangle Faktor 10 | Robustheit bei engen Point-Sets |
| Konturlinien-Perturbation `1e-9` | Vertex-Hit → Doppel-Intersection |
| `PickLongestSegment` | 3-Intersection-Fall bei nahen Vertices |
| Convex-Hull (Andrew's Monotone Chain) | `CalculateArea2D` braucht geordnete Polygon-Punkte |
| Face-Normalen vorberechnet | Spart 24 k sqrt/s beim 60-fps-Dreh |

---

## Hardware (nicht Teil der Solution)

### Rover-Stab (~285–375 EUR)

- ESP32-S3-WROOM + ZED-F9P (RTK, ±2 cm) + L1/L2 Multi-Band-Antenne
- BNO085 (9-Achsen IMU, Sensor Fusion, Tilt + Kompass)
- **AP2112K-3.3 LDO** — NIEMALS AMS1117 (1,1 V Dropout, stirbt bei halbem Akku!)
- SSD1306 OLED, Piezo, WS2812B RGB LED, 2× Taster (GPIO 4 + 5)
- 2× 18650 parallel (6000 mAh, ~10 h), TP4056 USB-C
- Alu-Rohr 1,5 m + Edelstahl-Spitze + **PETG**-Gehäuse (kein PLA — UV-spröde!)

**ESP32-S3 Pin-Belegung:**
- GPIO 8/9: I2C (BNO085 + SSD1306)
- GPIO 17/18: UART1 (ZED-F9P)
- GPIO 38: WS2812B (**NICHT GPIO 48** — oft reserviert auf DevKits!)
- GPIO 1 (ADC1_CH0): Akku-Spannung

### BLE GATT-Profil

- Position @2 Hz: 3× float64 (Lat, Lon, Alt Ellipsoid — App korrigiert zu NN)
- Fix Quality: uint8 (0=NoFix, 4=RTK-Fix, 5=Float)
- Accuracy: 2× float32 (H-cm, V-cm)
- Orientation @5 Hz: 3× float32 (Pitch, Roll, Yaw)
- Battery @0,1 Hz: uint8
- Point Trigger: SurveyPoint komplett bei Knopfdruck (inkl. TiltAngle + TiltAzimuth)
- Write: StabHeight, NTRIP-Config, WiFi-Config
- ESP-IDF: `esp_coex_preference_set(ESP_COEX_PREFER_WIFI)` — NTRIP hat Priorität

### Basisstation (~250–340 EUR)

ESP32-S3 + ZED-F9P + NTRIP-Server auf Port 2101. Handy-Hotspot verbindet Basis + Rover
(kein öffentliches Internet nötig).

---

## BLE-Architektur (AndroidBleService)

- `RequestMtu(247)` in `OnConnected` VOR `DiscoverServices` (BLE 5.3 DLE, default 23 reicht nicht für 48-Byte-Pakete)
- Write-Queue via `SemaphoreSlim` + `OnCharacteristicWrite`-Acknowledgment (BLE-Writes nicht parallel!)
- `BinaryPrimitives.ReadDoubleLittleEndian` statt `BitConverter` (ESP32 = little-endian, explizit)
- Exponential-Backoff-Reconnect: 1 s → 2 s → 4 s → 10 s, max 5 Versuche

**NTRIP-Config-Validation:**
- Port ∈ [1, 65535]
- Mountpoint ohne `:` (zerstört ESP32-Protokoll)
- `partial void OnNtripXxxChanged` persistiert via `IPreferencesService`

---

## ARCore-Architektur

### ArCaptureActivity Layout (3 Schichten)

```
FrameLayout
├── GLSurfaceView          OpenGL ES 3.0 Kamera-Preview
│   ├── ArBackgroundRenderer   Vertex+Fragment Shader für Camera-Textur
│   └── IRenderer.OnDrawFrame  Session.Update() → Frame → Projektion
├── ArPointOverlayView     Transparenter Canvas (Punkte, Linien, Auswahl)
└── Native Toolbar          Buttons (Punkt, Linie, Schließen, Undo, Redo, Löschen, Screenshot, ?, Fertig)
```

### partial class Aufteilung

`ArCaptureActivity` ist als `partial class` über drei Files verteilt, um die Datei
unter 4000 Zeilen zu halten und Verantwortlichkeiten zu trennen:

| Datei | Inhalt |
|-------|--------|
| `ArCaptureActivity.cs` | OnCreate/OnResume/OnPause/OnDestroy, CreateToolbar, OnDrawFrame, Touch-Handling, FinishCapture, GL-Rendering, Sensors, Geospatial, Thermal/Battery, Haptic, Sound, Screenshot, Snap-to-Edge |
| `ArCaptureActivity.Dialogs.cs` | ConfirmDelete, ConfirmFinish, ShowContourTypeDialog + StartNewContour + UpdateModeButtonHighlight, ShowCompassCalibrationHint, ShowReadinessDetailDialog, ShowHelpDialog, ShowCoachMarksIfNeeded + PersistCoachMarksShown, `ContourTypeOptions`-Tabelle |
| `ArCaptureActivity.Recovery.cs` | SaveRecoveryState, TryRestoreRecoveryState (mit Bestätigungs-Dialog), ClearRecoveryState, Earth-Anchor-Re-Attach-Queue |

ArPointOverlayView ist ebenfalls `partial sealed` markiert (Drawing-Methoden-Split vorbereitet, noch nicht durchgeführt).

### S25-Ultra-Spezifika

- `LightEstimationMode.EnvironmentalHdr` wenn RAM ≥ 8 GB (High-End-Check)
- Multi-Sample-Count: 15 (High-End) / 10 (Normal) / 5 (Thermal Severe)
- `PowerManager.CurrentThermalStatus` alle 60 Frames prüfen
- `OnApplyWindowInsets` liest Punch-Hole-Cutout → `ArOverlayState.TopInsetPixels`
- BLE MTU 247 (BLE 5.3 DLE voll ausgenutzt)

### Capture-Modi (`CaptureMode`)

| Mode | Verhalten |
|------|-----------|
| `Point` | Einzelne Messpunkte ins Projekt + Undo-Stack + Foto-Annotation. |
| `Contour` | Aktive Kontur (Weg/Beet/Mauer/...) — Mehrfach-Tap + `CloseActiveContour` mit Bowditch-Correction + Foto-Annotation pro Punkt. |
| `TapeMeasure` | Ad-hoc-Distanz (Apple-Measure-Klon, Plan-Kap. 5.3). Eigener Buffer `_tapeMeasurePoints`, kein Projekt-Save, kein Undo, kein Foto. Long-Press auf Mass-Button = Reset. Footer zeigt Σ Strecken-Summe. |
| `Stakeout` | Plan-Kap. 5.9: Pfeil + Distanz + Target-Label zum naechsten unerreichten Ziel. Targets via `IArCaptureService.SetStakeoutTargets` durchgereicht. Hysterese-Reached bei ≤10cm (von >30cm kommend). |

### Site-Marker (`IArCaptureService.SetSitePoints`, Plan-Kap. 5.2)

Bestehende Projekt-`SurveyPoints` werden vor Session-Start an die Activity uebergeben.
Sobald Geospatial-Tracking aktiv ist, erzeugt `CreatePendingSiteAnchors` Earth-Anchors
(max 2 pro Frame). Render als dezente graue Kreise — neue Punkte landen im selben
Koordinatensystem. Geoid-Korrektur grob via 48m-Pauschal (DE-Naehrung).

### RTK-Stab Live-Marker (Plan-Kap. 5.8)

Bei verbundenem BLE-Stab + Geospatial-Tracking refresht `UpdateRtkStabAnchor` 1x/s
den Earth-Anchor an der aktuellen Stab-Position. Alter Anchor wird detacht, neuer
erzeugt. Render: pulsierender Marker (1Hz Sinus) + Fix-Quality-Farbe
(Gruen=RTK-Fix, Gelb=Float, Orange=DGPS, Rot=GPS-only). PostInvalidateDelayed(33)
haelt die Pulse-Animation.

### UX-Features (AR-Modus)

| Feature | Beschreibung |
|---------|-------------|
| Bestätigungs-Dialoge | Löschen + Fertig fragen vor destruktiver Aktion (`ConfirmDeleteSelectedPoint`, `ConfirmFinishCapture`) |
| Sound beim Punkt-Setzen | `MediaActionSound.SHUTTER_CLICK` zusätzlich zur Vibration. SharedPreferences-Key `ar.sound.enabled` (Default an). Toggle im Help-Dialog. |
| Pop-Animation neuer Punkte | 250ms Scale-Easing in `ArPointOverlayView.DrawPoints` — junge Punkte (Timestamp <250ms alt) starten 2.2× groß und schrumpfen mit Ease-Out-Quadratic |
| Tooltips auf Toolbar-Buttons | Long-Press zeigt `Button.TooltipText` (API 26+) |
| Coach-Marks beim 1. AR-Start | Show-once Dialog erklärt Crosshair/Workflow/Toolbar. SharedPreferences-Key `ar.coachmarks.shown`. "Später nochmal" lässt den Pref unverändert → nächster Start zeigt erneut |
| Persistente System-Banner | `ArOverlayState.ThermalWarning` + `BatteryWarning` werden als persistente Top-Banner unter dem Tracking-Banner gezeichnet — bleiben sichtbar solange das System-Event andauert (vs. TransientHint-Fade) |
| Live-Footer-Bar | Über der Toolbar mit Punkte/Länge/Fläche in großer Schrift (`ArPointOverlayView.DrawLiveFooter`) — primärer Mess-Wert-Anker neben dem kleineren Stats-Panel oben rechts |
| Readiness-Badge Tap | Badge oben links ist klickbar (`ArPointOverlayView.ReadinessBadgeBounds` publiziert Tap-Target). Öffnet Detail-Dialog mit Checkliste je Condition (Stabilität / Kompass / Planes / GPS / Geospatial / Tracking-Continuity) |
| Recovery-Bestätigungs-Dialog | Statt automatischem Restore + Toast: Dialog "X Punkte aus letzter Sitzung wiederherstellen?" mit Wiederherstellen/Verwerfen-Buttons. Earth-Anchors werden parallel re-attached. |
| Verstärkter Toolbar-BG | Dichteres ARGB(235,18,18,28) statt halb-transparent (war bei sonnigem Garten kaum lesbar) + dünne weiße Trennlinie oben |

**Geplante Erweiterung (postponed):** Einhand-Layout (Toolbar vertikal links/rechts statt unten).
Würde Reorganisation aller Position-Logik in `DrawTrackingBanner`/`DrawSystemWarningBanners`/
`DrawStatsPanel`/`DrawNorthArrow`/`DrawScaleBar`/`DrawLiveFooter`/`DrawReadinessBadge` benötigen.
Pref-Key vorgesehen: `ar.toolbar.position` (Werte `bottom`/`left`/`right`).

### ARCore-Features aktiv

| Feature | Zweck |
|---------|-------|
| `ArAnchorManager` | Drift-Kompensation: Anchor pro gesetztem Punkt, RefreshAnchors pro Frame |
| `ArPoseSampler` (Shared.Services) | Multi-Frame-Averaging (15 Samples / 800 ms), Median + ±3σ-Outlier-Filter |
| `ArStabilityMonitor` (in `ArAnchorManager.cs`) | EMA über Gyro + Accel, StabilityScore 0..1, Block bei <0,6 |
| `ArPrecisionHelpers` | Depth-Sanity, Depth-Fallback fuer Instant-Placement, Ground-Plane, ARCore-Heading-Extraktion, Semantic-Label-Read, Sky-Check. Delegiert Math-Helfer an `ArMathHelpers` (Shared) |
| `ArMathHelpers` (Shared.Services) | Bowditch-Correction + Quaternion→Heading/Pitch — pure Mathematik, in Unit-Tests direkt fahrbar |
| Geospatial API (VPS) | `earth.CameraGeospatialPose` → Heading ±5° statt ±15–30° (Metall-immun) |
| Earth-Anchors | Persistent über Session-Ende via VPS re-lokalisierbar — Recovery-Restore queued Punkte fuer Re-Attach sobald Earth-Tracking aktiv ist |
| Raw Depth + Confidence | Pixel mit Confidence > 0,3 (Random-Noise-Filter) |
| Scene Semantics | `SemanticMode.Enabled` aktiv ausgelesen — Sky+Instant-Placement-Kombi wird abgelehnt, sonst Label in `ArPoint.SemanticLabel` |
| Light-Estimation | `LightEstimate.PixelIntensity` ausgelesen — Helligkeits-Sprung >40% bricht laufendes Sampling ab (2s Cooldown) |
| RTK-AR-Fusion | `IBleService`-Snapshot via `App.Services` — RTK-Position als GPS-Anker (±2cm) statt Android-LocationManager (±5m). `ArGpsSource`-Enum trackt die Quelle bis in `ArTransferService` (kein 50cm-Min, kein 100x-Faktor fuer RTK) |
| Session Recovery | State in SharedPreferences nach jedem Punkt, max 30 Min. alt |
| Recording API | MP4 in `ExternalFilesDir/Recordings/`, `SetAutoStopOnPause(true)` |

**Bewusst NICHT aktiviert:** Augmented Images (Marker-Druck nötig — vorgesehen fuer ArUco-Roadmap-Feature), Cloud Anchors (kostenpflichtig — Earth-Anchor-Cache ist die Default-Variante), Shared Camera/Camera2 (Vapolia-Binding unvollständig).

### Bowditch-Korrektur (klassische Vermessung)

Bei Kontur-Close: Schlussfehler-Vektor proportional zur Distanz auf alle Zwischenpunkte verteilen.
Nur aktiv bei 1 cm–2 m Schlussfehler (kleiner: unnötig, größer: Fehler-Detection).

### Foto-Annotation pro Punkt (Plan-Kap. 5.6)

Bei jedem AR-Punkt (Point + Contour, NICHT TapeMeasure) macht `CapturePhotoForPoint`
via `PixelCopy.Request` einen JPEG-Snapshot des reinen Kamera-Frames (ohne Overlay)
und legt ihn in `IAppPaths.PhotosFolder` ab. Dateiname `pt_<timestamp>_<guid>.jpg`,
JPEG-Quality 80 (~200KB pro Foto). `ArPoint.PhotoPath` wird sofort gesetzt, der
Disk-Write laeuft asynchron — PDF-Bericht muss `File.Exists` pruefen. Pfad wandert
durch `ArTransferService` in `SurveyPoint.PhotoPath`.

### Confidence-Formel

```
confidence =
    Hit-Quality     (0.1 Instant / 0.2 Point / 0.3 Plane)
  + StdDev          (0.3 wenn σ=0, linear auf 0 bei σ=5cm)
  + Stability       (0.2 × StabilityScore)
  + Anchor-Bonus    (+0.2 wenn Anchor erstellt)
→ max 1.0
```

---

## SkiaSharp-Renderer

| Renderer | Besonderheit |
|----------|-------------|
| `TerrainRenderer` | Painter's Algorithm auf Kamera-Z (nicht Screen-Y), gecachte screenX/Y/Z-Arrays, Lichtvektor rotiert (nicht Normale), Höhen-Legende als `LinearGradient`-Shader statt 400 DrawLines |
| `GardenPlanRenderer` | Min/Max in 1-Pass (nicht 6× LINQ), gecachter Preview-Path + SKPoint-Array, `element.LocalPoints` direkt (kein PointsJson-Re-Parse pro Frame) |
| `SurveyLiveRenderer` | Nordpfeil-Path gecacht, Shader-Caching für Fix-Glow, SKFont-API |
| `StakeoutRenderer` | Pfeil-Farbe distanz-codiert (grün <10 cm / gelb <1 m / orange <5 m / rot >5 m), Länge wächst bis 80 % Radius |
| `ProjectThumbnailRenderer` | Statisch mit gecachten Paints, SKFont-API |

---

## Farbpalette

| Token | Hex | Bedeutung |
|-------|-----|-----------|
| Primary | #FF6B00 | Orange — Messpunkte, AR-Punkte |
| Secondary | #2196F3 | Blau — Linien |
| Accent | #4CAF50 | Grün — RTK Fix |
| AR Contour | #00BCD4 | Cyan — Kontur-Linien |
| AR Active | #FFEB3B | Gelb — Aktive Kontur, gestrichelt |
| AR Selected | #00BCD4 | Cyan — Ausgewählter Punkt, Glow |
| Background | #1A1A2E | Dunkelblau |
| Surface | #16213E | |

---

## Conventions

### Naming

- MockBleService + MockArCaptureService → Desktop-Entwicklung ohne Hardware
- Debug-Panel in SurveyView nur sichtbar bei `IsMockMode=true`
- `IsMockMode` schaltet Edge-Cases frei: `CycleFixDegradation`, `SimulatePacketLoss`, `SimulateBatteryDrain`, `SimulateMagLoss`, `SimulateSpuriousDisconnect`

### Thread-Safety

- Alle BLE-Events via `Dispatcher.UIThread.Post` marshallen
- `_dataLock` in ArCaptureActivity für alle Zugriffe auf `_points`, `_contours`, `_activeContour`
- Undo/Redo-Actions halten Lock-Reference + setzen Lock bei Mutation
- `_frameLock` für `_lastFrame` (GL-Thread schreibt, UI-Thread liest)
- `RunOnUiThread` für alle Overlay-State-Updates

### Android

- `OperatingSystem.IsAndroidVersionAtLeast(31)` statt `Build.VERSION.SdkInt` (Static-Analyzer)
- `SupportedOSPlatformVersion=26` im csproj
- `RunAOTCompilation=false` + `AndroidEnableProguard=false` (Mapsui/NTS brauchen Reflection)
- ArCaptureActivity Theme: `@style/MyTheme.Fullscreen` (AppCompat, NICHT `android:Theme.Black`)

---

## Aktive Gotchas

| Problem | Fix |
|---------|-----|
| `LocalApplicationData` crasht auf Android im DI | `IAppPaths`-Pattern überall — auch in `SettingsViewModel` + `ProjectsViewModel` |
| ARCore `Frame.Dispose()` → Use-after-Dispose | KEIN Dispose auf `_lastFrame` — ARCore verwaltet Lifecycle |
| `ByteBuffer`-Leak in ArBackgroundRenderer | Gecachter `ByteBuffer` statt pro-Frame `AllocateDirect` |
| BLE MTU-Default 23 zu klein | `RequestMtu(247)` in `OnConnected` VOR `DiscoverServices` |
| BLE parallele Writes → Korruption | `SemaphoreSlim` + `OnCharacteristicWrite`-Acknowledgment |
| ARCore `+Z = hinten` naiv falsch | `east = arX*cosH - arZ*sinH`, `nord = -arX*sinH - arZ*cosH` |
| Bowyer-Watson bei kollinearen Punkten | 1 mm Dedup + CCW-Winding + Epsilon `1e-12` |
| Shoelace-Fläche auf ungeordneten Punkten | Convex-Hull (Andrew's Monotone Chain) vorher |
| Konturlinie exakt auf Vertex | Höhe um `1e-9` perturbieren + Dedup intersections |
| MeasurementService 111320 m/Grad | `ICoordinateService.ToLocalMetric` (UTM) — spart 8 cm/100 m |
| GardenElement-Konturen driften | PointsJson v2 (WGS84 absolut) + `LocalPoints` transient neu projiziert |
| RTK-Höhe ~48 m Offset zu NN | `IGeoidService` (EGM96) in `ParsePointData` + `ArTransferService` |
| Stab-Neigung sabotiert Präzision | App-seitige Tilt-Korrektur (vertikal immer, horizontal bei MagAccuracy ≥ 2) |
| O(N²)-Triangulation bei Projekt-Load | `MeasurementService.ReplacePoints` + `PointsReset` (1 Event statt N) |
| Mapsui MapControl crasht beim Start | Lazy-Init via Code-Behind (nicht XAML) — erst bei Karten-Tab-Aktivierung |
| PdfSharpCore crasht auf Android | Lazy XFont-Properties + `AndroidFontResolver` (/system/fonts/) |
| CSV-Labels mit `;` / Newline | RFC 4180 Quote-Escape in `ExportService.EscapeCsv` |
| GardenPlanService.CalculatePolygonArea in Lat/Lon | Plausibilitäts-Check: wenn |x| < 180 && |y| < 90 → Warning + 0 |
| FileProvider fehlt für Share-Intents | `<provider>` im Manifest + `Resources/xml/provider_paths.xml` |
| Blender Y/Z-Swap → falsche Normalen | Kein Swap — UTM-Koords sind bereits Blender-Standard (Z-up) |
| Fan-Triangulation kaputt bei konkaven Polygonen | Ear-Clipping in `BlenderExportService` |
| SurveyView-Handler akkumulieren | Handler-Dedup: `-=` vor `+=` in `DataContextChanged` |
| NTRIP-Mountpoint mit `:` | `CanSendNtripConfig`-Validation: kein `:` im Mountpoint |
| WS2812B auf GPIO 48 | GPIO 48 oft reserviert auf ESP32-S3-DevKits → GPIO 38 |
| AP2112K vs AMS1117 | AMS1117 hat 1,1 V Dropout → stirbt bei halbem Akku. Nur AP2112K! |

---

## Build

```bash
dotnet run --project src/Apps/SmartMeasure/SmartMeasure.Desktop
dotnet build src/Apps/SmartMeasure/SmartMeasure.Android
```

---

## Roadmap (offene Plan-Items)

Sprint 3/4-Features die noch nicht implementiert sind — eigene Iterationen wert:

| Kap. | Feature | Aufwand | Umsetzungs-Tipp |
|------|---------|---------|-----------------|
| 5.4 | Volumen-/Aushub-Messung | 5 PT | Truncated Prism aus geschl. Kontur + Hoehen-Anker; Mesh-Voxel-Variante optional |
| 5.5 | Scene-Reconstruction (PLY/OBJ) | 7 PT | Raw-Depth akkumulieren + Voxel-Filter + Poisson; RAM-Risiko via Octree |
| 5.7 | ArUco-/Augmented-Image-Marker | 4 PT | ARCore-AugmentedImages-API aktivieren, Marker als Image-Database, RTK-Stab als Einmess-Quelle |
| 5.17 | Total-Station-Modus | 5 PT | Stativ-Kalibrierung + RTK als Origin + Depth-API als Tachymeter-Ersatz |
| 5.14 | PDF-Bericht Vermessungs-Standard | 5 PT | PdfSharpCore + AndroidFontResolver; nutzt `SurveyPoint.PhotoPath` + Differential-Snapshot-Service |
| 5.12 | Sprach-Annotation pro Punkt | 3 PT | Android `SpeechRecognizer` + Audio-Save im PhotosFolder |
| 5.15 | Quality-Heatmap im Live-AR | 4 PT | 50x50px-Patches: FeaturePoint-Count + Depth-Confidence + Plane-Overlap → Gradient-Overlay |
| 5.18 | Least-Squares-Netzausgleich | 6 PT | Eigene Impl in `MeineApps.CalcLib` (Math.NET zu gross), Kovarianzmatrix + Constraints |
| 5.11 | Multi-User Co-located Capture | 6 PT | WiFi-Direct + lokaler SignalR-Hub, Earth-Anchor-Re-Localisation pro Geraet |
| 5.16 | GNSS-/Wetter-Konditions-Indikator | 2 PT | NOAA-Ionosphaere + NREL-Solar via HTTP, lokaler Cache |
| Test-Strat. | `IArSession`-Interface fuer Mocking | 3 PT | Wrappt `Google.AR.Core.Session` — erlaubt deterministische End-to-End-Tests |

## Verweise

- Hardware-Detail: `~/.claude/projects/F--Meine-Apps-Ava/memory/smartmeasure.md`
- Globale Conventions (DI, MVVM, DateTime, Thread-Safety): `F:\Meine_Apps_Ava\CLAUDE.md`
- Globale Gotchas (SkiaSharp, Avalonia, Android): `F:\Meine_Apps_Ava\CLAUDE.md` Troubleshooting
