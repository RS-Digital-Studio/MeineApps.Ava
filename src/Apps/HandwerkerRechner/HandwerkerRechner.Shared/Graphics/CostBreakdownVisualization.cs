using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Horizontale gestapelte Balken für Kostenaufschlüsselung.
/// Zeigt Material, Verschnitt, Arbeit etc. als farbige Segmente mit Labels und Werten.
/// Wiederverwendbar für alle Rechner mit Kostenausgabe.
/// </summary>
public static class CostBreakdownVisualization
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

    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 12f };
    private static readonly SKFont _totalFont = new() { Size = 14f };

    // Vordefinierte Farben für Kostensegmente
    private static readonly SKColor[] DefaultColors =
    {
        new(0x3B, 0x82, 0xF6), // Blau (Material)
        new(0xF5, 0x9E, 0x0B), // Amber (Verschnitt)
        new(0x22, 0xC5, 0x5E), // Grün (Arbeit)
        new(0xA7, 0x8B, 0xFA), // Violett
        new(0xEF, 0x44, 0x44), // Rot
        new(0x22, 0xD3, 0xEE), // Cyan
    };

    /// <summary>
    /// Ein einzelnes Kosten-Segment.
    /// </summary>
    public struct CostItem
    {
        /// <summary>Bezeichnung (z.B. "Material").</summary>
        public string Label;

        /// <summary>Wert in der Basiswährung.</summary>
        public float Value;

        /// <summary>Farbe (optional, nutzt Default wenn Alpha=0).</summary>
        public SKColor Color;
    }

    /// <summary>
    /// Rendert eine horizontale Kostenaufschlüsselung mit gestapelten Balken.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="items">Kostenpositionen (Label + Wert + optionale Farbe)</param>
    /// <param name="currencySymbol">Währungssymbol (z.B. "€")</param>
    /// <param name="totalLabel">Label für Gesamtsumme (z.B. "Gesamt")</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        CostItem[] items, string currencySymbol = "€", string totalLabel = "Gesamt")
    {
        if (items.Length == 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        float padding = 12f;
        float barHeight = 24f;
        float legendItemH = 20f;
        float barTop = bounds.Top + padding;
        float barLeft = bounds.Left + padding;
        float barRight = bounds.Right - padding;
        float barWidth = barRight - barLeft;

        if (barWidth <= 20) return;

        // Gesamtsumme berechnen
        float total = 0;
        foreach (var item in items)
            total += Math.Max(0, item.Value);

        if (total <= 0) return;

        // 1. Hintergrund-Bar (dezent)
        float cornerR = barHeight / 2f;
        var barRect = new SKRect(barLeft, barTop, barRight, barTop + barHeight);

        _bgPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 30);
        canvas.DrawRoundRect(barRect, cornerR, cornerR, _bgPaint);

        // 2. Gestapelte Segmente
        float currentX = barLeft;

        // Clip auf abgerundetes Rechteck für saubere Kanten
        canvas.Save();
        using var clipPath = new SKPath();
        clipPath.AddRoundRect(barRect, cornerR, cornerR);
        canvas.ClipPath(clipPath);

        for (int i = 0; i < items.Length; i++)
        {
            float value = Math.Max(0, items[i].Value);
            if (value <= 0) continue;

            // Animation: Segmentbreite wächst mit progress
            float segWidth = (value / total) * barWidth * progress;
            if (segWidth < 1f) continue;

            var color = items[i].Color.Alpha > 0
                ? items[i].Color
                : DefaultColors[i % DefaultColors.Length];

            // Segment zeichnen
            _barPaint.Color = color;
            canvas.DrawRect(currentX, barTop, segWidth, barHeight, _barPaint);

            // Prozentzahl im Segment (wenn breit genug)
            float pct = (value / total) * 100f;
            if (segWidth > 35f)
            {
                string pctText = $"{pct:F0}%";
                _textPaint.Color = SKColors.White.WithAlpha(230);
                _labelFont.Size = 10f;
                canvas.DrawText(pctText, currentX + segWidth / 2f, barTop + barHeight / 2f + 4f,
                    SKTextAlign.Center, _labelFont, _textPaint);
            }

            currentX += segWidth;
        }

        canvas.Restore();

        // 3. Legende unter dem Balken
        float legendY = barTop + barHeight + 10f;
        float legendX = barLeft;
        float dotR = 4f;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Value <= 0) continue;

            var color = items[i].Color.Alpha > 0
                ? items[i].Color
                : DefaultColors[i % DefaultColors.Length];

            // Farbpunkt
            _barPaint.Color = color;
            canvas.DrawCircle(legendX + dotR, legendY + legendItemH / 2f, dotR, _barPaint);

            // Label + Wert
            string text = $"{items[i].Label}: {items[i].Value:F2} {currencySymbol}";
            _textPaint.Color = SkiaThemeHelper.TextSecondary;
            _labelFont.Size = 11f;
            canvas.DrawText(text, legendX + dotR * 2 + 6f, legendY + legendItemH / 2f + 4f,
                SKTextAlign.Left, _labelFont, _textPaint);

            // Textbreite messen für nächstes Item
            using var blob = SKTextBlob.Create(text, _labelFont);
            float textW = blob?.Bounds.Width ?? 100f;

            legendX += dotR * 2 + 6f + textW + 16f;

            // Zeilenumbruch wenn zu breit
            if (legendX > barRight - 50f && i < items.Length - 1)
            {
                legendX = barLeft;
                legendY += legendItemH;
            }
        }

        // 4. Gesamtsumme (rechts unten, fett)
        legendY += legendItemH + 4f;
        string totalText = $"{totalLabel}: {total:F2} {currencySymbol}";
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _totalFont.Size = 13f;
        canvas.DrawText(totalText, barRight, legendY,
            SKTextAlign.Right, _totalFont, _textPaint);
    }
}
