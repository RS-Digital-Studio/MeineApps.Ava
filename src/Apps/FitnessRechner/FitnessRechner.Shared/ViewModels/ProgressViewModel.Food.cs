using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;

namespace FitnessRechner.ViewModels;

/// <summary>
/// Food-Search, Quick-Add, Mahlzeiten-Logging, "Gestern kopieren" Funktion.
/// </summary>
public sealed partial class ProgressViewModel
{
    #region Food Search

    [RelayCommand]
    private void ToggleFoodSearch()
    {
        ShowFoodSearch = !ShowFoodSearch;
        if (ShowFoodSearch)
        {
            ShowAddForm = false;
        }
        else
        {
            SearchQuery = "";
            SearchResults.Clear();
            SelectedFood = null;
            ShowAddFoodPanel = false;
        }
    }

    [RelayCommand]
    private void PerformFoodSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        var results = _foodSearchService.Search(SearchQuery, 15);
        SearchResults = new ObservableCollection<FoodSearchResult>(results);
    }

    [RelayCommand]
    private void SelectFoodItem(FoodSearchResult result)
    {
        SelectedFood = result.Food;
        PortionGrams = result.Food.DefaultPortionGrams;
        UpdateFoodCalculations();
        ShowAddFoodPanel = true;
    }

    partial void OnSearchQueryChanged(string value)
    {
        PerformFoodSearch();
    }

    partial void OnPortionGramsChanged(double value)
    {
        UpdateFoodCalculations();
    }

    private void UpdateFoodCalculations()
    {
        if (SelectedFood == null)
        {
            CalculatedCalories = 0;
            CalculatedProtein = 0;
            CalculatedCarbs = 0;
            CalculatedFat = 0;
            return;
        }

        var factor = PortionGrams / 100.0;
        CalculatedCalories = Math.Round(SelectedFood.CaloriesPer100g * factor, 1);
        CalculatedProtein = Math.Round(SelectedFood.ProteinPer100g * factor, 1);
        CalculatedCarbs = Math.Round(SelectedFood.CarbsPer100g * factor, 1);
        CalculatedFat = Math.Round(SelectedFood.FatPer100g * factor, 1);
    }

    #endregion

    #region Add Food to Log

    [RelayCommand]
    private async Task AddFoodToLog()
    {
        if (SelectedFood == null) return;

        var entry = new FoodLogEntry
        {
            Date = DateTime.Today,
            FoodName = SelectedFood.Name,
            Grams = PortionGrams,
            Calories = CalculatedCalories,
            Protein = CalculatedProtein,
            Carbs = CalculatedCarbs,
            Fat = CalculatedFat,
            Meal = (MealType)SelectedMeal
        };

        await _foodSearchService.SaveFoodLogAsync(entry);

        // Floating Text für hinzugefügtes Essen
        FloatingTextRequested?.Invoke($"+{entry.Calories:F0} kcal", "info");

        SelectedFood = null;
        SearchQuery = "";
        ShowAddFoodPanel = false;
        ShowFoodSearch = false;

        await LoadCalorieDataAsync();
    }

    [RelayCommand]
    private void CancelAddFood()
    {
        SelectedFood = null;
        ShowAddFoodPanel = false;
    }

    #endregion

    #region Gestern kopieren

    [RelayCommand]
    private async Task CopyYesterdayMeals()
    {
        var yesterdayMeals = await _foodSearchService.GetFoodLogAsync(DateTime.Today.AddDays(-1));
        if (yesterdayMeals.Count == 0)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.NoMealsYesterday);
            return;
        }

        foreach (var meal in yesterdayMeals)
        {
            var copy = new FoodLogEntry
            {
                Date = DateTime.Today,
                FoodName = meal.FoodName,
                Grams = meal.Grams,
                Calories = meal.Calories,
                Protein = meal.Protein,
                Carbs = meal.Carbs,
                Fat = meal.Fat,
                Meal = meal.Meal
            };
            await _foodSearchService.SaveFoodLogAsync(copy);
        }

        FloatingTextRequested?.Invoke(
            string.Format(AppStrings.MealsCopied, yesterdayMeals.Count), "success");
        await LoadCalorieDataAsync();
    }

    #endregion
}
