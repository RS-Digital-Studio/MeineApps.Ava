# SmartMeasure - 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt. Zwei Erfassungsmodi:
1. **RTK-GPS Stab** (±2cm) - DIY Vermessungsstab + eigene Basisstation + BLE
2. **AR-Kamera** (±5-50cm) - Live ARCore auf Samsung Galaxy S25 Ultra

Man geht durch den Garten, setzt Punkte und zeichnet Konturen.
Daraus entsteht ein 3D-Geländemodell für Gartenplanung (Wege, Beete, Mauern, Terrassen).
Export nach Blender (OBJ+MTL), GeoJSON, DXF (AutoCAD/Allplan/Revit), KMZ (Google Earth), CSV, PDF.
Absteckungs-Modus: geplante Punkte im Feld wiederfinden (Live-Kompass + Restmeter).

## Projekte

| Projekt | Zweck | Target |
|---------|-------|--------|
| SmartMeasure.Shared | ViewModels, Views, Services, Models | net10.0 |
| SmartMeasure.Desktop | Desktop Entry + DesktopBleService | net10.0 |
| SmartMeasure.Android | Android Entry + AndroidBleService + FG-Service + ARCore | net10.0-android (MinSdk 26) |

## Build

```bash
dotnet run --project src/Apps/SmartMeasure/SmartMeasure.Desktop
dotnet build src/Apps/SmartMeasure/SmartMeasure.Android
```

## Hardware (nicht Teil der Solution)

- **Rover-Stab**: ESP32-S3 + ZED-F9P + BNO085 + AP2112K LDO + BLE
- **Basisstation**: ESP32-S3 + ZED-F9P + NTRIP-Server + WiFi
- **AR-Kamera**: Samsung Galaxy S25 Ultra (200MP, Snapdragon 8 Elite, ARCore)
- Firmware: PlatformIO/Arduino (kommt in `firmware/` Unterordner)
- Kosten Stab+Basis: ~560-740 EUR gesamt

## Architektur

```
Modus 1: RTK-Stab (±2cm, Präzision)
[Handy-Hotspot (kein Internet nötig)]
  ├── WiFi → Basisstation (NTRIP-Server :2101)
  └── WiFi → Rover-Stab (NTRIP-Client)
                └── BLE → SmartMeasure App

Modus 2: AR-Kamera (±5-50cm, Schnell-Scan)
[Samsung S25 Ultra]
  ├── ARCore → 6DoF Pose Tracking + Plane Detection + Depth
  ├── GPS → Grobe Georeferenzierung (±3-5m)
  ├── Barometer → Relative Höhe (±10cm)
  └── Magnetometer → Nordausrichtung
```

## Services

| Service | Aufgabe |
|---------|---------|
| IAppPaths | Plattform-abstrahierte App-Pfade (Android: Context.FilesDir, Desktop: ApplicationData) |
| IBleService | BLE-Kommunikation zum Stab (plattform-spezifisch) |
| MockBleService | Simuliert RTK-Daten + Edge-Cases (FixDegradation, PacketLoss, BatteryDrain, MagLoss, Disconnect) — für Desktop-Entwicklung |
| IArCaptureService | AR-Kamera-Erfassung (Android: ARCore, Desktop: Mock) |
| MockArCaptureService | Simuliert AR-Capture für Desktop (12x8m Grundstück) |
| IArTransferService | AR-Punkte → SurveyPoints (GPS-Fusion, Heading-Rotation, Geoid-Korrektur) |
| IMeasurementService | Punkt-Verwaltung, Abstände, Flächen (Haversine, Shoelace auf Convex-Hull) |
| ICoordinateService | WGS84 ↔ UTM Konvertierung (Transverse-Mercator). ToLocalMetric nutzt feste UTM-Zone des Schwerpunkts (konsistent über Zonengrenzen) |
| IGeoidService | EGM96 Ellipsoid → Geoid-Höhe (NN). Hardcoded 2°-Grid für Deutschland, ±0.5-1m Genauigkeit. Client-Korrektur togglebar falls Firmware bereits MSL sendet |
| ITerrainService | Delaunay (Bowyer-Watson mit CCW-Winding + Dedup), Konturlinien, Interpolation, Volumen, Convex Hull für Flächen |
| IGardenPlanService | Gartenelemente CRUD, Flächenberechnung, Materialliste. PointsJson v2-Format (absolute WGS84) mit v1-Legacy-Fallback |
| IProjectService | SQLite Persistenz (Projekte, Punkte, Elemente) — DeleteProject atomar in Transaktion |
| IExportService | CSV + GeoJSON + DXF + KMZ + PDF Export |
| IBlenderExportService | OBJ + MTL Export für Blender (Terrain + Gartenelemente) |

## ViewModels

| ViewModel | Tab | Features |
|-----------|-----|----------|
| MainViewModel | - | Navigation (8 Tabs), Status-Bar, Back-Button, MessageRequested + ForegroundServiceRequested, Export-Banner (Teilen/Öffnen) |
| ConnectViewModel | BLE | BLE Scan, NTRIP-Config + Validation + Persistenz, WiFi-Config, Stablänge |
| SurveyViewModel | Messen | Live-Position, Punkt setzen, Labels, Disconnect-UX, IsMockMode (Debug-Panel mit FixDegradation/PacketLoss/Battery/Mag/Disconnect), MagWarning |
| TerrainViewModel | 3D | 3D-Geländemodell, Rotation/Zoom/Pan, Überhöhung, Konturlinien |
| GardenPlanViewModel | Garten | 2D-Draufsicht, Zeichenwerkzeuge, Materialliste, Undo. PointsJson v2 (WGS84 absolut) — LocalPoints transient berechnet bei Schwerpunkt-Änderung |
| MapViewModel | Karte | OpenStreetMap (Mapsui), Punkte, Polygon, Fläche/Umfang |
| ProjectsViewModel | Projekte | Projekt-CRUD, Duplizieren, CSV/GeoJSON/DXF/KMZ/Blender/PDF Export |
| StakeoutViewModel | Abstecken | Ziele aus Projekt (Messpunkte + Gartenelement-Knoten), Live-Pfeil + Distanz + Bearing + Höhen-Delta, Haptic bei <10cm |
| SettingsViewModel | Optionen | Einheiten, Stablänge, Fix-Quality |

## SkiaSharp Graphics

