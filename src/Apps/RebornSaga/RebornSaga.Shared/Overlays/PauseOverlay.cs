namespace RebornSaga.Overlays;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using RebornSaga.Scenes;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Pause-Menü Overlay: Fortsetzen, Speichern, Status, Inventar, Kodex, Settings, Hauptmenü.
/// Wird über der aktuellen Szene gezeigt (dimmt Hintergrund ab).
/// </summary>
public class PauseOverlay : Scene
{
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localization;
    private float _time;
    private bool _isSaving;
    private int _hoveredIndex = -1;  // Gedrückter/gehovter Menü-Eintrag für visuelles Feedback
    private int _pressedIndex = -1;  // Gedrückter Index (PointerDown → PointerUp)

    /// <summary>Callback für Speichern (wird von der aktiven Szene gesetzt, da Player + Slot kontextabhängig).</summary>
    public Func<System.Threading.Tasks.Task>? OnSaveRequested { get; set; }

    // Menü-Einträge (lokalisiert)
    private readonly string[] _menuItems = new string[7];
    private string _pauseTitle = "Pause";
    private string _savingText = "Saving...";

    private readonly SKRect[] _menuRects = new SKRect[7];

    // Gepoolte Paints
    private static readonly SKPaint _dimPaint = new() { IsAntialias = false };
    private static readonly SKPaint _panelPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _itemPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _itemFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };
    private static readonly SKMaskFilter _glow = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    public PauseOverlay(IAudioService audioService, ILocalizationService localization)
    {
        _audioService = audioService;
        _localization = localization;
        UpdateLocalizedTexts();
    }

    private void UpdateLocalizedTexts()
    {
        _menuItems[0] = _localization.GetString("Resume") ?? "Resume";
        _menuItems[1] = _localization.GetString("Save") ?? "Save";
        _menuItems[2] = _localization.GetString("Status") ?? "Status";
        _menuItems[3] = _localization.GetString("Inventory") ?? "Inventory";
        _menuItems[4] = _localization.GetString("Codex") ?? "Codex";
        _menuItems[5] = _localization.GetString("Settings") ?? "Settings";
        _menuItems[6] = _localization.GetString("MainMenu") ?? "Main Menu";
        _pauseTitle = _localization.GetString("Pause") ?? "Pause";
        _savingText = _localization.GetString("Saving") ?? "Saving...";
    }

    public override void OnEnter()
    {
        _time = 0;
        _isSaving = false;
        _hoveredIndex = -1;
        _pressedIndex = -1;
    }

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
    }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        var alpha = Math.Min(1f, _time / 0.2f);
        var byteAlpha = (byte)(alpha * 255);

        // Dimmen
        _dimPaint.Color = new SKColor(0, 0, 0, (byte)(180 * alpha));
        canvas.DrawRect(bounds, _dimPaint);

        // Panel (zentriert, 60% Breite, angepasste Höhe)
        var panelW = bounds.Width * 0.6f;
        var itemH = bounds.Height * 0.06f;
        var gap = 8f;
        var panelH = itemH * _menuItems.Length + gap * (_menuItems.Length - 1) + 80;
        var panelRect = new SKRect(
            bounds.MidX - panelW / 2, bounds.MidY - panelH / 2,
            bounds.MidX + panelW / 2, bounds.MidY + panelH / 2);

        // Panel-Hintergrund
        _panelPaint.Color = UIRenderer.PanelBg.WithAlpha((byte)(240 * alpha));
        using var roundRect = new SKRoundRect(panelRect, 8f);
        canvas.DrawRoundRect(roundRect, _panelPaint);

        // Glow-Rand
        _borderPaint.Color = UIRenderer.Primary.WithAlpha((byte)(80 * alpha));
        _borderPaint.MaskFilter = _glow;
        canvas.DrawRoundRect(roundRect, _borderPaint);
        _borderPaint.MaskFilter = null;
        _borderPaint.Color = UIRenderer.Border.WithAlpha(byteAlpha);
        canvas.DrawRoundRect(roundRect, _borderPaint);

        // Titel
        _titleFont.Size = bounds.Width * 0.045f;
        _textPaint.Color = UIRenderer.Primary.WithAlpha(byteAlpha);
        canvas.DrawText(_pauseTitle, panelRect.MidX, panelRect.Top + 35,
            SKTextAlign.Center, _titleFont, _textPaint);

        // Menü-Einträge
        var startY = panelRect.Top + 55;
        var pad = panelW * 0.08f;

        for (int i = 0; i < _menuItems.Length; i++)
        {
            var y = startY + i * (itemH + gap);
            _menuRects[i] = new SKRect(panelRect.Left + pad, y, panelRect.Right - pad, y + itemH);

            // Hintergrund (heller bei Hover/Press)
            var isLast = i == _menuItems.Length - 1; // Hauptmenü = rot
            var isHovered = i == _hoveredIndex;
            var isPressed = i == _pressedIndex;
            byte bgAlpha;
            if (isLast)
                bgAlpha = (byte)((isPressed ? 70 : isHovered ? 50 : 30) * alpha);
            else
                bgAlpha = (byte)((isPressed ? 255 : isHovered ? 240 : 200) * alpha);

            _itemPaint.Color = isLast
                ? UIRenderer.Danger.WithAlpha(bgAlpha)
                : UIRenderer.CardBg.WithAlpha(bgAlpha);
            using var itemRr = new SKRoundRect(_menuRects[i], 4f);
            canvas.DrawRoundRect(itemRr, _itemPaint);

            // Gedrückter Eintrag: dezenter Glow-Rand
            if (isPressed)
            {
                _borderPaint.Color = (isLast ? UIRenderer.Danger : UIRenderer.Primary).WithAlpha((byte)(100 * alpha));
                canvas.DrawRoundRect(itemRr, _borderPaint);
            }

            // Text
            _itemFont.Size = itemH * 0.45f;
            var textColor = isLast ? UIRenderer.Danger : UIRenderer.TextPrimary;
            _textPaint.Color = textColor.WithAlpha(byteAlpha);

            var text = _menuItems[i];
            if (i == 1 && _isSaving) text = _savingText;

            canvas.DrawText(text, _menuRects[i].MidX, _menuRects[i].MidY + itemH * 0.15f,
                SKTextAlign.Center, _itemFont, _textPaint);
        }
    }

    public override void HandlePointerDown(SKPoint position)
    {
        _pressedIndex = -1;
        for (int i = 0; i < _menuRects.Length; i++)
        {
            if (_menuRects[i].Contains(position))
            {
                _pressedIndex = i;
                return;
            }
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        _hoveredIndex = -1;
        for (int i = 0; i < _menuRects.Length; i++)
        {
            if (_menuRects[i].Contains(position))
            {
                _hoveredIndex = i;
                return;
            }
        }

        // Wenn Finger aus dem gedrückten Element herausbewegt, Press aufheben
        if (_pressedIndex >= 0 && !_menuRects[_pressedIndex].Contains(position))
            _pressedIndex = -1;
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _pressedIndex = -1;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action != InputAction.Tap || _time < 0.3f) return;

        for (int i = 0; i < _menuRects.Length; i++)
        {
            if (!_menuRects[i].Contains(position)) continue;

            _audioService.PlaySfx(GameSfx.ButtonTap);

            switch (i)
            {
                case 0: // Fortsetzen
                    SceneManager.HideOverlay(this);
                    break;

                case 1: // Speichern (_isSaving Guard verhindert doppelte Aufrufe)
                    if (!_isSaving)
                    {
                        _isSaving = true;
                        // AppChecker:ignore
                        _ = SaveAsync();
                    }
                    break;

                case 2: // Status
                    SceneManager.HideOverlay(this);
                    SceneManager.PushScene<StatusScene>();
                    break;

                case 3: // Inventar
                    SceneManager.HideOverlay(this);
                    SceneManager.PushScene<InventoryScene>();
                    break;

                case 4: // Kodex
                    SceneManager.HideOverlay(this);
                    SceneManager.PushScene<CodexScene>();
                    break;

                case 5: // Einstellungen
                    SceneManager.HideOverlay(this);
                    SceneManager.PushScene<SettingsScene>();
                    break;

                case 6: // Hauptmenü
                    SceneManager.HideOverlay(this);
                    SceneManager.ChangeScene<TitleScene>();
                    break;
            }
            break;
        }
    }

    private async System.Threading.Tasks.Task SaveAsync()
    {
        try
        {
            if (OnSaveRequested != null)
                await OnSaveRequested();
            _isSaving = false;
        }
        catch
        {
            _isSaving = false;
        }
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _dimPaint.Dispose();
        _panelPaint.Dispose();
        _borderPaint.Dispose();
        _itemPaint.Dispose();
        _titleFont.Dispose();
        _itemFont.Dispose();
        _textPaint.Dispose();
        // _glow ist static readonly — NICHT disposen
    }
}
