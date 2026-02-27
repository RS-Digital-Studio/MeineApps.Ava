using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Bauplan-Minigame.
/// Blaupausen-Atmosphaere mit technischem Grid, Schaltkreis-Ecken an den Kacheln,
/// Circuit-Verbindungslinien zwischen erledigten Schritten mit fliessenden Pulsen,
/// Completion-Burst-Partikel pro Kachel, goldene Celebration bei komplettem Bauplan,
/// Fehler-Schock mit Blitz-Effekt, Memorisierungs-Scan-Linie, Fortschrittsbalken.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// </summary>
public class BlueprintGameRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-SYSTEM (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_SPARKS = 50;
    private const int MAX_CIRCUIT_PULSES = 20;
    private const int MAX_AMBIENT = 12;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
        public bool IsGolden; // Goldene Partikel bei Komplett-Celebration
    }

    private struct CircuitPulse
    {
        public float Progress; // 0-1 entlang der Verbindungslinie
        public float Speed;
        public int FromTile, ToTile; // Index der verbundenen Kacheln
        public byte R, G, B;
        public float Life, MaxLife;
    }

    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;
    private readonly CircuitPulse[] _pulses = new CircuitPulse[MAX_CIRCUIT_PULSES];
    private int _pulseCount;

    // Atmosphaerische Hintergrund-Partikel (schwebender Blaupausen-Staub)
    private readonly SparkParticle[] _ambient = new SparkParticle[MAX_AMBIENT];
    private int _ambientCount;

    // Zustandsverfolgung fuer Effekt-Trigger
    private int _prevCompletedCount;
    private bool _prevAllComplete;
    private readonly bool[] _prevHasError = new bool[20]; // Max 20 Schritte
    private float _completionFlashTimer;
    private float _animTime;

    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN (Blaupausen-Farbschema)
    // ═══════════════════════════════════════════════════════════════════════

    // Hintergrund (Dunkelblau mit technischem Raster)
    private static readonly SKColor BlueprintBg = new(0x0D, 0x15, 0x3A);
    private static readonly SKColor GridCoarse = new(0x28, 0x35, 0x93);
    private static readonly SKColor GridFine = new(0x1A, 0x25, 0x60);
    private static readonly SKColor BlueprintBlue = new(0x42, 0xA5, 0xF5);
    private static readonly SKColor BlueprintCyan = new(0x00, 0xE5, 0xFF);
    private static readonly SKColor CompletedGreen = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor ErrorRed = new(0xF4, 0x43, 0x36);
    private static readonly SKColor GoldenYellow = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor ActiveYellow = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor TextWhite = SKColors.White;

    /// <summary>
    /// Daten-Struct fuer einen einzelnen Bauschritt (View-optimiert, keine VM-Referenz).
    /// </summary>
    public struct BlueprintStepData
    {
        public string Icon;
        public string DisplayNumber;
        public uint BackgroundColor; // ARGB
        public bool IsCompleted;
        public bool IsActive;
        public bool HasError;
        public int StepNumber; // Bau-Reihenfolge (1-basiert)
    }

    // Zwischengespeicherte Kachel-Positionen fuer HitTest und Circuit-Lines
    private SKRect[] _tileRects = Array.Empty<SKRect>();
    private SKPoint[] _tileCenters = Array.Empty<SKPoint>();

    /// <summary>
    /// Rendert das gesamte Bauplan-Spielfeld.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas zum Zeichnen.</param>
    /// <param name="bounds">Verfuegbarer Zeichenbereich.</param>
    /// <param name="steps">Bauschritt-Daten.</param>
    /// <param name="cols">Anzahl Spalten im Grid.</param>
    /// <param name="isMemorizing">Memorisierungsphase aktiv.</param>
    /// <param name="isPlaying">Spielphase aktiv.</param>
    /// <param name="completedCount">Anzahl bereits erledigter Schritte.</param>
    /// <param name="totalSteps">Gesamtanzahl Schritte.</param>
    /// <param name="deltaTime">Zeitdelta seit letztem Frame in Sekunden.</param>
    public void Render(SKCanvas canvas, SKRect bounds,
        BlueprintStepData[] steps, int cols,
        bool isMemorizing, bool isPlaying,
        int completedCount, int totalSteps,
        float deltaTime)
    {
        _animTime += deltaTime;

        // Effekt-Trigger erkennen
        DetectCompletion(steps, completedCount, totalSteps);
        DetectErrors(steps);

        // Blaupausen-Hintergrund mit technischem Grid
        DrawBlueprintBackground(canvas, bounds);

        if (steps.Length == 0 || cols <= 0) return;

        // Grid-Layout berechnen
        int rows = (int)Math.Ceiling((double)steps.Length / cols);
        float padding = 16;
        float tileSpacing = 10;

        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding - 30; // Platz fuer Progress-Bar

        float maxTileWidth = (availableWidth - (cols - 1) * tileSpacing) / cols;
        float maxTileHeight = (availableHeight - (rows - 1) * tileSpacing) / rows;
        float tileSize = Math.Min(maxTileWidth, maxTileHeight);

        // Grid zentrieren
        float gridWidth = cols * tileSize + (cols - 1) * tileSpacing;
        float gridHeight = rows * tileSize + (rows - 1) * tileSpacing;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        float startY = bounds.Top + padding + 4;

        // Kachel-Positionen speichern
        if (_tileRects.Length != steps.Length)
        {
            _tileRects = new SKRect[steps.Length];
            _tileCenters = new SKPoint[steps.Length];
        }

        for (int i = 0; i < steps.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = startX + col * (tileSize + tileSpacing);
            float y = startY + row * (tileSize + tileSpacing);
            _tileRects[i] = SKRect.Create(x, y, tileSize, tileSize);
            _tileCenters[i] = new SKPoint(x + tileSize / 2, y + tileSize / 2);
        }

        // Circuit-Verbindungslinien zwischen erledigten Schritten (unter den Kacheln)
        DrawCircuitConnections(canvas, steps);

        // Kacheln zeichnen
        for (int i = 0; i < steps.Length; i++)
        {
            DrawTile(canvas, _tileRects[i], steps[i], isMemorizing, isPlaying, tileSize);
        }

        // Memorisierungs-Scan-Linie
        if (isMemorizing)
            DrawScanLine(canvas, bounds, startY, gridHeight);

        // Partikel-Systeme aktualisieren und zeichnen
        UpdateAndDrawSparks(canvas, deltaTime);
        UpdateAndDrawCircuitPulses(canvas, steps, deltaTime);
        UpdateAndDrawAmbient(canvas, bounds, deltaTime);

        // Completion-Flash-Overlay
        if (_completionFlashTimer > 0)
            DrawCompletionFlash(canvas, bounds, deltaTime);

        // Fortschrittsanzeige unten
        DrawProgressBar(canvas, bounds, completedCount, totalSteps, startY + gridHeight + 8);
    }

    /// <summary>
    /// HitTest: Gibt den Index des getroffenen Schritts zurueck, oder -1.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int totalSteps)
    {
        for (int i = 0; i < _tileRects.Length && i < totalSteps; i++)
        {
            if (_tileRects[i].Contains(touchX, touchY))
                return i;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EFFEKT-TRIGGER-ERKENNUNG
    // ═══════════════════════════════════════════════════════════════════════

    private void DetectCompletion(BlueprintStepData[] steps, int completedCount, int totalSteps)
    {
        // Einzelner Schritt erledigt → blaue Burst-Partikel
        if (completedCount > _prevCompletedCount)
        {
            int newlyCompleted = completedCount - _prevCompletedCount;
            for (int i = 0; i < steps.Length && newlyCompleted > 0; i++)
            {
                if (steps[i].IsCompleted && i < _tileRects.Length)
                {
                    // Pruefen ob dieser Schritt gerade erst erledigt wurde
                    if (steps[i].StepNumber == completedCount)
                    {
                        SpawnCompletionBurst(_tileRects[i]);
                        SpawnCircuitPulse(steps, i);
                        newlyCompleted--;
                    }
                }
            }
        }

        // Alle Schritte komplett → goldene Celebration
        bool allComplete = completedCount >= totalSteps && totalSteps > 0;
        if (allComplete && !_prevAllComplete)
        {
            _completionFlashTimer = 1.5f;
            SpawnCelebrationSparks(steps);
        }

        _prevCompletedCount = completedCount;
        _prevAllComplete = allComplete;
    }

    private void DetectErrors(BlueprintStepData[] steps)
    {
        for (int i = 0; i < steps.Length && i < _prevHasError.Length; i++)
        {
            if (steps[i].HasError && !_prevHasError[i])
            {
                // Neuer Fehler → Schock-Partikel
                if (i < _tileRects.Length)
                    SpawnErrorShock(_tileRects[i]);
            }
            _prevHasError[i] = steps[i].HasError;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-SPAWNER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 10 blaue Burst-Partikel bei erledigtem Schritt.
    /// </summary>
    private void SpawnCompletionBurst(SKRect tileRect)
    {
        var rng = Random.Shared;
        float cx = tileRect.MidX;
        float cy = tileRect.MidY;

        for (int i = 0; i < 10 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = rng.NextSingle() * MathF.PI * 2;
            float speed = 40 + rng.NextSingle() * 80;
            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx, Y = cy,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Life = 0, MaxLife = 0.6f + rng.NextSingle() * 0.4f,
                Size = 2f + rng.NextSingle() * 3f,
                R = 0x42, G = 0xA5, B = 0xF5 // Blaupausen-Blau
            };
        }
    }

    /// <summary>
    /// Goldene Funken von allen Kacheln bei komplettem Bauplan.
    /// </summary>
    private void SpawnCelebrationSparks(BlueprintStepData[] steps)
    {
        var rng = Random.Shared;
        for (int t = 0; t < steps.Length && t < _tileRects.Length; t++)
        {
            float cx = _tileCenters[t].X;
            float cy = _tileCenters[t].Y;

            for (int i = 0; i < 4 && _sparkCount < MAX_SPARKS; i++)
            {
                float angle = rng.NextSingle() * MathF.PI * 2;
                float speed = 30 + rng.NextSingle() * 60;
                _sparks[_sparkCount++] = new SparkParticle
                {
                    X = cx, Y = cy,
                    VelocityX = MathF.Cos(angle) * speed,
                    VelocityY = MathF.Sin(angle) * speed - 20,
                    Life = 0, MaxLife = 0.8f + rng.NextSingle() * 0.6f,
                    Size = 2.5f + rng.NextSingle() * 3f,
                    R = 0xFF, G = 0xD7, B = 0x00,
                    IsGolden = true
                };
            }
        }
    }

    /// <summary>
    /// Rote Schock-Partikel bei falschem Tipp.
    /// </summary>
    private void SpawnErrorShock(SKRect tileRect)
    {
        var rng = Random.Shared;
        float cx = tileRect.MidX;
        float cy = tileRect.MidY;

        for (int i = 0; i < 8 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = rng.NextSingle() * MathF.PI * 2;
            float speed = 30 + rng.NextSingle() * 50;
            _sparks[_sparkCount++] = new SparkParticle
            {
                X = cx + (rng.NextSingle() - 0.5f) * tileRect.Width * 0.5f,
                Y = cy + (rng.NextSingle() - 0.5f) * tileRect.Height * 0.5f,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Life = 0, MaxLife = 0.3f + rng.NextSingle() * 0.3f,
                Size = 1.5f + rng.NextSingle() * 2.5f,
                R = 0xF4, G = 0x43, B = 0x36 // Rot
            };
        }
    }

    /// <summary>
    /// Cyan-Puls entlang der Verbindungslinie zum vorherigen erledigten Schritt.
    /// </summary>
    private void SpawnCircuitPulse(BlueprintStepData[] steps, int completedTileIndex)
    {
        int currentStep = steps[completedTileIndex].StepNumber;
        if (currentStep <= 1) return;

        int prevStep = currentStep - 1;
        int prevTileIndex = -1;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].StepNumber == prevStep && steps[i].IsCompleted)
            {
                prevTileIndex = i;
                break;
            }
        }

        if (prevTileIndex < 0 || _pulseCount >= MAX_CIRCUIT_PULSES) return;

        _pulses[_pulseCount++] = new CircuitPulse
        {
            Progress = 0, Speed = 1.2f,
            FromTile = prevTileIndex, ToTile = completedTileIndex,
            R = 0x00, G = 0xE5, B = 0xFF, // Cyan
            Life = 0, MaxLife = 1.5f
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den Blaupausen-Hintergrund mit Rasterlinien und Vignette.
    /// </summary>
    private void DrawBlueprintBackground(SKCanvas canvas, SKRect bounds)
    {
        // Dunkelblauer Hintergrund mit radialer Vignette
        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                Math.Max(bounds.Width, bounds.Height) * 0.7f,
                new[] { new SKColor(0x12, 0x1C, 0x50), BlueprintBg },
                null, SKShaderTileMode.Clamp),
            IsAntialias = false
        };
        canvas.DrawRect(bounds, bgPaint);

        // Feines Raster (20px Abstand)
        using var fineGridPaint = new SKPaint
        {
            Color = GridFine, IsAntialias = false,
            StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke
        };
        for (float x = bounds.Left; x < bounds.Right; x += 20)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, fineGridPaint);
        for (float y = bounds.Top; y < bounds.Bottom; y += 20)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, fineGridPaint);

        // Grobes Raster (80px Abstand)
        using var coarseGridPaint = new SKPaint
        {
            Color = GridCoarse, IsAntialias = false,
            StrokeWidth = 1f, Style = SKPaintStyle.Stroke
        };
        for (float x = bounds.Left; x < bounds.Right; x += 80)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, coarseGridPaint);
        for (float y = bounds.Top; y < bounds.Bottom; y += 80)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, coarseGridPaint);

        // Pulsierende Kreuzungspunkte (technische Atmosphaere)
        float pulse = 0.3f + 0.7f * (0.5f + 0.5f * MathF.Sin(_animTime * 1.5f));
        using var dotPaint = new SKPaint
        {
            Color = BlueprintBlue.WithAlpha((byte)(20 * pulse)),
            IsAntialias = true
        };
        for (float x = bounds.Left; x < bounds.Right; x += 80)
        {
            for (float y = bounds.Top; y < bounds.Bottom; y += 80)
            {
                canvas.DrawCircle(x, y, 2.5f, dotPaint);
            }
        }

        // L-foermige Eck-Markierungen (Blaupausen-Stil)
        using var cornerPaint = new SKPaint
        {
            Color = BlueprintBlue.WithAlpha(40),
            IsAntialias = true, StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke
        };
        float cs = 25;
        // Oben links
        canvas.DrawLine(bounds.Left + 4, bounds.Top + 4, bounds.Left + 4 + cs, bounds.Top + 4, cornerPaint);
        canvas.DrawLine(bounds.Left + 4, bounds.Top + 4, bounds.Left + 4, bounds.Top + 4 + cs, cornerPaint);
        // Oben rechts
        canvas.DrawLine(bounds.Right - 4, bounds.Top + 4, bounds.Right - 4 - cs, bounds.Top + 4, cornerPaint);
        canvas.DrawLine(bounds.Right - 4, bounds.Top + 4, bounds.Right - 4, bounds.Top + 4 + cs, cornerPaint);
        // Unten links
        canvas.DrawLine(bounds.Left + 4, bounds.Bottom - 4, bounds.Left + 4 + cs, bounds.Bottom - 4, cornerPaint);
        canvas.DrawLine(bounds.Left + 4, bounds.Bottom - 4, bounds.Left + 4, bounds.Bottom - 4 - cs, cornerPaint);
        // Unten rechts
        canvas.DrawLine(bounds.Right - 4, bounds.Bottom - 4, bounds.Right - 4 - cs, bounds.Bottom - 4, cornerPaint);
        canvas.DrawLine(bounds.Right - 4, bounds.Bottom - 4, bounds.Right - 4, bounds.Bottom - 4 - cs, cornerPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCHALTKREIS-VERBINDUNGSLINIEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet gestrichelte animierte Verbindungslinien zwischen erledigten Schritten
    /// (sortiert nach StepNumber) mit blauem Glow.
    /// </summary>
    private void DrawCircuitConnections(SKCanvas canvas, BlueprintStepData[] steps)
    {
        // Sammle erledigte Schritte mit ihren Indizes
        Span<(int index, int step)> completed = stackalloc (int, int)[Math.Min(steps.Length, 20)];
        int count = 0;

        for (int i = 0; i < steps.Length && count < completed.Length; i++)
        {
            if (steps[i].IsCompleted)
                completed[count++] = (i, steps[i].StepNumber);
        }

        if (count < 2) return;

        // Einfache Insertion-Sort nach StepNumber
        for (int i = 1; i < count; i++)
        {
            var key = completed[i];
            int j = i - 1;
            while (j >= 0 && completed[j].step > key.step)
            {
                completed[j + 1] = completed[j];
                j--;
            }
            completed[j + 1] = key;
        }

        // Glow-Linie (breit, unscharf)
        using var glowPaint = new SKPaint
        {
            Color = BlueprintBlue.WithAlpha(20),
            IsAntialias = true,
            StrokeWidth = 6f,
            Style = SKPaintStyle.Stroke,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };

        // Gestrichelte animierte Verbindungslinie
        using var linePaint = new SKPaint
        {
            Color = BlueprintBlue.WithAlpha(60),
            IsAntialias = true,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, _animTime * 30)
        };

        for (int i = 0; i < count - 1; i++)
        {
            int fromIdx = completed[i].index;
            int toIdx = completed[i + 1].index;

            if (fromIdx >= _tileCenters.Length || toIdx >= _tileCenters.Length) continue;

            var from = _tileCenters[fromIdx];
            var to = _tileCenters[toIdx];

            // Glow
            canvas.DrawLine(from, to, glowPaint);
            // Gestrichelte Linie
            canvas.DrawLine(from, to, linePaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KACHELN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet eine einzelne Kachel mit Icon, Nummer, Zustandsanzeige und Effekten.
    /// </summary>
    private void DrawTile(SKCanvas canvas, SKRect rect, BlueprintStepData step,
        bool isMemorizing, bool isPlaying, float tileSize)
    {
        float cr = 10;

        // Hintergrundfarbe extrahieren
        var bgColor = new SKColor(
            (byte)((step.BackgroundColor >> 16) & 0xFF),
            (byte)((step.BackgroundColor >> 8) & 0xFF),
            (byte)(step.BackgroundColor & 0xFF),
            (byte)((step.BackgroundColor >> 24) & 0xFF)
        );

        // Kachel-Schatten (Tiefe)
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 60),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
        };
        var shadowRect = SKRect.Create(rect.Left + 2, rect.Top + 3, rect.Width, rect.Height);
        canvas.DrawRoundRect(shadowRect, cr, cr, shadowPaint);

        // Kachel-Hintergrund mit Gradient
        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Bottom),
                new[] { bgColor, bgColor.WithAlpha((byte)(bgColor.Alpha * 0.7f)) },
                null, SKShaderTileMode.Clamp),
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, cr, cr, bgPaint);

        // Oberer Highlight-Streifen (Glaseffekt)
        using var highlightPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Top + rect.Height * 0.4f),
                new[] { new SKColor(0xFF, 0xFF, 0xFF, 0x18), SKColors.Transparent },
                null, SKShaderTileMode.Clamp),
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, cr, cr, highlightPaint);

        // Rahmen basierend auf Zustand
        DrawTileBorder(canvas, rect, step, cr);

        // Icon (Vektor-Icon oben in der Kachel)
        DrawTileIcon(canvas, rect, step.Icon);

        // Nummer oder Fragezeichen (unten in der Kachel)
        DrawTileNumber(canvas, rect, step.DisplayNumber, isMemorizing, step.IsCompleted);

        // Haekchen bei abgeschlossenen Schritten (oben rechts)
        if (step.IsCompleted)
            DrawCheckmark(canvas, rect);

        // Step-Number Badge (kleine Nummer oben links bei erledigten)
        if (step.IsCompleted && step.StepNumber > 0)
            DrawStepBadge(canvas, rect, step.StepNumber);

        // Fehler-Overlay (roter Puls + Zickzack-Blitz)
        if (step.HasError)
            DrawErrorOverlay(canvas, rect, cr);
    }

    /// <summary>
    /// Zeichnet den Kachel-Rahmen je nach Zustand mit Schaltkreis-Ecken.
    /// </summary>
    private void DrawTileBorder(SKCanvas canvas, SKRect rect, BlueprintStepData step, float cr)
    {
        if (step.IsActive && !step.IsCompleted)
        {
            // Pulsierender gelber Rand fuer aktiven Schritt
            float pulse = 0.5f + 0.5f * MathF.Sin(_animTime * 6);
            byte alpha = (byte)(160 + 95 * pulse);

            // Aeusserer Glow
            using var outerGlow = new SKPaint
            {
                Color = ActiveYellow.WithAlpha((byte)(50 * pulse)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 8,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
            };
            var glowRect = SKRect.Create(rect.Left - 3, rect.Top - 3, rect.Width + 6, rect.Height + 6);
            canvas.DrawRoundRect(glowRect, cr + 3, cr + 3, outerGlow);

            // Innerer Neon-Rand
            using var borderPaint = new SKPaint
            {
                Color = ActiveYellow.WithAlpha(alpha),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);

            // Schaltkreis-Ecken
            DrawCircuitCorners(canvas, rect, ActiveYellow.WithAlpha(alpha), cr);
        }
        else if (step.IsCompleted)
        {
            // Gruener Rahmen mit leichtem Glow
            using var glowPaint = new SKPaint
            {
                Color = CompletedGreen.WithAlpha(30),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
            };
            canvas.DrawRoundRect(rect, cr, cr, glowPaint);

            using var borderPaint = new SKPaint
            {
                Color = CompletedGreen,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);
        }
        else if (step.HasError)
        {
            // Roter Fehler-Rand (pulsierend)
            float errPulse = 0.6f + 0.4f * MathF.Sin(_animTime * 12);
            using var borderPaint = new SKPaint
            {
                Color = ErrorRed.WithAlpha((byte)(200 * errPulse)),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);
        }
        else
        {
            // Standard-Rahmen (dezent, technischer Stil)
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 0x25),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);

            // Dezente Schaltkreis-Ecken
            DrawCircuitCorners(canvas, rect, new SKColor(0xFF, 0xFF, 0xFF, 0x15), cr);
        }
    }

    /// <summary>
    /// Zeichnet L-foermige Schaltkreis-Markierungen an den Kachel-Ecken.
    /// </summary>
    private static void DrawCircuitCorners(SKCanvas canvas, SKRect rect, SKColor color, float cr)
    {
        float len = Math.Min(rect.Width, rect.Height) * 0.18f;
        using var paint = new SKPaint
        {
            Color = color, IsAntialias = true,
            StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round
        };

        float inset = cr * 0.3f;
        // Oben links
        canvas.DrawLine(rect.Left + inset, rect.Top + inset + len, rect.Left + inset, rect.Top + inset, paint);
        canvas.DrawLine(rect.Left + inset, rect.Top + inset, rect.Left + inset + len, rect.Top + inset, paint);
        // Oben rechts
        canvas.DrawLine(rect.Right - inset - len, rect.Top + inset, rect.Right - inset, rect.Top + inset, paint);
        canvas.DrawLine(rect.Right - inset, rect.Top + inset, rect.Right - inset, rect.Top + inset + len, paint);
        // Unten links
        canvas.DrawLine(rect.Left + inset, rect.Bottom - inset - len, rect.Left + inset, rect.Bottom - inset, paint);
        canvas.DrawLine(rect.Left + inset, rect.Bottom - inset, rect.Left + inset + len, rect.Bottom - inset, paint);
        // Unten rechts
        canvas.DrawLine(rect.Right - inset - len, rect.Bottom - inset, rect.Right - inset, rect.Bottom - inset, paint);
        canvas.DrawLine(rect.Right - inset, rect.Bottom - inset, rect.Right - inset, rect.Bottom - inset - len, paint);
    }

    /// <summary>
    /// Zeichnet den Step-Number-Badge oben links (kleine Zahl im Kreis).
    /// </summary>
    private static void DrawStepBadge(SKCanvas canvas, SKRect rect, int step)
    {
        float badgeR = 9;
        float bx = rect.Left + badgeR + 4;
        float by = rect.Top + badgeR + 4;

        using var bgPaint = new SKPaint
        {
            Color = CompletedGreen.WithAlpha(200),
            IsAntialias = true
        };
        canvas.DrawCircle(bx, by, badgeR, bgPaint);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = 11,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        canvas.DrawText(step.ToString(), bx, by + 4, textPaint);
    }

    /// <summary>
    /// Fehler-Overlay: Rotes Pulsieren + Zickzack-Blitz in der Kachelmitte.
    /// </summary>
    private void DrawErrorOverlay(SKCanvas canvas, SKRect rect, float cr)
    {
        float errPulse = 0.4f + 0.6f * MathF.Sin(_animTime * 15);

        // Rotes pulsierendes Overlay
        using var overlayPaint = new SKPaint
        {
            Color = ErrorRed.WithAlpha((byte)(80 * errPulse)),
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, cr, cr, overlayPaint);

        // Kleiner Zickzack-Blitz in der Mitte
        float cx = rect.MidX;
        float cy = rect.MidY;
        float boltH = rect.Height * 0.3f;

        using var boltPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0x80, 0x80, (byte)(200 * errPulse)),
            IsAntialias = true,
            StrokeWidth = 2.5f,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round
        };
        using var boltPath = new SKPath();
        boltPath.MoveTo(cx - boltH * 0.15f, cy - boltH * 0.5f);
        boltPath.LineTo(cx + boltH * 0.1f, cy - boltH * 0.05f);
        boltPath.LineTo(cx - boltH * 0.1f, cy + boltH * 0.05f);
        boltPath.LineTo(cx + boltH * 0.15f, cy + boltH * 0.5f);
        canvas.DrawPath(boltPath, boltPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEMORISIERUNGS-SCAN-LINIE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Horizontale leuchtende blaue Linie die ueber das Grid faehrt.
    /// </summary>
    private void DrawScanLine(SKCanvas canvas, SKRect bounds, float gridTop, float gridHeight)
    {
        float period = 2.0f; // 2 Sekunden pro Durchgang
        float progress = (_animTime % period) / period;
        float scanY = gridTop + progress * gridHeight;

        using var scanPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, scanY - 8),
                new SKPoint(bounds.Left, scanY + 8),
                new[] { SKColors.Transparent, BlueprintCyan.WithAlpha(60), BlueprintCyan.WithAlpha(100),
                        BlueprintCyan.WithAlpha(60), SKColors.Transparent },
                new[] { 0f, 0.3f, 0.5f, 0.7f, 1f },
                SKShaderTileMode.Clamp),
            IsAntialias = false
        };
        canvas.DrawRect(bounds.Left, scanY - 8, bounds.Width, 16, scanPaint);

        // Heller Kern
        using var corePaint = new SKPaint
        {
            Color = BlueprintCyan.WithAlpha(150),
            IsAntialias = false,
            StrokeWidth = 1
        };
        canvas.DrawLine(bounds.Left + 20, scanY, bounds.Right - 20, scanY, corePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETION-FLASH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Goldener Flash-Overlay bei komplettem Bauplan.
    /// </summary>
    private void DrawCompletionFlash(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        _completionFlashTimer -= deltaTime;
        float t = Math.Clamp(_completionFlashTimer / 1.5f, 0, 1);

        // Goldener Glow vom Zentrum
        byte alpha;
        if (t > 0.7f)
            alpha = (byte)(180 * ((t - 0.7f) / 0.3f)); // Schnelles Aufblitzen
        else
            alpha = (byte)(180 * t / 0.7f * 0.5f); // Langsames Ausblenden

        using var flashPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                Math.Max(bounds.Width, bounds.Height) * 0.5f,
                new[] { GoldenYellow.WithAlpha(alpha), SKColors.Transparent },
                null, SKShaderTileMode.Clamp),
            IsAntialias = false
        };
        canvas.DrawRect(bounds, flashPaint);

        // Goldener Rahmen
        if (t > 0.5f)
        {
            byte borderAlpha = (byte)(200 * ((t - 0.5f) / 0.5f));
            using var borderPaint = new SKPaint
            {
                Color = GoldenYellow.WithAlpha(borderAlpha),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
            };
            canvas.DrawRoundRect(new SKRect(bounds.Left + 4, bounds.Top + 4,
                bounds.Right - 4, bounds.Bottom - 4), 12, 12, borderPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITTSANZEIGE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fortschrittsbalken unten unter dem Grid.
    /// </summary>
    private void DrawProgressBar(SKCanvas canvas, SKRect bounds,
        int completed, int total, float y)
    {
        if (total <= 0) return;

        float barWidth = bounds.Width * 0.6f;
        float barHeight = 6;
        float barX = bounds.Left + (bounds.Width - barWidth) / 2;

        // Hintergrund
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x15),
            IsAntialias = true
        };
        canvas.DrawRoundRect(barX, y, barWidth, barHeight, 3, 3, bgPaint);

        // Fortschritt
        float progress = (float)completed / total;
        if (progress > 0)
        {
            float fillWidth = barWidth * progress;

            // Farbverlauf: Blau → Cyan → Gruen
            SKColor fillColor;
            if (progress < 0.5f)
                fillColor = BlueprintBlue;
            else if (progress < 1f)
                fillColor = BlueprintCyan;
            else
                fillColor = CompletedGreen;

            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true
            };
            canvas.DrawRoundRect(barX, y, fillWidth, barHeight, 3, 3, fillPaint);

            // Glow am Ende des Balkens
            if (progress > 0 && progress < 1)
            {
                using var glowPaint = new SKPaint
                {
                    Color = fillColor.WithAlpha(80),
                    IsAntialias = true,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
                };
                canvas.DrawCircle(barX + fillWidth, y + barHeight / 2, 5, glowPaint);
            }
        }

        // Text "X/Y"
        using var textPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x90),
            IsAntialias = true,
            TextSize = 10,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText($"{completed}/{total}", bounds.MidX, y + barHeight + 12, textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-UPDATE UND -RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert und zeichnet Spark-Partikel (Completion/Error/Celebration).
    /// Kompaktierung: Letztes Element an Stelle des geloeschten.
    /// </summary>
    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        using var paint = new SKPaint { IsAntialias = true };

        for (int i = _sparkCount - 1; i >= 0; i--)
        {
            ref var p = ref _sparks[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Leichte Verlangsamung
            p.VelocityX *= 0.97f;
            p.VelocityY *= 0.97f;

            if (p.Life >= p.MaxLife)
            {
                // Kompaktierung: Letztes Element an diese Stelle
                _sparks[i] = _sparks[--_sparkCount];
                continue;
            }

            float lifeRatio = p.Life / p.MaxLife;
            float alpha = lifeRatio < 0.2f
                ? lifeRatio / 0.2f
                : 1f - (lifeRatio - 0.2f) / 0.8f;

            float size = p.Size * (1f - lifeRatio * 0.5f);

            if (p.IsGolden)
            {
                // Goldene Partikel mit Shimmer
                float shimmer = 0.7f + 0.3f * MathF.Sin(_animTime * 10 + i * 2);
                paint.Color = new SKColor(p.R, p.G, p.B, (byte)(220 * alpha * shimmer));
            }
            else
            {
                paint.Color = new SKColor(p.R, p.G, p.B, (byte)(200 * alpha));
            }

            canvas.DrawCircle(p.X, p.Y, size, paint);
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet Circuit-Pulse (fliessende Cyan-Punkte entlang der Linien).
    /// </summary>
    private void UpdateAndDrawCircuitPulses(SKCanvas canvas, BlueprintStepData[] steps, float deltaTime)
    {
        using var paint = new SKPaint { IsAntialias = true };

        for (int i = _pulseCount - 1; i >= 0; i--)
        {
            ref var pulse = ref _pulses[i];
            pulse.Life += deltaTime;
            pulse.Progress += pulse.Speed * deltaTime;

            if (pulse.Life >= pulse.MaxLife || pulse.Progress > 1.5f)
            {
                _pulses[i] = _pulses[--_pulseCount];
                continue;
            }

            if (pulse.FromTile >= _tileCenters.Length || pulse.ToTile >= _tileCenters.Length)
                continue;

            var from = _tileCenters[pulse.FromTile];
            var to = _tileCenters[pulse.ToTile];

            float t = Math.Clamp(pulse.Progress, 0, 1);
            float px = from.X + (to.X - from.X) * t;
            float py = from.Y + (to.Y - from.Y) * t;

            float lifeAlpha = 1f - pulse.Life / pulse.MaxLife;

            // Glow
            paint.Color = new SKColor(pulse.R, pulse.G, pulse.B, (byte)(40 * lifeAlpha));
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
            canvas.DrawCircle(px, py, 6, paint);

            // Kern
            paint.MaskFilter = null;
            paint.Color = new SKColor(pulse.R, pulse.G, pulse.B, (byte)(200 * lifeAlpha));
            canvas.DrawCircle(px, py, 3, paint);
        }
    }

    /// <summary>
    /// Aktualisiert und zeichnet atmosphaerische Blaupausen-Staub-Partikel.
    /// Struct-basiertes Array statt List (kein GC-Druck).
    /// </summary>
    private void UpdateAndDrawAmbient(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        var rng = Random.Shared;

        // Neue Partikel erzeugen
        while (_ambientCount < MAX_AMBIENT)
        {
            _ambient[_ambientCount++] = new SparkParticle
            {
                X = bounds.Left + rng.NextSingle() * bounds.Width,
                Y = bounds.Top + rng.NextSingle() * bounds.Height,
                VelocityX = (rng.NextSingle() - 0.5f) * 15,
                VelocityY = -5 - rng.NextSingle() * 10,
                Life = 0,
                MaxLife = 3f + rng.NextSingle() * 4f,
                Size = 1 + rng.NextSingle() * 2,
                R = (byte)(200 + rng.Next(0, 56)),
                G = (byte)(210 + rng.Next(0, 46)),
                B = 255
            };
        }

        using var paint = new SKPaint { IsAntialias = false };

        for (int i = _ambientCount - 1; i >= 0; i--)
        {
            ref var p = ref _ambient[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Leichtes horizontales Driften (Sinus)
            p.X += MathF.Sin(p.Life * 1.5f + i) * 3 * deltaTime;

            if (p.Life >= p.MaxLife || p.Y < bounds.Top - 10 ||
                p.X < bounds.Left - 10 || p.X > bounds.Right + 10)
            {
                // Kompaktierung
                _ambient[i] = _ambient[--_ambientCount];
                continue;
            }

            // Alpha: Einblenden -> Halten -> Ausblenden
            float lifeRatio = p.Life / p.MaxLife;
            float alpha;
            if (lifeRatio < 0.2f)
                alpha = lifeRatio / 0.2f;
            else if (lifeRatio > 0.7f)
                alpha = (1 - lifeRatio) / 0.3f;
            else
                alpha = 1;

            paint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 60));
            canvas.DrawCircle(p.X, p.Y, p.Size, paint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KACHEL-ICONS (12 Vektor-Icons, unveraendert)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet ein Vektor-Icon in der oberen Haelfte der Kachel basierend auf dem Icon-Identifier.
    /// </summary>
    private static void DrawTileIcon(SKCanvas canvas, SKRect rect, string iconId)
    {
        if (string.IsNullOrEmpty(iconId)) return;

        float iconSize = rect.Height * 0.32f;
        float cx = rect.MidX;
        float cy = rect.Top + rect.Height * 0.38f;

        switch (iconId)
        {
            case "foundation": DrawFoundationIcon(canvas, cx, cy, iconSize); break;
            case "walls": DrawWallsIcon(canvas, cx, cy, iconSize); break;
            case "framework": DrawFrameworkIcon(canvas, cx, cy, iconSize); break;
            case "electrics": DrawElectricsIcon(canvas, cx, cy, iconSize); break;
            case "plumbing": DrawPlumbingIcon(canvas, cx, cy, iconSize); break;
            case "windows": DrawWindowsIcon(canvas, cx, cy, iconSize); break;
            case "doors": DrawDoorsIcon(canvas, cx, cy, iconSize); break;
            case "painting": DrawPaintingIcon(canvas, cx, cy, iconSize); break;
            case "roof": DrawRoofIcon(canvas, cx, cy, iconSize); break;
            case "fittings": DrawFittingsIcon(canvas, cx, cy, iconSize); break;
            case "measuring": DrawMeasuringIcon(canvas, cx, cy, iconSize); break;
            case "scaffolding": DrawScaffoldingIcon(canvas, cx, cy, iconSize); break;
            default: DrawDefaultIcon(canvas, cx, cy, iconSize); break;
        }
    }

    /// <summary>Fundament: Trapez mit horizontalen Streifen.</summary>
    private static void DrawFoundationIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Trapez-Fundament (unten breiter als oben)
        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x90, 0xA4, 0xAE) };
        using var path = new SKPath();
        path.MoveTo(cx - half * 0.6f, cy - half);      // Oben links
        path.LineTo(cx + half * 0.6f, cy - half);      // Oben rechts
        path.LineTo(cx + half, cy + half);              // Unten rechts
        path.LineTo(cx - half, cy + half);              // Unten links
        path.Close();
        canvas.DrawPath(path, fillPaint);

        // 3 horizontale Streifen
        using var linePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x60, 0x7D, 0x8B), StrokeWidth = 1.5f
        };
        for (int i = 0; i < 3; i++)
        {
            float yOff = cy - half + (i + 1) * (size / 4);
            float ratio = (yOff - (cy - half)) / size; // 0..1 von oben nach unten
            float w = half * (0.6f + 0.4f * ratio);    // Breite interpolieren
            canvas.DrawLine(cx - w, yOff, cx + w, yOff, linePaint);
        }
    }

    /// <summary>Mauern: 3x2 Ziegelsteinmuster.</summary>
    private static void DrawWallsIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float brickW = size / 3;
        float brickH = size / 2.5f;

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE5, 0x73, 0x73) };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xD4, 0xC5, 0xA9), StrokeWidth = 1.5f
        };

        // 2 Reihen Ziegel (versetzt)
        for (int row = 0; row < 2; row++)
        {
            float y = cy - half + row * brickH;
            float xOffset = (row % 2 == 1) ? brickW * 0.5f : 0;
            for (int col = 0; col < 3; col++)
            {
                float x = cx - half + col * brickW + xOffset;
                var r = SKRect.Create(x + 1, y + 1, brickW - 2, brickH - 2);
                canvas.DrawRoundRect(r, 1, 1, fillPaint);
                canvas.DrawRoundRect(r, 1, 1, borderPaint);
            }
        }
    }

    /// <summary>Rahmenwerk: Holzrahmen als H-Form.</summary>
    private static void DrawFrameworkIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xA1, 0x88, 0x7F), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };

        // Zwei vertikale Balken
        canvas.DrawLine(cx - half * 0.6f, cy - half, cx - half * 0.6f, cy + half, paint);
        canvas.DrawLine(cx + half * 0.6f, cy - half, cx + half * 0.6f, cy + half, paint);

        // Querbalken in der Mitte
        canvas.DrawLine(cx - half * 0.6f, cy, cx + half * 0.6f, cy, paint);

        // Diagonalstrebe
        using var diagPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x8D, 0x6E, 0x63), StrokeWidth = 2, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx - half * 0.6f, cy - half, cx + half * 0.6f, cy, diagPaint);
    }

    /// <summary>Elektrik: Blitzsymbol (Zickzack).</summary>
    private static void DrawElectricsIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var fillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xD5, 0x4F) };
        using var strokePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xA0, 0x00), StrokeWidth = 1.5f
        };

        using var path = new SKPath();
        path.MoveTo(cx + half * 0.1f, cy - half);
        path.LineTo(cx - half * 0.4f, cy - half * 0.05f);
        path.LineTo(cx + half * 0.15f, cy + half * 0.05f);
        path.LineTo(cx - half * 0.15f, cy + half);
        path.LineTo(cx + half * 0.5f, cy - half * 0.15f);
        path.LineTo(cx - half * 0.05f, cy - half * 0.1f);
        path.Close();

        canvas.DrawPath(path, fillPaint);
        canvas.DrawPath(path, strokePaint);
    }

    /// <summary>Sanitaer: Schraubenschluessel.</summary>
    private static void DrawPlumbingIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Griff (vertikale Linie)
        using var handlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx, cy - half * 0.1f, cx, cy + half, handlePaint);

        // Maulschluessel-Kopf (U-Form)
        using var headPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xBE, 0xC5), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        using var headPath = new SKPath();
        headPath.MoveTo(cx - half * 0.5f, cy - half * 0.1f);
        headPath.LineTo(cx - half * 0.5f, cy - half * 0.7f);
        headPath.ArcTo(
            SKRect.Create(cx - half * 0.5f, cy - half, half, half * 0.6f),
            180, -180, false);
        headPath.LineTo(cx + half * 0.5f, cy - half * 0.1f);
        canvas.DrawPath(headPath, headPaint);
    }

    /// <summary>Fenster: Blaues Rechteck mit weissem Kreuzrahmen.</summary>
    private static void DrawWindowsIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        var r = SKRect.Create(cx - half * 0.8f, cy - half * 0.8f, size * 0.8f, size * 0.8f);

        // Blaues Glas
        using var glassPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x42, 0xA5, 0xF5) };
        canvas.DrawRoundRect(r, 3, 3, glassPaint);

        // Weisser Rahmen
        using var framePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = SKColors.White, StrokeWidth = 2
        };
        canvas.DrawRoundRect(r, 3, 3, framePaint);

        // Kreuz
        canvas.DrawLine(r.MidX, r.Top, r.MidX, r.Bottom, framePaint);
        canvas.DrawLine(r.Left, r.MidY, r.Right, r.MidY, framePaint);
    }

    /// <summary>Tuer: Braunes Rechteck mit kleinem Griff-Kreis.</summary>
    private static void DrawDoorsIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        var r = SKRect.Create(cx - half * 0.6f, cy - half, size * 0.6f, size);

        // Tuerblatt
        using var doorPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x8D, 0x6E, 0x63) };
        canvas.DrawRoundRect(r, 3, 3, doorPaint);

        // Rahmen
        using var framePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x6D, 0x4C, 0x41), StrokeWidth = 2
        };
        canvas.DrawRoundRect(r, 3, 3, framePaint);

        // Tuergriff (kleiner Kreis rechts)
        using var knobPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xD5, 0x4F) };
        canvas.DrawCircle(r.Right - half * 0.25f, r.MidY + half * 0.1f, half * 0.12f, knobPaint);
    }

    /// <summary>Malerei: Farbroller mit Stiel.</summary>
    private static void DrawPaintingIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Stiel (diagonal)
        using var handlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x9E, 0x9E, 0x9E), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx + half * 0.1f, cy + half * 0.1f, cx + half * 0.1f, cy + half, handlePaint);

        // Roller-Halterung
        canvas.DrawLine(cx + half * 0.1f, cy + half * 0.1f, cx - half * 0.3f, cy - half * 0.1f, handlePaint);

        // Farbroller (Rechteck)
        using var rollerPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xEF, 0x6C, 0x00) };
        var rollerRect = SKRect.Create(cx - half * 0.8f, cy - half * 0.6f, size * 0.7f, half * 0.6f);
        canvas.DrawRoundRect(rollerRect, 4, 4, rollerPaint);

        // Roller-Textur (helle Streifen)
        using var texturePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0x9E, 0x40, 0x80), StrokeWidth = 1
        };
        for (float lx = rollerRect.Left + 3; lx < rollerRect.Right - 2; lx += 4)
        {
            canvas.DrawLine(lx, rollerRect.Top + 2, lx, rollerRect.Bottom - 2, texturePaint);
        }
    }

    /// <summary>Dach: Rotes Dreieck mit Schornstein.</summary>
    private static void DrawRoofIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        // Dach-Dreieck
        using var roofPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE5, 0x39, 0x35) };
        using var path = new SKPath();
        path.MoveTo(cx, cy - half);
        path.LineTo(cx + half, cy + half * 0.5f);
        path.LineTo(cx - half, cy + half * 0.5f);
        path.Close();
        canvas.DrawPath(path, roofPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xC6, 0x28, 0x28), StrokeWidth = 1.5f
        };
        canvas.DrawPath(path, borderPaint);

        // Schornstein (kleines Rechteck oben rechts)
        using var chimneyPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x79, 0x55, 0x48) };
        var chimneyRect = SKRect.Create(cx + half * 0.25f, cy - half * 0.7f, half * 0.25f, half * 0.55f);
        canvas.DrawRect(chimneyRect, chimneyPaint);
    }

    /// <summary>Beschlaege: Schraube mit Kreuzschlitz.</summary>
    private static void DrawFittingsIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float radius = half * 0.7f;

        // Schraubenkopf (Kreis)
        using var headPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawCircle(cx, cy, radius, headPaint);

        // Rand
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 2
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        // Kreuzschlitz
        using var slotPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 2, StrokeCap = SKStrokeCap.Round
        };
        float slotLen = radius * 0.6f;
        canvas.DrawLine(cx - slotLen, cy, cx + slotLen, cy, slotPaint);
        canvas.DrawLine(cx, cy - slotLen, cx, cy + slotLen, slotPaint);
    }

    /// <summary>Messen: Winkellineal (90-Grad-Winkel mit Markierungen).</summary>
    private static void DrawMeasuringIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var rulerPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xCA, 0x28), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };

        // Vertikale Linie (links)
        canvas.DrawLine(cx - half * 0.5f, cy - half, cx - half * 0.5f, cy + half * 0.5f, rulerPaint);
        // Horizontale Linie (unten)
        canvas.DrawLine(cx - half * 0.5f, cy + half * 0.5f, cx + half, cy + half * 0.5f, rulerPaint);

        // Markierungen an der vertikalen Linie
        using var markPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xA0, 0x00), StrokeWidth = 1.5f
        };
        for (int i = 0; i < 4; i++)
        {
            float yMark = cy - half + (i + 1) * (size * 0.3f / 2);
            float markLen = (i % 2 == 0) ? half * 0.3f : half * 0.2f;
            canvas.DrawLine(cx - half * 0.5f, yMark, cx - half * 0.5f + markLen, yMark, markPaint);
        }

        // Markierungen an der horizontalen Linie
        for (int i = 0; i < 4; i++)
        {
            float xMark = cx - half * 0.5f + (i + 1) * (size * 0.3f / 2);
            float markLen = (i % 2 == 0) ? half * 0.3f : half * 0.2f;
            canvas.DrawLine(xMark, cy + half * 0.5f, xMark, cy + half * 0.5f - markLen, markPaint);
        }
    }

    /// <summary>Geruest: Leiter (2 vertikale + 3 horizontale Linien).</summary>
    private static void DrawScaffoldingIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var polePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };

        // Zwei vertikale Stangen
        canvas.DrawLine(cx - half * 0.4f, cy - half, cx - half * 0.4f, cy + half, polePaint);
        canvas.DrawLine(cx + half * 0.4f, cy - half, cx + half * 0.4f, cy + half, polePaint);

        // 3 horizontale Sprossen
        using var rungPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x90, 0xA4, 0xAE), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        for (int i = 0; i < 3; i++)
        {
            float yRung = cy - half * 0.6f + i * (size * 0.6f / 2);
            canvas.DrawLine(cx - half * 0.4f, yRung, cx + half * 0.4f, yRung, rungPaint);
        }
    }

    /// <summary>Fallback-Icon: Einfacher Kreis mit Fragezeichen.</summary>
    private static void DrawDefaultIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var circlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), StrokeWidth = 2
        };
        canvas.DrawCircle(cx, cy, half * 0.6f, circlePaint);

        using var textPaint = new SKPaint
        {
            IsAntialias = true, TextSize = size * 0.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("?", cx, cy + size * 0.15f, textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KACHEL-TEXT UND HAEKCHEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet die Nummer oder das Fragezeichen in der unteren Haelfte der Kachel.
    /// Verbessert mit Glow-Effekt bei Memorisierung und Schatten.
    /// </summary>
    private void DrawTileNumber(SKCanvas canvas, SKRect rect, string displayNumber,
        bool isMemorizing, bool isCompleted)
    {
        if (string.IsNullOrEmpty(displayNumber)) return;

        bool isQuestion = displayNumber == "?";
        float fontSize = rect.Height * 0.28f;

        float x = rect.MidX;
        float y = rect.Bottom - rect.Height * 0.12f;

        // Schatten unter der Nummer
        if (!isQuestion)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 80),
                TextSize = fontSize,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText(displayNumber, x + 1, y + 1, shadowPaint);
        }

        // Haupttext
        using var numberPaint = new SKPaint
        {
            Color = isQuestion
                ? new SKColor(0xFF, 0xFF, 0xFF, 0x80)
                : new SKColor(0xFF, 0xFF, 0xFF, 0xE0),
            TextSize = fontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = !isQuestion
        };
        canvas.DrawText(displayNumber, x, y, numberPaint);

        // Memorisierungsphase: Nummer pulsiert mit Glow
        if (isMemorizing && !isQuestion)
        {
            float glowPulse = 0.5f + 0.5f * MathF.Sin(_animTime * 4);
            using var glowPaint = new SKPaint
            {
                Color = BlueprintCyan.WithAlpha((byte)(40 * glowPulse)),
                TextSize = fontSize + 2,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText(displayNumber, x, y, glowPaint);
        }
    }

    /// <summary>
    /// Zeichnet ein gruenes Haekchen oben rechts in der Kachel.
    /// Verbessert mit Glow-Effekt.
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, SKRect rect)
    {
        float badgeSize = 16;
        float badgeX = rect.Right - badgeSize - 3;
        float badgeY = rect.Top + 3;
        float bCx = badgeX + badgeSize / 2;
        float bCy = badgeY + badgeSize / 2;

        // Glow hinter dem Badge
        using var glowPaint = new SKPaint
        {
            Color = CompletedGreen.WithAlpha(40),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };
        canvas.DrawCircle(bCx, bCy, badgeSize / 2 + 2, glowPaint);

        // Gruener Kreis-Hintergrund
        using var circlePaint = new SKPaint
        {
            Color = CompletedGreen,
            IsAntialias = true
        };
        canvas.DrawCircle(bCx, bCy, badgeSize / 2, circlePaint);

        // Haekchen (zwei Linien)
        using var checkPaint = new SKPaint
        {
            Color = TextWhite,
            IsAntialias = true,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round,
            Style = SKPaintStyle.Stroke
        };

        float s = badgeSize * 0.22f;
        canvas.DrawLine(bCx - s * 1.2f, bCy, bCx - s * 0.1f, bCy + s, checkPaint);
        canvas.DrawLine(bCx - s * 0.1f, bCy + s, bCx + s * 1.5f, bCy - s * 0.8f, checkPaint);
    }
}
