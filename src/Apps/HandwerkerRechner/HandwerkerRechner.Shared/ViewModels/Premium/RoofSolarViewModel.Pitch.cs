using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class RoofSolarViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnRunChanged(double value) => ScheduleAutoCalculate();
    partial void OnRiseChanged(double value) => ScheduleAutoCalculate();

    // Roof Pitch Inputs
    [ObservableProperty] private double _run = 5;
    [ObservableProperty] private double _rise = 2;

    // Result
    [ObservableProperty] private RoofPitchResult? _pitchResult;

    // Dachneigung: Keine Kostenberechnung (nur Winkelberechnung)
}
