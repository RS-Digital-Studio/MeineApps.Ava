using FinanzRechner.Models;
using static FinanzRechner.ViewModels.ExpenseTrackerViewModel;

namespace FinanzRechner.Services;

/// <summary>
/// Filter- und Sortier-Kriterien für eine Transaktionsliste.
/// Reine Datenstruktur ohne UI-Bezug (testbar).
/// </summary>
public readonly record struct ExpenseFilterCriteria(
    string? SearchTerm,
    FilterTypeOption TypeFilter,
    ExpenseCategory? CategoryFilter,
    decimal MinAmount,
    decimal MaxAmount,
    SortOption Sort);

/// <summary>
/// Filtert und sortiert Transaktionslisten anhand von <see cref="ExpenseFilterCriteria"/>.
/// Reine Berechnungslogik ohne Avalonia-/Persistenz-API — gehört laut Architektur in einen Service.
/// </summary>
public interface IExpenseFilterService
{
    /// <summary>
    /// Wendet alle Filter (Suche, Typ, Kategorie, Betrag) und die Sortierung auf <paramref name="source"/> an.
    /// Liefert eine neue Liste; die Eingabe bleibt unverändert.
    /// </summary>
    List<Expense> Apply(IReadOnlyList<Expense> source, ExpenseFilterCriteria criteria);

    /// <summary>
    /// Prüft, ob mindestens ein Filter-Kriterium aktiv ist (für die Filter-Status-Anzeige).
    /// </summary>
    bool IsFilterActive(ExpenseFilterCriteria criteria);
}