| Renderer | Beschreibung |
|----------|-------------|
| TerrainRenderer | 3D-Geländemodell: gecachte Arrays (screenX/Y/Z), Painter's Algorithm auf Kamera-Z, vorberechnete Face-Normalen aus Mesh, rotierte Lichtrichtung, Nordpfeil-Path gecacht, Höhen-Legende als LinearGradient-Shader (statt 400 DrawLines) |
| GardenPlanRenderer | 2D-Draufsicht: Min/Max in 1-Pass (statt 6x LINQ), gecachte Preview-Path + SKPoint-Array, nutzt `element.LocalPoints` statt PointsJson zu parsen, SKFont-API |
| SurveyLiveRenderer | Live-Kompass mit Genauigkeits-Ring: Nordpfeil-Path gecacht, Shader-Caching für Fix-Glow, SKFont-API |
| StakeoutRenderer | Kompass-Pfeil zum Absteckziel: distanz-farbcodiert (grün <10cm, gelb <1m, orange <5m, rot >5m), Pfeil-Länge wächst mit Distanz bis max. 80% Radius, Rotation relativ zum Bewegungs-Heading |
| ProjectThumbnailRenderer | Mini-Vorschau für Projekt-Liste: statisch mit gecachten Paints, SKFont-API |

## IAppPaths-Pattern (Android-Sandbox-Fix)

Verhindert den Android-Startup-Crash bei `Environment.SpecialFolder.LocalApplicationData`. Analog zum BingXBot-Pattern:

```csharp
// SmartMeasure.Shared/Services/IAppPaths.cs
public interface IAppPaths
{
    string AppDataFolder { get; }
    string DatabasePath { get; }
    string ExportFolder { get; }
}

// SmartMeasure.Shared/Services/AppPaths.cs (Desktop-Default)
// SmartMeasure.Android/Services/AndroidAppPaths.cs (Context.FilesDir)

// App.axaml.cs:
public static Func<IAppPaths>? AppPathsFactory { get; set; }

// MainActivity.CustomizeAppBuilder (VOR DI-Build):
App.AppPathsFactory = () => new AndroidAppPaths(this);
```

## Samsung Galaxy S25 Ultra Full-Feature-Ausschöpfung (18.04.2026)

Alle realistisch nutzbaren ARCore-Features sind aktiviert. Erwartung auf S25 Ultra:
**±0.5-3 cm** lokale Mess-Präzision, **±1-3 m** absolute GPS-Position (via VPS).

### ARCore Geospatial API (VPS) — größter Präzisions-Gewinn
- `Config.GeospatialMode.Enabled` aktiviert (wenn supported)
- `earth.CameraGeospatialPose` pro Frame → globale Lat/Lon/Alt + Heading
- **Heading: ±5°** statt ±15-30° bei rohem Magnetometer (Metallumgebung-immun)
- **GPS-Position: ±1-3m** statt ±3-5m
- Voraussetzung: Google Cloud ARCore-API aktivieren, API-Key in AndroidManifest:
  ```xml
  <meta-data android:name="com.google.android.ar.API_KEY" android:value="DEIN_KEY" />
  ```
- Ohne Key: stumme Fallback auf Magnetometer+GPS
- Pro ArPoint: `GeoLatitude/Longitude/Altitude/HorizontalAccuracy` persistiert
- ArCaptureResult: `GeospatialActive`, `GeospatialHorizontalAccuracy`, `GeospatialHeadingAccuracy`

### Earth-Anchors (persistente, global-referenzierte Ankerpunkte)
- `ArAnchorManager.TryCreateEarthAnchor(earth, lat, lon, alt, point)`
- Wenn Geospatial aktiv: bevorzugt über lokalen Session-Anchor
- Anchors halten über Session-Ende hinweg (via VPS re-lokalisierbar)
- ARCore matcht Kamera-Bild kontinuierlich gegen Street View-3D-Daten

### Raw Depth + Confidence-Image
- `AcquireRawDepthImage16Bits()` + `AcquireRawDepthConfidenceImage()`
- Nur Pixel mit Confidence > 0.3 verwendet (Random-Noise-Filter)
- Fallback auf smoothed DepthImage wenn Raw nicht verfügbar
- Präzisere Depth-Sanity-Check im Multi-Frame-Sampling

### OpenGL ES 3.0
- `SetEGLContextClientVersion(3)` statt 2
- Shader-Caching + bessere Performance auf Snapdragon 8 Elite
- Android fällt automatisch auf 2.0 zurück wenn nicht supported

### Scene Semantic Segmentation
- `Config.SemanticMode.Enabled` aktiviert (wenn supported)
- Pro-Pixel-Kategorien verfügbar (Sky/Terrain/Building/Water)
- Infrastruktur steht bereit für späteres Filtern (z.B. Sky-Pixel ablehnen)

### Recording API
- `Session.StartRecording(RecordingConfig)` via REC-Toolbar-Button
- MP4 in `ExternalFilesDir/Recordings/SmartMeasure_yyyyMMdd_HHmmss.mp4`
- Camera-Feed + Sensor-Metadata → Session reproducible später im Playback-Mode
- `SetAutoStopOnPause(true)` — bei Activity-Pause stoppt Recording automatisch

### Thermal Management
- `PowerManager.CurrentThermalStatus` alle 60 Frames geprüft
- Bei Severe+ (Status ≥ 3): Multi-Sample-Count auf 5 reduziert, Warn-Hint
- Bei Moderate (Status = 2): 10 Samples
- Normal: 15 Samples (High-End-Gerät)
- User wird über Hitze informiert

### Battery Management
- `BatteryManager.GetIntProperty(Capacity)` geprüft
- Bei <15% Akku: Warnung einmalig angezeigt

### Android 15 HapticFeedbackConstants
- `View.PerformHapticFeedback(FeedbackConstants.Confirm/Reject)` (API 30+)
- Samsung tunt diese Constants für ihre Haptic-Engine
- Fallback auf VibrationEffect bei älteren Android-Versionen

### BLE 5.3 MTU 247
- `_gatt.RequestMtu(247)` — voll ausgenutzter BLE-5.3-DataLengthExtension
- Weniger Fragmentierung, höherer Durchsatz bei Point-Paketen (48 Bytes passen jetzt
  bequem in 1 Notification)

### Samsung-getunte Haptic-Effekte (aus Phase 1)
- `VibrationEffect.CreatePredefined(EffectTick/Click/DoubleClick)` auf API 29+
- OEM-Tuning durch Samsung für Linear-Actuator-Motor → "premium feel"

### EnvironmentalHdr Light Estimation (High-End only)
- `LightEstimationMode.EnvironmentalHdr` wenn RAM ≥ 8GB (S25 Ultra hat 12GB)
- Fallback auf `AmbientIntensity` auf schwächeren Geräten

### Multi-Sample-Count dynamisch
- High-End (IsHighEndDevice): **15 Samples** in 800ms
- Normal: 10 Samples
- Thermal Severe: 5 Samples
- StdDev sinkt mit √N

### Punch-Hole Safe Area
- `OnApplyWindowInsets` liest Status-Bar + Cutout-Höhe
- Alle Top-UI (Nord-Pfeil, Stats-Panel, Ready-Badge, Tracking-Banner) respektieren Inset
- Vermeidet Kollision mit zentraler S25-Kamera

### Focus Mode Auto
- `FocusMode.Auto` — nutzt Laser-AF des S25 Ultra

