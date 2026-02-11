using System.Timers;
using Avalonia.Threading;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using Timer = System.Timers.Timer;

namespace ZeitManager.Services;

public class TimerService : ITimerService, IDisposable
{
    private readonly IDatabaseService _database;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localization;
    private readonly List<TimerItem> _timers = [];
    private readonly object _lock = new();
    private Timer? _uiTimer;

    public IReadOnlyList<TimerItem> Timers
    {
        get { lock (_lock) return _timers.ToList().AsReadOnly(); }
    }

    public IReadOnlyList<TimerItem> RunningTimers
    {
        get { lock (_lock) return _timers.Where(t => t.State == TimerState.Running).ToList().AsReadOnly(); }
    }

    public event EventHandler<TimerItem>? TimerFinished;
    public event EventHandler<TimerItem>? TimerTick;
    public event EventHandler? TimersChanged;

    public TimerService(IDatabaseService database, INotificationService notificationService, ILocalizationService localization)
    {
        _database = database;
        _notificationService = notificationService;
        _localization = localization;
    }

    public async Task LoadTimersAsync()
    {
        var timers = await _database.GetTimersAsync();
        var needsSave = false;

        // Timer-Recovery: Laufende Timer prüfen, ob sie in der Zwischenzeit abgelaufen sind
        foreach (var timer in timers.Where(t => t.State == TimerState.Running))
        {
            if (timer.StartedAtDateTime == null)
            {
                timer.State = TimerState.Stopped;
                needsSave = true;
                continue;
            }

            var elapsed = DateTime.UtcNow - timer.StartedAtDateTime.Value;
            var remaining = TimeSpan.FromTicks(timer.RemainingAtStartTicks) - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                // Timer ist abgelaufen während die App geschlossen war
                timer.State = TimerState.Finished;
                timer.RemainingTimeTicks = 0;
                needsSave = true;
            }
            else
            {
                // Timer läuft noch → UI-Timer starten
                timer.RemainingTimeTicks = remaining.Ticks;
            }
        }

        // Pausierte Timer: PausedAt prüfen
        foreach (var timer in timers.Where(t => t.State == TimerState.Paused && t.PausedAtDateTime == null))
        {
            timer.State = TimerState.Stopped;
            needsSave = true;
        }

        lock (_lock)
        {
            _timers.Clear();
            // Abgelaufene Timer nicht in die Liste aufnehmen (werden als Finished gemeldet)
            _timers.AddRange(timers.Where(t => t.State != TimerState.Finished));
        }

        // Abgelaufene Timer als "fertig" melden
        foreach (var timer in timers.Where(t => t.State == TimerState.Finished))
        {
            TimerFinished?.Invoke(this, timer);
            await _notificationService.CancelNotificationAsync($"timer_{timer.Id}");
        }

        if (needsSave)
        {
            foreach (var timer in timers.Where(t => t.State is TimerState.Stopped or TimerState.Finished))
                await _database.SaveTimerAsync(timer);
        }

        // UI-Timer starten falls laufende Timer vorhanden
        if (timers.Any(t => t.State == TimerState.Running))
            EnsureUiTimer();

        TimersChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<TimerItem> CreateTimerAsync(string name, TimeSpan duration)
    {
        var timer = new TimerItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Timer {Timers.Count + 1}" : name,
            Duration = duration,
            RemainingTime = duration,
            State = TimerState.Stopped
        };

