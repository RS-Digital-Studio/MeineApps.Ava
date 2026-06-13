using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class RoofSolarViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnRoofAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnTilesPerSqmChanged(double value) => ScheduleAutoCalculate();

    // Roof Tiles Inputs
    [ObservableProperty] private double _roofArea = 100;
    [ObservableProperty] private double _tilesPerSqm = 10;

    // Result
    [ObservableProperty] private RoofTilesResult? _tilesResult;

    // Dachziegel: Preis pro Ziegel
    [ObservableProperty]
    private double _pricePerTile = 0;

    [ObservableProperty]
    private bool _showTileCost = false;

    public string RoofTileCostDisplay => (ShowTileCost && PricePerTile > 0 && TilesResult != null && TilesResult.TilesWithReserve > 0)
        ? $"{_localization.GetString("TotalCost")}: {(TilesResult.TilesWithReserve * PricePerTile):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnPricePerTileChanged(double value)
    {
        ShowTileCost = value > 0;
        OnPropertyChanged(nameof(RoofTileCostDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnTilesResultChanged(RoofTilesResult? value)
    {
        OnPropertyChanged(nameof(RoofTileCostDisplay));
    }
}