### Features, die BEWUSST nicht aktiviert sind
- **Augmented Images**: Benutzer müsste physische Marker drucken — Overkill für Garten-Use
- **Shared Camera (Camera2)**: Komplex, Vapolia-Binding unvollständig. Screen-Screenshots reichen
- **Cloud Anchors**: Kostenpflichtig über Free-Tier hinaus, Earth-Anchors bereits persistent
- **Multi-Camera Config**: ARCore wählt intern die beste Kamera

---

## Samsung Galaxy S25 Ultra Spezial-Optimierungen (18.04.2026)

Das Ziel-Gerät ist das Samsung Galaxy S25 Ultra (Snapdragon 8 Elite, 12GB RAM, Android 15).
Folgende Gerät-spezifische Features sind aktiviert:

### Punch-Hole Safe-Area
- **Problem**: S25 Ultra hat zentrale Front-Kamera oben → Nord-Pfeil wäre genau darin
- **Fix**: `OnApplyWindowInsets` liest Status-Bar + Cutout-Höhe, `ArOverlayState.TopInsetPixels` propagiert
- Alle Top-UI-Elemente (Nord-Pfeil, Ready-Badge, Tracking-Banner, Stats-Panel) respektieren `_state.TopInsetPixels`
- Fallback auf 40dp wenn Insets nicht verfügbar (ältere Geräte ohne Cutout)

### Samsung-getunte Haptic-Effekte
- Ab Android 10 (API 29) nutzt `VibrationEffect.CreatePredefined`:
  - `EffectTick` für Punkt-Set (leicht)
  - `EffectClick` für Aktion-Bestätigung (mittel)
  - `EffectDoubleClick` für Warning (zwei-Tap)
- Samsung tunt diese Effects für ihr Linear-Actuator-Motor → fühlt sich "premium" an
- Fallback auf `CreateOneShot` mit manueller Amplitude bei älteren Android-Versionen

### EnvironmentalHdr Light Estimation
- `IsHighEndDevice()` check via RAM ≥ 8GB (S25 Ultra hat 12GB)
- Auf High-End: `LightEstimationMode.EnvironmentalHdr` (vollständige Environment-Map)
- Auf schwächeren Geräten: Fallback `AmbientIntensity` (niedrigere CPU-Last)
- Snapdragon 8 Elite NPU verarbeitet HDR-Estimation effizient

### Erhöhter Multi-Sample-Count auf High-End
- Normal: 10 Samples in 800ms (= 12.5 Hz)
- High-End: **15 Samples** in 800ms (= 18.75 Hz)
- Bessere Median-Qualität, niedrigerer StdDev
- Elite-Chip liefert stabile 60 fps ARCore-Updates

### Allgemeine Android 15 / One UI 7 Kompatibilität
- `MinSdk 26`, `SupportedOSPlatformVersion 26`
- `OperatingSystem.IsAndroidVersionAtLeast(31)` statt deprecated `Build.VERSION.SdkInt`
- BLUETOOTH_SCAN + BLUETOOTH_CONNECT Runtime-Permissions
- `ForegroundService.TypeConnectedDevice` (API 30+)

### Was NICHT genutzt wird (bewusste Entscheidung)
- **ARCore Geospatial API**: benötigt Google Cloud API-Key, für privaten Garten-Use-Case Overkill
- **Cloud Anchors**: persistent zwischen Sessions — für eine Mess-Session unnötig
- **Scene Semantic Segmentation**: nur sehr neue Devices, wir leben ohne
- **Camera2 Shared Mode für Screenshots**: Screen-Canvas reicht für Vermessungs-Doku

### S25 Ultra Präzisions-Erwartung
Mit allen Optimierungen (Anchors + Multi-Frame + Bowditch + ARCore-Heading + Depth-Sanity + S25-Specials):
- Einzelpunkt flache Fläche: **±0.5-1.5 cm** (Elite Depth + 15 Samples)
- Geschlossene Kontur (Bowditch): **±1-3 cm** über alle Punkte
- Lange Session (Anchors): drift-frei
- Absolute Position (GPS): ±3-5 m (GPS-Limitation auf Consumer-Hardware)

---

## AR-Präzisions-Upgrade Phase 2 (18.04.2026)

Zweite Präzisions-Welle mit 7 weiteren Features. Erwartung: von ±5-15cm auf **±2-8cm**.

### Depth API aktiv ausgelesen
- `frame.AcquireDepthImage16Bits()` pro Multi-Frame-Sample-Finalize
- Vergleich Hit-Distance vs Depth-Wert am Target-Pixel
- Multiplikator: 1.2× (<5cm Abweichung) bis 0.5× (>30% rel. Differenz)
- Auf Samsung S25 Ultra: mm-genaue Depth-Verifikation. No-op auf Devices ohne Depth-Support

### Sensor Fusion Heading (ARCore statt rohes Magnetometer)
- ARCore liefert Sensor-fusioniertes Camera-Pose (Gyro+Accel+Mag kombiniert)
- `ArPrecisionHelpers.ExtractHeadingFromCameraPose` berechnet Heading aus Kamera-Z-Achse
- Über 5s gesammelt, circular median
- **Stabiler als rohes Magnetometer** in Metallumgebung (Zaun, Auto etc.)
- Fallback auf Magnetometer wenn ARCore-Pose instabil

### Ground-Plane als Höhen-Referenz
- Größte horizontale getrackte Plane als Boden identifiziert (Normalvektor Y > 0.9)
- `_groundPlaneY` wird alle ~1s aktualisiert
- In `ArCaptureResult.GroundPlaneY` weitergegeben
- Alle Höhen-Werte können relativ zum Boden interpretiert werden (absolute Garten-Höhen)

### Bowditch-Correction (klassische Vermessung)
- Bei Kontur-Close: Schlussfehler-Vektor (letzter ≠ erster Punkt)
- Wird **proportional zur zurückgelegten Distanz** auf alle Zwischenpunkte verteilt
- Standard in der Vermessungs-Technik seit 200+ Jahren
- Nur bei 1cm–2m Fehlern aktiv (kleiner: unnötig, größer: Fehler-Detection)

### Pre-Mess-Validation + Ready-Badge
- Vor jedem Punkt-Set geprüft:
  - Tracking OK ✓
  - StabilityScore ≥ 0.6 ✓
  - MagAccuracy ≥ 2 ✓
  - Min. 1 Plane erkannt ✓
- Wenn alles OK: grünes "✓ BEREIT" Badge oben links mit Quality-Score
- Wenn fehlt: gelb/rot mit Check-List "Kamera wackelt · Kompass unkalibriert"
- Punkt-Set wird bei Fail höflich abgelehnt + Vibration-Warning

### Kompass-Kalibrierungs-Dialog
- Automatisch bei MagAccuracy < 2
- Einmalig pro Session, dann nicht mehr nervig
- Anleitung: "Gerät langsam in liegender Acht bewegen"

