using SkiaSharp;
using MeineApps.UI.SkiaSharp.SplashScreen;

namespace ZeitManager.Graphics;

/// <summary>
/// App-spezifischer Splash-Renderer für ZeitManager: "Die tickende Uhr".
/// Große analoge Uhr mit Sekundenzeiger-Tick, kreisförmiger Progress-Ring,
/// rotierende Zahnrad-Partikel, konzentrische dekorative Ringe.
/// Alle Paints/Fonts sind gecacht (kein per-frame Allokation).
/// </summary>
public sealed class ZeitManagerSplashRenderer : SplashRendererBase
{
    // --- Farb-Konstanten (passend zur Schokobraun-Palette) ---
    private static readonly SKColor BgTop = SKColor.Parse("#382C22");
    private static readonly SKColor BgBottom = SKColor.Parse("#1A1410");
    private static readonly SKColor SecondHandColor = SKColor.Parse("#EF4444");
    private static readonly SKColor ProgressStart = SKColor.Parse("#F7A833");
    private static readonly SKColor ProgressEnd = SKColor.Parse("#FFBD55");

    // --- Zahnrad-Partikel ---
    private const int MaxGears = 12;

    private struct GearParticle
    {
        public float Angle, Distance, Radius, RotationSpeed, Rotation, Alpha;
        public int Teeth;
    }

    private readonly GearParticle[] _gears = new GearParticle[MaxGears];

