namespace RebornSaga.Rendering.Effects;

using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Vollbild-Charakter-Portrait bei Ultimate-Moves.
/// Zeigt Klassen-Silhouette mit Glow, Speed-Lines und Skill-Name.
/// Anwendung: Ultimate-Skill-Aktivierung, Boss-Spezial-Attacke.
/// </summary>
public class SplashArtRenderer
{
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, StrokeWidth = 2f, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _accentLinePaint = new() { IsAntialias = true, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKFont _nameFont = new() { LinearMetrics = true };
    private static readonly SKFont _subtitleFont = new() { LinearMetrics = true };
    private static readonly SKMaskFilter _glow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);
    // Deterministische Längen-Offsets für Speed-Lines (kein Random pro Frame)
    private static readonly float[] _lineOffsets = { 0.02f, 0.08f, 0.05f, 0.01f, 0.09f, 0.03f, 0.07f, 0.04f, 0.06f, 0.0f, 0.08f, 0.03f, 0.07f, 0.01f, 0.05f, 0.09f };

    private float _time;
    private float _duration;
    private bool _isActive;
    private string _skillName = "";
    private string _subtitle = "";
    private SKColor _accentColor;

    /// <summary>Ob der Splash aktiv ist.</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Startet den Splash-Art-Effekt.
    /// </summary>
    public void Start(string skillName, string subtitle, SKColor accentColor, float duration = 1.5f)
    {
        _skillName = skillName;
        _subtitle = subtitle;
        _accentColor = accentColor;
        _duration = duration;
        _time = 0;
        _isActive = true;
    }

    /// <summary>Stoppt den Effekt.</summary>
    public void Stop() => _isActive = false;

    /// <summary>Update pro Frame.</summary>
    public void Update(float deltaTime)
    {
        if (!_isActive) return;
        _time += deltaTime;
        if (_time >= _duration)
            _isActive = false;
    }

    /// <summary>
    /// Rendert den Splash-Art-Effekt als Overlay.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        if (!_isActive) return;

        var t = _time / _duration;

        // Fade-In/Out
        float alpha;
        if (t < 0.15f) alpha = t / 0.15f;
        else if (t > 0.8f) alpha = (1f - t) / 0.2f;
        else alpha = 1f;

        var byteAlpha = (byte)(alpha * 255);

        // Dramatischer Hintergrund
        _bgPaint.Color = new SKColor(0, 0, 0, (byte)(200 * alpha));
        canvas.DrawRect(bounds, _bgPaint);

        // Zentrale Glow-Kugel
        _glowPaint.Color = _accentColor.WithAlpha((byte)(100 * alpha));
        _glowPaint.MaskFilter = _glow;
        var glowRadius = bounds.Width * 0.3f * (0.8f + 0.2f * MathF.Sin(_time * 4f));
        canvas.DrawCircle(bounds.MidX, bounds.MidY * 0.8f, glowRadius, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Speed-Lines (radial vom Zentrum)
        _linePaint.Color = _accentColor.WithAlpha((byte)(80 * alpha));
        var lineCount = 16;
        for (int i = 0; i < lineCount; i++)
        {
            var angle = (i / (float)lineCount) * MathF.PI * 2f + _time * 2f;
            var innerR = bounds.Width * 0.15f;
            var outerR = bounds.Width * 0.5f + _lineOffsets[i] * bounds.Width;

            var cx = bounds.MidX;
            var cy = bounds.MidY * 0.8f;
            canvas.DrawLine(
                cx + MathF.Cos(angle) * innerR, cy + MathF.Sin(angle) * innerR,
                cx + MathF.Cos(angle) * outerR, cy + MathF.Sin(angle) * outerR,
                _linePaint);
        }

        // Skill-Name (groß, zentriert)
        var slideIn = Math.Min(1f, t / 0.2f);
        var nameX = bounds.MidX;
        var nameY = bounds.MidY + bounds.Height * 0.15f;

        _nameFont.Size = bounds.Width * 0.08f;
        _textPaint.Color = SKColors.White.WithAlpha(byteAlpha);
        canvas.DrawText(_skillName, nameX - bounds.Width * 0.3f * (1f - slideIn),
            nameY, SKTextAlign.Center, _nameFont, _textPaint);

        // Untertitel (Klasse oder Beschreibung)
        _subtitleFont.Size = bounds.Width * 0.035f;
        _textPaint.Color = _accentColor.WithAlpha(byteAlpha);
        canvas.DrawText(_subtitle, nameX, nameY + bounds.Width * 0.06f,
            SKTextAlign.Center, _subtitleFont, _textPaint);

        // Horizontale Akzent-Linien um den Namen
        var lineY = nameY - bounds.Width * 0.01f;
        var lineW = bounds.Width * 0.35f * slideIn;
        _accentLinePaint.Color = _accentColor.WithAlpha((byte)(150 * alpha));
        canvas.DrawLine(nameX - lineW, lineY, nameX + lineW, lineY, _accentLinePaint);
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _glowPaint.Dispose();
        _linePaint.Dispose();
        _accentLinePaint.Dispose();
        _textPaint.Dispose();
        _nameFont.Dispose();
        _subtitleFont.Dispose();
        // _glow ist static readonly — NICHT disposen
    }
}
