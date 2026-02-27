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
public class DesignPuzzleRenderer
{
    // Animationszeit (wird intern hochgezaehlt)
    private float _time;

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
    /// Rendert den gesamten Grundriss auf das Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, RoomSlotData[] slots, int cols, int rows,
        int filledCorrectCount, int totalSlots, float deltaTime)
    {
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
        using var paperPaint = new SKPaint { Color = PaperWhite, IsAntialias = false };
        canvas.DrawRect(bounds, paperPaint);

        // Leichte Papier-Textur (subtile horizontale Linien)
        using var texturePaint = new SKPaint
        {
            Color = new SKColor(0xE8, 0xE0, 0xD5, 30),
            IsAntialias = false,
            StrokeWidth = 1
        };
        for (float y = bounds.Top + 8; y < bounds.Bottom; y += 12)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, texturePaint);
        }

        // Papier-Schatten am Rand (dunkler Streifen)
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0x00, 0x00, 0x00, 15),
            IsAntialias = false
        };
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 3, shadowPaint);
        canvas.DrawRect(bounds.Left, bounds.Top, 3, bounds.Height, shadowPaint);
        canvas.DrawRect(bounds.Right - 3, bounds.Top, 3, bounds.Height, shadowPaint);
        canvas.DrawRect(bounds.Left, bounds.Bottom - 3, bounds.Width, 3, shadowPaint);
    }

    /// <summary>
    /// Zeichnet das feine Blaupause-Raster im Hintergrund.
    /// </summary>
    private void DrawBlueprintGrid(SKCanvas canvas, SKRect bounds)
    {
        using var gridPaint = new SKPaint
        {
            Color = BlueprintGrid.WithAlpha(40),
            IsAntialias = false,
            StrokeWidth = 1
        };

        // Vertikale Linien
        for (float x = bounds.Left + 20; x < bounds.Right; x += 20)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, gridPaint);
        }

        // Horizontale Linien
        for (float y = bounds.Top + 20; y < bounds.Bottom; y += 20)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, gridPaint);
        }
    }

    /// <summary>
    /// Zeichnet Massstab-Markierungen am Rand (Architektenplan-Deko).
    /// </summary>
    private void DrawScaleMarks(SKCanvas canvas, SKRect bounds, float innerLeft, float innerTop, float innerWidth, float innerHeight)
    {
        using var scalePaint = new SKPaint
        {
            Color = ScaleLineColor,
            IsAntialias = false,
            StrokeWidth = 1
        };

        // Linker Rand: vertikale Massstab-Striche
        for (float y = innerTop + 10; y < innerTop + innerHeight; y += 15)
        {
            float tickLen = ((int)((y - innerTop) / 15) % 5 == 0) ? 8 : 4;
            canvas.DrawLine(innerLeft + 2, y, innerLeft + 2 + tickLen, y, scalePaint);
        }

        // Oberer Rand: horizontale Massstab-Striche
        for (float x = innerLeft + 20; x < innerLeft + innerWidth; x += 15)
        {
            float tickLen = ((int)((x - innerLeft - 20) / 15) % 5 == 0) ? 8 : 4;
            canvas.DrawLine(x, innerTop + 2, x, innerTop + 2 + tickLen, scalePaint);
        }

        // Massstab-Linie links (durchgehend)
        canvas.DrawLine(innerLeft + 2, innerTop + 10, innerLeft + 2, innerTop + innerHeight - 10, scalePaint);

        // Massstab-Linie oben (durchgehend)
        canvas.DrawLine(innerLeft + 20, innerTop + 2, innerLeft + innerWidth - 10, innerTop + 2, scalePaint);
    }

    /// <summary>
    /// Zeichnet den aeusseren Grundriss-Rahmen (dicke blaue Linien).
    /// </summary>
    private void DrawFloorplanOutline(SKCanvas canvas, float x, float y, float w, float h)
    {
        // Aeussere Wand (dick)
        using var wallPaint = new SKPaint
        {
            Color = BlueprintLine,
            IsAntialias = false,
            StrokeWidth = 4,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(x - 4, y - 4, w + 8, h + 8, wallPaint);

        // Schatten-Effekt (dezent)
        using var shadowPaint = new SKPaint
        {
            Color = BlueprintLine.WithAlpha(30),
            IsAntialias = false,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(x - 7, y - 7, w + 14, h + 14, shadowPaint);
    }

    /// <summary>
    /// Zeichnet innere Trennwaende zwischen den Slots (Grundriss-Stil).
    /// </summary>
    private void DrawInnerWalls(SKCanvas canvas, float gridLeft, float gridTop, float totalW, float totalH,
        int cols, int rows, float slotSize, float spacing)
    {
        using var wallPaint = new SKPaint
        {
            Color = BlueprintLine,
            IsAntialias = false,
            StrokeWidth = 2
        };

        // Vertikale Waende
        for (int c = 1; c < cols; c++)
        {
            float wx = gridLeft + c * (slotSize + spacing) - spacing / 2;
            canvas.DrawLine(wx, gridTop - 4, wx, gridTop + totalH + 4, wallPaint);
        }

        // Horizontale Waende
        for (int r = 1; r < rows; r++)
        {
            float wy = gridTop + r * (slotSize + spacing) - spacing / 2;
            canvas.DrawLine(gridLeft - 4, wy, gridLeft + totalW + 4, wy, wallPaint);
        }

        // Tuer-Oeffnungen (kleine Luecken in den Waenden fuer optisches Detail)
        using var doorPaint = new SKPaint { Color = PaperWhite, IsAntialias = false };
        float doorWidth = slotSize * 0.3f;

        // Horizontale Tuer-Luecken (in vertikalen Waenden)
        for (int c = 1; c < cols; c++)
        {
            float wx = gridLeft + c * (slotSize + spacing) - spacing / 2;
            for (int r = 0; r < rows; r++)
            {
                float doorY = gridTop + r * (slotSize + spacing) + slotSize / 2 - doorWidth / 2;
                canvas.DrawRect(wx - 2, doorY, 4, doorWidth, doorPaint);
            }
        }

        // Vertikale Tuer-Luecken (in horizontalen Waenden)
        for (int r = 1; r < rows; r++)
        {
            float wy = gridTop + r * (slotSize + spacing) - spacing / 2;
            for (int c = 0; c < cols; c++)
            {
                float doorX = gridLeft + c * (slotSize + spacing) + slotSize / 2 - doorWidth / 2;
                canvas.DrawRect(doorX, wy - 2, doorWidth, 4, doorPaint);
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
        using var bgPaint = new SKPaint { Color = SlotFillEmpty, IsAntialias = false };
        canvas.DrawRect(x, y, w, h, bgPaint);

        // Farbiger Hinweis-Streifen am oberen Rand (zeigt welcher Raum hierhin gehoert)
        var hintColor = new SKColor(slot.HintColor);
        using var hintBarPaint = new SKPaint { Color = hintColor.WithAlpha(120), IsAntialias = false };
        canvas.DrawRect(x, y, w, 5, hintBarPaint);

        // Farbiger Punkt in der Mitte als Hinweis
        float pulse = (float)(0.5 + 0.3 * Math.Sin(_time * 2.5));
        using var hintDotPaint = new SKPaint
        {
            Color = hintColor.WithAlpha((byte)(pulse * 100)),
            IsAntialias = true
        };
        float dotRadius = Math.Min(w, h) * 0.15f;
        canvas.DrawCircle(x + w / 2, y + h / 2, dotRadius, hintDotPaint);

        // Gestrichelter Rahmen in Hint-Farbe
        using var borderPaint = new SKPaint
        {
            Color = hintColor.WithAlpha(100),
            IsAntialias = false,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, _time * 8)
        };
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, borderPaint);

        // "?" unter dem Punkt
        float qSize = Math.Min(w, h) * 0.2f;
        using var questionFont = new SKFont(SKTypeface.Default, qSize);
        using var questionPaint = new SKPaint { Color = hintColor.WithAlpha(140), IsAntialias = true };
        canvas.DrawText("?", x + w / 2, y + h / 2 + dotRadius + qSize, SKTextAlign.Center, questionFont, questionPaint);
    }

    /// <summary>
    /// Zeichnet einen gefuellten Slot mit Raumfarbe und Name.
    /// </summary>
    private void DrawFilledSlot(SKCanvas canvas, float x, float y, float w, float h, RoomSlotData slot)
    {
        // Raum-Farbe als Hintergrund
        var roomColor = slot.FilledColor != 0 ? new SKColor(slot.FilledColor) : new SKColor(slot.BackgroundColor);
        using var bgPaint = new SKPaint { Color = roomColor, IsAntialias = false };
        canvas.DrawRect(x, y, w, h, bgPaint);

        // Hellerer Innenbereich (leichte Tiefe)
        using var innerPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 35),
            IsAntialias = false
        };
        canvas.DrawRect(x + 3, y + 3, w - 6, h - 6, innerPaint);

        // Rahmen
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(slot.BorderColor),
            IsAntialias = false,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, borderPaint);

        // Raumname in der Mitte
        if (!string.IsNullOrEmpty(slot.DisplayLabel))
        {
            // Schriftgroesse an Slot-Groesse und Textlaenge anpassen
            float maxFontSize = Math.Min(w, h) * 0.22f;
            float fontSize = Math.Min(maxFontSize, w * 0.8f / Math.Max(slot.DisplayLabel.Length * 0.55f, 1));
            fontSize = Math.Max(fontSize, 8); // Minimum 8px

            using var nameFont = new SKFont(SKTypeface.Default, fontSize) { Embolden = true };
            using var namePaint = new SKPaint { Color = CheckmarkWhite, IsAntialias = true };
            canvas.DrawText(slot.DisplayLabel, x + w / 2, y + h / 2 + fontSize / 3, SKTextAlign.Center, nameFont, namePaint);
        }

        // Korrekt-Haekchen unten rechts (gruener Kreis + weisses Haekchen)
        if (slot.IsCorrect)
        {
            float checkSize = Math.Min(w, h) * 0.22f;
            float cx = x + w - checkSize - 4;
            float cy = y + h - checkSize - 4;

            // Gruener Kreis
            using var checkBgPaint = new SKPaint { Color = CorrectGreen, IsAntialias = true };
            canvas.DrawCircle(cx + checkSize / 2, cy + checkSize / 2, checkSize / 2, checkBgPaint);

            // Weisses Haekchen (vereinfacht als 2 Linien)
            using var checkPaint = new SKPaint
            {
                Color = CheckmarkWhite,
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            };
            float midX = cx + checkSize / 2;
            float midY = cy + checkSize / 2;
            canvas.DrawLine(midX - checkSize * 0.2f, midY, midX - checkSize * 0.05f, midY + checkSize * 0.2f, checkPaint);
            canvas.DrawLine(midX - checkSize * 0.05f, midY + checkSize * 0.2f, midX + checkSize * 0.25f, midY - checkSize * 0.15f, checkPaint);

            // Subtiler Glow
            using var glowPaint = new SKPaint
            {
                Color = CorrectGreen.WithAlpha(40),
                IsAntialias = true
            };
            canvas.DrawCircle(cx + checkSize / 2, cy + checkSize / 2, checkSize / 2 + 3, glowPaint);
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

        using var errorBgPaint = new SKPaint { Color = ErrorRed.WithAlpha(alpha), IsAntialias = false };
        canvas.DrawRect(x, y, w, h, errorBgPaint);

        // Basis-Slot darunter (leerer Slot Hintergrund)
        using var basePaint = new SKPaint { Color = SlotFillEmpty.WithAlpha((byte)(255 - alpha)), IsAntialias = false };
        canvas.DrawRect(x, y, w, h, basePaint);

        // Roter Rahmen
        using var borderPaint = new SKPaint
        {
            Color = ErrorRed.WithAlpha((byte)(100 + intensity * 155)),
            IsAntialias = false,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(x + 1, y + 1, w - 2, h - 2, borderPaint);

        // "X" in der Mitte
        float xSize = Math.Min(w, h) * 0.2f;
        float cx = x + w / 2;
        float cy = y + h / 2;
        using var xPaint = new SKPaint
        {
            Color = ErrorRed.WithAlpha((byte)(intensity * 255)),
            IsAntialias = false,
            StrokeWidth = 3,
            StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx - xSize, cy - xSize, cx + xSize, cy + xSize, xPaint);
        canvas.DrawLine(cx + xSize, cy - xSize, cx - xSize, cy + xSize, xPaint);

        // Farbiger Hinweis-Streifen oben (dezent durch Flash sichtbar)
        var hintColor = new SKColor(slot.HintColor);
        using var hintBarPaint = new SKPaint { Color = hintColor.WithAlpha((byte)(40 * (1 - intensity))), IsAntialias = false };
        canvas.DrawRect(x, y, w, 5, hintBarPaint);
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
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, glowAlpha),
            IsAntialias = false
        };
        canvas.DrawRect(gridLeft - 4, gridTop - 4, totalW + 8, totalH + 8, glowPaint);

        // Goldener Rahmen (pulsierend)
        byte borderAlpha = (byte)(intensity * 200);
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, borderAlpha),
            IsAntialias = false,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(gridLeft - 8, gridTop - 8, totalW + 16, totalH + 16, borderPaint);

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
}