    // --- Gecachte Paints ---
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _ringPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _hourHandPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 3f };
    private readonly SKPaint _minuteHandPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 2f };
    private readonly SKPaint _secondHandPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 1.5f };
    private readonly SKPaint _centerDotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _progressBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _progressFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4f, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _gearPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _percentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // --- Gecachte Fonts ---
    private readonly SKFont _titleFont = new() { Embolden = true, Size = 28f };
    private readonly SKFont _percentFont = new() { Size = 13f };

    // --- Gecachte Paths ---
    private readonly SKPath _gearPath = new();

    protected override void OnUpdate(float deltaTime)
    {
        // Zahnräder drehen
        for (var i = 0; i < MaxGears; i++)
        {
            ref var g = ref _gears[i];
            g.Rotation += g.RotationSpeed * deltaTime;
            if (g.Rotation > MathF.PI * 2f) g.Rotation -= MathF.PI * 2f;
        }
    }

    protected override void OnRender(SKCanvas canvas, SKRect bounds)
    {
        var w = bounds.Width;
        var h = bounds.Height;

        InitializeIfNeeded();

        RenderBackground(canvas, bounds);

        // Uhr-Zentrum
        var cx = w / 2f;
        var cy = h * 0.40f;
        var clockRadius = MathF.Min(70f, MathF.Min(w, h) * 0.15f);

        RenderDecoRings(canvas, cx, cy, clockRadius);
        RenderGearParticles(canvas, cx, cy, clockRadius);
        RenderClockFace(canvas, cx, cy, clockRadius);
        RenderClockHands(canvas, cx, cy, clockRadius);
        RenderProgressRing(canvas, cx, cy, clockRadius + 15f, w);

        // Prozent-Text unter der Uhr
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var percentText = $"{(int)(progress * 100)}%";
        _percentFont.Size = MathF.Min(13f, w * 0.033f);
        _percentPaint.Color = new SKColor(0xAA, 0xAA, 0xAA);
        DrawCenteredText(canvas, percentText, cy + clockRadius + 38f, _percentFont, _percentPaint, w);

        // App-Name oben bei y ~ 12%
        RenderAppName(canvas, w, h);

        // Status-Text unter Progress
        DrawStatusText(canvas, w, cy + clockRadius + 56f);

        // Version ganz unten bei y ~ 92%
        DrawVersion(canvas, w, h * 0.92f);
    }

    private void InitializeIfNeeded()
    {
        if (IsInitialized) return;
        IsInitialized = true;

        for (var i = 0; i < MaxGears; i++)
        {
            _gears[i] = new GearParticle
            {
                Angle = (float)(Rng.NextDouble() * Math.PI * 2),
                Distance = 100f + (float)Rng.NextDouble() * 40f,
                Radius = 4f + (float)Rng.NextDouble() * 3f,
                RotationSpeed = 0.5f + (float)Rng.NextDouble() * 1.5f,
                Rotation = (float)(Rng.NextDouble() * Math.PI * 2),
                Alpha = 30f + (float)Rng.NextDouble() * 30f,
                Teeth = 5 + Rng.Next(2) // 5 oder 6 Zähne
            };
            // Abwechselnd Drehrichtung
            if (i % 2 == 0) _gears[i].RotationSpeed = -_gears[i].RotationSpeed;
        }
    }

    /// <summary>Dunkler Indigo-Gradient.</summary>
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

    /// <summary>3 konzentrische dekorative Ringe um die Uhr.</summary>
    private void RenderDecoRings(SKCanvas canvas, float cx, float cy, float clockRadius)
    {
        _ringPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 15);
        canvas.DrawCircle(cx, cy, clockRadius + 25f, _ringPaint);
        canvas.DrawCircle(cx, cy, clockRadius + 35f, _ringPaint);
        canvas.DrawCircle(cx, cy, clockRadius + 45f, _ringPaint);
    }

    /// <summary>12 Strich-Markierungen (lang bei 12/3/6/9).</summary>
    private void RenderClockFace(SKCanvas canvas, float cx, float cy, float radius)
    {
        _tickPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 120);

        for (var i = 0; i < 12; i++)
        {
            var angle = (float)(i * 30 * Math.PI / 180) - MathF.PI / 2f;
            var isMajor = i % 3 == 0;

            var innerRadius = isMajor ? radius * 0.72f : radius * 0.82f;
            _tickPaint.StrokeWidth = isMajor ? 2f : 1f;

            var x1 = cx + MathF.Cos(angle) * innerRadius;
            var y1 = cy + MathF.Sin(angle) * innerRadius;
            var x2 = cx + MathF.Cos(angle) * (radius * 0.92f);
            var y2 = cy + MathF.Sin(angle) * (radius * 0.92f);

            canvas.DrawLine(x1, y1, x2, y2, _tickPaint);
        }
    }

    /// <summary>
    /// Stundenzeiger auf 10 Uhr, Minutenzeiger auf 2 Uhr, Sekundenzeiger tickt diskret.
    /// </summary>
    private void RenderClockHands(SKCanvas canvas, float cx, float cy, float radius)
    {
        // Stundenzeiger → 10 Uhr (300° = -60° vom 12-Uhr-Punkt)
        var hourAngle = 300f * MathF.PI / 180f - MathF.PI / 2f;
        _hourHandPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 200);
        canvas.DrawLine(cx, cy,
            cx + MathF.Cos(hourAngle) * radius * 0.48f,
            cy + MathF.Sin(hourAngle) * radius * 0.48f,
            _hourHandPaint);

        // Minutenzeiger → 2 Uhr (60° vom 12-Uhr-Punkt)
        var minuteAngle = 60f * MathF.PI / 180f - MathF.PI / 2f;
        _minuteHandPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 180);
        canvas.DrawLine(cx, cy,
            cx + MathF.Cos(minuteAngle) * radius * 0.68f,
            cy + MathF.Sin(minuteAngle) * radius * 0.68f,
            _minuteHandPaint);

        // Sekundenzeiger: Snap-Tick (6° pro Sekunde, diskrete Schritte)
        var seconds = (int)(Time % 60f);
        var secondAngle = seconds * 6f * MathF.PI / 180f - MathF.PI / 2f;
        _secondHandPaint.Color = SecondHandColor;
        canvas.DrawLine(
            cx - MathF.Cos(secondAngle) * radius * 0.15f,
            cy - MathF.Sin(secondAngle) * radius * 0.15f,
            cx + MathF.Cos(secondAngle) * radius * 0.78f,
            cy + MathF.Sin(secondAngle) * radius * 0.78f,
            _secondHandPaint);

        // Zentrum-Punkt (rot)
        _centerDotPaint.Color = SecondHandColor;
        canvas.DrawCircle(cx, cy, 3f, _centerDotPaint);
    }

    /// <summary>
    /// Kreisförmiger Progress-Ring statt horizontalem Balken.
    /// Arc von -90° (oben) im Uhrzeigersinn.
    /// </summary>
    private void RenderProgressRing(SKCanvas canvas, float cx, float cy, float ringRadius, float canvasWidth)
    {
        var progress = Math.Clamp(RenderedProgress, 0f, 1f);
        var ringRect = new SKRect(cx - ringRadius, cy - ringRadius, cx + ringRadius, cy + ringRadius);

        // Skaliere StrokeWidth proportional
        var strokeWidth = MathF.Min(4f, canvasWidth * 0.01f);
        _progressBgPaint.StrokeWidth = strokeWidth;
        _progressFillPaint.StrokeWidth = strokeWidth;

        // Hintergrund-Ring (komplett, dunkelgrau)
        _progressBgPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, 30);
        canvas.DrawArc(ringRect, 0f, 360f, false, _progressBgPaint);

        // Progress-Arc mit Gradient
        if (progress > 0.005f)
        {
            var sweepAngle = progress * 360f;

            using var shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { ProgressStart, ProgressEnd },
                new[] { 0f, 1f });

            _progressFillPaint.Shader = shader;
            // Starte bei -90° (oben)
            canvas.DrawArc(ringRect, -90f, sweepAngle, false, _progressFillPaint);
            _progressFillPaint.Shader = null;
        }
    }

    /// <summary>Rotierende Zahnrad-Partikel um die Uhr.</summary>
    private void RenderGearParticles(SKCanvas canvas, float cx, float cy, float clockRadius)
    {
        for (var i = 0; i < MaxGears; i++)
        {
            ref var g = ref _gears[i];

            // Position auf Kreisbahn um die Uhr
            // Skaliere Distance relativ zum Uhrenradius
            var scaledDist = clockRadius * (g.Distance / 70f);
            var gx = cx + MathF.Cos(g.Angle) * scaledDist;
            var gy = cy + MathF.Sin(g.Angle) * scaledDist;

            // Skaliere Zahnrad-Radius proportional
            var scaledRadius = g.Radius * (clockRadius / 70f);

            var alpha = (byte)Math.Clamp(g.Alpha + 10f * MathF.Sin(Time + g.Angle), 25, 65);
            _gearPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);

            DrawGear(canvas, gx, gy, scaledRadius, g.Teeth, g.Rotation);
        }
    }

    /// <summary>Zeichnet ein einzelnes Zahnrad (Zähne als Ausstülpungen auf dem Kreis).</summary>
    private void DrawGear(SKCanvas canvas, float cx, float cy, float radius, int teeth, float rotation)
    {
        // Innerer Kreis
        canvas.DrawCircle(cx, cy, radius * 0.5f, _gearPaint);

        // Zähne als kurze Linien-Paare
        var toothAngle = MathF.PI * 2f / teeth;
        var innerR = radius * 0.7f;
        var outerR = radius;

        _gearPath.Reset();

        for (var t = 0; t < teeth; t++)
        {
            var baseAngle = rotation + t * toothAngle;
            var halfTooth = toothAngle * 0.25f;

            // Zahnflanke links (innen → außen)
            var a1 = baseAngle - halfTooth;
            var a2 = baseAngle + halfTooth;

            // Innerer Punkt links
            var ix1 = cx + MathF.Cos(a1) * innerR;
            var iy1 = cy + MathF.Sin(a1) * innerR;
            // Äußerer Punkt links
            var ox1 = cx + MathF.Cos(a1) * outerR;
            var oy1 = cy + MathF.Sin(a1) * outerR;
            // Äußerer Punkt rechts
            var ox2 = cx + MathF.Cos(a2) * outerR;
            var oy2 = cy + MathF.Sin(a2) * outerR;
            // Innerer Punkt rechts
            var ix2 = cx + MathF.Cos(a2) * innerR;
            var iy2 = cy + MathF.Sin(a2) * innerR;

            _gearPath.MoveTo(ix1, iy1);
            _gearPath.LineTo(ox1, oy1);
            _gearPath.LineTo(ox2, oy2);
            _gearPath.LineTo(ix2, iy2);
        }

        canvas.DrawPath(_gearPath, _gearPaint);
    }

    /// <summary>App-Name oben bei y ~ 12%, weiß, bold.</summary>
    private void RenderAppName(SKCanvas canvas, float w, float h)
    {
        var centerY = h * 0.12f;
        _titleFont.Size = MathF.Min(28f, w * 0.065f);
        _titlePaint.Color = new SKColor(0xF8, 0xFA, 0xFC);
        DrawCenteredText(canvas, AppName, centerY + _titleFont.Size * 0.35f, _titleFont, _titlePaint, w);
    }

    protected override void OnDispose()
    {
        _bgPaint.Dispose();
        _ringPaint.Dispose();
        _tickPaint.Dispose();
        _hourHandPaint.Dispose();
        _minuteHandPaint.Dispose();
        _secondHandPaint.Dispose();
        _centerDotPaint.Dispose();
        _progressBgPaint.Dispose();
        _progressFillPaint.Dispose();
        _gearPaint.Dispose();
        _titlePaint.Dispose();
        _percentPaint.Dispose();
        _titleFont.Dispose();
        _percentFont.Dispose();
        _gearPath.Dispose();
    }
}
