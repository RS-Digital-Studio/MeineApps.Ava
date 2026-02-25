using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für das Glücksrad (Lucky Spin).
/// Zeichnet ein 8-Segment-Rad mit Zeiger, Nabe, Schatten und Glow-Effekt
/// für das gewinnende Segment. Icons werden als SkiaSharp-Pfade gezeichnet
/// (keine Emojis/Unicode, plattformunabhängig).
/// </summary>
public class LuckySpinWheelRenderer
{
    // Segment-Farben (8 Segmente)
    private static readonly SKColor[] SegmentColors =
    [
        new SKColor(0x9E, 0x9E, 0x9E), // MoneySmall - Grau
        new SKColor(0x4C, 0xAF, 0x50), // MoneyMedium - Grün
        new SKColor(0xFF, 0xD7, 0x00), // MoneyLarge - Gold
        new SKColor(0x21, 0x96, 0xF3), // XpBoost - Blau
        new SKColor(0xFF, 0x8F, 0x00), // GoldenScrews - Amber
        new SKColor(0x00, 0xBC, 0xD4), // SpeedBoost - Cyan
        new SKColor(0xFF, 0x57, 0x22), // ToolUpgrade - Orange
        new SKColor(0xD3, 0x2F, 0x2F)  // Jackpot - Rot
    ];

    // Dunklere Varianten für Gradient-Effekt
    private static readonly SKColor[] SegmentDarkColors =
    [
        new SKColor(0x75, 0x75, 0x75), // Grau dunkel
        new SKColor(0x38, 0x8E, 0x3C), // Grün dunkel
        new SKColor(0xCC, 0xA8, 0x00), // Gold dunkel
        new SKColor(0x18, 0x76, 0xD2), // Blau dunkel
        new SKColor(0xCC, 0x72, 0x00), // Amber dunkel
        new SKColor(0x00, 0x97, 0xA7), // Cyan dunkel
        new SKColor(0xE6, 0x4A, 0x19), // Orange dunkel
        new SKColor(0xB7, 0x1C, 0x1C)  // Rot dunkel
    ];

    // Rad-Farben
    private static readonly SKColor RimColor = new(0x37, 0x47, 0x4F);
    private static readonly SKColor RimHighlight = new(0x45, 0x55, 0x5E);
    private static readonly SKColor HubColor = new(0x45, 0x5A, 0x64);
    private static readonly SKColor HubHighlight = new(0x60, 0x7D, 0x8B);
    private static readonly SKColor PointerColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor PointerShadow = new(0xCC, 0xA8, 0x00);
    private static readonly SKColor JackpotBorderColor = new(0xFF, 0xD7, 0x00);

    private const int SegmentCount = 8;
    private const float SweepAngle = 360f / SegmentCount; // 45° pro Segment