        await _database.SaveTimerAsync(timer);
        lock (_lock) _timers.Add(timer);
        TimersChanged?.Invoke(this, EventArgs.Empty);
        return timer;
    }

    public async Task StartTimerAsync(TimerItem timer)
    {
        timer.State = TimerState.Running;
        timer.StartedAtDateTime = DateTime.UtcNow;
        timer.PausedAt = null;
        // Snapshot the remaining time at start, so tick updates don't cause drift
        timer.RemainingAtStartTicks = timer.RemainingTimeTicks;
        await _database.SaveTimerAsync(timer);
        EnsureUiTimer();
        TimersChanged?.Invoke(this, EventArgs.Empty);

        // System-Notification fuer den erwarteten Fertigstellungszeitpunkt planen
        var finishAt = DateTime.Now.Add(timer.RemainingTime);
        await _notificationService.ScheduleNotificationAsync(
            $"timer_{timer.Id}",
            timer.Name,
            _localization.GetString("TimerFinishedNotification"),
            finishAt);
    }

    public async Task PauseTimerAsync(TimerItem timer)
    {
        if (timer.State != TimerState.Running) return;

        // Save remaining time
        timer.RemainingTime = GetRemainingTime(timer);
        timer.State = TimerState.Paused;
        timer.PausedAtDateTime = DateTime.UtcNow;
        await _database.SaveTimerAsync(timer);
        CheckStopUiTimer();
        TimersChanged?.Invoke(this, EventArgs.Empty);
        await _notificationService.CancelNotificationAsync($"timer_{timer.Id}");
    }

    public async Task StopTimerAsync(TimerItem timer)
    {
        lock (_lock) _timers.Remove(timer);
        await _database.DeleteTimerAsync(timer);
        CheckStopUiTimer();
        TimersChanged?.Invoke(this, EventArgs.Empty);
        await _notificationService.CancelNotificationAsync($"timer_{timer.Id}");
    }

    public async Task SnoozeTimerAsync(TimerItem timer, int minutes = 1)
    {
        timer.RemainingTime = TimeSpan.FromMinutes(minutes);
        timer.State = TimerState.Stopped;
        timer.StartedAt = null;
        timer.PausedAt = null;
        // Re-add to list (was removed on finish)
        lock (_lock)
        {
            if (!_timers.Contains(timer))
                _timers.Add(timer);
        }
        await _database.SaveTimerAsync(timer);
        await StartTimerAsync(timer);
    }

    public async Task DeleteTimerAsync(TimerItem timer)
    {
        lock (_lock) _timers.Remove(timer);
        await _database.DeleteTimerAsync(timer);
        CheckStopUiTimer();
        TimersChanged?.Invoke(this, EventArgs.Empty);
        await _notificationService.CancelNotificationAsync($"timer_{timer.Id}");
    }

    public TimeSpan GetRemainingTime(TimerItem timer)
    {
        if (timer.State != TimerState.Running || timer.StartedAtDateTime == null)
            return timer.RemainingTime;

        var elapsed = DateTime.UtcNow - timer.StartedAtDateTime.Value;
        // Use the snapshot from start, not the continuously-updated RemainingTimeTicks
        var remaining = TimeSpan.FromTicks(timer.RemainingAtStartTicks) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void EnsureUiTimer()
    {
        if (_uiTimer != null) return;
        _uiTimer = new Timer(1000);
        _uiTimer.Elapsed += OnUiTimerTick;
        _uiTimer.Start();
    }

    private void CheckStopUiTimer()
    {
        if (RunningTimers.Count == 0 && _uiTimer != null)
        {
            _uiTimer.Stop();
            _uiTimer.Elapsed -= OnUiTimerTick;
            _uiTimer.Dispose();
            _uiTimer = null;
        }
    }

    private void OnUiTimerTick(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var timer in RunningTimers.ToList())
            {
                var remaining = GetRemainingTime(timer);
                if (remaining <= TimeSpan.Zero)
                {
                    timer.State = TimerState.Finished;
                    timer.RemainingTimeTicks = 0;
                    // Remove from list (hides from UI) but keep in DB for snooze
                    lock (_lock) _timers.Remove(timer);
                    TimersChanged?.Invoke(this, EventArgs.Empty);
                    TimerFinished?.Invoke(this, timer);
                    // System-Notification canceln (App war im Vordergrund, Overlay wird gezeigt)
                    _ = _notificationService.CancelNotificationAsync($"timer_{timer.Id}");
                }
                else
                {
                    TimerTick?.Invoke(this, timer);
                }
            }
        });
    }

    public void Dispose()
    {
        if (_uiTimer != null)
        {
            _uiTimer.Stop();
            _uiTimer.Elapsed -= OnUiTimerTick;
            _uiTimer.Dispose();
            _uiTimer = null;
        }
    }
}
