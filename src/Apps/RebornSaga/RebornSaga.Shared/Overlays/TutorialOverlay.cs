namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Tutorial-Overlay: Zeigt ARIA-Textbox mit Highlight-Bereich.
/// Dimmt den Bildschirm außerhalb des Highlight-Bereichs ab.
/// Wird über TutorialService gesteuert (SeenHints-Tracking).
/// </summary>
public class TutorialOverlay : Scene
{
    private readonly TutorialService _tutorialService;

    private float _time;
    private string _hintId = "";
    private string _title = "";
    private string _message = "";
    private SKRect _highlightRect; // Bereich der hervorgehoben wird (leer = kein Highlight)

    // Gepoolte Paints
    private static readonly SKPaint _dimPaint = new() { IsAntialias = false };
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _highlightBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _messageFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKMaskFilter _highlightGlow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    private readonly ILocalizationService _localization;
    private string _tapContinueText = "Tap to continue";

    public TutorialOverlay(TutorialService tutorialService, ILocalizationService localization)
    {
        _tutorialService = tutorialService;
        _localization = localization;
        _tapContinueText = _localization.GetString("TapToContinueTutorial") ?? "Tap to continue";
    }

    /// <summary>Konfiguriert den Tutorial-Hint.</summary>
    public void SetHint(string hintId, string title, string message, SKRect highlightRect = default)
    {
        _hintId = hintId;
        _title = title;
        _message = message;
        _highlightRect = highlightRect;
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        var alpha = Math.Min(1f, _time / 0.3f);
        var byteAlpha = (byte)(alpha * 255);

        // Dimmen außerhalb Highlight
        _dimPaint.Color = new SKColor(0, 0, 0, (byte)(160 * alpha));

        if (_highlightRect.Width > 0 && _highlightRect.Height > 0)
        {
            // 4 Rechtecke um den Highlight-Bereich
            canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, _highlightRect.Top - bounds.Top, _dimPaint);
            canvas.DrawRect(bounds.Left, _highlightRect.Top, _highlightRect.Left - bounds.Left, _highlightRect.Height, _dimPaint);
            canvas.DrawRect(_highlightRect.Right, _highlightRect.Top, bounds.Right - _highlightRect.Right, _highlightRect.Height, _dimPaint);
            canvas.DrawRect(bounds.Left, _highlightRect.Bottom, bounds.Width, bounds.Bottom - _highlightRect.Bottom, _dimPaint);

            // Glow um den Highlight-Bereich
            _highlightBorderPaint.Color = UIRenderer.Primary.WithAlpha((byte)(200 * alpha));
            _highlightBorderPaint.MaskFilter = _highlightGlow;
            canvas.DrawRect(_highlightRect, _highlightBorderPaint);
            _highlightBorderPaint.MaskFilter = null;
            canvas.DrawRect(_highlightRect, _highlightBorderPaint);
        }
        else
        {
            canvas.DrawRect(bounds, _dimPaint);
        }

        // ARIA-Textbox (unten, 70% Breite)
        var boxW = bounds.Width * 0.85f;
        var boxH = bounds.Height * 0.18f;
        var boxX = bounds.MidX - boxW / 2;
        var boxY = bounds.Bottom - boxH - bounds.Height * 0.05f;
        var boxRect = new SKRect(boxX, boxY, boxX + boxW, boxY + boxH);

        // Panel-Hintergrund
        _bgPaint.Color = new SKColor(0x0A, 0x0E, 0x18, (byte)(230 * alpha));
        using var roundRect = new SKRoundRect(boxRect, 8f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Rand
        _borderPaint.Color = UIRenderer.Primary.WithAlpha((byte)(120 * alpha));
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // ARIA-Label
        _titleFont.Size = boxH * 0.2f;
        _textPaint.Color = UIRenderer.Primary.WithAlpha(byteAlpha);
        canvas.DrawText($"[ARIA] {_title}", boxRect.Left + 15, boxRect.Top + boxH * 0.25f,
            SKTextAlign.Left, _titleFont, _textPaint);

        // Nachricht
        _messageFont.Size = boxH * 0.18f;
        _textPaint.Color = UIRenderer.TextPrimary.WithAlpha(byteAlpha);
        canvas.DrawText(_message, boxRect.Left + 15, boxRect.Top + boxH * 0.55f,
            SKTextAlign.Left, _messageFont, _textPaint);

        // "Tippe zum Fortfahren" Hint
        if (_time > 1f)
        {
            var hintAlpha = (byte)(Math.Abs(MathF.Sin(_time * 2f)) * 150 * alpha);
            _textPaint.Color = UIRenderer.TextMuted.WithAlpha(hintAlpha);
            _messageFont.Size = boxH * 0.14f;
            canvas.DrawText(_tapContinueText, boxRect.Right - 15, boxRect.Bottom - boxH * 0.15f,
                SKTextAlign.Right, _messageFont, _textPaint);
        }
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Tap && _time > 0.5f)
        {
            _tutorialService.MarkSeen(_hintId);
            SceneManager.HideOverlay(this);
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _dimPaint.Dispose();
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _highlightBorderPaint.Dispose();
        _titleFont.Dispose();
        _messageFont.Dispose();
        _textPaint.Dispose();
        _highlightGlow.Dispose();
    }
}
