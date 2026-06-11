using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class ElectricalViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnWattsChanged(double value) => ScheduleAutoCalculate();
    partial void OnHoursPerDayChanged(double value) => ScheduleAutoCalculate();
    partial void OnPricePerKwhChanged(double value) => ScheduleAutoCalculate();

    // Power Cost Inputs
    [ObservableProperty] private double _watts = Defaults.Power;
    [ObservableProperty] private double _hoursPerDay = Defaults.HoursPerDay;
    [ObservableProperty] private double _pricePerKwh = Defaults.PricePerKwh;

    // Result
    [ObservableProperty] private PowerCostResult? _powerCostResult;
}
