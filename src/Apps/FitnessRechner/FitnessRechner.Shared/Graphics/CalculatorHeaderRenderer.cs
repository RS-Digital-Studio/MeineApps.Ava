using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert den Medical-Style Header für alle 5 Rechner-Views.
/// Feature-Farbe als Gradient + Medical Grid + Mini-EKG + Holografischer Back-Button.
/// </summary>
public static class CalculatorHeaderRenderer
{
    // Hit-Test Zone für den Back-Button
    private const float BackButtonSize = 40f;
    private const float BackButtonMargin = 16f;

    // Grid-Linien Abstand
    private const float GridSpacing = 30f;

    // EKG-Animation
    private const float EkgAmplitude = 6f;
    private const float EkgAlpha = 0.4f;
    private const float EkgGlowAlpha = 0.2f;
    private const float EkgGlowBlur = 2f;

    // Titel
    private const float TitleFontSize = 20f;
    private const float TitleGlowAlpha = 0.25f;
    private const float TitleGlowBlur = 2f;

    /// <summary>
    /// Zeichnet den vollständigen Medical-Header mit Gradient, Grid, EKG, Back-Button und Titel.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string title, SKColor featureColor, SKColor featureColorDark, float time)
    {
        // Clip auf abgerundetes Rect (nur unten abgerundet)
        canvas.Save();
        using var clipPath = new SKPath();
        var rrect = new SKRoundRect(bounds, 0f, 0f);
        rrect.SetRectRadii(bounds, new[]
        {
            new SKPoint(0, 0),     // oben-links
            new SKPoint(0, 0),     // oben-rechts
            new SKPoint(24, 24),   // unten-rechts
            new SKPoint(24, 24)    // unten-links
        });
        clipPath.AddRoundRect(rrect);
        canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);

        RenderBackground(canvas, bounds, featureColor, featureColorDark);
        RenderMedicalGrid(canvas, bounds, featureColor);
        RenderEkgWave(canvas, bounds, featureColor, time);
        RenderBackButton(canvas, bounds);
        RenderTitle(canvas, bounds, title);

