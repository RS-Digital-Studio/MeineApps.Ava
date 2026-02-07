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

public partial class ExpenseTrackerViewModel : ObservableObject, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IExportService _exportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IFileShareService _fileShareService;
    private readonly IThemeService _themeService;

    public event Action<string, string>? MessageRequested;

    public ExpenseTrackerViewModel(IExpenseService expenseService, ILocalizationService localizationService, IExportService exportService, IFileDialogService fileDialogService, IFileShareService fileShareService, IThemeService themeService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
        _exportService = exportService;
        _fileDialogService = fileDialogService;
        _fileShareService = fileShareService;
        _themeService = themeService;

        // Initialize to current month
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

    private List<Expense> _allExpenses = []; // Unfiltered list

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

    public string TotalExpensesDisplay => Summary != null ? $"{Summary.TotalExpenses:N2} \u20ac" : "0,00 \u20ac";
    public string TotalIncomeDisplay => Summary != null ? $"{Summary.TotalIncome:N2} \u20ac" : "0,00 \u20ac";
    public string BalanceDisplay => Summary != null ? $"{Summary.Balance:N2} \u20ac" : "0,00 \u20ac";

    // Legacy compatibility
    public string TotalDisplay => Summary != null ? $"{Summary.TotalAmount:N2} \u20ac" : "0,00 \u20ac";

    partial void OnSelectedYearChanged(int value) => LoadExpensesAsync().ContinueWith(t =>
    {
        if (t.IsFaulted) MessageRequested?.Invoke("Error", t.Exception?.Message ?? "Unknown error");
    }, TaskScheduler.Default);

    partial void OnSelectedMonthChanged(int value)
    {
        OnPropertyChanged(nameof(MonthYearDisplay));
        LoadExpensesAsync().ContinueWith(t =>
        {
            if (t.IsFaulted) MessageRequested?.Invoke("Error", t.Exception?.Message ?? "Unknown error");
        }, TaskScheduler.Default);
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

    // Expense categories
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

    // Income categories
    private static readonly List<ExpenseCategory> IncomeCategories =
    [
        ExpenseCategory.Salary,
        ExpenseCategory.Freelance,
        ExpenseCategory.Investment,
        ExpenseCategory.Gift,
        ExpenseCategory.OtherIncome
    ];

    // Localized description suggestion keys for expenses
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

    // Localized description suggestion keys for income
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
        // Set default category
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
    private IEnumerable<ISeries> _categoryChartSeries = [];

    [ObservableProperty]
    private bool _hasCategoryChartData;

    private static readonly Dictionary<ExpenseCategory, SKColor> CategoryColors = new()
    {
        { ExpenseCategory.Food, new SKColor(0xFF, 0x98, 0x00) },
        { ExpenseCategory.Transport, new SKColor(0x21, 0x96, 0xF3) },
        { ExpenseCategory.Housing, new SKColor(0x9C, 0x27, 0xB0) },
        { ExpenseCategory.Entertainment, new SKColor(0xE9, 0x1E, 0x63) },
        { ExpenseCategory.Shopping, new SKColor(0x00, 0xBC, 0xD4) },
        { ExpenseCategory.Health, new SKColor(0xF4, 0x43, 0x36) },
        { ExpenseCategory.Education, new SKColor(0x3F, 0x51, 0xB5) },
        { ExpenseCategory.Bills, new SKColor(0x60, 0x7D, 0x8B) },
        { ExpenseCategory.Other, new SKColor(0x79, 0x55, 0x48) },
        { ExpenseCategory.Salary, new SKColor(0x4C, 0xAF, 0x50) },
        { ExpenseCategory.Freelance, new SKColor(0x00, 0x96, 0x88) },
        { ExpenseCategory.Investment, new SKColor(0x8B, 0xC3, 0x4A) },
        { ExpenseCategory.Gift, new SKColor(0xFF, 0xC1, 0x07) },
        { ExpenseCategory.OtherIncome, new SKColor(0xCD, 0xDC, 0x39) }
    };

    private void UpdateCategoryChart()
    {
        if (_allExpenses.Count == 0)
        {
            HasCategoryChartData = false;
            CategoryChartSeries = [];
            return;
        }

        var labelColor = _themeService.IsDarkTheme
            ? new SKColor(0xFF, 0xFF, 0xFF)
            : new SKColor(0x21, 0x21, 0x21);

        // Nur Ausgaben im Kreisdiagramm (Einnahmen wuerden es verzerren)
        var expensesByCategory = _allExpenses
            .Where(e => e.Type == TransactionType.Expense)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        if (expensesByCategory.Count == 0)
        {
            HasCategoryChartData = false;
            CategoryChartSeries = [];
            return;
        }

        var total = expensesByCategory.Sum(x => x.Amount);

        CategoryChartSeries = expensesByCategory.Select(c => new PieSeries<double>
        {
            Values = [c.Amount],
            Name = CategoryLocalizationHelper.GetLocalizedName(c.Category, _localizationService),
            Fill = new SolidColorPaint(CategoryColors.TryGetValue(c.Category, out var color) ? color : new SKColor(0x9E, 0x9E, 0x9E)),
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsPaint = new SolidColorPaint(labelColor),
            DataLabelsFormatter = _ => total > 0 ? $"{c.Amount / total * 100:F0}%" : "",
            DataLabelsSize = 11
        }).ToArray();

        HasCategoryChartData = true;
    }

    #endregion

    #region Undo Delete

    [ObservableProperty]
    private bool _showUndoDelete;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    private Expense? _deletedExpense;
    private CancellationTokenSource? _undoCancellation;

    #endregion

    #region Sorting and Filtering

    public enum SortOption
    {
        DateDescending,   // Newest first (Default)
        DateAscending,    // Oldest first
        AmountDescending, // Highest amount first
        AmountAscending,  // Lowest amount first
        Description       // A-Z
    }

    public enum FilterTypeOption
    {
        All,      // All transactions
        Expenses, // Only expenses
        Income    // Only income
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
    public async Task OnAppearingAsync()
    {
        await LoadExpensesAsync();
    }

    [RelayCommand]
    public async Task LoadExpensesAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            // Ensure ExpenseService is initialized
            await _expenseService.InitializeAsync();

            // Check and create due recurring transactions
            await _expenseService.ProcessDueRecurringTransactionsAsync();

            var expenses = await _expenseService.GetExpensesByMonthAsync(SelectedYear, SelectedMonth);
            _allExpenses = expenses.ToList();

            // Kategorie-Chart aktualisieren
            UpdateCategoryChart();

            // Apply filter and sort
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
        // Cancel previous timer
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            // 300ms debouncing
            await Task.Delay(300, _searchCancellation.Token);
            ApplyFilterAndSort();
        }
        catch (TaskCanceledException)
        {
            // New search term entered - do nothing
        }
    }

    private void ApplyFilterAndSort()
    {
        // Optimized filtering: single-pass with List instead of IEnumerable
        var filtered = new List<Expense>(_allExpenses.Count);
        var searchLower = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.ToLowerInvariant();

        foreach (var expense in _allExpenses)
        {
            // Filter by search term
            if (searchLower != null &&
                !expense.Description.ToLowerInvariant().Contains(searchLower) &&
                (expense.Note == null || !expense.Note.ToLowerInvariant().Contains(searchLower)))
                continue;

            // Filter by transaction type
            if (SelectedFilter == FilterTypeOption.Expenses && expense.Type != TransactionType.Expense)
                continue;
            if (SelectedFilter == FilterTypeOption.Income && expense.Type != TransactionType.Income)
                continue;

            // Filter by category
            if (SelectedCategoryFilter.HasValue && expense.Category != SelectedCategoryFilter.Value)
                continue;

            // Filter by amount
            if (MinAmountFilter > 0 && expense.Amount < MinAmountFilter)
                continue;
            if (MaxAmountFilter > 0 && expense.Amount > MaxAmountFilter)
                continue;

            // All filters passed - add
            filtered.Add(expense);
        }

        // Apply sort (in-place)
        filtered.Sort(SelectedSort switch
        {
            SortOption.DateAscending => (a, b) => a.Date.CompareTo(b.Date),
            SortOption.DateDescending => (a, b) => b.Date.CompareTo(a.Date),
            SortOption.AmountDescending => (a, b) => b.Amount.CompareTo(a.Amount),
            SortOption.AmountAscending => (a, b) => a.Amount.CompareTo(b.Amount),
            SortOption.Description => (a, b) => string.Compare(a.Description, b.Description, StringComparison.Ordinal),
            _ => (a, b) => b.Date.CompareTo(a.Date) // Default: DateDescending
        });

        Expenses = new ObservableCollection<Expense>(filtered);
        HasExpenses = Expenses.Count > 0;

        // Group by date
        UpdateGroupedExpenses(filtered);

        // Update filter status
        IsFilterActive = !string.IsNullOrWhiteSpace(SearchTerm) ||
                        SelectedFilter != FilterTypeOption.All ||
                        SelectedCategoryFilter.HasValue ||
                        MinAmountFilter > 0 ||
                        MaxAmountFilter > 0;

        // Update display
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

        try
        {
            if (IsEditing && SelectedExpense != null)
            {
                // Update existing
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
                // Add new
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

                // Create recurring transaction if toggled
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
                        LastExecuted = NewExpenseDate // Prevent immediate re-creation
                    };
                    await _expenseService.CreateRecurringTransactionAsync(recurring);
                }
            }

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
                SelectedYear = savedDate.Year;
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
        // Save for undo
        _deletedExpense = expense;

        // Remove from list (UI)
        _allExpenses.Remove(expense);
        ApplyFilterAndSort();

        // Show undo notification
        UndoMessage = $"{_localizationService.GetString("TransactionDeleted") ?? "Transaction deleted"} - {expense.Description}";
        ShowUndoDelete = true;

        // Start timer for permanent deletion (5 seconds)
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = new CancellationTokenSource();

        try
        {
            await Task.Delay(5000, _undoCancellation.Token);

            // Permanent deletion after 5 seconds
            if (_deletedExpense != null)
            {
                await _expenseService.DeleteExpenseAsync(_deletedExpense.Id);
                _deletedExpense = null;
                ShowUndoDelete = false;
            }
        }
        catch (TaskCanceledException)
        {
            // Undo was triggered - do nothing
        }
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        // Stop timer
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();

        // Restore deleted transaction
        if (_deletedExpense != null)
        {
            _allExpenses.Add(_deletedExpense);
            ApplyFilterAndSort();
            await LoadExpensesAsync(); // Update summary
            _deletedExpense = null;
        }

        ShowUndoDelete = false;
    }

    [RelayCommand]
    private void DismissUndo()
    {
        ShowUndoDelete = false;
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        if (SelectedMonth == 1)
        {
            SelectedMonth = 12;
            SelectedYear--;
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
            SelectedMonth = 1;
            SelectedYear++;
        }
        else
        {
            SelectedMonth++;
        }
    }

    [RelayCommand]
    private void GoToCurrentMonth()
    {
        SelectedYear = DateTime.Today.Year;
        SelectedMonth = DateTime.Today.Month;
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
    private async Task ExportToCsvAsync()
    {
        if (IsLoading) return;

        try
        {
            var monthName = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
            var suggestedName = $"transactions_{SelectedYear}_{SelectedMonth:D2}.csv";
            var title = $"{_localizationService.GetString("ExportTitle") ?? "Export"} - {monthName}";

            // Desktop: FileDialog, Android: direkt in Export-Verzeichnis
            var targetPath = await _fileDialogService.SaveFileAsync(suggestedName, title, "CSV", "csv");
            if (targetPath == null)
            {
                var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
                targetPath = Path.Combine(exportDir, suggestedName);
            }

            IsLoading = true;
            var filePath = await _exportService.ExportToCsvAsync(SelectedYear, SelectedMonth, targetPath);

            // Datei teilen/oeffnen
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

    [RelayCommand]
    private async Task ExportAllToCsvAsync()
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

            // Datei teilen/oeffnen
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
    }

    #endregion
}

/// <summary>
/// Wrapper for ExpenseCategory with selection state for chip-based UI.
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

    public string CategoryDisplay => Category switch
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
