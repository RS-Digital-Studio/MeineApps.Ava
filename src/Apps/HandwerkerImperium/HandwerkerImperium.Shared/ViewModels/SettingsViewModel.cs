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
    private readonly IPlayGamesService _playGamesService;

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
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _cloudSaveEnabled = true;

    [ObservableProperty]
    private bool _isPlayGamesSignedIn;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    // Cloud Save
    [ObservableProperty]
    private string _playGamesStatusText = "";

    [ObservableProperty]
    private string _lastCloudSaveText = "";

    [ObservableProperty]
    private bool _hasLastCloudSave;

    // Automatisierungs-Toggles
    [ObservableProperty]
    private bool _autoCollectDelivery;

    [ObservableProperty]
    private bool _autoAcceptOrder;

    [ObservableProperty]
    private bool _autoAssignWorkers;

    [ObservableProperty]
    private bool _autoClaimDaily;

    // Level-Gates für Automatisierung (delegiert an GameStateService)
    public bool IsAutoCollectUnlocked => _gameStateService.IsAutoCollectUnlocked;
    public bool IsAutoAcceptUnlocked => _gameStateService.IsAutoAcceptUnlocked;
    public bool IsAutoAssignUnlocked => _gameStateService.IsAutoAssignUnlocked;
    public bool IsAutoClaimUnlocked => _purchaseService.IsPremium;

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
    private bool _isBusy;

    public SettingsViewModel(
        IAudioService audioService,
        ILocalizationService localizationService,
        ISaveGameService saveGameService,
        IGameStateService gameStateService,
        IPurchaseService purchaseService,
        IPlayGamesService playGamesService)
    {
        _audioService = audioService;
        _localizationService = localizationService;
        _saveGameService = saveGameService;
        _gameStateService = gameStateService;
        _purchaseService = purchaseService;
        _playGamesService = playGamesService;

        // Don't load settings here - GameState is not initialized yet.
        // MainViewModel.InitializeAsync() will call ReloadSettings() after loading the save.
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
            NotificationsEnabled = state.NotificationsEnabled;
            CloudSaveEnabled = state.CloudSaveEnabled;

            // Automatisierungs-Einstellungen laden
            AutoCollectDelivery = state.Automation.AutoCollectDelivery;
            AutoAcceptOrder = state.Automation.AutoAcceptOrder;
            AutoAssignWorkers = state.Automation.AutoAssignWorkers;
            AutoClaimDaily = state.Automation.AutoClaimDaily;

            // Level-Gates aktualisieren
            OnPropertyChanged(nameof(IsAutoCollectUnlocked));
            OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
            OnPropertyChanged(nameof(IsAutoAssignUnlocked));
            OnPropertyChanged(nameof(IsAutoClaimUnlocked));

            // Fallback auf aktuelle Sprache (Gerätesprache) statt Languages[0] (English)
            var langCode = !string.IsNullOrEmpty(state.Language) ? state.Language : _localizationService.CurrentLanguage;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == langCode) ?? Languages[0];
            IsPremium = state.IsPremium;

            // Get app version from assembly
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            var version = assembly?.GetName().Version;
            AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

            // Cloud Save Status initialisieren
            RefreshPlayGamesStatus();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Aktualisiert den Play Games Anmelde-Status und Cloud-Save-Zeitstempel.
    /// </summary>
    private void RefreshPlayGamesStatus()
    {
        IsPlayGamesSignedIn = _playGamesService.IsSignedIn;

        if (IsPlayGamesSignedIn)
        {
            var name = _playGamesService.PlayerDisplayName;
            PlayGamesStatusText = !string.IsNullOrEmpty(name)
                ? $"{_localizationService.GetString("SignedInAs")} {name}"
                : _localizationService.GetString("SignedIn");
        }
        else
        {
            PlayGamesStatusText = _localizationService.GetString("NotSignedIn");
        }

        // Letzter Cloud-Save Zeitstempel
        var state = _gameStateService.State;
        if (state.LastCloudSaveTime != default)
        {
            HasLastCloudSave = true;
            LastCloudSaveText = $"{_localizationService.GetString("LastCloudSave")}: {state.LastCloudSaveTime.ToLocalTime():dd.MM.yyyy HH:mm}";
        }
        else
        {
            HasLastCloudSave = false;
            LastCloudSaveText = "";
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

    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.State.NotificationsEnabled = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnCloudSaveEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.State.CloudSaveEnabled = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoCollectDeliveryChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.State.Automation.AutoCollectDelivery = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoAcceptOrderChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.State.Automation.AutoAcceptOrder = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoAssignWorkersChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.State.Automation.AutoAssignWorkers = value;
        _gameStateService.MarkDirty();
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoClaimDailyChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.State.Automation.AutoClaimDaily = value;
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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            var success = await _purchaseService.PurchaseRemoveAdsAsync();
            IsPremium = _purchaseService.IsPremium;

            if (IsPremium)
            {
                _gameStateService.State.IsPremium = true;
                _gameStateService.MarkDirty();
                await _saveGameService.SaveAsync();
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            await _purchaseService.RestorePurchasesAsync();
            IsPremium = _purchaseService.IsPremium;

            if (IsPremium)
            {
                _gameStateService.State.IsPremium = true;
                _gameStateService.MarkDirty();
                await _saveGameService.SaveAsync();
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignInPlayGamesAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            var success = await _playGamesService.SignInAsync();
            RefreshPlayGamesStatus();

            if (!success)
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("SignInFailed"),
                    _localizationService.GetString("OK"));
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveToCloudAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            if (!_playGamesService.IsSignedIn || !_playGamesService.SupportsCloudSave)
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveNotAvailable"),
                    _localizationService.GetString("OK"));
                return;
            }

            // Aktuellen Spielstand exportieren
            var json = await _saveGameService.ExportSaveAsync();
            if (string.IsNullOrEmpty(json))
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveFailed"),
                    _localizationService.GetString("OK"));
                return;
            }

            var description = $"Lv.{_gameStateService.State.PlayerLevel} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
            var success = await _playGamesService.SaveToCloudAsync(json, description);

            if (success)
            {
                _gameStateService.State.LastCloudSaveTime = DateTime.UtcNow;
                _gameStateService.MarkDirty();
                await _saveGameService.SaveAsync();
                RefreshPlayGamesStatus();

                ShowAlert(
                    _localizationService.GetString("CloudSave"),
                    _localizationService.GetString("CloudSaveSuccess"),
                    _localizationService.GetString("OK"));
            }
            else
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveFailed"),
                    _localizationService.GetString("OK"));
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreFromCloudAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            if (!_playGamesService.IsSignedIn || !_playGamesService.SupportsCloudSave)
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveNotAvailable"),
                    _localizationService.GetString("OK"));
                return;
            }

            // Bestätigungsdialog: Lokaler Spielstand wird überschrieben
            bool confirmed = false;
            if (ConfirmationRequested != null)
            {
                confirmed = await ConfirmationRequested.Invoke(
                    _localizationService.GetString("RestoreFromCloud"),
                    _localizationService.GetString("RestoreFromCloudConfirmation"),
                    _localizationService.GetString("YesRestore"),
                    _localizationService.GetString("Cancel"));
            }

            if (!confirmed) return;

            var json = await _playGamesService.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(json))
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudRestoreFailed"),
                    _localizationService.GetString("OK"));
                return;
            }

            var success = await _saveGameService.ImportSaveAsync(json);
            if (success)
            {
                ShowAlert(
                    _localizationService.GetString("CloudSave"),
                    _localizationService.GetString("CloudRestoreSuccess"),
                    _localizationService.GetString("OK"));

                // Navigation zum Hauptmenü um neuen State zu laden
                NavigationRequested?.Invoke("//main");
            }
            else
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudRestoreFailed"),
                    _localizationService.GetString("OK"));
            }
        }
        finally
        {
            _isBusy = false;
        }
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
                _localizationService.GetString("OK"));

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
                _localizationService.GetString("OK"));
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
                _localizationService.GetString("OK"));
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
