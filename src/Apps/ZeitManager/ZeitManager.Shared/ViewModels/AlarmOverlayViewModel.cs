using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class AlarmOverlayViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ITimerService _timerService;
    private readonly IAlarmSchedulerService _alarmScheduler;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private bool _canSnooze;

    [ObservableProperty]
    private string _snoozeText = string.Empty;

    [ObservableProperty]
    private bool _isTimerSource;

    private TimerItem? _sourceTimer;
    private AlarmItem? _sourceAlarm;
    private System.Timers.Timer? _clockTimer;

    // Localized strings
    public string DismissText => _localization.GetString("Dismiss");
    public string SnoozeLabel => _localization.GetString("Snooze");

    public AlarmOverlayViewModel(
        ITimerService timerService,
        IAlarmSchedulerService alarmScheduler,
        IAudioService audioService,
        ILocalizationService localization)
    {
        _timerService = timerService;
        _alarmScheduler = alarmScheduler;
        _audioService = audioService;
        _localization = localization;
    }

    public void ShowForTimer(TimerItem timer)
    {
        _sourceTimer = timer;
        _sourceAlarm = null;
        IsTimerSource = true;
        Title = timer.Name;
        Subtitle = _localization.GetString("TimerFinishedNotification");
        CanSnooze = true;
        SnoozeText = $"1 {_localization.GetString("MinutesShort")}";
        StartClock();
        _ = _audioService.PlayAsync(timer.AlarmTone, loop: true);
    }

    public void ShowForAlarm(AlarmItem alarm)
    {
        _sourceAlarm = alarm;
        _sourceTimer = null;
        IsTimerSource = false;
        Title = string.IsNullOrEmpty(alarm.Name) ? _localization.GetString("Alarm") : alarm.Name;
        Subtitle = alarm.TimeFormatted;
        CanSnooze = alarm.CurrentSnoozeCount < alarm.MaxSnoozeCount;
        SnoozeText = $"{alarm.SnoozeDurationMinutes} {_localization.GetString("MinutesShort")} ({alarm.MaxSnoozeCount - alarm.CurrentSnoozeCount}x)";
        StartClock();
        _ = _audioService.PlayAsync(alarm.AlarmTone, loop: true);
    }

    [RelayCommand]
    private async Task Dismiss()
    {
        _audioService.Stop();
        StopClock();

        if (_sourceTimer != null)
        {
            await _timerService.StopTimerAsync(_sourceTimer);
        }
        else if (_sourceAlarm != null)
        {
            await _alarmScheduler.DismissAlarmAsync(_sourceAlarm);
        }

        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task Snooze()
    {
        _audioService.Stop();
        StopClock();

        if (_sourceTimer != null)
        {
            await _timerService.SnoozeTimerAsync(_sourceTimer);
        }
        else if (_sourceAlarm != null)
        {
            await _alarmScheduler.SnoozeAlarmAsync(_sourceAlarm);
        }

        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DismissRequested;

    private void StartClock()
    {
        _clockTimer?.Dispose();
        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                CurrentTime = DateTime.Now.ToString("HH:mm"));
        _clockTimer.Start();
        CurrentTime = DateTime.Now.ToString("HH:mm");
    }

    private void StopClock()
    {
        _clockTimer?.Stop();
        _clockTimer?.Dispose();
        _clockTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopClock();
        _audioService.Stop();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
