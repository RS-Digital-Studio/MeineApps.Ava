using SkiaSharp;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Renders a simple isometric-ish city view with workshops and support buildings.
/// Pixel-art style: flat fills, no gradients, no anti-aliasing.
/// Day/night palette shift based on current hour.
/// </summary>
public class CityRenderer
{
    // Workshop-Typ -> Farbe
    private static readonly Dictionary<WorkshopType, SKColor> WorkshopColors = new()
    {
        { WorkshopType.Carpenter, new SKColor(0x8D, 0x65, 0x34) },       // Brown (wood)
        { WorkshopType.Plumber, new SKColor(0x21, 0x96, 0xF3) },         // Blue (water)
        { WorkshopType.Electrician, new SKColor(0xFF, 0xC1, 0x07) },     // Yellow (electricity)
        { WorkshopType.Painter, new SKColor(0xE9, 0x1E, 0x63) },         // Pink (paint)
        { WorkshopType.Roofer, new SKColor(0xF4, 0x43, 0x36) },          // Red (roof tiles)
        { WorkshopType.Contractor, new SKColor(0x60, 0x7D, 0x8B) },      // Blue-grey (concrete)
        { WorkshopType.Architect, new SKColor(0x9C, 0x27, 0xB0) },       // Purple (design)
        { WorkshopType.GeneralContractor, new SKColor(0xFF, 0x98, 0x00) } // Orange (empire)
    };

    // Gebaeude-Typ -> Farbe
    private static readonly Dictionary<BuildingType, SKColor> BuildingColors = new()
    {
        { BuildingType.Canteen, new SKColor(0x4C, 0xAF, 0x50) },            // Green
        { BuildingType.Storage, new SKColor(0x79, 0x55, 0x48) },            // Brown
        { BuildingType.Office, new SKColor(0x42, 0xA5, 0xF5) },             // Light blue
        { BuildingType.Showroom, new SKColor(0xAB, 0x47, 0xBC) },           // Purple
        { BuildingType.TrainingCenter, new SKColor(0xFF, 0x70, 0x43) },     // Deep orange
        { BuildingType.VehicleFleet, new SKColor(0x78, 0x90, 0x9C) },       // Blue-grey
        { BuildingType.WorkshopExtension, new SKColor(0x8D, 0x6E, 0x63) }   // Brown
    };

    /// <summary>
    /// Renders the city view onto the given canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp canvas to draw on.</param>
    /// <param name="bounds">Available drawing area.</param>
    /// <param name="state">Current game state (for unlock checks).</param>
    /// <param name="buildings">List of support buildings.</param>
    public void Render(SKCanvas canvas, SKRect bounds, GameState state, List<Building> buildings)
    {
        float nightDim = GetNightDimFactor();

        // Hintergrund (Himmel)
        var skyColor = ApplyNightDim(new SKColor(0x87, 0xCE, 0xEB), nightDim);
        using (var skyPaint = new SKPaint { Color = skyColor, IsAntialias = false })
        {
            canvas.DrawRect(bounds, skyPaint);
        }

        // Boden
        float groundY = bounds.Top + bounds.Height * 0.55f;
        var groundColor = ApplyNightDim(new SKColor(0x6D, 0x8B, 0x54), nightDim);
        using (var groundPaint = new SKPaint { Color = groundColor, IsAntialias = false })
        {
            canvas.DrawRect(bounds.Left, groundY, bounds.Width, bounds.Height - (groundY - bounds.Top), groundPaint);
        }

        // Strasse (horizontale Linie)
        float streetY = groundY + 8;
        float streetHeight = 12;
        var streetColor = ApplyNightDim(new SKColor(0x61, 0x61, 0x61), nightDim);
        using (var streetPaint = new SKPaint { Color = streetColor, IsAntialias = false })
        {
            canvas.DrawRect(bounds.Left, streetY, bounds.Width, streetHeight, streetPaint);
        }

        // Mittelstreifen
        var stripeColor = ApplyNightDim(new SKColor(0xFF, 0xFF, 0x00), nightDim);
        using (var stripePaint = new SKPaint { Color = stripeColor, IsAntialias = false })
        {
            float stripeY = streetY + streetHeight / 2f - 1;
            for (float x = bounds.Left + 8; x < bounds.Right; x += 20)
            {
                canvas.DrawRect(x, stripeY, 10, 2, stripePaint);
            }
        }

        // Workshops oben (oberhalb der Strasse)
        float workshopRowTop = streetY - 4;
        DrawWorkshopRow(canvas, bounds, state, workshopRowTop, nightDim, above: true);

        // Support-Gebaeude unten (unterhalb der Strasse)
        float buildingRowTop = streetY + streetHeight + 4;
        DrawBuildingRow(canvas, bounds, buildings, buildingRowTop, nightDim);
    }

