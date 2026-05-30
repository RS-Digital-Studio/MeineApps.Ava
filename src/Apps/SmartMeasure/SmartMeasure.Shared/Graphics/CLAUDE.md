# Graphics — SkiaSharp-Renderer

5 SkiaSharp-Renderer für Geo-Visualisierungen. Alle implementieren `IDisposable` (gecachte Paints).
`Render()`-Methoden werden vom jeweiligen Code-Behind auf `PaintSurface` aufgerufen.
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

---

## Dateien

| Datei | Zweck | Besonderheit |
|-------|-------|-------------|
| `TerrainRenderer.cs` | 3D-Geländemodell: Höhenfarbkodierung, Konturlinien, Rotation, Painter's Algorithm | Painter's Algorithm nach Kamera-Z (nicht Screen-Y), gecachte screenX/Y/Z-Arrays, vorberechnete Face-Normalen aus Mesh, Höhen-Legende als `LinearGradient`-Shader |
| `GardenPlanRenderer.cs` | 2D-Gartenplan: Elemente als farbige Polygone/Linien, Labels | Min/Max in 1-Pass, gecachter Preview-Path + SKPoint-Array, `element.LocalPoints` direkt (kein PointsJson-Re-Parse pro Frame) |
| `SurveyLiveRenderer.cs` | Live-Kompass: Kompass-Ring, Genauigkeits-Ring, Satelliten, Fix-Glow, Neigungsindikator | Nordpfeil-Path gecacht, Shader-Caching für Fix-Glow, SKFont-API (SkiaSharp 3.x) |
| `StakeoutRenderer.cs` | Absteck-Pfeil: Richtung zum nächsten Ziel, Distanz-Anzeige | Pfeilfarbe distanz-codiert: grün <10 cm / gelb <1 m / orange <5 m / rot >5 m. Pfeillänge wächst bis 80 % Radius |
| `ProjectThumbnailRenderer.cs` | Vorschau-Thumbnail für Projekt-Liste | Statisch mit gecachten Paints, SKFont-API |

---

## Farbpalette (App-weit)

| Token | Hex | Verwendung |
|-------|-----|-----------|
| Primary | `#FF6B00` | Messpunkte, AR-Punkte, Labels |
| Secondary | `#2196F3` | Linien, Kontur-Hilfslinien |
| Accent | `#4CAF50` | RTK-Fix-Glow, grüner Pfeil |
| AR Contour | `#00BCD4` | AR-Kontur-Linien |
| AR Active | `#FFEB3B` | Aktive Kontur (gestrichelt), Stakeout-Ziel |
| AR Selected | `#00BCD4` | Ausgewählter Punkt mit Glow |
| Background | `#1A1A2E` | Canvas-Hintergrund |
| Surface | `#16213E` | Panel-Hintergrund |

---

## Performance-Regeln

1. **Paints NIEMALS pro Frame neu erstellen** — alle `SKPaint`-Objekte als Fields (kein GC-Druck beim Touch-Drag).
2. **SKFont explizit** — SkiaSharp 3.x API (`new SKFont(SKTypeface.Default, size)`), nicht über `SKPaint.TextSize`.
3. **`canvas.LocalClipBounds`** für Canvas-Größe, NICHT `e.Info.Width/Height` (DPI-Skalierung).
4. **Gecachte Paths** für statische Geometrien (Nordpfeil in `TerrainRenderer` + `SurveyLiveRenderer`, Preview-Path in `GardenPlanRenderer`).
5. **Shader-Caching** — Fix-Glow-Shader in `SurveyLiveRenderer` nur bei Parameter-Änderung neu erstellen.
6. **Normalen aus Mesh** — `TerrainMesh.NormalsX/Y/Z` sind vorberechnet; Renderer ruft `RecalculateNormals()` NICHT pro Frame auf.

---

## TerrainRenderer — Painter's Algorithm

Dreiecke werden nach ihrer Kamera-Z-Koordinate (Tiefe relativ zur Kamera) sortiert und von
hinten nach vorne gerendert. **Screen-Y ist kein korrekter Sort-Key** bei geneigter Kamera.
Rotation via Azimut + Elevation (Kugelkoordinaten-Projektion), Zoom + Pan als Canvas-Transform.

## SurveyLiveRenderer — Kompass-Aufbau

```
Äußerer Ring:  Kompass (N/E/S/W + 30°-Schritte, roter Nordpfeil)
Mittlerer Ring: Genauigkeits-Ring (Radius ∝ HorizontalAccuracy, Farbe = Fix-Qualität)
Zentrum:        Fadenkreuz + Accuracy-Zahl in cm
Overlay:        Satelliten-Zahl, Fix-Label, Tilt-Balken
```

## Gotchas

| Problem | Fix |
|---------|-----|
| `SKMaskFilter`-Leak bei Fix-Glow | `paint.MaskFilter?.Dispose()` VOR `CreateBlur`-Neuzuweisung (oder gecachte static SKMaskFilter bei festem Radius) |
| `e.Info.Width/Height` bei DPI > 1 größer als sichtbar | `canvas.LocalClipBounds` verwenden |
| Konturlinie exakt auf Vertex | Höhe um `1e-9` perturbieren + Doppel-Intersections dedup |
| GardenPlan-Renderer re-parsed PointsJson pro Frame | `element.LocalPoints` direkt nutzen (transient gecacht vom Service) |
