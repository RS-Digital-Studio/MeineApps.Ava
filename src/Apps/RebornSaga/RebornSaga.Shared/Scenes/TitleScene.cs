namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.Backgrounds;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Animierter Titelbildschirm mit Partikel-Effekten, Glow-Titel und 3 Buttons.
/// Erster Screen nach App-Start.
/// </summary>
public class TitleScene : Scene, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly string[] _buttonLabels = new string[3];
    private float _time;
    private float _titleAlpha;           // Fade-In für Titel
    private float _subtitleAlpha;         // Fade-In für Untertitel (verzögert)
    private float _buttonsAlpha;          // Fade-In für Buttons (verzögert)
    private readonly ParticleSystem _particles = new(150);
    private readonly SKRect[] _buttonRects = new SKRect[3];
    private int _hoveredButton = -1;
    private int _pressedButton = -1;
    private bool _disposed;

    public TitleScene(ILocalizationService localization)
    {
        _localization = localization;
        _buttonLabels[0] = _localization.GetString("NewGame") ?? "New Game";
        _buttonLabels[1] = _localization.GetString("Continue") ?? "Continue";
        _buttonLabels[2] = _localization.GetString("Settings") ?? "Settings";
    }

    // Gepoolte Paints für Titel-Glow
    private readonly SKPaint _titleGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };
    private static readonly SKMaskFilter _titleGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);

    // Gepoolte SKFont + Layer-Paint für Alpha-Fade
    private readonly SKFont _titleFont = new() { LinearMetrics = true };
    private readonly SKFont _subtitleFont = new() { LinearMetrics = true };
    private readonly SKPaint _layerPaint = new();

    public override void OnEnter()
    {
        _time = 0;
        _titleAlpha = 0;
        _subtitleAlpha = 0;
        _buttonsAlpha = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Titel fade-in (0-1s)
        if (_titleAlpha < 1f)
            _titleAlpha = Math.Min(1f, _time / 1f);

        // Untertitel fade-in (ab 1s, über 0.8s)
        if (_time > 1f && _subtitleAlpha < 1f)
            _subtitleAlpha = Math.Min(1f, (_time - 1f) / 0.8f);

        // Buttons fade-in (ab 1.5s, über 0.5s)
        if (_time > 1.5f && _buttonsAlpha < 1f)
            _buttonsAlpha = Math.Min(1f, (_time - 1.5f) / 0.5f);

        // Kontinuierliche Ambient-Partikel
        _particles.EmitContinuous(0, 0, 3f, deltaTime, ParticleSystem.MagicSparkle, 0, 0);
        _particles.Update(deltaTime);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Hintergrund (dunkler Gradient mit Partikel-Ringen)
        BackgroundCompositor.SetScene("title");
        BackgroundCompositor.RenderBack(canvas, bounds, _time);

        // Szenen-Partikel (Star + RingOrbit aus SceneDefinitions)
        BackgroundCompositor.RenderFront(canvas, bounds, _time);

        // Eigene Partikel (zusätzlich)
        RenderParticlesScattered(canvas, bounds);

        // "REBORN SAGA" Titel mit Glow-Effekt
        if (_titleAlpha > 0)
            RenderTitle(canvas, bounds);

        // "Isekai Rising" Untertitel
        if (_subtitleAlpha > 0)
            RenderSubtitle(canvas, bounds);

        // 3 Buttons
        if (_buttonsAlpha > 0)
            RenderButtons(canvas, bounds);
    }

    private void RenderParticlesScattered(SKCanvas canvas, SKRect bounds)
    {
        // Partikel werden relativ zur Bildschirmmitte emittiert,
        // daher Canvas-Translate um sie zu verteilen
        canvas.Save();
        canvas.Translate(bounds.MidX, bounds.MidY);
        _particles.Render(canvas);
        canvas.Restore();
    }

    private void RenderTitle(SKCanvas canvas, SKRect bounds)
    {
        var titleSize = bounds.Width * 0.1f;
        var titleY = bounds.Height * 0.28f;

        // Pulsierender Glow
        var pulseAlpha = (byte)(40 + MathF.Sin(_time * 2f) * 15);
        _titleGlowPaint.Color = UIRenderer.PrimaryGlow.WithAlpha((byte)(pulseAlpha * _titleAlpha));
        _titleGlowPaint.MaskFilter = _titleGlowFilter;
        _titleFont.Size = titleSize;
        canvas.DrawText("REBORN SAGA", bounds.MidX, titleY, SKTextAlign.Center, _titleFont, _titleGlowPaint);
        _titleGlowPaint.MaskFilter = null;

        // Haupttitel
        var alpha = (byte)(255 * _titleAlpha);
        UIRenderer.DrawTextWithShadow(canvas, "REBORN SAGA",
            bounds.MidX, titleY, titleSize,
            UIRenderer.PrimaryGlow.WithAlpha(alpha), 3f);
    }

    private void RenderSubtitle(SKCanvas canvas, SKRect bounds)
    {
        var subtitleSize = bounds.Width * 0.04f;
        var subtitleY = bounds.Height * 0.35f;

        var alpha = (byte)(200 * _subtitleAlpha);
        UIRenderer.DrawText(canvas, "Isekai Rising",
            bounds.MidX, subtitleY, subtitleSize,
            UIRenderer.TextSecondary.WithAlpha(alpha),
            SKTextAlign.Center);
    }

    private void RenderButtons(SKCanvas canvas, SKRect bounds)
    {
        var btnW = bounds.Width * 0.45f;
        var btnH = bounds.Height * 0.065f;
        var startY = bounds.Height * 0.55f;
        var spacing = btnH + bounds.Height * 0.025f;

        var labels = _buttonLabels;

        for (int i = 0; i < 3; i++)
        {
            _buttonRects[i] = new SKRect(
                bounds.MidX - btnW / 2, startY + i * spacing,
                bounds.MidX + btnW / 2, startY + i * spacing + btnH);

            // Buttons mit Transparenz-Fade
            if (_buttonsAlpha < 1f)
            {
                _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * _buttonsAlpha));
                canvas.SaveLayer(_layerPaint);
                UIRenderer.DrawButton(canvas, _buttonRects[i], labels[i],
                    i == _hoveredButton, i == _pressedButton);
                canvas.Restore();
            }
            else
            {
                UIRenderer.DrawButton(canvas, _buttonRects[i], labels[i],
                    i == _hoveredButton, i == _pressedButton);
            }
        }
    }

    public override void HandlePointerDown(SKPoint position)
    {
        for (int i = 0; i < 3; i++)
        {
            if (UIRenderer.HitTest(_buttonRects[i], position))
            {
                _pressedButton = i;
                return;
            }
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredButton = -1;
        for (int i = 0; i < 3; i++)
        {
            if (UIRenderer.HitTest(_buttonRects[i], position))
            {
                _hoveredButton = i;
                return;
            }
        }
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _pressedButton = -1;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Tap && _buttonsAlpha >= 1f)
        {
            for (int i = 0; i < 3; i++)
            {
                if (UIRenderer.HitTest(_buttonRects[i], position))
                {
                    OnButtonTapped(i);
                    return;
                }
            }
        }
    }

    private void OnButtonTapped(int index)
    {
        switch (index)
        {
            case 0: // Neues Spiel → SaveSlotScene im NewGame-Modus
                SceneManager.ChangeScene<SaveSlotScene>(
                    scene => scene.Mode = SaveSlotMode.NewGame,
                    new Engine.Transitions.FadeTransition());
                break;
            case 1: // Fortsetzen → SaveSlotScene im LoadGame-Modus
                SceneManager.ChangeScene<SaveSlotScene>(
                    scene => scene.Mode = SaveSlotMode.LoadGame,
                    new Engine.Transitions.FadeTransition());
                break;
            case 2: // Einstellungen
                SceneManager.PushScene<SettingsScene>();
                break;
        }
    }

    public override void OnExit()
    {
        _particles.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _particles.Dispose();
        _titleGlowPaint.Dispose();
        _titleFont.Dispose();
        _subtitleFont.Dispose();
        _layerPaint.Dispose();
    }
}
