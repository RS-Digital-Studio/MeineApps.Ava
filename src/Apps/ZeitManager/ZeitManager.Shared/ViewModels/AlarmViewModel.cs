using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

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

    // Guards gegen Double-Tap
    private bool _isToggling;

    // Delete confirmation state
    private AlarmItem? _alarmToDelete;

    [ObservableProperty]
    private bool _isDeleteConfirmVisible;

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

    public bool HasAlarms => Alarms.Count > 0;

    public IReadOnlyList<SoundItem> AvailableSounds => _audioService.AvailableSounds;

    public AlarmViewModel(IDatabaseService database, ILocalizationService localization, IAudioService audioService, IAlarmSchedulerService alarmScheduler, ShiftScheduleViewModel shiftScheduleViewModel)
    {
        _database = database;
        _localization = localization;
        _audioService = audioService;
        _alarmScheduler = alarmScheduler;
        _shiftScheduleViewModel = shiftScheduleViewModel;
        _localization.LanguageChanged += OnLanguageChanged;
        _ = LoadAlarms();
    }

    [RelayCommand]
    private async Task LoadAlarms()
    {
        var alarms = await _database.GetAlarmsAsync();
        Alarms = new ObservableCollection<AlarmItem>(alarms.Where(a => !a.IsShiftAlarm));
        OnPropertyChanged(nameof(HasAlarms));
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
            alarm.IsEnabled = !alarm.IsEnabled;
            // ScheduleAlarmAsync/CancelAlarmAsync speichert in DB + plant/cancelt System-Notification
            if (alarm.IsEnabled)
                await _alarmScheduler.ScheduleAlarmAsync(alarm);
            else
                await _alarmScheduler.CancelAlarmAsync(alarm);
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
        await _audioService.PlayAsync(SelectedAlarmTone);
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        IsShiftScheduleMode = !IsShiftScheduleMode;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);

        // Notify RepeatDaysFormatted on each AlarmItem (uses LocalizationManager)
        foreach (var alarm in Alarms)
            alarm.NotifyLocalizationChanged();
    }
}
