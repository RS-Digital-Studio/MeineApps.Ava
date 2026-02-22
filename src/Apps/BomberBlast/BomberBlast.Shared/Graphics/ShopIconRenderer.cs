using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Prozedurale SkiaSharp-Illustrationen fuer Shop-Upgrade-Icons.
/// 12 Upgrade-Typen mit detaillierten Cartoon-Grafiken statt simpler Material-Icons.
/// </summary>
public static class ShopIconRenderer
{
    // Gepoolte Paints (keine per-Frame Allokationen)
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _glow = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>
    /// Rendert ein Upgrade-Icon.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="size">Icon-Groesse (Breite/Hoehe)</param>
    /// <param name="upgradeTypeIndex">UpgradeType Index (0-11)</param>
    /// <param name="color">Hauptfarbe des Icons</param>
    public static void Render(SKCanvas canvas, float cx, float cy, float size,
        int upgradeTypeIndex, SKColor color)
    {
        float r = size / 2f;

        switch (upgradeTypeIndex)
        {
            case 0: DrawStartBombs(canvas, cx, cy, r, color); break;
            case 1: DrawStartFire(canvas, cx, cy, r, color); break;
            case 2: DrawStartSpeed(canvas, cx, cy, r, color); break;
            case 3: DrawExtraLives(canvas, cx, cy, r, color); break;
            case 4: DrawScoreMultiplier(canvas, cx, cy, r, color); break;
            case 5: DrawTimeBonus(canvas, cx, cy, r, color); break;
            case 6: DrawShieldStart(canvas, cx, cy, r, color); break;
            case 7: DrawCoinBonus(canvas, cx, cy, r, color); break;
            case 8: DrawPowerUpLuck(canvas, cx, cy, r, color); break;
            case 9: DrawIceBomb(canvas, cx, cy, r, color); break;
            case 10: DrawFireBomb(canvas, cx, cy, r, color); break;
            case 11: DrawStickyBomb(canvas, cx, cy, r, color); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // StartBombs: Detaillierte Bombe mit Zuendschnur und Funken
    // ═══════════════════════════════════════════════════════════════
    private static void DrawStartBombs(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.7f;

        // Schatten
        _fill.Color = new SKColor(0, 0, 0, 40);
        canvas.DrawOval(cx + s * 0.1f, cy + s * 0.85f, s * 0.6f, s * 0.15f, _fill);

        // Bomben-Koerper (Gradient: oben heller, unten dunkler)
        var bodyRect = new SKRect(cx - s, cy - s * 0.6f, cx + s, cy + s * 0.8f);
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.25f, cy - s * 0.1f), s * 1.2f,
            [Lighten(color, 40), color, Darken(color, 60)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawOval(bodyRect, _fill);
        _fill.Shader = null;

        // Highlight (Glanzpunkt oben links)
        _fill.Color = new SKColor(255, 255, 255, 80);
        canvas.DrawOval(cx - s * 0.35f, cy - s * 0.2f, s * 0.25f, s * 0.2f, _fill);

        // Zuendschnur-Huelse oben
        _fill.Color = Darken(color, 30);
        canvas.DrawRect(cx - s * 0.15f, cy - s * 0.8f, s * 0.3f, s * 0.25f, _fill);

        // Zuendschnur (geschwungene Linie)
        _stroke.Color = new SKColor(139, 90, 43);
        _stroke.StrokeWidth = s * 0.08f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        using var fusePath = new SKPath();
        fusePath.MoveTo(cx, cy - s * 0.8f);
        fusePath.CubicTo(cx + s * 0.3f, cy - s * 1.1f,
                         cx + s * 0.5f, cy - s * 1.0f,
                         cx + s * 0.4f, cy - s * 1.2f);
        canvas.DrawPath(fusePath, _stroke);

        // Funken am Ende der Zuendschnur (Stern-Form)
        float sparkX = cx + s * 0.4f, sparkY = cy - s * 1.2f;
        DrawSparkStar(canvas, sparkX, sparkY, s * 0.2f, new SKColor(255, 200, 50));

        // "+1" Beschriftung
        DrawPlusOne(canvas, cx + s * 0.7f, cy + s * 0.5f, s * 0.25f, color);
    }

    // ═══════════════════════════════════════════════════════════════
    // StartFire: Flamme mit Gradient und mehreren Schichten
    // ═══════════════════════════════════════════════════════════════
    private static void DrawStartFire(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.75f;

        // Aeussere Flamme (glow)
        _glow.Color = color.WithAlpha(30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.3f);
        canvas.DrawOval(cx, cy - s * 0.1f, s * 1.1f, s * 1.3f, _glow);
        _glow.MaskFilter = null;

        // Aeussere Flamme
        using var outerFlame = new SKPath();
        outerFlame.MoveTo(cx - s * 0.6f, cy + s * 0.6f);
        outerFlame.CubicTo(cx - s * 0.7f, cy - s * 0.2f,
                          cx - s * 0.2f, cy - s * 0.5f,
                          cx, cy - s * 1.1f);
        outerFlame.CubicTo(cx + s * 0.2f, cy - s * 0.5f,
                          cx + s * 0.7f, cy - s * 0.2f,
                          cx + s * 0.6f, cy + s * 0.6f);
        outerFlame.Close();

        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - s * 1.1f), new SKPoint(cx, cy + s * 0.6f),
            [new SKColor(255, 200, 50), color, Darken(color, 40)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawPath(outerFlame, _fill);
        _fill.Shader = null;

        // Innere Flamme (heller Kern)
        using var innerFlame = new SKPath();
        innerFlame.MoveTo(cx - s * 0.25f, cy + s * 0.4f);
        innerFlame.CubicTo(cx - s * 0.3f, cy,
                          cx - s * 0.1f, cy - s * 0.3f,
                          cx, cy - s * 0.6f);
        innerFlame.CubicTo(cx + s * 0.1f, cy - s * 0.3f,
                          cx + s * 0.3f, cy,
                          cx + s * 0.25f, cy + s * 0.4f);
        innerFlame.Close();

        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - s * 0.6f), new SKPoint(cx, cy + s * 0.4f),
            [SKColors.White, new SKColor(255, 230, 100)],
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(innerFlame, _fill);
        _fill.Shader = null;

        // "+1" Beschriftung
        DrawPlusOne(canvas, cx + s * 0.7f, cy + s * 0.5f, s * 0.22f, color);
    }

    // ═══════════════════════════════════════════════════════════════
    // StartSpeed: Blitz mit Speed-Linien
    // ═══════════════════════════════════════════════════════════════
    private static void DrawStartSpeed(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.75f;

        // Speed-Linien links
        _stroke.Color = color.WithAlpha(120);
        _stroke.StrokeWidth = s * 0.06f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        for (int i = 0; i < 3; i++)
        {
            float ly = cy - s * 0.4f + i * s * 0.4f;
            float lx = cx - s * 0.9f - i * s * 0.1f;
            canvas.DrawLine(lx, ly, lx + s * 0.3f, ly, _stroke);
        }

        // Blitz-Pfad (Zickzack)
        using var bolt = new SKPath();
        bolt.MoveTo(cx + s * 0.1f, cy - s * 1.0f);
        bolt.LineTo(cx - s * 0.3f, cy - s * 0.05f);
        bolt.LineTo(cx + s * 0.1f, cy - s * 0.05f);
        bolt.LineTo(cx - s * 0.2f, cy + s * 1.0f);
        bolt.LineTo(cx + s * 0.5f, cy + s * 0.05f);
        bolt.LineTo(cx + s * 0.1f, cy + s * 0.05f);
        bolt.Close();

        // Glow
        _glow.Color = color.WithAlpha(40);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.25f);
        canvas.DrawPath(bolt, _glow);
        _glow.MaskFilter = null;

        // Blitz-Fuellung
        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - s), new SKPoint(cx, cy + s),
            [Lighten(color, 60), color],
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(bolt, _fill);
        _fill.Shader = null;

