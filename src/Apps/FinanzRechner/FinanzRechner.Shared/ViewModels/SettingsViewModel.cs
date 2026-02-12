using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace FinanzRechner.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IExpenseService _expenseService;
    private readonly IPreferencesService _preferencesService;

    public event Action<string, string>? MessageRequested;

    private const string PrivacyPolicyUrl = "https://sites.google.com/rs-digital.org/finanzrechner/startseite";

    public SettingsViewModel(
        IThemeService themeService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IExpenseService expenseService,
        IPreferencesService preferencesService)
    {
        _themeService = themeService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _expenseService = expenseService;
        _preferencesService = preferencesService;

        _selectedTheme = _themeService.CurrentTheme;
        _selectedLanguage = _localizationService.CurrentLanguage;
        _isPremium = _purchaseService.IsPremium;
    }

    #region Observable Properties

    [ObservableProperty]
    private AppTheme _selectedTheme;

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

    // Localized text properties
    public string SettingsTitleText => _localizationService.GetString("SettingsTitle") ?? "Settings";
    public string ChooseDesignText => _localizationService.GetString("SettingsChooseDesign") ?? "Choose Design";
    public string LanguageText => _localizationService.GetString("SettingsLanguage") ?? "Language";
    public string PremiumText => _localizationService.GetString("Premium") ?? "Premium";
    public string PremiumDescriptionText => _localizationService.GetString("PremiumDescription") ?? "Remove ads and support the developer";
    public string PurchasePremiumText => _localizationService.GetString("PurchasePremium") ?? "Purchase Premium";
    public string RestorePurchasesText => _localizationService.GetString("RestorePurchases") ?? "Restore Purchases";
    public string BackupRestoreText => _localizationService.GetString("BackupRestore") ?? "Backup & Restore";
    public string BackupRestoreDescText => _localizationService.GetString("BackupRestoreDesc") ?? "Export your data or restore from a backup";
    public string CreateBackupText => _localizationService.GetString("CreateBackup") ?? "Create Backup";
    public string RestoreBackupText => _localizationService.GetString("RestoreBackup") ?? "Restore";
    public string AboutAppText => _localizationService.GetString("SettingsAboutApp") ?? "About App";
    public string FeedbackText => _localizationService.GetString("FeedbackButton") ?? "Send Feedback";
    public string ThemeMidnightName => _localizationService.GetString("ThemeMidnight") ?? "Midnight";
    public string ThemeMidnightDescText => _localizationService.GetString("ThemeMidnightDesc") ?? "Dark indigo theme";
    public string ThemeAuroraName => _localizationService.GetString("ThemeAurora") ?? "Aurora";
    public string ThemeAuroraDescText => _localizationService.GetString("ThemeAuroraDesc") ?? "Dark pink gradient theme";
    public string ThemeDaylightName => _localizationService.GetString("ThemeDaylight") ?? "Daylight";
    public string ThemeDaylightDescText => _localizationService.GetString("ThemeDaylightDesc") ?? "Light blue theme";
    public string ThemeForestName => _localizationService.GetString("ThemeForest") ?? "Forest";
    public string ThemeForestDescText => _localizationService.GetString("ThemeForestDesc") ?? "Dark green theme";

    // Restore-Dialog Texte
    public string RestoreQuestionText => _localizationService.GetString("RestoreQuestion") ?? "How do you want to restore?";
    public string RestoreMergeText => _localizationService.GetString("RestoreMerge") ?? "Merge";
    public string RestoreReplaceText => _localizationService.GetString("RestoreReplace") ?? "Replace";
    public string RestoreMergeDescText => _localizationService.GetString("RestoreMergeDesc") ?? "Combines existing data with backup";
    public string RestoreReplaceDescText => _localizationService.GetString("RestoreReplaceDesc") ?? "Replaces all data with backup";
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";

    #endregion

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

        // Notify listeners about language change (views can subscribe)
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Event raised when language changes (for views to update tab titles etc.)
    /// </summary>
    public event Action? LanguageChanged;

    private void UpdateLanguageProperties()
    {
        OnPropertyChanged(nameof(IsEnglishSelected));
        OnPropertyChanged(nameof(IsGermanSelected));
        OnPropertyChanged(nameof(IsSpanishSelected));
        OnPropertyChanged(nameof(IsFrenchSelected));
        OnPropertyChanged(nameof(IsItalianSelected));
        OnPropertyChanged(nameof(IsPortugueseSelected));

        // Refresh all localized text properties
        OnPropertyChanged(nameof(SettingsTitleText));
        OnPropertyChanged(nameof(ChooseDesignText));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(PremiumText));
        OnPropertyChanged(nameof(PremiumDescriptionText));
        OnPropertyChanged(nameof(PurchasePremiumText));
        OnPropertyChanged(nameof(RestorePurchasesText));
        OnPropertyChanged(nameof(BackupRestoreText));
        OnPropertyChanged(nameof(BackupRestoreDescText));
        OnPropertyChanged(nameof(CreateBackupText));
        OnPropertyChanged(nameof(RestoreBackupText));
        OnPropertyChanged(nameof(AboutAppText));
        OnPropertyChanged(nameof(FeedbackText));
        OnPropertyChanged(nameof(ThemeMidnightName));
        OnPropertyChanged(nameof(ThemeMidnightDescText));
        OnPropertyChanged(nameof(ThemeAuroraName));
        OnPropertyChanged(nameof(ThemeAuroraDescText));
        OnPropertyChanged(nameof(ThemeDaylightName));
        OnPropertyChanged(nameof(ThemeDaylightDescText));
        OnPropertyChanged(nameof(ThemeForestName));
        OnPropertyChanged(nameof(ThemeForestDescText));
        OnPropertyChanged(nameof(RestoreQuestionText));
        OnPropertyChanged(nameof(RestoreMergeText));
        OnPropertyChanged(nameof(RestoreReplaceText));
        OnPropertyChanged(nameof(RestoreMergeDescText));
        OnPropertyChanged(nameof(RestoreReplaceDescText));
        OnPropertyChanged(nameof(CancelText));
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

    #region Premium

    [RelayCommand]
    private async Task PurchasePremium()
    {
        await _purchaseService.PurchaseRemoveAdsAsync();
        IsPremium = _purchaseService.IsPremium;
    }

    [RelayCommand]
    private async Task RestorePurchases()
    {
        await _purchaseService.RestorePurchasesAsync();
        IsPremium = _purchaseService.IsPremium;
    }

    #endregion

    #region Feedback

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        // Open privacy policy URL - will be handled by the view via event
        OpenUrlRequested?.Invoke(PrivacyPolicyUrl);
    }

    /// <summary>
    /// Event raised when a URL should be opened
    /// </summary>
    public event Action<string>? OpenUrlRequested;

    [RelayCommand]
    private void SendFeedback()
    {
        // Feedback will be handled by the view via event
        FeedbackRequested?.Invoke("FinanzRechner");
    }

    /// <summary>
    /// Event raised when feedback email should be opened
    /// </summary>
    public event Action<string>? FeedbackRequested;

    #endregion

    #region Backup & Restore

    [ObservableProperty]
    private bool _isBackupInProgress;

    [ObservableProperty]
    private bool _showRestoreConfirmation;

    [ObservableProperty]
    private string _restoreFilePath = string.Empty;

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (IsBackupInProgress) return;

        try
        {
            IsBackupInProgress = true;

            var json = await _expenseService.ExportToJsonAsync();
            var fileName = $"FinanzRechner_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);

            await File.WriteAllTextAsync(filePath, json);

            // Backup file ready for sharing

            // Notify view to handle file sharing/saving
            BackupCreated?.Invoke(filePath);
        }
        catch (Exception ex)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = $"{_localizationService.GetString("BackupError") ?? "Backup failed"}: {ex.Message}";
            MessageRequested?.Invoke(title, message);
        }
        finally
        {
            IsBackupInProgress = false;
        }
    }

    /// <summary>
    /// Event raised when a backup file was created (path to file)
    /// </summary>
    public event Action<string>? BackupCreated;

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsBackupInProgress) return;

        try
        {
            IsBackupInProgress = true;

            // View muss File-Picker oeffnen und ProcessRestoreFileAsync aufrufen
            if (RestoreFileRequested != null)
            {
                RestoreFileRequested.Invoke();
            }
            else
            {
                // Kein Handler registriert - sofort zuruecksetzen
                IsBackupInProgress = false;
            }
        }
        catch (Exception ex)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = $"{_localizationService.GetString("RestoreError") ?? "Restore failed"}: {ex.Message}";
            MessageRequested?.Invoke(title, message);
            IsBackupInProgress = false;
        }
    }

    /// <summary>
    /// Event raised when a file should be picked for restore
    /// </summary>
    public event Action? RestoreFileRequested;

    /// <summary>
    /// Wird von der View aufgerufen nachdem der User eine Datei ausgewaehlt hat.
    /// Zeigt den Merge/Replace-Dialog an.
    /// </summary>
    public void OnRestoreFileSelected(string filePath)
    {
        RestoreFilePath = filePath;
        ShowRestoreConfirmation = true;
    }

    [RelayCommand]
    private async Task RestoreMerge()
    {
        ShowRestoreConfirmation = false;
        await ProcessRestoreFileAsync(RestoreFilePath, merge: true);
    }

    [RelayCommand]
    private async Task RestoreReplace()
    {
        ShowRestoreConfirmation = false;
        await ProcessRestoreFileAsync(RestoreFilePath, merge: false);
    }

    [RelayCommand]
    private void CancelRestore()
    {
        ShowRestoreConfirmation = false;
        IsBackupInProgress = false;
    }

    /// <summary>
    /// Fuehrt den eigentlichen Restore aus (Merge oder Replace).
    /// </summary>
    private async Task ProcessRestoreFileAsync(string filePath, bool merge)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var count = await _expenseService.ImportFromJsonAsync(json, merge);

            var title = _localizationService.GetString("Success") ?? "Success";
            var message = string.Format(_localizationService.GetString("RestoreSuccess") ?? "{0} entries restored.", count);
            MessageRequested?.Invoke(title, message);
        }
        catch (Exception ex)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = $"{_localizationService.GetString("RestoreError") ?? "Restore failed"}: {ex.Message}";
            MessageRequested?.Invoke(title, message);
        }
        finally
        {
            IsBackupInProgress = false;
        }
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
    }

    #endregion
}
