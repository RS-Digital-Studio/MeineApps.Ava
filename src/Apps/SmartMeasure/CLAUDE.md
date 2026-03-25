# SmartMeasure - 3D-Grundstücksvermessung + Gartenplanung

Privates Projekt. DIY RTK-GPS Vermessungsstab (±2cm) + eigene Basisstation + Avalonia-App.
Man geht mit dem Stab über ein Grundstück, drückt an jedem Punkt den Knopf → Position + Höhe.
Daraus entsteht ein 3D-Geländemodell für Gartenplanung (Wege, Beete, Mauern, Terrassen).

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
- Firmware: PlatformIO/Arduino (kommt in `firmware/` Unterordner)
- Kosten: ~560-740 EUR gesamt

## Architektur

```
[Handy-Hotspot (kein Internet nötig)]
  ├── WiFi → Basisstation (NTRIP-Server :2101)
  └── WiFi → Rover-Stab (NTRIP-Client)
                └── BLE → SmartMeasure App
```

## Services

| Service | Aufgabe |
|---------|---------|
| IBleService | BLE-Kommunikation zum Stab (plattform-spezifisch) |
| MockBleService | Simuliert RTK-Daten für Desktop-Entwicklung (IDisposable) |
| IMeasurementService | Punkt-Verwaltung, Abstände, Flächen (Haversine, Shoelace) |
| ICoordinateService | WGS84 ↔ UTM Konvertierung (Transverse-Mercator) |
| ITerrainService | Delaunay-Triangulierung (Bowyer-Watson), Konturlinien, Interpolation, Volumen |
| IGardenPlanService | Gartenelemente CRUD, Flächenberechnung, Materialliste |
| IProjectService | SQLite Persistenz (Projekte, Punkte, Elemente) |
| IExportService | CSV + GeoJSON Export |

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

## Noch zu implementieren

- RESX Lokalisierung (6 Sprachen)
- SurveyLiveRenderer (SkiaSharp Kompass + Genauigkeits-Ring)
- ProjectThumbnailRenderer (Mini-Vorschau für Projekt-Liste)
- Zeichenmodus in GardenPlanView (Touch → Punkte setzen → Element erstellen)
- PDF Export

## Farbpalette

- Primary: #FF6B00 (Orange - Messpunkte)
- Secondary: #2196F3 (Blau - Linien)
- Accent: #4CAF50 (Grün - RTK Fix)
- Background: #1A1A2E (Dunkelblau)
- Surface: #16213E
