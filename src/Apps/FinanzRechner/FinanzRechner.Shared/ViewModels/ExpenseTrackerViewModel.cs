using System.Collections.ObjectModel;
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

public partial class ExpenseTrackerViewModel : ObservableObject, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IExportService _exportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IFileShareService _fileShareService;
    private readonly IThemeService _themeService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    /// <summary>Wird ausgelöst wenn Daten geändert wurden (Add/Delete/Update), damit andere VMs ihren Cache invalidieren.</summary>
    public event Action? DataChanged;

    public ExpenseTrackerViewModel(IExpenseService expenseService, ILocalizationService localizationService,
        IExportService exportService, IFileDialogService fileDialogService,
        IFileShareService fileShareService, IThemeService themeService,
        IPurchaseService purchaseService, IRewardedAdService rewardedAdService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
        _exportService = exportService;
        _fileDialogService = fileDialogService;
        _fileShareService = fileShareService;
        _themeService = themeService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;

        // Auf aktuellen Monat initialisieren
        _selectedYear = DateTime.Today.Year;
        _selectedMonth = DateTime.Today.Month;
    }

    #region Localized Text Properties

    public string FinanceTrackerText => _localizationService.GetString("FinanceTracker") ?? "Finance Tracker";
    public string SearchTransactionsText => _localizationService.GetString("SearchTransactions") ?? "Search transactions...";
    public string SortText => _localizationService.GetString("Sort") ?? "Sort";
    public string FilterText => _localizationService.GetString("Filter") ?? "Filter";
    public string FilterByCategoryText => _localizationService.GetString("FilterByCategory") ?? "Filter by category";
    public string MinAmountText => _localizationService.GetString("MinAmount") ?? "Min. amount";
    public string MaxAmountText => _localizationService.GetString("MaxAmount") ?? "Max. amount";
    public string ResetFiltersText => _localizationService.GetString("ResetFilters") ?? "Reset filters";
    public string IncomeLabelText => _localizationService.GetString("IncomeTotalLabel") ?? "Income:";
    public string ExpensesLabelText => _localizationService.GetString("ExpensesTotalLabel") ?? "Expenses:";
    public string BalanceLabelText => _localizationService.GetString("BalanceTotalLabel") ?? "Balance:";
    public string TodayText => _localizationService.GetString("Today") ?? "Today";
    public string NewTransactionText => _localizationService.GetString("NewTransaction") ?? "New Transaction";
    public string EditTransactionText => _localizationService.GetString("EditTransaction") ?? "Edit Transaction";
    public string DialogTitleText => IsEditing ? EditTransactionText : NewTransactionText;
    public string AmountText => _localizationService.GetString("Amount") ?? "Amount";
    public string TypeText => _localizationService.GetString("Type") ?? "Type";
    public string ExpenseText => _localizationService.GetString("Expense") ?? "Expense";
    public string IncomeText => _localizationService.GetString("Income") ?? "Income";
    public string CategoryText => _localizationService.GetString("Category") ?? "Category";
    public string DescriptionText => _localizationService.GetString("Description") ?? "Description";
    public string NoteText => _localizationService.GetString("Note") ?? "Note";
    public string RecurringText => _localizationService.GetString("Recurring") ?? "Recurring";
    public string MakeRecurringText => _localizationService.GetString("MakeRecurring") ?? "Make recurring";
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string NoTransactionsText => _localizationService.GetString("EmptyTransactionsTitle") ?? "No Transactions";
    public string NoTransactionsHintText => _localizationService.GetString("EmptyTransactionsDesc") ?? "Start tracking your income and expenses by tapping the + button";
    public string UndoText => _localizationService.GetString("Undo") ?? "Undo";
    public string CategoryBreakdownText => _localizationService.GetString("CategoryBreakdown") ?? "Categories";
    public string ExportLockedText => _localizationService.GetString("ExportLocked") ?? "Unlock Export";
    public string ExportLockedDescText => _localizationService.GetString("ExportLockedDesc") ?? "Watch a short video to start your export.";
    public string WatchVideoExportText => _localizationService.GetString("WatchVideoExport") ?? "Watch Video → Export";

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(FinanceTrackerText));
        OnPropertyChanged(nameof(SearchTransactionsText));
        OnPropertyChanged(nameof(SortText));
        OnPropertyChanged(nameof(FilterText));
        OnPropertyChanged(nameof(FilterByCategoryText));
        OnPropertyChanged(nameof(MinAmountText));
        OnPropertyChanged(nameof(MaxAmountText));
        OnPropertyChanged(nameof(ResetFiltersText));
        OnPropertyChanged(nameof(IncomeLabelText));
        OnPropertyChanged(nameof(ExpensesLabelText));
        OnPropertyChanged(nameof(BalanceLabelText));
        OnPropertyChanged(nameof(TodayText));
        OnPropertyChanged(nameof(NewTransactionText));
        OnPropertyChanged(nameof(EditTransactionText));
        OnPropertyChanged(nameof(DialogTitleText));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(ExpenseText));
        OnPropertyChanged(nameof(IncomeText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(RecurringText));
        OnPropertyChanged(nameof(MakeRecurringText));
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(NoTransactionsText));
        OnPropertyChanged(nameof(NoTransactionsHintText));
        OnPropertyChanged(nameof(UndoText));
        OnPropertyChanged(nameof(CategoryBreakdownText));
        OnPropertyChanged(nameof(ExportLockedText));
        OnPropertyChanged(nameof(ExportLockedDescText));
        OnPropertyChanged(nameof(WatchVideoExportText));
    }

    #endregion

    #region Navigation Events

    public event Action<string>? NavigationRequested;
    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    #endregion

    #region Expenses List

    [ObservableProperty]
    private ObservableCollection<Expense> _expenses = [];

    [ObservableProperty]
    private ObservableCollection<ExpenseGroup> _groupedExpenses = [];

    private List<Expense> _allExpenses = []; // Ungefilterte Liste

    /// <summary>
    /// Unterdrueckt doppeltes Laden bei PreviousMonth/NextMonth (Year+Month aendern sich gleichzeitig).
    /// </summary>
    private bool _suppressLoad;

    [ObservableProperty]
    private int _selectedYear;

    [ObservableProperty]
    private int _selectedMonth;

    [ObservableProperty]
    private MonthSummary? _summary;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasExpenses;

    [ObservableProperty]
    private string _filteredCountDisplay = string.Empty;

    public string MonthYearDisplay => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");

    public string TotalExpensesDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalExpenses) : CurrencyHelper.Format(0);
    public string TotalIncomeDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalIncome) : CurrencyHelper.Format(0);
    public string BalanceDisplay => Summary != null ? CurrencyHelper.Format(Summary.Balance) : CurrencyHelper.Format(0);

    // Legacy-Kompatibilität
    public string TotalDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalAmount) : CurrencyHelper.Format(0);

    partial void OnSelectedYearChanged(int value)
    {
        if (_suppressLoad) return;
        _ = LoadExpensesWithErrorHandlingAsync();
    }

    partial void OnSelectedMonthChanged(int value)
    {
        OnPropertyChanged(nameof(MonthYearDisplay));
        if (_suppressLoad) return;
        _ = LoadExpensesWithErrorHandlingAsync();
    }

    /// <summary>
    /// Lädt Expenses und fängt Fehler auf dem UI-Thread ab.
    /// </summary>
    private async Task LoadExpensesWithErrorHandlingAsync()
    {
        try
        {
            await LoadExpensesAsync();
            _isDataStale = false;
        }
        catch (Exception ex)
        {
            var errorTitle = _localizationService.GetString("Error") ?? "Error";
            MessageRequested?.Invoke(errorTitle, ex.Message);
        }
    }

    #endregion

    #region New Expense Form

    [ObservableProperty]
    private DateTime _newExpenseDate = DateTime.Today;

    [ObservableProperty]
    private string _newExpenseDescription = string.Empty;

    [ObservableProperty]
    private double _newExpenseAmount;

    [ObservableProperty]
    private ExpenseCategory _newExpenseCategory = ExpenseCategory.Other;

    [ObservableProperty]
    private string _newExpenseNote = string.Empty;

    [ObservableProperty]
    private bool _isAddingExpense;

    [ObservableProperty]
    private TransactionType _newTransactionType = TransactionType.Expense;

    public bool IsExpenseSelected => NewTransactionType == TransactionType.Expense;
    public bool IsIncomeSelected => NewTransactionType == TransactionType.Income;

    public List<ExpenseCategory> Categories => NewTransactionType == TransactionType.Expense
        ? ExpenseCategories
        : IncomeCategories;

    // Ausgabe-Kategorien
    private static readonly List<ExpenseCategory> ExpenseCategories =
    [
        ExpenseCategory.Food,
        ExpenseCategory.Transport,
        ExpenseCategory.Housing,
        ExpenseCategory.Entertainment,
        ExpenseCategory.Shopping,
        ExpenseCategory.Health,
        ExpenseCategory.Education,
        ExpenseCategory.Bills,
        ExpenseCategory.Other
    ];

    // Einnahme-Kategorien
    private static readonly List<ExpenseCategory> IncomeCategories =
    [
        ExpenseCategory.Salary,
        ExpenseCategory.Freelance,
        ExpenseCategory.Investment,
        ExpenseCategory.Gift,
        ExpenseCategory.OtherIncome
    ];

    // Lokalisierte Beschreibungsvorschlag-Schlüssel für Ausgaben
    private static readonly string[] ExpenseSuggestionKeys =
    [
        "SuggestionGroceries",
        "SuggestionOnlineOrder",
        "SuggestionGas",
        "SuggestionRestaurant",
        "SuggestionRent",
        "SuggestionElectricity",
        "SuggestionInternet",
        "SuggestionInsurance"
    ];

    // Lokalisierte Beschreibungsvorschlag-Schlüssel für Einnahmen
    private static readonly string[] IncomeSuggestionKeys =
    [
        "SuggestionSalary",
        "SuggestionBonus",
        "SuggestionContract",
        "SuggestionInterest",
        "SuggestionDividend",
        "SuggestionGift"
    ];

    public List<string> ExpenseDescriptionSuggestions =>
        ExpenseSuggestionKeys.Select(k => _localizationService.GetString(k) ?? k).ToList();

    public List<string> IncomeDescriptionSuggestions =>
        IncomeSuggestionKeys.Select(k => _localizationService.GetString(k) ?? k).ToList();

    public List<string> DescriptionSuggestions => NewTransactionType == TransactionType.Expense
        ? ExpenseDescriptionSuggestions
        : IncomeDescriptionSuggestions;

    partial void OnNewTransactionTypeChanged(TransactionType value)
    {
        // Standardkategorie setzen
        NewExpenseCategory = value == TransactionType.Expense
            ? ExpenseCategory.Other
            : ExpenseCategory.Salary;

        OnPropertyChanged(nameof(IsExpenseSelected));
        OnPropertyChanged(nameof(IsIncomeSelected));
        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(DescriptionSuggestions));
        UpdateCategoryItems();
    }

    #endregion

    #region Edit Mode

    [ObservableProperty]
    private Expense? _selectedExpense;

    [ObservableProperty]
    private bool _isEditing;

    public bool ShowRecurringSection => !IsEditing;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(ShowRecurringSection));

    #endregion

    #region Recurring (Add Dialog)

    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private RecurrencePattern _selectedRecurrencePattern = RecurrencePattern.Monthly;

    [ObservableProperty]
    private DateTime _recurringStartDate = DateTime.Today;

    [ObservableProperty]
    private bool _hasRecurringEndDate;

    [ObservableProperty]
    private DateTime _recurringEndDate = DateTime.Today.AddYears(1);

    #endregion

    #region Category Display Items

    [ObservableProperty]
    private ObservableCollection<CategoryDisplayItem> _categoryItems = [];

    private void UpdateCategoryItems()
    {
        var categories = NewTransactionType == TransactionType.Expense
            ? ExpenseCategories
            : IncomeCategories;

        var items = new ObservableCollection<CategoryDisplayItem>();
        foreach (var cat in categories)
        {
            items.Add(new CategoryDisplayItem
            {
                Category = cat,
                CategoryName = CategoryLocalizationHelper.GetLocalizedName(cat, _localizationService),
                IsSelected = cat == NewExpenseCategory
            });
        }
        CategoryItems = items;
    }

    #endregion

    #region Category Chart

    [ObservableProperty]
    private DonutChartVisualization.Segment[]? _categoryDonutSegments;

    [ObservableProperty]
    private bool _hasCategoryChartData;

    // Farben via CategoryLocalizationHelper.GetCategoryColor() (5.1 zentralisiert)

    private void UpdateCategoryChart()
    {
        if (_allExpenses.Count == 0)
        {
            HasCategoryChartData = false;
            CategoryDonutSegments = null;
            return;
        }

        // Nur Ausgaben im Kreisdiagramm (Einnahmen würden es verzerren)
        var expensesByCategory = _allExpenses
            .Where(e => e.Type == TransactionType.Expense)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        if (expensesByCategory.Count == 0)
        {
            HasCategoryChartData = false;
            CategoryDonutSegments = null;
            return;
        }

        var total = expensesByCategory.Sum(x => x.Amount);

        CategoryDonutSegments = expensesByCategory.Select(c => new DonutChartVisualization.Segment
        {
            Value = (float)c.Amount,
            Color = CategoryLocalizationHelper.GetCategoryColor(c.Category),
            Label = CategoryLocalizationHelper.GetLocalizedName(c.Category, _localizationService),
            ValueText = total > 0 ? $"{c.Amount / total * 100:F0}%" : ""
        }).ToArray();

        HasCategoryChartData = true;
    }

    #endregion

    #region Undo Delete

    [ObservableProperty]
    private bool _showUndoDelete;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    /// <summary>
    /// Queue fuer geloeschte Expenses (verhindert Race Condition bei schnellem doppeltem Delete).
    /// </summary>
    private readonly Queue<Expense> _deletedExpenses = new();
    private CancellationTokenSource? _undoCancellation;

    #endregion

    #region Sorting and Filtering

    public enum SortOption
    {
        DateDescending,   // Neueste zuerst (Standard)
        DateAscending,    // Älteste zuerst
        AmountDescending, // Höchster Betrag zuerst
        AmountAscending,  // Niedrigster Betrag zuerst
        Description       // A-Z
    }

    public enum FilterTypeOption
    {
        All,      // Alle Transaktionen
        Expenses, // Nur Ausgaben
        Income    // Nur Einnahmen
    }

    [ObservableProperty]
    private SortOption _selectedSort = SortOption.DateDescending;

    [ObservableProperty]
    private FilterTypeOption _selectedFilter = FilterTypeOption.All;

    [ObservableProperty]
    private ExpenseCategory? _selectedCategoryFilter = null;

    [ObservableProperty]
    private double _minAmountFilter = 0;

    [ObservableProperty]
    private double _maxAmountFilter = 0;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    private CancellationTokenSource? _searchCancellation;

    partial void OnSelectedSortChanged(SortOption value) => ApplyFilterAndSort();
    partial void OnSelectedFilterChanged(FilterTypeOption value) => ApplyFilterAndSort();
    partial void OnSelectedCategoryFilterChanged(ExpenseCategory? value) => ApplyFilterAndSort();
    partial void OnSearchTermChanged(string value) => _ = OnSearchTermChangedDebounced(value);

    public List<SortOption> SortOptions { get; } =
    [
        SortOption.DateDescending,
        SortOption.DateAscending,
        SortOption.AmountDescending,
        SortOption.AmountAscending,
        SortOption.Description
    ];

    public List<FilterTypeOption> FilterTypeOptions { get; } =
    [
        FilterTypeOption.All,
        FilterTypeOption.Expenses,
        FilterTypeOption.Income
    ];

    public List<ExpenseCategory?> CategoryFilterOptions { get; } =
    [
        null, // "All"
        .. ExpenseCategories,
        .. IncomeCategories
    ];

    #endregion

    #region Commands

    /// <summary>
    /// Wird beim Tab-Wechsel zum Tracker aufgerufen.
    /// </summary>
    /// <summary>
    /// Markiert die Daten als veraltet, sodass beim nächsten Tab-Wechsel neu geladen wird.
    /// </summary>
    public void InvalidateCache() => _isDataStale = true;
    private bool _isDataStale = true;

    public async Task OnAppearingAsync()
    {
        if (!_isDataStale) return;
        await LoadExpensesAsync();
        _isDataStale = false;
    }

    [RelayCommand]
    public async Task LoadExpensesAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            // ExpenseService sicherstellen dass initialisiert
            await _expenseService.InitializeAsync();

            // Daueraufträge werden bereits in MainViewModel.OnAppearingAsync() verarbeitet,
            // daher hier kein erneuter Aufruf nötig

            var expenses = await _expenseService.GetExpensesByMonthAsync(SelectedYear, SelectedMonth);
            _allExpenses = expenses.ToList();

            // Kategorie-Chart aktualisieren
            UpdateCategoryChart();

            // Filter und Sortierung anwenden
            ApplyFilterAndSort();

            Summary = await _expenseService.GetMonthSummaryAsync(SelectedYear, SelectedMonth);
            OnPropertyChanged(nameof(TotalExpensesDisplay));
            OnPropertyChanged(nameof(TotalIncomeDisplay));
            OnPropertyChanged(nameof(BalanceDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnSearchTermChangedDebounced(string value)
    {
        // Vorherigen Timer abbrechen
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            // 300ms Entprellung
            await Task.Delay(300, _searchCancellation.Token);
            ApplyFilterAndSort();
        }
        catch (TaskCanceledException)
        {
            // Neuer Suchbegriff eingegeben - nichts tun
        }
    }

    private void ApplyFilterAndSort()
    {
        // Optimierte Filterung: Ein Durchlauf mit List statt IEnumerable
        var filtered = new List<Expense>(_allExpenses.Count);
        var hasSearch = !string.IsNullOrWhiteSpace(SearchTerm);

        foreach (var expense in _allExpenses)
        {
            // Nach Suchbegriff filtern (OrdinalIgnoreCase statt ToLowerInvariant)
            if (hasSearch &&
                !expense.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) &&
                (expense.Note == null || !expense.Note.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Nach Transaktionstyp filtern
            if (SelectedFilter == FilterTypeOption.Expenses && expense.Type != TransactionType.Expense)
                continue;
            if (SelectedFilter == FilterTypeOption.Income && expense.Type != TransactionType.Income)
                continue;

            // Nach Kategorie filtern
            if (SelectedCategoryFilter.HasValue && expense.Category != SelectedCategoryFilter.Value)
                continue;

            // Nach Betrag filtern
            if (MinAmountFilter > 0 && expense.Amount < MinAmountFilter)
                continue;
            if (MaxAmountFilter > 0 && expense.Amount > MaxAmountFilter)
                continue;

            // Alle Filter bestanden - hinzufügen
            filtered.Add(expense);
        }

        // Sortierung anwenden (in-place)
        filtered.Sort(SelectedSort switch
        {
            SortOption.DateAscending => (a, b) => a.Date.CompareTo(b.Date),
            SortOption.DateDescending => (a, b) => b.Date.CompareTo(a.Date),
            SortOption.AmountDescending => (a, b) => b.Amount.CompareTo(a.Amount),
            SortOption.AmountAscending => (a, b) => a.Amount.CompareTo(b.Amount),
            SortOption.Description => (a, b) => string.Compare(a.Description, b.Description, StringComparison.Ordinal),
            _ => (a, b) => b.Date.CompareTo(a.Date) // Default: DateDescending
        });

        // Neue Collection statt Clear+AddRange: Bei Avalonia effizienter, da Clear+Add
        // viele einzelne CollectionChanged-Events feuert (je eines pro Add), während
        // eine neue Collection nur ein einziges PropertyChanged auslöst.
        Expenses = new ObservableCollection<Expense>(filtered);
        HasExpenses = Expenses.Count > 0;

        // Nach Datum gruppieren
        UpdateGroupedExpenses(filtered);

        // Filterstatus aktualisieren
        IsFilterActive = !string.IsNullOrWhiteSpace(SearchTerm) ||
                        SelectedFilter != FilterTypeOption.All ||
                        SelectedCategoryFilter.HasValue ||
                        MinAmountFilter > 0 ||
                        MaxAmountFilter > 0;

        // Anzeige aktualisieren
        FilteredCountDisplay = _allExpenses.Count > 0
            ? string.Format(_localizationService.GetString("FilteredCountFormat") ?? "{0} / {1}", Expenses.Count, _allExpenses.Count)
            : string.Empty;
    }

    private void UpdateGroupedExpenses(List<Expense> expenses)
    {
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        var groups = expenses
            .GroupBy(e => e.Date.Date)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                string dateDisplay;
                if (g.Key == today)
                    dateDisplay = _localizationService.GetString("Today") ?? "Today";
                else if (g.Key == yesterday)
                    dateDisplay = _localizationService.GetString("Yesterday") ?? "Yesterday";
                else
                    dateDisplay = g.Key.ToString("dddd, dd. MMMM");

                return new ExpenseGroup(g.Key, dateDisplay, g.OrderByDescending(e => e.Date));
            })
            .ToList();

        GroupedExpenses = new ObservableCollection<ExpenseGroup>(groups);
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchTerm = string.Empty;
        SelectedFilter = FilterTypeOption.All;
        SelectedCategoryFilter = null;
        MinAmountFilter = 0;
        MaxAmountFilter = 0;
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private void ResetSort()
    {
        SelectedSort = SortOption.DateDescending;
    }

    [RelayCommand]
    private void ShowAddExpenseForm()
    {
        ResetForm();
        IsAddingExpense = true;
        IsEditing = false;
        UpdateCategoryItems();
    }

    [RelayCommand]
    private void CancelAddExpense()
    {
        IsAddingExpense = false;
        IsEditing = false;
        ResetForm();
    }

    [RelayCommand]
    private async Task SaveExpenseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewExpenseDescription) || NewExpenseAmount <= 0)
            return;

        // Enddatum muss nach Startdatum liegen (bei Daueraufträgen)
        if (IsRecurring && HasRecurringEndDate && RecurringEndDate < RecurringStartDate)
        {
            MessageRequested?.Invoke(
                _localizationService.GetString("Error") ?? "Error",
                _localizationService.GetString("ErrorEndDateBeforeStart") ?? "End date must be after start date.");
            return;
        }

        try
        {
            if (IsEditing && SelectedExpense != null)
            {
                // Bestehende aktualisieren
                SelectedExpense.Date = NewExpenseDate;
                SelectedExpense.Description = NewExpenseDescription;
                SelectedExpense.Amount = NewExpenseAmount;
                SelectedExpense.Category = NewExpenseCategory;
                SelectedExpense.Note = string.IsNullOrWhiteSpace(NewExpenseNote) ? null : NewExpenseNote;
                SelectedExpense.Type = NewTransactionType;

                await _expenseService.UpdateExpenseAsync(SelectedExpense);
            }
            else
            {
                // Neue hinzufügen
                var expense = new Expense
                {
                    Date = NewExpenseDate,
                    Description = NewExpenseDescription,
                    Amount = NewExpenseAmount,
                    Category = NewExpenseCategory,
                    Note = string.IsNullOrWhiteSpace(NewExpenseNote) ? null : NewExpenseNote,
                    Type = NewTransactionType
                };

                await _expenseService.AddExpenseAsync(expense);

                // Floating Text fuer visuelles Feedback
                var signedAmount = expense.Type == TransactionType.Income ? expense.Amount : -expense.Amount;
                var cat = expense.Type == TransactionType.Income ? "income" : "expense";
                FloatingTextRequested?.Invoke(Helpers.CurrencyHelper.FormatCompactSigned(signedAmount), cat);

                // Dauerauftrag erstellen wenn aktiviert
                if (IsRecurring)
                {
                    var recurring = new RecurringTransaction
                    {
                        Description = NewExpenseDescription,
                        Amount = NewExpenseAmount,
                        Category = NewExpenseCategory,
                        Type = NewTransactionType,
                        Note = string.IsNullOrWhiteSpace(NewExpenseNote) ? null : NewExpenseNote,
                        Pattern = SelectedRecurrencePattern,
                        StartDate = RecurringStartDate,
                        EndDate = HasRecurringEndDate ? RecurringEndDate : null,
                        IsActive = true,
                        LastExecuted = NewExpenseDate // Sofortige Neuerstellung verhindern
                    };
                    await _expenseService.CreateRecurringTransactionAsync(recurring);
                }
            }

            // Andere VMs informieren dass sich Daten geändert haben
            DataChanged?.Invoke();

            // Datum VOR ResetForm sichern (ResetForm setzt NewExpenseDate auf Today)
            var savedDate = NewExpenseDate;

            IsAddingExpense = false;
            IsEditing = false;
            ResetForm();

            // Zum Monat der gespeicherten Transaktion wechseln oder neu laden
            if (savedDate.Year == SelectedYear && savedDate.Month == SelectedMonth)
            {
                await LoadExpensesAsync();
            }
            else
            {
                // _suppressLoad verhindert doppeltes Laden wenn Jahr UND Monat sich ändern
                _suppressLoad = true;
                SelectedYear = savedDate.Year;
                _suppressLoad = false;
                SelectedMonth = savedDate.Month;
            }
        }
        catch (Exception)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("SaveError") ?? "Failed to save transaction. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private void EditExpense(Expense expense)
    {
        SelectedExpense = expense;
        NewExpenseDate = expense.Date;
        NewExpenseDescription = expense.Description;
        NewExpenseAmount = expense.Amount;
        NewExpenseCategory = expense.Category;
        NewExpenseNote = expense.Note ?? string.Empty;
        NewTransactionType = expense.Type;

        IsEditing = true;
        IsAddingExpense = true;
        UpdateCategoryItems();
    }

    [RelayCommand]
    private async Task DeleteExpenseAsync(Expense expense)
    {
        // In Queue speichern (statt einzelner Variable - verhindert Race Condition)
        _deletedExpenses.Enqueue(expense);

        // Aus UI-Liste entfernen
        _allExpenses.Remove(expense);
        ApplyFilterAndSort();

        // Undo-Nachricht anzeigen
        UndoMessage = $"{_localizationService.GetString("TransactionDeleted") ?? "Transaction deleted"} - {expense.Description}";
        ShowUndoDelete = true;

        // Timer fuer permanente Loeschung neu starten (5 Sekunden)
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = new CancellationTokenSource();

        try
        {
            await Task.Delay(5000, _undoCancellation.Token);

            // Alle in der Queue permanent loeschen
            await PermanentlyDeleteQueuedExpensesAsync();
            ShowUndoDelete = false;
        }
        catch (TaskCanceledException)
        {
            // Undo wurde ausgeloest - nichts tun
        }
    }

    /// <summary>
    /// Loescht alle Expenses in der Queue permanent aus dem Storage.
    /// </summary>
    private async Task PermanentlyDeleteQueuedExpensesAsync()
    {
        while (_deletedExpenses.Count > 0)
        {
            var expense = _deletedExpenses.Dequeue();
            await _expenseService.DeleteExpenseAsync(expense.Id);
        }
        // Andere VMs informieren dass sich Daten geändert haben
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        // Timer stoppen
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();

        // Alle geloeschten Transaktionen wiederherstellen
        while (_deletedExpenses.Count > 0)
        {
            var expense = _deletedExpenses.Dequeue();
            _allExpenses.Add(expense);
        }
        ApplyFilterAndSort();
        await LoadExpensesAsync(); // Summary aktualisieren

        ShowUndoDelete = false;
    }

    [RelayCommand]
    private async Task DismissUndoAsync()
    {
        ShowUndoDelete = false;
        // Bei Dismiss alle geloeschten Expenses permanent loeschen
        await PermanentlyDeleteQueuedExpensesAsync();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        if (SelectedMonth == 1)
        {
            _suppressLoad = true;
            SelectedYear--;
            _suppressLoad = false;
            SelectedMonth = 12;
        }
        else
        {
            SelectedMonth--;
        }
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (SelectedMonth == 12)
        {
            _suppressLoad = true;
            SelectedYear++;
            _suppressLoad = false;
            SelectedMonth = 1;
        }
        else
        {
            SelectedMonth++;
        }
    }

    [RelayCommand]
    private void GoToCurrentMonth()
    {
        var today = DateTime.Today;
        if (SelectedYear == today.Year && SelectedMonth == today.Month)
            return;

        _suppressLoad = true;
        SelectedYear = today.Year;
        _suppressLoad = false;
        SelectedMonth = today.Month;
    }

    [RelayCommand]
    private void ShowBudgets()
    {
        NavigateTo("BudgetsPage");
    }

    [RelayCommand]
    private void ShowRecurringTransactions()
    {
        NavigateTo("RecurringTransactionsPage");
    }

    [RelayCommand]
    private void SelectCategory(CategoryDisplayItem item)
    {
        foreach (var cat in CategoryItems)
            cat.IsSelected = false;
        item.IsSelected = true;
        NewExpenseCategory = item.Category;
    }

    [RelayCommand]
    private void SelectRecurrencePattern(RecurrencePattern pattern)
    {
        SelectedRecurrencePattern = pattern;
    }

    [RelayCommand]
    private void ApplyDescriptionSuggestion(string suggestion)
    {
        NewExpenseDescription = suggestion;
    }

    [RelayCommand]
    private void SetTransactionTypeExpense()
    {
        NewTransactionType = TransactionType.Expense;
    }

    [RelayCommand]
    private void SetTransactionTypeIncome()
    {
        NewTransactionType = TransactionType.Income;
    }

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

    #region CSV Export Ad Gate

    [ObservableProperty]
    private bool _showCsvExportAdOverlay;

    /// <summary>
    /// Merkt sich welcher CSV-Export angefragt wurde ("month" oder "all").
    /// </summary>
    private string _pendingCsvExportType = "";

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (IsLoading) return;

        if (_purchaseService.IsPremium)
        {
            await DoExportToCsvAsync();
            return;
        }

        _pendingCsvExportType = "month";
        ShowCsvExportAdOverlay = true;
    }

    [RelayCommand]
    private async Task ExportAllToCsvAsync()
    {
        if (IsLoading) return;

        if (_purchaseService.IsPremium)
        {
            await DoExportAllToCsvAsync();
            return;
        }

        _pendingCsvExportType = "all";
        ShowCsvExportAdOverlay = true;
    }

    [RelayCommand]
    private async Task ConfirmCsvExportAdAsync()
    {
        ShowCsvExportAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("export_csv");
        if (success)
        {
            if (_pendingCsvExportType == "month")
                await DoExportToCsvAsync();
            else if (_pendingCsvExportType == "all")
                await DoExportAllToCsvAsync();
        }
        else
        {
            var msg = _localizationService.GetString("ExportAdFailed") ?? "Could not load video";
            _ = ShowExportStatusAsync(msg);
        }
        _pendingCsvExportType = "";
    }

    [RelayCommand]
    private void CancelCsvExportAd()
    {
        ShowCsvExportAdOverlay = false;
        _pendingCsvExportType = "";
    }

    private async Task DoExportToCsvAsync()
    {
        if (IsLoading) return;

        try
        {
            var monthName = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
            var suggestedName = $"transactions_{SelectedYear}_{SelectedMonth:D2}.csv";
            var title = $"{_localizationService.GetString("ExportTitle") ?? "Export"} - {monthName}";

            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            IsLoading = true;
            var filePath = await _exportService.ExportToCsvAsync(SelectedYear, SelectedMonth, targetPath);

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
            IsLoading = false;
        }
    }

    private async Task DoExportAllToCsvAsync()
    {
        if (IsLoading) return;

        try
        {
            var title = _localizationService.GetString("ExportAllTitle") ?? "Export all transactions";

            var targetPath = await _fileDialogService.SaveFileAsync("transactions_all.csv", title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, "transactions_all.csv");
            }

            IsLoading = true;
            var filePath = await _exportService.ExportAllToCsvAsync(targetPath);

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
            IsLoading = false;
        }
    }

    #endregion

    #endregion

    #region Helpers

    private void ResetForm()
    {
        NewExpenseDate = DateTime.Today;
        NewExpenseDescription = string.Empty;
        NewExpenseAmount = 0;
        NewTransactionType = TransactionType.Expense;
        NewExpenseCategory = ExpenseCategory.Other;
        NewExpenseNote = string.Empty;
        SelectedExpense = null;
        IsRecurring = false;
        SelectedRecurrencePattern = RecurrencePattern.Monthly;
        RecurringStartDate = DateTime.Today;
        HasRecurringEndDate = false;
        RecurringEndDate = DateTime.Today.AddYears(1);
    }


    #endregion

    #region IDisposable

    public void Dispose()
    {
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _statusCts?.Cancel();
        _statusCts?.Dispose();
    }

    #endregion
}

/// <summary>
/// Wrapper für ExpenseCategory mit Auswahl-Status für Chip-basierte UI.
/// </summary>
public partial class CategoryDisplayItem : ObservableObject
{
    public ExpenseCategory Category { get; init; }

    /// <summary>
    /// Lokalisierter Kategorie-Name (wird beim Erstellen gesetzt).
    /// </summary>
    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public string CategoryDisplay => CategoryLocalizationHelper.GetCategoryIcon(Category);

    /// <summary>Hex-Farbcode der Kategorie (z.B. "#FF9800")</summary>
    public string CategoryColorHex
    {
        get
        {
            var c = CategoryLocalizationHelper.GetCategoryColor(Category);
            return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
        }
    }
}
