using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;

namespace FitnessRechner.ViewModels.Calculators;

public partial class IdealWeightViewModel : ObservableObject
{
    private readonly FitnessEngine _fitnessEngine;

    /// <summary>
    /// Event for navigation requests (replaces Shell.Current.GoToAsync)
    /// </summary>
    public event Action<string>? NavigationRequested;

    private void NavigateTo(string route) => NavigationRequested?.Invoke(route);

    public IdealWeightViewModel(FitnessEngine fitnessEngine)
    {
        _fitnessEngine = fitnessEngine;
    }

    [ObservableProperty]
    private double _height = 175;

    [ObservableProperty]
    private int _age = 30;

    [ObservableProperty]
    private bool _isMale = true;

    [ObservableProperty]
    private IdealWeightResult? _result;

    [ObservableProperty]
    private bool _hasResult;

    public string AverageIdealDisplay => Result != null ? $"{Result.AverageIdeal:F1} kg" : "";
    public string BrocaDisplay => Result != null ? $"{Result.BrocaWeight:F1} kg" : "";
    public string CreffDisplay => Result != null ? $"{Result.CreffWeight:F1} kg" : "";
    public string HealthyRangeDisplay => Result != null
        ? $"{Result.MinHealthyWeight:F1} - {Result.MaxHealthyWeight:F1} kg" : "";

    partial void OnResultChanged(IdealWeightResult? value)
    {
        OnPropertyChanged(nameof(AverageIdealDisplay));
        OnPropertyChanged(nameof(BrocaDisplay));
        OnPropertyChanged(nameof(CreffDisplay));
        OnPropertyChanged(nameof(HealthyRangeDisplay));
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Height <= 0 || Age <= 0)
        {
            HasResult = false;
            return;
        }

        Result = _fitnessEngine.CalculateIdealWeight(Height, IsMale, Age);
        HasResult = true;
    }

    [RelayCommand]
    private void Reset()
    {
        Height = 175;
        Age = 30;
        IsMale = true;
        Result = null;
        HasResult = false;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigateTo("..");
    }
}
