using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class ElectricalViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnVoltageChanged(double value) => ScheduleAutoCalculate();
    partial void OnCurrentChanged(double value) => ScheduleAutoCalculate();
    partial void OnCrossSectionChanged(double value) => ScheduleAutoCalculate();
    partial void OnIsCopperChanged(bool value) => ScheduleAutoCalculate();

    // Voltage Drop Inputs
    [ObservableProperty] private double _voltage = Defaults.Voltage;
    [ObservableProperty] private double _current = Defaults.Current;
    [ObservableProperty] private double _cableLength = Defaults.CableLength;
    [ObservableProperty] private double _crossSection = Defaults.CrossSection;
    [ObservableProperty] private bool _isCopper = true;

    // Result
    [ObservableProperty] private VoltageDropResult? _voltageDropResult;

    // Spannungsabfall: Kabelkosten
    [ObservableProperty]
    private double _cablePrice = 0;

    [ObservableProperty]
    private bool _showCableCost = false;

    public string CableCostDisplay => (ShowCableCost && CablePrice > 0 && CableLength > 0)
        ? $"{_localization.GetString("CableCostLabel")}: {(CableLength * CablePrice):F2} {_localization.GetString("CurrencySymbol")}"
        : "";

    partial void OnCablePriceChanged(double value)
    {
        ShowCableCost = value > 0;
        OnPropertyChanged(nameof(CableCostDisplay));
        ScheduleAutoCalculate();
    }

    partial void OnCableLengthChanged(double value)
    {
        OnPropertyChanged(nameof(CableCostDisplay));
        ScheduleAutoCalculate();
    }
}
