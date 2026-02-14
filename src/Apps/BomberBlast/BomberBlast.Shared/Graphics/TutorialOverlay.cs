using BomberBlast.Models;
using MeineApps.Core.Ava.Localization;
using SkiaSharp;

namespace BomberBlast.Graphics;

/// <summary>
/// SkiaSharp-basiertes Tutorial-Overlay das im Canvas gerendert wird.
/// Zeigt Highlight-Box mit Cutout, Pfeil und Textblase fuer den aktuellen Tutorial-Schritt.
/// Dim-Overlay hat ein "Loch" beim Highlight, sodass der hervorgehobene Bereich klar sichtbar ist.
/// </summary>
public class TutorialOverlay : IDisposable
{
    private readonly ILocalizationService _localizationService;

    // Gecachte SKPaint/SKFont (einmalig erstellt)
    private readonly SKPaint _dimPaint = new() { Color = new SKColor(0, 0, 0, 100) };
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
        Color = new SKColor(20, 20, 30, 128),
        IsAntialias = true
    };
    private readonly SKPaint _bubbleBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        Color = new SKColor(255, 215, 0, 200),
        IsAntialias = true
    };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _skipPaint = new() { Color = new SKColor(200, 200, 200), IsAntialias = true };
    private readonly SKPaint _skipBgPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = new SKColor(60, 60, 70, 220),
        IsAntialias = true
    };
    private readonly SKPaint _skipBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1,
        Color = new SKColor(180, 180, 180, 120),
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

        // Erweiterten Highlight-Rect fuer Cutout (etwas groesser als der Rahmen)
        float padding = 8;
        var cutoutRect = new SKRect(
            highlightRect.Left - padding,
            highlightRect.Top - padding,
            highlightRect.Right + padding,
            highlightRect.Bottom + padding);

        // Dim-Overlay mit Cutout ("Loch" beim Highlight)
        // 4 Rechtecke um den Highlight-Bereich zeichnen (robuster als SaveLayer+Clear)
        // Oben
        canvas.DrawRect(0, 0, screenWidth, cutoutRect.Top, _dimPaint);
        // Unten
        canvas.DrawRect(0, cutoutRect.Bottom, screenWidth, screenHeight - cutoutRect.Bottom, _dimPaint);
        // Links (zwischen oben und unten)
        canvas.DrawRect(0, cutoutRect.Top, cutoutRect.Left, cutoutRect.Height, _dimPaint);
        // Rechts (zwischen oben und unten)
        canvas.DrawRect(cutoutRect.Right, cutoutRect.Top, screenWidth - cutoutRect.Right, cutoutRect.Height, _dimPaint);

        // Pulsierender Gold-Highlight-Rahmen
        float pulse = MathF.Sin(_pulseTimer * 4f) * 2f;
        _highlightPaint.StrokeWidth = 3f + pulse;
        canvas.DrawRoundRect(
            highlightRect.Left - 4, highlightRect.Top - 4,
            highlightRect.Width + 8, highlightRect.Height + 8,
            10, 10, _highlightPaint);

        // Text-Blase Position: Intelligent platzieren (nicht ueber dem Highlight)
        string text = _localizationService.GetString(step.TextKey) ?? step.TextKey;
        float bubbleWidth = Math.Min(screenWidth * 0.55f, 360);
        float bubbleHeight = 54;
        float bubbleX = screenWidth / 2 - bubbleWidth / 2;

        // Blase oben wenn Highlight unten, sonst unten
        bool highlightIsInUpperHalf = highlightRect.MidY < screenHeight * 0.5f;
        float bubbleY = highlightIsInUpperHalf
            ? Math.Max(cutoutRect.Bottom + 20, screenHeight * 0.55f)
            : Math.Min(cutoutRect.Top - bubbleHeight - 20, screenHeight * 0.15f);

        // Sicherstellen dass Blase im sichtbaren Bereich bleibt
        bubbleY = Math.Clamp(bubbleY, 10, screenHeight - bubbleHeight - 10);

        // Blase zeichnen
        canvas.DrawRoundRect(bubbleX, bubbleY, bubbleWidth, bubbleHeight, 14, 14, _bubblePaint);
        canvas.DrawRoundRect(bubbleX, bubbleY, bubbleWidth, bubbleHeight, 14, 14, _bubbleBorderPaint);

        // Text in Blase
        canvas.DrawText(text, bubbleX + bubbleWidth / 2, bubbleY + bubbleHeight / 2 + 6,
            SKTextAlign.Center, _textFont, _textPaint);

        // Pfeil von Blase zum Highlight
        float arrowFromY = highlightIsInUpperHalf ? bubbleY : bubbleY + bubbleHeight;
        float arrowToY = highlightIsInUpperHalf ? cutoutRect.Bottom + 4 : cutoutRect.Top - 4;
        DrawArrow(canvas, bubbleX + bubbleWidth / 2, arrowFromY,
            highlightRect.MidX, arrowToY);

        // "Ueberspringen" Button (oben rechts, etwas groesser und sichtbarer)
        string skipText = _localizationService.GetString("TutorialSkip") ?? "Skip >";
        float skipWidth = 90;
        float skipHeight = 34;
        float skipX = screenWidth - skipWidth - 12;
        float skipY = 12;
        _skipButtonRect = new SKRect(skipX, skipY, skipX + skipWidth, skipY + skipHeight);

        canvas.DrawRoundRect(skipX, skipY, skipWidth, skipHeight, 8, 8, _skipBgPaint);
        canvas.DrawRoundRect(skipX, skipY, skipWidth, skipHeight, 8, 8, _skipBorderPaint);
        canvas.DrawText(skipText, skipX + skipWidth / 2, skipY + skipHeight / 2 + 5,
            SKTextAlign.Center, _skipFont, _skipPaint);
    }

    /// <summary>
    /// Prueft ob ein Touch auf den Skip-Button faellt
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
        // HUD ist 120px breit rechts, Spielfeld endet davor
        float gameAreaRight = screenWidth - 120f;

        return highlight switch
        {
            // Input-Controls: Links unten (Joystick-Bereich)
            TutorialHighlight.InputControl => new SKRect(
                10, screenHeight - 180, 200, screenHeight - 10),

            // Bomb-Button: Rechts unten
            TutorialHighlight.BombButton => new SKRect(
                screenWidth - 100, screenHeight - 100, screenWidth - 10, screenHeight - 10),

            // Spielfeld-Mitte (skaliert mit Viewport)
            TutorialHighlight.GameField => new SKRect(
                offsetX, offsetY, offsetX + 200 * scale, offsetY + 150 * scale),

            // PowerUp / Exit: Gesamtes Spielfeld hervorheben (ohne HUD-Bereich)
            TutorialHighlight.PowerUp or TutorialHighlight.Exit => new SKRect(
                offsetX, offsetY,
                gameAreaRight, screenHeight - offsetY),

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
        _skipBorderPaint.Dispose();
        _textFont.Dispose();
        _skipFont.Dispose();
        _arrowPath.Dispose();
    }
}
