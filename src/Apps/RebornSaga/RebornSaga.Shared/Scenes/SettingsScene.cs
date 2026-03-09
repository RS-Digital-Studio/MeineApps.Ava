namespace RebornSaga.Scenes;

using MeineApps.Core.Ava.Localization;
using RebornSaga.Engine;
using RebornSaga.Rendering.UI;
using RebornSaga.Services;
using SkiaSharp;
using System;

/// <summary>
/// Einstellungen: Sprache, Text-Geschwindigkeit, Audio-Lautstärke, Vibration.
/// Wird vom PauseOverlay oder Titelbildschirm aus aufgerufen.
/// </summary>
public class SettingsScene : Scene
{
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localization;
    private readonly MeineApps.Core.Ava.Services.IPreferencesService _preferences;

    // Layout
    private SKRect _lastBounds;
    private SKRect _backButtonRect;

    // Slider-Bereiche
    private SKRect _sfxSliderRect;
    private SKRect _bgmSliderRect;

    // Toggle-Bereiche
    private SKRect _sfxToggleRect;
    private SKRect _bgmToggleRect;
    private SKRect _vibrationToggleRect;

    // Text-Speed (0=langsam, 1=normal, 2=schnell, 3=sofort)
    private int _textSpeed = 1; // Default: Normal (Index 1)
    private readonly SKRect[] _speedButtonRects = new SKRect[4];
    private readonly string[] _speedLabels = new string[4];

    // Gecachte lokalisierte Strings
    private string _settingsTitle = "Settings";
    private string _backArrowText = "< Back";
    private string _audioLabel = "Audio";
    private string _soundEffectsLabel = "Sound Effects";
    private string _sfxVolumeLabel = "SFX Volume";
    private string _musicLabel = "Music";
    private string _musicVolumeLabel = "Music Volume";
    private string _vibrationLabel = "Vibration";
    private string _textLabel = "Text";
    private string _speedLabel = "Speed";

    // Tracking ob Slider gezogen wird
    private bool _isDraggingSfx;
    private bool _isDraggingBgm;

    // Gepoolte Paints
    private static readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _panelPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private static readonly SKPaint _sliderTrackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _sliderFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _sliderThumbPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _titleFont = new() { LinearMetrics = true };
    private static readonly SKFont _labelFont = new() { LinearMetrics = true };
    private static readonly SKFont _valueFont = new() { LinearMetrics = true };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    public SettingsScene(IAudioService audioService, MeineApps.Core.Ava.Services.IPreferencesService preferences, ILocalizationService localization)
    {
        _audioService = audioService;
        _preferences = preferences;
        _localization = localization;
        UpdateLocalizedTexts();
    }

    private void UpdateLocalizedTexts()
    {
        _settingsTitle = _localization.GetString("Settings") ?? "Settings";
        _backArrowText = _localization.GetString("BackArrow") ?? "< Back";
        _audioLabel = _localization.GetString("Audio") ?? "Audio";
        _soundEffectsLabel = _localization.GetString("SoundEffects") ?? "Sound Effects";
        _sfxVolumeLabel = _localization.GetString("SfxVolume") ?? "SFX Volume";
        _musicLabel = _localization.GetString("Music") ?? "Music";
        _musicVolumeLabel = _localization.GetString("MusicVolume") ?? "Music Volume";
        _vibrationLabel = _localization.GetString("Vibration") ?? "Vibration";
        _textLabel = _localization.GetString("Text") ?? "Text";
        _speedLabel = _localization.GetString("Speed") ?? "Speed";
        _speedLabels[0] = _localization.GetString("SpeedSlow") ?? "Slow";
        _speedLabels[1] = _localization.GetString("SpeedNormal") ?? "Normal";
        _speedLabels[2] = _localization.GetString("SpeedFast") ?? "Fast";
        _speedLabels[3] = _localization.GetString("SpeedInstant") ?? "Instant";
    }

