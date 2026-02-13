using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerRechner.ViewModels;

/// <summary>
/// ViewModel for the settings page (theme, language, units, premium)
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IUnitConverterService _unitConverterService;

    /// <summary>
    /// Raised when the VM wants to navigate (e.g. go back)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when language changes (for views to refresh UI)
    /// </summary>
    public event Action? LanguageChanged;

    /// <summary>
    /// Raised when feedback email should be opened
    /// </summary>
    public event Action<string>? FeedbackRequested;

    /// <summary>
    /// Event for showing alerts/messages to the user (title, message)
    /// </summary>
    public event Action<string, string>? MessageRequested;

    public SettingsViewModel(
        IThemeService themeService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IUnitConverterService unitConverterService)
    {
        _themeService = themeService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _unitConverterService = unitConverterService;

        _selectedTheme = _themeService.CurrentTheme;
        _selectedLanguage = _localizationService.CurrentLanguage;
        _selectedUnitSystem = _unitConverterService.CurrentSystem;

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        UpdatePremiumStatus();
    }

    #region Observable Properties

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private UnitSystem _selectedUnitSystem;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isAdFree;

    public bool ShowPurchaseSection => !IsPremium;
    public bool ShowAdFreeInfo => IsPremium;

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3) ?? "2.0.0"}";

    public IReadOnlyList<LanguageInfo> AvailableLanguages => _localizationService.AvailableLanguages;

    #endregion

    #region Localized Text Properties

    public string SettingsTitleText => _localizationService.GetString("SettingsTitle") ?? "Settings";
    public string ChooseDesignText => _localizationService.GetString("SettingsChooseDesign") ?? "Choose Design";
    public string LanguageTitleText => _localizationService.GetString("SettingsLanguage") ?? "Language";
    public string UnitSystemTitleText => _localizationService.GetString("SettingsUnitSystem") ?? "Unit System";
    public string MetricText => _localizationService.GetString("UnitSystemMetric") ?? "Metric";
    public string ImperialText => _localizationService.GetString("UnitSystemImperial") ?? "Imperial";
    public string PremiumTitleText => _localizationService.GetString("SettingsPremium") ?? "Premium";
    public string RemoveAdsText => _localizationService.GetString("RemoveAds") ?? "Remove Ads";
    public string RemoveAdsDescText => _localizationService.GetString("RemoveAdsFullDesc") ?? "Remove all ads and enjoy the full experience";
    public string RestorePurchasesText => _localizationService.GetString("RestorePurchases") ?? "Restore Purchases";
    public string AdFreeTitleText => _localizationService.GetString("AdFreeTitle") ?? "Ad-Free";
    public string AdFreeConfirmationText => _localizationService.GetString("AdFreeConfirmation") ?? "You are enjoying the ad-free experience. Thank you!";
    public string AboutTitleText => _localizationService.GetString("SettingsAbout") ?? "About";
    public string SendFeedbackText => _localizationService.GetString("FeedbackButton") ?? "Send Feedback";
    public string ThemeMidnightName => _localizationService.GetString("ThemeMidnightName") ?? "Midnight";
    public string ThemeMidnightDesc => _localizationService.GetString("ThemeMidnightDesc") ?? "Modern dark";
    public string ThemeAuroraName => _localizationService.GetString("ThemeAuroraName") ?? "Aurora";
    public string ThemeAuroraDesc => _localizationService.GetString("ThemeAuroraDesc") ?? "Vibrant neon";
    public string ThemeDaylightName => _localizationService.GetString("ThemeDaylightName") ?? "Daylight";
    public string ThemeDaylightDesc => _localizationService.GetString("ThemeDaylightDesc") ?? "Clean light";
    public string ThemeForestName => _localizationService.GetString("ThemeForestName") ?? "Forest";
    public string ThemeForestDesc => _localizationService.GetString("ThemeForestDesc") ?? "Natural green";

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(SettingsTitleText));
        OnPropertyChanged(nameof(ChooseDesignText));
        OnPropertyChanged(nameof(LanguageTitleText));
        OnPropertyChanged(nameof(UnitSystemTitleText));
        OnPropertyChanged(nameof(MetricText));
        OnPropertyChanged(nameof(ImperialText));
        OnPropertyChanged(nameof(PremiumTitleText));
        OnPropertyChanged(nameof(RemoveAdsText));
        OnPropertyChanged(nameof(RemoveAdsDescText));
        OnPropertyChanged(nameof(RestorePurchasesText));
        OnPropertyChanged(nameof(AdFreeTitleText));
        OnPropertyChanged(nameof(AdFreeConfirmationText));
        OnPropertyChanged(nameof(AboutTitleText));
        OnPropertyChanged(nameof(SendFeedbackText));
        OnPropertyChanged(nameof(ThemeMidnightName));
        OnPropertyChanged(nameof(ThemeMidnightDesc));
        OnPropertyChanged(nameof(ThemeAuroraName));
        OnPropertyChanged(nameof(ThemeAuroraDesc));
        OnPropertyChanged(nameof(ThemeDaylightName));
        OnPropertyChanged(nameof(ThemeDaylightDesc));
        OnPropertyChanged(nameof(ThemeForestName));
        OnPropertyChanged(nameof(ThemeForestDesc));
    }

    #endregion

    #region Theme Selection

    public bool IsMidnightSelected => SelectedTheme == AppTheme.Midnight;
    public bool IsAuroraSelected => SelectedTheme == AppTheme.Aurora;
    public bool IsDaylightSelected => SelectedTheme == AppTheme.Daylight;
    public bool IsForestSelected => SelectedTheme == AppTheme.Forest;

    [RelayCommand]
    private void SelectTheme(string themeName)
    {
        var theme = themeName switch
        {
            "Midnight" => AppTheme.Midnight,
            "Aurora" => AppTheme.Aurora,
            "Daylight" => AppTheme.Daylight,
            "Forest" => AppTheme.Forest,
            _ => AppTheme.Midnight
        };

        SelectedTheme = theme;
        _themeService.SetTheme(theme);

        OnPropertyChanged(nameof(IsMidnightSelected));
        OnPropertyChanged(nameof(IsAuroraSelected));
        OnPropertyChanged(nameof(IsDaylightSelected));
        OnPropertyChanged(nameof(IsForestSelected));
    }

    #endregion

    #region Language Selection

    public bool IsEnglishSelected => SelectedLanguage == "en";
    public bool IsGermanSelected => SelectedLanguage == "de";
    public bool IsSpanishSelected => SelectedLanguage == "es";
    public bool IsFrenchSelected => SelectedLanguage == "fr";
    public bool IsItalianSelected => SelectedLanguage == "it";
    public bool IsPortugueseSelected => SelectedLanguage == "pt";

    [RelayCommand]
    private void SelectLanguage(string languageCode)
    {
        if (SelectedLanguage == languageCode) return;

        SelectedLanguage = languageCode;
        _localizationService.SetLanguage(languageCode);

        UpdateLanguageProperties();
        UpdateLocalizedTexts();

        // Notify listeners so views can refresh
        LanguageChanged?.Invoke();
    }

    private void UpdateLanguageProperties()
    {
        OnPropertyChanged(nameof(IsEnglishSelected));
        OnPropertyChanged(nameof(IsGermanSelected));
        OnPropertyChanged(nameof(IsSpanishSelected));
        OnPropertyChanged(nameof(IsFrenchSelected));
        OnPropertyChanged(nameof(IsItalianSelected));
        OnPropertyChanged(nameof(IsPortugueseSelected));
    }

    #endregion

    #region Unit System Selection

    public bool IsMetricSelected => SelectedUnitSystem == UnitSystem.Metric;
    public bool IsImperialSelected => SelectedUnitSystem == UnitSystem.Imperial;

    [RelayCommand]
    private void SelectUnitSystem(string systemName)
    {
        var system = systemName switch
        {
            "Metric" => UnitSystem.Metric,
            "Imperial" => UnitSystem.Imperial,
            _ => UnitSystem.Metric
        };

        if (SelectedUnitSystem == system) return;

        SelectedUnitSystem = system;
        _unitConverterService.CurrentSystem = system;

        OnPropertyChanged(nameof(IsMetricSelected));
        OnPropertyChanged(nameof(IsImperialSelected));

        var systemText = system == UnitSystem.Metric
            ? _localizationService.GetString("UnitSystemMetric")
            : _localizationService.GetString("UnitSystemImperial");

        MessageRequested?.Invoke(_localizationService.GetString("UnitSystemChanged"), string.Format(_localizationService.GetString("UnitSystemChangedMessage"), systemText));
    }

    #endregion

    #region Premium / Purchases

    [RelayCommand]
    private async Task PurchaseRemoveAds()
    {
        if (_purchaseService.IsPremium)
        {
            MessageRequested?.Invoke(_localizationService.GetString("AlreadyAdFree"), _localizationService.GetString("AlreadyAdFreeMessage"));
            return;
        }

        var success = await _purchaseService.PurchaseRemoveAdsAsync();
        if (success)
        {
            MessageRequested?.Invoke(_localizationService.GetString("PurchaseSuccessful"), _localizationService.GetString("RemoveAdsPurchaseSuccessMessage"));
            UpdatePremiumStatus();
        }
    }

    [RelayCommand]
    private async Task RestorePurchases()
    {
        var restored = await _purchaseService.RestorePurchasesAsync();

        if (restored)
        {
            MessageRequested?.Invoke(_localizationService.GetString("PurchasesRestored"), _localizationService.GetString("AdsRemovedRestoredMessage"));
            UpdatePremiumStatus();
        }
        else
        {
            MessageRequested?.Invoke(_localizationService.GetString("NoPurchasesFound"), _localizationService.GetString("NoPurchasesFoundMessage"));
        }
    }

    private void UpdatePremiumStatus()
    {
        IsPremium = _purchaseService.IsPremium;
        IsAdFree = _purchaseService.IsPremium;

        OnPropertyChanged(nameof(ShowPurchaseSection));
        OnPropertyChanged(nameof(ShowAdFreeInfo));
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePremiumStatus);
    }

    #endregion

    #region Feedback

    [RelayCommand]
    private void SendFeedback()
    {
        FeedbackRequested?.Invoke("HandwerkerRechner");
    }

    #endregion

    #region Navigation

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    #endregion

    #region Lifecycle

    public void Initialize()
    {
        SelectedTheme = _themeService.CurrentTheme;

        OnPropertyChanged(nameof(IsMidnightSelected));
        OnPropertyChanged(nameof(IsAuroraSelected));
        OnPropertyChanged(nameof(IsDaylightSelected));
        OnPropertyChanged(nameof(IsForestSelected));

        UpdatePremiumStatus();
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;

        _disposed = true;
    }
}
