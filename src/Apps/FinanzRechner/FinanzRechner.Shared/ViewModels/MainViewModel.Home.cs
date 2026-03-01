using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.UI.SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.Helpers;
using FinanzRechner.Models;

// Home-Dashboard Logik: Dashboard-Properties, Budget-Status, Recent Transactions,
// Expense-Chart, Sparkline, Mini-Ringe, Quick-Add, Budget-Analyse

namespace FinanzRechner.ViewModels;

public partial class MainViewModel
{
    #region Dashboard

    [ObservableProperty]
    private double _monthlyIncome;

    [ObservableProperty]
    private double _monthlyExpenses;

    [ObservableProperty]
    private double _monthlyBalance;

    [ObservableProperty]
    private bool _hasTransactions;

    public string MonthlyIncomeDisplay => CurrencyHelper.FormatSigned(MonthlyIncome);
    // Kein Minus-Prefix bei 0,00 (verhindert "-0,00 €")
    public string MonthlyExpensesDisplay => MonthlyExpenses > 0
        ? $"-{CurrencyHelper.Format(MonthlyExpenses)}"
        : CurrencyHelper.Format(0);
    public string MonthlyBalanceDisplay => CurrencyHelper.FormatSigned(MonthlyBalance);
    public string CurrentMonthDisplay => DateTime.Today.ToString("MMMM yyyy");
    public bool IsBalancePositive => MonthlyBalance >= 0;

    #endregion

    #region Budget Status

    [ObservableProperty]
    private bool _hasBudgets;

    [ObservableProperty]
    private double _overallBudgetPercentage;

    [ObservableProperty]
    private ObservableCollection<BudgetDisplayItem> _topBudgets = [];

    private void UpdateBudgetDisplayNames()
    {
        foreach (var b in TopBudgets)
            b.CategoryName = CategoryLocalizationHelper.GetLocalizedName(b.Category, _localizationService);
    }

    #endregion

    #region Recent Transactions

    [ObservableProperty]
    private bool _hasRecentTransactions;

    [ObservableProperty]
    private ObservableCollection<Expense> _recentTransactions = [];

    #endregion

    #region Home Expense Chart (Mini-Donut via SkiaSharp)

    [ObservableProperty]
    private DonutChartVisualization.Segment[] _homeExpenseSegments = [];

    [ObservableProperty]
    private bool _hasHomeChartData;

    /// <summary>
    /// Baut Mini-Donut-Segmente für HomeView aus den Monats-Ausgaben auf.
    /// </summary>
    private void BuildHomeExpenseChart(List<Expense> monthExpenses)
    {
        var expensesByCategory = monthExpenses
            .Where(e => e.Type == TransactionType.Expense)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .Take(6) // Top 6 Kategorien für übersichtliches Donut
            .ToList();

        if (expensesByCategory.Count == 0)
        {
            HasHomeChartData = false;
            HomeExpenseSegments = [];
            return;
        }

        HasHomeChartData = true;
        HomeExpenseSegments = expensesByCategory.Select(c => new DonutChartVisualization.Segment
        {
            Value = (float)c.Amount,
            Color = CategoryLocalizationHelper.GetCategoryColor(c.Category),
            Label = CategoryLocalizationHelper.GetLocalizedName(c.Category, _localizationService),
            ValueText = $"{c.Amount:N0} €"
        }).ToArray();
    }

    #endregion

    #region Sparkline (30-Tage-Ausgaben-Trend)

    [ObservableProperty]
    private float[]? _sparklineValues;

    [ObservableProperty]
    private string? _sparklineTrendLabel;

    [ObservableProperty]
    private bool _hasSparklineData;

