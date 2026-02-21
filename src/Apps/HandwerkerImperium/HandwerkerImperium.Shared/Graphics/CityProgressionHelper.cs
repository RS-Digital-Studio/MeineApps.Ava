using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Dynamische Welt-Progression: Visuelle Verbesserungen der City-Szene basierend auf WorldTier.
/// Straßen-Upgrades, Dekoration (Bäume/Laternen/Bänke), lebendigere Farben bei höheren Leveln.
/// </summary>
public static class CityProgressionHelper
{
    // Gecachte Paints für Dekorationen
    private static readonly SKPaint _treeTrunkPaint = new() { Color = new SKColor(0x5D, 0x40, 0x37), IsAntialias = true };
    private static readonly SKPaint _treeLeafPaint = new() { IsAntialias = true };
    private static readonly SKPaint _lanternPolePaint = new() { Color = new SKColor(0x42, 0x42, 0x42), IsAntialias = true };
    private static readonly SKPaint _lanternGlowPaint = new() { IsAntialias = true };
    private static readonly SKPaint _benchPaint = new() { Color = new SKColor(0x6D, 0x4C, 0x41), IsAntialias = true };
    private static readonly SKPaint _benchLegPaint = new() { Color = new SKColor(0x42, 0x42, 0x42), IsAntialias = true };
    private static readonly SKPaint _flowerPaint = new() { IsAntialias = true };
    private static readonly SKPaint _sidewalkPaint = new() { IsAntialias = true };

    // Blumen-Farben für Beete
    private static readonly SKColor[] FlowerColors =
    [
        new SKColor(0xE5, 0x39, 0x35), // Rot
        new SKColor(0xFF, 0xB3, 0x00), // Gelb
        new SKColor(0xAB, 0x47, 0xBC), // Lila
        new SKColor(0xE9, 0x1E, 0x63), // Pink
        new SKColor(0xFF, 0x7A, 0x00), // Orange
    ];

    /// <summary>
    /// Zeichnet die Straße mit Progression (Schotter→Asphalt→Pflaster→Premium-Pflaster).
    /// </summary>
    public static void DrawProgressiveStreet(SKCanvas canvas, SKRect bounds, float streetY,
        float streetHeight, int worldTier, float nightDim, float time)
    {
        // Straßenoberfläche je nach Tier
        SKColor streetColor;
        SKColor curbiColor;
        SKColor stripeColor;
        bool hasSidewalk = worldTier >= 3;
        bool hasCobblestone = worldTier >= 5;
        bool hasPremiumPaving = worldTier >= 7;

        if (hasPremiumPaving)
        {
            // Tier 7+: Premium-Pflaster (warm-grau, edel)
            streetColor = new SKColor(0x6D, 0x63, 0x5B);
            curbiColor = new SKColor(0x9E, 0x94, 0x8A);
            stripeColor = new SKColor(0xFF, 0xD5, 0x4F); // Gold-Streifen
        }
        else if (hasCobblestone)
        {
            // Tier 5-6: Gepflasterter Asphalt
            streetColor = new SKColor(0x5A, 0x5A, 0x5A);
            curbiColor = new SKColor(0x8A, 0x8A, 0x8A);
            stripeColor = new SKColor(0xFF, 0xFF, 0x00);
        }
        else if (worldTier >= 3)
        {
            // Tier 3-4: Sauberer Asphalt
            streetColor = new SKColor(0x55, 0x55, 0x55);
            curbiColor = new SKColor(0x78, 0x78, 0x78);
            stripeColor = new SKColor(0xFF, 0xFF, 0x00);
        }
        else
        {
            // Tier 1-2: Schotter/Feldweg
            streetColor = new SKColor(0x7D, 0x6B, 0x5D);
            curbiColor = new SKColor(0x6D, 0x5B, 0x4D);
            stripeColor = new SKColor(0xCC, 0xCC, 0x80); // Blasses Gelb
        }

        // Bürgersteig (ab Tier 3)
        if (hasSidewalk)
        {
            var sidewalkColor = CityBuildingShapes.ApplyDim(new SKColor(0xBD, 0xB7, 0xAB), nightDim);
            _sidewalkPaint.Color = sidewalkColor;
            canvas.DrawRect(bounds.Left, streetY - 3, bounds.Width, 3, _sidewalkPaint);
            canvas.DrawRect(bounds.Left, streetY + streetHeight, bounds.Width, 3, _sidewalkPaint);
        }

        // Straßenfläche
        using var streetPaint = new SKPaint { IsAntialias = false };
        streetPaint.Color = CityBuildingShapes.ApplyDim(streetColor, nightDim);
        canvas.DrawRect(bounds.Left, streetY, bounds.Width, streetHeight, streetPaint);

        // Kopfsteinpflaster-Textur (Tier 5+)
        if (hasCobblestone)
        {
            DrawCobblestoneTexture(canvas, bounds, streetY, streetHeight, nightDim, hasPremiumPaving);
        }
        else if (worldTier <= 2)
        {
            // Schotter-Textur (kleine Punkte)
            DrawGravelTexture(canvas, bounds, streetY, streetHeight, nightDim);
        }

        // Bordsteine
        using var curbPaint = new SKPaint { IsAntialias = false };
        curbPaint.Color = CityBuildingShapes.ApplyDim(curbiColor, nightDim);
        canvas.DrawRect(bounds.Left, streetY, bounds.Width, 1.5f, curbPaint);
        canvas.DrawRect(bounds.Left, streetY + streetHeight - 1.5f, bounds.Width, 1.5f, curbPaint);

        // Mittelstreifen
        using var stripePaint = new SKPaint { IsAntialias = false };
        stripePaint.Color = CityBuildingShapes.ApplyDim(stripeColor, nightDim);
        float stripeY = streetY + streetHeight / 2f - 0.75f;
        float dashLen = hasPremiumPaving ? 12 : 9;
        float dashGap = hasPremiumPaving ? 24 : 18;
        for (float sx = bounds.Left + 8; sx < bounds.Right; sx += dashGap)
        {
            canvas.DrawRect(sx, stripeY, dashLen, 1.5f, stripePaint);
        }
    }

