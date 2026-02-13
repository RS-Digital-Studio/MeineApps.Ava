using System.Collections.ObjectModel;
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;
using Timer = System.Timers.Timer;

namespace ZeitManager.ViewModels;

public partial class AlarmViewModel : ObservableObject
{
    private readonly IDatabaseService _database;
    private readonly ILocalizationService _localization;
    private readonly IAudioService _audioService;
    private readonly IAlarmSchedulerService _alarmScheduler;

    [ObservableProperty]
    private ObservableCollection<AlarmItem> _alarms = [];

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isShiftScheduleMode;

    [ObservableProperty]
    private ShiftScheduleViewModel _shiftScheduleViewModel;

    [ObservableProperty]
    private AlarmItem? _editingAlarm;

    // Editor fields
    [ObservableProperty]
    private int _alarmHour = 7;

    [ObservableProperty]
    private int _alarmMinute;

    [ObservableProperty]
    private string _alarmName = string.Empty;

    [ObservableProperty]
    private WeekDays _repeatDays = WeekDays.None;

    [ObservableProperty]
    private bool _vibrate = true;

    [ObservableProperty]
    private int _snoozeDuration = 5;

    [ObservableProperty]
    private string _selectedAlarmTone = "default";

    // Challenge
    [ObservableProperty]
    private bool _challengeEnabled;

    [ObservableProperty]
    private ChallengeType _selectedChallengeType = ChallengeType.None;

    [ObservableProperty]
    private ChallengeDifficulty _selectedDifficulty = ChallengeDifficulty.Easy;

    // Nächster-Alarm-Countdown
    [ObservableProperty]
    private string _nextAlarmCountdown = "";

    private Timer? _countdownTimer;

    // Guards gegen Double-Tap
    private bool _isToggling;

    // Delete confirmation state
    private AlarmItem? _alarmToDelete;

    [ObservableProperty]
    private bool _isDeleteConfirmVisible;

    // Urlaubsmodus (Alarm-Pause)
    [ObservableProperty]
    private bool _isPauseDialogVisible;

    [ObservableProperty]
    private int _pauseDays = 7;

    // Localized strings
    public string TitleText => _localization.GetString("AlarmTitle");
    public string NewAlarmText => _localization.GetString("NewAlarm");
    public string NoAlarmsText => _localization.GetString("NoAlarms");
    public string CreateAlarmText => _localization.GetString("CreateAlarm");
    public string SaveText => _localization.GetString("Save");
    public string CancelText => _localization.GetString("Cancel");
    public string DeleteText => _localization.GetString("Delete");
    public string AlarmSoundText => _localization.GetString("AlarmSound");
    public string TestText => _localization.GetString("Test");
    public string ShiftText => _localization.GetString("Shift");
    public string RepeatText => _localization.GetString("Repeat");
    public string VibrateText => _localization.GetString("Vibrate");
    public string ConfirmDeleteTitleText => _localization.GetString("ConfirmDeleteTitle");
    public string ConfirmDeleteMessageText => _localization.GetString("ConfirmDeleteMessage");
    public string YesText => _localization.GetString("Yes");
    public string NoText => _localization.GetString("No");

    // Localized weekday short names for editor buttons
    public string MondayShortText => _localization.GetString("MondayShort");
    public string TuesdayShortText => _localization.GetString("TuesdayShort");
    public string WednesdayShortText => _localization.GetString("WednesdayShort");
    public string ThursdayShortText => _localization.GetString("ThursdayShort");
    public string FridayShortText => _localization.GetString("FridayShort");
    public string SaturdayShortText => _localization.GetString("SaturdayShort");
    public string SundayShortText => _localization.GetString("SundayShort");
    public string AlarmNamePlaceholderText => _localization.GetString("AlarmNamePlaceholder");
    public string HoursShortText => _localization.GetString("HoursShort");
    public string MinutesShortText => _localization.GetString("MinutesShort");

    public string PickFromDeviceText => _localization.GetString("PickFromDevice");

