using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// App-spezifischer Splash-Renderer für HandwerkerRechner: "Das Maßband".
/// Warmer Holz-Hintergrund mit Maserungslinien, gelbes Maßband als Fortschrittsbalken,
/// Bleistift-Silhouette am Ende, Sägespäne-Partikel.
/// Alle Paints/Fonts sind gecacht (keine per-frame Allokation).
/// </summary>
public sealed class HandwerkerRechnerSplashRenderer : SplashRendererBase
{
    public HandwerkerRechnerSplashRenderer()
    {
        _titleFont = new SKFont(_titleTypeface) { Size = 26f };
    }

    // --- Farb-Konstanten ---
    private static readonly SKColor BgTop = SKColor.Parse("#1C140E");
    private static readonly SKColor BgBottom = SKColor.Parse("#0D0A07");
    private static readonly SKColor GrainColor = new(0x3A, 0x2A, 0x1A, 77); // Alpha ~30%
    private static readonly SKColor TapeBandColor = SKColor.Parse("#FFC107");
    private static readonly SKColor TapeHousingColor = SKColor.Parse("#D4A017");
    private static readonly SKColor TapeHousingHole = new(0x0D, 0x0A, 0x07);
    private static readonly SKColor PencilBody = SKColor.Parse("#EA580C");
    private static readonly SKColor PencilTip = new(0x2A, 0x1A, 0x0A);
    private static readonly SKColor MarkingColor = new(0x00, 0x00, 0x00, 200);
    private static readonly SKColor TitleColor = SKColor.Parse("#FFF5E6");
    private static readonly SKColor PercentColor = new(0xFF, 0xF5, 0xE6, 180);

    // --- Sägespäne ---
    private const int MaxSawdust = 12;

    private static readonly SKColor[] SawdustColors =
    {
        SKColor.Parse("#8B6914"),
        SKColor.Parse("#A0522D"),
        SKColor.Parse("#DEB887")
    };

    private struct SawdustParticle
    {
        public float X, Y, Alpha, Phase, Speed, Rotation, RotSpeed;
        public SKColor Color;
    }

    private readonly SawdustParticle[] _sawdust = new SawdustParticle[MaxSawdust];

