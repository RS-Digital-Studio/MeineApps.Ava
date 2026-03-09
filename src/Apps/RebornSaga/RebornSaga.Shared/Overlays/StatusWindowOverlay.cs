namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Status-Fenster Overlay im Solo-Leveling-Stil.
/// Zeigt Spieler-Stats mit Einblend-Animation und Glow-Effekten.
/// </summary>
public class StatusWindowOverlay : Scene
{
    private readonly ILocalizationService _localization;
    private float _time;
    private float _animProgress; // 0-1 Einblend-Animation
    private Player? _player;

    // Schließen-Button
    private SKRect _closeButtonRect;

    // Gecachte lokalisierte Strings
    private string _closeText = "Close";

    public StatusWindowOverlay(ILocalizationService localization)
    {
        _localization = localization;
        _closeText = _localization.GetString("Close") ?? "Close";
    }

    /// <summary>Setzt die Spieler-Daten für die Anzeige.</summary>
    public void SetPlayer(Player player) => _player = player;

    public override void OnEnter()
    {
        _time = 0;
        _animProgress = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;

        // Einblend-Animation über 1 Sekunde
        if (_animProgress < 1f)
            _animProgress = Math.Min(1f, _animProgress + deltaTime);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_player == null) return;

        StatusWindowRenderer.Render(canvas, bounds, _player, _time, _animProgress);

        // Schließen-Button (unten)
        var btnW = bounds.Width * 0.2f;
        var btnH = bounds.Height * 0.045f;
        _closeButtonRect = new SKRect(
            bounds.MidX - btnW / 2, bounds.Height * 0.93f,
            bounds.MidX + btnW / 2, bounds.Height * 0.93f + btnH);
        UIRenderer.DrawButton(canvas, _closeButtonRect, _closeText);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        switch (action)
        {
            case InputAction.Back:
                SceneManager.HideOverlay(this);
                break;
            case InputAction.Tap:
                if (UIRenderer.HitTest(_closeButtonRect, position))
                    SceneManager.HideOverlay(this);
                break;
        }
    }
}