    public override void OnEnter()
    {
        _textSpeed = _preferences.Get("settings_text_speed", 1);
        // Gespeicherte Audio-Werte laden
        _audioService.SfxVolume = _preferences.Get("settings_sfx_volume", 1.0f);
        _audioService.BgmVolume = _preferences.Get("settings_bgm_volume", 0.7f);
        _audioService.SfxEnabled = _preferences.Get("settings_sfx_enabled", true);
        _audioService.BgmEnabled = _preferences.Get("settings_bgm_enabled", true);
    }

    /// <summary>Speichert alle Audio- und Text-Einstellungen in Preferences.</summary>
    private void SaveSettings()
    {
        _preferences.Set("settings_sfx_volume", _audioService.SfxVolume);
        _preferences.Set("settings_bgm_volume", _audioService.BgmVolume);
        _preferences.Set("settings_sfx_enabled", _audioService.SfxEnabled);
        _preferences.Set("settings_bgm_enabled", _audioService.BgmEnabled);
        _preferences.Set("settings_vibration_enabled", _audioService.VibrationEnabled);
        _preferences.Set("settings_text_speed", _textSpeed);
    }

    public override void Update(float deltaTime) { }

    public override void Render(SKCanvas canvas, SKRect bounds)
    {
        _lastBounds = bounds;
        var pad = bounds.Width * 0.05f;
        var rowH = bounds.Height * 0.07f;
        var y = bounds.Top + pad;

        // Hintergrund
        _bgPaint.Color = UIRenderer.DarkBg;
        canvas.DrawRect(bounds, _bgPaint);

        // Titel
        _titleFont.Size = bounds.Width * 0.06f;
        _textPaint.Color = UIRenderer.TextPrimary;
        canvas.DrawText(_settingsTitle, bounds.MidX, y + rowH * 0.7f,
            SKTextAlign.Center, _titleFont, _textPaint);
        y += rowH * 1.2f;

        // Zurück-Button
        _backButtonRect = new SKRect(pad, bounds.Top + pad * 0.5f, pad + bounds.Width * 0.12f, bounds.Top + pad * 0.5f + rowH * 0.8f);
        _panelPaint.Color = UIRenderer.CardBg;
        using (var rr = new SKRoundRect(_backButtonRect, 4f))
            canvas.DrawRoundRect(rr, _panelPaint);
        _labelFont.Size = rowH * 0.4f;
        _textPaint.Color = UIRenderer.TextSecondary;
        canvas.DrawText(_backArrowText, _backButtonRect.MidX, _backButtonRect.MidY + rowH * 0.12f,
            SKTextAlign.Center, _labelFont, _textPaint);

        // --- Audio-Bereich ---
        y = RenderSectionHeader(canvas, bounds, y, pad, rowH, _audioLabel);

        // SFX Toggle + Slider
        y = RenderToggleRow(canvas, bounds, y, pad, rowH, _soundEffectsLabel,
            _audioService.SfxEnabled, ref _sfxToggleRect);
        y = RenderSliderRow(canvas, bounds, y, pad, rowH, _sfxVolumeLabel,
            _audioService.SfxVolume, ref _sfxSliderRect);

        // BGM Toggle + Slider
        y = RenderToggleRow(canvas, bounds, y, pad, rowH, _musicLabel,
            _audioService.BgmEnabled, ref _bgmToggleRect);
        y = RenderSliderRow(canvas, bounds, y, pad, rowH, _musicVolumeLabel,
            _audioService.BgmVolume, ref _bgmSliderRect);

        // Vibration Toggle
        y = RenderToggleRow(canvas, bounds, y, pad, rowH, _vibrationLabel,
            _audioService.VibrationEnabled, ref _vibrationToggleRect);

        y += rowH * 0.5f;

        // --- Text-Geschwindigkeit ---
        y = RenderSectionHeader(canvas, bounds, y, pad, rowH, _textLabel);

        _labelFont.Size = rowH * 0.35f;
        _textPaint.Color = UIRenderer.TextSecondary;
        canvas.DrawText(_speedLabel, pad + 10, y + rowH * 0.55f,
            SKTextAlign.Left, _labelFont, _textPaint);
        y += rowH * 0.7f;

        var btnW = (bounds.Width - pad * 2 - 30) / 4f;
        for (int i = 0; i < 4; i++)
        {
            var bx = pad + 5 + i * (btnW + 10);
            _speedButtonRects[i] = new SKRect(bx, y, bx + btnW, y + rowH * 0.8f);

            var isSelected = _textSpeed == i;
            _panelPaint.Color = isSelected ? UIRenderer.Primary : UIRenderer.CardBg;
            using var rr = new SKRoundRect(_speedButtonRects[i], 4f);
            canvas.DrawRoundRect(rr, _panelPaint);

            _valueFont.Size = rowH * 0.3f;
            _textPaint.Color = isSelected ? UIRenderer.DarkBg : UIRenderer.TextSecondary;
            canvas.DrawText(_speedLabels[i], _speedButtonRects[i].MidX, _speedButtonRects[i].MidY + rowH * 0.1f,
                SKTextAlign.Center, _valueFont, _textPaint);
        }
    }

