using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using RechnerPlus.Services;

namespace RechnerPlus.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly IPreferencesService _preferences;
    private readonly IHapticService _haptic;

    private const string DecimalPlacesKey = "calculator_decimal_places";
    private const string NumberFormatKey = "calculator_number_format";
    private const string HapticEnabledKey = "calculator_haptic_enabled";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMidnightSelected))]
    [NotifyPropertyChangedFor(nameof(IsAuroraSelected))]
    [NotifyPropertyChangedFor(nameof(IsDaylightSelected))]
    [NotifyPropertyChangedFor(nameof(IsForestSelected))]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnglishSelected))]
    [NotifyPropertyChangedFor(nameof(IsGermanSelected))]
    [NotifyPropertyChangedFor(nameof(IsSpanishSelected))]
    [NotifyPropertyChangedFor(nameof(IsFrenchSelected))]
    [NotifyPropertyChangedFor(nameof(IsItalianSelected))]
    [NotifyPropertyChangedFor(nameof(IsPortugueseSelected))]
    private string _selectedLanguage;

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";
    public IReadOnlyList<AppTheme> AvailableThemes => _themeService.AvailableThemes;
    public IReadOnlyList<LanguageInfo> AvailableLanguages => _localization.AvailableLanguages;

    // Theme selection indicators
    public bool IsMidnightSelected => SelectedTheme == AppTheme.Midnight;
    public bool IsAuroraSelected => SelectedTheme == AppTheme.Aurora;
    public bool IsDaylightSelected => SelectedTheme == AppTheme.Daylight;
    public bool IsForestSelected => SelectedTheme == AppTheme.Forest;

    /// <summary>Dezimalstellen: -1 = Auto, 0-10 = feste Anzahl.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecimalPlacesDisplayText))]
    private int _decimalPlaces;

    public string DecimalPlacesDisplayText => DecimalPlaces < 0
        ? _localization.GetString("SettingsDecimalAuto")
        : DecimalPlaces.ToString();
    public string DecimalPlacesText => _localization.GetString("SettingsDecimalPlaces");

    /// <summary>Zahlenformat: 0 = US (1,234.56), 1 = EU (1.234,56).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUSFormat))]
    [NotifyPropertyChangedFor(nameof(IsEUFormat))]
    private int _numberFormat;

    public bool IsUSFormat => NumberFormat == 0;
    public bool IsEUFormat => NumberFormat == 1;
    public string NumberFormatText => _localization.GetString("SettingsNumberFormat");

    /// <summary>Haptic Feedback Ein/Aus (nur Android relevant).</summary>
    [ObservableProperty]
    private bool _hapticEnabled;

    public string HapticText => _localization.GetString("SettingsHaptic");

    // Language selection indicators
    public bool IsEnglishSelected => SelectedLanguage == "en";
    public bool IsGermanSelected => SelectedLanguage == "de";
    public bool IsSpanishSelected => SelectedLanguage == "es";
    public bool IsFrenchSelected => SelectedLanguage == "fr";
    public bool IsItalianSelected => SelectedLanguage == "it";
    public bool IsPortugueseSelected => SelectedLanguage == "pt";

    // Localized strings
    public string SettingsTitle => _localization.GetString("SettingsTitle");
    public string ChooseDesignText => _localization.GetString("SettingsChooseDesign");
    public string LanguageText => _localization.GetString("SettingsLanguage");
    public string AboutAppText => _localization.GetString("SettingsAboutApp");
    public string FeedbackText => _localization.GetString("FeedbackButton");
    public string PrivacyPolicyText => _localization.GetString("PrivacyPolicy");
    public string AppDescriptionText => _localization.GetString("AppDescription");
    public string VersionText => string.Format(_localization.GetString("VersionFormat"), AppVersion);

    // Theme name strings
    public string ThemeMidnightName => _localization.GetString("ThemeMidnight");
    public string ThemeMidnightDescText => _localization.GetString("ThemeMidnightDesc");
    public string ThemeAuroraName => _localization.GetString("ThemeAurora");
    public string ThemeAuroraDescText => _localization.GetString("ThemeAuroraDesc");
    public string ThemeDaylightName => _localization.GetString("ThemeDaylight");
    public string ThemeDaylightDescText => _localization.GetString("ThemeDaylightDesc");
    public string ThemeForestName => _localization.GetString("ThemeForest");
    public string ThemeForestDescText => _localization.GetString("ThemeForestDesc");

    public SettingsViewModel(IThemeService themeService, ILocalizationService localization,
                              IPreferencesService preferences, IHapticService haptic)
    {
        _themeService = themeService;
        _localization = localization;
        _preferences = preferences;
        _haptic = haptic;
        _selectedTheme = _themeService.CurrentTheme;
        _selectedLanguage = _localization.CurrentLanguage;
        _decimalPlaces = _preferences.Get(DecimalPlacesKey, -1);
        _numberFormat = _preferences.Get(NumberFormatKey, 0);
        _hapticEnabled = _preferences.Get(HapticEnabledKey, true);
        _haptic.IsEnabled = _hapticEnabled;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    partial void OnDecimalPlacesChanged(int value)
    {
        _preferences.Set(DecimalPlacesKey, value);
    }

    partial void OnNumberFormatChanged(int value)
    {
        _preferences.Set(NumberFormatKey, value);
    }

    partial void OnHapticEnabledChanged(bool value)
    {
        _preferences.Set(HapticEnabledKey, value);
        _haptic.IsEnabled = value;
    }

    [RelayCommand]
    private void ToggleHaptic()
    {
        HapticEnabled = !HapticEnabled;
    }

    [RelayCommand]
    private void SetNumberFormat(string format)
    {
        if (int.TryParse(format, out var value))
            NumberFormat = value;
    }

    [RelayCommand]
    private void IncrementDecimalPlaces()
    {
        if (DecimalPlaces < 10)
            DecimalPlaces++;
    }

    [RelayCommand]
    private void DecrementDecimalPlaces()
    {
        if (DecimalPlaces > -1)
            DecimalPlaces--;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _themeService.SetTheme(value);
    }

    [RelayCommand]
    private void SetTheme(AppTheme theme) => SelectedTheme = theme;

    [RelayCommand]
    private void SelectLanguage(string languageCode)
    {
        if (SelectedLanguage == languageCode) return;
        SelectedLanguage = languageCode;
        _localization.SetLanguage(languageCode);
    }

    [RelayCommand]
    private void SendFeedback()
    {
        UriLauncher.OpenUri("mailto:info@rs-digital.org?subject=RechnerPlus%20Feedback");
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        var url = _localization.GetString("PrivacyPolicyUrl");
        UriLauncher.OpenUri(url);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Refresh all localized string properties
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _localization.LanguageChanged -= OnLanguageChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