        // Heller Kern-Streifen
        _fill.Color = new SKColor(255, 255, 255, 80);
        canvas.DrawRect(cx - s * 0.05f, cy - s * 0.7f, s * 0.1f, s * 0.5f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════
    // ExtraLives: Herz mit Puls-Effekt und +1
    // ═══════════════════════════════════════════════════════════════
    private static void DrawExtraLives(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.75f;

        // Glow
        _glow.Color = color.WithAlpha(35);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.3f);
        DrawHeartPath(canvas, cx, cy, s * 1.15f, _glow);
        _glow.MaskFilter = null;

        // Herz mit Gradient
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.15f, cy - s * 0.15f), s * 1.5f,
            [Lighten(color, 50), color, Darken(color, 50)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        DrawHeartPath(canvas, cx, cy, s, _fill);
        _fill.Shader = null;

        // Highlight oben links
        _fill.Color = new SKColor(255, 255, 255, 70);
        canvas.DrawOval(cx - s * 0.3f, cy - s * 0.3f, s * 0.2f, s * 0.15f, _fill);

        // Puls-Linien (EKG-artig)
        _stroke.Color = new SKColor(255, 255, 255, 100);
        _stroke.StrokeWidth = s * 0.06f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        using var ekg = new SKPath();
        float ex = cx - s * 0.3f, ey = cy + s * 0.05f;
        ekg.MoveTo(ex, ey);
        ekg.LineTo(ex + s * 0.12f, ey);
        ekg.LineTo(ex + s * 0.18f, ey - s * 0.25f);
        ekg.LineTo(ex + s * 0.24f, ey + s * 0.15f);
        ekg.LineTo(ex + s * 0.32f, ey - s * 0.1f);
        ekg.LineTo(ex + s * 0.4f, ey);
        ekg.LineTo(ex + s * 0.55f, ey);
        canvas.DrawPath(ekg, _stroke);

        // "+1"
        DrawPlusOne(canvas, cx + s * 0.65f, cy + s * 0.55f, s * 0.22f, color);
    }

