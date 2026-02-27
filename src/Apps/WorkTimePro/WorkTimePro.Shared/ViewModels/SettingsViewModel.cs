using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for settings
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    public event Action<string, string>? MessageRequested;
    public event EventHandler? SettingsChanged;

    private readonly IDatabaseService _database;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly ITrialService _trialService;
    private readonly IPurchaseService _purchaseService;
    private readonly IReminderService _reminderService;

    private WorkSettings? _settings;
    private bool _disposed;
    private bool _isInitializing;
    private bool _workTimeSettingsChanged;
    private CancellationTokenSource? _autoSaveCts;
    private CancellationTokenSource? _reminderRescheduleCts;

    public SettingsViewModel(
        IDatabaseService database,
        IThemeService themeService,
        ILocalizationService localization,
        ITrialService trialService,
        IPurchaseService purchaseService,
        IReminderService reminderService)
    {
        _database = database;
        _themeService = themeService;
        _localization = localization;
        _trialService = trialService;
        _purchaseService = purchaseService;
        _reminderService = reminderService;

        _purchaseService.PremiumStatusChanged += OnPurchaseStatusChanged;
    }

    // === Work time settings ===

    [ObservableProperty]
    private double _dailyHours = 8.0;

    [ObservableProperty]
    private double _weeklyHours = 40.0;

    [ObservableProperty]
    private bool _mondayEnabled = true;

    [ObservableProperty]
    private bool _tuesdayEnabled = true;

    [ObservableProperty]
    private bool _wednesdayEnabled = true;

    [ObservableProperty]
    private bool _thursdayEnabled = true;

    [ObservableProperty]
    private bool _fridayEnabled = true;

    [ObservableProperty]
    private bool _saturdayEnabled = false;

    [ObservableProperty]
    private bool _sundayEnabled = false;

    // === Individual daily hours ===

    [ObservableProperty]
    private bool _useIndividualHours = false;

    [ObservableProperty]
    private double _mondayHours = 8.0;

    [ObservableProperty]
    private double _tuesdayHours = 8.0;

    [ObservableProperty]
    private double _wednesdayHours = 8.0;

    [ObservableProperty]
    private double _thursdayHours = 8.0;

    [ObservableProperty]
    private double _fridayHours = 8.0;

    [ObservableProperty]
    private double _saturdayHours = 0.0;

    [ObservableProperty]
    private double _sundayHours = 0.0;

    // === Zeitrundung ===

    [ObservableProperty]
    private int _roundingMinutes;

    /// <summary>
    /// Verfügbare Rundungsoptionen (0 = keine, 5/10/15/30 Minuten)
    /// </summary>
    public int[] RoundingOptions => [0, 5, 10, 15, 30];

    partial void OnRoundingMinutesChanged(int value) => ScheduleAutoSave();

    // === Stundenlohn ===

    [ObservableProperty]
    private double _hourlyRate;

    partial void OnHourlyRateChanged(double value)
    {
        if (value < 0) HourlyRate = 0;
        else ScheduleAutoSave();
    }

    // === Auto-Save mit Debounce (800ms) ===

    private void ScheduleAutoSave()
    {
        if (_isInitializing) return;

        _autoSaveCts?.Cancel();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800, token);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => SaveSettingsAsync());
            }
            catch (TaskCanceledException) { }
        });
    }

    /// <summary>
    /// Plant Reminder neu nach Settings-Änderung (nach Debounce)
    /// </summary>
    private void ScheduleReminderReschedule()
    {
        if (_isInitializing) return;

        _reminderRescheduleCts?.Cancel();
        _reminderRescheduleCts = new CancellationTokenSource();
        var token = _reminderRescheduleCts.Token;

        // Verzögert aufrufen damit Auto-Save zuerst durchläuft
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token); // Nach Auto-Save (800ms)
                await _reminderService.RescheduleAsync();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReminderReschedule Fehler: {ex.Message}");
            }
        });
    }

    // === Automatische Wochenstunden-Berechnung ===

    partial void OnUseIndividualHoursChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnMondayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnTuesdayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnWednesdayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnThursdayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnFridayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnSaturdayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnSundayHoursChanged(double value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnMondayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnTuesdayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnWednesdayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnThursdayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnFridayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnSaturdayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }
    partial void OnSundayEnabledChanged(bool value) { _workTimeSettingsChanged = true; RecalculateWeeklyHours(); ScheduleAutoSave(); }

    /// <summary>
    /// Berechnet WeeklyHours automatisch aus den individuellen Tagesstunden
    /// </summary>
    private void RecalculateWeeklyHours()
    {
        if (_isInitializing || !UseIndividualHours) return;

        double total = 0;
        if (MondayEnabled) total += MondayHours;
        if (TuesdayEnabled) total += TuesdayHours;
        if (WednesdayEnabled) total += WednesdayHours;
        if (ThursdayEnabled) total += ThursdayHours;
        if (FridayEnabled) total += FridayHours;
        if (SaturdayEnabled) total += SaturdayHours;
        if (SundayEnabled) total += SundayHours;

        WeeklyHours = Math.Round(total, 1);
    }

    // === Auto-Pause ===

    [ObservableProperty]
    private bool _autoPauseEnabled = true;

    [ObservableProperty]
    private double _autoPauseAfterHours = 6.0;

    [ObservableProperty]
    private int _autoPauseMinutes = 30;

    // === Reminders ===

    [ObservableProperty]
    private bool _morningReminderEnabled = true;

    [ObservableProperty]
    private TimeSpan _morningReminderTime = new(8, 0, 0);

    [ObservableProperty]
    private bool _eveningReminderEnabled = true;

    [ObservableProperty]
    private TimeSpan _eveningReminderTime = new(18, 0, 0);

    [ObservableProperty]
    private bool _pauseReminderEnabled = true;

    // === Overtime ===

    [ObservableProperty]
    private bool _overtimeWarningEnabled = true;

    [ObservableProperty]
    private double _overtimeWarningHours = 10.0;

    // === Vacation ===

    [ObservableProperty]
    private int _vacationDaysPerYear = 30;

    // === Holidays ===

    [ObservableProperty]
    private int _selectedRegionIndex = 1; // Bayern

    public string[] GermanRegions { get; } = new[]
    {
        "Baden-W\u00fcrttemberg", "Bayern", "Berlin", "Brandenburg", "Bremen",
        "Hamburg", "Hessen", "Mecklenburg-Vorpommern", "Niedersachsen",
        "Nordrhein-Westfalen", "Rheinland-Pfalz", "Saarland", "Sachsen",
        "Sachsen-Anhalt", "Schleswig-Holstein", "Th\u00fcringen"
    };

    // === Work time law ===

    [ObservableProperty]
    private bool _legalComplianceEnabled = true;

    // === Theme ===

    [ObservableProperty]
    private int _selectedThemeIndex;

    public bool IsMidnightSelected => SelectedThemeIndex == 0;
    public bool IsAuroraSelected => SelectedThemeIndex == 1;
    public bool IsDaylightSelected => SelectedThemeIndex == 2;
    public bool IsForestSelected => SelectedThemeIndex == 3;

    // Localized theme names and descriptions
    public string ThemeMidnightName => _localization.GetString("ThemeMidnight");
    public string ThemeMidnightDescText => _localization.GetString("ThemeMidnightDesc");
    public string ThemeAuroraName => _localization.GetString("ThemeAurora");
    public string ThemeAuroraDescText => _localization.GetString("ThemeAuroraDesc");
    public string ThemeDaylightName => _localization.GetString("ThemeDaylight");
    public string ThemeDaylightDescText => _localization.GetString("ThemeDaylightDesc");
    public string ThemeForestName => _localization.GetString("ThemeForest");
    public string ThemeForestDescText => _localization.GetString("ThemeForestDesc");

    partial void OnSelectedThemeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsMidnightSelected));
        OnPropertyChanged(nameof(IsAuroraSelected));
        OnPropertyChanged(nameof(IsDaylightSelected));
        OnPropertyChanged(nameof(IsForestSelected));

        // Theme sofort anwenden
        if (!_isInitializing)
            _themeService.SetTheme((AppTheme)value);

        ScheduleAutoSave();
    }

    [RelayCommand]
    private void SelectTheme(string themeName)
    {
        SelectedThemeIndex = themeName switch
        {
            "Midnight" => 0,
            "Aurora" => 1,
            "Daylight" => 2,
            "Forest" => 3,
            _ => 0
        };
    }

    // === Language ===

    [ObservableProperty]
    private int _selectedLanguageIndex;

    public bool IsGermanSelected => SelectedLanguageIndex == 0;
    public bool IsEnglishSelected => SelectedLanguageIndex == 1;
    public bool IsSpanishSelected => SelectedLanguageIndex == 2;
    public bool IsFrenchSelected => SelectedLanguageIndex == 3;
    public bool IsItalianSelected => SelectedLanguageIndex == 4;
    public bool IsPortugueseSelected => SelectedLanguageIndex == 5;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGermanSelected));
        OnPropertyChanged(nameof(IsEnglishSelected));
        OnPropertyChanged(nameof(IsSpanishSelected));
        OnPropertyChanged(nameof(IsFrenchSelected));
        OnPropertyChanged(nameof(IsItalianSelected));
        OnPropertyChanged(nameof(IsPortugueseSelected));
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void SelectLanguage(string langCode)
    {
        SelectedLanguageIndex = langCode switch
        {
            "de" => 0,
            "en" => 1,
            "es" => 2,
            "fr" => 3,
            "it" => 4,
            "pt" => 5,
            _ => 0
        };
    }

    // === Premium ===

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isInTrial;

    [ObservableProperty]
    private int _trialDaysLeft;

    [ObservableProperty]
    private string _premiumStatusText = "";

    [ObservableProperty]
    private string _premiumStatusColor = "#9E9E9E";

    [ObservableProperty]
    private double _trialProgress;

    [ObservableProperty]
    private string _trialProgressText = "";

    [ObservableProperty]
    private bool _isLoading;

    // Localized texts
    public string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"WorkTime Pro v{version?.Major ?? 2}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        }
    }

    public string BuyPremiumButtonText => $"{Icons.Rocket} {AppStrings.BuyPremium}";

    // Reminder time displays
    public string MorningReminderTimeDisplay => MorningReminderTime.ToString(@"hh\:mm");
    public string EveningReminderTimeDisplay => EveningReminderTime.ToString(@"hh\:mm");

    partial void OnAutoPauseEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnMorningReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); }
    partial void OnEveningReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); }
    partial void OnPauseReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); }
    partial void OnOvertimeWarningEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); }
    partial void OnLegalComplianceEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnSelectedRegionIndexChanged(int value) => ScheduleAutoSave();

    partial void OnMorningReminderTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(MorningReminderTimeDisplay));
        ScheduleAutoSave();
        ScheduleReminderReschedule();
    }

    partial void OnEveningReminderTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(EveningReminderTimeDisplay));
        ScheduleAutoSave();
        ScheduleReminderReschedule();
    }

    // === Input Validation ===

    partial void OnDailyHoursChanged(double value)
    {
        if (value < 0) DailyHours = 0;
        else if (value > 24) DailyHours = 24;
        else { _workTimeSettingsChanged = true; ScheduleAutoSave(); }
    }

    partial void OnWeeklyHoursChanged(double value)
    {
        if (value < 0) WeeklyHours = 0;
        else if (value > 168) WeeklyHours = 168;
        else { _workTimeSettingsChanged = true; ScheduleAutoSave(); }
    }

    partial void OnAutoPauseMinutesChanged(int value)
    {
        if (value < 0) AutoPauseMinutes = 0;
        else if (value > 120) AutoPauseMinutes = 120;
        else ScheduleAutoSave();
    }

    partial void OnAutoPauseAfterHoursChanged(double value)
    {
        if (value < 0) AutoPauseAfterHours = 0;
        else if (value > 24) AutoPauseAfterHours = 24;
        else ScheduleAutoSave();
    }

    partial void OnVacationDaysPerYearChanged(int value)
    {
        if (value < 0) VacationDaysPerYear = 0;
        else if (value > 365) VacationDaysPerYear = 365;
        else ScheduleAutoSave();
    }

    partial void OnOvertimeWarningHoursChanged(double value)
    {
        if (value < 0) OvertimeWarningHours = 0;
        else if (value > 100) OvertimeWarningHours = 100;
        else ScheduleAutoSave();
    }

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            _isInitializing = true;

            _settings = await _database.GetSettingsAsync();

            // Work time
            DailyHours = _settings.DailyHours;
            WeeklyHours = _settings.WeeklyHours;

            var workDays = _settings.WorkDaysArray;
            MondayEnabled = workDays.Contains(1);
            TuesdayEnabled = workDays.Contains(2);
            WednesdayEnabled = workDays.Contains(3);
            ThursdayEnabled = workDays.Contains(4);
            FridayEnabled = workDays.Contains(5);
            SaturdayEnabled = workDays.Contains(6);
            SundayEnabled = workDays.Contains(7);

            // Individual hours
            UseIndividualHours = !string.IsNullOrEmpty(_settings.DailyHoursPerDay);
            MondayHours = _settings.GetHoursForDay(1);
            TuesdayHours = _settings.GetHoursForDay(2);
            WednesdayHours = _settings.GetHoursForDay(3);
            ThursdayHours = _settings.GetHoursForDay(4);
            FridayHours = _settings.GetHoursForDay(5);
            SaturdayHours = _settings.GetHoursForDay(6);
            SundayHours = _settings.GetHoursForDay(7);

            // Zeitrundung
            RoundingMinutes = _settings.RoundingMinutes;

            // Stundenlohn
            HourlyRate = _settings.HourlyRate;

            // Vacation
            VacationDaysPerYear = _settings.VacationDaysPerYear;

            // Auto-Pause
            AutoPauseEnabled = _settings.AutoPauseEnabled;
            AutoPauseAfterHours = _settings.AutoPauseAfterHours;
            AutoPauseMinutes = _settings.AutoPauseMinutes;

            // Reminders
            MorningReminderEnabled = _settings.MorningReminderEnabled;
            MorningReminderTime = _settings.MorningReminderTime.ToTimeSpan();
            EveningReminderEnabled = _settings.EveningReminderEnabled;
            EveningReminderTime = _settings.EveningReminderTime.ToTimeSpan();
            PauseReminderEnabled = _settings.PauseReminderEnabled;

            // Overtime
            OvertimeWarningEnabled = _settings.OvertimeWarningEnabled;
            OvertimeWarningHours = _settings.OvertimeWarningHours;

            // Holidays
            var regionCode = _settings.HolidayRegion.Replace("DE-", "");
            var regionIndex = Array.IndexOf(Enum.GetNames<GermanState>(), regionCode);
            SelectedRegionIndex = regionIndex >= 0 ? regionIndex : 1;

            // Work time law
            LegalComplianceEnabled = _settings.LegalComplianceEnabled;

            // Theme
            SelectedThemeIndex = (int)_themeService.CurrentTheme;

            // Premium
            UpdatePremiumStatus();

            _isInitializing = false;
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_settings == null) return;

        try
        {
            // Work time
            _settings.DailyHours = DailyHours;
            _settings.WeeklyHours = WeeklyHours;

            var workDays = new List<int>();
            if (MondayEnabled) workDays.Add(1);
            if (TuesdayEnabled) workDays.Add(2);
            if (WednesdayEnabled) workDays.Add(3);
            if (ThursdayEnabled) workDays.Add(4);
            if (FridayEnabled) workDays.Add(5);
            if (SaturdayEnabled) workDays.Add(6);
            if (SundayEnabled) workDays.Add(7);
            _settings.WorkDays = string.Join(",", workDays);

            // Individual hours
            if (UseIndividualHours)
            {
                _settings.SetHoursForDay(1, MondayHours);
                _settings.SetHoursForDay(2, TuesdayHours);
                _settings.SetHoursForDay(3, WednesdayHours);
                _settings.SetHoursForDay(4, ThursdayHours);
                _settings.SetHoursForDay(5, FridayHours);
                _settings.SetHoursForDay(6, SaturdayHours);
                _settings.SetHoursForDay(7, SundayHours);
            }
            else
            {
                _settings.DailyHoursPerDay = "";
            }

            // Zeitrundung
            _settings.RoundingMinutes = RoundingMinutes;

            // Stundenlohn
            _settings.HourlyRate = HourlyRate;

            // Vacation
            _settings.VacationDaysPerYear = VacationDaysPerYear;

            // Auto-Pause
            _settings.AutoPauseEnabled = AutoPauseEnabled;
            _settings.AutoPauseAfterHours = AutoPauseAfterHours;
            _settings.AutoPauseMinutes = AutoPauseMinutes;

            // Reminders
            _settings.MorningReminderEnabled = MorningReminderEnabled;
            _settings.MorningReminderTime = TimeOnly.FromTimeSpan(MorningReminderTime);
            _settings.EveningReminderEnabled = EveningReminderEnabled;
            _settings.EveningReminderTime = TimeOnly.FromTimeSpan(EveningReminderTime);
            _settings.PauseReminderEnabled = PauseReminderEnabled;

            // Overtime
            _settings.OvertimeWarningEnabled = OvertimeWarningEnabled;
            _settings.OvertimeWarningHours = OvertimeWarningHours;

            // Holidays
            var stateNames = Enum.GetNames<GermanState>();
            var regionIdx = Math.Clamp(SelectedRegionIndex, 0, stateNames.Length - 1);
            var stateName = stateNames[regionIdx];
            _settings.HolidayRegion = $"DE-{stateName}";

            // Work time law
            _settings.LegalComplianceEnabled = LegalComplianceEnabled;

            await _database.SaveSettingsAsync(_settings);

            // Andere Tabs über Änderungen informieren
            SettingsChanged?.Invoke(this, EventArgs.Empty);

            // Warnung bei Arbeitszeit-relevanten Änderungen wenn bestehende Daten vorhanden
            if (_workTimeSettingsChanged)
            {
                _workTimeSettingsChanged = false;
                await ShowWorkTimeSettingsWarningAsync();
            }
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorSaving, ex.Message));
        }
    }

    /// <summary>
    /// Zeigt Warnung wenn bestehende WorkDays von der Settings-Änderung betroffen sein könnten
    /// </summary>
    private async Task ShowWorkTimeSettingsWarningAsync()
    {
        try
        {
            var today = DateTime.Today;
            var futureWorkDays = await _database.GetWorkDaysAsync(today, today.AddDays(30));
            var withData = futureWorkDays.Count(w => w.ActualWorkMinutes > 0);
            if (withData > 0)
            {
                MessageRequested?.Invoke(AppStrings.Info,
                    string.Format(AppStrings.SettingsChangedWarning, withData));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings-Warnung Fehler: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PurchasePremiumAsync()
    {
        try
        {
            // If trial is active, go directly to purchase
            // If trial not started and offer not seen, show dialog
            if (!_trialService.IsTrialActive && !_trialService.HasSeenTrialOffer && !_trialService.IsTrialStarted)
            {
                // Start trial by default in Avalonia (no action sheet available)
                _trialService.MarkTrialOfferAsSeen();
                _trialService.StartTrial();
                MessageRequested?.Invoke(AppStrings.Info, string.Format(AppStrings.TrialStartedMessage, _trialService.DaysRemaining));
                UpdatePremiumStatus();
                return;
            }

            IsLoading = true;
            bool success = await _purchaseService.PurchaseLifetimeAsync();

            if (success)
            {
                MessageRequested?.Invoke(AppStrings.Info, AppStrings.PurchaseSuccess);
            }

            UpdatePremiumStatus();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        try
        {
            IsLoading = true;
            await _purchaseService.RestorePurchasesAsync();
            UpdatePremiumStatus();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private const string ARBZG_URL = "https://www.gesetze-im-internet.de/arbzg/";
    private const string HOLIDAYS_URL = "https://www.bmi.bund.de/DE/themen/verfassung/staatliche-symbole/nationale-feiertage/nationale-feiertage-node.html";
    private const string PRIVACY_POLICY_URL = "https://rs-digital-studio.github.io/privacy/worktimepro.html";

    [RelayCommand]
    private void OpenArbZG()
    {
        UriLauncher.OpenUri(ARBZG_URL);
    }

    [RelayCommand]
    private void OpenHolidaysSource()
    {
        UriLauncher.OpenUri(HOLIDAYS_URL);
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        UriLauncher.OpenUri(PRIVACY_POLICY_URL);
    }

    // === Helper methods ===

    private void OnPurchaseStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePremiumStatus);
    }

    private void UpdatePremiumStatus()
    {
        IsPremium = _purchaseService.IsPremium;
        IsInTrial = _trialService.IsTrialActive;
        TrialDaysLeft = _trialService.DaysRemaining;

        if (_purchaseService.HasLifetime)
        {
            // Lifetime Premium
            PremiumStatusText = AppStrings.HasLifetime;
            PremiumStatusColor = "#4CAF50"; // Green
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (_purchaseService.HasActiveSubscription)
        {
            // Active subscription
            PremiumStatusText = AppStrings.HasSubscription;
            PremiumStatusColor = "#4CAF50"; // Green
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (IsPremium)
        {
            // Legacy Premium (remove_ads)
            PremiumStatusText = AppStrings.PremiumActive;
            PremiumStatusColor = "#4CAF50"; // Green
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (IsInTrial)
        {
            PremiumStatusText = string.Format(AppStrings.TrialDaysLeft, TrialDaysLeft);
            PremiumStatusColor = "#FF9800"; // Orange
            TrialProgress = TrialDaysLeft / 7.0;
            TrialProgressText = $"{TrialDaysLeft} / 7";
        }
        else if (_trialService.IsTrialExpired)
        {
            // Trial was started and expired
            PremiumStatusText = AppStrings.FreeVersion;
            PremiumStatusColor = "#F44336"; // Red - expired
            TrialProgress = 0;
            TrialProgressText = "";
        }
        else
        {
            // Trial was never started
            PremiumStatusText = AppStrings.FreeVersion;
            PremiumStatusColor = "#9E9E9E"; // Gray
            TrialProgress = 0;
            TrialProgressText = "";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _purchaseService.PremiumStatusChanged -= OnPurchaseStatusChanged;
        GC.SuppressFinalize(this);
    }
}
