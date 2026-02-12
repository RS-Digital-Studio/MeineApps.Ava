using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.ViewModels.Calculators;

public partial class CaloriesViewModel : ObservableObject
{
    private readonly FitnessEngine _fitnessEngine;
    private readonly IPreferencesService _preferences;
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Event for navigation requests (replaces Shell.Current.GoToAsync)
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event for showing messages to the user (title, message).
    /// </summary>
    public event Action<string, string>? MessageRequested;

    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    public CaloriesViewModel(FitnessEngine fitnessEngine, IPreferencesService preferences, ILocalizationService localization)
    {
        _fitnessEngine = fitnessEngine;
        _preferences = preferences;
        _localization = localization;
    }

    [ObservableProperty]
    private double _weight = 70;

    [ObservableProperty]
    private double _height = 175;

    [ObservableProperty]
    private int _age = 30;

    [ObservableProperty]
    private bool _isMale = true;

    [ObservableProperty]
    private int _activityLevelIndex = 2;

    /// <summary>
    /// Localized activity level names for the Picker
    /// </summary>
    public List<string> ActivityLevels =>
    [
        AppStrings.ActivitySedentary,
        AppStrings.ActivityLight,
        AppStrings.ActivityModerate,
        AppStrings.ActivityActive,
        AppStrings.ActivityVeryActive
    ];

    public double ActivityLevel => ActivityLevelIndex switch
    {
        0 => 1.2,   // Sedentary
        1 => 1.375, // Light
        2 => 1.55,  // Moderate
        3 => 1.725, // Active
        4 => 1.9,   // Very Active
        _ => 1.55
    };

    [ObservableProperty]
    private CaloriesResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public string BmrDisplay => Result != null ? $"{Result.Bmr:F0} kcal" : "";
    public string TdeeDisplay => Result != null ? $"{Result.Tdee:F0} kcal" : "";
    public string WeightLossDisplay => Result != null ? $"{Result.WeightLossCalories:F0} kcal" : "";
    public string WeightGainDisplay => Result != null ? $"{Result.WeightGainCalories:F0} kcal" : "";

    partial void OnResultChanged(CaloriesResult? value)
    {
        OnPropertyChanged(nameof(BmrDisplay));
        OnPropertyChanged(nameof(TdeeDisplay));
        OnPropertyChanged(nameof(WeightLossDisplay));
        OnPropertyChanged(nameof(WeightGainDisplay));
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Weight < 20 || Weight > 300 || Height < 80 || Height > 250 || Age < 8 || Age > 120)
        {
            HasResult = false;
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertInvalidInput"));
            return;
        }

        Result = _fitnessEngine.CalculateCalories(Weight, Height, Age, IsMale, ActivityLevel);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        Weight = 70;
        Height = 175;
        Age = 30;
        IsMale = true;
        ActivityLevelIndex = 2;
        Result = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private void SetAsCalorieGoal()
    {
        if (Result == null || !HasResult)
        {
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertCalculateFirst"));
            return;
        }

        try
        {
            // In Avalonia we don't have DisplayActionSheet, so we save the TDEE as default goal.
            // The UI can provide a selection mechanism separately.
            _preferences.Set("daily_calorie_goal", Result.Tdee);

            MessageRequested?.Invoke(
                _localization.GetString("AlertSuccess"),
                _localization.GetString("AlertCalorieGoalSaved"));

            // Navigate back
            NavigateTo("..");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                string.Format(_localization.GetString("AlertSaveError"), ex.Message));
        }
    }

    /// <summary>
    /// Set a specific calorie goal (maintenance, weight loss, or weight gain).
    /// Called from UI with the selected goal type.
    /// </summary>
    [RelayCommand]
    private void SetSpecificCalorieGoal(string goalType)
    {
        if (Result == null || !HasResult) return;

        double selectedGoal = goalType switch
        {
            "maintenance" => Result.Tdee,
            "weightloss" => Result.WeightLossCalories,
            "weightgain" => Result.WeightGainCalories,
            _ => 0
        };

        if (selectedGoal == 0) return;

        try
        {
            _preferences.Set("daily_calorie_goal", selectedGoal);

            MessageRequested?.Invoke(
                _localization.GetString("AlertSuccess"),
                _localization.GetString("AlertCalorieGoalSaved"));

            NavigateTo("..");
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                string.Format(_localization.GetString("AlertSaveError"), ex.Message));
        }
    }
}
