using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;
    private readonly IPreferencesService _preferences;
    private readonly IPurchaseService _purchaseService;
    private readonly IHapticService _hapticService;
    private readonly IFitnessSoundService _soundService;
    private readonly IReminderService _reminderService;

    /// <summary>
    /// Raised when the VM wants to navigate (e.g. go back)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when the VM wants to show a message (title, message).
    /// </summary>
    public event Action<string, string>? MessageRequested;

    /// <summary>
    /// Raised when language changes (for views to refresh UI)
    /// </summary>
    public event Action? LanguageChanged;

    /// <summary>
    /// Raised when feedback email should be opened
    /// </summary>
    public event Action<string>? FeedbackRequested;

    /// <summary>
    /// Wird ausgelöst wenn sich Profildaten ändern (Größe, Alter, Geschlecht, Aktivitätslevel).
    /// </summary>
    public event Action? ProfileChanged;

    public SettingsViewModel(
        ILocalizationService localizationService,
        IPreferencesService preferences,
        IPurchaseService purchaseService,
        IHapticService hapticService,
        IFitnessSoundService soundService,
        IReminderService reminderService)
    {
        _localizationService = localizationService;
        _preferences = preferences;
        _purchaseService = purchaseService;
        _hapticService = hapticService;
        _soundService = soundService;
        _reminderService = reminderService;

        _selectedLanguage = _localizationService.CurrentLanguage;
        _isPremium = _purchaseService.IsPremium;

        // Profil-Daten laden
        _profileHeight = _preferences.Get(PreferenceKeys.ProfileHeight, 175.0);
        _profileAge = _preferences.Get(PreferenceKeys.ProfileAge, 30);
        _profileIsMale = _preferences.Get(PreferenceKeys.ProfileIsMale, true);
        _profileActivityLevel = _preferences.Get(PreferenceKeys.ProfileActivityLevel, 2);

        // Haptic/Sound-Status aus Service laden
        _isHapticEnabled = _hapticService.IsEnabled;
        _isSoundEnabled = _soundService.IsEnabled;

        // Reminder-Status laden
        _isWaterReminderEnabled = _reminderService.IsWaterReminderEnabled;
        _isWeightReminderEnabled = _reminderService.IsWeightReminderEnabled;
        _isEveningSummaryEnabled = _reminderService.IsEveningSummaryEnabled;

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;
    }

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private bool _isPremium;

    public bool IsNotPremium => !IsPremium;

    partial void OnIsPremiumChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotPremium));
    }

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";

    #region Language Selection

    public IReadOnlyList<LanguageInfo> AvailableLanguages => _localizationService.AvailableLanguages;

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

    #region Benutzerprofil

    [ObservableProperty]
    private double _profileHeight;

    [ObservableProperty]
    private int _profileAge;

    [ObservableProperty]
    private bool _profileIsMale;

    [ObservableProperty]
    private int _profileActivityLevel;

    /// <summary>Profil vollständig ausgefüllt (Größe und Alter gesetzt)?</summary>
    public bool HasProfile => ProfileHeight > 0 && ProfileAge > 0;

    partial void OnProfileHeightChanged(double value)
    {
        if (value >= 80 && value <= 250)
        {
            _preferences.Set(PreferenceKeys.ProfileHeight, value);
            ProfileChanged?.Invoke();
        }
    }

    partial void OnProfileAgeChanged(int value)
    {
        if (value >= 8 && value <= 120)
        {
            _preferences.Set(PreferenceKeys.ProfileAge, value);
            ProfileChanged?.Invoke();
        }
    }

    partial void OnProfileIsMaleChanged(bool value)
    {
        _preferences.Set(PreferenceKeys.ProfileIsMale, value);
        ProfileChanged?.Invoke();
    }

    partial void OnProfileActivityLevelChanged(int value)
    {
        if (value >= 0 && value <= 4)
        {
            _preferences.Set(PreferenceKeys.ProfileActivityLevel, value);
            ProfileChanged?.Invoke();
        }
    }

    [RelayCommand]
    private void SetGenderMale() => ProfileIsMale = true;

    [RelayCommand]
    private void SetGenderFemale() => ProfileIsMale = false;

    #endregion

    #region Haptic & Sound

    [ObservableProperty]
    private bool _isHapticEnabled;

    [ObservableProperty]
    private bool _isSoundEnabled;

    partial void OnIsHapticEnabledChanged(bool value)
    {
        _hapticService.IsEnabled = value;
    }

    partial void OnIsSoundEnabledChanged(bool value)
    {
        _soundService.IsEnabled = value;
    }

    #endregion

    #region Reminders

    [ObservableProperty]
    private bool _isWaterReminderEnabled;

    [ObservableProperty]
    private bool _isWeightReminderEnabled;

    [ObservableProperty]
    private bool _isEveningSummaryEnabled;

    partial void OnIsWaterReminderEnabledChanged(bool value)
    {
        _reminderService.IsWaterReminderEnabled = value;
        _reminderService.UpdateSchedule();
    }

    partial void OnIsWeightReminderEnabledChanged(bool value)
    {
        _reminderService.IsWeightReminderEnabled = value;
        _reminderService.UpdateSchedule();
    }

    partial void OnIsEveningSummaryEnabledChanged(bool value)
    {
        _reminderService.IsEveningSummaryEnabled = value;
        _reminderService.UpdateSchedule();
    }

    #endregion

    #region Premium / Purchases

    [RelayCommand]
    private async Task PurchasePremium()
    {
        var success = await _purchaseService.PurchaseRemoveAdsAsync();
        if (success)
        {
            IsPremium = _purchaseService.IsPremium;
            MessageRequested?.Invoke(
                _localizationService.GetString("AlertSuccess"),
                _localizationService.GetString("AlertPurchaseSuccess"));
        }
    }

    [RelayCommand]
    private async Task RestorePurchases()
    {
        var restored = await _purchaseService.RestorePurchasesAsync();
        if (restored)
        {
            IsPremium = _purchaseService.IsPremium;
            MessageRequested?.Invoke(
                _localizationService.GetString("AlertSuccess"),
                _localizationService.GetString("AlertPurchaseRestored"));
        }
        else
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("AlertError"),
                _localizationService.GetString("AlertNoPurchases"));
        }
    }

    #endregion

    #region Feedback

    [RelayCommand]
    private void SendFeedback()
    {
        FeedbackRequested?.Invoke("FitnessRechner");
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        try
        {
            MeineApps.Core.Ava.Services.UriLauncher.OpenUri("https://rs-digital-studio.github.io/privacy/fitnessrechner.html");
        }
        catch
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("AlertError") ?? "Error",
                _localizationService.GetString("BrowserError") ?? "Could not open browser.");
        }
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
        IsPremium = _purchaseService.IsPremium;
    }

    #endregion

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsPremium = _purchaseService.IsPremium;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
