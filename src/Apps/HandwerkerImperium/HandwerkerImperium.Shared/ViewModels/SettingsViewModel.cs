using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// Manages game settings like sound, language, and premium status.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly ISaveGameService _saveGameService;
    private readonly IGameStateService _gameStateService;
    private readonly IPurchaseService _purchaseService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

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
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _soundEnabled = true;

    [ObservableProperty]
    private bool _vibrationEnabled = true;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    // Available languages
    public List<LanguageOption> Languages { get; } =
    [
        new("English", "en"),
        new("Deutsch", "de"),
        new("Español", "es"),
        new("Français", "fr"),
        new("Italiano", "it"),
        new("Português", "pt")
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    private bool _isInitializing;

    public SettingsViewModel(
        IAudioService audioService,
        ILocalizationService localizationService,
        ISaveGameService saveGameService,
        IGameStateService gameStateService,
        IPurchaseService purchaseService)
    {
        _audioService = audioService;
        _localizationService = localizationService;
        _saveGameService = saveGameService;
        _gameStateService = gameStateService;
        _purchaseService = purchaseService;

        // Don't load settings here - GameState is not initialized yet.
        // MainViewModel.Initialize() will call ReloadSettings() after loading the save.
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reload settings from game state. Called by MainViewModel after save is loaded.
    /// </summary>
    public void ReloadSettings()
    {
        _isInitializing = true;
        try
        {
            var state = _gameStateService.State;

            SoundEnabled = state.SoundEnabled;
            VibrationEnabled = state.HapticsEnabled;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == state.Language) ?? Languages[0];
            IsPremium = state.IsPremium;

            // Get app version from assembly
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            var version = assembly?.GetName().Version;
            AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
        finally
        {
            _isInitializing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTY CHANGE HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.State.SoundEnabled = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();

        if (value)
        {
            _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        }
    }

    partial void OnVibrationEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.State.HapticsEnabled = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing || value == null) return;

        _gameStateService.State.Language = value.Code;
        _localizationService.SetLanguage(value.Code);
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private async Task BuyPremiumAsync()
    {
        // TODO: Implement IAP
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        ShowAlert(
            _localizationService.GetString("Premium"),
            _localizationService.GetString("ComingSoonDesc"),
            "OK");
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        // TODO: Implement restore purchases
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        ShowAlert(
            _localizationService.GetString("RestorePurchases"),
            _localizationService.GetString("RestoringPurchasesMessage"),
            "OK");
    }

    [RelayCommand]
    private async Task ResetGameAsync()
    {
        bool confirmed = false;
        if (ConfirmationRequested != null)
        {
            confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ResetGameTitle"),
                _localizationService.GetString("ResetGameConfirmation"),
                _localizationService.GetString("YesReset"),
                _localizationService.GetString("Cancel"));
        }
        else
        {
            return;
        }

        if (confirmed)
        {
            _gameStateService.Reset();
            await _saveGameService.DeleteSaveAsync();

            ShowAlert(
                _localizationService.GetString("GameResetCompleteTitle"),
                _localizationService.GetString("GameResetComplete"),
                "OK");

            // Navigate back to main page
            NavigationRequested?.Invoke("//main");
        }
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        try
        {
            UriLauncher.OpenUri("https://sites.google.com/rs-digital.org/handwerkerimperium/privacy");
        }
        catch
        {
            ShowAlert(
                _localizationService.GetString("Error"),
                _localizationService.GetString("PrivacyPolicyOpenError"),
                "OK");
        }
    }

    [RelayCommand]
    private void SendFeedback()
    {
        try
        {
            var subject = Uri.EscapeDataString($"Handwerker Imperium Feedback (v{AppVersion})");
            var body = Uri.EscapeDataString(_localizationService.GetString("FeedbackBody"));
            UriLauncher.OpenUri($"mailto:info@rs-digital.org?subject={subject}&body={body}");
        }
        catch
        {
            ShowAlert(
                _localizationService.GetString("Error"),
                _localizationService.GetString("EmailOpenError"),
                "OK");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlert(string title, string message, string buttonText)
    {
        AlertRequested?.Invoke(title, message, buttonText);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents a language option for the picker.
/// </summary>
public record LanguageOption(string DisplayName, string Code);
