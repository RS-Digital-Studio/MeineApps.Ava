using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.ViewModels.Calculators;

public partial class WaterViewModel : ObservableObject
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

    public WaterViewModel(FitnessEngine fitnessEngine, IPreferencesService preferences, ILocalizationService localization)
    {
        _fitnessEngine = fitnessEngine;
        _preferences = preferences;
        _localization = localization;
    }

    [ObservableProperty]
    private double _weight = 70;

    [ObservableProperty]
    private int _activityMinutes = 30;

    [ObservableProperty]
    private bool _isHotWeather = false;

    [ObservableProperty]
    private WaterResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public string TotalLitersDisplay => Result != null ? $"{Result.TotalLiters:F1} L" : "";
    public double TotalLitersValue => Result?.TotalLiters ?? 0;
    public string GlassesDisplay => Result != null ? $"{Result.Glasses}" : "";
    public string BaseWaterDisplay => Result != null ? $"{Result.BaseWater:F1} L" : "";
    public string ActivityWaterDisplay => Result != null ? $"+{Result.ActivityWater:F1} L" : "";
    public string HeatWaterDisplay => Result != null ? $"+{Result.HeatWater:F1} L" : "";

    partial void OnResultChanged(WaterResult? value)
    {
        OnPropertyChanged(nameof(TotalLitersDisplay));
        OnPropertyChanged(nameof(TotalLitersValue));
        OnPropertyChanged(nameof(GlassesDisplay));
        OnPropertyChanged(nameof(BaseWaterDisplay));
        OnPropertyChanged(nameof(ActivityWaterDisplay));
        OnPropertyChanged(nameof(HeatWaterDisplay));
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Weight < 20 || Weight > 300 || ActivityMinutes < 0 || ActivityMinutes > 480)
        {
            HasResult = false;
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertInvalidInput"));
            return;
        }

        Result = _fitnessEngine.CalculateWater(Weight, ActivityMinutes, IsHotWeather);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        Weight = 70;
        ActivityMinutes = 30;
        IsHotWeather = false;
        Result = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private void SaveWaterGoal()
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
            // Wasserziel in Milliliter speichern (alle anderen VMs erwarten ml)
            _preferences.Set(PreferenceKeys.WaterGoal, Result.TotalLiters * 1000);

            MessageRequested?.Invoke(
                _localization.GetString("AlertSuccess"),
                _localization.GetString("AlertWaterGoalSaved"));

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
}