    // Urlaubsmodus Texte
    public bool IsAllPaused => _alarmScheduler.IsAllPaused;
    public string PausedUntilText => _alarmScheduler.PausedUntil != null
        ? string.Format(_localization.GetString("PausedUntilFormat"), _alarmScheduler.PausedUntil.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"))
        : string.Empty;
    public string PauseAllAlarmsText => _localization.GetString("PauseAllAlarms");
    public string ResumeAllAlarmsText => _localization.GetString("ResumeAllAlarms");
    public string VacationModeText => _localization.GetString("VacationMode");
    public string PauseDurationText => _localization.GetString("PauseDuration");
    public string DaysText => _localization.GetString("Days");

    public bool HasAlarms => Alarms.Count > 0;

    public bool HasNextAlarm => !string.IsNullOrEmpty(NextAlarmCountdown);
    public string NextAlarmInText => _localization.GetString("NextAlarmIn");
    public string NewAlarmButtonText => _localization.GetString("NewAlarmButton");

    // Weekday-Selected Properties für runde Toggle-Buttons
    public bool IsMondaySelected => RepeatDays.HasFlag(WeekDays.Monday);
    public bool IsTuesdaySelected => RepeatDays.HasFlag(WeekDays.Tuesday);
    public bool IsWednesdaySelected => RepeatDays.HasFlag(WeekDays.Wednesday);
    public bool IsThursdaySelected => RepeatDays.HasFlag(WeekDays.Thursday);
    public bool IsFridaySelected => RepeatDays.HasFlag(WeekDays.Friday);
    public bool IsSaturdaySelected => RepeatDays.HasFlag(WeekDays.Saturday);
    public bool IsSundaySelected => RepeatDays.HasFlag(WeekDays.Sunday);

    [ObservableProperty]
    private IReadOnlyList<SoundItem> _availableSounds;

    private void RefreshAvailableSounds() => AvailableSounds = _audioService.AvailableSounds;

    public AlarmViewModel(IDatabaseService database, ILocalizationService localization, IAudioService audioService, IAlarmSchedulerService alarmScheduler, ShiftScheduleViewModel shiftScheduleViewModel)
    {
        _database = database;
        _localization = localization;
        _audioService = audioService;
        _alarmScheduler = alarmScheduler;
        _shiftScheduleViewModel = shiftScheduleViewModel;
        _availableSounds = _audioService.AvailableSounds;
        _localization.LanguageChanged += OnLanguageChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadAlarms();
        UpdateNextAlarmCountdown();

        // Timer für regelmäßige Countdown-Aktualisierung (60s)
        _countdownTimer = new Timer(60_000);
        _countdownTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(UpdateNextAlarmCountdown);
        _countdownTimer.Start();
    }

    /// <summary>Berechnet den Countdown zum nächsten aktiven Alarm.</summary>
    private void UpdateNextAlarmCountdown()
    {
        if (Alarms.Count == 0 || _alarmScheduler.IsAllPaused)
        {
            NextAlarmCountdown = "";
            OnPropertyChanged(nameof(HasNextAlarm));
            return;
        }

        var now = DateTime.Now;
        TimeSpan? shortest = null;

        foreach (var alarm in Alarms.Where(a => a.IsEnabled))
        {
            var alarmToday = now.Date.Add(alarm.Time.ToTimeSpan());
            if (alarmToday <= now)
                alarmToday = alarmToday.AddDays(1);

            var delta = alarmToday - now;
            if (shortest == null || delta < shortest)
                shortest = delta;
        }

        if (shortest != null)
        {
            var h = (int)shortest.Value.TotalHours;
            var m = shortest.Value.Minutes;
            NextAlarmCountdown = string.Format(_localization.GetString("NextAlarmCountdownFormat"), h, m);
        }
        else
        {
            NextAlarmCountdown = "";
        }
        OnPropertyChanged(nameof(HasNextAlarm));
    }

    partial void OnRepeatDaysChanged(WeekDays value)
    {
        OnPropertyChanged(nameof(IsMondaySelected));
        OnPropertyChanged(nameof(IsTuesdaySelected));
        OnPropertyChanged(nameof(IsWednesdaySelected));
        OnPropertyChanged(nameof(IsThursdaySelected));
        OnPropertyChanged(nameof(IsFridaySelected));
        OnPropertyChanged(nameof(IsSaturdaySelected));
        OnPropertyChanged(nameof(IsSundaySelected));
    }

    [RelayCommand]
    private async Task LoadAlarms()
    {
        var alarms = await _database.GetAlarmsAsync();
        Alarms = new ObservableCollection<AlarmItem>(alarms.Where(a => !a.IsShiftAlarm));
        OnPropertyChanged(nameof(HasAlarms));
        UpdateNextAlarmCountdown();
    }

    [RelayCommand]
    private void NewAlarm()
    {
        EditingAlarm = null;
        AlarmHour = 7;
        AlarmMinute = 0;
        AlarmName = "";
        RepeatDays = WeekDays.None;
        Vibrate = true;
        SnoozeDuration = 5;
        SelectedAlarmTone = _audioService.DefaultAlarmSound;
        ChallengeEnabled = false;
        SelectedChallengeType = ChallengeType.None;
        SelectedDifficulty = ChallengeDifficulty.Easy;
        IsEditMode = true;
    }

    [RelayCommand]
    private void EditAlarm(AlarmItem alarm)
    {
        EditingAlarm = alarm;
        AlarmHour = alarm.Time.Hour;
        AlarmMinute = alarm.Time.Minute;
        AlarmName = alarm.Name;
        RepeatDays = alarm.RepeatDaysEnum;
        Vibrate = alarm.Vibrate;
        SnoozeDuration = alarm.SnoozeDurationMinutes;
        SelectedAlarmTone = alarm.AlarmTone;
        ChallengeEnabled = alarm.ChallengeEnabled;
        SelectedChallengeType = alarm.ChallengeType;
        SelectedDifficulty = alarm.ChallengeDifficulty;
        IsEditMode = true;
    }

    [RelayCommand]
    private async Task SaveAlarm()
    {
        var alarm = EditingAlarm ?? new AlarmItem();
        alarm.Name = AlarmName;
        alarm.Time = new TimeOnly(AlarmHour, AlarmMinute);
        alarm.RepeatDaysEnum = RepeatDays;
        alarm.Vibrate = Vibrate;
        alarm.SnoozeDurationMinutes = Math.Max(1, SnoozeDuration); // Mindestens 1 Minute
        alarm.AlarmTone = SelectedAlarmTone;
        alarm.ChallengeEnabled = ChallengeEnabled;
        alarm.ChallengeType = SelectedChallengeType;
        alarm.ChallengeDifficulty = SelectedDifficulty;
        alarm.IsEnabled = true;

        // ScheduleAlarmAsync speichert in DB + plant System-Notification
        await _alarmScheduler.ScheduleAlarmAsync(alarm);
        IsEditMode = false;
        await LoadAlarms();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _audioService.Stop();
        IsEditMode = false;
    }

    [RelayCommand]
    private void DeleteAlarm(AlarmItem alarm)
    {
        _alarmToDelete = alarm;
        IsDeleteConfirmVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAlarm()
    {
        if (_alarmToDelete != null)
        {
            // Erst System-Notification canceln, dann aus DB löschen
            await _alarmScheduler.CancelAlarmAsync(_alarmToDelete);
            await _database.DeleteAlarmAsync(_alarmToDelete);
            _alarmToDelete = null;
            await LoadAlarms();
        }
        IsDeleteConfirmVisible = false;
    }

    [RelayCommand]
    private void CancelDeleteAlarm()
    {
        _alarmToDelete = null;
        IsDeleteConfirmVisible = false;
    }

    [RelayCommand]
    private async Task ToggleAlarm(AlarmItem alarm)
    {
        if (_isToggling) return;
        _isToggling = true;
        try
        {
            // IsEnabled wird bereits vom ToggleSwitch-Binding gesetzt,
            // hier nur noch Scheduler aktualisieren
            if (alarm.IsEnabled)
                await _alarmScheduler.ScheduleAlarmAsync(alarm);
            else
                await _alarmScheduler.CancelAlarmAsync(alarm);
            UpdateNextAlarmCountdown();
        }
        finally
        {
            _isToggling = false;
        }
    }

    [RelayCommand]
    private void ToggleDay(string dayName)
    {
        var day = dayName switch
        {
            "Monday" => WeekDays.Monday,
            "Tuesday" => WeekDays.Tuesday,
            "Wednesday" => WeekDays.Wednesday,
            "Thursday" => WeekDays.Thursday,
            "Friday" => WeekDays.Friday,
            "Saturday" => WeekDays.Saturday,
            "Sunday" => WeekDays.Sunday,
            _ => WeekDays.None
        };

        RepeatDays = RepeatDays.HasFlag(day) ? RepeatDays & ~day : RepeatDays | day;
    }

    [RelayCommand]
    private async Task PreviewSound()
    {
        // Prüfen ob gewählter Sound eine URI hat (System/Custom Sound)
        var sound = AvailableSounds.FirstOrDefault(s => s.Id == SelectedAlarmTone);
        if (sound?.Uri != null)
            await _audioService.PlayUriAsync(sound.Uri);
        else
            await _audioService.PlayAsync(SelectedAlarmTone);
    }

    [RelayCommand]
    private async Task PickAlarmSound()
    {
        var picked = await _audioService.PickSoundAsync();
        if (picked != null)
        {
            RefreshAvailableSounds();
            SelectedAlarmTone = picked.Id;
        }
    }

    [RelayCommand]
    private async Task SwipeDeleteAlarm(AlarmItem alarm)
    {
        await _alarmScheduler.CancelAlarmAsync(alarm);
        await _database.DeleteAlarmAsync(alarm);
        await LoadAlarms();
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        IsShiftScheduleMode = !IsShiftScheduleMode;
    }

    // Urlaubsmodus Commands
    [RelayCommand]
    private void ShowPauseDialog() => IsPauseDialogVisible = true;

    [RelayCommand]
    private void CancelPauseDialog() => IsPauseDialogVisible = false;

    [RelayCommand]
    private async Task PauseAllAlarms()
    {
        var pauseUntil = DateTime.UtcNow.AddDays(PauseDays);
        await _alarmScheduler.PauseAllAlarmsAsync(pauseUntil);
        IsPauseDialogVisible = false;
        OnPropertyChanged(nameof(IsAllPaused));
        OnPropertyChanged(nameof(PausedUntilText));
    }

    [RelayCommand]
    private async Task ResumeAllAlarms()
    {
        await _alarmScheduler.ResumeAllAlarmsAsync();
        OnPropertyChanged(nameof(IsAllPaused));
        OnPropertyChanged(nameof(PausedUntilText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);

        // Notify RepeatDaysFormatted on each AlarmItem (uses LocalizationManager)
        foreach (var alarm in Alarms)
            alarm.NotifyLocalizationChanged();
    }
}