    /// <summary>
    /// Baut Sparkline-Daten aus den letzten 30 Tagen auf.
    /// </summary>
    private void BuildSparklineData(List<Expense> monthExpenses)
    {
        var today = DateTime.Today;
        var days = new float[30];

        // Ausgaben pro Tag summieren (letzte 30 Tage)
        foreach (var e in monthExpenses.Where(e => e.Type == TransactionType.Expense))
        {
            int daysAgo = (today - e.Date.Date).Days;
            if (daysAgo >= 0 && daysAgo < 30)
                days[29 - daysAgo] += (float)e.Amount;
        }

        // Prüfen ob überhaupt Daten vorhanden
        float total = days.Sum();
        if (total < 0.01f)
        {
            HasSparklineData = false;
            SparklineValues = null;
            SparklineTrendLabel = null;
            return;
        }

        HasSparklineData = true;
        SparklineValues = days;

        // Trend-Label: Vergleich letzte 7 Tage vs. vorherige 7 Tage
        float recent7 = days[23..].Sum();
        float prev7 = days[16..23].Sum();
        if (prev7 > 0.01f)
        {
            double changePercent = ((recent7 - prev7) / prev7) * 100;
            SparklineTrendLabel = changePercent >= 0 ? $"+{changePercent:F0}%" : $"{changePercent:F0}%";
        }
        else
        {
            SparklineTrendLabel = null;
        }
    }

    #endregion

    #region Budget Mini-Ringe

    [ObservableProperty]
    private BudgetMiniRingVisualization.BudgetRingData[]? _budgetRings;

    [ObservableProperty]
    private bool _hasBudgetRings;

    /// <summary>
    /// Baut Mini-Ring-Daten aus den Budget-Status auf.
    /// </summary>
    private void BuildBudgetRings(IReadOnlyList<BudgetStatus> budgetStatuses)
    {
        if (budgetStatuses.Count == 0)
        {
            HasBudgetRings = false;
            BudgetRings = null;
            return;
        }

        HasBudgetRings = true;
        BudgetRings = budgetStatuses
            .OrderByDescending(b => b.PercentageUsed)
            .Take(5) // Max 5 Ringe
            .Select(b => new BudgetMiniRingVisualization.BudgetRingData(
                CategoryLocalizationHelper.GetLocalizedName(b.Category, _localizationService),
                (float)b.PercentageUsed,
                CategoryLocalizationHelper.GetCategoryColor(b.Category)))
            .ToArray();
    }

    #endregion

    #region Quick Add

    [ObservableProperty]
    private bool _showQuickAdd;

    [ObservableProperty]
    private string _quickAddAmount = string.Empty;

    [ObservableProperty]
    private string _quickAddDescription = string.Empty;

    [ObservableProperty]
    private ExpenseCategory _quickAddCategory = ExpenseCategory.Other;

    [ObservableProperty]
    private TransactionType _quickAddType = TransactionType.Expense;

    public bool IsQuickExpenseSelected => QuickAddType == TransactionType.Expense;
    public bool IsQuickIncomeSelected => QuickAddType == TransactionType.Income;

    private static readonly List<ExpenseCategory> QuickExpenseCategories =
    [
        ExpenseCategory.Food,
        ExpenseCategory.Transport,
        ExpenseCategory.Shopping,
        ExpenseCategory.Entertainment,
        ExpenseCategory.Bills,
        ExpenseCategory.Health,
        ExpenseCategory.Other
    ];

    private static readonly List<ExpenseCategory> QuickIncomeCategories =
    [
        ExpenseCategory.Salary,
        ExpenseCategory.Freelance,
        ExpenseCategory.Investment,
        ExpenseCategory.Gift,
        ExpenseCategory.OtherIncome
    ];

    [ObservableProperty]
    private ObservableCollection<CategoryDisplayItem> _quickCategoryItems = [];

    private void UpdateQuickCategoryItems()
    {
        var categories = QuickAddType == TransactionType.Expense
            ? QuickExpenseCategories
            : QuickIncomeCategories;

        var items = new ObservableCollection<CategoryDisplayItem>();
        foreach (var cat in categories)
        {
            items.Add(new CategoryDisplayItem
            {
                Category = cat,
                CategoryName = CategoryLocalizationHelper.GetLocalizedName(cat, _localizationService),
                IsSelected = cat == QuickAddCategory
            });
        }
        QuickCategoryItems = items;
    }

    partial void OnQuickAddTypeChanged(TransactionType value)
    {
        OnPropertyChanged(nameof(IsQuickExpenseSelected));
        OnPropertyChanged(nameof(IsQuickIncomeSelected));
        QuickAddCategory = value == TransactionType.Expense ? ExpenseCategory.Other : ExpenseCategory.Salary;
        UpdateQuickCategoryItems();
    }

