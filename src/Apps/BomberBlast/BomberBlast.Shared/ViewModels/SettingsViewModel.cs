using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Ava.Services;
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
public sealed partial class SettingsViewModel : ViewModelBase, INavigable
{
    private readonly IProgressService _progressService;
    private readonly IHighScoreService _highScoreService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IGameStyleService _gameStyleService;
    private readonly IPlayGamesService _playGames;
    private readonly ICloudSaveService _cloudSaveService;
    private readonly InputManager _inputManager;
    private readonly SoundManager _soundManager;
    // v2.0.44 — : Accessibility + Performance + Privacy
    private readonly IPreferencesService _preferences;
    private readonly IAccessibilityService _accessibilityService;
    private readonly IAccountDeletionService? _accountDeletionService;
    // v2.0.60 (B-C16): Datenexport vor Konto-Löschung (DSGVO Art. 20).
    private readonly IDataExportService? _dataExportService;
    // Phase 23b — Premium-Tier-Status für Settings-Anzeige
    private readonly IBattlePassPlusService? _battlePassPlus;
    private readonly IVipSubscriptionService? _vipSubscription;
    // Welle 2 v2.0.58 : Funnel-Tracking fuer Accessibility-Toggles.
    private readonly IAnalyticsService? _analytics;

    private bool _isInitializing = true;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<NavigationRequest>? NavigationRequested;

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
    private bool _reducedEffects; // Reduzierte visuelle Effekte (Reduce Motion)

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - ACCESSIBILITY (v2.0.44)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Hochkontrast-Modus für UI (verstärkte Trennung Foreground/Background)</summary>
    [ObservableProperty]
    private bool _highContrast;

    /// <summary>Untertitel/Caption-Toggle für Audio-Cues (Boss-Roar, Time-Warning)</summary>
    [ObservableProperty]
    private bool _subtitlesEnabled;

    /// <summary>
    /// v2.0.60 (B-C10 / WCAG 2.1): Photosensitivity-Schutz. Drosselt hochfrequente
    /// Pulse-/Blitz-Effekte (Combo-Pulse 12 Hz, UltraComboFlash, Damage-Flash).
    /// </summary>
    [ObservableProperty]
    private bool _reducedFlashing;

    /// <summary>UI-Skalierung 0.75/1.0/1.25/1.5</summary>
    [ObservableProperty]
    private double _uiScale = 1.0;

    /// <summary>Colorblind-Modus: "Off", "Deuteranopia", "Protanopia", "Tritanopia"</summary>
    [ObservableProperty]
    private string _colorblindMode = "Off";

    public List<string> ColorblindModes { get; } = ["Off", "Deuteranopia", "Protanopia", "Tritanopia"];

    public List<double> UiScales { get; } = [0.75, 1.0, 1.25, 1.5];

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - PERFORMANCE (v2.0.44)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>true = 60 FPS, false = 30 FPS (Default).</summary>
    [ObservableProperty]
    private bool _useHighFrameRate;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - DSGVO PRIVACY (v2.0.55 — Phase 15 Security-Fix P0)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>DSGVO-Consent für anonyme Crash-Reports an Firebase Crashlytics. Default false.</summary>
    [ObservableProperty]
    private bool _crashlyticsConsent;

    /// <summary>DSGVO-Consent für anonyme Nutzungs-Statistiken an Firebase Analytics. Default false.</summary>
    [ObservableProperty]
    private bool _analyticsConsent;

    // === Phase 25b — Privacy-Center-Toggles ====================================

    /// <summary>Personalisierte Werbung (Behavioral-Targeting via AdMob). Default false.</summary>
    [ObservableProperty]
    private bool _personalizedAdsConsent;

    /// <summary>Push-Notifications (Re-Engagement, Daily-Reminder). Default true.</summary>
    [ObservableProperty]
    private bool _pushNotificationsConsent = true;

    /// <summary>COPPA-Toggle: Spieler &lt;13 → kontextuelle Ads only. Default false.</summary>
    [ObservableProperty]
    private bool _childSafeMode;

    // === Phase 23b — Premium-Tier-Anzeige =====================================

