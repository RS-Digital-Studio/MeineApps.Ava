using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Animierter Hintergrund-Renderer für Menü-Screens.
/// Rendert Bomberman-thematische Elemente: Gradient, Grid, Bomben-Silhouetten,
/// Funken-Partikel und Flammen-Wisps. Struct-basiert, keine per-Frame-Allokationen.
/// </summary>
public static class MenuBackgroundRenderer
{
    // ═══════════════════════════════════════════════════════════════════════
    // KONSTANTEN
    // ═══════════════════════════════════════════════════════════════════════

    private const int BOMB_COUNT = 8;
    private const int SPARK_COUNT = 25;
    private const int FLAME_COUNT = 6;
    private const float GRID_SPACING = 48f;

    // Palette (aufgehellt für bessere Sichtbarkeit)
    private const byte GRID_ALPHA = 20;           // ~0.08 von 255
    private const byte BOMB_ALPHA_MIN = 25;       // ~0.10
    private const byte BOMB_ALPHA_MAX = 35;       // ~0.14
    private const byte SPARK_ALPHA = 80;
    private const byte FLAME_ALPHA_BASE = 50;

    // Gradient-Farben (helleres Bomberman-Blau)
    private static readonly SKColor GradientTopLeft = new(0x2D, 0x2D, 0x48);     // #2D2D48
    private static readonly SKColor GradientBottomRight = new(0x29, 0x3A, 0x56);  // #293A56

