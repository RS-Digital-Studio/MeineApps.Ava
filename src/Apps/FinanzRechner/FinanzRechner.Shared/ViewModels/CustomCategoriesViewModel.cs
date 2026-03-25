using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanzRechner.Models;
using FinanzRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace FinanzRechner.ViewModels;

/// <summary>
/// ViewModel für benutzerdefinierte Kategorien.
/// </summary>
public sealed partial class CustomCategoriesViewModel : ViewModelBase
{
    private readonly ICustomCategoryService _categoryService;
    private readonly ILocalizationService _localizationService;

    public event Action<string>? NavigationRequested;
    public event Action? DataChanged;
    public event Action<string, string>? FloatingTextRequested;

    public ObservableCollection<CustomCategory> ExpenseCategories { get; } = [];
    public ObservableCollection<CustomCategory> IncomeCategories { get; } = [];

    [ObservableProperty] private bool _hasCategories;
    [ObservableProperty] private bool _showAddDialog;
    [ObservableProperty] private bool _isEditing;

    // Vordefinierte Icons zur Auswahl
    public static readonly string[] AvailableIcons =
    [
        "\U0001F697", "\U0001F431", "\U0001F436", "\U0001F3E0", "\U0001F4BB",
        "\U0001F3AE", "\U0001F3B5", "\U0001F4F1", "\U00002708", "\U0001F393",
        "\U0001F48A", "\U0001F451", "\U0001F3CB", "\U0001F37D", "\U0001F6CD",
        "\U0001F4E6", "\U0001F527", "\U0001F3A8", "\U0001F4DA", "\U0001F381"
    ];

    // Vordefinierte Farben zur Auswahl
    public static readonly string[] AvailableColors =
    [
        "#EF4444", "#F97316", "#EAB308", "#22C55E", "#10B981",
        "#06B6D4", "#3B82F6", "#6366F1", "#8B5CF6", "#EC4899",
        "#F43F5E", "#78716C", "#0EA5E9", "#14B8A6", "#A855F7"
    ];

    // Add/Edit Formular
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private int _editTypeIndex; // 0=Expense, 1=Income
    [ObservableProperty] private string _editIcon = "\U0001F4E6";
    [ObservableProperty] private string _editColorHex = "#3B82F6";
    private string? _editingCategoryId;

    // Lokalisierte Texte
    [ObservableProperty] private string _titleText = "Eigene Kategorien";
    [ObservableProperty] private string _addCategoryText = "Kategorie hinzufügen";
    [ObservableProperty] private string _emptyText = "Noch keine eigenen Kategorien.";
    [ObservableProperty] private string _expenseCategoriesText = "Ausgaben";
    [ObservableProperty] private string _incomeCategoriesText = "Einnahmen";

    public CustomCategoriesViewModel(
        ICustomCategoryService categoryService,
        ILocalizationService localizationService)
    {
        _categoryService = categoryService;
        _localizationService = localizationService;
        UpdateLocalizedTexts();
    }

    public async Task LoadDataAsync()
    {
        var all = await _categoryService.GetAllCategoriesAsync();
        ExpenseCategories.Clear();
        IncomeCategories.Clear();

        foreach (var c in all)
        {
            if (c.Type == TransactionType.Expense)
                ExpenseCategories.Add(c);
            else
                IncomeCategories.Add(c);
        }

        HasCategories = ExpenseCategories.Count > 0 || IncomeCategories.Count > 0;
    }

    [RelayCommand]
    private void OpenAddDialog()
    {
        _editingCategoryId = null;
        IsEditing = false;
        EditName = string.Empty;
        EditTypeIndex = 0;
        EditIcon = "\U0001F4E6";
        EditColorHex = "#3B82F6";
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void EditCategory(CustomCategory category)
    {
        _editingCategoryId = category.Id;
        IsEditing = true;
        EditName = category.Name;
        EditTypeIndex = category.Type == TransactionType.Income ? 1 : 0;
        EditIcon = category.Icon;
        EditColorHex = category.ColorHex;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;

        var type = EditTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

        try
        {
            if (IsEditing && _editingCategoryId != null)
            {
                var existing = await _categoryService.GetCategoryAsync(_editingCategoryId);
                if (existing != null)
                {
                    existing.Name = EditName;
                    existing.Type = type;
                    existing.Icon = EditIcon;
                    existing.ColorHex = EditColorHex;
                    await _categoryService.UpdateCategoryAsync(existing);
                }
            }
            else
            {
                await _categoryService.CreateCategoryAsync(new CustomCategory
                {
                    Name = EditName,
                    Type = type,
                    Icon = EditIcon,
                    ColorHex = EditColorHex
                });
            }

            ShowAddDialog = false;
            await LoadDataAsync();
            DataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            FloatingTextRequested?.Invoke(ex.Message, "error");
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(CustomCategory category)
    {
        await _categoryService.DeleteCategoryAsync(category.Id);
        await LoadDataAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void CancelDialog() => ShowAddDialog = false;

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    public string CancelText => _localizationService.GetString("Cancel") ?? "Cancel";
    public string SaveText => _localizationService.GetString("Save") ?? "Save";
    public string EditText => _localizationService.GetString("Edit") ?? "Edit";
    public string DeleteText => _localizationService.GetString("Delete") ?? "Delete";
    public string CategoryNameText => _localizationService.GetString("CategoryName") ?? "Category name";
    public string IconLabelText => _localizationService.GetString("IconLabel") ?? "Icon:";

    public void UpdateLocalizedTexts()
    {
        TitleText = _localizationService.GetString("CustomCategoriesTitle") ?? "Eigene Kategorien";
        AddCategoryText = _localizationService.GetString("AddCustomCategory") ?? "Kategorie hinzufügen";
        EmptyText = _localizationService.GetString("NoCustomCategoriesYet") ?? "Noch keine eigenen Kategorien.";
        ExpenseCategoriesText = _localizationService.GetString("ExpenseCategories") ?? "Ausgaben";
        IncomeCategoriesText = _localizationService.GetString("IncomeCategories") ?? "Einnahmen";
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(CategoryNameText));
        OnPropertyChanged(nameof(IconLabelText));
    }
}
