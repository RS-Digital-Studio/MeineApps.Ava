using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class RoofSolarViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSolarRoofAreaChanged(double value) => ScheduleAutoCalculate();
    partial void OnPanelEfficiencyChanged(double value) => ScheduleAutoCalculate();
    partial void OnSelectedOrientationChanged(int value) => ScheduleAutoCalculate();
    partial void OnTiltDegreesChanged(double value) => ScheduleAutoCalculate();

    // Solar Yield Inputs
    [ObservableProperty] private double _solarRoofArea = 50;
    [ObservableProperty] private double _panelEfficiency = 20;
    [ObservableProperty] private int _selectedOrientation = 4; // South
    [ObservableProperty] private double _tiltDegrees = 30;

    public List<string> Orientations => [
        _localization.GetString("OrientationNorth"),
        _localization.GetString("OrientationNorthEast"),
        _localization.GetString("OrientationEast"),
        _localization.GetString("OrientationSouthEast"),
        _localization.GetString("OrientationSouth"),
        _localization.GetString("OrientationSouthWest"),
        _localization.GetString("OrientationWest"),
        _localization.GetString("OrientationNorthWest")
    ];

    // Result
    [ObservableProperty] private SolarYieldResult? _solarResult;

    // Solar-Ertrag: Strompreis + Anlagenkosten
    [ObservableProperty]
    private double _pricePerKwh = 0.30;

    [ObservableProperty]
    private double _solarSystemCost = 0;

    [ObservableProperty]
    private bool _showSolarCost = false;

    public string SolarCostDisplay => ShowSolarCost && SolarSystemCost > 0
        ? $"{_localization.GetString("ResultSystemCost")}: {SolarSystemCost:F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    public string PaybackTimeDisplay => (ShowSolarCost && SolarSystemCost > 0 && SolarResult != null && SolarResult.AnnualYieldKwh > 0 && PricePerKwh > 0)
        ? $"{_localization.GetString("ResultPaybackTime")}: {(SolarSystemCost / (SolarResult.AnnualYieldKwh * PricePerKwh)):F1} {_localization.GetString("HistoryYears")}"
        : "";

    partial void OnPricePerKwhChanged(double value)
    {
        OnPropertyChanged(nameof(PaybackTimeDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnSolarSystemCostChanged(double value)
    {
        ShowSolarCost = value > 0;
        OnPropertyChanged(nameof(SolarCostDisplay));
        OnPropertyChanged(nameof(PaybackTimeDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnSolarResultChanged(SolarYieldResult? value)
    {
        OnPropertyChanged(nameof(PaybackTimeDisplay));
    }
}
