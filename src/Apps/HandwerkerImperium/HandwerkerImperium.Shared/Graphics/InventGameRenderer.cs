using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Erfinder-Puzzle-Minigame.
/// Tech-Labor-Atmosphaere mit Neon-Grid, holographische Kacheln mit Schaltkreis-Rahmen,
/// Circuit-Verbindungslinien zwischen erledigten Teilen mit fliessenden Pulsen,
/// Completion-Burst-Partikel pro Kachel, goldene Celebration bei Komplett-Montage,
/// Fehler-Schock mit Blitz-Effekt, Memorisierungs-Scan-Linie.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// </summary>
public class InventGameRenderer : IDisposable
{
    private bool _disposed;
    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-SYSTEM (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_SPARKS = 60;
    private const int MAX_CIRCUIT_PULSES = 20;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
        public bool IsGolden; // Goldene Partikel bei Komplett-Montage
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

    // Zustandsverfolgung fuer Effekt-Trigger
    private int _prevCompletedCount;
    private bool _prevAllComplete;
    private readonly bool[] _prevHasError = new bool[20]; // Max 20 Teile
    private float _completionFlashTimer;
    private float _animTime;

    // Atmosphaerische Hintergrund-Partikel
    private const int MAX_AMBIENT = 12;
    private readonly SparkParticle[] _ambient = new SparkParticle[MAX_AMBIENT];
    private int _ambientCount;

    // Gecachter SKPath fuer wiederholte Nutzung (vermeidet GC-Allokationen pro Frame)
    private readonly SKPath _cachedPath = new();

    // ═══════════════════════════════════════════════════════════════════════
    // FARBEN
    // ═══════════════════════════════════════════════════════════════════════

    // Hintergrund (Dunkelviolett mit Tech-Raster)
    private static readonly SKColor LabBg = new(0x0D, 0x08, 0x2A);
    private static readonly SKColor GridCoarse = new(0x2D, 0x1B, 0x69);
    private static readonly SKColor GridFine = new(0x1A, 0x10, 0x45);
    private static readonly SKColor NeonViolet = new(0xBB, 0x86, 0xFC);
    private static readonly SKColor NeonCyan = new(0x00, 0xE5, 0xFF);
    private static readonly SKColor CompletedGreen = new(0x4C, 0xAF, 0x50);
    private static readonly SKColor ErrorRed = new(0xF4, 0x43, 0x36);
    private static readonly SKColor GoldenYellow = new(0xFF, 0xD7, 0x00);

    /// <summary>
    /// Daten-Struct fuer ein einzelnes Bauteil (View-optimiert, keine VM-Referenz).
    /// </summary>
    public struct InventPartData
    {
        public string Icon;
        public string DisplayNumber;
        public uint BackgroundColor; // ARGB
        public bool IsCompleted;
        public bool IsActive;
        public bool HasError;
        public int StepNumber; // Montage-Reihenfolge (1-basiert)
    }

    // Zwischengespeicherte Kachel-Positionen fuer HitTest und Circuit-Lines
    private SKRect[] _tileRects = Array.Empty<SKRect>();
    private SKPoint[] _tileCenters = Array.Empty<SKPoint>();

    /// <summary>
    /// Rendert das gesamte Erfinder-Puzzle-Spielfeld.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
        InventPartData[] parts, int cols,
        bool isMemorizing, bool isPlaying,
        int completedCount, int totalParts,
        float deltaTime)
    {
        _animTime += deltaTime;

        // Effekt-Trigger erkennen
        DetectCompletion(parts, completedCount, totalParts);
        DetectErrors(parts);

        // Labor-Hintergrund mit Neon-Grid
        DrawLabBackground(canvas, bounds);

        if (parts.Length == 0 || cols <= 0) return;

        // Grid-Layout berechnen
        int rows = (int)Math.Ceiling((double)parts.Length / cols);
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
        if (_tileRects.Length != parts.Length)
        {
            _tileRects = new SKRect[parts.Length];
            _tileCenters = new SKPoint[parts.Length];
        }

        for (int i = 0; i < parts.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = startX + col * (tileSize + tileSpacing);
            float y = startY + row * (tileSize + tileSpacing);
            _tileRects[i] = SKRect.Create(x, y, tileSize, tileSize);
            _tileCenters[i] = new SKPoint(x + tileSize / 2, y + tileSize / 2);
        }

        // Circuit-Verbindungslinien zwischen erledigten Teilen (unter den Kacheln)
        DrawCircuitConnections(canvas, parts);

        // Kacheln zeichnen
        for (int i = 0; i < parts.Length; i++)
        {
            DrawTile(canvas, _tileRects[i], parts[i], isMemorizing, isPlaying, tileSize);
        }

        // Memorisierungs-Scan-Linie
        if (isMemorizing)
            DrawScanLine(canvas, bounds, startY, gridHeight);

        // Partikel-Systeme aktualisieren und zeichnen
        UpdateAndDrawSparks(canvas, deltaTime);
        UpdateAndDrawCircuitPulses(canvas, parts, deltaTime);
        UpdateAndDrawAmbient(canvas, bounds, deltaTime);

        // Completion-Flash-Overlay
        if (_completionFlashTimer > 0)
            DrawCompletionFlash(canvas, bounds, deltaTime);

        // Fortschrittsanzeige unten
        DrawProgressBar(canvas, bounds, completedCount, totalParts, startY + gridHeight + 8);
    }

