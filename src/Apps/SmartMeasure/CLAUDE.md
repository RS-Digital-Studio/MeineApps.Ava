# SmartMeasure - 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt. Zwei Erfassungsmodi:
1. **RTK-GPS Stab** (±2cm) - DIY Vermessungsstab + eigene Basisstation + BLE
2. **AR-Kamera** (±5-50cm) - Live ARCore auf Samsung Galaxy S25 Ultra

Man geht durch den Garten, setzt Punkte und zeichnet Konturen.
Daraus entsteht ein 3D-Geländemodell für Gartenplanung (Wege, Beete, Mauern, Terrassen).
Export nach Blender (OBJ+MTL) und GeoJSON.

## Projekte

| Projekt | Zweck | Target |
|---------|-------|--------|
| SmartMeasure.Shared | ViewModels, Views, Services, Models | net10.0 |
| SmartMeasure.Desktop | Desktop Entry + DesktopBleService | net10.0 |
| SmartMeasure.Android | Android Entry + AndroidBleService | net10.0-android |

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
Modus 1: RTK-Stab (±2cm, Praezision)
[Handy-Hotspot (kein Internet nötig)]
  ├── WiFi → Basisstation (NTRIP-Server :2101)
  └── WiFi → Rover-Stab (NTRIP-Client)
                └── BLE → SmartMeasure App

Modus 2: AR-Kamera (±5-50cm, Schnell-Scan)
[Samsung S25 Ultra]
  ├── ARCore → 6DoF Pose Tracking + Plane Detection + Depth
  ├── GPS → Grobe Georeferenzierung (±3-5m)
  ├── Barometer → Relative Hoehe (±10cm)
  └── Magnetometer → Nordausrichtung
