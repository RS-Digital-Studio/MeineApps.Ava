using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using WorkTimePro.Models;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel für die Statistik-Seite. Stellt Daten-Arrays für SkiaSharp-Renderer bereit.
/// Phase 7: LiveCharts durch SkiaSharp-Visualisierungen ersetzt.
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

    // Farb-Palette für Charts (zentralisiert in AppColors)
    private static string[] ChartColors => AppColors.ChartColors;

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

    // Perioden-Auswahl Booleans
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
    private string _overtimeColor = AppColors.BalancePositive;

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

    // Pausen-Statistik
    [ObservableProperty]
    private string _totalManualPauseDisplay = "0:00";

    [ObservableProperty]
    private string _totalAutoPauseDisplay = "0:00";

    [ObservableProperty]
    private string _averagePauseDisplay = "0:00";

    // === SkiaSharp Chart-Daten (ersetzen LiveCharts ISeries/Axis) ===

    // Wöchentliche Arbeitszeit
    [ObservableProperty]
    private string[] _weeklyLabels = Array.Empty<string>();

    [ObservableProperty]
    private float[] _weeklyHoursData = Array.Empty<float>();

    [ObservableProperty]
    private float _weeklyTargetHours;

    // Überstunden-Trend
    [ObservableProperty]
    private float[] _overtimeDailyBalance = Array.Empty<float>();

    [ObservableProperty]
    private float[] _overtimeCumulativeBalance = Array.Empty<float>();

    [ObservableProperty]
    private string[] _overtimeDateLabels = Array.Empty<string>();

    // Wochentag-Durchschnitt
    [ObservableProperty]
    private string[] _weekdayLabels = Array.Empty<string>();

    [ObservableProperty]
    private float[] _weekdayAvgHours = Array.Empty<float>();

    [ObservableProperty]
    private float _weekdayTargetPerDay;

    // Pausen-Donut
    [ObservableProperty]
    private DonutChartVisualization.Segment[] _pauseSegments = Array.Empty<DonutChartVisualization.Segment>();

    // Projekt-Donut
    [ObservableProperty]
    private DonutChartVisualization.Segment[] _projectSegments = Array.Empty<DonutChartVisualization.Segment>();

    // Arbeitgeber-Donut
    [ObservableProperty]
    private DonutChartVisualization.Segment[] _employerSegments = Array.Empty<DonutChartVisualization.Segment>();

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

    // Tabellen-Ansicht (Standard: Tabelle anzeigen)
    [ObservableProperty]
    private bool _showTable = true;

    [ObservableProperty]
    private ObservableCollection<WorkDayTableItem> _tableDays = new();

    // Abgeleitete Properties
    public bool HasPauseChartData => PauseSegments.Length > 0;
    public bool HasOvertimeData => OvertimeDailyBalance.Length > 0;
    public bool HasWeeklyData => WeeklyHoursData.Length > 0;
    public bool HasWeekdayData => WeekdayAvgHours.Length > 0;
    public bool HasNoTableData => TableDays.Count == 0;

    // Lokalisierte Titel
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

            // Zeitraum berechnen
            CalculatePeriod();

            // Daten laden
            var workDays = await _database.GetWorkDaysAsync(StartDate, EndDate);

            // Statistiken berechnen
            CalculateStatistics(workDays);

            // Tabellendaten füllen
            FillTableData(workDays);

            // Charts erstellen (parallel)
            await Task.WhenAll(
                CreateWeeklyChartDataAsync(workDays),
                CreateOvertimeChartData(workDays),
                CreateProjectChartDataAsync(),
                CreateEmployerChartDataAsync(),
                CreateWeekdayChartData(workDays),
                CreatePauseChartData(workDays)
            );

            // Premium-Status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;

            OnPropertyChanged(nameof(HasPauseChartData));
            OnPropertyChanged(nameof(HasOvertimeData));
            OnPropertyChanged(nameof(HasWeeklyData));
            OnPropertyChanged(nameof(HasWeekdayData));
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

    /// <summary>Prüft ob der Nutzer erweiterten Statistik-Zugang hat (24h nach Video)</summary>
    private bool HasExtendedStatsAccess()
    {
        var expiryStr = _preferences.Get<string>(ExtendedStatsExpiryKey, "");
        if (string.IsNullOrEmpty(expiryStr))
            return false;

        if (DateTime.TryParse(expiryStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry))
            return DateTime.UtcNow < expiry;

        return false;
    }

    /// <summary>Gewährt 24h Zugang zu erweiterten Statistik-Zeiträumen</summary>
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
            _pendingAction = () => ExportAsync(skipPremiumCheck: true);
            ShowRewardedAdOverlay = true;
            return;
        }

        try
        {
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

    // === Berechnungen ===

    private void CalculateStatistics(List<WorkDay> workDays)
    {
        var totalMinutes = workDays.Sum(w => w.ActualWorkMinutes);
        var totalOvertime = workDays.Sum(w => w.BalanceMinutes);

        TotalWorkDisplay = TimeFormatter.FormatMinutes(totalMinutes);
        TotalOvertimeDisplay = (totalOvertime >= 0 ? "+" : "") + TimeFormatter.FormatMinutes(totalOvertime);
        OvertimeColor = totalOvertime >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative;

        WorkedDays = workDays.Count(w => w.ActualWorkMinutes > 0);
        VacationDays = workDays.Count(w => w.Status == DayStatus.Vacation);
        SickDays = workDays.Count(w => w.Status == DayStatus.Sick);
        HomeOfficeDays = workDays.Count(w => w.Status == DayStatus.HomeOffice);
        HolidayDays = workDays.Count(w => w.Status == DayStatus.Holiday);

        if (WorkedDays > 0)
        {
            var avgMinutes = totalMinutes / WorkedDays;
            AverageDailyDisplay = TimeFormatter.FormatMinutes(avgMinutes);
        }
        else
        {
            AverageDailyDisplay = "0:00";
        }

        // Pausen-Statistik
        var totalManualPause = workDays.Sum(w => w.ManualPauseMinutes);
        var totalAutoPause = workDays.Sum(w => w.AutoPauseMinutes);
        var totalPause = totalManualPause + totalAutoPause;

        TotalManualPauseDisplay = TimeFormatter.FormatMinutes(totalManualPause);
        TotalAutoPauseDisplay = TimeFormatter.FormatMinutes(totalAutoPause);
        AveragePauseDisplay = WorkedDays > 0 ? TimeFormatter.FormatMinutes(totalPause / WorkedDays) : "0:00";
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
                PeriodDisplay = string.Format(AppStrings.WeekNumberFormat ?? "CW {0} / {1}",
                    _calculation.GetIsoWeekNumber(today), today.Year);
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

    // === Chart-Daten erstellen (SkiaSharp-Daten statt LiveCharts) ===

    private async Task CreateWeeklyChartDataAsync(List<WorkDay> workDays)
    {
        var settings = await _database.GetSettingsAsync();
        var weeklyTarget = settings.WeeklyHours;

        var weeks = workDays
            .Where(w => w.ActualWorkMinutes > 0)
            .GroupBy(w => _calculation.GetIsoWeekNumber(w.Date))
            .OrderBy(g => g.Key)
            .Take(12)
            .ToList();

        WeeklyLabels = weeks.Select(g => string.Format(AppStrings.WeekNumberShort ?? "CW {0}", g.Key)).ToArray();
        WeeklyHoursData = weeks.Select(g => (float)(g.Sum(w => w.ActualWorkMinutes) / 60.0)).ToArray();
        WeeklyTargetHours = (float)weeklyTarget;
    }

    private Task CreateOvertimeChartData(List<WorkDay> workDays)
    {
        var orderedDays = workDays
            .Where(w => w.Status == DayStatus.WorkDay || w.Status == DayStatus.HomeOffice)
            .OrderBy(w => w.Date)
            .ToList();

        if (orderedDays.Count == 0)
        {
            OvertimeDailyBalance = Array.Empty<float>();
            OvertimeCumulativeBalance = Array.Empty<float>();
            OvertimeDateLabels = Array.Empty<string>();
            return Task.CompletedTask;
        }

        OvertimeDailyBalance = orderedDays.Select(w => (float)(w.BalanceMinutes / 60.0)).ToArray();
        OvertimeDateLabels = orderedDays.Select(w => w.Date.ToString("dd.MM")).ToArray();

        // Kumulativ berechnen
        var cumulative = new float[orderedDays.Count];
        float cum = 0f;
        for (int i = 0; i < orderedDays.Count; i++)
        {
            cum += OvertimeDailyBalance[i];
            cumulative[i] = cum;
        }
        OvertimeCumulativeBalance = cumulative;

        return Task.CompletedTask;
    }

    private async Task CreateProjectChartDataAsync()
    {
        try
        {
            var projectHours = await _projectService.GetProjectHoursAsync(StartDate, EndDate);
            HasProjects = projectHours.Count > 0;

            if (!HasProjects)
            {
                ProjectSegments = Array.Empty<DonutChartVisualization.Segment>();
                return;
            }

            var segments = new List<DonutChartVisualization.Segment>();
            int colorIndex = 0;

            foreach (var kvp in projectHours.OrderByDescending(x => x.Value))
            {
                var project = kvp.Key;
                var hours = kvp.Value;
                if (hours <= 0) continue;

                var colorStr = !string.IsNullOrEmpty(project.Color)
                    ? project.Color
                    : ChartColors[colorIndex % ChartColors.Length];

                segments.Add(new DonutChartVisualization.Segment
                {
                    Value = (float)hours,
                    Color = SKColor.Parse(colorStr),
                    Label = project.Name,
                    ValueText = $"{hours:F1}h"
                });

                colorIndex++;
            }

            ProjectSegments = segments.ToArray();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
            ProjectSegments = Array.Empty<DonutChartVisualization.Segment>();
        }
    }

    private async Task CreateEmployerChartDataAsync()
    {
        try
        {
            var employerHours = await _employerService.GetEmployerHoursAsync(StartDate, EndDate);
            HasEmployers = employerHours.Count > 0;

            if (!HasEmployers)
            {
                EmployerSegments = Array.Empty<DonutChartVisualization.Segment>();
                return;
            }

            var segments = new List<DonutChartVisualization.Segment>();
            int colorIndex = 0;

            foreach (var kvp in employerHours.OrderByDescending(x => x.Value))
            {
                var employer = kvp.Key;
                var hours = kvp.Value;
                if (hours <= 0) continue;

                var colorStr = !string.IsNullOrEmpty(employer.Color)
                    ? employer.Color
                    : ChartColors[colorIndex % ChartColors.Length];

                segments.Add(new DonutChartVisualization.Segment
                {
                    Value = (float)hours,
                    Color = SKColor.Parse(colorStr),
                    Label = employer.Name,
                    ValueText = $"{hours:F1}h"
                });

                colorIndex++;
            }

            EmployerSegments = segments.ToArray();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
            EmployerSegments = Array.Empty<DonutChartVisualization.Segment>();
        }
    }

    private Task CreateWeekdayChartData(List<WorkDay> workDays)
    {
        WeekdayLabels = new[] { AppStrings.Mon, AppStrings.Tue, AppStrings.Wed, AppStrings.Thu, AppStrings.Fri, AppStrings.Sat, AppStrings.Sun };
        var weekdayHours = new double[7];
        var weekdayCounts = new int[7];

        foreach (var day in workDays.Where(w => w.ActualWorkMinutes > 0))
        {
            var index = ((int)day.Date.DayOfWeek + 6) % 7;
            weekdayHours[index] += day.ActualWorkMinutes / 60.0;
            weekdayCounts[index]++;
        }

        var avgHours = new float[7];
        for (int i = 0; i < 7; i++)
            avgHours[i] = weekdayCounts[i] > 0 ? (float)(weekdayHours[i] / weekdayCounts[i]) : 0f;

        WeekdayAvgHours = avgHours;

        // Tägliches Soll berechnen (Wochensoll / Anzahl Arbeitstage)
        // Vereinfachung: Wenn 5-Tage-Woche, dann Wochensoll/5
        int workingDayCount = weekdayCounts.Count(c => c > 0);
        WeekdayTargetPerDay = workingDayCount > 0 ? WeeklyTargetHours / Math.Max(workingDayCount, 5) : 8f;

        return Task.CompletedTask;
    }

    private Task CreatePauseChartData(List<WorkDay> workDays)
    {
        var totalManual = (float)(workDays.Sum(w => w.ManualPauseMinutes) / 60.0);
        var totalAuto = (float)(workDays.Sum(w => w.AutoPauseMinutes) / 60.0);

        if (totalManual <= 0 && totalAuto <= 0)
        {
            PauseSegments = Array.Empty<DonutChartVisualization.Segment>();
            return Task.CompletedTask;
        }

        var segments = new List<DonutChartVisualization.Segment>();

        if (totalManual > 0)
        {
            segments.Add(new DonutChartVisualization.Segment
            {
                Value = totalManual,
                Color = SkiaThemeHelper.Success,
                Label = AppStrings.Manual,
                ValueText = $"{totalManual:F1}h"
            });
        }

        if (totalAuto > 0)
        {
            segments.Add(new DonutChartVisualization.Segment
            {
                Value = totalAuto,
                Color = SkiaThemeHelper.Warning,
                Label = $"Auto",
                ValueText = $"{totalAuto:F1}h"
            });
        }

        PauseSegments = segments.ToArray();

        return Task.CompletedTask;
    }

    // === Tabellendaten ===

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
                StatusName = TimeFormatter.GetStatusName(w.Status),
                CheckInTime = w.FirstCheckIn?.ToString("HH:mm") ?? "--:--",
                CheckOutTime = w.LastCheckOut?.ToString("HH:mm") ?? "--:--",
                WorkTime = TimeFormatter.FormatMinutes(w.ActualWorkMinutes),
                PauseTime = TimeFormatter.FormatMinutes(w.ManualPauseMinutes + w.AutoPauseMinutes),
                Balance = (w.BalanceMinutes >= 0 ? "+" : "") + TimeFormatter.FormatMinutes(w.BalanceMinutes),
                BalanceColor = w.BalanceMinutes >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative,
                HasAutoBreak = w.AutoPauseMinutes > 0,
                IsSpecialDay = w.Status != DayStatus.WorkDay && w.Status != DayStatus.HomeOffice && w.Status != DayStatus.Weekend
            })
            .ToList();

        TableDays = new ObservableCollection<WorkDayTableItem>(items);
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
}

/// <summary>
/// Tabellen-Element für die Arbeitszeitübersicht
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
    public string BalanceColor { get; set; } = AppColors.BalancePositive;
    public bool HasAutoBreak { get; set; }
    public bool IsSpecialDay { get; set; }
}