    private float RenderSectionHeader(SKCanvas canvas, SKRect bounds, float y, float pad, float rowH, string title)
    {
        _borderPaint.Color = UIRenderer.Border;
        canvas.DrawLine(pad, y, bounds.Right - pad, y, _borderPaint);
        y += rowH * 0.3f;

        _labelFont.Size = rowH * 0.38f;
        _textPaint.Color = UIRenderer.Primary;
        canvas.DrawText(title, pad + 10, y + rowH * 0.5f, SKTextAlign.Left, _labelFont, _textPaint);
        return y + rowH * 0.8f;
    }

    private float RenderToggleRow(SKCanvas canvas, SKRect bounds, float y, float pad, float rowH,
        string label, bool isOn, ref SKRect toggleRect)
    {
        _labelFont.Size = rowH * 0.35f;
        _textPaint.Color = UIRenderer.TextSecondary;
        canvas.DrawText(label, pad + 10, y + rowH * 0.55f, SKTextAlign.Left, _labelFont, _textPaint);

        // Toggle-Button rechts
        var toggleW = bounds.Width * 0.12f;
        var toggleH = rowH * 0.5f;
        toggleRect = new SKRect(bounds.Right - pad - toggleW, y + (rowH - toggleH) / 2,
            bounds.Right - pad, y + (rowH + toggleH) / 2);

        using var rr = new SKRoundRect(toggleRect, toggleH / 2);
        _panelPaint.Color = isOn ? UIRenderer.Success : UIRenderer.Border;
        canvas.DrawRoundRect(rr, _panelPaint);

        // Thumb
        var thumbR = toggleH * 0.4f;
        var thumbX = isOn ? toggleRect.Right - thumbR - 3 : toggleRect.Left + thumbR + 3;
        _sliderThumbPaint.Color = SKColors.White;
        canvas.DrawCircle(thumbX, toggleRect.MidY, thumbR, _sliderThumbPaint);

        return y + rowH;
    }

