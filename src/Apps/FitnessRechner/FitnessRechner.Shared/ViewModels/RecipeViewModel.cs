using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;

namespace FitnessRechner.ViewModels;

/// <summary>
/// ViewModel für den Rezept-Editor.
/// Ermöglicht Erstellen, Bearbeiten, Löschen und Loggen von Rezepten.
/// </summary>
public sealed partial class RecipeViewModel : ViewModelBase
{
    private readonly IFoodSearchService _foodSearchService;
    private readonly ILocalizationService _localization;

    /// <summary>Navigation anfordern (z.B. zurück).</summary>
    public event Action<string>? NavigationRequested;

    /// <summary>Nachricht anzeigen (Titel, Text).</summary>
    public event Action<string, string>? MessageRequested;

    /// <summary>Floating-Text anzeigen (Text, Kategorie).</summary>
    public event Action<string, string>? FloatingTextRequested;

    public RecipeViewModel(
        IFoodSearchService foodSearchService,
        ILocalizationService localization)
    {
        _foodSearchService = foodSearchService;
        _localization = localization;
    }

    #region Rezeptliste

    [ObservableProperty]
    private ObservableCollection<Recipe> _recipes = [];

    [ObservableProperty]
    private Recipe? _selectedRecipe;

    [ObservableProperty]
    private bool _hasRecipes;

    #endregion

    #region Editor-Zustand

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _recipeName = "";

    [ObservableProperty]
    private string _recipeDescription = "";

    [ObservableProperty]
    private int _recipeServings = 1;

    [ObservableProperty]
    private ObservableCollection<RecipeIngredient> _ingredients = [];

    /// <summary>ID des gerade bearbeiteten Rezepts (null = neues Rezept).</summary>
    private string? _editingRecipeId;

    #endregion

    #region Zutatsuche

    [ObservableProperty]
    private bool _isSearchingIngredient;

    [ObservableProperty]
    private string _ingredientSearchQuery = "";

    [ObservableProperty]
    private ObservableCollection<FoodSearchResult> _ingredientSearchResults = [];

    [ObservableProperty]
    private bool _hasIngredientResults;

    [ObservableProperty]
    private FoodItem? _selectedIngredientFood;

    [ObservableProperty]
    private double _ingredientGrams = 100;

    [ObservableProperty]
    private bool _showIngredientPanel;

    #endregion

    #region Berechnete Nährwerte

    [ObservableProperty]
    private double _totalCalories;

    [ObservableProperty]
    private double _totalProtein;

    [ObservableProperty]
    private double _totalCarbs;

    [ObservableProperty]
    private double _totalFat;

    [ObservableProperty]
    private double _caloriesPerServing;

    #endregion

    #region Mahlzeit-Auswahl beim Loggen

    [ObservableProperty]
    private bool _showLogPanel;

    [ObservableProperty]
    private int _logMealType;

    [ObservableProperty]
    private int _logServingCount = 1;

    // Lokalisierte Labels für XAML-Bindings
    public string ServingsLabel => _localization.GetString("RecipeServings") ?? "Servings";
    public string IngredientsLabel => _localization.GetString("RecipeIngredientsLabel") ?? "Ingredients";

    public List<string> Meals =>
    [
        _localization.GetString("Breakfast") ?? "Breakfast",
        _localization.GetString("Lunch") ?? "Lunch",
        _localization.GetString("Dinner") ?? "Dinner",
        _localization.GetString("Snack") ?? "Snack"
    ];

    #endregion

    #region Lifecycle

