# Models — Datenmodelle

SQLite-Entities, immutable Structs und AR-Typen. Alle Entities mit `[PrimaryKey, AutoIncrement]`
für SQLite. AR-Modelle sind Plain-C#-Klassen (kein SQLite).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `SurveyProject.cs` | SQLite-Entity: Projekt (Name, Typ, Fläche, Umfang, PointCount). `Points` + `GardenElements` sind `[Ignore]` — werden separat geladen |
| `SurveyPoint.cs` | SQLite-Entity: WGS84-Position (±2 cm RTK), Genauigkeit, Tilt, Fix-Quality, Foto-Pfad, Voice-Transkript, Confidence |
| `GardenElement.cs` | SQLite-Entity: Polygon/Linie mit `PointsJson` (v2: WGS84, v1: legacy UTM). `LocalPoints` ist `[Ignore]` + transient. `GardenElementType`-Enum: Weg, Beet, Rasen, Mauer, Zaun, Terrasse, Grenze, Gebäude, Wasser, Kante |
| `TerrainMesh.cs` | Immutable Delaunay-Gitter: Vertex-Arrays X/Y/Z, Triangle-Index-Array, vorberechnete Normalen, Bounding Box. `ContourLine` (Isohypse) ebenfalls in dieser Datei |
| `StakeoutTarget.cs` | Absteck-Ziel (Lat/Lon/Altitude/Label). `ObservableObject` + `[ObservableProperty]` auf `IsReached` + `BestDistance` (live UI-Update). `StakeoutTargetSource`-Enum: `SurveyPoint` / `GardenElement` |
| `StickState.cs` | BLE-Snapshot: Fix-Quality, Accuracy, Tilt, Battery, MagAccuracy, NtripStatus, SatelliteCount, laufende Position (Lat/Lon/Altitude nullable) |
| `NtripConfig.cs` | NTRIP-Konfiguration: Server, Port, Mountpoint, Username, Password, `IsOwnBase`, ProfileName |
| `MaterialEstimate.cs` | Berechneter Materialbedarf: `Material`-String, Menge, Einheit, `QuantityWithSafety` (+15 %). Kein Typ-Enum — `Material` ist Freitext |
| `ArPoint.cs` | AR-Messpunkt: ARCore-Koordinaten + Confidence + SemanticLabel (`ArSemanticLabel`-Enum) + PhotoPath + optionale Geo-Koordinaten (VPS) + Tracking-Metadaten |
| `ArContour.cs` | AR-Kontur aus mehreren `ArPoint`s. Nicht zwingend geschlossen (`IsClosed`-Flag). `ArContourType`-Enum: Grenze, Weg, Beet, Mauer, Zaun, Terrasse, Gebäude, Wasser, Kante |
| `ArCaptureResult.cs` | Übergabe-Objekt von `ArCaptureActivity` → `IArCaptureService.CaptureAsync()`. Enthält alle Punkte + Konturen + `TotalPointCount`, GPS-Anker, Geospatial-Metadaten, `ArGpsSource`-Enum |
| `ArReferenceMarker.cs` | SQLite-Entity: vorab mit RTK eingemessener Referenz-Marker (ArUco). Wird beim AR-Session-Start übergeben; Recognition via `AugmentedImageDatabase`. Kein transienter Erkennungs-Snapshot |

---

## SurveyPoint — Feld-Details

- `Altitude` = NN-Höhe in Metern (bereits Geoid-korrigiert + Tilt-korrigiert). NICHT Ellipsoid-Höhe.
- `TiltAngle` / `TiltAzimuth` = Stab-Neigung beim Messen (Grad vom Lot / true north). Wichtig für nachträgliche Korrektheit-Überprüfung.
- `MagAccuracy` (BNO085, 0–3): ≥ 2 = Horizontal-Tilt-Korrektur aktiv; < 2 = nur Vertikal-Korrektur.
- `PhotoPath` + `VoiceTranscript` / `VoiceAudioPath` = optional, können null sein. PDF-Bericht prüft `File.Exists`.
- `Confidence` (0–1): bei AR-Punkten echte ARCore-Confidence (Hit-Quality, Streuung, Stability); bei RTK-Stab 1.0.

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

`StickState.Latitude/Longitude/Altitude` (nullable) enthalten die laufende Rover-Position
aus dem `PositionUpdated`-Event — werden in `SurveyViewModel` für den Live-Positionsmarker genutzt.

---

## StakeoutTarget — ObservableObject

`StakeoutTarget` erbt von `ObservableObject` (CommunityToolkit.Mvvm), damit `IsReached` und
`BestDistance` direkt per Binding in der Stakeout-Liste aktualisiert werden — kein separates
ViewModel für jeden Listeneintrag nötig. `StakeoutTargetSource` unterscheidet Ursprung
(gespeicherter Messpunkt vs. Kontur-Knotenpunkt aus GardenElement).
