using HandwerkerImperium.Models.Enums;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert SkiaSharp-Illustrationen als Header für Workshop-Karten auf dem Dashboard.
/// Jeder Workshop-Typ hat eine thematische Mini-Szene:
/// - Carpenter: Hobel + Holzbretter + Sägespäne
/// - Plumber: Rohre + Wassertropfen + Schraubenschlüssel
/// - Electrician: Kabel + Blitze + Sicherungskasten
/// - Painter: Farbroller + Farbkleckse + Palette
/// - Roofer: Dachziegel + Dachstuhl
/// - Contractor: Kran + Bauhelm + Ziegel
/// - Architect: Zirkel + Bauplan + Geodreieck
/// - GeneralContractor: Gebäude-Silhouette + Krone + Gold-Akzente
/// </summary>
public static class WorkshopCardRenderer
{
    // Workshop-Farben (identisch zu WorkshopColorConverter.cs)
    private static readonly Dictionary<WorkshopType, SKColor> WorkshopColors = new()
    {
        [WorkshopType.Carpenter] = new SKColor(0xA0, 0x52, 0x2D),          // Sienna
        [WorkshopType.Plumber] = new SKColor(0x0E, 0x74, 0x90),            // Teal
        [WorkshopType.Electrician] = new SKColor(0xF9, 0x73, 0x16),        // Orange
        [WorkshopType.Painter] = new SKColor(0xEC, 0x48, 0x99),            // Pink
        [WorkshopType.Roofer] = new SKColor(0xDC, 0x26, 0x26),             // Rot
        [WorkshopType.Contractor] = new SKColor(0xEA, 0x58, 0x0C),         // Craft-Orange
        [WorkshopType.Architect] = new SKColor(0x78, 0x71, 0x6C),          // Stone-Grau
        [WorkshopType.GeneralContractor] = new SKColor(0xFF, 0xD7, 0x00),  // Gold
        [WorkshopType.MasterSmith] = new SKColor(0xD4, 0xA3, 0x73),       // Kupfer-Orange
        [WorkshopType.InnovationLab] = new SKColor(0x6A, 0x5A, 0xCD)      // Violett
    };

    // Gemeinsame Farben
    private static readonly SKColor WoodLight = new(0xD7, 0xB2, 0x8A);
    private static readonly SKColor WoodDark = new(0x8B, 0x6B, 0x4A);
    private static readonly SKColor MetalLight = new(0xB0, 0xBE, 0xC5);
    private static readonly SKColor MetalDark = new(0x60, 0x70, 0x78);
    private static readonly SKColor BgDark = new(0x1A, 0x14, 0x10);
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor GoldDark = new(0xD4, 0xA0, 0x00);

    // Gecachte Paints
    private static readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <summary>
    /// Gibt die Workshop-Farbe für einen Typ zurück.
    /// </summary>
    public static SKColor GetWorkshopColor(WorkshopType type)
    {
        return WorkshopColors.GetValueOrDefault(type, new SKColor(0x80, 0x80, 0x80));
    }

