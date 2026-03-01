using System;
using SkiaSharp;

namespace HandwerkerImperium.Graphics;

/// <summary>
/// Übergangseffekt-Typen für View-Wechsel.
/// </summary>
public enum TransitionType
{
    /// <summary>Neue View schiebt von rechts rein (300ms).</summary>
    WipeRight,

    /// <summary>Alte View zoomt raus, neue zoomt rein (250ms).</summary>
    ZoomIn,

    /// <summary>Sanfter Crossfade (400ms).</summary>
    Dissolve,

    /// <summary>Neue View kommt von unten (300ms).</summary>
    SlideUp
}

/// <summary>
/// Animierter Screen-Übergangs-Renderer für View-Wechsel.
/// Rendert Overlay-Effekte (Verdunklung, Trennkanten, Vignetten) während einer Transition.
/// Gecachte Paint-Objekte für GC-freie Performance im Render-Loop.
/// </summary>
public class ScreenTransitionRenderer : IDisposable
{
    private bool _disposed;
    // Dauer pro TransitionType in Sekunden
    private const float WipeRightDuration = 0.300f;
    private const float ZoomInDuration = 0.250f;
    private const float DissolveDuration = 0.400f;
    private const float SlideUpDuration = 0.300f;

    // Goldene Trennkante
    private static readonly SKColor GoldColor = new(0xFF, 0xD7, 0x00);

    // Gecachte Paint-Objekte (keine Allokationen im Render-Loop)
    private readonly SKPaint _overlayPaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _linePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _vignettePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// True während eine Transition aktiv ist.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Aktueller Übergangstyp.
    /// </summary>
    public TransitionType CurrentType { get; private set; }

    /// <summary>
    /// Fortschritt der aktuellen Transition (0.0 bis 1.0).
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// Gesamtdauer der aktuellen Transition in Sekunden.
    /// </summary>
    public float Duration { get; private set; }

    /// <summary>
    /// Vergangene Zeit seit Transition-Start in Sekunden.
    /// </summary>
    public float Elapsed { get; private set; }

    /// <summary>
    /// Startet eine neue Screen-Transition.
    /// Setzt Elapsed und Progress zurück und aktiviert den Renderer.
    /// </summary>
    /// <param name="type">Der gewünschte Übergangstyp.</param>
    public void StartTransition(TransitionType type)
    {
        CurrentType = type;
        Elapsed = 0f;
        Progress = 0f;
        IsActive = true;

        Duration = type switch
        {
            TransitionType.WipeRight => WipeRightDuration,
            TransitionType.ZoomIn => ZoomInDuration,
            TransitionType.Dissolve => DissolveDuration,
            TransitionType.SlideUp => SlideUpDuration,
            _ => WipeRightDuration
        };
    }

    /// <summary>
    /// Aktualisiert den Transition-Timer.
    /// Muss pro Frame aufgerufen werden.
    /// </summary>
    /// <param name="deltaTime">Vergangene Zeit seit dem letzten Frame in Sekunden.</param>
    public void Update(float deltaTime)
    {
        if (!IsActive) return;

        Elapsed += deltaTime;

        if (Elapsed >= Duration)
        {
            // Transition abgeschlossen
            Elapsed = Duration;
            Progress = 1f;
            IsActive = false;
        }
        else
        {
            Progress = Elapsed / Duration;
        }
    }

    /// <summary>
    /// Zeichnet den Übergangseffekt auf den Canvas.
    /// Wird als Overlay über die aktuelle View gerendert.
    /// </summary>
    /// <param name="canvas">Der SkiaSharp-Canvas.</param>
    /// <param name="bounds">Der sichtbare Bereich.</param>
    public void Render(SKCanvas canvas, SKRect bounds)
    {
        // Auch direkt nach Abschluss (Progress=1) noch einen Frame rendern,
        // damit der letzte Zustand sichtbar wird
        if (!IsActive && Progress < 0.01f) return;

        switch (CurrentType)
        {
            case TransitionType.WipeRight:
                RenderWipeRight(canvas, bounds);
                break;
            case TransitionType.ZoomIn:
                RenderZoomIn(canvas, bounds);
                break;
            case TransitionType.Dissolve:
                RenderDissolve(canvas, bounds);
                break;
            case TransitionType.SlideUp:
                RenderSlideUp(canvas, bounds);
                break;
        }
    }

    /// <summary>
    /// WipeRight: Vertikale Trennlinie wandert von links nach rechts.
    /// Links wird verdunkelt (alte View), rechts aufgehellt (neue View).
    /// Goldener 2px-Streifen an der Trennkante.
    /// </summary>
    private void RenderWipeRight(SKCanvas canvas, SKRect bounds)
    {
        float easedProgress = EasingFunctions.EaseInOutQuint(Progress);
        float splitX = bounds.Left + bounds.Width * easedProgress;

        // Linke Seite: Alte View wird verdunkelt
        byte leftAlpha = (byte)(80 * easedProgress);
        if (leftAlpha > 0)
        {
            _overlayPaint.Color = new SKColor(0, 0, 0, leftAlpha);
            canvas.DrawRect(bounds.Left, bounds.Top, splitX - bounds.Left, bounds.Height, _overlayPaint);
        }

        // Rechte Seite: Neue View wird aufgehellt (Verdunklung nimmt ab)
        byte rightAlpha = (byte)(80 * (1f - easedProgress));
        if (rightAlpha > 0)
        {
            _overlayPaint.Color = new SKColor(0, 0, 0, rightAlpha);
            canvas.DrawRect(splitX, bounds.Top, bounds.Right - splitX, bounds.Height, _overlayPaint);
        }

        // Goldener Trennstreifen (2px breit)
        // Alpha ist in der Mitte am hellsten, am Anfang/Ende transparent
        float edgeAlpha = EasingFunctions.PingPong(Progress);
        byte goldAlpha = (byte)(220 * edgeAlpha);
        if (goldAlpha > 5)
        {
            _linePaint.Color = GoldColor.WithAlpha(goldAlpha);
            _linePaint.MaskFilter = null;
            canvas.DrawRect(splitX - 1f, bounds.Top, 2f, bounds.Height, _linePaint);

            // Subtiler Glow um die Kante (nur wenn sichtbar genug)
            if (goldAlpha > 40)
            {
                byte glowAlpha = (byte)(goldAlpha / 3);
                _linePaint.Color = GoldColor.WithAlpha(glowAlpha);
                _linePaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
                canvas.DrawRect(splitX - 3f, bounds.Top, 6f, bounds.Height, _linePaint);
                _linePaint.MaskFilter = null;
            }
        }
    }

