using SkiaSharp;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Isometrische Gebäude-Formen mit Material-Texturen für die City-Szene.
/// Zeichnet 2.5D-Gebäude mit Vorder-/Seitenwand + Dachfläche.
/// Level-abhängige Größe und typ-spezifische Details.
/// </summary>
public static class CityBuildingShapes
{
    // Gecachte Paints (wiederverwendbar)
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true };
    private static readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _detailPaint = new() { IsAntialias = true };
    private static readonly SKPaint _windowPaint = new() { IsAntialias = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Color = SKColors.White };

    // Workshop-Farben (Vorderseite)
    private static readonly Dictionary<WorkshopType, SKColor> FrontColors = new()
    {
        { WorkshopType.Carpenter, new SKColor(0xA0, 0x52, 0x2D) },
        { WorkshopType.Plumber, new SKColor(0x0E, 0x74, 0x90) },
        { WorkshopType.Electrician, new SKColor(0xF9, 0x73, 0x16) },
        { WorkshopType.Painter, new SKColor(0xEC, 0x48, 0x99) },
        { WorkshopType.Roofer, new SKColor(0xDC, 0x26, 0x26) },
        { WorkshopType.Contractor, new SKColor(0xEA, 0x58, 0x0C) },
        { WorkshopType.Architect, new SKColor(0x78, 0x71, 0x6C) },
        { WorkshopType.GeneralContractor, new SKColor(0xFF, 0xD7, 0x00) }
    };

    // Gebäude-Farben
    private static readonly Dictionary<BuildingType, SKColor> BuildingFrontColors = new()
    {
        { BuildingType.Canteen, new SKColor(0x4C, 0xAF, 0x50) },
        { BuildingType.Storage, new SKColor(0x79, 0x55, 0x48) },
        { BuildingType.Office, new SKColor(0x42, 0xA5, 0xF5) },
        { BuildingType.Showroom, new SKColor(0xAB, 0x47, 0xBC) },
        { BuildingType.TrainingCenter, new SKColor(0xFF, 0x70, 0x43) },
        { BuildingType.VehicleFleet, new SKColor(0x78, 0x90, 0x9C) },
        { BuildingType.WorkshopExtension, new SKColor(0x8D, 0x6E, 0x63) }
    };

    /// <summary>
    /// Berechnet die Gebäude-Höhe basierend auf Level (dp).
    /// Lv1=30, Lv100=55, Lv500=75, Lv1000+=90.
    /// </summary>
    public static float GetBuildingHeight(int level)
    {
        if (level <= 0) return 28f;
        if (level <= 100) return 28f + level * 0.27f;
        if (level <= 500) return 55f + (level - 100) * 0.05f;
        return 75f + Math.Min(level - 500, 500) * 0.03f;
    }

    /// <summary>
    /// Zeichnet ein isometrisches Workshop-Gebäude mit Vorderseite, Seite und Dach.
    /// </summary>
    public static void DrawIsometricWorkshop(SKCanvas canvas, float x, float y,
        float width, float height, WorkshopType type, int level, float nightDim, float time)
    {
        var baseColor = FrontColors.GetValueOrDefault(type, new SKColor(0x80, 0x80, 0x80));
        DrawIsometricBuilding(canvas, x, y, width, height, baseColor, nightDim, level);

        // Typ-spezifische Dach-Details
        DrawWorkshopRoofDetail(canvas, x, y, width, height, type, nightDim, time);

        // Fenster
        DrawWindows(canvas, x, y, width, height, nightDim, type.GetHashCode(), time);

        // Tür
        DrawDoor(canvas, x, y, width, height, baseColor, nightDim);

        // Level-Rahmen (Bronze/Silber/Gold/Diamant)
        DrawLevelFrame(canvas, x, y, width, height, level, time);
    }

    /// <summary>
    /// Zeichnet ein isometrisches Support-Gebäude (kleiner, einfacher).
    /// Höhe wird intern aus Level berechnet (18 + level*3 dp).
    /// </summary>
    public static void DrawIsometricBuilding(SKCanvas canvas, float x, float y,
        float width, BuildingType type, int level, float nightDim)
    {
        var baseColor = BuildingFrontColors.GetValueOrDefault(type, new SKColor(0x80, 0x80, 0x80));
        float h = 18f + level * 3f;
        DrawIsometricBuilding(canvas, x, y, width, h, baseColor, nightDim, level);

        // Level-Punkte
        DrawLevelDots(canvas, x, y + h + 2, width, level);
    }

    /// <summary>
    /// Zeichnet ein gesperrtes Gebäude (grau, Schloss-Symbol).
    /// </summary>
    public static void DrawLockedBuilding(SKCanvas canvas, float x, float y,
        float width, float height, float nightDim)
    {
        var lockedColor = ApplyDim(new SKColor(0x50, 0x50, 0x50), nightDim);
        var darkerColor = DarkenColor(lockedColor, 0.3f);

        // Vorderseite (einfaches Rechteck, leicht abgedunkelt)
        _fillPaint.Color = lockedColor;
        canvas.DrawRoundRect(x, y, width, height, 3, 3, _fillPaint);

        // Dach (dunkler Streifen)
        _fillPaint.Color = darkerColor;
        canvas.DrawRoundRect(x, y, width, 5, 3, 3, _fillPaint);

        // Schloss-Symbol (Bügel + Körper)
        float cx = x + width / 2f;
        float cy = y + height / 2f;
        float lockSize = Math.Min(width, height) * 0.2f;

        _strokePaint.Color = new SKColor(0x90, 0x90, 0x90);
        _strokePaint.StrokeWidth = 1.5f;
        // Bügel (Halbkreis)
        canvas.DrawArc(new SKRect(cx - lockSize * 0.6f, cy - lockSize * 1.2f,
            cx + lockSize * 0.6f, cy - lockSize * 0.2f), 180, 180, false, _strokePaint);
        // Körper (Rechteck)
        _fillPaint.Color = new SKColor(0x70, 0x70, 0x70);
        canvas.DrawRect(cx - lockSize * 0.7f, cy - lockSize * 0.3f,
            lockSize * 1.4f, lockSize * 1.0f, _fillPaint);
        // Schlüsselloch
        _fillPaint.Color = new SKColor(0x40, 0x40, 0x40);
        canvas.DrawCircle(cx, cy + lockSize * 0.1f, lockSize * 0.15f, _fillPaint);
    }

    /// <summary>
    /// Zeichnet einen animierten Mini-Arbeiter vor einem Workshop.
    /// </summary>
    public static void DrawMiniWorker(SKCanvas canvas, float x, float y,
        WorkshopType type, float animPhase, float nightDim)
    {
        float figHeight = 10f;

        // Körper-Farbe je nach Workshop
        var bodyColor = type switch
        {
            WorkshopType.Carpenter => new SKColor(0x8B, 0x69, 0x14),    // Braune Schürze
            WorkshopType.Plumber => new SKColor(0x15, 0x65, 0xC0),      // Blaue Latzhose
            WorkshopType.Electrician => new SKColor(0xFD, 0xD8, 0x35),  // Gelbe Weste
            WorkshopType.Painter => new SKColor(0xE0, 0xE0, 0xE0),      // Weiß
            WorkshopType.Roofer => new SKColor(0xE6, 0x51, 0x00),       // Orange
            WorkshopType.Contractor => new SKColor(0x61, 0x61, 0x61),   // Grau
            WorkshopType.Architect => new SKColor(0xF5, 0xF5, 0xF5),    // Weiß (Hemd)
            WorkshopType.GeneralContractor => new SKColor(0x21, 0x21, 0x21), // Schwarz (Anzug)
            _ => new SKColor(0x80, 0x80, 0x80)
        };

        // Leichtes Wippen (Sinus-Animation)
        float bob = MathF.Sin(animPhase * 3f) * 1.5f;

        // Hautfarbe
        _fillPaint.Color = ApplyDim(new SKColor(0xFF, 0xDA, 0xB9), nightDim);
        // Kopf
        canvas.DrawCircle(x, y - figHeight + bob, 3f, _fillPaint);

        // Helm
        var helmColor = type switch
        {
            WorkshopType.Electrician => new SKColor(0xFD, 0xD8, 0x35),
            WorkshopType.Roofer => new SKColor(0xE6, 0x51, 0x00),
            _ => new SKColor(0xFF, 0xEB, 0x3B)
        };
        _fillPaint.Color = ApplyDim(helmColor, nightDim);
        canvas.DrawRect(x - 3.5f, y - figHeight - 2f + bob, 7f, 3f, _fillPaint);

        // Körper
        _fillPaint.Color = ApplyDim(bodyColor, nightDim);
        canvas.DrawRect(x - 2.5f, y - figHeight + 3f + bob, 5f, 5f, _fillPaint);

        // Beine
        _fillPaint.Color = ApplyDim(new SKColor(0x33, 0x33, 0x33), nightDim);
        canvas.DrawRect(x - 2f, y - figHeight + 8f + bob, 2f, 3f, _fillPaint);
        canvas.DrawRect(x + 0.5f, y - figHeight + 8f + bob, 2f, 3f, _fillPaint);
    }

    // ═════════════════════════════════════════════════════════════════
    // Interne Hilfsmethoden
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen isometrischen 2.5D-Block (Vorderseite + Seite + Dach).
    /// </summary>
    private static void DrawIsometricBuilding(SKCanvas canvas, float x, float y,
        float width, float height, SKColor baseColor, float nightDim, int level)
    {
        var frontColor = ApplyDim(baseColor, nightDim);
        var sideColor = ApplyDim(DarkenColor(baseColor, 0.25f), nightDim);
        var roofColor = ApplyDim(LightenColor(baseColor, 0.15f), nightDim);

        float sideOffset = width * 0.15f; // Isometrische Seiten-Verschiebung
        float roofThickness = 4f;

        // Seitenwand (rechts, dunkler - Parallelogramm)
        using var sidePath = new SKPath();
        sidePath.MoveTo(x + width, y);
        sidePath.LineTo(x + width + sideOffset, y - sideOffset * 0.6f);
        sidePath.LineTo(x + width + sideOffset, y - sideOffset * 0.6f + height);
        sidePath.LineTo(x + width, y + height);
        sidePath.Close();
        _fillPaint.Color = sideColor;
        canvas.DrawPath(sidePath, _fillPaint);

        // Vorderseite (Hauptfläche)
        _fillPaint.Color = frontColor;
        canvas.DrawRoundRect(x, y, width, height, 2, 2, _fillPaint);

        // Dach (Trapez oben)
        using var roofPath = new SKPath();
        roofPath.MoveTo(x - 1, y);
        roofPath.LineTo(x + width + 1, y);
        roofPath.LineTo(x + width + sideOffset + 1, y - sideOffset * 0.6f);
        roofPath.LineTo(x + sideOffset - 1, y - sideOffset * 0.6f);
        roofPath.Close();
        _fillPaint.Color = roofColor;
        canvas.DrawPath(roofPath, _fillPaint);

        // Dachrand (dunkle Linie für Tiefe)
        _strokePaint.Color = DarkenColor(frontColor, 0.4f);
        _strokePaint.StrokeWidth = 1f;
        canvas.DrawLine(x - 1, y, x + width + 1, y, _strokePaint);
    }

    /// <summary>
    /// Typ-spezifische Dach-Details (Schornstein, Rohre, etc.).
    /// </summary>
    private static void DrawWorkshopRoofDetail(SKCanvas canvas, float x, float y,
        float width, float height, WorkshopType type, float nightDim, float time)
    {
        float roofY = y - width * 0.15f * 0.6f; // Oben auf dem Dach

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Schornstein (Holzrauch)
                _fillPaint.Color = ApplyDim(new SKColor(0x6D, 0x4C, 0x33), nightDim);
                canvas.DrawRect(x + width * 0.7f, roofY - 10, 6, 10, _fillPaint);
                _fillPaint.Color = ApplyDim(new SKColor(0x5D, 0x3C, 0x23), nightDim);
                canvas.DrawRect(x + width * 0.7f - 1, roofY - 10, 8, 2, _fillPaint);
                break;

            case WorkshopType.Plumber:
                // Rohre auf dem Dach
                _strokePaint.Color = ApplyDim(new SKColor(0x90, 0xA4, 0xAE), nightDim);
                _strokePaint.StrokeWidth = 2f;
                canvas.DrawLine(x + width * 0.3f, roofY - 2, x + width * 0.3f, roofY - 8, _strokePaint);
                canvas.DrawLine(x + width * 0.3f, roofY - 8, x + width * 0.6f, roofY - 8, _strokePaint);
                canvas.DrawLine(x + width * 0.6f, roofY - 8, x + width * 0.6f, roofY - 2, _strokePaint);
                break;

            case WorkshopType.Electrician:
                // Blitzableiter
                _strokePaint.Color = ApplyDim(new SKColor(0xBD, 0xBD, 0xBD), nightDim);
                _strokePaint.StrokeWidth = 1.5f;
                float lightningX = x + width * 0.8f;
                canvas.DrawLine(lightningX, roofY - 2, lightningX, roofY - 14, _strokePaint);
                // Spitze
                _fillPaint.Color = ApplyDim(new SKColor(0xFF, 0xD5, 0x4F), nightDim);
                canvas.DrawCircle(lightningX, roofY - 14, 2f, _fillPaint);
                break;

            case WorkshopType.Painter:
                // Farbtopf auf dem Dach
                _fillPaint.Color = ApplyDim(new SKColor(0xE0, 0x40, 0x40), nightDim);
                canvas.DrawRect(x + width * 0.4f, roofY - 6, 8, 6, _fillPaint);
                // Farbe tropft
                _fillPaint.Color = ApplyDim(new SKColor(0xE0, 0x40, 0x40, 0xA0), nightDim);
                float dripY = roofY + MathF.Sin(time * 2f) * 2f;
                canvas.DrawCircle(x + width * 0.45f, dripY, 1.5f, _fillPaint);
                break;

            case WorkshopType.Roofer:
                // Ziegelmuster auf dem Dach
                _fillPaint.Color = ApplyDim(new SKColor(0xB7, 0x1C, 0x1C), nightDim);
                for (float tx = x + 2; tx < x + width - 2; tx += 6)
                {
                    canvas.DrawRect(tx, roofY - 3, 5, 3, _fillPaint);
                }
                break;

            case WorkshopType.Contractor:
                // Kran-Arm
                _strokePaint.Color = ApplyDim(new SKColor(0xFF, 0xC1, 0x07), nightDim);
                _strokePaint.StrokeWidth = 2f;
                float craneBase = x + width * 0.6f;
                canvas.DrawLine(craneBase, roofY - 2, craneBase, roofY - 16, _strokePaint);
                // Rotierender Arm
                float armAngle = time * 0.3f;
                float armLen = 12f;
                float armEndX = craneBase + MathF.Cos(armAngle) * armLen;
                float armEndY = roofY - 16 + MathF.Sin(armAngle) * 3f;
                canvas.DrawLine(craneBase, roofY - 16, armEndX, armEndY, _strokePaint);
                // Haken
                _strokePaint.StrokeWidth = 1f;
                canvas.DrawLine(armEndX, armEndY, armEndX, armEndY + 5, _strokePaint);
                break;

            case WorkshopType.Architect:
                // Kuppel (Halbkreis)
                _fillPaint.Color = ApplyDim(new SKColor(0x90, 0xCA, 0xF9), nightDim);
                canvas.DrawArc(new SKRect(x + width * 0.3f, roofY - 10,
                    x + width * 0.7f, roofY), 180, 180, true, _fillPaint);
                break;

            case WorkshopType.GeneralContractor:
                // Gold-Dach (Krone bei Lv1000+)
                _fillPaint.Color = ApplyDim(new SKColor(0xFF, 0xD7, 0x00), nightDim);
                // Golddach-Akzent
                canvas.DrawRect(x + 2, roofY - 1, width - 4, 2, _fillPaint);
                break;
        }
    }

    /// <summary>
    /// Fenster mit Tag/Nacht-Effekt und deterministischem Blinkmuster.
    /// </summary>
    private static void DrawWindows(SKCanvas canvas, float x, float y,
        float width, float height, float nightDim, int seedBase, float time)
    {
        int hour = DateTime.Now.Hour;
        bool isNight = hour >= 20 || hour < 6;

        var windowLitColor = new SKColor(0xFF, 0xF1, 0x76, 0xE0);
        var windowDayColor = ApplyDim(new SKColor(0xC0, 0xE0, 0xFF), nightDim);
        float winSize = Math.Min(5, width * 0.14f);
        if (winSize < 2) return;

        // 2 Fenster nebeneinander, bis zu 2 Reihen bei genug Höhe
        float winMarginX = width * 0.2f;
        float winGapX = width * 0.3f;
        float winY1 = y + 8;

        for (int row = 0; row < (height > 40 ? 2 : 1); row++)
        {
            for (int col = 0; col < 2; col++)
            {
                float wx = x + winMarginX + col * winGapX;
                float wy = winY1 + row * 14;

                if (wy + winSize > y + height - 12) continue;

                int windowIdx = seedBase * 4 + row * 2 + col;
                bool lit = isNight && IsWindowLitStatic(windowIdx, time);

                if (isNight && lit)
                {
                    // Fenster-Glow nachts
                    _windowPaint.Color = windowLitColor;
                    canvas.DrawRect(wx, wy, winSize, winSize, _windowPaint);
                    // Subtiler Glow
                    _windowPaint.Color = new SKColor(0xFF, 0xF1, 0x76, 0x30);
                    canvas.DrawRect(wx - 1, wy - 1, winSize + 2, winSize + 2, _windowPaint);
                }
                else
                {
                    _windowPaint.Color = windowDayColor;
                    canvas.DrawRect(wx, wy, winSize, winSize, _windowPaint);
                }

                // Fensterkreuz
                _strokePaint.Color = ApplyDim(new SKColor(0x60, 0x60, 0x60), nightDim);
                _strokePaint.StrokeWidth = 0.5f;
                canvas.DrawLine(wx + winSize / 2, wy, wx + winSize / 2, wy + winSize, _strokePaint);
                canvas.DrawLine(wx, wy + winSize / 2, wx + winSize, wy + winSize / 2, _strokePaint);
            }
        }
    }

    /// <summary>
    /// Tür am unteren Rand des Gebäudes.
    /// </summary>
    private static void DrawDoor(SKCanvas canvas, float x, float y,
        float width, float height, SKColor baseColor, float nightDim)
    {
        var doorColor = ApplyDim(DarkenColor(baseColor, 0.45f), nightDim);
        float doorW = Math.Min(7, width * 0.22f);
        float doorH = Math.Min(10, height * 0.22f);
        float doorX = x + (width - doorW) / 2f;
        float doorY = y + height - doorH;

        _fillPaint.Color = doorColor;
        canvas.DrawRoundRect(doorX, doorY, doorW, doorH, 1, 1, _fillPaint);

        // Türgriff
        _fillPaint.Color = ApplyDim(new SKColor(0xCC, 0xCC, 0x00), nightDim);
        canvas.DrawCircle(doorX + doorW * 0.75f, doorY + doorH * 0.55f, 1f, _fillPaint);
    }

    /// <summary>
    /// Level-Rahmen: Bronze/Silber/Gold/Diamant je nach Level.
    /// </summary>
    private static void DrawLevelFrame(SKCanvas canvas, float x, float y,
        float width, float height, int level, float time)
    {
        if (level < 50) return; // Kein Rahmen unter Level 50

        SKColor frameColor;
        float strokeWidth;

        if (level >= 500)
        {
            // Diamant: Cyan mit Puls
            byte alpha = (byte)(180 + 75 * MathF.Sin(time * 2f));
            frameColor = new SKColor(0xB9, 0xF2, 0xFF, alpha);
            strokeWidth = 2f;
        }
        else if (level >= 250)
        {
            // Gold: Shimmer
            byte alpha = (byte)(200 + 55 * MathF.Sin(time * 1.5f));
            frameColor = new SKColor(0xFF, 0xD7, 0x00, alpha);
            strokeWidth = 1.5f;
        }
        else if (level >= 100)
        {
            // Silber
            frameColor = new SKColor(0xC0, 0xC0, 0xC0, 0xC0);
            strokeWidth = 1.5f;
        }
        else
        {
            // Bronze
            frameColor = new SKColor(0xCD, 0x7F, 0x32, 0xA0);
            strokeWidth = 1f;
        }

        _strokePaint.Color = frameColor;
        _strokePaint.StrokeWidth = strokeWidth;
        canvas.DrawRoundRect(x - 1, y - 1, width + 2, height + 2, 3, 3, _strokePaint);

        // Krone bei Level 1000+
        if (level >= 1000)
        {
            DrawCrown(canvas, x + width / 2f, y - width * 0.15f * 0.6f - 10);
        }
    }

    /// <summary>
    /// Kleine goldene Krone über dem Gebäude (Level 1000+).
    /// </summary>
    private static void DrawCrown(SKCanvas canvas, float cx, float cy)
    {
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        using var path = new SKPath();
        float w = 8f, h = 6f;
        // Krone: 3 Zacken
        path.MoveTo(cx - w, cy + h);
        path.LineTo(cx - w, cy);
        path.LineTo(cx - w * 0.5f, cy + h * 0.4f);
        path.LineTo(cx, cy - h * 0.3f);
        path.LineTo(cx + w * 0.5f, cy + h * 0.4f);
        path.LineTo(cx + w, cy);
        path.LineTo(cx + w, cy + h);
        path.Close();
        canvas.DrawPath(path, _fillPaint);

        // Juwelen (3 kleine Punkte)
        _fillPaint.Color = new SKColor(0xE0, 0x40, 0x40);
        canvas.DrawCircle(cx, cy + h * 0.2f, 1.2f, _fillPaint);
        _fillPaint.Color = new SKColor(0x40, 0x40, 0xE0);
        canvas.DrawCircle(cx - w * 0.6f, cy + h * 0.5f, 1f, _fillPaint);
        canvas.DrawCircle(cx + w * 0.6f, cy + h * 0.5f, 1f, _fillPaint);
    }

    /// <summary>
    /// Level-Punkt-Anzeige für Support-Gebäude (1-5 kleine Kreise).
    /// </summary>
    private static void DrawLevelDots(SKCanvas canvas, float x, float y, float width, int level)
    {
        if (level <= 0) return;
        int dots = Math.Min(level, 5);
        float dotSize = 2f;
        float dotGap = 3f;
        float totalWidth = dots * dotSize * 2 + (dots - 1) * dotGap;
        float startX = x + (width - totalWidth) / 2f;

        _fillPaint.Color = SKColors.White;
        for (int i = 0; i < dots; i++)
        {
            canvas.DrawCircle(startX + i * (dotSize * 2 + dotGap) + dotSize, y + dotSize, dotSize, _fillPaint);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // Farb-Hilfsmethoden (statisch, wiederverwendbar)
    // ═════════════════════════════════════════════════════════════════

    public static SKColor ApplyDim(SKColor color, float factor)
    {
        return new SKColor(
            (byte)(color.Red * factor),
            (byte)(color.Green * factor),
            (byte)(color.Blue * factor),
            color.Alpha);
    }

    public static SKColor DarkenColor(SKColor color, float amount)
    {
        float f = 1f - amount;
        return new SKColor(
            (byte)(color.Red * f),
            (byte)(color.Green * f),
            (byte)(color.Blue * f),
            color.Alpha);
    }

    public static SKColor LightenColor(SKColor color, float amount)
    {
        return new SKColor(
            (byte)Math.Min(255, color.Red + (255 - color.Red) * amount),
            (byte)Math.Min(255, color.Green + (255 - color.Green) * amount),
            (byte)Math.Min(255, color.Blue + (255 - color.Blue) * amount),
            color.Alpha);
    }

    /// <summary>
    /// Deterministisches Fenster-Blinken (kein Random, zeit-basiert).
    /// </summary>
    private static bool IsWindowLitStatic(int windowIdx, float time)
    {
        float freq = 0.5f + (windowIdx % 4) * 0.3f;
        return ((int)(time * freq + windowIdx * 3.7f) % 3) != 0;
    }
}
