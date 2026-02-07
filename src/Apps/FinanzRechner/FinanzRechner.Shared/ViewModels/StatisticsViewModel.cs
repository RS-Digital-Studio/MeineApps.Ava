using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using SkiaSharp;

namespace FinanzRechner.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly IExpenseService _expenseService;
    private readonly IExportService _exportService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IFileShareService _fileShareService;

    public event Action<string, string>? MessageRequested;

    public StatisticsViewModel(IExpenseService expenseService, IExportService exportService, ILocalizationService localizationService, IThemeService themeService, IFileDialogService fileDialogService, IFileShareService fileShareService)
    {
        _expenseService = expenseService;
        _exportService = exportService;
        _localizationService = localizationService;
        _themeService = themeService;
        _fileDialogService = fileDialogService;
        _fileShareService = fileShareService;
        _selectedPeriod = TimePeriod.Month;
    }

    /// <summary>
    /// Wird beim Tab-Wechsel zur Statistik aufgerufen.
    /// </summary>
    public async Task OnAppearingAsync()
    {
        await LoadStatisticsAsync();
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

        LoadStatisticsAsync().ContinueWith(t =>
        {
            if (t.IsFaulted) MessageRequested?.Invoke("Error", t.Exception?.Message ?? "Unknown error");
        }, TaskScheduler.Default);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        LoadStatisticsAsync().ContinueWith(t =>
        {
            if (t.IsFaulted) MessageRequested?.Invoke("Error", t.Exception?.Message ?? "Unknown error");
        }, TaskScheduler.Default);
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
    private bool _isExportingPdf;

    [ObservableProperty]
    private bool _hasNoData;

    public bool HasExpenseData => ExpensesByCategory.Count > 0;
    public bool HasIncomeData => IncomeByCategory.Count > 0;

    // LiveCharts PieChart Series
    [ObservableProperty]
    private IEnumerable<ISeries> _expenseChartSeries = [];

    [ObservableProperty]
    private IEnumerable<ISeries> _incomeChartSeries = [];

    // LineChart Trend (6 months)
    [ObservableProperty]
    private IEnumerable<ISeries> _trendChartSeries = [];

    [ObservableProperty]
    private Axis[] _trendXAxes = [];

    [ObservableProperty]
    private Axis[] _trendYAxes = [];

    // Month comparison
    [ObservableProperty]
    private double _lastMonthExpenses;

    [ObservableProperty]
    private double _lastMonthIncome;

    [ObservableProperty]
    private string _expensesTrend = string.Empty;

    [ObservableProperty]
    private string _incomeTrend = string.Empty;

    public string LastMonthExpensesDisplay => $"{LastMonthExpenses:N2} \u20ac";
    public string LastMonthIncomeDisplay => $"{LastMonthIncome:N2} \u20ac";

    public string TotalIncomeDisplay => $"{TotalIncome:N2} \u20ac";
    public string TotalExpensesDisplay => $"{TotalExpenses:N2} \u20ac";
    public string BalanceDisplay => $"{Balance:N2} \u20ac";

    // Colors for expense categories
    private static readonly Dictionary<ExpenseCategory, SKColor> ExpenseCategoryColors = new()
    {
        { ExpenseCategory.Food, new SKColor(0xFF, 0x98, 0x00) },           // Orange
        { ExpenseCategory.Transport, new SKColor(0x21, 0x96, 0xF3) },      // Blue
        { ExpenseCategory.Housing, new SKColor(0x9C, 0x27, 0xB0) },        // Purple
        { ExpenseCategory.Entertainment, new SKColor(0xE9, 0x1E, 0x63) },  // Pink
        { ExpenseCategory.Shopping, new SKColor(0x00, 0xBC, 0xD4) },       // Cyan
        { ExpenseCategory.Health, new SKColor(0xF4, 0x43, 0x36) },         // Red
        { ExpenseCategory.Education, new SKColor(0x3F, 0x51, 0xB5) },      // Indigo
        { ExpenseCategory.Bills, new SKColor(0x60, 0x7D, 0x8B) },          // Blue-grey
        { ExpenseCategory.Other, new SKColor(0x79, 0x55, 0x48) },          // Brown
        { ExpenseCategory.Salary, new SKColor(0x4C, 0xAF, 0x50) },         // Green
        { ExpenseCategory.Freelance, new SKColor(0x00, 0x96, 0x88) },      // Teal
        { ExpenseCategory.Investment, new SKColor(0x8B, 0xC3, 0x4A) },     // Light green
        { ExpenseCategory.Gift, new SKColor(0xFF, 0xC1, 0x07) },           // Amber
        { ExpenseCategory.OtherIncome, new SKColor(0xCD, 0xDC, 0x39) }     // Lime
    };

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
                    totalExpenses > 0 ? kvp.Value / totalExpenses : 0))
                .OrderByDescending(c => c.Amount)
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
                    totalIncome > 0 ? kvp.Value / totalIncome : 0))
                .OrderByDescending(c => c.Amount)
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

        for (int i = trendMonths - 1; i >= 0; i--)
        {
            var monthDate = currentDate.AddMonths(-(trendMonths - 1 - i));
            var (startDate, endDate) = GetMonthRange(monthDate);

            var filter = new ExpenseFilter { StartDate = startDate, EndDate = endDate };
            var transactions = await _expenseService.GetExpensesAsync(filter);

            // Single-pass calculation for both expenses and income
            double expenses = 0, income = 0;
            foreach (var t in transactions)
            {
                if (t.Type == TransactionType.Expense)
                    expenses += t.Amount;
                else
                    income += t.Amount;
            }
            monthlyExpenses[i] = expenses;
            monthlyIncomes[i] = income;
            monthLabels[i] = monthDate.ToString("MMM");
        }

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
            ExpensesTrend = currentMonthExpenses > 0 ? "+\u221e" : "0%";
        }

        if (LastMonthIncome > 0)
        {
            var incomeChange = ((currentMonthIncome - LastMonthIncome) / LastMonthIncome) * 100;
            IncomeTrend = incomeChange >= 0 ? $"+{incomeChange:F0}%" : $"{incomeChange:F0}%";
        }
        else
        {
            IncomeTrend = currentMonthIncome > 0 ? "+\u221e" : "0%";
        }

        // Determine label color based on theme (light vs dark)
        var labelColor = _themeService.IsDarkTheme
            ? new SKColor(0xFF, 0xFF, 0xFF)
            : new SKColor(0x21, 0x21, 0x21);

        // Create LineChart series
        TrendChartSeries =
        [
            new LineSeries<double>
            {
                Values = monthlyIncomes,
                Name = _localizationService.GetString("Income") ?? "Income",
                Stroke = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50)) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50)) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(new SKColor(0x4C, 0xAF, 0x50)),
                GeometrySize = 8,
                Fill = null
            },
            new LineSeries<double>
            {
                Values = monthlyExpenses,
                Name = _localizationService.GetString("Expenses") ?? "Expenses",
                Stroke = new SolidColorPaint(new SKColor(0xF4, 0x43, 0x36)) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(new SKColor(0xF4, 0x43, 0x36)) { StrokeThickness = 3 },
                GeometryFill = new SolidColorPaint(new SKColor(0xF4, 0x43, 0x36)),
                GeometrySize = 8,
                Fill = null
            }
        ];

        TrendXAxes =
        [
            new Axis
            {
                Labels = monthLabels,
                LabelsPaint = new SolidColorPaint(labelColor),
                TextSize = 12
            }
        ];

        TrendYAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(labelColor),
                TextSize = 12,
                Labeler = value => $"{value:N0} \u20ac"
            }
        ];
    }

    private void UpdateCharts(List<CategoryStatistic> expenses, List<CategoryStatistic> incomes)
    {
        // Determine label color based on theme (light vs dark)
        var labelColor = _themeService.IsDarkTheme
            ? new SKColor(0xFF, 0xFF, 0xFF)
            : new SKColor(0x21, 0x21, 0x21);

        // Expenses PieChart
        ExpenseChartSeries = expenses.Select(c => new PieSeries<double>
        {
            Values = [c.Amount],
            Name = GetCategoryName(c.Category),
            Fill = new SolidColorPaint(GetCategoryColor(c.Category)),
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsPaint = new SolidColorPaint(labelColor),
            DataLabelsFormatter = point => $"{c.Percentage * 100:F0}%",
            DataLabelsSize = 12
        }).ToArray();

        // Income PieChart
        IncomeChartSeries = incomes.Select(c => new PieSeries<double>
        {
            Values = [c.Amount],
            Name = GetCategoryName(c.Category),
            Fill = new SolidColorPaint(GetCategoryColor(c.Category)),
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsPaint = new SolidColorPaint(labelColor),
            DataLabelsFormatter = point => $"{c.Percentage * 100:F0}%",
            DataLabelsSize = 12
        }).ToArray();
    }

    private static SKColor GetCategoryColor(ExpenseCategory category)
    {
        return ExpenseCategoryColors.TryGetValue(category, out var color)
            ? color
            : new SKColor(0x9E, 0x9E, 0x9E); // Grey as fallback
    }

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
            TimePeriod.Week => $"KW {GetWeekNumber(start)} - {start:dd.MM} bis {end:dd.MM.yyyy}",
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

    #region PDF Export

    [ObservableProperty]
    private string? _exportStatusMessage;

    [ObservableProperty]
    private bool _isExportStatusVisible;

    private CancellationTokenSource? _statusCts;

    private async Task ShowExportStatusAsync(string message)
    {
        _statusCts?.Cancel();
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
        if (IsExportingPdf) return;

        try
        {
            var title = _localizationService.GetString("ExportStatistics") ?? "Export Statistics";
            var suggestedName = $"statistics_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            // Desktop: FileDialog, Android: direkt in Export-Verzeichnis
            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "PDF", "pdf");
            if (targetPath == null)
            {
                // Fallback fuer Android (FileDialog gibt null zurueck)
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            IsExportingPdf = true;
            var filePath = await _exportService.ExportStatisticsToPdfAsync(PeriodLabel, targetPath);

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
            IsExportingPdf = false;
        }
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
    double Percentage)
{
    public string AmountDisplay => $"{Amount:N2} \u20ac";
    public string PercentageDisplay => $"{Percentage * 100:F1}%";
    public double BarHeight => Percentage * 200; // Max 200 pixel
    public string CategoryName => Category.ToString();
    public string CategoryIcon => Category switch
    {
        ExpenseCategory.Food => "\U0001F354",
        ExpenseCategory.Transport => "\U0001F697",
        ExpenseCategory.Housing => "\U0001F3E0",
        ExpenseCategory.Entertainment => "\U0001F3AC",
        ExpenseCategory.Shopping => "\U0001F6D2",
        ExpenseCategory.Health => "\U0001F48A",
        ExpenseCategory.Education => "\U0001F4DA",
        ExpenseCategory.Bills => "\U0001F4C4",
        ExpenseCategory.Salary => "\U0001F4B0",
        ExpenseCategory.Freelance => "\U0001F4BC",
        ExpenseCategory.Investment => "\U0001F4C8",
        ExpenseCategory.Gift => "\U0001F381",
        ExpenseCategory.OtherIncome => "\U0001F4B5",
        _ => "\U0001F4E6"
    };
}
