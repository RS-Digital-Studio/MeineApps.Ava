using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// SkiaSharp-Renderer fuer das Streich-Minigame.
/// Zeichnet eine helle Putzwand mit Zellen die gestrichen werden muessen.
/// Hoher Kontrast zwischen ungestrichener Wand (hell) und gestrichener Farbe (satt).
/// Anti-Aliasing fuer glatte Kanten, diagonale Pinselstrich-Textur, Nass-Effekt.
/// Struct-basiertes Partikel-Array (kein GC-Druck auf Android).
/// Completion-Celebration mit goldenem Flash und Extra-Spritzern.
/// </summary>
public class PaintingGameRenderer
{
    // Wand-Farben (hell/creme fuer maximalen Kontrast zu gestrichenen Zellen)
    private static readonly SKColor WallBg = new(0xF5, 0xF0, 0xE8);          // Helle Creme-Wand
    private static readonly SKColor WallLineH = new(0xE8, 0xE0, 0xD0);       // Horizontale Putzfugen
    private static readonly SKColor WallLineV = new(0xEB, 0xE3, 0xD5);       // Vertikale Putzfugen (subtiler)
    private static readonly SKColor CellNormal = new(0xED, 0xE8, 0xDC);      // Saubere ungestrichene Wand
    private static readonly SKColor CellBorder = new(0xC8, 0xBF, 0xA8, 50);  // Zell-Rand

    // Feedback-Farben
    private static readonly SKColor ErrorFlash = new(0xEF, 0x44, 0x44, 120); // Fehler-Rot
    private static readonly SKColor ErrorCross = new(0xEF, 0x44, 0x44);      // X-Markierung
    private static readonly SKColor CheckColor = new(0xFF, 0xFF, 0xFF, 220);  // Haekchen

    // Gold-Farben fuer Completion-Celebration
    private static readonly SKColor GoldLight = new(0xFF, 0xD7, 0x00);       // Gold hell
    private static readonly SKColor GoldDark = new(0xDA, 0xA5, 0x20);        // Gold dunkel

    // Struct-basiertes Partikel-Array (GC-frei auf Android)
    private const int MAX_SPLATTERS = 30;
    private readonly PaintSplatter[] _splatters = new PaintSplatter[MAX_SPLATTERS];
    private int _splatterCount;

    // Animations-Zeit
    private float _animTime;

    // Completion-Celebration Zustand
    private bool _prevAllPainted;
    private float _completionFlashTimer;

    private struct PaintSplatter
    {
        public float X, Y, Size, Life, MaxLife;
        public SKColor Color;
    }

