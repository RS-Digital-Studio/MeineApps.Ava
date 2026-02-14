using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel für Tagesdetails mit Bearbeitung von Zeiteinträgen und Pausen
/// </summary>
public partial class DayDetailViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IPurchaseService _purchaseService;
    private readonly ITrialService _trialService;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    // Tracking welcher Eintrag bearbeitet wird (null = neuer Eintrag)
    private TimeEntry? _editingTimeEntry;
    private PauseEntry? _editingPauseEntry;

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

    // === TimeEntry Overlay Properties ===

    [ObservableProperty]
    private bool _isTimeEntryOverlayVisible;

    [ObservableProperty]
    private string _timeEntryOverlayTitle = "";

    [ObservableProperty]
    private int _editHour;

    [ObservableProperty]
    private int _editMinute;

    [ObservableProperty]
    private bool _editIsCheckIn = true;

    [ObservableProperty]
    private string _editNote = "";

    // === PauseEntry Overlay Properties ===

    [ObservableProperty]
    private bool _isPauseOverlayVisible;

    [ObservableProperty]
    private string _pauseOverlayTitle = "";

    [ObservableProperty]
    private int _pauseStartHour;

    [ObservableProperty]
    private int _pauseStartMinute;

    [ObservableProperty]
    private int _pauseEndHour;

    [ObservableProperty]
    private int _pauseEndMinute;

    [ObservableProperty]
    private string _pauseNote = "";

    // === Confirm Delete Overlay Properties ===

    [ObservableProperty]
    private bool _isConfirmDeleteVisible;

    [ObservableProperty]
    private string _confirmDeleteMessage = "";

    private Func<Task>? _pendingDeleteAction;

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
            StatusDisplay = TimeFormatter.GetStatusName(WorkDay.Status);
            StatusIcon = WorkDay.StatusIcon;
            IsLocked = WorkDay.IsLocked;

            // Einträge laden
            var entries = await _database.GetTimeEntriesAsync(WorkDay.Id);
            TimeEntries = new ObservableCollection<TimeEntry>(entries);

            var pauses = await _database.GetPauseEntriesAsync(WorkDay.Id);
            PauseEntries = new ObservableCollection<PauseEntry>(pauses);

            // Zeiten
            WorkTimeDisplay = WorkDay.ActualWorkDisplay;
            PauseTimeDisplay = TimeFormatter.FormatMinutes(WorkDay.ManualPauseMinutes);
            AutoPauseDisplay = TimeFormatter.FormatMinutes(WorkDay.AutoPauseMinutes);
            BalanceDisplay = WorkDay.BalanceDisplay;
            BalanceColor = WorkDay.BalanceColor;
            HasAutoPause = WorkDay.HasAutoPause;

            // Warnungen
            var warningList = await _calculation.CheckLegalComplianceAsync(WorkDay);
            Warnings = new ObservableCollection<string>(warningList);

            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasNoTimeEntries));
            OnPropertyChanged(nameof(HasNoPauseEntries));
            OnPropertyChanged(nameof(StatusIconKind));

            // Premium Status
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

        WorkDay.Status = WorkDay.Status switch
        {
            DayStatus.WorkDay => DayStatus.HomeOffice,
            DayStatus.HomeOffice => DayStatus.Vacation,
            DayStatus.Vacation => DayStatus.Sick,
            DayStatus.Sick => DayStatus.Holiday,
            DayStatus.Holiday => DayStatus.BusinessTrip,
            DayStatus.BusinessTrip => DayStatus.SpecialLeave,
            DayStatus.SpecialLeave => DayStatus.UnpaidLeave,
            DayStatus.UnpaidLeave => DayStatus.OvertimeCompensation,
            DayStatus.OvertimeCompensation => DayStatus.Training,
            DayStatus.Training => DayStatus.CompensatoryTime,
            _ => DayStatus.WorkDay
        };

        await _database.SaveWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    // === TimeEntry Overlay Commands ===

    [RelayCommand]
    private void AddEntry()
    {
        if (WorkDay == null)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, "WorkDay"));
            return;
        }
        if (IsLocked) return;

        _editingTimeEntry = null;
        TimeEntryOverlayTitle = AppStrings.AddEntry;

        // Standardzeit: heute = jetzt, sonst 08:00
        var defaultTime = SelectedDate.Date == DateTime.Today
            ? DateTime.Now
            : SelectedDate.Date.Add(new TimeSpan(8, 0, 0));

        EditHour = defaultTime.Hour;
        EditMinute = defaultTime.Minute;
        EditIsCheckIn = true;
        EditNote = "";
        IsTimeEntryOverlayVisible = true;
    }

    [RelayCommand]
    private void EditEntry(TimeEntry? entry)
    {
        if (entry == null || WorkDay == null || IsLocked) return;

        _editingTimeEntry = entry;
        TimeEntryOverlayTitle = AppStrings.EditEntry;

        EditHour = entry.Timestamp.Hour;
        EditMinute = entry.Timestamp.Minute;
        EditIsCheckIn = entry.Type == EntryType.CheckIn;
        EditNote = entry.Note ?? "";
        IsTimeEntryOverlayVisible = true;
    }

    [RelayCommand]
    private async Task SaveTimeEntryAsync()
    {
        if (WorkDay == null) return;

        var newTimestamp = SelectedDate.Date.Add(new TimeSpan(EditHour, EditMinute, 0));
        var newType = EditIsCheckIn ? EntryType.CheckIn : EntryType.CheckOut;

        // Validierung: CheckIn/CheckOut-Reihenfolge prüfen
        if (!ValidateTimeEntry(newTimestamp, newType, _editingTimeEntry?.Id))
            return;

        if (_editingTimeEntry != null)
        {
            // Bestehenden Eintrag bearbeiten
            if (_editingTimeEntry.OriginalTimestamp == null)
                _editingTimeEntry.OriginalTimestamp = _editingTimeEntry.Timestamp;

            _editingTimeEntry.Timestamp = newTimestamp;
            _editingTimeEntry.Type = newType;
            _editingTimeEntry.Note = string.IsNullOrWhiteSpace(EditNote) ? null : EditNote;
            _editingTimeEntry.IsManuallyEdited = true;

            await _database.SaveTimeEntryAsync(_editingTimeEntry);
        }
        else
        {
            // Neuen Eintrag erstellen
            var entry = new TimeEntry
            {
                WorkDayId = WorkDay.Id,
                Timestamp = newTimestamp,
                Type = newType,
                Note = string.IsNullOrWhiteSpace(EditNote) ? null : EditNote,
                IsManuallyEdited = true
            };

            await _database.SaveTimeEntryAsync(entry);
        }

        IsTimeEntryOverlayVisible = false;
        await _calculation.RecalculateWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void CancelTimeEntryOverlay()
    {
        IsTimeEntryOverlayVisible = false;
    }

    [RelayCommand]
    private void ToggleEntryType()
    {
        EditIsCheckIn = !EditIsCheckIn;
    }

    // === PauseEntry Overlay Commands ===

    [RelayCommand]
    private void AddPause()
    {
        if (WorkDay == null)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, "WorkDay"));
            return;
        }
        if (IsLocked) return;

        _editingPauseEntry = null;
        PauseOverlayTitle = AppStrings.AddBreak;

        // Standard: 12:00-12:30
        PauseStartHour = 12;
        PauseStartMinute = 0;
        PauseEndHour = 12;
        PauseEndMinute = 30;
        PauseNote = "";
        IsPauseOverlayVisible = true;
    }

    [RelayCommand]
    private void EditPause(PauseEntry? pause)
    {
        if (pause == null || WorkDay == null || IsLocked) return;

        if (pause.IsAutoPause)
        {
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.AutoBreakInfo);
            return;
        }

        _editingPauseEntry = pause;
        PauseOverlayTitle = AppStrings.EditBreak;

        PauseStartHour = pause.StartTime.Hour;
        PauseStartMinute = pause.StartTime.Minute;
        PauseEndHour = pause.EndTime?.Hour ?? pause.StartTime.Hour;
        PauseEndMinute = pause.EndTime?.Minute ?? (pause.StartTime.Minute + 30) % 60;
        PauseNote = pause.Note ?? "";
        IsPauseOverlayVisible = true;
    }

    [RelayCommand]
    private async Task SavePauseAsync()
    {
        if (WorkDay == null) return;

        var startTime = SelectedDate.Date.Add(new TimeSpan(PauseStartHour, PauseStartMinute, 0));
        var endTime = SelectedDate.Date.Add(new TimeSpan(PauseEndHour, PauseEndMinute, 0));

        // Validierung: Endzeit nach Startzeit
        if (endTime <= startTime)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ValidationEndBeforeStart);
            return;
        }

        // Validierung: Keine Überlappung mit bestehenden manuellen Pausen
        if (!ValidatePauseEntry(startTime, endTime, _editingPauseEntry?.Id))
            return;

        if (_editingPauseEntry != null)
        {
            // Bestehende Pause bearbeiten
            _editingPauseEntry.StartTime = startTime;
            _editingPauseEntry.EndTime = endTime;
            _editingPauseEntry.Note = string.IsNullOrWhiteSpace(PauseNote) ? null : PauseNote;

            await _database.SavePauseEntryAsync(_editingPauseEntry);
        }
        else
        {
            // Neue Pause erstellen
            var pause = new PauseEntry
            {
                WorkDayId = WorkDay.Id,
                StartTime = startTime,
                EndTime = endTime,
                Type = PauseType.Manual,
                IsAutoPause = false,
                Note = string.IsNullOrWhiteSpace(PauseNote) ? null : PauseNote
            };

            await _database.SavePauseEntryAsync(pause);
        }

        IsPauseOverlayVisible = false;
        await _calculation.RecalculateWorkDayAsync(WorkDay);
        await LoadDataAsync();
    }

    [RelayCommand]
    private void CancelPauseOverlay()
    {
        IsPauseOverlayVisible = false;
    }

    // === Bestehende Commands ===

    [RelayCommand]
    private void DeleteEntry(TimeEntry? entry)
    {
        if (entry == null || WorkDay == null || IsLocked) return;

        // Bestätigung anfordern
        ConfirmDeleteMessage = string.Format(AppStrings.DeleteEntryConfirm, entry.Timestamp.ToString("HH:mm"));
        _pendingDeleteAction = async () =>
        {
            await _database.DeleteTimeEntryAsync(entry.Id);
            await _calculation.RecalculateWorkDayAsync(WorkDay);
            await LoadDataAsync();
        };
        IsConfirmDeleteVisible = true;
    }

    [RelayCommand]
    private void DeletePause(PauseEntry? pause)
    {
        if (pause == null || WorkDay == null || IsLocked) return;

        if (pause.IsAutoPause)
        {
            MessageRequested?.Invoke(AppStrings.Info, AppStrings.AutoPauseCannotDelete);
            return;
        }

        // Bestätigung anfordern
        ConfirmDeleteMessage = AppStrings.DeletePauseConfirm;
        _pendingDeleteAction = async () =>
        {
            await _database.DeletePauseEntryAsync(pause.Id);
            await _calculation.RecalculateWorkDayAsync(WorkDay);
            await LoadDataAsync();
        };
        IsConfirmDeleteVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsConfirmDeleteVisible = false;
        if (_pendingDeleteAction != null)
        {
            await _pendingDeleteAction();
            _pendingDeleteAction = null;
        }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmDeleteVisible = false;
        _pendingDeleteAction = null;
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // === Validierung ===

    /// <summary>
    /// Prüft ob ein TimeEntry konsistent ist (CheckIn/CheckOut-Reihenfolge)
    /// </summary>
    private bool ValidateTimeEntry(DateTime timestamp, EntryType type, int? excludeId)
    {
        // Alle Einträge sammeln, den bearbeiteten ausschließen
        var allEntries = TimeEntries
            .Where(e => e.Id != (excludeId ?? -1))
            .Select(e => (Time: e.Timestamp, e.Type))
            .Append((Time: timestamp, Type: type))
            .OrderBy(e => e.Time)
            .ToList();

        // Prüfen: Einträge sollten alternierend sein (CheckIn, CheckOut, CheckIn, ...)
        for (int i = 1; i < allEntries.Count; i++)
        {
            var prev = allEntries[i - 1];
            var curr = allEntries[i];

            // Gleiche Typen hintereinander → CheckIn vor CheckOut Reihenfolge verletzt
            if (prev.Type == curr.Type)
            {
                MessageRequested?.Invoke(AppStrings.Error, AppStrings.ValidationCheckInOutOrder);
                return false;
            }
        }

        // Erster Eintrag sollte CheckIn sein
        if (allEntries.Count > 0 && allEntries[0].Type == EntryType.CheckOut)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ValidationCheckInOutOrder);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Prüft ob eine Pause sich nicht mit bestehenden manuellen Pausen überschneidet
    /// </summary>
    private bool ValidatePauseEntry(DateTime startTime, DateTime endTime, int? excludeId)
    {
        var overlapping = PauseEntries
            .Where(p => !p.IsAutoPause && p.Id != (excludeId ?? -1) && p.EndTime != null)
            .Any(p => startTime < p.EndTime!.Value && endTime > p.StartTime);

        if (overlapping)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ValidationPauseOverlap);
            return false;
        }

        return true;
    }

}