    // ═══════════════════════════════════════════════════════════════════════
    // GEPOOLTE PAINT-OBJEKTE (statisch, keine per-Frame-Allokationen)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly SKPaint _gradientPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 0.5f,
        Color = new SKColor(255, 255, 255, GRID_ALPHA)
    };
    private static readonly SKPaint _bombPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _sparkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _flamePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKMaskFilter _sparkGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f);
    private static readonly SKMaskFilter _flameGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // ═══════════════════════════════════════════════════════════════════════
    // PARTIKEL-STRUCTS (GC-freundlich, kein Heap)
    // ═══════════════════════════════════════════════════════════════════════

    private struct BombSilhouette
    {
        public float X, Y, Size, Speed, Phase;
        public byte Alpha;
    }

    private struct SparkParticle
    {
        public float X, Y, Size, Speed, Phase;
        public byte R, G, B;
    }

    private struct FlameWisp
    {
        public float X, Phase, Width, Height;
        public byte Alpha;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STATISCHE PARTIKEL-ARRAYS
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly BombSilhouette[] _bombs = new BombSilhouette[BOMB_COUNT];
    private static readonly SparkParticle[] _sparks = new SparkParticle[SPARK_COUNT];
    private static readonly FlameWisp[] _flames = new FlameWisp[FLAME_COUNT];
    private static bool _initialized;

    /// <summary>
    /// Initialisiert alle Partikel mit deterministischem Seed.
    /// Muss einmal aufgerufen werden bevor Render() verwendet wird.
    /// </summary>
    public static void Initialize(int seed)
    {
        var rng = new Random(seed);

        // Bomben-Silhouetten: Verschiedene Größen, Geschwindigkeiten, Phasen
        for (int i = 0; i < BOMB_COUNT; i++)
        {
            ref var b = ref _bombs[i];
            b.X = (float)rng.NextDouble();     // Normalisiert 0-1 (wird bei Render skaliert)
            b.Y = (float)rng.NextDouble();
            b.Size = 20f + (float)rng.NextDouble() * 20f;  // 20-40px
            b.Speed = 8f + (float)rng.NextDouble() * 12f;  // Aufstiegsgeschwindigkeit
            b.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            // Parallax: Größere Bomben bewegen sich langsamer
            b.Speed *= 1f - (b.Size - 20f) / 40f * 0.5f;  // 50% langsamer bei max Größe
            b.Alpha = (byte)(BOMB_ALPHA_MIN + rng.Next(BOMB_ALPHA_MAX - BOMB_ALPHA_MIN + 1));
        }

        // Funken-Partikel: Warme Farben (Orange/Gelb)
        for (int i = 0; i < SPARK_COUNT; i++)
        {
            ref var s = ref _sparks[i];
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Size = 2f + (float)rng.NextDouble() * 2f;    // 2-4px
            s.Speed = 10f + (float)rng.NextDouble() * 15f;
            s.Phase = (float)rng.NextDouble() * MathF.PI * 2f;

            // Zufällig Orange oder Gelb
            if (rng.NextDouble() < 0.5)
            {
                // Orange
                s.R = 255; s.G = (byte)(140 + rng.Next(40)); s.B = 30;
            }
            else
            {
                // Gelb
                s.R = 255; s.G = (byte)(200 + rng.Next(40)); s.B = (byte)(60 + rng.Next(40));
            }
        }

        // Flammen-Wisps: Über die Breite verteilt
        for (int i = 0; i < FLAME_COUNT; i++)
        {
            ref var f = ref _flames[i];
            f.X = (i + 0.5f) / FLAME_COUNT;   // Gleichmäßig verteilt (normalisiert)
            f.Phase = (float)rng.NextDouble() * MathF.PI * 2f;
            f.Width = 30f + (float)rng.NextDouble() * 20f;  // 30-50px
            f.Height = 20f + (float)rng.NextDouble() * 15f;  // 20-35px
            f.Alpha = (byte)(FLAME_ALPHA_BASE + rng.Next(15));
        }

        _initialized = true;
    }

    /// <summary>
    /// Rendert den animierten Menü-Hintergrund auf den Canvas.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="width">Breite in logischen Pixeln</param>
    /// <param name="height">Höhe in logischen Pixeln</param>
    /// <param name="time">Animationszeit in Sekunden (monoton steigend)</param>
    public static void Render(SKCanvas canvas, float width, float height, float time)
    {
        if (!_initialized)
            Initialize(42);

        RenderGradientBackground(canvas, width, height);
        RenderGridLines(canvas, width, height);
        RenderBombSilhouettes(canvas, width, height, time);
        RenderSparkParticles(canvas, width, height, time);
        RenderFlameWisps(canvas, width, height, time);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRADIENT-HINTERGRUND
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dunkler diagonaler Gradient (links-oben nach rechts-unten).
    /// </summary>
    private static void RenderGradientBackground(SKCanvas canvas, float width, float height)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, height),
            [GradientTopLeft, GradientBottomRight],
            SKShaderTileMode.Clamp);

        _gradientPaint.Shader = shader;
        canvas.DrawRect(0, 0, width, height, _gradientPaint);
        _gradientPaint.Shader = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRID-MUSTER (Bomberman-Kacheln)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Feines Grid-Muster wie Bomberman-Spielfeld-Kacheln.
    /// Alle 48px, sehr niedrige Deckkraft.
    /// </summary>
    private static void RenderGridLines(SKCanvas canvas, float width, float height)
    {
        // Vertikale Linien
        for (float x = GRID_SPACING; x < width; x += GRID_SPACING)
        {
            canvas.DrawLine(x, 0, x, height, _gridPaint);
        }

        // Horizontale Linien
        for (float y = GRID_SPACING; y < height; y += GRID_SPACING)
        {
            canvas.DrawLine(0, y, width, y, _gridPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BOMBEN-SILHOUETTEN (schwebend, aufsteigend, mit Sinus-Schwanken)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 8 transparente Bomben-Silhouetten die langsam aufsteigen
    /// und horizontal hin- und herschwanken (sin-basiert).
    /// </summary>
    private static void RenderBombSilhouettes(SKCanvas canvas, float width, float height, float time)
    {
        _bombPaint.MaskFilter = null;

        for (int i = 0; i < BOMB_COUNT; i++)
        {
            ref var b = ref _bombs[i];

            // Position berechnen: Aufsteigend mit Wrap-Around
            float baseX = b.X * width;
            float baseY = height - ((time * b.Speed + b.Y * height) % (height + b.Size * 2)) + b.Size;

            // Horizontales Schwanken (Sinus-basiert)
            float swayAmount = b.Size * 0.8f;
            float sway = MathF.Sin(time * 0.5f + b.Phase) * swayAmount;
            float x = baseX + sway;
            float y = baseY;

            float r = b.Size * 0.5f;

            _bombPaint.Color = new SKColor(255, 255, 255, b.Alpha);

            // Bomben-Körper (Kreis)
            canvas.DrawCircle(x, y, r, _bombPaint);

            // Zündschnur (kleiner Strich oben)
            float fuseBaseY = y - r;
            float fuseTopY = fuseBaseY - r * 0.4f;
            float fuseOffsetX = r * 0.15f;

            // Zündschnur als kleines Rechteck
            canvas.DrawRect(
                x - fuseOffsetX - 1f, fuseTopY,
                fuseOffsetX * 2f + 2f, r * 0.4f,
                _bombPaint);

            // Kleiner Kreis oben an der Zündschnur (Funke/Knopf)
            canvas.DrawCircle(x, fuseTopY - 1.5f, 2f, _bombPaint);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FUNKEN-PARTIKEL (warm, glühend, aufsteigend)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 25 kleine glühende Partikel in warmen Farben (Orange/Gelb).
    /// Steigen langsam auf mit leichter horizontaler Oszillation.
    /// Wrap-Around wenn sie den oberen Bildschirmrand verlassen.
    /// </summary>
    private static void RenderSparkParticles(SKCanvas canvas, float width, float height, float time)
    {
        _sparkPaint.MaskFilter = _sparkGlow;

        for (int i = 0; i < SPARK_COUNT; i++)
        {
            ref var s = ref _sparks[i];

            // Position: Aufsteigend mit Wrap-Around
            float baseX = s.X * width;
            float baseY = height - ((time * s.Speed + s.Y * height) % (height + 20f));

            // Horizontale Oszillation
            float oscillation = MathF.Sin(time * 1.2f + s.Phase) * 8f;
            float x = baseX + oscillation;
            float y = baseY;

            // Pulsierender Alpha-Wert (Funkeln-Effekt)
            float pulse = MathF.Sin(time * 3f + s.Phase) * 0.3f + 0.7f;
            byte alpha = (byte)(SPARK_ALPHA * pulse);

            _sparkPaint.Color = new SKColor(s.R, s.G, s.B, alpha);
            canvas.DrawCircle(x, y, s.Size, _sparkPaint);
        }

        _sparkPaint.MaskFilter = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FLAMMEN-WISPS (unten, flackernd, orange-rot)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 6 flammenartige Formen am unteren Bildschirmrand.
    /// Sanftes Flackern durch sin-Wellen, orange-roter Gradient, sehr transparent.
    /// </summary>
    private static void RenderFlameWisps(SKCanvas canvas, float width, float height, float time)
    {
        _flamePaint.MaskFilter = _flameGlow;

        for (int i = 0; i < FLAME_COUNT; i++)
        {
            ref var f = ref _flames[i];

            float x = f.X * width;

            // Flacker-Effekt: Höhe und Alpha variieren per Sinus
            float flicker1 = MathF.Sin(time * 2.5f + f.Phase) * 0.3f + 0.7f;
            float flicker2 = MathF.Sin(time * 3.8f + f.Phase * 1.3f) * 0.2f + 0.8f;
            float combinedFlicker = flicker1 * flicker2;

            float currentHeight = f.Height * combinedFlicker;
            float currentWidth = f.Width * (0.9f + flicker2 * 0.1f);
            byte alpha = (byte)(f.Alpha * combinedFlicker);

            float flameBottom = height;
            float flameTop = flameBottom - currentHeight;

            // Horizontale Auslenkung (Wind-Effekt)
            float windOffset = MathF.Sin(time * 0.8f + f.Phase * 0.7f) * 4f;

            // Flammen-Form als Pfad (Tropfenform von unten nach oben)
            using var path = new SKPath();
            path.MoveTo(x - currentWidth * 0.5f, flameBottom);

            // Linke Seite: Kurve nach oben
            path.QuadTo(
                x - currentWidth * 0.3f + windOffset * 0.5f,
                flameBottom - currentHeight * 0.6f,
                x + windOffset,
                flameTop);

            // Rechte Seite: Zurück nach unten
            path.QuadTo(
                x + currentWidth * 0.3f + windOffset * 0.5f,
                flameBottom - currentHeight * 0.6f,
                x + currentWidth * 0.5f,
                flameBottom);

            path.Close();

            // Äußere Flamme: Orange-rot
            _flamePaint.Color = new SKColor(255, 80, 20, alpha);
            canvas.DrawPath(path, _flamePaint);

            // Innerer Kern: Heller, kleiner
            float innerScale = 0.5f;
            float innerHeight = currentHeight * innerScale;
            float innerWidth = currentWidth * innerScale;
            float innerTop = flameBottom - innerHeight;

            using var innerPath = new SKPath();
            innerPath.MoveTo(x - innerWidth * 0.4f, flameBottom);
            innerPath.QuadTo(
                x - innerWidth * 0.2f + windOffset * 0.3f,
                flameBottom - innerHeight * 0.6f,
                x + windOffset * 0.6f,
                innerTop);
            innerPath.QuadTo(
                x + innerWidth * 0.2f + windOffset * 0.3f,
                flameBottom - innerHeight * 0.6f,
                x + innerWidth * 0.4f,
                flameBottom);
            innerPath.Close();

            // Innerer Kern heller (orange-gelb)
            byte innerAlpha = (byte)Math.Min(255, alpha * 0.7f);
            _flamePaint.Color = new SKColor(255, 160, 40, innerAlpha);
            canvas.DrawPath(innerPath, _flamePaint);
        }

        _flamePaint.MaskFilter = null;
    }
}