    /// <summary>
    /// Zeichnet Dekorationen entlang der Straße basierend auf WorldTier.
    /// Tier 2+: Büsche, Tier 3+: Bäume, Tier 4+: Laternen, Tier 5+: Bänke, Tier 6+: Blumenbeete.
    /// </summary>
    public static void DrawStreetDecorations(SKCanvas canvas, SKRect bounds, float streetY,
        int worldTier, float nightDim, float time)
    {
        if (worldTier < 2) return;

        float decoY = streetY - 6; // Über der Straße, auf dem Bürgersteig
        float spacing = bounds.Width / 8f; // 7 Deko-Slots

        for (int i = 0; i < 7; i++)
        {
            float x = bounds.Left + spacing * (i + 0.5f);
            int decoHash = i * 7919 + 137;

            // Alternierend verschiedene Dekorationen je nach Tier
            int decoType = decoHash % 6;

            switch (decoType)
            {
                case 0 when worldTier >= 3:
                    // Baum
                    DrawTree(canvas, x, decoY, worldTier, nightDim, time + i * 2.1f);
                    break;

                case 1 when worldTier >= 4:
                    // Laterne
                    DrawLantern(canvas, x, decoY, nightDim, time);
                    break;

                case 2 when worldTier >= 5:
                    // Bank
                    DrawBench(canvas, x, decoY, nightDim);
                    break;

                case 3 when worldTier >= 6:
                    // Blumenbeet
                    DrawFlowerBed(canvas, x, decoY, nightDim, time + i);
                    break;

                case 4 when worldTier >= 2:
                    // Busch
                    DrawBush(canvas, x, decoY, nightDim);
                    break;

                case 5 when worldTier >= 7:
                    // Brunnen (Premium)
                    DrawFountain(canvas, x, decoY, nightDim, time);
                    break;
            }
        }
    }

    /// <summary>
    /// Gibt einen Lebhaftigkeits-Multiplikator für Hintergrundfarben zurück (1.0-1.3).
    /// Höhere Tiers = lebendigere, gesättigtere Farben.
    /// </summary>
    public static float GetVibrancyMultiplier(int worldTier)
    {
        return worldTier switch
        {
            <= 2 => 1.0f,
            <= 4 => 1.08f,
            <= 6 => 1.15f,
            _ => 1.25f,
        };
    }

    /// <summary>
    /// Wendet den Lebhaftigkeits-Multiplikator auf eine Farbe an.
    /// </summary>
    public static SKColor ApplyVibrancy(SKColor color, float vibrancy)
    {
        if (vibrancy <= 1.0f) return color;

        // Sättigung erhöhen (Abstand von Grau verstärken)
        float avg = (color.Red + color.Green + color.Blue) / 3f;
        byte r = (byte)Math.Clamp(avg + (color.Red - avg) * vibrancy, 0, 255);
        byte g = (byte)Math.Clamp(avg + (color.Green - avg) * vibrancy, 0, 255);
        byte b = (byte)Math.Clamp(avg + (color.Blue - avg) * vibrancy, 0, 255);

        return new SKColor(r, g, b, color.Alpha);
    }

    // ═════════════════════════════════════════════════════════════════
    // PRIVATE DEKORATIONS-ZEICHNER
    // ═════════════════════════════════════════════════════════════════

