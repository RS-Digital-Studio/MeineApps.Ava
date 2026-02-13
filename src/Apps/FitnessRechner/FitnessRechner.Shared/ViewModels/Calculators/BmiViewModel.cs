using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;

namespace FitnessRechner.ViewModels.Calculators;

public partial class BmiViewModel : ObservableObject
{
    private readonly FitnessEngine _fitnessEngine;
    private readonly ITrackingService _trackingService;
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

    public BmiViewModel(FitnessEngine fitnessEngine, ITrackingService trackingService, ILocalizationService localization)
    {
        _fitnessEngine = fitnessEngine;
        _trackingService = trackingService;
        _localization = localization;
    }

    [ObservableProperty]
    private double _weight = 70;

    [ObservableProperty]
    private double _height = 175;

    [ObservableProperty]
    private BmiResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public string BmiDisplay => Result != null ? $"{Result.Bmi:F1}" : "";
    public double BmiValue => Result?.Bmi ?? 0;
    public string CategoryDisplay => Result != null ? GetCategoryText(Result.Category) : "";
    public string HealthyRangeDisplay => Result != null
        ? $"{Result.MinHealthyWeight:F1} - {Result.MaxHealthyWeight:F1} kg" : "";

    partial void OnResultChanged(BmiResult? value)
    {
        OnPropertyChanged(nameof(BmiDisplay));
        OnPropertyChanged(nameof(BmiValue));
        OnPropertyChanged(nameof(CategoryDisplay));
        OnPropertyChanged(nameof(HealthyRangeDisplay));
    }

    private string GetCategoryText(BmiCategory category)
    {
        return category switch
        {
            BmiCategory.SevereUnderweight => _localization.GetString("BmiSevereUnderweight"),
            BmiCategory.ModerateUnderweight => _localization.GetString("BmiModerateUnderweight"),
            BmiCategory.MildUnderweight => _localization.GetString("BmiMildUnderweight"),
            BmiCategory.Normal => _localization.GetString("BmiNormal"),
            BmiCategory.Overweight => _localization.GetString("BmiOverweight"),
            BmiCategory.ObeseClass1 => _localization.GetString("BmiObeseClass1"),
            BmiCategory.ObeseClass2 => _localization.GetString("BmiObeseClass2"),
            BmiCategory.ObeseClass3 => _localization.GetString("BmiObeseClass3"),
            _ => ""
        };
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Weight < 20 || Weight > 300 || Height < 80 || Height > 250)
        {
            HasResult = false;
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertInvalidInput"));
            return;
        }

        Result = _fitnessEngine.CalculateBmi(Weight, Height);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        Weight = 70;
        Height = 175;
        Result = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }

    [RelayCommand]
    private async Task SaveToTracking()
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
            // Save weight
            var weightEntry = new TrackingEntry
            {
                Type = TrackingType.Weight,
                Value = Weight,
                Date = DateTime.Today,
                Note = $"{_localization.GetString("Height")}: {Height} cm"
            };
            await _trackingService.AddEntryAsync(weightEntry);

            // Save BMI
            var bmiEntry = new TrackingEntry
            {
                Type = TrackingType.Bmi,
                Value = Result.Bmi,
                Date = DateTime.Today,
                Note = $"{GetCategoryText(Result.Category)} - {Weight} kg, {Height} cm"
            };
            await _trackingService.AddEntryAsync(bmiEntry);

            MessageRequested?.Invoke(
                _localization.GetString("AlertSuccess"),
                _localization.GetString("AlertSavedToTracking"));

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