    /// <summary>
    /// Rendert die Workshop-Illustration in den angegebenen Bereich.
    /// </summary>
    /// <param name="canvas">Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfügbarer Bereich.</param>
    /// <param name="type">Workshop-Typ.</param>
    /// <param name="isUnlocked">Ob der Workshop freigeschaltet ist.</param>
    /// <param name="level">Level des Workshops (beeinflusst Detailstufe).</param>
    public static void Render(SKCanvas canvas, SKRect bounds, WorkshopType type,
        bool isUnlocked, int level)
    {
        float w = bounds.Width;
        float h = bounds.Height;
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        var color = GetWorkshopColor(type);

        // Hintergrund-Gradient (dunkel → Workshop-Farbe)
        DrawBackground(canvas, bounds, color, isUnlocked, level);

        // Typ-spezifische Szene
        if (!isUnlocked)
        {
            // Gesperrte Workshops: Nur Silhouette
            DrawLockedSilhouette(canvas, cx, cy, w, h, type, color);
        }
        else
        {
            switch (type)
            {
                case WorkshopType.Carpenter:
                    DrawCarpenterScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Plumber:
                    DrawPlumberScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Electrician:
                    DrawElectricianScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Painter:
                    DrawPainterScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Roofer:
                    DrawRooferScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Contractor:
                    DrawContractorScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.Architect:
                    DrawArchitectScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.GeneralContractor:
                    DrawGeneralContractorScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.MasterSmith:
                    DrawMasterSmithScene(canvas, cx, cy, w, h, color);
                    break;
                case WorkshopType.InnovationLab:
                    DrawInnovationLabScene(canvas, cx, cy, w, h, color);
                    break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawBackground(SKCanvas canvas, SKRect bounds, SKColor color,
        bool isUnlocked, int level)
    {
        // Dunkler Basis-Hintergrund
        _fill.Color = BgDark;
        canvas.DrawRect(bounds, _fill);

        // Farbiger Gradient (stärker bei höherem Level)
        byte alpha = !isUnlocked ? (byte)15 : (byte)Math.Min(50 + level / 5, 100);
        _fill.Color = color.WithAlpha(alpha);
        canvas.DrawRect(bounds, _fill);

        // Dezentes Raster (Werkstatt-Atmosphäre)
        _stroke.Color = new SKColor(0xFF, 0xFF, 0xFF, 0x08);
        _stroke.StrokeWidth = 0.5f;
        for (float x = bounds.Left; x < bounds.Right; x += 12)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, _stroke);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GESPERRTE WORKSHOPS
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawLockedSilhouette(SKCanvas canvas, float cx, float cy,
        float w, float h, WorkshopType type, SKColor color)
    {
        // Dezente Werkzeug-Silhouette (gleiche Form wie freigeschaltet, aber grau)
        _fill.Color = color.WithAlpha(20);

        // Einfaches generisches Werkzeug-Symbol
        float s = Math.Min(w, h) * 0.25f;

        // Kreis-Hintergrund
        canvas.DrawCircle(cx, cy, s, _fill);

        // Fragezeichen
        _fill.Color = color.WithAlpha(40);
        using var font = new SKFont { Size = s * 1.2f, Embolden = true };
        canvas.DrawText("?", cx, cy + s * 0.35f, SKTextAlign.Center, font, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CARPENTER: Hobel + Holzbretter + Sägespäne
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawCarpenterScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Holzbretter (gestapelt, leicht versetzt)
        for (int i = 0; i < 3; i++)
        {
            float bx = cx - s * 0.7f + i * s * 0.15f;
            float by = cy + s * 0.15f - i * s * 0.2f;
            float bw = s * 1.2f;
            float bh = s * 0.14f;

            _fill.Color = i == 0 ? WoodDark : WoodLight;
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(bx, by, bx + bw, by + bh), 2), _fill);

            // Holzmaserung
            _stroke.Color = WoodDark.WithAlpha(40);
            _stroke.StrokeWidth = 0.5f;
            canvas.DrawLine(bx + 4, by + bh * 0.5f, bx + bw - 4, by + bh * 0.5f, _stroke);
        }

        // Hobel (über den Brettern)
        float hobelX = cx - s * 0.15f;
        float hobelY = cy - s * 0.3f;

        // Hobel-Körper
        _fill.Color = WoodDark;
        using var hobelPath = new SKPath();
        hobelPath.MoveTo(hobelX, hobelY);
        hobelPath.LineTo(hobelX + s * 0.6f, hobelY);
        hobelPath.LineTo(hobelX + s * 0.55f, hobelY + s * 0.2f);
        hobelPath.LineTo(hobelX + s * 0.05f, hobelY + s * 0.2f);
        hobelPath.Close();
        canvas.DrawPath(hobelPath, _fill);

        // Hobel-Griff
        _fill.Color = color;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(hobelX + s * 0.15f, hobelY - s * 0.12f, hobelX + s * 0.45f, hobelY), 2), _fill);

        // Metallklinge unten
        _fill.Color = MetalLight;
        canvas.DrawRect(hobelX + s * 0.2f, hobelY + s * 0.18f, s * 0.2f, s * 0.04f, _fill);

        // Sägespäne (kleine Punkte)
        _fill.Color = WoodLight.WithAlpha(100);
        float[] spawnX = [-0.3f, -0.1f, 0.2f, 0.5f, -0.4f, 0.6f, 0.0f];
        float[] spawnY = [0.35f, 0.42f, 0.38f, 0.43f, 0.4f, 0.36f, 0.45f];
        for (int i = 0; i < spawnX.Length; i++)
        {
            canvas.DrawCircle(cx + s * spawnX[i], cy + s * spawnY[i],
                1.5f + (i % 3) * 0.5f, _fill);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLUMBER: Rohre + Wassertropfen + Schraubenschlüssel
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawPlumberScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Rohr-System (L-Form)
        _stroke.Color = MetalLight;
        _stroke.StrokeWidth = s * 0.15f;

        // Horizontales Rohr
        canvas.DrawLine(cx - s * 0.6f, cy - s * 0.1f, cx + s * 0.1f, cy - s * 0.1f, _stroke);

        // Vertikales Rohr (nach unten)
        canvas.DrawLine(cx + s * 0.1f, cy - s * 0.1f, cx + s * 0.1f, cy + s * 0.35f, _stroke);

        // Rohr-Verbindungen (Muffen)
        _fill.Color = MetalDark;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx + s * 0.01f, cy - s * 0.18f, cx + s * 0.19f, cy - s * 0.02f), 2), _fill);

        // Ventil oben
        _fill.Color = color;
        canvas.DrawCircle(cx - s * 0.3f, cy - s * 0.1f, s * 0.08f, _fill);
        _fill.Color = MetalDark;
        canvas.DrawRect(cx - s * 0.34f, cy - s * 0.22f, s * 0.08f, s * 0.1f, _fill);

        // Wassertropfen (unter dem Rohr)
        _fill.Color = new SKColor(0x42, 0xA5, 0xF5, 0xC0);
        DrawWaterDrop(canvas, cx + s * 0.1f, cy + s * 0.45f, s * 0.06f);
        DrawWaterDrop(canvas, cx + s * 0.05f, cy + s * 0.38f, s * 0.04f);

        // Schraubenschlüssel (rechts)
        canvas.Save();
        canvas.Translate(cx + s * 0.5f, cy);
        canvas.RotateDegrees(-25);

        _fill.Color = MetalLight;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-2, 0, 2, s * 0.55f), 1), _fill);

        // Schlüssel-Maul
        using var maulPath = new SKPath();
        maulPath.MoveTo(-s * 0.1f, 0);
        maulPath.LineTo(s * 0.1f, 0);
        maulPath.LineTo(s * 0.08f, -s * 0.15f);
        maulPath.LineTo(s * 0.03f, -s * 0.08f);
        maulPath.LineTo(-s * 0.03f, -s * 0.08f);
        maulPath.LineTo(-s * 0.08f, -s * 0.15f);
        maulPath.Close();
        canvas.DrawPath(maulPath, _fill);

        canvas.Restore();
    }

    private static void DrawWaterDrop(SKCanvas canvas, float cx, float cy, float size)
    {
        using var dropPath = new SKPath();
        dropPath.MoveTo(cx, cy - size * 2);
        dropPath.QuadTo(cx + size * 1.2f, cy, cx, cy + size);
        dropPath.QuadTo(cx - size * 1.2f, cy, cx, cy - size * 2);
        dropPath.Close();
        canvas.DrawPath(dropPath, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ELECTRICIAN: Kabel + Blitze + Sicherungskasten
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawElectricianScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Sicherungskasten (Mitte)
        _fill.Color = MetalDark;
        float boxW = s * 0.8f;
        float boxH = s * 0.65f;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - boxW / 2, cy - boxH / 2, cx + boxW / 2, cy + boxH / 2), 3), _fill);

        // Kasten-Rand
        _stroke.Color = MetalLight;
        _stroke.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - boxW / 2, cy - boxH / 2, cx + boxW / 2, cy + boxH / 2), 3), _stroke);

        // Sicherungen (3 Schalter)
        for (int i = 0; i < 3; i++)
        {
            float switchX = cx - s * 0.2f + i * s * 0.2f;
            float switchY = cy - s * 0.12f;

            // Schalter-Slot
            _fill.Color = new SKColor(0x20, 0x20, 0x20);
            canvas.DrawRoundRect(new SKRoundRect(
                new SKRect(switchX - 3, switchY, switchX + 3, switchY + s * 0.25f), 1), _fill);

            // Schalter (oben = an)
            _fill.Color = i < 2 ? color : new SKColor(0x50, 0x50, 0x50);
            canvas.DrawRoundRect(new SKRoundRect(
                new SKRect(switchX - 2.5f, switchY + 1, switchX + 2.5f, switchY + s * 0.12f), 1), _fill);
        }

        // Kabel (links und rechts aus dem Kasten)
        _stroke.Color = color;
        _stroke.StrokeWidth = 2;
        // Links
        canvas.DrawLine(cx - boxW / 2, cy, cx - boxW / 2 - s * 0.3f, cy - s * 0.3f, _stroke);
        // Rechts
        canvas.DrawLine(cx + boxW / 2, cy, cx + boxW / 2 + s * 0.3f, cy + s * 0.2f, _stroke);

        // Blitz-Symbol (oben rechts)
        _fill.Color = color;
        float bx = cx + s * 0.5f;
        float by = cy - s * 0.35f;
        DrawLightningBolt(canvas, bx, by, s * 0.3f, color);

        // Warnung-Dreieck (unten am Kasten)
        _fill.Color = color.WithAlpha(80);
        using var warnPath = new SKPath();
        float wx = cx;
        float wy = cy + boxH / 2 - s * 0.05f;
        warnPath.MoveTo(wx, wy - s * 0.1f);
        warnPath.LineTo(wx - s * 0.07f, wy + s * 0.02f);
        warnPath.LineTo(wx + s * 0.07f, wy + s * 0.02f);
        warnPath.Close();
        canvas.DrawPath(warnPath, _fill);
    }

    private static void DrawLightningBolt(SKCanvas canvas, float cx, float cy, float size, SKColor color)
    {
        _fill.Color = color;
        using var boltPath = new SKPath();
        boltPath.MoveTo(cx - size * 0.15f, cy - size * 0.5f);
        boltPath.LineTo(cx + size * 0.2f, cy - size * 0.5f);
        boltPath.LineTo(cx, cy);
        boltPath.LineTo(cx + size * 0.25f, cy);
        boltPath.LineTo(cx - size * 0.1f, cy + size * 0.5f);
        boltPath.LineTo(cx + size * 0.05f, cy + size * 0.05f);
        boltPath.LineTo(cx - size * 0.2f, cy + size * 0.05f);
        boltPath.Close();
        canvas.DrawPath(boltPath, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PAINTER: Farbroller + Farbkleckse + Palette
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawPainterScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Farbpalette (links)
        _fill.Color = WoodLight;
        canvas.DrawOval(cx - s * 0.35f, cy, s * 0.4f, s * 0.3f, _fill);

        // Farbtupfer auf der Palette
        SKColor[] paletteColors = [
            new(0xF4, 0x43, 0x36), // Rot
            new(0x21, 0x96, 0xF3), // Blau
            new(0x4C, 0xAF, 0x50), // Grün
            new(0xFF, 0xEB, 0x3B), // Gelb
            color                   // Workshop-Farbe
        ];

        float[] dots = [-0.2f, -0.05f, 0.1f, -0.15f, 0.05f];
        float[] dotsY = [-0.08f, -0.12f, -0.05f, 0.08f, 0.1f];
        for (int i = 0; i < paletteColors.Length; i++)
        {
            _fill.Color = paletteColors[i];
            canvas.DrawCircle(cx - s * 0.35f + s * dots[i], cy + s * dotsY[i],
                s * 0.06f, _fill);
        }

        // Daumenloch
        _fill.Color = BgDark;
        canvas.DrawCircle(cx - s * 0.35f + s * 0.15f, cy + s * 0.12f, s * 0.05f, _fill);

        // Farbroller (rechts)
        canvas.Save();
        canvas.Translate(cx + s * 0.3f, cy);
        canvas.RotateDegrees(-15);

        // Roller-Griff
        _fill.Color = MetalLight;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(-2, s * 0.05f, 2, s * 0.45f), 1), _fill);

        // Roller-Bügel (gebogen)
        _stroke.Color = MetalLight;
        _stroke.StrokeWidth = 2;
        canvas.DrawLine(0, s * 0.05f, -s * 0.12f, -s * 0.1f, _stroke);

        // Rolle
        _fill.Color = color;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(-s * 0.2f, -s * 0.2f, s * 0.04f, -s * 0.05f), 3), _fill);

        // Farbspur unter der Rolle
        _fill.Color = color.WithAlpha(60);
        canvas.DrawRect(-s * 0.18f, -s * 0.22f, s * 0.2f, s * 0.03f, _fill);

        canvas.Restore();

        // Farbkleckse am Boden
        _fill.Color = color.WithAlpha(50);
        canvas.DrawOval(cx + s * 0.1f, cy + s * 0.35f, s * 0.15f, s * 0.04f, _fill);
        _fill.Color = new SKColor(0x21, 0x96, 0xF3, 0x30);
        canvas.DrawOval(cx - s * 0.2f, cy + s * 0.38f, s * 0.1f, s * 0.03f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROOFER: Dachziegel + Dachstuhl
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawRooferScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Dachstuhl (Dreiecks-Form)
        _stroke.Color = WoodDark;
        _stroke.StrokeWidth = 2;
        // Dachbalken links
        canvas.DrawLine(cx, cy - s * 0.5f, cx - s * 0.7f, cy + s * 0.2f, _stroke);
        // Dachbalken rechts
        canvas.DrawLine(cx, cy - s * 0.5f, cx + s * 0.7f, cy + s * 0.2f, _stroke);
        // Querbalken
        canvas.DrawLine(cx - s * 0.5f, cy, cx + s * 0.5f, cy, _stroke);

        // Dachziegel (3 Reihen, versetzt)
        for (int row = 0; row < 3; row++)
        {
            float tileY = cy - s * 0.35f + row * s * 0.2f;
            float rowW = s * 0.4f + row * s * 0.2f;
            int tileCount = 2 + row;
            float tileW = rowW / tileCount;
            float startX = cx - rowW / 2 + (row % 2 == 1 ? tileW * 0.5f : 0);

            for (int i = 0; i < tileCount; i++)
            {
                float tx = startX + i * tileW;
                float ty = tileY;

                // Ziegel (abgerundetes Trapez)
                byte shade = (byte)(0xC0 - row * 0x15 + i * 0x08);
                _fill.Color = new SKColor(shade, (byte)(shade * 0.4f), (byte)(shade * 0.25f));
                using var tilePath = new SKPath();
                tilePath.MoveTo(tx, ty);
                tilePath.LineTo(tx + tileW - 1, ty);
                tilePath.LineTo(tx + tileW - 2, ty + s * 0.15f);
                tilePath.LineTo(tx + 1, ty + s * 0.15f);
                tilePath.Close();
                canvas.DrawPath(tilePath, _fill);
            }
        }

        // Schornstein (rechts oben)
        _fill.Color = color.WithAlpha(180);
        canvas.DrawRect(cx + s * 0.25f, cy - s * 0.55f, s * 0.12f, s * 0.2f, _fill);
        _fill.Color = color;
        canvas.DrawRect(cx + s * 0.23f, cy - s * 0.56f, s * 0.16f, s * 0.04f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONTRACTOR: Kran + Bauhelm + Ziegel
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawContractorScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Kran (links)
        // Turm
        _fill.Color = color;
        canvas.DrawRect(cx - s * 0.55f, cy - s * 0.4f, s * 0.08f, s * 0.75f, _fill);

        // Ausleger (horizontal)
        canvas.DrawRect(cx - s * 0.55f, cy - s * 0.4f, s * 0.7f, s * 0.04f, _fill);

        // Gegengewicht
        _fill.Color = MetalDark;
        canvas.DrawRect(cx - s * 0.65f, cy - s * 0.36f, s * 0.12f, s * 0.08f, _fill);

        // Seil + Haken
        _stroke.Color = MetalLight;
        _stroke.StrokeWidth = 1;
        canvas.DrawLine(cx + s * 0.05f, cy - s * 0.36f, cx + s * 0.05f, cy + s * 0.05f, _stroke);

        // Haken
        _stroke.Color = color;
        _stroke.StrokeWidth = 2;
        canvas.DrawArc(new SKRect(cx - s * 0.01f, cy + s * 0.02f, cx + s * 0.11f, cy + s * 0.12f),
            0, 180, false, _stroke);

        // Ziegelstapel (rechts unten)
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col <= 2 - row; col++)
            {
                float bx = cx + s * 0.25f + col * s * 0.15f + row * s * 0.07f;
                float by = cy + s * 0.3f - row * s * 0.1f;

                _fill.Color = new SKColor(0xCC, 0x66, 0x33);
                canvas.DrawRect(bx, by, s * 0.12f, s * 0.08f, _fill);

                // Mörtel-Fugen
                _stroke.Color = new SKColor(0xE0, 0xD0, 0xC0);
                _stroke.StrokeWidth = 0.5f;
                canvas.DrawRect(bx, by, s * 0.12f, s * 0.08f, _stroke);
            }
        }

        // Bauhelm (über den Ziegeln)
        _fill.Color = color;
        float helmX = cx + s * 0.35f;
        float helmY = cy - s * 0.1f;
        using var helmPath = new SKPath();
        helmPath.MoveTo(helmX - s * 0.15f, helmY + s * 0.03f);
        helmPath.QuadTo(helmX, helmY - s * 0.12f, helmX + s * 0.15f, helmY + s * 0.03f);
        helmPath.Close();
        canvas.DrawPath(helmPath, _fill);

        // Helm-Krempe
        _fill.Color = color.WithAlpha(200);
        canvas.DrawRect(helmX - s * 0.17f, helmY + s * 0.01f, s * 0.34f, s * 0.04f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ARCHITECT: Zirkel + Bauplan + Geodreieck
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawArchitectScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Bauplan (Hintergrund, leicht gedreht)
        canvas.Save();
        canvas.Translate(cx - s * 0.1f, cy);
        canvas.RotateDegrees(-5);

        _fill.Color = new SKColor(0x1A, 0x23, 0x7E, 0x90);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(-s * 0.5f, -s * 0.35f, s * 0.5f, s * 0.35f), 2), _fill);

        // Raster auf Bauplan
        _stroke.Color = new SKColor(0x40, 0x50, 0x90, 0x60);
        _stroke.StrokeWidth = 0.5f;
        for (float gx = -s * 0.4f; gx <= s * 0.4f; gx += s * 0.2f)
            canvas.DrawLine(gx, -s * 0.3f, gx, s * 0.3f, _stroke);
        for (float gy = -s * 0.25f; gy <= s * 0.25f; gy += s * 0.15f)
            canvas.DrawLine(-s * 0.45f, gy, s * 0.45f, gy, _stroke);

        // Grundriss-Zeichnung (weiße Linien)
        _stroke.Color = SKColors.White.WithAlpha(150);
        _stroke.StrokeWidth = 1;
        canvas.DrawRect(-s * 0.25f, -s * 0.15f, s * 0.4f, s * 0.25f, _stroke);
        canvas.DrawLine(-s * 0.05f, -s * 0.15f, -s * 0.05f, s * 0.1f, _stroke);
        canvas.DrawLine(-s * 0.25f, 0, s * 0.15f, 0, _stroke);

        canvas.Restore();

        // Geodreieck (rechts unten)
        _fill.Color = new SKColor(0xFF, 0xFF, 0xFF, 0x20);
        using var trianglePath = new SKPath();
        float tx = cx + s * 0.3f;
        float ty = cy + s * 0.15f;
        trianglePath.MoveTo(tx, ty - s * 0.2f);
        trianglePath.LineTo(tx + s * 0.25f, ty + s * 0.15f);
        trianglePath.LineTo(tx - s * 0.25f, ty + s * 0.15f);
        trianglePath.Close();
        canvas.DrawPath(trianglePath, _fill);

        _stroke.Color = color.WithAlpha(100);
        _stroke.StrokeWidth = 1;
        canvas.DrawPath(trianglePath, _stroke);

        // Zirkel (links oben)
        float zx = cx - s * 0.4f;
        float zy = cy - s * 0.25f;

        _stroke.Color = MetalLight;
        _stroke.StrokeWidth = 1.5f;
        // Linker Schenkel
        canvas.DrawLine(zx, zy, zx - s * 0.12f, zy + s * 0.3f, _stroke);
        // Rechter Schenkel
        canvas.DrawLine(zx, zy, zx + s * 0.12f, zy + s * 0.3f, _stroke);

        // Spitze (links)
        _fill.Color = MetalLight;
        canvas.DrawCircle(zx - s * 0.12f, zy + s * 0.3f, 1.5f, _fill);

        // Bleistift (rechts)
        _fill.Color = color;
        canvas.DrawCircle(zx + s * 0.12f, zy + s * 0.3f, 2, _fill);

        // Schraube oben
        canvas.DrawCircle(zx, zy, 2, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GENERAL CONTRACTOR: Gebäude-Silhouette + Krone + Gold-Akzente
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawGeneralContractorScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Gebäude-Skyline (3 Gebäude)
        // Links: Kleines Gebäude
        _fill.Color = GoldDark.WithAlpha(120);
        canvas.DrawRect(cx - s * 0.6f, cy, s * 0.2f, s * 0.4f, _fill);
        // Fenster
        _fill.Color = new SKColor(0xFF, 0xFF, 0xE0, 0x50);
        canvas.DrawRect(cx - s * 0.55f, cy + s * 0.05f, s * 0.04f, s * 0.04f, _fill);
        canvas.DrawRect(cx - s * 0.47f, cy + s * 0.05f, s * 0.04f, s * 0.04f, _fill);

        // Mitte: Großes Gebäude
        _fill.Color = GoldDark.WithAlpha(150);
        canvas.DrawRect(cx - s * 0.2f, cy - s * 0.15f, s * 0.35f, s * 0.55f, _fill);
        // Fenster-Reihen
        _fill.Color = new SKColor(0xFF, 0xFF, 0xE0, 0x50);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                canvas.DrawRect(cx - s * 0.14f + col * s * 0.15f,
                    cy - s * 0.08f + row * s * 0.14f, s * 0.06f, s * 0.06f, _fill);
            }
        }

        // Rechts: Mittleres Gebäude
        _fill.Color = GoldDark.WithAlpha(100);
        canvas.DrawRect(cx + s * 0.25f, cy + s * 0.05f, s * 0.25f, s * 0.35f, _fill);
        _fill.Color = new SKColor(0xFF, 0xFF, 0xE0, 0x50);
        canvas.DrawRect(cx + s * 0.3f, cy + s * 0.1f, s * 0.04f, s * 0.04f, _fill);
        canvas.DrawRect(cx + s * 0.38f, cy + s * 0.1f, s * 0.04f, s * 0.04f, _fill);

        // Boden
        _fill.Color = GoldDark.WithAlpha(40);
        canvas.DrawRect(cx - s * 0.7f, cy + s * 0.4f, s * 1.4f, s * 0.04f, _fill);

        // Krone (oben zentriert, Gold)
        _fill.Color = GoldColor;
        float crownCx = cx;
        float crownCy = cy - s * 0.35f;
        float cs = s * 0.2f;

        using var crownPath = new SKPath();
        crownPath.MoveTo(crownCx - cs, crownCy + cs * 0.4f);
        crownPath.LineTo(crownCx - cs, crownCy - cs * 0.2f);
        crownPath.LineTo(crownCx - cs * 0.5f, crownCy + cs * 0.1f);
        crownPath.LineTo(crownCx, crownCy - cs * 0.5f);
        crownPath.LineTo(crownCx + cs * 0.5f, crownCy + cs * 0.1f);
        crownPath.LineTo(crownCx + cs, crownCy - cs * 0.2f);
        crownPath.LineTo(crownCx + cs, crownCy + cs * 0.4f);
        crownPath.Close();
        canvas.DrawPath(crownPath, _fill);

        // Basisband der Krone
        _fill.Color = GoldDark;
        canvas.DrawRect(crownCx - cs, crownCy + cs * 0.25f, cs * 2, cs * 0.15f, _fill);

        // Edelstein
        _fill.Color = new SKColor(0xF4, 0x43, 0x36);
        canvas.DrawCircle(crownCx, crownCy + cs * 0.32f, cs * 0.08f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MASTER SMITH: Amboss + Schmiedehammer + Esse/Feuer + Funken
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawMasterSmithScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Esse/Feuer (links)
        _fill.Color = new SKColor(0x5C, 0x3A, 0x21);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.7f, cy - s * 0.15f, cx - s * 0.35f, cy + s * 0.3f), 3), _fill);
        // Feuer in der Esse
        _fill.Color = new SKColor(0xFF, 0x6B, 0x00, 0xD0);
        using var firePath = new SKPath();
        float fx = cx - s * 0.525f;
        float fy = cy + s * 0.05f;
        firePath.MoveTo(fx - s * 0.1f, fy + s * 0.15f);
        firePath.QuadTo(fx - s * 0.06f, fy - s * 0.08f, fx, fy - s * 0.15f);
        firePath.QuadTo(fx + s * 0.06f, fy - s * 0.08f, fx + s * 0.1f, fy + s * 0.15f);
        firePath.Close();
        canvas.DrawPath(firePath, _fill);
        // Innerer Kern (heller)
        _fill.Color = new SKColor(0xFF, 0xD5, 0x4F, 0xC0);
        canvas.DrawOval(fx, fy, s * 0.05f, s * 0.08f, _fill);

        // Amboss (Mitte)
        _fill.Color = MetalDark;
        // Amboss-Basis (breit)
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.2f, cy + s * 0.12f, cx + s * 0.25f, cy + s * 0.3f), 2), _fill);
        // Amboss-Körper (schmal, höher)
        _fill.Color = MetalLight;
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.12f, cy - s * 0.05f, cx + s * 0.17f, cy + s * 0.14f), 2), _fill);
        // Amboss-Horn (rechts, spitz zulaufend)
        using var hornPath = new SKPath();
        hornPath.MoveTo(cx + s * 0.17f, cy - s * 0.02f);
        hornPath.LineTo(cx + s * 0.35f, cy + s * 0.03f);
        hornPath.LineTo(cx + s * 0.17f, cy + s * 0.08f);
        hornPath.Close();
        canvas.DrawPath(hornPath, _fill);
        // Metallglanz auf Amboss
        _fill.Color = new SKColor(0xFF, 0xFF, 0xFF, 0x30);
        canvas.DrawRect(cx - s * 0.1f, cy - s * 0.04f, s * 0.2f, s * 0.03f, _fill);

        // Schmiedehammer (darüber, leicht geneigt)
        canvas.Save();
        canvas.Translate(cx + s * 0.05f, cy - s * 0.25f);
        canvas.RotateDegrees(-20);
        // Stiel
        _fill.Color = WoodDark;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-2, 0, 2, s * 0.35f), 1), _fill);
        // Hammerkopf
        _fill.Color = MetalLight;
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(-s * 0.1f, -s * 0.06f, s * 0.1f, s * 0.04f), 2), _fill);
        // Glanz auf Hammerkopf
        _fill.Color = new SKColor(0xFF, 0xFF, 0xFF, 0x40);
        canvas.DrawRect(-s * 0.08f, -s * 0.05f, s * 0.16f, s * 0.02f, _fill);
        canvas.Restore();

        // Glühende Funken (kleine Punkte, verstreut)
        _fill.Color = new SKColor(0xFF, 0x8F, 0x00, 0xB0);
        float[] sparkX = [-0.15f, 0.1f, 0.25f, -0.05f, 0.35f, -0.25f];
        float[] sparkY = [-0.35f, -0.42f, -0.3f, -0.45f, -0.15f, -0.2f];
        for (int i = 0; i < sparkX.Length; i++)
        {
            _fill.Color = (i % 2 == 0)
                ? new SKColor(0xFF, 0x8F, 0x00, (byte)(0x60 + i * 0x18))
                : new SKColor(0xFF, 0xD5, 0x4F, (byte)(0x50 + i * 0x15));
            canvas.DrawCircle(cx + s * sparkX[i], cy + s * sparkY[i], 1.2f + (i % 3) * 0.5f, _fill);
        }

        // Glühendes Werkstück auf dem Amboss
        _fill.Color = new SKColor(0xFF, 0x6B, 0x00, 0xA0);
        canvas.DrawRoundRect(new SKRoundRect(
            new SKRect(cx - s * 0.06f, cy + s * 0.01f, cx + s * 0.12f, cy + s * 0.07f), 1), _fill);
        // Heller Kern
        _fill.Color = new SKColor(0xFF, 0xE0, 0x82, 0x90);
        canvas.DrawRect(cx - s * 0.03f, cy + s * 0.03f, s * 0.1f, s * 0.02f, _fill);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INNOVATION LAB: Reagenzglas + Zahnräder + Glühbirne + Blaupause
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawInnovationLabScene(SKCanvas canvas, float cx, float cy,
        float w, float h, SKColor color)
    {
        float s = Math.Min(w, h) * 0.35f;

        // Blaupause-Linien im Hintergrund (dezent)
        _stroke.Color = color.WithAlpha(25);
        _stroke.StrokeWidth = 0.5f;
        for (float gx = cx - s * 0.6f; gx <= cx + s * 0.6f; gx += s * 0.15f)
            canvas.DrawLine(gx, cy - s * 0.4f, gx, cy + s * 0.45f, _stroke);
        for (float gy = cy - s * 0.35f; gy <= cy + s * 0.4f; gy += s * 0.15f)
            canvas.DrawLine(cx - s * 0.6f, gy, cx + s * 0.6f, gy, _stroke);

        // Reagenzglas/Erlenmeyer-Kolben (links)
        _stroke.Color = new SKColor(0xB0, 0xD0, 0xF0, 0xC0);
        _stroke.StrokeWidth = 1.5f;
        float flaskX = cx - s * 0.3f;
        float flaskY = cy - s * 0.1f;
        // Kolbenhals
        canvas.DrawLine(flaskX - s * 0.03f, flaskY - s * 0.25f, flaskX - s * 0.03f, flaskY, _stroke);
        canvas.DrawLine(flaskX + s * 0.03f, flaskY - s * 0.25f, flaskX + s * 0.03f, flaskY, _stroke);
        // Kolbenkörper (breit)
        using var flaskPath = new SKPath();
        flaskPath.MoveTo(flaskX - s * 0.03f, flaskY);
        flaskPath.LineTo(flaskX - s * 0.13f, flaskY + s * 0.2f);
        flaskPath.LineTo(flaskX + s * 0.13f, flaskY + s * 0.2f);
        flaskPath.LineTo(flaskX + s * 0.03f, flaskY);
        canvas.DrawPath(flaskPath, _stroke);
        // Flüssigkeit im Kolben
        _fill.Color = color.WithAlpha(80);
        using var liquidPath = new SKPath();
        liquidPath.MoveTo(flaskX - s * 0.06f, flaskY + s * 0.08f);
        liquidPath.LineTo(flaskX - s * 0.12f, flaskY + s * 0.19f);
        liquidPath.LineTo(flaskX + s * 0.12f, flaskY + s * 0.19f);
        liquidPath.LineTo(flaskX + s * 0.06f, flaskY + s * 0.08f);
        liquidPath.Close();
        canvas.DrawPath(liquidPath, _fill);
        // Blasen
        _fill.Color = color.WithAlpha(60);
        canvas.DrawCircle(flaskX - s * 0.02f, flaskY + s * 0.12f, 1.5f, _fill);
        canvas.DrawCircle(flaskX + s * 0.04f, flaskY + s * 0.15f, 1.0f, _fill);

        // Zahnräder (rechts)
        float gearCx = cx + s * 0.35f;
        float gearCy = cy + s * 0.05f;
        float gearR1 = s * 0.14f;
        float gearR2 = s * 0.09f;

        // Großes Zahnrad
        _stroke.Color = color.WithAlpha(140);
        _stroke.StrokeWidth = 1.5f;
        canvas.DrawCircle(gearCx, gearCy, gearR1, _stroke);
        canvas.DrawCircle(gearCx, gearCy, gearR1 * 0.5f, _stroke);
        // Zähne (6 Stück)
        for (int t = 0; t < 6; t++)
        {
            float a = t * MathF.PI / 3;
            float ix = gearCx + MathF.Cos(a) * gearR1;
            float iy = gearCy + MathF.Sin(a) * gearR1;
            float ox = gearCx + MathF.Cos(a) * (gearR1 + s * 0.04f);
            float oy = gearCy + MathF.Sin(a) * (gearR1 + s * 0.04f);
            canvas.DrawLine(ix, iy, ox, oy, _stroke);
        }

        // Kleines Zahnrad (versetzt)
        float gear2Cx = gearCx + gearR1 + gearR2 - s * 0.02f;
        float gear2Cy = gearCy - gearR2 * 0.5f;
        canvas.DrawCircle(gear2Cx, gear2Cy, gearR2, _stroke);
        canvas.DrawCircle(gear2Cx, gear2Cy, gearR2 * 0.4f, _stroke);
        for (int t = 0; t < 5; t++)
        {
            float a = t * MathF.PI * 2 / 5;
            float ix = gear2Cx + MathF.Cos(a) * gearR2;
            float iy = gear2Cy + MathF.Sin(a) * gearR2;
            float ox = gear2Cx + MathF.Cos(a) * (gearR2 + s * 0.03f);
            float oy = gear2Cy + MathF.Sin(a) * (gearR2 + s * 0.03f);
            canvas.DrawLine(ix, iy, ox, oy, _stroke);
        }

        // Glühbirne (oben Mitte)
        float bulbCx = cx + s * 0.05f;
        float bulbCy = cy - s * 0.32f;
        float bulbR = s * 0.1f;
        // Glaskolben
        _fill.Color = new SKColor(0xFF, 0xF1, 0x76, 0x60);
        canvas.DrawCircle(bulbCx, bulbCy, bulbR, _fill);
        _stroke.Color = new SKColor(0xFF, 0xF1, 0x76, 0xA0);
        _stroke.StrokeWidth = 1;
        canvas.DrawCircle(bulbCx, bulbCy, bulbR, _stroke);
        // Sockel
        _fill.Color = MetalLight.WithAlpha(180);
        canvas.DrawRect(bulbCx - s * 0.04f, bulbCy + bulbR - 1, s * 0.08f, s * 0.06f, _fill);
        // Glühdraht
        _stroke.Color = new SKColor(0xFF, 0xD5, 0x4F, 0x90);
        _stroke.StrokeWidth = 0.8f;
        canvas.DrawLine(bulbCx - s * 0.02f, bulbCy + s * 0.02f, bulbCx, bulbCy - s * 0.04f, _stroke);
        canvas.DrawLine(bulbCx, bulbCy - s * 0.04f, bulbCx + s * 0.02f, bulbCy + s * 0.02f, _stroke);

        // Lichtschein um Glühbirne
        _fill.Color = new SKColor(0xFF, 0xF1, 0x76, 0x15);
        canvas.DrawCircle(bulbCx, bulbCy, bulbR * 2f, _fill);

        // Prototyp-Skizze (unten rechts, kleine technische Zeichnung)
        _stroke.Color = color.WithAlpha(50);
        _stroke.StrokeWidth = 0.8f;
        float skX = cx + s * 0.15f;
        float skY = cy + s * 0.28f;
        canvas.DrawRect(skX, skY, s * 0.2f, s * 0.12f, _stroke);
        canvas.DrawLine(skX + s * 0.05f, skY, skX + s * 0.05f, skY + s * 0.12f, _stroke);
        canvas.DrawLine(skX, skY + s * 0.06f, skX + s * 0.2f, skY + s * 0.06f, _stroke);
    }
}
