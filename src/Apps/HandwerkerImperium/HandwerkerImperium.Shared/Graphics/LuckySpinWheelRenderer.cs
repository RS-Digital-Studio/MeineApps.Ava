using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer für das Glücksrad (Lucky Spin).
/// Zeichnet ein 8-Segment-Rad mit Zeiger, Nabe, Schatten und Glow-Effekt
/// für das gewinnende Segment. Icons werden als SkiaSharp-Pfade gezeichnet
/// (keine Emojis/Unicode, plattformunabhängig).
/// </summary>
public sealed class LuckySpinWheelRenderer : IDisposable
{
    private bool _disposed;
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

    // Instanz-Paints deren Farbe pro Aufruf geaendert wird (nicht static wegen Mutation)
    private readonly SKPaint _shadowPaint = new() { IsAntialias = true, MaskFilter = _blurSmall2 };
    private readonly SKPaint _glintPaint = new() { IsAntialias = true, MaskFilter = _blurSmall1_5 };

    // Instanz-Paints fuer Segment-Rendering (Shader/Color wird pro Aufruf geaendert)
    private readonly SKPaint _segFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _segLinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(0x00, 0x00, 0x00, 80) };
    private static readonly SKPaint _jackpotBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3, Color = JackpotBorderColor };
    private readonly SKPaint _segGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, MaskFilter = _blurGlow10 };
    private static readonly SKPaint _segInnerGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White.WithAlpha(80), MaskFilter = _blurGlow6 };
    private static readonly SKPaint _segBorderGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, Color = SKColors.White.WithAlpha(180), MaskFilter = _blurGlow4 };

    // Gecachte Paints fuer Rim/Hub/Pointer
    private static readonly SKPaint _rimPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = RimColor };
    private static readonly SKPaint _rimHighlightPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = RimHighlight };
    private static readonly SKPaint _rimDotPaint = new() { IsAntialias = true, Color = RimHighlight };
    private static readonly SKPaint _hubShadowPaint = new() { IsAntialias = true, Color = new SKColor(0x00, 0x00, 0x00, 70), MaskFilter = _blurGlow4 };
    private readonly SKPaint _hubFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _hubRingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = new SKColor(0x78, 0x90, 0x9C) };
    private static readonly SKPaint _hubScrewPaint = new() { IsAntialias = true, Color = new SKColor(0x37, 0x47, 0x4F) };
    private static readonly SKPaint _wheelShadowPaint = new() { IsAntialias = true, Color = new SKColor(0x00, 0x00, 0x00, 60), MaskFilter = _blurShadow12 };
    private readonly SKPaint _pointerFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _pointerBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = new SKColor(0xB7, 0x8C, 0x00) };
    private static readonly SKPaint _pointerGlintPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = SKColors.White.WithAlpha(120) };
    private static readonly SKPaint _pointerShadowPaint = new() { IsAntialias = true, Color = new SKColor(0x00, 0x00, 0x00, 80), MaskFilter = _blurShadow3 };

    // Icon-Paints (wiederverwendbar, Color/Shader/StrokeWidth werden vor Verwendung gesetzt)
    // Vermeidet ~22 SKPaint-Allokationen pro Render-Aufruf in den Icon-Methoden
    private readonly SKPaint _iconShaderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _iconBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _iconFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _iconStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _iconTextPaint = new() { IsAntialias = true };
    // Mutierbarer Font fuer Icon-Text (Größe variiert je nach Icon)
    private readonly SKFont _iconTextFont = new() { Embolden = true };

    // Gecachte SKPaths fuer CreateStarPath/CreateHexPath (vermeidet Allokation pro Aufruf)
    private readonly SKPath _starPathCache = new();
    private readonly SKPath _hexPathCache = new();

    // Wiederverwendbare Arrays für DrawJackpotIcon-Juwelen (kein new float[3] pro Frame)
    private static readonly float[] s_jackpotJx = new float[3];
    private static readonly float[] s_jackpotJy = new float[3];

    // --- Shader/Path-Cache (gecacht nach Radius, invalidiert bei Radius-Aenderung) ---

    // Gecachte Paths (Wiederverwendung via Rewind)
    private readonly SKPath _segPath = new();
    private readonly SKPath _iconPathA = new();
    private readonly SKPath _iconPathB = new(); // Zweiter gleichzeitig aktiver Path (DrawToolIcon: hammerHead + hammerStiel)

    // Shader-Cache (invalidiert bei Radius-Aenderung)
    private float _lastCachedRadius;
    private readonly SKShader?[] _segmentShaders = new SKShader?[8];
    private SKShader? _coinShader1, _coinShader2, _coinShader3;
    private SKShader? _starShader, _hexShader, _boltShader;
    private SKShader? _headShader, _shaftShader, _crownShader;
    private SKShader? _hubShader, _pointerShader;
    // MaskFilter mit dynamischem Radius (haengen vom jeweiligen iconSize/s ab)
    private SKMaskFilter? _moneyGlowFilter, _speedGlowFilter;
    // Letzter bekannter iconSize-Wert fuer MaskFilter-Cache-Invalidierung
    private float _lastIconSize;

    /// <summary>
    /// Rendert das Glücksrad auf den Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, double currentAngle, int? highlightedSegment)
    {
        if (_disposed) return;
        canvas.Clear(SKColors.Transparent);

        float padding = 16;
        float available = Math.Min(bounds.Width - padding * 2, bounds.Height - padding * 2);
        float radius = available / 2f;

        // Shader-Cache invalidieren wenn sich der Radius geaendert hat
        if (_lastCachedRadius != radius)
        {
            InvalidateShaderCache();
            _lastCachedRadius = radius;
        }

        float cx = bounds.MidX;
        float cy = bounds.MidY;

        float pointerHeight = radius * 0.14f;
        cy += pointerHeight * 0.3f;

        DrawWheelShadow(canvas, cx, cy, radius);
        DrawRim(canvas, cx, cy, radius);

        canvas.Save();
        canvas.Translate(cx, cy);
        canvas.RotateDegrees((float)currentAngle);
        canvas.Translate(-cx, -cy);

        float innerRadius = radius * 0.88f;
        DrawSegments(canvas, cx, cy, innerRadius, highlightedSegment);
        DrawSegmentIcons(canvas, cx, cy, innerRadius);

        canvas.Restore();

        DrawHub(canvas, cx, cy, radius);
        DrawPointer(canvas, cx, cy - radius + 2, pointerHeight);
    }

    /// <summary>
    /// Zeichnet den Schatten unter dem Rad fuer Tiefenwirkung.
    /// </summary>
    private void DrawWheelShadow(SKCanvas canvas, float cx, float cy, float radius)
    {
        canvas.DrawCircle(cx + 4, cy + 6, radius + 4, _wheelShadowPaint);
    }

    /// <summary>
    /// Zeichnet den aeusseren Ring des Rades.
    /// </summary>
    private void DrawRim(SKCanvas canvas, float cx, float cy, float radius)
    {
        canvas.DrawCircle(cx, cy, radius, _rimPaint);
        canvas.DrawCircle(cx, cy, radius - 1, _rimHighlightPaint);

        // Dekorative Nieten am Rand
        int dotCount = 24;
        float dotRadius = radius * 0.02f;
        for (int i = 0; i < dotCount; i++)
        {
            float angle = i * 360f / dotCount * MathF.PI / 180f;
            float dx = cx + (radius * 0.94f) * MathF.Cos(angle);
            float dy = cy + (radius * 0.94f) * MathF.Sin(angle);
            canvas.DrawCircle(dx, dy, dotRadius, _rimDotPaint);
        }
    }

    /// <summary>
    /// Zeichnet alle 8 Segmente mit Gradient und optionalem Glow-Highlight.
    /// </summary>
    private void DrawSegments(SKCanvas canvas, float cx, float cy, float innerRadius,
        int? highlightedSegment)
    {
        var segmentRect = new SKRect(cx - innerRadius, cy - innerRadius,
            cx + innerRadius, cy + innerRadius);

        for (int i = 0; i < SegmentCount; i++)
        {
            float startAngle = i * SweepAngle - 90;

            // Shader gecacht pro Segment-Index, neu erstellt nur wenn Cache leer (Radius-Aenderung)
            _segmentShaders[i] ??= SKShader.CreateRadialGradient(
                new SKPoint(cx, cy),
                innerRadius,
                [SegmentColors[i], SegmentDarkColors[i]],
                [0.3f, 1.0f],
                SKShaderTileMode.Clamp);
            _segFillPaint.Shader = _segmentShaders[i];

            // Gecachter Path per Rewind statt new SKPath()
            _segPath.Rewind();
            _segPath.MoveTo(cx, cy);
            _segPath.ArcTo(segmentRect, startAngle, SweepAngle, false);
            _segPath.Close();
            canvas.DrawPath(_segPath, _segFillPaint);
            _segFillPaint.Shader = null;

            float lineAngle = startAngle * MathF.PI / 180f;
            canvas.DrawLine(cx, cy,
                cx + innerRadius * MathF.Cos(lineAngle),
                cy + innerRadius * MathF.Sin(lineAngle),
                _segLinePaint);

            if (i == 7)
            {
                // Jackpot-Rahmen: gleichen _segPath nochmal verwenden (noch aktiv)
                _segPath.Rewind();
                _segPath.MoveTo(cx, cy);
                _segPath.ArcTo(segmentRect, startAngle, SweepAngle, false);
                _segPath.Close();
                canvas.DrawPath(_segPath, _jackpotBorderPaint);
            }

            if (highlightedSegment == i)
            {
                DrawSegmentGlow(canvas, cx, cy, innerRadius, startAngle, i);
            }
        }
    }

    /// <summary>
    /// Zeichnet den leuchtenden Glow-Effekt auf dem gewinnenden Segment.
    /// </summary>
    private void DrawSegmentGlow(SKCanvas canvas, float cx, float cy, float innerRadius,
        float startAngle, int segmentIndex)
    {
        var segmentRect = new SKRect(cx - innerRadius, cy - innerRadius,
            cx + innerRadius, cy + innerRadius);

        _segGlowPaint.Color = SegmentColors[segmentIndex].WithAlpha(100);
        // Gecachter Path per Rewind statt new SKPath()
        _segPath.Rewind();
        _segPath.MoveTo(cx, cy);
        _segPath.ArcTo(segmentRect, startAngle, SweepAngle, false);
        _segPath.Close();
        canvas.DrawPath(_segPath, _segGlowPaint);
        canvas.DrawPath(_segPath, _segInnerGlowPaint);
        canvas.DrawPath(_segPath, _segBorderGlowPaint);
    }

    /// <summary>
    /// Zeichnet die SkiaSharp-Icons in die Segmente, ~65% vom Zentrum entfernt.
    /// </summary>
    private void DrawSegmentIcons(SKCanvas canvas, float cx, float cy, float innerRadius)
    {
        float iconSize = innerRadius * 0.17f;

        // MaskFilter mit dynamischem Radius invalidieren wenn iconSize sich aendert
        if (_lastIconSize != iconSize)
        {
            _moneyGlowFilter?.Dispose(); _moneyGlowFilter = null;
            _speedGlowFilter?.Dispose(); _speedGlowFilter = null;
            _lastIconSize = iconSize;
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            float midAngle = (i * SweepAngle + SweepAngle / 2f - 90) * MathF.PI / 180f;
            float iconRadius = innerRadius * 0.65f;
            float ix = cx + iconRadius * MathF.Cos(midAngle);
            float iy = cy + iconRadius * MathF.Sin(midAngle);

            canvas.Save();
            canvas.Translate(ix, iy);
            float rotDeg = i * SweepAngle + SweepAngle / 2f;
            canvas.RotateDegrees(rotDeg);

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
    /// Muenze mit Euro-Symbol. amount=1 klein, 2 mittel, 3 gross (+ Glow).
    /// </summary>
    private void DrawMoneyIcon(SKCanvas canvas, float cx, float cy, float size, int amount)
    {
        // Glow-Halo bei amount=3 (MoneyLarge) - gecachter MaskFilter (Radius abhaengig von size)
        if (amount == 3)
        {
            _moneyGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.25f);
            _iconFillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, 80);
            _iconFillPaint.MaskFilter = _moneyGlowFilter;
            canvas.DrawCircle(cx, cy, size * 0.9f, _iconFillPaint);
            _iconFillPaint.MaskFilter = null;
        }

        float coinRadius = size * (0.55f + amount * 0.08f);

        // Schatten (gecachter MaskFilter)
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.DrawCircle(cx + 1, cy + 1, coinRadius, _shadowPaint);

        // Muenzkoerper (goldener Gradient, gecacht pro amount)
        var coinShader = amount switch
        {
            1 => _coinShader1 ??= SKShader.CreateRadialGradient(
                new SKPoint(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f),
                coinRadius * 1.5f,
                [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xCC, 0x99, 0x00)],
                null,
                SKShaderTileMode.Clamp),
            2 => _coinShader2 ??= SKShader.CreateRadialGradient(
                new SKPoint(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f),
                coinRadius * 1.5f,
                [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xCC, 0x99, 0x00)],
                null,
                SKShaderTileMode.Clamp),
            _ => _coinShader3 ??= SKShader.CreateRadialGradient(
                new SKPoint(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f),
                coinRadius * 1.5f,
                [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xCC, 0x99, 0x00)],
                null,
                SKShaderTileMode.Clamp),
        };
        _iconShaderPaint.Shader = coinShader;
        canvas.DrawCircle(cx, cy, coinRadius, _iconShaderPaint);
        _iconShaderPaint.Shader = null;

        // Muenzrand
        _iconBorderPaint.StrokeWidth = size * 0.06f;
        _iconBorderPaint.Color = new SKColor(0xB7, 0x8C, 0x00);
        canvas.DrawCircle(cx, cy, coinRadius * 0.85f, _iconBorderPaint);

        // Euro-Text in der Muenze
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

        _iconTextPaint.Color = new SKColor(0x6D, 0x4C, 0x00);
        _iconTextFont.Size = textSize;
        _iconTextFont.MeasureText(euroText, out var textBounds);
        canvas.DrawText(euroText, cx, cy + textBounds.Height * 0.35f, SKTextAlign.Center, _iconTextFont, _iconTextPaint);

        // Glanz-Highlight oben links (gecachter MaskFilter)
        _glintPaint.Color = SKColors.White.WithAlpha(90);
        canvas.DrawCircle(cx - coinRadius * 0.3f, cy - coinRadius * 0.3f,
            coinRadius * 0.22f, _glintPaint);
    }

    /// <summary>
    /// 5-zackiger Stern mit "XP"-Text in der Mitte.
    /// </summary>
    private void DrawXpIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float outerR = size * 0.75f;
        float innerR = outerR * 0.42f;

        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        FillStarPath(_starPathCache, cx + 1, cy + 1, outerR, innerR, 5);
        canvas.DrawPath(_starPathCache, _shadowPaint);

        // Stern-Koerper (weiss mit leichtem Blau-Gradient, gecacht)
        _starShader ??= SKShader.CreateRadialGradient(
            new SKPoint(cx, cy - outerR * 0.2f),
            outerR * 1.2f,
            [SKColors.White, new SKColor(0xBB, 0xDE, 0xFB)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _starShader;
        FillStarPath(_starPathCache, cx, cy, outerR, innerR, 5);
        canvas.DrawPath(_starPathCache, _iconShaderPaint);
        _iconShaderPaint.Shader = null;

        // Stern-Rand
        _iconBorderPaint.StrokeWidth = size * 0.04f;
        _iconBorderPaint.Color = new SKColor(0x0D, 0x47, 0xA1);
        canvas.DrawPath(_starPathCache, _iconBorderPaint);

        // "XP" Text
        _iconTextPaint.Color = new SKColor(0x0D, 0x47, 0xA1);
        _iconTextFont.Size = innerR * 1.0f;
        _iconTextFont.MeasureText("XP", out var tb);
        canvas.DrawText("XP", cx, cy + tb.Height * 0.35f, SKTextAlign.Center, _iconTextFont, _iconTextPaint);
    }

    /// <summary>
    /// Goldene Sechskant-Schraube von oben gesehen.
    /// </summary>
    private void DrawScrewIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float hexR = size * 0.65f;
        float slotW = size * 0.12f;
        float slotH = size * 0.75f;

        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        FillHexPath(_hexPathCache, cx + 1, cy + 1, hexR);
        canvas.DrawPath(_hexPathCache, _shadowPaint);

        // Sechskant-Koerper (goldener Gradient, gecacht)
        _hexShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx - hexR, cy - hexR),
            new SKPoint(cx + hexR, cy + hexR),
            [new SKColor(0xFF, 0xE0, 0x82), new SKColor(0xCC, 0x99, 0x00)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _hexShader;
        FillHexPath(_hexPathCache, cx, cy, hexR);
        canvas.DrawPath(_hexPathCache, _iconShaderPaint);
        _iconShaderPaint.Shader = null;

        // Sechskant-Rand
        _iconBorderPaint.StrokeWidth = size * 0.05f;
        _iconBorderPaint.Color = new SKColor(0x8C, 0x6D, 0x00);
        canvas.DrawPath(_hexPathCache, _iconBorderPaint);

        // Kreuzschlitz in der Mitte (Phillips-Kreuz)
        _iconFillPaint.Color = new SKColor(0x6D, 0x4C, 0x00);
        canvas.DrawRect(cx - slotW / 2, cy - slotH / 2, slotW, slotH, _iconFillPaint);
        canvas.DrawRect(cx - slotH / 2, cy - slotW / 2, slotH, slotW, _iconFillPaint);

        // Glanz (gecachter MaskFilter)
        _glintPaint.Color = SKColors.White.WithAlpha(70);
        canvas.DrawCircle(cx - hexR * 0.25f, cy - hexR * 0.3f, hexR * 0.2f, _glintPaint);
    }

    /// <summary>
    /// Blitz-Symbol (Lightning Bolt) fuer SpeedBoost.
    /// </summary>
    private void DrawSpeedIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.8f;

        // Gecachter Path per Rewind statt new SKPath()
        _iconPathA.Rewind();
        _iconPathA.MoveTo(cx + s * 0.10f, cy - s * 0.65f);
        _iconPathA.LineTo(cx - s * 0.35f, cy + s * 0.05f);
        _iconPathA.LineTo(cx - s * 0.02f, cy + s * 0.05f);
        _iconPathA.LineTo(cx - s * 0.15f, cy + s * 0.65f);
        _iconPathA.LineTo(cx + s * 0.35f, cy - s * 0.05f);
        _iconPathA.LineTo(cx + s * 0.05f, cy - s * 0.05f);
        _iconPathA.Close();

        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.Save();
        canvas.Translate(1, 1);
        canvas.DrawPath(_iconPathA, _shadowPaint);
        canvas.Restore();

        // Blitz-Fuellung (weiss-gelber Gradient, gecacht)
        _boltShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - s * 0.65f),
            new SKPoint(cx, cy + s * 0.65f),
            [SKColors.White, new SKColor(0xFF, 0xF1, 0x76)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _boltShader;
        canvas.DrawPath(_iconPathA, _iconShaderPaint);
        _iconShaderPaint.Shader = null;

        // Blitz-Rand
        _iconBorderPaint.StrokeWidth = size * 0.04f;
        _iconBorderPaint.StrokeJoin = SKStrokeJoin.Round;
        _iconBorderPaint.Color = new SKColor(0x00, 0x6B, 0x76);
        canvas.DrawPath(_iconPathA, _iconBorderPaint);

        // Kleiner Glow in der Mitte (gecachter MaskFilter, Radius abhaengig von size)
        _speedGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, size * 0.12f);
        _iconFillPaint.Color = SKColors.White.WithAlpha(100);
        _iconFillPaint.MaskFilter = _speedGlowFilter;
        canvas.DrawCircle(cx, cy, s * 0.15f, _iconFillPaint);
        _iconFillPaint.MaskFilter = null;
    }

    /// <summary>
    /// Gekreuzter Hammer + Schraubenschluessel fuer ToolUpgrade.
    /// </summary>
    private void DrawToolIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.7f;
        float thick = s * 0.16f;

        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);

        // Werkzeug-Fuellfarbe (weiss)
        _iconFillPaint.Color = SKColors.White;

        // Rand-Paint (Holz-Braun, abgerundete Ecken)
        _iconBorderPaint.StrokeWidth = size * 0.03f;
        _iconBorderPaint.StrokeCap = SKStrokeCap.Round;
        _iconBorderPaint.StrokeJoin = SKStrokeJoin.Round;
        _iconBorderPaint.Color = new SKColor(0x4E, 0x34, 0x2E);

        // --- Hammer (links unten nach rechts oben geneigt) ---
        canvas.Save();
        canvas.RotateDegrees(-35, cx, cy);

        // Hammer-Stiel (gecachter Path _iconPathA)
        _iconPathA.Rewind();
        _iconPathA.AddRect(new SKRect(cx - thick * 0.4f, cy - s * 0.15f,
            cx + thick * 0.4f, cy + s * 0.7f));
        canvas.DrawPath(_iconPathA, _shadowPaint);
        canvas.DrawPath(_iconPathA, _iconFillPaint);

        // Hammer-Kopf (gecachter Path _iconPathB, gleichzeitig aktiv neben _iconPathA)
        _iconPathB.Rewind();
        _iconPathB.AddRect(new SKRect(cx - s * 0.32f, cy - s * 0.45f,
            cx + s * 0.32f, cy - s * 0.15f));
        canvas.DrawPath(_iconPathB, _shadowPaint);

        // Hammerkopf-Gradient (gecacht)
        _headShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx - s * 0.32f, cy - s * 0.45f),
            new SKPoint(cx + s * 0.32f, cy - s * 0.15f),
            [new SKColor(0xE0, 0xE0, 0xE0), new SKColor(0x90, 0x90, 0x90)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _headShader;
        canvas.DrawPath(_iconPathB, _iconShaderPaint);
        _iconShaderPaint.Shader = null;
        canvas.DrawPath(_iconPathB, _iconBorderPaint);
        canvas.DrawPath(_iconPathA, _iconBorderPaint);

        canvas.Restore();

        // --- Schraubenschluessel (rechts unten nach links oben geneigt) ---
        canvas.Save();
        canvas.RotateDegrees(35, cx, cy);

        // Schluessel-Schaft (gecachter Path _iconPathA, hammerStiel nicht mehr benoetigt)
        _iconPathA.Rewind();
        _iconPathA.AddRect(new SKRect(cx - thick * 0.35f, cy - s * 0.1f,
            cx + thick * 0.35f, cy + s * 0.7f));
        canvas.DrawPath(_iconPathA, _shadowPaint);

        // Schaft-Gradient (gecacht)
        _shaftShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx - thick, cy),
            new SKPoint(cx + thick, cy),
            [new SKColor(0xD0, 0xD0, 0xD0), new SKColor(0x80, 0x80, 0x80)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _shaftShader;
        canvas.DrawPath(_iconPathA, _iconShaderPaint);
        canvas.DrawPath(_iconPathA, _iconBorderPaint);

        // Schluessel-Maul oben (U-Form)
        float maulW = s * 0.32f;
        float maulH = s * 0.28f;
        float maulGap = s * 0.12f;

        // Schluessel-Maul (gecachter Path _iconPathA, shaftPath bereits gezeichnet)
        _iconPathA.Rewind();
        _iconPathA.MoveTo(cx - maulW, cy - s * 0.1f);
        _iconPathA.LineTo(cx - maulW, cy - s * 0.1f - maulH);
        _iconPathA.LineTo(cx - maulGap, cy - s * 0.1f - maulH);
        _iconPathA.LineTo(cx - maulGap, cy - s * 0.1f - maulH * 0.45f);
        _iconPathA.LineTo(cx + maulGap, cy - s * 0.1f - maulH * 0.45f);
        _iconPathA.LineTo(cx + maulGap, cy - s * 0.1f - maulH);
        _iconPathA.LineTo(cx + maulW, cy - s * 0.1f - maulH);
        _iconPathA.LineTo(cx + maulW, cy - s * 0.1f);
        _iconPathA.Close();
        canvas.DrawPath(_iconPathA, _shadowPaint);
        canvas.DrawPath(_iconPathA, _iconShaderPaint);
        _iconShaderPaint.Shader = null;
        canvas.DrawPath(_iconPathA, _iconBorderPaint);

        canvas.Restore();
    }

    /// <summary>
    /// Krone mit Strahlen fuer Jackpot.
    /// </summary>
    private void DrawJackpotIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float s = size * 0.75f;

        // Strahlen hinter der Krone
        _iconStrokePaint.StrokeWidth = size * 0.04f;
        _iconStrokePaint.StrokeCap = SKStrokeCap.Round;
        _iconStrokePaint.Color = new SKColor(0xFF, 0xF1, 0x76, 140);
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
            canvas.DrawLine(x1, y1, x2, y2, _iconStrokePaint);
        }

        // Kronen-Pfad (gecacht per Rewind statt new SKPath())
        _iconPathA.Rewind();
        float baseY = cy + s * 0.35f;
        float topY = cy - s * 0.5f;
        float w = s * 0.7f;

        _iconPathA.MoveTo(cx - w, baseY);
        _iconPathA.LineTo(cx - w * 0.8f, topY + s * 0.2f);
        _iconPathA.LineTo(cx - w * 0.5f, topY);
        _iconPathA.LineTo(cx - w * 0.15f, topY + s * 0.25f);
        _iconPathA.LineTo(cx, topY - s * 0.1f);
        _iconPathA.LineTo(cx + w * 0.15f, topY + s * 0.25f);
        _iconPathA.LineTo(cx + w * 0.5f, topY);
        _iconPathA.LineTo(cx + w * 0.8f, topY + s * 0.2f);
        _iconPathA.LineTo(cx + w, baseY);
        _iconPathA.Close();

        // Schatten
        _shadowPaint.Color = new SKColor(0x00, 0x00, 0x00, 100);
        canvas.Save();
        canvas.Translate(1, 1);
        canvas.DrawPath(_iconPathA, _shadowPaint);
        canvas.Restore();

        // Kronen-Fuellung (Gold-Gradient, gecacht)
        _crownShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx, topY),
            new SKPoint(cx, baseY),
            [new SKColor(0xFF, 0xF1, 0x76), new SKColor(0xFF, 0xB3, 0x00)],
            null,
            SKShaderTileMode.Clamp);
        _iconShaderPaint.Shader = _crownShader;
        canvas.DrawPath(_iconPathA, _iconShaderPaint);
        _iconShaderPaint.Shader = null;

        // Kronen-Rand
        _iconBorderPaint.StrokeWidth = size * 0.04f;
        _iconBorderPaint.StrokeJoin = SKStrokeJoin.Round;
        _iconBorderPaint.Color = new SKColor(0xB7, 0x6E, 0x00);
        canvas.DrawPath(_iconPathA, _iconBorderPaint);

        // Band an der Basis
        _iconFillPaint.Color = new SKColor(0xCC, 0x00, 0x00);
        canvas.DrawRect(cx - w * 0.95f, baseY - s * 0.12f, w * 1.9f, s * 0.12f, _iconFillPaint);

        // Juwelen auf den Zacken (3 kleine Kreise)
        _iconFillPaint.Color = new SKColor(0xE5, 0x3E, 0x3E);
        _iconBorderPaint.StrokeWidth = size * 0.02f;
        _iconBorderPaint.Color = new SKColor(0xB7, 0x6E, 0x00);
        float jSize = s * 0.08f;
        // Statische Arrays wiederverwenden (kein Heap-Alloc pro Frame)
        s_jackpotJx[0] = cx - w * 0.5f; s_jackpotJx[1] = cx; s_jackpotJx[2] = cx + w * 0.5f;
        s_jackpotJy[0] = topY + s * 0.02f; s_jackpotJy[1] = topY - s * 0.08f; s_jackpotJy[2] = topY + s * 0.02f;
        for (int j = 0; j < 3; j++)
        {
            canvas.DrawCircle(s_jackpotJx[j], s_jackpotJy[j], jSize, _iconFillPaint);
            canvas.DrawCircle(s_jackpotJx[j], s_jackpotJy[j], jSize, _iconBorderPaint);
        }
    }

    /// <summary>
    /// Zeichnet die dekorative Nabe in der Mitte des Rades.
    /// </summary>
    private void DrawHub(SKCanvas canvas, float cx, float cy, float radius)
    {
        float hubRadius = radius * 0.15f;

        canvas.DrawCircle(cx + 2, cy + 2, hubRadius, _hubShadowPaint);

        // Hub-Shader gecacht, kein Dispose des alten Shaders noetig (InvalidateShaderCache erledigt das)
        _hubShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - hubRadius),
            new SKPoint(cx, cy + hubRadius),
            [HubHighlight, HubColor],
            null,
            SKShaderTileMode.Clamp);
        _hubFillPaint.Shader = _hubShader;
        canvas.DrawCircle(cx, cy, hubRadius, _hubFillPaint);
        _hubFillPaint.Shader = null;

        canvas.DrawCircle(cx, cy, hubRadius, _hubRingPaint);

        float glintRadius = hubRadius * 0.3f;
        _shadowPaint.Color = SKColors.White.WithAlpha(60);
        canvas.DrawCircle(cx - hubRadius * 0.2f, cy - hubRadius * 0.25f, glintRadius, _shadowPaint);

        canvas.DrawCircle(cx, cy, hubRadius * 0.18f, _hubScrewPaint);
    }

    /// <summary>
    /// Zeichnet den goldenen Zeiger oben mittig, der nach unten ins Rad zeigt.
    /// </summary>
    private void DrawPointer(SKCanvas canvas, float cx, float topY, float height)
    {
        float halfWidth = height * 0.55f;

        // Shadow-Path (gecacht per Rewind statt new SKPath())
        _iconPathA.Rewind();
        _iconPathA.MoveTo(cx + 2, topY + height + 4);
        _iconPathA.LineTo(cx - halfWidth + 2, topY + 4);
        _iconPathA.LineTo(cx + halfWidth + 2, topY + 4);
        _iconPathA.Close();
        canvas.DrawPath(_iconPathA, _pointerShadowPaint);

        // Pointer-Shader gecacht, kein Dispose des alten Shaders noetig (InvalidateShaderCache erledigt das)
        _pointerShader ??= SKShader.CreateLinearGradient(
            new SKPoint(cx - halfWidth, topY),
            new SKPoint(cx + halfWidth, topY),
            [PointerColor, PointerShadow, PointerColor],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        _pointerFillPaint.Shader = _pointerShader;

        // Pointer-Path (gecacht per Rewind, shadowPath bereits gezeichnet)
        _iconPathA.Rewind();
        _iconPathA.MoveTo(cx, topY + height);
        _iconPathA.LineTo(cx - halfWidth, topY);
        _iconPathA.LineTo(cx + halfWidth, topY);
        _iconPathA.Close();
        canvas.DrawPath(_iconPathA, _pointerFillPaint);
        _pointerFillPaint.Shader = null;

        canvas.DrawPath(_iconPathA, _pointerBorderPaint);
        canvas.DrawLine(cx - halfWidth + 3, topY + 2, cx - 1, topY + height - 3, _pointerGlintPaint);
    }

    // --- Hilfsmethoden ---

    /// <summary>
    /// Befuellt den uebergebenen SKPath mit einem Stern-Pfad (n Zacken).
    /// Der Path wird vor dem Befuellen zurueckgesetzt (Rewind).
    /// </summary>
    private static void FillStarPath(SKPath path, float cx, float cy, float outerR, float innerR, int points)
    {
        path.Rewind();
        float angleStep = MathF.PI / points;
        float startOffset = -MathF.PI / 2f;

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
    }

    /// <summary>
    /// Befuellt den uebergebenen SKPath mit einem regelmaessigen Sechskant.
    /// Der Path wird vor dem Befuellen zurueckgesetzt (Rewind).
    /// </summary>
    private static void FillHexPath(SKPath path, float cx, float cy, float radius)
    {
        path.Rewind();
        for (int i = 0; i < 6; i++)
        {
            float angle = (60f * i - 30f) * MathF.PI / 180f;
            float x = cx + radius * MathF.Cos(angle);
            float y = cy + radius * MathF.Sin(angle);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
    }

    /// <summary>
    /// Gibt alle gecachten Shader und dynamischen MaskFilter frei.
    /// Wird aufgerufen wenn sich der Render-Radius geaendert hat.
    /// </summary>
    private void InvalidateShaderCache()
    {
        for (int i = 0; i < _segmentShaders.Length; i++)
        {
            _segmentShaders[i]?.Dispose();
            _segmentShaders[i] = null;
        }
        _coinShader1?.Dispose(); _coinShader1 = null;
        _coinShader2?.Dispose(); _coinShader2 = null;
        _coinShader3?.Dispose(); _coinShader3 = null;
        _starShader?.Dispose(); _starShader = null;
        _hexShader?.Dispose(); _hexShader = null;
        _boltShader?.Dispose(); _boltShader = null;
        _headShader?.Dispose(); _headShader = null;
        _shaftShader?.Dispose(); _shaftShader = null;
        _crownShader?.Dispose(); _crownShader = null;
        _hubShader?.Dispose(); _hubShader = null;
        _pointerShader?.Dispose(); _pointerShader = null;
        _moneyGlowFilter?.Dispose(); _moneyGlowFilter = null;
        _speedGlowFilter?.Dispose(); _speedGlowFilter = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shadowPaint.Dispose();
        _glintPaint.Dispose();
        _segFillPaint.Dispose();
        _segGlowPaint.Dispose();
        _hubFillPaint.Dispose();
        _pointerFillPaint.Dispose();
        _iconShaderPaint.Dispose();
        _iconBorderPaint.Dispose();
        _iconFillPaint.Dispose();
        _iconStrokePaint.Dispose();
        _iconTextPaint.Dispose();
        _iconTextFont.Dispose();
        _starPathCache.Dispose();
        _hexPathCache.Dispose();

        // Gecachte Paths freigeben
        _segPath.Dispose();
        _iconPathA.Dispose();
        _iconPathB.Dispose();

        // Gecachte Shader + dynamische MaskFilter freigeben
        InvalidateShaderCache();
    }
}
