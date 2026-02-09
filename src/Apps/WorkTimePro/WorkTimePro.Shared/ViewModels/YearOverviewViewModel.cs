using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MeineApps.Core.Premium.Ava.Services;
using SkiaSharp;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for year overview (Premium feature)
/// </summary>
public partial class YearOverviewViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IVacationService _vacationService;
    private readonly IExportService _exportService;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;
    private readonly IRewardedAdService _rewardedAdService;

    // Rewarded Ad Overlay
    [ObservableProperty]
    private bool _showRewardedAdOverlay;

    /// <summary>Aufgeschobene Aktion nach erfolgreicher Ad-Wiedergabe</summary>
    private Func<Task>? _pendingAction;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _showAds = true;

    // Year overview
    [ObservableProperty]
    private ObservableCollection<MonthSummary> _months = new();

    [ObservableProperty]
    private int _totalWorkDays;

    [ObservableProperty]
    private string _totalWorkTimeDisplay = "0:00";

    [ObservableProperty]
    private string _totalTargetTimeDisplay = "0:00";

    [ObservableProperty]
    private string _totalBalanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = "#4CAF50";

    [ObservableProperty]
    private int _vacationDaysTaken;

    [ObservableProperty]
    private int _sickDays;

    [ObservableProperty]
    private double _averageHoursPerDay;

    // Display strings
    public string AverageHoursPerDayDisplay => $"{AverageHoursPerDay:F1}h";
    public string VacationDaysTakenDisplay => VacationDaysTaken.ToString();
    public string SickDaysDisplay => SickDays.ToString();

    // Charts
    [ObservableProperty]
    private ISeries[] _monthlyChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _balanceChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    public YearOverviewViewModel(
        IDatabaseService database,
        ICalculationService calculation,
        IVacationService vacationService,
        IExportService exportService,
        IPurchaseService purchaseService,
        ITrialService trialService,
        IRewardedAdService rewardedAdService)
    {
        _database = database;
        _calculation = calculation;
        _vacationService = vacationService;
        _exportService = exportService;
        _purchaseService = purchaseService;
        _trialService = trialService;
        _rewardedAdService = rewardedAdService;

        SelectedYear = DateTime.Today.Year;
        InitializeAxes();
    }

    private void InitializeAxes()
    {
        // Use culture-aware abbreviated month names
        var monthNames = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames
            .Take(12).ToArray();

        XAxes = new Axis[]
        {
            new Axis
            {
                Labels = monthNames,
                TextSize = 10
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                Name = AppStrings.ChartHours,
                TextSize = 10
            }
        };
    }

    partial void OnSelectedYearChanged(int value)
    {
        _ = LoadDataAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, t.Exception?.Message));
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            IsPremium = _purchaseService.IsPremium || _trialService.IsTrialActive;
            ShowAds = !IsPremium;

            var monthSummaries = new List<MonthSummary>();
            var monthlyWorkHours = new List<double>();
            var cumulativeBalance = new List<double>();
            var runningBalance = 0.0;

            int yearWorkMinutes = 0;
            int yearTargetMinutes = 0;
            int yearBalanceMinutes = 0;
            int yearWorkDays = 0;

            for (int month = 1; month <= 12; month++)
            {
                var monthData = await _calculation.CalculateMonthAsync(SelectedYear, month);

                var summary = new MonthSummary
                {
                    Month = month,
                    MonthName = new DateTime(SelectedYear, month, 1).ToString("MMMM"),
                    WorkDays = monthData.WorkedDays,
                    WorkMinutes = monthData.ActualWorkMinutes,
                    TargetMinutes = monthData.TargetWorkMinutes,
                    BalanceMinutes = monthData.BalanceMinutes,
                    IsLocked = await _database.IsMonthLockedAsync(SelectedYear, month)
                };

                monthSummaries.Add(summary);
                monthlyWorkHours.Add(monthData.ActualWorkMinutes / 60.0);

                runningBalance += monthData.BalanceMinutes / 60.0;
                cumulativeBalance.Add(runningBalance);

                yearWorkMinutes += monthData.ActualWorkMinutes;
                yearTargetMinutes += monthData.TargetWorkMinutes;
                yearBalanceMinutes += monthData.BalanceMinutes;
                yearWorkDays += monthData.WorkedDays;
            }

            Months = new ObservableCollection<MonthSummary>(monthSummaries);

            // Year totals
            TotalWorkDays = yearWorkDays;
            TotalWorkTimeDisplay = FormatMinutes(yearWorkMinutes);
            TotalTargetTimeDisplay = FormatMinutes(yearTargetMinutes);
            TotalBalanceDisplay = FormatBalance(yearBalanceMinutes);
            BalanceColor = yearBalanceMinutes >= 0 ? "#4CAF50" : "#F44336";

            // Average
            AverageHoursPerDay = yearWorkDays > 0 ? (yearWorkMinutes / 60.0) / yearWorkDays : 0;

            // Vacation/Sick
            var vacationStats = await _vacationService.GetStatisticsAsync(SelectedYear);
            VacationDaysTaken = vacationStats.TakenDays;
            SickDays = vacationStats.SickDays;

            OnPropertyChanged(nameof(AverageHoursPerDayDisplay));
            OnPropertyChanged(nameof(VacationDaysTakenDisplay));
            OnPropertyChanged(nameof(SickDaysDisplay));

            // Charts
            UpdateCharts(monthlyWorkHours, cumulativeBalance);
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

    private void UpdateCharts(List<double> monthlyHours, List<double> cumulativeBalance)
    {
        // Monthly work hours
        MonthlyChart = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = monthlyHours,
                Name = AppStrings.ChartWorkHours,
                Fill = new SolidColorPaint(SKColor.Parse("#1565C0")),
                Stroke = null,
                MaxBarWidth = 30
            }
        };

        // Cumulative balance
        BalanceChart = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = cumulativeBalance,
                Name = AppStrings.ChartCumulativeBalance,
                Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 3),
                Fill = new SolidColorPaint(SKColor.Parse("#FF9800").WithAlpha(50)),
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2)
            }
        };
    }

    [RelayCommand]
    private void PreviousYear()
    {
        SelectedYear--;
    }

    [RelayCommand]
    private void NextYear()
    {
        SelectedYear++;
    }

    [RelayCommand]
    private void GoToCurrentYear()
    {
        SelectedYear = DateTime.Today.Year;
    }

    [RelayCommand]
    private async Task ExportPdfAsync(bool skipPremiumCheck = false)
    {
        if (!skipPremiumCheck && !IsPremium)
        {
            // Soft-Paywall: Ad-Overlay anzeigen statt hart blockieren
            _pendingAction = () => ExportPdfAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        try
        {
            IsLoading = true;
            var filePath = await _exportService.ExportYearToPdfAsync(SelectedYear);
            await _exportService.ShareFileAsync(filePath);
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ExportFailedMessage, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    // === Rewarded Ad Commands ===

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        ShowRewardedAdOverlay = false;
        var success = await _rewardedAdService.ShowAdAsync("export");
        if (success && _pendingAction != null)
        {
            await _pendingAction();
        }
        _pendingAction = null;
    }

    [RelayCommand]
    private void CancelAdOverlay()
    {
        ShowRewardedAdOverlay = false;
        _pendingAction = null;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void NavigateToMonth(MonthSummary month)
    {
        var date = new DateTime(SelectedYear, month.Month, 1);
        NavigationRequested?.Invoke($"month?date={date:yyyy-MM-dd}");
    }

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var mins = Math.Abs(minutes % 60);
        return $"{hours}:{mins:D2}";
    }

    private static string FormatBalance(int minutes)
    {
        var sign = minutes >= 0 ? "+" : "";
        return sign + FormatMinutes(minutes);
    }
}

/// <summary>
/// Summary of a month
/// </summary>
public class MonthSummary
{
    public int Month { get; set; }
    public string MonthName { get; set; } = "";
    public int WorkDays { get; set; }
    public int WorkMinutes { get; set; }
    public int TargetMinutes { get; set; }
    public int BalanceMinutes { get; set; }
    public bool IsLocked { get; set; }

    public string WorkTimeDisplay => FormatMinutes(WorkMinutes);
    public string TargetTimeDisplay => FormatMinutes(TargetMinutes);
    public string BalanceDisplay => (BalanceMinutes >= 0 ? "+" : "") + FormatMinutes(BalanceMinutes);
    public string BalanceColor => BalanceMinutes >= 0 ? "#4CAF50" : "#F44336";
    public string LockIcon => IsLocked ? Icons.Lock : "";

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var mins = Math.Abs(minutes % 60);
        return $"{hours}:{mins:D2}";
    }
}
