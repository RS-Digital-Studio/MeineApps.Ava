using ZeitManager.Models;

namespace ZeitManager.Services;

public interface ITimerService
{
    IReadOnlyList<TimerItem> Timers { get; }
    IReadOnlyList<TimerItem> RunningTimers { get; }

    Task LoadTimersAsync();
    Task<TimerItem> CreateTimerAsync(string name, TimeSpan duration);
    Task StartTimerAsync(TimerItem timer);
    Task PauseTimerAsync(TimerItem timer);
    Task StopTimerAsync(TimerItem timer);
    Task DeleteTimerAsync(TimerItem timer);
    Task DeleteAllTimersAsync();
    Task ExtendTimerAsync(TimerItem timer, TimeSpan extra);
    Task SnoozeTimerAsync(TimerItem timer, int minutes = 1);
    TimeSpan GetRemainingTime(TimerItem timer);

    event EventHandler<TimerItem>? TimerFinished;
    event EventHandler<TimerItem>? TimerTick;
    event EventHandler? TimersChanged;
}
