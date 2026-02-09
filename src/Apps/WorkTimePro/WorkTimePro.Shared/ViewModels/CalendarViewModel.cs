using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for calendar view with heatmap and status overlay
/// </summary>
public partial class CalendarViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;
    private readonly IVacationService _vacationService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public CalendarViewModel(
        IDatabaseService database,
        ICalculationService calculation,
        IPurchaseService purchaseService,
        ITrialService trialService,
        IVacationService vacationService)
    {
        _database = database;
        _calculation = calculation;
        _purchaseService = purchaseService;
        _trialService = trialService;
        _vacationService = vacationService;
    }

    // === Properties ===

    [ObservableProperty]
    private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _monthDisplay = "";

    [ObservableProperty]
    private ObservableCollection<CalendarDay> _calendarDays = new();

    [ObservableProperty]
    private ObservableCollection<List<CalendarDay>> _calendarWeeks = new();

    [ObservableProperty]
    private WorkMonth? _monthSummary;

    [ObservableProperty]
    private string _totalWorkDisplay = "0:00";

    [ObservableProperty]
    private string _balanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = "#4CAF50";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Localized texts
    public string TodayButtonText => $"{Icons.CalendarToday} {AppStrings.Today}";

    // === Status Overlay ===

    [ObservableProperty]
    private bool _isOverlayVisible;

    [ObservableProperty]
    private DateTime _overlayStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _overlayEndDate = DateTime.Today;

    [ObservableProperty]
    private VacationTypeItem? _overlaySelectedType;

    [ObservableProperty]
    private string _overlayNote = "";

    [ObservableProperty]
    private int _overlayCalculatedDays;

    [ObservableProperty]
    private string _overlayDateDisplay = "";

    [ObservableProperty]
    private bool _overlayHasExistingStatus;

    [ObservableProperty]
    private string _overlayExistingStatusText = "";

    public string OverlayCalculatedDaysDisplay => string.Format(AppStrings.WorkDaysFormat, OverlayCalculatedDays);

    partial void OnOverlayCalculatedDaysChanged(int value) => OnPropertyChanged(nameof(OverlayCalculatedDaysDisplay));

    partial void OnOverlayStartDateChanged(DateTime value)
    {
        if (value > OverlayEndDate)
            OverlayEndDate = value;
        _ = RecalculateOverlayDaysAsync();
    }

    partial void OnOverlayEndDateChanged(DateTime value)
    {
        if (value < OverlayStartDate)
            OverlayStartDate = value;
        _ = RecalculateOverlayDaysAsync();
    }

    private async Task RecalculateOverlayDaysAsync()
    {
        try
        {
            OverlayCalculatedDays = await _vacationService.CalculateWorkDaysAsync(OverlayStartDate, OverlayEndDate);
        }
        catch (Exception)
        {
            OverlayCalculatedDays = 0;
        }
    }

    public List<VacationTypeItem> AvailableStatusTypes => new()
    {
        new() { Status = DayStatus.Vacation, Name = AppStrings.Vacation },
        new() { Status = DayStatus.Sick, Name = AppStrings.Illness },
        new() { Status = DayStatus.HomeOffice, Name = AppStrings.DayStatus_HomeOffice },
        new() { Status = DayStatus.BusinessTrip, Name = AppStrings.DayStatus_BusinessTrip },
        new() { Status = DayStatus.SpecialLeave, Name = AppStrings.SpecialLeave },
        new() { Status = DayStatus.UnpaidLeave, Name = AppStrings.UnpaidLeave },
        new() { Status = DayStatus.OvertimeCompensation, Name = AppStrings.OvertimeCompensation },
        new() { Status = DayStatus.Training, Name = AppStrings.DayStatus_Training },
        new() { Status = DayStatus.CompensatoryTime, Name = AppStrings.DayStatus_CompensatoryTime }
    };

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            MonthDisplay = SelectedMonth.ToString("MMMM yyyy");

            // Load month summary
            MonthSummary = await _calculation.CalculateMonthAsync(SelectedMonth.Year, SelectedMonth.Month);

            TotalWorkDisplay = MonthSummary.ActualWorkDisplay;
            BalanceDisplay = MonthSummary.BalanceDisplay;
            BalanceColor = MonthSummary.BalanceColor;

            // Generate calendar days
            await GenerateCalendarDaysAsync();

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
    private async Task PreviousMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(-1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = SelectedMonth.AddMonths(1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        SelectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task SelectDayAsync(CalendarDay? day)
    {
        if (day == null || !day.IsCurrentMonth) return;

        // Open status overlay
        OverlayStartDate = day.Date;
        OverlayEndDate = day.Date;
        OverlaySelectedType = AvailableStatusTypes[0];
        OverlayNote = "";
        OverlayDateDisplay = day.Date.ToString("dddd, dd. MMMM yyyy");

        // Check existing status
        var hasSpecialStatus = day.Status != DayStatus.WorkDay &&
                               day.Status != DayStatus.Work &&
                               day.Status != DayStatus.Weekend;
        OverlayHasExistingStatus = hasSpecialStatus;
        if (hasSpecialStatus)
        {
            OverlayExistingStatusText = GetStatusName(day.Status);
            // Pre-select existing type
            var existing = AvailableStatusTypes.FirstOrDefault(t => t.Status == day.Status);
            if (existing != null)
                OverlaySelectedType = existing;
        }

        await RecalculateOverlayDaysAsync();
        IsOverlayVisible = true;
    }

    [RelayCommand]
    private void CancelOverlay()
    {
        IsOverlayVisible = false;
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (OverlaySelectedType == null) return;

        try
        {
            var entry = new VacationEntry
            {
                Year = OverlayStartDate.Year,
                StartDate = OverlayStartDate,
                EndDate = OverlayEndDate,
                Days = OverlayCalculatedDays,
                Type = OverlaySelectedType.Status,
                Note = string.IsNullOrWhiteSpace(OverlayNote) ? null : OverlayNote
            };

            await _vacationService.SaveVacationEntryAsync(entry);

            IsOverlayVisible = false;
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.Saved);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorSaving, ex.Message));
        }
    }

    [RelayCommand]
    private async Task RemoveStatusAsync()
    {
        try
        {
            // Check if there is a vacation entry for the selected date
            var existing = await _vacationService.GetVacationForDateAsync(OverlayStartDate);
            if (existing != null)
            {
                await _vacationService.DeleteVacationEntryAsync(existing.Id);
            }
            else
            {
                // Directly reset WorkDay status
                await SetDayStatusAsync(OverlayStartDate, DayStatus.WorkDay);
            }

            IsOverlayVisible = false;
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.ResetStatus);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorSaving, ex.Message));
        }
    }

    private static string GetStatusName(DayStatus status) => status switch
    {
        DayStatus.Vacation => AppStrings.Vacation,
        DayStatus.Sick => AppStrings.Illness,
        DayStatus.HomeOffice => AppStrings.DayStatus_HomeOffice,
        DayStatus.BusinessTrip => AppStrings.DayStatus_BusinessTrip,
        DayStatus.SpecialLeave => AppStrings.SpecialLeave,
        DayStatus.UnpaidLeave => AppStrings.UnpaidLeave,
        DayStatus.OvertimeCompensation => AppStrings.OvertimeCompensation,
        DayStatus.Holiday => AppStrings.Holiday,
        DayStatus.Training => AppStrings.DayStatus_Training,
        DayStatus.CompensatoryTime => AppStrings.DayStatus_CompensatoryTime,
        _ => AppStrings.DayStatus_WorkDay
    };

    /// <summary>
    /// Set status for a specific day
    /// </summary>
    public async Task SetDayStatusAsync(DateTime date, DayStatus status, string? note = null)
    {
        var workDay = await _database.GetOrCreateWorkDayAsync(date);
        workDay.Status = status;
        if (!string.IsNullOrWhiteSpace(note))
            workDay.Note = note;
        await _database.SaveWorkDayAsync(workDay);
        await LoadDataAsync();
    }

    /// <summary>
    /// Enter vacation for multiple consecutive days
    /// </summary>
    public async Task SetVacationRangeAsync(DateTime startDate, int days)
    {
        days = Math.Min(days, 30);

        for (var i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);

            // Skip weekends
            if (IsWeekend(date))
                continue;

            var workDay = await _database.GetOrCreateWorkDayAsync(date);
            workDay.Status = DayStatus.Vacation;
            workDay.Note = $"{AppStrings.Vacation} ({startDate:dd.MM} - {startDate.AddDays(days - 1):dd.MM})";
            await _database.SaveWorkDayAsync(workDay);
        }

        MessageRequested?.Invoke(AppStrings.Info, string.Format(AppStrings.VacationDaysEnteredFormat, days, startDate.ToString("dd.MM.yyyy")));
        await LoadDataAsync();
    }

    // === Helper methods ===

    private async Task GenerateCalendarDaysAsync()
    {
        var days = new List<CalendarDay>();

        var firstDayOfMonth = SelectedMonth;
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

        // Day of week of first day (0=Sun, 1=Mon, ..., 6=Sat)
        // We want Monday as first day
        var firstDayOfWeek = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

        // Days from previous month
        var prevMonthStart = firstDayOfMonth.AddDays(-firstDayOfWeek);
        for (var i = 0; i < firstDayOfWeek; i++)
        {
            days.Add(new CalendarDay
            {
                Date = prevMonthStart.AddDays(i),
                IsCurrentMonth = false
            });
        }

        // Load WorkDays for the month
        var workDays = await _database.GetWorkDaysAsync(firstDayOfMonth, lastDayOfMonth);

        // Days of current month
        for (var date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
        {
            var workDay = workDays.FirstOrDefault(w => w.Date.Date == date.Date);

            days.Add(new CalendarDay
            {
                Date = date,
                IsCurrentMonth = true,
                IsToday = date.Date == DateTime.Today,
                WorkMinutes = workDay?.ActualWorkMinutes ?? 0,
                Status = workDay?.Status ?? (IsWeekend(date) ? DayStatus.Weekend : DayStatus.WorkDay),
                HasData = workDay != null && workDay.ActualWorkMinutes > 0
            });
        }

        // Days from next month (fill up to 42 days = 6 weeks)
        var remainingDays = 42 - days.Count;
        var nextMonthStart = lastDayOfMonth.AddDays(1);
        for (var i = 0; i < remainingDays; i++)
        {
            days.Add(new CalendarDay
            {
                Date = nextMonthStart.AddDays(i),
                IsCurrentMonth = false
            });
        }

        CalendarDays = new ObservableCollection<CalendarDay>(days);

        // Group into weeks (7 days each)
        var weeks = new List<List<CalendarDay>>();
        for (int i = 0; i < days.Count; i += 7)
        {
            weeks.Add(days.Skip(i).Take(7).ToList());
        }
        CalendarWeeks = new ObservableCollection<List<CalendarDay>>(weeks);
    }

    private static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }
}

