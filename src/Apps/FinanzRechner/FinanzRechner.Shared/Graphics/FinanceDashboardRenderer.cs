using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// Subtiler animierter Hintergrund für den Hero-Header der HomeView.
/// Zeigt ein langsam rotierendes Gradient-Mesh, Grid-Linien (Börsen-Chart-Stil),
/// schwebende Finanzsymbole und leuchtende Glow-Dots.
/// </summary>
public static class FinanceDashboardRenderer
{
    // --- Partikel-Struct (kein GC-Druck) ---
    private struct DashboardParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Size;
        public float Alpha;
        public char Symbol;
        public float Phase;
        public float Lifetime;
        public float MaxLifetime;
    }

    // --- Partikel-Pool ---
    private const int ParticleCount = 16;
    private static DashboardParticle[] _particles = new DashboardParticle[ParticleCount];

    // --- Glow-Dot-Struct ---
    private struct GlowDot
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Phase;
        public float Radius;
    }

    private const int GlowDotCount = 14;
    private static GlowDot[] _glowDots = new GlowDot[GlowDotCount];

    // --- Initialisierungs-Flag ---
    private static bool _initialized;

    // --- Verfügbare Symbole ---
    private static readonly char[] Symbols = { '\u20AC', '$', '%', '\u2191', '\u2193' };

    // --- Gecachte SKPaints ---
    private static readonly SKPaint GradientPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint GridPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint SymbolPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint GlowDotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint GlowDotCorePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte SKFonts ---
    private static readonly SKFont SymbolFont = new() { Size = 14f };

    // --- Gecachte MaskFilter (3 Stufen) ---
    private static readonly SKMaskFilter BlurSmall = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
    private static readonly SKMaskFilter BlurMedium = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
    private static readonly SKMaskFilter BlurLarge = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f);
    // --- Zufallsgenerator ---
    private static readonly Random Rng = new();

    /// <summary>
    /// Initialisiert Partikel und Glow-Dots mit zufälligen Positionen.
    /// Wird einmal aufgerufen bevor der erste Frame gerendert wird.
    /// </summary>
    public static void Initialize()
    {
        // Floating-Symbole initialisieren
        for (var i = 0; i < ParticleCount; i++)
        {
            _particles[i] = CreateParticle(randomizeLifetime: true);
        }

        // Glow-Dots initialisieren
        for (var i = 0; i < GlowDotCount; i++)
        {
            _glowDots[i] = new GlowDot
            {
                X = Rng.NextSingle(),
                Y = Rng.NextSingle(),
                VelocityX = (Rng.NextSingle() - 0.5f) * 0.01f,
                VelocityY = (Rng.NextSingle() - 0.5f) * 0.008f,
                Phase = Rng.NextSingle() * MathF.Tau,
                Radius = 1.5f + Rng.NextSingle() * 2.5f
            };
        }

        _initialized = true;
    }

    /// <summary>
    /// Erstellt einen neuen Partikel mit zufälligen Werten.
    /// </summary>
    private static DashboardParticle CreateParticle(bool randomizeLifetime)
    {
        var maxLife = 30f + Rng.NextSingle() * 30f; // 30-60s Lebensdauer
        return new DashboardParticle
        {
            X = 0.05f + Rng.NextSingle() * 0.9f,
            Y = randomizeLifetime ? Rng.NextSingle() : 1.1f, // Bei Recycling: von unten starten
            VelocityX = (Rng.NextSingle() - 0.5f) * 0.003f,
            VelocityY = -(0.008f + Rng.NextSingle() * 0.012f), // Langsam aufwärts
            Size = 10f + Rng.NextSingle() * 6f,
            Alpha = 0f,
            Symbol = Symbols[Rng.Next(Symbols.Length)],
            Phase = Rng.NextSingle() * MathF.Tau,
            Lifetime = randomizeLifetime ? Rng.NextSingle() * maxLife : 0f,
            MaxLifetime = maxLife
        };
    }

    /// <summary>
    /// Aktualisiert Partikel-Positionen und recycelt abgelaufene Partikel.
    /// </summary>
    public static void Update(float deltaTime)
    {
        if (!_initialized) Initialize();

        // Floating-Symbole bewegen
        for (var i = 0; i < ParticleCount; i++)
        {
            ref var p = ref _particles[i];
            p.Lifetime += deltaTime;
            p.X += p.VelocityX * deltaTime;
            p.Y += p.VelocityY * deltaTime;
            p.Phase += deltaTime * 0.8f;

            // Alpha basierend auf Lebensdauer (Fade-In → Hold → Fade-Out)
            var lifeRatio = p.Lifetime / p.MaxLifetime;
            if (lifeRatio < 0.1f)
                p.Alpha = lifeRatio / 0.1f; // Fade-In in ersten 10%
            else if (lifeRatio > 0.8f)
                p.Alpha = (1f - lifeRatio) / 0.2f; // Fade-Out in letzten 20%
            else
                p.Alpha = 1f;

            // Recyceln wenn Lebensdauer abgelaufen oder aus dem Sichtbereich
            if (p.Lifetime >= p.MaxLifetime || p.Y < -0.1f)
            {
                _particles[i] = CreateParticle(randomizeLifetime: false);
            }
        }

        // Glow-Dots langsam driften lassen
        for (var i = 0; i < GlowDotCount; i++)
        {
            ref var d = ref _glowDots[i];
            d.X += d.VelocityX * deltaTime;
            d.Y += d.VelocityY * deltaTime;
            d.Phase += deltaTime * 1.2f;

            // Wrap: Bildschirmrand → gegenüberliegende Seite
            if (d.X < -0.05f) d.X = 1.05f;
            else if (d.X > 1.05f) d.X = -0.05f;
            if (d.Y < -0.05f) d.Y = 1.05f;
            else if (d.Y > 1.05f) d.Y = -0.05f;
        }
    }

    /// <summary>
    /// Rendert den animierten Hintergrund auf den Canvas.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        if (!_initialized) Initialize();

        var w = bounds.Width;
        var h = bounds.Height;
        if (w < 1 || h < 1) return;

        // 1) Gradient-Mesh (langsam rotierende Farben)
        DrawGradientMesh(canvas, bounds, w, h, time);

        // 2) Grid-Linien (Börsen-Chart-Stil)
        DrawGridLines(canvas, w, h, time);

        // 3) Glow-Dots (leuchtende Punkte)
        DrawGlowDots(canvas, w, h, time);

        // 4) Floating-Symbole (€, $, %, Pfeile)
        DrawFloatingSymbols(canvas, w, h);
    }

    /// <summary>
    /// Zeichnet ein langsam rotierendes Gradient-Mesh mit Theme-Farben.
    /// Der Gradient dreht sich über die Zeit für einen lebendigen Effekt.
    /// </summary>
    private static void DrawGradientMesh(SKCanvas canvas, SKRect bounds, float w, float h, float time)
    {
        // Rotationswinkel ändert sich langsam (volle Rotation in ~60s)
        var angle = time * 0.1f;
        var cx = w * 0.5f;
        var cy = h * 0.5f;
        var radius = MathF.Max(w, h) * 0.7f;

        // Start- und Endpunkt des Gradienten rotieren
        var startX = cx + MathF.Cos(angle) * radius;
        var startY = cy + MathF.Sin(angle) * radius;
        var endX = cx + MathF.Cos(angle + MathF.PI) * radius;
        var endY = cy + MathF.Sin(angle + MathF.PI) * radius;

        // Theme-Farben mit sehr niedriger Opacity (5-8%)
        var primary = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, 18);   // ~7%
        var secondary = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Secondary, 15); // ~6%
        var accent = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 13);     // ~5%

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(startX, startY),
            new SKPoint(endX, endY),
            new[] { primary, secondary, accent, primary },
            new[] { 0f, 0.33f, 0.66f, 1f },
            SKShaderTileMode.Clamp);

        GradientPaint.Shader = shader;
        canvas.DrawRect(bounds, GradientPaint);
        GradientPaint.Shader = null;
    }

    /// <summary>
    /// Zeichnet horizontale und vertikale Grid-Linien die an ein Börsen-Chart erinnern.
    /// Leichte Sinus-Animation für lebendigen Effekt.
    /// </summary>
    private static void DrawGridLines(SKCanvas canvas, float w, float h, float time)
    {
        // Grid-Farbe: Theme TextMuted mit sehr niedriger Opacity (3-5%)
        var gridColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 10); // ~4%
        GridPaint.Color = gridColor;

        var gridSpacingH = h / 6f; // ~6 horizontale Linien
        var gridSpacingV = w / 8f; // ~8 vertikale Linien

        // Horizontale Linien mit leichtem Sinus-Offset
        for (var i = 1; i < 6; i++)
        {
            var y = i * gridSpacingH;
            var offset = MathF.Sin(time * 0.3f + i * 0.7f) * 1.5f;
            canvas.DrawLine(0, y + offset, w, y + offset, GridPaint);
        }

        // Vertikale Linien mit leichtem Sinus-Offset
        for (var i = 1; i < 8; i++)
        {
            var x = i * gridSpacingV;
            var offset = MathF.Cos(time * 0.25f + i * 0.5f) * 1.2f;
            canvas.DrawLine(x + offset, 0, x + offset, h, GridPaint);
        }
    }

    /// <summary>
    /// Zeichnet leuchtende Punkte die langsam driften.
    /// Verschiedene Blur-Stufen für Tiefeneffekt.
    /// </summary>
    private static void DrawGlowDots(SKCanvas canvas, float w, float h, float time)
    {
        for (var i = 0; i < GlowDotCount; i++)
        {
            ref var d = ref _glowDots[i];
            var px = d.X * w;
            var py = d.Y * h;

            // Pulsierende Helligkeit
            var pulse = 0.5f + 0.5f * MathF.Sin(d.Phase + time * 0.5f);
            var alpha = (byte)(15 + pulse * 25); // 15-40 (~6-16% Opacity)

            // Äußerer Glow (größerer Blur)
            var glowColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, (byte)(alpha * 0.6f));
            GlowDotPaint.Color = glowColor;
            GlowDotPaint.MaskFilter = i % 3 == 0 ? BlurLarge : BlurMedium;
            canvas.DrawCircle(px, py, d.Radius * 3f, GlowDotPaint);
            GlowDotPaint.MaskFilter = null;

            // Innerer Kern (heller, schärfer)
            var coreColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, (byte)(alpha * 1.2f));
            GlowDotCorePaint.Color = coreColor;
            GlowDotCorePaint.MaskFilter = BlurSmall;
            canvas.DrawCircle(px, py, d.Radius, GlowDotCorePaint);
            GlowDotCorePaint.MaskFilter = null;
        }
    }

    /// <summary>
    /// Zeichnet langsam aufsteigende Finanzsymbole (€, $, %, Pfeile) die verblassen.
    /// </summary>
    private static void DrawFloatingSymbols(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < ParticleCount; i++)
        {
            ref var p = ref _particles[i];
            if (p.Alpha <= 0.01f) continue;

            var px = p.X * w + MathF.Sin(p.Phase) * w * 0.015f;
            var py = p.Y * h;
            var alpha = (byte)(p.Alpha * 50); // Max ~20% Opacity (subtil)

            // Symbol-Farbe: Mischung aus Secondary und TextMuted
            var symbolColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Secondary, alpha);
            SymbolPaint.Color = symbolColor;

            SymbolFont.Size = p.Size;
            var symbolStr = p.Symbol.ToString();

            // Zentriert zeichnen
            var textWidth = SymbolFont.MeasureText(symbolStr);
            canvas.DrawText(symbolStr, px - textWidth * 0.5f, py + p.Size * 0.35f, SymbolFont, SymbolPaint);
        }
    }
}