### Tracking-Quality-Score (0-100%)
- Aus mehreren Faktoren zusammengesetzt:
  - Basis 50 (Tracking aktiv)
  - +3 pro Plane (max 15)
  - +10 × StabilityScore
  - +5 × MagAccuracy (max 10)
  - +AnchorCount (max 10)
  - −500 × StdDev (Penalty für ungenaue Punkte)
- Im Ready-Badge sichtbar
- Wird mit `TrackingContinuityRatio` (Frames tracking/total) im Result persistiert

### Präzisions-Gewinn Phase 1+2
| Aspekt | Vorher | Phase 1 | Phase 2 |
|--------|--------|---------|---------|
| Einzel-Punkt flache Fläche | ±3-5cm | ±0.5-2cm | ±0.5-2cm |
| Lange Session (Drift) | ±30cm | drift-frei | drift-frei |
| Magnetometer-Ausreisser | 30° schief | 5° via Median | **2° via ARCore-Fusion** |
| Geschlossene Kontur Rundungsfehler | akkumuliert | akkumuliert | **Bowditch-verteilt** |
| Schlechte Conditions | User setzt trotzdem Punkt | teilweise blockiert | **komplett blockiert** |
| Depth-Sanity | nicht geprüft | nicht geprüft | **Multiplier aktiv** |
| Höhen-Referenz | erster Punkt | erster Punkt | **Ground-Plane absolut** |

### Neue Dateien
- `ArPrecisionHelpers.cs` — Depth-Read, Ground-Plane-Detection, ARCore-Heading-Extraktion, Bowditch, Quality-Score

### Erweiterte Models
- `ArCaptureResult.GroundPlaneY` (Boden-Y-Referenz)
- `ArCaptureResult.TrackingQualityScore` (0-100)
- `ArCaptureResult.TrackingContinuityRatio` (Tracking-Frames / Total-Frames)

---

## AR-Präzisions-Upgrade (18.04.2026)

Der AR-Modus wurde um mehrere Präzisions-kritische Features erweitert. Erwartete
Verbesserung bei Garten-Vermessung: **von ~50cm auf ~5-15cm**.

### Anchors für Drift-Kompensation
- Jeder gesetzte Punkt erhält einen ARCore-Anchor (`session.CreateAnchor(pose)`)
- Pro Frame liest `ArAnchorManager.RefreshAnchors` die aktualisierte Anchor-Pose und
  schreibt sie zurück in `ArPoint.X/Y/Z` — ARCore kompensiert die Session-Drift automatisch
- Soft-Limit 150 Anchors/Session (für Garten mit <50 Punkten mehr als genug)
- Datei: `ArAnchorManager.cs`

### Multi-Frame Pose-Averaging
- Beim Tap: nicht single frame, sondern bis zu **10 Samples über 800ms**
- Robuster Median + Outlier-Filter (±3σ) → typisch σ=1-3cm statt Einzel-Frame-Wackler
- Während Sampling: gelber Progress-Ring um das Reticle, Transient-Hint "📐 Messung läuft..."
- `ArPoint.PositionStdDev` + `SampleCount` + `HitQuality` persistiert für Quality-Audit
- Datei: `ArPoseSampler` in `ArAnchorManager.cs`

### Stabilitäts-Monitor (Gyroscope + Accelerometer)
- `ArStabilityMonitor`: EMA über angular velocity + linear acceleration
- `StabilityScore` 0..1 (1=still, 0=stark bewegt)
- Vor Punkt-Set: bei <0.6 → Toast "Bitte Kamera still halten" + Abbruch + Warning-Vibration
- Visuell als Balken links vom Reticle (grün/gelb/rot)

### GPS-Multi-Sample-Averaging (5s aktiv)
- Vorher: einmaliger `GetLastKnownLocation` (kann minuten-alt sein, ±10m off)
- Jetzt: `RequestLocationUpdates` über 5 Sekunden, bis zu 10 Samples
- Gewichtetes Mittel nach Accuracy (präzisere Samples zählen stärker)
- Finale `_gpsLatitude/Longitude/Altitude` deutlich genauer als Snapshot

### Heading-Multi-Sample-Averaging
- `HeadingSensorListener` sammelt 20 Samples über 2s statt direktes Overwrite
- **Circular Median** via sin/cos-Summe (kein arithmetisches Mittel — 359° und 1° wären sonst falsch gemittelt)
- Magnetometer-Ausreisser werden geglättet → alle WGS84-Koordinaten rotieren korrekt

### Quality-Indikatoren im Overlay
- Punkt-Darstellung richtet sich nach `Confidence`:
  - Radius: 60% bei 0% Confidence → 100% bei 100%
  - Alpha: 50% → 100%
- `HitQuality` als Text über dem Punkt: `~` für Point, `?` für Instant
- `σ=XcmR` Label unter dem Punkt wenn Standardabweichung > 5mm
- Stats-Panel: Anchor-Count (gelb bei >100, nahe Limit)

### Session-Start-Delay (5s) für präzise Referenzen
- `CollectInitialSensorSamples` finalisiert Heading-Mittel nach 5s Session-Zeit
- GPS wird automatisch nach 5s finalisiert (PostDelayed-Listener)
- Vor Session-Start: User sieht "Kalibrierung läuft..."-Hinweis

### Confidence-Berechnung
```
confidence =
    Hit-Quality-Komponente (0.1 Instant / 0.2 Point / 0.3 Plane) +
    StdDev-Komponente (0.3 wenn σ=0, linear auf 0 bei σ=5cm) +
    Stability-Komponente (0.2 × StabilityScore) +
    Anchor-Bonus (+0.2 wenn Anchor erstellt)
→ Max 1.0
```

### Neue Dateien
- `ArAnchorManager.cs` — 3 Klassen: `ArAnchorManager`, `ArPoseSampler`, `ArStabilityMonitor`

### Neue ArPoint-Felder
- `PositionStdDev` (Messgenauigkeit in Metern)
- `SampleCount` (Anzahl aggregierter Frames)
- `HitQuality` (3=Plane, 2=Point, 1=Instant, 0=keiner)

### Präzisions-Gewinn-Übersicht
| Feature | Vorher | Nachher | Gewinn |
|---------|--------|---------|--------|
| Drift (lange Session) | nicht kompensiert, ~30cm/5min | Anchor-korrigiert | 10-30cm |
| Hand-Wackler | ±3-5cm pro Tap | ±0.5-2cm nach Averaging | 1-3cm |
| Magnetometer-Ausreisser | 1 Sample | 20 Samples, Circular Median | Vermeidung 30°-Rotationsfehler |
| Stabilitäts-Filter | keiner | Threshold 0.6 | User setzt nur noch bei Stillstand |
| GPS-Schnappschuss | LastKnown (alt!) | 10 Samples über 5s, gewichtet | 2-5m |

---

## AR-Kamera Komplett-Feature-Set (18.04.2026)