    // ═══════════════════════════════════════════════════════════════
    // ScoreMultiplier: Stern mit "x2" Text und Funkeln
    // ═══════════════════════════════════════════════════════════════
    private static void DrawScoreMultiplier(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.75f;

        // Glow hinter Stern
        _glow.Color = color.WithAlpha(35);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.35f);
        DrawStar(canvas, cx, cy, s * 1.1f, s * 0.5f, 5, _glow);
        _glow.MaskFilter = null;

        // Stern mit Gradient
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.15f, cy - s * 0.2f), s * 1.5f,
            [Lighten(color, 60), color, Darken(color, 40)],
            [0f, 0.45f, 1f], SKShaderTileMode.Clamp);
        DrawStar(canvas, cx, cy, s, s * 0.45f, 5, _fill);
        _fill.Shader = null;

        // "x2" im Zentrum
        using var font = new SKFont { Size = s * 0.5f };
        _fill.Color = SKColors.White;
        canvas.DrawText("x2", cx, cy + s * 0.17f, SKTextAlign.Center, font, _fill);

        // Funkeln-Punkte
        DrawSparkle(canvas, cx + s * 0.7f, cy - s * 0.6f, s * 0.12f, SKColors.White.WithAlpha(200));
        DrawSparkle(canvas, cx - s * 0.65f, cy - s * 0.5f, s * 0.08f, SKColors.White.WithAlpha(160));
    }

    // ═══════════════════════════════════════════════════════════════
    // TimeBonus: Uhr mit beschleunigten Zeigern
    // ═══════════════════════════════════════════════════════════════
    private static void DrawTimeBonus(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.7f;

        // Glow
        _glow.Color = color.WithAlpha(30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.3f);
        canvas.DrawCircle(cx, cy, s * 1.1f, _glow);
        _glow.MaskFilter = null;

        // Uhr-Koerper
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.2f, cy - s * 0.2f), s * 1.5f,
            [Lighten(color, 50), color, Darken(color, 50)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawCircle(cx, cy, s, _fill);
        _fill.Shader = null;

        // Innerer Ring
        _stroke.Color = Darken(color, 30);
        _stroke.StrokeWidth = s * 0.06f;
        canvas.DrawCircle(cx, cy, s * 0.85f, _stroke);

        // Ziffernblatt-Markierungen (12 Striche)
        _stroke.Color = SKColors.White.WithAlpha(180);
        _stroke.StrokeWidth = s * 0.05f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f * MathF.PI / 180f;
            float innerR = i % 3 == 0 ? s * 0.65f : s * 0.72f;
            float outerR = s * 0.82f;
            canvas.DrawLine(
                cx + MathF.Sin(angle) * innerR, cy - MathF.Cos(angle) * innerR,
                cx + MathF.Sin(angle) * outerR, cy - MathF.Cos(angle) * outerR, _stroke);
        }

        // Zeiger (Stunde 10:10 - klassische Uhr-Pose)
        _stroke.Color = SKColors.White;
        _stroke.StrokeWidth = s * 0.08f;
        // Stundenzeiger (10 Uhr = -60 Grad)
        float hAngle = -60f * MathF.PI / 180f;
        canvas.DrawLine(cx, cy, cx + MathF.Sin(hAngle) * s * 0.45f, cy - MathF.Cos(hAngle) * s * 0.45f, _stroke);
        // Minutenzeiger (2 Uhr = 60 Grad)
        _stroke.StrokeWidth = s * 0.05f;
        float mAngle = 60f * MathF.PI / 180f;
        canvas.DrawLine(cx, cy, cx + MathF.Sin(mAngle) * s * 0.65f, cy - MathF.Cos(mAngle) * s * 0.65f, _stroke);

        // Mittel-Punkt
        _fill.Color = SKColors.White;
        canvas.DrawCircle(cx, cy, s * 0.06f, _fill);

        // Speed-Pfeile (doppelter ">>")
        _fill.Color = new SKColor(255, 255, 255, 200);
        float ax = cx + s * 0.55f, ay = cy + s * 0.65f;
        DrawChevron(canvas, ax, ay, s * 0.15f);
        DrawChevron(canvas, ax + s * 0.15f, ay, s * 0.15f);
    }

    // ═══════════════════════════════════════════════════════════════
    // ShieldStart: Schild mit Energie-Aura
    // ═══════════════════════════════════════════════════════════════
    private static void DrawShieldStart(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.75f;

        // Glow-Aura
        _glow.Color = color.WithAlpha(30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.35f);
        DrawShieldPath(canvas, cx, cy, s * 1.15f, _glow);
        _glow.MaskFilter = null;

        // Schild-Koerper mit Gradient
        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx - s, cy - s), new SKPoint(cx + s * 0.5f, cy + s),
            [Lighten(color, 50), color, Darken(color, 40)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        DrawShieldPath(canvas, cx, cy, s, _fill);
        _fill.Shader = null;

        // Rand
        _stroke.Color = Lighten(color, 30);
        _stroke.StrokeWidth = s * 0.07f;
        DrawShieldPath(canvas, cx, cy, s, _stroke);

        // Highlight links oben
        _fill.Color = new SKColor(255, 255, 255, 60);
        canvas.DrawOval(cx - s * 0.25f, cy - s * 0.35f, s * 0.2f, s * 0.3f, _fill);

        // Energie-Kreuz in der Mitte
        _fill.Color = SKColors.White.WithAlpha(180);
        canvas.DrawRoundRect(cx - s * 0.06f, cy - s * 0.35f, s * 0.12f, s * 0.6f, s * 0.03f, s * 0.03f, _fill);
        canvas.DrawRoundRect(cx - s * 0.25f, cy - s * 0.06f, s * 0.5f, s * 0.12f, s * 0.03f, s * 0.03f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════
    // CoinBonus: Muenz-Stapel mit Glow
    // ═══════════════════════════════════════════════════════════════
    private static void DrawCoinBonus(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.7f;

        // 3 Muenzen gestapelt (von unten nach oben)
        for (int i = 0; i < 3; i++)
        {
            float coinY = cy + s * 0.4f - i * s * 0.35f;
            float coinX = cx - s * 0.1f + i * s * 0.05f;

            // Schatten
            _fill.Color = new SKColor(0, 0, 0, 30);
            canvas.DrawOval(coinX, coinY + s * 0.08f, s * 0.45f, s * 0.12f, _fill);

            // Muenz-Seite (Dicke)
            _fill.Color = Darken(color, 30);
            canvas.DrawOval(coinX, coinY + s * 0.04f, s * 0.45f, s * 0.14f, _fill);

            // Muenz-Oberseite
            _fill.Shader = SKShader.CreateRadialGradient(
                new SKPoint(coinX - s * 0.1f, coinY - s * 0.05f), s * 0.6f,
                [Lighten(color, 60), color, Darken(color, 30)],
                [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
            canvas.DrawOval(coinX, coinY, s * 0.45f, s * 0.14f, _fill);
            _fill.Shader = null;

            // Rand
            _stroke.Color = Darken(color, 20);
            _stroke.StrokeWidth = s * 0.03f;
            canvas.DrawOval(coinX, coinY, s * 0.45f, s * 0.14f, _stroke);
        }

        // "%" Symbol rechts oben
        using var font = new SKFont { Size = s * 0.45f };
        _fill.Color = Lighten(color, 40);
        canvas.DrawText("+%", cx + s * 0.55f, cy - s * 0.45f, SKTextAlign.Center, font, _fill);

        // Funkeln
        DrawSparkle(canvas, cx - s * 0.5f, cy - s * 0.55f, s * 0.1f, SKColors.White.WithAlpha(180));
    }

    // ═══════════════════════════════════════════════════════════════
    // PowerUpLuck: Vierblaetriges Kleeblatt mit Gluecksfunken
    // ═══════════════════════════════════════════════════════════════
    private static void DrawPowerUpLuck(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.65f;

        // Glow
        _glow.Color = color.WithAlpha(30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.35f);
        canvas.DrawCircle(cx, cy, s * 1.3f, _glow);
        _glow.MaskFilter = null;

        // 4 Herzfoermige Blaetter (Kleeblatt)
        float leafSize = s * 0.55f;
        float offset = s * 0.3f;
        SKColor darkLeaf = Darken(color, 25);
        SKColor lightLeaf = Lighten(color, 35);

        // Blaetter in 4 Richtungen
        DrawCloverLeaf(canvas, cx, cy - offset, leafSize, 0, color, lightLeaf);
        DrawCloverLeaf(canvas, cx + offset, cy, leafSize, 90, color, lightLeaf);
        DrawCloverLeaf(canvas, cx, cy + offset, leafSize, 180, color, lightLeaf);
        DrawCloverLeaf(canvas, cx - offset, cy, leafSize, 270, darkLeaf, color);

        // Stiel
        _stroke.Color = Darken(color, 40);
        _stroke.StrokeWidth = s * 0.1f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        using var stem = new SKPath();
        stem.MoveTo(cx, cy + s * 0.15f);
        stem.CubicTo(cx + s * 0.15f, cy + s * 0.6f,
                     cx - s * 0.1f, cy + s * 0.8f,
                     cx + s * 0.05f, cy + s * 1.1f);
        canvas.DrawPath(stem, _stroke);

        // Gluecksfunken
        DrawSparkle(canvas, cx + s * 0.65f, cy - s * 0.65f, s * 0.12f, new SKColor(255, 215, 0, 200));
        DrawSparkle(canvas, cx - s * 0.55f, cy - s * 0.45f, s * 0.08f, new SKColor(255, 215, 0, 160));
    }

    // ═══════════════════════════════════════════════════════════════
    // IceBomb: Bombe mit Eiskristallen
    // ═══════════════════════════════════════════════════════════════
    private static void DrawIceBomb(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.65f;

        // Frost-Glow
        _glow.Color = color.WithAlpha(30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.4f);
        canvas.DrawCircle(cx, cy, s * 1.3f, _glow);
        _glow.MaskFilter = null;

        // Bomben-Koerper (eisblau)
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.2f, cy - s * 0.1f), s * 1.3f,
            [Lighten(color, 40), color, Darken(color, 50)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawCircle(cx, cy + s * 0.1f, s * 0.75f, _fill);
        _fill.Shader = null;

        // Eis-Muster auf Bombe (Kristall-Linien)
        _stroke.Color = SKColors.White.WithAlpha(120);
        _stroke.StrokeWidth = s * 0.04f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        // 3 Schneeflocken-Arme
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * MathF.PI / 180f;
            float innerR = s * 0.1f;
            float outerR = s * 0.45f;
            canvas.DrawLine(
                cx + MathF.Sin(angle) * innerR, cy + s * 0.1f - MathF.Cos(angle) * innerR,
                cx + MathF.Sin(angle) * outerR, cy + s * 0.1f - MathF.Cos(angle) * outerR, _stroke);
        }

        // Zuendschnur
        _stroke.Color = new SKColor(139, 90, 43);
        _stroke.StrokeWidth = s * 0.07f;
        canvas.DrawLine(cx, cy - s * 0.55f, cx + s * 0.25f, cy - s * 0.85f, _stroke);

        // Frost-Funke
        DrawSparkStar(canvas, cx + s * 0.25f, cy - s * 0.85f, s * 0.15f, SKColors.White);

        // Eiskristalle drum herum
        DrawSparkle(canvas, cx - s * 0.7f, cy - s * 0.4f, s * 0.1f, SKColors.White.WithAlpha(180));
        DrawSparkle(canvas, cx + s * 0.65f, cy + s * 0.3f, s * 0.08f, SKColors.White.WithAlpha(150));
    }

    // ═══════════════════════════════════════════════════════════════
    // FireBomb: Bombe mit Flammen-Aura und +2 Range
    // ═══════════════════════════════════════════════════════════════
    private static void DrawFireBomb(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.65f;

        // Flammen-Glow
        _glow.Color = new SKColor(255, 100, 0, 30);
        _glow.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s * 0.4f);
        canvas.DrawCircle(cx, cy, s * 1.4f, _glow);
        _glow.MaskFilter = null;

        // Flammen-Aura hinter der Bombe (4 kleine Flammen)
        SKColor flameOuter = new(255, 100, 0);
        SKColor flameInner = new(255, 200, 50);
        for (int i = 0; i < 5; i++)
        {
            float angle = (i * 72f - 90f) * MathF.PI / 180f;
            float fx = cx + MathF.Cos(angle) * s * 0.75f;
            float fy = cy + MathF.Sin(angle) * s * 0.7f;
            DrawMiniFlame(canvas, fx, fy, s * 0.25f, flameOuter, flameInner);
        }

        // Bomben-Koerper (dunkelrot)
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.15f, cy - s * 0.1f), s * 1.2f,
            [Lighten(color, 30), color, Darken(color, 50)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawCircle(cx, cy + s * 0.1f, s * 0.65f, _fill);
        _fill.Shader = null;

        // Glanz
        _fill.Color = new SKColor(255, 255, 255, 50);
        canvas.DrawOval(cx - s * 0.2f, cy - s * 0.1f, s * 0.2f, s * 0.15f, _fill);

        // Zuendschnur
        _stroke.Color = new SKColor(139, 90, 43);
        _stroke.StrokeWidth = s * 0.07f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(cx, cy - s * 0.45f, cx + s * 0.2f, cy - s * 0.7f, _stroke);

        // Feuer-Funke
        DrawSparkStar(canvas, cx + s * 0.2f, cy - s * 0.7f, s * 0.15f, new SKColor(255, 200, 50));

        // "+2" Reichweite-Label
        using var font = new SKFont { Size = s * 0.35f };
        _fill.Color = new SKColor(255, 200, 50);
        canvas.DrawText("+2", cx + s * 0.75f, cy + s * 0.7f, SKTextAlign.Center, font, _fill);
    }

    // ═══════════════════════════════════════════════════════════════
    // StickyBomb: Bombe mit Schleim-Tropfen
    // ═══════════════════════════════════════════════════════════════
    private static void DrawStickyBomb(SKCanvas canvas, float cx, float cy, float r, SKColor color)
    {
        float s = r * 0.65f;

        // Schleim-Pfuetze
        _fill.Color = color.WithAlpha(60);
        canvas.DrawOval(cx, cy + s * 0.75f, s * 0.8f, s * 0.18f, _fill);

        // Schleim-Tropfen die herunterlaufen (3 Stueck)
        DrawSlimeDrop(canvas, cx - s * 0.35f, cy + s * 0.3f, s * 0.18f, color);
        DrawSlimeDrop(canvas, cx + s * 0.4f, cy + s * 0.15f, s * 0.15f, color);
        DrawSlimeDrop(canvas, cx + s * 0.05f, cy + s * 0.5f, s * 0.12f, color);

        // Bomben-Koerper (gruen)
        _fill.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx - s * 0.15f, cy - s * 0.1f), s * 1.2f,
            [Lighten(color, 40), color, Darken(color, 50)],
            [0f, 0.5f, 1f], SKShaderTileMode.Clamp);
        canvas.DrawCircle(cx, cy + s * 0.05f, s * 0.7f, _fill);
        _fill.Shader = null;

        // Schleim-Highlights auf der Bombe
        _fill.Color = Lighten(color, 60).WithAlpha(100);
        canvas.DrawOval(cx - s * 0.2f, cy - s * 0.15f, s * 0.15f, s * 0.1f, _fill);

        // Zuendschnur
        _stroke.Color = new SKColor(139, 90, 43);
        _stroke.StrokeWidth = s * 0.07f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(cx, cy - s * 0.55f, cx + s * 0.2f, cy - s * 0.8f, _stroke);

        // Gruener Funke
        DrawSparkStar(canvas, cx + s * 0.2f, cy - s * 0.8f, s * 0.13f, Lighten(color, 50));

        // Schleim-Faeden von der Bombe nach unten
        _stroke.Color = color.WithAlpha(120);
        _stroke.StrokeWidth = s * 0.04f;
        using var slime1 = new SKPath();
        slime1.MoveTo(cx - s * 0.25f, cy + s * 0.55f);
        slime1.CubicTo(cx - s * 0.3f, cy + s * 0.7f,
                       cx - s * 0.15f, cy + s * 0.8f,
                       cx - s * 0.2f, cy + s * 0.9f);
        canvas.DrawPath(slime1, _stroke);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODEN
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Zeichnet ein "+1" Label</summary>
    private static void DrawPlusOne(SKCanvas canvas, float x, float y, float size, SKColor color)
    {
        using var font = new SKFont { Size = size * 2.5f };
        _fill.Color = SKColors.White;
        // Schatten
        _fill.Color = new SKColor(0, 0, 0, 80);
        canvas.DrawText("+1", x + 1, y + 1, SKTextAlign.Center, font, _fill);
        // Text
        _fill.Color = Lighten(color, 60);
        canvas.DrawText("+1", x, y, SKTextAlign.Center, font, _fill);
    }

    /// <summary>Zeichnet einen Funken-Stern (4-zackig)</summary>
    private static void DrawSparkStar(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        _fill.Color = color;
        using var path = new SKPath();
        // 4 Zacken (Kreuz-foermig)
        path.MoveTo(cx, cy - size);
        path.LineTo(cx + size * 0.2f, cy - size * 0.2f);
        path.LineTo(cx + size, cy);
        path.LineTo(cx + size * 0.2f, cy + size * 0.2f);
        path.LineTo(cx, cy + size);
        path.LineTo(cx - size * 0.2f, cy + size * 0.2f);
        path.LineTo(cx - size, cy);
        path.LineTo(cx - size * 0.2f, cy - size * 0.2f);
        path.Close();
        canvas.DrawPath(path, _fill);

        // Heller Kern
        _fill.Color = SKColors.White;
        canvas.DrawCircle(cx, cy, size * 0.2f, _fill);
    }

    /// <summary>Zeichnet einen kleinen Funkelpunkt (4-zackig, winzig)</summary>
    private static void DrawSparkle(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        _fill.Color = color;
        using var path = new SKPath();
        path.MoveTo(cx, cy - size);
        path.LineTo(cx + size * 0.15f, cy - size * 0.15f);
        path.LineTo(cx + size, cy);
        path.LineTo(cx + size * 0.15f, cy + size * 0.15f);
        path.LineTo(cx, cy + size);
        path.LineTo(cx - size * 0.15f, cy + size * 0.15f);
        path.LineTo(cx - size, cy);
        path.LineTo(cx - size * 0.15f, cy - size * 0.15f);
        path.Close();
        canvas.DrawPath(path, _fill);
    }

    /// <summary>Zeichnet ein Herz als Pfad</summary>
    private static void DrawHeartPath(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        using var path = new SKPath();
        float w = size * 0.95f;
        float h = size * 1.1f;
        float top = cy - h * 0.35f;

        path.MoveTo(cx, cy + h * 0.45f); // Spitze unten
        path.CubicTo(cx - w * 1.1f, cy + h * 0.05f,
                     cx - w * 0.9f, top - h * 0.15f,
                     cx, top + h * 0.2f);
        path.CubicTo(cx + w * 0.9f, top - h * 0.15f,
                     cx + w * 1.1f, cy + h * 0.05f,
                     cx, cy + h * 0.45f);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>Zeichnet einen n-zackigen Stern</summary>
    private static void DrawStar(SKCanvas canvas, float cx, float cy, float outerR, float innerR, int points, SKPaint paint)
    {
        using var path = new SKPath();
        float angleStep = MathF.PI / points;
        for (int i = 0; i < points * 2; i++)
        {
            float r = i % 2 == 0 ? outerR : innerR;
            float angle = i * angleStep - MathF.PI / 2f;
            float x = cx + MathF.Cos(angle) * r;
            float y = cy + MathF.Sin(angle) * r;
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>Zeichnet einen Schild-Umriss als Pfad</summary>
    private static void DrawShieldPath(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        using var path = new SKPath();
        float w = size * 0.85f;
        float h = size * 1.1f;
        float top = cy - h * 0.45f;

        path.MoveTo(cx, top); // Spitze oben Mitte
        path.LineTo(cx + w, top + h * 0.12f);
        path.CubicTo(cx + w * 0.95f, top + h * 0.6f,
                     cx + w * 0.5f, top + h * 0.85f,
                     cx, top + h);
        path.CubicTo(cx - w * 0.5f, top + h * 0.85f,
                     cx - w * 0.95f, top + h * 0.6f,
                     cx - w, top + h * 0.12f);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    /// <summary>Zeichnet einen kleinen Chevron/Pfeil ">>"</summary>
    private static void DrawChevron(SKCanvas canvas, float cx, float cy, float size)
    {
        _stroke.Color = SKColors.White.WithAlpha(200);
        _stroke.StrokeWidth = size * 0.3f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.StrokeJoin = SKStrokeJoin.Round;

        using var path = new SKPath();
        path.MoveTo(cx - size * 0.3f, cy - size * 0.5f);
        path.LineTo(cx + size * 0.3f, cy);
        path.LineTo(cx - size * 0.3f, cy + size * 0.5f);
        canvas.DrawPath(path, _stroke);
    }

    /// <summary>Zeichnet ein Kleeblatt-Blatt</summary>
    private static void DrawCloverLeaf(SKCanvas canvas, float cx, float cy, float size,
        float rotationDeg, SKColor mainColor, SKColor lightColor)
    {
        canvas.Save();
        canvas.RotateDegrees(rotationDeg, cx, cy);

        // Herzfoermiges Blatt
        using var path = new SKPath();
        path.MoveTo(cx, cy + size * 0.4f);
        path.CubicTo(cx - size * 0.8f, cy + size * 0.1f,
                     cx - size * 0.6f, cy - size * 0.6f,
                     cx, cy - size * 0.15f);
        path.CubicTo(cx + size * 0.6f, cy - size * 0.6f,
                     cx + size * 0.8f, cy + size * 0.1f,
                     cx, cy + size * 0.4f);
        path.Close();

        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - size * 0.5f), new SKPoint(cx, cy + size * 0.4f),
            [lightColor, mainColor],
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(path, _fill);
        _fill.Shader = null;

        // Blattader (Mittellinie)
        _stroke.Color = Darken(mainColor, 20).WithAlpha(100);
        _stroke.StrokeWidth = size * 0.04f;
        canvas.DrawLine(cx, cy - size * 0.1f, cx, cy + size * 0.35f, _stroke);

        canvas.Restore();
    }

    /// <summary>Zeichnet eine kleine Flamme</summary>
    private static void DrawMiniFlame(SKCanvas canvas, float cx, float cy, float size,
        SKColor outer, SKColor inner)
    {
        using var path = new SKPath();
        path.MoveTo(cx - size * 0.4f, cy + size * 0.3f);
        path.CubicTo(cx - size * 0.5f, cy - size * 0.3f,
                     cx - size * 0.15f, cy - size * 0.5f,
                     cx, cy - size * 0.8f);
        path.CubicTo(cx + size * 0.15f, cy - size * 0.5f,
                     cx + size * 0.5f, cy - size * 0.3f,
                     cx + size * 0.4f, cy + size * 0.3f);
        path.Close();

        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, cy - size * 0.8f), new SKPoint(cx, cy + size * 0.3f),
            [inner, outer],
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(path, _fill);
        _fill.Shader = null;
    }

    /// <summary>Zeichnet einen Schleim-Tropfen</summary>
    private static void DrawSlimeDrop(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        using var path = new SKPath();
        path.MoveTo(cx, cy - size);
        path.CubicTo(cx + size * 0.5f, cy - size * 0.5f,
                     cx + size * 0.4f, cy + size * 0.3f,
                     cx, cy + size * 0.5f);
        path.CubicTo(cx - size * 0.4f, cy + size * 0.3f,
                     cx - size * 0.5f, cy - size * 0.5f,
                     cx, cy - size);
        path.Close();

        _fill.Color = color.WithAlpha(160);
        canvas.DrawPath(path, _fill);

        // Highlight
        _fill.Color = SKColors.White.WithAlpha(60);
        canvas.DrawOval(cx - size * 0.1f, cy - size * 0.3f, size * 0.12f, size * 0.1f, _fill);
    }

    // Farb-Hilfsmethoden
    private static SKColor Lighten(SKColor c, int amount)
    {
        return new SKColor(
            (byte)Math.Min(255, c.Red + amount),
            (byte)Math.Min(255, c.Green + amount),
            (byte)Math.Min(255, c.Blue + amount),
            c.Alpha);
    }

    private static SKColor Darken(SKColor c, int amount)
    {
        return new SKColor(
            (byte)Math.Max(0, c.Red - amount),
            (byte)Math.Max(0, c.Green - amount),
            (byte)Math.Max(0, c.Blue - amount),
            c.Alpha);
    }
}
