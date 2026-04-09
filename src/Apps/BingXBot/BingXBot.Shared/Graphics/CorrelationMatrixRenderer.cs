using SkiaSharp;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet eine Korrelations-Heatmap zwischen gehandelten Symbolen.
/// Werte von -1 (inverse Korrelation, Blau) bis +1 (perfekte Korrelation, Rot).
/// </summary>
public static class CorrelationMatrixRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor TextColor = SKColor.Parse("#E2E8F0");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 10);
    private static readonly SKFont ValueFont = new(SKTypeface.Default, 9);
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint GridPaint = new() { Color = GridColor, StrokeWidth = 0.5f };

    public static void Render(SKCanvas canvas, SKRect bounds, string[] symbols, float[,] matrix)
    {
        canvas.Clear(BgColor);
        var n = symbols.Length;
        if (n < 2)
        {
            canvas.DrawText("Min. 2 offene Positionen für Korrelations-Matrix",
                bounds.MidX, bounds.MidY, SKTextAlign.Center, new SKFont(SKTypeface.Default, 13),
                new SKPaint { Color = SKColor.Parse("#64748B"), IsAntialias = true });
            return;
        }

        var labelW = 70f;
        var cellSize = Math.Min((bounds.Width - labelW) / n, (bounds.Height - labelW) / n);
        cellSize = Math.Min(cellSize, 50f);

        var startX = bounds.Left + labelW;
        var startY = bounds.Top + labelW;

        // Zeilen- und Spalten-Labels
        for (int i = 0; i < n; i++)
        {
            // Spalten-Labels (oben, rotiert)
            var x = startX + i * cellSize + cellSize / 2;
            canvas.Save();
            canvas.RotateDegrees(-45, x, startY - 5);
            canvas.DrawText(symbols[i], x, startY - 5, SKTextAlign.Center, LabelFont, TextPaint);
            canvas.Restore();

            // Zeilen-Labels (links)
            var y = startY + i * cellSize + cellSize / 2 + 4;
            canvas.DrawText(symbols[i], startX - 8, y, SKTextAlign.Right, LabelFont, TextPaint);
        }

        // Zellen zeichnen
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                var val = matrix[row, col];
                var x = startX + col * cellSize;
                var y = startY + row * cellSize;

                // Farbe: -1=Blau, 0=Grau, +1=Rot
                var color = CorrelationToColor(val);
                using var cellPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                canvas.DrawRect(x + 1, y + 1, cellSize - 2, cellSize - 2, cellPaint);

                // Wert in der Zelle
                var textCol = Math.Abs(val) > 0.5f ? SKColors.White : TextColor;
                using var valPaint = new SKPaint { Color = textCol, IsAntialias = true };
                canvas.DrawText($"{val:F2}", x + cellSize / 2, y + cellSize / 2 + 4, SKTextAlign.Center, ValueFont, valPaint);

                // Grid-Linie
                canvas.DrawRect(x, y, cellSize, cellSize, GridPaint);
            }
        }
    }

    private static SKColor CorrelationToColor(float val)
    {
        val = Math.Clamp(val, -1f, 1f);
        if (val >= 0)
        {
            // 0=transparent, 1=Rot
            var alpha = (byte)(val * 180);
            return new SKColor(239, 68, 68, alpha); // Rot
        }
        else
        {
            // -1=Blau, 0=transparent
            var alpha = (byte)(-val * 180);
            return new SKColor(59, 130, 246, alpha); // Blau
        }
    }
}