Der AR-Modus wurde maximal ausgebaut. Alle sinnvollen ARCore-Features sind aktiv:

### Live-Interaktion
- **Reticle/Crosshair** in Bildmitte mit Live-Hit-Test pro Frame
- **Farbcodiert**: Grün=Plane-Hit, Orange=Feature-Point, Gelb=Instant Placement, Weiß=kein Hit
- **Distanz-Label** am Reticle (Abstand zur Kamera) + **Höhen-Δ** (relativ zum ersten Punkt)
- **Auto-Close-Detection**: Bei Kontur-Modus mit >=3 Punkten in Reticle-Nähe zum ersten Punkt → visueller "Schließen"-Hint

### Tracking-Qualität & Feedback
- **Tracking-State-Banner** bei Verlust: "Nicht genug Licht" / "Mehr Texturen/Kanten nötig" / "Langsamer bewegen" / "Kamera nicht verfügbar" / "Session-Fehler"
- **Haptic Feedback**: Light (30ms) bei Punkt-Set + Toolbar-Taps, Medium (60ms) bei Kontur-Schließen/Löschen, Warning-Pattern (80-40-80ms) bei Tracking-Verlust
- **Transient-Hints** (1.5s Einblendung): "Punkt N", "↶ Rückgängig", "📸 Screenshot gespeichert", "⚠ Keine Fläche"

### Instant Placement Fallback
- ARCore `InstantPlacementMode.LocalYUp` aktiviert
- Wenn Plane-HitTest nichts liefert → `Frame.HitTestInstantPlacement(x, y, 1.5f)` als Fallback
- Confidence dynamisch: 0.9 (Plane), 0.7 (Point), 0.5 (Instant)

### Depth & Light Estimation
- `DepthMode.Automatic` (wenn supported) für präzisere HitTests
- `LightEstimationMode.AmbientIntensity` aktiviert
- `FocusMode.Auto` — Kamera stellt auf Messziel scharf

### Live-Stats-Panel (oben rechts)
- Session-Zeit (m:ss), Punkt-Count, Fläche (m²) oder erkannte Planes, Kontur-Länge (m), Höhen-Range (ΔH in m)
- Wird pro Frame live aktualisiert

### Live-Messungen im Overlay
- **Distanz-Label** zwischen ALLEN aufeinanderfolgenden Punkten (vorher nur letzte 2)
- **Höhen-Δ** pro Punkt wenn >2cm vom ersten Punkt abweichend (▲/▼ Symbol)
- **Aktive Kontur**: halbtransparente Gelb-Füllung + gestrichelte Umrandung
- **Live-Fläche** bei Polygon mit >=3 Punkten (provisorisch geschlossen)

### UI-Elemente
- **Nord-Pfeil** oben mittig, rotiert mit Kompass-Heading
- **Maßstab-Balken** unten links (1m/2m/5m-Referenz, skaliert zur aktuellen Reticle-Distanz)
- **Punkt-Nummerierung** (1, 2, 3...) neben jedem Punkt
- **Plane-Polygone** halbtransparent grün eingeblendet

### Toolbar-Buttons (+ haptisch gekoppelt)
- ◎ Punkt, ─ Linie, ⭕ Schließen, ↶ Undo, ↷ Redo, ✖ Löschen, 📷 Screenshot, ? Hilfe, ✔ Fertig
- Scrollbar-horizontal auf schmalen Screens

### Session-Recovery
- Nach jedem Punkt-Set wird State in `SharedPreferences` temp gespeichert
- Bei App-Crash + Restart: Session wird automatisch wiederhergestellt (max 30 Min alt)
- Nach erfolgreichem "Fertig" wird Recovery gelöscht

### Screenshot-Export
- 📷-Button: Canvas-Snapshot (GL + Overlay) als PNG in `ExternalFilesDir/Screenshots/`
- Dateiname: `SmartMeasure_yyyyMMdd_HHmmss.png`

### Help-Dialog
- ?-Button öffnet AlertDialog mit Kurz-Anleitung aller Buttons + Tipps (Beleuchtung, Bewegung, Farb-Legende)

### Thread-Safety (alle behoben)
- `_dataLock` für alle Zugriffe auf `_points`, `_contours`, `_activeContour`
- Undo/Redo-Actions halten Lock-Reference und sperren bei jeder Mutation
- `_frameLock` für `_lastFrame` (GL-Thread schreibt, UI-Thread liest)
- `RunOnUiThread` für alle Overlay-State-Updates

## AR-Kamera Architektur (ARCore)

