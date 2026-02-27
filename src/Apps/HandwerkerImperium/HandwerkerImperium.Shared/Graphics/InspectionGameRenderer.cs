using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Datenstruktur fuer eine einzelne Inspektions-Zelle (View -> Renderer).
/// </summary>
public struct InspectionCellData
{
    public string Icon;
    public bool IsDefect;
    public bool IsDefectFound;
    public bool IsFalseAlarm;
    public bool IsInspected;
    public float ContentOpacity;
    public SKColor BackgroundColor;
}

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Bauabnahme-Minigame (Inspection).
/// Baustellen-Optik: Betongrau mit Rissen, Kacheln mit Baustellen-Elementen,
/// Staub-Partikel, Lupe als Deko, Mangel-Hinweis per subtiles Schimmern.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// Entdeckungs-Effekte (gruene/rote Partikel), pulsierende Lupe,
/// Completion-Effekt wenn alle Defekte gefunden.
/// </summary>
public class InspectionGameRenderer
{
    // Animationszeit
    private float _time;

    // Hintergrund-Farben (Beton-Baustelle)
    private static readonly SKColor ConcreteBase = new(0x60, 0x7D, 0x8B);       // Betongrau
    private static readonly SKColor ConcreteDark = new(0x45, 0x5A, 0x64);       // Dunklerer Beton
    private static readonly SKColor CrackColor = new(0x37, 0x47, 0x4F, 100);    // Risse
    private static readonly SKColor GridLineColor = new(0x78, 0x90, 0x9C, 80);  // Grid-Linien

    // Zellen-Farben
    private static readonly SKColor CellNormal = new(0x37, 0x47, 0x4F);         // Uninspiziert
    private static readonly SKColor CellDefectFound = new(0x2E, 0x7D, 0x32);    // Gruener Hintergrund
    private static readonly SKColor CellFalseAlarm = new(0xC6, 0x28, 0x28);     // Roter Hintergrund
    private static readonly SKColor CellInspected = new(0x4E, 0x5B, 0x65);      // Schon inspiziert (kein Defekt)

    // Rahmenfarben
    private static readonly SKColor BorderNormal = new(0x78, 0x90, 0x9C);
    private static readonly SKColor BorderDefectFound = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor BorderFalseAlarm = new(0xF4, 0x43, 0x36);

    // Akzent-Farben
    private static readonly SKColor CheckmarkGreen = new(0x66, 0xBB, 0x6A);
    private static readonly SKColor CrossRed = new(0xEF, 0x53, 0x50);
    private static readonly SKColor DefectShimmer = new(0xFF, 0x17, 0x44, 25);  // Subtil

    // Lupe-Farben
    private static readonly SKColor MagnifierRing = new(0xB0, 0xBE, 0xC5);
    private static readonly SKColor MagnifierGlass = new(0x42, 0xA5, 0xF5, 40);
    private static readonly SKColor MagnifierHandle = new(0x5D, 0x40, 0x37);

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel-System (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_DUST = 15;
    private const int MAX_SPARKS = 30;
    private const int MAX_TRACKED_CELLS = 30;

    // Staub-Partikel (Struct-Array statt List)
    private struct DustParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte Alpha;
    }

    private readonly DustParticle[] _dust = new DustParticle[MAX_DUST];
    private int _dustCount;

