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

    // Gecachte Path-Objekte (vermeidet Allokationen pro Frame, nur UI-Thread)
    private static readonly SKPath _sidePath = new();
    private static readonly SKPath _roofPath = new();
    private static readonly SKPath _crownPath = new();
    private static readonly SKPath _iconPath = new();

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
        { WorkshopType.GeneralContractor, new SKColor(0xFF, 0xD7, 0x00) },
        { WorkshopType.MasterSmith, new SKColor(0xD4, 0xA3, 0x73) },
        { WorkshopType.InnovationLab, new SKColor(0x6A, 0x5A, 0xCD) }
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
    /// Workshop-Farbe für externe Nutzung (z.B. Fahnen, Spotlight).
    /// </summary>
    public static SKColor GetWorkshopColor(WorkshopType type)
        => FrontColors.GetValueOrDefault(type, new SKColor(0x80, 0x80, 0x80));

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
            WorkshopType.MasterSmith => new SKColor(0x6D, 0x4C, 0x41),       // Braune Lederschürze
            WorkshopType.InnovationLab => new SKColor(0xE0, 0xE0, 0xF0),     // Weißer Laborkittel
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
            WorkshopType.MasterSmith => new SKColor(0xD4, 0xA3, 0x73),       // Kupfer-Kappe
            WorkshopType.InnovationLab => new SKColor(0x6A, 0x5A, 0xCD),     // Violett-Brille
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
    // WORKSHOP-MINI-ICONS (unter dem Level-Label in der City-Szene)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein kleines (8x8dp) Workshop-spezifisches Vektor-Icon.
    /// Wird direkt unter dem "LvX"-Text in der City-Skyline angezeigt.
    /// </summary>
    public static void DrawWorkshopMiniIcon(SKCanvas canvas, float centerX, float y,
        WorkshopType type, float nightDim)
    {
        var color = ApplyDim(GetWorkshopColor(type), nightDim);
        _detailPaint.Color = color;
        _detailPaint.Style = SKPaintStyle.Fill;

        const float s = 6f; // Halbe Icon-Größe (12dp total)

        switch (type)
        {
            case WorkshopType.Carpenter:
                // Sägeblatt (Kreis mit Zähnen)
                _detailPaint.Style = SKPaintStyle.Stroke;
                _detailPaint.StrokeWidth = 1.2f;
                canvas.DrawCircle(centerX, y, s * 0.7f, _detailPaint);
                _detailPaint.Style = SKPaintStyle.Fill;
                // Zähne (4 kleine Dreiecke am Rand)
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * MathF.PI / 2f;
                    float tx = centerX + MathF.Cos(angle) * s * 0.7f;
                    float ty = y + MathF.Sin(angle) * s * 0.7f;
                    canvas.DrawCircle(tx, ty, 1f, _detailPaint);
                }
                break;

            case WorkshopType.Plumber:
                // Wassertropfen (gecachter Path)
                _iconPath.Reset();
                _iconPath.MoveTo(centerX, y - s * 0.8f);
                _iconPath.QuadTo(centerX + s * 0.6f, y + s * 0.2f, centerX, y + s * 0.7f);
                _iconPath.QuadTo(centerX - s * 0.6f, y + s * 0.2f, centerX, y - s * 0.8f);
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                break;

            case WorkshopType.Electrician:
                // Blitz-Symbol (gecachter Path)
                _iconPath.Reset();
                _iconPath.MoveTo(centerX + s * 0.1f, y - s * 0.9f);
                _iconPath.LineTo(centerX - s * 0.4f, y + s * 0.1f);
                _iconPath.LineTo(centerX + s * 0.1f, y + s * 0.1f);
                _iconPath.LineTo(centerX - s * 0.1f, y + s * 0.9f);
                _iconPath.LineTo(centerX + s * 0.4f, y - s * 0.1f);
                _iconPath.LineTo(centerX - s * 0.1f, y - s * 0.1f);
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                break;

            case WorkshopType.Painter:
                // Farbpinsel (Griff + Borsten)
                _detailPaint.Color = ApplyDim(new SKColor(0x8B, 0x69, 0x14), nightDim);
                canvas.DrawRect(centerX - 0.8f, y - s * 0.8f, 1.6f, s * 1.0f, _detailPaint);
                _detailPaint.Color = color;
                canvas.DrawRoundRect(centerX - s * 0.4f, y + s * 0.2f,
                    s * 0.8f, s * 0.6f, 1, 1, _detailPaint);
                break;

            case WorkshopType.Roofer:
                // Dachgiebel-Silhouette (gecachter Path)
                _iconPath.Reset();
                _iconPath.MoveTo(centerX, y - s * 0.7f);
                _iconPath.LineTo(centerX + s, y + s * 0.3f);
                _iconPath.LineTo(centerX + s * 0.7f, y + s * 0.3f);
                _iconPath.LineTo(centerX + s * 0.7f, y + s * 0.7f);
                _iconPath.LineTo(centerX - s * 0.7f, y + s * 0.7f);
                _iconPath.LineTo(centerX - s * 0.7f, y + s * 0.3f);
                _iconPath.LineTo(centerX - s, y + s * 0.3f);
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                break;

            case WorkshopType.Contractor:
                // Bauhelm (gecachter Path)
                _iconPath.Reset();
                _iconPath.AddArc(new SKRect(centerX - s * 0.8f, y - s * 0.6f,
                    centerX + s * 0.8f, y + s * 0.4f), 180, 180);
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                // Krempe
                canvas.DrawRect(centerX - s, y + s * 0.2f, s * 2, s * 0.25f, _detailPaint);
                break;

            case WorkshopType.Architect:
                // Winkelmesser/Zirkel (gecachter Path)
                _detailPaint.Style = SKPaintStyle.Stroke;
                _detailPaint.StrokeWidth = 1.2f;
                _iconPath.Reset();
                _iconPath.MoveTo(centerX, y - s * 0.8f);
                _iconPath.LineTo(centerX - s * 0.7f, y + s * 0.7f);
                _iconPath.LineTo(centerX + s * 0.7f, y + s * 0.7f);
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                _detailPaint.Style = SKPaintStyle.Fill;
                break;

            case WorkshopType.GeneralContractor:
                // Gold-Stern (gecachter Path)
                _detailPaint.Color = ApplyDim(new SKColor(0xFF, 0xD7, 0x00), nightDim);
                _iconPath.Reset();
                for (int i = 0; i < 5; i++)
                {
                    float outerAngle = -MathF.PI / 2f + i * 2f * MathF.PI / 5f;
                    float innerAngle = outerAngle + MathF.PI / 5f;
                    float ox = centerX + MathF.Cos(outerAngle) * s * 0.8f;
                    float oy = y + MathF.Sin(outerAngle) * s * 0.8f;
                    float ix = centerX + MathF.Cos(innerAngle) * s * 0.35f;
                    float iy = y + MathF.Sin(innerAngle) * s * 0.35f;
                    if (i == 0) _iconPath.MoveTo(ox, oy);
                    else _iconPath.LineTo(ox, oy);
                    _iconPath.LineTo(ix, iy);
                }
                _iconPath.Close();
                canvas.DrawPath(_iconPath, _detailPaint);
                break;

            case WorkshopType.MasterSmith:
                // Amboss-Silhouette
                canvas.DrawRect(centerX - s * 0.8f, y + s * 0.1f, s * 1.6f, s * 0.4f, _detailPaint);
                canvas.DrawRect(centerX - s * 0.4f, y - s * 0.5f, s * 0.8f, s * 0.6f, _detailPaint);
                canvas.DrawRect(centerX - s * 0.6f, y - s * 0.5f, s * 1.2f, s * 0.2f, _detailPaint);
                break;

            case WorkshopType.InnovationLab:
                // Glühbirne
                _detailPaint.Style = SKPaintStyle.Stroke;
                _detailPaint.StrokeWidth = 1.2f;
                canvas.DrawCircle(centerX, y - s * 0.15f, s * 0.55f, _detailPaint);
                _detailPaint.Style = SKPaintStyle.Fill;
                // Sockel
                canvas.DrawRect(centerX - s * 0.25f, y + s * 0.4f, s * 0.5f, s * 0.35f, _detailPaint);
                // Filament
                _detailPaint.Style = SKPaintStyle.Stroke;
                _detailPaint.StrokeWidth = 0.8f;
                canvas.DrawLine(centerX - s * 0.15f, y + s * 0.1f,
                    centerX + s * 0.15f, y - s * 0.2f, _detailPaint);
                _detailPaint.Style = SKPaintStyle.Fill;
                break;
        }
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

        // Seitenwand (rechts, dunkler - Parallelogramm, gecachter Path)
        _sidePath.Reset();
        _sidePath.MoveTo(x + width, y);
        _sidePath.LineTo(x + width + sideOffset, y - sideOffset * 0.6f);
        _sidePath.LineTo(x + width + sideOffset, y - sideOffset * 0.6f + height);
        _sidePath.LineTo(x + width, y + height);
        _sidePath.Close();
        _fillPaint.Color = sideColor;
        canvas.DrawPath(_sidePath, _fillPaint);

        // Vorderseite (Hauptfläche)
        _fillPaint.Color = frontColor;
        canvas.DrawRoundRect(x, y, width, height, 2, 2, _fillPaint);

        // Dach (Trapez oben, gecachter Path)
        _roofPath.Reset();
        _roofPath.MoveTo(x - 1, y);
        _roofPath.LineTo(x + width + 1, y);
        _roofPath.LineTo(x + width + sideOffset + 1, y - sideOffset * 0.6f);
        _roofPath.LineTo(x + sideOffset - 1, y - sideOffset * 0.6f);
        _roofPath.Close();
        _fillPaint.Color = roofColor;
        canvas.DrawPath(_roofPath, _fillPaint);

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

            case WorkshopType.MasterSmith:
                // Esse-Schornstein mit Rauch
                _fillPaint.Color = ApplyDim(new SKColor(0x5C, 0x3A, 0x21), nightDim);
                canvas.DrawRect(x + width * 0.65f, roofY - 14, 8, 14, _fillPaint);
                // Schornsteinkopf
                _fillPaint.Color = ApplyDim(new SKColor(0x4A, 0x2E, 0x18), nightDim);
                canvas.DrawRect(x + width * 0.65f - 1, roofY - 14, 10, 3, _fillPaint);
                // Rauch-Partikel (statisch angedeutet)
                _fillPaint.Color = ApplyDim(new SKColor(0x90, 0x90, 0x90, 0x40), nightDim);
                float smokeX = x + width * 0.69f;
                canvas.DrawCircle(smokeX, roofY - 18, 3f, _fillPaint);
                canvas.DrawCircle(smokeX + 2, roofY - 23, 2.5f, _fillPaint);
                // Amboss-Silhouette auf dem Dach
                _fillPaint.Color = ApplyDim(new SKColor(0xD4, 0xA3, 0x73, 0x60), nightDim);
                canvas.DrawRect(x + width * 0.15f, roofY - 5, 12, 4, _fillPaint);
                canvas.DrawRect(x + width * 0.17f, roofY - 8, 8, 4, _fillPaint);
                break;

            case WorkshopType.InnovationLab:
                // Teleskop/Antenne
                _strokePaint.Color = ApplyDim(new SKColor(0x90, 0x90, 0xA0), nightDim);
                _strokePaint.StrokeWidth = 1.5f;
                float antennaX = x + width * 0.75f;
                canvas.DrawLine(antennaX, roofY - 2, antennaX, roofY - 16, _strokePaint);
                // Antennenkopf (Schüssel)
                canvas.DrawArc(new SKRect(antennaX - 4, roofY - 18, antennaX + 4, roofY - 14),
                    200, 140, false, _strokePaint);
                // Leuchtende Kuppel (Halbkreis, violett)
                float domeAlpha = (byte)(0x60 + 0x30 * MathF.Sin(time * 2f));
                _fillPaint.Color = ApplyDim(new SKColor(0x6A, 0x5A, 0xCD, (byte)domeAlpha), nightDim);
                canvas.DrawArc(new SKRect(x + width * 0.2f, roofY - 8,
                    x + width * 0.55f, roofY + 2), 180, 180, true, _fillPaint);
                // Glow der Kuppel
                _fillPaint.Color = ApplyDim(new SKColor(0x6A, 0x5A, 0xCD, 0x20), nightDim);
                canvas.DrawArc(new SKRect(x + width * 0.15f, roofY - 10,
                    x + width * 0.6f, roofY + 4), 180, 180, true, _fillPaint);
                break;
        }
    }

    /// <summary>
    /// Fenster mit Tag/Nacht-Effekt und deterministischem Blinkmuster.
    /// </summary>
    private static void DrawWindows(SKCanvas canvas, float x, float y,
        float width, float height, float nightDim, int seedBase, float time)
    {
        // Lokalzeit für visuelle Darstellung (Fenster-Beleuchtung nachts)
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

                // Primzahl-basierter Hash für gut verteilte, deterministische Flicker-Phasen
                int windowIdx = seedBase * 73 + col * 37 + row * 19;
                bool lit = isNight && IsWindowLitStatic(windowIdx, time);

                if (isNight && lit)
                {
                    // Warmer radialer Glow der aus dem Fenster strahlt
                    float glowRadius = winSize * 1.8f;
                    float centerWx = wx + winSize / 2f;
                    float centerWy = wy + winSize / 2f;
                    using var glowShader = SKShader.CreateRadialGradient(
                        new SKPoint(centerWx, centerWy), glowRadius,
                        [new SKColor(0xFF, 0xE0, 0x82, 0x50), new SKColor(0xFF, 0xA5, 0x00, 0x00)],
                        [0f, 1f], SKShaderTileMode.Clamp);
                    _windowPaint.Shader = glowShader;
                    canvas.DrawCircle(centerWx, centerWy, glowRadius, _windowPaint);
                    _windowPaint.Shader = null;

                    // Heller Fensterkern
                    _windowPaint.Color = windowLitColor;
                    canvas.DrawRect(wx, wy, winSize, winSize, _windowPaint);
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
        _crownPath.Reset();
        float w = 8f, h = 6f;
        // Krone: 3 Zacken (gecachter Path)
        _crownPath.MoveTo(cx - w, cy + h);
        _crownPath.LineTo(cx - w, cy);
        _crownPath.LineTo(cx - w * 0.5f, cy + h * 0.4f);
        _crownPath.LineTo(cx, cy - h * 0.3f);
        _crownPath.LineTo(cx + w * 0.5f, cy + h * 0.4f);
        _crownPath.LineTo(cx + w, cy);
        _crownPath.LineTo(cx + w, cy + h);
        _crownPath.Close();
        canvas.DrawPath(_crownPath, _fillPaint);

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
    /// Deterministisches Fenster-Blinken nachts (kein Random, Sin-basiert).
    /// Jedes Fenster hat eine eigene Phase, ~65% der Zeit beleuchtet.
    /// Erzeugt natürliches, versetztes Flackern ohne Random.
    /// </summary>
    private static bool IsWindowLitStatic(int windowIdx, float time)
    {
        // Hash aus buildingIndex (in seedBase codiert) + Fenster-Index für deterministische Phase
        float flickerPhase = (windowIdx % 100) / 100f * MathF.PI * 2;
        return MathF.Sin(time * 0.3f + flickerPhase) > -0.3f; // ~65% der Zeit an
    }
}
