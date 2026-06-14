namespace FinanzRechner.ViewModels;

// Partial: Lokalisierte Text-Properties + Sprachwechsel-Benachrichtigung.
public sealed partial class ExpenseTrackerViewModel
{
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
}
