using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;

namespace FitnessRechner.ViewModels.Calculators;

public partial class BodyFatViewModel : ObservableObject
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

    public BodyFatViewModel(FitnessEngine fitnessEngine, ITrackingService trackingService, ILocalizationService localization)
    {
        _fitnessEngine = fitnessEngine;
        _trackingService = trackingService;
        _localization = localization;
    }

    [ObservableProperty]
    private double _height = 175;

    [ObservableProperty]
    private double _neck = 38;

    [ObservableProperty]
    private double _waist = 85;

    [ObservableProperty]
    private double _hip = 95;

    [ObservableProperty]
    private bool _isMale = true;

    [ObservableProperty]
    private BodyFatResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public bool ShowHip => !IsMale;

    partial void OnIsMaleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowHip));
    }

    public string BodyFatDisplay => Result != null ? $"{Result.BodyFatPercent:F1} %" : "";
    public string CategoryDisplay => Result != null ? GetCategoryText(Result.Category) : "";

    partial void OnResultChanged(BodyFatResult? value)
    {
        OnPropertyChanged(nameof(BodyFatDisplay));
        OnPropertyChanged(nameof(CategoryDisplay));
    }

    private string GetCategoryText(BodyFatCategory category)
    {
        return category switch
        {
            BodyFatCategory.Essential => _localization.GetString("BodyFatEssential"),
            BodyFatCategory.Athletes => _localization.GetString("BodyFatAthletes"),
            BodyFatCategory.Fitness => _localization.GetString("BodyFatFitness"),
            BodyFatCategory.Average => _localization.GetString("BodyFatAverage"),
            BodyFatCategory.Obese => _localization.GetString("BodyFatObese"),
            _ => ""
        };
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Height < 80 || Height > 250 ||
            Neck < 25 || Neck > 65 ||
            Waist < 50 || Waist > 200 ||
            (!IsMale && (Hip < 60 || Hip > 200)))
        {
            HasResult = false;
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertInvalidInput"));
            return;
        }

        // Navy-Methode: Taillenumfang muss größer als Halsumfang sein
        var diff = IsMale ? Waist - Neck : Waist + Hip - Neck;
        if (diff <= 0)
        {
            HasResult = false;
            MessageRequested?.Invoke(
                _localization.GetString("AlertError"),
                _localization.GetString("AlertBodyFatMeasurement"));
            return;
        }

        Result = _fitnessEngine.CalculateBodyFat(Height, Neck, Waist, Hip, IsMale);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        Height = 175;
        Neck = 38;
        Waist = 85;
        Hip = 95;
        IsMale = true;
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
            var entry = new TrackingEntry
            {
                Type = TrackingType.BodyFat,
                Value = Result.BodyFatPercent,
                Date = DateTime.Today,
                Note = $"{GetCategoryText(Result.Category)} - {_localization.GetString("Height")}: {Height} cm, {_localization.GetString("Neck")}: {Neck} cm, {_localization.GetString("Waist")}: {Waist} cm"
            };
            await _trackingService.AddEntryAsync(entry);

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
