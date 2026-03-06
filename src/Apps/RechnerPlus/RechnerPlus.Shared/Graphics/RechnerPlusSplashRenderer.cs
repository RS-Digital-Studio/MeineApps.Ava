using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace RechnerPlus.Graphics;

/// <summary>
/// App-spezifischer Splash-Renderer für RechnerPlus: "Die saubere Gleichung".
/// Taschenrechner-Tasten-Matrix mit diagonaler Sweep-Welle, mathematische Partikel,
/// LCD-Display-Stil App-Name, Punkt-Grid Hintergrund.
/// Alle Paints/Fonts sind gecacht (kein per-frame Allokation).
/// </summary>
public sealed class RechnerPlusSplashRenderer : SplashRendererBase
{
    public RechnerPlusSplashRenderer()
    {
        _titleFont = new SKFont(_titleTypeface) { Size = 28f };
    }

    // --- Farb-Konstanten (passend zur Violet-Palette) ---
    private static readonly SKColor BgTop = SKColor.Parse("#302A56");
    private static readonly SKColor BgBottom = SKColor.Parse("#1A1430");
    private static readonly SKColor GlowColor = SKColor.Parse("#7C7FF7");
    private static readonly SKColor KeyDark = SKColor.Parse("#3E3870");
    private static readonly SKColor ProgressStart = SKColor.Parse("#7C7FF7");
    private static readonly SKColor ProgressEnd = SKColor.Parse("#9598FA");

    // --- Tasten-Layout (4x4) ---
    private static readonly string[] KeyLabels =
    {
        "7", "8", "9", "/",
        "4", "5", "6", "x",
        "1", "2", "3", "-",
        "0", ".", "=", "+"
    };

