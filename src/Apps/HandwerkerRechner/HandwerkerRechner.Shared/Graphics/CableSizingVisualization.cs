using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Kabelquerschnitt-Visualisierung: Links Leiterquerschnitt (Kreis), rechts Spannungsabfall-Balken.
/// Kupfer = Kupferbraun, Aluminium = Silber. VDE-konform = Grün, nicht konform = Rot.
/// </summary>
public static class CableSizingVisualization
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
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barFill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Farben
    private static readonly SKColor _copperColor = new(0xB8, 0x73, 0x33);      // Kupfer
    private static readonly SKColor _aluminumColor = new(0xC0, 0xC0, 0xC0);    // Aluminium
    private static readonly SKColor _insulationColor = new(0x42, 0x42, 0x42);   // Kabelisolierung (dunkelgrau)
    private static readonly SKColor _vdeGreen = new(0x22, 0xC5, 0x5E);         // VDE konform
    private static readonly SKColor _vdeRed = new(0xEF, 0x44, 0x44);           // VDE nicht konform

    public static void Render(SKCanvas canvas, SKRect bounds,
        float recommendedCrossSection, float actualDropPercent, float maxDropPercent,
        int materialType, bool isVdeCompliant)
    {
        if (recommendedCrossSection <= 0) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        SkiaBlueprintCanvas.DrawGrid(canvas, bounds, 20f);

        // Global Alpha Fade-In
        using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * progress)) };
        canvas.SaveLayer(layerPaint);

        float margin = 30f;
        float availW = bounds.Width - 2 * margin;
        float availH = bounds.Height - 2 * margin;

        // === Linke Hälfte: Kabelquerschnitt (Kreis) ===
        float leftW = availW * 0.55f;
        float centerX = bounds.Left + margin + leftW * 0.5f;
        float centerY = bounds.Top + margin + availH * 0.5f;

        // Leiter-Radius proportional zum Querschnitt (1.5mm² = klein, 120mm² = groß)
        float maxRadius = Math.Min(leftW, availH) * 0.35f;
        float minRadius = maxRadius * 0.15f;
        float normalizedSize = Math.Clamp(recommendedCrossSection / 120f, 0.05f, 1f);
        float leiterRadius = minRadius + (maxRadius - minRadius) * normalizedSize;

        // Isolierung drumherum (ca. 20% größer)
        float insulRadius = leiterRadius * 1.25f;

        // Isolierung zeichnen
        _fillPaint.Color = SkiaThemeHelper.WithAlpha(_insulationColor, 180);
        canvas.DrawCircle(centerX, centerY, insulRadius * progress, _fillPaint);

        // Leiter zeichnen (Kupfer oder Aluminium)
        SKColor leiterColor = materialType == 1 ? _aluminumColor : _copperColor;
        _fillPaint.Color = SkiaThemeHelper.WithAlpha(leiterColor, 220);
        canvas.DrawCircle(centerX, centerY, leiterRadius * progress, _fillPaint);

        // Leiter-Textur: Feines Glanzlicht (Halbkreis oben)
        if (leiterRadius * progress > 8f)
        {
            using var glanzPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = SKColors.White.WithAlpha(40)
            };
            float glanzR = leiterRadius * progress * 0.7f;
            canvas.DrawArc(
                new SKRect(centerX - glanzR, centerY - glanzR, centerX + glanzR, centerY + glanzR),
                200f, 140f, true, glanzPaint);
        }

        // Umriss
        _strokePaint.Color = SkiaThemeHelper.TextSecondary;
        canvas.DrawCircle(centerX, centerY, insulRadius * progress, _strokePaint);
        canvas.DrawCircle(centerX, centerY, leiterRadius * progress, _strokePaint);

        // Querschnitt-Text in der Mitte
        if (leiterRadius * progress > 12f)
        {
            float fontSize = Math.Clamp(leiterRadius * progress * 0.35f, 8f, 14f);
            _textPaint.Color = materialType == 1 ? SKColors.Black.WithAlpha(200) : SKColors.White.WithAlpha(220);
            _textPaint.TextSize = fontSize;
            _textPaint.TextAlign = SKTextAlign.Center;
            _textPaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            canvas.DrawText($"{recommendedCrossSection:F1}", centerX, centerY + fontSize * 0.35f, _textPaint);

            // mm² unter der Zahl
            _textPaint.TextSize = fontSize * 0.7f;
            canvas.DrawText("mm\u00b2", centerX, centerY + fontSize * 0.35f + fontSize * 0.8f, _textPaint);
        }

        // Bemaßung: Durchmesser-Linie unter dem Kreis
        float dimY = centerY + insulRadius * progress + 18f;
        float dimLeft = centerX - insulRadius * progress;
        float dimRight = centerX + insulRadius * progress;
        if (insulRadius * progress > 4f)
        {
            SkiaBlueprintCanvas.DrawDimensionLine(canvas,
                new SKPoint(dimLeft, dimY),
                new SKPoint(dimRight, dimY),
                $"\u00d8 {Math.Sqrt(recommendedCrossSection / Math.PI) * 2:F1} mm", offset: 0f);
        }

        // === Rechte Hälfte: Spannungsabfall-Balken ===
        float barX = bounds.Left + margin + leftW + 20f;
        float barW = availW - leftW - 30f;
        float barH = availH * 0.7f;
        float barY = bounds.Top + margin + (availH - barH) * 0.5f;

        // Hintergrund-Balken
        _fillPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextSecondary, 30);
        canvas.DrawRoundRect(barX, barY, barW, barH, 4f, 4f, _fillPaint);

        // Gefüllter Balken (von unten nach oben, proportional zu actualDropPercent/maxDropPercent)
        float maxDisplay = Math.Max(maxDropPercent, actualDropPercent) * 1.3f; // Etwas Puffer oben
        if (maxDisplay <= 0) maxDisplay = 5f;
        float fillRatio = Math.Clamp(actualDropPercent / maxDisplay, 0f, 1f) * progress;
        float fillH = barH * fillRatio;

        _barFill.Color = isVdeCompliant ? _vdeGreen : _vdeRed;
        if (fillH > 0)
        {
            canvas.DrawRoundRect(barX, barY + barH - fillH, barW, fillH, 4f, 4f, _barFill);
        }

        // Grenzlinie bei maxDropPercent
        float limitRatio = Math.Clamp(maxDropPercent / maxDisplay, 0f, 1f);
        float limitY = barY + barH - barH * limitRatio;
        using var dashPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = _vdeRed.WithAlpha(180),
            PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
        };
        canvas.DrawLine(barX - 4f, limitY, barX + barW + 4f, limitY, dashPaint);

        // Grenzlinie-Label ("max 3%")
        _textPaint.Color = _vdeRed.WithAlpha(200);
        _textPaint.TextSize = 9f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
        canvas.DrawText($"max {maxDropPercent:F0}%", barX + barW * 0.5f, limitY - 4f, _textPaint);

        // Ist-Wert Label am Balken
        float istLabelY = barY + barH - fillH - 4f;
        if (istLabelY < limitY + 14f && fillH > 0)
            istLabelY = barY + barH - fillH + 14f; // Unter dem Balken wenn zu nah an Grenzlinie
        _textPaint.Color = isVdeCompliant ? _vdeGreen : _vdeRed;
        _textPaint.TextSize = 11f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        if (fillH > 2f)
            canvas.DrawText($"{actualDropPercent:F1}%", barX + barW * 0.5f, istLabelY, _textPaint);

        // Info-Text unten: Material + Spannung
        string materialText = materialType == 1 ? "Aluminium" : "Kupfer";
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"{materialText}  |  {recommendedCrossSection:F1} mm\u00b2",
            new SKPoint(bounds.MidX, bounds.Bottom - margin + 10f),
            SkiaThemeHelper.TextSecondary, 10f);

        canvas.Restore();
    }
}
