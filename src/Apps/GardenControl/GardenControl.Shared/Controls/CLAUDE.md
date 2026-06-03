# Controls — SkiaSharp Custom Controls

App-eigene SkiaSharp-Visualisierungen für Feuchtigkeitsdaten. Beide Controls verwenden
`ICustomDrawOperation` (kein `SKCanvasView`) um nahtlos in den Avalonia-Render-Tree zu
passen. SkiaSharp-Grundlagen/Gotchas → [MeineApps.UI](../../../../UI/MeineApps.UI/CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MoistureGaugeControl.cs` | Kreisförmiger Ring-Gauge: 270°-Bogen mit Sweep-Gradient, Schwellenwert-Markierung, Glow bei Bewässerung. |
| `MoistureChartControl.cs` | Liniengraph: Feuchtigkeitsverlauf mit Farbfläche, Schwellenwert-Linie, Bewässerungsereignisse als blaue Balken. Enthält `ChartDataPoint`. |

## MoistureGaugeControl

Styled Properties: `MoisturePercent` (double), `ThresholdPercent` (int, Default 40),
`ZoneName` (string), `IsWatering` (bool), `StatusText` (string).
`AffectsRender<>` auf allen Properties — kein manuelles `InvalidateVisual()` nötig.

**Paint-Caching:** Alle `SKPaint`-Instanzen sowie `InterBold`/`InterSemiBold` Typefaces sind
`static readonly` auf `GaugeDrawOperation`. Sie leben bis Prozess-Ende — kein `Dispose()` nötig
und kein Pro-Frame-Allokations-Druck. Properties wie `StrokeWidth`, `Color`, `Shader` werden
pro Frame mutiert.

**Sweep-Gradient:** Farben (`_gradientColors`) und Positionen (`_gradientPositions`) sind
`static readonly` gecacht. Das `SKShader`-Objekt selbst (`SKShader.CreateSweepGradient`)
wird pro `Render()`-Aufruf lokal erstellt, weil es den Center-Punkt (Bounds-abhängig) einbettet
→ `using`-Block garantiert Dispose.

**Gecachter MaskFilter:** `_glowBlurFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)`
ist `static readonly` — wird nur einmal erstellt. In `_glowPaint.MaskFilter` gesetzt und nach
dem Draw auf `null` zurückgesetzt (verhindert ungewollten Glow auf anderen Draws).

**Gauge-Geometrie:** 270°-Bogen (135° Start, 270° Sweep). Feuchtigkeit → Sweep-Anteil:
`sweepAngle = (moisture / 100.0) * 270`. Schwellenwert → Winkel in Grad → Radiant → Linienpunkt
auf innerem/äußerem Radius.

**Farben nach Feuchtigkeit:**

| Bereich | Farbe |
|---------|-------|
| < 25 % | `#EF5350` Rot (kritisch trocken) |
| 25–40 % | `#FF9800` Orange (zu trocken) |
| 40–70 % | `#66BB6A` Grün (optimal) |
| > 70 % | `#42A5F5` Blau (sehr nass) |

## MoistureChartControl

Styled Properties: `DataPoints` (`List<ChartDataPoint>`), `ThresholdPercent` (int, Default 40),
`Title` (string). `AffectsRender<>` auf allen Properties.

**Paint-Caching:** `InterSemiBold` Typeface ist `static readonly` auf `ChartDrawOperation`.
Die übrigen `SKPaint`-Instanzen werden pro `Render()` mit `using` lokal alloziert (anders als
`GaugeDrawOperation`). Bei Performance-Problemen wäre statisches Paint-Caching analog zu
`GaugeDrawOperation` der erste Ansatzpunkt.

**Layout:** Padding: links 40px (Y-Achse), oben 28px (Titel), rechts 12px, unten 24px (Zeit-Achse).
Grid-Linien alle 25 % (0/25/50/75/100). Schwellenwert-Linie gestrichelt orange.

**Daten-Normalisierung:** X-Achse → Zeitstempel-Ticks relativ zu min/max. Y-Achse → 0-100 %
von unten nach oben.

**Bewässerungsereignisse:** `ChartDataPoint.WasWatering == true` → 3px breiter blauer Balken
(`#42A5F5, Alpha 40`) über die gesamte Chart-Höhe.

**Zeit-Labels:** Bis zu 6 gleichmäßig verteilte Zeitstempel auf der X-Achse, lokale Zeit `"HH:mm"`.

**`ChartDataPoint`** (im selben File):
```csharp
public class ChartDataPoint
{
    public DateTime TimestampUtc { get; set; }
    public double MoisturePercent { get; set; }
    public bool WasWatering { get; set; }
}
```

## Gotchas

- **Kein `ICustomDrawOperation.Equals()` implementiert:** Beide `Equals()`-Overrides
  geben immer `false` zurück → Avalonia rendert bei jeder Invalidierung neu. Das ist für
  Live-Daten (SignalR-Updates alle ~10s) korrekt.
- **`MoistureChartControl` braucht mindestens 2 Punkte:** Weniger als 2 Datenpunkte → kein
  Pfad gezeichnet (early return nach Grid/Schwellenwert). Die View sollte einen "Keine Daten"-
  Hinweis zeigen wenn `DataPoints.Count < 2`.
- **Sweep-Gradient neu erstellen bei Bounds-Wechsel:** Das `SKShader`-Objekt muss den
  Center-Punkt kennen — deshalb pro Frame neu erstellen, nicht statisch cachen. Die Farb-Arrays
  selbst sind statisch.