/// <summary>
/// Calendar day for heatmap display
/// </summary>
public class CalendarDay
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public int WorkMinutes { get; set; }
    public DayStatus Status { get; set; }
    public bool HasData { get; set; }

    public string DayNumber => Date.Day.ToString();

    public string HeatmapColor
    {
        get
        {
            if (!IsCurrentMonth) return "#F5F5F5";
            if (!HasData) return "#EEEEEE";

            return WorkMinutes switch
            {
                < 240 => "#C8E6C9",  // < 4h
                < 360 => "#81C784",  // 4-6h
                < 480 => "#4CAF50",  // 6-8h
                < 600 => "#388E3C",  // 8-10h
                _ => "#F44336"       // > 10h (overtime)
            };
        }
    }

    public string TextColor
    {
        get
        {
            if (!IsCurrentMonth) return "#BDBDBD";
            if (IsToday) return "#FFFFFF";
            if (Status == DayStatus.Weekend) return "#9E9E9E";
            return "#212121";
        }
    }

    public string BackgroundColor
    {
        get
        {
            if (IsToday) return "#1565C0";
            return HeatmapColor;
        }
    }

    /// <summary>
    /// MaterialIconKind for status display
    /// </summary>
    public MaterialIconKind StatusIconKind => Status switch
    {
        DayStatus.Vacation => MaterialIconKind.Beach,
        DayStatus.Holiday => MaterialIconKind.PartyPopper,
        DayStatus.Sick => MaterialIconKind.Thermometer,
        DayStatus.HomeOffice => MaterialIconKind.HomeAccount,
        DayStatus.BusinessTrip => MaterialIconKind.Airplane,
        DayStatus.SpecialLeave => MaterialIconKind.Gift,
        DayStatus.UnpaidLeave => MaterialIconKind.PowerSleep,
        DayStatus.OvertimeCompensation => MaterialIconKind.ClockAlert,
        DayStatus.Training => MaterialIconKind.BookOpenPageVariant,
        DayStatus.CompensatoryTime => MaterialIconKind.SwapHorizontal,
        _ => MaterialIconKind.Circle
    };

    public bool HasStatusIcon => Status != DayStatus.WorkDay &&
                                  Status != DayStatus.Work &&
                                  Status != DayStatus.Weekend;
}