### Separate Activity Pattern (wie BarcodeScannerActivity in FitnessRechner)
- `ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia)
- Wird per `StartActivityForResult` gestartet, Ergebnis kommt via `OnActivityResult`
- `AndroidArCaptureService` nutzt `TaskCompletionSource<ArCaptureResult?>` als Brücke
- Factory-Pattern in `App.axaml.cs`: `ArCaptureServiceFactory`

### ArCaptureActivity Aufbau
```
FrameLayout (3 Schichten)
├── GLSurfaceView          ARCore Kamera-Preview (OpenGL ES 2.0)
│   ├── ArBackgroundRenderer   Vertex+Fragment Shader für Camera-Textur
│   └── IRenderer.OnDrawFrame  Session.Update() → Frame → Projektion
├── ArPointOverlayView     Transparenter Canvas (Punkte, Linien, Auswahl)
└── Native Toolbar          Buttons (Punkt, Linie, Undo, Löschen, Fertig)
```

### ARCore-Koordinatensystem + Rotation (KRITISCH)
- ARCore: **+X = rechts, +Y = oben, +Z = hinten** (vom Gerät weg)
- Bei heading=0 (User blickt nach Norden) zeigt -Z nach Norden, +X nach Osten
- `ArTransferService.RotateAndProject` berücksichtigt das korrekt:
  - `eastOffset = arX * cosH + arZ * sinH`
  - `nordOffset = arX * sinH - arZ * cosH`

### AR → Terrain Transfer (ArTransferService)
1. GPS-Ankerposition als Referenzpunkt
2. MagneticHeading → sin/cos Rotation der AR-Koordinaten nach Norden
3. Rotierte Meter-Offsets → WGS84 (metersPerDegreeLat/Lon)
4. ArPoint → SurveyPoint (FixQuality=10 für AR-erfasst, HorizontalAccuracy ≥50cm)
5. ArContour → GardenElement (PointsJson in UTM-Meter via ICoordinateService)
6. Punkte in IMeasurementService + IProjectService → automatische UI-Updates

### Blender Export (BlenderExportService)
- OBJ + MTL (reiner Text, kein NuGet)
- Y/Z-Swap: Unsere Daten (Y=horizontal) → Blender (Z=up)
- Vertex-Farben nach Höhe (Grün→Gelb→Orange→Braun)
- Gartenelemente als separate Objekte mit Materialien

### NuGet: Vapolia.Google.ARCore 1.47.1
- Community-Binding für Google ARCore SDK
- net9.0-android35 → kompatibel mit net10.0-android

### AndroidManifest
- `<uses-permission android:name="android.permission.CAMERA" />`
- `<uses-permission android:name="android.permission.BLUETOOTH_SCAN" android:usesPermissionFlags="neverForLocation" />`
- `<uses-permission android:name="android.permission.BLUETOOTH_CONNECT" />`
- `<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />`
- `<uses-permission android:name="android.permission.FOREGROUND_SERVICE_CONNECTED_DEVICE" />`
- `<uses-feature android:name="android.hardware.camera.ar" android:required="false" />`
- `<meta-data android:name="com.google.ar.core" android:value="required" />`

### Android-Plattform-Härtung (18.04.2026)

Seit der Refactor-Session haben wir drei kritische Android-Blocker behoben:

**1. AndroidBleService: MTU 128 + Write-Serialisierung + Reconnect**
- `RequestMtu(128)` in `OnConnected` VOR `DiscoverServices` — Default 23 Bytes reicht nicht für 48-Byte Point-Pakete
- Write-Queue über `SemaphoreSlim` + `OnCharacteristicWrite`-Acknowledgment — BLE-Writes sind NICHT parallel erlaubt
- Exponential-Backoff-Reconnect bei Disconnect (1s → 2s → 4s → 10s, max 5 Versuche)
- `BinaryPrimitives.ReadDoubleLittleEndian` statt `BitConverter` (ESP32 = little-endian, explizit)

**2. MainActivity: Runtime-Permissions**
- Android 12+ (API 31): `BLUETOOTH_SCAN`, `BLUETOOTH_CONNECT` als Runtime-Permissions
- `ACCESS_FINE_LOCATION` für Mapsui + AR-Georeferenzierung
- `OperatingSystem.IsAndroidVersionAtLeast(31)` statt `Build.VERSION.SdkInt` (Static-Analyzer-freundlich)

**3. MeasurementForegroundService**
- Neue native `Service`-Klasse in `SmartMeasure.Android/Services/`
- Notification-Channel "smartmeasure_measurement" (Low-Priority, ongoing)
- `ForegroundService.TypeConnectedDevice` (Android 10+)
- `MainViewModel.ForegroundServiceRequested`-Event + `MainActivity` koppelt an BLE-Status

### Android-spezifische Build-Settings (SmartMeasure.Android.csproj)
- `SupportedOSPlatformVersion=26` (Android 8.0+, deckt NotificationChannel, FileProvider, Immutable PendingIntent ab)
- `RunAOTCompilation=false` + `AndroidEnableProguard=false` (Mapsui/NTS brauchen Reflection)
- ArCaptureActivity Theme: `@style/MyTheme.Fullscreen` (AppCompat-basiert, nicht android:Theme.Black)

### PdfSharpCore Android-Fix
- `ExportService.EnsureFontResolver()` registriert `AndroidFontResolver` (nutzt `/system/fonts/Roboto-*.ttf`)
- XFont-Felder sind lazy Properties (kein static-Init-Crash auf Android)

### MapView Lazy-Init
- Mapsui MapControl wird NICHT im XAML erstellt (GL-Crash auf Android beim Start)
- MainView.axaml.cs erstellt MapView per Code-Behind erst wenn Karten-Tab aktiviert wird
- MapViewModel.EnsureInitialized() erstellt Tile-Layer erst bei Bedarf

## Algorithmus-Härtung TerrainService (18.04.2026)

Bowyer-Watson Delaunay + abhängige Algorithmen wurden für RTK-Genauigkeit (±2cm) robuster gemacht:

1. **Punkt-Dedup** (`PointMergeEpsilon = 1mm`): RTK-Streuung kann dicht benachbarte Messwiederholungen erzeugen → Circumcircle-Determinante wird numerisch instabil. Punkte < 1mm Abstand werden gemergt.
2. **CCW-Winding**: Alle neu erzeugten Dreiecke werden auf Counter-Clockwise normalisiert (`NormalizeWinding` + `OrientCcw`). Circumcircle-Test setzt CCW voraus.
3. **Epsilon-Toleranz in `IsInCircumcircle`**: `det > 1e-12` statt `> 0` verhindert Endless-Loops bei quasi-kollinearen Punkten.
4. **Super-Triangle vergrößert**: Faktor 10 statt 2 — robuster gegen enge Point-Sets.
5. **Konturlinien-Perturbation** (`ContourVertexEpsilon = 1e-9`): Wenn Höhe exakt auf Vertex liegt, würde `TryAddEdgeIntersection` doppelte Punkte an beiden anliegenden Kanten finden.
6. **Konturlinien 3-Intersection-Fall**: Bei nahen-Vertex-Trefferern werden die beiden weitest entfernten Intersections genommen (`PickLongestSegment`).
7. **Convex-Hull für `CalculateArea2D`**: Andrew's Monotone Chain — Shoelace-Flächenformel erwartet geordnete Polygon-Punkte. Messpunkte aus `IMeasurementService` sind in Mess-Reihenfolge, nicht Polygon-Rand.
8. **Face-Normalen im Mesh vorberechnet** (`TerrainMesh.NormalsX/Y/Z`): Renderer liest nur, muss keine 24k sqrt/s beim 60fps-Dreh berechnen.

## MVVM-Compliance (18.04.2026 Audit)

- `x:CompileBindings="True"` + `x:DataType` auf ALLEN 8 Views (kein stilles Fallback auf ReflectionBinding)
- Kein `App.Services.GetRequiredService<T>()` in View-Ctor (Android-Crash-Pattern)
- Kein `DataContext = …` im Code-Behind (außer Lazy-MapView wegen Mapsui GL-Crash)
- `IProjectService` in `MainViewModel` per Constructor-Injection (vorher Service-Locator im Lambda)
- Alle BLE-Events via `Dispatcher.UIThread.Post` auf UI-Thread marshalled

## Präzisions-Pipeline (24.04.2026)

Nach dem großen Koordinaten-Refactor läuft die Präzisions-Kette konsistent:

### Koordinaten-Pipeline (CoordinateService)
- `ToLocalMetric` nutzt jetzt UTM-Projektion (statt 111320-Approximation). Auf 100m spart das ~8cm Fehler.
- `ToUtmFixedZone(lat, lon, zone)` — projiziert in erzwungene Zone, konsistent über UTM-Zonengrenzen.
- `LatLonToLocal(lat, lon, alt, refLat, refLon, refAlt)` — einzelner Punkt relativ zur Referenz.
- `LocalToLatLon` — invers, für Persistenz von gezeichneten Gartenelementen.
- Zonen-Abweichungs-Warnung bei >3° Longitude-Distanz vom Schwerpunkt.

### PointsJson Format v2 (GardenElement)
- NEU: `{"v":2,"points":[[lat,lon],...]}` — absolute WGS84.
- LEGACY v1: `[[x,y],...]` — lokale UTM-Meter (wird gelesen mit Drift-Risiko).
- `GardenPlanService.GetLocalPoints(element, refLat, refLon, coord)` — konvertiert v2 → lokale Meter für Rendering.
- `GardenElement.LocalPoints` ist `[SQLite.Ignore]` transient Cache, wird vom VM bei Projekt-Load oder Messpunkt-Änderung neu berechnet.
- ArTransferService persistiert direkt v2 (kein UTM-Zwischenschritt mehr).

### Geoid-Korrektur (Egm96GeoidService)
- `EllipsoidToGeoid(lat, lon, altEllipsoid)` — für DE ~-48m Offset.
- Hardcoded 2°-Grid 46-56°N, 4-16°E (Mitteleuropa) mit bilinearer Interpolation.
- Fallback auf 48m Pauschal außerhalb des Grids (Debug-Warnung).
- `IsClientCorrectionEnabled=false` wenn Firmware bereits heightMSL sendet.
- Angewendet in: `AndroidBleService.ParsePointData` + `ParsePositionData` + `ArTransferService.ConvertToSurveyPoints`.

### Tilt-Korrektur (Stabspitze statt Antenne)
- In `AndroidBleService.ParsePointData`: BLE-Paket liefert Antennen-Position + TiltAngle + TiltAzimuth.
- Vertikale Korrektur (immer): `tipAlt = antAlt - stabHeight * cos(tilt)`.
- Horizontale Korrektur (nur bei MagAccuracy ≥ 2): Offset in Azimuth-Richtung = `stabHeight * sin(tilt)`.
- Bei 1.8m Stab + 5° Neigung: 15.7cm horizontaler Versatz korrigiert.
- `AndroidBleService.StabHeightMeters` wird über `SetStabHeightAsync` gesetzt.

### Stakeout (Absteckung)
- Neuer 8. Tab "Abstecken" mit Kompass-Pfeil + Restmeter.
- Ziele: Messpunkte + Garten-Kontur-Knoten (aus PointsJson v2 parsen).
- Bearing via Initial-Bearing-Formel auf Kugel.
- Heading aus Bewegungsrichtung (nur wenn Δ > 30cm, um GPS-Noise zu filtern).
- Haptic-Feedback bei <10cm (Hysterese bis 20cm für Retrigger).
- `StakeoutRenderer` farbcodiert die Pfeilfarbe nach Distanz.

### Export-Formate
- DXF (R12 ASCII, Layer pro Element-Typ, LWPOLYLINE + POINT + TEXT-Labels)
- KMZ (ZIP + doc.kml, Placemarks + Polygon für Grundstück-Umriss + LineStrings für Kontur-Elemente)
- Share-Banner nach Export: "Teilen" → Intent.ActionSend via FileProvider, "Öffnen" → Intent.ActionView mit MIME-Type.
- FileProvider Authority: `${applicationId}.fileprovider`, `Resources/xml/provider_paths.xml`.

## Bekannte Gotchas

| Problem | Fix |
|---------|-----|
| PdfSharpCore crasht auf Android (FontResolver) | Lazy XFont-Properties + AndroidFontResolver (/system/fonts/) |
| Mapsui MapControl crasht auf Android beim Start | Lazy-Init per Code-Behind (nicht im XAML) |
| ArCaptureActivity Theme.Black crasht | AppCompat-Theme verwenden (MyTheme.Fullscreen) |
| ARCore Frame.Dispose() → Use-after-Dispose | KEIN Dispose auf _lastFrame (ARCore verwaltet Lifecycle) |
| ByteBuffer-Leak in ArBackgroundRenderer | Gecachter ByteBuffer statt pro-Frame AllocateDirect |
| Thread-Safety _points/_contours | _dataLock für alle Schreib-/Lese-Zugriffe (GL+UI Thread) |
| Thread-Safety _activeContour + Undo/Redo | Lock-Reference in allen Action-Klassen + SetMode/CloseActiveContour komplett unter Lock |
| BLE MTU 23 Default zu klein | `RequestMtu(128)` in OnConnected VOR DiscoverServices |
| BLE parallele Writes | SemaphoreSlim + OnCharacteristicWrite-Acknowledgment |
| Android-Crash bei LocalApplicationData | IAppPaths-Pattern (analog BingXBot) — auch in SettingsViewModel + ProjectsViewModel, nicht nur ProjectService |
| Bowyer-Watson bei kollinearen Punkten | 1mm Dedup + CCW-Winding + Epsilon-Toleranz |
| Shoelace-Fläche auf ungeordneten Punkten | Convex-Hull (Andrew's Monotone Chain) vorher — betrifft TerrainService + MeasurementService |
| Konturlinie exakt auf Vertex | Höhe um 1e-9 perturbieren + Dedup intersections |
| ARCore +Z = hinten (nicht vorne!) | `east = arX*cosH - arZ*sinH`, `nord = -arX*sinH - arZ*cosH` (bei heading=90° bricht naive Formel) |
| SurveyPoint bei Disconnect | `ResetLivePositionUi()` setzt UI-Werte auf "—" |
| MeasurementService 111320 m/Grad | Bei ±2cm RTK-Anspruch 8cm Fehler pro 100m → `ICoordinateService.ToLocalMetric` (UTM) |
| Blender Y/Z-Swap invertierte Winding | Kein Swap mehr — unsere Koords sind bereits Blender-Standard (Z-up) |
| Fan-Triangulation kaputt bei konkaven Polygonen (L, Hufeisen) | Ear-Clipping-Triangulation in BlenderExportService |
| CSV-Labels mit Semikolon/Newline zerstören Struktur | RFC 4180 Quote-Escape in ExportService.EscapeCsv |
| GardenPlanService.CalculatePolygonArea mit Lat/Lon statt UTM → Grad² | Plausibilitäts-Check: wenn \|x\|&lt;180 && \|y\|&lt;90 → Warning + 0 |
| ExportReady-Event hatte 0 Subscribers → CSV/GeoJSON Dead-Code | `FileExportReady` + `ExportFailed` Events, ProjectsViewModel schreibt in IAppPaths.ExportFolder |
| TerrainViewModel N² Triangulierung beim Projekt-Load | `IMeasurementService.ReplacePoints` + `PointsReset`-Event statt AddPoint pro Punkt |
| SurveyView CompassInvalidate-Handler akkumulieren | Handler-Dedup via -= vor += in DataContextChanged |
| GardenPlanView Tap stumm verworfen wenn LastScale=0 | InvalidateSurface() forcieren damit Renderer erstmal LastScale berechnet |
| SettingsViewModel.UseMetric etc. nicht persistiert | IPreferencesService DI + partial OnXxxChanged-Setter speichert automatisch |
| AndroidArCaptureService null-Result bei Abbruch | `LastError`-Property + TCS-Lock-Pattern — User bekommt Grund via MessageRequested |
| BLE-Fehler in ConnectViewModel still verschluckt | try/catch um alle Commands + MessageRequested-Event → Toast über MainViewModel |
| `ToLocalMetric` mit 111320-Approximation | Jetzt via UTM (`ToUtmFixedZone` mit Ref-Zone des Schwerpunkts) — konsistent über Zonengrenzen, ±cm-präzise |
| GardenElement-Konturen driften wenn Messpunkte sich ändern | PointsJson v2 speichert absolute WGS84 Lat/Lon. `element.LocalPoints` (transient) wird beim Projekt-Load oder bei Messpunkt-Änderung neu aus Schwerpunkt projiziert |
| RTK-Höhe hat ~48m Offset zu NN in Deutschland | `IGeoidService` (EGM96, 2°-Grid) korrigiert Ellipsoid → NN in `AndroidBleService.ParsePointData` + `ArTransferService` |
| Stab-Neigung sabotiert ±2cm Präzision | App-seitige Tilt-Korrektur (vertikal immer, horizontal nur bei MagAccuracy ≥ 2) — 5° Neigung bei 1.8m Stab = 15.7cm Offset korrigiert |
| NTRIP-Mountpoint mit ':' zerstört ESP32-Protokoll | `CanSendNtripConfig` Validation: Server nicht leer, Port ∈ [1, 65535], kein ':' im Mountpoint |
| NTRIP-Credentials weg nach App-Neustart | `partial void OnNtripXxxChanged` persistiert via `IPreferencesService` (Keys `ntrip.server` etc.) |
| Mock testet nur Happy-Path | Debug-Panel in SurveyView (nur bei `IsMockMode`): FixDegradation/PacketLoss/BatteryDrain/MagLoss/Disconnect |
| Export-Datei nach Toast nicht weiterverwendbar | Export-Banner in MainView bietet Teilen/Öffnen. MIME-Type-basiert (KMZ → Google Earth, DXF → CAD, etc.) |
| FileProvider fehlt für Share-Intents | `<provider>` im Manifest + `Resources/xml/provider_paths.xml` (files-path + external-files-path) |
| Geplanter Punkt im Feld nicht wiederfindbar | Stakeout-Tab: Ziel wählen → Live-Pfeil + Restmeter + Höhen-Delta. Bearing via Initial-Bearing-Formel, Heading aus Bewegungsrichtung (>30cm Δ) |

## Integration-Datenfluss (Referenz)

### RTK-GPS Messung
```
ESP32-Rover → BLE GATT → AndroidBleService.OnCharacteristicChanged
  → ParsePointData (BinaryPrimitives.ReadDoubleLittleEndian)
  → PointReceived-Event (Background-Thread)
  → SurveyViewModel.OnPointReceived (via Dispatcher.UIThread.Post)
  → MeasurementService.AddPoint (UI-Thread)
  → PointAdded-Event
  → TerrainViewModel.RecalculateMesh (inkrementell)
  → MapViewModel.UpdateMap
  → GardenPlanViewModel.UpdateCoordinates