    /// <summary>
    /// HitTest: Gibt den Index des getroffenen Bauteils zurueck, oder -1.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int totalParts)
    {
        for (int i = 0; i < _tileRects.Length && i < totalParts; i++)
        {
            if (_tileRects[i].Contains(touchX, touchY))
                return i;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EFFEKT-TRIGGER-ERKENNUNG
    // ═══════════════════════════════════════════════════════════════════════

    private void DetectCompletion(InventPartData[] parts, int completedCount, int totalParts)
    {
        // Einzelnes Teil erledigt → Burst-Partikel
        if (completedCount > _prevCompletedCount)
        {
            int newlyCompleted = completedCount - _prevCompletedCount;
            for (int i = 0; i < parts.Length && newlyCompleted > 0; i++)
            {
                if (parts[i].IsCompleted && i < _tileRects.Length)
                {
                    // Pruefen ob dieses Teil gerade erst erledigt wurde (StepNumber == completedCount)
                    if (parts[i].StepNumber == completedCount)
                    {
                        SpawnCompletionBurst(_tileRects[i]);
                        SpawnCircuitPulse(parts, i);
                        newlyCompleted--;
                    }
                }
            }
        }

        // Alle Teile komplett → Celebration
        bool allComplete = completedCount >= totalParts && totalParts > 0;
        if (allComplete && !_prevAllComplete)
        {
            _completionFlashTimer = 1.5f;
            SpawnCelebrationSparks(parts);
        }

        _prevCompletedCount = completedCount;
        _prevAllComplete = allComplete;
    }

    private void DetectErrors(InventPartData[] parts)
    {
        for (int i = 0; i < parts.Length && i < _prevHasError.Length; i++)
        {
            if (parts[i].HasError && !_prevHasError[i])
            {
                // Neuer Fehler → Schock-Effekt
                if (i < _tileRects.Length)
                    SpawnErrorShock(_tileRects[i]);
            }
            _prevHasError[i] = parts[i].HasError;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-SPAWNER
    // ═══════════════════════════════════════════════════════════════════════

    private void SpawnCompletionBurst(SKRect tileRect)
    {
        var rng = Random.Shared;
        float cx = tileRect.MidX;
        float cy = tileRect.MidY;

        for (int i = 0; i < 12 && _sparkCount < MAX_SPARKS; i++)
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
                R = 0x4C, G = 0xAF, B = 0x50 // Gruen
            };
        }
    }

    private void SpawnCelebrationSparks(InventPartData[] parts)
    {
        var rng = Random.Shared;
        // Goldene Funken von jeder Kachel
        for (int t = 0; t < parts.Length && t < _tileRects.Length; t++)
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

    private void SpawnErrorShock(SKRect tileRect)
    {
        var rng = Random.Shared;
        float cx = tileRect.MidX;
        float cy = tileRect.MidY;

        // Rote Schock-Partikel
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

    private void SpawnCircuitPulse(InventPartData[] parts, int completedTileIndex)
    {
        // Finde den vorangehenden erledigten Teil (StepNumber - 1)
        int currentStep = parts[completedTileIndex].StepNumber;
        if (currentStep <= 1) return;

        int prevStep = currentStep - 1;
        int prevTileIndex = -1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StepNumber == prevStep && parts[i].IsCompleted)
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

    private void DrawLabBackground(SKCanvas canvas, SKRect bounds)
    {
        // Tiefdunkler Hintergrund mit Vignette
        using var bgPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(bounds.MidX, bounds.MidY),
                Math.Max(bounds.Width, bounds.Height) * 0.7f,
                new[] { new SKColor(0x15, 0x0B, 0x35), LabBg },
                null, SKShaderTileMode.Clamp),
            IsAntialias = false
        };
        canvas.DrawRect(bounds, bgPaint);

        // Feines Raster (Neon-Stil)
        using var fineGridPaint = new SKPaint
        {
            Color = GridFine, IsAntialias = false,
            StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke
        };
        for (float x = bounds.Left; x < bounds.Right; x += 20)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, fineGridPaint);
        for (float y = bounds.Top; y < bounds.Bottom; y += 20)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, fineGridPaint);

        // Grobes Raster mit leichtem Glow
        using var coarseGridPaint = new SKPaint
        {
            Color = GridCoarse, IsAntialias = false,
            StrokeWidth = 1f, Style = SKPaintStyle.Stroke
        };
        for (float x = bounds.Left; x < bounds.Right; x += 80)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, coarseGridPaint);
        for (float y = bounds.Top; y < bounds.Bottom; y += 80)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, coarseGridPaint);

        // Pulsierende Kreuzungspunkte (Tech-Atmosphaere)
        float pulse = 0.3f + 0.7f * (0.5f + 0.5f * MathF.Sin(_animTime * 1.5f));
        using var dotPaint = new SKPaint
        {
            Color = NeonViolet.WithAlpha((byte)(25 * pulse)),
            IsAntialias = true
        };
        for (float x = bounds.Left; x < bounds.Right; x += 80)
        {
            for (float y = bounds.Top; y < bounds.Bottom; y += 80)
            {
                canvas.DrawCircle(x, y, 2.5f, dotPaint);
            }
        }

        // Eck-Markierungen (Laborstil)
        using var cornerPaint = new SKPaint
        {
            Color = NeonViolet.WithAlpha(40),
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

    private void DrawCircuitConnections(SKCanvas canvas, InventPartData[] parts)
    {
        // Sortiere erledigte Teile nach StepNumber und verbinde sie
        // Sammle erledigte Teile mit ihren Indizes
        Span<(int index, int step)> completed = stackalloc (int, int)[Math.Min(parts.Length, 20)];
        int count = 0;

        for (int i = 0; i < parts.Length && count < completed.Length; i++)
        {
            if (parts[i].IsCompleted)
                completed[count++] = (i, parts[i].StepNumber);
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

        // Verbindungslinien zeichnen
        using var linePaint = new SKPaint
        {
            Color = CompletedGreen.WithAlpha(60),
            IsAntialias = true,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, _animTime * 30)
        };

        using var glowPaint = new SKPaint
        {
            Color = CompletedGreen.WithAlpha(20),
            IsAntialias = true,
            StrokeWidth = 6f,
            Style = SKPaintStyle.Stroke,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
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

    private void DrawTile(SKCanvas canvas, SKRect rect, InventPartData part,
        bool isMemorizing, bool isPlaying, float tileSize)
    {
        float cr = 10;

        // Hintergrundfarbe extrahieren
        var bgColor = new SKColor(
            (byte)((part.BackgroundColor >> 16) & 0xFF),
            (byte)((part.BackgroundColor >> 8) & 0xFF),
            (byte)(part.BackgroundColor & 0xFF),
            (byte)((part.BackgroundColor >> 24) & 0xFF)
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
        DrawTileBorder(canvas, rect, part, cr);

        // Icon
        DrawTileIcon(canvas, rect, part.Icon);

        // Nummer oder Fragezeichen
        DrawTileNumber(canvas, rect, part.DisplayNumber, isMemorizing, part.IsCompleted);

        // Haekchen bei erledigten Teilen
        if (part.IsCompleted)
            DrawCheckmark(canvas, rect);

        // Step-Number Badge (kleine Nummer oben links bei erledigten)
        if (part.IsCompleted && part.StepNumber > 0)
            DrawStepBadge(canvas, rect, part.StepNumber);

        // Fehler-Overlay (roter Puls + Zickzack-Blitz)
        if (part.HasError)
            DrawErrorOverlay(canvas, rect, cr);
    }

    private void DrawTileBorder(SKCanvas canvas, SKRect rect, InventPartData part, float cr)
    {
        if (part.IsActive && !part.IsCompleted)
        {
            // Pulsierender Neon-Rahmen fuer aktives Bauteil
            float pulse = 0.5f + 0.5f * MathF.Sin(_animTime * 6);
            byte alpha = (byte)(160 + 95 * pulse);

            // Aeusserer Glow
            using var outerGlow = new SKPaint
            {
                Color = NeonViolet.WithAlpha((byte)(50 * pulse)),
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
                Color = NeonViolet.WithAlpha(alpha),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);

            // Ecken-Akzente (Schaltkreis-Stil)
            DrawCircuitCorners(canvas, rect, NeonViolet.WithAlpha(alpha), cr);
        }
        else if (part.IsCompleted)
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
        else if (part.HasError)
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
            // Standard-Rahmen (dezent, Tech-Stil)
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 0x25),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f
            };
            canvas.DrawRoundRect(rect, cr, cr, borderPaint);

            // Dezente Ecken-Markierungen
            DrawCircuitCorners(canvas, rect, new SKColor(0xFF, 0xFF, 0xFF, 0x15), cr);
        }
    }

    /// <summary>
    /// Zeichnet Schaltkreis-Ecken (L-foermige Markierungen) an den Kachel-Ecken.
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
    /// Fehler-Overlay: Rotes Pulsieren + Zickzack-Blitz.
    /// </summary>
    private void DrawErrorOverlay(SKCanvas canvas, SKRect rect, float cr)
    {
        float errPulse = 0.4f + 0.6f * MathF.Sin(_animTime * 15);

        // Rotes Overlay
        using var overlayPaint = new SKPaint
        {
            Color = ErrorRed.WithAlpha((byte)(80 * errPulse)),
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, cr, cr, overlayPaint);

        // Kleiner Blitz in der Mitte
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
        _cachedPath.Reset();
        _cachedPath.MoveTo(cx - boltH * 0.15f, cy - boltH * 0.5f);
        _cachedPath.LineTo(cx + boltH * 0.1f, cy - boltH * 0.05f);
        _cachedPath.LineTo(cx - boltH * 0.1f, cy + boltH * 0.05f);
        _cachedPath.LineTo(cx + boltH * 0.15f, cy + boltH * 0.5f);
        canvas.DrawPath(_cachedPath, boltPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEMORISIERUNGS-SCAN-LINIE
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawScanLine(SKCanvas canvas, SKRect bounds, float gridTop, float gridHeight)
    {
        // Horizontale leuchtende Linie die ueber das Grid faehrt
        float period = 2.0f; // 2 Sekunden pro Durchgang
        float progress = (_animTime % period) / period;
        float scanY = gridTop + progress * gridHeight;

        using var scanPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, scanY - 8),
                new SKPoint(bounds.Left, scanY + 8),
                new[] { SKColors.Transparent, NeonCyan.WithAlpha(60), NeonCyan.WithAlpha(100),
                        NeonCyan.WithAlpha(60), SKColors.Transparent },
                new[] { 0f, 0.3f, 0.5f, 0.7f, 1f },
                SKShaderTileMode.Clamp),
            IsAntialias = false
        };
        canvas.DrawRect(bounds.Left, scanY - 8, bounds.Width, 16, scanPaint);

        // Heller Kern
        using var corePaint = new SKPaint
        {
            Color = NeonCyan.WithAlpha(150),
            IsAntialias = false,
            StrokeWidth = 1
        };
        canvas.DrawLine(bounds.Left + 20, scanY, bounds.Right - 20, scanY, corePaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETION-FLASH
    // ═══════════════════════════════════════════════════════════════════════

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

            // Gradient: Violett → Cyan → Gruen
            SKColor fillColor;
            if (progress < 0.5f)
                fillColor = NeonViolet;
            else if (progress < 1f)
                fillColor = NeonCyan;
            else
                fillColor = CompletedGreen;

            using var fillPaint = new SKPaint
            {
                Color = fillColor,
                IsAntialias = true
            };
            canvas.DrawRoundRect(barX, y, fillWidth, barHeight, 3, 3, fillPaint);

            // Glow am Ende
            using var glowPaint = new SKPaint
            {
                Color = fillColor.WithAlpha(60),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
            };
            canvas.DrawCircle(barX + fillWidth, y + barHeight / 2, 5, glowPaint);
        }

        // Fortschritts-Text
        using var textPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0xB0),
            IsAntialias = true,
            TextSize = 11,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText($"{completed}/{total}", bounds.MidX, y + barHeight + 14, textPaint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-UPDATE & ZEICHNEN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        using var paint = new SKPaint { IsAntialias = true };

        for (int i = _sparkCount - 1; i >= 0; i--)
        {
            ref var s = ref _sparks[i];
            s.Life += deltaTime;
            if (s.Life >= s.MaxLife)
            {
                // Kompaktieren: Letztes Element an diese Stelle
                _sparks[i] = _sparks[--_sparkCount];
                continue;
            }

            s.X += s.VelocityX * deltaTime;
            s.Y += s.VelocityY * deltaTime;
            s.VelocityY += 30 * deltaTime; // Leichte Schwerkraft

            float lifeRatio = s.Life / s.MaxLife;
            float alpha = lifeRatio < 0.2f ? lifeRatio / 0.2f : (1 - lifeRatio) / 0.8f;
            alpha = Math.Clamp(alpha, 0, 1);

            if (s.IsGolden)
            {
                // Goldene Funken schimmern
                float shimmer = 0.7f + 0.3f * MathF.Sin(s.Life * 15 + s.X * 0.1f);
                paint.Color = new SKColor(s.R, s.G, (byte)(s.B * shimmer), (byte)(alpha * 220));
            }
            else
            {
                paint.Color = new SKColor(s.R, s.G, s.B, (byte)(alpha * 200));
            }

            float size = s.Size * (1 - lifeRatio * 0.5f);
            canvas.DrawCircle(s.X, s.Y, size, paint);

            // Heller Kern
            paint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(alpha * 100));
            canvas.DrawCircle(s.X, s.Y, size * 0.4f, paint);
        }
    }

    private void UpdateAndDrawCircuitPulses(SKCanvas canvas, InventPartData[] parts, float deltaTime)
    {
        using var pulsePaint = new SKPaint
        {
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };

        for (int i = _pulseCount - 1; i >= 0; i--)
        {
            ref var p = ref _pulses[i];
            p.Life += deltaTime;
            p.Progress += p.Speed * deltaTime;

            if (p.Life >= p.MaxLife || p.Progress > 1.2f)
            {
                _pulses[i] = _pulses[--_pulseCount];
                continue;
            }

            if (p.FromTile >= _tileCenters.Length || p.ToTile >= _tileCenters.Length) continue;

            var from = _tileCenters[p.FromTile];
            var to = _tileCenters[p.ToTile];

            // Position entlang der Linie
            float prog = Math.Clamp(p.Progress, 0, 1);
            float px = from.X + (to.X - from.X) * prog;
            float py = from.Y + (to.Y - from.Y) * prog;

            float alpha = p.Life < 0.2f ? p.Life / 0.2f : (1 - p.Life / p.MaxLife);
            alpha = Math.Clamp(alpha, 0, 1);

            // Leuchtender Punkt
            pulsePaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 180));
            canvas.DrawCircle(px, py, 5, pulsePaint);

            // Heller Kern
            pulsePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(alpha * 120));
            canvas.DrawCircle(px, py, 2, pulsePaint);
        }
    }

    private void UpdateAndDrawAmbient(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        var rng = Random.Shared;

        // Atmosphaerische Partikel auffuellen
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
                Size = 1 + rng.NextSingle() * 2f,
                R = 0xBB, G = 0x86, B = 0xFC // Violett
            };
        }

        using var paint = new SKPaint { IsAntialias = false };

        for (int i = _ambientCount - 1; i >= 0; i--)
        {
            ref var a = ref _ambient[i];
            a.Life += deltaTime;
            if (a.Life >= a.MaxLife)
            {
                _ambient[i] = _ambient[--_ambientCount];
                continue;
            }

            a.X += a.VelocityX * deltaTime;
            a.Y += a.VelocityY * deltaTime;
            a.X += MathF.Sin(a.Life * 2 + a.X * 0.02f) * 3 * deltaTime;

            if (a.Y < bounds.Top - 10 || a.X < bounds.Left - 10 || a.X > bounds.Right + 10)
            {
                _ambient[i] = _ambient[--_ambientCount];
                continue;
            }

            float lifeRatio = a.Life / a.MaxLife;
            float alpha;
            if (lifeRatio < 0.15f) alpha = lifeRatio / 0.15f;
            else if (lifeRatio > 0.65f) alpha = (1 - lifeRatio) / 0.35f;
            else alpha = 1;

            paint.Color = new SKColor(a.R, a.G, a.B, (byte)(alpha * 50));
            canvas.DrawCircle(a.X, a.Y, a.Size, paint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BAUTEIL-ICONS (12 SkiaSharp-Zeichnungen)
    // ═══════════════════════════════════════════════════════════════════════

    private static void DrawTileIcon(SKCanvas canvas, SKRect rect, string iconId)
    {
        if (string.IsNullOrEmpty(iconId)) return;

        float iconSize = rect.Height * 0.32f;
        float cx = rect.MidX;
        float cy = rect.Top + rect.Height * 0.38f;

        switch (iconId)
        {
            case "gear": DrawGearIcon(canvas, cx, cy, iconSize); break;
            case "piston": DrawPistonIcon(canvas, cx, cy, iconSize); break;
            case "wire": DrawWireIcon(canvas, cx, cy, iconSize); break;
            case "board": DrawBoardIcon(canvas, cx, cy, iconSize); break;
            case "screw": DrawScrewIcon(canvas, cx, cy, iconSize); break;
            case "housing": DrawHousingIcon(canvas, cx, cy, iconSize); break;
            case "spring": DrawSpringIcon(canvas, cx, cy, iconSize); break;
            case "lens": DrawLensIcon(canvas, cx, cy, iconSize); break;
            case "motor": DrawMotorIcon(canvas, cx, cy, iconSize); break;
            case "battery": DrawBatteryIcon(canvas, cx, cy, iconSize); break;
            case "switch": DrawSwitchIcon(canvas, cx, cy, iconSize); break;
            case "antenna": DrawAntennaIcon(canvas, cx, cy, iconSize); break;
            default: DrawDefaultIcon(canvas, cx, cy, iconSize); break;
        }
    }

    /// <summary>Zahnrad: Kreis mit 8 Zaehnen und Gradient.</summary>
    private static void DrawGearIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float outerR = half * 0.85f;
        float toothW = half * 0.22f;
        int teeth = 8;

        using var fillPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - half * 0.2f, cy - half * 0.2f), outerR * 1.2f,
                new[] { new SKColor(0xCF, 0xD8, 0xDC), new SKColor(0x90, 0xA4, 0xAE) },
                null, SKShaderTileMode.Clamp)
        };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x60, 0x7D, 0x8B), StrokeWidth = 1.5f
        };

        canvas.DrawCircle(cx, cy, outerR, fillPaint);

        using var toothPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x90, 0xA4, 0xAE) };
        for (int i = 0; i < teeth; i++)
        {
            float angle = i * 360f / teeth;
            float rad = angle * MathF.PI / 180f;
            float tx = cx + MathF.Cos(rad) * outerR;
            float ty = cy + MathF.Sin(rad) * outerR;

            canvas.Save();
            canvas.Translate(tx, ty);
            canvas.RotateDegrees(angle);
            canvas.DrawRect(-toothW / 2, -toothW / 2, toothW, toothW, toothPaint);
            canvas.Restore();
        }

        canvas.DrawCircle(cx, cy, outerR, borderPaint);

        using var holePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x15, 0x0B, 0x35) };
        float innerR = half * 0.55f;
        canvas.DrawCircle(cx, cy, innerR * 0.45f, holePaint);
        canvas.DrawCircle(cx, cy, innerR * 0.45f, borderPaint);
    }

    /// <summary>Kolben: Rechteck mit Stange nach unten.</summary>
    private static void DrawPistonIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var headPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy - half * 0.7f), new SKPoint(cx, cy),
                new[] { new SKColor(0xB0, 0xBE, 0xC5), new SKColor(0x78, 0x90, 0x9C) },
                null, SKShaderTileMode.Clamp)
        };
        var headRect = SKRect.Create(cx - half * 0.6f, cy - half * 0.7f, half * 1.2f, half * 0.7f);
        canvas.DrawRoundRect(headRect, 3, 3, headPaint);

        using var ringPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x61, 0x61, 0x61), StrokeWidth = 2
        };
        canvas.DrawLine(headRect.Left + 3, headRect.Top + half * 0.15f, headRect.Right - 3, headRect.Top + half * 0.15f, ringPaint);

        using var rodPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        var rodRect = SKRect.Create(cx - half * 0.15f, cy, half * 0.3f, half * 0.8f);
        canvas.DrawRect(rodRect, rodPaint);

        using var boltPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawCircle(cx, cy + half * 0.8f, half * 0.18f, boltPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(headRect, 3, 3, borderPaint);
    }

    /// <summary>Kabel: Wellenlinie mit Steckern an beiden Enden.</summary>
    private static void DrawWireIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var wirePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xEF, 0x53, 0x50), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };

        using var path = new SKPath();
        path.MoveTo(cx - half, cy);
        float segW = half * 2f / 3f;
        for (int i = 0; i < 3; i++)
        {
            float startX = cx - half + i * segW;
            float controlY = (i % 2 == 0) ? cy - half * 0.5f : cy + half * 0.5f;
            path.QuadTo(startX + segW / 2, controlY, startX + segW, cy);
        }
        canvas.DrawPath(path, wirePaint);

        using var plugPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xCA, 0x28) };
        canvas.DrawCircle(cx - half, cy, half * 0.15f, plugPaint);
        canvas.DrawCircle(cx + half, cy, half * 0.15f, plugPaint);
    }

    /// <summary>Platine: Rechteck mit Leiterbahnen und Bauteilen.</summary>
    private static void DrawBoardIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var boardPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x2E, 0x7D, 0x32) };
        var boardRect = SKRect.Create(cx - half * 0.8f, cy - half * 0.6f, half * 1.6f, half * 1.2f);
        canvas.DrawRoundRect(boardRect, 3, 3, boardPaint);

        using var tracePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xD5, 0x4F, 0xC0), StrokeWidth = 1.5f
        };
        canvas.DrawLine(boardRect.Left + 4, cy - half * 0.2f, boardRect.Right - 4, cy - half * 0.2f, tracePaint);
        canvas.DrawLine(boardRect.Left + 4, cy + half * 0.2f, boardRect.Right - 4, cy + half * 0.2f, tracePaint);
        canvas.DrawLine(cx - half * 0.3f, boardRect.Top + 4, cx - half * 0.3f, boardRect.Bottom - 4, tracePaint);
        canvas.DrawLine(cx + half * 0.3f, boardRect.Top + 4, cx + half * 0.3f, boardRect.Bottom - 4, tracePaint);

        using var chipPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x21, 0x21, 0x21) };
        canvas.DrawRect(cx - half * 0.2f, cy - half * 0.15f, half * 0.4f, half * 0.3f, chipPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x1B, 0x5E, 0x20), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(boardRect, 3, 3, borderPaint);
    }

    /// <summary>Schraube: Kreuz mit Kreis (Kreuzschlitz).</summary>
    private static void DrawScrewIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;
        float radius = half * 0.7f;

        using var headPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - radius * 0.2f, cy - radius * 0.2f), radius * 1.3f,
                new[] { new SKColor(0xCF, 0xD8, 0xDC), new SKColor(0x90, 0xA4, 0xAE) },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, cy, radius, headPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x78, 0x90, 0x9C), StrokeWidth = 2
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        using var slotPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        float slotLen = radius * 0.6f;
        canvas.DrawLine(cx - slotLen, cy, cx + slotLen, cy, slotPaint);
        canvas.DrawLine(cx, cy - slotLen, cx, cy + slotLen, slotPaint);
    }

    /// <summary>Gehaeuse: Abgerundetes Rechteck mit Schrauben in Ecken.</summary>
    private static void DrawHousingIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var housingPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy - half * 0.7f), new SKPoint(cx, cy + half * 0.7f),
                new[] { new SKColor(0x78, 0x90, 0x9C), new SKColor(0x54, 0x6E, 0x7A) },
                null, SKShaderTileMode.Clamp)
        };
        var housingRect = SKRect.Create(cx - half * 0.8f, cy - half * 0.7f, half * 1.6f, half * 1.4f);
        canvas.DrawRoundRect(housingRect, 6, 6, housingPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x45, 0x5A, 0x64), StrokeWidth = 2
        };
        canvas.DrawRoundRect(housingRect, 6, 6, borderPaint);

        using var screwPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        float screwR = half * 0.08f;
        float inset = half * 0.2f;
        canvas.DrawCircle(housingRect.Left + inset, housingRect.Top + inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Right - inset, housingRect.Top + inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Left + inset, housingRect.Bottom - inset, screwR, screwPaint);
        canvas.DrawCircle(housingRect.Right - inset, housingRect.Bottom - inset, screwR, screwPaint);

        using var slitPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x45, 0x5A, 0x64, 0x80), StrokeWidth = 1.5f
        };
        for (int i = 0; i < 3; i++)
        {
            float ly = cy - half * 0.2f + i * half * 0.2f;
            canvas.DrawLine(cx - half * 0.3f, ly, cx + half * 0.3f, ly, slitPaint);
        }
    }

    /// <summary>Feder: Zickzack-Linie (Spiralfeder).</summary>
    private static void DrawSpringIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var springPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xE0, 0xE0, 0xE0), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };

        int segments = 7;
        float segHeight = (half * 1.6f) / segments;
        float zigWidth = half * 0.5f;

        canvas.DrawLine(cx - half * 0.15f, cy - half * 0.8f, cx + half * 0.15f, cy - half * 0.8f, springPaint);

        using var path = new SKPath();
        path.MoveTo(cx, cy - half * 0.8f);
        for (int i = 0; i < segments; i++)
        {
            float y = cy - half * 0.8f + (i + 1) * segHeight;
            float x = (i % 2 == 0) ? cx + zigWidth : cx - zigWidth;
            path.LineTo(x, y);
        }
        canvas.DrawPath(path, springPaint);

        float bottomY = cy - half * 0.8f + segments * segHeight;
        float lastX = (segments % 2 != 0) ? cx + zigWidth : cx - zigWidth;
        canvas.DrawLine(lastX, bottomY, cx, bottomY, springPaint);
        canvas.DrawLine(cx - half * 0.15f, bottomY, cx + half * 0.15f, bottomY, springPaint);
    }

    /// <summary>Linse: Doppelkonvexe Form mit Lichtreflex.</summary>
    private static void DrawLensIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var lensPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x42, 0xA5, 0xF5, 0xA0) };
        using var path = new SKPath();

        float lensHeight = half * 1.4f;
        float bulgeFactor = half * 0.6f;

        path.MoveTo(cx, cy - lensHeight / 2);
        path.QuadTo(cx - bulgeFactor, cy, cx, cy + lensHeight / 2);
        path.QuadTo(cx + bulgeFactor, cy, cx, cy - lensHeight / 2);
        path.Close();
        canvas.DrawPath(path, lensPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x1E, 0x88, 0xE5), StrokeWidth = 2
        };
        canvas.DrawPath(path, borderPaint);

        using var reflexPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80), StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round
        };
        using var reflexPath = new SKPath();
        reflexPath.MoveTo(cx - half * 0.2f, cy - half * 0.35f);
        reflexPath.QuadTo(cx - half * 0.1f, cy - half * 0.45f, cx + half * 0.05f, cy - half * 0.35f);
        canvas.DrawPath(reflexPath, reflexPaint);
    }

    /// <summary>Motor: Rechteck mit Achse und Wicklungs-Andeutung.</summary>
    private static void DrawMotorIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var bodyPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx - half * 0.6f, cy), new SKPoint(cx + half * 0.6f, cy),
                new[] { new SKColor(0x60, 0x7D, 0x8B), new SKColor(0x90, 0xA4, 0xAE) },
                null, SKShaderTileMode.Clamp)
        };
        var bodyRect = SKRect.Create(cx - half * 0.6f, cy - half * 0.5f, half * 1.2f, half);
        canvas.DrawRoundRect(bodyRect, 4, 4, bodyPaint);

        using var coilPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0x8F, 0x00), StrokeWidth = 2
        };
        canvas.DrawLine(bodyRect.Left + 4, cy - half * 0.15f, bodyRect.Right - 4, cy - half * 0.15f, coilPaint);
        canvas.DrawLine(bodyRect.Left + 4, cy + half * 0.15f, bodyRect.Right - 4, cy + half * 0.15f, coilPaint);

        using var axlePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xBE, 0xC5), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(bodyRect.Right, cy, bodyRect.Right + half * 0.35f, cy, axlePaint);

        using var bearingPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE0, 0xE0, 0xE0) };
        canvas.DrawCircle(bodyRect.Right + half * 0.35f, cy, half * 0.1f, bearingPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(bodyRect, 4, 4, borderPaint);
    }

    /// <summary>Batterie: Rechteck mit +/- Symbolen.</summary>
    private static void DrawBatteryIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var bodyPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x43, 0xA0, 0x47) };
        var bodyRect = SKRect.Create(cx - half * 0.5f, cy - half * 0.7f, half, half * 1.4f);
        canvas.DrawRoundRect(bodyRect, 3, 3, bodyPaint);

        using var capPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        canvas.DrawRect(cx - half * 0.2f, bodyRect.Top - half * 0.15f, half * 0.4f, half * 0.15f, capPaint);

        using var symbolPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = SKColors.White, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round
        };
        float plusY = cy - half * 0.3f;
        canvas.DrawLine(cx - half * 0.15f, plusY, cx + half * 0.15f, plusY, symbolPaint);
        canvas.DrawLine(cx, plusY - half * 0.15f, cx, plusY + half * 0.15f, symbolPaint);

        float minusY = cy + half * 0.3f;
        canvas.DrawLine(cx - half * 0.15f, minusY, cx + half * 0.15f, minusY, symbolPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x2E, 0x7D, 0x32), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(bodyRect, 3, 3, borderPaint);
    }

    /// <summary>Schalter: Hebel mit Basis.</summary>
    private static void DrawSwitchIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var basePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        var baseRect = SKRect.Create(cx - half * 0.7f, cy + half * 0.1f, half * 1.4f, half * 0.4f);
        canvas.DrawRoundRect(baseRect, 4, 4, basePaint);

        using var pivotPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xB0, 0xBE, 0xC5) };
        float pivotY = baseRect.Top;
        canvas.DrawCircle(cx, pivotY, half * 0.12f, pivotPaint);

        using var leverPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFF, 0xCA, 0x28), StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx, pivotY, cx + half * 0.5f, cy - half * 0.6f, leverPaint);

        using var handlePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xFF, 0xD5, 0x4F) };
        canvas.DrawCircle(cx + half * 0.5f, cy - half * 0.6f, half * 0.15f, handlePaint);

        using var contactPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0xE0, 0xE0, 0xE0) };
        canvas.DrawCircle(baseRect.Left + half * 0.2f, pivotY, half * 0.08f, contactPaint);
        canvas.DrawCircle(baseRect.Right - half * 0.2f, pivotY, half * 0.08f, contactPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x54, 0x6E, 0x7A), StrokeWidth = 1.5f
        };
        canvas.DrawRoundRect(baseRect, 4, 4, borderPaint);
    }

    /// <summary>Antenne: Vertikale Linie mit Signalwellen.</summary>
    private static void DrawAntennaIcon(SKCanvas canvas, float cx, float cy, float size)
    {
        float half = size / 2;

        using var stickPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xB0, 0xBE, 0xC5), StrokeWidth = 2.5f, StrokeCap = SKStrokeCap.Round
        };
        canvas.DrawLine(cx, cy + half * 0.5f, cx, cy - half * 0.5f, stickPaint);

        using var basePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x78, 0x90, 0x9C) };
        using var basePath = new SKPath();
        basePath.MoveTo(cx, cy + half * 0.5f);
        basePath.LineTo(cx - half * 0.3f, cy + half * 0.85f);
        basePath.LineTo(cx + half * 0.3f, cy + half * 0.85f);
        basePath.Close();
        canvas.DrawPath(basePath, basePaint);

        using var wavePaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xBB, 0x86, 0xFC, 0xA0), StrokeWidth = 1.5f
        };
        for (int i = 1; i <= 3; i++)
        {
            float waveR = half * 0.2f * i;
            byte alpha = (byte)(160 - i * 40);
            wavePaint.Color = new SKColor(0xBB, 0x86, 0xFC, alpha);
            canvas.DrawArc(
                SKRect.Create(cx - waveR, cy - half * 0.5f - waveR, waveR * 2, waveR * 2),
                -150, 120, false, wavePaint);
        }
    }

    /// <summary>Fallback-Icon: Kreis mit Fragezeichen.</summary>
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
    // NUMMER + HAEKCHEN
    // ═══════════════════════════════════════════════════════════════════════

    private void DrawTileNumber(SKCanvas canvas, SKRect rect, string displayNumber,
        bool isMemorizing, bool isCompleted)
    {
        if (string.IsNullOrEmpty(displayNumber)) return;

        bool isQuestion = displayNumber == "?";
        float fontSize = rect.Height * 0.28f;

        float x = rect.MidX;
        float y = rect.Bottom - rect.Height * 0.12f;

        // Schatten
        if (!isQuestion)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 100),
                TextSize = fontSize, IsAntialias = true,
                TextAlign = SKTextAlign.Center, FakeBoldText = true
            };
            canvas.DrawText(displayNumber, x + 1, y + 1, shadowPaint);
        }

        // Haupttext
        SKColor textColor;
        if (isQuestion)
            textColor = new SKColor(0xFF, 0xFF, 0xFF, 0x60);
        else if (isCompleted)
            textColor = CompletedGreen;
        else
            textColor = new SKColor(0xFF, 0xFF, 0xFF, 0xE0);

        using var numberPaint = new SKPaint
        {
            Color = textColor,
            TextSize = fontSize, IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = !isQuestion
        };
        canvas.DrawText(displayNumber, x, y, numberPaint);

        // Holographischer Glow bei Memorisierung
        if (isMemorizing && !isQuestion)
        {
            float glowPulse = 0.5f + 0.5f * MathF.Sin(_animTime * 4 + rect.Left * 0.05f);
            using var glowPaint = new SKPaint
            {
                Color = NeonCyan.WithAlpha((byte)(60 * glowPulse)),
                TextSize = fontSize + 3,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
            };
            canvas.DrawText(displayNumber, x, y, glowPaint);
        }
    }

    private static void DrawCheckmark(SKCanvas canvas, SKRect rect)
    {
        float badgeSize = 16;
        float badgeX = rect.Right - badgeSize - 3;
        float badgeY = rect.Top + 3;
        float cx = badgeX + badgeSize / 2;
        float cy = badgeY + badgeSize / 2;

        // Gruener Kreis mit Glow
        using var glowPaint = new SKPaint
        {
            Color = CompletedGreen.WithAlpha(40),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3)
        };
        canvas.DrawCircle(cx, cy, badgeSize / 2 + 2, glowPaint);

        using var circlePaint = new SKPaint { Color = CompletedGreen, IsAntialias = true };
        canvas.DrawCircle(cx, cy, badgeSize / 2, circlePaint);

        using var checkPaint = new SKPaint
        {
            Color = SKColors.White, IsAntialias = true,
            StrokeWidth = 2, StrokeCap = SKStrokeCap.Round,
            Style = SKPaintStyle.Stroke
        };

        float s = badgeSize * 0.22f;
        canvas.DrawLine(cx - s * 1.2f, cy, cx - s * 0.1f, cy + s, checkPaint);
        canvas.DrawLine(cx - s * 0.1f, cy + s, cx + s * 1.5f, cy - s * 0.8f, checkPaint);
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedPath?.Dispose();
    }
}