    // Entdeckungs-Funken (gruen bei Defekt gefunden, rot bei Fehlalarm)
    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
    }

    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;

    // Zustandsverfolgung fuer Entdeckungs-Erkennung
    private readonly bool[] _prevDefectFound = new bool[MAX_TRACKED_CELLS];
    private readonly bool[] _prevFalseAlarm = new bool[MAX_TRACKED_CELLS];

    /// <summary>
    /// Rendert das gesamte Inspektions-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, InspectionCellData[] cells, int cols, int rows,
        bool isPlaying, int defectsFound, int totalDefects, float deltaTime)
    {
        _time += deltaTime;

        // Hintergrund: Beton mit Rissen
        DrawBackground(canvas, bounds);

        // Grid-Bereich berechnen (zentriert, mit Padding)
        float padding = 12;
        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding;

        // Zellengroesse berechnen (quadratisch, passend zum Grid)
        float cellSize = Math.Min(availableWidth / cols, availableHeight / rows);
        float spacing = 3;
        float effectiveCellSize = cellSize - spacing;

        float gridWidth = cols * cellSize;
        float gridHeight = rows * cellSize;
        float gridLeft = bounds.Left + (bounds.Width - gridWidth) / 2;
        // Oben ausrichten statt vertikal zentrieren (bessere Platznutzung)
        float gridTop = bounds.Top + padding;

        // Grid-Linien
        DrawGridLines(canvas, gridLeft, gridTop, gridWidth, gridHeight, cols, rows, cellSize);

        // Zellen zeichnen
        if (cells != null)
        {
            for (int i = 0; i < cells.Length && i < cols * rows; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float cellX = gridLeft + col * cellSize + spacing / 2;
                float cellY = gridTop + row * cellSize + spacing / 2;
                DrawCell(canvas, cellX, cellY, effectiveCellSize, effectiveCellSize, cells[i], isPlaying);
            }

            // Entdeckungs-Effekte pruefen und spawnen
            if (isPlaying)
            {
                DetectNewDiscoveries(cells, gridLeft, gridTop, cellSize, spacing, cols);
            }
        }

        // Lupe/Inspektor-Deko (oben rechts, pulsierend)
        DrawMagnifier(canvas, bounds.Right - 44, bounds.Top + 12, isPlaying);

        // Staub-Partikel
        if (isPlaying)
        {
            UpdateAndDrawDust(canvas, bounds, deltaTime);
        }

        // Entdeckungs-Funken zeichnen
        UpdateAndDrawSparks(canvas, deltaTime);
    }

    /// <summary>
    /// HitTest: Gibt den Zell-Index zurueck oder -1 wenn kein Treffer.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int rows)
    {
        float padding = 12;
        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding;

        float cellSize = Math.Min(availableWidth / cols, availableHeight / rows);

        float gridWidth = cols * cellSize;
        float gridHeight = rows * cellSize;
        float gridLeft = bounds.Left + (bounds.Width - gridWidth) / 2;
        // Oben ausrichten (identisch mit Render)
        float gridTop = bounds.Top + padding;

        // Pruefen ob Touch im Grid-Bereich liegt
        if (touchX < gridLeft || touchX >= gridLeft + gridWidth ||
            touchY < gridTop || touchY >= gridTop + gridHeight)
        {
            return -1;
        }

        int col = (int)((touchX - gridLeft) / cellSize);
        int row = (int)((touchY - gridTop) / cellSize);

        col = Math.Clamp(col, 0, cols - 1);
        row = Math.Clamp(row, 0, rows - 1);

        return row * cols + col;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet Beton-Hintergrund mit Rissen und Texturen.
    /// </summary>
    private void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        // Grundfarbe: Betongrau
        using var bgPaint = new SKPaint { Color = ConcreteBase, IsAntialias = false };
        canvas.DrawRect(bounds, bgPaint);

        // Beton-Textur: Horizontale Streifen fuer Fugen
        using var stripePaint = new SKPaint { Color = ConcreteDark, IsAntialias = false, StrokeWidth = 1 };
        for (float y = bounds.Top + 30; y < bounds.Bottom; y += 40)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, stripePaint);
        }

        // Risse im Beton (deterministische Positionen)
        using var crackPaint = new SKPaint { Color = CrackColor, IsAntialias = false, StrokeWidth = 1 };

        // Riss 1: Oben links
        float cx1 = bounds.Left + bounds.Width * 0.15f;
        float cy1 = bounds.Top + bounds.Height * 0.2f;
        canvas.DrawLine(cx1, cy1, cx1 + 18, cy1 + 12, crackPaint);
        canvas.DrawLine(cx1 + 18, cy1 + 12, cx1 + 14, cy1 + 28, crackPaint);
        canvas.DrawLine(cx1 + 18, cy1 + 12, cx1 + 30, cy1 + 8, crackPaint);

        // Riss 2: Unten rechts
        float cx2 = bounds.Right - bounds.Width * 0.2f;
        float cy2 = bounds.Bottom - bounds.Height * 0.25f;
        canvas.DrawLine(cx2, cy2, cx2 - 10, cy2 + 16, crackPaint);
        canvas.DrawLine(cx2, cy2, cx2 + 12, cy2 + 10, crackPaint);

        // Riss 3: Mitte oben
        float cx3 = bounds.MidX + 20;
        float cy3 = bounds.Top + 10;
        canvas.DrawLine(cx3, cy3, cx3 + 8, cy3 + 14, crackPaint);
        canvas.DrawLine(cx3 + 8, cy3 + 14, cx3 + 20, cy3 + 18, crackPaint);

        // Kleine Beton-Kratzer
        using var scratchPaint = new SKPaint { Color = new SKColor(0x50, 0x60, 0x68, 60), IsAntialias = false, StrokeWidth = 1 };
        canvas.DrawLine(bounds.Left + 40, bounds.Bottom - 20, bounds.Left + 70, bounds.Bottom - 22, scratchPaint);
        canvas.DrawLine(bounds.Right - 60, bounds.Top + 50, bounds.Right - 30, bounds.Top + 48, scratchPaint);
    }

    /// <summary>
    /// Zeichnet subtile Hilfslinien fuer das Grid.
    /// </summary>
    private static void DrawGridLines(SKCanvas canvas, float gridLeft, float gridTop, float gridWidth, float gridHeight, int cols, int rows, float cellSize)
    {
        using var linePaint = new SKPaint { Color = GridLineColor, IsAntialias = false, StrokeWidth = 1 };

        // Vertikale Linien
        for (int c = 0; c <= cols; c++)
        {
            float x = gridLeft + c * cellSize;
            canvas.DrawLine(x, gridTop, x, gridTop + gridHeight, linePaint);
        }

        // Horizontale Linien
        for (int r = 0; r <= rows; r++)
        {
            float y = gridTop + r * cellSize;
            canvas.DrawLine(gridLeft, y, gridLeft + gridWidth, y, linePaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ZELLEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine einzelne Inspektions-Zelle mit Icon, Rahmen und Status-Overlay.
    /// </summary>
    private void DrawCell(SKCanvas canvas, float x, float y, float w, float h, InspectionCellData cell, bool isPlaying)
    {
        float cornerRadius = 4;

        // Hintergrundfarbe bestimmen
        SKColor bgColor;
        if (cell.IsDefectFound)
            bgColor = CellDefectFound;
        else if (cell.IsFalseAlarm)
            bgColor = CellFalseAlarm;
        else if (cell.IsInspected)
            bgColor = CellInspected;
        else
            bgColor = CellNormal;

        // Zellen-Hintergrund (abgerundet)
        using var bgPaint = new SKPaint { Color = bgColor, IsAntialias = false };
        var cellRect = new SKRect(x, y, x + w, y + h);
        canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, bgPaint);

        // Rahmenfarbe bestimmen
        SKColor borderColor;
        if (cell.IsDefectFound)
            borderColor = BorderDefectFound;
        else if (cell.IsFalseAlarm)
            borderColor = BorderFalseAlarm;
        else
            borderColor = BorderNormal;

        // Rahmen zeichnen (2px bei inspiziert, 1px sonst)
        float borderWidth = cell.IsInspected ? 2.5f : 1;
        using var borderPaint = new SKPaint { Color = borderColor, IsAntialias = false, StrokeWidth = borderWidth, Style = SKPaintStyle.Stroke };
        canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, borderPaint);

        // Subtiles Mangel-Schimmern (nur fuer unentdeckte Defekte, leicht pulsierend)
        if (cell.IsDefect && !cell.IsInspected && isPlaying)
        {
            float shimmerPulse = (float)(0.3 + 0.7 * Math.Sin(_time * 2.5 + x * 0.1));
            byte shimmerAlpha = (byte)(DefectShimmer.Alpha * shimmerPulse);
            using var shimmerPaint = new SKPaint { Color = DefectShimmer.WithAlpha(shimmerAlpha), IsAntialias = false };
            canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, shimmerPaint);
        }

        // Icon zeichnen (zentriert in der Zelle)
        if (!string.IsNullOrEmpty(cell.Icon))
        {
            float iconSize = Math.Min(w, h) * 0.45f;
            float iconCx = x + w / 2;
            float iconCy = y + h / 2;
            byte alpha = (byte)(255 * cell.ContentOpacity);
            DrawCellIcon(canvas, iconCx, iconCy, iconSize, cell.Icon, alpha);
        }

        // Haekchen oben rechts bei gefundenem Defekt
        if (cell.IsDefectFound)
        {
            DrawCheckmark(canvas, x + w - 14, y + 2, 12);
        }

        // X-Markierung oben rechts bei Fehlalarm
        if (cell.IsFalseAlarm)
        {
            DrawCrossMark(canvas, x + w - 14, y + 2, 12);
        }
    }

    /// <summary>
    /// Zeichnet ein Pixel-Art Haekchen (gruener Kreis mit weissem Check).
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float x, float y, float size)
    {
        // Gruener Kreis-Hintergrund
        float centerX = x + size / 2;
        float centerY = y + size / 2;
        float radius = size / 2;
        using var circlePaint = new SKPaint { Color = CheckmarkGreen, IsAntialias = false };
        canvas.DrawCircle(centerX, centerY, radius, circlePaint);

        // Weisses Haekchen (2 Linien: kurz links, lang rechts)
        using var checkPaint = new SKPaint { Color = SKColors.White, IsAntialias = false, StrokeWidth = 2, StrokeCap = SKStrokeCap.Square };
        float s = size * 0.25f; // Skalierungsfaktor
        canvas.DrawLine(centerX - s * 1.2f, centerY, centerX - s * 0.2f, centerY + s, checkPaint);
        canvas.DrawLine(centerX - s * 0.2f, centerY + s, centerX + s * 1.5f, centerY - s * 0.8f, checkPaint);
    }

    /// <summary>
    /// Zeichnet eine Pixel-Art X-Markierung (roter Kreis mit weissem X).
    /// </summary>
    private static void DrawCrossMark(SKCanvas canvas, float x, float y, float size)
    {
        // Roter Kreis-Hintergrund
        float centerX = x + size / 2;
        float centerY = y + size / 2;
        float radius = size / 2;
        using var circlePaint = new SKPaint { Color = CrossRed, IsAntialias = false };
        canvas.DrawCircle(centerX, centerY, radius, circlePaint);

        // Weisses X (2 diagonale Linien)
        using var xPaint = new SKPaint { Color = SKColors.White, IsAntialias = false, StrokeWidth = 2, StrokeCap = SKStrokeCap.Square };
        float offset = size * 0.25f;
        canvas.DrawLine(centerX - offset, centerY - offset, centerX + offset, centerY + offset, xPaint);
        canvas.DrawLine(centerX + offset, centerY - offset, centerX - offset, centerY + offset, xPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VEKTOR-ICONS (Ersatz fuer Emojis - Desktop rendert Emojis als Quadrat)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Vektor-Icon zentriert an (cx, cy) mit gegebener Groesse.
    /// 16 Icons: 8 gute (Baustellen-Elemente) + 8 defekte (Maengel).
    /// </summary>
    private static void DrawCellIcon(SKCanvas canvas, float cx, float cy, float size, string iconId, byte alpha)
    {
        float half = size / 2;

        switch (iconId)
        {
            // -- GUTE ICONS (Baustellen-Elemente, "in Ordnung") --

            case "brick":
                DrawBrickIcon(canvas, cx, cy, half, alpha);
                break;
            case "wood":
                DrawWoodIcon(canvas, cx, cy, half, alpha);
                break;
            case "bolt":
                DrawBoltIcon(canvas, cx, cy, half, alpha);
                break;
            case "ladder":
                DrawLadderIcon(canvas, cx, cy, half, alpha);
                break;
            case "crane":
                DrawCraneIcon(canvas, cx, cy, half, alpha);
                break;
            case "wrench":
                DrawWrenchIcon(canvas, cx, cy, half, alpha);
                break;
            case "gear":
                DrawGearIcon(canvas, cx, cy, half, alpha);
                break;
            case "beam":
                DrawBeamIcon(canvas, cx, cy, half, alpha);
                break;

            // -- DEFEKT-ICONS (Maengel, "Problem") --

            case "warning":
                DrawWarningIcon(canvas, cx, cy, half, alpha);
                break;
            case "barrier":
                DrawBarrierIcon(canvas, cx, cy, half, alpha);
                break;
            case "crack":
                DrawCrackIcon(canvas, cx, cy, half, alpha);
                break;
            case "fire":
                DrawFireIcon(canvas, cx, cy, half, alpha);
                break;
            case "cross":
                DrawCrossIcon(canvas, cx, cy, half, alpha);
                break;
            case "stop":
                DrawStopIcon(canvas, cx, cy, half, alpha);
                break;
            case "hole":
                DrawHoleIcon(canvas, cx, cy, half, alpha);
                break;
            case "leak":
                DrawLeakIcon(canvas, cx, cy, half, alpha);
                break;
        }
    }

    // -- Gute Icons --

    /// <summary>Ziegel-Muster (2x3 orange/braune Rechtecke).</summary>
    private static void DrawBrickIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float bw = half * 0.6f;
        float bh = half * 0.35f;
        float gap = half * 0.08f;

        using var brickPaint = new SKPaint { Color = new SKColor(0xCC, 0x66, 0x33, alpha), IsAntialias = true };
        using var brickDark = new SKPaint { Color = new SKColor(0x8B, 0x45, 0x13, alpha), IsAntialias = true };
        using var mortarPaint = new SKPaint { Color = new SKColor(0xD2, 0xB4, 0x8C, alpha), IsAntialias = true, StrokeWidth = 1, Style = SKPaintStyle.Stroke };

        float topY = cy - bh - gap / 2;
        canvas.DrawRect(cx - bw - gap / 2, topY, bw, bh, brickPaint);
        canvas.DrawRect(cx + gap / 2, topY, bw, bh, brickDark);

        float midY = cy - bh / 2 + gap / 2;
        canvas.DrawRect(cx - bw * 0.5f - gap / 2, midY, bw, bh, brickDark);
        canvas.DrawRect(cx + bw * 0.5f + gap / 2, midY, bw * 0.5f, bh, brickPaint);
        canvas.DrawRect(cx - bw - gap / 2, midY, bw * 0.5f, bh, brickPaint);

        float botY = cy + gap / 2 + bh * 0.5f;
        canvas.DrawRect(cx - bw - gap / 2, botY, bw, bh, brickPaint);
        canvas.DrawRect(cx + gap / 2, botY, bw, bh, brickDark);

        canvas.DrawLine(cx - bw - gap, topY + bh, cx + bw + gap, topY + bh, mortarPaint);
        canvas.DrawLine(cx - bw - gap, midY + bh, cx + bw + gap, midY + bh, mortarPaint);
    }

    /// <summary>Holzbalken (braunes Rechteck mit Maserungslinien).</summary>
    private static void DrawWoodIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float w = half * 1.6f;
        float h = half * 0.7f;

        using var woodPaint = new SKPaint { Color = new SKColor(0x8B, 0x6B, 0x3D, alpha), IsAntialias = true };
        using var grainPaint = new SKPaint { Color = new SKColor(0x6B, 0x4B, 0x2D, alpha), IsAntialias = true, StrokeWidth = 1 };
        using var lightPaint = new SKPaint { Color = new SKColor(0xA0, 0x80, 0x50, alpha), IsAntialias = true, StrokeWidth = 1 };

        var rect = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
        canvas.DrawRoundRect(rect, 2, 2, woodPaint);

        float left = cx - w / 2 + 3;
        float right = cx + w / 2 - 3;
        canvas.DrawLine(left, cy - h * 0.25f, right, cy - h * 0.2f, grainPaint);
        canvas.DrawLine(left, cy + h * 0.05f, right, cy, grainPaint);
        canvas.DrawLine(left, cy + h * 0.3f, right, cy + h * 0.25f, grainPaint);
        canvas.DrawLine(left + w * 0.1f, cy - h * 0.1f, right - w * 0.1f, cy - h * 0.08f, lightPaint);
        canvas.DrawLine(left + w * 0.15f, cy + h * 0.18f, right - w * 0.05f, cy + h * 0.15f, lightPaint);
    }

    /// <summary>Schraube/Bolzen (Kreis mit Kreuzschlitz + kurzer Schaft).</summary>
    private static void DrawBoltIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float headR = half * 0.45f;
        float shaftW = half * 0.25f;
        float shaftH = half * 0.7f;

        using var metalPaint = new SKPaint { Color = new SKColor(0xB0, 0xB0, 0xB8, alpha), IsAntialias = true };
        using var slotPaint = new SKPaint { Color = new SKColor(0x50, 0x50, 0x58, alpha), IsAntialias = true, StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        using var shaftPaint = new SKPaint { Color = new SKColor(0x90, 0x90, 0x98, alpha), IsAntialias = true };

        canvas.DrawRect(cx - shaftW / 2, cy, shaftW, shaftH, shaftPaint);
        canvas.DrawCircle(cx, cy - half * 0.1f, headR, metalPaint);

        float slotLen = headR * 0.6f;
        float scy = cy - half * 0.1f;
        canvas.DrawLine(cx - slotLen, scy, cx + slotLen, scy, slotPaint);
        canvas.DrawLine(cx, scy - slotLen, cx, scy + slotLen, slotPaint);
    }

    /// <summary>Leiter (2 vertikale + 3 horizontale Linien).</summary>
    private static void DrawLadderIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float lw = half * 0.7f;
        float lh = half * 0.9f;

        using var railPaint = new SKPaint { Color = new SKColor(0xA0, 0x7B, 0x50, alpha), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round };
        using var rungPaint = new SKPaint { Color = new SKColor(0xC0, 0x95, 0x60, alpha), IsAntialias = true, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };

        canvas.DrawLine(cx - lw, cy + lh, cx - lw * 0.7f, cy - lh, railPaint);
        canvas.DrawLine(cx + lw, cy + lh, cx + lw * 0.7f, cy - lh, railPaint);

        for (int i = 0; i < 3; i++)
        {
            float t = 0.2f + i * 0.3f;
            float ry = cy - lh + 2 * lh * t;
            float rOffset = lw * (1.0f - t * 0.15f);
            canvas.DrawLine(cx - rOffset, ry, cx + rOffset, ry, rungPaint);
        }
    }

    /// <summary>Kran-Arm (L-Form mit Haken).</summary>
    private static void DrawCraneIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var cranePaint = new SKPaint { Color = new SKColor(0xFF, 0xB3, 0x00, alpha), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Square };
        using var cablePaint = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90, alpha), IsAntialias = true, StrokeWidth = 1.5f };
        using var hookPaint = new SKPaint { Color = new SKColor(0xD0, 0xD0, 0xD0, alpha), IsAntialias = true, StrokeWidth = 2, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

        float mastX = cx - half * 0.4f;
        canvas.DrawLine(mastX, cy + half, mastX, cy - half * 0.7f, cranePaint);
        canvas.DrawLine(mastX, cy - half * 0.7f, cx + half * 0.8f, cy - half * 0.7f, cranePaint);

        float hookX = cx + half * 0.6f;
        canvas.DrawLine(hookX, cy - half * 0.7f, hookX, cy + half * 0.1f, cablePaint);

        using var hookPath = new SKPath();
        hookPath.MoveTo(hookX, cy + half * 0.1f);
        hookPath.ArcTo(new SKRect(hookX - half * 0.2f, cy + half * 0.05f, hookX + half * 0.2f, cy + half * 0.45f), 0, 180, false);
        canvas.DrawPath(hookPath, hookPaint);
    }

    /// <summary>Schraubenschluessel (U-Form oben + gerader Griff).</summary>
    private static void DrawWrenchIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var metalPaint = new SKPaint { Color = new SKColor(0xA0, 0xA0, 0xA8, alpha), IsAntialias = true };
        using var handlePaint = new SKPaint { Color = new SKColor(0x80, 0x80, 0x88, alpha), IsAntialias = true };

        float gw = half * 0.22f;
        float gh = half * 1.2f;

        canvas.Save();
        canvas.RotateDegrees(-20, cx, cy);

        canvas.DrawRoundRect(new SKRect(cx - gw / 2, cy - gh * 0.1f, cx + gw / 2, cy + gh), 2, 2, handlePaint);

        float jawW = half * 0.5f;
        float jawH = half * 0.4f;
        using var jawPath = new SKPath();
        jawPath.MoveTo(cx - jawW, cy - gh * 0.1f);
        jawPath.LineTo(cx - jawW, cy - gh * 0.1f - jawH);
        jawPath.LineTo(cx - jawW * 0.3f, cy - gh * 0.1f - jawH);
        jawPath.LineTo(cx - jawW * 0.3f, cy - gh * 0.1f - jawH * 0.4f);
        jawPath.LineTo(cx + jawW * 0.3f, cy - gh * 0.1f - jawH * 0.4f);
        jawPath.LineTo(cx + jawW * 0.3f, cy - gh * 0.1f - jawH);
        jawPath.LineTo(cx + jawW, cy - gh * 0.1f - jawH);
        jawPath.LineTo(cx + jawW, cy - gh * 0.1f);
        jawPath.Close();
        canvas.DrawPath(jawPath, metalPaint);

        canvas.Restore();
    }

    /// <summary>Zahnrad (Kreis mit 6 Zacken).</summary>
    private static void DrawGearIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float outerR = half * 0.85f;
        float innerR = half * 0.55f;
        float holeR = half * 0.2f;
        int teeth = 6;

        using var gearPaint = new SKPaint { Color = new SKColor(0x90, 0x90, 0x98, alpha), IsAntialias = true };
        using var holePaint = new SKPaint { Color = new SKColor(0x37, 0x47, 0x4F, alpha), IsAntialias = true };

        using var path = new SKPath();
        float angleStep = 360f / teeth;
        float toothHalf = angleStep * 0.25f;

        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * angleStep - 90;
            float rad1 = (baseAngle - toothHalf) * MathF.PI / 180;
            float rad2 = (baseAngle + toothHalf) * MathF.PI / 180;
            float radM1 = (baseAngle - toothHalf * 0.6f) * MathF.PI / 180;
            float radM2 = (baseAngle + toothHalf * 0.6f) * MathF.PI / 180;
            float radGap1 = (baseAngle + toothHalf + 2) * MathF.PI / 180;
            float radGap2 = (baseAngle + angleStep - toothHalf - 2) * MathF.PI / 180;

            if (i == 0)
                path.MoveTo(cx + innerR * MathF.Cos(rad1), cy + innerR * MathF.Sin(rad1));
            else
                path.LineTo(cx + innerR * MathF.Cos(rad1), cy + innerR * MathF.Sin(rad1));

            path.LineTo(cx + outerR * MathF.Cos(radM1), cy + outerR * MathF.Sin(radM1));
            path.LineTo(cx + outerR * MathF.Cos(radM2), cy + outerR * MathF.Sin(radM2));
            path.LineTo(cx + innerR * MathF.Cos(rad2), cy + innerR * MathF.Sin(rad2));
            path.LineTo(cx + innerR * MathF.Cos(radGap1), cy + innerR * MathF.Sin(radGap1));
            path.LineTo(cx + innerR * MathF.Cos(radGap2), cy + innerR * MathF.Sin(radGap2));
        }
        path.Close();
        canvas.DrawPath(path, gearPaint);

        canvas.DrawCircle(cx, cy, holeR, holePaint);
    }

    /// <summary>I-Traeger (breite Flansche oben/unten + schmaler Steg).</summary>
    private static void DrawBeamIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float flangeW = half * 1.2f;
        float flangeH = half * 0.25f;
        float webW = half * 0.3f;
        float webH = half * 1.0f;

        using var steelPaint = new SKPaint { Color = new SKColor(0x78, 0x90, 0x9C, alpha), IsAntialias = true };
        using var highlightPaint = new SKPaint { Color = new SKColor(0x90, 0xA4, 0xAE, alpha), IsAntialias = true };

        canvas.DrawRect(cx - webW / 2, cy - webH / 2, webW, webH, steelPaint);
        canvas.DrawRect(cx - flangeW / 2, cy - webH / 2 - flangeH / 2, flangeW, flangeH, steelPaint);
        canvas.DrawRect(cx - flangeW / 2, cy - webH / 2 - flangeH / 2, flangeW, flangeH * 0.3f, highlightPaint);
        canvas.DrawRect(cx - flangeW / 2, cy + webH / 2 - flangeH / 2, flangeW, flangeH, steelPaint);
    }

    // -- Defekt-Icons --

    /// <summary>Warndreieck (gelbes Dreieck mit schwarzem "!").</summary>
    private static void DrawWarningIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var triPaint = new SKPaint { Color = new SKColor(0xFF, 0xC1, 0x07, alpha), IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(0xE6, 0x9C, 0x00, alpha), IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
        using var exclPaint = new SKPaint { Color = new SKColor(0x33, 0x33, 0x33, alpha), IsAntialias = true, StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round };

        using var triPath = new SKPath();
        triPath.MoveTo(cx, cy - half * 0.85f);
        triPath.LineTo(cx - half * 0.9f, cy + half * 0.7f);
        triPath.LineTo(cx + half * 0.9f, cy + half * 0.7f);
        triPath.Close();
        canvas.DrawPath(triPath, triPaint);
        canvas.DrawPath(triPath, borderPaint);

        canvas.DrawLine(cx, cy - half * 0.3f, cx, cy + half * 0.2f, exclPaint);
        canvas.DrawCircle(cx, cy + half * 0.45f, 1.5f, exclPaint);
    }

    /// <summary>Absperrung (rot-weiss gestreifter Balken auf 2 Fuessen).</summary>
    private static void DrawBarrierIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float barW = half * 1.6f;
        float barH = half * 0.35f;
        float barY = cy - half * 0.15f;

        using var redPaint = new SKPaint { Color = new SKColor(0xE5, 0x39, 0x35, alpha), IsAntialias = true };
        using var whitePaint = new SKPaint { Color = new SKColor(0xF5, 0xF5, 0xF5, alpha), IsAntialias = true };
        using var legPaint = new SKPaint { Color = new SKColor(0x90, 0x90, 0x90, alpha), IsAntialias = true, StrokeWidth = 2.5f };

        canvas.DrawLine(cx - half * 0.55f, barY + barH, cx - half * 0.55f, cy + half * 0.8f, legPaint);
        canvas.DrawLine(cx + half * 0.55f, barY + barH, cx + half * 0.55f, cy + half * 0.8f, legPaint);

        var barRect = new SKRect(cx - barW / 2, barY, cx + barW / 2, barY + barH);
        canvas.DrawRoundRect(barRect, 2, 2, redPaint);

        canvas.Save();
        canvas.ClipRect(barRect);
        float stripeW = barH * 0.6f;
        for (float sx = cx - barW / 2 - barH; sx < cx + barW / 2 + barH; sx += stripeW * 2)
        {
            using var stripePath = new SKPath();
            stripePath.MoveTo(sx, barY + barH);
            stripePath.LineTo(sx + barH, barY);
            stripePath.LineTo(sx + barH + stripeW, barY);
            stripePath.LineTo(sx + stripeW, barY + barH);
            stripePath.Close();
            canvas.DrawPath(stripePath, whitePaint);
        }
        canvas.Restore();
    }

    /// <summary>Riss (Zickzack-Linie von oben nach unten).</summary>
    private static void DrawCrackIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var crackPaint = new SKPaint
        {
            Color = new SKColor(0x40, 0x40, 0x40, alpha),
            IsAntialias = true,
            StrokeWidth = 2.5f,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        using var lightPaint = new SKPaint
        {
            Color = new SKColor(0xA0, 0xA0, 0xA0, alpha),
            IsAntialias = true,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke
        };

        using var path = new SKPath();
        path.MoveTo(cx - half * 0.1f, cy - half * 0.9f);
        path.LineTo(cx + half * 0.3f, cy - half * 0.5f);
        path.LineTo(cx - half * 0.25f, cy - half * 0.15f);
        path.LineTo(cx + half * 0.2f, cy + half * 0.2f);
        path.LineTo(cx - half * 0.15f, cy + half * 0.5f);
        path.LineTo(cx + half * 0.1f, cy + half * 0.9f);
        canvas.DrawPath(path, crackPaint);

        using var lightPath = new SKPath();
        lightPath.MoveTo(cx - half * 0.1f + 3, cy - half * 0.9f);
        lightPath.LineTo(cx + half * 0.3f + 3, cy - half * 0.5f);
        lightPath.LineTo(cx - half * 0.25f + 3, cy - half * 0.15f);
        canvas.DrawPath(lightPath, lightPaint);
    }

    /// <summary>Flamme (orange/rote Tropfen-Form mit innerem gelben Kern).</summary>
    private static void DrawFireIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var outerPaint = new SKPaint { Color = new SKColor(0xE5, 0x50, 0x00, alpha), IsAntialias = true };
        using var outerPath = new SKPath();
        outerPath.MoveTo(cx, cy - half * 0.9f);
        outerPath.CubicTo(cx + half * 0.6f, cy - half * 0.3f, cx + half * 0.7f, cy + half * 0.3f, cx + half * 0.4f, cy + half * 0.8f);
        outerPath.CubicTo(cx + half * 0.1f, cy + half, cx - half * 0.1f, cy + half, cx - half * 0.4f, cy + half * 0.8f);
        outerPath.CubicTo(cx - half * 0.7f, cy + half * 0.3f, cx - half * 0.6f, cy - half * 0.3f, cx, cy - half * 0.9f);
        outerPath.Close();
        canvas.DrawPath(outerPath, outerPaint);

        using var midPaint = new SKPaint { Color = new SKColor(0xFF, 0x8F, 0x00, alpha), IsAntialias = true };
        using var midPath = new SKPath();
        midPath.MoveTo(cx, cy - half * 0.5f);
        midPath.CubicTo(cx + half * 0.35f, cy - half * 0.1f, cx + half * 0.4f, cy + half * 0.3f, cx + half * 0.2f, cy + half * 0.7f);
        midPath.CubicTo(cx, cy + half * 0.8f, cx - half * 0.05f, cy + half * 0.8f, cx - half * 0.2f, cy + half * 0.7f);
        midPath.CubicTo(cx - half * 0.4f, cy + half * 0.3f, cx - half * 0.35f, cy - half * 0.1f, cx, cy - half * 0.5f);
        midPath.Close();
        canvas.DrawPath(midPath, midPaint);

        using var innerPaint = new SKPaint { Color = new SKColor(0xFF, 0xEB, 0x3B, alpha), IsAntialias = true };
        using var innerPath = new SKPath();
        innerPath.MoveTo(cx, cy + half * 0.05f);
        innerPath.CubicTo(cx + half * 0.15f, cy + half * 0.25f, cx + half * 0.12f, cy + half * 0.55f, cx, cy + half * 0.65f);
        innerPath.CubicTo(cx - half * 0.12f, cy + half * 0.55f, cx - half * 0.15f, cy + half * 0.25f, cx, cy + half * 0.05f);
        innerPath.Close();
        canvas.DrawPath(innerPath, innerPaint);
    }

    /// <summary>X-Kreuz (2 rote diagonale Linien).</summary>
    private static void DrawCrossIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float len = half * 0.7f;
        using var crossPaint = new SKPaint
        {
            Color = new SKColor(0xEF, 0x53, 0x50, alpha),
            IsAntialias = true,
            StrokeWidth = 4,
            StrokeCap = SKStrokeCap.Round
        };

        canvas.DrawLine(cx - len, cy - len, cx + len, cy + len, crossPaint);
        canvas.DrawLine(cx + len, cy - len, cx - len, cy + len, crossPaint);
    }

    /// <summary>Stoppschild (roter Kreis mit weissem horizontalem Strich).</summary>
    private static void DrawStopIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float radius = half * 0.75f;
        using var circlePaint = new SKPaint { Color = new SKColor(0xE5, 0x39, 0x35, alpha), IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(0xB7, 0x1C, 0x1C, alpha), IsAntialias = true, StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        using var linePaint = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, alpha), IsAntialias = true, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round };

        canvas.DrawCircle(cx, cy, radius, circlePaint);
        canvas.DrawCircle(cx, cy, radius, borderPaint);
        canvas.DrawLine(cx - radius * 0.55f, cy, cx + radius * 0.55f, cy, linePaint);
    }

    /// <summary>Loch (dunkler Kreis mit hellem Rand - perspektivisch).</summary>
    private static void DrawHoleIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float outerR = half * 0.7f;
        float innerR = half * 0.5f;

        using var rimPaint = new SKPaint { Color = new SKColor(0x90, 0x9D, 0xA5, alpha), IsAntialias = true };
        canvas.DrawOval(cx, cy, outerR, outerR * 0.75f, rimPaint);

        using var holePaint = new SKPaint { Color = new SKColor(0x1A, 0x1A, 0x1A, alpha), IsAntialias = true };
        canvas.DrawOval(cx, cy + 1, innerR, innerR * 0.7f, holePaint);

        using var shadowPaint = new SKPaint { Color = new SKColor(0x30, 0x30, 0x30, (byte)(alpha * 0.5f)), IsAntialias = true };
        canvas.DrawOval(cx, cy - 1, innerR * 0.8f, innerR * 0.3f, shadowPaint);
    }

    /// <summary>Wasserleck (3 blaue Tropfen fallend).</summary>
    private static void DrawLeakIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        using var waterPaint = new SKPaint { Color = new SKColor(0x42, 0xA5, 0xF5, alpha), IsAntialias = true };
        using var lightPaint = new SKPaint { Color = new SKColor(0x90, 0xCA, 0xF9, alpha), IsAntialias = true };

        float[] dropXOffsets = { -half * 0.35f, half * 0.1f, half * 0.4f };
        float[] dropYOffsets = { -half * 0.4f, half * 0.1f, -half * 0.1f };
        float[] dropSizes = { half * 0.28f, half * 0.35f, half * 0.25f };

        for (int i = 0; i < 3; i++)
        {
            float dx = cx + dropXOffsets[i];
            float dy = cy + dropYOffsets[i];
            float ds = dropSizes[i];

            using var dropPath = new SKPath();
            dropPath.MoveTo(dx, dy - ds * 1.2f);
            dropPath.CubicTo(dx + ds * 0.7f, dy - ds * 0.2f, dx + ds * 0.7f, dy + ds * 0.5f, dx, dy + ds * 0.7f);
            dropPath.CubicTo(dx - ds * 0.7f, dy + ds * 0.5f, dx - ds * 0.7f, dy - ds * 0.2f, dx, dy - ds * 1.2f);
            dropPath.Close();
            canvas.DrawPath(dropPath, waterPaint);

            canvas.DrawCircle(dx - ds * 0.15f, dy - ds * 0.1f, ds * 0.15f, lightPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DEKO: LUPE (pulsierend)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine kleine Lupe/Inspektor-Symbol als Deko-Element.
    /// Leichtes Schwanken und Puls-Animation waehrend das Spiel laeuft.
    /// </summary>
    private void DrawMagnifier(SKCanvas canvas, float x, float y, bool isPlaying)
    {
        // Leichte Schwankbewegung
        float bobY = (float)Math.Sin(_time * 1.5) * 2;

        float cx = x + 12;
        float cy = y + 12 + bobY;
        float baseRadius = 10;

        // Puls-Animation: Lupe pulsiert subtil (Scale-Effekt)
        float pulseScale = isPlaying ? 1.0f + 0.08f * (float)Math.Sin(_time * 3.0) : 1.0f;
        float radius = baseRadius * pulseScale;

        canvas.Save();
        // Scale um den Lupe-Mittelpunkt
        canvas.Translate(cx, cy);
        canvas.Scale(pulseScale);
        canvas.Translate(-cx, -cy);

        // Glasflaeche (halbtransparent blau)
        using var glassPaint = new SKPaint { Color = MagnifierGlass, IsAntialias = false };
        canvas.DrawCircle(cx, cy, baseRadius, glassPaint);

        // Metallring
        using var ringPaint = new SKPaint { Color = MagnifierRing, IsAntialias = false, StrokeWidth = 3, Style = SKPaintStyle.Stroke };
        canvas.DrawCircle(cx, cy, baseRadius, ringPaint);

        // Glanz auf dem Glas (kleiner heller Punkt)
        using var glintPaint = new SKPaint { Color = new SKColor(255, 255, 255, 80), IsAntialias = false };
        canvas.DrawCircle(cx - 3, cy - 3, 3, glintPaint);

        // Griff (diagonal nach unten rechts)
        using var handlePaint = new SKPaint { Color = MagnifierHandle, IsAntialias = false, StrokeWidth = 4, StrokeCap = SKStrokeCap.Round };
        float handleStartX = cx + baseRadius * 0.6f;
        float handleStartY = cy + baseRadius * 0.6f;
        canvas.DrawLine(handleStartX, handleStartY, handleStartX + 10, handleStartY + 10, handlePaint);

        // Griff-Akzent (hellere Kante)
        using var handleAccent = new SKPaint { Color = new SKColor(0x8D, 0x6E, 0x63), IsAntialias = false, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(handleStartX + 1, handleStartY, handleStartX + 9, handleStartY + 8, handleAccent);

        canvas.Restore();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL: STAUB (Struct-Array statt List)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert und zeichnet Staub-Partikel die ueber die Baustelle schweben.
    /// </summary>
    private void UpdateAndDrawDust(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        var random = Random.Shared;

        // Neue Partikel erzeugen
        if (_dustCount < MAX_DUST)
        {
            _dust[_dustCount++] = new DustParticle
            {
                X = bounds.Left + (float)(random.NextDouble() * bounds.Width),
                Y = bounds.Bottom + 5,
                VelocityX = (float)(random.NextDouble() - 0.5) * 15,
                VelocityY = -10 - (float)(random.NextDouble() * 20),
                Life = 0,
                MaxLife = 2.0f + (float)random.NextDouble() * 2.0f,
                Size = 1 + random.Next(0, 3),
                Alpha = (byte)(60 + random.Next(0, 60))
            };
        }

        // Partikel aktualisieren und zeichnen
        using var dustPaint = new SKPaint { IsAntialias = false };
        for (int i = 0; i < _dustCount; i++)
        {
            var p = _dust[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Leichte horizontale Drift (Wind-Effekt)
            p.VelocityX += (float)(Math.Sin(_time * 0.5 + i) * 2) * deltaTime;

            if (p.Life >= p.MaxLife || p.Y < bounds.Top - 10)
            {
                // Entfernen durch Kompaktierung
                _dust[i] = _dust[--_dustCount];
                i--;
                continue;
            }

            _dust[i] = p;

            // Alpha basierend auf Lebenszeit (Fade-Out)
            float lifeRatio = p.Life / p.MaxLife;
            float alpha = lifeRatio < 0.2f
                ? lifeRatio / 0.2f   // Fade-In
                : 1.0f - (lifeRatio - 0.2f) / 0.8f; // Fade-Out
            byte finalAlpha = (byte)(p.Alpha * alpha);

            dustPaint.Color = new SKColor(0xB0, 0xBE, 0xC5, finalAlpha);
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, dustPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENTDECKUNGS-EFFEKTE (Funken bei Defekt/Fehlalarm)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt neue Defekt-Funde und Fehlalarme und spawnt entsprechende Funken.
    /// </summary>
    private void DetectNewDiscoveries(InspectionCellData[] cells, float gridLeft, float gridTop,
        float cellSize, float spacing, int cols)
    {
        for (int i = 0; i < cells.Length && i < MAX_TRACKED_CELLS; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float cx = gridLeft + col * cellSize + cellSize / 2;
            float cy = gridTop + row * cellSize + cellSize / 2;

            // Defekt gefunden: 10 gruene Partikel
            if (cells[i].IsDefectFound && !_prevDefectFound[i])
            {
                SpawnDiscoverySparks(cx, cy, CheckmarkGreen, 10);
            }

            // Fehlalarm: 8 rote Partikel
            if (cells[i].IsFalseAlarm && !_prevFalseAlarm[i])
            {
                SpawnDiscoverySparks(cx, cy, CrossRed, 8);
            }

            _prevDefectFound[i] = cells[i].IsDefectFound;
            _prevFalseAlarm[i] = cells[i].IsFalseAlarm;
        }
    }

    /// <summary>
    /// Spawnt Funken-Partikel vom Zell-Mittelpunkt.
    /// </summary>
    private void SpawnDiscoverySparks(float cx, float cy, SKColor color, int count)
    {
        var rng = Random.Shared;
        for (int i = 0; i < count && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float speed = 30 + (float)(rng.NextDouble() * 50);

            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx,
                Y = cy,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Life = 0,
                MaxLife = 0.4f + (float)(rng.NextDouble() * 0.4f),
                Size = 2 + (float)(rng.NextDouble() * 2),
                R = color.Red,
                G = color.Green,
                B = color.Blue
            };
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet alle aktiven Entdeckungs-Funken.
    /// </summary>
    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        if (_sparkCount == 0) return;

        using var sparkPaint = new SKPaint { IsAntialias = false };

        for (int i = 0; i < _sparkCount; i++)
        {
            var p = _sparks[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 80 * deltaTime; // Leichte Schwerkraft
            p.VelocityX *= 0.97f; // Luftwiderstand

            if (p.Life >= p.MaxLife)
            {
                // Entfernen durch Kompaktierung
                _sparks[i] = _sparks[--_sparkCount];
                i--;
                continue;
            }

            _sparks[i] = p;

            float alpha = 1 - (p.Life / p.MaxLife);
            sparkPaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));
            canvas.DrawRect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size, sparkPaint);
        }
    }
}