```

### Projekt-Load (Batch)
```
ProjectsView.OpenProject → ProjectsVm.ProjectSelected-Event
  → MainViewModel lädt Projekt aus DB
  → MeasurementService.ReplacePoints (EIN PointsReset-Event, kein N-faches AddPoint!)
  → TerrainViewModel.RecalculateMesh (EINMAL für 50 Punkte, nicht 50-mal)
  → MapViewModel.UpdateMap
  → GardenPlanViewModel.LoadElementsFromProjectAsync
  → Navigate("Survey")
```

### AR-Capture → Terrain
```
SurveyView → StartArCapture → AndroidArCaptureService.CaptureAsync
  → Permission-Check (CAMERA + LOCATION)
  → StartActivityForResult(ArCaptureActivity)
  → ArCaptureActivity: GL-Preview + Plane-Detection + HitTest
  → User setzt Punkte/Konturen (alles unter _dataLock)
  → Finish → ConsumeLastResult
  → AndroidArCaptureService.HandleActivityResult → TCS.SetResult
  → SurveyViewModel.ArCaptureCompleted-Event
  → MainViewModel: ArTransferService.TransferToProjectAsync
    → RotateAndProject (ARCore +Z=hinten korrigiert)
    → ProjectService.AddPointAsync pro Punkt
    → ProjectService.AddGardenElementAsync pro Kontur
  → MessageRequested("AR-Capture", "N Punkte übertragen")
