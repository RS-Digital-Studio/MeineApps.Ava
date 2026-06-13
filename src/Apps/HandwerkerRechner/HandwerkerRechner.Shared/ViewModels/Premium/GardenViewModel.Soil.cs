using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class GardenViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSoilAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnSoilDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnBagLitersChanged(double value) => ScheduleAutoCalculate();

    // Soil Inputs
    [ObservableProperty] private double _soilArea = 10;
    [ObservableProperty] private double _soilDepth = 5;
    [ObservableProperty] private double _bagLiters = 40;

    // Erde/Mulch: Preis pro Sack
    [ObservableProperty]
    private double _pricePerBag = 0;

    [ObservableProperty]
    private bool _showSoilCost = false;

    public string SoilCostDisplay => (ShowSoilCost && PricePerBag > 0 && SoilResult != null && SoilResult.BagsNeeded > 0)
        ? $"{_localization.GetString("TotalCost")}: {(SoilResult.BagsNeeded * PricePerBag):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerBagChanged(double value)
    {
        ShowSoilCost = value > 0;
        OnPropertyChanged(nameof(SoilCostDisplay));
        ScheduleAutoCalculate();
    }

    // Result
    [ObservableProperty] private SoilResult? _soilResult;

    partial void OnSoilResultChanged(SoilResult? value)
    {
        OnPropertyChanged(nameof(SoilCostDisplay));
    }
}