    private float RenderSliderRow(SKCanvas canvas, SKRect bounds, float y, float pad, float rowH,
        string label, float value, ref SKRect sliderRect)
    {
        _labelFont.Size = rowH * 0.3f;
        _textPaint.Color = UIRenderer.TextMuted;
        canvas.DrawText(label, pad + 20, y + rowH * 0.45f, SKTextAlign.Left, _labelFont, _textPaint);

        // Prozent-Wert
        _valueFont.Size = rowH * 0.3f;
        _textPaint.Color = UIRenderer.TextSecondary;
        canvas.DrawText($"{(int)(value * 100)}%", bounds.Right - pad - 10, y + rowH * 0.45f,
            SKTextAlign.Right, _valueFont, _textPaint);

        // Slider-Track
        var sliderL = pad + 20;
        var sliderR = bounds.Right - pad - 60;
        var sliderY = y + rowH * 0.7f;
        var trackH = 4f;
        sliderRect = new SKRect(sliderL, sliderY - 15, sliderR, sliderY + 15);

        _sliderTrackPaint.Color = UIRenderer.Border;
        canvas.DrawRect(sliderL, sliderY - trackH / 2, sliderR - sliderL, trackH, _sliderTrackPaint);

        // Filled Track
        var fillW = (sliderR - sliderL) * value;
        _sliderFillPaint.Color = UIRenderer.Primary;
        canvas.DrawRect(sliderL, sliderY - trackH / 2, fillW, trackH, _sliderFillPaint);

        // Thumb
        _sliderThumbPaint.Color = UIRenderer.PrimaryGlow;
        canvas.DrawCircle(sliderL + fillW, sliderY, 8f, _sliderThumbPaint);

        return y + rowH;
    }

    public override void HandleInput(InputAction action, SKPoint position)
    {
        if (action == InputAction.Back)
        {
            SaveSettings();
            SceneManager.PopScene();
            return;
        }

        if (action == InputAction.Tap)
        {
            // Zurück
            if (_backButtonRect.Contains(position))
            {
                SaveSettings();
                SceneManager.PopScene();
                return;
            }

            // Toggles
            if (_sfxToggleRect.Contains(position))
            {
                _audioService.SfxEnabled = !_audioService.SfxEnabled;
                return;
            }
            if (_bgmToggleRect.Contains(position))
            {
                _audioService.BgmEnabled = !_audioService.BgmEnabled;
                return;
            }
            if (_vibrationToggleRect.Contains(position))
            {
                _audioService.VibrationEnabled = !_audioService.VibrationEnabled;
                return;
            }

            // Text-Speed Buttons
            for (int i = 0; i < 4; i++)
            {
                if (_speedButtonRects[i].Contains(position))
                {
                    _textSpeed = i;
                    _preferences.Set("settings_text_speed", i);
                    return;
                }
            }
        }
    }

    public override void HandlePointerDown(SKPoint position)
    {
        if (_sfxSliderRect.Contains(position))
        {
            _isDraggingSfx = true;
            UpdateSliderValue(position.X, _sfxSliderRect, v => _audioService.SfxVolume = v);
        }
        else if (_bgmSliderRect.Contains(position))
        {
            _isDraggingBgm = true;
            UpdateSliderValue(position.X, _bgmSliderRect, v => _audioService.BgmVolume = v);
        }
    }

    public override void HandlePointerMove(SKPoint position)
    {
        if (_isDraggingSfx)
            UpdateSliderValue(position.X, _sfxSliderRect, v => _audioService.SfxVolume = v);
        else if (_isDraggingBgm)
            UpdateSliderValue(position.X, _bgmSliderRect, v => _audioService.BgmVolume = v);
    }

    public override void HandlePointerUp(SKPoint position)
    {
        _isDraggingSfx = false;
        _isDraggingBgm = false;
    }

    private static void UpdateSliderValue(float x, SKRect sliderRect, Action<float> setter)
    {
        var t = Math.Clamp((x - sliderRect.Left) / sliderRect.Width, 0f, 1f);
        setter(t);
    }

    /// <summary>Gibt statische Ressourcen frei.</summary>
    public static void Cleanup()
    {
        _bgPaint.Dispose();
        _panelPaint.Dispose();
        _borderPaint.Dispose();
        _sliderTrackPaint.Dispose();
        _sliderFillPaint.Dispose();
        _sliderThumbPaint.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
        _valueFont.Dispose();
        _textPaint.Dispose();
    }
}
