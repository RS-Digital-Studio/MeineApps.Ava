using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using FinanzRechner.ViewModels.Calculators;

// CategoryDisplayItem aus ExpenseTrackerViewModel wiederverwenden

namespace FinanzRechner.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly ILocalizationService _localizationService;
    private readonly IExpenseService _expenseService;
    private readonly IRewardedAdService _rewardedAdService;

    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;

    public ExpenseTrackerViewModel ExpenseTrackerViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public BudgetsViewModel BudgetsViewModel { get; }
    public RecurringTransactionsViewModel RecurringTransactionsViewModel { get; }
    public CompoundInterestViewModel CompoundInterestViewModel { get; }
    public SavingsPlanViewModel SavingsPlanViewModel { get; }
    public LoanViewModel LoanViewModel { get; }
    public AmortizationViewModel AmortizationViewModel { get; }
    public YieldViewModel YieldViewModel { get; }

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        ILocalizationService localizationService,
        IExpenseService expenseService,
        IRewardedAdService rewardedAdService,
        ExpenseTrackerViewModel expenseTrackerViewModel,
        StatisticsViewModel statisticsViewModel,
        SettingsViewModel settingsViewModel,
        BudgetsViewModel budgetsViewModel,
        RecurringTransactionsViewModel recurringTransactionsViewModel,
        CompoundInterestViewModel compoundInterestViewModel,
        SavingsPlanViewModel savingsPlanViewModel,
        LoanViewModel loanViewModel,
        AmortizationViewModel amortizationViewModel,
        YieldViewModel yieldViewModel)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localizationService = localizationService;
        _expenseService = expenseService;
        _rewardedAdService = rewardedAdService;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        ExpenseTrackerViewModel = expenseTrackerViewModel;
        StatisticsViewModel = statisticsViewModel;
        SettingsViewModel = settingsViewModel;
        BudgetsViewModel = budgetsViewModel;
        RecurringTransactionsViewModel = recurringTransactionsViewModel;
        CompoundInterestViewModel = compoundInterestViewModel;
        SavingsPlanViewModel = savingsPlanViewModel;
        LoanViewModel = loanViewModel;
        AmortizationViewModel = amortizationViewModel;
        YieldViewModel = yieldViewModel;

        // Wire up GoBack actions
        CompoundInterestViewModel.GoBackAction = CloseCalculator;
        SavingsPlanViewModel.GoBackAction = CloseCalculator;
        LoanViewModel.GoBackAction = CloseCalculator;
        AmortizationViewModel.GoBackAction = CloseCalculator;
        YieldViewModel.GoBackAction = CloseCalculator;

        // Wire up sub-page navigation from ExpenseTracker
        ExpenseTrackerViewModel.NavigationRequested += OnExpenseTrackerNavigation;
        BudgetsViewModel.NavigationRequested += OnSubPageGoBack;
        RecurringTransactionsViewModel.NavigationRequested += OnSubPageGoBack;

        // Set default tab
        _selectedTab = 0;
        UpdateNavTexts();

        // Subscribe to language changes
        SettingsViewModel.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        UpdateNavTexts();
        UpdateHomeTexts();
        ExpenseTrackerViewModel.UpdateLocalizedTexts();
        StatisticsViewModel.UpdateLocalizedTexts();
        BudgetsViewModel.UpdateLocalizedTexts();
        RecurringTransactionsViewModel.UpdateLocalizedTexts();
    }

    [ObservableProperty]
    private bool _isPremium;

    #region Navigation

    [ObservableProperty]
    private int _selectedTab;

    public bool IsHomeActive => SelectedTab == 0;
    public bool IsTrackerActive => SelectedTab == 1;
    public bool IsStatsActive => SelectedTab == 2;
    public bool IsSettingsActive => SelectedTab == 3;

    [ObservableProperty]
    private string _navHomeText = "Home";

    [ObservableProperty]
    private string _navTrackerText = "Tracker";

    [ObservableProperty]
    private string _navStatsText = "Statistics";

    [ObservableProperty]
    private string _navSettingsText = "Settings";

    private void UpdateNavTexts()
    {
        NavHomeText = _localizationService.GetString("TabHome") ?? "Home";
        NavTrackerText = _localizationService.GetString("TabTracker") ?? "Tracker";
        NavStatsText = _localizationService.GetString("TabStatistics") ?? "Statistics";
        NavSettingsText = _localizationService.GetString("TabSettings") ?? "Settings";
    }

    [RelayCommand]
    private void NavigateToHome() => SelectedTab = 0;

    [RelayCommand]
    private void NavigateToTracker() => SelectedTab = 1;

    [RelayCommand]
    private void NavigateToStats() => SelectedTab = 2;

    [RelayCommand]
    private void NavigateToSettings() => SelectedTab = 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsTrackerActive));
        OnPropertyChanged(nameof(IsStatsActive));
        OnPropertyChanged(nameof(IsSettingsActive));

        // Close any open overlays on tab switch
        if (IsCalculatorOpen)
            CloseCalculator();
        if (IsSubPageOpen)
            CurrentSubPage = null;
        if (ShowBudgetAdOverlay)
            ShowBudgetAdOverlay = false;
        if (ShowBudgetAnalysisOverlay)
            CloseBudgetAnalysis();

        // Daten laden beim Tab-Wechsel
        if (value == 0)
            _ = LoadMonthlyDataAsync();
        else if (value == 1)
            _ = ExpenseTrackerViewModel.OnAppearingAsync();
        else if (value == 2)
            _ = StatisticsViewModel.OnAppearingAsync();
    }

    #endregion

    #region Calculator Navigation

    [ObservableProperty]
    private bool _isCalculatorOpen;

    [ObservableProperty]
    private int _activeCalculatorIndex = -1;

    public bool IsCompoundInterestActive => ActiveCalculatorIndex == 0;
    public bool IsSavingsPlanActive => ActiveCalculatorIndex == 1;
    public bool IsLoanActive => ActiveCalculatorIndex == 2;
    public bool IsAmortizationActive => ActiveCalculatorIndex == 3;
    public bool IsYieldActive => ActiveCalculatorIndex == 4;

    partial void OnActiveCalculatorIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCompoundInterestActive));
        OnPropertyChanged(nameof(IsSavingsPlanActive));
        OnPropertyChanged(nameof(IsLoanActive));
        OnPropertyChanged(nameof(IsAmortizationActive));
        OnPropertyChanged(nameof(IsYieldActive));
    }

    [RelayCommand]
    private void OpenCompoundInterest()
    {
        ActiveCalculatorIndex = 0;
        IsCalculatorOpen = true;
        // Automatisch mit Standardwerten berechnen
        if (!CompoundInterestViewModel.HasResult)
            CompoundInterestViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenSavingsPlan()
    {
        ActiveCalculatorIndex = 1;
        IsCalculatorOpen = true;
        if (!SavingsPlanViewModel.HasResult)
            SavingsPlanViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenLoan()
    {
        ActiveCalculatorIndex = 2;
        IsCalculatorOpen = true;
        if (!LoanViewModel.HasResult)
            LoanViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenAmortization()
    {
        ActiveCalculatorIndex = 3;
        IsCalculatorOpen = true;
        if (!AmortizationViewModel.HasResult)
            AmortizationViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenYield()
    {
        ActiveCalculatorIndex = 4;
        IsCalculatorOpen = true;
        if (!YieldViewModel.HasResult)
            YieldViewModel.CalculateCommand.Execute(null);
    }

    private void CloseCalculator()
    {
        IsCalculatorOpen = false;
        ActiveCalculatorIndex = -1;
    }

    #endregion

    #region Sub-Page Navigation (Budgets, Recurring Transactions)

    [ObservableProperty]
    private string? _currentSubPage;

    public bool IsSubPageOpen => CurrentSubPage != null;
    public bool IsBudgetsPageActive => CurrentSubPage == "BudgetsPage";
    public bool IsRecurringPageActive => CurrentSubPage == "RecurringTransactionsPage";

    partial void OnCurrentSubPageChanged(string? value)
    {
        OnPropertyChanged(nameof(IsSubPageOpen));
        OnPropertyChanged(nameof(IsBudgetsPageActive));
        OnPropertyChanged(nameof(IsRecurringPageActive));
    }

    private void OnExpenseTrackerNavigation(string route)
    {
        if (route is "BudgetsPage" or "RecurringTransactionsPage")
        {
            CurrentSubPage = route;
        }
    }

    private void OnSubPageGoBack(string route)
    {
        if (route == "..")
        {
            CurrentSubPage = null;
        }
    }

    #endregion

    #region HomeView Localized Text

    public string HomeTitleText => _localizationService.GetString("AppName") ?? "FinanceCalc";
    public string HomeSubtitleText => _localizationService.GetString("AppDescription") ?? "Financial calculator for savings, loans and investments";
    public string SectionCalculatorsText => _localizationService.GetString("SectionCalculators") ?? "CALCULATORS";
    public string CalculatorsTitleText => _localizationService.GetString("CalculatorsTitle") ?? "Financial Calculations";
    public string CalcCompoundInterestText => _localizationService.GetString("CalcCompoundInterest") ?? "Compound Interest";
    public string CalcSavingsPlanText => _localizationService.GetString("CalcSavingsPlan") ?? "Savings Plan";
    public string CalcLoanText => _localizationService.GetString("CalcLoan") ?? "Loan";
    public string CalcAmortizationText => _localizationService.GetString("CalcAmortization") ?? "Amortization";
    public string CalcYieldText => _localizationService.GetString("CalcYield") ?? "Yield";
    public string IncomeLabelText => _localizationService.GetString("IncomeTotalLabel") ?? "Income:";
    public string ExpensesLabelText => _localizationService.GetString("ExpensesTotalLabel") ?? "Expenses:";
    public string BalanceLabelText => _localizationService.GetString("BalanceTotalLabel") ?? "Balance:";
    public string RemoveAdsText => _localizationService.GetString("RemoveAds") ?? "Remove Ads";
    public string RemoveAdsDescText => _localizationService.GetString("RemoveAdsDesc") ?? "Enjoy ad-free experience with Premium";
    public string GetPremiumText => _localizationService.GetString("GetPremium") ?? "Get Premium";
    public string SectionBudgetText => _localizationService.GetString("SectionBudget") ?? "Budget Status";
    public string SectionRecentText => _localizationService.GetString("SectionRecent") ?? "Recent Transactions";
    public string ViewAllText => _localizationService.GetString("ViewAll") ?? "View all";
    public string PremiumPriceText => _localizationService.GetString("PremiumPrice") ?? "From \u20ac3.99";
    public string SectionCalculatorsShortText => _localizationService.GetString("SectionCalculatorsShort") ?? "Calculators";

    private void UpdateHomeTexts()
    {
        OnPropertyChanged(nameof(HomeTitleText));
        OnPropertyChanged(nameof(HomeSubtitleText));
        OnPropertyChanged(nameof(SectionCalculatorsText));
        OnPropertyChanged(nameof(CalculatorsTitleText));
        OnPropertyChanged(nameof(CalcCompoundInterestText));
        OnPropertyChanged(nameof(CalcSavingsPlanText));
        OnPropertyChanged(nameof(CalcLoanText));
        OnPropertyChanged(nameof(CalcAmortizationText));
        OnPropertyChanged(nameof(CalcYieldText));
        OnPropertyChanged(nameof(IncomeLabelText));
        OnPropertyChanged(nameof(ExpensesLabelText));
        OnPropertyChanged(nameof(BalanceLabelText));
        OnPropertyChanged(nameof(RemoveAdsText));
        OnPropertyChanged(nameof(RemoveAdsDescText));
        OnPropertyChanged(nameof(GetPremiumText));
        OnPropertyChanged(nameof(SectionBudgetText));
        OnPropertyChanged(nameof(SectionRecentText));
        OnPropertyChanged(nameof(ViewAllText));
        OnPropertyChanged(nameof(PremiumPriceText));
        OnPropertyChanged(nameof(SectionCalculatorsShortText));
        OnPropertyChanged(nameof(MonthlyReportText));
        OnPropertyChanged(nameof(BudgetAnalysisTitleText));
        OnPropertyChanged(nameof(BudgetAnalysisDescText));
        OnPropertyChanged(nameof(SavingTipText));
        OnPropertyChanged(nameof(ComparedToLastMonthText));
        OnPropertyChanged(nameof(WatchVideoReportText));
        OnPropertyChanged(nameof(CloseText));
        // Budget-Kategorie-Namen koennen sich bei Sprachwechsel aendern
        UpdateBudgetDisplayNames();
    }

    #endregion

    #region Dashboard

    [ObservableProperty]
    private double _monthlyIncome;

    [ObservableProperty]
    private double _monthlyExpenses;

    [ObservableProperty]
    private double _monthlyBalance;

    [ObservableProperty]
    private bool _hasTransactions;

    public string MonthlyIncomeDisplay => $"+{MonthlyIncome:N2} \u20ac";
    public string MonthlyExpensesDisplay => $"-{MonthlyExpenses:N2} \u20ac";
    public string MonthlyBalanceDisplay => MonthlyBalance >= 0 ? $"+{MonthlyBalance:N2} \u20ac" : $"{MonthlyBalance:N2} \u20ac";
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

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        UpdateNavTexts();

        // Faellige Dauerauftraege verarbeiten (bei jedem App-Start)
        try
        {
            await _expenseService.ProcessDueRecurringTransactionsAsync();
        }
        catch (Exception)
        {
            // Fehler beim Verarbeiten der Dauerauftraege ignorieren
        }

        await LoadMonthlyDataAsync();
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
            }
            else
            {
                TopBudgets.Clear();
                OverallBudgetPercentage = 0;
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
            var recent = expenses
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .Take(3)
                .ToList();

            HasRecentTransactions = recent.Count > 0;
            RecentTransactions = new ObservableCollection<Expense>(recent);
        }
        catch (Exception)
        {
            HasRecentTransactions = false;
        }
    }

    #region Budget Analysis Report

    [ObservableProperty]
    private bool _showBudgetAdOverlay;

    [ObservableProperty]
    private bool _showBudgetAnalysisOverlay;

    [ObservableProperty]
    private BudgetAnalysisReport? _budgetAnalysisReport;

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
        ShowBudgetAnalysisOverlay = false;
        BudgetAnalysisReport = null;
    }

    private async Task GenerateAndShowBudgetAnalysis()
    {
        try
        {
            var today = DateTime.Today;
            var startDate = new DateTime(today.Year, today.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Aktuellen Monat laden
            var filter = new ExpenseFilter { StartDate = startDate, EndDate = endDate };
            var transactions = await _expenseService.GetExpensesAsync(filter);

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
            double prevExpenses = 0;
            foreach (var t in prevTransactions)
            {
                if (t.Type == TransactionType.Expense)
                    prevExpenses += t.Amount;
            }

            // Kategorie-Aufschluesselung
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

            BudgetAnalysisReport = report;
            ShowBudgetAnalysisOverlay = true;
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                ex.Message);
        }
    }

    #endregion

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
            await LoadMonthlyDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                ex.Message);
        }
    }
}
