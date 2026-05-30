# Models — Datenmodelle

SQLite-Entities, immutable Structs und AR-Typen. Alle Entities mit `[PrimaryKey, AutoIncrement]`
für SQLite. AR-Modelle sind Plain-C#-Klassen (kein SQLite).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `SurveyProject.cs` | SQLite-Entity: Projekt (Name, Typ, Fläche, Umfang, PointCount). `Points` + `GardenElements` sind `[Ignore]` — werden separat geladen |
| `SurveyPoint.cs` | SQLite-Entity: WGS84-Position (±2 cm RTK), Genauigkeit, Tilt, Fix-Quality, Foto-Pfad, Voice-Transkript |
| `GardenElement.cs` | SQLite-Entity: Polygon/Linie mit `PointsJson` (v2: WGS84, v1: legacy UTM). `LocalPoints` ist `[Ignore]` + transient |
| `TerrainMesh.cs` | Immutable Delaunay-Gitter: Vertex-Arrays X/Y/Z, Triangle-Index-Array, vorberechnete Normalen, Bounding Box |
| `ContourLine.cs` | Segment-Liste einer Isohypse (Höhe + Segment-Paare) |
| `StakeoutTarget.cs` | Ein Absteck-Ziel (Lat/Lon/Label, `IsReached`-Flag) |
| `StickState.cs` | BLE-Snapshot: Fix-Quality, Accuracy, Tilt, Azimuth, Battery, MagAccuracy |
| `NtripConfig.cs` | NTRIP-Konfiguration: Host, Port, Mountpoint, Credentials |
| `MaterialEstimate.cs` | Berechnete Materialliste (Menge, Einheit, Materialtyp) |
| `ArPoint.cs` | AR-Messpunkt: ARCore-Koordinaten + Confidence + SemanticLabel + PhotoPath |
| `ArContour.cs` | Geschlossene AR-Kontur aus mehreren `ArPoint`s |
| `ArCaptureResult.cs` | Übergabe-Objekt von `ArCaptureActivity` → `IArCaptureService.CaptureAsync()`. Enthält alle Punkte + Konturen + `TotalPointCount`. |
| `ArReferenceMarker.cs` | ArUco-Marker-Referenz (erkannte ID + Pose) |

---

## SurveyPoint — Feld-Details

- `Altitude` = NN-Höhe in Metern (bereits Geoid-korrigiert + Tilt-korrigiert). NICHT Ellipsoid-Höhe.
- `TiltAngle` / `TiltAzimuth` = Stab-Neigung beim Messen (Grad vom Lot / true north). Wichtig für nachträgliche Korrektheit-Überprüfung.
- `MagAccuracy` (BNO085, 0–3): ≥ 2 = Horizontal-Tilt-Korrektur aktiv; < 2 = nur Vertikal-Korrektur.
- `PhotoPath` + `VoiceTranscript` / `VoiceAudioPath` = optional, können null sein. PDF-Bericht prüft `File.Exists`.

---

## GardenElement — PointsJson-Formate

```json
// v2 (aktuell, WGS84 absolut — robust gegen Schwerpunkt-Drift)
{"v":2,"points":[[lat,lon],...]}

// v1 (legacy, lokale UTM-Meter relativ zum damaligen Schwerpunkt — Drift-Risiko)
[[x,y],...]
```

`LocalPoints` (`[SQLite.Ignore]`) ist die transiente Meter-Projektion relativ zum aktuellen
Projektschwerpunkt. Renderer und Export nutzen `LocalPoints` direkt; PointsJson wird nur
für Persistenz gelesen/geschrieben. `ArTransferService` schreibt direkt v2 (kein UTM-Zwischenschritt).

---

## TerrainMesh — Thread-Safety

`TerrainMesh` ist nach Erstellung **immutable** (alle Properties `init`). Renderer halten die
Referenz beim `Render()`-Call. Wenn neue Punkte kommen, MUSS ein neues `TerrainMesh` erzeugt
werden — niemals die Arrays eines bestehenden Mesh mutieren (IndexOutOfRangeException beim Render).

Vorberechnete Normalen (`NormalsX/Y/Z`) sparen ~24 k sqrt/s bei 400 Dreiecken × 60 fps.

---

## StickState — Fix-Quality-Tabelle

| Wert | Bedeutung | UI-Farbe |
|------|-----------|----------|
| 0 | NoFix | Rot |
| 1 | GPS-only | Orange |
| 2 | DGPS | Orange |
| 4 | RTK-Fix (±2 cm) | Grün |
| 5 | RTK-Float (±10–50 cm) | Gelb |

`StickState.GetFixStatusText(fixQuality)` → lokalisierter Anzeigetext.