    // --- Mathe-Partikel ---
    private const int MaxMathParticles = 16;
    private static readonly char[] MathSymbols = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '-', 'x', '/', '=', '%' };

    private struct MathParticle
    {
        public float X, Y, Alpha, Phase, Speed;
        public char Symbol;
    }

    private readonly MathParticle[] _mathParticles = new MathParticle[MaxMathParticles];

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _keyBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _keyGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _keyTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _titleGlowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _particlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts + Typefaces ---
    private readonly SKTypeface _titleTypeface = SKTypeface.FromFamilyName("Courier New", SKFontStyle.Bold);
    private readonly SKFont _titleFont;
    private readonly SKFont _keyFont = new() { Size = 16f };
    private readonly SKFont _particleFont = new() { Size = 14f };

    // --- Vorberechnete Symbol-Strings (vermeidet per-frame ToString()) ---
    private static readonly string[] MathSymbolStrings = Array.ConvertAll(MathSymbols, c => c.ToString());

    // --- Gecachte MaskFilter ---
    private SKMaskFilter? _glowFilter;
    private SKMaskFilter? _titleGlowFilter;

    // --- Gecachter Path + RoundRect für Tasten ---
    private readonly SKPath _keyPath = new();
    private readonly SKRoundRect _keyRoundRect = new();

    protected override void OnUpdate(float deltaTime)
    {
        // Mathe-Partikel bewegen (langsam aufsteigend)
        for (var i = 0; i < MaxMathParticles; i++)
        {
            ref var p = ref _mathParticles[i];
            p.Phase += deltaTime * 1.2f;
            p.Y -= p.Speed * deltaTime;

            // Sinus-Floating
            p.X += MathF.Sin(p.Phase) * 0.15f * deltaTime;

            // Wrap wenn oben raus
            if (p.Y < -0.05f)
            {
                p.Y = 1.05f;
                p.X = (float)Rng.NextDouble();
                p.Symbol = MathSymbols[Rng.Next(MathSymbols.Length)];
            }
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeIfNeeded(w, h);

        _glowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);
        _titleGlowFilter ??= SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);

        RenderBackground(canvas, bounds);
        RenderDotGrid(canvas, w, h);
        RenderMathParticles(canvas, w, h);
        RenderAppName(canvas, w, h);
        RenderKeyMatrix(canvas, w, h);

        // Progress-Balken bei y ~ 75%
        var barWidth = MathF.Min(240f, w * 0.6f);
        DrawProgressBar(canvas, w, h * 0.75f, barWidth, 6f, 3f, ProgressStart, ProgressEnd, new SKColor(0x1A, 0x1E, 0x3A));

        // Status-Text bei y ~ 80%
        DrawStatusText(canvas, w, h * 0.80f);

        // Version bei y ~ 92%
        DrawVersion(canvas, w, h * 0.92f);
    }

    private void InitializeIfNeeded(float w, float h)
    {
        if (IsInitialized) return;
        IsInitialized = true;

        for (var i = 0; i < MaxMathParticles; i++)
        {
            _mathParticles[i] = new MathParticle
            {
                X = (float)Rng.NextDouble(),
                Y = (float)Rng.NextDouble(),
                Alpha = 40f + (float)Rng.NextDouble() * 60f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2),
                Speed = 0.02f + (float)Rng.NextDouble() * 0.04f,
                Symbol = MathSymbols[Rng.Next(MathSymbols.Length)]
            };
        }
    }

    /// <summary>Tiefblauer Gradient-Hintergrund.</summary>
    private void RenderBackground(SKCanvas canvas, SKRect bounds)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.MidX, bounds.Top),
            new SKPoint(bounds.MidX, bounds.Bottom),
            new[] { BgTop, BgBottom },
            null,
            SKShaderTileMode.Clamp);

        _bgPaint.Shader = shader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    /// <summary>Subtiles Punkt-Grid (mathematisches Papier-Gefühl).</summary>
    private void RenderDotGrid(SKCanvas canvas, float w, float h)
    {
        _dotPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 15);
        const float spacing = 30f;
        var cols = (int)(w / spacing) + 1;
        var rows = (int)(h / spacing) + 1;

        for (var row = 0; row < rows; row++)
        {
            var py = row * spacing;
            for (var col = 0; col < cols; col++)
            {
                canvas.DrawCircle(col * spacing, py, 0.8f, _dotPaint);
            }
        }
    }

    /// <summary>Schwebende Mathe-Zeichen als Partikel.</summary>
    private void RenderMathParticles(SKCanvas canvas, float w, float h)
    {
        _particleFont.Size = MathF.Min(14f, w * 0.035f);

        for (var i = 0; i < MaxMathParticles; i++)
        {
            ref var p = ref _mathParticles[i];
            var px = p.X * w;
            var py = p.Y * h;

            // Alpha pulsiert leicht
            var alpha = (byte)Math.Clamp(p.Alpha + 20f * MathF.Sin(p.Phase * 0.5f), 40, 100);
            _particlePaint.Color = new SKColor(0x63, 0x66, 0xF1, alpha);

            var symbolIdx = Array.IndexOf(MathSymbols, p.Symbol);
            var symbolStr = symbolIdx >= 0 ? MathSymbolStrings[symbolIdx] : p.Symbol.ToString();
            var textWidth = _particleFont.MeasureText(symbolStr);
            canvas.DrawText(symbolStr, px - textWidth * 0.5f, py, _particleFont, _particlePaint);
        }
    }

    /// <summary>App-Name im LCD-Display-Stil bei y ~ 18%.</summary>
    private void RenderAppName(SKCanvas canvas, float w, float h)
    {
        var centerY = h * 0.18f;
        _titleFont.Size = MathF.Min(28f, w * 0.07f);

        // Glow-Hintergrund
        var glowAlpha = (byte)(25 + 15 * MathF.Sin(Time * 2f));
        _titleGlowPaint.Color = GlowColor.WithAlpha(glowAlpha);
        _titleGlowPaint.MaskFilter = _titleGlowFilter;

        var titleWidth = _titleFont.MeasureText(AppName);
        var titleX = (w - titleWidth) / 2f;

        canvas.DrawCircle(w / 2f, centerY, titleWidth * 0.5f, _titleGlowPaint);
        _titleGlowPaint.MaskFilter = null;

        // Text
        _titlePaint.Color = new SKColor(0xE0, 0xE7, 0xFF);
        canvas.DrawText(AppName, titleX, centerY + _titleFont.Size * 0.35f, _titleFont, _titlePaint);
    }

    /// <summary>
    /// 4x4 Taschenrechner-Matrix mit diagonaler Sweep-Welle.
    /// Aktive Tasten (auf der Diagonale) leuchten, Rest bleibt dunkel.
    /// </summary>
    private void RenderKeyMatrix(SKCanvas canvas, float w, float h)
    {
        var keySize = MathF.Min(40f, w * 0.09f);
        var spacing = keySize * 0.3f;
        var totalWidth = 4 * keySize + 3 * spacing;
        var totalHeight = 4 * keySize + 3 * spacing;
        var startX = (w - totalWidth) / 2f;
        var startY = h * 0.35f - totalHeight / 2f;
        var cornerRadius = keySize * 0.2f;

        // Diagonale Sweep-Position (0-6, wandert über Zeit)
        var sweepPos = (Time * 1.8f) % 8f;

        _keyFont.Size = MathF.Min(16f, keySize * 0.4f);

        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var idx = row * 4 + col;
                var kx = startX + col * (keySize + spacing);
                var ky = startY + row * (keySize + spacing);

                // Diagonale: row + col ergibt die Diagonal-Position (0-6)
                var diag = row + col;
                var dist = MathF.Abs(diag - sweepPos);
                // Wrap für die zyklische Animation
                var distWrap = MathF.Min(dist, 8f - dist);
                var isActive = distWrap < 1.2f;
                var intensity = MathF.Max(0f, 1f - distWrap / 1.2f);

                // Taste zeichnen
                var keyRect = new SKRect(kx, ky, kx + keySize, ky + keySize);
                _keyPath.Reset();
                _keyRoundRect.SetRect(keyRect, cornerRadius, cornerRadius);
                _keyPath.AddRoundRect(_keyRoundRect);

                if (isActive)
                {
                    // Glow-Effekt für aktive Tasten
                    _keyGlowPaint.Color = GlowColor.WithAlpha((byte)(60 * intensity));
                    _keyGlowPaint.MaskFilter = _glowFilter;
                    canvas.DrawPath(_keyPath, _keyGlowPaint);
                    _keyGlowPaint.MaskFilter = null;

                    // Helle Taste
                    var bgAlpha = (byte)(40 + 80 * intensity);
                    _keyBgPaint.Color = GlowColor.WithAlpha(bgAlpha);
                }
                else
                {
                    // Dunkle Taste
                    _keyBgPaint.Color = KeyDark;
                }

                canvas.DrawPath(_keyPath, _keyBgPaint);

                // Tasten-Text
                var label = KeyLabels[idx];
                var textAlpha = isActive ? (byte)(180 + 75 * intensity) : (byte)120;
                _keyTextPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, textAlpha);

                var textW = _keyFont.MeasureText(label);
                canvas.DrawText(label, kx + (keySize - textW) / 2f, ky + keySize / 2f + _keyFont.Size * 0.35f, _keyFont, _keyTextPaint);
            }
        }
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _dotPaint.Dispose();
        _keyBgPaint.Dispose();
        _keyGlowPaint.Dispose();
        _keyTextPaint.Dispose();
        _titlePaint.Dispose();
        _titleGlowPaint.Dispose();
        _particlePaint.Dispose();
        _titleFont.Dispose();
        _titleTypeface.Dispose();
        _keyFont.Dispose();
        _particleFont.Dispose();
        _keyPath.Dispose();
        _keyRoundRect.Dispose();
        _glowFilter?.Dispose();
        _titleGlowFilter?.Dispose();
    }
}