    // --- Holzmaserung-Parameter (8 Linien) ---
    private const int GrainLineCount = 8;
    private readonly float[] _grainFreq = new float[GrainLineCount];
    private readonly float[] _grainAmp = new float[GrainLineCount];
    private readonly float[] _grainPhase = new float[GrainLineCount];
    private readonly float[] _grainY = new float[GrainLineCount];

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _grainPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TitleColor };
    private readonly SKPaint _bandPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TapeBandColor };
    private readonly SKPaint _markingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = MarkingColor };
    private readonly SKPaint _markingTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = MarkingColor };
    private readonly SKPaint _housingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TapeHousingColor };
    private readonly SKPaint _housingHolePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = TapeHousingHole };
    private readonly SKPaint _pencilPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _percentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = PercentColor };
    private readonly SKPaint _sawdustPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts + Typefaces ---
    private readonly SKTypeface _titleTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
    private readonly SKFont _titleFont;
    private readonly SKFont _markingFont = new() { Size = 8f };
    private readonly SKFont _percentFont = new() { Size = 14f };

    // --- Gecachter Path ---
    private readonly SKPath _pencilPath = new();
    private readonly SKPath _grainPath = new();

    protected override void OnUpdate(float deltaTime)
    {
        // Sägespäne bewegen (langsam schwebend, rotierend)
        for (var i = 0; i < MaxSawdust; i++)
        {
            ref var p = ref _sawdust[i];
            p.Phase += deltaTime * 0.8f;
            p.Y += p.Speed * deltaTime * 8f;
            p.X += MathF.Sin(p.Phase) * 0.3f * deltaTime;
            p.Rotation += p.RotSpeed * deltaTime;

            // Wrap wenn unten raus
            if (p.Y > 1.1f)
            {
                p.Y = -0.05f;
                p.X = (float)Rng.NextDouble();
                p.Alpha = 30f + (float)Rng.NextDouble() * 30f;
            }
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeIfNeeded(w, h);

        RenderBackground(canvas, bounds);
        RenderWoodGrain(canvas, w, h);
        RenderSawdust(canvas, w, h);
        RenderAppName(canvas, w, h);
        RenderMeasuringTape(canvas, w, h);
        DrawStatusText(canvas, w, h * 0.70f);
        DrawVersion(canvas, w, h * 0.92f);
    }

    private void InitializeIfNeeded(float w, float h)
    {
        if (IsInitialized) return;
        IsInitialized = true;

        // Sägespäne initialisieren
        for (var i = 0; i < MaxSawdust; i++)
        {
            _sawdust[i] = new SawdustParticle
            {
                X = (float)Rng.NextDouble(),
                Y = (float)Rng.NextDouble(),
                Alpha = 30f + (float)Rng.NextDouble() * 30f,
                Phase = (float)(Rng.NextDouble() * Math.PI * 2),
                Speed = 0.01f + (float)Rng.NextDouble() * 0.02f,
                Rotation = (float)(Rng.NextDouble() * Math.PI * 2),
                RotSpeed = 0.3f + (float)Rng.NextDouble() * 0.8f,
                Color = SawdustColors[Rng.Next(SawdustColors.Length)]
            };
        }

        // Holzmaserung-Linien initialisieren (verschiedene Frequenz/Amplitude/Phase)
        for (var i = 0; i < GrainLineCount; i++)
        {
            _grainFreq[i] = 0.008f + (float)Rng.NextDouble() * 0.012f;
            _grainAmp[i] = 3f + (float)Rng.NextDouble() * 8f;
            _grainPhase[i] = (float)(Rng.NextDouble() * Math.PI * 2);
            _grainY[i] = (i + 1f) / (GrainLineCount + 1f); // Gleichmäßig verteilt
        }
    }

    /// <summary>Warmes Holz-Braun Gradient.</summary>
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

    /// <summary>Horizontale Sinus-Linien als Holzmaserung.</summary>
    private void RenderWoodGrain(SKCanvas canvas, float w, float h)
    {
        _grainPaint.Color = GrainColor;

        for (var i = 0; i < GrainLineCount; i++)
        {
            _grainPath.Reset();
            var baseY = _grainY[i] * h;
            var freq = _grainFreq[i];
            var amp = _grainAmp[i];
            var phase = _grainPhase[i];

            _grainPath.MoveTo(0, baseY + MathF.Sin(phase) * amp);
            // Punkte alle ~20px für glatte Kurve
            var steps = (int)(w / 20f) + 1;
            for (var s = 1; s <= steps; s++)
            {
                var x = s * 20f;
                if (x > w) x = w;
                var y = baseY + MathF.Sin(x * freq + phase) * amp;
                _grainPath.LineTo(x, y);
            }

            canvas.DrawPath(_grainPath, _grainPaint);
        }
    }

    /// <summary>Sägespäne-Partikel (kleine rotierende Rechtecke).</summary>
    private void RenderSawdust(SKCanvas canvas, float w, float h)
    {
        for (var i = 0; i < MaxSawdust; i++)
        {
            ref var p = ref _sawdust[i];
            var px = p.X * w;
            var py = p.Y * h;

            // Alpha pulsiert leicht
            var alpha = (byte)Math.Clamp(p.Alpha + 10f * MathF.Sin(p.Phase * 0.5f), 20, 60);
            _sawdustPaint.Color = p.Color.WithAlpha(alpha);

            canvas.Save();
            canvas.Translate(px, py);
            canvas.RotateRadians(p.Rotation);
            canvas.DrawRect(-2f, -1f, 4f, 2f, _sawdustPaint);
            canvas.Restore();
        }
    }

    /// <summary>App-Name oben bei y ~ 15%, warm-weiß, bold.</summary>
    private void RenderAppName(SKCanvas canvas, float w, float h)
    {
        _titleFont.Size = MathF.Min(26f, w * 0.065f);
        DrawCenteredText(canvas, AppName, h * 0.15f, _titleFont, _titlePaint, w);
    }

    /// <summary>
    /// Gelbes Maßband als Fortschrittsbalken (y ~ 40-55%).
    /// Enthält cm-Markierungen, Bleistift am rechten Ende, Gehäuse links.
    /// </summary>
    private void RenderMeasuringTape(SKCanvas canvas, float w, float h)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var bandHeight = MathF.Min(20f, w * 0.05f);
        var bandMaxWidth = w * 0.7f;
        var bandLeft = w * 0.15f;
        var bandY = h * 0.47f - bandHeight / 2f;
        var bandWidth = bandMaxWidth * progress;

        // --- Maßband-Gehäuse links ---
        var housingSize = MathF.Min(36f, w * 0.09f);
        var housingX = bandLeft - housingSize - 4f;
        var housingY = bandY + bandHeight / 2f - housingSize / 2f;
        var housingRect = new SKRect(housingX, housingY, housingX + housingSize, housingY + housingSize);
        var housingRadius = housingSize * 0.15f;

        canvas.DrawRoundRect(housingRect, housingRadius, housingRadius, _housingPaint);

        // Rundes Loch in der Mitte
        var holeCx = housingX + housingSize / 2f;
        var holeCy = housingY + housingSize / 2f;
        var holeRadius = housingSize * 0.18f;
        canvas.DrawCircle(holeCx, holeCy, holeRadius, _housingHolePaint);

        // --- Gelbes Band ---
        if (bandWidth > 1f)
        {
            var bandRect = new SKRect(bandLeft, bandY, bandLeft + bandWidth, bandY + bandHeight);
            canvas.DrawRoundRect(bandRect, 2f, 2f, _bandPaint);

            // --- cm-Markierungen auf dem Band ---
            _markingFont.Size = MathF.Min(8f, bandHeight * 0.35f);
            var shortTickHeight = bandHeight * 0.25f;
            var longTickHeight = bandHeight * 0.45f;
            var cmSpacing = 15f; // ~15dp pro cm-Einheit
            var longEvery = 5; // Alle 5 Einheiten eine lange Markierung

            var tickCount = (int)(bandWidth / cmSpacing);
            for (var t = 1; t <= tickCount; t++)
            {
                var tx = bandLeft + t * cmSpacing;
                if (tx > bandLeft + bandWidth - 2f) break;

                var isLong = t % longEvery == 0;
                var tickH = isLong ? longTickHeight : shortTickHeight;

                // Strich von unten nach oben
                canvas.DrawLine(tx, bandY + bandHeight, tx, bandY + bandHeight - tickH, _markingPaint);

                // Zahl an langen Strichen
                if (isLong)
                {
                    var numText = t.ToString();
                    var numWidth = _markingFont.MeasureText(numText);
                    canvas.DrawText(numText, tx - numWidth / 2f, bandY + bandHeight * 0.4f, _markingFont, _markingTextPaint);
                }
            }

            // --- Bleistift am rechten Ende ---
            var pencilLen = MathF.Min(22f, w * 0.055f);
            var pencilW = bandHeight * 0.55f;
            var pencilX = bandLeft + bandWidth;
            var pencilCy = bandY + bandHeight / 2f;

            // Bleistift-Körper (Orange)
            _pencilPaint.Color = PencilBody;
            var bodyRect = new SKRect(pencilX, pencilCy - pencilW / 2f, pencilX + pencilLen * 0.7f, pencilCy + pencilW / 2f);
            canvas.DrawRect(bodyRect, _pencilPaint);

            // Bleistift-Spitze (Dreieck, dunkel)
            _pencilPath.Reset();
            _pencilPath.MoveTo(pencilX + pencilLen * 0.7f, pencilCy - pencilW / 2f);
            _pencilPath.LineTo(pencilX + pencilLen, pencilCy);
            _pencilPath.LineTo(pencilX + pencilLen * 0.7f, pencilCy + pencilW / 2f);
            _pencilPath.Close();

            _pencilPaint.Color = PencilTip;
            canvas.DrawPath(_pencilPath, _pencilPaint);
        }

        // --- Prozent-Text ---
        var percentText = $"{(int)(progress * 100)}%";
        _percentFont.Size = MathF.Min(14f, w * 0.035f);

        if (bandWidth > 1f)
        {
            // Unter dem Maßband, zentriert
            var ptW = _percentFont.MeasureText(percentText);
            var ptX = bandLeft + bandWidth / 2f - ptW / 2f;
            canvas.DrawText(percentText, ptX, bandY + bandHeight + _percentFont.Size + 6f, _percentFont, _percentPaint);
        }
        else
        {
            // Bei 0% links neben dem Gehäuse
            canvas.DrawText(percentText, bandLeft, bandY + bandHeight + _percentFont.Size + 6f, _percentFont, _percentPaint);
        }
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _grainPaint.Dispose();
        _titlePaint.Dispose();
        _bandPaint.Dispose();
        _markingPaint.Dispose();
        _markingTextPaint.Dispose();
        _housingPaint.Dispose();
        _housingHolePaint.Dispose();
        _pencilPaint.Dispose();
        _percentPaint.Dispose();
        _sawdustPaint.Dispose();
        _titleFont.Dispose();
        _titleTypeface.Dispose();
        _markingFont.Dispose();
        _percentFont.Dispose();
        _pencilPath.Dispose();
        _grainPath.Dispose();
    }
}
