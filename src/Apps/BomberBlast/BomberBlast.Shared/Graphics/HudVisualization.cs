using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// Erweiterte HUD-Elemente: Animierter Score-Counter (Ziffern rollen bei Änderung),
/// pulsierender Timer unter 30s, PowerUp-Icons mit Glow-Aura.
/// Wird von GameRenderer aufgerufen als optionale Enhancement-Schicht.
/// </summary>
public static class HudVisualization
{
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _iconStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKFont _scoreFont = new() { Size = 28f };
    private static readonly SKFont _timerFont = new() { Size = 22f };
    private static readonly SKFont _labelFont = new() { Size = 10f };

    // Score-Counter Animation
    private static int _displayScore;
    private static int _targetScore;
    private static float _scoreAnimTime;

    // Timer-Warnung
    private static readonly SKColor _timerNormal = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor _timerWarning = new(0xEF, 0x44, 0x44);
    private static readonly SKColor _timerCritical = new(0xFF, 0x00, 0x00);

    /// <summary>
    /// Rendert einen animierten Score-Counter.
    /// Ziffern rollen stufenweise hoch statt direkt zu springen.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="x">X-Position (linksbündig)</param>
    /// <param name="y">Y-Position (Baseline)</param>
    /// <param name="currentScore">Aktueller Ziel-Score</param>
    /// <param name="deltaTime">Frame-Delta für Animation</param>
    /// <param name="fontSize">Schriftgröße</param>
    public static void DrawAnimatedScore(SKCanvas canvas, float x, float y,
        int currentScore, float deltaTime, float fontSize = 28f)
    {
        _targetScore = currentScore;

        // Score rolliert stufenweise zum Ziel
        if (_displayScore < _targetScore)
        {
            _scoreAnimTime += deltaTime;
            int step = Math.Max(1, (_targetScore - _displayScore) / 10);
            _displayScore = Math.Min(_displayScore + step, _targetScore);
        }
        else if (_displayScore > _targetScore)
        {
            _displayScore = _targetScore;
        }

        string scoreStr = _displayScore.ToString("N0");

        // Glow hinter dem Score (wenn sich Score ändert)
        if (_displayScore != _targetScore || _scoreAnimTime < 0.3f)
        {
            float glowAlpha = Math.Max(0, 1f - _scoreAnimTime * 3f);
            _glowPaint.Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(glowAlpha * 80));
            _glowPaint.MaskFilter?.Dispose();
            _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f);
            _scoreFont.Size = fontSize;
            canvas.DrawText(scoreStr, x, y, SKTextAlign.Left, _scoreFont, _glowPaint);
            _glowPaint.MaskFilter?.Dispose();
            _glowPaint.MaskFilter = null;
        }
        else
        {
            _scoreAnimTime = 0;
        }

        // Score-Text
        _textPaint.Color = SKColors.White;
        _scoreFont.Size = fontSize;
        canvas.DrawText(scoreStr, x, y, SKTextAlign.Left, _scoreFont, _textPaint);
    }

    /// <summary>
    /// Rendert einen Timer mit Puls-Effekt unter 30 Sekunden.
    /// Text wird rot + pulsiert (Scale-Bounce) + Glow bei kritischem Timer.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Zentrierte X-Position</param>
    /// <param name="y">Y-Position (Baseline)</param>
    /// <param name="remainingSeconds">Verbleibende Sekunden</param>
    /// <param name="animTime">Globale Animations-Zeit</param>
    /// <param name="fontSize">Schriftgröße</param>
    public static void DrawPulsingTimer(SKCanvas canvas, float cx, float y,
        float remainingSeconds, float animTime, float fontSize = 22f)
    {
        // Farbe basierend auf verbleibender Zeit
        SKColor timerColor;
        float pulseIntensity = 0;

        if (remainingSeconds > 30)
        {
            timerColor = _timerNormal;
        }
        else if (remainingSeconds > 10)
        {
            // 30-10s: Gelb→Rot Übergang
            float t = (30f - remainingSeconds) / 20f;
            timerColor = Lerp(_timerWarning, _timerCritical, t);
            pulseIntensity = 0.3f + t * 0.4f;
        }
        else
        {
            // <10s: Rot + starkes Pulsieren
            timerColor = _timerCritical;
            pulseIntensity = 0.7f + 0.3f * (1f - remainingSeconds / 10f);
        }

        // Timer-Text formatieren
        int mins = (int)(remainingSeconds / 60);
        int secs = (int)(remainingSeconds % 60);
        string timerStr = $"{mins}:{secs:D2}";

        // Puls-Effekt (Scale-Bounce)
        if (pulseIntensity > 0)
        {
            float pulse = MathF.Sin(animTime * (8f + pulseIntensity * 8f));
            float scale = 1f + pulse * pulseIntensity * 0.15f;

            // Glow
            float glowAlpha = (0.5f + pulse * 0.5f) * pulseIntensity;
            _glowPaint.Color = timerColor.WithAlpha((byte)(glowAlpha * 100));
            _glowPaint.MaskFilter?.Dispose();
            _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);
            _timerFont.Size = fontSize * scale;
            canvas.DrawText(timerStr, cx, y, SKTextAlign.Center, _timerFont, _glowPaint);
            _glowPaint.MaskFilter?.Dispose();
            _glowPaint.MaskFilter = null;

            // Text mit Scale
            _textPaint.Color = timerColor;
            _timerFont.Size = fontSize * scale;
            canvas.DrawText(timerStr, cx, y, SKTextAlign.Center, _timerFont, _textPaint);
        }
        else
        {
            _textPaint.Color = timerColor;
            _timerFont.Size = fontSize;
            canvas.DrawText(timerStr, cx, y, SKTextAlign.Center, _timerFont, _textPaint);
        }
    }

    /// <summary>
    /// Rendert ein PowerUp-Icon mit Glow-Aura.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="size">Icon-Größe (Durchmesser)</param>
    /// <param name="color">PowerUp-Farbe</param>
    /// <param name="animTime">Animation-Zeit für Glow-Pulsation</param>
    /// <param name="label">Optionales Label (z.B. "3" für Anzahl)</param>
    public static void DrawPowerUpIcon(SKCanvas canvas, float cx, float cy,
        float size, SKColor color, float animTime, string? label = null)
    {
        float r = size / 2f;
        float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 4f);

        // Glow-Aura (pulsierend)
        _glowPaint.Color = color.WithAlpha((byte)(pulse * 50));
        _glowPaint.MaskFilter?.Dispose();
        _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.4f);
        canvas.DrawCircle(cx, cy, r * 1.2f, _glowPaint);
        _glowPaint.MaskFilter?.Dispose();
        _glowPaint.MaskFilter = null;

        // Icon-Hintergrund (Kreis)
        _iconPaint.Color = color.WithAlpha(200);
        canvas.DrawCircle(cx, cy, r, _iconPaint);

        // Heller Ring
        _iconStroke.Color = color.WithAlpha((byte)(pulse * 180));
        canvas.DrawCircle(cx, cy, r, _iconStroke);

        // Highlight (obere Hälfte, heller)
        _iconPaint.Color = SKColors.White.WithAlpha(40);
        canvas.Save();
        canvas.ClipRect(new SKRect(cx - r, cy - r, cx + r, cy));
        canvas.DrawCircle(cx, cy, r * 0.85f, _iconPaint);
        canvas.Restore();

        // Label (z.B. Anzahl)
        if (!string.IsNullOrEmpty(label))
        {
            _textPaint.Color = SKColors.White;
            _labelFont.Size = size * 0.4f;
            canvas.DrawText(label, cx, cy + size * 0.15f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }

    /// <summary>
    /// Reset des Score-Counters (bei neuem Level).
    /// </summary>
    public static void ResetScore()
    {
        _displayScore = 0;
        _targetScore = 0;
        _scoreAnimTime = 0;
    }

    /// <summary>
    /// Lineare Farbinterpolation.
    /// </summary>
    private static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}
