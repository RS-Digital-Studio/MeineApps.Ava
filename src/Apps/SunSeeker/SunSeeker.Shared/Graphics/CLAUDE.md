# Graphics — SkiaSharp-Renderer

3 instanzbasierte Renderer (`IDisposable` — gecachte Paints/Fonts/Shader). `Render(SKCanvas, SKRect, …)`
wird vom View-Code-Behind im `PaintSurface`-Handler aufgerufen. SkiaSharp-Grundlagen/Gotchas →
[MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

---

| Renderer | Zweck | Besonderheit |
|----------|-------|-------------|
| `SunCompassRenderer` | Ausricht-Kompass (Nord oben): Sonne (elevations-abhängiger Glow), grüner Soll-Marker, qualitätsgefärbter Panel-Pfeil, Neigungs-Bogen, Quality-Glow. | Shader-Caching für Glow; Winkel→Bildschirm: `x=cx+sin(az)`, `y=cy−cos(az)`. |
| `SunPathRenderer` | Sonnenbahn-Diagramm: Tagesbahn (Elevation über Azimut O/S/W), Horizont, aktuelle Position. | X-Achse 30–330° (DE-Tagesbahn), Y 0–70°; Gradient-Füllung gecacht. |
| `PowerChartRenderer` | Live-Watt-Trend (gleitendes Fenster). | Flächen-Gradient + Linie + Watt-Gitter; `MaxWatts` = Panel-Nennleistung. |

---

## Performance-Regeln (wie alle Skia-Renderer im Workspace)

1. **Paints/Fonts NIEMALS pro Frame neu** — als Felder, in `Dispose()` freigeben.
2. **`SKFont` explizit** (SkiaSharp 3.x), nicht `SKPaint.TextSize`.
3. **`canvas.LocalClipBounds`** für die Größe, NICHT `e.Info.Width/Height` (DPI).
4. **Shader-Caching** — Radial-/Linear-Gradienten nur bei Parameter-/Größen-Änderung neu erzeugen
   (alten `Shader.Dispose()` vor Neuzuweisung — sonst Leak).

## App-Palette (Solar)

Primary Sonnen-Amber `#FFB300`, Sonne `#FFD54F`, Horizont `#FF7043`, Akzent/Erfolg `#43A047`,
Hintergrund daemmriges Dunkel `#14182B`. Quality-Ampel: Excellent grün → Poor rot.
