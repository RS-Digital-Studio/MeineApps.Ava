using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.ViewModels;

public sealed partial class ExpenseTrackerViewModel : ViewModelBase, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IExportService _exportService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IFileShareService _fileShareService;
    private readonly IPurchaseService _purchaseService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IExpenseFilterService _filterService;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    /// <summary>Wird ausgelöst wenn Daten geändert wurden (Add/Delete/Update), damit andere VMs ihren Cache invalidieren.</summary>
    public event Action? DataChanged;

    public ExpenseTrackerViewModel(IExpenseService expenseService, ILocalizationService localizationService,
        IExportService exportService, IFileDialogService fileDialogService,
        IFileShareService fileShareService,
        IPurchaseService purchaseService, IRewardedAdService rewardedAdService,
        IExpenseFilterService filterService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
        _exportService = exportService;
        _fileDialogService = fileDialogService;
        _fileShareService = fileShareService;
        _purchaseService = purchaseService;
        _rewardedAdService = rewardedAdService;
        _filterService = filterService;

        // Auf aktuellen Monat initialisieren
        _selectedYear = DateTime.Today.Year;
        _selectedMonth = DateTime.Today.Month;
    }

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

    public string MonthYearDisplay => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy", _localizationService.CurrentCulture);

    public string TotalExpensesDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalExpenses) : CurrencyHelper.Format(0m);
    public string TotalIncomeDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalIncome) : CurrencyHelper.Format(0m);
    public string BalanceDisplay => Summary != null ? CurrencyHelper.Format(Summary.Balance) : CurrencyHelper.Format(0m);

    // Legacy-Kompatibilität
    public string TotalDisplay => Summary != null ? CurrencyHelper.Format(Summary.TotalAmount) : CurrencyHelper.Format(0m);

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
    private decimal _newExpenseAmount;

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
    private decimal _minAmountFilter = 0m;

    [ObservableProperty]
    private decimal _maxAmountFilter = 0m;

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
        var criteria = new ExpenseFilterCriteria(
            SearchTerm,
            SelectedFilter,
            SelectedCategoryFilter,
            MinAmountFilter,
            MaxAmountFilter,
            SelectedSort);

        // Filterung + Sortierung in den Service ausgelagert (reine, testbare Berechnung)
        var filtered = _filterService.Apply(_allExpenses, criteria);

        // Bestehende Container wiederverwenden: Clear+Add behält die Item-Container
        // (inkl. SwipeToReveal/StaggerFadeIn-Behaviors). Neue Collection-Zuweisung würde
        // alle Container disposen + neu erzeugen (sichtbares Flackern, hoher GC-Druck).
        Expenses.Clear();
        foreach (var e in filtered) Expenses.Add(e);
        HasExpenses = Expenses.Count > 0;

        // Nach Datum gruppieren
        UpdateGroupedExpenses(filtered);

        // Filterstatus aktualisieren
        IsFilterActive = _filterService.IsFilterActive(criteria);

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
                {
                    // Sprachwechsel kann von System-Culture abweichen — explizit die
                    // Service-Culture nutzen, sonst bleibt der Wochentag z.B. deutsch
                    // obwohl der User in den Settings auf Spanisch gewechselt hat.
                    dateDisplay = g.Key.ToString("dddd, dd. MMMM", _localizationService.CurrentCulture);
                }

                return new ExpenseGroup(g.Key, dateDisplay, g.OrderByDescending(e => e.Date));
            })
            .ToList();

        GroupedExpenses.Clear();
        foreach (var g in groups) GroupedExpenses.Add(g);
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchTerm = string.Empty;
        SelectedFilter = FilterTypeOption.All;
        SelectedCategoryFilter = null;
        MinAmountFilter = 0m;
        MaxAmountFilter = 0m;
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
        if (string.IsNullOrWhiteSpace(NewExpenseDescription) || NewExpenseAmount <= 0m)
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

    #endregion

    #region Helpers

    private void ResetForm()
    {
        NewExpenseDate = DateTime.Today;
        NewExpenseDescription = string.Empty;
        NewExpenseAmount = 0m;
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
