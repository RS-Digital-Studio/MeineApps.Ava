using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Animierter Lade-Bildschirm: App-Name, zwei gegenläufige Zahnräder,
/// animierter Fortschrittsbalken und rotierende Tipps.
/// Wird als SkiaSharp-Overlay über dem Loading-Screen gerendert.
/// </summary>
public class LoadingScreenRenderer
{
    // Gecachte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true };
    private static readonly SKPaint _gearPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gearStrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _barBgPaint = new() { Color = new SKColor(0x33, 0x33, 0x33), IsAntialias = true };
    private static readonly SKPaint _barFillPaint = new() { IsAntialias = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Color = SKColors.White };
    private static readonly SKPaint _subtitlePaint = new() { IsAntialias = true, Color = new SKColor(0xFF, 0xA7, 0x26) };
    private static readonly SKPaint _tipPaint = new() { IsAntialias = true, Color = new SKColor(0xBB, 0xBB, 0xBB) };
    private static readonly SKPaint _sparkPaint = new() { IsAntialias = true };

    private static readonly SKFont _titleFont = new(SKTypeface.Default, 28) { Edging = SKFontEdging.SubpixelAntialias };
    private static readonly SKFont _subtitleFont = new(SKTypeface.Default, 14) { Edging = SKFontEdging.SubpixelAntialias };
    private static readonly SKFont _tipFont = new(SKTypeface.Default, 11) { Edging = SKFontEdging.SubpixelAntialias };

    // Tipps (werden rotiert angezeigt)
    private string[] _tips = [];
    private int _currentTipIndex;
    private float _tipTimer;
    private const float TipInterval = 3f;

    /// <summary>
    /// Setzt die Tipps für die rotierende Anzeige.
    /// </summary>
    public void SetTips(string[] tips)
    {
        _tips = tips;
        _currentTipIndex = 0;
        _tipTimer = 0;
    }

    /// <summary>
    /// Rendert den Loading-Screen mit Animationen.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds, float time)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // Hintergrund: Dunkler Gradient (oben→unten)
        DrawBackground(canvas, bounds);

        // App-Titel + Untertitel
        DrawTitle(canvas, bounds, cx, cy - 80, time);

        // Zwei gegenläufige Zahnräder (zentriert)
        DrawGears(canvas, cx, cy + 10, time);

        // Funken um die Zahnräder
        DrawSparks(canvas, cx, cy + 10, time);

        // Fortschrittsbalken
        DrawProgressBar(canvas, bounds, cx, cy + 60, time);