    /// <summary>
    /// Rendert das Streich-Spielfeld.
    /// isAllPainted: true wenn alle Zielzellen gestrichen sind (fuer Completion-Celebration).
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, PaintCellData[] cells, int gridSize,
        SKColor paintColor, bool isPlaying, bool isAllPainted, float deltaTime)
    {
        _animTime += deltaTime;

        // Completion-Celebration erkennen (Flanke: war vorher nicht fertig, jetzt schon)
        if (isAllPainted && !_prevAllPainted)
        {
            _completionFlashTimer = 1.2f;
            SpawnCompletionSplatters(bounds);
        }
        _prevAllPainted = isAllPainted;

        // Completion-Flash-Timer herunterzaehlen
        if (_completionFlashTimer > 0f)
            _completionFlashTimer = MathF.Max(0f, _completionFlashTimer - deltaTime);

        // Tile-Groesse berechnen (quadratisches Grid)
        // Padding 12 pro Seite wegen CornerRadius=8 am Border
        float padding = 12;
        float maxTileSize = Math.Min(
            (bounds.Width - padding * 2) / gridSize,
            (bounds.Height - padding * 2) / gridSize);
        float tileSize = maxTileSize;

        float gridWidth = gridSize * tileSize;
        float gridHeight = gridSize * tileSize;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        // Oben ausrichten statt vertikal zentrieren
        float startY = bounds.Top + padding;

        // 1. Wand-Hintergrund
        DrawWallBackground(canvas, bounds);

        // 2. Zellen zeichnen
        for (int i = 0; i < cells.Length && i < gridSize * gridSize; i++)
        {
            int col = i % gridSize;
            int row = i / gridSize;
            float cx = startX + col * tileSize;
            float cy = startY + row * tileSize;

            DrawCell(canvas, cx, cy, tileSize, cells[i], paintColor, deltaTime);
        }

        // 3. Farbroller-Spritzer ueber den Zellen
        UpdateAndDrawSplatters(canvas, deltaTime);

        // 4. Regenbogen-Lichtreflex oben rechts auf der Wand
        DrawRainbowReflex(canvas, bounds);

        // 5. Completion-Celebration Flash (goldener Gradient ueber alles)
        if (_completionFlashTimer > 0f)
            DrawCompletionFlash(canvas, bounds);
    }

    /// <summary>
    /// Berechnet welche Zelle bei Touch-Koordinaten getroffen wurde.
    /// Gibt -1 zurueck wenn kein Treffer.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int gridSize)
    {
        float padding = 12;
        float maxTileSize = Math.Min(
            (bounds.Width - padding * 2) / gridSize,
            (bounds.Height - padding * 2) / gridSize);
        float tileSize = maxTileSize;

        float gridWidth = gridSize * tileSize;
        float gridHeight = gridSize * tileSize;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        // Oben ausrichten (identisch mit Render)
        float startY = bounds.Top + padding;

        int col = (int)((touchX - startX) / tileSize);
        int row = (int)((touchY - startY) / tileSize);

        if (col < 0 || col >= gridSize || row < 0 || row >= gridSize) return -1;
        return row * gridSize + col;
    }

    /// <summary>
    /// Fuegt Farbspritzer hinzu (bei erfolgreichem Streichen).
    /// Struct-Array mit Kapazitaets-Pruefung statt List (GC-frei).
    /// </summary>
    public void AddSplatter(SKRect bounds, int cellIndex, int gridSize, SKColor color)
    {
        float padding = 12;
        float maxTileSize = Math.Min((bounds.Width - padding * 2) / gridSize, (bounds.Height - padding * 2) / gridSize);
        float tileSize = maxTileSize;
        float gridWidth = gridSize * tileSize;
        float startX = bounds.Left + (bounds.Width - gridWidth) / 2;
        // Oben ausrichten (identisch mit Render)
        float startY = bounds.Top + padding;

        int col = cellIndex % gridSize;
        int row = cellIndex / gridSize;
        float cx = startX + col * tileSize + tileSize / 2;
        float cy = startY + row * tileSize + tileSize / 2;

        var random = Random.Shared;
        for (int i = 0; i < 8; i++)
        {
            if (_splatterCount >= MAX_SPLATTERS) break;

            // Farbvariationen: Heller/dunkler Varianten der Streichfarbe
            byte rVar = (byte)Math.Clamp(color.Red + random.Next(-30, 31), 0, 255);
            byte gVar = (byte)Math.Clamp(color.Green + random.Next(-30, 31), 0, 255);
            byte bVar = (byte)Math.Clamp(color.Blue + random.Next(-30, 31), 0, 255);

            _splatters[_splatterCount++] = new PaintSplatter
            {
                X = cx + random.Next(-25, 26),
                Y = cy + random.Next(-25, 26),
                Size = 4 + random.Next(0, 11),
                Life = 0,
                MaxLife = 1.0f + (float)random.NextDouble() * 0.8f,
                Color = new SKColor(rVar, gVar, bVar)
            };
        }
    }

    /// <summary>
    /// Spawnt 20 goldene Extra-Spritzer ueber das ganze Canvas bei Completion.
    /// </summary>
    private void SpawnCompletionSplatters(SKRect bounds)
    {
        var random = Random.Shared;
        for (int i = 0; i < 20; i++)
        {
            if (_splatterCount >= MAX_SPLATTERS) break;

            // Goldtoene variieren (warm-gelb bis dunkel-gold)
            byte r = (byte)Math.Clamp(0xFF + random.Next(-30, 1), 0, 255);
            byte g = (byte)Math.Clamp(0xC8 + random.Next(-40, 41), 0, 255);
            byte b = (byte)Math.Clamp(0x00 + random.Next(0, 60), 0, 255);

            _splatters[_splatterCount++] = new PaintSplatter
            {
                X = bounds.Left + (float)random.NextDouble() * bounds.Width,
                Y = bounds.Top + (float)random.NextDouble() * bounds.Height,
                Size = 6 + random.Next(0, 14),
                Life = 0,
                MaxLife = 1.5f + (float)random.NextDouble() * 1.0f,
                Color = new SKColor(r, g, b)
            };
        }
    }

    /// <summary>
    /// Zeichnet den Putzwand-Hintergrund mit Kreuzschraffur-Textur und subtiler Vignette.
    /// </summary>
    private void DrawWallBackground(SKCanvas canvas, SKRect bounds)
    {
        // Helle Putzwand-Flaeche
        using var wallPaint = new SKPaint { Color = WallBg, IsAntialias = true };
        canvas.DrawRect(bounds, wallPaint);

        // Kreuzschraffur-Putzstruktur (horizontal + vertikal, sehr subtil)
        using var lineHPaint = new SKPaint { Color = WallLineH.WithAlpha(20), IsAntialias = false, StrokeWidth = 1 };
        for (float y = bounds.Top + 18; y < bounds.Bottom; y += 18)
        {
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, lineHPaint);
        }
        using var lineVPaint = new SKPaint { Color = WallLineV.WithAlpha(15), IsAntialias = false, StrokeWidth = 1 };
        for (float x = bounds.Left + 24; x < bounds.Right; x += 24)
        {
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, lineVPaint);
        }

        // Subtile radiale Vignette (Mitte heller)
        float vigCx = bounds.MidX;
        float vigCy = bounds.MidY;
        float vigR = Math.Max(bounds.Width, bounds.Height) * 0.7f;
        using var vigPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(vigCx, vigCy), vigR,
                [new SKColor(0xFF, 0xFF, 0xFF, 15), new SKColor(0x00, 0x00, 0x00, 12)],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(bounds, vigPaint);
    }

    /// <summary>
    /// Zeichnet eine einzelne Zelle (normal, Ziel, gestrichen oder Fehler).
    /// Zielzellen haben farbigen Ring + pulsierenden Punkt, gestrichene Zellen diagonale Textur.
    /// </summary>
    private void DrawCell(SKCanvas canvas, float x, float y, float size, PaintCellData cell,
        SKColor paintColor, float deltaTime)
    {
        float margin = 2;
        float innerX = x + margin;
        float innerY = y + margin;
        float innerSize = size - margin * 2;

        if (cell.IsPainted)
        {
            // Gestrichen: Satte Farbflaeche als Basis
            using var paintedPaint = new SKPaint { Color = paintColor, IsAntialias = true };
            canvas.DrawRect(innerX, innerY, innerSize, innerSize, paintedPaint);

            // Diagonale Pinselstrich-Textur (realistischer als horizontal)
            using var strokePaint = new SKPaint
            {
                Color = paintColor.WithAlpha(160),
                IsAntialias = true,
                StrokeWidth = 2
            };
            for (float d = -innerSize; d < innerSize * 2; d += 5)
            {
                float x1 = innerX + d;
                float y1 = innerY;
                float x2 = innerX + d + innerSize;
                float y2 = innerY + innerSize;
                // Clipping innerhalb der Zelle
                canvas.Save();
                canvas.ClipRect(new SKRect(innerX, innerY, innerX + innerSize, innerY + innerSize));
                canvas.DrawLine(x1, y1, x2, y2, strokePaint);
                canvas.Restore();
            }

            // Nass-Effekt: Weißer Highlight-Streifen diagonal
            using var wetPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xFF, 0xFF, 40),
                IsAntialias = true,
                StrokeWidth = 3
            };
            canvas.Save();
            canvas.ClipRect(new SKRect(innerX, innerY, innerX + innerSize, innerY + innerSize));
            canvas.DrawLine(innerX + innerSize * 0.2f, innerY + innerSize * 0.1f,
                innerX + innerSize * 0.8f, innerY + innerSize * 0.6f, wetPaint);
            canvas.Restore();

            // Frisch-gestrichen-Flash (kurzer weißer Glow)
            if (cell.PaintedAge < 0.4f)
            {
                float flashAlpha = (1f - cell.PaintedAge / 0.4f) * 0.3f;
                using var flashPaint = new SKPaint
                {
                    Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(flashAlpha * 255)),
                    IsAntialias = true
                };
                canvas.DrawRect(innerX, innerY, innerSize, innerSize, flashPaint);
            }

            // Korrekt-Markierung (Haekchen auf weißem Kreis)
            if (cell.IsCorrect)
            {
                float ccx = innerX + innerSize / 2;
                float ccy = innerY + innerSize / 2;
                float checkR = innerSize * 0.22f;

                // Weißer Kreis-Hintergrund
                using var circlePaint = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 150), IsAntialias = true };
                canvas.DrawCircle(ccx, ccy, checkR, circlePaint);

                // Haekchen (dickere Linien)
                using var checkPaint = new SKPaint
                {
                    Color = CheckColor,
                    IsAntialias = true,
                    StrokeWidth = 3,
                    Style = SKPaintStyle.Stroke,
                    StrokeCap = SKStrokeCap.Round
                };
                canvas.DrawLine(ccx - 6, ccy, ccx - 2, ccy + 5, checkPaint);
                canvas.DrawLine(ccx - 2, ccy + 5, ccx + 6, ccy - 5, checkPaint);
            }
        }
        else if (cell.IsTarget)
        {
            // Zielzelle: Subtiler farbiger Schimmer der Zielfarbe
            using var targetBgPaint = new SKPaint { Color = CellNormal, IsAntialias = true };
            canvas.DrawRect(innerX, innerY, innerSize, innerSize, targetBgPaint);

            // Farbiger Schimmer (paintColor mit niedrigem Alpha)
            using var shimmerPaint = new SKPaint { Color = paintColor.WithAlpha(25), IsAntialias = true };
            canvas.DrawRect(innerX, innerY, innerSize, innerSize, shimmerPaint);

            // Farbiger Ring um die Zelle (paintColor, dickere Linie)
            using var ringPaint = new SKPaint
            {
                Color = paintColor.WithAlpha(120),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3
            };
            canvas.DrawRect(innerX + 1, innerY + 1, innerSize - 2, innerSize - 2, ringPaint);

            // Groesserer pulsierender Markierungspunkt (12px statt 6px)
            float pulse = 0.5f + 0.3f * MathF.Sin(_animTime * 2.5f + x * 0.1f);
            float pointSize = 6;
            using var markPaint = new SKPaint
            {
                Color = paintColor.WithAlpha((byte)(pulse * 200)),
                IsAntialias = true
            };
            canvas.DrawCircle(innerX + innerSize / 2, innerY + innerSize / 2, pointSize, markPaint);

            // Gestrichelter animierter Rand (hoehere Alpha)
            using var dashPaint = new SKPaint
            {
                Color = paintColor.WithAlpha(120),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                PathEffect = SKPathEffect.CreateDash([4, 4], _animTime * 10)
            };
            canvas.DrawRect(innerX + 4, innerY + 4, innerSize - 8, innerSize - 8, dashPaint);
        }
        else
        {
            // Normale Zelle (saubere helle Putzfarbe)
            using var normalPaint = new SKPaint { Color = CellNormal, IsAntialias = true };
            canvas.DrawRect(innerX, innerY, innerSize, innerSize, normalPaint);
        }

        // Fehler-Flash (rotes Overlay mit X-Markierung)
        if (cell.HasError)
        {
            using var errorPaint = new SKPaint { Color = ErrorFlash, IsAntialias = true };
            canvas.DrawRect(innerX, innerY, innerSize, innerSize, errorPaint);

            // X-Markierung
            using var xPaint = new SKPaint
            {
                Color = ErrorCross, IsAntialias = true,
                StrokeWidth = 3, StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(innerX + 8, innerY + 8, innerX + innerSize - 8, innerY + innerSize - 8, xPaint);
            canvas.DrawLine(innerX + innerSize - 8, innerY + 8, innerX + 8, innerY + innerSize - 8, xPaint);
        }

        // Zell-Rand (subtil)
        using var borderPaint = new SKPaint
        {
            Color = CellBorder,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        canvas.DrawRect(innerX, innerY, innerSize, innerSize, borderPaint);
    }

    /// <summary>
    /// Aktualisiert und zeichnet Farbspritzer-Partikel (entstehen bei erfolgreichem Streichen).
    /// Rueckwaerts-Iteration mit Kompaktierung: letztes Element an Stelle des geloeschten kopieren.
    /// </summary>
    private void UpdateAndDrawSplatters(SKCanvas canvas, float deltaTime)
    {
        using var splatPaint = new SKPaint { IsAntialias = true };

        for (int i = _splatterCount - 1; i >= 0; i--)
        {
            var s = _splatters[i];
            s.Life += deltaTime;

            if (s.Life >= s.MaxLife)
            {
                // Kompaktierung: Letztes Element an diese Stelle kopieren
                _splatterCount--;
                if (i < _splatterCount)
                    _splatters[i] = _splatters[_splatterCount];
                continue;
            }
            _splatters[i] = s;

            // Alpha verblasst ueber Lebensdauer
            float alpha = 1 - (s.Life / s.MaxLife);
            splatPaint.Color = s.Color.WithAlpha((byte)(alpha * 200));
            canvas.DrawCircle(s.X, s.Y, s.Size, splatPaint);
        }
    }

    /// <summary>
    /// Zeichnet einen subtilen Regenbogen-Lichtreflex oben rechts auf der Wand.
    /// Simuliert Licht das durch ein Fenster auf die Putzwand faellt.
    /// </summary>
    private void DrawRainbowReflex(SKCanvas canvas, SKRect bounds)
    {
        float reflexRadius = Math.Min(bounds.Width, bounds.Height) * 0.25f;
        float reflexX = bounds.Right - reflexRadius * 0.6f;
        float reflexY = bounds.Top + reflexRadius * 0.4f;

        // Regenbogen-Gradient als Halbkreis mit sehr niedrigem Alpha
        using var reflexPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(reflexX, reflexY), reflexRadius,
                [
                    new SKColor(0xFF, 0x60, 0x60, 12),   // Rot (aussen)
                    new SKColor(0xFF, 0xA0, 0x40, 10),   // Orange
                    new SKColor(0xFF, 0xFF, 0x60, 10),   // Gelb
                    new SKColor(0x60, 0xFF, 0x60, 8),    // Gruen
                    new SKColor(0x60, 0x80, 0xFF, 8),    // Blau
                    new SKColor(0xA0, 0x60, 0xFF, 6),    // Violett
                    new SKColor(0x00, 0x00, 0x00, 0)     // Transparent (Mitte)
                ],
                [0.3f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 1.0f],
                SKShaderTileMode.Clamp)
        };

        // Nur obere rechte Ecke clippen fuer Halbkreis-Effekt
        canvas.Save();
        canvas.ClipRect(new SKRect(
            reflexX - reflexRadius, reflexY - reflexRadius * 0.3f,
            bounds.Right, reflexY + reflexRadius * 0.8f));
        canvas.DrawCircle(reflexX, reflexY, reflexRadius, reflexPaint);
        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet den goldenen Completion-Flash (radialer Gradient der ausblendend ueber das Canvas liegt).
    /// Wird getriggert wenn alle Zielzellen gestrichen sind.
    /// </summary>
    private void DrawCompletionFlash(SKCanvas canvas, SKRect bounds)
    {
        // Fortschritt: 1.0 (Start) -> 0.0 (Ende) ueber 1.2 Sekunden
        float progress = _completionFlashTimer / 1.2f;

        // Alpha schnell einblenden, langsam ausblenden (EaseOut)
        float alpha = progress * progress * 0.4f;

        float centerX = bounds.MidX;
        float centerY = bounds.MidY;
        float maxRadius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * 0.6f;

        // Expandierender Ring-Radius (von 30% auf 100%)
        float currentRadius = maxRadius * (0.3f + 0.7f * (1f - progress));

        using var flashPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(centerX, centerY), currentRadius,
                [
                    GoldLight.WithAlpha((byte)(alpha * 255)),
                    GoldDark.WithAlpha((byte)(alpha * 180)),
                    new SKColor(0xFF, 0xD7, 0x00, 0) // Transparent am Rand
                ],
                [0f, 0.5f, 1.0f],
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(bounds, flashPaint);

        // Goldener Ring am expandierenden Rand (staerkerer Kontrast)
        float ringAlpha = progress * 0.6f;
        using var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            Color = GoldLight.WithAlpha((byte)(ringAlpha * 255))
        };
        canvas.DrawCircle(centerX, centerY, currentRadius * 0.8f, ringPaint);
    }
}

/// <summary>
/// Vereinfachte Zell-Daten fuer den Renderer.
/// Wird aus dem ViewModel (PaintCell) befuellt.
/// </summary>
public struct PaintCellData
{
    public bool IsTarget;
    public bool IsPainted;
    public bool IsCorrect;
    public bool HasError;

    /// <summary>Sekunden seit dem Streichen (fuer Frisch-Effekt). Wird per deltaTime inkrementiert.</summary>
    public float PaintedAge;
}
