using SkiaSharp;
using HandwerkerImperium.Models;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Rendert die Gilden-Hauptquartier-Szene: Isometrisches 8x6 Grid mit Gras/Stein-Tiles,
/// prozedural gezeichnete Gebäude (Y-sortiert), Rauch-Partikel, Fenster-Glow, Fahne.
/// Terrain + Gebäude auf Offscreen-Bitmap mit Dirty-Flag für Performance.
/// </summary>
public sealed class GuildHallSceneRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Grid-Konstanten
    private const int GridW = 8;
    private const int GridH = 6;
    private const float TileW = 64;
    private const float TileH = 32;

    // Offscreen-Cache
    private SKBitmap? _cachedTerrain;
    private bool _terrainDirty = true;
    private float _lastWidth, _lastHeight;

    // Rauch-Partikel (Struct-Pool)
    private const int MaxSmoke = 12;
    private readonly SmokeParticle[] _smokeParticles = new SmokeParticle[MaxSmoke];
    private int _smokeCount;
    private float _smokeTimer;

    // Gebäude-Daten
    private IReadOnlyList<GuildBuildingDisplay>? _buildings;
    private int _hallLevel;

    // Gecachte Paints
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _strokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKFont _labelFont = new() { Edging = SKFontEdging.Antialias };
    private readonly SKPath _buildingPath = new();

    // Tile-Farben
    private static readonly SKColor GrassBase = new(0x2D, 0x5A, 0x27);
    private static readonly SKColor GrassDark = new(0x23, 0x4A, 0x1F);
    private static readonly SKColor StonePath = new(0x5A, 0x4C, 0x3E);
    private static readonly SKColor StonePathDark = new(0x4A, 0x3C, 0x2E);

    // Gebäude-Positionen (isometrisch, fest)
    private static readonly (int x, int y)[] BuildingPositions =
    [
        (2, 1), (5, 1), (1, 3), (4, 3), (7, 3),
        (2, 5), (5, 5), (0, 2), (7, 2), (3, 4)
    ];

    private struct SmokeParticle
    {
        public float X, Y, Size, Life, Speed, Alpha;
    }

    /// <summary>Setzt Gebäude-Daten. Markiert Terrain als dirty.</summary>
    public void SetData(IReadOnlyList<GuildBuildingDisplay>? buildings, int hallLevel)
    {
        _buildings = buildings;
        _hallLevel = hallLevel;
        _terrainDirty = true;
    }

    /// <summary>
    /// Rendert die Szene. deltaTime in Sekunden.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float deltaTime)
    {
        _time += deltaTime;

        float w = bounds.Width;
        float h = bounds.Height;

        // Offscreen-Terrain bei Bedarf neu zeichnen
        // ReSharper disable CompareOfFloatsByEqualityOperator
        if (_terrainDirty || _cachedTerrain == null || _lastWidth != w || _lastHeight != h)
        {
            RebuildTerrainCache(w, h);
            _lastWidth = w;
            _lastHeight = h;
            _terrainDirty = false;
        }
        // ReSharper restore CompareOfFloatsByEqualityOperator

        // 1. Terrain (gecacht)
        if (_cachedTerrain != null)
            canvas.DrawBitmap(_cachedTerrain, 0, 0);

        // 2. Animierte Elemente (pro Frame)
        float originX = w / 2;
        float originY = 20;

        // Fenster-Glow auf Gebäuden
        DrawWindowGlows(canvas, originX, originY);

        // Fahne am Hauptgebäude
        DrawFlag(canvas, originX, originY);

        // 3. Rauch-Partikel
        UpdateAndDrawSmoke(canvas, originX, originY, deltaTime);
    }

    /// <summary>Gibt den Index des Gebäudes zurück, das bei (x,y) getroffen wurde. -1 wenn keins.</summary>
    public int HitTest(float x, float y, float width)
    {
        if (_buildings == null) return -1;

        float originX = width / 2;
        float originY = 20;

        // Rückwärts iterieren (oberstes Gebäude zuerst)
        int count = Math.Min(_buildings.Count, BuildingPositions.Length);
        for (int i = count - 1; i >= 0; i--)
        {
            if (!_buildings[i].IsUnlocked) continue;
            var (gx, gy) = BuildingPositions[i];
            var (sx, sy) = IsoToScreen(gx, gy, originX, originY);

            // Einfacher Bounding-Box-Test
            float bw = TileW * 0.8f;
            float bh = TileH * 2.5f;
            if (x >= sx - bw / 2 && x <= sx + bw / 2 &&
                y >= sy - bh && y <= sy)
            {
                return i;
            }
        }
        return -1;
    }

    private void RebuildTerrainCache(float w, float h)
    {
        _cachedTerrain?.Dispose();
        _cachedTerrain = new SKBitmap((int)w, (int)h);
        using var terrainCanvas = new SKCanvas(_cachedTerrain);
        terrainCanvas.Clear(SKColors.Transparent);

        float originX = w / 2;
        float originY = 20;

        // Terrain-Tiles
        for (int gy = 0; gy < GridH; gy++)
        {
            for (int gx = 0; gx < GridW; gx++)
            {
                var (sx, sy) = IsoToScreen(gx, gy, originX, originY);
                bool isPath = (gx == 3 || gx == 4) && gy > 0;
                DrawIsoTile(terrainCanvas, sx, sy, isPath);
            }
        }

        // Gebäude (Y-sortiert = Painter's Algorithm durch Grid-Reihenfolge)
        if (_buildings != null)
        {
            int count = Math.Min(_buildings.Count, BuildingPositions.Length);
            for (int i = 0; i < count; i++)
            {
                if (!_buildings[i].IsUnlocked) continue;
                var (gx, gy) = BuildingPositions[i];
                var (sx, sy) = IsoToScreen(gx, gy, originX, originY);
                DrawBuilding(terrainCanvas, sx, sy, _buildings[i]);
            }
        }

        // Hauptgebäude in der Mitte
        var (hx, hy) = IsoToScreen(3, 0, originX, originY);
        DrawMainHall(terrainCanvas, hx + TileW * 0.5f, hy);
    }

    private void DrawIsoTile(SKCanvas canvas, float cx, float cy, bool isPath)
    {
        _buildingPath.Rewind();
        _buildingPath.MoveTo(cx, cy - TileH / 2);
        _buildingPath.LineTo(cx + TileW / 2, cy);
        _buildingPath.LineTo(cx, cy + TileH / 2);
        _buildingPath.LineTo(cx - TileW / 2, cy);
        _buildingPath.Close();

        _fillPaint.Color = isPath ? StonePath : GrassBase;
        canvas.DrawPath(_buildingPath, _fillPaint);

        // Leichter Rand
        _strokePaint.Color = isPath ? StonePathDark : GrassDark;
        _strokePaint.StrokeWidth = 0.5f;
        canvas.DrawPath(_buildingPath, _strokePaint);
    }

    private void DrawBuilding(SKCanvas canvas, float cx, float cy, GuildBuildingDisplay building)
    {
        // Einfaches isometrisches Gebäude
        float bw = TileW * 0.6f;
        float bh = TileH * 1.5f + building.CurrentLevel * 3;
        if (!SKColor.TryParse(building.Color, out var color))
            color = new SKColor(0x88, 0x88, 0x88);

        // Dach (Dreieck)
        _buildingPath.Rewind();
        _buildingPath.MoveTo(cx, cy - bh - 12);
        _buildingPath.LineTo(cx + bw / 2 + 4, cy - bh + 8);
        _buildingPath.LineTo(cx - bw / 2 - 4, cy - bh + 8);
        _buildingPath.Close();
        _fillPaint.Color = color.WithAlpha(180);
        canvas.DrawPath(_buildingPath, _fillPaint);

        // Wand
        _fillPaint.Color = color.WithAlpha(120);
        canvas.DrawRect(cx - bw / 2, cy - bh + 8, bw, bh - 8, _fillPaint);

        // Tür
        _fillPaint.Color = new SKColor(0x30, 0x20, 0x10);
        canvas.DrawRect(cx - 4, cy - 14, 8, 14, _fillPaint);

        // Level-Sterne (max 5)
        int stars = Math.Min(building.CurrentLevel, 5);
        float starY = cy - bh - 16;
        for (int s = 0; s < stars; s++)
        {
            float sx = cx - (stars - 1) * 5f + s * 10f;
            _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
            canvas.DrawCircle(sx, starY, 2.5f, _fillPaint);
        }
    }

    private void DrawMainHall(SKCanvas canvas, float cx, float cy)
    {
        // Großes Hauptgebäude
        float bw = TileW * 1.2f;
        float bh = TileH * 3f;

        // Wand (Stein)
        _fillPaint.Color = new SKColor(0x5A, 0x4C, 0x3E);
        canvas.DrawRect(cx - bw / 2, cy - bh, bw, bh, _fillPaint);

        // Dach
        _buildingPath.Rewind();
        _buildingPath.MoveTo(cx, cy - bh - 20);
        _buildingPath.LineTo(cx + bw / 2 + 8, cy - bh + 4);
        _buildingPath.LineTo(cx - bw / 2 - 8, cy - bh + 4);
        _buildingPath.Close();
        _fillPaint.Color = new SKColor(0x92, 0x40, 0x0E);
        canvas.DrawPath(_buildingPath, _fillPaint);

        // Tor
        _fillPaint.Color = new SKColor(0x38, 0x2C, 0x20);
        canvas.DrawRoundRect(cx - 10, cy - 24, 20, 24, 10, 0, _fillPaint);

        // Level-Anzeige
        _labelFont.Size = 10;
        _fillPaint.Color = new SKColor(0xFF, 0xD7, 0x00);
        canvas.DrawText($"Lv.{_hallLevel}", cx, cy - bh - 24, SKTextAlign.Center, _labelFont, _fillPaint);
    }

    private void DrawWindowGlows(SKCanvas canvas, float originX, float originY)
    {
        if (_buildings == null) return;

        int count = Math.Min(_buildings.Count, BuildingPositions.Length);
        for (int i = 0; i < count; i++)
        {
            if (!_buildings[i].IsUnlocked) continue;
            var (gx, gy) = BuildingPositions[i];
            var (sx, sy) = IsoToScreen(gx, gy, originX, originY);

            float bh = TileH * 1.5f + _buildings[i].CurrentLevel * 3;
            float glow = 0.5f + MathF.Sin(_time * 2f + i * 1.3f) * 0.3f;

            _fillPaint.Color = new SKColor(0xFF, 0xD5, 0x4F, (byte)(40 * glow));
            canvas.DrawRect(sx - 6, sy - bh + 16, 5, 5, _fillPaint);
            canvas.DrawRect(sx + 2, sy - bh + 16, 5, 5, _fillPaint);
        }
    }

    private void DrawFlag(SKCanvas canvas, float originX, float originY)
    {
        var (hx, hy) = IsoToScreen(3, 0, originX, originY);
        float flagX = hx + TileW * 0.5f + 20;
        float flagY = hy - TileH * 3f - 30;

        // Mast
        _strokePaint.Color = new SKColor(0x78, 0x68, 0x58);
        _strokePaint.StrokeWidth = 2f;
        canvas.DrawLine(flagX, flagY, flagX, flagY + 35, _strokePaint);

        // Fahne (Sinus-Welle)
        float wave = MathF.Sin(_time * 3f) * 3f;
        _buildingPath.Rewind();
        _buildingPath.MoveTo(flagX, flagY);
        _buildingPath.LineTo(flagX + 16 + wave, flagY + 4);
        _buildingPath.LineTo(flagX + 14 + wave * 0.8f, flagY + 12);
        _buildingPath.LineTo(flagX, flagY + 10);
        _buildingPath.Close();

        _fillPaint.Color = new SKColor(0xEA, 0x58, 0x0C);
        canvas.DrawPath(_buildingPath, _fillPaint);
    }

    private void UpdateAndDrawSmoke(SKCanvas canvas, float originX, float originY, float deltaTime)
    {
        _smokeTimer += deltaTime;

        // Neuen Rauch spawnen (aus Schornstein des Hauptgebäudes)
        if (_smokeTimer >= 0.4f && _smokeCount < MaxSmoke)
        {
            _smokeTimer = 0;
            var (hx, hy) = IsoToScreen(3, 0, originX, originY);
            _smokeParticles[_smokeCount++] = new SmokeParticle
            {
                X = hx + TileW * 0.5f + 5 + Random.Shared.NextSingle() * 6 - 3,
                Y = hy - TileH * 3f - 20,
                Size = 3 + Random.Shared.NextSingle() * 2,
                Life = 2f,
                Speed = 8 + Random.Shared.NextSingle() * 4,
                Alpha = 0.6f
            };
        }

        // Aktualisieren und zeichnen
        for (int i = _smokeCount - 1; i >= 0; i--)
        {
            var p = _smokeParticles[i];
            p.Y -= p.Speed * deltaTime;
            p.X += MathF.Sin(_time * 2f + i) * deltaTime * 3;
            p.Size += deltaTime * 2;
            p.Life -= deltaTime;
            p.Alpha = MathF.Max(0, p.Life / 2f) * 0.4f;

            if (p.Life <= 0)
            {
                _smokeParticles[i] = _smokeParticles[--_smokeCount];
                continue;
            }

            _fillPaint.Color = new SKColor(0x88, 0x88, 0x88, (byte)(255 * p.Alpha));
            canvas.DrawCircle(p.X, p.Y, p.Size, _fillPaint);
            _smokeParticles[i] = p;
        }
    }

    private static (float x, float y) IsoToScreen(int gx, int gy, float originX, float originY)
    {
        float sx = originX + (gx - gy) * TileW / 2;
        float sy = originY + (gx + gy) * TileH / 2;
        return (sx, sy);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedTerrain?.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _labelFont.Dispose();
        _buildingPath.Dispose();
    }
}
