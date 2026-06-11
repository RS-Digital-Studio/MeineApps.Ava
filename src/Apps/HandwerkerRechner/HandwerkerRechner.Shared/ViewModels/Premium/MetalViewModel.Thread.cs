using CommunityToolkit.Mvvm.ComponentModel;
using HandwerkerRechner.Models;

namespace HandwerkerRechner.ViewModels.Premium;

public sealed partial class MetalViewModel
{
    // Live-Berechnung: Debounce bei Eingabe-Änderungen
    partial void OnSelectedThreadChanged(int value) => ScheduleAutoCalculate();

    // Thread Drill Inputs
    [ObservableProperty] private int _selectedThread;
    public List<string> ThreadSizes { get; } = ["M3", "M4", "M5", "M6", "M8", "M10", "M12", "M14", "M16", "M18", "M20", "M22", "M24", "M27", "M30"];

    // Result
    [ObservableProperty] private ThreadDrillResult? _threadResult;

    // Gewindebohrer: Keine Kostenberechnung (nur Tabelle)
}
