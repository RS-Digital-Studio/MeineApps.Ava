# Models — Datenmodelle

SQLite-Entities, immutable Structs und AR-Typen. Alle Entities mit `[PrimaryKey, AutoIncrement]`
für SQLite. AR-Modelle sind Plain-C#-Klassen (kein SQLite).
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `SurveyProject.cs` | SQLite-Entity: Projekt (Name, Typ, Fläche, Umfang, PointCount). `Points` + `GardenElements` sind `[Ignore]` — werden separat geladen |
| `SurveyPoint.cs` | SQLite-Entity: WGS84-Position, Genauigkeit (H/V in cm), Confidence, Foto-Pfad, Voice-Transkript |
| `GardenElement.cs` | SQLite-Entity: Polygon/Linie mit `PointsJson` (v2: WGS84, v1: legacy UTM). `LocalPoints` ist `[Ignore]` + transient. `GardenElementType`-Enum: Weg, Beet, Rasen, Mauer, Zaun, Terrasse, Grenze, Gebäude, Wasser, Kante |
| `TerrainMesh.cs` | Immutable Delaunay-Gitter: Vertex-Arrays X/Y/Z, Triangle-Index-Array, vorberechnete Normalen, Bounding Box. `ContourLine` (Isohypse) ebenfalls in dieser Datei |
| `MaterialEstimate.cs` | Berechneter Materialbedarf: `Material`-String, Menge, Einheit, `QuantityWithSafety` (+15 %). Kein Typ-Enum — `Material` ist Freitext |
| `ArPoint.cs` | AR-Messpunkt: ARCore-Koordinaten + Confidence + SemanticLabel (`ArSemanticLabel`-Enum) + PhotoPath + optionale Geo-Koordinaten (VPS) + Tracking-Metadaten. Flags → eigener Abschnitt unten |
| `ArContour.cs` | AR-Kontur aus mehreren `ArPoint`s. Nicht zwingend geschlossen (`IsClosed`-Flag). `ArContourType`-Enum: Grenze, Weg, Beet, Mauer, Zaun, Terrasse, Gebäude, Wasser, Kante |
| `ArCaptureResult.cs` | Übergabe-Objekt von `ArCaptureActivity` → `IArCaptureService.CaptureAsync()`. Enthält alle Punkte + Konturen + `TotalPointCount`, GPS-Anker, Geospatial-Metadaten, `ArGpsSource`-Enum |

---

## ArPoint — Flags, die der Transfer auswerten MUSS

Der Transfer entscheidet anhand dieser Flags, woher die Position eines Punkts kommt. `HasValue`
auf den Geo-Feldern allein ist **nie** ein ausreichendes Kriterium.

| Flag | Bedeutung | Wirkung im Transfer |
|------|-----------|---------------------|
| `HasLocalPosition` (Default **true**) | `X`/`Y`/`Z` sind eine echte Position im Session-Frame | Priorität 1: Georeferenzierung über Session-Anker + Heading (starr für den ganzen Satz). Total-Station setzt `false` (radial gerechnet, X/Y/Z bleiben 0) |
| `GeoIsExact` | `GeoLatitude/-Longitude` sind die Geo-Pose des **Messpunkts** (ARCore-Earth auf der Hit-Pose). `false` = **Kamera**-Position, nur Höhe korrigiert — Fehler ist die volle Messdistanz | Ohne lokale Position nur bei `true` verwendbar; sonst wird der Punkt verworfen |
| `GroundOffsetYAtCapture` | Boden-Ebene **zum Messzeitpunkt** | Schlägt den Sitzungswert `ArCaptureResult.GroundPlaneY` (ein EMA vom Sitzungsende) |
| `IsPreloaded` | Vorlade-Punkt aus dem Projekt, relativ platziert | Geht nie ins Result (Duplikat-/Korruptionsschutz) |
| `RestoredWithoutGeo` | Recovery ohne belastbaren Geo-Bezug (auch bei `GeoIsExact == false`) | Accuracy ≥ 5 m, Konfidenz ≤ 0,3 |

Recovery-States werden als JSON serialisiert — neue Flags reisen automatisch mit, alte States
tragen sie nicht (JSON-Default) und werden dadurch bewusst konservativ behandelt.

## SurveyPoint — Feld-Details

- `Altitude` = NN-Höhe in Metern (bereits Geoid-korrigiert). NICHT Ellipsoid-Höhe.
- `PhotoPath` + `VoiceTranscript` / `VoiceAudioPath` = optional, können null sein. PDF-Bericht prüft `File.Exists`.
- `Confidence` (0–1): echte ARCore-Confidence (Hit-Quality, Streuung, Stability). 0 = unbekannt.

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