    public string QuickAddTitleText => _localizationService.GetString("QuickAddTitle") ?? "Quick Add";
    public string QuickAddAmountPlaceholder => _localizationService.GetString("Amount") ?? "Amount";
    public string QuickAddDescriptionPlaceholder => _localizationService.GetString("Description") ?? "Description";
    public string QuickExpenseText => _localizationService.GetString("Expense") ?? "Expense";
    public string QuickIncomeText => _localizationService.GetString("Income") ?? "Income";
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";

    [RelayCommand]
    private void SetQuickTypeExpense() => QuickAddType = TransactionType.Expense;

    [RelayCommand]
    private void SetQuickTypeIncome() => QuickAddType = TransactionType.Income;

    [RelayCommand]
    private void SelectQuickCategory(CategoryDisplayItem item)
    {
        foreach (var cat in QuickCategoryItems)
            cat.IsSelected = false;
        item.IsSelected = true;
        QuickAddCategory = item.Category;
    }

    #endregion

    #region Home Daten laden

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        UpdateNavTexts();

        // Fällige Daueraufträge verarbeiten (bei jedem App-Start)
        try
        {
            await _expenseService.ProcessDueRecurringTransactionsAsync();
        }
        catch (Exception)
        {
            // Fehler beim Verarbeiten der Daueraufträge ignorieren
        }

