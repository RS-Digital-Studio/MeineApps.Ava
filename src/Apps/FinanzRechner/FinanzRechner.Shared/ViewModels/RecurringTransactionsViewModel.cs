using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;

namespace FinanzRechner.ViewModels;

public partial class RecurringTransactionsViewModel : ObservableObject, IDisposable
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;

    public event Action<string, string>? MessageRequested;
    public event Action? DataChanged;

    public RecurringTransactionsViewModel(IExpenseService expenseService, ILocalizationService localizationService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
    }

    #region Localized Text Properties

    public string RecurringTransactionsTitleText => _localizationService.GetString("RecurringTransactions") ?? "Recurring Transactions";
    public string NoRecurringText => _localizationService.GetString("EmptyRecurringTitle") ?? "No Recurring Entries";
    public string NoRecurringHintText => _localizationService.GetString("EmptyRecurringDesc") ?? "Add recurring income or expenses that repeat automatically";
    public string NextDueText => _localizationService.GetString("NextDue") ?? "Next:";
    public string ActiveText => _localizationService.GetString("Active") ?? "Active";
    public string InactiveText => _localizationService.GetString("Inactive") ?? "Inactive";
    public string RecurringTransactionText => _localizationService.GetString("RecurringTransaction") ?? "Recurring Transaction";
    public string ExpenseText => _localizationService.GetString("Expense") ?? "Expense";
    public string IncomeText => _localizationService.GetString("Income") ?? "Income";
    public string DescriptionWatermarkText => _localizationService.GetString("Description") ?? "Description";
    public string AmountWatermarkText => _localizationService.GetString("Amount") ?? "Amount";
    public string CategoryText => _localizationService.GetString("Category") ?? "Category";
    public string PatternLabelText => _localizationService.GetString("Pattern") ?? "Pattern";
    public string SetEndDateText => _localizationService.GetString("SetEndDate") ?? "Set end date";
    public string NoteWatermarkText => _localizationService.GetString("Note") ?? "Note";
    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string UndoText => _localizationService.GetString("Undo") ?? "Undo";
    public string EditTooltipText => _localizationService.GetString("Edit") ?? "Edit";
    public string DeleteTooltipText => _localizationService.GetString("Delete") ?? "Delete";

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(RecurringTransactionsTitleText));
        OnPropertyChanged(nameof(NoRecurringText));
        OnPropertyChanged(nameof(NoRecurringHintText));
        OnPropertyChanged(nameof(NextDueText));
        OnPropertyChanged(nameof(ActiveText));
        OnPropertyChanged(nameof(InactiveText));
        OnPropertyChanged(nameof(RecurringTransactionText));
        OnPropertyChanged(nameof(ExpenseText));
        OnPropertyChanged(nameof(IncomeText));
        OnPropertyChanged(nameof(DescriptionWatermarkText));
        OnPropertyChanged(nameof(AmountWatermarkText));
        OnPropertyChanged(nameof(CategoryText));
        OnPropertyChanged(nameof(PatternLabelText));
        OnPropertyChanged(nameof(SetEndDateText));
        OnPropertyChanged(nameof(NoteWatermarkText));
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(UndoText));
        OnPropertyChanged(nameof(EditTooltipText));
        OnPropertyChanged(nameof(DeleteTooltipText));
    }

    #endregion

    #region Navigation Events

    public event Action<string>? NavigationRequested;
    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    #endregion

    [ObservableProperty]
    private ObservableCollection<RecurringTransaction> _recurringTransactions = [];

    [ObservableProperty]
    private bool _hasTransactions;

    [ObservableProperty]
    private bool _isLoading;

    // Undo-Löschen
    [ObservableProperty]
    private bool _showUndoDelete;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    private RecurringTransaction? _deletedTransaction;
    private CancellationTokenSource? _undoCancellation;

    [ObservableProperty]
    private bool _showAddDialog;

    // Formular-Felder
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private double _amount;

    [ObservableProperty]
    private ExpenseCategory _selectedCategory = ExpenseCategory.Other;

    [ObservableProperty]
    private TransactionType _transactionType = TransactionType.Expense;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    private RecurrencePattern _selectedPattern = RecurrencePattern.Monthly;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private bool _hasEndDate;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today.AddYears(1);

    [ObservableProperty]
    private bool _isActive = true;

    private RecurringTransaction? _editingTransaction;

    public List<ExpenseCategory> ExpenseCategories { get; } =
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

    public List<ExpenseCategory> IncomeCategories { get; } =
    [
        ExpenseCategory.Salary,
        ExpenseCategory.Freelance,
        ExpenseCategory.Investment,
        ExpenseCategory.Gift,
        ExpenseCategory.OtherIncome
    ];

    public List<ExpenseCategory> Categories => TransactionType == TransactionType.Expense
        ? ExpenseCategories
        : IncomeCategories;

    public List<RecurrencePattern> Patterns { get; } =
    [
        RecurrencePattern.Daily,
        RecurrencePattern.Weekly,
        RecurrencePattern.Biweekly,
        RecurrencePattern.Monthly,
        RecurrencePattern.Yearly
    ];

    partial void OnTransactionTypeChanged(TransactionType value)
    {
        SelectedCategory = value == TransactionType.Expense
            ? ExpenseCategory.Other
            : ExpenseCategory.Salary;
        OnPropertyChanged(nameof(Categories));
    }

    [RelayCommand]
    public async Task LoadTransactionsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            // ExpenseService sicherstellen dass initialisiert
            await _expenseService.InitializeAsync();

            var transactions = await _expenseService.GetAllRecurringTransactionsAsync();
            RecurringTransactions = new ObservableCollection<RecurringTransaction>(transactions);
            HasTransactions = RecurringTransactions.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        ResetForm();
        _editingTransaction = null;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void CancelAddDialog()
    {
        ShowAddDialog = false;
        ResetForm();
    }

    [RelayCommand]
    private async Task SaveTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(Description) || Amount <= 0)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("ErrorInvalidTransaction") ?? "Please enter description and amount.";
            MessageRequested?.Invoke(title, message);
            return;
        }

        // Enddatum muss nach Startdatum liegen
        if (HasEndDate && EndDate < StartDate)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("ErrorEndDateBeforeStart") ?? "End date must be after start date.";
            MessageRequested?.Invoke(title, message);
            return;
        }

        try
        {
            if (_editingTransaction != null)
            {
                // Bestehende aktualisieren
                _editingTransaction.Description = Description;
                _editingTransaction.Amount = Amount;
                _editingTransaction.Category = SelectedCategory;
                _editingTransaction.Type = TransactionType;
                _editingTransaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note;
                _editingTransaction.Pattern = SelectedPattern;
                _editingTransaction.StartDate = StartDate;
                _editingTransaction.EndDate = HasEndDate ? EndDate : null;
                _editingTransaction.IsActive = IsActive;

                await _expenseService.UpdateRecurringTransactionAsync(_editingTransaction);
            }
            else
            {
                // Neue erstellen
                var transaction = new RecurringTransaction
                {
                    Description = Description,
                    Amount = Amount,
                    Category = SelectedCategory,
                    Type = TransactionType,
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note,
                    Pattern = SelectedPattern,
                    StartDate = StartDate,
                    EndDate = HasEndDate ? EndDate : null,
                    IsActive = IsActive
                };

                await _expenseService.CreateRecurringTransactionAsync(transaction);
            }

            ShowAddDialog = false;
            ResetForm();
            await LoadTransactionsAsync();
            DataChanged?.Invoke();
        }
        catch (Exception)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("SaveError") ?? "Failed to save transaction. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private void EditTransaction(RecurringTransaction transaction)
    {
        _editingTransaction = transaction;
        Description = transaction.Description;
        Amount = transaction.Amount;
        SelectedCategory = transaction.Category;
        TransactionType = transaction.Type;
        Note = transaction.Note ?? string.Empty;
        SelectedPattern = transaction.Pattern;
        StartDate = transaction.StartDate;
        HasEndDate = transaction.EndDate.HasValue;
        EndDate = transaction.EndDate ?? DateTime.Today.AddYears(1);
        IsActive = transaction.IsActive;

        ShowAddDialog = true;
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(RecurringTransaction transaction)
    {
        CancellationTokenSource? cts = null;
        try
        {
            // Für Undo sichern
            _deletedTransaction = transaction;

            // Aus UI entfernen
            RecurringTransactions.Remove(transaction);
            HasTransactions = RecurringTransactions.Count > 0;

            // Undo-Benachrichtigung anzeigen
            UndoMessage = $"{_localizationService.GetString("RecurringDeleted") ?? "Recurring transaction deleted"} - {transaction.Description}";
            ShowUndoDelete = true;

            // Timer für permanente Löschung starten (5 Sekunden)
            _undoCancellation?.Cancel();
            _undoCancellation?.Dispose();
            cts = _undoCancellation = new CancellationTokenSource();

            await Task.Delay(5000, cts.Token);

            // Permanente Löschung nach 5 Sekunden
            if (_deletedTransaction != null)
            {
                await _expenseService.DeleteRecurringTransactionAsync(_deletedTransaction.Id);
                _deletedTransaction = null;
                ShowUndoDelete = false;
                DataChanged?.Invoke();
            }
        }
        catch (TaskCanceledException)
        {
            // Undo wurde ausgelöst - nichts tun
        }
        catch (OperationCanceledException)
        {
            // Undo wurde ausgelöst - nichts tun
        }
        catch (Exception)
        {
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("DeleteError") ?? "Failed to delete transaction. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private async Task UndoDeleteAsync()
    {
        // Timer stoppen
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();

        // Gelöschte Transaktion wiederherstellen
        if (_deletedTransaction != null)
        {
            await LoadTransactionsAsync();
            _deletedTransaction = null;
        }

        ShowUndoDelete = false;
    }

    [RelayCommand]
    private void DismissUndo()
    {
        ShowUndoDelete = false;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(RecurringTransaction transaction)
    {
        try
        {
            transaction.IsActive = !transaction.IsActive;
            await _expenseService.UpdateRecurringTransactionAsync(transaction);
            await LoadTransactionsAsync();
        }
        catch (Exception)
        {
            // Bei Fehler rückgängig machen
            transaction.IsActive = !transaction.IsActive;
            var title = _localizationService.GetString("Error") ?? "Error";
            var message = _localizationService.GetString("SaveError") ?? "Failed to update transaction. Please try again.";
            MessageRequested?.Invoke(title, message);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private void SetTransactionTypeExpense()
    {
        TransactionType = TransactionType.Expense;
    }

    [RelayCommand]
    private void SetTransactionTypeIncome()
    {
        TransactionType = TransactionType.Income;
    }

    private void ResetForm()
    {
        Description = string.Empty;
        Amount = 0;
        SelectedCategory = ExpenseCategory.Other;
        TransactionType = TransactionType.Expense;
        Note = string.Empty;
        SelectedPattern = RecurrencePattern.Monthly;
        StartDate = DateTime.Today;
        HasEndDate = false;
        EndDate = DateTime.Today.AddYears(1);
        IsActive = true;
        _editingTransaction = null;
    }

    public string GetPatternName(RecurrencePattern pattern)
    {
        var key = pattern switch
        {
            RecurrencePattern.Daily => "PatternDaily",
            RecurrencePattern.Weekly => "PatternWeekly",
            RecurrencePattern.Biweekly => "PatternBiweekly",
            RecurrencePattern.Monthly => "PatternMonthly",
            RecurrencePattern.Yearly => "PatternYearly",
            _ => null
        };
        return key != null ? (_localizationService.GetString(key) ?? pattern.ToString()) : pattern.ToString();
    }

    #region IDisposable

    public void Dispose()
    {
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;
    }

    #endregion
}
