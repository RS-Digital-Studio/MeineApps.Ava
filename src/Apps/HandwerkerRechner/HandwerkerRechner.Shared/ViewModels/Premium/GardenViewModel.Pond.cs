using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class GardenViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnPondLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnPondWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnPondDepthChanged(double value) => ScheduleAutoCalculate();
    partial void OnOverlapChanged(double value) => ScheduleAutoCalculate();

    // Pond Liner Inputs
    [ObservableProperty] private double _pondLength = 3;
    [ObservableProperty] private double _pondWidth = 2;
    [ObservableProperty] private double _pondDepth = 1;
    [ObservableProperty] private double _overlap = 0.5;

    // Teichfolie: Preis pro m²
    [ObservableProperty]
    private double _pricePerSqmLiner = 0;

    [ObservableProperty]
    private bool _showLinerCost = false;

    public string LinerCostDisplay => (ShowLinerCost && PricePerSqmLiner > 0 && PondResult != null && PondResult.LinerArea > 0)
        ? $"{_localization.GetString("TotalCost")}: {(PondResult.LinerArea * PricePerSqmLiner):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerSqmLinerChanged(double value)
    {
        ShowLinerCost = value > 0;
        OnPropertyChanged(nameof(LinerCostDisplay));
        ScheduleAutoCalculate();
    }

    // Result
    [ObservableProperty] private PondLinerResult? _pondResult;

    partial void OnPondResultChanged(PondLinerResult? value)
    {
        OnPropertyChanged(nameof(LinerCostDisplay));
    }
}