```

## Services

| Service | Aufgabe |
|---------|---------|
| IBleService | BLE-Kommunikation zum Stab (plattform-spezifisch) |
| MockBleService | Simuliert RTK-Daten für Desktop-Entwicklung (IDisposable) |
| IArCaptureService | AR-Kamera-Erfassung (Android: ARCore, Desktop: Mock) |
| MockArCaptureService | Simuliert AR-Capture für Desktop (12x8m Grundstück) |
| IArTransferService | AR-Punkte → SurveyPoints (GPS-Fusion, Heading-Rotation, WGS84) |
| IMeasurementService | Punkt-Verwaltung, Abstände, Flächen (Haversine, Shoelace) |
| ICoordinateService | WGS84 ↔ UTM Konvertierung (Transverse-Mercator) |
| ITerrainService | Delaunay-Triangulierung (Bowyer-Watson), Konturlinien, Interpolation, Volumen |
| IGardenPlanService | Gartenelemente CRUD, Flächenberechnung, Materialliste |
| IProjectService | SQLite Persistenz (Projekte, Punkte, Elemente) |
| IExportService | CSV + GeoJSON Export |
| IBlenderExportService | OBJ + MTL Export für Blender (Terrain + Gartenelemente) |

## ViewModels

| ViewModel | Tab | Features |
|-----------|-----|----------|
| MainViewModel | - | Navigation (7 Tabs), Status-Bar (BLE, Fix, Akku), Back-Button |
| ConnectViewModel | BLE | BLE Scan, NTRIP-Config, WiFi-Config, Stablänge |
| SurveyViewModel | Messen | Live-Position, Punkt setzen, Labels, Punkte-Liste |
| TerrainViewModel | 3D | 3D-Geländemodell, Rotation/Zoom/Pan, Überhöhung, Konturlinien |
| GardenPlanViewModel | Garten | 2D-Draufsicht, Zeichenwerkzeuge, Materialliste, Undo |
| MapViewModel | Karte | OpenStreetMap (Mapsui), Punkte, Polygon, Fläche/Umfang |
| ProjectsViewModel | Projekte | Projekt-CRUD, Duplizieren, CSV/GeoJSON Export |
| SettingsViewModel | Optionen | Einheiten, Stablänge, Fix-Quality |

## SkiaSharp Graphics

| Renderer | Beschreibung |
|----------|-------------|
| TerrainRenderer | 3D-Geländemodell: Dreiecke, Höhenfarbkodierung, Konturlinien, Painter's Algorithm, Diffuse-Shading, Nordpfeil, Maßstab, Höhenskala |
| GardenPlanRenderer | 2D-Draufsicht: Messpunkte, Gartenelemente (Wege/Beete/Mauern/Terrassen), Grid, Maße |
| SurveyLiveRenderer | Live-Kompass mit Genauigkeits-Ring: Kompass (N/E/S/W + 30°-Schritte), Accuracy-Ring (farbcodiert), Fadenkreuz, Satelliten-Anzeige, Fix-Glow, Neigungsindikator |
| ProjectThumbnailRenderer | Mini-Vorschau für Projekt-Liste: Punkte als Dots, Verbindungslinien, Polygon-Füllung (>=3 Punkte), Auto-Fit, Projekt-Typ Badge. Statisch (kein DI) |

## AR-Kamera Architektur (ARCore)

### Separate Activity Pattern (wie BarcodeScannerActivity in FitnessRechner)
- `ArCaptureActivity` ist eine native `AppCompatActivity` (kein Avalonia)
- Wird per `StartActivityForResult` gestartet, Ergebnis kommt via `OnActivityResult`
- `AndroidArCaptureService` nutzt `TaskCompletionSource<ArCaptureResult?>` als Bruecke
- Factory-Pattern in `App.axaml.cs`: `ArCaptureServiceFactory`

### ArCaptureActivity Aufbau
```
FrameLayout (3 Schichten)
├── GLSurfaceView          ARCore Kamera-Preview (OpenGL ES 2.0)
│   ├── ArBackgroundRenderer   Vertex+Fragment Shader fuer Camera-Textur
│   └── IRenderer.OnDrawFrame  Session.Update() → Frame → Projektion
├── ArPointOverlayView     Transparenter Canvas (Punkte, Linien, Auswahl)
└── Native Toolbar          Buttons (Punkt, Linie, Undo, Loeschen, Fertig)
```

### Touch-Handling
- **Tap** (kein Drag): Neuen Punkt setzen via `Frame.HitTest(x, y)` → Plane/Point
- **Tap auf existierenden Punkt**: Auswahl (Cyan-Highlight)
- **Drag auf ausgewaehltem Punkt**: Verschieben (neuer Hit-Test → neue 3D-Position)
- **Loeschen-Button**: Ausgewaehlten Punkt entfernen
- **Undo/Redo**: Stack-basiert (AddPointAction, DeletePointAction, AddContourPointAction)

### Welt-zu-Screen Projektion
- Pro Frame: `ViewMatrix × ProjectionMatrix → MVP`
- Alle ArPoints → homogene Clip-Koordinaten → NDC → Screen-Pixel
- Overlay zeichnet Punkte an projizierten Positionen (bewegen sich mit Kamerabild)

### AR → Terrain Transfer (ArTransferService)
1. GPS-Ankerposition als Referenzpunkt
2. MagneticHeading → sin/cos Rotation der AR-Koordinaten nach Norden
3. Rotierte Meter-Offsets → WGS84 (metersPerDegreeLat/Lon)
4. ArPoint → SurveyPoint (FixQuality=10 fuer AR-erfasst, HorizontalAccuracy ≥50cm)
5. ArContour → GardenElement (PointsJson in UTM-Meter via ICoordinateService)
6. Punkte in IMeasurementService + IProjectService eingespeist → automatische UI-Updates

### Blender Export (BlenderExportService)
- OBJ + MTL (reiner Text, kein NuGet)
- Y/Z-Swap: Unsere Daten (Y=horizontal) → Blender (Z=up)
- Vertex-Farben nach Hoehe (Gruen→Gelb→Orange→Braun)
- Gartenelemente als separate Objekte mit Materialien
- Flaechen: Fan-Triangulierung; Linien: Box-Extrusion mit Width/Height

### NuGet: Vapolia.Google.ARCore 1.47.1
- Community-Binding fuer Google ARCore SDK
- net9.0-android35 → kompatibel mit net10.0-android
- Klassen: Session, Frame, HitResult, Plane, Anchor, Pose, Config, Coordinates2d, ArCoreApk

### AndroidManifest
- `<uses-permission android:name="android.permission.CAMERA" />`
- `<uses-feature android:name="android.hardware.camera.ar" android:required="false" />`
- `<meta-data android:name="com.google.ar.core" android:value="required" />`

### Android-spezifische Build-Settings (SmartMeasure.Android.csproj)
- `RunAOTCompilation=false` + `AndroidEnableProguard=false` (Mapsui/NTS brauchen Reflection)
- ArCaptureActivity Theme: `@style/MyTheme.Fullscreen` (AppCompat-basiert, nicht android:Theme.Black)

### PdfSharpCore Android-Fix
- `ExportService.EnsureFontResolver()` registriert `AndroidFontResolver` (nutzt `/system/fonts/Roboto-*.ttf`)
- XFont-Felder sind lazy Properties (kein static-Init-Crash auf Android)

### MapView Lazy-Init
- Mapsui MapControl wird NICHT im XAML erstellt (GL-Crash auf Android beim Start)
- MainView.axaml.cs erstellt MapView per Code-Behind erst wenn Karten-Tab aktiviert wird
- MapViewModel.EnsureInitialized() erstellt Tile-Layer erst bei Bedarf

## Noch zu implementieren

- BLE: AndroidBleService mit echter Hardware testen (ESP32-Firmware muss BLE-Protokoll definieren)

## Bekannte Gotchas

| Problem | Fix |
|---------|-----|
| PdfSharpCore crasht auf Android (FontResolver) | Lazy XFont-Properties + AndroidFontResolver (/system/fonts/) |
| Mapsui MapControl crasht auf Android beim Start | Lazy-Init per Code-Behind (nicht im XAML) |
| ArCaptureActivity Theme.Black crasht | AppCompat-Theme verwenden (MyTheme.Fullscreen) |
| ARCore Frame.Dispose() → Use-after-Dispose | KEIN Dispose auf _lastFrame (ARCore verwaltet Lifecycle) |
| ByteBuffer-Leak in ArBackgroundRenderer | Gecachter ByteBuffer statt pro-Frame AllocateDirect |
| Thread-Safety _points/_contours | _dataLock fuer alle Schreib-/Lese-Zugriffe (GL+UI Thread) |

## Models (AR)

| Model | Beschreibung |
|-------|-------------|
| ArPoint | 3D-Punkt aus AR (X/Y/Z in lokalen Metern, Confidence, AnchorId, Label) |
| ArContour | Konturlinie (Liste von ArPoints, Typ, IsClosed, Laenge/Flaeche) |
| ArCaptureResult | Session-Ergebnis (Punkte, Konturen, GPS-Anker, Heading, Barometer) |

## Android AR-Dateien

```
SmartMeasure.Android/Ar/
├── ArCaptureActivity.cs         Native AppCompatActivity (ARCore + GL + Overlay + Editor)
├── ArBackgroundRenderer.cs      OpenGL ES 2.0 Kamera-Hintergrund-Shader
├── ArPointOverlayView.cs        Transparentes Canvas-Overlay (Punkte, Linien, Auswahl)
└── AndroidArCaptureService.cs   TaskCompletionSource-Bruecke (Permission, Activity-Start)
```

## Farbpalette

- Primary: #FF6B00 (Orange - Messpunkte, AR-Punkte)
- Secondary: #2196F3 (Blau - Linien)
- Accent: #4CAF50 (Grün - RTK Fix)
- AR Contour: #00BCD4 (Cyan - Kontur-Linien)
- AR Active: #FFEB3B (Gelb - Aktive Kontur, gestrichelt)
- AR Selected: #00BCD4 (Cyan - Ausgewaehlter Punkt, Glow)
- Background: #1A1A2E (Dunkelblau)
- Surface: #16213E
