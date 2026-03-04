using CommunityToolkit.Mvvm.ComponentModel;
using FinanzRechner.Helpers;

namespace FinanzRechner.Models;

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
