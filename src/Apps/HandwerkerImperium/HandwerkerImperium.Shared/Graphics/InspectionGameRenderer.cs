using HandwerkerImperium.Services;
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
/// Wiederverwendbare SKPaint-Instanzen statt per-Frame Allokationen.
/// </summary>
public sealed class InspectionGameRenderer : IDisposable
{
    private bool _disposed;

    // AI-Hintergrund (optionaler Layer unter den Spielelementen)
    private IGameAssetService? _assetService;
    private SKBitmap? _background;

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
    // Wiederverwendbare Paint-Instanzen (kein GC pro Frame)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Flaechenfuellung ohne Antialiasing (Hintergrund, Zellen, Partikel, Lupe).</summary>
    private readonly SKPaint _fillNoAA = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    /// <summary>Flaechenfuellung mit Antialiasing (Icons, Formen).</summary>
    private readonly SKPaint _fillAA = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>Zweite Flaechenfuellung mit Antialiasing (Icons mit mehreren Farben gleichzeitig).</summary>
    private readonly SKPaint _fillAA2 = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>Dritte Flaechenfuellung mit Antialiasing (Icons mit 3 Farben gleichzeitig).</summary>
    private readonly SKPaint _fillAA3 = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>Linienmodus ohne Antialiasing (Grid-Linien, Hintergrund-Risse, Beton-Fugen).</summary>
    private readonly SKPaint _strokeNoAA = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };

    /// <summary>Linienmodus mit Antialiasing (Icon-Konturen, Rahmen).</summary>
    private readonly SKPaint _strokeAA = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <summary>Zweiter Linienmodus mit Antialiasing (fuer Methoden mit 2+ Stroke-Paints).</summary>
    private readonly SKPaint _strokeAA2 = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <summary>Dritter Linienmodus mit Antialiasing (fuer Methoden mit 3 Stroke-Paints).</summary>
    private readonly SKPaint _strokeAA3 = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    // Gecachter SKPath fuer wiederholte Nutzung (vermeidet ~11 SKPath-Allokationen pro Frame)
    private readonly SKPath _cachedPath = new();

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
    /// Initialisiert den AI-Asset-Service für den Hintergrund.
    /// </summary>
    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Rendert das gesamte Inspektions-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, InspectionCellData[] cells, int cols, int rows,
        bool isPlaying, int defectsFound, int totalDefects, float deltaTime)
    {
        // AI-Hintergrund als Atmosphäre-Layer
        if (_assetService != null)
        {
            _background ??= _assetService.GetBitmap("minigames/inspection_bg.webp");
            if (_background == null)
                _ = _assetService.LoadBitmapAsync("minigames/inspection_bg.webp");
            if (_background != null)
                canvas.DrawBitmap(_background, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }

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
        _fillNoAA.Color = ConcreteBase;
        canvas.DrawRect(bounds, _fillNoAA);

        // Beton-Textur: Horizontale Streifen fuer Fugen
        _strokeNoAA.Color = ConcreteDark;
        _strokeNoAA.StrokeWidth = 1;
        for (float y = bounds.Top + 30; y < bounds.Bottom; y += 40)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _strokeNoAA);
        }

        // Risse im Beton (deterministische Positionen)
        _strokeNoAA.Color = CrackColor;

        // Riss 1: Oben links
        float cx1 = bounds.Left + bounds.Width * 0.15f;
        float cy1 = bounds.Top + bounds.Height * 0.2f;
        canvas.DrawLine(cx1, cy1, cx1 + 18, cy1 + 12, _strokeNoAA);
        canvas.DrawLine(cx1 + 18, cy1 + 12, cx1 + 14, cy1 + 28, _strokeNoAA);
        canvas.DrawLine(cx1 + 18, cy1 + 12, cx1 + 30, cy1 + 8, _strokeNoAA);

        // Riss 2: Unten rechts
        float cx2 = bounds.Right - bounds.Width * 0.2f;
        float cy2 = bounds.Bottom - bounds.Height * 0.25f;
        canvas.DrawLine(cx2, cy2, cx2 - 10, cy2 + 16, _strokeNoAA);
        canvas.DrawLine(cx2, cy2, cx2 + 12, cy2 + 10, _strokeNoAA);

        // Riss 3: Mitte oben
        float cx3 = bounds.MidX + 20;
        float cy3 = bounds.Top + 10;
        canvas.DrawLine(cx3, cy3, cx3 + 8, cy3 + 14, _strokeNoAA);
        canvas.DrawLine(cx3 + 8, cy3 + 14, cx3 + 20, cy3 + 18, _strokeNoAA);

        // Kleine Beton-Kratzer
        _strokeNoAA.Color = new SKColor(0x50, 0x60, 0x68, 60);
        canvas.DrawLine(bounds.Left + 40, bounds.Bottom - 20, bounds.Left + 70, bounds.Bottom - 22, _strokeNoAA);
        canvas.DrawLine(bounds.Right - 60, bounds.Top + 50, bounds.Right - 30, bounds.Top + 48, _strokeNoAA);
    }

    /// <summary>
    /// Zeichnet subtile Hilfslinien fuer das Grid.
    /// </summary>
    private void DrawGridLines(SKCanvas canvas, float gridLeft, float gridTop, float gridWidth, float gridHeight, int cols, int rows, float cellSize)
    {
        _strokeNoAA.Color = GridLineColor;
        _strokeNoAA.StrokeWidth = 1;

        // Vertikale Linien
        for (int c = 0; c <= cols; c++)
        {
            float x = gridLeft + c * cellSize;
            canvas.DrawLine(x, gridTop, x, gridTop + gridHeight, _strokeNoAA);
        }

        // Horizontale Linien
        for (int r = 0; r <= rows; r++)
        {
            float y = gridTop + r * cellSize;
            canvas.DrawLine(gridLeft, y, gridLeft + gridWidth, y, _strokeNoAA);
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
        _fillNoAA.Color = bgColor;
        var cellRect = new SKRect(x, y, x + w, y + h);
        canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, _fillNoAA);

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
        _strokeNoAA.Color = borderColor;
        _strokeNoAA.StrokeWidth = borderWidth;
        canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, _strokeNoAA);

        // Subtiles Mangel-Schimmern (nur fuer unentdeckte Defekte, leicht pulsierend)
        if (cell.IsDefect && !cell.IsInspected && isPlaying)
        {
            float shimmerPulse = (float)(0.3 + 0.7 * Math.Sin(_time * 2.5 + x * 0.1));
            byte shimmerAlpha = (byte)(DefectShimmer.Alpha * shimmerPulse);
            _fillNoAA.Color = DefectShimmer.WithAlpha(shimmerAlpha);
            canvas.DrawRoundRect(cellRect, cornerRadius, cornerRadius, _fillNoAA);
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
    private void DrawCheckmark(SKCanvas canvas, float x, float y, float size)
    {
        // Gruener Kreis-Hintergrund
        float centerX = x + size / 2;
        float centerY = y + size / 2;
        float radius = size / 2;
        _fillNoAA.Color = CheckmarkGreen;
        canvas.DrawCircle(centerX, centerY, radius, _fillNoAA);

        // Weisses Haekchen (2 Linien: kurz links, lang rechts)
        _strokeNoAA.Color = SKColors.White;
        _strokeNoAA.StrokeWidth = 2;
        _strokeNoAA.StrokeCap = SKStrokeCap.Square;
        float s = size * 0.25f; // Skalierungsfaktor
        canvas.DrawLine(centerX - s * 1.2f, centerY, centerX - s * 0.2f, centerY + s, _strokeNoAA);
        canvas.DrawLine(centerX - s * 0.2f, centerY + s, centerX + s * 1.5f, centerY - s * 0.8f, _strokeNoAA);
    }

    /// <summary>
    /// Zeichnet eine Pixel-Art X-Markierung (roter Kreis mit weissem X).
    /// </summary>
    private void DrawCrossMark(SKCanvas canvas, float x, float y, float size)
    {
        // Roter Kreis-Hintergrund
        float centerX = x + size / 2;
        float centerY = y + size / 2;
        float radius = size / 2;
        _fillNoAA.Color = CrossRed;
        canvas.DrawCircle(centerX, centerY, radius, _fillNoAA);

        // Weisses X (2 diagonale Linien)
        _strokeNoAA.Color = SKColors.White;
        _strokeNoAA.StrokeWidth = 2;
        _strokeNoAA.StrokeCap = SKStrokeCap.Square;
        float offset = size * 0.25f;
        canvas.DrawLine(centerX - offset, centerY - offset, centerX + offset, centerY + offset, _strokeNoAA);
        canvas.DrawLine(centerX + offset, centerY - offset, centerX - offset, centerY + offset, _strokeNoAA);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VEKTOR-ICONS (Ersatz fuer Emojis - Desktop rendert Emojis als Quadrat)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Vektor-Icon zentriert an (cx, cy) mit gegebener Groesse.
    /// 16 Icons: 8 gute (Baustellen-Elemente) + 8 defekte (Maengel).
    /// </summary>
    private void DrawCellIcon(SKCanvas canvas, float cx, float cy, float size, string iconId, byte alpha)
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
    private void DrawBrickIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float bw = half * 0.6f;
        float bh = half * 0.35f;
        float gap = half * 0.08f;

        _fillAA.Color = new SKColor(0xCC, 0x66, 0x33, alpha);
        _fillAA2.Color = new SKColor(0x8B, 0x45, 0x13, alpha);
        _strokeAA.Color = new SKColor(0xD2, 0xB4, 0x8C, alpha);
        _strokeAA.StrokeWidth = 1;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;

        float topY = cy - bh - gap / 2;
        canvas.DrawRect(cx - bw - gap / 2, topY, bw, bh, _fillAA);
        canvas.DrawRect(cx + gap / 2, topY, bw, bh, _fillAA2);

        float midY = cy - bh / 2 + gap / 2;
        canvas.DrawRect(cx - bw * 0.5f - gap / 2, midY, bw, bh, _fillAA2);
        canvas.DrawRect(cx + bw * 0.5f + gap / 2, midY, bw * 0.5f, bh, _fillAA);
        canvas.DrawRect(cx - bw - gap / 2, midY, bw * 0.5f, bh, _fillAA);

        float botY = cy + gap / 2 + bh * 0.5f;
        canvas.DrawRect(cx - bw - gap / 2, botY, bw, bh, _fillAA);
        canvas.DrawRect(cx + gap / 2, botY, bw, bh, _fillAA2);

        canvas.DrawLine(cx - bw - gap, topY + bh, cx + bw + gap, topY + bh, _strokeAA);
        canvas.DrawLine(cx - bw - gap, midY + bh, cx + bw + gap, midY + bh, _strokeAA);
    }

    /// <summary>Holzbalken (braunes Rechteck mit Maserungslinien).</summary>
    private void DrawWoodIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float w = half * 1.6f;
        float h = half * 0.7f;

        _fillAA.Color = new SKColor(0x8B, 0x6B, 0x3D, alpha);
        _strokeAA.Color = new SKColor(0x6B, 0x4B, 0x2D, alpha);
        _strokeAA.StrokeWidth = 1;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;
        _strokeAA2.Color = new SKColor(0xA0, 0x80, 0x50, alpha);
        _strokeAA2.StrokeWidth = 1;
        _strokeAA2.StrokeCap = SKStrokeCap.Butt;

        var rect = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
        canvas.DrawRoundRect(rect, 2, 2, _fillAA);

        float left = cx - w / 2 + 3;
        float right = cx + w / 2 - 3;
        canvas.DrawLine(left, cy - h * 0.25f, right, cy - h * 0.2f, _strokeAA);
        canvas.DrawLine(left, cy + h * 0.05f, right, cy, _strokeAA);
        canvas.DrawLine(left, cy + h * 0.3f, right, cy + h * 0.25f, _strokeAA);
        canvas.DrawLine(left + w * 0.1f, cy - h * 0.1f, right - w * 0.1f, cy - h * 0.08f, _strokeAA2);
        canvas.DrawLine(left + w * 0.15f, cy + h * 0.18f, right - w * 0.05f, cy + h * 0.15f, _strokeAA2);
    }

    /// <summary>Schraube/Bolzen (Kreis mit Kreuzschlitz + kurzer Schaft).</summary>
    private void DrawBoltIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float headR = half * 0.45f;
        float shaftW = half * 0.25f;
        float shaftH = half * 0.7f;

        _fillAA.Color = new SKColor(0xB0, 0xB0, 0xB8, alpha);
        _strokeAA.Color = new SKColor(0x50, 0x50, 0x58, alpha);
        _strokeAA.StrokeWidth = 2;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;
        _fillAA2.Color = new SKColor(0x90, 0x90, 0x98, alpha);

        canvas.DrawRect(cx - shaftW / 2, cy, shaftW, shaftH, _fillAA2);
        canvas.DrawCircle(cx, cy - half * 0.1f, headR, _fillAA);

        float slotLen = headR * 0.6f;
        float scy = cy - half * 0.1f;
        canvas.DrawLine(cx - slotLen, scy, cx + slotLen, scy, _strokeAA);
        canvas.DrawLine(cx, scy - slotLen, cx, scy + slotLen, _strokeAA);
    }

    /// <summary>Leiter (2 vertikale + 3 horizontale Linien).</summary>
    private void DrawLadderIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float lw = half * 0.7f;
        float lh = half * 0.9f;

        _strokeAA.Color = new SKColor(0xA0, 0x7B, 0x50, alpha);
        _strokeAA.StrokeWidth = 3;
        _strokeAA.StrokeCap = SKStrokeCap.Round;
        _strokeAA2.Color = new SKColor(0xC0, 0x95, 0x60, alpha);
        _strokeAA2.StrokeWidth = 2;
        _strokeAA2.StrokeCap = SKStrokeCap.Round;

        canvas.DrawLine(cx - lw, cy + lh, cx - lw * 0.7f, cy - lh, _strokeAA);
        canvas.DrawLine(cx + lw, cy + lh, cx + lw * 0.7f, cy - lh, _strokeAA);

        for (int i = 0; i < 3; i++)
        {
            float t = 0.2f + i * 0.3f;
            float ry = cy - lh + 2 * lh * t;
            float rOffset = lw * (1.0f - t * 0.15f);
            canvas.DrawLine(cx - rOffset, ry, cx + rOffset, ry, _strokeAA2);
        }
    }

    /// <summary>Kran-Arm (L-Form mit Haken).</summary>
    private void DrawCraneIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        _strokeAA.Color = new SKColor(0xFF, 0xB3, 0x00, alpha);
        _strokeAA.StrokeWidth = 3;
        _strokeAA.StrokeCap = SKStrokeCap.Square;
        _strokeAA2.Color = new SKColor(0x90, 0x90, 0x90, alpha);
        _strokeAA2.StrokeWidth = 1.5f;
        _strokeAA2.StrokeCap = SKStrokeCap.Butt;
        _strokeAA3.Color = new SKColor(0xD0, 0xD0, 0xD0, alpha);
        _strokeAA3.StrokeWidth = 2;
        _strokeAA3.StrokeCap = SKStrokeCap.Round;

        float mastX = cx - half * 0.4f;
        canvas.DrawLine(mastX, cy + half, mastX, cy - half * 0.7f, _strokeAA);
        canvas.DrawLine(mastX, cy - half * 0.7f, cx + half * 0.8f, cy - half * 0.7f, _strokeAA);

        float hookX = cx + half * 0.6f;
        canvas.DrawLine(hookX, cy - half * 0.7f, hookX, cy + half * 0.1f, _strokeAA2);

        _cachedPath.Rewind();
        _cachedPath.MoveTo(hookX, cy + half * 0.1f);
        _cachedPath.ArcTo(new SKRect(hookX - half * 0.2f, cy + half * 0.05f, hookX + half * 0.2f, cy + half * 0.45f), 0, 180, false);
        canvas.DrawPath(_cachedPath, _strokeAA3);
    }

    /// <summary>Schraubenschluessel (U-Form oben + gerader Griff).</summary>
    private void DrawWrenchIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        _fillAA.Color = new SKColor(0xA0, 0xA0, 0xA8, alpha);
        _fillAA2.Color = new SKColor(0x80, 0x80, 0x88, alpha);

        float gw = half * 0.22f;
        float gh = half * 1.2f;

        canvas.Save();
        canvas.RotateDegrees(-20, cx, cy);

        canvas.DrawRoundRect(new SKRect(cx - gw / 2, cy - gh * 0.1f, cx + gw / 2, cy + gh), 2, 2, _fillAA2);

        float jawW = half * 0.5f;
        float jawH = half * 0.4f;
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx - jawW, cy - gh * 0.1f);
        _cachedPath.LineTo(cx - jawW, cy - gh * 0.1f - jawH);
        _cachedPath.LineTo(cx - jawW * 0.3f, cy - gh * 0.1f - jawH);
        _cachedPath.LineTo(cx - jawW * 0.3f, cy - gh * 0.1f - jawH * 0.4f);
        _cachedPath.LineTo(cx + jawW * 0.3f, cy - gh * 0.1f - jawH * 0.4f);
        _cachedPath.LineTo(cx + jawW * 0.3f, cy - gh * 0.1f - jawH);
        _cachedPath.LineTo(cx + jawW, cy - gh * 0.1f - jawH);
        _cachedPath.LineTo(cx + jawW, cy - gh * 0.1f);
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA);

        canvas.Restore();
    }

    /// <summary>Zahnrad (Kreis mit 6 Zacken).</summary>
    private void DrawGearIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float outerR = half * 0.85f;
        float innerR = half * 0.55f;
        float holeR = half * 0.2f;
        int teeth = 6;

        _fillAA.Color = new SKColor(0x90, 0x90, 0x98, alpha);
        _fillAA2.Color = new SKColor(0x37, 0x47, 0x4F, alpha);

        _cachedPath.Rewind();
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
                _cachedPath.MoveTo(cx + innerR * MathF.Cos(rad1), cy + innerR * MathF.Sin(rad1));
            else
                _cachedPath.LineTo(cx + innerR * MathF.Cos(rad1), cy + innerR * MathF.Sin(rad1));

            _cachedPath.LineTo(cx + outerR * MathF.Cos(radM1), cy + outerR * MathF.Sin(radM1));
            _cachedPath.LineTo(cx + outerR * MathF.Cos(radM2), cy + outerR * MathF.Sin(radM2));
            _cachedPath.LineTo(cx + innerR * MathF.Cos(rad2), cy + innerR * MathF.Sin(rad2));
            _cachedPath.LineTo(cx + innerR * MathF.Cos(radGap1), cy + innerR * MathF.Sin(radGap1));
            _cachedPath.LineTo(cx + innerR * MathF.Cos(radGap2), cy + innerR * MathF.Sin(radGap2));
        }
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA);

        canvas.DrawCircle(cx, cy, holeR, _fillAA2);
    }

    /// <summary>I-Traeger (breite Flansche oben/unten + schmaler Steg).</summary>
    private void DrawBeamIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float flangeW = half * 1.2f;
        float flangeH = half * 0.25f;
        float webW = half * 0.3f;
        float webH = half * 1.0f;

        _fillAA.Color = new SKColor(0x78, 0x90, 0x9C, alpha);
        _fillAA2.Color = new SKColor(0x90, 0xA4, 0xAE, alpha);

        canvas.DrawRect(cx - webW / 2, cy - webH / 2, webW, webH, _fillAA);
        canvas.DrawRect(cx - flangeW / 2, cy - webH / 2 - flangeH / 2, flangeW, flangeH, _fillAA);
        canvas.DrawRect(cx - flangeW / 2, cy - webH / 2 - flangeH / 2, flangeW, flangeH * 0.3f, _fillAA2);
        canvas.DrawRect(cx - flangeW / 2, cy + webH / 2 - flangeH / 2, flangeW, flangeH, _fillAA);
    }

    // -- Defekt-Icons --

    /// <summary>Warndreieck (gelbes Dreieck mit schwarzem "!").</summary>
    private void DrawWarningIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        _fillAA.Color = new SKColor(0xFF, 0xC1, 0x07, alpha);
        _strokeAA.Color = new SKColor(0xE6, 0x9C, 0x00, alpha);
        _strokeAA.StrokeWidth = 1.5f;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;
        _strokeAA2.Color = new SKColor(0x33, 0x33, 0x33, alpha);
        _strokeAA2.StrokeWidth = 2.5f;
        _strokeAA2.StrokeCap = SKStrokeCap.Round;

        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx, cy - half * 0.85f);
        _cachedPath.LineTo(cx - half * 0.9f, cy + half * 0.7f);
        _cachedPath.LineTo(cx + half * 0.9f, cy + half * 0.7f);
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA);
        canvas.DrawPath(_cachedPath, _strokeAA);

        canvas.DrawLine(cx, cy - half * 0.3f, cx, cy + half * 0.2f, _strokeAA2);
        canvas.DrawCircle(cx, cy + half * 0.45f, 1.5f, _strokeAA2);
    }

    /// <summary>Absperrung (rot-weiss gestreifter Balken auf 2 Fuessen).</summary>
    private void DrawBarrierIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float barW = half * 1.6f;
        float barH = half * 0.35f;
        float barY = cy - half * 0.15f;

        _fillAA.Color = new SKColor(0xE5, 0x39, 0x35, alpha);
        _fillAA2.Color = new SKColor(0xF5, 0xF5, 0xF5, alpha);
        _strokeAA.Color = new SKColor(0x90, 0x90, 0x90, alpha);
        _strokeAA.StrokeWidth = 2.5f;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;

        canvas.DrawLine(cx - half * 0.55f, barY + barH, cx - half * 0.55f, cy + half * 0.8f, _strokeAA);
        canvas.DrawLine(cx + half * 0.55f, barY + barH, cx + half * 0.55f, cy + half * 0.8f, _strokeAA);

        var barRect = new SKRect(cx - barW / 2, barY, cx + barW / 2, barY + barH);
        canvas.DrawRoundRect(barRect, 2, 2, _fillAA);

        canvas.Save();
        canvas.ClipRect(barRect);
        float stripeW = barH * 0.6f;
        for (float sx = cx - barW / 2 - barH; sx < cx + barW / 2 + barH; sx += stripeW * 2)
        {
            _cachedPath.Rewind();
            _cachedPath.MoveTo(sx, barY + barH);
            _cachedPath.LineTo(sx + barH, barY);
            _cachedPath.LineTo(sx + barH + stripeW, barY);
            _cachedPath.LineTo(sx + stripeW, barY + barH);
            _cachedPath.Close();
            canvas.DrawPath(_cachedPath, _fillAA2);
        }
        canvas.Restore();
    }

    /// <summary>Riss (Zickzack-Linie von oben nach unten).</summary>
    private void DrawCrackIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        _strokeAA.Color = new SKColor(0x40, 0x40, 0x40, alpha);
        _strokeAA.StrokeWidth = 2.5f;
        _strokeAA.StrokeCap = SKStrokeCap.Round;
        _strokeAA.StrokeJoin = SKStrokeJoin.Round;
        _strokeAA2.Color = new SKColor(0xA0, 0xA0, 0xA0, alpha);
        _strokeAA2.StrokeWidth = 1;
        _strokeAA2.StrokeCap = SKStrokeCap.Butt;

        // Hauptriss
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx - half * 0.1f, cy - half * 0.9f);
        _cachedPath.LineTo(cx + half * 0.3f, cy - half * 0.5f);
        _cachedPath.LineTo(cx - half * 0.25f, cy - half * 0.15f);
        _cachedPath.LineTo(cx + half * 0.2f, cy + half * 0.2f);
        _cachedPath.LineTo(cx - half * 0.15f, cy + half * 0.5f);
        _cachedPath.LineTo(cx + half * 0.1f, cy + half * 0.9f);
        canvas.DrawPath(_cachedPath, _strokeAA);

        // Lichtreflex (teilweise parallel zum Hauptriss)
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx - half * 0.1f + 3, cy - half * 0.9f);
        _cachedPath.LineTo(cx + half * 0.3f + 3, cy - half * 0.5f);
        _cachedPath.LineTo(cx - half * 0.25f + 3, cy - half * 0.15f);
        canvas.DrawPath(_cachedPath, _strokeAA2);
    }

    /// <summary>Flamme (orange/rote Tropfen-Form mit innerem gelben Kern).</summary>
    private void DrawFireIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        // Aeussere Flamme (rot-orange)
        _fillAA.Color = new SKColor(0xE5, 0x50, 0x00, alpha);
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx, cy - half * 0.9f);
        _cachedPath.CubicTo(cx + half * 0.6f, cy - half * 0.3f, cx + half * 0.7f, cy + half * 0.3f, cx + half * 0.4f, cy + half * 0.8f);
        _cachedPath.CubicTo(cx + half * 0.1f, cy + half, cx - half * 0.1f, cy + half, cx - half * 0.4f, cy + half * 0.8f);
        _cachedPath.CubicTo(cx - half * 0.7f, cy + half * 0.3f, cx - half * 0.6f, cy - half * 0.3f, cx, cy - half * 0.9f);
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA);

        // Mittlere Flamme (orange)
        _fillAA2.Color = new SKColor(0xFF, 0x8F, 0x00, alpha);
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx, cy - half * 0.5f);
        _cachedPath.CubicTo(cx + half * 0.35f, cy - half * 0.1f, cx + half * 0.4f, cy + half * 0.3f, cx + half * 0.2f, cy + half * 0.7f);
        _cachedPath.CubicTo(cx, cy + half * 0.8f, cx - half * 0.05f, cy + half * 0.8f, cx - half * 0.2f, cy + half * 0.7f);
        _cachedPath.CubicTo(cx - half * 0.4f, cy + half * 0.3f, cx - half * 0.35f, cy - half * 0.1f, cx, cy - half * 0.5f);
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA2);

        // Innerer Kern (gelb)
        _fillAA3.Color = new SKColor(0xFF, 0xEB, 0x3B, alpha);
        _cachedPath.Rewind();
        _cachedPath.MoveTo(cx, cy + half * 0.05f);
        _cachedPath.CubicTo(cx + half * 0.15f, cy + half * 0.25f, cx + half * 0.12f, cy + half * 0.55f, cx, cy + half * 0.65f);
        _cachedPath.CubicTo(cx - half * 0.12f, cy + half * 0.55f, cx - half * 0.15f, cy + half * 0.25f, cx, cy + half * 0.05f);
        _cachedPath.Close();
        canvas.DrawPath(_cachedPath, _fillAA3);
    }

    /// <summary>X-Kreuz (2 rote diagonale Linien).</summary>
    private void DrawCrossIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float len = half * 0.7f;
        _strokeAA.Color = new SKColor(0xEF, 0x53, 0x50, alpha);
        _strokeAA.StrokeWidth = 4;
        _strokeAA.StrokeCap = SKStrokeCap.Round;

        canvas.DrawLine(cx - len, cy - len, cx + len, cy + len, _strokeAA);
        canvas.DrawLine(cx + len, cy - len, cx - len, cy + len, _strokeAA);
    }

    /// <summary>Stoppschild (roter Kreis mit weissem horizontalem Strich).</summary>
    private void DrawStopIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float radius = half * 0.75f;
        _fillAA.Color = new SKColor(0xE5, 0x39, 0x35, alpha);
        _strokeAA.Color = new SKColor(0xB7, 0x1C, 0x1C, alpha);
        _strokeAA.StrokeWidth = 2;
        _strokeAA.StrokeCap = SKStrokeCap.Butt;
        _strokeAA2.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);
        _strokeAA2.StrokeWidth = 3;
        _strokeAA2.StrokeCap = SKStrokeCap.Round;

        canvas.DrawCircle(cx, cy, radius, _fillAA);
        canvas.DrawCircle(cx, cy, radius, _strokeAA);
        canvas.DrawLine(cx - radius * 0.55f, cy, cx + radius * 0.55f, cy, _strokeAA2);
    }

    /// <summary>Loch (dunkler Kreis mit hellem Rand - perspektivisch).</summary>
    private void DrawHoleIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        float outerR = half * 0.7f;
        float innerR = half * 0.5f;

        _fillAA.Color = new SKColor(0x90, 0x9D, 0xA5, alpha);
        canvas.DrawOval(cx, cy, outerR, outerR * 0.75f, _fillAA);

        _fillAA2.Color = new SKColor(0x1A, 0x1A, 0x1A, alpha);
        canvas.DrawOval(cx, cy + 1, innerR, innerR * 0.7f, _fillAA2);

        _fillAA3.Color = new SKColor(0x30, 0x30, 0x30, (byte)(alpha * 0.5f));
        canvas.DrawOval(cx, cy - 1, innerR * 0.8f, innerR * 0.3f, _fillAA3);
    }

    /// <summary>Wasserleck (3 blaue Tropfen fallend).</summary>
    private void DrawLeakIcon(SKCanvas canvas, float cx, float cy, float half, byte alpha)
    {
        _fillAA.Color = new SKColor(0x42, 0xA5, 0xF5, alpha);
        _fillAA2.Color = new SKColor(0x90, 0xCA, 0xF9, alpha);

        float[] dropXOffsets = { -half * 0.35f, half * 0.1f, half * 0.4f };
        float[] dropYOffsets = { -half * 0.4f, half * 0.1f, -half * 0.1f };
        float[] dropSizes = { half * 0.28f, half * 0.35f, half * 0.25f };

        for (int i = 0; i < 3; i++)
        {
            float dx = cx + dropXOffsets[i];
            float dy = cy + dropYOffsets[i];
            float ds = dropSizes[i];

            _cachedPath.Rewind();
            _cachedPath.MoveTo(dx, dy - ds * 1.2f);
            _cachedPath.CubicTo(dx + ds * 0.7f, dy - ds * 0.2f, dx + ds * 0.7f, dy + ds * 0.5f, dx, dy + ds * 0.7f);
            _cachedPath.CubicTo(dx - ds * 0.7f, dy + ds * 0.5f, dx - ds * 0.7f, dy - ds * 0.2f, dx, dy - ds * 1.2f);
            _cachedPath.Close();
            canvas.DrawPath(_cachedPath, _fillAA);

            canvas.DrawCircle(dx - ds * 0.15f, dy - ds * 0.1f, ds * 0.15f, _fillAA2);
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
        _fillNoAA.Color = MagnifierGlass;
        canvas.DrawCircle(cx, cy, baseRadius, _fillNoAA);

        // Metallring
        _strokeNoAA.Color = MagnifierRing;
        _strokeNoAA.StrokeWidth = 3;
        _strokeNoAA.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawCircle(cx, cy, baseRadius, _strokeNoAA);

        // Glanz auf dem Glas (kleiner heller Punkt)
        _fillNoAA.Color = new SKColor(255, 255, 255, 80);
        canvas.DrawCircle(cx - 3, cy - 3, 3, _fillNoAA);

        // Griff (diagonal nach unten rechts)
        _strokeNoAA.Color = MagnifierHandle;
        _strokeNoAA.StrokeWidth = 4;
        _strokeNoAA.StrokeCap = SKStrokeCap.Round;
        float handleStartX = cx + baseRadius * 0.6f;
        float handleStartY = cy + baseRadius * 0.6f;
        canvas.DrawLine(handleStartX, handleStartY, handleStartX + 10, handleStartY + 10, _strokeNoAA);

        // Griff-Akzent (hellere Kante, sequentiell nach Griff)
        _strokeNoAA.Color = new SKColor(0x8D, 0x6E, 0x63);
        _strokeNoAA.StrokeWidth = 2;
        canvas.DrawLine(handleStartX + 1, handleStartY, handleStartX + 9, handleStartY + 8, _strokeNoAA);

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

            _fillNoAA.Color = new SKColor(0xB0, 0xBE, 0xC5, finalAlpha);
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, _fillNoAA);
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
            _fillNoAA.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));
            canvas.DrawRect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size, _fillNoAA);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IDisposable
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fillNoAA.Dispose();
        _fillAA.Dispose();
        _fillAA2.Dispose();
        _fillAA3.Dispose();
        _strokeNoAA.Dispose();
        _strokeAA.Dispose();
        _strokeAA2.Dispose();
        _strokeAA3.Dispose();
        _cachedPath.Dispose();
    }
}
