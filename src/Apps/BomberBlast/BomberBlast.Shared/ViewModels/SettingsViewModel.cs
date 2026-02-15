using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Core;
using BomberBlast.Input;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer die Einstellungen.
/// Verwaltet Input-Modus, Sound-Lautstärke, Sprache und Premium-Status.
/// Persistiert alle Einstellungen via InputManager und SoundManager.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IProgressService _progressService;
    private readonly IHighScoreService _highScoreService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IGameStyleService _gameStyleService;
    private readonly InputManager _inputManager;
    private readonly SoundManager _soundManager;

    private bool _isInitializing = true;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    /// <summary>
    /// Event to request a confirmation dialog.
    /// Parameters: title, message, acceptText, cancelText. Returns bool.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - CONTROLS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _joystickFixed; // false=schwebend, true=fixiert

    [ObservableProperty]
    private double _joystickSize = 120;

    [ObservableProperty]
    private double _joystickOpacity = 0.7;

    [ObservableProperty]
    private bool _hapticEnabled = true;

    [ObservableProperty]
    private string _joystickSizeText = "120";

    [ObservableProperty]
    private string _joystickOpacityText = "70%";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - SOUND
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _sfxEnabled = true;

    [ObservableProperty]
    private double _sfxVolume = 1.0;

    [ObservableProperty]
    private bool _musicEnabled = true;

    [ObservableProperty]
    private double _musicVolume = 0.7;

    [ObservableProperty]
    private string _sfxVolumeText = "100%";

    [ObservableProperty]
    private string _musicVolumeText = "70%";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - VISUAL STYLE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Whether Classic style is selected.</summary>
    public bool IsClassicSelected
    {
        get => _gameStyleService.CurrentStyle == GameVisualStyle.Classic;
        set { if (value) SelectStyle("Classic"); }
    }

    /// <summary>Whether Neon style is selected.</summary>
    public bool IsNeonSelected
    {
        get => _gameStyleService.CurrentStyle == GameVisualStyle.Neon;
        set { if (value) SelectStyle("Neon"); }
    }

    /// <summary>Localized label for visual style section.</summary>
    public string VisualStyleText => _localizationService.GetString("VisualStyle");

    /// <summary>Localized name for Classic style.</summary>
    public string ClassicStyleName => _localizationService.GetString("StyleClassic");

    /// <summary>Localized name for Neon style.</summary>
    public string NeonStyleName => _localizationService.GetString("StyleNeon");

    /// <summary>Localized description for Classic style.</summary>
    public string ClassicStyleDesc => _localizationService.GetString("StyleClassicDesc");

    /// <summary>Localized description for Neon style.</summary>
    public string NeonStyleDesc => _localizationService.GetString("StyleNeonDesc");

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - LANGUAGE & PREMIUM
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _selectedLanguage = "en";

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isBuyingPremium;

    [ObservableProperty]
    private string _versionText = "BomberBlast v1.0.0";

    [ObservableProperty]
    private string _copyrightText = "";

    /// <summary>Alias for VersionText used in the View.</summary>
    public string AppVersion => VersionText;

    // Available languages for the UI
    public List<LanguageOption> Languages { get; } =
    [
        new("Deutsch", "de"),
        new("English", "en"),
        new("Espanol", "es"),
        new("Francais", "fr"),
        new("Italiano", "it"),
        new("Portugues", "pt")
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public SettingsViewModel(
        IProgressService progressService,
        IHighScoreService highScoreService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IGameStyleService gameStyleService,
        InputManager inputManager,
        SoundManager soundManager)
    {
        _progressService = progressService;
        _highScoreService = highScoreService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _gameStyleService = gameStyleService;
        _inputManager = inputManager;
        _soundManager = soundManager;

        // Version info
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        VersionText = version != null
            ? $"BomberBlast v{version.Major}.{version.Minor}.{version.Build}"
            : "BomberBlast v1.0.0";
        CopyrightText = $"\u00a9 {DateTime.Now.Year} RS-Digital";

        LoadSettings();
        _isInitializing = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadSettings()
    {
        // Input-Einstellungen aus InputManager laden
        JoystickFixed = _inputManager.JoystickFixed;
        JoystickSize = _inputManager.JoystickSize;
        JoystickOpacity = _inputManager.JoystickOpacity;
        HapticEnabled = _inputManager.HapticEnabled;

        // Sound-Einstellungen aus SoundManager laden
        SfxEnabled = _soundManager.SfxEnabled;
        SfxVolume = _soundManager.SfxVolume;
        MusicEnabled = _soundManager.MusicEnabled;
        MusicVolume = _soundManager.MusicVolume;

        // Sprache
        SelectedLanguage = _localizationService.CurrentLanguage;

        // Premium
        IsPremium = _purchaseService.IsPremium;
    }

    /// <summary>
    /// Called when the view appears.
    /// </summary>
    public void OnAppearing()
    {
        IsPremium = _purchaseService.IsPremium;

    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTY CHANGE HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnJoystickFixedChanged(bool value)
    {
        if (_isInitializing) return;

        _inputManager.JoystickFixed = value;
        _inputManager.SaveSettings();
    }

    partial void OnJoystickSizeChanged(double value)
    {
        JoystickSizeText = $"{(int)value}";
        if (_isInitializing) return;

        _inputManager.JoystickSize = (float)value;
        _inputManager.SaveSettings();
    }

    partial void OnJoystickOpacityChanged(double value)
    {
        JoystickOpacityText = $"{(int)(value * 100)}%";
        if (_isInitializing) return;

        _inputManager.JoystickOpacity = (float)value;
        _inputManager.SaveSettings();
    }

    partial void OnHapticEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _inputManager.HapticEnabled = value;
        _inputManager.SaveSettings();
    }

    partial void OnSfxEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _soundManager.SfxEnabled = value;
        _soundManager.SaveSettings();
    }

    partial void OnSfxVolumeChanged(double value)
    {
        SfxVolumeText = $"{(int)(value * 100)}%";
        if (_isInitializing) return;

        _soundManager.SfxVolume = (float)value;
        _soundManager.SaveSettings();
    }

    partial void OnMusicEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _soundManager.MusicEnabled = value;
        _soundManager.SaveSettings();
    }

    partial void OnMusicVolumeChanged(double value)
    {
        MusicVolumeText = $"{(int)(value * 100)}%";
        if (_isInitializing) return;

        _soundManager.MusicVolume = (float)value;
        _soundManager.SaveSettings();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - LANGUAGE
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectLanguage(string code)
    {
        if (_isInitializing || string.IsNullOrEmpty(code))
            return;

        SelectedLanguage = code;
        _localizationService.SetLanguage(code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - VISUAL STYLE
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectStyle(string style)
    {
        if (_isInitializing || string.IsNullOrEmpty(style))
            return;

        if (Enum.TryParse<GameVisualStyle>(style, out var parsed))
        {
            _gameStyleService.SetStyle(parsed);
            OnPropertyChanged(nameof(IsClassicSelected));
            OnPropertyChanged(nameof(IsNeonSelected));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - DATA MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ResetProgressAsync()
    {
        bool confirmed = false;
        if (ConfirmationRequested != null)
        {
            confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ResetProgress"),
                _localizationService.GetString("ResetProgressConfirm"),
                _localizationService.GetString("Reset"),
                _localizationService.GetString("Cancel"));
        }

        if (confirmed)
        {
            _progressService.ResetProgress();
            ShowAlert(
                _localizationService.GetString("ResetProgress"),
                _localizationService.GetString("ProgressResetDone"),
                _localizationService.GetString("OK"));
        }
    }

    [RelayCommand]
    private async Task ClearHighScoresAsync()
    {
        bool confirmed = false;
        if (ConfirmationRequested != null)
        {
            confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ClearHighScores"),
                _localizationService.GetString("ClearScoresConfirm"),
                _localizationService.GetString("Clear"),
                _localizationService.GetString("Cancel"));
        }

        if (confirmed)
        {
            _highScoreService.ClearScores();
            ShowAlert(
                _localizationService.GetString("ClearHighScores"),
                _localizationService.GetString("ScoresClearedDone"),
                _localizationService.GetString("OK"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - PREMIUM
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task BuyPremiumAsync()
    {
        try
        {
            IsBuyingPremium = true;
            var success = await _purchaseService.PurchaseRemoveAdsAsync();

            if (success)
            {
                IsPremium = true;
        
                ShowAlert(
                    _localizationService.GetString("ThankYou"),
                    _localizationService.GetString("PremiumActivated"),
                    _localizationService.GetString("OK"));
            }
            else
            {
                ShowAlert(
                    _localizationService.GetString("PurchaseFailed"),
                    _localizationService.GetString("PurchaseFailedMessage"),
                    _localizationService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            ShowAlert(
                _localizationService.GetString("Error"),
                $"{_localizationService.GetString("ErrorOccurred")}: {ex.Message}",
                _localizationService.GetString("OK"));
        }
        finally
        {
            IsBuyingPremium = false;
        }
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        try
        {
            var success = await _purchaseService.RestorePurchasesAsync();

            if (success && _purchaseService.IsPremium)
            {
                IsPremium = true;
        
                ShowAlert(
                    _localizationService.GetString("Restored"),
                    _localizationService.GetString("PurchaseRestored"),
                    _localizationService.GetString("OK"));
            }
            else
            {
                ShowAlert(
                    _localizationService.GetString("NoPurchases"),
                    _localizationService.GetString("NoPurchasesFound"),
                    _localizationService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            ShowAlert(
                _localizationService.GetString("Error"),
                $"{_localizationService.GetString("RestoreFailed")}: {ex.Message}",
                _localizationService.GetString("OK"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        MeineApps.Core.Ava.Services.UriLauncher.OpenUri(
            "https://sites.google.com/rs-digital.org/bomberblast/startseite");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlert(string title, string message, string buttonText)
    {
        AlertRequested?.Invoke(title, message, buttonText);
    }
}

/// <summary>
/// Represents a language option for selection.
/// </summary>
public record LanguageOption(string DisplayName, string Code);