    /// <summary>
    /// ZoomIn: Dunkles Overlay mit PingPong-Alpha (stärker in der Mitte).
    /// Vignette wird in der ersten Hälfte stärker (alte View verschwindet),
    /// in der zweiten Hälfte schwächer (neue View erscheint).
    /// </summary>
    private void RenderZoomIn(SKCanvas canvas, SKRect bounds)
    {
        float easedProgress = EasingFunctions.EaseOutCubic(Progress);
        float pingPong = EasingFunctions.PingPong(Progress);

        // Dunkles Overlay über den gesamten Bildschirm
        byte overlayAlpha = (byte)(120 * pingPong);
        if (overlayAlpha > 0)
        {
            _overlayPaint.Color = new SKColor(0, 0, 0, overlayAlpha);
            canvas.DrawRect(bounds, _overlayPaint);
        }

        // Vignette-Effekt (radialer Gradient von transparent zu dunkel)
        float vignetteStrength;
        if (Progress < 0.5f)
        {
            // Erste Hälfte: Vignette wird stärker (alte View zoomt weg)
            vignetteStrength = easedProgress * 0.8f;
        }
        else
        {
            // Zweite Hälfte: Vignette wird schwächer (neue View erscheint)
            vignetteStrength = (1f - easedProgress) * 0.8f;
        }

        if (vignetteStrength > 0.01f)
        {
            float centerX = bounds.MidX;
            float centerY = bounds.MidY;
            float radius = MathF.Max(bounds.Width, bounds.Height) * 0.7f;

            byte vigAlpha = (byte)(180 * vignetteStrength);
            using var shader = SKShader.CreateRadialGradient(
                new SKPoint(centerX, centerY),
                radius,
                new[] { SKColors.Transparent, new SKColor(0, 0, 0, vigAlpha) },
                new[] { 0.3f, 1.0f },
                SKShaderTileMode.Clamp);

            _vignettePaint.Shader = shader;
            canvas.DrawRect(bounds, _vignettePaint);
            _vignettePaint.Shader = null;
        }
    }

    /// <summary>
    /// Dissolve: Einfacher Crossfade durch Verdunklung mit PingPong-Alpha.
    /// Keine geometrischen Effekte, nur Licht-Modulation.
    /// </summary>
    private void RenderDissolve(SKCanvas canvas, SKRect bounds)
    {
        float easedProgress = EasingFunctions.EaseInOutQuint(Progress);
        float pingPong = EasingFunctions.PingPong(easedProgress);

        // Dunkler Overlay über den gesamten Bildschirm
        byte overlayAlpha = (byte)(60 * pingPong);
        if (overlayAlpha > 0)
        {
            _overlayPaint.Color = new SKColor(0, 0, 0, overlayAlpha);
            canvas.DrawRect(bounds, _overlayPaint);
        }
    }

    /// <summary>
    /// SlideUp: Horizontale Trennlinie wandert von unten nach oben.
    /// Unterhalb wird verdunkelt, goldener Streifen an der Kante.
    /// </summary>
    private void RenderSlideUp(SKCanvas canvas, SKRect bounds)
    {
        float easedProgress = EasingFunctions.EaseOutCubic(Progress);
        float splitY = bounds.Top + bounds.Height * (1f - easedProgress);

        // Unterhalb der Trennlinie: Neue View wird aufgehellt (Verdunklung nimmt ab)
        byte bottomAlpha = (byte)(80 * (1f - easedProgress));
        if (bottomAlpha > 0)
        {
            _overlayPaint.Color = new SKColor(0, 0, 0, bottomAlpha);
            canvas.DrawRect(bounds.Left, splitY, bounds.Width, bounds.Bottom - splitY, _overlayPaint);
        }

        // Goldener Trennstreifen (2px hoch, horizontal)
        float edgeAlpha = EasingFunctions.PingPong(Progress);
        byte goldAlpha = (byte)(220 * edgeAlpha);
        if (goldAlpha > 5)
        {
            _linePaint.Color = GoldColor.WithAlpha(goldAlpha);
            _linePaint.MaskFilter = null;
            canvas.DrawRect(bounds.Left, splitY - 1f, bounds.Width, 2f, _linePaint);

            // Subtiler Glow um die Kante
            if (goldAlpha > 40)
            {
                byte glowAlpha = (byte)(goldAlpha / 3);
                _linePaint.Color = GoldColor.WithAlpha(glowAlpha);
                _linePaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
                canvas.DrawRect(bounds.Left, splitY - 3f, bounds.Width, 6f, _linePaint);
                _linePaint.MaskFilter = null;
            }
        }
    }

    /// <summary>
    /// Gibt native SkiaSharp-Ressourcen frei.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _overlayPaint?.Dispose();
        _linePaint?.Dispose();
        _vignettePaint?.Dispose();
    }
}
