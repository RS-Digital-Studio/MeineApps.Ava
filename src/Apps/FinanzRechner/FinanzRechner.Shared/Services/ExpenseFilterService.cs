using FinanzRechner.Models;
using static FinanzRechner.ViewModels.ExpenseTrackerViewModel;

namespace FinanzRechner.Services;

/// <summary>
/// Standard-Implementierung der Filter-/Sortier-Logik für Transaktionslisten.
/// Verhalten 1:1 aus <c>ExpenseTrackerViewModel.ApplyFilterAndSort</c> extrahiert.
/// </summary>
public sealed class ExpenseFilterService : IExpenseFilterService
{
    public List<Expense> Apply(IReadOnlyList<Expense> source, ExpenseFilterCriteria criteria)
    {
        // Optimierte Filterung: Ein Durchlauf mit List statt IEnumerable
        var filtered = new List<Expense>(source.Count);
        var hasSearch = !string.IsNullOrWhiteSpace(criteria.SearchTerm);

        foreach (var expense in source)
        {
            // Nach Suchbegriff filtern (OrdinalIgnoreCase statt ToLowerInvariant)
            if (hasSearch &&
                !expense.Description.Contains(criteria.SearchTerm!, StringComparison.OrdinalIgnoreCase) &&
                (expense.Note == null || !expense.Note.Contains(criteria.SearchTerm!, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Nach Transaktionstyp filtern
            if (criteria.TypeFilter == FilterTypeOption.Expenses && expense.Type != TransactionType.Expense)
                continue;
            if (criteria.TypeFilter == FilterTypeOption.Income && expense.Type != TransactionType.Income)
                continue;

            // Nach Kategorie filtern
            if (criteria.CategoryFilter.HasValue && expense.Category != criteria.CategoryFilter.Value)
                continue;

            // Nach Betrag filtern
            if (criteria.MinAmount > 0m && expense.Amount < criteria.MinAmount)
                continue;
            if (criteria.MaxAmount > 0m && expense.Amount > criteria.MaxAmount)
                continue;

            // Alle Filter bestanden - hinzufügen
            filtered.Add(expense);
        }

        // Sortierung anwenden (in-place)
        filtered.Sort(criteria.Sort switch
        {
            SortOption.DateAscending => (a, b) => a.Date.CompareTo(b.Date),
            SortOption.DateDescending => (a, b) => b.Date.CompareTo(a.Date),
            SortOption.AmountDescending => (a, b) => b.Amount.CompareTo(a.Amount),
            SortOption.AmountAscending => (a, b) => a.Amount.CompareTo(b.Amount),
            SortOption.Description => (a, b) => string.Compare(a.Description, b.Description, StringComparison.Ordinal),
            _ => (a, b) => b.Date.CompareTo(a.Date) // Default: DateDescending
        });

        return filtered;
    }

    public bool IsFilterActive(ExpenseFilterCriteria criteria) =>
        !string.IsNullOrWhiteSpace(criteria.SearchTerm) ||
        criteria.TypeFilter != FilterTypeOption.All ||
        criteria.CategoryFilter.HasValue ||
        criteria.MinAmount > 0m ||
        criteria.MaxAmount > 0m;
}
