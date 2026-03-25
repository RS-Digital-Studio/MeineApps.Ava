using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace FinanzRechner.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IExpenseService _expenseService;
    private readonly IPreferencesService _preferencesService;
    private readonly IAccountService _accountService;
    private readonly ISavingsGoalService _savingsGoalService;
    private readonly IDebtService _debtService;
    private readonly ICustomCategoryService _customCategoryService;

    public event Action<string, string>? MessageRequested;

    private const string PrivacyPolicyUrl = "https://rs-digital-studio.github.io/privacy/finanzrechner.html";

    public SettingsViewModel(
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IExpenseService expenseService,
        IPreferencesService preferencesService,
        IAccountService accountService,
        ISavingsGoalService savingsGoalService,
        IDebtService debtService,
        ICustomCategoryService customCategoryService)
    {
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _expenseService = expenseService;
        _preferencesService = preferencesService;
        _accountService = accountService;
        _savingsGoalService = savingsGoalService;
        _debtService = debtService;
        _customCategoryService = customCategoryService;

        _selectedLanguage = _localizationService.CurrentLanguage;
        _isPremium = _purchaseService.IsPremium;

        // Währung aus Preferences laden
        var savedCurrency = _preferencesService.Get("currency_code", "EUR");
        _selectedCurrencyIndex = CurrencyDisplayNames
            .ToList().FindIndex(c => c.StartsWith(savedCurrency));
        if (_selectedCurrencyIndex < 0) _selectedCurrencyIndex = 0;
    }

    #region Observable Properties

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private int _selectedCurrencyIndex;

    /// <summary>Währungs-Auswahlliste für ComboBox.</summary>
    public static IReadOnlyList<string> CurrencyDisplayNames { get; } =
        Models.CurrencySettings.Presets.Select(p => $"{p.CurrencyCode} ({p.CurrencySymbol})").ToList();

    partial void OnSelectedCurrencyIndexChanged(int value)
    {
        if (value < 0 || value >= Models.CurrencySettings.Presets.Count) return;
        var preset = Models.CurrencySettings.Presets[value];
        _preferencesService.Set("currency_code", preset.CurrencyCode);
        Helpers.CurrencyHelper.Configure(preset);
    }

    public bool IsNotPremium => !IsPremium;

    partial void OnIsPremiumChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotPremium));
    }

    public string AppVersion => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";

    // Localized text properties
    public string SettingsTitleText => _localizationService.GetString("SettingsTitle") ?? "Settings";
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
        OnPropertyChanged(nameof(RestoreQuestionText));
        OnPropertyChanged(nameof(RestoreMergeText));
        OnPropertyChanged(nameof(RestoreReplaceText));
        OnPropertyChanged(nameof(RestoreMergeDescText));
        OnPropertyChanged(nameof(RestoreReplaceDescText));
        OnPropertyChanged(nameof(CancelText));
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

            // Alle Services exportieren und in Container-JSON zusammenfassen
            var expenseJson = await _expenseService.ExportToJsonAsync();
            var accountsJson = await _accountService.ExportToJsonAsync();
            var goalsJson = await _savingsGoalService.ExportToJsonAsync();
            var debtsJson = await _debtService.ExportToJsonAsync();
            var categoriesJson = await _customCategoryService.ExportToJsonAsync();

            // Wrapper-Objekt: Erweitertes Backup-Format (rückwärtskompatibel)
            var fullBackup = new Dictionary<string, string>
            {
                ["version"] = "2.0",
                ["expenses"] = expenseJson,
                ["accounts"] = accountsJson,
                ["savings_goals"] = goalsJson,
                ["debts"] = debtsJson,
                ["custom_categories"] = categoriesJson
            };
            var json = JsonSerializer.Serialize(fullBackup, new JsonSerializerOptions { WriteIndented = true });

            var fileName = $"FinanzRechner_Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);

            await File.WriteAllTextAsync(filePath, json);

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
    /// Unterstuetzt sowohl das alte Format (nur Expenses) als auch das neue v2.0 Container-Format.
    /// </summary>
    private async Task ProcessRestoreFileAsync(string filePath, bool merge)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var totalCount = 0;

            // Prüfen ob es ein v2.0 Container-Backup ist (hat "version" Key)
            Dictionary<string, string>? container = null;
            try
            {
                container = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch { /* Kein Container-Format - altes Format */ }

            if (container != null && container.ContainsKey("version") && container.ContainsKey("expenses"))
            {
                // Neues v2.0 Format: Alle Services wiederherstellen
                if (container.TryGetValue("expenses", out var expensesJson))
                    totalCount += await _expenseService.ImportFromJsonAsync(expensesJson, merge);
                if (container.TryGetValue("accounts", out var accountsJson))
                    totalCount += await _accountService.ImportFromJsonAsync(accountsJson, merge);
                if (container.TryGetValue("savings_goals", out var goalsJson))
                    totalCount += await _savingsGoalService.ImportFromJsonAsync(goalsJson, merge);
                if (container.TryGetValue("debts", out var debtsJson))
                    totalCount += await _debtService.ImportFromJsonAsync(debtsJson, merge);
                if (container.TryGetValue("custom_categories", out var categoriesJson))
                    totalCount += await _customCategoryService.ImportFromJsonAsync(categoriesJson, merge);
            }
            else
            {
                // Altes Format (nur ExpenseService) - rückwärtskompatibel
                totalCount = await _expenseService.ImportFromJsonAsync(json, merge);
            }

            var title = _localizationService.GetString("Success") ?? "Success";
            var message = string.Format(_localizationService.GetString("RestoreSuccess") ?? "{0} entries restored.", totalCount);
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
    }

    #endregion
}