    // Gecachte MaskFilter (statt pro Frame neu erstellen)
    private static readonly SKMaskFilter _blurShadow12 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12);
    private static readonly SKMaskFilter _blurGlow10 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10);
    private static readonly SKMaskFilter _blurGlow6 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6);
    private static readonly SKMaskFilter _blurGlow4 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private static readonly SKMaskFilter _blurSmall2 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2);
    private static readonly SKMaskFilter _blurSmall1_5 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f);
    private static readonly SKMaskFilter _blurShadow3 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);

    // Gecachte Paints für häufig verwendete Schatten/Glow (nur Farbe wird pro Aufruf geändert)
    private static readonly SKPaint _shadowPaint = new() { IsAntialias = true, MaskFilter = _blurSmall2 };
    private static readonly SKPaint _glintPaint = new() { IsAntialias = true, MaskFilter = _blurSmall1_5 };

    /// <summary>
    /// Rendert das Glücksrad auf den Canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas.</param>
    /// <param name="bounds">Verfügbarer Zeichenbereich.</param>
    /// <param name="currentAngle">Aktueller Rotationswinkel in Grad.</param>
    /// <param name="highlightedSegment">Index des gewinnenden Segments (0-7) oder null.</param>
    public void Render(SKCanvas canvas, SKRect bounds, double currentAngle, int? highlightedSegment)
    {
        canvas.Clear(SKColors.Transparent);

        float padding = 16;
        float available = Math.Min(bounds.Width - padding * 2, bounds.Height - padding * 2);
        float radius = available / 2f;

        // Zentriert im Bounds-Bereich
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Zeiger-Platz oben berücksichtigen
        float pointerHeight = radius * 0.14f;
        cy += pointerHeight * 0.3f;

        // --- Schatten unter dem Rad ---
        DrawWheelShadow(canvas, cx, cy, radius);

        // --- Äußerer Ring (Rim) ---
        DrawRim(canvas, cx, cy, radius);

        // --- Segmente ---
        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees((float)currentAngle);
        canvas.Translate(-cx, -cy);

        float innerRadius = radius * 0.88f;
        DrawSegments(canvas, cx, cy, innerRadius, highlightedSegment);

        // --- Segment-Icons ---
        DrawSegmentIcons(canvas, cx, cy, innerRadius);

        canvas.Restore();

        // --- Nabe (Mitte) ---
        DrawHub(canvas, cx, cy, radius);

        // --- Zeiger oben ---
        DrawPointer(canvas, cx, cy - radius + 2, pointerHeight);
    }

    /// <summary>
    /// Zeichnet den Schatten unter dem Rad für Tiefenwirkung.
    /// </summary>
    private static void DrawWheelShadow(SKCanvas canvas, float cx, float cy, float radius)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x00, 0x00, 0x00, 60),
            MaskFilter = _blurShadow12
        };
        canvas.DrawCircle(cx + 4, cy + 6, radius + 4, paint);
    }

    /// <summary>
    /// Zeichnet den äußeren Ring des Rades.
    /// </summary>
    private static void DrawRim(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Äußerer Kreis (dunkler Rand)
        using var rimPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = RimColor
        };
        canvas.DrawCircle(cx, cy, radius, rimPaint);

        // Highlight-Ring für 3D-Effekt (oben heller)
        using var highlightPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = RimHighlight
        };
        canvas.DrawCircle(cx, cy, radius - 1, highlightPaint);

        // Dekorative Nieten am Rand
        int dotCount = 24;
        float dotRadius = radius * 0.02f;
        using var dotPaint = new SKPaint
        {
            IsAntialias = true,
            Color = RimHighlight
        };
        for (int i = 0; i < dotCount; i++)
        {
            float angle = i * 360f / dotCount * MathF.PI / 180f;
            float dx = cx + (radius * 0.94f) * MathF.Cos(angle);
            float dy = cy + (radius * 0.94f) * MathF.Sin(angle);
            canvas.DrawCircle(dx, dy, dotRadius, dotPaint);
        }
    }

    /// <summary>
    /// Zeichnet alle 8 Segmente mit Gradient und optionalem Glow-Highlight.
    /// </summary>
    private static void DrawSegments(SKCanvas canvas, float cx, float cy, float innerRadius,
        int? highlightedSegment)
    {
        var segmentRect = new SKRect(cx - innerRadius, cy - innerRadius,
            cx + innerRadius, cy + innerRadius);

        for (int i = 0; i < SegmentCount; i++)
        {
            float startAngle = i * SweepAngle - 90; // Start oben (12-Uhr-Position)

            // Segment mit radialem Gradient (heller in der Mitte, dunkler am Rand)
            using var segPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Shader = SKShader.CreateRadialGradient(
                    new SKPoint(cx, cy),
                    innerRadius,
                    [SegmentColors[i], SegmentDarkColors[i]],
                    [0.3f, 1.0f],
                    SKShaderTileMode.Clamp)
            };

            using var path = new SKPath();
            path.MoveTo(cx, cy);
            path.ArcTo(segmentRect, startAngle, SweepAngle, false);
            path.Close();
            canvas.DrawPath(path, segPaint);

            // Trennlinien zwischen Segmenten
            using var linePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                Color = new SKColor(0x00, 0x00, 0x00, 80)
            };
            float lineAngle = startAngle * MathF.PI / 180f;
            canvas.DrawLine(cx, cy,
                cx + innerRadius * MathF.Cos(lineAngle),
                cy + innerRadius * MathF.Sin(lineAngle),
                linePaint);

            // Jackpot-Segment: zusätzlicher goldener Rand
            if (i == 7)
            {
                using var jackpotBorder = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 3,
                    Color = JackpotBorderColor
                };
                using var jackpotPath = new SKPath();
                jackpotPath.MoveTo(cx, cy);
                jackpotPath.ArcTo(segmentRect, startAngle, SweepAngle, false);
                jackpotPath.Close();
                canvas.DrawPath(jackpotPath, jackpotBorder);
            }

            // Glow-Effekt auf dem gewinnenden Segment
            if (highlightedSegment == i)
            {
                DrawSegmentGlow(canvas, cx, cy, innerRadius, startAngle, i);
            }
        }
    }

    /// <summary>
    /// Zeichnet den leuchtenden Glow-Effekt auf dem gewinnenden Segment.
    /// </summary>
    private static void DrawSegmentGlow(SKCanvas canvas, float cx, float cy, float innerRadius,
        float startAngle, int segmentIndex)
    {
        var segmentRect = new SKRect(cx - innerRadius, cy - innerRadius,
            cx + innerRadius, cy + innerRadius);

        // Äußerer Glow (breit, transparent)
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SegmentColors[segmentIndex].WithAlpha(100),
            MaskFilter = _blurGlow10
        };
        using var glowPath = new SKPath();
        glowPath.MoveTo(cx, cy);
        glowPath.ArcTo(segmentRect, startAngle, SweepAngle, false);
        glowPath.Close();
        canvas.DrawPath(glowPath, glowPaint);

        // Innerer Glow (konzentriert, heller)
        using var innerGlowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White.WithAlpha(80),
            MaskFilter = _blurGlow6
        };
        canvas.DrawPath(glowPath, innerGlowPaint);

        // Leuchtender Rand des Segments
        using var borderGlow = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            Color = SKColors.White.WithAlpha(180),
            MaskFilter = _blurGlow4
        };
        canvas.DrawPath(glowPath, borderGlow);
    }

    /// <summary>
    /// Zeichnet die SkiaSharp-Icons in die Segmente, ~65% vom Zentrum entfernt.
    /// Jedes Segment bekommt ein individuelles, vektorbasiertes Icon.
    /// </summary>
    private static void DrawSegmentIcons(SKCanvas canvas, float cx, float cy, float innerRadius)
    {
        float iconSize = innerRadius * 0.17f;

        for (int i = 0; i < SegmentCount; i++)
        {
            // Winkel zur Mitte des Segments
            float midAngle = (i * SweepAngle + SweepAngle / 2f - 90) * MathF.PI / 180f;

            // Position ~65% vom Zentrum
            float iconRadius = innerRadius * 0.65f;
            float ix = cx + iconRadius * MathF.Cos(midAngle);
            float iy = cy + iconRadius * MathF.Sin(midAngle);

            // Canvas rotieren damit Icon aufrecht im Segment steht
            canvas.Save();
            canvas.Translate(ix, iy);
            float rotDeg = i * SweepAngle + SweepAngle / 2f;
            canvas.RotateDegrees(rotDeg);

            // Icon zeichnen (zentriert um 0,0)
            switch (i)
            {
                case 0: DrawMoneyIcon(canvas, 0, 0, iconSize, 1); break;
                case 1: DrawMoneyIcon(canvas, 0, 0, iconSize, 2); break;
                case 2: DrawMoneyIcon(canvas, 0, 0, iconSize, 3); break;
                case 3: DrawXpIcon(canvas, 0, 0, iconSize); break;
                case 4: DrawScrewIcon(canvas, 0, 0, iconSize); break;
                case 5: DrawSpeedIcon(canvas, 0, 0, iconSize); break;
                case 6: DrawToolIcon(canvas, 0, 0, iconSize); break;
                case 7: DrawJackpotIcon(canvas, 0, 0, iconSize); break;
            }

            canvas.Restore();
        }
    }

    /// <summary>
    /// Münze mit €-Symbol. amount=1 klein, 2 mittel (€€), 3 groß (€€€ + Glow).
    /// </summary>
    private static void DrawMoneyIcon(SKCanvas canvas, float cx, float cy, float size, int amount)
    {
        // Glow-Halo bei amount=3 (MoneyLarge) - dynamischer Blur-Radius, daher using
        if (amount == 3)
        {
            using var glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.25f);
            using var glowPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0xFF, 0xD7, 0x00, 80),
                MaskFilter = glowFilter
            };
            canvas.DrawCircle(cx, cy, size * 0.9f, glowPaint);
        }

        float coinRadius = size * (0.55f + amount * 0.08f);

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.DrawCircle(cx + 1, cy + 1, coinRadius, _shadowPaint);

        // Münzkörper (goldener Gradient)
        using var coinPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f),
                coinRadius * 1.5f,
                [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xCC, 0x99, 0x00)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, coinRadius, coinPaint);

        // Münzrand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.06f,
            Color = new SKColor(0xB7, 0x8C, 0x00)
        };
        canvas.DrawCircle(cx, cy, coinRadius * 0.85f, borderPaint);

        // €-Text in der Münze
        string euroText = amount switch
        {
            1 => "\u20AC",
            2 => "\u20AC\u20AC",
            _ => "\u20AC\u20AC\u20AC"
        };
        float textSize = amount switch
        {
            1 => coinRadius * 1.1f,
            2 => coinRadius * 0.72f,
            _ => coinRadius * 0.52f
        };

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x6D, 0x4C, 0x00),
            TextSize = textSize,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        // Vertikal zentrieren
        var textBounds = new SKRect();
        textPaint.MeasureText(euroText, ref textBounds);
        canvas.DrawText(euroText, cx, cy + textBounds.Height * 0.35f, textPaint);

        // Glanz-Highlight oben links (gecachter MaskFilter)
        _glintPaint.Color = SKColors.White.WithAlpha(90);
        canvas.DrawCircle(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f,
            coinRadius * 0.22f, _glintPaint);
    }

    /// <summary>
    /// 5-zackiger Stern mit "XP"-Text in der Mitte.
    /// </summary>
    private static void DrawXpIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float outerR = size * 0.75f;
        float innerR = outerR * 0.42f;

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        using var starPath = CreateStarPath(cx + 1, cy + 1, outerR, innerR, 5);
        canvas.DrawPath(starPath, _shadowPaint);

        // Stern-Körper (weiß mit leichtem Blau-Gradient)
        using var starFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy - outerR * 0.2f),
                outerR * 1.2f,
                [SKColors.White, new SKColor(0xBB, 0xDE, 0xFB)],
                null,
                SKShaderTileMode.Clamp)
        };
        using var starMainPath = CreateStarPath(cx, cy, outerR, innerR, 5);
        canvas.DrawPath(starMainPath, starFill);

        // Stern-Rand
        using var starBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.04f,
            Color = new SKColor(0x0D, 0x47, 0xA1)
        };
        canvas.DrawPath(starMainPath, starBorder);

        // "XP" Text
        using var xpPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x0D, 0x47, 0xA1),
            TextSize = innerR * 1.0f,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        var tb = new SKRect();
        xpPaint.MeasureText("XP", ref tb);
        canvas.DrawText("XP", cx, cy + tb.Height * 0.35f, xpPaint);
    }

    /// <summary>
    /// Goldene Sechskant-Schraube von oben gesehen.
    /// </summary>
    private static void DrawScrewIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float hexR = size * 0.65f;
        float slotW = size * 0.12f;
        float slotH = size * 0.75f;

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        using var hexShadow = CreateHexPath(cx + 1, cy + 1, hexR);
        canvas.DrawPath(hexShadow, _shadowPaint);

        // Sechskant-Körper (goldener Gradient)
        using var hexPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx - hexR, cy - hexR),
                new SKPoint(cx + hexR, cy + hexR),
                [new SKColor(0xFF, 0xE0, 0x82), new SKColor(0xCC, 0x99, 0x00)],
                null,
                SKShaderTileMode.Clamp)
        };
        using var hexPath = CreateHexPath(cx, cy, hexR);
        canvas.DrawPath(hexPath, hexPaint);

        // Sechskant-Rand
        using var hexBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.05f,
            Color = new SKColor(0x8C, 0x6D, 0x00)
        };
        canvas.DrawPath(hexPath, hexBorder);

        // Kreuzschlitz in der Mitte (Phillips-Kreuz)
        using var slotPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x6D, 0x4C, 0x00)
        };
        // Vertikaler Schlitz
        canvas.DrawRect(cx - slotW / 2, cy - slotH / 2, slotW, slotH, slotPaint);
        // Horizontaler Schlitz
        canvas.DrawRect(cx - slotH / 2, cy - slotW / 2, slotH, slotW, slotPaint);

        // Glanz (gecachter MaskFilter)
        _glintPaint.Color = SKColors.White.WithAlpha(70);
        canvas.DrawCircle(cx - hexR * 0.25f, cy - hexR * 0.3f, hexR * 0.2f, _glintPaint);
    }

    /// <summary>
    /// Blitz-Symbol (Lightning Bolt) für SpeedBoost.
    /// </summary>
    private static void DrawSpeedIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.8f;

        // Blitz-Pfad
        using var boltPath = new SKPath();
        boltPath.MoveTo(cx + s * 0.10f, cy - s * 0.65f);  // Obere Spitze
        boltPath.LineTo(cx - s * 0.35f, cy + s * 0.05f);   // Links Mitte
        boltPath.LineTo(cx - s * 0.02f, cy + s * 0.05f);   // Knick links
        boltPath.LineTo(cx - s * 0.15f, cy + s * 0.65f);   // Untere Spitze
        boltPath.LineTo(cx + s * 0.35f, cy - s * 0.05f);   // Rechts Mitte
        boltPath.LineTo(cx + s * 0.05f, cy - s * 0.05f);   // Knick rechts
        boltPath.Close();

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.Save();
        canvas.Translate(1, 1);
        canvas.DrawPath(boltPath, _shadowPaint);
        canvas.Restore();

        // Blitz-Füllung (weiß-gelber Gradient)
        using var boltFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy - s * 0.65f),
                new SKPoint(cx, cy + s * 0.65f),
                [SKColors.White, new SKColor(0xFF, 0xF1, 0x76)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(boltPath, boltFill);

        // Blitz-Rand
        using var boltBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.04f,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(0x00, 0x6B, 0x76)
        };
        canvas.DrawPath(boltPath, boltBorder);

        // Kleiner Glow in der Mitte (dynamischer Blur-Radius, daher using)
        using var glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.12f);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(100),
            MaskFilter = glowFilter
        };
        canvas.DrawCircle(cx, cy, s * 0.15f, glowPaint);
    }

    /// <summary>
    /// Gekreuzter Hammer + Schraubenschlüssel für ToolUpgrade.
    /// </summary>
    private static void DrawToolIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.7f;
        float thick = s * 0.16f;

        // Schatten (gecachter MaskFilter, nur Farbe setzen)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        // shadowPaint-Referenz für lokale Verwendung
        var shadowPaint = _shadowPaint;

        // Werkzeug-Paint (weiß)
        using var toolPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        // Rand-Paint
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.03f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(0x4E, 0x34, 0x2E)
        };

        // --- Hammer (links unten nach rechts oben geneigt) ---
        canvas.Save();
        canvas.RotateDegrees(-35, cx, cy);

        // Hammer-Stiel
        using var hammerStiel = new SKPath();
        hammerStiel.AddRect(new SKRect(cx - thick * 0.4f, cy - s * 0.15f,
            cx + thick * 0.4f, cy + s * 0.7f));
        canvas.DrawPath(hammerStiel, shadowPaint);
        canvas.DrawPath(hammerStiel, toolPaint);

        // Hammer-Kopf (breites Rechteck oben)
        using var hammerHead = new SKPath();
        hammerHead.AddRect(new SKRect(cx - s * 0.32f, cy - s * 0.45f,
            cx + s * 0.32f, cy - s * 0.15f));
        canvas.DrawPath(hammerHead, shadowPaint);

        using var headPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx - s * 0.32f, cy - s * 0.45f),
                new SKPoint(cx + s * 0.32f, cy - s * 0.15f),
                [new SKColor(0xE0, 0xE0, 0xE0), new SKColor(0x90, 0x90, 0x90)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(hammerHead, headPaint);
        canvas.DrawPath(hammerHead, borderPaint);
        canvas.DrawPath(hammerStiel, borderPaint);

        canvas.Restore();

        // --- Schraubenschlüssel (rechts unten nach links oben geneigt) ---
        canvas.Save();
        canvas.RotateDegrees(35, cx, cy);

        // Schlüssel-Schaft
        using var shaftPath = new SKPath();
        shaftPath.AddRect(new SKRect(cx - thick * 0.35f, cy - s * 0.1f,
            cx + thick * 0.35f, cy + s * 0.7f));
        canvas.DrawPath(shaftPath, shadowPaint);

        using var shaftPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx - thick, cy),
                new SKPoint(cx + thick, cy),
                [new SKColor(0xD0, 0xD0, 0xD0), new SKColor(0x80, 0x80, 0x80)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(shaftPath, shaftPaint);
        canvas.DrawPath(shaftPath, borderPaint);

        // Schlüssel-Maul oben (U-Form)
        float maulW = s * 0.32f;
        float maulH = s * 0.28f;
        float maulGap = s * 0.12f;

        using var maulPath = new SKPath();
        maulPath.MoveTo(cx - maulW, cy - s * 0.1f);
        maulPath.LineTo(cx - maulW, cy - s * 0.1f - maulH);
        maulPath.LineTo(cx - maulGap, cy - s * 0.1f - maulH);
        maulPath.LineTo(cx - maulGap, cy - s * 0.1f - maulH * 0.45f);
        maulPath.LineTo(cx + maulGap, cy - s * 0.1f - maulH * 0.45f);
        maulPath.LineTo(cx + maulGap, cy - s * 0.1f - maulH);
        maulPath.LineTo(cx + maulW, cy - s * 0.1f - maulH);
        maulPath.LineTo(cx + maulW, cy - s * 0.1f);
        maulPath.Close();
        canvas.DrawPath(maulPath, shadowPaint);
        canvas.DrawPath(maulPath, shaftPaint);
        canvas.DrawPath(maulPath, borderPaint);

        canvas.Restore();
    }

    /// <summary>
    /// Krone mit Strahlen für Jackpot.
    /// </summary>
    private static void DrawJackpotIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.75f;

        // Strahlen hinter der Krone
        using var rayPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.04f,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(0xFF, 0xF1, 0x76, 140)
        };
        int rayCount = 8;
        float rayInner = s * 0.75f;
        float rayOuter = s * 1.05f;
        for (int r = 0; r < rayCount; r++)
        {
            float angle = r * 360f / rayCount * MathF.PI / 180f;
            float x1 = cx + rayInner * MathF.Cos(angle);
            float y1 = cy + rayInner * MathF.Sin(angle);
            float x2 = cx + rayOuter * MathF.Cos(angle);
            float y2 = cy + rayOuter * MathF.Sin(angle);
            canvas.DrawLine(x1, y1, x2, y2, rayPaint);
        }

        // Kronen-Pfad
        using var crownPath = new SKPath();
        float baseY = cy + s * 0.35f;
        float topY = cy - s * 0.5f;
        float w = s * 0.7f;

        // Basis der Krone (unten)
        crownPath.MoveTo(cx - w, baseY);

        // Linke Zacke
        crownPath.LineTo(cx - w * 0.8f, topY + s * 0.2f);
        crownPath.LineTo(cx - w * 0.5f, topY);

        // Mittlere Zacke (höchste)
        crownPath.LineTo(cx - w * 0.15f, topY + s * 0.25f);
        crownPath.LineTo(cx, topY - s * 0.1f);
        crownPath.LineTo(cx + w * 0.15f, topY + s * 0.25f);

        // Rechte Zacke
        crownPath.LineTo(cx + w * 0.5f, topY);
        crownPath.LineTo(cx + w * 0.8f, topY + s * 0.2f);

        crownPath.LineTo(cx + w, baseY);
        crownPath.Close();

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.Save();
        canvas.Translate(1, 1);
        canvas.DrawPath(crownPath, _shadowPaint);
        canvas.Restore();

        // Kronen-Füllung (Gold-Gradient)
        using var crownFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, topY),
                new SKPoint(cx, baseY),
                [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xFF, 0xB3, 0x00)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(crownPath, crownFill);

        // Kronen-Rand
        using var crownBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.04f,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(0xB7, 0x6E, 0x00)
        };
        canvas.DrawPath(crownPath, crownBorder);

        // Band an der Basis
        using var bandPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xCC, 0x00, 0x00)
        };
        canvas.DrawRect(cx - w * 0.95f, baseY - s * 0.12f, w * 1.9f, s * 0.12f, bandPaint);

        // Juwelen auf den Zacken (3 kleine Kreise)
        using var jewelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xE5, 0x3E, 0x3E)
        };
        using var jewelBorder = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.02f,
            Color = new SKColor(0xB7, 0x6E, 0x00)
        };
        float jSize = s * 0.08f;
        float[] jx = [cx - w * 0.5f, cx, cx + w * 0.5f];
        float[] jy = [topY + s * 0.02f, topY - s * 0.08f, topY + s * 0.02f];
        for (int j = 0; j < 3; j++)
        {
            canvas.DrawCircle(jx[j], jy[j], jSize, jewelPaint);
            canvas.DrawCircle(jx[j], jy[j], jSize, jewelBorder);
        }
    }

    /// <summary>
    /// Zeichnet die dekorative Nabe in der Mitte des Rades.
    /// </summary>
    private static void DrawHub(SKCanvas canvas, float cx, float cy, float radius)
    {
        float hubRadius = radius * 0.15f;

        // Schatten der Nabe (gecachter MaskFilter)
        using var hubShadow = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x00, 0x00, 0x00, 70),
            MaskFilter = _blurGlow4
        };
        canvas.DrawCircle(cx + 2, cy + 2, hubRadius, hubShadow);

        // Naben-Gradient (oben heller, unten dunkler für 3D)
        using var hubPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy - hubRadius),
                new SKPoint(cx, cy + hubRadius),
                [HubHighlight, HubColor],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, hubRadius, hubPaint);

        // Naben-Ring (Metallrand)
        using var hubRingPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = new SKColor(0x78, 0x90, 0x9C)
        };
        canvas.DrawCircle(cx, cy, hubRadius, hubRingPaint);

        // Innerer Glanz-Punkt (gecachter MaskFilter)
        float glintRadius = hubRadius * 0.3f;
        _shadowPaint.Color = SKColors.White.WithAlpha(60);
        canvas.DrawCircle(cx - hubRadius * 0.2f, cy - hubRadius * 0.25f, glintRadius, _shadowPaint);

        // Kleiner Schraub-Punkt in der Mitte
        using var screwPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x37, 0x47, 0x4F)
        };
        canvas.DrawCircle(cx, cy, hubRadius * 0.18f, screwPaint);
    }

    /// <summary>
    /// Zeichnet den goldenen Zeiger oben mittig, der nach unten ins Rad zeigt.
    /// </summary>
    private static void DrawPointer(SKCanvas canvas, float cx, float topY, float height)
    {
        float halfWidth = height * 0.55f;

        // Zeiger-Schatten (gecachter MaskFilter)
        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x00, 0x00, 0x00, 80),
            MaskFilter = _blurShadow3
        };
        using var shadowPath = new SKPath();
        shadowPath.MoveTo(cx + 2, topY + height + 4);          // Spitze unten (versetzt)
        shadowPath.LineTo(cx - halfWidth + 2, topY + 4);       // Links oben
        shadowPath.LineTo(cx + halfWidth + 2, topY + 4);       // Rechts oben
        shadowPath.Close();
        canvas.DrawPath(shadowPath, shadowPaint);

        // Zeiger-Gradient (Gold mit Tiefe)
        using var pointerPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx - halfWidth, topY),
                new SKPoint(cx + halfWidth, topY),
                [PointerColor, PointerShadow, PointerColor],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp)
        };
        using var pointerPath = new SKPath();
        pointerPath.MoveTo(cx, topY + height);      // Spitze unten (zeigt ins Rad)
        pointerPath.LineTo(cx - halfWidth, topY);    // Links oben
        pointerPath.LineTo(cx + halfWidth, topY);    // Rechts oben
        pointerPath.Close();
        canvas.DrawPath(pointerPath, pointerPaint);

        // Rand des Zeigers
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xB7, 0x8C, 0x00)
        };
        canvas.DrawPath(pointerPath, borderPaint);

        // Glanz-Highlight auf der linken Seite
        using var glintPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = SKColors.White.WithAlpha(120)
        };
        canvas.DrawLine(cx - halfWidth + 3, topY + 2, cx - 1, topY + height - 3, glintPaint);
    }

    // --- Hilfsmethoden ---

    /// <summary>
    /// Erstellt einen Stern-Pfad mit n Zacken.
    /// </summary>
    private static SKPath CreateStarPath(float cx, float cy, float outerR, float innerR, int points)
    {
        var path = new SKPath();
        float angleStep = MathF.PI / points;
        float startOffset = -MathF.PI / 2f; // Start oben

        for (int i = 0; i < points * 2; i++)
        {
            float r = i % 2 == 0 ? outerR : innerR;
            float angle = startOffset + i * angleStep;
            float x = cx + r * MathF.Cos(angle);
            float y = cy + r * MathF.Sin(angle);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

    /// <summary>
    /// Erstellt einen regelmäßigen Sechskant-Pfad.
    /// </summary>
    private static SKPath CreateHexPath(float cx, float cy, float radius)
    {
        var path = new SKPath();
        for (int i = 0; i < 6; i++)
        {
            float angle = (60f * i - 30f) * MathF.PI / 180f;
            float x = cx + radius * MathF.Cos(angle);
            float y = cy + radius * MathF.Sin(angle);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }
}
