using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace FinanzRechner.Graphics;

/// <summary>
/// Splash-Renderer "Das wachsende Kapital" für den FinanzRechner.
/// Zeigt einen steigenden Aktien-Chart, gestapelte Münzen und goldene Partikel.
/// </summary>
public sealed class FinanzRechnerSplashRenderer : SplashRendererBase
{
    // --- Partikel ---
    private const int CoinParticleCount = 16;
    private struct CoinParticle { public float X, Y, Alpha, Phase, Speed, Radius; }
    private readonly CoinParticle[] _coins = new CoinParticle[CoinParticleCount];

    // --- Münz-Stapel State ---
    private const int StackCoinCount = 5;
    private readonly float[] _coinScales = new float[StackCoinCount]; // 0→1 Scale-In
    private static readonly float[] CoinThresholds = { 0.10f, 0.25f, 0.40f, 0.60f, 0.80f };

    // --- Farben ---
    private static readonly SKColor ColorBgTop = new(0x0A, 0x1F, 0x0A);
    private static readonly SKColor ColorBgBottom = new(0x04, 0x0D, 0x04);
    private static readonly SKColor ColorChartLine = new(0x22, 0xC5, 0x5E);
    private static readonly SKColor ColorChartFillTop = new(0x22, 0xC5, 0x5E, 0x28);
    private static readonly SKColor ColorChartFillBottom = new(0x22, 0xC5, 0x5E, 0x00);
    private static readonly SKColor ColorGold = new(0xFF, 0xD7, 0x00);
    private static readonly SKColor ColorGoldDark = new(0xB8, 0x86, 0x0B);
    private static readonly SKColor ColorGlowGreen = new(0x22, 0xC5, 0x5E, 0x60);
    private static readonly SKColor ColorGridLine = new(0xFF, 0xFF, 0xFF, 0x08);
    private static readonly SKColor ColorProgressBg = new(0x1A, 0x2A, 0x1A);

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gridPaint = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, Color = ColorGridLine };
    private readonly SKPaint _chartLinePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f, Color = ColorChartLine, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _chartFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _namePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _nameGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _coinBodyPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ColorGold };
    private readonly SKPaint _coinEdgePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, Color = ColorGoldDark };
    private readonly SKPaint _coinTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = new SKColor(0x8B, 0x6B, 0x00) };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particleTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _nameFont = new() { Size = 28f };
    private readonly SKFont _coinSymbolFont = new() { Size = 14f };
    private readonly SKFont _particleSymbolFont = new() { Size = 8f };

    // --- Gecachte Pfade ---
    private readonly SKPath _chartPath = new();
    private readonly SKPath _chartFillPath = new();

    // --- Gecachter MaskFilter für Glow ---
    private readonly SKMaskFilter _glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12f);
    private readonly SKMaskFilter _nameGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);

    public FinanzRechnerSplashRenderer()
    {
        // Partikel initialisieren
        for (var i = 0; i < CoinParticleCount; i++)
        {
            _coins[i] = new CoinParticle
            {
                X = Rng.NextSingle(),
                Y = Rng.NextSingle(),
                Alpha = 30f + Rng.NextSingle() * 50f,
                Phase = Rng.NextSingle() * MathF.Tau,
                Speed = 0.02f + Rng.NextSingle() * 0.04f,
                Radius = 3f + Rng.NextSingle() * 2f
            };
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        // Partikel aufwärts bewegen + Sinus-Oszillation
        for (var i = 0; i < CoinParticleCount; i++)
        {
            ref var c = ref _coins[i];
            c.Y -= c.Speed * deltaTime;
            c.Phase += deltaTime * 1.5f;

            // Wrap: oben raus → unten wieder rein
            if (c.Y < -0.05f)
            {
                c.Y = 1.05f;
                c.X = 0.1f + Rng.NextSingle() * 0.8f;
                c.Alpha = 30f + Rng.NextSingle() * 50f;
            }
        }

        // Münz-Stapel: Scale-In Animation
        for (var i = 0; i < StackCoinCount; i++)
        {
            var target = RenderedProgress >= CoinThresholds[i] ? 1f : 0f;
            var diff = target - _coinScales[i];
            if (MathF.Abs(diff) > 0.001f)
                _coinScales[i] += diff * 0.08f;
            else
                _coinScales[i] = target;
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        // --- Hintergrund: Tiefer Grün-Gradient ---
        using (var bgShader = SKShader.CreateLinearGradient(
                   new SKPoint(w / 2f, 0), new SKPoint(w / 2f, h),
                   new[] { ColorBgTop, ColorBgBottom }, null, SKShaderTileMode.Clamp))
        {
            _bgPaint.Shader = bgShader;
            canvas.DrawRect(bounds, _bgPaint);
            _bgPaint.Shader = null;
        }

        // --- Grid-Pattern ---
        var gridSpacing = 40f;
        for (var x = gridSpacing; x < w; x += gridSpacing)
            canvas.DrawLine(x, 0, x, h, _gridPaint);
        for (var y = gridSpacing; y < h; y += gridSpacing)
            canvas.DrawLine(0, y, w, y, _gridPaint);

        // --- App-Name mit Gold-Schimmer ---
        var nameY = h * 0.15f;
        _nameFont.Size = MathF.Min(28f, w * 0.065f);

        // Gold-Glow hinter dem Namen
        var goldShimmerAlpha = (byte)(40 + 20 * MathF.Sin(Time * 2f));
        _nameGlowPaint.Color = new SKColor(0xFF, 0xD7, 0x00, goldShimmerAlpha);
        _nameGlowPaint.MaskFilter = _nameGlowMask;
        DrawCenteredText(canvas, AppName, nameY, _nameFont, _nameGlowPaint, w);
        _nameGlowPaint.MaskFilter = null;

        // Name weiß darüber
        _namePaint.Color = SKColors.White;
        DrawCenteredText(canvas, AppName, nameY, _nameFont, _namePaint, w);

        // --- Aktien-Chart (Bezier-Kurve) ---
        DrawStockChart(canvas, w, h);

        // --- Münz-Stapel ---
        DrawCoinStack(canvas, w, h);

        // --- Goldene Partikel ---
        DrawCoinParticles(canvas, w, h);

        // --- Fortschrittsbalken ---
        var barWidth = w * 0.55f;
        var barY = h * 0.78f;
        DrawProgressBar(canvas, w, barY, barWidth, 8f, 4f, ColorChartLine, ColorGold, ColorProgressBg);

        // --- Status-Text und Version ---
        DrawStatusText(canvas, w, h * 0.83f);
        DrawVersion(canvas, w, h * 0.92f);
    }

    /// <summary>
    /// Zeichnet den steigenden Aktien-Chart mit Bezier-Kurve und Gradient-Füllung.
    /// </summary>
    private void DrawStockChart(SKCanvas canvas, float w, float h)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        if (progress < 0.01f) return;

        var chartLeft = w * 0.10f;
        var chartRight = chartLeft + w * 0.80f * progress;
        var chartTop = h * 0.35f;
        var chartBottom = h * 0.55f;
        var chartHeight = chartBottom - chartTop;

        // 4 Kontrollpunkte mit leichter Sinus-Schwankung (aufwärts-Tendenz)
        var p0 = new SKPoint(chartLeft, chartBottom - chartHeight * 0.1f);
        var p1 = new SKPoint(
            chartLeft + (chartRight - chartLeft) * 0.30f,
            chartBottom - chartHeight * (0.35f + 0.04f * MathF.Sin(Time * 1.2f)));
        var p2 = new SKPoint(
            chartLeft + (chartRight - chartLeft) * 0.55f,
            chartBottom - chartHeight * (0.25f + 0.03f * MathF.Sin(Time * 1.5f + 1.0f)));
        var p3 = new SKPoint(
            chartRight,
            chartBottom - chartHeight * (0.75f + 0.05f * MathF.Sin(Time * 0.9f + 2.0f)));

        // Linie als Kubische Bezier
        _chartPath.Reset();
        _chartPath.MoveTo(p0);
        _chartPath.CubicTo(p1, p2, p3);

        canvas.DrawPath(_chartPath, _chartLinePaint);

        // Füllung unter der Kurve
        _chartFillPath.Reset();
        _chartFillPath.MoveTo(p0);
        _chartFillPath.CubicTo(p1, p2, p3);
        _chartFillPath.LineTo(chartRight, chartBottom);
        _chartFillPath.LineTo(chartLeft, chartBottom);
        _chartFillPath.Close();

        using (var fillShader = SKShader.CreateLinearGradient(
                   new SKPoint(w / 2f, chartTop), new SKPoint(w / 2f, chartBottom),
                   new[] { ColorChartFillTop, ColorChartFillBottom }, null, SKShaderTileMode.Clamp))
        {
            _chartFillPaint.Shader = fillShader;
            canvas.DrawPath(_chartFillPath, _chartFillPaint);
            _chartFillPaint.Shader = null;
        }

        // Leuchtender Punkt am Ende der Kurve (pulsierend)
        var pulseSize = 6f + 3f * MathF.Sin(Time * 4f);
        _glowPaint.Color = ColorGlowGreen;
        _glowPaint.MaskFilter = _glowMask;
        canvas.DrawCircle(p3.X, p3.Y, pulseSize * 2f, _glowPaint);
        _glowPaint.MaskFilter = null;

        _glowPaint.Color = ColorChartLine;
        canvas.DrawCircle(p3.X, p3.Y, pulseSize * 0.5f, _glowPaint);
    }

    /// <summary>
    /// Zeichnet 5 gestapelte Münzen mit Scale-In-Animation.
    /// </summary>
    private void DrawCoinStack(SKCanvas canvas, float w, float h)
    {
        var stackCenterX = w * 0.50f;
        var stackBaseY = h * 0.68f;
        var coinRadius = MathF.Min(18f, w * 0.045f);
        var coinSpacing = coinRadius * 1.1f;

        _coinSymbolFont.Size = coinRadius * 0.8f;

        for (var i = 0; i < StackCoinCount; i++)
        {
            var scale = _coinScales[i];
            if (scale < 0.01f) continue;

            var coinY = stackBaseY - i * coinSpacing;
            var r = coinRadius * scale;

            // 3D-Effekt: Dunklerer Rand unten
            _coinEdgePaint.StrokeWidth = MathF.Max(1.5f, r * 0.12f);
            canvas.DrawCircle(stackCenterX, coinY + r * 0.08f, r, _coinEdgePaint);

            // Münzkörper
            _coinBodyPaint.Color = ColorGold;
            canvas.DrawCircle(stackCenterX, coinY, r, _coinBodyPaint);

            // Leichter Glanz oben
            _coinBodyPaint.Color = new SKColor(0xFF, 0xE4, 0x4D, 0x60);
            canvas.DrawCircle(stackCenterX, coinY - r * 0.2f, r * 0.5f, _coinBodyPaint);

            // Euro-Zeichen
            _coinTextPaint.Color = new SKColor(0x8B, 0x6B, 0x00, (byte)(255 * scale));
            DrawCenteredText(canvas, "\u20AC", coinY + _coinSymbolFont.Size * 0.35f, _coinSymbolFont, _coinTextPaint, w);
        }
    }

    /// <summary>
    /// Zeichnet goldene Münz-Partikel die aufwärts schweben.
    /// </summary>
    private void DrawCoinParticles(SKCanvas canvas, float w, float h)
    {
        _particleSymbolFont.Size = MathF.Min(8f, w * 0.02f);

        for (var i = 0; i < CoinParticleCount; i++)
        {
            ref var c = ref _coins[i];

            // Sinus-Oszillation horizontal
            var xOff = MathF.Sin(c.Phase) * w * 0.02f;
            var px = c.X * w + xOff;
            var py = c.Y * h;
            var alpha = (byte)Math.Clamp(c.Alpha, 0, 255);

            // Partikel-Kreis
            _particlePaint.Color = new SKColor(0xFF, 0xD7, 0x00, alpha);
            canvas.DrawCircle(px, py, c.Radius, _particlePaint);

            // Kleines Euro-Zeichen auf dem Partikel
            _particleTextPaint.Color = new SKColor(0x8B, 0x6B, 0x00, (byte)(alpha * 0.8f));
            canvas.DrawText("\u20AC", px - _particleSymbolFont.Size * 0.3f, py + _particleSymbolFont.Size * 0.35f, _particleSymbolFont, _particleTextPaint);
        }
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _gridPaint.Dispose();
        _chartLinePaint.Dispose();
        _chartFillPaint.Dispose();
        _glowPaint.Dispose();
        _namePaint.Dispose();
        _nameGlowPaint.Dispose();
        _coinBodyPaint.Dispose();
        _coinEdgePaint.Dispose();
        _coinTextPaint.Dispose();
        _particlePaint.Dispose();
        _particleTextPaint.Dispose();

        _nameFont.Dispose();
        _coinSymbolFont.Dispose();
        _particleSymbolFont.Dispose();

        _chartPath.Dispose();
        _chartFillPath.Dispose();

        _glowMask.Dispose();
        _nameGlowMask.Dispose();
    }
}
