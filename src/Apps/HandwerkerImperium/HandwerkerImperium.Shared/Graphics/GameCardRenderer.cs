using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Level-basierte Rahmen-Stufe fuer Karten und Items.
/// </summary>
public enum CardFrameTier
{
    None,    // Kein spezieller Rahmen
    Bronze,  // < Lv50
    Silver,  // < Lv250
    Gold,    // < Lv500
    Diamond  // >= Lv500
}

/// <summary>
/// Shared Card-Rendering-Utilities im Handwerker-Game-Stil.
/// Bietet wiederverwendbare Elemente: Holz-Rahmen, Metall-Nieten,
/// Level-Rahmen (Bronze/Silber/Gold/Diamant), Progress-Bar mit Glow-Kopf,
/// 3D-Buttons, illustrierte Waehrungs-Icons, Ribbon-Banner.
/// Alle Methoden statisch, gecachte SKPaint-Objekte.
/// </summary>
public static class GameCardRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // Farben
    // ═══════════════════════════════════════════════════════════════════════

    // Holz-Rahmen
    private static readonly SKColor WoodDark = new(0x5D, 0x40, 0x37);
    private static readonly SKColor WoodMedium = new(0x8D, 0x6E, 0x63);
    private static readonly SKColor WoodLight = new(0xD7, 0xB2, 0x8A);

    // Metall-Nieten
    private static readonly SKColor MetalBase = new(0x78, 0x90, 0x9C);
    private static readonly SKColor MetalHighlight = new(0xB0, 0xBE, 0xC5);
    private static readonly SKColor MetalShadow = new(0x45, 0x5A, 0x64);

    // Rahmen-Stufen
    private static readonly SKColor BronzeColor = new(0xCD, 0x7F, 0x32);
    private static readonly SKColor SilverColor = new(0xC0, 0xC0, 0xC0);
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor DiamondColor = new(0xB9, 0xF2, 0xFF);

    // Waehrung
    private static readonly SKColor CoinGold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor CoinDark = new(0xCC, 0xA3, 0x00);
    private static readonly SKColor ScrewGold = new(0xFF, 0xD7, 0x00);

    // ═══════════════════════════════════════════════════════════════════════
    // Gecachte Paints
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _fillPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _strokePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };

    private static readonly SKPaint _progressPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _glowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private static readonly SKPaint _textPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = SKColors.White
    };

    // ═══════════════════════════════════════════════════════════════════════
    // Rahmen-Stufe bestimmen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt die Rahmen-Stufe basierend auf dem Level zurueck.
    /// </summary>
    public static CardFrameTier GetFrameTier(int level)
    {
        if (level >= 500) return CardFrameTier.Diamond;
        if (level >= 250) return CardFrameTier.Gold;
        if (level >= 100) return CardFrameTier.Silver;
        if (level >= 50) return CardFrameTier.Bronze;
        return CardFrameTier.None;
    }

    /// <summary>
    /// Gibt die Farbe fuer die angegebene Rahmen-Stufe zurueck.
    /// </summary>
    public static SKColor GetFrameColor(CardFrameTier tier) => tier switch
    {
        CardFrameTier.Bronze => BronzeColor,
        CardFrameTier.Silver => SilverColor,
        CardFrameTier.Gold => GoldColor,
        CardFrameTier.Diamond => DiamondColor,
        _ => WoodDark
    };

    // ═══════════════════════════════════════════════════════════════════════
    // Holz-Rahmen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen Holz-Rahmen mit optionaler Level-Stufe und Nieten.
    /// </summary>
    public static void DrawWoodFrame(SKCanvas canvas, SKRect bounds, CardFrameTier tier, float cornerRadius = 10f, float frameWidth = 3f)
    {
        // Aeusserer Rahmen (Holz dunkel)
        _fillPaint.Color = WoodDark;
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _fillPaint);

        // Innerer Bereich (transparent = Content-Flaeche)
        var innerBounds = new SKRect(
            bounds.Left + frameWidth,
            bounds.Top + frameWidth,
            bounds.Right - frameWidth,
            bounds.Bottom - frameWidth);

        // Holz-Maserung auf dem Rahmen (subtile horizontale Linien)
        _strokePaint.Color = WoodMedium.WithAlpha(60);
        _strokePaint.StrokeWidth = 0.5f;
        for (float y = bounds.Top + 4f; y < bounds.Bottom - 4f; y += 3f)
        {
            float wave = MathF.Sin(y * 0.1f) * 1f;
            canvas.DrawLine(bounds.Left + 2f + wave, y, bounds.Right - 2f + wave, y, _strokePaint);
        }

        // Highlight am oberen Rand (Holz-Glanz)
        _strokePaint.Color = WoodLight.WithAlpha(40);
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(bounds.Left + cornerRadius, bounds.Top + 1f, bounds.Right - cornerRadius, bounds.Top + 1f, _strokePaint);

        // Schatten am unteren Rand
        _strokePaint.Color = new SKColor(0, 0, 0, 40);
        canvas.DrawLine(bounds.Left + cornerRadius, bounds.Bottom - 1f, bounds.Right - cornerRadius, bounds.Bottom - 1f, _strokePaint);

        // Level-Stufe: Farbiger Rand-Overlay
        if (tier != CardFrameTier.None)
        {
            var tierColor = GetFrameColor(tier);
            _strokePaint.Color = tierColor.WithAlpha(tier == CardFrameTier.Diamond ? (byte)180 : (byte)140);
            _strokePaint.StrokeWidth = tier >= CardFrameTier.Gold ? 2f : 1.5f;
            canvas.DrawRoundRect(bounds.Left + 0.5f, bounds.Top + 0.5f,
                bounds.Width - 1f, bounds.Height - 1f,
                cornerRadius, cornerRadius, _strokePaint);
        }

        // Metall-Nieten in den 4 Ecken
        DrawRivet(canvas, bounds.Left + 6f, bounds.Top + 6f);
        DrawRivet(canvas, bounds.Right - 6f, bounds.Top + 6f);
        DrawRivet(canvas, bounds.Left + 6f, bounds.Bottom - 6f);
        DrawRivet(canvas, bounds.Right - 6f, bounds.Bottom - 6f);
    }

    /// <summary>
    /// Zeichnet eine einzelne Metall-Niete mit Highlight und Schatten.
    /// </summary>
    public static void DrawRivet(SKCanvas canvas, float x, float y, float radius = 2.5f)
    {
        // Basis
        _fillPaint.Color = MetalBase;
        canvas.DrawCircle(x, y, radius, _fillPaint);

        // Highlight (oben-links)
        _fillPaint.Color = MetalHighlight;
        canvas.DrawCircle(x - radius * 0.25f, y - radius * 0.25f, radius * 0.45f, _fillPaint);

        // Schatten (unten-rechts)
        _fillPaint.Color = MetalShadow;
        canvas.DrawCircle(x + radius * 0.2f, y + radius * 0.2f, radius * 0.3f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Progress-Bar mit Glow-Kopf
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine Progress-Bar mit Gradient-Fuellung und leuchtendem Kopf.
    /// </summary>
    public static void DrawProgressBar(SKCanvas canvas, SKRect bounds, float progress,
        SKColor barColorStart, SKColor barColorEnd, float height = 6f)
    {
        progress = Math.Clamp(progress, 0f, 1f);

        float barY = bounds.MidY - height / 2f;
        float barW = bounds.Width;
        float cornerR = height / 2f;

        // Hintergrund (dunkel)
        _fillPaint.Color = new SKColor(0x20, 0x20, 0x20, 180);
        canvas.DrawRoundRect(bounds.Left, barY, barW, height, cornerR, cornerR, _fillPaint);

        if (progress < 0.01f) return;

        // Gradient-Fuellung
        float fillW = barW * progress;
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, barY),
            new SKPoint(bounds.Left + fillW, barY),
            new[] { barColorStart, barColorEnd },
            null,
            SKShaderTileMode.Clamp);

        _progressPaint.Shader = shader;
        canvas.DrawRoundRect(bounds.Left, barY, fillW, height, cornerR, cornerR, _progressPaint);
        _progressPaint.Shader = null;

        // Glanz-Streifen oben (33% Hoehe)
        float glanzH = height * 0.33f;
        _fillPaint.Color = new SKColor(255, 255, 255, 60);
        canvas.DrawRoundRect(bounds.Left + 1f, barY + 1f, fillW - 2f, glanzH, cornerR * 0.5f, cornerR * 0.5f, _fillPaint);

        // Glow-Kopf (leuchtender Kreis am Ende des Balkens)
        if (progress > 0.03f && progress < 0.98f)
        {
            float glowX = bounds.Left + fillW;
            float glowY = barY + height / 2f;
            float glowR = height * 1.2f;

            _glowPaint.Color = barColorEnd.WithAlpha(80);
            _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
            canvas.DrawCircle(glowX, glowY, glowR, _glowPaint);
            _glowPaint.MaskFilter = null;

            // Heller Kern
            _fillPaint.Color = barColorEnd.WithAlpha(200);
            canvas.DrawCircle(glowX, glowY, height * 0.4f, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3D-Button
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen 3D-Button mit Bevel-Effekt (heller Rand oben, dunkler unten).
    /// Gibt die Bounds des klickbaren Bereichs zurueck.
    /// </summary>
    public static SKRect Draw3DButton(SKCanvas canvas, SKRect bounds, SKColor baseColor,
        string text, float fontSize = 11f, bool enabled = true, float cornerRadius = 8f)
    {
        byte alpha = enabled ? (byte)255 : (byte)120;

        // Schatten unter dem Button
        _fillPaint.Color = new SKColor(0, 0, 0, (byte)(40 * (alpha / 255f)));
        canvas.DrawRoundRect(bounds.Left + 1f, bounds.Top + 2f, bounds.Width, bounds.Height, cornerRadius, cornerRadius, _fillPaint);

        // Button-Koerper
        _fillPaint.Color = baseColor.WithAlpha(alpha);
        canvas.DrawRoundRect(bounds, cornerRadius, cornerRadius, _fillPaint);

        // Bevel: Heller Rand oben
        _strokePaint.Color = new SKColor(255, 255, 255, (byte)(50 * (alpha / 255f)));
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(bounds.Left + cornerRadius, bounds.Top + 1f, bounds.Right - cornerRadius, bounds.Top + 1f, _strokePaint);

        // Bevel: Dunkler Rand unten
        _strokePaint.Color = new SKColor(0, 0, 0, (byte)(40 * (alpha / 255f)));
        canvas.DrawLine(bounds.Left + cornerRadius, bounds.Bottom - 1f, bounds.Right - cornerRadius, bounds.Bottom - 1f, _strokePaint);

        // Text
        if (!string.IsNullOrEmpty(text))
        {
            _textPaint.Color = enabled ? SKColors.White : new SKColor(200, 200, 200, 160);
            _textPaint.TextSize = fontSize;
            _textPaint.TextAlign = SKTextAlign.Center;
            _textPaint.FakeBoldText = true;

            float textY = bounds.MidY + fontSize * 0.35f;
            canvas.DrawText(text, bounds.MidX, textY, _textPaint);
            _textPaint.FakeBoldText = false;
        }

        return bounds;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Waehrungs-Icons
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine illustrierte Gold-Muenze (Kreis mit Euro-Praegung und Glanz).
    /// </summary>
    public static void DrawCoinIcon(SKCanvas canvas, float x, float y, float radius = 7f)
    {
        // Aeusserer Ring (dunkleres Gold)
        _fillPaint.Color = CoinDark;
        canvas.DrawCircle(x, y, radius, _fillPaint);

        // Innerer Kreis (helles Gold)
        _fillPaint.Color = CoinGold;
        canvas.DrawCircle(x, y, radius * 0.85f, _fillPaint);

        // Euro-Symbol in der Mitte
        _textPaint.Color = CoinDark;
        _textPaint.TextSize = radius * 1.2f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.FakeBoldText = true;
        canvas.DrawText("\u20AC", x, y + radius * 0.4f, _textPaint);
        _textPaint.FakeBoldText = false;

        // Glanz-Highlight (oben-links)
        _fillPaint.Color = new SKColor(255, 255, 255, 100);
        canvas.DrawCircle(x - radius * 0.25f, y - radius * 0.25f, radius * 0.35f, _fillPaint);
    }

    /// <summary>
    /// Zeichnet eine illustrierte Gold-Schraube (Kreis mit Gewinde-Detail).
    /// </summary>
    public static void DrawScrewIcon(SKCanvas canvas, float x, float y, float radius = 7f)
    {
        // Schrauben-Kopf (Gold-Kreis)
        _fillPaint.Color = ScrewGold;
        canvas.DrawCircle(x, y, radius, _fillPaint);

        // Kreuzschlitz
        _strokePaint.Color = CoinDark;
        _strokePaint.StrokeWidth = radius * 0.15f;
        float slotLen = radius * 0.6f;
        canvas.DrawLine(x - slotLen, y, x + slotLen, y, _strokePaint);
        canvas.DrawLine(x, y - slotLen, x, y + slotLen, _strokePaint);
        _strokePaint.StrokeWidth = 1f;

        // Rand (Gewinde-Andeuting: 8 kleine Kerben am Rand)
        _strokePaint.Color = CoinDark.WithAlpha(80);
        _strokePaint.StrokeWidth = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * MathF.PI / 4f;
            float innerR = radius * 0.85f;
            float outerR = radius;
            canvas.DrawLine(
                x + MathF.Cos(angle) * innerR, y + MathF.Sin(angle) * innerR,
                x + MathF.Cos(angle) * outerR, y + MathF.Sin(angle) * outerR,
                _strokePaint);
        }

        // Glanz
        _fillPaint.Color = new SKColor(255, 255, 255, 80);
        canvas.DrawCircle(x - radius * 0.2f, y - radius * 0.25f, radius * 0.3f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Ribbon-Banner
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein dekoratives Ribbon-Banner mit gefalteten Enden.
    /// Text wird zentriert darauf gezeichnet.
    /// </summary>
    public static void DrawRibbonBanner(SKCanvas canvas, float centerX, float centerY,
        float width, float height, SKColor ribbonColor, string text, float fontSize = 11f)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;
        float foldW = height * 0.6f; // Breite der gefalteten Enden

        // Haupt-Banner (SKPath mit 7 Punkten)
        using var path = new SKPath();
        path.MoveTo(centerX - halfW - foldW, centerY);            // Links-Spitze
        path.LineTo(centerX - halfW, centerY - halfH);            // Links-oben
        path.LineTo(centerX + halfW, centerY - halfH);            // Rechts-oben
        path.LineTo(centerX + halfW + foldW, centerY);            // Rechts-Spitze
        path.LineTo(centerX + halfW, centerY + halfH);            // Rechts-unten
        path.LineTo(centerX - halfW, centerY + halfH);            // Links-unten
        path.Close();

        // Banner-Gradient (leichter 3D-Effekt)
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(centerX, centerY - halfH),
            new SKPoint(centerX, centerY + halfH),
            new[] { Lighten(ribbonColor, 0.15f), ribbonColor, Darken(ribbonColor, 0.1f) },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = shader;
        canvas.DrawPath(path, _fillPaint);
        _fillPaint.Shader = null;

        // Falten-Schatten an den Enden
        _fillPaint.Color = new SKColor(0, 0, 0, 40);
        using var foldLeftPath = new SKPath();
        foldLeftPath.MoveTo(centerX - halfW, centerY - halfH);
        foldLeftPath.LineTo(centerX - halfW - foldW * 0.3f, centerY - halfH * 0.7f);
        foldLeftPath.LineTo(centerX - halfW - foldW, centerY);
        foldLeftPath.LineTo(centerX - halfW, centerY - halfH * 0.3f);
        foldLeftPath.Close();
        canvas.DrawPath(foldLeftPath, _fillPaint);

        using var foldRightPath = new SKPath();
        foldRightPath.MoveTo(centerX + halfW, centerY - halfH);
        foldRightPath.LineTo(centerX + halfW + foldW * 0.3f, centerY - halfH * 0.7f);
        foldRightPath.LineTo(centerX + halfW + foldW, centerY);
        foldRightPath.LineTo(centerX + halfW, centerY - halfH * 0.3f);
        foldRightPath.Close();
        canvas.DrawPath(foldRightPath, _fillPaint);

        // Text
        _textPaint.Color = SKColors.White;
        _textPaint.TextSize = fontSize;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.FakeBoldText = true;
        canvas.DrawText(text, centerX, centerY + fontSize * 0.35f, _textPaint);
        _textPaint.FakeBoldText = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Level-Badge
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein goldenes Level-Badge (Schild-Form mit Level-Zahl).
    /// </summary>
    public static void DrawLevelBadge(SKCanvas canvas, float x, float y, int level, float size = 24f)
    {
        float halfW = size * 0.45f;
        float topH = size * 0.3f;
        float botH = size * 0.35f;

        var tier = GetFrameTier(level);
        var badgeColor = tier == CardFrameTier.None ? new SKColor(0x78, 0x71, 0x6C) : GetFrameColor(tier);

        // Schild-Form
        using var path = new SKPath();
        path.MoveTo(x - halfW, y - topH);
        path.LineTo(x + halfW, y - topH);
        path.LineTo(x + halfW, y + botH * 0.4f);
        path.LineTo(x, y + botH);
        path.LineTo(x - halfW, y + botH * 0.4f);
        path.Close();

        // Gradient
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y - topH),
            new SKPoint(x, y + botH),
            new[] { Lighten(badgeColor, 0.3f), badgeColor },
            null,
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = shader;
        canvas.DrawPath(path, _fillPaint);
        _fillPaint.Shader = null;

        // Rand
        _strokePaint.Color = Darken(badgeColor, 0.2f);
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawPath(path, _strokePaint);

        // Level-Zahl
        _textPaint.Color = SKColors.White;
        _textPaint.TextSize = size * 0.35f;
        _textPaint.TextAlign = SKTextAlign.Center;
        _textPaint.FakeBoldText = true;
        canvas.DrawText(level.ToString(), x, y + size * 0.08f, _textPaint);
        _textPaint.FakeBoldText = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Krone (fuer Lv.1000+)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine goldene Krone ueber einem Element.
    /// </summary>
    public static void DrawCrown(SKCanvas canvas, float x, float y, float width = 20f, float height = 14f)
    {
        float halfW = width / 2f;

        using var path = new SKPath();
        // Basis
        path.MoveTo(x - halfW, y + height * 0.6f);
        // Linke Zacke
        path.LineTo(x - halfW, y + height * 0.1f);
        path.LineTo(x - halfW * 0.5f, y + height * 0.35f);
        // Mittlere Zacke (hoechste)
        path.LineTo(x, y);
        path.LineTo(x + halfW * 0.5f, y + height * 0.35f);
        // Rechte Zacke
        path.LineTo(x + halfW, y + height * 0.1f);
        path.LineTo(x + halfW, y + height * 0.6f);
        path.Close();

        // Gold-Gradient
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y),
            new SKPoint(x, y + height),
            new[] { new SKColor(0xFF, 0xE0, 0x40), GoldColor, new SKColor(0xCC, 0xA3, 0x00) },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        _fillPaint.Shader = shader;
        canvas.DrawPath(path, _fillPaint);
        _fillPaint.Shader = null;

        // Edelsteine auf den Zacken (3 kleine Kreise)
        _fillPaint.Color = new SKColor(0xDC, 0x26, 0x26); // Rubinrot
        canvas.DrawCircle(x - halfW * 0.5f, y + height * 0.25f, 1.5f, _fillPaint);
        _fillPaint.Color = new SKColor(0x22, 0xC5, 0x5E); // Smaragdgruen
        canvas.DrawCircle(x, y + height * 0.12f, 1.5f, _fillPaint);
        _fillPaint.Color = new SKColor(0x38, 0x82, 0xF6); // Saphirblau
        canvas.DrawCircle(x + halfW * 0.5f, y + height * 0.25f, 1.5f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Lock-Icon
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Schloss-Symbol (geschlossen oder offen).
    /// </summary>
    public static void DrawLockIcon(SKCanvas canvas, float x, float y, float size = 20f, bool isOpen = false)
    {
        float halfW = size * 0.35f;
        float bodyH = size * 0.45f;
        float shackleH = size * 0.35f;

        // Schloss-Koerper
        _fillPaint.Color = isOpen ? new SKColor(0x22, 0xC5, 0x5E) : new SKColor(0x94, 0xA3, 0xB8);
        canvas.DrawRoundRect(x - halfW, y, halfW * 2, bodyH, 2f, 2f, _fillPaint);

        // Buegel
        _strokePaint.Color = _fillPaint.Color;
        _strokePaint.StrokeWidth = size * 0.1f;
        _strokePaint.StrokeCap = SKStrokeCap.Round;

        float shackleW = halfW * 0.7f;
        if (isOpen)
        {
            // Offener Buegel (linke Seite angehoben)
            using var shacklePath = new SKPath();
            shacklePath.MoveTo(x - shackleW, y);
            shacklePath.LineTo(x - shackleW, y - shackleH * 1.2f);
            shacklePath.ArcTo(new SKRect(x - shackleW, y - shackleH * 1.2f - shackleW, x + shackleW, y - shackleH * 1.2f + shackleW), 180, -90, false);
            canvas.DrawPath(shacklePath, _strokePaint);
            canvas.DrawLine(x + shackleW, y - shackleH * 0.5f, x + shackleW, y, _strokePaint);
        }
        else
        {
            // Geschlossener Buegel
            using var shacklePath = new SKPath();
            shacklePath.MoveTo(x - shackleW, y);
            shacklePath.LineTo(x - shackleW, y - shackleH);
            shacklePath.ArcTo(new SKRect(x - shackleW, y - shackleH - shackleW * 2, x + shackleW, y - shackleH), 180, -180, false);
            shacklePath.LineTo(x + shackleW, y);
            canvas.DrawPath(shacklePath, _strokePaint);
        }

        _strokePaint.StrokeCap = SKStrokeCap.Butt;

        // Schluesselloch
        _fillPaint.Color = new SKColor(0, 0, 0, 100);
        float khY = y + bodyH * 0.35f;
        canvas.DrawCircle(x, khY, bodyH * 0.12f, _fillPaint);
        canvas.DrawRect(x - bodyH * 0.05f, khY, bodyH * 0.1f, bodyH * 0.2f, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helfer
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aufhellen einer Farbe um den angegebenen Faktor (0-1).</summary>
    private static SKColor Lighten(SKColor c, float amount)
    {
        return new SKColor(
            (byte)Math.Min(255, c.Red + (255 - c.Red) * amount),
            (byte)Math.Min(255, c.Green + (255 - c.Green) * amount),
            (byte)Math.Min(255, c.Blue + (255 - c.Blue) * amount),
            c.Alpha);
    }

    /// <summary>Abdunkeln einer Farbe um den angegebenen Faktor (0-1).</summary>
    private static SKColor Darken(SKColor c, float amount)
    {
        return new SKColor(
            (byte)(c.Red * (1f - amount)),
            (byte)(c.Green * (1f - amount)),
            (byte)(c.Blue * (1f - amount)),
            c.Alpha);
    }
}
