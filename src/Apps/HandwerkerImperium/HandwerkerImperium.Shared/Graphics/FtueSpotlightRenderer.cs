using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für das Spotlight-Overlay.
/// Zeichnet ein dunkles semi-transparentes Overlay mit ausgespartem Hot-Spot
/// (Kreis um Spotlight-Target) plus pulsierendem Glow-Ring.
///
/// Wird von <c>FtueOverlayControl</c> aus dem PaintSurface-Handler gerufen.
/// State-frei — Renderer ist statisch, alle Daten kommen als Parameter.
/// </summary>
public static class FtueSpotlightRenderer
{
    /// <summary>Standard-Spotlight-Radius in dp (~64dp = grosser Touch-Target).</summary>
    public const float DefaultRadiusDp = 64f;

    /// <summary>Pulse-Geschwindigkeit (0.5 = langsam, 2.0 = schnell).</summary>
    private const float PulseSpeed = 1.2f;

    /// <summary>
    /// Zeichnet das gesamte Overlay. Gibt die Bubble-Position (oberhalb/unterhalb des Spotlights) zurück,
    /// damit der UI-Layer (AXAML/UserControl) die Title+Text-Bubble dort positionieren kann.
    /// </summary>
    /// <param name="canvas">Die Avalonia-SKCanvas.</param>
    /// <param name="canvasWidth">Canvas-Breite in Pixel (DPI-skaliert).</param>
    /// <param name="canvasHeight">Canvas-Hoehe in Pixel.</param>
    /// <param name="dpi">DPI-Skalierung (1.0 = mdpi, 2.0 = xhdpi, 3.0 = xxhdpi).</param>
    /// <param name="spotlightX">X-Center des Hot-Spots in dp (oder negativ = kein Spotlight, nur Backdrop).</param>
    /// <param name="spotlightY">Y-Center in dp.</param>
    /// <param name="spotlightRadius">Hot-Spot-Radius in dp.</param>
    /// <param name="elapsedSeconds">Verstrichene Zeit fuer Pulse-Animation (DispatcherTimer).</param>
    public static void Render(
        SKCanvas canvas,
        float canvasWidth,
        float canvasHeight,
        float dpi,
        float spotlightX,
        float spotlightY,
        float spotlightRadius,
        float elapsedSeconds)
    {
        // 1. Voller Backdrop (75% schwarz) — fuellt den ganzen Canvas.
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 190), // ~75%
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawRect(0, 0, canvasWidth, canvasHeight, bgPaint);

        // Wenn kein Spotlight gesetzt, nur Backdrop zeichnen (z.B. fuer Welcome-Step).
        if (spotlightX < 0 || spotlightY < 0 || spotlightRadius <= 0) return;

        var cx = spotlightX * dpi;
        var cy = spotlightY * dpi;
        var rPixel = spotlightRadius * dpi;

        // 2. Hot-Spot ausstanzen (Clear-Mode).
        // Pulse: radius variiert 95%-105% der Basis-Groesse.
        var pulse = 1.0f + 0.05f * (float)Math.Sin(elapsedSeconds * PulseSpeed * Math.PI * 2);
        var rPulsed = rPixel * pulse;

        using var clearPaint = new SKPaint
        {
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
        };
        canvas.DrawCircle(cx, cy, rPulsed, clearPaint);

        // 3. Glow-Ring um den Hot-Spot (Craft-Gold).
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, 200),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f * dpi,
        };
        canvas.DrawCircle(cx, cy, rPulsed + 2f * dpi, glowPaint);

        // 4. Aeusserer Glow-Halo (gedimmt).
        using var haloPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, 70),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 12f * dpi,
        };
        canvas.DrawCircle(cx, cy, rPulsed + 8f * dpi, haloPaint);

        // 5. Pfeil-Indikator: zeigt von der Bubble-Mitte zum Spotlight (Cue: hier tappen!).
        // Der Pfeil wird hier nicht mehr gerendert — die UI-Bubble hat einen eigenen Caret-Triangle.
    }

    /// <summary>
    /// Liefert die empfohlene Bubble-Y-Position (in dp): oberhalb des Spotlights,
    /// es sei denn der Spotlight liegt zu weit oben — dann unterhalb.
    /// </summary>
    /// <param name="spotlightYDp">Y-Center des Spotlights in dp.</param>
    /// <param name="spotlightRadiusDp">Radius in dp.</param>
    /// <param name="canvasHeightDp">Canvas-Hoehe in dp.</param>
    /// <param name="bubbleHeightDp">Geschaetzte Bubble-Hoehe in dp.</param>
    public static (float Y, bool Above) ComputeBubblePosition(
        float spotlightYDp, float spotlightRadiusDp, float canvasHeightDp, float bubbleHeightDp)
    {
        var spaceAbove = spotlightYDp - spotlightRadiusDp;
        var minSpaceForBubble = bubbleHeightDp + 24f;
        if (spaceAbove >= minSpaceForBubble)
            return (spotlightYDp - spotlightRadiusDp - bubbleHeightDp - 16f, true);
        return (spotlightYDp + spotlightRadiusDp + 16f, false);
    }
}
