using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for shift plan page (Premium feature)
/// Phase 9: Shift plan UI
/// </summary>
public partial class ShiftPlanViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IDatabaseService _database;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public ShiftPlanViewModel(
        IShiftService shiftService,
        IDatabaseService database,
        IPurchaseService purchaseService,
        ITrialService trialService)
    {
        _shiftService = shiftService;
        _database = database;
        _purchaseService = purchaseService;
        _trialService = trialService;
    }

    // === Properties ===

    [ObservableProperty]
    private DateTime _currentWeekStart = GetMondayOfWeek(DateTime.Today);

    [ObservableProperty]
    private string _weekDisplay = "";

    [ObservableProperty]
    private ObservableCollection<ShiftPattern> _shiftPatterns = new();

    [ObservableProperty]
    private ObservableCollection<ShiftDayItem> _weekDays = new();

    [ObservableProperty]
    private ShiftPattern? _selectedPattern;

    [ObservableProperty]
    private ShiftDayItem? _selectedDay;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _totalHoursDisplay = "0:00";

    // Derived properties
    public bool HasNoPatterns => ShiftPatterns.Count == 0;
    public string PatternStartTimeDisplay => PatternStartTime.ToString(@"hh\:mm");
    public string PatternEndTimeDisplay => PatternEndTime.ToString(@"hh\:mm");

    partial void OnPatternStartTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(PatternStartTimeDisplay));
    partial void OnPatternEndTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(PatternEndTimeDisplay));

    // Pattern-Editor
    [ObservableProperty]
    private bool _isPatternEditorVisible;

    [ObservableProperty]
    private ShiftPattern? _editingPattern;

    [ObservableProperty]
    private string _patternName = "";

    [ObservableProperty]
    private ShiftType _patternType = ShiftType.Normal;

    [ObservableProperty]
    private TimeSpan _patternStartTime = new(9, 0, 0);

    [ObservableProperty]
    private TimeSpan _patternEndTime = new(17, 0, 0);

    [ObservableProperty]
    private string _patternColor = "#1565C0";

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load shift patterns
            var patterns = await _shiftService.GetShiftPatternsAsync();
            ShiftPatterns = new ObservableCollection<ShiftPattern>(patterns);
            OnPropertyChanged(nameof(HasNoPatterns));

            // Display week
            UpdateWeekDisplay();

            // Load week days
            await LoadWeekAsync();

            // Premium status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PreviousWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(-7);
        UpdateWeekDisplay();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task NextWeekAsync()
    {
        CurrentWeekStart = CurrentWeekStart.AddDays(7);
        UpdateWeekDisplay();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        CurrentWeekStart = GetMondayOfWeek(DateTime.Today);
        UpdateWeekDisplay();
        await LoadWeekAsync();
    }

    [RelayCommand]
    private void SelectPattern(ShiftPattern? pattern)
    {
        SelectedPattern = pattern;
    }

    [RelayCommand]
    private async Task AssignShiftAsync(ShiftDayItem? day)
    {
        if (day == null || SelectedPattern == null)
            return;

        try
        {
            if (SelectedPattern.Type == ShiftType.Off)
            {
                await _shiftService.RemoveShiftAssignmentAsync(day.Date);
            }
            else
            {
                await _shiftService.AssignShiftAsync(day.Date, SelectedPattern.Id);
            }

            // Update day
            day.AssignedPattern = SelectedPattern.Type == ShiftType.Off ? null : SelectedPattern;
            day.PatternName = SelectedPattern.Type == ShiftType.Off ? AppStrings.ShiftOff : SelectedPattern.Name;
            day.PatternColor = SelectedPattern.Color;
            day.WorkMinutes = SelectedPattern.Type == ShiftType.Off ? 0 : (int)SelectedPattern.WorkDuration.TotalMinutes;

            CalculateTotalHours();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    [RelayCommand]
    private async Task ClearDayAsync(ShiftDayItem? day)
    {
        if (day == null)
            return;

        try
        {
            await _shiftService.RemoveShiftAssignmentAsync(day.Date);
            day.AssignedPattern = null;
            day.PatternName = "\u2014";
            day.PatternColor = "#9E9E9E";
            day.WorkMinutes = 0;

            CalculateTotalHours();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    [RelayCommand]
    private async Task ApplyPatternToWeekAsync()
    {
        if (SelectedPattern == null)
            return;

        try
        {
            // Assign pattern Mo-Fr, Sa-So free
            var patternIds = new List<int?>
            {
                SelectedPattern.Id, // Mo
                SelectedPattern.Id, // Di
                SelectedPattern.Id, // Mi
                SelectedPattern.Id, // Do
                SelectedPattern.Id, // Fr
                null,               // Sa (free)
                null                // So (free)
            };

            await _shiftService.GenerateWeekScheduleAsync(CurrentWeekStart, patternIds);
            await LoadWeekAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    [RelayCommand]
    private void ShowPatternEditor(ShiftPattern? pattern)
    {
        if (pattern != null)
        {
            // Edit
            EditingPattern = pattern;
            PatternName = pattern.Name;
            PatternType = pattern.Type;
            PatternStartTime = pattern.StartTime.ToTimeSpan();
            PatternEndTime = pattern.EndTime.ToTimeSpan();
            PatternColor = pattern.Color;
        }
        else
        {
            // New
            EditingPattern = null;
            PatternName = "";
            PatternType = ShiftType.Normal;
            PatternStartTime = new TimeSpan(9, 0, 0);
            PatternEndTime = new TimeSpan(17, 0, 0);
            PatternColor = "#1565C0";
        }

        IsPatternEditorVisible = true;
    }

    [RelayCommand]
    private void HidePatternEditor()
    {
        IsPatternEditorVisible = false;
        EditingPattern = null;
    }

    [RelayCommand]
    private void SetPatternColor(string color)
    {
        PatternColor = color;
    }

    [RelayCommand]
    private async Task SavePatternAsync()
    {
        try
        {
            var pattern = EditingPattern ?? new ShiftPattern();
            pattern.Name = PatternName;
            pattern.Type = PatternType;
            pattern.StartTime = TimeOnly.FromTimeSpan(PatternStartTime);
            pattern.EndTime = TimeOnly.FromTimeSpan(PatternEndTime);
            pattern.Color = PatternColor;
            pattern.IsActive = true;

            await _shiftService.SaveShiftPatternAsync(pattern);

            IsPatternEditorVisible = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorSaving, ex.Message));
        }
    }

    [RelayCommand]
    private async Task DeletePatternAsync(ShiftPattern? pattern)
    {
        if (pattern == null)
            return;

        try
        {
            await _shiftService.DeleteShiftPatternAsync(pattern.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // === Helper methods ===

    private void UpdateWeekDisplay()
    {
        var weekEnd = CurrentWeekStart.AddDays(6);
        WeekDisplay = $"{CurrentWeekStart:dd.MM.} - {weekEnd:dd.MM.yyyy}";
    }

    private async Task LoadWeekAsync()
    {
        var weekEnd = CurrentWeekStart.AddDays(6);
        var assignments = await _shiftService.GetShiftAssignmentsAsync(CurrentWeekStart, weekEnd);

        var days = new List<ShiftDayItem>();
        for (int i = 0; i < 7; i++)
        {
            var date = CurrentWeekStart.AddDays(i);
            var assignment = assignments.FirstOrDefault(a => a.Date.Date == date.Date);

            days.Add(new ShiftDayItem
            {
                Date = date,
                DayName = GetDayName(date.DayOfWeek),
                DateDisplay = date.ToString("dd.MM."),
                IsToday = date.Date == DateTime.Today,
                IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday,
                AssignedPattern = assignment?.ShiftPattern,
                PatternName = assignment?.ShiftPattern?.Name ?? "\u2014",
                PatternColor = assignment?.ShiftPattern?.Color ?? "#9E9E9E",
                WorkMinutes = assignment?.ShiftPattern != null
                    ? (int)assignment.ShiftPattern.WorkDuration.TotalMinutes
                    : 0
            });
        }

        WeekDays = new ObservableCollection<ShiftDayItem>(days);
        CalculateTotalHours();
    }

    private void CalculateTotalHours()
    {
        var totalMinutes = WeekDays.Sum(d => d.WorkMinutes);
        var hours = totalMinutes / 60;
        var mins = totalMinutes % 60;
        TotalHoursDisplay = $"{hours}:{mins:D2}";
    }

    private static DateTime GetMondayOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday).Date;
    }

    private static string GetDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => AppStrings.Mon,
            DayOfWeek.Tuesday => AppStrings.Tue,
            DayOfWeek.Wednesday => AppStrings.Wed,
            DayOfWeek.Thursday => AppStrings.Thu,
            DayOfWeek.Friday => AppStrings.Fri,
            DayOfWeek.Saturday => AppStrings.Sat,
            DayOfWeek.Sunday => AppStrings.Sun,
            _ => dayOfWeek.ToString()
        };
    }
}

/// <summary>
/// Item for a weekday in the shift plan
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
    private string _patternColor = "#9E9E9E";

    [ObservableProperty]
    private int _workMinutes;

    public string WorkTimeDisplay => WorkMinutes > 0
        ? $"{WorkMinutes / 60}:{WorkMinutes % 60:D2}"
        : "\u2014";

    public Thickness TodayBorderThickness => IsToday ? new Thickness(2) : new Thickness(0);
    public double DayOpacity => IsWeekend && WorkMinutes == 0 ? 0.6 : 1.0;
}