        await LoadMonthlyDataAsync();
        _isHomeDataStale = false;
    }

    private async Task LoadMonthlyDataAsync()
    {
        try
        {
            var today = DateTime.Today;
            var summary = await _expenseService.GetMonthSummaryAsync(today.Year, today.Month);

            MonthlyIncome = summary.TotalIncome;
            MonthlyExpenses = summary.TotalExpenses;
            MonthlyBalance = summary.Balance;
            HasTransactions = summary.TotalExpenses > 0 || summary.TotalIncome > 0;

            OnPropertyChanged(nameof(MonthlyIncomeDisplay));
            OnPropertyChanged(nameof(MonthlyExpensesDisplay));
            OnPropertyChanged(nameof(MonthlyBalanceDisplay));
            OnPropertyChanged(nameof(IsBalancePositive));

            // Budget-Status laden
            await LoadBudgetStatusAsync();

            // Letzte 3 Transaktionen laden
            await LoadRecentTransactionsAsync(today);
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                ex.Message);
            MonthlyIncome = 0;
            MonthlyExpenses = 0;
            MonthlyBalance = 0;
            HasTransactions = false;
        }
    }

    private async Task LoadBudgetStatusAsync()
    {
        try
        {
            var budgetStatuses = await _expenseService.GetAllBudgetStatusAsync();
            HasBudgets = budgetStatuses.Count > 0;

            if (HasBudgets)
            {
                OverallBudgetPercentage = budgetStatuses.Average(b => b.PercentageUsed);

                var top3 = budgetStatuses
                    .OrderByDescending(b => b.PercentageUsed)
                    .Take(3)
                    .Select(b => new BudgetDisplayItem
                    {
                        Category = b.Category,
                        CategoryName = CategoryLocalizationHelper.GetLocalizedName(b.Category, _localizationService),
                        Percentage = b.PercentageUsed,
                        AlertLevel = b.AlertLevel
                    });

                TopBudgets = new ObservableCollection<BudgetDisplayItem>(top3);

                // Mini-Ringe für Budget-Übersicht aufbauen
                BuildBudgetRings(budgetStatuses);
            }
            else
            {
                TopBudgets.Clear();
                OverallBudgetPercentage = 0;
                HasBudgetRings = false;
                BudgetRings = null;
            }
        }
        catch (Exception)
        {
            HasBudgets = false;
        }
    }

    private async Task LoadRecentTransactionsAsync(DateTime today)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByMonthAsync(today.Year, today.Month);
            var expenseList = expenses.ToList();

            var recent = expenseList
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .Take(3)
                .ToList();

            HasRecentTransactions = recent.Count > 0;
            RecentTransactions = new ObservableCollection<Expense>(recent);

            // Mini-Donut-Chart für HomeView aufbauen
            BuildHomeExpenseChart(expenseList);

            // Sparkline für 30-Tage-Trend aufbauen:
            // Am Monatsanfang (Tag <= 30) fehlen Vormonats-Daten,
            // daher zusätzlich den Vormonat laden und zusammenführen
            var sparklineExpenses = expenseList;
            if (today.Day < 30)
            {
                var prevMonth = today.AddMonths(-1);
                var prevExpenses = await _expenseService.GetExpensesByMonthAsync(prevMonth.Year, prevMonth.Month);
                sparklineExpenses = prevExpenses.Concat(expenseList).ToList();
            }
            BuildSparklineData(sparklineExpenses);
        }
        catch (Exception)
        {
            HasRecentTransactions = false;
            HasHomeChartData = false;
        }
    }

    #endregion

    #region Budget Analysis Report

    [ObservableProperty]
    private bool _showBudgetAdOverlay;

    [ObservableProperty]
    private bool _showBudgetAnalysisOverlay;

    [ObservableProperty]
    private BudgetAnalysisReport? _budgetAnalysisReport;

    private CancellationTokenSource? _budgetAnalysisCts;

    public string MonthlyReportText => _localizationService.GetString("MonthlyReport") ?? "Monthly Report";
    public string BudgetAnalysisTitleText => _localizationService.GetString("BudgetAnalysisTitle") ?? "Budget Analysis";
    public string BudgetAnalysisDescText => _localizationService.GetString("BudgetAnalysisDesc") ?? "Watch a video to see your detailed monthly report.";
    public string SavingTipText => _localizationService.GetString("SavingTip") ?? "Saving Tip";
    public string ComparedToLastMonthText => _localizationService.GetString("ComparedToLastMonth") ?? "Compared to last month";
    public string WatchVideoReportText => _localizationService.GetString("WatchVideoExport") ?? "Watch Video → Report";
    public string CloseText => _localizationService.GetString("Cancel") ?? "Close";

    [RelayCommand]
    private async Task RequestBudgetAnalysisAsync()
    {
        // Premium: Report direkt generieren und anzeigen
        if (_purchaseService.IsPremium)
        {
            await GenerateAndShowBudgetAnalysis();
            return;
        }

        // Free: Ad-Overlay anzeigen
        ShowBudgetAdOverlay = true;
    }

    [RelayCommand]
    private async Task ConfirmBudgetAdAsync()
    {
        ShowBudgetAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("budget_analysis");
        if (success)
        {
            await GenerateAndShowBudgetAnalysis();
        }
        else
        {
            var msg = _localizationService.GetString("ExportAdFailed") ?? "Could not load video";
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                msg);
        }
    }

    [RelayCommand]
    private void CancelBudgetAd()
    {
        ShowBudgetAdOverlay = false;
    }

    [RelayCommand]
    private void CloseBudgetAnalysis()
    {
        _budgetAnalysisCts?.Cancel();
        _budgetAnalysisCts?.Dispose();
        _budgetAnalysisCts = null;
        ShowBudgetAnalysisOverlay = false;
        BudgetAnalysisReport = null;
    }

    private async Task GenerateAndShowBudgetAnalysis()
    {
        // Vorherige Generierung abbrechen falls noch aktiv
        _budgetAnalysisCts?.Cancel();
        _budgetAnalysisCts?.Dispose();
        _budgetAnalysisCts = new CancellationTokenSource();
        var ct = _budgetAnalysisCts.Token;

        try
        {
            var today = DateTime.Today;
            var startDate = new DateTime(today.Year, today.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Aktuellen Monat laden
            var filter = new ExpenseFilter { StartDate = startDate, EndDate = endDate };
            var transactions = await _expenseService.GetExpensesAsync(filter);
            ct.ThrowIfCancellationRequested();

            double totalExpenses = 0, totalIncome = 0;
            var categoryExpenses = new Dictionary<ExpenseCategory, double>();

            foreach (var t in transactions)
            {
                if (t.Type == TransactionType.Expense)
                {
                    totalExpenses += t.Amount;
                    if (!categoryExpenses.ContainsKey(t.Category))
                        categoryExpenses[t.Category] = 0;
                    categoryExpenses[t.Category] += t.Amount;
                }
                else
                {
                    totalIncome += t.Amount;
                }
            }

            // Vormonat laden
            var prevStart = startDate.AddMonths(-1);
            var prevEnd = startDate.AddDays(-1);
            var prevFilter = new ExpenseFilter { StartDate = prevStart, EndDate = prevEnd };
            var prevTransactions = await _expenseService.GetExpensesAsync(prevFilter);
            ct.ThrowIfCancellationRequested();

            double prevExpenses = 0;
            foreach (var t in prevTransactions)
            {
                if (t.Type == TransactionType.Expense)
                    prevExpenses += t.Amount;
            }

            // Kategorie-Aufschlüsselung
            var breakdown = categoryExpenses
                .Select(kvp => new CategoryBreakdownItem
                {
                    Category = kvp.Key,
                    CategoryName = Helpers.CategoryLocalizationHelper.GetLocalizedName(kvp.Key, _localizationService),
                    Amount = kvp.Value,
                    Percentage = totalExpenses > 0 ? kvp.Value / totalExpenses * 100 : 0
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            // Spartipps: Top-3 Kategorien mit Hinweis
            var savingTips = breakdown.Take(3).Select(c =>
            {
                var tipFormat = _localizationService.GetString("SavingTip") ?? "You spend the most on {0}";
                return new SavingTipItem
                {
                    CategoryName = c.CategoryName,
                    Tip = string.Format(tipFormat, c.CategoryName),
                    Amount = c.Amount
                };
            }).ToList();

            // Monatsvergleich
            double changePercent = 0;
            if (prevExpenses > 0)
                changePercent = ((totalExpenses - prevExpenses) / prevExpenses) * 100;

            var report = new BudgetAnalysisReport
            {
                PeriodDisplay = today.ToString("MMMM yyyy"),
                TotalExpenses = totalExpenses,
                TotalIncome = totalIncome,
                CategoryBreakdown = breakdown,
                SavingTips = savingTips,
                PreviousMonthExpenses = prevExpenses,
                MonthChangePercent = changePercent
            };

            ct.ThrowIfCancellationRequested();

            BudgetAnalysisReport = report;
            ShowBudgetAnalysisOverlay = true;

            // Celebration-Effekt bei Budget-Analyse
            CelebrationRequested?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Generierung wurde abgebrochen (Tab-Wechsel oder Overlay geschlossen)
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                ex.Message);
        }
    }

    #endregion

    #region Quick Add Commands

    [RelayCommand]
    private void ToggleQuickAdd()
    {
        ShowQuickAdd = !ShowQuickAdd;
        if (ShowQuickAdd)
        {
            QuickAddAmount = string.Empty;
            QuickAddDescription = string.Empty;
            QuickAddType = TransactionType.Expense;
            QuickAddCategory = ExpenseCategory.Other;
            UpdateQuickCategoryItems();
        }
    }

    [RelayCommand]
    private void CancelQuickAdd()
    {
        ShowQuickAdd = false;
    }

    private const int MaxDescriptionLength = 200;

    [RelayCommand]
    private async Task SaveQuickExpenseAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickAddAmount) || string.IsNullOrWhiteSpace(QuickAddDescription))
            return;

        if (!double.TryParse(QuickAddAmount.Replace(",", "."), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return;

        var description = QuickAddDescription.Trim();
        if (description.Length > MaxDescriptionLength)
            description = description[..MaxDescriptionLength];

        try
        {
            var expense = new Expense
            {
                Id = Guid.NewGuid().ToString(),
                Date = DateTime.Today,
                Description = description,
                Amount = amount,
                Category = QuickAddCategory,
                Type = QuickAddType
            };

            await _expenseService.AddExpenseAsync(expense);
            ShowQuickAdd = false;

            // Caches der Sub-VMs invalidieren (Daten haben sich geändert)
            ExpenseTrackerViewModel.InvalidateCache();
            StatisticsViewModel.InvalidateCache();

            await LoadMonthlyDataAsync();

            // Floating Text für Quick-Add Feedback
            var signedAmount = expense.Type == TransactionType.Income ? expense.Amount : -expense.Amount;
            var cat = expense.Type == TransactionType.Income ? "income" : "expense";
            FloatingTextRequested?.Invoke(CurrencyHelper.FormatCompactSigned(signedAmount), cat);
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                ex.Message);
        }
    }

    #endregion
}
