# Services — Geo-Algorithmen, Persistenz, BLE, AR, Export

Alle Services sind Singleton (DI). Interfaces in diesem Ordner, plattform-spezifische
Implementierungen in `SmartMeasure.Android/Services/` oder `SmartMeasure.Android/Ar/`.
Generische Service-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Service-Übersicht

| Interface | Impl | Zweck |
|-----------|------|-------|
| `IAppPaths` | `AppPaths` (Desktop), `AndroidAppPaths` | Sandbox-sichere Pfade |
| `IHardwareModeService` | `HardwareModeService` | Adaptiver Betriebsmodus AR-First vs RTK (`ShowRtkUi`). Persistiert Erst-Verbindung, `Changed`-Event vom BLE-Thread. Details → [App-CLAUDE.md](../../CLAUDE.md) "Adaptiver Betriebsmodus" |
| `IBleService` | `MockBleService` (Desktop), `AndroidBleService` | BLE-Kommunikation zum Rover-Stab |
| `IArCaptureService` | `MockArCaptureService` (Desktop), `AndroidArCaptureService` | AR-Kamera-Erfassung |
| `IArTransferService` | `ArTransferService` | AR-Punkte → SurveyPoints (GPS-Fusion, Heading-Rotation, Geoid) |
| `IMeasurementService` | `MeasurementService` | Punkt-Verwaltung, Abstände, Flächen. `ReplacePoints` + `PointsReset`-Event |
| `ICoordinateService` | `CoordinateService` | WGS84 ↔ UTM (Transverse-Mercator). `ToUtmFixedZone` für Zonen-Konsistenz |
| `IGeoidService` | `Egm96GeoidService` | EGM96 Ellipsoid→NN-Höhe. Hardcoded 2°-Grid DE (46–56°N, 4–16°E) |
| `ITerrainService` | `TerrainService` | Bowyer-Watson Delaunay, Konturlinien, Volumen, Convex Hull |
| `IGardenPlanService` | `GardenPlanService` | Gartenelemente CRUD, PointsJson v2, v1-Legacy-Fallback |
| `IProjectService` | `ProjectService` | SQLite (Projekte, Punkte, Elemente). `DeleteProject` atomar |
| `IExportService` | `ExportService` | CSV, GeoJSON, DXF, KMZ, PDF |
| `IBlenderExportService` | `BlenderExportService` | OBJ + MTL (kein Y/Z-Swap — UTM ist schon Z-up) |
| `IDifferentialSnapshotService` | `DifferentialSnapshotService` | Greedy-Nearest-Neighbor Snapshot-Vergleich (Moved/Added/Removed/Unchanged) |
| `IGnssConditionService` | `GnssConditionService` | NOAA Kp + F10.7 via HTTP (Cache 1h). Good/Fair/Poor-Klassifikation |
| `IVolumeService` | `VolumeService` | Prism/Layered/Frustum-Volumen + Material-Schätzung (8 Materialien) |
| `ITotalStationService` | `TotalStationService` | Stationierung + Radial-Projektion (Distanz+Bearing+Pitch → Lat/Lon) |
| `ILeastSquaresAdjustmentService` | `LeastSquaresAdjustmentService` | Position-based-Dynamics-Netzausgleich, gewichtet 1/σ² |
| `IVoiceAnnotationService` | `NullVoiceAnnotationService` (Desktop), `AndroidVoiceAnnotationService` | SpeechRecognizer-Transkript |
| `ISurveyReportService` | `SurveyReportService` | PdfSharpCore: Cover, Punkt-Tabelle + Foto-Thumbnails, Materialien, optional Differential |
| `ISceneReconstructionService` | `SceneReconstructionService` | Voxel-Filter + PLY/OBJ-Punktwolke-Export |
| `IMultiUserSessionService` | `LocalTcpMultiUserService` | TCP-NDJSON-Broadcast Port 5119, kein extra SignalR-Package |
| `IArSessionLike` | `AndroidArSession` (Frame-Provider), `MockArSession` (Tests) | Testbarkeits-Wrapper — Pattern-Stub, noch nicht produktiv verkabelt |
| `ArSnapEngine` | Stateless | Snap-Hilfen: Vertex (15 cm), Right-Angle (5°), Parallel (3°), Extension (10 cm) |
| `ArPoseSampler` | Stateless | Multi-Frame HitTest-Averaging: Median + ±3σ-Outlier-Filter, 15 Samples/800 ms |
| `ArMathHelpers` | Static | Bowditch-Correction, Quaternion→Heading/Pitch — reine Mathematik, in Unit-Tests direkt testbar |

---

## IAppPaths-Pattern

`Environment.SpecialFolder.LocalApplicationData` crasht auf Android im DI-Kontext.
**Immer** `IAppPaths` per Constructor-Injection, NIEMALS `Environment.GetFolderPath` direkt.

```csharp
// Android: Context.FilesDir → sandbox-sicherer Pfad
// Desktop: Environment.SpecialFolder.LocalApplicationData
```

Betroffen: `ProjectService`, `ExportService`, `SettingsViewModel`, `SurveyReportService`.

---

## IMeasurementService — Batch vs. Einzel

