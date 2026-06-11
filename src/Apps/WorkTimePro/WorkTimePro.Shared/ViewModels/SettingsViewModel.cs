using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for settings
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, IMessageSource, IDisposable
{
    public event Action<string, string>? MessageRequested;
    /// <summary>
    /// Bool-Argument: true wenn ein arbeitszeit-relevantes Setting geändert wurde
    /// (DailyHours/WeeklyHours/Wochentage/AutoPause/HolidayRegion).
    /// MainViewModel reloadet den aktiven Tab nur in diesem Fall — kosmetische
    /// Settings (HourlyRate, Reminder-Zeiten) brauchen keinen Daten-Reload.
    /// </summary>
    public event EventHandler<bool>? SettingsChanged;

    private readonly IDatabaseService _database;
    private readonly ILocalizationService _localization;
    private readonly ITrialService _trialService;
    private readonly IPurchaseService _purchaseService;
    private readonly IReminderService _reminderService;
    private readonly IBackupService _backupService;
    private readonly IHolidayService _holidayService;
    private readonly INotificationService _notificationService;

    private WorkSettings? _settings;
    private bool _disposed;
    private bool _isInitializing;
    private bool _workTimeSettingsChanged;
    private CancellationTokenSource? _autoSaveCts;
    private CancellationTokenSource? _reminderRescheduleCts;

    // Region-Codes parallel zum Display-Array (für Speicherung)
    private string[] _regionCodes = Array.Empty<string>();

    public SettingsViewModel(
        IDatabaseService database,
        ILocalizationService localization,
        ITrialService trialService,
        IPurchaseService purchaseService,
        IReminderService reminderService,
        IBackupService backupService,
        IHolidayService holidayService,
        INotificationService notificationService)
    {
        _database = database;
        _localization = localization;
        _trialService = trialService;
        _purchaseService = purchaseService;
        _reminderService = reminderService;
        _backupService = backupService;
        _holidayService = holidayService;
        _notificationService = notificationService;

        // Regionen aus HolidayService laden (DE + AT + CH)
        var regions = _holidayService.GetAvailableRegions();
        _regionCodes = regions.Select(r => r.Code).ToArray();
        HolidayRegions = regions.Select(r => r.Name).ToArray();

        _purchaseService.PremiumStatusChanged += OnPurchaseStatusChanged;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// VM-komponierte (nicht via {loc:Translate} gebundene) Texte bei Sprachwechsel auffrischen.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BuyPremiumButtonText));
        UpdatePremiumStatus();
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
                // InvokeAsync<Task> awaiten damit der innere Save-Task wirklich abgeschlossen wird.
                // Vorher wurde das Lambda-Task verworfen → Exceptions geschluckt + Race-Conditions möglich.
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await SaveSettingsAsync());
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsViewModel.ScheduleAutoSave Fehler: {ex.Message}");
            }
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

    /// <summary>
    /// Gemeinsamer Handler für alle Arbeitszeit-Settings (vorher: 15× identische Boilerplate-Zeile).
    /// </summary>
    private void NotifyWorkTimeChanged()
    {
        _workTimeSettingsChanged = true;
        RecalculateWeeklyHours();
        ScheduleAutoSave();
    }

    partial void OnUseIndividualHoursChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnMondayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnTuesdayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnWednesdayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnThursdayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnFridayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnSaturdayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnSundayHoursChanged(double value) => NotifyWorkTimeChanged();
    partial void OnMondayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnTuesdayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnWednesdayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnThursdayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnFridayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnSaturdayEnabledChanged(bool value) => NotifyWorkTimeChanged();
    partial void OnSundayEnabledChanged(bool value) => NotifyWorkTimeChanged();

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

    /// <summary>Resturlaub-Übertrag verfällt zum Stichtag (BUrlG: regulär 31.03.).</summary>
    [ObservableProperty]
    private bool _vacationCarryOverExpires = true;

    /// <summary>Maximaler Resturlaub-Übertrag in Tagen (0 = unbegrenzt).</summary>
    [ObservableProperty]
    private int _vacationMaxCarryOverDays;

    partial void OnVacationCarryOverExpiresChanged(bool value) => ScheduleAutoSave();

    partial void OnVacationMaxCarryOverDaysChanged(int value)
    {
        if (value < 0) VacationMaxCarryOverDays = 0;
        else if (value > 365) VacationMaxCarryOverDays = 365;
        else ScheduleAutoSave();
    }

    // === Holidays ===

    [ObservableProperty]
    private int _selectedRegionIndex = 1; // Bayern

    /// <summary>
    /// Verfügbare Feiertags-Regionen (DE + AT + CH, geladen aus HolidayService)
    /// </summary>
    public string[] HolidayRegions { get; private set; } = Array.Empty<string>();

    // === Work time law ===

    [ObservableProperty]
    private bool _legalComplianceEnabled = true;

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
        // Sprache tatsächlich umschalten + persistieren (LocalizationService speichert selbst).
        _localization.SetLanguage(langCode);
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
    private string _premiumStatusColor = AppColors.PremiumFree;

    [ObservableProperty]
    private double _trialProgress;

    [ObservableProperty]
    private string _trialProgressText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPurchaseOptionsVisible;

    /// <summary>Trial-Option im Kauf-Overlay nur anbieten, solange noch nie ein Trial lief.</summary>
    [ObservableProperty]
    private bool _canStartTrial;

    // Localized texts
    public string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"WorkTime Pro v{version?.Major ?? 2}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        }
    }

    public string BuyPremiumButtonText => AppStrings.BuyPremium;

    partial void OnAutoPauseEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnMorningReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); if (value) WarnIfNotificationsDisabled(); }
    partial void OnEveningReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); if (value) WarnIfNotificationsDisabled(); }
    partial void OnPauseReminderEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); if (value) WarnIfNotificationsDisabled(); }
    partial void OnOvertimeWarningEnabledChanged(bool value) { ScheduleAutoSave(); ScheduleReminderReschedule(); if (value) WarnIfNotificationsDisabled(); }

    /// <summary>
    /// Hinweis, wenn ein Reminder eingeschaltet wird, System-Benachrichtigungen aber deaktiviert
    /// sind (sonst würden Erinnerungen still verschluckt). Nutzt das verdrahtete MessageRequested.
    /// </summary>
    private void WarnIfNotificationsDisabled()
    {
        if (_isInitializing) return;
        if (!_notificationService.AreNotificationsEnabled())
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.ReminderNotificationsOff);
    }
    partial void OnLegalComplianceEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnSelectedRegionIndexChanged(int value) => ScheduleAutoSave();

    partial void OnMorningReminderTimeChanged(TimeSpan value)
    {
        ScheduleAutoSave();
        ScheduleReminderReschedule();
    }

    partial void OnEveningReminderTimeChanged(TimeSpan value)
    {
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
            VacationCarryOverExpires = _settings.VacationCarryOverExpires;
            VacationMaxCarryOverDays = _settings.VacationMaxCarryOverDays;

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

            // Holidays - Region-Code im Array finden
            var regionIndex = Array.IndexOf(_regionCodes, _settings.HolidayRegion);
            SelectedRegionIndex = regionIndex >= 0 ? regionIndex : 1;

            // Work time law
            LegalComplianceEnabled = _settings.LegalComplianceEnabled;

            // Language - aktuellen Sprachindex aus dem LocalizationService ableiten
            // (während _isInitializing löst das keinen AutoSave aus).
            SelectedLanguageIndex = _localization.CurrentLanguage switch
            {
                "en" => 1,
                "es" => 2,
                "fr" => 3,
                "it" => 4,
                "pt" => 5,
                _ => 0
            };

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
            _settings.VacationCarryOverExpires = VacationCarryOverExpires;
            _settings.VacationMaxCarryOverDays = VacationMaxCarryOverDays;

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

            // Holidays - Region-Code direkt aus Array
            var regionIdx = Math.Clamp(SelectedRegionIndex, 0, _regionCodes.Length - 1);
            _settings.HolidayRegion = _regionCodes[regionIdx];

            // Work time law
            _settings.LegalComplianceEnabled = LegalComplianceEnabled;

            await _database.SaveSettingsAsync(_settings);

            // Flag VOR den folgenden awaits lesen UND zurücksetzen — setzt der Nutzer
            // währenddessen erneut ein arbeitszeit-relevantes Feld, geht dessen
            // Flag-Setzung sonst durch das späte Zurücksetzen verloren.
            var requiresReload = _workTimeSettingsChanged;
            _workTimeSettingsChanged = false;

            // Andere Tabs über Änderungen informieren — Bool signalisiert ob ein
            // Daten-Reload nötig ist (nur bei arbeitszeit-relevanten Settings).
            SettingsChanged?.Invoke(this, requiresReload);

            // Warnung bei Arbeitszeit-relevanten Änderungen wenn bestehende Daten vorhanden
            if (requiresReload)
            {
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
    private void PurchasePremium()
    {
        // Kauf-Optionen-Overlay öffnen: Trial (falls noch nie gestartet), Monatsabo
        // oder Lifetime. Kein stiller Trial-Start mehr — die Kauf-Intention des
        // Nutzers wird nicht umgeleitet.
        CanStartTrial = !_trialService.IsTrialStarted;
        IsPurchaseOptionsVisible = true;
    }

    [RelayCommand]
    private void StartTrial()
    {
        IsPurchaseOptionsVisible = false;
        _trialService.MarkTrialOfferAsSeen();
        _trialService.StartTrial();
        MessageRequested?.Invoke(AppStrings.Info, string.Format(AppStrings.TrialStartedMessage, _trialService.DaysRemaining));
        UpdatePremiumStatus();
    }

    [RelayCommand]
    private async Task PurchaseMonthlyAsync() => await PurchaseAsync(_purchaseService.PurchaseMonthlyAsync);

    [RelayCommand]
    private async Task PurchaseLifetimeAsync() => await PurchaseAsync(_purchaseService.PurchaseLifetimeAsync);

    [RelayCommand]
    private void CancelPurchaseOptions() => IsPurchaseOptionsVisible = false;

    private async Task PurchaseAsync(Func<Task<bool>> purchase)
    {
        try
        {
            IsPurchaseOptionsVisible = false;
            IsLoading = true;
            bool success = await purchase();

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

    // === Backup Export/Import ===

    // Inline-Confirm-Overlay vor dem destruktiven Import (überschreibt ALLE aktuellen Daten).
    [ObservableProperty]
    private bool _isImportConfirmVisible;

    [ObservableProperty]
    private string _importConfirmText = "";

    /// <summary>Gate für den ScrollViewer-Hit-Test, solange irgendein Overlay offen ist.</summary>
    public bool IsAnyOverlayVisible => IsImportConfirmVisible || IsPurchaseOptionsVisible;

    partial void OnIsImportConfirmVisibleChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));
    partial void OnIsPurchaseOptionsVisibleChanged(bool value) => OnPropertyChanged(nameof(IsAnyOverlayVisible));

    private string? _pendingImportPath;

    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        try
        {
            IsLoading = true;
            var result = await _backupService.ExportBackupAsync();

            if (result.Success)
            {
                MessageRequested?.Invoke(
                    AppStrings.Backup ?? "Backup",
                    AppStrings.BackupExportSuccess ?? "Backup erfolgreich exportiert");
            }
            else
            {
                MessageRequested?.Invoke(
                    AppStrings.Error,
                    result.ErrorMessage ?? AppStrings.BackupExportFailed ?? "Backup-Export fehlgeschlagen");
            }
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

    /// <summary>
    /// Schritt 1: jüngstes lokales Backup ermitteln und Bestätigung anfordern.
    /// Der eigentliche (destruktive) Import läuft erst nach Bestätigung in <see cref="ConfirmImportAsync"/>.
    /// </summary>
    [RelayCommand]
    private async Task ImportBackupAsync()
    {
        try
        {
            var backups = await _backupService.GetLocalBackupsAsync();

            if (backups.Count == 0)
            {
                MessageRequested?.Invoke(
                    AppStrings.Backup ?? "Backup",
                    AppStrings.BackupNoBackupsFound ?? "Keine Backups gefunden");
                return;
            }

            // Jüngstes Backup vormerken; Bestätigung mit Datum + Überschreib-Warnung anzeigen.
            var latest = backups[0];
            _pendingImportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorkTimePro", "Backups", latest.FileName);

            ImportConfirmText = string.Format(
                AppStrings.BackupImportConfirmText ?? "Backup vom {0} wiederherstellen? Alle aktuellen Daten werden überschrieben.",
                latest.DateDisplay);
            IsImportConfirmVisible = true;
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    /// <summary>Schritt 2: Import nach Bestätigung durchführen (überschreibt alle aktuellen Daten).</summary>
    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        IsImportConfirmVisible = false;
        var filePath = _pendingImportPath;
        _pendingImportPath = null;
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            IsLoading = true;
            var success = await _backupService.ImportBackupFromFileAsync(filePath);

            if (success)
            {
                MessageRequested?.Invoke(
                    AppStrings.Backup ?? "Backup",
                    AppStrings.BackupImportSuccess ?? "Backup erfolgreich importiert");
                // Backup-Import = strukturelle Datenänderung → vollständiger Tab-Reload nötig
                SettingsChanged?.Invoke(this, true);
            }
            else
            {
                MessageRequested?.Invoke(
                    AppStrings.Error,
                    AppStrings.BackupImportFailed ?? "Backup-Import fehlgeschlagen");
            }
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

    /// <summary>Bricht den vorgemerkten Import ab.</summary>
    [RelayCommand]
    private void CancelImport()
    {
        IsImportConfirmVisible = false;
        _pendingImportPath = null;
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
            PremiumStatusText = AppStrings.HasLifetime;
            PremiumStatusColor = AppColors.PremiumActive;
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (_purchaseService.HasActiveSubscription)
        {
            PremiumStatusText = AppStrings.HasSubscription;
            PremiumStatusColor = AppColors.PremiumActive;
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (IsPremium)
        {
            PremiumStatusText = AppStrings.PremiumActive;
            PremiumStatusColor = AppColors.PremiumActive;
            TrialProgress = 1.0;
            TrialProgressText = "";
        }
        else if (IsInTrial)
        {
            // Trial-Dauer aus der einzigen Quelle (ITrialService) statt hartkodiert — TrialService
            // gewährt 14 Tage; eine feste 7 hier ergibt in der ersten Woche Progress > 100%.
            var trialDays = _trialService.TrialDurationDays;
            PremiumStatusText = string.Format(AppStrings.TrialDaysLeft, TrialDaysLeft);
            PremiumStatusColor = AppColors.PremiumTrial;
            TrialProgress = trialDays > 0 ? (double)TrialDaysLeft / trialDays : 0;
            TrialProgressText = $"{TrialDaysLeft} / {trialDays}";
        }
        else if (_trialService.IsTrialExpired)
        {
            PremiumStatusText = AppStrings.FreeVersion;
            PremiumStatusColor = AppColors.PremiumExpired;
            TrialProgress = 0;
            TrialProgressText = "";
        }
        else
        {
            PremiumStatusText = AppStrings.FreeVersion;
            PremiumStatusColor = AppColors.PremiumFree;
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
        _reminderRescheduleCts?.Cancel();
        _reminderRescheduleCts?.Dispose();
        _purchaseService.PremiumStatusChanged -= OnPurchaseStatusChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
