namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Models;
using RebornSaga.Rendering.Effects;
using RebornSaga.Rendering.UI;
using SkiaSharp;
using System;

/// <summary>
/// Level-Up Overlay: Zeigt neues Level, Stats-Zuwachs und ermöglicht
/// Verteilung der 3 freien Punkte. Partikel-Effekte + CountUp-Animation.
/// </summary>
public class LevelUpOverlay : Scene, IDisposable
{
    private float _time;
    private Player? _player;
    private int _newLevel;
    private readonly ParticleSystem _particles = new();

    // Stat-Verteilung
    private int _remainingPoints;
    private readonly int[] _addedStats = new int[5]; // ATK, DEF, INT, SPD, LUK
    private readonly SKRect[] _plusButtonRects = new SKRect[5];
    private SKRect _confirmButtonRect;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _statFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKMaskFilter _glowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);

    private static readonly string[] _statNames = { "ATK", "DEF", "INT", "SPD", "LUK" };

    private readonly ILocalizationService _localization;

    // Gecachte lokalisierte Strings
    private string _levelUpText = "LEVEL UP!";
    private string _confirmText = "Confirm";

    /// <summary>Event wenn alle Punkte verteilt und bestätigt werden.</summary>
    public event Action<int[]>? StatsConfirmed;

    public LevelUpOverlay(ILocalizationService localization)
    {
        _localization = localization;
        _levelUpText = _localization.GetString("LevelUp") ?? "LEVEL UP!";
        _confirmText = _localization.GetString("Confirm") ?? "Confirm";
    }

    /// <summary>Setzt den Spieler und das neue Level.</summary>
    public void SetLevelUp(Player player, int newLevel)
    {
        _player = player;
        _newLevel = newLevel;
        _remainingPoints = 3;
        Array.Clear(_addedStats);
    }

    public override void OnEnter()
    {
        _time = 0;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        _particles.Update(deltaTime);

        // Initiale Partikel (Position wird beim Rendern relativ gezeichnet)
        if (_time < 0.1f)
            _particles.Emit(400, 300, 20, ParticleSystem.LevelUpGlow);
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        if (_player == null) return;

        // Halbtransparenter Hintergrund
        _bgPaint.Color = new SKColor(0x0D, 0x11, 0x17, 230);
        canvas.DrawRect(bounds, _bgPaint);

        // Partikel
        _particles.Render(canvas);

        // Panel
        var panelW = bounds.Width * 0.8f;
        var panelH = bounds.Height * 0.7f;
        var panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.Height * 0.1f,
            bounds.MidX + panelW / 2, bounds.Height * 0.1f + panelH);

        _bgPaint.Color = new SKColor(0x12, 0x16, 0x1F, 240);
        using var roundRect = new SKRoundRect(panelRect, 10f);
        canvas.DrawRoundRect(roundRect, _bgPaint);

        // Glow-Rand (gold)
        var glowColor = new SKColor(0xF3, 0x9C, 0x12);
        var glowAlpha = (byte)(100 + MathF.Sin(_time * 3f) * 50);
        _borderPaint.Color = glowColor.WithAlpha(glowAlpha);
        _borderPaint.MaskFilter = _glowBlur;
        canvas.DrawRoundRect(roundRect, _borderPaint);
        _borderPaint.MaskFilter = null;
        _borderPaint.Color = glowColor.WithAlpha(150);
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // "LEVEL UP!" Titel
        var titleY = panelRect.Top + panelH * 0.08f;
        _titleFont.Size = panelW * 0.08f;
        _textPaint.Color = glowColor;
        canvas.DrawText(_levelUpText, panelRect.MidX, titleY,
            SKTextAlign.Center, _titleFont, _textPaint);

        // Neues Level
        titleY += _titleFont.Size * 2f;
        _titleFont.Size = panelW * 0.12f;
        _textPaint.Color = UIRenderer.PrimaryGlow;
        canvas.DrawText(_newLevel.ToString(), panelRect.MidX, titleY,
            SKTextAlign.Center, _titleFont, _textPaint);

        // "Verteile 3 Punkte"
        titleY += _titleFont.Size * 1.5f;
        _statFont.Size = panelW * 0.04f;
        _textPaint.Color = _remainingPoints > 0 ? UIRenderer.Accent : UIRenderer.Success;
        var pointsText = _remainingPoints > 0
            ? string.Format(_localization.GetString("DistributePoints") ?? "Distribute {0} points:", _remainingPoints)
            : _localization.GetString("AllPointsDistributed") ?? "All points distributed!";
        canvas.DrawText(pointsText, panelRect.MidX, titleY,
            SKTextAlign.Center, _statFont, _textPaint);

        // Stats mit + Buttons
        var statsY = titleY + _statFont.Size * 2f;
        var statSpacing = panelH * 0.08f;
        _statFont.Size = panelW * 0.05f;

        int[] baseStats = { _player.Atk, _player.Def, _player.Int, _player.Spd, _player.Luk };

        for (int i = 0; i < 5; i++)
        {
            var y = statsY + i * statSpacing;

            // Stat-Name
            _textPaint.Color = UIRenderer.TextSecondary;
            canvas.DrawText(_statNames[i], panelRect.Left + panelW * 0.15f, y,
                SKTextAlign.Left, _statFont, _textPaint);

            // Wert
            _textPaint.Color = UIRenderer.TextPrimary;
            canvas.DrawText(baseStats[i].ToString(), panelRect.MidX - panelW * 0.05f, y,
                SKTextAlign.Right, _statFont, _textPaint);

            // Zugewiesene Punkte
            if (_addedStats[i] > 0)
            {
                _textPaint.Color = UIRenderer.Success;
                canvas.DrawText($"+{_addedStats[i]}", panelRect.MidX + panelW * 0.02f, y,
                    SKTextAlign.Left, _statFont, _textPaint);
            }

            // + Button
            var btnSize = statSpacing * 0.7f;
            _plusButtonRects[i] = new SKRect(
                panelRect.Right - panelW * 0.2f, y - btnSize * 0.7f,
                panelRect.Right - panelW * 0.2f + btnSize, y + btnSize * 0.3f);

            if (_remainingPoints > 0)
                UIRenderer.DrawButton(canvas, _plusButtonRects[i], "+", false, false, UIRenderer.Success);
        }

        // Bestätigen-Button (nur wenn alle Punkte verteilt)
        var btnW = panelW * 0.4f;
        var btnH = panelH * 0.06f;
        _confirmButtonRect = new SKRect(
            panelRect.MidX - btnW / 2, panelRect.Bottom - panelH * 0.12f,
            panelRect.MidX + btnW / 2, panelRect.Bottom - panelH * 0.12f + btnH);

        if (_remainingPoints == 0)
            UIRenderer.DrawButton(canvas, _confirmButtonRect, _confirmText, false, false, glowColor);
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action != InputAction.Tap) return;

        // + Buttons
        if (_remainingPoints > 0)
        {
            for (int i = 0; i < 5; i++)
            {
                if (UIRenderer.HitTest(_plusButtonRects[i], position))
                {
                    _addedStats[i]++;
                    _remainingPoints--;
                    return;
                }
            }
        }

        // Bestätigen
        if (_remainingPoints == 0 && UIRenderer.HitTest(_confirmButtonRect, position))
        {
            // Stats auf Player anwenden
            if (_player != null)
            {
                _player.Atk += _addedStats[0];
                _player.Def += _addedStats[1];
                _player.Int += _addedStats[2];
                _player.Spd += _addedStats[3];
                _player.Luk += _addedStats[4];
                _player.FreeStatPoints -= 3;
            }

            StatsConfirmed?.Invoke(_addedStats);
            SceneManager.HideOverlay(this);
        }
    }

    public void Dispose()
    {
        _particles.Dispose();
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _titleFont.Dispose();
        _statFont.Dispose();
        _textPaint.Dispose();
        // _glowBlur ist static readonly — NICHT disposen
    }
}