| Methode | Event | Wann |
|---------|-------|------|
| `AddPoint(p)` | `PointAdded` | Einzelner RTK-Punkt (Live-Messung) |
| `ReplacePoints(list)` | `PointsReset` | Projekt-Load oder ClearPoints (feuert EIN Event für N Punkte) |

`PointsReset` verhindert O(N²)-Triangulation: TerrainViewModel hört auf `PointsReset` und ruft
`RecalculateMesh()` einmal auf statt N-mal.

---

## Präzisions-Algorithmen

### CoordinateService

- `ToLocalMetric` nutzt UTM (nicht 111320-Approximation) — spart 8 cm auf 100 m.
- `ToUtmFixedZone(lat, lon, zone)` projiziert in feste Zone — konsistent über Zonengrenzen.
- Warnung bei >3° Longitude-Distanz vom Projektschwerpunkt.

### Egm96GeoidService

- `EllipsoidToGeoid(lat, lon, altEllipsoid)` → NN-Höhe (Geoid-Undulation ~−48 m in DE).
- Hardcoded 2°-Grid 46–56°N, 4–16°E, bilineare Interpolation.
- Außerhalb: 48 m Pauschal-Fallback + Debug-Warnung.
- `IsClientCorrectionEnabled = false` wenn Firmware bereits MSL sendet.

### TerrainService (Bowyer-Watson Delaunay)

| Härtung | Grund |
|---------|-------|
| Punkt-Dedup bei 1 mm | RTK-Streuung → numerisch instabile Circumcircle-Determinante |
| CCW-Winding (`NormalizeWinding`) | Circumcircle-Test setzt CCW voraus |
| Epsilon `1e-12` in `IsInCircumcircle` | Endless-Loop bei quasi-kollinearen Punkten |
| Super-Triangle Faktor 10 | Robustheit bei engen Point-Sets |
| Konturlinien-Perturbation `1e-9` | Vertex-Hit → Doppel-Intersection |
| `PickLongestSegment` | 3-Intersection-Fall bei nahen Vertices |
| Convex-Hull (Andrew's Monotone Chain) | `CalculateArea2D` braucht geordnete Polygon-Punkte |
| Face-Normalen vorberechnet | Spart 24 k sqrt/s beim 60-fps-Dreh |

### ArMathHelpers / ARCore Koordinatensystem

ARCore: +X = rechts, +Y = oben, **+Z = hinten** (vom Gerät weg). Bei heading=0 zeigt -Z nach Norden.
Korrekte Rotation (KRITISCH — naive Formel bricht bei heading ≠ 0°):

```csharp
eastOffset = arX * cosH - arZ * sinH
nordOffset = -arX * sinH - arZ * cosH
```

### ExportService

- CSV: RFC 4180 Quote-Escape (`EscapeCsv` bei `;` + Newline in Feldern).
- PointsJson v2 Format: `{"v":2,"points":[[lat,lon],...]}` (WGS84 absolut, nicht UTM-Meter).
- Blender OBJ/MTL: KEIN Y/Z-Swap — UTM-Koordinaten sind bereits Blender-Standard Z-up.

### GardenPlanService

`GardenElement.LocalPoints` ist `[SQLite.Ignore]` und transient. Bei Projekt-Load oder
Schwerpunkt-Änderung aus `GetLocalPoints()` neu projiziert. Renderer und Export nutzen
`LocalPoints` direkt — kein PointsJson-Re-Parse pro Frame.
`GardenPlanService.CalculatePolygonArea`: Plausibilitäts-Check — wenn |x| < 180 && |y| < 90
sind das Lat/Lon-Grad statt Meter → Warning + 0 zurückgeben.

---

## Gotchas

| Problem | Fix |
|---------|-----|
| `LocalApplicationData` crasht auf Android | `IAppPaths`-Pattern, NIEMALS direkt aufrufen |
| O(N²)-Triangulation bei Projekt-Load | `MeasurementService.ReplacePoints` statt N× `AddPoint` |
| GardenElement-Konturen driften bei Schwerpunkt-Änderung | PointsJson v2 (WGS84) + `LocalPoints` transient neu projiziert |
| RTK-Höhe ~48 m Offset zu NN | `IGeoidService.EllipsoidToGeoid` in `ParsePointData` + `ArTransferService` |
| Stab-Neigung sabotiert Präzision | App-seitige Tilt-Korrektur: vertikal immer, horizontal nur bei `MagAccuracy ≥ 2` |
| Shoelace-Fläche auf ungeordneten Punkten | Convex-Hull (Andrew's Monotone Chain) vorher |
| CSV-Labels mit `;` / Newline | RFC 4180 Quote-Escape in `ExportService.EscapeCsv` |
| Blender Y/Z-Swap → falsche Normalen | KEIN Swap — UTM-Koords sind bereits Blender-Standard (Z-up) |
| Fan-Triangulation kaputt bei konkaven Polygonen | Ear-Clipping in `BlenderExportService` |
| NTRIP-Mountpoint mit `:` | `CanSendNtripConfig`-Validation: kein `:` im Mountpoint |
| PdfSharpCore crasht auf Android | Lazy XFont-Properties + `AndroidFontResolver` (/system/fonts/) |
| FileProvider fehlt für Share-Intents | `<provider>` im Manifest + `Resources/xml/provider_paths.xml` |