    private static void DrawTree(SKCanvas canvas, float x, float y, int worldTier, float nightDim, float time)
    {
        // Stamm
        _treeTrunkPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x5D, 0x40, 0x37), nightDim);
        canvas.DrawRect(x - 1, y - 8, 2, 8, _treeTrunkPaint);

        // Krone (wird üppiger mit höherem Tier)
        float sway = MathF.Sin(time * 0.8f) * 0.5f; // Leichtes Schwanken im Wind
        float crownSize = worldTier >= 5 ? 6 : 4.5f;

        var leafColor = worldTier switch
        {
            <= 3 => new SKColor(0x66, 0xAA, 0x44),
            <= 5 => new SKColor(0x4C, 0xAF, 0x50),
            _ => new SKColor(0x2E, 0x7D, 0x32)
        };

        _treeLeafPaint.Color = CityBuildingShapes.ApplyDim(leafColor, nightDim);
        canvas.DrawCircle(x + sway, y - 8 - crownSize * 0.6f, crownSize, _treeLeafPaint);

        // Tier 6+: Zweite Krone oben (üppiger Baum)
        if (worldTier >= 6)
        {
            var lighterLeaf = CityBuildingShapes.ApplyDim(
                new SKColor((byte)Math.Min(leafColor.Red + 20, 255), (byte)Math.Min(leafColor.Green + 15, 255), leafColor.Blue),
                nightDim);
            _treeLeafPaint.Color = lighterLeaf;
            canvas.DrawCircle(x + sway * 0.5f, y - 8 - crownSize * 1.3f, crownSize * 0.7f, _treeLeafPaint);
        }
    }

    private static void DrawLantern(SKCanvas canvas, float x, float y, float nightDim, float time)
    {
        // Mast
        _lanternPolePaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x42, 0x42, 0x42), nightDim);
        canvas.DrawRect(x - 0.5f, y - 14, 1, 14, _lanternPolePaint);

        // Laternenkopf
        canvas.DrawRect(x - 2, y - 16, 4, 2.5f, _lanternPolePaint);

        // Licht (nachts heller)
        bool isNight = nightDim < 0.85f;
        float glowAlpha = isNight ? 0.9f : 0.3f;
        float pulseAlpha = glowAlpha + MathF.Sin(time * 3f) * 0.05f;

        _lanternGlowPaint.Color = new SKColor(0xFF, 0xE0, 0x82, (byte)(pulseAlpha * 255));
        canvas.DrawCircle(x, y - 14.5f, isNight ? 5 : 2.5f, _lanternGlowPaint);

        // Lampe selbst (heller Punkt)
        _lanternGlowPaint.Color = new SKColor(0xFF, 0xF1, 0xB8, (byte)(Math.Min(pulseAlpha + 0.2f, 1f) * 255));
        canvas.DrawCircle(x, y - 14.5f, 1.5f, _lanternGlowPaint);
    }

    private static void DrawBench(SKCanvas canvas, float x, float y, float nightDim)
    {
        _benchPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x6D, 0x4C, 0x41), nightDim);
        _benchLegPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x42, 0x42, 0x42), nightDim);

        // Sitzfläche (3 Latten)
        for (int i = 0; i < 3; i++)
        {
            canvas.DrawRect(x - 4, y - 3 - i * 1.2f, 8, 1, _benchPaint);
        }

        // Beine
        canvas.DrawRect(x - 3.5f, y - 3, 1, 3, _benchLegPaint);
        canvas.DrawRect(x + 2.5f, y - 3, 1, 3, _benchLegPaint);
    }

    private static void DrawBush(SKCanvas canvas, float x, float y, float nightDim)
    {
        var bushColor = CityBuildingShapes.ApplyDim(new SKColor(0x4C, 0x8C, 0x3C), nightDim);
        _treeLeafPaint.Color = bushColor;
        canvas.DrawOval(x, y - 2.5f, 4, 2.5f, _treeLeafPaint);

        // Dunklere Mitte für Tiefe
        var darkBush = CityBuildingShapes.ApplyDim(new SKColor(0x38, 0x6B, 0x2C), nightDim);
        _treeLeafPaint.Color = darkBush;
        canvas.DrawOval(x, y - 2, 2.5f, 1.5f, _treeLeafPaint);
    }

    private static void DrawFlowerBed(SKCanvas canvas, float x, float y, float nightDim, float time)
    {
        // Erde
        var earthColor = CityBuildingShapes.ApplyDim(new SKColor(0x5D, 0x40, 0x37), nightDim);
        _treeTrunkPaint.Color = earthColor;
        canvas.DrawRect(x - 5, y - 1, 10, 2, _treeTrunkPaint);

        // 5 Blumen
        for (int f = 0; f < 5; f++)
        {
            float fx = x - 4 + f * 2.2f;
            float fy = y - 2 - MathF.Abs(MathF.Sin(time * 1.2f + f * 1.5f)) * 1f;
            int colorIdx = f % FlowerColors.Length;

            _flowerPaint.Color = CityBuildingShapes.ApplyDim(FlowerColors[colorIdx], nightDim);
            canvas.DrawCircle(fx, fy, 1.2f, _flowerPaint);

            // Stiel
            _treeTrunkPaint.Color = CityBuildingShapes.ApplyDim(new SKColor(0x4C, 0x8C, 0x3C), nightDim);
            canvas.DrawRect(fx - 0.3f, fy + 1, 0.6f, y - 1 - fy - 1, _treeTrunkPaint);
        }
    }

    private static void DrawFountain(SKCanvas canvas, float x, float y, float nightDim, float time)
    {
        // Becken (steingrau)
        var stoneColor = CityBuildingShapes.ApplyDim(new SKColor(0x9E, 0x9E, 0x9E), nightDim);
        using var stonePaint = new SKPaint { Color = stoneColor, IsAntialias = true };
        canvas.DrawOval(x, y - 2, 5, 2.5f, stonePaint);

        // Wasser (blau, animiert)
        var waterColor = CityBuildingShapes.ApplyDim(new SKColor(0x42, 0xA5, 0xF5, 180), nightDim);
        using var waterPaint = new SKPaint { Color = waterColor, IsAntialias = true };
        canvas.DrawOval(x, y - 2.5f, 3.5f, 1.5f, waterPaint);

        // Wasserfontäne (3 Tropfen steigen auf)
        for (int d = 0; d < 3; d++)
        {
            float phase = (time * 2f + d * 1.1f) % 2f;
            if (phase > 1.5f) continue;
            float progress = phase / 1.5f;
            float dy = y - 3 - progress * 6;
            float dx = x + MathF.Sin(d * 2.1f) * 2 * progress;
            byte alpha = (byte)((1f - progress) * 180);
            waterPaint.Color = new SKColor(0x64, 0xB5, 0xF6, alpha);
            canvas.DrawCircle(dx, dy, 0.8f + (1f - progress) * 0.5f, waterPaint);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // STRASSENTEXTUREN
    // ═════════════════════════════════════════════════════════════════

    private static void DrawCobblestoneTexture(SKCanvas canvas, SKRect bounds, float streetY,
        float streetHeight, float nightDim, bool isPremium)
    {
        // Pflasterstein-Muster: Raster aus kleinen Rechtecken mit Fugen
        var fugenColor = CityBuildingShapes.ApplyDim(
            isPremium ? new SKColor(0x54, 0x4D, 0x47) : new SKColor(0x45, 0x45, 0x45), nightDim);
        using var fugenPaint = new SKPaint { Color = fugenColor, IsAntialias = false };

        float stoneW = isPremium ? 8 : 6;
        float stoneH = streetHeight / 2f;

        // Horizontale Fugen
        canvas.DrawRect(bounds.Left, streetY + stoneH - 0.5f, bounds.Width, 1, fugenPaint);

        // Vertikale Fugen (versetzt in zweiter Reihe)
        for (float sx = bounds.Left; sx < bounds.Right; sx += stoneW)
        {
            canvas.DrawRect(sx, streetY, 0.5f, stoneH, fugenPaint);
            canvas.DrawRect(sx + stoneW / 2f, streetY + stoneH, 0.5f, stoneH, fugenPaint);
        }
    }

    private static void DrawGravelTexture(SKCanvas canvas, SKRect bounds, float streetY,
        float streetHeight, float nightDim)
    {
        // Schotter: Vereinzelte helle Punkte
        using var gravelPaint = new SKPaint { IsAntialias = false };
        var lightGravel = CityBuildingShapes.ApplyDim(new SKColor(0x96, 0x86, 0x78), nightDim);
        var darkGravel = CityBuildingShapes.ApplyDim(new SKColor(0x6D, 0x5B, 0x4D), nightDim);

        for (int g = 0; g < 25; g++)
        {
            uint hash = (uint)(g * 2039 + 4177);
            float gx = bounds.Left + (hash % 1000) / 1000f * bounds.Width;
            hash = hash * 1664525 + 1013904223;
            float gy = streetY + 2 + (hash % 100) / 100f * (streetHeight - 4);
            hash = hash * 1664525 + 1013904223;

            gravelPaint.Color = (hash % 2 == 0) ? lightGravel : darkGravel;
            canvas.DrawCircle(gx, gy, 0.8f + (hash % 50) / 100f, gravelPaint);
        }
    }
}