    private void DrawWorkshopRow(SKCanvas canvas, SKRect bounds, GameState state, float rowBottom, float nightDim, bool above)
    {
        var allTypes = Enum.GetValues<WorkshopType>();
        int count = allTypes.Length;
        if (count == 0) return;

        float totalWidth = bounds.Width - 16;
        float gap = 6;
        float buildingWidth = Math.Max(20, (totalWidth - (count - 1) * gap) / count);

        float x = bounds.Left + 8;
        for (int i = 0; i < count; i++)
        {
            var type = allTypes[i];
            var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
            bool isUnlocked = state.IsWorkshopUnlocked(type);

            // Hoehe skaliert mit Level (min 30, max 90)
            int level = workshop?.Level ?? 0;
            float height = 30 + (level * 1.2f);
            height = Math.Min(height, 90);

            float buildingTop = above ? rowBottom - height : rowBottom;

            if (isUnlocked && workshop != null)
            {
                // Aktiver Workshop: Farbiges Rechteck
                var color = WorkshopColors.GetValueOrDefault(type, new SKColor(0x90, 0x90, 0x90));
                color = ApplyNightDim(color, nightDim);

                using (var fillPaint = new SKPaint { Color = color, IsAntialias = false })
                {
                    canvas.DrawRect(x, buildingTop, buildingWidth, height, fillPaint);
                }

                // Dach (dunklere Linie oben)
                var roofColor = DarkenColor(color, 0.3f);
                using (var roofPaint = new SKPaint { Color = roofColor, IsAntialias = false })
                {
                    canvas.DrawRect(x, buildingTop, buildingWidth, 4, roofPaint);
                }

                // Fenster (kleine helle Quadrate)
                var windowColor = ApplyNightDim(new SKColor(0xFF, 0xF1, 0x76), nightDim);
                // Nachts heller (Licht an)
                int hour = DateTime.Now.Hour;
                if (hour >= 20 || hour < 6)
                    windowColor = new SKColor(0xFF, 0xF1, 0x76);

                using (var windowPaint = new SKPaint { Color = windowColor, IsAntialias = false })
                {
                    float winSize = Math.Min(6, buildingWidth / 5);
                    float winY = buildingTop + 8;
                    float winX1 = x + buildingWidth * 0.25f - winSize / 2;
                    float winX2 = x + buildingWidth * 0.75f - winSize / 2;
                    canvas.DrawRect(winX1, winY, winSize, winSize, windowPaint);
                    canvas.DrawRect(winX2, winY, winSize, winSize, windowPaint);

                    // Zweite Fensterreihe bei groesseren Gebaeuden
                    if (height > 50)
                    {
                        float winY2 = buildingTop + 20;
                        canvas.DrawRect(winX1, winY2, winSize, winSize, windowPaint);
                        canvas.DrawRect(winX2, winY2, winSize, winSize, windowPaint);
                    }
                }

                // Tuer (Rechteck unten Mitte)
                var doorColor = DarkenColor(color, 0.4f);
                using (var doorPaint = new SKPaint { Color = doorColor, IsAntialias = false })
                {
                    float doorW = Math.Min(8, buildingWidth / 4);
                    float doorH = Math.Min(12, height * 0.25f);
                    float doorX = x + (buildingWidth - doorW) / 2;
                    float doorY = buildingTop + height - doorH;
                    canvas.DrawRect(doorX, doorY, doorW, doorH, doorPaint);
                }

                // Level-Text
                using (var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = false })
                using (var font = new SKFont(SKTypeface.Default, 9))
                {
                    string label = $"Lv{workshop.Level}";
                    float textWidth = font.MeasureText(label);
                    float textX = x + (buildingWidth - textWidth) / 2;
                    float textY = buildingTop + height + 10;
                    canvas.DrawText(label, textX, textY, SKTextAlign.Left, font, textPaint);
                }
            }
            else
            {
                // Gesperrter Workshop: Graues Rechteck mit Schloss-Symbol
                var lockedColor = ApplyNightDim(new SKColor(0x60, 0x60, 0x60), nightDim);
                using (var fillPaint = new SKPaint { Color = lockedColor, IsAntialias = false })
                {
                    canvas.DrawRect(x, buildingTop, buildingWidth, 30, fillPaint);
                }

                // Schloss-Symbol (einfaches Kreuz)
                using (var lockPaint = new SKPaint
                {
                    Color = new SKColor(0xA0, 0xA0, 0xA0),
                    IsAntialias = false,
                    StrokeWidth = 2,
                    Style = SKPaintStyle.Stroke
                })
                {
                    float cx = x + buildingWidth / 2;
                    float cy = buildingTop + 15;
                    float armLen = 5;
                    canvas.DrawLine(cx - armLen, cy - armLen, cx + armLen, cy + armLen, lockPaint);
                    canvas.DrawLine(cx + armLen, cy - armLen, cx - armLen, cy + armLen, lockPaint);
                }
            }

            x += buildingWidth + gap;
        }
    }

    private void DrawBuildingRow(SKCanvas canvas, SKRect bounds, List<Building> buildings, float rowTop, float nightDim)
    {
        var allTypes = Enum.GetValues<BuildingType>();
        int count = allTypes.Length;
        if (count == 0) return;

        float totalWidth = bounds.Width - 16;
        float gap = 4;
        float buildingWidth = Math.Max(16, (totalWidth - (count - 1) * gap) / count);
        float buildingHeight = 22;

        float x = bounds.Left + 8;
        for (int i = 0; i < count; i++)
        {
            var type = allTypes[i];
            var building = buildings.FirstOrDefault(b => b.Type == type);
            bool isBuilt = building?.IsBuilt ?? false;

            if (isBuilt)
            {
                var color = BuildingColors.GetValueOrDefault(type, new SKColor(0x80, 0x80, 0x80));
                color = ApplyNightDim(color, nightDim);

                using (var fillPaint = new SKPaint { Color = color, IsAntialias = false })
                {
                    canvas.DrawRect(x, rowTop, buildingWidth, buildingHeight, fillPaint);
                }

                // Level-Punkt-Anzeige (1-5 kleine Quadrate)
                int level = building!.Level;
                using (var dotPaint = new SKPaint { Color = SKColors.White, IsAntialias = false })
                {
                    float dotSize = 3;
                    float dotGap = 2;
                    float totalDotWidth = level * dotSize + (level - 1) * dotGap;
                    float dotX = x + (buildingWidth - totalDotWidth) / 2;
                    float dotY = rowTop + buildingHeight - 6;
                    for (int d = 0; d < level; d++)
                    {
                        canvas.DrawRect(dotX, dotY, dotSize, dotSize, dotPaint);
                        dotX += dotSize + dotGap;
                    }
                }
            }
            else
            {
                // Nicht gebaut: gestricheltes Outline
                var outlineColor = ApplyNightDim(new SKColor(0x80, 0x80, 0x80), nightDim);
                using (var outlinePaint = new SKPaint
                {
                    Color = outlineColor,
                    IsAntialias = false,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
                })
                {
                    canvas.DrawRect(x, rowTop, buildingWidth, buildingHeight, outlinePaint);
                }
            }

            x += buildingWidth + gap;
        }
    }

    /// <summary>
    /// Returns a dim factor for night mode (0.0 = full dim, 1.0 = no dim).
    /// Dark hours: 20-6 → dim by 40%, transition hours: 6-8 and 18-20.
    /// </summary>
    private static float GetNightDimFactor()
    {
        int hour = DateTime.Now.Hour;
        if (hour >= 8 && hour < 18) return 1.0f;     // Daytime: no dim
        if (hour >= 20 || hour < 6) return 0.6f;      // Night: 40% dim
        if (hour >= 6 && hour < 8) return 0.6f + (hour - 6) * 0.2f;  // Dawn transition
        return 1.0f - (hour - 18) * 0.2f;             // Dusk transition
    }

    private static SKColor ApplyNightDim(SKColor color, float factor)
    {
        return new SKColor(
            (byte)(color.Red * factor),
            (byte)(color.Green * factor),
            (byte)(color.Blue * factor),
            color.Alpha);
    }

    private static SKColor DarkenColor(SKColor color, float amount)
    {
        float factor = 1.0f - amount;
        return new SKColor(
            (byte)(color.Red * factor),
            (byte)(color.Green * factor),
            (byte)(color.Blue * factor),
            color.Alpha);
    }
}
