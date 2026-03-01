using HandwerkerRechner.Models;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Visualisierung: Draufsicht auf gefliesten Boden mit sichtbaren Fugen.
/// Zeigt Fliesengitter mit proportionalen Fugenbreiten, Bemaßung und Eimer-Info.
/// </summary>
public static class GroutVisualization
{
    // Einschwing-Animation
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    /// <summary>Startet die Einschwing-Animation.</summary>
    public static void StartAnimation() => _animation.StartAnimation();

    /// <summary>True wenn noch animiert wird (für InvalidateSurface-Loop).</summary>
    public static bool NeedsRedraw => _animation.IsAnimating;

    // Gecachte Paints
    private static readonly SKPaint TilePaint = new() { Color = new SKColor(0xDD, 0xD6, 0xCE), Style = SKPaintStyle.Fill, IsAntialias = true };
    private static readonly SKPaint TileBorderPaint = new() { Color = new SKColor(0xBB, 0xB3, 0xA8), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, IsAntialias = true };
    private static readonly SKPaint GroutPaint = new() { Color = new SKColor(0x78, 0x71, 0x6C), Style = SKPaintStyle.Fill, IsAntialias = true };
    private static readonly SKPaint DimensionPaint = new() { Color = new SKColor(0xEC, 0x48, 0x99), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true, PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0) };
    private static readonly SKPaint DimTextPaint = new() { Color = new SKColor(0xEC, 0x48, 0x99), TextSize = 11, IsAntialias = true, Typeface = SKTypeface.Default };
    private static readonly SKPaint InfoBgPaint = new() { Color = new SKColor(0x30, 0xEC, 0x48, 0x99), Style = SKPaintStyle.Fill, IsAntialias = true };
    private static readonly SKPaint InfoTextPaint = new() { Color = new SKColor(0xFF, 0xFF, 0xFF), TextSize = 12, IsAntialias = true, Typeface = SKTypeface.Default, FakeBoldText = true };
    private static readonly SKPaint SubTextPaint = new() { Color = new SKColor(0xCC, 0xCC, 0xCC), TextSize = 10, IsAntialias = true, Typeface = SKTypeface.Default };

    public static void Render(SKCanvas canvas, SKRect bounds, GroutResult result)
    {
        if (result.TotalKg <= 0) return;

        // Animation-Update
        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Global Alpha Fade-In
        using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * progress)) };
        canvas.SaveLayer(layerPaint);

        float padding = 16;
        float drawW = bounds.Width - 2 * padding;
        float drawH = bounds.Height - 2 * padding;

        // Fliesengitter-Bereich (linke 65%)
        float gridW = drawW * 0.65f;
        float gridH = drawH;
        float gridX = bounds.Left + padding;
        float gridY = bounds.Top + padding;

        // Fliesengröße und Fugenbreite proportional berechnen
        float totalTileW = (float)result.TileLengthCm;
        float totalTileH = (float)result.TileWidthCm;
        float groutW = (float)result.GroutWidthMm / 10f; // mm → cm

        // Skalierung berechnen (Fliesen + Fugen passen in den Grid-Bereich)
        float unitW = totalTileW + groutW;
        float unitH = totalTileH + groutW;

        int cols = Math.Max(1, (int)(gridW / (unitW * 2))); // Mindestens sichtbar
        int rows = Math.Max(1, (int)(gridH / (unitH * 2)));

        // Scale so das Grid reinpasst
        float scaleX = gridW / (cols * unitW + groutW);
        float scaleY = gridH / (rows * unitH + groutW);
        float scale = Math.Min(scaleX, scaleY);

        float tilePixW = totalTileW * scale;
        float tilePixH = totalTileH * scale;
        float groutPixW = groutW * scale;
        // Mindest-Fugenbreite für Sichtbarkeit
        groutPixW = Math.Max(groutPixW, 2f);

        // Hintergrund (Fugenmasse-Farbe)
        float totalGridW = cols * (tilePixW + groutPixW) + groutPixW;
        float totalGridH = rows * (tilePixH + groutPixW) + groutPixW;
        float startX = gridX + (gridW - totalGridW) / 2;
        float startY = gridY + (gridH - totalGridH) / 2;

        canvas.DrawRect(startX, startY, totalGridW, totalGridH, GroutPaint);

        // Fliesen zeichnen
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float tx = startX + groutPixW + c * (tilePixW + groutPixW);
                float ty = startY + groutPixW + r * (tilePixH + groutPixW);
                canvas.DrawRect(tx, ty, tilePixW, tilePixH, TilePaint);
                canvas.DrawRect(tx, ty, tilePixW, tilePixH, TileBorderPaint);
            }
        }

        // Bemaßung: Fugenbreite (horizontal)
        if (cols >= 2)
        {
            float dimY = startY + totalGridH + 10;
            float fX1 = startX + groutPixW + tilePixW;
            float fX2 = fX1 + groutPixW;
            canvas.DrawLine(fX1, dimY, fX2, dimY, DimensionPaint);
            canvas.DrawLine(fX1, dimY - 4, fX1, dimY + 4, DimensionPaint);
            canvas.DrawLine(fX2, dimY - 4, fX2, dimY + 4, DimensionPaint);
            string dimText = $"{result.GroutWidthMm:F1} mm";
            canvas.DrawText(dimText, fX2 + 4, dimY + 4, DimTextPaint);
        }

        // Info-Box (rechte 30%)
        float infoX = gridX + gridW + padding;
        float infoY = gridY + 10;
        float infoW = drawW * 0.30f;
        float infoH = drawH - 20;

        var infoRect = new SKRoundRect(new SKRect(infoX, infoY, infoX + infoW, infoY + infoH), 8);
        canvas.DrawRoundRect(infoRect, InfoBgPaint);

        // Info-Texte
        float textX = infoX + 10;
        float textY = infoY + 24;
        float lineH = 22;

        canvas.DrawText($"{result.TotalWithReserveKg:F1} kg", textX, textY, InfoTextPaint);
        textY += 16;
        canvas.DrawText("inkl. 10% Reserve", textX, textY, SubTextPaint);
        textY += lineH + 8;

        canvas.DrawText($"{result.BucketsNeeded} Eimer", textX, textY, InfoTextPaint);
        textY += 16;
        canvas.DrawText("\u00e0 5 kg", textX, textY, SubTextPaint);
        textY += lineH + 8;

        canvas.DrawText($"{result.ConsumptionPerSqm:F2} kg/m\u00b2", textX, textY, InfoTextPaint);
        textY += 16;
        canvas.DrawText("Verbrauch", textX, textY, SubTextPaint);
        textY += lineH + 8;

        canvas.DrawText($"{result.TotalCost:F2} \u20ac", textX, textY, InfoTextPaint);
        textY += 16;
        canvas.DrawText("Materialkosten", textX, textY, SubTextPaint);

        canvas.Restore();
    }
}
