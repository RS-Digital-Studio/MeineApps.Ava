using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// Manages game settings like sound, language, and premium status.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, INavigable
{
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly ISaveGameService _saveGameService;
    private readonly IGameStateService _gameStateService;
    private readonly IPurchaseService _purchaseService;
    private readonly IPlayGamesService _playGamesService;
    private readonly IContextualHintService _contextualHintService;
    private readonly IDialogService _dialogService;
    // Telemetrie + Cloud-Save (Firebase-REST, plattformuebergreifend)
    private readonly IAnalyticsService? _analyticsService;
    private readonly ICloudSaveService? _cloudSaveService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

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
    [NotifyPropertyChangedFor(nameof(CanUseCloudSave))]
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

    /// <summary>DSGVO-Consent fuer anonyme Telemetrie. Gespeichert in SettingsData.AnalyticsEnabled.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseCloudSave))]
    private bool _analyticsEnabled;

    /// <summary>Gibt an ob Firebase online erreichbar ist (Cloud-Save-Upload-Button deaktivieren wenn offline).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseCloudSave))]
    private bool _isCloudSaveOnline;

    /// <summary>True wenn Cloud-Save per Firebase oder Play Games nutzbar ist.</summary>
    public bool CanUseCloudSave => IsCloudSaveOnline || IsPlayGamesSignedIn;

    /// <summary>Lokalisierter Status-Text fuer letzte Cloud-Save-Aktion (Upload/Download).</summary>
    [ObservableProperty]
    private string _cloudSaveActionStatus = "";

    // Grafik-Qualität
    [ObservableProperty]
    private GraphicsQualityOption? _selectedGraphicsQuality;

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

    // Grafik-Qualitäts-Optionen (lokalisiert, wird im Konstruktor befüllt)
    public List<GraphicsQualityOption> GraphicsQualities { get; } = [];

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

    /// <summary>AAA-Audit P1: Cross-Promotion-Karte (House-Ad zwischen den 11 eigenen Apps).</summary>
    public CrossPromoViewModel CrossPromoVM { get; }

    public SettingsViewModel(
        IAudioService audioService,
        ILocalizationService localizationService,
        ISaveGameService saveGameService,
        IGameStateService gameStateService,
        IPurchaseService purchaseService,
        IPlayGamesService playGamesService,
        IContextualHintService contextualHintService,
        IDialogService dialogService,
        CrossPromoViewModel crossPromoVm,
        IAnalyticsService? analyticsService = null,
        ICloudSaveService? cloudSaveService = null)
    {
        _audioService = audioService;
        _localizationService = localizationService;
        _saveGameService = saveGameService;
        _gameStateService = gameStateService;
        _purchaseService = purchaseService;
        _playGamesService = playGamesService;
        _contextualHintService = contextualHintService;
        _dialogService = dialogService;
        _analyticsService = analyticsService;
        _cloudSaveService = cloudSaveService;
        CrossPromoVM = crossPromoVm;

        // Grafik-Qualitäts-Optionen lokalisiert befüllen
        GraphicsQualities.Add(new(localizationService.GetString("GraphicsLow") ?? "Low", GraphicsQuality.Low));
        GraphicsQualities.Add(new(localizationService.GetString("GraphicsMedium") ?? "Medium", GraphicsQuality.Medium));
        GraphicsQualities.Add(new(localizationService.GetString("GraphicsHigh") ?? "High", GraphicsQuality.High));

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

            SoundEnabled = state.Settings.SoundEnabled;
            VibrationEnabled = state.Settings.HapticsEnabled;
            NotificationsEnabled = state.Settings.NotificationsEnabled;
            CloudSaveEnabled = state.Settings.CloudSaveEnabled;
            AnalyticsEnabled = state.Settings.AnalyticsEnabled;
            IsCloudSaveOnline = _cloudSaveService?.IsAvailable ?? false;

            // Grafik-Qualitaet laden
            SelectedGraphicsQuality = GraphicsQualities.FirstOrDefault(q => q.Quality == state.Settings.GraphicsQuality)
                                      ?? GraphicsQualities[2]; // High als Fallback

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
            var langCode = !string.IsNullOrEmpty(state.Settings.Language) ? state.Settings.Language : _localizationService.CurrentLanguage;
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
        if (state.Settings.LastCloudSaveTime != default)
        {
            HasLastCloudSave = true;
            LastCloudSaveText = $"{_localizationService.GetString("LastCloudSave")}: {state.Settings.LastCloudSaveTime.ToLocalTime():dd.MM.yyyy HH:mm}";
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

        _gameStateService.Settings.SoundEnabled = value;
        _saveGameService.SaveAsync().FireAndForget();

        if (value)
        {
            _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        }
    }

    partial void OnVibrationEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.Settings.HapticsEnabled = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.Settings.NotificationsEnabled = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnCloudSaveEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _gameStateService.Settings.CloudSaveEnabled = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAnalyticsEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        if (_analyticsService != null)
        {
            _analyticsService.IsEnabled = value; // persistiert in Settings + startet/stoppt Flush
            if (value) _ = _analyticsService.InitializeAsync();
        }
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnSelectedGraphicsQualityChanged(GraphicsQualityOption? value)
    {
        if (_isInitializing || value == null) return;

        _gameStateService.Settings.GraphicsQuality = value.Quality;
        // FpsProfile sofort aktualisieren — bereits laufende Render-Timer lesen den
        // neuen Wert beim naechsten Neustart (Tab-Wechsel, IsVisible-Toggle),
        // der WorkerAvatar-Shared-Timer reagiert sofort via CurrentChanged-Event.
        Graphics.FpsProfile.SetCurrent(value.Quality);

        // AAA-Audit P2 A11y: ReduceMotion sofort an GameJuiceEngine durchreichen,
        // damit Confetti/CoinFly/Sparkle/RadialBurst sofort respektiert werden —
        // ohne App-Neustart.
        var juice = App.Services?.GetService(typeof(Graphics.GameJuiceEngine)) as Graphics.GameJuiceEngine;
        if (juice != null)
            juice.ReduceMotion = value.Quality == GraphicsQuality.Low;

        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoCollectDeliveryChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.Automation.AutoCollectDelivery = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoAcceptOrderChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.Automation.AutoAcceptOrder = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoAssignWorkersChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.Automation.AutoAssignWorkers = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnAutoClaimDailyChanged(bool value)
    {
        if (_isInitializing) return;
        _gameStateService.Automation.AutoClaimDaily = value;
        _saveGameService.SaveAsync().FireAndForget();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing || value == null) return;

        _gameStateService.Settings.Language = value.Code;
        _localizationService.SetLanguage(value.Code);
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

    // Manuelle Upload/Download-Commands sind bewusst nicht nochmal angelegt — die bestehenden
    // SaveToCloudCommand / RestoreFromCloudCommand nutzen jetzt intern den ICloudSaveService
    // (Firebase-REST) und fallen nur bei fehlender Firebase-Verbindung auf Play Games zurueck.

    [RelayCommand]
    private void NavigateToStatistics()
    {
        NavigationRequested?.Invoke("../statistics");
    }

    [RelayCommand]
    private async Task BuyPremiumAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseStarted, new Dictionary<string, object?>
            {
                ["item"] = "remove_ads_premium"
            });

            var success = await _purchaseService.PurchaseRemoveAdsAsync();
            IsPremium = _purchaseService.IsPremium;

            if (IsPremium)
            {
                _gameStateService.State.IsPremium = true;
                _gameStateService.State.InvalidateMaxOfflineHoursCache();
                await _saveGameService.SaveAsync();

                _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseSuccess, new Dictionary<string, object?>
                {
                    ["item"] = "remove_ads_premium"
                });
                _analyticsService?.SetUserProperty(Models.AnalyticsUserProperties.Premium, "true");
            }
            else
            {
                _analyticsService?.TrackEvent(Models.AnalyticsEvents.IapPurchaseFailed, new Dictionary<string, object?>
                {
                    ["item"] = "remove_ads_premium"
                });
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
                _gameStateService.State.InvalidateMaxOfflineHoursCache();
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

            // Bevorzugt Firebase-Cloud-Save (plattformuebergreifend, REST).
            // Fallback auf Play-Games-Snapshots wenn Firebase offline und Play Games verfuegbar.
            if (_cloudSaveService != null && _cloudSaveService.IsAvailable)
            {
                // v2.1.1 (Audit FB-C02): Vor dem Upload pruefen, ob der Cloud-Stand bereits neuer oder staerker
                // ist — sonst ueberschreibt ein manueller Upload kommentarlos einen Fortschritt
                // von einem anderen Geraet. Bei Konflikt: Diff-Dialog, Spieler entscheidet.
                var cloudMeta = await _cloudSaveService.GetMetadataAsync();
                if (cloudMeta != null)
                {
                    var local = _gameStateService.State;
                    bool cloudIsNewer = cloudMeta.SavedAtUtc > local.LastSavedAt.AddSeconds(5);
                    bool cloudIsStronger = cloudMeta.PlayerLevel > local.PlayerLevel;
                    if (cloudIsNewer || cloudIsStronger)
                    {
                        var localLine = string.Format(
                            _localizationService.GetString("CloudSaveLocalSummary") ?? "Local: Level {0} ({1})",
                            local.PlayerLevel, Helpers.MoneyFormatter.FormatCompact(local.Money));
                        var cloudLine = string.Format(
                            _localizationService.GetString("CloudSaveCloudSummary") ?? "Cloud: Level {0} ({1})",
                            cloudMeta.PlayerLevel, Helpers.MoneyFormatter.FormatCompact(cloudMeta.Money));
                        var overwriteConfirmed = await _dialogService.ShowConfirmDialog(
                            _localizationService.GetString("CloudSave"),
                            $"{localLine}\n{cloudLine}",
                            _localizationService.GetString("Continue"),
                            _localizationService.GetString("Cancel"));
                        if (!overwriteConfirmed) return;
                    }
                }

                await _saveGameService.SaveAsync(); // Lokal erst sichern
                var ok = await _cloudSaveService.UploadAsync(_gameStateService.State);
                if (ok)
                {
                    _gameStateService.Settings.LastCloudSaveTime = DateTime.UtcNow;
                    await _saveGameService.SaveAsync();
                    RefreshPlayGamesStatus();
                    _analyticsService?.TrackEvent(Models.AnalyticsEvents.CloudSaveUploaded, null);
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
                return;
            }

            if (!_playGamesService.IsSignedIn || !_playGamesService.SupportsCloudSave)
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveNotAvailable"),
                    _localizationService.GetString("OK"));
                return;
            }

            // Legacy-Fallback: Play-Games-Snapshots (aktuell Stub, tritt nur bei Desktop/Offline Firebase auf)
            var json = await _saveGameService.ExportSaveAsync();
            if (string.IsNullOrEmpty(json))
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveFailed"),
                    _localizationService.GetString("OK"));
                return;
            }

            var description = $"Lv.{_gameStateService.PlayerLevel} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
            var success = await _playGamesService.SaveToCloudAsync(json, description);

            if (success)
            {
                _gameStateService.Settings.LastCloudSaveTime = DateTime.UtcNow;
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

            // Bevorzugt Firebase-Cloud-Save
            if (_cloudSaveService != null && _cloudSaveService.IsAvailable)
            {
                // FB-C02: Metadaten zuerst holen → Diff-Confirm statt generischem
                // "wird ueberschrieben"-Hinweis. So sieht der Spieler, ob sein lokaler
                // Stand evtl. staerker ist und er durch den Restore Fortschritt verlieren wuerde.
                var cloudMeta = await _cloudSaveService.GetMetadataAsync();
                if (cloudMeta == null)
                {
                    ShowAlert(
                        _localizationService.GetString("Error"),
                        _localizationService.GetString("CloudRestoreFailed"),
                        _localizationService.GetString("OK"));
                    return;
                }

                var local = _gameStateService.State;
                var localLine = string.Format(
                    _localizationService.GetString("CloudSaveLocalSummary") ?? "Local: Level {0} ({1})",
                    local.PlayerLevel, Helpers.MoneyFormatter.FormatCompact(local.Money));
                var cloudLine = string.Format(
                    _localizationService.GetString("CloudSaveCloudSummary") ?? "Cloud: Level {0} ({1})",
                    cloudMeta.PlayerLevel, Helpers.MoneyFormatter.FormatCompact(cloudMeta.Money));
                var confirmed = await _dialogService.ShowConfirmDialog(
                    _localizationService.GetString("RestoreFromCloud"),
                    $"{localLine}\n{cloudLine}",
                    _localizationService.GetString("YesRestore"),
                    _localizationService.GetString("Cancel"));
                if (!confirmed) return;

                var cloudState = await _cloudSaveService.DownloadAsync();
                if (cloudState == null)
                {
                    ShowAlert(
                        _localizationService.GetString("Error"),
                        _localizationService.GetString("CloudRestoreFailed"),
                        _localizationService.GetString("OK"));
                    return;
                }
                var cloudJson = System.Text.Json.JsonSerializer.Serialize(cloudState);
                var okImport = await _saveGameService.ImportSaveAsync(cloudJson);
                if (okImport)
                {
                    _analyticsService?.TrackEvent(Models.AnalyticsEvents.CloudSaveDownloaded, null);
                    ShowAlert(
                        _localizationService.GetString("CloudSave"),
                        _localizationService.GetString("CloudRestoreSuccess"),
                        _localizationService.GetString("OK"));
                    NavigationRequested?.Invoke("//main");
                }
                else
                {
                    ShowAlert(
                        _localizationService.GetString("Error"),
                        _localizationService.GetString("CloudRestoreFailed"),
                        _localizationService.GetString("OK"));
                }
                return;
            }

            // Play-Games-Fallback: kein Metadaten-Read moeglich → generischer Confirm.
            var legacyConfirmed = await _dialogService.ShowConfirmDialog(
                _localizationService.GetString("RestoreFromCloud"),
                _localizationService.GetString("RestoreFromCloudConfirmation"),
                _localizationService.GetString("YesRestore"),
                _localizationService.GetString("Cancel"));
            if (!legacyConfirmed) return;

            if (!_playGamesService.IsSignedIn || !_playGamesService.SupportsCloudSave)
            {
                ShowAlert(
                    _localizationService.GetString("Error"),
                    _localizationService.GetString("CloudSaveNotAvailable"),
                    _localizationService.GetString("OK"));
                return;
            }

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
    private void ResetHints()
    {
        _contextualHintService.ResetAllHints();
        ShowAlert(
            _localizationService.GetString("ResetTutorialHintsTitle") ?? "Tutorial Reset",
            _localizationService.GetString("ResetTutorialHintsMessage") ?? "All tutorial hints will be shown again on the next game start.",
            _localizationService.GetString("OK") ?? "OK");
    }

    [RelayCommand]
    private async Task ResetGameAsync()
    {
        var confirmed = await _dialogService.ShowConfirmDialog(
            _localizationService.GetString("ResetGameTitle"),
            _localizationService.GetString("ResetGameConfirmation"),
            _localizationService.GetString("YesReset"),
            _localizationService.GetString("Cancel"));
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
            UriLauncher.OpenUri("https://rs-digital-studio.github.io/privacy/handwerkerimperium.html");
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
        _dialogService.ShowAlertDialog(title, message, buttonText);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents a language option for the picker.
/// </summary>
public record LanguageOption(string DisplayName, string Code);

/// <summary>
/// Represents a graphics quality option for the picker.
/// </summary>
public record GraphicsQualityOption(string DisplayName, GraphicsQuality Quality);
