using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Daten-Struktur fuer einen einzelnen Dachziegel im Renderer.
/// Wird aus dem ViewModel-RoofTile konvertiert um Rendering von Logik zu trennen.
/// </summary>
public struct RoofTileData
{
    public uint TargetColor;  // ARGB
    public uint DisplayColor; // ARGB
    public bool IsPlaced;
    public bool IsHint;
    public bool HasError;
    public bool IsEmpty;      // Noch nicht belegt und kein Hint
}

/// <summary>
/// AAA SkiaSharp-Renderer fuer das Dachdecken-Minigame.
/// Zeichnet ein realistisches Dach mit Sparren-Struktur, gebogene Dachziegel,
/// Hint-Markierungen (Schloss), Fehler-Blinken und Dachfirst.
/// Struct-basierte Partikel-Arrays fuer GC-freie Android-Performance.
/// Platzierungs-Partikel (Funken in Ziegelfarbe), Holzstaub-Atmosphaere,
/// Completion-Celebration mit goldenem Flash.
/// </summary>
public class RoofTilingRenderer
{
    // Dachstuhl-Farben
    private static readonly SKColor RoofFrameColor = new(0x5D, 0x40, 0x37);   // Holz-Dachstuhl
    private static readonly SKColor RoofFrameLight = new(0x7B, 0x5B, 0x4C);   // Helle Sparren
    private static readonly SKColor RoofFrameDark = new(0x3E, 0x27, 0x23);    // Dunkle Balken
    private static readonly SKColor RidgeColor = new(0x8D, 0x6E, 0x63);       // Dachfirst
    private static readonly SKColor RidgeHighlight = new(0xA1, 0x88, 0x7F);   // First-Glanz
    private static readonly SKColor EmptySlotColor = new(0x3A, 0x3A, 0x3A);   // Leerer Platz
    private static readonly SKColor ErrorFlashColor = new(0xF4, 0x43, 0x36);  // Fehler-Rot
    private static readonly SKColor LockColor = new(0xFF, 0xFF, 0xFF, 0x60);  // Schloss-Symbol
    private static readonly SKColor CheckColor = new(0xFF, 0xFF, 0xFF, 0xD0); // Haekchen
    private static readonly SKColor HintBorder = new(0xFF, 0xD7, 0x00);       // Gold-Rand
    private static readonly SKColor PlacedBorder = new(0x4C, 0xAF, 0x50);     // Gruen-Rand
    private static readonly SKColor DefaultBorder = new(0x55, 0x55, 0x55);    // Grau-Rand

    // ═══════════════════════════════════════════════════════════════════════
    // Partikel-System (Struct-basiert, kein GC)
    // ═══════════════════════════════════════════════════════════════════════

    private const int MAX_TILES = 30;
    private const int MAX_SPARKS = 40;
    private const int MAX_DUST = 10;

    // Fehler-Blink-Timer pro Tile (Array statt Dictionary)
    private readonly float[] _errorBlinkTimers = new float[MAX_TILES];