```

### Export (CSV/GeoJSON/OBJ/PDF)
```
ProjectsView.Export-Button → ProjectsVm.ExportXxxAsync
  → try: Project laden, Mesh berechnen (Task.Run für OBJ/PDF)
  → Datei in IAppPaths.ExportFolder schreiben
  → FileExportReady-Event mit Pfad
  → MainViewModel.MessageRequested("Export erstellt", pfad)
  → MainActivity: Toast mit Dateipfad
catch: ExportFailed-Event → MessageRequested("Export fehlgeschlagen", ex.Message)
```

## Models (AR)

| Model | Beschreibung |
|-------|-------------|
| ArPoint | 3D-Punkt aus AR (X/Y/Z in lokalen Metern, Confidence, AnchorId, Label) |
| ArContour | Konturlinie (Liste von ArPoints, Typ, IsClosed, Länge/Fläche) |
| ArCaptureResult | Session-Ergebnis (Punkte, Konturen, GPS-Anker, Heading, Barometer) |

## Android AR-Dateien

```
SmartMeasure.Android/
├── Ar/
│   ├── ArCaptureActivity.cs            Native AppCompatActivity (ARCore + GL + Overlay + Editor)
│   ├── ArBackgroundRenderer.cs         OpenGL ES 2.0 Kamera-Hintergrund-Shader
│   ├── ArPointOverlayView.cs           Transparentes Canvas-Overlay (Punkte, Linien, Auswahl)
│   └── AndroidArCaptureService.cs      TaskCompletionSource-Brücke (Permission, Activity-Start)
└── Services/
    ├── AndroidAppPaths.cs              Context.FilesDir-basierte IAppPaths-Impl
    ├── AndroidBleService.cs            BLE mit MTU128, Write-Queue, Reconnect, little-endian Parse
    └── MeasurementForegroundService.cs Doze-Kill-Schutz, Notification, TypeConnectedDevice
```

## Farbpalette

- Primary: #FF6B00 (Orange - Messpunkte, AR-Punkte)
- Secondary: #2196F3 (Blau - Linien)
- Accent: #4CAF50 (Grün - RTK Fix)
- AR Contour: #00BCD4 (Cyan - Kontur-Linien)
- AR Active: #FFEB3B (Gelb - Aktive Kontur, gestrichelt)
- AR Selected: #00BCD4 (Cyan - Ausgewählter Punkt, Glow)
- Background: #1A1A2E (Dunkelblau)
- Surface: #16213E
