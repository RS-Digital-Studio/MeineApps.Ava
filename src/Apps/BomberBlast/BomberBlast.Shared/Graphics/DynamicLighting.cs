using BomberBlast.Models.Grid;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Dynamisches Beleuchtungssystem: Radius-basierte Lichtquellen als additiver Overlay.
/// Sammelt Lichtquellen pro Frame und rendert sie als halbtransparenten Layer.
/// </summary>
public sealed class DynamicLighting
{
    /// <summary>Eine einzelne Lichtquelle</summary>
    private struct LightSource
    {
        public float X, Y;       // Position in Spielfeld-Koordinaten
        public float Radius;     // Lichtradius in Pixeln
        public SKColor Color;    // Lichtfarbe
        public float Intensity;  // 0..1
    }

    private const int MAX_LIGHTS = 40;
    private readonly LightSource[] _lights = new LightSource[MAX_LIGHTS];
    private int _lightCount;

    // Gepoolter Paint für Licht-Rendering
    private readonly SKPaint _lightPaint = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
        BlendMode = SKBlendMode.Screen
    };

    // Gepoolte Arrays für Gradient-Erstellung (vermeidet pro-Licht Heap-Allokation)
    private readonly SKColor[] _gradientColors = new SKColor[2];
    private static readonly float[] GradientPositions = [0f, 1f];

    /// <summary>
    /// Alle Lichtquellen für diesen Frame zurücksetzen
    /// </summary>
    public void Clear()
    {
        _lightCount = 0;
    }

    /// <summary>
    /// Lichtquelle hinzufügen
    /// </summary>
    public void AddLight(float x, float y, float radius, SKColor color, float intensity)
    {
        if (_lightCount >= MAX_LIGHTS) return;

        _lights[_lightCount++] = new LightSource
        {
            X = x,
            Y = y,
            Radius = radius,
            Color = color,
            Intensity = Math.Clamp(intensity, 0f, 1f)
        };
    }

    /// <summary>
    /// Explosions-Licht: Hell, warm-orange, großer Radius
    /// </summary>
    public void AddExplosionLight(float x, float y, float range, float progress, int cellSize)
    {
        float radius = range * cellSize;
        // Intensität folgt Explosions-Progress (hell am Anfang, dunkler zum Ende)
        float intensity = (1f - progress) * 0.8f;
        // Nachglow: Kurz nach der Explosion noch schwaches Licht
        if (intensity < 0.05f) return;

        AddLight(x, y, radius, new SKColor(255, 180, 80), intensity);
    }

    /// <summary>
    /// Bomben-Licht: Schwacher roter Puls
    /// </summary>
    public void AddBombLight(float x, float y, float fuseTimer, float maxFuse, int cellSize, float globalTimer)
    {
        float fuseProgress = 1f - (fuseTimer / maxFuse);
        float pulse = MathF.Sin(globalTimer * (8f + fuseProgress * 16f)) * 0.3f + 0.7f;
        float intensity = 0.15f + fuseProgress * 0.3f;

        AddLight(x, y, cellSize * 1.5f, new SKColor(255, 60, 20), intensity * pulse);
    }

    /// <summary>
    /// Lava-Zellen-Licht: Schwaches dauerhaftes Orange
    /// </summary>
    public void AddLavaLight(float x, float y, int cellSize, float globalTimer, int gx, int gy)
    {
        float pulse = MathF.Sin(globalTimer * 1.5f + gx * 0.8f + gy * 1.2f) * 0.15f + 0.85f;
        AddLight(x, y, cellSize * 1.2f, new SKColor(255, 120, 20), 0.2f * pulse);
    }

    /// <summary>
    /// Eis-Zellen-Licht: Schwaches Cyan
    /// </summary>
    public void AddIceLight(float x, float y, int cellSize)
    {
        AddLight(x, y, cellSize * 0.8f, new SKColor(100, 200, 255), 0.12f);
    }

    /// <summary>
    /// PowerUp-Licht: Leichter Glow in PowerUp-Farbe
    /// </summary>
    public void AddPowerUpLight(float x, float y, int cellSize, SKColor color)
    {
        AddLight(x, y, cellSize * 0.7f, color, 0.15f);
    }

    /// <summary>
    /// Exit-Portal-Licht: Goldener Glow
    /// </summary>
    public void AddExitLight(float x, float y, int cellSize, float globalTimer)
    {
        float pulse = MathF.Sin(globalTimer * 2f) * 0.15f + 0.85f;
        AddLight(x, y, cellSize * 2.5f, new SKColor(255, 220, 80), 0.35f * pulse);
    }

    /// <summary>
    /// Boss-Enrage-Licht: Roter Puls
    /// </summary>
    public void AddBossEnrageLight(float x, float y, int cellSize, float globalTimer)
    {
        float pulse = MathF.Sin(globalTimer * 3f) * 0.3f + 0.7f;
        AddLight(x, y, cellSize * 3.5f, new SKColor(255, 30, 30), 0.25f * pulse);
    }

    /// <summary>
    /// Fackel-Licht: Warmes flackerndes Licht
    /// </summary>
    public void AddTorchLight(float x, float y, int cellSize, float globalTimer, int index)
    {
        float flicker = MathF.Sin(globalTimer * 6f + index * 2.3f) * 0.1f
                       + MathF.Sin(globalTimer * 9.7f + index * 1.7f) * 0.05f + 0.85f;
        AddLight(x, y, cellSize * 2.2f, new SKColor(255, 180, 80), 0.3f * flicker);
    }

    /// <summary>
    /// Schild-Licht: Cyan-Glow um den Spieler
    /// </summary>
    public void AddShieldLight(float x, float y, int cellSize)
    {
        AddLight(x, y, cellSize * 1.2f, new SKColor(0, 220, 255), 0.2f);
    }

    /// <summary>
    /// Alle gesammelten Lichtquellen rendern
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        if (_lightCount == 0) return;

        for (int i = 0; i < _lightCount; i++)
        {
            ref var light = ref _lights[i];
            if (light.Intensity < 0.02f) continue;

            byte alpha = (byte)(light.Intensity * 80); // Subtiler Effekt
            _gradientColors[0] = light.Color.WithAlpha(alpha);
            _gradientColors[1] = light.Color.WithAlpha(0);

            var shader = SKShader.CreateRadialGradient(
                new SKPoint(light.X, light.Y),
                light.Radius,
                _gradientColors,
                GradientPositions,
                SKShaderTileMode.Clamp);
            _lightPaint.Shader = shader;

            canvas.DrawCircle(light.X, light.Y, light.Radius, _lightPaint);
            _lightPaint.Shader = null;
            shader.Dispose();
        }
    }

    /// <summary>
    /// Fackeln an zufälligen Wand-Zellen generieren und als Lichtquellen hinzufügen.
    /// Rendert auch die visuelle Fackel-Darstellung.
    /// </summary>
    public void AddTorchesFromGrid(GameGrid grid, int cellSize, float globalTimer,
        SKCanvas canvas, SKPaint fillPaint, int worldSeed)
    {
        int torchCount = 0;
        const int MAX_TORCHES = 4;

        for (int y = 1; y < grid.Height - 1 && torchCount < MAX_TORCHES; y++)
        {
            for (int x = 1; x < grid.Width - 1 && torchCount < MAX_TORCHES; x++)
            {
                var cell = grid[x, y];
                if (cell.Type != CellType.Wall) continue;

                // Deterministische Auswahl: Nur bestimmte Wand-Zellen bekommen Fackeln
                float rng = ProceduralTextures.CellRandom(x, y, worldSeed + 999);
                if (rng > 0.08f) continue; // ~8% der Wände

                // Prüfen ob benachbarte Zelle begehbar ist (Fackel zeigt zur begehbaren Seite)
                bool hasFloor = false;
                float fackelX = x * cellSize + cellSize * 0.5f;
                float fackelY = y * cellSize + cellSize * 0.5f;

                if (y + 1 < grid.Height && grid[x, y + 1].Type != CellType.Wall)
                {
                    fackelY = y * cellSize + cellSize - 2;
                    hasFloor = true;
                }
                else if (y - 1 >= 0 && grid[x, y - 1].Type != CellType.Wall)
                {
                    fackelY = y * cellSize + 2;
                    hasFloor = true;
                }

                if (!hasFloor) continue;

                // Fackel-Rendering: Kleines Feuer
                float flicker1 = MathF.Sin(globalTimer * 8f + torchCount * 3.1f) * 2f;
                float flicker2 = MathF.Sin(globalTimer * 12f + torchCount * 1.7f) * 1.5f;

                // Flamme (3 flackernde Dreiecke)
                for (int f = 0; f < 3; f++)
                {
                    float fOff = MathF.Sin(globalTimer * (6f + f * 2f) + torchCount * 2f + f) * 1.5f;
                    float fHeight = 4f + MathF.Sin(globalTimer * 5f + f * 1.3f + torchCount) * 2f;

                    byte fAlpha = (byte)(160 - f * 30);
                    byte r = (byte)(255 - f * 20);
                    byte g = (byte)(150 - f * 40);

                    fillPaint.Color = new SKColor(r, g, 20, fAlpha);
                    fillPaint.MaskFilter = null;

                    using var path = new SKPath();
                    path.MoveTo(fackelX - 2 + fOff, fackelY);
                    path.LineTo(fackelX + flicker1 + fOff, fackelY - fHeight);
                    path.LineTo(fackelX + 2 + fOff, fackelY);
                    path.Close();
                    canvas.DrawPath(path, fillPaint);
                }

                // Lichtquelle
                AddTorchLight(fackelX, fackelY, cellSize, globalTimer, torchCount);
                torchCount++;
            }
        }
    }

    public void Dispose()
    {
        _lightPaint.Shader?.Dispose();
        _lightPaint.Dispose();
    }
}
