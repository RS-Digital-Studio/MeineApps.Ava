using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for week overview
/// </summary>
public partial class WeekOverviewViewModel : ObservableObject
{
    private readonly ICalculationService _calculation;
    private readonly IDatabaseService _database;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;
    private readonly ILocalizationService _localization;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public WeekOverviewViewModel(
        ICalculationService calculation,
        IDatabaseService database,
        IPurchaseService purchaseService,
        ITrialService trialService,
        ILocalizationService localization)
    {
        _calculation = calculation;
        _database = database;
        _purchaseService = purchaseService;
        _trialService = trialService;
        _localization = localization;
    }

    // === Properties ===

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private WorkWeek? _currentWeek;

    [ObservableProperty]
    private WorkWeek? _previousWeek;

    [ObservableProperty]
    private ObservableCollection<WorkDay> _days = new();

    [ObservableProperty]
    private string _weekDisplay = "";

    [ObservableProperty]
    private string _dateRangeDisplay = "";

    [ObservableProperty]
    private string _workTimeDisplay = "0:00";

    [ObservableProperty]
    private string _targetTimeDisplay = "40:00";

    [ObservableProperty]
    private string _balanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = AppColors.BalancePositive;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private int _workedDays;

    [ObservableProperty]
    private int _vacationDays;

    [ObservableProperty]
    private int _sickDays;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // === Predictive Insights ===

    [ObservableProperty]
    private string _predictiveInsightText = "";

    [ObservableProperty]
    private bool _hasPredictiveInsight;

    // Derived properties
    public bool HasVacationDays => VacationDays > 0;
    public bool HasSickDays => SickDays > 0;

    partial void OnVacationDaysChanged(int value) => OnPropertyChanged(nameof(HasVacationDays));
    partial void OnSickDaysChanged(int value) => OnPropertyChanged(nameof(HasSickDays));

    // Localized texts
    public string TodayButtonText => $"{Icons.CalendarToday} {AppStrings.Today}";

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Fallback-Werte VOR DB-Zugriff setzen (damit bei Exception zumindest Titel sichtbar ist)
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var cal = culture.Calendar;
            var weekNum = cal.GetWeekOfYear(SelectedDate, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            WeekDisplay = string.Format(
                AppStrings.WeekNumberFormat ?? "CW {0} / {1}",
                weekNum, SelectedDate.Year);

            // Montag bis Sonntag der aktuellen Woche
            var dayOfWeek = ((int)SelectedDate.DayOfWeek + 6) % 7;
            var monday = SelectedDate.AddDays(-dayOfWeek);
            var sunday = monday.AddDays(6);
            DateRangeDisplay = $"{monday.ToString("d")} - {sunday.ToString("d")}";

            // Load current week
            CurrentWeek = await _calculation.CalculateWeekAsync(SelectedDate);

            // Load previous week
            PreviousWeek = await _calculation.CalculateWeekAsync(SelectedDate.AddDays(-7));

            // Update UI (überschreibt Fallback-Werte mit exakten Berechnungen)
            WeekDisplay = CurrentWeek.WeekDisplay;
            DateRangeDisplay = CurrentWeek.DateRangeDisplay;
            WorkTimeDisplay = CurrentWeek.ActualWorkDisplay;
            TargetTimeDisplay = CurrentWeek.TargetWorkDisplay;
            BalanceDisplay = CurrentWeek.BalanceDisplay;
            BalanceColor = CurrentWeek.BalanceColor;
            ProgressPercent = CurrentWeek.ProgressPercent;
            ProgressText = $"{ProgressPercent:F0}%";
            WorkedDays = CurrentWeek.WorkedDays;
            VacationDays = CurrentWeek.VacationDays;
            SickDays = CurrentWeek.SickDays;

            Days = new ObservableCollection<WorkDay>(CurrentWeek.Days);

            // Predictive Insights berechnen
            await UpdatePredictiveInsightAsync();

            // Premium status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        SelectedDate = SelectedDate.AddDays(-7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        SelectedDate = SelectedDate.AddDays(7);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadDataAsync();
    }

    [RelayCommand]
    private void SelectDay(WorkDay? day)
    {
        if (day == null) return;
        NavigationRequested?.Invoke($"DayDetailPage?date={day.Date:yyyy-MM-dd}");
    }

    /// <summary>
    /// Berechnet prädiktive Einblicke: Verbleibende Stunden bis Wochenziel,
    /// geschätzter Monatstrend basierend auf bisherigem Durchschnitt.
    /// </summary>
    private async Task UpdatePredictiveInsightAsync()
    {
        try
        {
            if (CurrentWeek == null)
            {
                HasPredictiveInsight = false;
                return;
            }

            var settings = await _database.GetSettingsAsync();
            var weekTargetMinutes = (int)(settings.WeeklyHours * 60);
            var weekActualMinutes = CurrentWeek.Days.Sum(d => d.ActualWorkMinutes);
            var remainingMinutes = weekTargetMinutes - weekActualMinutes;

            // Nur für die aktuelle Woche sinnvoll
            var today = DateTime.Today;
            var dayOfWeek = ((int)today.DayOfWeek + 6) % 7;
            var monday = today.AddDays(-dayOfWeek);
            var isCurrentWeek = SelectedDate >= monday && SelectedDate <= monday.AddDays(6);

            if (!isCurrentWeek)
            {
                // Vergangene/zukünftige Wochen: Keine Prediction
                HasPredictiveInsight = false;
                return;
            }

            if (remainingMinutes > 0)
            {
                // Noch nicht erreicht: "Noch X:XX bis Wochenziel"
                var hours = remainingMinutes / 60;
                var mins = remainingMinutes % 60;
                PredictiveInsightText = string.Format(
                    _localization.GetString("InsightRemainingWeek") ?? "Noch {0}:{1:D2} bis Wochenziel",
                    hours, mins);
                HasPredictiveInsight = true;
            }
            else
            {
                // Monatstrend berechnen
                var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                var monthWorkDays = await _database.GetWorkDaysAsync(firstOfMonth, today);
                var totalOvertimeMinutes = monthWorkDays.Sum(d => d.BalanceMinutes);

                if (Math.Abs(totalOvertimeMinutes) >= 5)
                {
                    var absHours = Math.Abs(totalOvertimeMinutes) / 60;
                    var absMins = Math.Abs(totalOvertimeMinutes) % 60;
                    var sign = totalOvertimeMinutes < 0 ? "-" : "+";
                    PredictiveInsightText = string.Format(
                        _localization.GetString("InsightMonthTrend") ?? "Monatstrend: {0}{1}:{2:D2} Überstunden",
                        sign, absHours, absMins);
                    HasPredictiveInsight = true;
                }
                else
                {
                    HasPredictiveInsight = false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Predictive Insight Fehler: {ex.Message}");
            HasPredictiveInsight = false;
        }
    }
}
