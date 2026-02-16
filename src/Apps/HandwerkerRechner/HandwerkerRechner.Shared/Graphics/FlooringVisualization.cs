using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Draufsicht Dielenverlegung: 50%-Versatz, abwechselnd hell/dunkelbraun, Maßlinien.
/// </summary>
public static class FlooringVisualization
{
    private static readonly SKPaint _boardFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _boardStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f };
    private static readonly SKPaint _roomStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };

    // Holzfarben (hell/dunkel abwechselnd)
    private static readonly SKColor _woodLight = new(0xD4, 0xA5, 0x74);
    private static readonly SKColor _woodDark = new(0xA0, 0x7A, 0x55);
    private static readonly SKColor _woodMedium = new(0xC4, 0x93, 0x65);

    public static void Render(SKCanvas canvas, SKRect bounds,
        float roomLengthM, float roomWidthM, float boardLengthM, float boardWidthCm,
        float wastePercent, bool hasResult)
    {
        if (!hasResult || roomLengthM <= 0 || roomWidthM <= 0) return;
        if (boardLengthM <= 0 || boardWidthCm <= 0) return;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        float boardWidthM = boardWidthCm / 100f;

        var fit = SkiaBlueprintCanvas.FitToCanvas(bounds, roomLengthM, roomWidthM, 40f);
        float scale = fit.Scale;
        float ox = fit.OffsetX;
        float oy = fit.OffsetY;

        float rw = roomLengthM * scale;
        float rh = roomWidthM * scale;

        float bw = boardLengthM * scale; // Diele-Länge (horizontal)
        float bh = boardWidthM * scale;  // Diele-Breite (vertikal)

        if (bw > 1f && bh > 1f)
        {
            int rows = (int)Math.Ceiling(rh / bh);

            canvas.Save();
            canvas.ClipRect(new SKRect(ox, oy, ox + rw, oy + rh));

            for (int row = 0; row < rows; row++)
            {
                float y = oy + row * bh;
                float h = Math.Min(bh, oy + rh - y);
                float offset = (row % 2 == 1) ? bw * 0.5f : 0f; // 50% Versatz

                int cols = (int)Math.Ceiling((rw + offset) / bw) + 1;

                for (int col = -1; col < cols; col++)
                {
                    float x = ox + col * bw - offset;
                    float drawX = Math.Max(x, ox);
                    float drawW = Math.Min(x + bw, ox + rw) - drawX;
                    if (drawW <= 0) continue;

                    // Abwechselnde Holzfarben
                    int colorIdx = (row + col) % 3;
                    SKColor woodColor = colorIdx switch
                    {
                        0 => _woodLight,
                        1 => _woodDark,
                        _ => _woodMedium
                    };

                    _boardFill.Color = woodColor;
                    canvas.DrawRect(drawX, y, drawW, h, _boardFill);

                    // Dielen-Rahmen (Fugen)
                    _boardStroke.Color = SkiaThemeHelper.WithAlpha(new SKColor(0x60, 0x40, 0x20), 120);
                    canvas.DrawRect(drawX, y, drawW, h, _boardStroke);
                }
            }

            canvas.Restore();
        }

        // Raum-Umriss
        _roomStroke.Color = SkiaThemeHelper.TextPrimary;
        canvas.DrawRect(ox, oy, rw, rh, _roomStroke);

        // Maßlinien
        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox + rw, oy),
            $"{roomLengthM:F2} m", offset: -14f);

        SkiaBlueprintCanvas.DrawDimensionLine(canvas,
            new SKPoint(ox, oy), new SKPoint(ox, oy + rh),
            $"{roomWidthM:F2} m", offset: -14f);
    }
}