    // Platzierungs-Funken (Partikel in Ziegelfarbe)
    private struct SparkParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte R, G, B;
    }

    private readonly SparkParticle[] _sparks = new SparkParticle[MAX_SPARKS];
    private int _sparkCount;

    // Atmosphaerischer Holzstaub
    private struct DustParticle
    {
        public float X, Y, VelocityX, VelocityY, Life, MaxLife, Size;
        public byte Alpha;
    }

    private readonly DustParticle[] _dust = new DustParticle[MAX_DUST];
    private int _dustCount;

    // Zustandsverfolgung
    private int _prevPlacedCount;
    private bool _prevAllPlaced;
    private float _completionFlashTimer;

    // Animationszeit
    private float _time;

    /// <summary>
    /// Rendert das gesamte Dach-Grid auf das Canvas.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, RoofTileData[] tiles, int cols, int rows,
        int placedCount, int totalSlots, float deltaTime)
    {
        _time += deltaTime;

        if (tiles.Length == 0 || cols == 0 || rows == 0) return;

        float padding = 12;
        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding;

        // Dachfirst-Hoehe reservieren
        float ridgeHeight = 16;
        float gridTop = bounds.Top + padding + ridgeHeight + 4;
        float gridHeight = availableHeight - ridgeHeight - 8;

        // Ziegel-Groesse berechnen (mit Abstand fuer Dachziegel-Ueberlappung)
        float tileSpacing = 3;
        float tileWidth = (availableWidth - (cols - 1) * tileSpacing) / cols;
        float tileHeight = (gridHeight - (rows - 1) * tileSpacing) / rows;

        // Grid zentrieren
        float totalGridWidth = cols * tileWidth + (cols - 1) * tileSpacing;
        float totalGridHeight = rows * tileHeight + (rows - 1) * tileSpacing;
        float gridLeft = bounds.Left + (bounds.Width - totalGridWidth) / 2;
        // Oben ausrichten statt vertikal zentrieren
        gridTop = bounds.Top + padding + ridgeHeight + 4;

        // 1. Hintergrund: Holz-Dachstuhl
        DrawRoofFrame(canvas, bounds, gridLeft, gridTop, totalGridWidth, totalGridHeight, ridgeHeight, cols);

        // 2. Dachfirst (dekoratives Element oben)
        DrawRidge(canvas, gridLeft, gridTop - ridgeHeight - 2, totalGridWidth, ridgeHeight);

        // 3. Holzstaub-Partikel (hinter den Ziegeln)
        UpdateAndDrawDust(canvas, bounds, gridLeft, gridTop, totalGridWidth, totalGridHeight, deltaTime);

        // 4. Ziegel zeichnen
        DrawTiles(canvas, tiles, cols, rows, gridLeft, gridTop, tileWidth, tileHeight, tileSpacing, deltaTime);

        // 5. Fehler-Blinks aktualisieren
        UpdateErrorBlinks(tiles, deltaTime);

        // 6. Platzierungs-Partikel pruefen und spawnen
        DetectNewPlacements(tiles, placedCount, gridLeft, gridTop, tileWidth, tileHeight, tileSpacing, cols);

        // 7. Platzierungs-Funken zeichnen
        UpdateAndDrawSparks(canvas, deltaTime);

        // 8. Completion-Celebration
        bool allPlaced = totalSlots > 0 && placedCount >= totalSlots;
        if (allPlaced && !_prevAllPlaced)
        {
            _completionFlashTimer = 1.5f; // 1.5s goldener Flash
        }
        _prevAllPlaced = allPlaced;

        if (_completionFlashTimer > 0)
        {
            DrawCompletionFlash(canvas, gridLeft, gridTop, totalGridWidth, totalGridHeight, ridgeHeight);
            _completionFlashTimer -= deltaTime;
        }
    }

    /// <summary>
    /// Prueft welcher Ziegel an der Touch-Position liegt.
    /// Gibt den Index zurueck oder -1 wenn kein Ziegel getroffen wurde.
    /// </summary>
    public int HitTest(SKRect bounds, float touchX, float touchY, int cols, int rows)
    {
        if (cols == 0 || rows == 0) return -1;

        float padding = 12;
        float availableWidth = bounds.Width - 2 * padding;
        float availableHeight = bounds.Height - 2 * padding;

        float ridgeHeight = 16;
        float gridHeight = availableHeight - ridgeHeight - 8;

        float tileSpacing = 3;
        float tileWidth = (availableWidth - (cols - 1) * tileSpacing) / cols;
        float tileHeight = (gridHeight - (rows - 1) * tileSpacing) / rows;

        float totalGridWidth = cols * tileWidth + (cols - 1) * tileSpacing;
        float gridLeft = bounds.Left + (bounds.Width - totalGridWidth) / 2;
        // Oben ausrichten (identisch mit Render)
        float gridTop = bounds.Top + padding + ridgeHeight + 4;

        // Pruefe ob Touch innerhalb des Grids liegt
        if (touchX < gridLeft || touchX > gridLeft + totalGridWidth) return -1;
        float totalGridHeight = rows * tileHeight + (rows - 1) * tileSpacing;
        if (touchY < gridTop || touchY > gridTop + totalGridHeight) return -1;

        // Spalte und Zeile berechnen
        float relX = touchX - gridLeft;
        float relY = touchY - gridTop;

        int col = (int)(relX / (tileWidth + tileSpacing));
        int row = (int)(relY / (tileHeight + tileSpacing));

        // Grenzen pruefen
        col = Math.Clamp(col, 0, cols - 1);
        row = Math.Clamp(row, 0, rows - 1);

        // Pruefen ob Touch tatsaechlich auf dem Ziegel liegt (nicht im Spacing)
        float tileLeft = gridLeft + col * (tileWidth + tileSpacing);
        float tileTop = gridTop + row * (tileHeight + tileSpacing);
        if (touchX > tileLeft + tileWidth || touchY > tileTop + tileHeight) return -1;

        return row * cols + col;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet den Holz-Dachstuhl als Hintergrund.
    /// Diagonale Sparren und horizontale Latten.
    /// </summary>
    private void DrawRoofFrame(SKCanvas canvas, SKRect bounds, float gridLeft, float gridTop,
        float gridWidth, float gridHeight, float ridgeHeight, int cols)
    {
        // Dachstuhl-Hintergrund (dunkles Holz)
        float frameLeft = gridLeft - 8;
        float frameTop = gridTop - ridgeHeight - 8;
        float frameWidth = gridWidth + 16;
        float frameHeight = gridHeight + ridgeHeight + 16;

        using var framePaint = new SKPaint { Color = RoofFrameColor, IsAntialias = false };
        canvas.DrawRect(frameLeft, frameTop, frameWidth, frameHeight, framePaint);

        // Diagonale Sparren (links-oben nach rechts-unten)
        using var sparrenPaint = new SKPaint
        {
            Color = RoofFrameLight,
            IsAntialias = false,
            StrokeWidth = 4
        };

        float sparrenSpacing = gridWidth / Math.Max(2, cols - 1);
        for (float sx = frameLeft - frameHeight; sx < frameLeft + frameWidth; sx += sparrenSpacing)
        {
            canvas.DrawLine(sx, frameTop, sx + frameHeight * 0.7f, frameTop + frameHeight, sparrenPaint);
        }

        // Gegenlaueufige Sparren (rechts-oben nach links-unten)
        using var sparrenPaint2 = new SKPaint
        {
            Color = RoofFrameDark.WithAlpha(80),
            IsAntialias = false,
            StrokeWidth = 2
        };

        for (float sx = frameLeft; sx < frameLeft + frameWidth + frameHeight; sx += sparrenSpacing * 1.5f)
        {
            canvas.DrawLine(sx, frameTop, sx - frameHeight * 0.5f, frameTop + frameHeight, sparrenPaint2);
        }

        // Horizontale Dachlatten
        using var lattenPaint = new SKPaint
        {
            Color = RoofFrameLight.WithAlpha(60),
            IsAntialias = false,
            StrokeWidth = 2
        };

        for (float ly = frameTop + 20; ly < frameTop + frameHeight; ly += 24)
        {
            canvas.DrawLine(frameLeft, ly, frameLeft + frameWidth, ly, lattenPaint);
        }
    }

    /// <summary>
    /// Zeichnet den dekorativen Dachfirst oben.
    /// </summary>
    private void DrawRidge(SKCanvas canvas, float x, float y, float width, float height)
    {
        // Hauptkoerper des Firsts
        using var ridgePaint = new SKPaint { Color = RidgeColor, IsAntialias = false };
        canvas.DrawRect(x - 4, y, width + 8, height, ridgePaint);

        // Obere Kante (heller Glanz)
        using var highlightPaint = new SKPaint { Color = RidgeHighlight, IsAntialias = false };
        canvas.DrawRect(x - 4, y, width + 8, 4, highlightPaint);

        // Untere Schattenkante
        using var shadowPaint = new SKPaint { Color = RoofFrameDark, IsAntialias = false };
        canvas.DrawRect(x - 4, y + height - 2, width + 8, 2, shadowPaint);

        // First-Ziegel Segmente (vertikale Trennlinien)
        using var segmentPaint = new SKPaint
        {
            Color = RoofFrameDark.WithAlpha(80),
            IsAntialias = false,
            StrokeWidth = 1
        };

        float segmentWidth = width / 8;
        for (float sx = x + segmentWidth; sx < x + width; sx += segmentWidth)
        {
            canvas.DrawLine(sx, y + 2, sx, y + height - 2, segmentPaint);
        }

        // Dekorative Firstkappe-Punkte
        using var capPaint = new SKPaint { Color = new SKColor(0xBC, 0x8F, 0x6B), IsAntialias = false };
        for (float cx = x + segmentWidth / 2; cx < x + width; cx += segmentWidth)
        {
            canvas.DrawRect(cx - 2, y + height / 2 - 2, 4, 4, capPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ZIEGEL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet alle Dachziegel im Grid.
    /// </summary>
    private void DrawTiles(SKCanvas canvas, RoofTileData[] tiles, int cols, int rows,
        float gridLeft, float gridTop, float tileWidth, float tileHeight, float spacing, float deltaTime)
    {
        using var tilePaint = new SKPaint { IsAntialias = false };
        using var borderPaint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        using var highlightPaint = new SKPaint { IsAntialias = false };
        using var shadowPaint = new SKPaint { IsAntialias = false };
        using var iconPaint = new SKPaint { IsAntialias = false, StrokeWidth = 2 };

        for (int i = 0; i < tiles.Length && i < cols * rows; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var tile = tiles[i];

            float tx = gridLeft + col * (tileWidth + spacing);
            float ty = gridTop + row * (tileHeight + spacing);

            // Dachziegel-Versatz: Ungerade Reihen leicht nach rechts versetzt
            if (row % 2 == 1)
            {
                tx += tileWidth * 0.15f;
            }

            // Ziegel-Farbe bestimmen
            SKColor tileColor;
            if (tile.IsEmpty)
            {
                tileColor = EmptySlotColor;
            }
            else
            {
                tileColor = new SKColor(tile.DisplayColor);
            }

            // Fehler-Blink-Effekt (Array-basiert)
            bool isBlinking = i < MAX_TILES && _errorBlinkTimers[i] > 0;
            if (tile.HasError || isBlinking)
            {
                float blinkPhase = (float)Math.Sin(_time * 12) * 0.5f + 0.5f;
                tileColor = BlendColors(tileColor, ErrorFlashColor, blinkPhase * 0.6f);

                if (i < MAX_TILES && _errorBlinkTimers[i] <= 0)
                    _errorBlinkTimers[i] = 0.5f; // 0.5s Blink-Dauer
            }

            // --- Dachziegel zeichnen (leicht gebogene Optik) ---

            // Hauptflaeche des Ziegels
            tilePaint.Color = tileColor;
            canvas.DrawRect(tx, ty, tileWidth, tileHeight, tilePaint);

            // Obere Kante heller (Glanz-Highlight wie gebogener Ziegel)
            if (!tile.IsEmpty)
            {
                float highlightHeight = Math.Max(3, tileHeight * 0.2f);
                highlightPaint.Color = tileColor.WithAlpha(255);
                var lighter = LightenColor(tileColor, 0.25f);
                highlightPaint.Color = lighter;
                canvas.DrawRect(tx + 1, ty, tileWidth - 2, highlightHeight, highlightPaint);

                // Zweiter Glanz-Streifen (subtiler, fuer gewoelbte Optik)
                highlightPaint.Color = lighter.WithAlpha(100);
                canvas.DrawRect(tx + 2, ty + 1, tileWidth - 4, 2, highlightPaint);
            }

            // Untere Kante dunkler (Schatten)
            if (!tile.IsEmpty)
            {
                float shadowHeight = Math.Max(2, tileHeight * 0.12f);
                var darker = DarkenColor(tileColor, 0.3f);
                shadowPaint.Color = darker;
                canvas.DrawRect(tx, ty + tileHeight - shadowHeight, tileWidth, shadowHeight, shadowPaint);
            }

            // Seitliche Kanten (leichter Schatten links/rechts)
            if (!tile.IsEmpty)
            {
                var sideShadow = DarkenColor(tileColor, 0.15f);
                shadowPaint.Color = sideShadow;
                canvas.DrawRect(tx, ty, 2, tileHeight, shadowPaint);
                canvas.DrawRect(tx + tileWidth - 2, ty, 2, tileHeight, shadowPaint);
            }

            // Rand-Farbe je nach Zustand
            if (tile.IsHint)
            {
                borderPaint.Color = HintBorder;
            }
            else if (tile.HasError)
            {
                borderPaint.Color = ErrorFlashColor;
            }
            else if (tile.IsPlaced)
            {
                borderPaint.Color = PlacedBorder;
            }
            else
            {
                borderPaint.Color = DefaultBorder;
            }

            canvas.DrawRect(tx, ty, tileWidth, tileHeight, borderPaint);

            // --- Icons auf dem Ziegel ---

            float centerX = tx + tileWidth / 2;
            float centerY = ty + tileHeight / 2;
            float iconSize = Math.Min(tileWidth, tileHeight) * 0.3f;

            if (tile.IsHint)
            {
                // Schloss-Symbol fuer vorplatzierte Ziegel
                DrawLockIcon(canvas, centerX, centerY, iconSize, iconPaint);
            }
            else if (tile.IsPlaced && !tile.HasError)
            {
                // Haekchen fuer korrekt platzierte Ziegel
                DrawCheckIcon(canvas, centerX, centerY, iconSize, iconPaint);
            }
            else if (tile.IsEmpty)
            {
                // Leerer Platz: Dezenter Punkt als Hinweis
                tilePaint.Color = new SKColor(0x60, 0x60, 0x60);
                canvas.DrawCircle(centerX, centerY, 3, tilePaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet ein kleines Schloss-Symbol (Pixel-Art).
    /// </summary>
    private static void DrawLockIcon(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        paint.Color = LockColor;
        paint.Style = SKPaintStyle.Fill;

        // Schloss-Koerper (Rechteck)
        float bodyW = size * 1.2f;
        float bodyH = size * 0.9f;
        canvas.DrawRect(cx - bodyW / 2, cy - bodyH / 4, bodyW, bodyH, paint);

        // Schloss-Buegel (oben, halbkreisfoermig per Pixel-Bloecke)
        float buegelW = size * 0.7f;
        float buegelH = size * 0.6f;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        canvas.DrawRect(cx - buegelW / 2, cy - bodyH / 4 - buegelH, buegelW, buegelH, paint);

        // Schlueselloch (dunkler Punkt in der Mitte)
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(0x00, 0x00, 0x00, 0x60);
        canvas.DrawCircle(cx, cy + bodyH * 0.15f, 2, paint);
    }

    /// <summary>
    /// Zeichnet ein Haekchen-Symbol (Pixel-Art).
    /// </summary>
    private static void DrawCheckIcon(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        paint.Color = CheckColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = Math.Max(2, size * 0.3f);
        paint.StrokeCap = SKStrokeCap.Square;

        // Haekchen: zwei Linien
        float halfSize = size * 0.6f;
        canvas.DrawLine(
            cx - halfSize * 0.5f, cy,
            cx - halfSize * 0.1f, cy + halfSize * 0.5f,
            paint);
        canvas.DrawLine(
            cx - halfSize * 0.1f, cy + halfSize * 0.5f,
            cx + halfSize * 0.5f, cy - halfSize * 0.4f,
            paint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FEHLER-BLINK (Array-basiert)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert Fehler-Blink-Timer (Array statt Dictionary).
    /// </summary>
    private void UpdateErrorBlinks(RoofTileData[] tiles, float deltaTime)
    {
        for (int i = 0; i < tiles.Length && i < MAX_TILES; i++)
        {
            // Neue Fehler registrieren
            if (tiles[i].HasError && _errorBlinkTimers[i] <= 0)
            {
                _errorBlinkTimers[i] = 0.5f;
            }

            // Timer runterzaehlen
            if (_errorBlinkTimers[i] > 0)
            {
                _errorBlinkTimers[i] -= deltaTime;
                if (_errorBlinkTimers[i] < 0) _errorBlinkTimers[i] = 0;

                // Stoppen wenn Fehler nicht mehr aktiv
                if (!tiles[i].HasError)
                    _errorBlinkTimers[i] = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLATZIERUNGS-PARTIKEL (Funken in Ziegelfarbe)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erkennt neu platzierte Ziegel und spawnt Funken-Partikel.
    /// </summary>
    private void DetectNewPlacements(RoofTileData[] tiles, int placedCount,
        float gridLeft, float gridTop, float tileWidth, float tileHeight, float spacing, int cols)
    {
        if (placedCount > _prevPlacedCount && _prevPlacedCount >= 0)
        {
            // Neuen platzierten Ziegel finden (letzten mit IsPlaced der kein Hint ist)
            for (int i = tiles.Length - 1; i >= 0; i--)
            {
                if (tiles[i].IsPlaced && !tiles[i].IsHint)
                {
                    int row = i / cols;
                    int col = i % cols;
                    float tx = gridLeft + col * (tileWidth + spacing) + tileWidth / 2;
                    float ty = gridTop + row * (tileHeight + spacing) + tileHeight / 2;

                    // Versatz fuer ungerade Reihen
                    if (row % 2 == 1) tx += tileWidth * 0.15f;

                    // Ziegelfarbe extrahieren
                    var color = new SKColor(tiles[i].DisplayColor);

                    // 8 Funken in Ziegelfarbe spawnen
                    SpawnPlacementSparks(tx, ty, color);
                    break;
                }
            }
        }
        _prevPlacedCount = placedCount;
    }

    /// <summary>
    /// Spawnt 8 Funken vom Ziegel-Mittelpunkt in Ziegelfarbe.
    /// </summary>
    private void SpawnPlacementSparks(float cx, float cy, SKColor color)
    {
        var rng = Random.Shared;
        for (int i = 0; i < 8 && _sparkCount < MAX_SPARKS; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float speed = 40 + (float)(rng.NextDouble() * 60);

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
            p.VelocityY += 100 * deltaTime; // Schwerkraft
            p.VelocityX *= 0.97f; // Luftwiderstand

            if (p.Life >= p.MaxLife)
            {
                // Entfernen durch Kompaktierung (letztes Element an geloeschte Stelle)
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
    // HOLZSTAUB-ATMOSPHAERE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert und zeichnet atmosphaerische Holzstaub-Partikel ueber dem Dachstuhl.
    /// </summary>
    private void UpdateAndDrawDust(SKCanvas canvas, SKRect bounds, float gridLeft, float gridTop,
        float gridWidth, float gridHeight, float deltaTime)
    {
        var rng = Random.Shared;

        // Neue Partikel erzeugen (langsam, atmosphaerisch)
        if (_dustCount < MAX_DUST && rng.NextDouble() < 0.3 * deltaTime * 10)
        {
            _dust[_dustCount++] = new DustParticle
            {
                X = gridLeft + (float)(rng.NextDouble() * gridWidth),
                Y = gridTop + (float)(rng.NextDouble() * gridHeight),
                VelocityX = (float)(rng.NextDouble() - 0.5) * 8,
                VelocityY = -5 - (float)(rng.NextDouble() * 8),
                Life = 0,
                MaxLife = 3.0f + (float)(rng.NextDouble() * 3.0f),
                Size = 1 + (float)(rng.NextDouble() * 2),
                Alpha = (byte)(30 + rng.Next(0, 40))
            };
        }

        using var dustPaint = new SKPaint { IsAntialias = false };

        for (int i = 0; i < _dustCount; i++)
        {
            var p = _dust[i];
            p.Life += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;

            // Leichte horizontale Drift (Wind-Effekt)
            p.VelocityX += (float)(Math.Sin(_time * 0.5 + i) * 1.5) * deltaTime;

            if (p.Life >= p.MaxLife)
            {
                _dust[i] = _dust[--_dustCount];
                i--;
                continue;
            }

            _dust[i] = p;

            // Alpha: Fade-In/Fade-Out
            float lifeRatio = p.Life / p.MaxLife;
            float alpha = lifeRatio < 0.15f
                ? lifeRatio / 0.15f
                : 1.0f - (lifeRatio - 0.15f) / 0.85f;
            byte finalAlpha = (byte)(p.Alpha * alpha);

            // Warme Holzstaub-Farbe
            dustPaint.Color = new SKColor(0xA0, 0x8B, 0x70, finalAlpha);
            canvas.DrawRect(p.X, p.Y, p.Size, p.Size, dustPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMPLETION-CELEBRATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Zeichnet einen goldenen Flash ueber dem gesamten Dach wenn alle Ziegel platziert sind.
    /// </summary>
    private void DrawCompletionFlash(SKCanvas canvas, float gridLeft, float gridTop,
        float gridWidth, float gridHeight, float ridgeHeight)
    {
        float intensity = _completionFlashTimer / 1.5f; // 0-1, nimmt ab
        float pulse = (float)(0.5 + 0.5 * Math.Sin(_time * 8));

        // Goldener Glow ueber das gesamte Dach
        byte alpha = (byte)(intensity * pulse * 80);
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, alpha),
            IsAntialias = false
        };
        canvas.DrawRect(gridLeft - 8, gridTop - ridgeHeight - 8,
            gridWidth + 16, gridHeight + ridgeHeight + 16, glowPaint);

        // Goldene Randlinie (staerker)
        byte borderAlpha = (byte)(intensity * 180);
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xD7, 0x00, borderAlpha),
            IsAntialias = false,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawRect(gridLeft - 10, gridTop - ridgeHeight - 10,
            gridWidth + 20, gridHeight + ridgeHeight + 20, borderPaint);

        // Goldene Sterne oben (3 Stueck, versetzt animiert)
        if (intensity > 0.3f)
        {
            using var starPaint = new SKPaint
            {
                Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(intensity * 220)),
                IsAntialias = true
            };

            float centerX = gridLeft + gridWidth / 2;
            float starY = gridTop - ridgeHeight - 20;
            float starSize = 6 + pulse * 3;

            // 3 Sterne nebeneinander
            for (int s = -1; s <= 1; s++)
            {
                float sx = centerX + s * 25;
                float sOffset = (float)Math.Sin(_time * 4 + s * 1.2f) * 2;
                DrawStar(canvas, sx, starY + sOffset, starSize, starPaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet einen einfachen 4-zackigen Stern.
    /// </summary>
    private static void DrawStar(SKCanvas canvas, float cx, float cy, float size, SKPaint paint)
    {
        // 4-zackiger Stern aus Rauten
        canvas.DrawRect(cx - size / 2, cy - 1, size, 2, paint);
        canvas.DrawRect(cx - 1, cy - size / 2, 2, size, paint);
        // Diagonale Zacken (kleiner)
        float d = size * 0.35f;
        canvas.DrawRect(cx - d, cy - d, d * 0.7f, d * 0.7f, paint);
        canvas.DrawRect(cx + d * 0.3f, cy - d, d * 0.7f, d * 0.7f, paint);
        canvas.DrawRect(cx - d, cy + d * 0.3f, d * 0.7f, d * 0.7f, paint);
        canvas.DrawRect(cx + d * 0.3f, cy + d * 0.3f, d * 0.7f, d * 0.7f, paint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSFUNKTIONEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mischt zwei Farben mit gegebenem Faktor (0 = color1, 1 = color2).
    /// </summary>
    private static SKColor BlendColors(SKColor color1, SKColor color2, float factor)
    {
        factor = Math.Clamp(factor, 0, 1);
        byte r = (byte)(color1.Red + (color2.Red - color1.Red) * factor);
        byte g = (byte)(color1.Green + (color2.Green - color1.Green) * factor);
        byte b = (byte)(color1.Blue + (color2.Blue - color1.Blue) * factor);
        byte a = (byte)(color1.Alpha + (color2.Alpha - color1.Alpha) * factor);
        return new SKColor(r, g, b, a);
    }

    /// <summary>
    /// Hellt eine Farbe auf.
    /// </summary>
    private static SKColor LightenColor(SKColor color, float amount)
    {
        byte r = (byte)Math.Min(255, color.Red + (255 - color.Red) * amount);
        byte g = (byte)Math.Min(255, color.Green + (255 - color.Green) * amount);
        byte b = (byte)Math.Min(255, color.Blue + (255 - color.Blue) * amount);
        return new SKColor(r, g, b, color.Alpha);
    }

    /// <summary>
    /// Verdunkelt eine Farbe.
    /// </summary>
    private static SKColor DarkenColor(SKColor color, float amount)
    {
        byte r = (byte)(color.Red * (1 - amount));
        byte g = (byte)(color.Green * (1 - amount));
        byte b = (byte)(color.Blue * (1 - amount));
        return new SKColor(r, g, b, color.Alpha);
    }
}
