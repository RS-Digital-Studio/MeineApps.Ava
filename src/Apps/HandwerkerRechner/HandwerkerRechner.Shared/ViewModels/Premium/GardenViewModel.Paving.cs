using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class GardenViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnPavingAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnStoneLengthChanged(double value) => ScheduleAutoCalculate();
    partial void OnStoneWidthChanged(double value) => ScheduleAutoCalculate();
    partial void OnJointWidthChanged(double value) => ScheduleAutoCalculate();

    // Paving Inputs
    [ObservableProperty] private double _pavingArea = 20;
    [ObservableProperty] private double _stoneLength = 20;
    [ObservableProperty] private double _stoneWidth = 10;
    [ObservableProperty] private double _jointWidth = 3;

    // Pflastersteine: Preis pro Stein
    [ObservableProperty]
    private double _pricePerStone = 0;

    [ObservableProperty]
    private bool _showPavingCost = false;

    public string PavingCostDisplay => (ShowPavingCost && PricePerStone > 0 && PavingResult != null && PavingResult.StonesWithReserve > 0)
        ? $"{_localization.GetString("TotalCost")}: {(PavingResult.StonesWithReserve * PricePerStone):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerStoneChanged(double value)
    {
        ShowPavingCost = value > 0;
        OnPropertyChanged(nameof(PavingCostDisplay));
        ScheduleAutoCalculate();
    }

    // Result
    [ObservableProperty] private PavingResult? _pavingResult;

    partial void OnPavingResultChanged(PavingResult? value)
    {
        OnPropertyChanged(nameof(PavingCostDisplay));
    }
}