        // Tipp-Text (rotierend)
        DrawTip(canvas, bounds, cx, cy + 100, time);
    }

    // ═════════════════════════════════════════════════════════════════
    // HINTERGRUND
    // ═════════════════════════════════════════════════════════════════

    private static void DrawBackground(SKCanvas canvas, SKRect bounds)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Left, bounds.Bottom),
            new SKColor[] { new(0x1A, 0x1A, 0x2E), new(0x0D, 0x0D, 0x1A) },
            null, SKShaderTileMode.Clamp);
        _bgPaint.Shader = shader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;

        // Subtile Vignette
        using var vignetteShader = SKShader.CreateRadialGradient(
            new SKPoint(bounds.MidX, bounds.MidY),
            Math.Max(bounds.Width, bounds.Height) * 0.7f,
            new SKColor[] { SKColors.Transparent, new SKColor(0x00, 0x00, 0x00, 0x80) },
            null, SKShaderTileMode.Clamp);
        _bgPaint.Shader = vignetteShader;
        canvas.DrawRect(bounds, _bgPaint);
        _bgPaint.Shader = null;
    }

    // ═════════════════════════════════════════════════════════════════
    // TITEL
    // ═════════════════════════════════════════════════════════════════

    private static void DrawTitle(SKCanvas canvas, SKRect bounds, float cx, float y, float time)
    {
        // App-Name mit leichtem Glow-Puls
        float pulse = 0.9f + MathF.Sin(time * 2f) * 0.1f;
        byte alpha = (byte)(pulse * 255);
        _textPaint.Color = new SKColor(0xFF, 0xFF, 0xFF, alpha);

        string title = "HandwerkerImperium";
        float titleWidth = _titleFont.MeasureText(title);
        canvas.DrawText(title, cx - titleWidth / 2f, y, SKTextAlign.Left, _titleFont, _textPaint);

        // Untertitel
        string subtitle = "Baue dein Imperium";
        float subWidth = _subtitleFont.MeasureText(subtitle);
        _subtitlePaint.Color = new SKColor(0xFF, 0xA7, 0x26, (byte)(alpha * 0.8f));
        canvas.DrawText(subtitle, cx - subWidth / 2f, y + 22, SKTextAlign.Left, _subtitleFont, _subtitlePaint);
    }

    // ═════════════════════════════════════════════════════════════════
    // ZAHNRÄDER (zwei gegenläufig rotierende)
    // ═════════════════════════════════════════════════════════════════

    private static void DrawGears(SKCanvas canvas, float cx, float cy, float time)
    {
        float gearRadius1 = 22;
        float gearRadius2 = 16;
        float offset = gearRadius1 + gearRadius2 - 4; // Überlappung für Verzahnung

        // Linkes Zahnrad (im Uhrzeigersinn)
        DrawSingleGear(canvas, cx - offset / 2f, cy, gearRadius1, 10, time * 40f,
            new SKColor(0xEA, 0x58, 0x0C)); // Craft-Orange

        // Rechtes Zahnrad (gegen Uhrzeigersinn, angepasste Geschwindigkeit)
        float gearRatio = gearRadius1 / gearRadius2;
        DrawSingleGear(canvas, cx + offset / 2f, cy, gearRadius2, 8, -time * 40f * gearRatio,
            new SKColor(0xFF, 0xA7, 0x26)); // Gold-Orange
    }

    private static void DrawSingleGear(SKCanvas canvas, float cx, float cy,
        float radius, int teeth, float angleDeg, SKColor color)
    {
        canvas.Save();
        canvas.RotateDegrees(angleDeg, cx, cy);

        using var path = new SKPath();

        float toothDepth = radius * 0.2f;
        float innerRadius = radius - toothDepth;
        float toothAngle = 360f / teeth;
        float halfTooth = toothAngle * 0.3f;

        // Zahnrad-Pfad erzeugen
        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = i * toothAngle;
            float rad1 = (baseAngle - halfTooth) * MathF.PI / 180f;
            float rad2 = (baseAngle + halfTooth) * MathF.PI / 180f;
            float radMid1 = (baseAngle - halfTooth * 0.8f) * MathF.PI / 180f;
            float radMid2 = (baseAngle + halfTooth * 0.8f) * MathF.PI / 180f;

            if (i == 0)
            {
                path.MoveTo(cx + innerRadius * MathF.Cos(rad1), cy + innerRadius * MathF.Sin(rad1));
            }
            else
            {
                float prevAngle = (baseAngle - toothAngle + halfTooth) * MathF.PI / 180f;
                // Innerer Bogen zum nächsten Zahn
                path.LineTo(cx + innerRadius * MathF.Cos(rad1), cy + innerRadius * MathF.Sin(rad1));
            }

            // Zahnflanke hoch
            path.LineTo(cx + radius * MathF.Cos(radMid1), cy + radius * MathF.Sin(radMid1));
            // Zahnspitze
            path.LineTo(cx + radius * MathF.Cos(radMid2), cy + radius * MathF.Sin(radMid2));
            // Zahnflanke runter
            path.LineTo(cx + innerRadius * MathF.Cos(rad2), cy + innerRadius * MathF.Sin(rad2));
        }
        path.Close();

        // Zahnrad füllen
        _gearPaint.Color = color;
        canvas.DrawPath(path, _gearPaint);

        // Dunkler Rand
        _gearStrokePaint.Color = new SKColor((byte)(color.Red * 0.7f), (byte)(color.Green * 0.7f), (byte)(color.Blue * 0.7f));
        canvas.DrawPath(path, _gearStrokePaint);

        // Nabe (Mitte)
        float hubRadius = radius * 0.35f;
        _gearPaint.Color = new SKColor((byte)(color.Red * 0.85f), (byte)(color.Green * 0.85f), (byte)(color.Blue * 0.85f));
        canvas.DrawCircle(cx, cy, hubRadius, _gearPaint);

        // Naben-Loch
        _gearPaint.Color = new SKColor(0x1A, 0x1A, 0x2E);
        canvas.DrawCircle(cx, cy, hubRadius * 0.4f, _gearPaint);

        canvas.Restore();
    }

    // ═════════════════════════════════════════════════════════════════
    // FUNKEN (um die Zahnräder)
    // ═════════════════════════════════════════════════════════════════

    private static void DrawSparks(SKCanvas canvas, float cx, float cy, float time)
    {
        for (int i = 0; i < 6; i++)
        {
            float phase = (time * 1.5f + i * 1.047f) % 2f; // 1.047 = PI/3
            if (phase > 1f) continue;

            float angle = (i * 60f + time * 30f) * MathF.PI / 180f;
            float dist = 28 + phase * 18;
            float sx = cx + MathF.Cos(angle) * dist;
            float sy = cy + MathF.Sin(angle) * dist;

            byte alpha = (byte)((1f - phase) * 200);
            float size = (1f - phase) * 2.5f;

            _sparkPaint.Color = new SKColor(0xFF, 0xD5, 0x4F, alpha);
            canvas.DrawCircle(sx, sy, size, _sparkPaint);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // FORTSCHRITTSBALKEN
    // ═════════════════════════════════════════════════════════════════

    private static void DrawProgressBar(SKCanvas canvas, SKRect bounds, float cx, float y, float time)
    {
        float barWidth = Math.Min(bounds.Width * 0.6f, 240);
        float barHeight = 6;
        float barLeft = cx - barWidth / 2f;

        // Hintergrund
        var barRect = new SKRect(barLeft, y, barLeft + barWidth, y + barHeight);
        canvas.DrawRoundRect(barRect, 3, 3, _barBgPaint);

        // Animierter Fortschritt (indeterminiert: wandernder Block)
        float progress = (time * 0.4f) % 1f;
        float blockWidth = barWidth * 0.3f;
        float blockStart = barLeft + progress * (barWidth - blockWidth);

        var fillRect = new SKRect(blockStart, y, blockStart + blockWidth, y + barHeight);

        using var fillShader = SKShader.CreateLinearGradient(
            new SKPoint(fillRect.Left, y),
            new SKPoint(fillRect.Right, y),
            new SKColor[] { new(0xEA, 0x58, 0x0C, 0x60), new(0xEA, 0x58, 0x0C), new(0xFF, 0xA7, 0x26), new(0xEA, 0x58, 0x0C, 0x60) },
            new float[] { 0, 0.2f, 0.8f, 1f },
            SKShaderTileMode.Clamp);
        _barFillPaint.Shader = fillShader;

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(barRect, 3, 3));
        canvas.DrawRect(fillRect, _barFillPaint);
        canvas.Restore();
        _barFillPaint.Shader = null;
    }

    // ═════════════════════════════════════════════════════════════════
    // TIPP-TEXT
    // ═════════════════════════════════════════════════════════════════

    private void DrawTip(SKCanvas canvas, SKRect bounds, float cx, float y, float time)
    {
        if (_tips.Length == 0) return;

        // Tipp-Rotation
        _tipTimer += 0.05f; // 50ms pro Frame bei 20fps
        if (_tipTimer >= TipInterval)
        {
            _tipTimer = 0;
            _currentTipIndex = (_currentTipIndex + 1) % _tips.Length;
        }

        // Fade-In/Out am Übergang
        float fadeProgress = _tipTimer / TipInterval;
        float alpha;
        if (fadeProgress < 0.1f)
            alpha = fadeProgress / 0.1f; // Fade-In
        else if (fadeProgress > 0.9f)
            alpha = (1f - fadeProgress) / 0.1f; // Fade-Out
        else
            alpha = 1f;

        _tipPaint.Color = new SKColor(0xBB, 0xBB, 0xBB, (byte)(alpha * 200));

        string tip = _tips[_currentTipIndex % _tips.Length];
        float tipWidth = _tipFont.MeasureText(tip);
        canvas.DrawText(tip, cx - tipWidth / 2f, y, SKTextAlign.Left, _tipFont, _tipPaint);
    }
}