    /// <summary>True wenn Battle-Pass-Plus aktuell aktiv ist (nur Read-Display).</summary>
    [ObservableProperty]
    private bool _hasBattlePassPlus;

    /// <summary>True wenn VIP-Subscription aktuell aktiv ist (nur Read-Display).</summary>
    [ObservableProperty]
    private bool _hasVipSubscription;

    /// <summary>VIP-Ablaufdatum als formatierter String (für Settings-Anzeige).</summary>
    [ObservableProperty]
    private string _vipExpiresAtText = string.Empty;

    // === Phase 30b/c — Co-Op-Mode-Selector =====================================

    /// <summary>
    /// Co-Op-Modus aktiv (true = LocalCoop, false = Single-Player).
    /// Persistiert in Preferences. Engine.EnableMultiplayer wird beim nächsten Game-Start gelesen.
    /// </summary>
    [ObservableProperty]
    private bool _coOpModeEnabled;

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

    /// <summary>Whether Retro style is selected.</summary>
    public bool IsRetroSelected
    {
        get => _gameStyleService.CurrentStyle == GameVisualStyle.Retro;
        set { if (value) SelectStyle("Retro"); }
    }

    /// <summary>Localized label for visual style section.</summary>
    public string VisualStyleText => _localizationService.GetString("VisualStyle");

    /// <summary>Localized name for Classic style.</summary>
    public string ClassicStyleName => _localizationService.GetString("StyleClassic");

    /// <summary>Localized name for Neon style.</summary>
    public string NeonStyleName => _localizationService.GetString("StyleNeon");

    /// <summary>Localized name for Retro style.</summary>
    public string RetroStyleName => _localizationService.GetString("StyleRetro");

    /// <summary>Localized description for Classic style.</summary>
    public string ClassicStyleDesc => _localizationService.GetString("StyleClassicDesc");

    /// <summary>Localized description for Neon style.</summary>
    public string NeonStyleDesc => _localizationService.GetString("StyleNeonDesc");

    /// <summary>Localized description for Retro style.</summary>
    public string RetroStyleDesc => _localizationService.GetString("StyleRetroDesc");

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - GOOGLE PLAY GAMES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _playGamesEnabled;

    [ObservableProperty]
    private bool _isPlayGamesSignedIn;

    [ObservableProperty]
    private string _playGamesPlayerName = "";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - CLOUD SAVE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _cloudSaveEnabled;

    [ObservableProperty]
    private bool _isCloudSyncing;

    [ObservableProperty]
    private string _cloudSyncStatus = "";

    [ObservableProperty]
    private string _cloudSaveLocation = "";

    /// <summary>Lokalisierter Titel für Cloud Save Sektion.</summary>
    public string CloudSaveTitle => _localizationService.GetString("CloudSaveTitle");

    /// <summary>Lokalisierter Toggle-Text für Cloud Save.</summary>
    public string CloudSaveToggleText => _localizationService.GetString("CloudSaveToggle");

    /// <summary>Lokalisierter Sync-Button-Text.</summary>
    public string CloudSaveSyncText => _localizationService.GetString("CloudSaveSync") ?? "Sync";

    /// <summary>Lokalisierter Download-Button-Text.</summary>
    public string CloudSaveDownloadText => _localizationService.GetString("CloudSaveDownload") ?? "Download";

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES - LANGUAGE & PREMIUM
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private LanguageOption? _selectedLanguageOption;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isBuyingPremium;

