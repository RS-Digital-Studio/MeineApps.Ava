namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Game-Over-Overlay: "Gefallen..." mit Optionen für Rewarded-Ad-Revive oder Laden.
/// Roter Hintergrund, langsame Fade-In-Animation, fallende rote Partikel.
/// </summary>
public class GameOverOverlay : Scene, IDisposable
{
    private float _time;
    private float _fadeProgress;
    private readonly ParticleSystem _particles = new(100);

    // UI-Rects
    private SKRect _reviveButtonRect;
    private SKRect _loadButtonRect;
    private int _hoveredButton = -1; // 0=Revive, 1=Load

    private readonly ILocalizationService _localization;

    // Gecachte lokalisierte Strings
    private string _defeatText = "Fallen...";
    private string _journeyEndsText = "Your journey ends here... or does it?";
    private string _reviveText = "Revive (Ad)";
    private string _loadSaveText = "Load save";

    // Events
    public event Action? ReviveRequested;
    public event Action? LoadSaveRequested;

    public GameOverOverlay(ILocalizationService localization)
    {
        _localization = localization;
        _defeatText = _localization.GetString("Defeat") ?? "Fallen...";
        _journeyEndsText = _localization.GetString("YourJourneyEnds") ?? "Your journey ends here... or does it?";
        _reviveText = _localization.GetString("ReviveAd") ?? "Revive (Ad)";
        _loadSaveText = _localization.GetString("LoadSaveGame") ?? "Load save";
    }

    // Gecachte Paints
    private static readonly SKPaint _overlayPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _labelFont = new() { LinearMetrics = true };

    // Rote Partikel-Config (fallend)
    private static readonly ParticleConfig _deathParticles = new()
    {
        MinSpeed = 10f, MaxSpeed = 30f, MinLife = 2f, MaxLife = 4f,
        MinSize = 1f, MaxSize = 3f, SpreadAngle = 60f, BaseAngle = 90f,
        Gravity = 30f, Color = new SKColor(0xE7, 0x4C, 0x3C, 150),
        FadeOut = true, Shape = 0
    };

    public override void OnEnter()
    {
        _time = 0;
        _fadeProgress = 0;
        _particles.Clear();
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        _fadeProgress = Math.Min(1f, _time / 2f);
        _particles.Update(deltaTime);

        // Kontinuierlich rote Partikel von oben fallen lassen
        if (_fadeProgress > 0.5f)
            _particles.EmitContinuous(400f, 0f, 3f, deltaTime, _deathParticles, 600f, 0f);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        // Rotes Overlay (langsam einblendend)
        var overlayAlpha = (byte)(_fadeProgress * 200);
        _overlayPaint.Color = new SKColor(0x15, 0x00, 0x00, overlayAlpha);
        canvas.DrawRect(bounds, _overlayPaint);

        // Partikel (rote Tropfen die fallen)
        _particles.Render(canvas);

        if (_fadeProgress < 0.3f) return;

        var textAlpha = (byte)(Math.Min(1f, (_fadeProgress - 0.3f) / 0.3f) * 255);

        // "Gefallen..." Titel
        var titleY = bounds.Height * 0.3f;
        _titleFont.Size = bounds.Width * 0.08f;
        _textPaint.Color = UIRenderer.Danger.WithAlpha(textAlpha);

        // Schatten
        _textPaint.Color = SKColors.Black.WithAlpha((byte)(textAlpha * 0.5f));
        canvas.DrawText(_defeatText, bounds.MidX + 3, titleY + 3, SKTextAlign.Center, _titleFont, _textPaint);
        _textPaint.Color = UIRenderer.Danger.WithAlpha(textAlpha);
        canvas.DrawText(_defeatText, bounds.MidX, titleY, SKTextAlign.Center, _titleFont, _textPaint);

        // Untertitel
        if (_fadeProgress > 0.6f)
        {
            var subAlpha = (byte)(Math.Min(1f, (_fadeProgress - 0.6f) / 0.2f) * 200);
            _labelFont.Size = bounds.Width * 0.035f;
            _textPaint.Color = UIRenderer.TextSecondary.WithAlpha(subAlpha);
            canvas.DrawText(_journeyEndsText,
                bounds.MidX, titleY + bounds.Height * 0.08f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Buttons (erst nach vollständigem Fade-In)
        if (_fadeProgress >= 1f)
        {
            var btnW = bounds.Width * 0.5f;
            var btnH = bounds.Height * 0.06f;
            var btnY = bounds.Height * 0.55f;

            // Revive-Button (Rewarded Ad)
            _reviveButtonRect = new SKRect(
                bounds.MidX - btnW / 2, btnY,
                bounds.MidX + btnW / 2, btnY + btnH);
            UIRenderer.DrawButton(canvas, _reviveButtonRect, _reviveText,
                _hoveredButton == 0, false, UIRenderer.Success);

            // Laden-Button
            var loadY = btnY + btnH + bounds.Height * 0.03f;
            _loadButtonRect = new SKRect(
                bounds.MidX - btnW / 2, loadY,
                bounds.MidX + btnW / 2, loadY + btnH);
            UIRenderer.DrawButton(canvas, _loadButtonRect, _loadSaveText,
                _hoveredButton == 1, false, UIRenderer.Primary);
        }
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action != InputAction.Tap || _fadeProgress < 1f) return;

        if (UIRenderer.HitTest(_reviveButtonRect, position))
        {
            ReviveRequested?.Invoke();
            return;
        }

        if (UIRenderer.HitTest(_loadButtonRect, position))
        {
            LoadSaveRequested?.Invoke();
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredButton = -1;
        if (_fadeProgress < 1f) return;

        if (UIRenderer.HitTest(_reviveButtonRect, position))
            _hoveredButton = 0;
        else if (UIRenderer.HitTest(_loadButtonRect, position))
            _hoveredButton = 1;
    }

    public void Dispose()
    {
        _particles.Dispose();
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _overlayPaint.Dispose();
        _textPaint.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
    }
}
