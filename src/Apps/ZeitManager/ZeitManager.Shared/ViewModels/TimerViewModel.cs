using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class TimerViewModel : ObservableObject
{
    private readonly ITimerService _timerService;
    private readonly ILocalizationService _localization;
    private readonly IDatabaseService _database;

    [ObservableProperty]
    private ObservableCollection<TimerItem> _timers = [];

    [ObservableProperty]
    private bool _isCreatingTimer;

    [ObservableProperty]
    private string _newTimerName = string.Empty;

    [ObservableProperty]
    private int _newTimerHours;

    [ObservableProperty]
    private int _newTimerMinutes = 5;

    [ObservableProperty]
    private int _newTimerSeconds;

    [ObservableProperty]
    private bool _newTimerAutoRepeat;

    [ObservableProperty]
    private ObservableCollection<TimerPreset> _presets = [];

    // Delete confirmation state
    private TimerItem? _timerToDelete;

    [ObservableProperty]
    private bool _isDeleteConfirmVisible;

    // Delete-All confirmation state
    [ObservableProperty]
    private bool _isDeleteAllConfirmVisible;

    // Localized strings
    public string TitleText => _localization.GetString("TimerTitle");
    public string QuickTimerText => _localization.GetString("QuickTimer");
    public string NoTimersText => _localization.GetString("NoTimers");
    public string CreateTimerText => _localization.GetString("CreateTimer");
    public string NewTimerTitleText => _localization.GetString("NewTimerTitle");
    public string StartText => _localization.GetString("Start");
    public string PauseText => _localization.GetString("Pause");
    public string StopText => _localization.GetString("Stop");
    public string DeleteText => _localization.GetString("Delete");
    public string CreateText => _localization.GetString("Create");
    public string CancelText => _localization.GetString("Cancel");
    public string HoursText => _localization.GetString("Hours");
    public string MinutesText => _localization.GetString("Minutes");
    public string ConfirmDeleteTitleText => _localization.GetString("ConfirmDeleteTitle");
    public string ConfirmDeleteMessageText => _localization.GetString("ConfirmDeleteMessage");
    public string DeleteAllText => _localization.GetString("DeleteAllTimers");
    public string ConfirmDeleteAllMessageText => _localization.GetString("DeleteAllTimersConfirm");
    public string ExtendTimerText => _localization.GetString("ExtendTimer");
    public string YesText => _localization.GetString("Yes");
    public string NoText => _localization.GetString("No");
    public string AutoRepeatText => _localization.GetString("AutoRepeat");
    public string PresetsText => _localization.GetString("Presets");
    public string SaveAsPresetText => _localization.GetString("SaveAsPreset");
    public string TimerEmptyHintText => _localization.GetString("TimerEmptyHint");
    public string CreateTimerButtonText => _localization.GetString("CreateTimerButton");

    public bool HasTimers => Timers.Count > 0;

    public TimerViewModel(ITimerService timerService, ILocalizationService localization, IDatabaseService database)
    {
        _timerService = timerService;
        _localization = localization;
        _database = database;
        _localization.LanguageChanged += OnLanguageChanged;

        _timerService.TimersChanged += (_, _) => LoadTimers();
        _timerService.TimerTick += (_, timer) => timer.RemainingTimeTicks = _timerService.GetRemainingTime(timer).Ticks;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _timerService.LoadTimersAsync();
        LoadTimers();

        // Presets laden
        var presets = await _database.GetTimerPresetsAsync();
        Presets = new ObservableCollection<TimerPreset>(presets);
    }

    private void LoadTimers()
    {
        Timers = new ObservableCollection<TimerItem>(
            _timerService.Timers.OrderBy(t => t.State switch
            {
                TimerState.Running => 0,
                TimerState.Paused => 1,
                TimerState.Stopped => 2,
                TimerState.Finished => 3,
                _ => 4
            }));
        OnPropertyChanged(nameof(HasTimers));
    }

    [RelayCommand]
    private void ShowCreateTimer() => IsCreatingTimer = true;

    [RelayCommand]
    private void HideCreateTimer() { IsCreatingTimer = false; NewTimerName = ""; NewTimerHours = 0; NewTimerMinutes = 5; NewTimerSeconds = 0; NewTimerAutoRepeat = false; }

    public event Action<string, string>? MessageRequested;

    private static readonly TimeSpan MaxDuration = new(23, 59, 59);

    [RelayCommand]
    private async Task CreateTimer()
    {
        var duration = new TimeSpan(NewTimerHours, NewTimerMinutes, NewTimerSeconds);
        if (duration <= TimeSpan.Zero)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error"),
                _localization.GetString("TimerDurationInvalid"));
            return;
        }

        if (duration > MaxDuration)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error"),
                _localization.GetString("TimerDurationTooLong"));
            return;
        }

        var timer = await _timerService.CreateTimerAsync(NewTimerName, duration);
        timer.AutoRepeat = NewTimerAutoRepeat;
        HideCreateTimer();
        await _timerService.StartTimerAsync(timer);
    }

    [RelayCommand]
    private async Task AddQuickTimer(string minutes)
    {
        if (int.TryParse(minutes, out var min))
        {
            var timer = await _timerService.CreateTimerAsync($"{min} min", TimeSpan.FromMinutes(min));
            await _timerService.StartTimerAsync(timer);
        }
    }

    [RelayCommand]
    private async Task StartTimer(TimerItem timer)
    {
        if (timer.State == TimerState.Running) return;
        await _timerService.StartTimerAsync(timer);
    }

    [RelayCommand]
    private async Task PauseTimer(TimerItem timer) => await _timerService.PauseTimerAsync(timer);

    [RelayCommand]
    private async Task StopTimer(TimerItem timer) => await _timerService.StopTimerAsync(timer);

    [RelayCommand]
    private void DeleteTimer(TimerItem timer)
    {
        _timerToDelete = timer;
        IsDeleteConfirmVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteTimer()
    {
        if (_timerToDelete != null)
        {
            await _timerService.DeleteTimerAsync(_timerToDelete);
            _timerToDelete = null;
        }
        IsDeleteConfirmVisible = false;
    }

    [RelayCommand]
    private void CancelDeleteTimer()
    {
        _timerToDelete = null;
        IsDeleteConfirmVisible = false;
    }

    // Alle Timer löschen
    [RelayCommand]
    private void DeleteAllTimers() => IsDeleteAllConfirmVisible = true;

    [RelayCommand]
    private async Task ConfirmDeleteAllTimers()
    {
        await _timerService.DeleteAllTimersAsync();
        IsDeleteAllConfirmVisible = false;
    }

    [RelayCommand]
    private void CancelDeleteAllTimers() => IsDeleteAllConfirmVisible = false;

    // Timer verlängern (+1 / +5 Min)
    [RelayCommand]
    private async Task ExtendTimer1(TimerItem timer) =>
        await _timerService.ExtendTimerAsync(timer, TimeSpan.FromMinutes(1));

    [RelayCommand]
    private async Task ExtendTimer5(TimerItem timer) =>
        await _timerService.ExtendTimerAsync(timer, TimeSpan.FromMinutes(5));

    // Preset-Commands

    [RelayCommand]
    private async Task StartFromPreset(TimerPreset preset)
    {
        var timer = await _timerService.CreateTimerAsync(preset.Name, preset.Duration);
        timer.AutoRepeat = preset.AutoRepeat;
        await _timerService.StartTimerAsync(timer);
    }

    [RelayCommand]
    private async Task SaveAsPreset()
    {
        var duration = new TimeSpan(NewTimerHours, NewTimerMinutes, NewTimerSeconds);
        if (duration <= TimeSpan.Zero) return;

        var preset = new TimerPreset
        {
            Name = string.IsNullOrWhiteSpace(NewTimerName) ? duration.ToString(@"mm\:ss") : NewTimerName,
            Duration = duration,
            AutoRepeat = NewTimerAutoRepeat
        };

        await _database.SaveTimerPresetAsync(preset);
        Presets.Insert(0, preset);
        MessageRequested?.Invoke(
            _localization.GetString("Success"),
            _localization.GetString("PresetSaved"));
    }

    [RelayCommand]
    private async Task DeletePreset(TimerPreset preset)
    {
        await _database.DeleteTimerPresetAsync(preset);
        Presets.Remove(preset);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }
}
