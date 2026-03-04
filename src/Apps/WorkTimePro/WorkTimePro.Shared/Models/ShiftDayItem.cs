using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkTimePro.Models;

/// <summary>
/// Anzeige-Item für einen Wochentag im Schichtplan.
/// </summary>
public partial class ShiftDayItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = "";
    public string DateDisplay { get; set; } = "";
    public bool IsToday { get; set; }
    public bool IsWeekend { get; set; }

    [ObservableProperty]
    private ShiftPattern? _assignedPattern;

    [ObservableProperty]
    private string _patternName = "\u2014";

    [ObservableProperty]
    private string _patternColor = AppColors.StatusIdle;

    [ObservableProperty]
    private int _workMinutes;

    partial void OnWorkMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(WorkTimeDisplay));
        OnPropertyChanged(nameof(DayOpacity));
    }

    public string WorkTimeDisplay => WorkMinutes > 0
        ? $"{WorkMinutes / 60}:{WorkMinutes % 60:D2}"
        : "\u2014";

    public Thickness TodayBorderThickness => IsToday ? new Thickness(2) : new Thickness(0);
    public double DayOpacity => IsWeekend && WorkMinutes == 0 ? 0.6 : 1.0;
}
