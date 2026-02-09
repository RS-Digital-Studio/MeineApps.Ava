using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using SkiaSharp;
using WorkTimePro.Models;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for statistics page with extended charts (Premium)
/// Phase 7: Extended charts and statistics
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IProjectService _projectService;
    private readonly IEmployerService _employerService;
    private readonly IExportService _exportService;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPreferencesService _preferences;

    private const string ExtendedStatsExpiryKey = "ExtendedStatsExpiry";

    // Rewarded Ad Overlay
    [ObservableProperty]
    private bool _showRewardedAdOverlay;

    /// <summary>Aufgeschobene Aktion nach erfolgreicher Ad-Wiedergabe</summary>
    private Func<Task>? _pendingAction;

    // Color palette for charts
    private static readonly string[] ChartColors = new[]
    {
        "#1565C0", "#2E7D32", "#F57C00", "#C62828", "#6A1B9A",
        "#00838F", "#4527A0", "#AD1457", "#00695C", "#EF6C00"
    };

    public event Action<string, string>? MessageRequested;

    public StatisticsViewModel(
        IDatabaseService database,
        ICalculationService calculation,
        IProjectService projectService,
        IEmployerService employerService,
        IExportService exportService,
        IPurchaseService purchaseService,
        ITrialService trialService,
        IRewardedAdService rewardedAdService,
        IPreferencesService preferences)
    {
        _database = database;
        _calculation = calculation;
        _projectService = projectService;
        _employerService = employerService;
        _exportService = exportService;
        _purchaseService = purchaseService;
        _trialService = trialService;
        _rewardedAdService = rewardedAdService;
        _preferences = preferences;
    }

    // === Properties ===

    [ObservableProperty]
    private StatisticsPeriod _selectedPeriod = StatisticsPeriod.Month;

    // Period selection booleans
    public bool IsWeekSelected => SelectedPeriod == StatisticsPeriod.Week;
    public bool IsMonthSelected => SelectedPeriod == StatisticsPeriod.Month;
    public bool IsQuarterSelected => SelectedPeriod == StatisticsPeriod.Quarter;
    public bool IsYearSelected => SelectedPeriod == StatisticsPeriod.Year;

    partial void OnSelectedPeriodChanged(StatisticsPeriod value)
    {
        OnPropertyChanged(nameof(IsWeekSelected));
        OnPropertyChanged(nameof(IsMonthSelected));
        OnPropertyChanged(nameof(IsQuarterSelected));
        OnPropertyChanged(nameof(IsYearSelected));
    }

    [ObservableProperty]
    private DateTime _startDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private string _periodDisplay = "";

    [ObservableProperty]
    private string _totalWorkDisplay = "0:00";

    [ObservableProperty]
    private string _totalOvertimeDisplay = "+0:00";

    [ObservableProperty]
    private string _overtimeColor = "#4CAF50";

    [ObservableProperty]
    private string _averageDailyDisplay = "0:00";

    [ObservableProperty]
    private int _workedDays;

    [ObservableProperty]
    private int _vacationDays;

    [ObservableProperty]
    private int _sickDays;

    [ObservableProperty]
    private int _homeOfficeDays;

    [ObservableProperty]
    private int _holidayDays;

    // Pause statistics
    [ObservableProperty]
    private string _totalManualPauseDisplay = "0:00";

    [ObservableProperty]
    private string _totalAutoPauseDisplay = "0:00";

    [ObservableProperty]
    private string _averagePauseDisplay = "0:00";

    // Charts
    [ObservableProperty]
    private ISeries[] _weeklyChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _weeklyXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _weeklyYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] _overtimeChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _overtimeXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _overtimeYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] _projectChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _employerChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _weekdayChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _weekdayXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _weekdayYAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private ISeries[] _pauseChart = Array.Empty<ISeries>();

    [ObservableProperty]
    private bool _hasProjects;

    [ObservableProperty]
    private bool _hasEmployers;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Table view (default: show table)
    [ObservableProperty]
    private bool _showTable = true;

    [ObservableProperty]
    private ObservableCollection<WorkDayTableItem> _tableDays = new();

    // Derived properties
    public bool HasPauseChartData => PauseChart.Length > 0;
    public bool HasNoTableData => TableDays.Count == 0;

    // Localized title texts
    public string ChartsButtonText => $"{Icons.ChartBar} {AppStrings.Charts}";
    public string TableButtonText => $"{Icons.FileDocument} {AppStrings.Table}";
    public string PauseStatsTitle => $"{Icons.Coffee} {AppStrings.PauseStats}";
    public string AutoPauseLabel => $"{AppStrings.Auto} {Icons.Lightning}";
    public string WeeklyChartTitle => $"{Icons.ChartBar} {AppStrings.WeeklyWorkTimeChart}";
    public string OvertimeChartTitle => $"{Icons.TrendingUp} {AppStrings.OvertimeTrendChart}";
    public string WeekdayChartTitle => $"{Icons.CalendarWeek} {AppStrings.WeekdayAverage}";
    public string ProjectChartTitle => $"{Icons.Briefcase} {AppStrings.ProjectDistribution}";
    public string EmployerChartTitle => $"{Icons.AccountGroup} {AppStrings.EmployerDistribution}";
    public string WorkTimeTableTitle => $"{Icons.FileDocument} {AppStrings.WorkTimeTable}";

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Calculate period
            CalculatePeriod();

            // Load data
            var workDays = await _database.GetWorkDaysAsync(StartDate, EndDate);

            // Calculate statistics
            CalculateStatistics(workDays);

            // Fill table data
            FillTableData(workDays);

            // Create charts (parallel)
            await Task.WhenAll(
                CreateWeeklyChartAsync(workDays),
                CreateOvertimeChartAsync(workDays),
                CreateProjectChartAsync(),
                CreateEmployerChartAsync(),
                CreateWeekdayChartAsync(workDays),
                CreatePauseChartAsync(workDays)
            );

            // Premium status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;

            OnPropertyChanged(nameof(HasPauseChartData));
            OnPropertyChanged(nameof(HasNoTableData));
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
    private async Task ChangePeriodAsync(string periodStr)
    {
        if (!Enum.TryParse<StatisticsPeriod>(periodStr, out var period))
            return;

        // Quartal/Jahr erfordern Premium oder Rewarded Ad (24h Zugang)
        if ((period == StatisticsPeriod.Quarter || period == StatisticsPeriod.Year)
            && !_purchaseService.IsPremium && !_trialService.IsTrialActive
            && !HasExtendedStatsAccess())
        {
            _pendingAction = async () =>
            {
                // 24h Zugang gewaehren
                GrantExtendedStatsAccess();
                SelectedPeriod = period;
                await LoadDataAsync();
            };
            ShowRewardedAdOverlay = true;
            return;
        }

        SelectedPeriod = period;
        await LoadDataAsync();
    }

    /// <summary>Prueft ob der Nutzer erweiterten Statistik-Zugang hat (24h nach Video)</summary>
    private bool HasExtendedStatsAccess()
    {
        var expiryStr = _preferences.Get<string>(ExtendedStatsExpiryKey, "");
        if (string.IsNullOrEmpty(expiryStr))
            return false;

        if (DateTime.TryParse(expiryStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry))
            return DateTime.UtcNow < expiry;

        return false;
    }

    /// <summary>Gewaehrt 24h Zugang zu erweiterten Statistik-Zeitraeumen</summary>
    private void GrantExtendedStatsAccess()
    {
        var expiry = DateTime.UtcNow.AddHours(24);
        _preferences.Set(ExtendedStatsExpiryKey, expiry.ToString("O", CultureInfo.InvariantCulture));
    }

    [RelayCommand]
    private void ToggleView()
    {
        ShowTable = !ShowTable;
    }

    [RelayCommand]
    private async Task ExportAsync(bool skipPremiumCheck = false)
    {
        if (!skipPremiumCheck && !_purchaseService.IsPremium && !_trialService.IsTrialActive)
        {
            // Soft-Paywall: Ad-Overlay anzeigen statt hart blockieren
            _pendingAction = () => ExportAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        try
        {
            // Standard-Export als PDF in Avalonia
            IsLoading = true;
            string? filePath = await _exportService.ExportRangeToPdfAsync(StartDate, EndDate);

            if (!string.IsNullOrEmpty(filePath))
            {
                await _exportService.ShareFileAsync(filePath);
            }
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

    // Lokalisierter Export-Button-Text
    public string ExportButtonText => $"{Icons.Export} {AppStrings.Export}";

    // === Rewarded Ad Commands ===

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        ShowRewardedAdOverlay = false;
        var success = await _rewardedAdService.ShowAdAsync("monthly_stats");
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

    // === Calculations ===

    private void CalculateStatistics(List<WorkDay> workDays)
    {
        var totalMinutes = workDays.Sum(w => w.ActualWorkMinutes);
        var totalOvertime = workDays.Sum(w => w.BalanceMinutes);

        TotalWorkDisplay = FormatMinutes(totalMinutes);
        TotalOvertimeDisplay = (totalOvertime >= 0 ? "+" : "") + FormatMinutes(totalOvertime);
        OvertimeColor = totalOvertime >= 0 ? "#4CAF50" : "#F44336";

        WorkedDays = workDays.Count(w => w.ActualWorkMinutes > 0);
        VacationDays = workDays.Count(w => w.Status == DayStatus.Vacation);
        SickDays = workDays.Count(w => w.Status == DayStatus.Sick);
        HomeOfficeDays = workDays.Count(w => w.Status == DayStatus.HomeOffice);
        HolidayDays = workDays.Count(w => w.Status == DayStatus.Holiday);

        if (WorkedDays > 0)
        {
            var avgMinutes = totalMinutes / WorkedDays;
            AverageDailyDisplay = FormatMinutes(avgMinutes);
        }
        else
        {
            AverageDailyDisplay = "0:00";
        }

        // Pause statistics
        var totalManualPause = workDays.Sum(w => w.ManualPauseMinutes);
        var totalAutoPause = workDays.Sum(w => w.AutoPauseMinutes);
        var totalPause = totalManualPause + totalAutoPause;

        TotalManualPauseDisplay = FormatMinutes(totalManualPause);
        TotalAutoPauseDisplay = FormatMinutes(totalAutoPause);
        AveragePauseDisplay = WorkedDays > 0 ? FormatMinutes(totalPause / WorkedDays) : "0:00";
    }

    private void CalculatePeriod()
    {
        var today = DateTime.Today;

        switch (SelectedPeriod)
        {
            case StatisticsPeriod.Week:
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                StartDate = today.AddDays(-daysSinceMonday);
                EndDate = StartDate.AddDays(6);
                PeriodDisplay = $"KW {_calculation.GetIsoWeekNumber(today)}";
                break;

            case StatisticsPeriod.Month:
                StartDate = new DateTime(today.Year, today.Month, 1);
                EndDate = StartDate.AddMonths(1).AddDays(-1);
                PeriodDisplay = today.ToString("MMMM yyyy");
                break;

            case StatisticsPeriod.Quarter:
                var quarter = (today.Month - 1) / 3;
                StartDate = new DateTime(today.Year, quarter * 3 + 1, 1);
                EndDate = StartDate.AddMonths(3).AddDays(-1);
                PeriodDisplay = $"Q{quarter + 1} {today.Year}";
                break;

            case StatisticsPeriod.Year:
                StartDate = new DateTime(today.Year, 1, 1);
                EndDate = new DateTime(today.Year, 12, 31);
                PeriodDisplay = today.Year.ToString();
                break;
        }
    }

    // === Chart creation ===

    private Task CreateWeeklyChartAsync(List<WorkDay> workDays)
    {
        var weeks = workDays
            .Where(w => w.ActualWorkMinutes > 0)
            .GroupBy(w => _calculation.GetIsoWeekNumber(w.Date))
            .OrderBy(g => g.Key)
            .Take(12)
            .ToList();

        var weekLabels = weeks.Select(g => $"KW {g.Key}").ToArray();
        var weekValues = weeks.Select(g => g.Sum(w => w.ActualWorkMinutes) / 60.0).ToArray();
        var targetValues = weeks.Select(g => 40.0).ToArray(); // Default weekly target

        WeeklyChart = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = weekValues,
                Name = AppStrings.ChartActualHours,
                Fill = new SolidColorPaint(SKColor.Parse("#1565C0")),
                MaxBarWidth = 25
            },
            new LineSeries<double>
            {
                Values = targetValues,
                Name = string.Format(AppStrings.ChartTargetFormat, 40),
                Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0
            }
        };

        WeeklyXAxes = new Axis[]
        {
            new Axis
            {
                Labels = weekLabels,
                LabelsRotation = 45,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575"))
            }
        };

        WeeklyYAxes = new Axis[]
        {
            new Axis
            {
                Name = AppStrings.ChartHours,
                NameTextSize = 11,
                TextSize = 10,
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575")),
                NamePaint = new SolidColorPaint(SKColor.Parse("#757575"))
            }
        };

        return Task.CompletedTask;
    }

    private Task CreateOvertimeChartAsync(List<WorkDay> workDays)
    {
        var orderedDays = workDays
            .Where(w => w.Status == DayStatus.WorkDay || w.Status == DayStatus.HomeOffice)
            .OrderBy(w => w.Date)
            .ToList();

        if (orderedDays.Count == 0)
        {
            OvertimeChart = Array.Empty<ISeries>();
            return Task.CompletedTask;
        }

        // Daily overtime
        var dailyBalance = orderedDays.Select(w => w.BalanceMinutes / 60.0).ToArray();

        // Cumulative overtime
        var cumulativeBalance = new List<double>();
        double cumulative = 0;
        foreach (var balance in dailyBalance)
        {
            cumulative += balance;
            cumulativeBalance.Add(cumulative);
        }

        // Date labels
        var dateLabels = orderedDays.Select(w => w.Date.ToString("dd.MM")).ToArray();

        OvertimeChart = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = dailyBalance,
                Name = AppStrings.ChartDaily,
                Fill = new SolidColorPaint(SKColor.Parse("#90CAF9")),
                MaxBarWidth = 8
            },
            new LineSeries<double>
            {
                Values = cumulativeBalance,
                Name = AppStrings.ChartCumulative,
                Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 3),
                Fill = null,
                GeometrySize = 4,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 2)
            }
        };

        OvertimeXAxes = new Axis[]
        {
            new Axis
            {
                Labels = dateLabels,
                LabelsRotation = 45,
                TextSize = 9,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575")),
                ShowSeparatorLines = false
            }
        };

        OvertimeYAxes = new Axis[]
        {
            new Axis
            {
                Name = AppStrings.ChartHours,
                NameTextSize = 11,
                TextSize = 10,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575")),
                NamePaint = new SolidColorPaint(SKColor.Parse("#757575"))
            }
        };

        return Task.CompletedTask;
    }

    private async Task CreateProjectChartAsync()
    {
        try
        {
            var projectHours = await _projectService.GetProjectHoursAsync(StartDate, EndDate);
            HasProjects = projectHours.Count > 0;

            if (!HasProjects)
            {
                ProjectChart = Array.Empty<ISeries>();
                return;
            }

            var pieSeries = new List<ISeries>();
            int colorIndex = 0;

            foreach (var kvp in projectHours.OrderByDescending(x => x.Value))
            {
                var project = kvp.Key;
                var hours = kvp.Value;

                if (hours <= 0) continue;

                var color = !string.IsNullOrEmpty(project.Color)
                    ? project.Color
                    : ChartColors[colorIndex % ChartColors.Length];

                pieSeries.Add(new PieSeries<double>
                {
                    Values = new[] { hours },
                    Name = project.Name,
                    Fill = new SolidColorPaint(SKColor.Parse(color)),
                    DataLabelsSize = 11,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsFormatter = point => $"{hours:F1}h"
                });

                colorIndex++;
            }

            ProjectChart = pieSeries.ToArray();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
            ProjectChart = Array.Empty<ISeries>();
        }
    }

    private async Task CreateEmployerChartAsync()
    {
        try
        {
            var employerHours = await _employerService.GetEmployerHoursAsync(StartDate, EndDate);
            HasEmployers = employerHours.Count > 0;

            if (!HasEmployers)
            {
                EmployerChart = Array.Empty<ISeries>();
                return;
            }

            var pieSeries = new List<ISeries>();
            int colorIndex = 0;

            foreach (var kvp in employerHours.OrderByDescending(x => x.Value))
            {
                var employer = kvp.Key;
                var hours = kvp.Value;

                if (hours <= 0) continue;

                var color = !string.IsNullOrEmpty(employer.Color)
                    ? employer.Color
                    : ChartColors[colorIndex % ChartColors.Length];

                pieSeries.Add(new PieSeries<double>
                {
                    Values = new[] { hours },
                    Name = employer.Name,
                    Fill = new SolidColorPaint(SKColor.Parse(color)),
                    DataLabelsSize = 11,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsFormatter = point => $"{hours:F1}h"
                });

                colorIndex++;
            }

            EmployerChart = pieSeries.ToArray();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
            EmployerChart = Array.Empty<ISeries>();
        }
    }

    private Task CreateWeekdayChartAsync(List<WorkDay> workDays)
    {
        var weekdayNames = new[] { AppStrings.Mon, AppStrings.Tue, AppStrings.Wed, AppStrings.Thu, AppStrings.Fri, AppStrings.Sat, AppStrings.Sun };
        var weekdayHours = new double[7];
        var weekdayCounts = new int[7];

        foreach (var day in workDays.Where(w => w.ActualWorkMinutes > 0))
        {
            // DayOfWeek: Sunday=0, Monday=1, ... -> Convert to Mo=0, Di=1, ...
            var index = ((int)day.Date.DayOfWeek + 6) % 7;
            weekdayHours[index] += day.ActualWorkMinutes / 60.0;
            weekdayCounts[index]++;
        }

        // Calculate average
        var avgHours = new double[7];
        for (int i = 0; i < 7; i++)
        {
            avgHours[i] = weekdayCounts[i] > 0 ? weekdayHours[i] / weekdayCounts[i] : 0;
        }

        WeekdayChart = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = avgHours,
                Name = AppStrings.ChartAvgHours,
                Fill = new SolidColorPaint(SKColor.Parse("#1565C0")),
                MaxBarWidth = 35
            }
        };

        WeekdayXAxes = new Axis[]
        {
            new Axis
            {
                Labels = weekdayNames,
                LabelsRotation = 0,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575"))
            }
        };

        WeekdayYAxes = new Axis[]
        {
            new Axis
            {
                Name = AppStrings.ChartAvgHours,
                NameTextSize = 11,
                TextSize = 10,
                MinLimit = 0,
                MaxLimit = 12,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#757575")),
                NamePaint = new SolidColorPaint(SKColor.Parse("#757575"))
            }
        };

        return Task.CompletedTask;
    }

    private Task CreatePauseChartAsync(List<WorkDay> workDays)
    {
        var totalManual = workDays.Sum(w => w.ManualPauseMinutes) / 60.0;
        var totalAuto = workDays.Sum(w => w.AutoPauseMinutes) / 60.0;

        if (totalManual <= 0 && totalAuto <= 0)
        {
            PauseChart = Array.Empty<ISeries>();
            return Task.CompletedTask;
        }

        PauseChart = new ISeries[]
        {
            new PieSeries<double>
            {
                Values = new[] { totalManual },
                Name = AppStrings.Manual,
                Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                DataLabelsSize = 11,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsFormatter = point => $"{totalManual:F1}h"
            },
            new PieSeries<double>
            {
                Values = new[] { totalAuto },
                Name = $"Auto {Icons.Lightning}",
                Fill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                DataLabelsSize = 11,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsFormatter = point => $"{totalAuto:F1}h"
            }
        };

        return Task.CompletedTask;
    }

    // === Table data ===

    private void FillTableData(List<WorkDay> workDays)
    {
        var items = workDays
            .OrderByDescending(w => w.Date)
            .Select(w => new WorkDayTableItem
            {
                Date = w.Date,
                DateDisplay = w.Date.ToString("ddd, dd.MM"),
                Status = w.Status,
                StatusIcon = GetStatusIcon(w.Status),
                StatusName = GetStatusName(w.Status),
                CheckInTime = w.FirstCheckIn?.ToString("HH:mm") ?? "--:--",
                CheckOutTime = w.LastCheckOut?.ToString("HH:mm") ?? "--:--",
                WorkTime = FormatMinutes(w.ActualWorkMinutes),
                PauseTime = FormatMinutes(w.ManualPauseMinutes + w.AutoPauseMinutes),
                Balance = (w.BalanceMinutes >= 0 ? "+" : "") + FormatMinutes(w.BalanceMinutes),
                BalanceColor = w.BalanceMinutes >= 0 ? "#4CAF50" : "#F44336",
                HasAutoBreak = w.AutoPauseMinutes > 0,
                IsSpecialDay = w.Status != DayStatus.WorkDay && w.Status != DayStatus.HomeOffice && w.Status != DayStatus.Weekend
            })
            .ToList();

        TableDays = new ObservableCollection<WorkDayTableItem>(items);
    }

    private static string GetStatusName(DayStatus status)
    {
        return status switch
        {
            DayStatus.WorkDay => AppStrings.DayStatus_WorkDay,
            DayStatus.HomeOffice => AppStrings.DayStatus_HomeOffice,
            DayStatus.Vacation => AppStrings.DayStatus_Vacation,
            DayStatus.Sick => AppStrings.DayStatus_Sick,
            DayStatus.Holiday => AppStrings.DayStatus_Holiday,
            DayStatus.Weekend => AppStrings.DayStatus_Weekend,
            DayStatus.BusinessTrip => AppStrings.DayStatus_BusinessTrip,
            DayStatus.OvertimeCompensation => AppStrings.OvertimeCompensation,
            DayStatus.SpecialLeave => AppStrings.SpecialLeave,
            _ => ""
        };
    }

    private static string GetStatusIcon(DayStatus status)
    {
        return status switch
        {
            DayStatus.WorkDay => Icons.Briefcase,
            DayStatus.HomeOffice => Icons.HomeAccount,
            DayStatus.Vacation => Icons.Beach,
            DayStatus.Sick => Icons.Thermometer,
            DayStatus.Holiday => Icons.PartyPopper,
            DayStatus.Weekend => Icons.Sleep,
            _ => ""
        };
    }

    // === Helper methods ===

    private static string FormatMinutes(int minutes)
    {
        var hours = Math.Abs(minutes) / 60;
        var mins = Math.Abs(minutes) % 60;
        var sign = minutes < 0 ? "-" : "";
        return $"{sign}{hours}:{mins:D2}";
    }
}

/// <summary>
/// Table item for work time overview
/// </summary>
public class WorkDayTableItem
{
    public DateTime Date { get; set; }
    public string DateDisplay { get; set; } = "";
    public DayStatus Status { get; set; }
    public string StatusIcon { get; set; } = "";
    public string StatusName { get; set; } = "";
    public string CheckInTime { get; set; } = "--:--";
    public string CheckOutTime { get; set; } = "--:--";
    public string WorkTime { get; set; } = "0:00";
    public string PauseTime { get; set; } = "0:00";
    public string Balance { get; set; } = "+0:00";
    public string BalanceColor { get; set; } = "#4CAF50";
    public bool HasAutoBreak { get; set; }
    public bool IsSpecialDay { get; set; } // Vacation, Sick, Holiday
}
