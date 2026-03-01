using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class ShiftScheduleViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IShiftScheduleService _shiftService;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private ShiftSchedule? _activeSchedule;

    [ObservableProperty]
    private bool _hasActiveSchedule;

    [ObservableProperty]
    private bool _isEditing;

    // Editor fields
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFifteenShift))]
    [NotifyPropertyChangedFor(nameof(IsTwentyOneShift))]
    [NotifyPropertyChangedFor(nameof(MaxGroup))]
    private ShiftPatternType _selectedPatternType = ShiftPatternType.FifteenShift;

    [ObservableProperty]
    private int _selectedGroup = 1;

    public bool IsFifteenShift => SelectedPatternType == ShiftPatternType.FifteenShift;
    public bool IsTwentyOneShift => SelectedPatternType == ShiftPatternType.TwentyOneShift;
    public int MaxGroup => SelectedPatternType == ShiftPatternType.FifteenShift ? 3 : 5;

    [ObservableProperty]
    private DateTimeOffset _selectedStartDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string _scheduleName = string.Empty;

    // Calendar display
    [ObservableProperty]
    private int _displayMonth;

    [ObservableProperty]
    private int _displayYear;

    [ObservableProperty]
    private string _displayMonthName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalendarDay> _calendarDays = [];

    private List<ShiftException> _exceptions = [];

    // Deactivate confirmation state
    [ObservableProperty]
    private bool _isDeactivateConfirmVisible;

    // Exception-Dialog state
    [ObservableProperty]
    private bool _isExceptionDialogVisible;

    [ObservableProperty]
    private CalendarDay? _selectedDay;

    // Localized strings
    public string TitleText => _localization.GetString("ShiftTitle");
    public string NoScheduleText => _localization.GetString("NoSchedule");
    public string SetupScheduleText => _localization.GetString("SetupSchedule");
    public string ConfigureText => _localization.GetString("ConfigureSchedule");
    public string ShiftModelText => _localization.GetString("ShiftModel");
    public string ShiftGroupText => _localization.GetString("ShiftGroup");
    public string StartDateText => _localization.GetString("StartDate");
    public string FifteenShiftText => _localization.GetString("FifteenShiftModel");
    public string TwentyOneShiftText => _localization.GetString("TwentyOneShiftModel");
    public string SaveText => _localization.GetString("Save");
    public string CancelText => _localization.GetString("Cancel");
    public string DeactivateText => _localization.GetString("DeactivateSchedule");
    public string EditText => _localization.GetString("EditSchedule");
    public string PreviousMonthText => _localization.GetString("PreviousMonth");
    public string NextMonthText => _localization.GetString("NextMonth");
    public string ConfirmDeactivateTitleText => _localization.GetString("ConfirmDeactivateTitle");
    public string ConfirmDeactivateMessageText => _localization.GetString("ConfirmDeactivateMessage");
    public string YesText => _localization.GetString("Yes");
    public string NoText => _localization.GetString("No");

    // Localized weekday headers for calendar
    public string MondayShortText => _localization.GetString("MondayShort");
    public string TuesdayShortText => _localization.GetString("TuesdayShort");
    public string WednesdayShortText => _localization.GetString("WednesdayShort");
    public string ThursdayShortText => _localization.GetString("ThursdayShort");
    public string FridayShortText => _localization.GetString("FridayShort");
    public string SaturdayShortText => _localization.GetString("SaturdayShort");
    public string SundayShortText => _localization.GetString("SundayShort");

    // Exception-Dialog strings
    public string SelectExceptionText => _localization.GetString("SelectException");
    public string VacationText => _localization.GetString("Vacation");
    public string SickText => _localization.GetString("Sick");
    public string ShiftSwapText => _localization.GetString("ShiftSwap");
    public string NormalText => _localization.GetString("DayOff");
    public string CloseText => _localization.GetString("Cancel");

    // Localized shift labels for legend
    public string EarlyShiftShortText => _localization.GetString("ShiftEarlyShort");
    public string LateShiftShortText => _localization.GetString("ShiftLateShort");
    public string NightShiftShortText => _localization.GetString("ShiftNightShort");
    public string DayOffShortText => _localization.GetString("DayOff");

    private Task _initTask = Task.CompletedTask;

    public ShiftScheduleViewModel(IShiftScheduleService shiftService, ILocalizationService localization)
    {
        _shiftService = shiftService;
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;

        var now = DateTime.Today;
        _displayMonth = now.Month;
        _displayYear = now.Year;

        _initTask = LoadAsync();
    }

    private async Task LoadAsync()
    {
        ActiveSchedule = await _shiftService.GetActiveScheduleAsync();
        HasActiveSchedule = ActiveSchedule != null;

        if (ActiveSchedule != null)
        {
            _exceptions = await _shiftService.GetExceptionsAsync(ActiveSchedule.Id);
        }

        UpdateCalendar();
    }

    [RelayCommand]
    private async Task ShowEditor()
    {
        await _initTask;
        if (ActiveSchedule != null)
        {
            ScheduleName = ActiveSchedule.Name;
            SelectedPatternType = ActiveSchedule.PatternType;
            SelectedGroup = ActiveSchedule.ShiftGroupNumber;
            SelectedStartDate = ActiveSchedule.StartDateValue.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            ScheduleName = "";
            SelectedPatternType = ShiftPatternType.FifteenShift;
            SelectedGroup = 1;
            SelectedStartDate = DateTimeOffset.Now;
        }
        IsEditing = true;
    }

    [RelayCommand]
    private void HideEditor() => IsEditing = false;

    [RelayCommand]
    private void SelectFifteenShift()
    {
        SelectedPatternType = ShiftPatternType.FifteenShift;
        if (SelectedGroup > MaxGroup) SelectedGroup = 1;
    }

    [RelayCommand]
    private void SelectTwentyOneShift()
    {
        SelectedPatternType = ShiftPatternType.TwentyOneShift;
        if (SelectedGroup > MaxGroup) SelectedGroup = 1;
    }

    [RelayCommand]
    private async Task SaveSchedule()
    {
        await _initTask;
        var schedule = ActiveSchedule ?? new ShiftSchedule();
        schedule.Name = ScheduleName;
        schedule.PatternType = SelectedPatternType;
        schedule.ShiftGroupNumber = SelectedGroup;
        schedule.StartDateValue = DateOnly.FromDateTime(SelectedStartDate.DateTime);

        await _shiftService.SaveScheduleAsync(schedule);
        await _shiftService.ActivateScheduleAsync(schedule);
        IsEditing = false;
        await LoadAsync();
    }

    [RelayCommand]
    private void DeactivateSchedule()
    {
        if (ActiveSchedule == null) return;
        IsDeactivateConfirmVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeactivateSchedule()
    {
        if (ActiveSchedule != null)
        {
            await _shiftService.DeactivateScheduleAsync(ActiveSchedule);
            await LoadAsync();
        }
        IsDeactivateConfirmVisible = false;
    }

    [RelayCommand]
    private void CancelDeactivateSchedule()
    {
        IsDeactivateConfirmVisible = false;
    }

    // Exception-Dialog: Tap auf Kalendertag
    [RelayCommand]
    private void SelectDay(CalendarDay day)
    {
        if (day.IsEmpty || ActiveSchedule == null) return;
        SelectedDay = day;
        IsExceptionDialogVisible = true;
    }

    [RelayCommand]
    private async Task SetException(string exceptionType)
    {
        await _initTask;
        if (SelectedDay == null || ActiveSchedule == null)
        {
            IsExceptionDialogVisible = false;
            return;
        }

        // "normal" = Ausnahme entfernen
        if (exceptionType == "normal")
        {
            var existing = _exceptions.FirstOrDefault(e => e.DateValue == SelectedDay.Date);
            if (existing != null)
            {
                await _shiftService.DeleteExceptionAsync(existing);
                _exceptions.Remove(existing);
            }
        }
        else
        {
            var type = exceptionType switch
            {
                "vacation" => ExceptionType.Vacation,
                "sick" => ExceptionType.Sick,
                "swap" => ExceptionType.ShiftSwap,
                _ => ExceptionType.Other
            };

            var shiftType = type switch
            {
                ExceptionType.Vacation => ShiftType.Vacation,
                ExceptionType.Sick => ShiftType.Sick,
                _ => (ShiftType?)null
            };

            var existing = _exceptions.FirstOrDefault(e => e.DateValue == SelectedDay.Date);
            if (existing != null)
            {
                existing.ExceptionType = type;
                existing.NewShiftType = shiftType;
                await _shiftService.SaveExceptionAsync(existing);
            }
            else
            {
                var exception = new ShiftException
                {
                    ShiftScheduleId = ActiveSchedule.Id,
                    DateValue = SelectedDay.Date,
                    ExceptionType = type,
                    NewShiftType = shiftType
                };
                await _shiftService.SaveExceptionAsync(exception);
                _exceptions.Add(exception);
            }
        }

        IsExceptionDialogVisible = false;
        UpdateCalendar();
    }

    [RelayCommand]
    private void CloseExceptionDialog() => IsExceptionDialogVisible = false;

    [RelayCommand]
    private void PreviousMonth()
    {
        if (DisplayMonth == 1)
        {
            DisplayMonth = 12;
            DisplayYear--;
        }
        else
        {
            DisplayMonth--;
        }
        UpdateCalendar();
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (DisplayMonth == 12)
        {
            DisplayMonth = 1;
            DisplayYear++;
        }
        else
        {
            DisplayMonth++;
        }
        UpdateCalendar();
    }

    private void UpdateCalendar()
    {
        DisplayMonthName = new DateTime(DisplayYear, DisplayMonth, 1).ToString("MMMM yyyy");
        var days = new ObservableCollection<CalendarDay>();

        var firstDay = new DateOnly(DisplayYear, DisplayMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(DisplayYear, DisplayMonth);

        // Fill leading empty cells (Monday = 0)
        var startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7; // Monday=0
        for (int i = 0; i < startDayOfWeek; i++)
        {
            days.Add(new CalendarDay());
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(DisplayYear, DisplayMonth, day);
            var shiftType = ActiveSchedule != null
                ? _shiftService.GetShiftForDate(ActiveSchedule, date, _exceptions)
                : ShiftType.Free;

            days.Add(new CalendarDay
            {
                Day = day,
                Date = date,
                ShiftType = shiftType,
                ShiftLabel = GetShiftLabel(shiftType),
                BackgroundColor = GetShiftColor(shiftType),
                IsToday = date == DateOnly.FromDateTime(DateTime.Today)
            });
        }

        CalendarDays = days;
    }

    private string GetShiftLabel(ShiftType type) => type switch
    {
        ShiftType.Early => _localization.GetString("ShiftEarlyShort"),
        ShiftType.Late => _localization.GetString("ShiftLateShort"),
        ShiftType.Night => _localization.GetString("ShiftNightShort"),
        ShiftType.Vacation => _localization.GetString("Vacation"),
        ShiftType.Sick => _localization.GetString("Sick"),
        _ => ""
    };

    private static string GetShiftColor(ShiftType type) => type switch
    {
        ShiftType.Early => "#FBBF24",
        ShiftType.Late => "#FB923C",
        ShiftType.Night => "#60A5FA",
        ShiftType.Vacation => "#34D399",
        ShiftType.Sick => "#F87171",
        _ => "Transparent"
    };

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
        UpdateCalendar();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localization.LanguageChanged -= OnLanguageChanged;

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents one cell in the calendar grid
/// </summary>
public class CalendarDay
{
    public int Day { get; init; }
    public DateOnly Date { get; init; }
    public ShiftType ShiftType { get; init; } = ShiftType.Free;
    public bool IsToday { get; init; }
    public bool IsEmpty => Day == 0;

    public string DayText => IsEmpty ? "" : Day.ToString();
    public string ShiftLabel { get; init; } = "";
    public string BackgroundColor { get; init; } = "Transparent";
}
