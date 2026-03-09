using HandwerkerImperium.Services;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Verkabelungs-Minigame.
/// Sicherungskasten mit Bezier-Kabelverbindungen, Strom-Puls-Animation,
/// Funken-Explosion bei Verbindung, Kurzschluss-Effekt bei Fehler,
/// Completion-Blitz-Flash wenn alle Kabel verbunden sind.
/// </summary>
public sealed class WiringGameRenderer : IDisposable
{
    private bool _disposed;

    // AI-Hintergrund (optionaler Layer unter den Spielelementen)
    private IGameAssetService? _assetService;
    private SKBitmap? _background;

    // ═══════════════════════════════════════════════════════════════════
    // WIEDERVERWENDBARE SKPAINT-INSTANZEN (vermeidet per-Frame Allokationen)
    // ═══════════════════════════════════════════════════════════════════

    // Allgemeiner Fill-Paint (ohne Antialiasing)
    private readonly SKPaint _fillPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    // Fill-Paint mit Antialiasing (fuer Kreise, LEDs, Dots)
    private readonly SKPaint _fillPaintAA = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Allgemeiner Stroke-Paint (ohne Antialiasing)
    private readonly SKPaint _strokePaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };

    // Stroke-Paint mit Antialiasing (fuer Bezier-Kurven, Haekchen, Blitze)
    private readonly SKPaint _strokePaintAA = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    // Duenne Linien (Fugen, Schrauben-Schlitze - StrokeWidth=1)
    private readonly SKPaint _thinStrokePaint = new() { IsAntialias = false, StrokeWidth = 1 };

    // Text-Paint (immer AA)
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Shader-basierter Fill (Gradient fuer Wand, Panel, Stecker)
    private readonly SKPaint _shaderPaint = new() { IsAntialias = false };

    // Glow-Paint mit MaskFilter (fuer LED-Glow, Node-Glow, Completion-Glow)
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // Gecachte MaskFilter (haeufigste Blur-Radien)
    private readonly SKMaskFilter _blur3 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3);
    private readonly SKMaskFilter _blur4 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4);
    private readonly SKMaskFilter _blur5 = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5);

    // ═══════════════════════════════════════════════════════════════════
    // FARBEN
    // ═══════════════════════════════════════════════════════════════════

    private static readonly SKColor PanelBg = new(0x37, 0x47, 0x4F);
    private static readonly SKColor PanelBorder = new(0x26, 0x32, 0x38);
    private static readonly SKColor PanelAccent = new(0x54, 0x6E, 0x7A);
    private static readonly SKColor PanelHighlight = new(0x60, 0x7D, 0x8B);
    private static readonly SKColor ConnectedBg = new(0x2E, 0x7D, 0x32, 50);
    private static readonly SKColor SelectedBg = new(0xFF, 0xFF, 0xFF, 50);
    private static readonly SKColor ErrorBg = new(0xFF, 0x44, 0x44, 80);
    private static readonly SKColor WallColor = new(0x3E, 0x3E, 0x3E);
    private static readonly SKColor WallLine = new(0x48, 0x48, 0x48);

    // Kabelfarben (passend zum WireColor Enum im ViewModel)
    private static readonly SKColor[] WireColors =
    {
        new(0xFF, 0x44, 0x44), // Red
        new(0x44, 0x88, 0xFF), // Blue (heller fuer besseren Kontrast)
        new(0x44, 0xFF, 0x44), // Green
        new(0xFF, 0xFF, 0x44), // Yellow
        new(0xFF, 0x88, 0x44), // Orange
        new(0xBB, 0x55, 0xFF), // Purple (heller)
        new(0x44, 0xFF, 0xFF), // Cyan
    };

    // ═══════════════════════════════════════════════════════════════════
    // ANIMATIONSZUSTAND
    // ═══════════════════════════════════════════════════════════════════

    private float _time;

    // Strom-Puls pro Verbindung (wandert entlang Bezier-Kurve)
    private readonly float[] _pulseProgress = new float[8]; // max 8 Kabel

    // Completion-Zustand
    private bool _prevAllConnected;
    private bool _completionStarted;
    private float _completionTime;

    // Verbindungs-Burst: Explosion bei neuer Verbindung
    private int _prevConnectedCount;

    // Error-Erkennung: Vorheriger Error-Status pro Kabel
    private readonly bool[] _prevHasError = new bool[8];

    // ═══════════════════════════════════════════════════════════════════
    // PARTIKEL-SYSTEME (Struct-Arrays, kein GC)
    // ═══════════════════════════════════════════════════════════════════

    private const int MAX_SPARKS = 60;
    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;

    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
        public bool Active;
    }

    // Kurzschluss-Blitze bei Fehler
    private const int MAX_BOLTS = 8;
    private readonly LightningBolt[] _bolts = new LightningBolt[MAX_BOLTS];
    private int _boltCount;

    // Gecachte SKPath/SKFont fuer wiederholte Nutzung (vermeidet GC-Allokationen pro Frame)
    private readonly SKPath _cachedPath = new();
    private readonly SKFont _cachedFont = new(SKTypeface.Default, 11);

    private struct LightningBolt
    {
        public float X1, Y1, X2, Y2, Life, MaxLife;
        public bool Active;
    }

    // ═══════════════════════════════════════════════════════════════════
    // RENDER
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialisiert den AI-Asset-Service für den Hintergrund.
    /// </summary>
    public void Initialize(IGameAssetService assetService)
    {
        _assetService = assetService;
    }

    /// <summary>
    /// Rendert das Verkabelungs-Spielfeld mit AAA-Effekten.
    /// isAllConnected und connectedCount werden fuer Completion/Burst-Erkennung benoetigt.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, WireRenderData[] leftWires, WireRenderData[] rightWires,
        int? selectedLeftIndex, bool isAllConnected, int connectedCount, float deltaTime)
    {
        // AI-Hintergrund als Atmosphäre-Layer
        if (_assetService != null)
        {
            _background ??= _assetService.GetBitmap("minigames/wiring_bg.webp");
            if (_background == null)
                _ = _assetService.LoadBitmapAsync("minigames/wiring_bg.webp");
            if (_background != null)
                canvas.DrawBitmap(_background, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
        }

        _time += deltaTime;

        float padding = 12;
        float gap = 6;
        float panelWidth = (bounds.Width - padding * 2 - gap) / 2;
        float panelHeight = bounds.Height - padding * 2;
        float leftPanelX = bounds.Left + padding;
        float rightPanelX = bounds.Left + padding + panelWidth + gap;
        float panelY = bounds.Top + padding;

        // Completion-Erkennung
        DetectCompletion(isAllConnected);

        // Verbindungs-Burst erkennen (neue Verbindung)
        if (connectedCount > _prevConnectedCount && _prevConnectedCount >= 0)
        {
            SpawnConnectionBurst(leftWires, rightWires, leftPanelX, rightPanelX,
                panelWidth, panelY, panelHeight);
        }
        _prevConnectedCount = connectedCount;

        // Error-Erkennung: Kurzschluss-Effekt bei neuem Fehler
        DetectErrors(leftWires, rightWires, leftPanelX, rightPanelX, panelWidth, panelY, panelHeight);

        // Strom-Pulse aktualisieren
        for (int i = 0; i < Math.Min(leftWires.Length, _pulseProgress.Length); i++)
        {
            if (leftWires[i].IsConnected)
                _pulseProgress[i] = (_pulseProgress[i] + deltaTime * 1.5f) % 1.0f;
        }

        // === ZEICHNEN ===

        // 1. Betonwand-Hintergrund
        DrawWallBackground(canvas, bounds);

        // 2. Panels (Sicherungskaesten)
        DrawPanel(canvas, leftPanelX, panelY, panelWidth, panelHeight, "IN", isAllConnected);
        DrawPanel(canvas, rightPanelX, panelY, panelWidth, panelHeight, "OUT", isAllConnected);

        // 3. Kabel in Panels
        DrawWires(canvas, leftPanelX + 8, panelY + 28, panelWidth - 16, panelHeight - 36,
            leftWires, true, selectedLeftIndex);
        DrawWires(canvas, rightPanelX + 8, panelY + 28, panelWidth - 16, panelHeight - 36,
            rightWires, false, null);

        // 4. Bezier-Verbindungslinien mit Strom-Puls
        DrawBezierConnections(canvas, leftPanelX, rightPanelX, panelWidth, panelY, panelHeight,
            leftWires, rightWires);

        // 5. Partikel (Funken + Blitze)
        UpdateAndDrawSparks(canvas, deltaTime);
        UpdateAndDrawBolts(canvas, deltaTime);

        // 6. Completion-Flash-Overlay
        if (_completionStarted)
        {
            _completionTime += deltaTime;
            DrawCompletionFlash(canvas, bounds);
        }
    }

    /// <summary>
    /// Berechnet welches Kabel bei Touch getroffen wurde.
    /// </summary>
    public (bool isLeft, int index) HitTest(SKRect bounds, float touchX, float touchY, int wireCount)
    {
        if (wireCount <= 0) return (false, -1);

        float padding = 12;
        float gap = 6;
        float panelWidth = (bounds.Width - padding * 2 - gap) / 2;
        float panelY = bounds.Top + padding;
        float panelHeight = bounds.Height - padding * 2;
        float wireAreaTop = panelY + 28;
        float wireAreaHeight = panelHeight - 36;
        float wireHeight = Math.Min(50, (wireAreaHeight - (wireCount - 1) * 8) / wireCount);

        float leftPanelX = bounds.Left + padding;
        if (touchX >= leftPanelX && touchX <= leftPanelX + panelWidth)
        {
            for (int i = 0; i < wireCount; i++)
            {
                float wy = wireAreaTop + i * (wireHeight + 8);
                if (touchY >= wy && touchY <= wy + wireHeight)
                    return (true, i);
            }
        }

        float rightPanelX = bounds.Left + padding + panelWidth + gap;
        if (touchX >= rightPanelX && touchX <= rightPanelX + panelWidth)
        {
            for (int i = 0; i < wireCount; i++)
            {
                float wy = wireAreaTop + i * (wireHeight + 8);
                if (touchY >= wy && touchY <= wy + wireHeight)
                    return (false, i);
            }
        }

        return (false, -1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // COMPLETION-ERKENNUNG
    // ═══════════════════════════════════════════════════════════════════

    private void DetectCompletion(bool isAllConnected)
    {
        if (isAllConnected && !_prevAllConnected)
        {
            _completionStarted = true;
            _completionTime = 0;
            // Grosser Funken-Burst
            SpawnCompletionSparks();
        }
        _prevAllConnected = isAllConnected;
    }

    private void DetectErrors(WireRenderData[] leftWires, WireRenderData[] rightWires,
        float leftPanelX, float rightPanelX, float panelWidth, float panelY, float panelHeight)
    {
        float wireAreaTop = panelY + 28;
        float wireAreaHeight = panelHeight - 36;
        int wireCount = rightWires.Length;
        if (wireCount == 0) return;
        float wireHeight = Math.Min(50, (wireAreaHeight - (wireCount - 1) * 8) / wireCount);

        // Rechte Seite pruefen (dort wird HasError gesetzt)
        for (int i = 0; i < Math.Min(rightWires.Length, _prevHasError.Length); i++)
        {
            bool hasError = rightWires[i].HasError;
            if (hasError && !_prevHasError[i])
            {
                // Neuer Fehler! Kurzschluss-Effekt spawnen
                float rightStartX = rightPanelX + 8;
                float wy = wireAreaTop + i * (wireHeight + 8) + wireHeight / 2;
                SpawnErrorEffect(rightStartX + (panelWidth - 16) / 2, wy);
            }
            _prevHasError[i] = hasError;
        }
    }

    private void SpawnCompletionSparks()
    {
        var rng = Random.Shared;
        // 40 goldene Funken aus der Mitte
        for (int i = 0; i < 40 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float speed = 80 + rng.Next(120);
            _sparks[_sparkCount++] = new SparkParticle
            {
                Active = true,
                X = 0, Y = 0, // Wird beim Zeichnen relativ gesetzt
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed,
                Life = 0,
                MaxLife = 0.5f + (float)rng.NextDouble() * 0.5f,
                Size = 2 + rng.Next(3),
                R = 0xFF, G = 0xD5, B = 0x00
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // VERBINDUNGS-BURST (bei neuer Verbindung)
    // ═══════════════════════════════════════════════════════════════════

    private void SpawnConnectionBurst(WireRenderData[] leftWires, WireRenderData[] rightWires,
        float leftPanelX, float rightPanelX, float panelWidth, float panelY, float panelHeight)
    {
        float wireAreaTop = panelY + 28;
        float wireAreaHeight = panelHeight - 36;
        int wireCount = leftWires.Length;
        if (wireCount == 0) return;
        float wireHeight = Math.Min(50, (wireAreaHeight - (wireCount - 1) * 8) / wireCount);
        float leftEndX = leftPanelX + panelWidth;
        float rightStartX = rightPanelX;
        float midX = (leftEndX + rightStartX) / 2;
        var rng = Random.Shared;

        // Finde die zuletzt verbundene Verbindung
        for (int i = 0; i < leftWires.Length; i++)
        {
            if (!leftWires[i].IsConnected) continue;
            var wireColor = WireColors[Math.Min(leftWires[i].ColorIndex, WireColors.Length - 1)];

            for (int j = 0; j < rightWires.Length; j++)
            {
                if (!rightWires[j].IsConnected || rightWires[j].ColorIndex != leftWires[i].ColorIndex)
                    continue;

                float leftY = wireAreaTop + i * (wireHeight + 8) + wireHeight / 2;
                float rightY = wireAreaTop + j * (wireHeight + 8) + wireHeight / 2;
                float midY = leftY + (rightY - leftY) / 2;

                // 15 Funken in der Kabelfarbe
                for (int k = 0; k < 15 && _sparkCount < MAX_SPARKS; k++)
                {
                    float angle = (float)(rng.NextDouble() * Math.PI * 2);
                    float speed = 40 + rng.Next(80);
                    _sparks[_sparkCount++] = new SparkParticle
                    {
                        Active = true,
                        X = midX + rng.Next(-8, 9),
                        Y = midY + rng.Next(-8, 9),
                        VelocityX = (float)Math.Cos(angle) * speed,
                        VelocityY = (float)Math.Sin(angle) * speed - 30,
                        Life = 0,
                        MaxLife = 0.3f + (float)rng.NextDouble() * 0.4f,
                        Size = 2 + rng.Next(2),
                        R = wireColor.Red, G = wireColor.Green, B = wireColor.Blue
                    };
                }
                break;
            }
        }
    }

    /// <summary>
    /// Spawnt Kurzschluss-Blitze und rote Funken bei Fehler-Verbindung.
    /// Wird vom View aufgerufen wenn ein Fehler erkannt wird.
    /// </summary>
    public void SpawnErrorEffect(float x, float y)
    {
        var rng = Random.Shared;

        // 3-4 Zick-Zack-Blitze
        for (int i = 0; i < 4 && _boltCount < MAX_BOLTS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float length = 20 + rng.Next(30);
            _bolts[_boltCount++] = new LightningBolt
            {
                Active = true,
                X1 = x, Y1 = y,
                X2 = x + (float)Math.Cos(angle) * length,
                Y2 = y + (float)Math.Sin(angle) * length,
                Life = 0,
                MaxLife = 0.25f + (float)rng.NextDouble() * 0.15f
            };
        }

        // 10 rote Funken
        for (int i = 0; i < 10 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float speed = 50 + rng.Next(80);
            _sparks[_sparkCount++] = new SparkParticle
            {
                Active = true,
                X = x + rng.Next(-5, 6),
                Y = y + rng.Next(-5, 6),
                VelocityX = (float)Math.Cos(angle) * speed,
                VelocityY = (float)Math.Sin(angle) * speed - 20,
                Life = 0,
                MaxLife = 0.3f + (float)rng.NextDouble() * 0.3f,
                Size = 2 + rng.Next(2),
                R = 0xFF, G = 0x44, B = 0x22
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════

    private void DrawWallBackground(SKCanvas canvas, SKRect bounds)
    {
        // Dunklere Betonwand mit Gradient
        _shaderPaint.Shader?.Dispose();
        _shaderPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Bottom),
            new[] { new SKColor(0x42, 0x42, 0x42), new SKColor(0x35, 0x35, 0x35) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(bounds, _shaderPaint);

        // Fugenlinien (horizontal)
        _thinStrokePaint.Color = WallLine;
        for (float y = bounds.Top + 24; y < bounds.Bottom; y += 24)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _thinStrokePaint);

        // Vertikale Fugen (versetzt pro Reihe)
        _thinStrokePaint.Color = new SKColor(0x3C, 0x3C, 0x3C);
        int row = 0;
        for (float y = bounds.Top; y < bounds.Bottom; y += 24)
        {
            float offset = (row % 2 == 0) ? 0 : 30;
            for (float x = bounds.Left + offset; x < bounds.Right; x += 60)
                canvas.DrawLine(x, y, x, y + 24, _thinStrokePaint);
            row++;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PANEL (Sicherungskasten)
    // ═══════════════════════════════════════════════════════════════════

    private void DrawPanel(SKCanvas canvas, float x, float y, float width, float height,
        string label, bool isAllConnected)
    {
        // Panel-Koerper mit leichtem Gradient
        _shaderPaint.Shader?.Dispose();
        _shaderPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y), new SKPoint(x, y + height),
            new[] { PanelAccent, PanelBg },
            null, SKShaderTileMode.Clamp);
        canvas.DrawRect(x, y, width, height, _shaderPaint);

        // Rand (bei Completion gruen leuchtend)
        _strokePaint.Color = isAllConnected && _completionStarted
            ? new SKColor(0x4C, 0xAF, 0x50, (byte)(180 + 75 * Math.Sin(_time * 4)))
            : PanelBorder;
        _strokePaint.StrokeWidth = isAllConnected && _completionStarted ? 3 : 2;
        canvas.DrawRect(x, y, width, height, _strokePaint);

        // Header-Leiste
        _fillPaint.Color = PanelHighlight;
        canvas.DrawRect(x + 2, y + 2, width - 4, 22, _fillPaint);

        // LED-Indikator im Header (gruen wenn fertig, rot sonst)
        float ledX = x + 10;
        float ledY = y + 13;
        _fillPaintAA.Color = isAllConnected ? new SKColor(0x4C, 0xAF, 0x50) : new SKColor(0xCC, 0x33, 0x33);
        canvas.DrawCircle(ledX, ledY, 4, _fillPaintAA);

        // LED-Glow
        if (isAllConnected)
        {
            float glow = (float)(0.3 + 0.2 * Math.Sin(_time * 6));
            _glowPaint.Color = new SKColor(0x4C, 0xAF, 0x50, (byte)(glow * 255));
            _glowPaint.MaskFilter = _blur4;
            canvas.DrawCircle(ledX, ledY, 8, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Label-Text
        _textPaint.Color = SKColors.White;
        _cachedFont.Size = 11;
        canvas.DrawText(label, x + width / 2, y + 16, SKTextAlign.Center, _cachedFont, _textPaint);

        // Schrauben in den Ecken (Phillips-Kreuzschlitz)
        DrawScrew(canvas, x + 7, y + 7);
        DrawScrew(canvas, x + width - 7, y + 7);
        DrawScrew(canvas, x + 7, y + height - 7);
        DrawScrew(canvas, x + width - 7, y + height - 7);
    }

    private void DrawScrew(SKCanvas canvas, float cx, float cy)
    {
        _fillPaint.Color = new SKColor(0x78, 0x78, 0x78);
        canvas.DrawCircle(cx, cy, 4, _fillPaint);
        _thinStrokePaint.Color = new SKColor(0x55, 0x55, 0x55);
        canvas.DrawLine(cx - 3, cy, cx + 3, cy, _thinStrokePaint);
        canvas.DrawLine(cx, cy - 3, cx, cy + 3, _thinStrokePaint);
    }

    // ═══════════════════════════════════════════════════════════════════
    // KABEL IM PANEL
    // ═══════════════════════════════════════════════════════════════════

    private void DrawWires(SKCanvas canvas, float x, float y, float width, float height,
        WireRenderData[] wires, bool isLeft, int? selectedIndex)
    {
        if (wires.Length == 0) return;
        float wireHeight = Math.Min(50, (height - (wires.Length - 1) * 8) / wires.Length);

        for (int i = 0; i < wires.Length; i++)
        {
            var wire = wires[i];
            float wy = y + i * (wireHeight + 8);
            var wireColor = WireColors[Math.Min(wire.ColorIndex, WireColors.Length - 1)];

            // Status-Hintergrund
            if (wire.IsConnected)
            {
                // Gruener Hintergrund + leichter Puls
                float connAlpha = 40 + 15 * (float)Math.Sin(_time * 3 + i);
                _fillPaint.Color = new SKColor(0x2E, 0x7D, 0x32, (byte)connAlpha);
                canvas.DrawRect(x, wy, width, wireHeight, _fillPaint);
            }
            else if (wire.HasError)
            {
                // Intensiveres rotes Blinken (schnell pulsierend)
                float errAlpha = 40 + 80 * Math.Abs((float)Math.Sin(_time * 12));
                _fillPaint.Color = new SKColor(0xFF, 0x22, 0x22, (byte)errAlpha);
                canvas.DrawRect(x, wy, width, wireHeight, _fillPaint);
            }
            else if (isLeft && selectedIndex == i)
            {
                // Ausgewaehlt: Pulsierender Kabelfarben-Glow
                float pulse = (float)(0.15 + 0.25 * Math.Sin(_time * 6));
                _fillPaint.Color = wireColor.WithAlpha((byte)(pulse * 255));
                canvas.DrawRect(x - 2, wy - 2, width + 4, wireHeight + 4, _fillPaint);

                _fillPaint.Color = SelectedBg;
                canvas.DrawRect(x, wy, width, wireHeight, _fillPaint);
            }

            // Kabel-Rahmen
            _strokePaint.Color = wireColor.WithAlpha(wire.IsConnected ? (byte)255 : (byte)180);
            _strokePaint.StrokeWidth = 2;
            canvas.DrawRect(x + 2, wy + 2, width - 4, wireHeight - 4, _strokePaint);

            // Kabelstrang
            float cableY = wy + wireHeight / 2;
            float cableThickness = 5;
            _fillPaint.Color = wireColor;
            var highlightColor = new SKColor(
                (byte)Math.Min(255, wireColor.Red + 60),
                (byte)Math.Min(255, wireColor.Green + 60),
                (byte)Math.Min(255, wireColor.Blue + 60), 100);

            if (isLeft)
            {
                canvas.DrawRect(x + width * 0.2f, cableY - cableThickness / 2,
                    width * 0.8f, cableThickness, _fillPaint);
                // Glanz-Linie oben auf dem Kabel
                _fillPaint.Color = highlightColor;
                canvas.DrawRect(x + width * 0.2f, cableY - cableThickness / 2,
                    width * 0.8f, 1, _fillPaint);
                // Stecker-Ende (metallisch)
                _shaderPaint.Shader?.Dispose();
                _shaderPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(x + width - 6, cableY - cableThickness),
                    new SKPoint(x + width, cableY + cableThickness),
                    new[] { new SKColor(0x90, 0x90, 0x90), new SKColor(0x60, 0x60, 0x60) },
                    null, SKShaderTileMode.Clamp);
                canvas.DrawRect(x + width - 6, cableY - cableThickness - 1, 6, cableThickness * 2 + 2, _shaderPaint);
            }
            else
            {
                canvas.DrawRect(x, cableY - cableThickness / 2,
                    width * 0.8f, cableThickness, _fillPaint);
                _fillPaint.Color = highlightColor;
                canvas.DrawRect(x, cableY - cableThickness / 2,
                    width * 0.8f, 1, _fillPaint);
                _shaderPaint.Shader?.Dispose();
                _shaderPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(x, cableY - cableThickness),
                    new SKPoint(x + 6, cableY + cableThickness),
                    new[] { new SKColor(0x60, 0x60, 0x60), new SKColor(0x90, 0x90, 0x90) },
                    null, SKShaderTileMode.Clamp);
                canvas.DrawRect(x, cableY - cableThickness - 1, 6, cableThickness * 2 + 2, _shaderPaint);
            }

            // Farbiger Indikator-Kreis
            float dotX = isLeft ? x + 12 : x + width - 12;
            _fillPaintAA.Color = wireColor;
            canvas.DrawCircle(dotX, cableY, 7, _fillPaintAA);
            _fillPaintAA.Color = new SKColor(255, 255, 255, 100);
            canvas.DrawCircle(dotX - 1, cableY - 2, 3, _fillPaintAA);

            // Verbunden: Haekchen-Icon
            if (wire.IsConnected)
            {
                _strokePaintAA.Color = new SKColor(0x4C, 0xAF, 0x50);
                _strokePaintAA.StrokeWidth = 2.5f;
                _strokePaintAA.StrokeCap = SKStrokeCap.Round;
                float cx = x + width / 2;
                float cy = cableY;
                _cachedPath.Reset();
                _cachedPath.MoveTo(cx - 5, cy);
                _cachedPath.LineTo(cx - 1, cy + 4);
                _cachedPath.LineTo(cx + 6, cy - 4);
                canvas.DrawPath(_cachedPath, _strokePaintAA);
            }

            // Isolierung-Streifen
            if (!wire.IsConnected)
            {
                _fillPaint.Color = wireColor.WithAlpha(100);
                float stripeStart = isLeft ? x + width * 0.3f : x + width * 0.1f;
                float stripeEnd = isLeft ? x + width * 0.7f : x + width * 0.5f;
                for (float sx = stripeStart; sx < stripeEnd; sx += 10)
                    canvas.DrawRect(sx, cableY - cableThickness / 2 - 1, 2, cableThickness + 2, _fillPaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // BEZIER-VERBINDUNGEN MIT STROM-PULS
    // ═══════════════════════════════════════════════════════════════════

    private void DrawBezierConnections(SKCanvas canvas, float leftPanelX, float rightPanelX,
        float panelWidth, float panelY, float panelHeight,
        WireRenderData[] leftWires, WireRenderData[] rightWires)
    {
        if (leftWires.Length == 0) return;

        float wireAreaTop = panelY + 28;
        float wireAreaHeight = panelHeight - 36;
        float wireHeight = Math.Min(50, (wireAreaHeight - (leftWires.Length - 1) * 8) / leftWires.Length);
        float leftEndX = leftPanelX + panelWidth;
        float rightStartX = rightPanelX;
        float gapWidth = rightStartX - leftEndX;

        for (int i = 0; i < leftWires.Length; i++)
        {
            if (!leftWires[i].IsConnected) continue;

            var wireColor = WireColors[Math.Min(leftWires[i].ColorIndex, WireColors.Length - 1)];

            for (int j = 0; j < rightWires.Length; j++)
            {
                if (!rightWires[j].IsConnected || rightWires[j].ColorIndex != leftWires[i].ColorIndex)
                    continue;

                float leftY = wireAreaTop + i * (wireHeight + 8) + wireHeight / 2;
                float rightY = wireAreaTop + j * (wireHeight + 8) + wireHeight / 2;

                // Bezier-Kurve statt gerader Linie
                _cachedPath.Reset();
                float cp1X = leftEndX + gapWidth * 0.35f;
                float cp2X = leftEndX + gapWidth * 0.65f;
                _cachedPath.MoveTo(leftEndX, leftY);
                _cachedPath.CubicTo(cp1X, leftY, cp2X, rightY, rightStartX, rightY);

                // Glow-Linie (breit, transparent)
                _strokePaintAA.Color = wireColor.WithAlpha(40);
                _strokePaintAA.StrokeWidth = 8;
                _strokePaintAA.StrokeCap = SKStrokeCap.Butt;
                canvas.DrawPath(_cachedPath, _strokePaintAA);

                // Kabel-Linie
                _strokePaintAA.Color = wireColor.WithAlpha(200);
                _strokePaintAA.StrokeWidth = 3;
                canvas.DrawPath(_cachedPath, _strokePaintAA);

                // Glanz-Linie (duenn, hell)
                _strokePaintAA.Color = new SKColor(255, 255, 255, 50);
                _strokePaintAA.StrokeWidth = 1;
                canvas.DrawPath(_cachedPath, _strokePaintAA);

                // Strom-Puls (wandernder Lichtpunkt entlang der Kurve)
                float pulseT = i < _pulseProgress.Length ? _pulseProgress[i] : 0;
                DrawElectricPulse(canvas, _cachedPath, wireColor, pulseT);

                // Verbindungs-Knoten in der Mitte
                float midX = leftEndX + gapWidth / 2;
                float midY = leftY + (rightY - leftY) / 2;
                _fillPaintAA.Color = wireColor;
                canvas.DrawCircle(midX, midY, 4, _fillPaintAA);
                _glowPaint.Color = wireColor.WithAlpha(60);
                _glowPaint.MaskFilter = _blur3;
                canvas.DrawCircle(midX, midY, 8, _glowPaint);
                _glowPaint.MaskFilter = null;

                break;
            }
        }
    }

    /// <summary>
    /// Zeichnet einen wandernden Strom-Puls entlang einer Bezier-Kurve.
    /// SKPathMeasure muss lokal bleiben (abhaengig vom uebergebenen Path).
    /// </summary>
    private void DrawElectricPulse(SKCanvas canvas, SKPath bezierPath, SKColor wireColor, float t)
    {
        // Position auf der Bezier-Kurve bei t ermitteln
        using var measure = new SKPathMeasure(bezierPath, false);
        float totalLength = measure.Length;
        if (totalLength <= 0) return;

        // Zwei Pulse (vorderer und hinterer)
        for (int p = 0; p < 2; p++)
        {
            float pulseT = (t + p * 0.5f) % 1.0f;

            if (measure.GetPositionAndTangent(pulseT * totalLength, out var pos, out _))
            {
                // Leuchtpunkt
                _fillPaintAA.Color = new SKColor(255, 255, 255, 220);
                canvas.DrawCircle(pos.X, pos.Y, 3, _fillPaintAA);

                // Glow um den Punkt
                _glowPaint.Color = wireColor.WithAlpha(120);
                _glowPaint.MaskFilter = _blur5;
                canvas.DrawCircle(pos.X, pos.Y, 7, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // FUNKEN-PARTIKEL
    // ═══════════════════════════════════════════════════════════════════

    private void UpdateAndDrawSparks(SKCanvas canvas, float deltaTime)
    {
        for (int i = 0; i < _sparkCount; i++)
        {
            ref var p = ref _sparks[i];
            if (!p.Active) continue;

            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.VelocityY += 120 * deltaTime; // Schwerkraft

            if (p.Life >= p.MaxLife)
            {
                p.Active = false;
                continue;
            }

            float alpha = 1 - (p.Life / p.MaxLife);
            float size = p.Size * (0.5f + 0.5f * alpha);

            // Funke zeichnen
            _fillPaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));
            canvas.DrawRect(p.X - size / 2, p.Y - size / 2, size, size, _fillPaint);

            // Heller Kern
            if (alpha > 0.4f)
            {
                _fillPaint.Color = new SKColor(255, 255, 255, (byte)(alpha * 150));
                canvas.DrawRect(p.X, p.Y, 1, 1, _fillPaint);
            }
        }

        // Inaktive Partikel komprimieren
        CompactSparks();
    }

    private void CompactSparks()
    {
        int writeIdx = 0;
        for (int readIdx = 0; readIdx < _sparkCount; readIdx++)
        {
            if (_sparks[readIdx].Active)
            {
                if (writeIdx != readIdx)
                    _sparks[writeIdx] = _sparks[readIdx];
                writeIdx++;
            }
        }
        _sparkCount = writeIdx;
    }

    // ═══════════════════════════════════════════════════════════════════
    // KURZSCHLUSS-BLITZE
    // ═══════════════════════════════════════════════════════════════════

    private void UpdateAndDrawBolts(SKCanvas canvas, float deltaTime)
    {
        for (int i = 0; i < _boltCount; i++)
        {
            ref var b = ref _bolts[i];
            if (!b.Active) continue;

            b.Life += deltaTime;
            if (b.Life >= b.MaxLife)
            {
                b.Active = false;
                continue;
            }

            float alpha = 1 - (b.Life / b.MaxLife);

            // Zick-Zack-Blitz zeichnen (3 Segmente)
            float dx = b.X2 - b.X1;
            float dy = b.Y2 - b.Y1;
            var rng = Random.Shared;

            _cachedPath.Reset();
            _cachedPath.MoveTo(b.X1, b.Y1);

            for (int seg = 1; seg <= 3; seg++)
            {
                float t = seg / 4.0f;
                float nx = b.X1 + dx * t + (rng.Next(-8, 9));
                float ny = b.Y1 + dy * t + (rng.Next(-8, 9));
                if (seg == 3) { nx = b.X2; ny = b.Y2; }
                _cachedPath.LineTo(nx, ny);
            }

            // Glow-Blitz (breit)
            _strokePaintAA.Color = new SKColor(0xFF, 0x33, 0x11, (byte)(alpha * 100));
            _strokePaintAA.StrokeWidth = 5;
            canvas.DrawPath(_cachedPath, _strokePaintAA);

            // Blitz-Linie (schmal)
            _strokePaintAA.Color = new SKColor(0xFF, 0x55, 0x33, (byte)(alpha * 255));
            _strokePaintAA.StrokeWidth = 2;
            canvas.DrawPath(_cachedPath, _strokePaintAA);
        }

        // Komprimieren
        int writeIdx = 0;
        for (int readIdx = 0; readIdx < _boltCount; readIdx++)
        {
            if (_bolts[readIdx].Active)
            {
                if (writeIdx != readIdx)
                    _bolts[writeIdx] = _bolts[readIdx];
                writeIdx++;
            }
        }
        _boltCount = writeIdx;
    }

    // ═══════════════════════════════════════════════════════════════════
    // COMPLETION-FLASH
    // ═══════════════════════════════════════════════════════════════════

    private void DrawCompletionFlash(SKCanvas canvas, SKRect bounds)
    {
        if (_completionTime > 1.5f) return;

        // Phase 1 (0-0.3s): Weisser Flash (quick in, slow out)
        if (_completionTime < 0.3f)
        {
            float flashAlpha = _completionTime < 0.08f
                ? _completionTime / 0.08f
                : 1 - (_completionTime - 0.08f) / 0.22f;
            flashAlpha = Math.Clamp(flashAlpha, 0, 1);

            _fillPaint.Color = new SKColor(255, 255, 255, (byte)(flashAlpha * 120));
            canvas.DrawRect(bounds, _fillPaint);
        }

        // Phase 2 (0.1-1.5s): Gruener Rand-Glow (pulsierend, abklingend)
        if (_completionTime > 0.1f)
        {
            float fade = Math.Clamp(1 - (_completionTime - 0.1f) / 1.4f, 0, 1);
            float pulse = (float)(0.5 + 0.5 * Math.Sin(_completionTime * 8));
            byte alpha = (byte)(fade * pulse * 60);

            _strokePaintAA.Color = new SKColor(0x4C, 0xAF, 0x50, alpha);
            _strokePaintAA.StrokeWidth = 6;
            // Temporaer MaskFilter setzen fuer Glow-Rand
            _strokePaintAA.MaskFilter = _blur4;
            canvas.DrawRect(bounds.Left + 4, bounds.Top + 4,
                bounds.Width - 8, bounds.Height - 8, _strokePaintAA);
            _strokePaintAA.MaskFilter = null; // Zuruecksetzen
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
        _shaderPaint.Dispose();
        _glowPaint.Dispose();
        _blur3.Dispose();
        _blur4.Dispose();
        _blur5.Dispose();
        _cachedPath.Dispose();
        _cachedFont.Dispose();
    }
}

/// <summary>
/// Vereinfachte Kabel-Daten fuer den Renderer.
/// Wird im Code-Behind aus dem ViewModel-Wire extrahiert.
/// </summary>
public struct WireRenderData
{
    /// <summary>Index in WireColors Array (0=Red, 1=Blue, ...)</summary>
    public int ColorIndex;

    /// <summary>Ob das Kabel aktuell ausgewaehlt ist.</summary>
    public bool IsSelected;

    /// <summary>Ob das Kabel erfolgreich verbunden wurde.</summary>
    public bool IsConnected;

    /// <summary>Ob ein Fehl-Versuch angezeigt wird (roter Flash).</summary>
    public bool HasError;
}