    [ObservableProperty]
    private string _restoreButtonText = "";

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
        new("Español", "es"),
        new("Français", "fr"),
        new("Italiano", "it"),
        new("Português", "pt")
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
        IPlayGamesService playGames,
        ICloudSaveService cloudSaveService,
        InputManager inputManager,
        SoundManager soundManager,
        IPreferencesService preferences,
        IAccessibilityService accessibilityService,
        IAccountDeletionService? accountDeletionService = null,
        IBattlePassPlusService? battlePassPlus = null,
        IVipSubscriptionService? vipSubscription = null,
        IAnalyticsService? analytics = null,
        IDataExportService? dataExportService = null)
    {
        _progressService = progressService;
        _highScoreService = highScoreService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _gameStyleService = gameStyleService;
        _playGames = playGames;
        _cloudSaveService = cloudSaveService;
        _inputManager = inputManager;
        _soundManager = soundManager;
        _preferences = preferences;
        _accessibilityService = accessibilityService;
        _accountDeletionService = accountDeletionService;
        _battlePassPlus = battlePassPlus;
        _vipSubscription = vipSubscription;
        _analytics = analytics;
        _dataExportService = dataExportService;

        // Version info
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        VersionText = version != null
            ? $"BomberBlast v{version.Major}.{version.Minor}.{version.Build}"
            : "BomberBlast v1.0.0";
        CopyrightText = $"\u00a9 {DateTime.UtcNow.Year} RS-Digital";

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
        ReducedEffects = _inputManager.ReducedEffects;
        JoystickSize = _inputManager.JoystickSize;
        JoystickOpacity = _inputManager.JoystickOpacity;
        HapticEnabled = _inputManager.HapticEnabled;

        // Sound-Einstellungen aus SoundManager laden
        SfxEnabled = _soundManager.SfxEnabled;
        SfxVolume = _soundManager.SfxVolume;
        MusicEnabled = _soundManager.MusicEnabled;
        MusicVolume = _soundManager.MusicVolume;

        // Sprache - LanguageOption-Objekt anhand des aktuellen Codes suchen
        var currentLang = _localizationService.CurrentLanguage;
        SelectedLanguageOption = Languages.FirstOrDefault(l => l.Code == currentLang) ?? Languages[0];

        // Premium
        IsPremium = _purchaseService.IsPremium;

        // Google Play Games
        PlayGamesEnabled = _playGames.IsEnabled;
        IsPlayGamesSignedIn = _playGames.IsSignedIn;
        PlayGamesPlayerName = _playGames.PlayerName ?? "";

        // Accessibility (v2.0.44)
        ColorblindMode = _accessibilityService.ColorblindMode;
        HighContrast = _accessibilityService.HighContrast;
        UiScale = _accessibilityService.UiScale;
        SubtitlesEnabled = _accessibilityService.SubtitlesEnabled;
        // v2.0.60 (B-C10): Photosensitivity-Toggle laden.
        ReducedFlashing = _accessibilityService.ReducedFlashing;

        // Performance (v2.0.44)
        UseHighFrameRate = GameLoopSettings.TargetFps == GameLoopSettings.FrameRate60;

        // Privacy (v2.0.55 — DSGVO Consent-Flow für Firebase)
        CrashlyticsConsent = _preferences.Get("CrashlyticsConsent", false);
        AnalyticsConsent = _preferences.Get("AnalyticsConsent", false);
        // Phase 25b — Privacy-Center-Toggles
        PersonalizedAdsConsent = _preferences.Get("Privacy_PersonalizedAds", false);
        PushNotificationsConsent = _preferences.Get("Privacy_PushNotifications", true);
        ChildSafeMode = _preferences.Get("Privacy_ChildSafeMode", false);

        // Phase 23b — Premium-Tier-Status laden (Read-Display)
        HasBattlePassPlus = _battlePassPlus?.HasPlus ?? false;
        HasVipSubscription = _vipSubscription?.IsActive ?? false;
        var vipExp = _vipSubscription?.ExpiresAtUtc;
        VipExpiresAtText = vipExp.HasValue
            ? vipExp.Value.ToLocalTime().ToString("yyyy-MM-dd")
            : string.Empty;

        // Phase 30b/c — Co-Op-Modus aus Preferences laden
        CoOpModeEnabled = _preferences.Get("Multiplayer_CoOpEnabled", false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTY CHANGE HANDLERS - ACCESSIBILITY + PERFORMANCE (v2.0.44)
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnColorblindModeChanged(string value)
    {
        if (_isInitializing) return;
        _accessibilityService.ColorblindMode = value;
        // Welle 2 v2.0.58 : Accessibility-Funnel-Event.
        _analytics?.LogEvent(AnalyticsEvents.AccessibilityToggle, new Dictionary<string, object>
        {
            ["feature"] = "colorblind_mode",
            ["value"] = value ?? "Off",
        });
    }

    partial void OnHighContrastChanged(bool value)
    {
        if (_isInitializing) return;
        _accessibilityService.HighContrast = value;
        _analytics?.LogEvent(AnalyticsEvents.AccessibilityToggle, new Dictionary<string, object>
        {
            ["feature"] = "high_contrast",
            ["value"] = value ? 1 : 0,
        });
    }

    partial void OnUiScaleChanged(double value)
    {
        if (_isInitializing) return;
        _accessibilityService.UiScale = value;
        _analytics?.LogEvent(AnalyticsEvents.AccessibilityToggle, new Dictionary<string, object>
        {
            ["feature"] = "ui_scale",
            ["value"] = value,
        });
    }

    partial void OnSubtitlesEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _accessibilityService.SubtitlesEnabled = value;
        _analytics?.LogEvent(AnalyticsEvents.AccessibilityToggle, new Dictionary<string, object>
        {
            ["feature"] = "subtitles",
            ["value"] = value ? 1 : 0,
        });
    }

    partial void OnReducedFlashingChanged(bool value)
    {
        if (_isInitializing) return;
        _accessibilityService.ReducedFlashing = value;
        _analytics?.LogEvent(AnalyticsEvents.AccessibilityToggle, new Dictionary<string, object>
        {
            ["feature"] = "reduced_flashing",
            ["value"] = value ? 1 : 0,
        });
    }

    partial void OnUseHighFrameRateChanged(bool value)
    {
        if (_isInitializing) return;
        GameLoopSettings.SetTargetFps(
            value ? GameLoopSettings.FrameRate60 : GameLoopSettings.FrameRate30,
            _preferences);
    }

    // v2.0.55 — Phase 15 P0-Fix: DSGVO Consent-Toggles. Werden beim nächsten App-Start
    // von ITelemetryService/IAnalyticsService.Initialize() gelesen — keine sofortige
    // Aktivierung/Deaktivierung weil die Firebase-SDKs nur einmal pro Process initialisiert werden.
    partial void OnCrashlyticsConsentChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("CrashlyticsConsent", value);
    }

    partial void OnAnalyticsConsentChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("AnalyticsConsent", value);
    }

    // Phase 25b — Privacy-Center-Toggles persistieren
    partial void OnPersonalizedAdsConsentChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("Privacy_PersonalizedAds", value);
    }

    partial void OnPushNotificationsConsentChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("Privacy_PushNotifications", value);
    }

    partial void OnChildSafeModeChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("Privacy_ChildSafeMode", value);
        // ChildSafeMode aktiviert → automatisch Personalized-Ads aus
        if (value && PersonalizedAdsConsent)
        {
            PersonalizedAdsConsent = false;
        }
    }

    // Phase 30b/c — Co-Op-Modus persistieren
    partial void OnCoOpModeEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _preferences.Set("Multiplayer_CoOpEnabled", value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMAND - DSGVO ACCOUNT DELETION (v2.0.44)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (_accountDeletionService == null)
        {
            ShowAlert(
                _localizationService.GetString("Error") ?? "Error",
                "Account-Löschung nicht verfügbar.",
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        if (ConfirmationRequested == null) return;

        // v2.0.60 (B-C16): 2-Step-Confirm mit Daten-Export-Hint (DSGVO Art. 17 + 20).
        // Step 1: Hinweis auf Export-Möglichkeit + erste Confirm.
        bool confirmed1 = await ConfirmationRequested.Invoke(
            _localizationService.GetString("DeleteAccount") ?? "Konto löschen",
            _localizationService.GetString("DeleteAccountExportHint") ??
                "Tipp: Du kannst deine Daten vorher per \"Daten exportieren\" sichern.\n\n" +
                "Wenn du fortfährst, werden alle deine Spieldaten unwiderruflich gelöscht. Möchtest du fortfahren?",
            _localizationService.GetString("Continue") ?? "Weiter",
            _localizationService.GetString("Cancel") ?? "Abbrechen");

        if (!confirmed1) return;

        // Step 2: Endgültige Bestätigung — keine versehentliche Löschung.
        bool confirmed2 = await ConfirmationRequested.Invoke(
            _localizationService.GetString("DeleteAccountFinalTitle") ?? "Letzte Bestätigung",
            _localizationService.GetString("DeleteAccountFinalMessage") ??
                "Alle deine Spieldaten werden JETZT permanent gelöscht. Dieser Schritt kann nicht rückgängig gemacht werden.\n\nWirklich fortfahren?",
            _localizationService.GetString("DeletePermanently") ?? "Permanent löschen",
            _localizationService.GetString("Cancel") ?? "Abbrechen");

        if (!confirmed2) return;

        var result = await _accountDeletionService.DeleteAccountAsync();
        if (result.Success)
        {
            // v2.0.60: Funnel-Event fuer DSGVO-Compliance-Tracking (DSGVO erlaubt anonyme Aggregat-Statistik).
            _analytics?.LogEvent("account_deleted", new Dictionary<string, object>
            {
                ["success"] = 1,
            });
            ShowAlert(
                _localizationService.GetString("DeleteAccount") ?? "Konto gelöscht",
                _localizationService.GetString("DeleteAccountDone") ??
                    "Alle Daten wurden entfernt. Bitte starte die App neu.",
                _localizationService.GetString("OK") ?? "OK");
        }
        else
        {
            ShowAlert(
                _localizationService.GetString("DeleteAccount") ?? "Konto gelöscht",
                $"{_localizationService.GetString("DeleteAccountPartial") ?? "Lokale Daten gelöscht. Cloud-Daten konnten nicht erreicht werden."} {result.ErrorMessage}",
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    /// <summary>
    /// v2.0.60 (B-C16): DSGVO Art. 20 — Datenexport. Lädt alle Spielerdaten als
    /// human-readable Text in die Zwischenablage / nativen Share-Sheet.
    /// </summary>
    [RelayCommand]
    private async Task ExportDataAsync()
    {
        if (_dataExportService == null)
        {
            ShowAlert(
                _localizationService.GetString("Error") ?? "Error",
                _localizationService.GetString("DataExportUnavailable") ?? "Datenexport nicht verfügbar.",
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        try
        {
            var text = await _dataExportService.ExportAsHumanReadableAsync();
            // Plattformübergreifend: Android = Share-Sheet, Desktop = Clipboard-Copy.
            MeineApps.Core.Ava.Services.UriLauncher.ShareText(
                text,
                _localizationService.GetString("DataExportTitle") ?? "Meine BomberBlast-Daten");
            ShowAlert(
                _localizationService.GetString("DataExportTitle") ?? "Datenexport",
                _localizationService.GetString("DataExportSuccess") ??
                    "Deine Daten wurden bereitgestellt. Auf Android kannst du sie per Share-Sheet weiterleiten, auf Desktop wurden sie in die Zwischenablage kopiert.",
                _localizationService.GetString("OK") ?? "OK");
        }
        catch (Exception ex)
        {
            ShowAlert(
                _localizationService.GetString("Error") ?? "Error",
                $"{_localizationService.GetString("DataExportFailed") ?? "Export fehlgeschlagen"}: {ex.Message}",
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    /// <summary>
    /// Called when the view appears.
    /// </summary>
    public void OnAppearing()
    {
        IsPremium = _purchaseService.IsPremium;
        // Restore-Button: Bei Premium "Kauf validieren", sonst "Wiederherstellen"
        RestoreButtonText = IsPremium
            ? (_localizationService.GetString("ValidatePurchase") ?? "Validate Purchase")
            : (_localizationService.GetString("RestorePurchases") ?? "Restore Purchases");
        IsPlayGamesSignedIn = _playGames.IsSignedIn;
        PlayGamesPlayerName = _playGames.PlayerName ?? "";

        // Sprache erneut setzen (ComboBox zeigt sonst beim ersten Öffnen leer)
        _isInitializing = true;
        var currentLang = _localizationService.CurrentLanguage;
        SelectedLanguageOption = Languages.FirstOrDefault(l => l.Code == currentLang) ?? Languages[0];
        _isInitializing = false;

        // Cloud Save Status aktualisieren
        CloudSaveEnabled = _cloudSaveService.IsEnabled;
        UpdateCloudSyncStatus();
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

    partial void OnReducedEffectsChanged(bool value)
    {
        if (_isInitializing) return;

        _inputManager.ReducedEffects = value;
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

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (_isInitializing || value == null)
            return;

        _localizationService.SetLanguage(value.Code);
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
            OnPropertyChanged(nameof(IsRetroSelected));
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
    // PROPERTY CHANGE HANDLERS - PLAY GAMES
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnPlayGamesEnabledChanged(bool value)
    {
        if (_isInitializing) return;

        _playGames.IsEnabled = value;

        // Bei Aktivierung Sign-In versuchen
        if (value && !_playGames.IsSignedIn)
        {
            _ = TryPlayGamesSignInAsync();
        }
    }

    private async Task TryPlayGamesSignInAsync()
    {
        var result = await _playGames.SignInAsync();
        IsPlayGamesSignedIn = result;
        PlayGamesPlayerName = _playGames.PlayerName ?? "";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - PLAY GAMES
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ShowLeaderboardsAsync()
    {
        await _playGames.ShowLeaderboardsAsync();
    }

    [RelayCommand]
    private async Task ShowGpgsAchievementsAsync()
    {
        await _playGames.ShowAchievementsAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - CLOUD SAVE
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleCloudSave()
    {
        CloudSaveEnabled = !CloudSaveEnabled;
        _cloudSaveService.SetEnabled(CloudSaveEnabled);
        UpdateCloudSyncStatus();
    }

    [RelayCommand]
    private async Task SyncNow()
    {
        if (!_cloudSaveService.IsEnabled) return;
        IsCloudSyncing = true;
        try
        {
            await _cloudSaveService.ForceUploadAsync();
            UpdateCloudSyncStatus();
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    [RelayCommand]
    private async Task DownloadFromCloud()
    {
        if (!_cloudSaveService.IsEnabled) return;
        IsCloudSyncing = true;
        try
        {
            var success = await _cloudSaveService.ForceDownloadAsync();
            if (success)
            {
                AlertRequested?.Invoke(
                    _localizationService.GetString("CloudSaveDownloadSuccess") ?? "Cloud data loaded",
                    _localizationService.GetString("CloudSaveRestartHint") ?? "Restart app for full consistency",
                    _localizationService.GetString("OK"));
            }
        }
        finally
        {
            IsCloudSyncing = false;
        }
    }

    private void UpdateCloudSyncStatus()
    {
        // Speicherort anzeigen
        if (_playGames.IsSignedIn)
            CloudSaveLocation = _localizationService.GetString("CloudSaveLocationGPGS") ?? "Google Play Games";
        else
            CloudSaveLocation = _localizationService.GetString("CloudSaveLocationLocal") ?? "Local";

        if (!_cloudSaveService.IsEnabled)
        {
            CloudSyncStatus = _localizationService.GetString("CloudSaveDisabled") ?? "Disabled";
            return;
        }

        var lastSync = _cloudSaveService.LastSyncTimeUtc;
        if (string.IsNullOrEmpty(lastSync))
        {
            CloudSyncStatus = _localizationService.GetString("CloudSaveNeverSynced") ?? "Never synced";
            return;
        }

        if (DateTime.TryParse(lastSync, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var syncTime))
        {
            var ago = DateTime.UtcNow - syncTime;
            if (ago.TotalMinutes < 1)
                CloudSyncStatus = _localizationService.GetString("CloudSaveJustNow") ?? "Just now";
            else if (ago.TotalHours < 1)
                CloudSyncStatus = $"{(int)ago.TotalMinutes} min";
            else if (ago.TotalDays < 1)
                CloudSyncStatus = $"{(int)ago.TotalHours}h";
            else
                CloudSyncStatus = $"{(int)ago.TotalDays}d";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        MeineApps.Core.Ava.Services.UriLauncher.OpenUri(
            "https://rs-digital-studio.github.io/privacy/bomberblast.html");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
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
