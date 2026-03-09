namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Schicksals-Wendepunkt Overlay: "Das Schicksal hat sich verändert..."
/// Glitch-Effekt, dramatische Präsentation, Auto-Dismiss nach 2.5 Sekunden.
/// </summary>
public class FateChangedOverlay : Scene
{
    private float _time;
    private const float Duration = 2.5f;
    private const float FadeIn = 0.3f;
    private const float FadeOut = 0.5f;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, StrokeWidth = 1f };
    private static readonly SKFont _mainFont = new() { LinearMetrics = true };
    private static readonly SKFont _subFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    private readonly ILocalizationService _localization;
    private string _fateText = "Fate has changed...";

    public FateChangedOverlay(ILocalizationService localization)
    {
        _localization = localization;
        _fateText = _localization.GetString("FateChanged") ?? "Fate has changed...";
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        if (_time >= Duration + FadeOut)
            SceneManager.HideOverlay(this);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Alpha berechnen
        float alpha;
        if (_time < FadeIn)
            alpha = _time / FadeIn;
        else if (_time > Duration)
            alpha = Math.Max(0, 1f - (_time - Duration) / FadeOut);
        else
            alpha = 1f;

        var byteAlpha = (byte)(alpha * 255);

        // Schwarzer Hintergrund
        _bgPaint.Color = new SKColor(0, 0, 0, (byte)(byteAlpha * 0.85f));
        canvas.DrawRect(bounds, _bgPaint);

        // Glitch-Streifen
        if (_time < 0.8f)
        {
            var glitchIntensity = 1f - _time / 0.8f;
            var rng = new Random((int)(_time * 1000));
            _bgPaint.Color = UIRenderer.Danger.WithAlpha((byte)(glitchIntensity * 30));
            for (int i = 0; i < 6; i++)
            {
                var y = rng.NextSingle() * bounds.Height;
                var h = rng.NextSingle() * bounds.Height * 0.05f;
                var offset = (rng.NextSingle() - 0.5f) * bounds.Width * 0.3f * glitchIntensity;
                canvas.DrawRect(offset, y, bounds.Width, h, _bgPaint);
            }
        }

        // Horizontale Trennlinien (dramatisch)
        var lineY1 = bounds.MidY - bounds.Height * 0.08f;
        var lineY2 = bounds.MidY + bounds.Height * 0.08f;
        _linePaint.Color = UIRenderer.Danger.WithAlpha((byte)(byteAlpha * 0.4f));
        canvas.DrawLine(0, lineY1, bounds.Width, lineY1, _linePaint);
        canvas.DrawLine(0, lineY2, bounds.Width, lineY2, _linePaint);

        // Haupttext
        _mainFont.Size = bounds.Width * 0.055f;
        _textPaint.Color = UIRenderer.Danger.WithAlpha(byteAlpha);
        canvas.DrawText(_fateText,
            bounds.MidX, bounds.MidY, SKTextAlign.Center, _mainFont, _textPaint);

        // Untertitel
        _subFont.Size = bounds.Width * 0.03f;
        _textPaint.Color = UIRenderer.TextMuted.WithAlpha((byte)(byteAlpha * 0.7f));
        canvas.DrawText(">>> FATE_DIVERGENCE DETECTED",
            bounds.MidX, bounds.MidY + bounds.Height * 0.06f,
            SKTextAlign.Center, _subFont, _textPaint);

        // Pulsierender Rand
        var pulseAlpha = (byte)(MathF.Sin(_time * 8f) * 20 + 30);
        _linePaint.Color = UIRenderer.Danger.WithAlpha((byte)(pulseAlpha * alpha));
        canvas.DrawRect(bounds.Left + 2, bounds.Top + 2, bounds.Width - 4, bounds.Height - 4, _linePaint);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        // Tap zum vorzeitigen Schließen
        if (action == InputAction.Tap && _time > 1f)
            SceneManager.HideOverlay(this);
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _linePaint.Dispose();
        _mainFont.Dispose();
        _subFont.Dispose();
        _textPaint.Dispose();
    }
}
