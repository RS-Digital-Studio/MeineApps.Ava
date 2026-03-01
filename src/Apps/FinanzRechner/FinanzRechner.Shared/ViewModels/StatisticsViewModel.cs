using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.ViewModels;

public partial class StatisticsViewModel : ObservableObject, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly IExportService _exportService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IFileShareService _fileShareService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPreferencesService _preferencesService;

    private const string ExtendedStatsExpiryKey = "ExtendedStatsExpiry";

    public event Action<string, string>? MessageRequested;

    public StatisticsViewModel(IExpenseService expenseService, IExportService exportService,
        ILocalizationService localizationService, IThemeService themeService,
        IFileDialogService fileDialogService, IFileShareService fileShareService,
        IPurchaseService purchaseService, IRewardedAdService rewardedAdService,
        IPreferencesService preferencesService)
    {
        _expenseService = expenseService;
        _exportService = exportService;
        _localizationService = localizationService;
        _themeService = themeService;
        _fileDialogService = fileDialogService;
        _fileShareService = fileShareService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _preferencesService = preferencesService;
        _selectedPeriod = TimePeriod.Month;
    }

    /// <summary>
    /// Markiert die Daten als veraltet, sodass beim nächsten Tab-Wechsel neu geladen wird.
    /// </summary>
    public void InvalidateCache() => _isDataStale = true;
    private bool _isDataStale = true;

    /// <summary>
    /// Wird beim Tab-Wechsel zur Statistik aufgerufen.
    /// Lädt nur neu wenn Daten als veraltet markiert sind.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        if (!_isDataStale) return;
        await LoadStatisticsAsync();
        _isDataStale = false;
    }

    #region Localized Text Properties

    public string StatisticsTitleText => _localizationService.GetString("StatisticsTitle") ?? "Statistics";
    public string WeekText => _localizationService.GetString("Week") ?? "Week";
    public string MonthText => _localizationService.GetString("Month") ?? "Month";
    public string QuarterText => _localizationService.GetString("Quarter") ?? "Quarter";
    public string HalfYearText => _localizationService.GetString("HalfYear") ?? "Half Year";
    public string YearText => _localizationService.GetString("Year") ?? "Year";
    public string TodayText => _localizationService.GetString("Today") ?? "Today";
    public string IncomeLabelText => _localizationService.GetString("IncomeTotalLabel") ?? "Income:";
    public string ExpensesLabelText => _localizationService.GetString("ExpensesTotalLabel") ?? "Expenses:";
    public string BalanceLabelText => _localizationService.GetString("BalanceTotalLabel") ?? "Balance:";
    public string NoStatsTitleText => _localizationService.GetString("EmptyStatsTitle") ?? "No Statistics";
    public string NoStatsDescText => _localizationService.GetString("EmptyStatsDesc") ?? "Add some transactions first to see your financial overview";
    public string ExpensesByCategoryText => _localizationService.GetString("ExpensesByCategory") ?? "Expenses by Category";
    public string CategoryBreakdownText => _localizationService.GetString("ByCategory") ?? "By Category";
    public string IncomeByCategoryText => _localizationService.GetString("IncomeByCategory") ?? "Income by Category";
    public string TrendOverviewText => _localizationService.GetString("TrendOverview") ?? "6-Month Trend";
    public string LastMonthText => _localizationService.GetString("LastMonth") ?? "Last Month";
    public string IncomeText => _localizationService.GetString("Income") ?? "Income";
    public string ExpensesText => _localizationService.GetString("Expenses") ?? "Expenses";
    public string ExportingPdfText => _localizationService.GetString("ExportStatistics") ?? "Exporting PDF...";
    public string ExportLockedText => _localizationService.GetString("ExportLocked") ?? "Unlock Export";
    public string ExportLockedDescText => _localizationService.GetString("ExportLockedDesc") ?? "Watch a short video to start your export.";
    public string WatchVideoExportText => _localizationService.GetString("WatchVideoExport") ?? "Watch Video → Export";
    public string ExtendedStatsTitleText => _localizationService.GetString("ExtendedStatsTitle") ?? "Extended Statistics";
    public string ExtendedStatsDescText => _localizationService.GetString("ExtendedStatsDesc") ?? "Watch a video for 24h access to quarterly and yearly statistics";
    public string AccessFor24hText => _localizationService.GetString("AccessFor24h") ?? "Access for 24 hours";

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(StatisticsTitleText));
        OnPropertyChanged(nameof(WeekText));
        OnPropertyChanged(nameof(MonthText));
        OnPropertyChanged(nameof(QuarterText));
        OnPropertyChanged(nameof(HalfYearText));
        OnPropertyChanged(nameof(YearText));
        OnPropertyChanged(nameof(TodayText));
        OnPropertyChanged(nameof(IncomeLabelText));
        OnPropertyChanged(nameof(ExpensesLabelText));
        OnPropertyChanged(nameof(BalanceLabelText));
        OnPropertyChanged(nameof(NoStatsTitleText));
        OnPropertyChanged(nameof(NoStatsDescText));
        OnPropertyChanged(nameof(ExpensesByCategoryText));
        OnPropertyChanged(nameof(CategoryBreakdownText));
        OnPropertyChanged(nameof(IncomeByCategoryText));
        OnPropertyChanged(nameof(TrendOverviewText));
        OnPropertyChanged(nameof(LastMonthText));
        OnPropertyChanged(nameof(IncomeText));
        OnPropertyChanged(nameof(ExpensesText));
        OnPropertyChanged(nameof(ExportingPdfText));
        OnPropertyChanged(nameof(ExportLockedText));
        OnPropertyChanged(nameof(ExportLockedDescText));
        OnPropertyChanged(nameof(WatchVideoExportText));
        OnPropertyChanged(nameof(ExtendedStatsTitleText));
        OnPropertyChanged(nameof(ExtendedStatsDescText));
        OnPropertyChanged(nameof(AccessFor24hText));
    }

    #endregion

    #region Time Period Selection

    [ObservableProperty]
    private TimePeriod _selectedPeriod;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _periodLabel = string.Empty;

    public bool IsWeekSelected => SelectedPeriod == TimePeriod.Week;
    public bool IsMonthSelected => SelectedPeriod == TimePeriod.Month;
    public bool IsQuarterSelected => SelectedPeriod == TimePeriod.Quarter;
    public bool IsHalfYearSelected => SelectedPeriod == TimePeriod.HalfYear;
    public bool IsYearSelected => SelectedPeriod == TimePeriod.Year;

    partial void OnSelectedPeriodChanged(TimePeriod value)
    {
        OnPropertyChanged(nameof(IsWeekSelected));
        OnPropertyChanged(nameof(IsMonthSelected));
        OnPropertyChanged(nameof(IsQuarterSelected));
        OnPropertyChanged(nameof(IsHalfYearSelected));
        OnPropertyChanged(nameof(IsYearSelected));

        // Quartal/Halbjahr/Jahr brauchen Extended Stats (Premium oder 24h-Zugang)
        if (value is TimePeriod.Quarter or TimePeriod.HalfYear or TimePeriod.Year
            && !_purchaseService.IsPremium && !IsExtendedStatsValid())
        {
            _pendingExtendedStatsPeriod = value;
            ShowExtendedStatsAdOverlay = true;
            // Zurueck auf Monat setzen ohne erneuten Trigger
            _selectedPeriod = TimePeriod.Month;
            OnPropertyChanged(nameof(SelectedPeriod));
            OnPropertyChanged(nameof(IsWeekSelected));
            OnPropertyChanged(nameof(IsMonthSelected));
            OnPropertyChanged(nameof(IsQuarterSelected));
            OnPropertyChanged(nameof(IsHalfYearSelected));
            OnPropertyChanged(nameof(IsYearSelected));
            return;
        }

        _ = LoadStatisticsWithErrorHandlingAsync();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadStatisticsWithErrorHandlingAsync();
    }

    /// <summary>
    /// Lädt Statistiken und fängt Fehler auf dem UI-Thread ab.
    /// </summary>
    private async Task LoadStatisticsWithErrorHandlingAsync()
    {
        try
        {
            await LoadStatisticsAsync();
            _isDataStale = false;
        }
        catch (Exception ex)
        {
            var errorTitle = _localizationService.GetString("Error") ?? "Error";
            MessageRequested?.Invoke(errorTitle, ex.Message);
        }
    }

    #endregion

    #region Statistics Data

    [ObservableProperty]
    private double _totalIncome;

    [ObservableProperty]
    private double _totalExpenses;

    [ObservableProperty]
    private double _balance;

    [ObservableProperty]
    private ObservableCollection<CategoryStatistic> _expensesByCategory = [];

    [ObservableProperty]
    private ObservableCollection<CategoryStatistic> _incomeByCategory = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _hasNoData;

    public bool HasExpenseData => ExpensesByCategory.Count > 0;
    public bool HasIncomeData => IncomeByCategory.Count > 0;

    // SkiaSharp Donut-Chart Segmente
    [ObservableProperty]
    private DonutChartVisualization.Segment[] _expenseDonutSegments = [];

    [ObservableProperty]
    private DonutChartVisualization.Segment[] _incomeDonutSegments = [];

    // SkiaSharp Trend-Chart Daten (6 Monate)
    [ObservableProperty]
    private string[] _trendMonthLabels = [];

    [ObservableProperty]
    private float[] _trendIncomeData = [];

    [ObservableProperty]
    private float[] _trendExpenseData = [];

    // Month comparison
    [ObservableProperty]
    private double _lastMonthExpenses;

    [ObservableProperty]
    private double _lastMonthIncome;

    [ObservableProperty]
    private string _expensesTrend = string.Empty;

    [ObservableProperty]
    private string _incomeTrend = string.Empty;

    public string LastMonthExpensesDisplay => CurrencyHelper.Format(LastMonthExpenses);
    public string LastMonthIncomeDisplay => CurrencyHelper.Format(LastMonthIncome);

    public string TotalIncomeDisplay => CurrencyHelper.Format(TotalIncome);
    public string TotalExpensesDisplay => CurrencyHelper.Format(TotalExpenses);
    public string BalanceDisplay => CurrencyHelper.Format(Balance);

    // 6-Monats-Durchschnitte (berechnet in LoadTrendDataAsync)
    [ObservableProperty]
    private double _avgMonthlyIncome;

    [ObservableProperty]
    private double _avgMonthlyExpense;

    public string AvgMonthlyIncomeDisplay => $"Ø {CurrencyHelper.Format(AvgMonthlyIncome)}";
    public string AvgMonthlyExpenseDisplay => $"Ø {CurrencyHelper.Format(AvgMonthlyExpense)}";

    // Farben via CategoryLocalizationHelper.GetCategoryColor() (5.1 zentralisiert)

    #endregion

    #region Commands

    [RelayCommand]
    private void SelectPeriod(string period)
    {
        SelectedPeriod = Enum.Parse<TimePeriod>(period);
    }

    [RelayCommand]
    private void PreviousPeriod()
    {
        SelectedDate = SelectedPeriod switch
        {
            TimePeriod.Week => SelectedDate.AddDays(-7),
            TimePeriod.Month => SelectedDate.AddMonths(-1),
            TimePeriod.Quarter => SelectedDate.AddMonths(-3),
            TimePeriod.HalfYear => SelectedDate.AddMonths(-6),
            TimePeriod.Year => SelectedDate.AddYears(-1),
            _ => SelectedDate
        };
    }

    [RelayCommand]
    private void NextPeriod()
    {
        SelectedDate = SelectedPeriod switch
        {
            TimePeriod.Week => SelectedDate.AddDays(7),
            TimePeriod.Month => SelectedDate.AddMonths(1),
            TimePeriod.Quarter => SelectedDate.AddMonths(3),
            TimePeriod.HalfYear => SelectedDate.AddMonths(6),
            TimePeriod.Year => SelectedDate.AddYears(1),
            _ => SelectedDate
        };
    }

    [RelayCommand]
    private void GoToToday()
    {
        SelectedDate = DateTime.Today;
    }

    [RelayCommand]
    public async Task LoadStatisticsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            // Ensure ExpenseService is initialized
            await _expenseService.InitializeAsync();

            var (startDate, endDate) = GetDateRange();
            PeriodLabel = GetPeriodLabel(startDate, endDate);
            OnPropertyChanged(nameof(PeriodLabel));

            var filter = new ExpenseFilter
            {
                StartDate = startDate,
                EndDate = endDate
            };

            var transactions = await _expenseService.GetExpensesAsync(filter);

            // Single-pass calculation: totals + categories grouped
            var expenseGroups = new Dictionary<ExpenseCategory, double>();
            var incomeGroups = new Dictionary<ExpenseCategory, double>();
            double totalExpenses = 0;
            double totalIncome = 0;

            foreach (var t in transactions)
            {
                if (t.Type == TransactionType.Expense)
                {
                    totalExpenses += t.Amount;
                    if (!expenseGroups.ContainsKey(t.Category))
                        expenseGroups[t.Category] = 0;
                    expenseGroups[t.Category] += t.Amount;
                }
                else // TransactionType.Income
                {
                    totalIncome += t.Amount;
                    if (!incomeGroups.ContainsKey(t.Category))
                        incomeGroups[t.Category] = 0;
                    incomeGroups[t.Category] += t.Amount;
                }
            }

            TotalExpenses = totalExpenses;
            TotalIncome = totalIncome;
            Balance = totalIncome - totalExpenses;

            OnPropertyChanged(nameof(TotalIncomeDisplay));
            OnPropertyChanged(nameof(TotalExpensesDisplay));
            OnPropertyChanged(nameof(BalanceDisplay));

            // Expenses by category (from Dictionary)
            var expenseCategories = expenseGroups
                .Select(kvp => new CategoryStatistic(
                    kvp.Key,
                    kvp.Value,
                    totalExpenses > 0 ? kvp.Value / totalExpenses : 0,
                    GetCategoryName(kvp.Key)))
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.CategoryName)
                .ToList();

            ExpensesByCategory = new ObservableCollection<CategoryStatistic>(expenseCategories);
            OnPropertyChanged(nameof(HasExpenseData));

            // Check if there's any data
            HasNoData = transactions.Count == 0;

            // Income by category (from Dictionary)
            var incomeCategories = incomeGroups
                .Select(kvp => new CategoryStatistic(
                    kvp.Key,
                    kvp.Value,
                    totalIncome > 0 ? kvp.Value / totalIncome : 0,
                    GetCategoryName(kvp.Key)))
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.CategoryName)
                .ToList();

            IncomeByCategory = new ObservableCollection<CategoryStatistic>(incomeCategories);
            OnPropertyChanged(nameof(HasIncomeData));

            // Update charts
            UpdateCharts(expenseCategories, incomeCategories);

            // Load trend data (6 months)
            await LoadTrendDataAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadTrendDataAsync()
    {
        var trendMonths = 6;
        var monthlyExpenses = new double[trendMonths];
        var monthlyIncomes = new double[trendMonths];
        var monthLabels = new string[trendMonths];

        var currentDate = DateTime.Today;

        // 1 Query statt 6: Gesamten 6-Monats-Zeitraum laden und lokal gruppieren
        var firstMonth = currentDate.AddMonths(-(trendMonths - 1));
        var (rangeStart, _) = GetMonthRange(firstMonth);
        var (_, rangeEnd) = GetMonthRange(currentDate);

        var allFilter = new ExpenseFilter { StartDate = rangeStart, EndDate = rangeEnd };
        var allTransactions = await _expenseService.GetExpensesAsync(allFilter);

        // Lokale Gruppierung nach Monat (Single-Pass)
        for (int i = 0; i < trendMonths; i++)
        {
            var monthDate = currentDate.AddMonths(-(trendMonths - 1 - i));
            monthLabels[i] = monthDate.ToString("MMM");
        }

        foreach (var t in allTransactions)
        {
            // Monats-Index bestimmen (0 = ältester, 5 = aktueller)
            int monthDiff = (t.Date.Year - rangeStart.Year) * 12 + (t.Date.Month - rangeStart.Month);
            if (monthDiff < 0 || monthDiff >= trendMonths) continue;

            if (t.Type == TransactionType.Expense)
                monthlyExpenses[monthDiff] += t.Amount;
            else
                monthlyIncomes[monthDiff] += t.Amount;
        }

        // 6-Monats-Durchschnitte berechnen
        AvgMonthlyIncome = monthlyIncomes.Average();
        AvgMonthlyExpense = monthlyExpenses.Average();
        OnPropertyChanged(nameof(AvgMonthlyIncomeDisplay));
        OnPropertyChanged(nameof(AvgMonthlyExpenseDisplay));

        // Last month comparison
        LastMonthExpenses = monthlyExpenses[trendMonths - 2];
        LastMonthIncome = monthlyIncomes[trendMonths - 2];
        OnPropertyChanged(nameof(LastMonthExpensesDisplay));
        OnPropertyChanged(nameof(LastMonthIncomeDisplay));

        // Calculate trend (this vs last month)
        var currentMonthExpenses = monthlyExpenses[trendMonths - 1];
        var currentMonthIncome = monthlyIncomes[trendMonths - 1];

        if (LastMonthExpenses > 0)
        {
            var expenseChange = ((currentMonthExpenses - LastMonthExpenses) / LastMonthExpenses) * 100;
            ExpensesTrend = expenseChange >= 0 ? $"+{expenseChange:F0}%" : $"{expenseChange:F0}%";
        }
        else
        {
            ExpensesTrend = currentMonthExpenses > 0 ? (_localizationService.GetString("New") ?? "Neu") : "—";
        }

        if (LastMonthIncome > 0)
        {
            var incomeChange = ((currentMonthIncome - LastMonthIncome) / LastMonthIncome) * 100;
            IncomeTrend = incomeChange >= 0 ? $"+{incomeChange:F0}%" : $"{incomeChange:F0}%";
        }
        else
        {
            IncomeTrend = currentMonthIncome > 0 ? (_localizationService.GetString("New") ?? "Neu") : "—";
        }

        // SkiaSharp Trend-Daten setzen (float[] statt LiveCharts ISeries)
        TrendMonthLabels = monthLabels;
        TrendIncomeData = monthlyIncomes.Select(v => (float)v).ToArray();
        TrendExpenseData = monthlyExpenses.Select(v => (float)v).ToArray();
    }

    private void UpdateCharts(List<CategoryStatistic> expenses, List<CategoryStatistic> incomes)
    {
        // Ausgaben-Donut als SkiaSharp-Segmente
        ExpenseDonutSegments = expenses.Select(c => new DonutChartVisualization.Segment
        {
            Value = (float)c.Amount,
            Color = GetCategoryColor(c.Category),
            Label = GetCategoryName(c.Category),
            ValueText = $"{c.Percentage * 100:F0}%"
        }).ToArray();

        // Einnahmen-Donut als SkiaSharp-Segmente
        IncomeDonutSegments = incomes.Select(c => new DonutChartVisualization.Segment
        {
            Value = (float)c.Amount,
            Color = GetCategoryColor(c.Category),
            Label = GetCategoryName(c.Category),
            ValueText = $"{c.Percentage * 100:F0}%"
        }).ToArray();
    }

    private static SKColor GetCategoryColor(ExpenseCategory category)
        => CategoryLocalizationHelper.GetCategoryColor(category);

    private string GetCategoryName(ExpenseCategory category)
        => CategoryLocalizationHelper.GetLocalizedName(category, _localizationService);

    #endregion

    #region Helper Methods

    private (DateTime start, DateTime end) GetDateRange()
    {
        return SelectedPeriod switch
        {
            TimePeriod.Week => GetWeekRange(SelectedDate),
            TimePeriod.Month => GetMonthRange(SelectedDate),
            TimePeriod.Quarter => GetQuarterRange(SelectedDate),
            TimePeriod.HalfYear => GetHalfYearRange(SelectedDate),
            TimePeriod.Year => GetYearRange(SelectedDate),
            _ => GetMonthRange(SelectedDate)
        };
    }

    private static (DateTime start, DateTime end) GetWeekRange(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var startOfWeek = date.AddDays(-(dayOfWeek == 0 ? 6 : dayOfWeek - 1));
        return (startOfWeek, startOfWeek.AddDays(6));
    }

    private static (DateTime start, DateTime end) GetMonthRange(DateTime date)
    {
        var start = new DateTime(date.Year, date.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return (start, end);
    }

    private static (DateTime start, DateTime end) GetQuarterRange(DateTime date)
    {
        var quarter = (date.Month - 1) / 3;
        var startMonth = quarter * 3 + 1;
        var start = new DateTime(date.Year, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return (start, end);
    }

    private static (DateTime start, DateTime end) GetHalfYearRange(DateTime date)
    {
        var startMonth = date.Month <= 6 ? 1 : 7;
        var start = new DateTime(date.Year, startMonth, 1);
        var end = start.AddMonths(6).AddDays(-1);
        return (start, end);
    }

    private static (DateTime start, DateTime end) GetYearRange(DateTime date)
    {
        var start = new DateTime(date.Year, 1, 1);
        var end = new DateTime(date.Year, 12, 31);
        return (start, end);
    }

    private string GetPeriodLabel(DateTime start, DateTime end)
    {
        return SelectedPeriod switch
        {
            TimePeriod.Week => $"{_localizationService.GetString("CalendarWeekAbbreviation") ?? "CW"} {GetWeekNumber(start)} - {start:dd.MM} - {end:dd.MM.yyyy}",
            TimePeriod.Month => start.ToString("MMMM yyyy"),
            TimePeriod.Quarter => $"Q{((start.Month - 1) / 3) + 1} {start.Year}",
            TimePeriod.HalfYear => start.Month == 1 ? $"H1 {start.Year}" : $"H2 {start.Year}",
            TimePeriod.Year => start.Year.ToString(),
            _ => $"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}"
        };
    }

    private static int GetWeekNumber(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return culture.Calendar.GetWeekOfYear(date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }

    #endregion

    #region Export Ad Gate

    [ObservableProperty]
    private bool _showExportAdOverlay;

    /// <summary>
    /// Merkt sich welcher Export-Typ angefragt wurde ("pdf" oder "csv")
    /// </summary>
    private string _pendingExportType = "";

    [RelayCommand]
    private async Task ConfirmExportAdAsync()
    {
        ShowExportAdOverlay = false;

        // Placement je nach Export-Typ
        var placement = _pendingExportType == "csv" ? "export_csv" : "export_pdf";
        var success = await _rewardedAdService.ShowAdAsync(placement);
        if (success)
        {
            // Nach erfolgreicher Ad den gemerkten Export ausfuehren
            if (_pendingExportType == "pdf")
                await DoExportToPdfAsync();
            else if (_pendingExportType == "csv")
                await DoExportToCsvAsync();
        }
        else
        {
            var msg = _localizationService.GetString("ExportAdFailed") ?? "Could not load video";
            _ = ShowExportStatusAsync(msg);
        }
        _pendingExportType = "";
    }

    [RelayCommand]
    private void CancelExportAd()
    {
        ShowExportAdOverlay = false;
        _pendingExportType = "";
    }

    #endregion

    #region Extended Stats Ad Gate

    [ObservableProperty]
    private bool _showExtendedStatsAdOverlay;

    private TimePeriod _pendingExtendedStatsPeriod;

    /// <summary>
    /// Prueft ob der 24h-Zugang fuer erweiterte Statistiken noch gueltig ist.
    /// </summary>
    private bool IsExtendedStatsValid()
    {
        var expiryStr = _preferencesService.Get<string>(ExtendedStatsExpiryKey, "");
        if (string.IsNullOrEmpty(expiryStr)) return false;

        if (DateTime.TryParse(expiryStr, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var expiry))
        {
            return DateTime.UtcNow < expiry;
        }
        return false;
    }

    [RelayCommand]
    private async Task ConfirmExtendedStatsAdAsync()
    {
        ShowExtendedStatsAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("extended_stats");
        if (success)
        {
            // 24h Zugang speichern
            var expiry = DateTime.UtcNow.AddHours(24).ToString("O");
            _preferencesService.Set(ExtendedStatsExpiryKey, expiry);

            // Gewaehlten Zeitraum anwenden
            SelectedPeriod = _pendingExtendedStatsPeriod;
        }
        else
        {
            var msg = _localizationService.GetString("ExportAdFailed") ?? "Could not load video";
            _ = ShowExportStatusAsync(msg);
        }
    }

    [RelayCommand]
    private void CancelExtendedStatsAd()
    {
        ShowExtendedStatsAdOverlay = false;
    }

    #endregion

    #region CSV Export

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (IsExporting) return;

        // Premium: Direkt exportieren. Free: Ad-Overlay anzeigen.
        if (_purchaseService.IsPremium)
        {
            await DoExportToCsvAsync();
            return;
        }

        _pendingExportType = "csv";
        ShowExportAdOverlay = true;
    }

    private async Task DoExportToCsvAsync()
    {
        if (IsExporting) return;

        try
        {
            IsExporting = true;

            var (startDate, endDate) = GetDateRange();
            var suggestedName = $"statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var title = _localizationService.GetString("ExportStatistics") ?? "Export Statistics";

            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            var filePath = await _exportService.ExportToCsvAsync(
                startDate, endDate, targetPath);

            await _fileShareService.ShareFileAsync(filePath, title, "text/csv");

            var successMsg = _localizationService.GetString("ExportSuccess") ?? "Export successful";
            _ = ShowExportStatusAsync($"{successMsg}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"{_localizationService.GetString("ExportError") ?? "Export failed"}: {ex.Message}";
            _ = ShowExportStatusAsync(errorMsg);
        }
        finally
        {
            IsExporting = false;
        }
    }

    #endregion

    #region PDF Export

    [ObservableProperty]
    private string? _exportStatusMessage;

    [ObservableProperty]
    private bool _isExportStatusVisible;

    private CancellationTokenSource? _statusCts;

    private async Task ShowExportStatusAsync(string message)
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = new CancellationTokenSource();
        var token = _statusCts.Token;

        ExportStatusMessage = message;
        IsExportStatusVisible = true;

        try
        {
            await Task.Delay(4000, token);
            IsExportStatusVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    [RelayCommand]
    private async Task ExportToPdfAsync()
    {
        if (IsExporting) return;

        // Premium: Direkt exportieren. Free: Ad-Overlay anzeigen.
        if (_purchaseService.IsPremium)
        {
            await DoExportToPdfAsync();
            return;
        }

        // Free User: Overlay anzeigen
        _pendingExportType = "pdf";
        ShowExportAdOverlay = true;
    }

    /// <summary>
    /// Fuehrt den eigentlichen PDF-Export durch (nach Premium-Check oder Ad)
    /// </summary>
    private async Task DoExportToPdfAsync()
    {
        if (IsExporting) return;

        try
        {
            var title = _localizationService.GetString("ExportStatistics") ?? "Export Statistics";
            var suggestedName = $"statistics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

            // Desktop: FileDialog, Android: direkt in Export-Verzeichnis
            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "PDF", "pdf");
            if (targetPath == null)
            {
                // Fallback fuer Android (FileDialog gibt null zurueck)
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            IsExporting = true;
            var (pdfStartDate, pdfEndDate) = GetDateRange();
            var filePath = await _exportService.ExportStatisticsToPdfAsync(PeriodLabel, pdfStartDate, pdfEndDate, targetPath);

            // Datei teilen/oeffnen
            await _fileShareService.ShareFileAsync(filePath, title, "application/pdf");

            var successMsg = _localizationService.GetString("ExportSuccess") ?? "Export successful";
            _ = ShowExportStatusAsync($"{successMsg}: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            var errorMsg = $"{_localizationService.GetString("ExportError") ?? "Export failed"}: {ex.Message}";
            _ = ShowExportStatusAsync(errorMsg);
        }
        finally
        {
            IsExporting = false;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _statusCts?.Cancel();
        _statusCts?.Dispose();
        _statusCts = null;
    }

    #endregion
}

/// <summary>
/// Time period selection for statistics
/// </summary>
public enum TimePeriod
{
    Week,
    Month,
    Quarter,
    HalfYear,
    Year
}

/// <summary>
/// Statistics for a category
/// </summary>
public record CategoryStatistic(
    ExpenseCategory Category,
    double Amount,
    double Percentage,
    string CategoryName)
{
    public string AmountDisplay => CurrencyHelper.Format(Amount);
    public string PercentageDisplay => $"{Percentage * 100:F1}%";
    public double BarHeight => Percentage * 200; // Max 200 pixel
    public string CategoryIcon => CategoryLocalizationHelper.GetCategoryIcon(Category);
}
