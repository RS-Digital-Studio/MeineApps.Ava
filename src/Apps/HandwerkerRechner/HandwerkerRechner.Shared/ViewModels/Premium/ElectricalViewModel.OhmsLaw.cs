using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class ElectricalViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnOhmsVoltageChanged(string value) => ScheduleAutoCalculate();
    partial void OnOhmsCurrentChanged(string value) => ScheduleAutoCalculate();
    partial void OnOhmsResistanceChanged(string value) => ScheduleAutoCalculate();
    partial void OnOhmsPowerChanged(string value) => ScheduleAutoCalculate();

    // Ohms Law Inputs
    [ObservableProperty] private string _ohmsVoltage = "";
    [ObservableProperty] private string _ohmsCurrent = "";
    [ObservableProperty] private string _ohmsResistance = "";
    [ObservableProperty] private string _ohmsPower = "";

    // Result
    [ObservableProperty] private OhmsLawResult? _ohmsLawResult;

    /// <summary>
    /// Parst eine freie Zahleneingabe kulturunabhängig: akzeptiert sowohl '.' als auch ','
    /// als Dezimaltrennzeichen (InvariantCulture zuerst, dann CurrentCulture als Fallback).
    /// Verhindert, dass z.B. "2.5" auf DE-Geräten als ungültig verworfen wird.
    /// </summary>
    private static double? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var inv)) return inv;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cur)) return cur;
        return null;
    }
}
