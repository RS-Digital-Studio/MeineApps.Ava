# Graphics — SkiaSharp-Renderer

3 SkiaSharp-Renderer für Geo-Visualisierungen. Die zwei instanzbasierten Renderer implementieren `IDisposable` (gecachte Paints/Fonts/Shader). `ProjectThumbnailRenderer` ist `static` (statische Paint-Felder, kein `IDisposable`).
`Render(SKCanvas canvas, SKRect bounds, …)`-Methoden werden vom jeweiligen Code-Behind im `PaintSurface`-Handler aufgerufen.
SkiaSharp-Grundlagen/Gotchas (Paint-Lifecycle, DPI, MaskFilter-Leak) → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

---

## Dateien

| Datei | Zweck | Besonderheit |
|-------|-------|-------------|
| `TerrainRenderer.cs` | 3D-Geländemodell: Höhenfarbkodierung, Konturlinien, Rotation, Painter's Algorithm | Painter's Algorithm nach Kamera-Z (nicht Screen-Y), gecachte screenX/Y/Z-Arrays, vorberechnete Face-Normalen aus Mesh, Höhen-Legende als `LinearGradient`-Shader, **stabile Einpassung** |
| `GardenPlanRenderer.cs` | 2D-Gartenplan: Elemente als farbige Polygone/Linien, Labels | Min/Max in 1-Pass, gecachter Preview-Path + SKPoint-Array, `element.LocalPoints` direkt (kein PointsJson-Re-Parse pro Frame), **stabile Einpassung** |
| `ProjectThumbnailRenderer.cs` | Vorschau-Thumbnail für Projekt-Liste | Statisch mit gecachten Paints, SKFont-API |

---

## Farbpalette (App-weit)

| Token | Hex | Verwendung |
|-------|-----|-----------|
| Primary | `#FF6B00` | Messpunkte, AR-Punkte, Labels |
| Secondary | `#2196F3` | Linien, Kontur-Hilfslinien |
| Accent | `#4CAF50` | Erfolg, grüne Akzente |
| AR Contour | `#00BCD4` | AR-Kontur-Linien |
| AR Active | `#FFEB3B` | Aktive Kontur (gestrichelt) |
| AR Selected | `#00BCD4` | Ausgewählter Punkt mit Glow |
| Background | `#1A1A2E` | Canvas-Hintergrund |
| Surface | `#16213E` | Panel-Hintergrund |

---

## Performance-Regeln

1. **Paints NIEMALS pro Frame neu erstellen** — alle `SKPaint`-Objekte als Fields (kein GC-Druck beim Touch-Drag).
2. **SKFont explizit** — SkiaSharp 3.x API (`new SKFont(SKTypeface.Default, size)`), nicht über `SKPaint.TextSize`.
3. **`canvas.LocalClipBounds`** für Canvas-Größe, NICHT `e.Info.Width/Height` (DPI-Skalierung).
4. **Gecachte Paths** für statische Geometrien (Nordpfeil in `TerrainRenderer`, Preview-Path in `GardenPlanRenderer`).
5. **Normalen aus Mesh** — `TerrainMesh.NormalsX/Y/Z` sind vorberechnet; Renderer ruft `RecalculateNormals()` NICHT pro Frame auf.

---

## Stabile Einpassung (`ResetFit`) — Terrain + GardenPlan

Zentrum und Spannweite (`_fitCenter*`, `_fitRange`) werden **einmal** aus den Daten bestimmt und
danach beibehalten. Vorher rechnete jeder Frame beides neu aus der aktuellen Bounding-Box — ein
einzelner neuer Messpunkt verschob und skalierte damit die ganze Szene, alle bereits gesetzten
Punkte wanderten sichtbar auf dem Schirm.

Neu eingepasst wird nur, wenn (a) noch keine Einpassung existiert, (b) die Daten aus der
bestehenden herauswachsen — sonst wären sie unsichtbar — oder (c) `ResetFit()` gerufen wurde.
`ResetFit()` gehört an **echte Datensatz-Wechsel**, nicht an einzelne Punkte:
`PointsReset` (Projekt-Load/Clear), `LoadElementsFromProjectAsync`, `TerrainViewModel.ResetView`.
`PointAdded` ruft es bewusst **nicht**.

Beim Terrain bleibt auch die Höhen-Mitte (`_fitCenterZ`) stehen — sonst kippt ein einzelner
tiefer Punkt das ganze Relief. Der Maßstab-Balken rechnet gegen `_fitRange`, damit
Pixel-pro-Meter und Anzeige zusammenpassen.

## TerrainRenderer — Painter's Algorithm

Dreiecke werden nach ihrer Kamera-Z-Koordinate (Tiefe relativ zur Kamera) sortiert und von
hinten nach vorne gerendert. **Screen-Y ist kein korrekter Sort-Key** bei geneigter Kamera.
Rotation via Azimut + Elevation (Kugelkoordinaten-Projektion), Zoom + Pan als Canvas-Transform.

## Gotchas

| Problem | Fix |
|---------|-----|
| `SKMaskFilter`-Leak bei Glow-Effekten | `paint.MaskFilter?.Dispose()` VOR `CreateBlur`-Neuzuweisung (oder gecachte static SKMaskFilter bei festem Radius) |
| `e.Info.Width/Height` bei DPI > 1 größer als sichtbar | `canvas.LocalClipBounds` verwenden |
| Konturlinie exakt auf Vertex | Höhe um `1e-9` perturbieren + Doppel-Intersections dedup |
| GardenPlan-Renderer re-parsed PointsJson pro Frame | `element.LocalPoints` direkt nutzen (transient gecacht vom Service) |
