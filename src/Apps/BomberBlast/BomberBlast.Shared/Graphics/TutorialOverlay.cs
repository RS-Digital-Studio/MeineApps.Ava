using BomberBlast.Models;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// SkiaSharp-basiertes Tutorial-Overlay das im Canvas gerendert wird.
/// Zeigt Highlight-Box, Pfeil und Textblase für den aktuellen Tutorial-Schritt.
/// </summary>
public class TutorialOverlay : IDisposable
{
    private readonly ILocalizationService _localizationService;

    // Gecachte SKPaint/SKFont (einmalig erstellt)
    private readonly SKPaint _dimPaint = new() { Color = new SKColor(0, 0, 0, 140) };
    private readonly SKPaint _highlightPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 3,
        Color = new SKColor(255, 215, 0), // Gold
        IsAntialias = true
    };
    private readonly SKPaint _bubblePaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(30, 30, 40, 230),
        IsAntialias = true
    };
    private readonly SKPaint _bubbleBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        Color = new SKColor(255, 215, 0, 180),
        IsAntialias = true
    };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _skipPaint = new() { Color = new SKColor(180, 180, 180), IsAntialias = true };
    private readonly SKPaint _skipBgPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(60, 60, 70, 200),
        IsAntialias = true
    };
    private readonly SKFont _textFont = new() { Size = 18, Embolden = true };
    private readonly SKFont _skipFont = new() { Size = 14 };
    private readonly SKPath _arrowPath = new();

    // Skip-Button Hit-Test Rectangle
    private SKRect _skipButtonRect;

    // Animation
    private float _pulseTimer;

    public TutorialOverlay(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Tutorial-Overlay rendern
    /// </summary>
    public void Render(SKCanvas canvas, float screenWidth, float screenHeight,
        TutorialStep step, float scale, float offsetX, float offsetY)
    {
        _pulseTimer += 0.016f; // ~60fps

        // Highlight-Bereich bestimmen
        var highlightRect = GetHighlightRect(step.Highlight, screenWidth, screenHeight, scale, offsetX, offsetY);

        // Semi-transparenten Hintergrund zeichnen (mit Loch für Highlight)
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, screenWidth, screenHeight));
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _dimPaint);
        canvas.Restore();

        // Pulsierender Highlight-Rahmen
        float pulse = MathF.Sin(_pulseTimer * 4f) * 2f;
        _highlightPaint.StrokeWidth = 3f + pulse;
        canvas.DrawRoundRect(highlightRect.Left - 4, highlightRect.Top - 4,
            highlightRect.Width + 8, highlightRect.Height + 8, 8, 8, _highlightPaint);

        // Text-Blase
        string text = _localizationService.GetString(step.TextKey) ?? step.TextKey;
        float bubbleWidth = Math.Min(screenWidth * 0.5f, 320);
        float bubbleHeight = 50;
        float bubbleX = screenWidth / 2 - bubbleWidth / 2;
        float bubbleY = screenHeight * 0.15f;

        // Blase zeichnen
        canvas.DrawRoundRect(bubbleX, bubbleY, bubbleWidth, bubbleHeight, 12, 12, _bubblePaint);
        canvas.DrawRoundRect(bubbleX, bubbleY, bubbleWidth, bubbleHeight, 12, 12, _bubbleBorderPaint);

        // Text in Blase
        canvas.DrawText(text, bubbleX + bubbleWidth / 2, bubbleY + bubbleHeight / 2 + 6,
            SKTextAlign.Center, _textFont, _textPaint);

        // Pfeil von Blase zum Highlight
        DrawArrow(canvas, bubbleX + bubbleWidth / 2, bubbleY + bubbleHeight,
            highlightRect.MidX, highlightRect.Top - 8);

        // "Überspringen" Button (oben rechts)
        string skipText = _localizationService.GetString("TutorialSkip") ?? "Skip >";
        float skipWidth = 80;
        float skipHeight = 30;
        float skipX = screenWidth - skipWidth - 10;
        float skipY = 10;
        _skipButtonRect = new SKRect(skipX, skipY, skipX + skipWidth, skipY + skipHeight);

        canvas.DrawRoundRect(skipX, skipY, skipWidth, skipHeight, 6, 6, _skipBgPaint);
        canvas.DrawText(skipText, skipX + skipWidth / 2, skipY + skipHeight / 2 + 5,
            SKTextAlign.Center, _skipFont, _skipPaint);
    }

    /// <summary>
    /// Prüft ob ein Touch auf den Skip-Button fällt
    /// </summary>
    public bool IsSkipButtonHit(float x, float y)
    {
        return _skipButtonRect.Contains(x, y);
    }

    private void DrawArrow(SKCanvas canvas, float fromX, float fromY, float toX, float toY)
    {
        _highlightPaint.StrokeWidth = 2;
        canvas.DrawLine(fromX, fromY, toX, toY, _highlightPaint);

        // Pfeilspitze
        float angle = MathF.Atan2(toY - fromY, toX - fromX);
        float arrowSize = 10;
        float arrowAngle = MathF.PI / 6;

        _arrowPath.Reset();
        _arrowPath.MoveTo(toX, toY);
        _arrowPath.LineTo(
            toX - arrowSize * MathF.Cos(angle - arrowAngle),
            toY - arrowSize * MathF.Sin(angle - arrowAngle));
        _arrowPath.MoveTo(toX, toY);
        _arrowPath.LineTo(
            toX - arrowSize * MathF.Cos(angle + arrowAngle),
            toY - arrowSize * MathF.Sin(angle + arrowAngle));

        canvas.DrawPath(_arrowPath, _highlightPaint);
    }

    private static SKRect GetHighlightRect(TutorialHighlight highlight,
        float screenWidth, float screenHeight, float scale, float offsetX, float offsetY)
    {
        return highlight switch
        {
            // Input-Controls: Links unten
            TutorialHighlight.InputControl => new SKRect(
                10, screenHeight - 180, 200, screenHeight - 10),

            // Bomb-Button: Rechts unten
            TutorialHighlight.BombButton => new SKRect(
                screenWidth - 100, screenHeight - 100, screenWidth - 10, screenHeight - 10),

            // Spielfeld-Mitte
            TutorialHighlight.GameField => new SKRect(
                offsetX, offsetY, offsetX + 200 * scale, offsetY + 150 * scale),

            // PowerUp / Exit: Spielfeld-Mitte (dynamisch, aber statischer Fallback)
            TutorialHighlight.PowerUp or TutorialHighlight.Exit => new SKRect(
                screenWidth * 0.3f, screenHeight * 0.3f,
                screenWidth * 0.7f, screenHeight * 0.7f),

            _ => new SKRect(screenWidth * 0.25f, screenHeight * 0.25f,
                screenWidth * 0.75f, screenHeight * 0.75f)
        };
    }

    public void Dispose()
    {
        _dimPaint.Dispose();
        _highlightPaint.Dispose();
        _bubblePaint.Dispose();
        _bubbleBorderPaint.Dispose();
        _textPaint.Dispose();
        _skipPaint.Dispose();
        _skipBgPaint.Dispose();
        _textFont.Dispose();
        _skipFont.Dispose();
        _arrowPath.Dispose();
    }
}
