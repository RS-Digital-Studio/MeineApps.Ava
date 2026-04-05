using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Grundriss-Raetsel Mini-Game.
/// Zeichnet einen Architektenplan: Weisses Papier, blaue Grundrisslinien,
/// Slots mit Hint-Icons, gefuellte Slots mit Label, Korrekt-Haekchen,
/// Fehler-Blinken, Massstab-Linien am Rand.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// Platzierungs-Partikel (gruene Funken bei korrekt), Completion-Celebration
/// mit goldenem Grundriss-Glow.
/// </summary>
public sealed class DesignPuzzleRenderer : IDisposable
{
    private bool _disposed;

    // AI-Hintergrund (optionaler Layer unter den Spielelementen)
    private IGameAssetService? _assetService;
    private SKBitmap? _background;

    // Animationszeit (wird intern hochgezaehlt)
    private float _time;

    // ═══════════════════════════════════════════════════════════════════════
    // Wiederverwendbare SKPaint-Instanzen (vermeidet per-Frame Allokationen)
    // ═══════════════════════════════════════════════════════════════════════

    // Allgemeiner Fill-Paint (ohne Antialiasing)
    private readonly SKPaint _fillPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    // Fill-Paint mit Antialiasing (fuer Kreise, Glow-Effekte)
    private readonly SKPaint _fillPaintAA = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Allgemeiner Stroke-Paint (ohne Antialiasing, variable Breite)
    private readonly SKPaint _strokePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };

    // Stroke-Paint mit Antialiasing (fuer Haekchen, runde Linien)
    private readonly SKPaint _strokePaintAA = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    // Duenne Linien (Raster, Massstab, Textur - StrokeWidth=1)
    private readonly SKPaint _thinStrokePaint = new() { IsAntialias = false, StrokeWidth = 1 };

    // Text-Paint (immer AA)
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachte Font-Instanz fuer Text-Rendering
    private readonly SKFont _cachedFont = new(SKTypeface.Default, 12);

    // ═══════════════════════════════════════════════════════════════════════
    // Fehler-Flash (Array-basiert statt Dictionary)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_SLOTS = 20;
    private readonly float[] _errorFlashTimers = new float[MAX_SLOTS];

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel-System (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_SPARKS = 30;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
    }

    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;

    // Zustandsverfolgung fuer Platzierungs-Erkennung
    private int _prevCorrectCount;
    private bool _prevAllCorrect;
    private float _completionGlowTimer;

    // Farb-Palette (Architektenplan)
    private static readonly SKColor PaperWhite = new(0xF5, 0xF0, 0xE8);       // Warmes Papier-Weiss
    private static readonly SKColor BlueprintLine = new(0x1A, 0x6B, 0xAF);     // Blaupause-Linienfarbe
    private static readonly SKColor BlueprintLineLight = new(0x6B, 0xAE, 0xD6); // Hellere Hilfslinien
    private static readonly SKColor BlueprintGrid = new(0xBB, 0xD7, 0xEA);     // Feines Raster
    private static readonly SKColor SlotBorderEmpty = new(0x78, 0x90, 0x9C);   // Leerer Slot Rand
    private static readonly SKColor SlotFillEmpty = new(0xEC, 0xEF, 0xF1);     // Leerer Slot Fuellfarbe
    private static readonly SKColor CorrectGreen = new(0x4C, 0xAF, 0x50);      // Korrekt platziert
    private static readonly SKColor ErrorRed = new(0xF4, 0x43, 0x36);          // Fehler-Flash
    private static readonly SKColor HintTextColor = new(0x90, 0xA4, 0xAE);     // Hint-Icon Farbe (transparent)
    private static readonly SKColor QuestionMarkColor = new(0xB0, 0xBE, 0xC5); // "?" Overlay
    private static readonly SKColor CheckmarkWhite = new(0xFF, 0xFF, 0xFF);    // Haekchen-Farbe
    private static readonly SKColor ScaleLineColor = new(0x78, 0x90, 0x9C);    // Massstab-Linien
    private static readonly SKColor CraftOrange = new(0xEA, 0x58, 0x0C);       // Craft-Akzentfarbe

    /// <summary>
    /// Daten-Struct fuer einen einzelnen Slot (vom ViewModel befuellt).
    /// </summary>
    public struct RoomSlotData
    {
        public uint HintColor;       // Raum-Farbe als Hinweis (ARGB)
        public string DisplayLabel;  // Lokalisierter Raumname (fuer gefuellte Slots)
        public uint FilledColor;     // Farbe des platzierten Raums (ARGB)
        public uint BackgroundColor; // ARGB
        public uint BorderColor;     // ARGB
        public bool IsFilled;
        public bool IsCorrect;
        public bool HasError;
    }

    /// <summary>
    /// Initialisiert den AI-Asset-Service für den Hintergrund.
    /// </summary>
    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Rendert den gesamten Grundriss auf das Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, RoomSlotData[] slots, int cols, int rows,
        int filledCorrectCount, int totalSlots, float deltaTime)
    {
        // AI-Hintergrund als Atmosphäre-Layer
        if (_assetService != null)
        {
            _background ??= _assetService.GetBitmap("minigames/design_puzzle_bg.webp");
            if (_background == null)
                _ = _assetService.LoadBitmapAsync("minigames/design_puzzle_bg.webp");
            if (_background != null)
                canvas.DrawBitmap(_background, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }

        _time += deltaTime;

        // Fehler-Flash Timer aktualisieren
        UpdateErrorFlash(slots, deltaTime);

        float padding = 12;
        float innerLeft = bounds.Left + padding;
        float innerTop = bounds.Top + padding;
        float innerWidth = bounds.Width - 2 * padding;
        float innerHeight = bounds.Height - 2 * padding;

        // 1. Papier-Hintergrund
        DrawPaperBackground(canvas, bounds);

        // 2. Blaupause-Raster (feines Hintergrundmuster)
        DrawBlueprintGrid(canvas, bounds);

        // 3. Massstab-Linien am Rand (Deko)
        DrawScaleMarks(canvas, bounds, innerLeft, innerTop, innerWidth, innerHeight);

        // 4. Grundriss-Slots berechnen und zeichnen
        if (slots.Length == 0) return;

        // Slot-Groesse berechnen (gleichmaessig verteilt)
        float slotSpacing = 6;
        float availW = innerWidth - 40; // Platz fuer Massstab-Linien links/rechts
        float availH = innerHeight - 40;
        float slotW = (availW - (cols - 1) * slotSpacing) / cols;
        float slotH = (availH - (rows - 1) * slotSpacing) / rows;
        float slotSize = Math.Min(slotW, slotH);

        // Grid zentrieren
        float totalW = cols * slotSize + (cols - 1) * slotSpacing;
        float totalH = rows * slotSize + (rows - 1) * slotSpacing;
        float gridLeft = bounds.Left + (bounds.Width - totalW) / 2;
        // Oben ausrichten statt vertikal zentrieren
        float gridTop = bounds.Top + padding + 20; // +20 fuer Massstab-Markierungen oben

        // 5. Grundriss-Umriss (dicker blauer Rahmen)
        DrawFloorplanOutline(canvas, gridLeft, gridTop, totalW, totalH);

        // 6. Slots zeichnen
        for (int i = 0; i < slots.Length && i < cols * rows; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float sx = gridLeft + col * (slotSize + slotSpacing);
            float sy = gridTop + row * (slotSize + slotSpacing);

            DrawSlot(canvas, sx, sy, slotSize, slotSize, slots[i], i);
        }

        // 7. Innere Trennlinien (Grundriss-Waende)
        DrawInnerWalls(canvas, gridLeft, gridTop, totalW, totalH, cols, rows, slotSize, slotSpacing);

        // 8. Platzierungs-Partikel: neue korrekte Platzierungen erkennen
        DetectNewCorrectPlacements(slots, filledCorrectCount, gridLeft, gridTop, slotSize, slotSpacing, cols);

        // 9. Funken-Partikel zeichnen
        UpdateAndDrawSparks(canvas, deltaTime);

        // 10. Completion-Celebration
        bool allCorrect = totalSlots > 0 && filledCorrectCount >= totalSlots;
        if (allCorrect && !_prevAllCorrect)
        {
            _completionGlowTimer = 2.0f; // 2s goldener Grundriss-Glow
        }
        _prevAllCorrect = allCorrect;

        if (_completionGlowTimer > 0)
        {
            DrawCompletionGlow(canvas, gridLeft, gridTop, totalW, totalH);
            _completionGlowTimer -= deltaTime;
        }
    }

    /// <summary>
    /// HitTest: Gibt den Slot-Index zurueck (-1 wenn kein Treffer).
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int rows, int slotCount)
    {
        if (slotCount == 0 || cols == 0 || rows == 0) return -1;

        float padding = 12;
        float innerWidth = bounds.Width - 2 * padding;
        float innerHeight = bounds.Height - 2 * padding;

        float slotSpacing = 6;
        float availW = innerWidth - 40;
        float availH = innerHeight - 40;
        float slotW = (availW - (cols - 1) * slotSpacing) / cols;
        float slotH = (availH - (rows - 1) * slotSpacing) / rows;
        float slotSize = Math.Min(slotW, slotH);

        float totalW = cols * slotSize + (cols - 1) * slotSpacing;
        float gridLeft = bounds.Left + (bounds.Width - totalW) / 2;
        // Oben ausrichten (identisch mit Render)
        float gridTop = bounds.Top + padding + 20;

        for (int i = 0; i < slotCount && i < cols * rows; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float sx = gridLeft + col * (slotSize + slotSpacing);
            float sy = gridTop + row * (slotSize + slotSpacing);

            if (touchX >= sx && touchX <= sx + slotSize &&
                touchY >= sy && touchY <= sy + slotSize)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Setzt einen Fehler-Flash fuer einen bestimmten Slot (Interface-kompatibel).
    /// </summary>
    public void TriggerErrorFlash(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < MAX_SLOTS)
        {
            _errorFlashTimers[slotIndex] = 0.4f; // 400ms Flash
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ZEICHENFUNKTIONEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den Papier-Hintergrund mit leichter Textur.
    /// </summary>
    private void DrawPaperBackground(SKCanvas canvas, SKRect bounds)
    {
        // Basis-Papierfarbe
        _fillPaint.Color = PaperWhite;
        canvas.DrawRect(bounds, _fillPaint);

        // Leichte Papier-Textur (subtile horizontale Linien)
        _thinStrokePaint.Color = new SKColor(0xE8, 0xE0, 0xD5, 30);
        for (float y = bounds.Top + 8; y < bounds.Bottom; y += 12)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _thinStrokePaint);
        }

        // Papier-Schatten am Rand (dunkler Streifen)
        _fillPaint.Color = new SKColor(0x00, 0x00, 0x00, 15);
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 3, _fillPaint);
        canvas.DrawRect(bounds.Left, bounds.Top, 3, bounds.Height, _fillPaint);
        canvas.DrawRect(bounds.Right - 3, bounds.Top, 3, bounds.Height, _fillPaint);
        canvas.DrawRect(bounds.Left, bounds.Bottom - 3, bounds.Width, 3, _fillPaint);
    }

    /// <summary>
    /// Zeichnet das feine Blaupause-Raster im Hintergrund.
    /// </summary>
    private void DrawBlueprintGrid(SKCanvas canvas, SKRect bounds)
    {
        _thinStrokePaint.Color = BlueprintGrid.WithAlpha(40);

        // Vertikale Linien
        for (float x = bounds.Left + 20; x < bounds.Right; x += 20)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, _thinStrokePaint);
        }

        // Horizontale Linien
        for (float y = bounds.Top + 20; y < bounds.Bottom; y += 20)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _thinStrokePaint);
        }
    }

    /// <summary>
    /// Zeichnet Massstab-Markierungen am Rand (Architektenplan-Deko).
    /// </summary>
    private void DrawScaleMarks(SKCanvas canvas, SKRect bounds, float innerLeft, float innerTop, float innerWidth, float innerHeight)
    {
        _thinStrokePaint.Color = ScaleLineColor;

        // Linker Rand: vertikale Massstab-Striche
        for (float y = innerTop + 10; y < innerTop + innerHeight; y += 15)
        {
            float tickLen = ((int)((y - innerTop) / 15) % 5 == 0) ? 8 : 4;
            canvas.DrawLine(innerLeft + 2, y, innerLeft + 2 + tickLen, y, _thinStrokePaint);
        }

        // Oberer Rand: horizontale Massstab-Striche
        for (float x = innerLeft + 20; x < innerLeft + innerWidth; x += 15)
        {
            float tickLen = ((int)((x - innerLeft - 20) / 15) % 5 == 0) ? 8 : 4;
            canvas.DrawLine(x, innerTop + 2, x, innerTop + 2 + tickLen, _thinStrokePaint);
        }

        // Massstab-Linie links (durchgehend)
        canvas.DrawLine(innerLeft + 2, innerTop + 10, innerLeft + 2, innerTop + innerHeight - 10, _thinStrokePaint);

        // Massstab-Linie oben (durchgehend)
        canvas.DrawLine(innerLeft + 20, innerTop + 2, innerLeft + innerWidth - 10, innerTop + 2, _thinStrokePaint);
    }

    /// <summary>
    /// Zeichnet den aeusseren Grundriss-Rahmen (dicke blaue Linien).
    /// </summary>
    private void DrawFloorplanOutline(SKCanvas canvas, float x, float y, float w, float h)
    {
        // Aeussere Wand (dick)
        _strokePaint.Color = BlueprintLine;
        _strokePaint.StrokeWidth = 4;
        canvas.DrawRect(x - 4, y - 4, w + 8, h + 8, _strokePaint);

        // Schatten-Effekt (dezent)
        _strokePaint.Color = BlueprintLine.WithAlpha(30);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawRect(x - 7, y - 7, w + 14, h + 14, _strokePaint);
    }

    /// <summary>
    /// Zeichnet innere Trennwaende zwischen den Slots (Grundriss-Stil).
    /// </summary>
    private void DrawInnerWalls(SKCanvas canvas, float gridLeft, float gridTop, float totalW, float totalH,
        int cols, int rows, float slotSize, float spacing)
    {
        _strokePaint.Color = BlueprintLine;
        _strokePaint.StrokeWidth = 2;

        // Vertikale Waende
        for (int c = 1; c < cols; c++)
        {
            float wx = gridLeft + c * (slotSize + spacing) - spacing / 2;
            canvas.DrawLine(wx, gridTop - 4, wx, gridTop + totalH + 4, _strokePaint);
        }

        // Horizontale Waende
        for (int r = 1; r < rows; r++)
        {
            float wy = gridTop + r * (slotSize + spacing) - spacing / 2;
            canvas.DrawLine(gridLeft - 4, wy, gridLeft + totalW + 4, wy, _strokePaint);
        }

        // Tuer-Oeffnungen (kleine Luecken in den Waenden fuer optisches Detail)
        _fillPaint.Color = PaperWhite;
        float doorWidth = slotSize * 0.3f;

        // Horizontale Tuer-Luecken (in vertikalen Waenden)
        for (int c = 1; c < cols; c++)
        {
            float wx = gridLeft + c * (slotSize + spacing) - spacing / 2;
            for (int r = 0; r < rows; r++)
            {
                float doorY = gridTop + r * (slotSize + spacing) + slotSize / 2 - doorWidth / 2;
                canvas.DrawRect(wx - 2, doorY, 4, doorWidth, _fillPaint);
            }
        }

        // Vertikale Tuer-Luecken (in horizontalen Waenden)
        for (int r = 1; r < rows; r++)
        {
            float wy = gridTop + r * (slotSize + spacing) - spacing / 2;
            for (int c = 0; c < cols; c++)
            {
                float doorX = gridLeft + c * (slotSize + spacing) + slotSize / 2 - doorWidth / 2;
                canvas.DrawRect(doorX, wy - 2, doorWidth, 4, _fillPaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet einen einzelnen Slot (leer oder gefuellt).
    /// </summary>
    private void DrawSlot(SKCanvas canvas, float x, float y, float w, float h, RoomSlotData slot, int index)
    {
        // Fehler-Flash pruefen (Array-basiert)
        bool isFlashing = index < MAX_SLOTS && _errorFlashTimers[index] > 0;

        if (slot.IsFilled)
        {
            DrawFilledSlot(canvas, x, y, w, h, slot);
        }
        else if (isFlashing)
        {
            DrawErrorSlot(canvas, x, y, w, h, slot, _errorFlashTimers[index]);
        }
        else
        {
            DrawEmptySlot(canvas, x, y, w, h, slot);
        }
    }

    /// <summary>
    /// Zeichnet einen leeren Slot mit gestricheltem Rahmen und farbigem Hinweis.
    /// </summary>
    private void DrawEmptySlot(SKCanvas canvas, float x, float y, float w, float h, RoomSlotData slot)
    {
        // Hintergrund (leicht gefuellt)
        _fillPaint.Color = SlotFillEmpty;
        canvas.DrawRect(x, y, w, h, _fillPaint);

        // Farbiger Hinweis-Streifen am oberen Rand (zeigt welcher Raum hierhin gehoert)
        var hintColor = new SKColor(slot.HintColor);
        _fillPaint.Color = hintColor.WithAlpha(120);
        canvas.DrawRect(x, y, w, 5, _fillPaint);

        // Farbiger Punkt in der Mitte als Hinweis
        float pulse = (float)(0.5 + 0.3 * Math.Sin(_time * 2.5));
        _fillPaintAA.Color = hintColor.WithAlpha((byte)(pulse * 100));
        float dotRadius = Math.Min(w, h) * 0.15f;
        canvas.DrawCircle(x + w / 2, y + h / 2, dotRadius, _fillPaintAA);

        // Gestrichelter Rahmen in Hint-Farbe
        _strokePaint.Color = hintColor.WithAlpha(100);
        _strokePaint.StrokeWidth = 2;
        _strokePaint.PathEffect?.Dispose();
        _strokePaint.PathEffect = SKPathEffect.CreateDash([6, 4], _time * 8);
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, _strokePaint);
        _strokePaint.PathEffect?.Dispose(); // Native-Speicher freigeben
        _strokePaint.PathEffect = null;

        // "?" unter dem Punkt
        float qSize = Math.Min(w, h) * 0.2f;
        _cachedFont.Size = qSize;
        _textPaint.Color = hintColor.WithAlpha(140);
        canvas.DrawText("?", x + w / 2, y + h / 2 + dotRadius + qSize, SKTextAlign.Center, _cachedFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet einen gefuellten Slot mit Raumfarbe und Name.
    /// </summary>
    private void DrawFilledSlot(SKCanvas canvas, float x, float y, float w, float h, RoomSlotData slot)
    {
        // Raum-Farbe als Hintergrund
        var roomColor = slot.FilledColor != 0 ? new SKColor(slot.FilledColor) : new SKColor(slot.BackgroundColor);
        _fillPaint.Color = roomColor;
        canvas.DrawRect(x, y, w, h, _fillPaint);

        // Hellerer Innenbereich (leichte Tiefe)
        _fillPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 35);
        canvas.DrawRect(x + 3, y + 3, w - 6, h - 6, _fillPaint);

        // Rahmen
        _strokePaint.Color = new SKColor(slot.BorderColor);
        _strokePaint.StrokeWidth = 2;
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, _strokePaint);

        // Raumname in der Mitte
        if (!string.IsNullOrEmpty(slot.DisplayLabel))
        {
            // Schriftgroesse an Slot-Groesse und Textlaenge anpassen
            float maxFontSize = Math.Min(w, h) * 0.22f;
            float fontSize = Math.Min(maxFontSize, w * 0.8f / Math.Max(slot.DisplayLabel.Length * 0.55f, 1));
            fontSize = Math.Max(fontSize, 8); // Minimum 8px

            _cachedFont.Size = fontSize;
            _cachedFont.Embolden = true;
            _textPaint.Color = CheckmarkWhite;
            canvas.DrawText(slot.DisplayLabel, x + w / 2, y + h / 2 + fontSize / 3, SKTextAlign.Center, _cachedFont, _textPaint);
            _cachedFont.Embolden = false; // Zuruecksetzen
        }

        // Korrekt-Haekchen unten rechts (gruener Kreis + weisses Haekchen)
        if (slot.IsCorrect)
        {
            float checkSize = Math.Min(w, h) * 0.22f;
            float cx = x + w - checkSize - 4;
            float cy = y + h - checkSize - 4;

            // Gruener Kreis
            _fillPaintAA.Color = CorrectGreen;
            canvas.DrawCircle(cx + checkSize / 2, cy + checkSize / 2, checkSize / 2, _fillPaintAA);

            // Weisses Haekchen (vereinfacht als 2 Linien)
            _strokePaintAA.Color = CheckmarkWhite;
            _strokePaintAA.StrokeWidth = 2;
            _strokePaintAA.StrokeCap = SKStrokeCap.Round;
            float midX = cx + checkSize / 2;
            float midY = cy + checkSize / 2;
            canvas.DrawLine(midX - checkSize * 0.2f, midY, midX - checkSize * 0.05f, midY + checkSize * 0.2f, _strokePaintAA);
            canvas.DrawLine(midX - checkSize * 0.05f, midY + checkSize * 0.2f, midX + checkSize * 0.25f, midY - checkSize * 0.15f, _strokePaintAA);

            // Subtiler Glow
            _fillPaintAA.Color = CorrectGreen.WithAlpha(40);
            canvas.DrawCircle(cx + checkSize / 2, cy + checkSize / 2, checkSize / 2 + 3, _fillPaintAA);
        }
    }

    /// <summary>
    /// Zeichnet einen Slot im Fehler-Zustand (rotes Blinken).
    /// </summary>
    private void DrawErrorSlot(SKCanvas canvas, float x, float y, float w, float h, RoomSlotData slot, float flashTimeLeft)
    {
        // Roter Flash-Hintergrund (Intensitaet basierend auf verbleibender Zeit)
        float intensity = flashTimeLeft / 0.4f; // 0-1
        float flashPulse = (float)(0.5 + 0.5 * Math.Sin(_time * 20)); // Schnelles Blinken
        byte alpha = (byte)(intensity * flashPulse * 180);

        _fillPaint.Color = ErrorRed.WithAlpha(alpha);
        canvas.DrawRect(x, y, w, h, _fillPaint);

        // Basis-Slot darunter (leerer Slot Hintergrund)
        _fillPaint.Color = SlotFillEmpty.WithAlpha((byte)(255 - alpha));
        canvas.DrawRect(x, y, w, h, _fillPaint);

        // Roter Rahmen
        _strokePaint.Color = ErrorRed.WithAlpha((byte)(100 + intensity * 155));
        _strokePaint.StrokeWidth = 3;
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, _strokePaint);

        // "X" in der Mitte
        float xSize = Math.Min(w, h) * 0.2f;
        float cx = x + w / 2;
        float cy = y + h / 2;
        _strokePaint.Color = ErrorRed.WithAlpha((byte)(intensity * 255));
        _strokePaint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, _strokePaint);
        canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, _strokePaint);
        _strokePaint.StrokeCap = SKStrokeCap.Butt; // Zuruecksetzen

        // Farbiger Hinweis-Streifen oben (dezent durch Flash sichtbar)
        var hintColor = new SKColor(slot.HintColor);
        _fillPaint.Color = hintColor.WithAlpha((byte)(40 * (1 - intensity)));
        canvas.DrawRect(x, y, w, 5, _fillPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FEHLER-FLASH (Array-basiert)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert die Fehler-Flash Timer (Array statt Dictionary).
    /// </summary>
    private void UpdateErrorFlash(RoomSlotData[] slots, float deltaTime)
    {
        for (int i = 0; i < slots.Length && i < MAX_SLOTS; i++)
        {
            // Neue Fehler-Slots registrieren
            if (slots[i].HasError && _errorFlashTimers[i] <= 0)
            {
                _errorFlashTimers[i] = 0.4f;
            }

            // Timer herunterzaehlen
            if (_errorFlashTimers[i] > 0)
            {
                _errorFlashTimers[i] -= deltaTime;
                if (_errorFlashTimers[i] < 0) _errorFlashTimers[i] = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLATZIERUNGS-PARTIKEL (gruene Funken bei korrekt)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt neue korrekte Platzierungen und spawnt gruene Funken-Partikel.
    /// </summary>
    private void DetectNewCorrectPlacements(RoomSlotData[] slots, int filledCorrectCount,
        float gridLeft, float gridTop, float slotSize, float slotSpacing, int cols)
    {
        if (filledCorrectCount > _prevCorrectCount && _prevCorrectCount >= 0)
        {
            // Neuen korrekt platzierten Slot finden
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].IsCorrect && slots[i].IsFilled)
                {
                    int col = i % cols;
                    int row = i / cols;
                    float cx = gridLeft + col * (slotSize + slotSpacing) + slotSize / 2;
                    float cy = gridTop + row * (slotSize + slotSpacing) + slotSize / 2;

                    // 8 gruene Partikel spawnen
                    SpawnCorrectSparks(cx, cy);
                    break;
                }
            }
        }
        _prevCorrectCount = filledCorrectCount;
    }

    /// <summary>
    /// Spawnt 8 gruene Funken vom Slot-Mittelpunkt.
    /// </summary>
    private void SpawnCorrectSparks(float cx, float cy)
    {
        var rng = Random.Shared;
        for (int i = 0; i < 8 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float speed = 35 + (float)(rng.NextDouble() * 50);

            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx,
                Y = cy,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Life = 0,
                MaxLife = 0.5f + (float)(rng.NextDouble() * 0.4f),
                Size = 2 + (float)(rng.NextDouble() * 2),
                R = CorrectGreen.Red,
                G = CorrectGreen.Green,
                B = CorrectGreen.Blue
            };
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet alle aktiven Funken-Partikel.
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
            _fillPaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));
            canvas.DrawRect(p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size, _fillPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETION-CELEBRATION (goldener Grundriss-Glow)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen goldenen Glow ueber den gesamten Grundriss wenn alle Raeume korrekt platziert sind.
    /// Zusaetzlich goldene Partikel entlang des Grundrisses.
    /// </summary>
    private void DrawCompletionGlow(SKCanvas canvas, float gridLeft, float gridTop, float totalW, float totalH)
    {
        float intensity = _completionGlowTimer / 2.0f; // 0-1, nimmt ab
        float pulse = (float)(0.5 + 0.5 * Math.Sin(_time * 6));

        // Goldener Overlay ueber den Grundriss
        byte glowAlpha = (byte)(intensity * pulse * 60);
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00, glowAlpha);
        canvas.DrawRect(gridLeft - 4, gridTop - 4, totalW + 8, totalH + 8, _fillPaint);

        // Goldener Rahmen (pulsierend)
        byte borderAlpha = (byte)(intensity * 200);
        _strokePaint.Color = new SKColor(0xFF, 0xD7, 0x00, borderAlpha);
        _strokePaint.StrokeWidth = 3;
        canvas.DrawRect(gridLeft - 8, gridTop - 8, totalW + 16, totalH + 16, _strokePaint);

        // Goldene Completion-Partikel entlang des Randes spawnen
        if (intensity > 0.2f)
        {
            var rng = Random.Shared;
            // Pro Frame 1-2 Partikel am Rand spawnen
            for (int s = 0; s < 2 && _sparkCount < MAX_SPARKS; s++)
            {
                if (rng.NextDouble() > 0.5) continue;

                float px, py;
                int edge = rng.Next(4);
                switch (edge)
                {
                    case 0: px = gridLeft + (float)(rng.NextDouble() * totalW); py = gridTop - 4; break;
                    case 1: px = gridLeft + (float)(rng.NextDouble() * totalW); py = gridTop + totalH + 4; break;
                    case 2: px = gridLeft - 4; py = gridTop + (float)(rng.NextDouble() * totalH); break;
                    default: px = gridLeft + totalW + 4; py = gridTop + (float)(rng.NextDouble() * totalH); break;
                }

                _sparks[_sparkCount++] = new SparkParticle
                {
                    X = px,
                    Y = py,
                    VelocityX = (float)(rng.NextDouble() - 0.5) * 30,
                    VelocityY = -20 - (float)(rng.NextDouble() * 30),
                    Life = 0,
                    MaxLife = 0.6f + (float)(rng.NextDouble() * 0.5f),
                    Size = 2 + (float)(rng.NextDouble() * 2),
                    R = 0xFF,
                    G = 0xD7,
                    B = 0x00
                };
            }
        }
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fillPaint.Dispose();
        _fillPaintAA.Dispose();
        _strokePaint.Dispose();
        _strokePaintAA.Dispose();
        _thinStrokePaint.Dispose();
        _textPaint.Dispose();
        _cachedFont.Dispose();
    }
}
