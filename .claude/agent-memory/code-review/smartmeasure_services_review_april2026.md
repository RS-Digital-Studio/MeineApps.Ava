---
name: SmartMeasure Services Tiefgründiges Review April 2026
description: 9 Services gereviewt (MeasurementService, GardenPlanService, ExportService, BlenderExportService, MockArCaptureService, AndroidArCaptureService, ArCaptureActivity, ArBackgroundRenderer, ArPointOverlayView) - 23 Findings (9 krit, 8 hoch, 6 mittel)
type: project
---

# SmartMeasure Services Review - 17.04.2026

## Kontext
Tiefgründiges Review der bisher nicht reviewten Services: Punkt-Verwaltung, Flächenberechnung, CSV/GeoJSON/PDF/OBJ Export, ARCore-Bridge.

## KRITISCHE Findings (9)

1. **CSV zerstört Daten bei Semikolon/Newline in Label** - ExportService.cs:102-113 - Kein Escape/Quoting
2. **BlenderExportService Extrudierte Polyline - Faces doppelt pro Segment** - BlenderExportService.cs:288-337 - Vertices nicht geteilt, Risse/Überlappungen am Knick
3. **BlenderExportService Extrudierte Polyline Vertex-Orientierung** - BlenderExportService.cs:319-335 - Alle 6 Richtungs-Faces willkürlich orientiert, Backface-Culling+Shading kaputt
4. **GardenPlanService.CalculatePolygonArea keine Einheiten-Garantie** - GardenPlanService.cs:136-148 - Wenn Lat/Lon in PointsJson → Grad² Fläche
5. **MeasurementService.CalculateArea ohne Convex-Hull** - MeasurementService.cs:48-64 - Shoelace auf Mess-Reihenfolge, falsche Fläche bei ungeordneten Punkten (TerrainService macht es richtig)
6. **ArCaptureActivity _activeContour Race** - ArCaptureActivity.cs:278,316,1085 - SetMode() ohne Lock, NRE möglich
7. **ArCaptureActivity Undo/Redo ohne Lock** - ArCaptureActivity.cs:1276-1298 - Collection-Modification während GL-Thread iteriert
8. **AndroidArCaptureService TCS stillschweigend null** - AndroidArCaptureService.cs:51 - Vorherige Session wird mit null beendet, User-Daten gehen verloren
9. **BlenderExportService Terrain-Normalen Winding nach Y/Z-Swap invertiert** - BlenderExportService.cs:184-186 - Y→Z-Negation kippt CCW→CW, Shading kaputt

## HOHE Findings (8)

1. MeasurementService ToLocalMetric mit 111320m/Grad statt WGS84-präzise (8cm/100m Fehler bei RTK-Anspruch)
2. PDF Material-Tabelle ohne Header-Wiederholung bei Seitenumbruch
3. GardenPlanService doppelte Flächenberechnung (gecacht vs frisch) → Inkonsistenz
4. ArCaptureActivity _sensorManager Listener-Leak bei schnellem Schließen (PostDelayed auf destroyed Activity)
5. ArCaptureActivity _lastFrame Race (Frame wird ungültig beim nächsten Session.Update, HitTest kann zwischen Lock und Nutzung invalidiert werden)
6. BlenderExportService Fan-Triangulation nur für konvexe Polygone (L-förmige Beete kaputt)
7. ExportService PDF-Export Task.Run mit nicht-threadsafer PdfSharp-API + lazy XFont-Init
8. MockArCaptureService new Random(42) nicht thread-safe (bereits in CLAUDE.md als Regel)

## MITTLERE Findings (6)

1. GeoJSON-Polygon nicht RFC-7946-konform (CCW für äußere Ringe fehlt)
2. GardenPlanService.ParsePoints schluckt Exceptions stumm (kein Log)
3. ExportService.SanitizeFileName unvollständig (trailing dots, reserved names, length)
4. MeasurementService.CurrentPoints public mutable (keine Kapselung)
5. ArCaptureActivity zwei Lock-Objekte mit impliziter Reihenfolge → fragil
6. ArCaptureActivity.SnapToPlaneEdge ignoriert Y-Achse → falsch auf vertikalen Planes

## Positiv
- IAppPaths-Pattern konsequent (Android-Sandbox-Crash vermieden)
- ARCore Frame.Dispose() vermieden (CLAUDE.md Gotcha)
- ByteBuffer-Caching in ArBackgroundRenderer (Native-Memory-Leak vermieden)
- CultureInfo.InvariantCulture konsequent
- _dataLock existiert (nur lückenhaft angewandt)
- Android Font-Resolver lazy
