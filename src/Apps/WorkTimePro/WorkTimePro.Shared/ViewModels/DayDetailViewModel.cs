using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for day details
/// </summary>
public partial class DayDetailViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    public DayDetailViewModel(
        IDatabaseService database,
        ICalculationService calculation,
        IPurchaseService purchaseService,
        ITrialService trialService)
    {
        _database = database;
        _calculation = calculation;
        _purchaseService = purchaseService;
        _trialService = trialService;
    }

    // === Properties ===

    [ObservableProperty]
    private string _dateString = DateTime.Today.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _dateDisplay = "";

    [ObservableProperty]
    private WorkDay? _workDay;

    [ObservableProperty]
    private ObservableCollection<TimeEntry> _timeEntries = new();

    [ObservableProperty]
    private ObservableCollection<PauseEntry> _pauseEntries = new();

    [ObservableProperty]
    private string _workTimeDisplay = "0:00";

    [ObservableProperty]
    private string _pauseTimeDisplay = "0:00";

    [ObservableProperty]
    private string _autoPauseDisplay = "0:00";

    [ObservableProperty]
    private string _balanceDisplay = "+0:00";

    [ObservableProperty]
    private string _balanceColor = "#4CAF50";

    [ObservableProperty]
    private string _statusDisplay = "";

    [ObservableProperty]
    private string _statusIcon = "";

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _hasAutoPause;

    [ObservableProperty]
    private ObservableCollection<string> _warnings = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Derived properties
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasNoTimeEntries => TimeEntries.Count == 0;
    public bool HasNoPauseEntries => PauseEntries.Count == 0;

    public MaterialIconKind StatusIconKind => WorkDay?.Status switch
    {
        DayStatus.WorkDay => MaterialIconKind.Briefcase,
        DayStatus.Weekend => MaterialIconKind.Sleep,
        DayStatus.Vacation => MaterialIconKind.Beach,
        DayStatus.Holiday => MaterialIconKind.PartyPopper,
        DayStatus.Sick => MaterialIconKind.Thermometer,
        DayStatus.HomeOffice => MaterialIconKind.HomeAccount,
        DayStatus.BusinessTrip => MaterialIconKind.Airplane,
        DayStatus.OvertimeCompensation => MaterialIconKind.ClockAlert,
        DayStatus.SpecialLeave => MaterialIconKind.Gift,
        _ => MaterialIconKind.CalendarMonth
    };

    // === Lifecycle ===

    partial void OnDateStringChanged(string value)
    {
        if (DateTime.TryParse(value, out var date))
        {
            SelectedDate = date;
        }
        _ = LoadDataAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, t.Exception?.Message));
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    // === Commands ===

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            WorkDay = await _database.GetOrCreateWorkDayAsync(SelectedDate);

            DateDisplay = SelectedDate.ToString("dddd, dd. MMMM yyyy");
            StatusDisplay = GetStatusText(WorkDay.Status);
            StatusIcon = WorkDay.StatusIcon;
            IsLocked = WorkDay.IsLocked;

            // Load entries
            var entries = await _database.GetTimeEntriesAsync(WorkDay.Id);
            TimeEntries = new ObservableCollection<TimeEntry>(entries);

            var pauses = await _database.GetPauseEntriesAsync(WorkDay.Id);
            PauseEntries = new ObservableCollection<PauseEntry>(pauses);

            // Times
            WorkTimeDisplay = WorkDay.ActualWorkDisplay;
            PauseTimeDisplay = FormatMinutes(WorkDay.ManualPauseMinutes);
            AutoPauseDisplay = FormatMinutes(WorkDay.AutoPauseMinutes);
            BalanceDisplay = WorkDay.BalanceDisplay;
            BalanceColor = WorkDay.BalanceColor;
            HasAutoPause = WorkDay.HasAutoPause;

            // Warnings
            var warningList = await _calculation.CheckLegalComplianceAsync(WorkDay);
            Warnings = new ObservableCollection<string>(warningList);

            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasNoTimeEntries));
            OnPropertyChanged(nameof(HasNoPauseEntries));
            OnPropertyChanged(nameof(StatusIconKind));

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
    private async Task ChangeStatusAsync()
    {
        if (WorkDay == null || IsLocked) return;

        // TODO: implement platform-specific status picker dialog
        // Default: cycle through common statuses
        WorkDay.Status = WorkDay.Status switch
        {
            DayStatus.WorkDay => DayStatus.HomeOffice,
            DayStatus.HomeOffice => DayStatus.Vacation,
            DayStatus.Vacation => DayStatus.Sick,
            DayStatus.Sick => DayStatus.Holiday,
            DayStatus.Holiday => DayStatus.BusinessTrip,
            _ => DayStatus.WorkDay
        };

        await _database.SaveWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (WorkDay == null || IsLocked) return;

        // TODO: implement platform-specific time picker dialog
        var defaultTime = SelectedDate == DateTime.Today
            ? DateTime.Now
            : SelectedDate.Date.Add(new TimeSpan(8, 0, 0));

        var entry = new TimeEntry
        {
            WorkDayId = WorkDay.Id,
            Timestamp = defaultTime,
            Type = EntryType.CheckIn,
            IsManuallyEdited = true
        };

        await _database.SaveTimeEntryAsync(entry);
        await _calculation.RecalculateWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task EditEntryAsync(TimeEntry? entry)
    {
        if (entry == null || WorkDay == null || IsLocked) return;

        // TODO: implement platform-specific time edit dialog
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(TimeEntry? entry)
    {
        if (entry == null || WorkDay == null || IsLocked) return;

        await _database.DeleteTimeEntryAsync(entry.Id);
        await _calculation.RecalculateWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task EditPauseAsync(PauseEntry? pause)
    {
        if (pause == null || WorkDay == null || IsLocked) return;

        if (pause.IsAutoPause)
        {
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.AutoBreakInfo);
            return;
        }

        // TODO: implement platform-specific pause edit dialog
    }

    [RelayCommand]
    private async Task DeletePauseAsync(PauseEntry? pause)
    {
        if (pause == null || WorkDay == null || IsLocked) return;

        if (pause.IsAutoPause)
        {
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.AutoPauseCannotDelete);
            return;
        }

        await _database.DeletePauseEntryAsync(pause.Id);
        await _calculation.RecalculatePauseTimeAsync(WorkDay);
        await _calculation.RecalculateWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // === Helper methods ===

    private static string GetStatusText(DayStatus status) => status switch
    {
        DayStatus.WorkDay => AppStrings.DayStatus_WorkDay,
        DayStatus.Weekend => AppStrings.DayStatus_Weekend,
        DayStatus.Vacation => AppStrings.DayStatus_Vacation,
        DayStatus.Holiday => AppStrings.DayStatus_Holiday,
        DayStatus.Sick => AppStrings.DayStatus_Sick,
        DayStatus.HomeOffice => AppStrings.DayStatus_HomeOffice,
        DayStatus.BusinessTrip => AppStrings.DayStatus_BusinessTrip,
        DayStatus.OvertimeCompensation => AppStrings.OvertimeCompensation,
        DayStatus.SpecialLeave => AppStrings.SpecialLeave,
        _ => ""
    };

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return $"{hours}:{mins:D2}";
    }
}
