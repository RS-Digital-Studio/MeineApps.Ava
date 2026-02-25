using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Wetter-Overlay für die City-Szene.
/// Saisonale Effekte: Sonnenstrahlen (Sommer), Regen + Regenbogen (Frühling),
/// fallende Blätter (Herbst), Schnee (Winter).
/// Wetter ändert sich nach aktuellem Monat oder aktivem SeasonalEvent.
/// </summary>
public class CityWeatherSystem
{
    // Wetter-Typ
    public enum WeatherType { Clear, Rain, Snow, Leaves, Sunshine }

    // Partikel-Pool (GC-frei)
    private const int MaxParticles = 80;
    private readonly WeatherParticle[] _particles = new WeatherParticle[MaxParticles];
    private int _activeCount;

    // Gecachte Paints
    private readonly SKPaint _particlePaint = new() { IsAntialias = true };
    private readonly SKPaint _sunPaint = new() { IsAntialias = true };
    private readonly SKPaint _rainbowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };

    // Zustand
    private WeatherType _currentWeather = WeatherType.Clear;
    private float _time;
    private float _rainbowOpacity; // 0-1, wird bei Regen+Sonnenstrahlen kurz sichtbar

    /// <summary>
    /// Aktives Wetter (kann extern gesetzt werden, z.B. durch SeasonalEvent).
    /// </summary>
    public WeatherType CurrentWeather
    {
        get => _currentWeather;
        set
        {
            if (_currentWeather != value)
            {
                _currentWeather = value;
                _activeCount = 0; // Partikel neu spawnen
            }
        }
    }

    /// <summary>
    /// Bestimmt das Wetter automatisch basierend auf aktuellem Monat.
    /// Kann durch SeasonalEvent überschrieben werden.
    /// </summary>
    public void SetWeatherByMonth()
    {
        // Lokalzeit für visuelle Darstellung (Jahreszeit)
        int month = DateTime.Now.Month;
        _currentWeather = month switch
        {
            3 or 4 or 5 => WeatherType.Rain,       // Frühling: Regen + Regenbogen
            6 or 7 or 8 => WeatherType.Sunshine,   // Sommer: Sonnenstrahlen
            9 or 10 or 11 => WeatherType.Leaves,    // Herbst: Fallende Blätter
            _ => WeatherType.Snow                    // Winter: Schnee
        };
    }

    /// <summary>
    /// Aktualisiert alle Wetter-Partikel.
    /// </summary>
    public void Update(float deltaTime)
    {
        _time += deltaTime;

        // Partikel spawnen wenn nötig
        int targetCount = _currentWeather switch
        {
            WeatherType.Rain => 60,
            WeatherType.Snow => 50,
            WeatherType.Leaves => 20,
            WeatherType.Sunshine => 0, // Sonnenstrahlen sind keine Partikel
            _ => 0
        };

        // Neue Partikel hinzufügen
        while (_activeCount < targetCount)
        {
            SpawnParticle(_activeCount);
            _activeCount++;
        }

        // Überschüssige deaktivieren
        if (_activeCount > targetCount) _activeCount = targetCount;

        // Regenbogen-Opacity (kurz sichtbar bei Frühlings-Regen)
        if (_currentWeather == WeatherType.Rain)
        {
            // Regenbogen erscheint alle ~20s für ~5s
            float rainbowCycle = _time % 20f;
            _rainbowOpacity = rainbowCycle > 15f ? (rainbowCycle - 15f) / 2f : 0f;
            if (rainbowCycle > 17f) _rainbowOpacity = Math.Max(0f, 1f - (rainbowCycle - 17f) / 3f);
            _rainbowOpacity = Math.Clamp(_rainbowOpacity, 0f, 0.6f);
        }
        else
        {
            _rainbowOpacity = 0f;
        }
    }

    /// <summary>
    /// Rendert das Wetter-Overlay über die City-Szene.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_currentWeather == WeatherType.Clear) return;

        switch (_currentWeather)
        {
            case WeatherType.Rain:
                RenderRain(canvas, bounds);
                if (_rainbowOpacity > 0.01f)
                    RenderRainbow(canvas, bounds);
                break;
            case WeatherType.Snow:
                RenderSnow(canvas, bounds);
                break;
            case WeatherType.Leaves:
                RenderLeaves(canvas, bounds);
                break;
            case WeatherType.Sunshine:
                RenderSunshine(canvas, bounds);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // REGEN (Frühling)
    // ═══════════════════════════════════════════════════════════════

    private void RenderRain(SKCanvas canvas, SKRect bounds)
    {
        _particlePaint.StrokeWidth = 1f;
        _particlePaint.Style = SKPaintStyle.Stroke;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            // Bewegung: nach unten + leicht seitlich (Wind)
            float windX = MathF.Sin(_time * 0.3f) * 0.5f;
            float x = ((p.X + _time * (p.SpeedX + windX)) % bounds.Width + bounds.Width) % bounds.Width + bounds.Left;
            float y = ((p.Y + _time * p.SpeedY) % bounds.Height) + bounds.Top;

            byte alpha = (byte)(80 + p.Size * 30);
            _particlePaint.Color = new SKColor(0xA0, 0xC4, 0xE0, alpha);

            // Regentropfen als kurze Linie
            float len = 3f + p.Size * 3f;
            canvas.DrawLine(x, y, x + windX * 2, y + len, _particlePaint);
        }

        _particlePaint.Style = SKPaintStyle.Fill;
    }

    /// <summary>
    /// Regenbogen-Bogen bei Regen (subtil, Alpha 0-0.6).
    /// </summary>
    private void RenderRainbow(SKCanvas canvas, SKRect bounds)
    {
        float cx = bounds.Left + bounds.Width * 0.7f;
        float cy = bounds.Top + bounds.Height * 0.6f;
        float radius = bounds.Width * 0.5f;

        // 6 Farben des Regenbogens (von außen nach innen)
        SKColor[] colors =
        {
            new(0xFF, 0x00, 0x00), // Rot
            new(0xFF, 0xA5, 0x00), // Orange
            new(0xFF, 0xFF, 0x00), // Gelb
            new(0x00, 0xFF, 0x00), // Grün
            new(0x00, 0x00, 0xFF), // Blau
            new(0x80, 0x00, 0xFF)  // Violett
        };

        for (int i = 0; i < colors.Length; i++)
        {
            float r = radius - i * 3.5f;
            byte alpha = (byte)(_rainbowOpacity * 100);
            _rainbowPaint.Color = colors[i].WithAlpha(alpha);

            var rect = new SKRect(cx - r, cy - r, cx + r, cy + r);
            canvas.DrawArc(rect, 200, 140, false, _rainbowPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SCHNEE (Winter)
    // ═══════════════════════════════════════════════════════════════

    private void RenderSnow(SKCanvas canvas, SKRect bounds)
    {
        _particlePaint.Style = SKPaintStyle.Fill;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            // Sinus-Drift (Schneeflocken schwanken horizontal)
            float windDrift = MathF.Sin(_time * 0.5f + p.Phase) * 15f;
            float x = ((p.X + windDrift + _time * p.SpeedX * 0.3f) % bounds.Width + bounds.Width) % bounds.Width + bounds.Left;
            float y = ((p.Y + _time * p.SpeedY) % bounds.Height) + bounds.Top;

            float size = 1f + p.Size * 2f;
            byte alpha = (byte)(120 + p.Size * 80);

            _particlePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);
            canvas.DrawCircle(x, y, size, _particlePaint);

            // Größere Flocken haben einen subtilen Glow
            if (p.Size > 0.6f)
            {
                _particlePaint.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(alpha * 0.3f));
                canvas.DrawCircle(x, y, size * 1.8f, _particlePaint);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // BLÄTTER (Herbst)
    // ═══════════════════════════════════════════════════════════════

    private void RenderLeaves(SKCanvas canvas, SKRect bounds)
    {
        _particlePaint.Style = SKPaintStyle.Fill;

        // Herbstfarben
        SKColor[] leafColors =
        {
            new(0xFF, 0x8C, 0x00), // Orange
            new(0xCD, 0x53, 0x1B), // Herbstbraun
            new(0xD4, 0xA0, 0x17), // Dunkelgold
            new(0x8B, 0x45, 0x13), // Sattbraun
            new(0xCC, 0x55, 0x00)  // Rostrot
        };

        for (int i = 0; i < _activeCount; i++)
        {
            ref var p = ref _particles[i];

            // Sanfter Sinus-Drift (Blätter taumeln)
            float sinDrift = MathF.Sin(_time * 0.8f + p.Phase) * 20f;
            float x = ((p.X + sinDrift + _time * p.SpeedX) % bounds.Width + bounds.Width) % bounds.Width + bounds.Left;
            float y = ((p.Y + _time * p.SpeedY) % bounds.Height) + bounds.Top;

            // Blatt-Farbe deterministisch aus Index
            var color = leafColors[i % leafColors.Length];
            byte alpha = (byte)(150 + p.Size * 60);
            _particlePaint.Color = color.WithAlpha(alpha);

            // Blatt als rotiertes Oval
            float rotation = _time * (40f + i * 15f) + p.Phase * 100f;
            float leafW = 3f + p.Size * 3f;
            float leafH = leafW * 0.6f;

            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(rotation);
            canvas.DrawOval(0, 0, leafW, leafH, _particlePaint);
            // Blattader (dünne Linie)
            _particlePaint.Color = CityBuildingShapes.DarkenColor(color, 0.3f).WithAlpha((byte)(alpha * 0.6f));
            _particlePaint.StrokeWidth = 0.5f;
            _particlePaint.Style = SKPaintStyle.Stroke;
            canvas.DrawLine(-leafW * 0.7f, 0, leafW * 0.7f, 0, _particlePaint);
            _particlePaint.Style = SKPaintStyle.Fill;
            canvas.Restore();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // SONNENSTRAHLEN (Sommer)
    // ═══════════════════════════════════════════════════════════════

    private void RenderSunshine(SKCanvas canvas, SKRect bounds)
    {
        // 6 Sonnenstrahlen-Dreiecke vom oberen rechten Eck
        float sunX = bounds.Right - bounds.Width * 0.15f;
        float sunY = bounds.Top + bounds.Height * 0.05f;

        // Strahlen pulsieren leicht
        float pulse = 0.8f + 0.2f * MathF.Sin(_time * 1.2f);

        for (int i = 0; i < 6; i++)
        {
            float angle = -30f + i * 20f; // -30° bis +70° Fächer
            float angleRad = angle * MathF.PI / 180f;
            float len = bounds.Width * 0.6f * pulse;
            float spread = 8f + i * 2f; // Breite am Ende

            float endX = sunX + MathF.Cos(angleRad) * len;
            float endY = sunY + MathF.Sin(angleRad) * len;
            float perpX = MathF.Cos(angleRad + MathF.PI / 2) * spread;
            float perpY = MathF.Sin(angleRad + MathF.PI / 2) * spread;

            // Alpha-Wert: Strahl ist in der Mitte heller
            byte alpha = (byte)(12 + 8 * MathF.Sin(_time * 0.5f + i * 0.9f));

            using var path = new SKPath();
            path.MoveTo(sunX, sunY);
            path.LineTo(endX + perpX, endY + perpY);
            path.LineTo(endX - perpX, endY - perpY);
            path.Close();

            _sunPaint.Color = new SKColor(0xFF, 0xFF, 0xE0, alpha);
            canvas.DrawPath(path, _sunPaint);
        }

        // Heat-Shimmer am Boden (subtile Wellen-Verzerrung visuell)
        float shimmerY = bounds.Top + bounds.Height * 0.85f;
        for (float sx = bounds.Left; sx < bounds.Right; sx += 12)
        {
            float shimmerOffset = MathF.Sin(sx * 0.1f + _time * 3f) * 1.5f;
            byte shimmerAlpha = (byte)(15 + 10 * MathF.Sin(sx * 0.05f + _time * 2f));
            _sunPaint.Color = new SKColor(0xFF, 0xFF, 0xE0, shimmerAlpha);
            canvas.DrawRect(sx, shimmerY + shimmerOffset, 10, 3, _sunPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PARTIKEL-SPAWNING
    // ═══════════════════════════════════════════════════════════════

    private void SpawnParticle(int index)
    {
        // Deterministisch basierend auf Index (kein Random für Reproduzierbarkeit)
        uint hash = (uint)(index * 7919 + 5381);
        hash = hash * 1664525 + 1013904223;

        ref var p = ref _particles[index];

        p.X = (hash % 10000) / 10000f * 400f; // 0-400 range, wird mit bounds moduliert
        hash = hash * 1664525 + 1013904223;
        p.Y = (hash % 10000) / 10000f * 400f;
        hash = hash * 1664525 + 1013904223;
        p.Size = (hash % 1000) / 1000f; // 0-1 normalisiert
        hash = hash * 1664525 + 1013904223;
        p.Phase = (hash % 1000) / 1000f * MathF.PI * 2f;

        // Geschwindigkeit je nach Wetter-Typ
        hash = hash * 1664525 + 1013904223;
        switch (_currentWeather)
        {
            case WeatherType.Rain:
                p.SpeedX = ((hash % 100) / 100f - 0.5f) * 5f; // -2.5 bis 2.5
                p.SpeedY = 80f + (hash % 100) / 100f * 60f;     // 80-140 (schnell nach unten)
                break;
            case WeatherType.Snow:
                p.SpeedX = ((hash % 100) / 100f - 0.5f) * 8f;
                p.SpeedY = 15f + (hash % 100) / 100f * 20f;     // 15-35 (langsam)
                break;
            case WeatherType.Leaves:
                p.SpeedX = 5f + (hash % 100) / 100f * 10f;      // 5-15 (seitlich treiben)
                p.SpeedY = 8f + (hash % 100) / 100f * 12f;      // 8-20 (langsam fallen)
                break;
            default:
                p.SpeedX = 0;
                p.SpeedY = 0;
                break;
        }
    }

    /// <summary>
    /// Wetter-Partikel (Struct für GC-freien Pool).
    /// </summary>
    private struct WeatherParticle
    {
        public float X, Y;
        public float SpeedX, SpeedY;
        public float Size;       // 0-1 normalisiert
        public float Phase;      // Zufällige Phase für Sinus-Drift
    }
}
