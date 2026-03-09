namespace RebornSaga.Overlays;

using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// ARIA System-Nachricht Overlay. Zeigt holographische Nachrichten
/// mit Auto-Dismiss nach einstellbarer Zeit.
/// </summary>
public class SystemMessageOverlay : Scene
{
    private float _time;
    private string _message = "";
    private float _dismissTime = 4f; // Automatisch schließen nach X Sekunden
    private float _fadeInDuration = 0.3f;
    private float _fadeOutDuration = 0.5f;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKFont _messageFont = new() { LinearMetrics = true };
    private static readonly SKFont _headerFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    /// <summary>Setzt die Nachricht und optionale Dismiss-Zeit.</summary>
    public void SetMessage(string message, float dismissAfter = 4f)
    {
        _message = message;
        _dismissTime = dismissAfter;
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Auto-Dismiss
        if (_time >= _dismissTime + _fadeOutDuration)
            SceneManager.HideOverlay(this);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Fade-Alpha berechnen
        float alpha;
        if (_time < _fadeInDuration)
            alpha = _time / _fadeInDuration;
        else if (_time > _dismissTime)
            alpha = Math.Max(0, 1f - (_time - _dismissTime) / _fadeOutDuration);
        else
            alpha = 1f;

        var byteAlpha = (byte)(alpha * 255);

        // System-Fenster (zentral, schmaler als StatusWindow)
        var panelW = bounds.Width * 0.8f;
        var panelH = bounds.Height * 0.15f;
        var panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.MidY - panelH / 2,
            bounds.MidX + panelW / 2, bounds.MidY + panelH / 2);

        // Hintergrund
        _bgPaint.Color = new SKColor(0x0A, 0x0E, 0x15, (byte)(byteAlpha * 0.9f));
        using var roundRect = new SKRoundRect(panelRect, 6f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Glow-Rand
        _borderPaint.Color = UIRenderer.Primary.WithAlpha((byte)(byteAlpha * 0.6f));
        _borderPaint.MaskFilter = _glowBlur;
        canvas.DrawRoundRect(roundRect, _borderPaint);
        _borderPaint.MaskFilter = null;

        // Normaler Rand
        _borderPaint.Color = UIRenderer.Primary.WithAlpha((byte)(byteAlpha * 0.4f));
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // Header: ">>> SYSTEM"
        _headerFont.Size = panelH * 0.18f;
        _textPaint.Color = UIRenderer.Primary.WithAlpha(byteAlpha);
        canvas.DrawText(">>> SYSTEM", panelRect.Left + 15, panelRect.Top + panelH * 0.3f,
            SKTextAlign.Left, _headerFont, _textPaint);

        // Nachricht
        _messageFont.Size = panelH * 0.22f;
        _textPaint.Color = UIRenderer.PrimaryGlow.WithAlpha(byteAlpha);
        canvas.DrawText(_message, panelRect.MidX, panelRect.Top + panelH * 0.65f,
            SKTextAlign.Center, _messageFont, _textPaint);

        // Scan-Line Effekt
        if (alpha > 0.5f)
        {
            var scanY = panelRect.Top + ((_time * 100f) % panelH);
            _bgPaint.Color = UIRenderer.Primary.WithAlpha(15);
            canvas.DrawRect(panelRect.Left, scanY, panelW, 2, _bgPaint);
        }
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        // Tap zum sofortigen Schließen
        if (action == InputAction.Tap)
            SceneManager.HideOverlay(this);
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _messageFont.Dispose();
        _headerFont.Dispose();
        _textPaint.Dispose();
        // _glowBlur ist static readonly — NICHT disposen
    }
}
