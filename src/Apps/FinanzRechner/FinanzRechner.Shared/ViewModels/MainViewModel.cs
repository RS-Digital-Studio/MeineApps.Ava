using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using FinanzRechner.Resources.Strings;
using FinanzRechner.ViewModels.Calculators;

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
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>
    /// Wird ausgelöst um einen Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").
    /// </summary>
    public event Action<string>? ExitHintRequested;

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
    public InflationViewModel InflationViewModel { get; }

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
        YieldViewModel yieldViewModel,
        InflationViewModel inflationViewModel)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _localizationService = localizationService;
        _expenseService = expenseService;
        _rewardedAdService = rewardedAdService;
        _rewardedAdService.AdUnavailable += () => MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        // Lade-Fehler an den User melden (4.7)
        _expenseService.OnDataLoadError += msg =>
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                $"{_localizationService.GetString("LoadError") ?? "Fehler beim Laden"}: {msg}");

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        ExpenseTrackerViewModel = expenseTrackerViewModel;

        // ExpenseTracker FloatingText weiterleiten
        expenseTrackerViewModel.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);

        // Datenänderungen im Tracker invalidieren Home + Statistics Cache
        expenseTrackerViewModel.DataChanged += () =>
        {
            statisticsViewModel.InvalidateCache();
            _isHomeDataStale = true;
        };

        StatisticsViewModel = statisticsViewModel;
        SettingsViewModel = settingsViewModel;
        BudgetsViewModel = budgetsViewModel;
        RecurringTransactionsViewModel = recurringTransactionsViewModel;

        // Budget-/Dauerauftrags-Änderungen invalidieren Home + Statistics Cache
        budgetsViewModel.DataChanged += () =>
        {
            statisticsViewModel.InvalidateCache();
            _isHomeDataStale = true;
        };
        recurringTransactionsViewModel.DataChanged += () =>
        {
            statisticsViewModel.InvalidateCache();
            _isHomeDataStale = true;
        };
        CompoundInterestViewModel = compoundInterestViewModel;
        SavingsPlanViewModel = savingsPlanViewModel;
        LoanViewModel = loanViewModel;
        AmortizationViewModel = amortizationViewModel;
        YieldViewModel = yieldViewModel;
        InflationViewModel = inflationViewModel;

        // Wire up GoBack actions
        CompoundInterestViewModel.GoBackAction = CloseCalculator;
        SavingsPlanViewModel.GoBackAction = CloseCalculator;
        LoanViewModel.GoBackAction = CloseCalculator;
        AmortizationViewModel.GoBackAction = CloseCalculator;
        YieldViewModel.GoBackAction = CloseCalculator;
        InflationViewModel.GoBackAction = CloseCalculator;

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

    /// <summary>Home-Tab Daten-Cache: true = veraltet, muss neu geladen werden.</summary>
    private bool _isHomeDataStale = true;

    #region Back-Navigation (Double-Back-to-Exit)

    private DateTime _lastBackPressTime = DateTime.MinValue;

    /// <summary>
    /// Verarbeitet den Zurück-Button. Gibt true zurück wenn behandelt,
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// </summary>
    public bool HandleBackPressed()
    {
        // Overlays schließen (höchste Priorität zuerst)
        if (ShowBudgetAnalysisOverlay) { CloseBudgetAnalysis(); return true; }
        if (ShowBudgetAdOverlay) { ShowBudgetAdOverlay = false; return true; }
        if (ShowQuickAdd) { ShowQuickAdd = false; return true; }

        // Tab-spezifische Dialoge schließen
        if (SelectedTab == 3 && SettingsViewModel.ShowRestoreConfirmation)
        {
            SettingsViewModel.CancelRestoreCommand.Execute(null);
            return true;
        }
        if (SelectedTab == 1 && ExpenseTrackerViewModel.IsAddingExpense)
        {
            ExpenseTrackerViewModel.CancelAddExpenseCommand.Execute(null);
            return true;
        }

        // SubPage-interne Dialoge schließen, dann SubPage selbst
        if (IsSubPageOpen)
        {
            if (IsBudgetsPageActive && BudgetsViewModel.ShowAddBudget)
            {
                BudgetsViewModel.CancelAddBudgetCommand.Execute(null);
                return true;
            }
            if (IsRecurringPageActive && RecurringTransactionsViewModel.ShowAddDialog)
            {
                RecurringTransactionsViewModel.CancelAddDialogCommand.Execute(null);
                return true;
            }
            CurrentSubPage = null;
            return true;
        }

        // Calculator schließen
        if (IsCalculatorOpen) { CloseCalculator(); return true; }

        // Nicht auf Home-Tab → zu Home wechseln
        if (SelectedTab != 0) { SelectedTab = 0; return true; }

        // Home-Tab, kein Overlay → Double-Back-to-Exit (2s Fenster)
        var now = DateTime.UtcNow;
        if ((now - _lastBackPressTime).TotalSeconds < 2)
            return false;

        _lastBackPressTime = now;
        ExitHintRequested?.Invoke(
            _localizationService.GetString("PressBackToExit") ?? "Press back again to exit");
        return true;
    }

    #endregion

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
        if (ShowQuickAdd)
            ShowQuickAdd = false;
        if (ShowBudgetAdOverlay)
            ShowBudgetAdOverlay = false;
        if (ShowBudgetAnalysisOverlay)
            CloseBudgetAnalysis();

        // Daten laden beim Tab-Wechsel (nur wenn Cache veraltet)
        // Flag wird erst NACH Abschluss in LoadMonthlyDataAsync zurückgesetzt,
        // damit bei Fehler/Abbruch der Cache nicht fälschlich als aktuell gilt
        if (value == 0 && _isHomeDataStale)
        {
            _ = LoadMonthlyDataAsync().ContinueWith(_ => _isHomeDataStale = false,
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }
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
    public bool IsInflationActive => ActiveCalculatorIndex == 5;

    partial void OnActiveCalculatorIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCompoundInterestActive));
        OnPropertyChanged(nameof(IsSavingsPlanActive));
        OnPropertyChanged(nameof(IsLoanActive));
        OnPropertyChanged(nameof(IsAmortizationActive));
        OnPropertyChanged(nameof(IsYieldActive));
        OnPropertyChanged(nameof(IsInflationActive));
    }

    [RelayCommand]
    private void OpenCompoundInterest()
    {
        ActiveCalculatorIndex = 0;
        IsCalculatorOpen = true;
        // IMMER berechnen - InvalidateSurface() auf unsichtbare Canvas wird ignoriert,
        // daher muss nach Sichtbar-Werden ein frisches PropertyChanged feuern
        CompoundInterestViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenSavingsPlan()
    {
        ActiveCalculatorIndex = 1;
        IsCalculatorOpen = true;
        SavingsPlanViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenLoan()
    {
        ActiveCalculatorIndex = 2;
        IsCalculatorOpen = true;
        LoanViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenAmortization()
    {
        ActiveCalculatorIndex = 3;
        IsCalculatorOpen = true;
        AmortizationViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenYield()
    {
        ActiveCalculatorIndex = 4;
        IsCalculatorOpen = true;
        YieldViewModel.CalculateCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenInflation()
    {
        ActiveCalculatorIndex = 5;
        IsCalculatorOpen = true;
        InflationViewModel.CalculateCommand.Execute(null);
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
    public string CalcInflationText => _localizationService.GetString("CalcInflation") ?? "Inflation";
    public string IncomeLabelText => _localizationService.GetString("IncomeTotalLabel") ?? "Income:";
    public string ExpensesLabelText => _localizationService.GetString("ExpensesTotalLabel") ?? "Expenses:";
    public string BalanceLabelText => _localizationService.GetString("BalanceTotalLabel") ?? "Balance:";
    public string RemoveAdsText => _localizationService.GetString("RemoveAds") ?? "Remove Ads";
    public string RemoveAdsDescText => _localizationService.GetString("RemoveAdsDesc") ?? "Enjoy ad-free experience with Premium";
    public string GetPremiumText => _localizationService.GetString("GetPremium") ?? "Get Premium";
    public string SectionBudgetText => _localizationService.GetString("SectionBudget") ?? "Budget Status";
    public string SectionExpensesText => _localizationService.GetString("ExpensesByCategory") ?? "Expenses by Category";
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
        OnPropertyChanged(nameof(CalcInflationText));
        OnPropertyChanged(nameof(IncomeLabelText));
        OnPropertyChanged(nameof(ExpensesLabelText));
        OnPropertyChanged(nameof(BalanceLabelText));
        OnPropertyChanged(nameof(RemoveAdsText));
        OnPropertyChanged(nameof(RemoveAdsDescText));
        OnPropertyChanged(nameof(GetPremiumText));
        OnPropertyChanged(nameof(SectionBudgetText));
        OnPropertyChanged(nameof(SectionExpensesText));
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

    // Home-Dashboard Logik (Dashboard, Budget-Status, Recent Transactions,
    // Expense-Chart, Sparkline, Mini-Ringe, Quick-Add, Budget-Analyse)
    // → siehe MainViewModel.Home.cs (partial class)
}