    /// <summary>Aktualisiert lokalisierte Texte nach Sprachwechsel.</summary>
    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(ServingsLabel));
        OnPropertyChanged(nameof(IngredientsLabel));
        OnPropertyChanged(nameof(Meals));
    }

    /// <summary>Wird aufgerufen wenn die View sichtbar wird.</summary>
    public async Task OnAppearingAsync()
    {
        await LoadRecipesAsync();
    }

    private async Task LoadRecipesAsync()
    {
        try
        {
            var recipes = await _foodSearchService.GetRecipesAsync();
            Recipes = new ObservableCollection<Recipe>(recipes);
            HasRecipes = Recipes.Count > 0;
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorLoadingData") ?? "Fehler beim Laden");
        }
    }

    #endregion

    #region Rezept erstellen/bearbeiten

    /// <summary>Neues Rezept anlegen - Editor öffnen.</summary>
    [RelayCommand]
    private void CreateRecipe()
    {
        _editingRecipeId = null;
        RecipeName = "";
        RecipeDescription = "";
        RecipeServings = 1;
        Ingredients = [];
        ResetIngredientSearch();
        RecalculateNutrition();
        IsEditing = true;
    }

    /// <summary>Bestehendes Rezept bearbeiten.</summary>
    [RelayCommand]
    private void EditRecipe(Recipe recipe)
    {
        _editingRecipeId = recipe.Id;
        RecipeName = recipe.Name;
        RecipeDescription = recipe.Description;
        RecipeServings = recipe.Servings;
        // Zutaten kopieren damit das Original nicht verändert wird
        Ingredients = new ObservableCollection<RecipeIngredient>(
            recipe.Ingredients.Select(i => new RecipeIngredient
            {
                Food = i.Food,
                Grams = i.Grams
            }));
        ResetIngredientSearch();
        RecalculateNutrition();
        IsEditing = true;
    }

    /// <summary>Rezept speichern (neu oder bearbeitet).</summary>
    [RelayCommand]
    private async Task SaveRecipe()
    {
        if (string.IsNullOrWhiteSpace(RecipeName))
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("RecipeNameRequired") ?? "Bitte einen Namen eingeben");
            return;
        }

        if (Ingredients.Count == 0)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("RecipeNeedsIngredients") ?? "Mindestens eine Zutat hinzufügen");
            return;
        }

        try
        {
            var recipe = new Recipe
            {
                Id = _editingRecipeId ?? Guid.NewGuid().ToString(),
                Name = RecipeName.Trim(),
                Description = RecipeDescription.Trim(),
                Servings = Math.Max(1, RecipeServings),
                Ingredients = Ingredients.ToList(),
                CreatedAt = DateTime.UtcNow
            };

            if (_editingRecipeId != null)
            {
                await _foodSearchService.UpdateRecipeAsync(recipe);
            }
            else
            {
                await _foodSearchService.SaveRecipeAsync(recipe);
            }

            IsEditing = false;
            await LoadRecipesAsync();

            FloatingTextRequested?.Invoke(
                _localization.GetString("RecipeSaved") ?? "Rezept gespeichert", "success");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Fehler beim Speichern");
        }
    }

    /// <summary>Editor abbrechen.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        ResetIngredientSearch();
    }

    /// <summary>Rezept löschen.</summary>
    [RelayCommand]
    private async Task DeleteRecipe(Recipe recipe)
    {
        try
        {
            await _foodSearchService.DeleteRecipeAsync(recipe.Id);
            await LoadRecipesAsync();

            FloatingTextRequested?.Invoke(
                _localization.GetString("RecipeDeleted") ?? "Rezept gelöscht", "info");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorDeletingData") ?? "Fehler beim Löschen");
        }
    }

    #endregion

    #region Zutaten verwalten

    /// <summary>Zutatsuche starten.</summary>
    [RelayCommand]
    private void StartAddIngredient()
    {
        ResetIngredientSearch();
        IsSearchingIngredient = true;
    }

    /// <summary>Zutatsuche-Eingabe verarbeiten (debounced).</summary>
    partial void OnIngredientSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IngredientSearchResults.Clear();
            HasIngredientResults = false;
            return;
        }

        var results = _foodSearchService.Search(value, 10);
        IngredientSearchResults = new ObservableCollection<FoodSearchResult>(results);
        HasIngredientResults = IngredientSearchResults.Count > 0;
    }

    /// <summary>Lebensmittel aus der Suche auswählen.</summary>
    [RelayCommand]
    private void SelectIngredientFood(FoodSearchResult result)
    {
        SelectedIngredientFood = result.Food;
        IngredientGrams = result.Food.DefaultPortionGrams;
        ShowIngredientPanel = true;
    }

    /// <summary>Ausgewählte Zutat mit Menge zum Rezept hinzufügen.</summary>
    [RelayCommand]
    private void ConfirmAddIngredient()
    {
        if (SelectedIngredientFood == null || IngredientGrams <= 0) return;

        var ingredient = new RecipeIngredient
        {
            Food = SelectedIngredientFood,
            Grams = IngredientGrams
        };

        Ingredients.Add(ingredient);
        RecalculateNutrition();
        ResetIngredientSearch();
    }

    /// <summary>Zutat aus dem Rezept entfernen.</summary>
    [RelayCommand]
    private void RemoveIngredient(RecipeIngredient ingredient)
    {
        Ingredients.Remove(ingredient);
        RecalculateNutrition();
    }

    /// <summary>Zutatsuche zurücksetzen.</summary>
    private void ResetIngredientSearch()
    {
        IsSearchingIngredient = false;
        ShowIngredientPanel = false;
        IngredientSearchQuery = "";
        IngredientSearchResults.Clear();
        HasIngredientResults = false;
        SelectedIngredientFood = null;
        IngredientGrams = 100;
    }

    /// <summary>Zutat-Hinzufügen abbrechen.</summary>
    [RelayCommand]
    private void CancelAddIngredient()
    {
        ResetIngredientSearch();
    }

    #endregion

    #region Rezept als Mahlzeit loggen

    /// <summary>Log-Panel öffnen für das ausgewählte Rezept.</summary>
    [RelayCommand]
    private void UseRecipe(Recipe recipe)
    {
        SelectedRecipe = recipe;
        LogMealType = 0;
        LogServingCount = 1;
        ShowLogPanel = true;
    }

    /// <summary>Rezept als FoodLogEntry in das Tages-Log eintragen.</summary>
    [RelayCommand]
    private async Task ConfirmUseRecipe()
    {
        if (SelectedRecipe == null) return;

        try
        {
            var servings = Math.Max(1, LogServingCount);
            var factor = (double)servings / Math.Max(1, SelectedRecipe.Servings);

            var entry = new FoodLogEntry
            {
                Date = DateTime.Today,
                FoodName = SelectedRecipe.Name,
                Grams = SelectedRecipe.Ingredients.Sum(i => i.Grams) * factor,
                Calories = SelectedRecipe.TotalCalories * factor,
                Protein = SelectedRecipe.TotalProtein * factor,
                Carbs = SelectedRecipe.TotalCarbs * factor,
                Fat = SelectedRecipe.TotalFat * factor,
                Meal = (MealType)LogMealType
            };

            await _foodSearchService.SaveFoodLogAsync(entry);
            await _foodSearchService.IncrementRecipeUsageAsync(SelectedRecipe.Id);

            ShowLogPanel = false;
            SelectedRecipe = null;
            await LoadRecipesAsync();

            FloatingTextRequested?.Invoke(
                $"+{entry.Calories:F0} kcal", "success");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Fehler beim Speichern");
        }
    }

    /// <summary>Log-Panel schließen.</summary>
    [RelayCommand]
    private void CancelUseRecipe()
    {
        ShowLogPanel = false;
        SelectedRecipe = null;
    }

    #endregion

    #region Nährwert-Berechnung

    /// <summary>Gesamtnährwerte aus allen Zutaten neu berechnen.</summary>
    private void RecalculateNutrition()
    {
        TotalCalories = Ingredients.Sum(i => i.Calories);
        TotalProtein = Ingredients.Sum(i => i.Protein);
        TotalCarbs = Ingredients.Sum(i => i.Carbs);
        TotalFat = Ingredients.Sum(i => i.Fat);

        var servings = Math.Max(1, RecipeServings);
        CaloriesPerServing = TotalCalories / servings;
    }

    partial void OnRecipeServingsChanged(int value)
    {
        if (value < 1)
        {
            RecipeServings = 1;
            return;
        }
        RecalculateNutrition();
    }

    #endregion

    #region Navigation

    [RelayCommand]
    private void GoBack()
    {
        if (IsEditing)
        {
            IsEditing = false;
            ResetIngredientSearch();
            return;
        }

        NavigationRequested?.Invoke("..");
    }

    #endregion
}