        canvas.Restore();
    }

    // =====================================================================
    // 1. Hintergrund: Feature-Farbe Gradient (links oben → rechts unten)
    // =====================================================================

    private static void RenderBackground(SKCanvas canvas, SKRect bounds,
        SKColor featureColor, SKColor featureColorDark)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Right, bounds.Bottom),
            new[] { featureColor, featureColorDark },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = shader
        };

        canvas.DrawRect(bounds, paint);
    }

    // =====================================================================
    // 2. Medical Grid Overlay: Feine Linien (15% Opacity)
    // =====================================================================

    private static void RenderMedicalGrid(SKCanvas canvas, SKRect bounds, SKColor featureColor)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            Color = featureColor.WithAlpha(38) // ~15% Alpha
        };

        // Vertikale Linien
        for (float x = bounds.Left + GridSpacing; x < bounds.Right; x += GridSpacing)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, paint);

        // Horizontale Linien
        for (float y = bounds.Top + GridSpacing; y < bounds.Bottom; y += GridSpacing)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, paint);
    }

    // =====================================================================
    // 3. Mini-EKG: Feature-farbige EKG-Welle über die gesamte Breite
    // =====================================================================

    private static void RenderEkgWave(SKCanvas canvas, SKRect bounds, SKColor featureColor, float time)
    {
        var wave = MedicalColors.EkgWave;
        int waveLen = wave.Length;
        if (waveLen == 0) return;

        float midY = bounds.MidY;
        float width = bounds.Width;

        // Sweep-Offset basierend auf Zeit (72 BPM)
        float cycleProgress = (time * MedicalColors.BeatsPerSecond) % 1f;
        float sweepOffset = cycleProgress * width;

        // EKG-Pfad erstellen
        using var path = new SKPath();
        bool first = true;

        for (float px = 0; px <= width; px += 2f)
        {
            // Position im Wellenform-Array (mit Sweep-Offset)
            float wavePos = ((px + sweepOffset) % width) / width * waveLen;
            int idx = (int)wavePos;
            float frac = wavePos - idx;

            // Interpolation zwischen benachbarten Samples
            float sample0 = wave[idx % waveLen];
            float sample1 = wave[(idx + 1) % waveLen];
            float sample = sample0 + (sample1 - sample0) * frac;

            float y = midY - sample * EkgAmplitude * 2f;
            float x = bounds.Left + px;

            if (first)
            {
                path.MoveTo(x, y);
                first = false;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        // Glow-Effekt (breitere, transparentere Linie dahinter)
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = featureColor.WithAlpha((byte)(255 * EkgGlowAlpha)),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, EkgGlowBlur)
        };
        canvas.DrawPath(path, glowPaint);

        // Hauptlinie
        using var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = featureColor.WithAlpha((byte)(255 * EkgAlpha))
        };
        canvas.DrawPath(path, linePaint);
    }

    // =====================================================================
    // 4. Back-Button: Holografischer Kreis mit Chevron-Left
    // =====================================================================

    private static void RenderBackButton(SKCanvas canvas, SKRect bounds)
    {
        float cx = bounds.Left + BackButtonMargin + BackButtonSize / 2f;
        float cy = bounds.MidY;
        float radius = BackButtonSize / 2f;

        // Halbtransparenter Surface-Hintergrund
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Surface.WithAlpha(128) // 50% Alpha
        };
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Cyan-Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = MedicalColors.Cyan.WithAlpha(180)
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        // Chevron-Left als SKPath (einfacher Pfeil <)
        using var chevronPath = new SKPath();
        float arrowSize = 9f;
        chevronPath.MoveTo(cx + arrowSize * 0.3f, cy - arrowSize);
        chevronPath.LineTo(cx - arrowSize * 0.5f, cy);
        chevronPath.LineTo(cx + arrowSize * 0.3f, cy + arrowSize);

        using var chevronPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = SKColors.White
        };
        canvas.DrawPath(chevronPath, chevronPaint);
    }

    // =====================================================================
    // 5. Titel: 20pt, Bold, Weiß, mittig vertikal, nach dem Back-Button
    // =====================================================================

    private static void RenderTitle(SKCanvas canvas, SKRect bounds, string title)
    {
        if (string.IsNullOrEmpty(title)) return;

        float textX = bounds.Left + BackButtonMargin + BackButtonSize + 16f;
        float textY = bounds.MidY;

        // Subtiler Glow
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)(255 * TitleGlowAlpha)),
            TextSize = TitleFontSize,
            Typeface = SKTypeface.FromFamilyName("Inter", SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, TitleGlowBlur)
        };

        // Vertikal zentrieren
        var metrics = glowPaint.FontMetrics;
        float centerY = textY - (metrics.Ascent + metrics.Descent) / 2f;

        canvas.DrawText(title, textX, centerY, glowPaint);

        // Haupttext
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            TextSize = TitleFontSize,
            Typeface = SKTypeface.FromFamilyName("Inter", SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        canvas.DrawText(title, textX, centerY, textPaint);
    }

    // =====================================================================
    // Hit-Test: Prüft ob der Tap auf den Back-Button-Bereich gefallen ist
    // =====================================================================

    /// <summary>
    /// Prüft ob der Tap auf den Back-Button-Bereich gefallen ist.
    /// Toleranz: +8px um den Kreis für einfacheres Tippen.
    /// </summary>
    public static bool IsBackButtonHit(SKRect bounds, float skiaX, float skiaY)
    {
        float cx = bounds.Left + BackButtonMargin + BackButtonSize / 2f;
        float cy = bounds.MidY;
        float dx = skiaX - cx;
        float dy = skiaY - cy;
        float hitRadius = BackButtonSize / 2f + 8f;
        return dx * dx + dy * dy <= hitRadius * hitRadius;
    }
}
