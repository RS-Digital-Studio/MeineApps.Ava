using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class MetalViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSelectedMetalChanged(int value) => ScheduleAutoCalculate();
    partial void OnSelectedProfileChanged(int value) => ScheduleAutoCalculate();
    partial void OnLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnDimension1Changed(double value) => ScheduleAutoCalculate();
    partial void OnDimension2Changed(double value) => ScheduleAutoCalculate();
    partial void OnWallThicknessChanged(double value) => ScheduleAutoCalculate();

    // Metal Weight Inputs
    [ObservableProperty] private int _selectedMetal;
    [ObservableProperty] private int _selectedProfile;
    [ObservableProperty] private double _length = 1.0;
    [ObservableProperty] private double _dimension1 = 20;
    [ObservableProperty] private double _dimension2 = 10;
    [ObservableProperty] private double _wallThickness = 2;

    public List<string> Metals => [
        _localization.GetString("MetalSteel"),
        _localization.GetString("MetalStainlessSteel"),
        _localization.GetString("MetalAluminum"),
        _localization.GetString("MetalCopper"),
        _localization.GetString("MetalBrass"),
        _localization.GetString("MetalBronze")
    ];
    public List<string> Profiles => [
        _localization.GetString("ProfileRoundBar"),
        _localization.GetString("ProfileFlatBar"),
        _localization.GetString("ProfileSquareBar"),
        _localization.GetString("ProfileRoundTube"),
        _localization.GetString("ProfileSquareTube"),
        _localization.GetString("ProfileAngle")
    ];

    // Result
    [ObservableProperty] private MetalWeightResult? _weightResult;

    // Metallgewicht: Preis pro kg
    [ObservableProperty]
    private double _pricePerKg = 0;

    [ObservableProperty]
    private bool _showMetalCost = false;

    public string MetalCostDisplay => (ShowMetalCost && PricePerKg > 0 && WeightResult != null && WeightResult.Weight > 0)
        ? $"{_localization.GetString("ResultMaterialCost")}: {(WeightResult.Weight * PricePerKg):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerKgChanged(double value)
    {
        ShowMetalCost = value > 0;
        OnPropertyChanged(nameof(MetalCostDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnWeightResultChanged(MetalWeightResult? value)
    {
        OnPropertyChanged(nameof(MetalCostDisplay));
    }
}
