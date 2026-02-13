using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace ZeitManager.Models;

[Table("Timers")]
public partial class TimerItem : ObservableObject
{
    private int _id;
    private string _name = string.Empty;
    private long _durationTicks;
    private long _remainingTimeTicks;
    private TimerState _state = TimerState.Stopped;
    private string? _startedAt;
    private string? _pausedAt;
    private bool _notifyOnFinish = true;
    private string _alarmTone = "default";
    private bool _vibrate = true;
    private bool _autoRepeat;
    private string _createdAt = DateTime.UtcNow.ToString("O");

    [PrimaryKey, AutoIncrement]
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public long DurationTicks
    {
        get => _durationTicks;
        set => SetProperty(ref _durationTicks, value);
    }

    public long RemainingTimeTicks
    {
        get => _remainingTimeTicks;
        set
        {
            if (SetProperty(ref _remainingTimeTicks, value))
            {
                OnPropertyChanged(nameof(RemainingTime));
                OnPropertyChanged(nameof(RemainingTimeFormatted));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(ProgressFraction));
            }
        }
    }

    public TimerState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsNotRunning));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsFinished));
                OnPropertyChanged(nameof(IsNotFinished));
            }
        }
    }

    public string? StartedAt
    {
        get => _startedAt;
        set => SetProperty(ref _startedAt, value);
    }

    public string? PausedAt
    {
        get => _pausedAt;
        set => SetProperty(ref _pausedAt, value);
    }

    public bool NotifyOnFinish
    {
        get => _notifyOnFinish;
        set => SetProperty(ref _notifyOnFinish, value);
    }

    public string AlarmTone
    {
        get => _alarmTone;
        set => SetProperty(ref _alarmTone, value);
    }

    public bool Vibrate
    {
        get => _vibrate;
        set => SetProperty(ref _vibrate, value);
    }

    public bool AutoRepeat
    {
        get => _autoRepeat;
        set => SetProperty(ref _autoRepeat, value);
    }

    public string CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    /// <summary>
    /// Snapshot of RemainingTimeTicks at the moment the timer was started.
    /// Used by GetRemainingTime to avoid cumulative drift.
    /// </summary>
    [Ignore]
    public long RemainingAtStartTicks { get; set; }

    // Computed properties (not in DB)

    [Ignore]
    public TimeSpan Duration
    {
        get => TimeSpan.FromTicks(DurationTicks);
        set => DurationTicks = value.Ticks;
    }

    [Ignore]
    public TimeSpan RemainingTime
    {
        get => TimeSpan.FromTicks(RemainingTimeTicks);
        set => RemainingTimeTicks = value.Ticks;
    }

    [Ignore]
    public DateTime? StartedAtDateTime
    {
        get => string.IsNullOrEmpty(StartedAt) ? null : DateTime.Parse(StartedAt, null, DateTimeStyles.RoundtripKind);
        set => StartedAt = value?.ToString("O");
    }

    [Ignore]
    public DateTime? PausedAtDateTime
    {
        get => string.IsNullOrEmpty(PausedAt) ? null : DateTime.Parse(PausedAt, null, DateTimeStyles.RoundtripKind);
        set => PausedAt = value?.ToString("O");
    }

    [Ignore]
    public double ProgressPercent =>
        DurationTicks > 0 ? Math.Clamp((double)RemainingTimeTicks / DurationTicks * 100, 0, 100) : 0;

    /// <summary>Fortschritt als Fraktion (0.0-1.0) für CircularProgress-Bindung.</summary>
    [Ignore]
    public double ProgressFraction =>
        DurationTicks > 0 ? Math.Clamp((double)RemainingTimeTicks / DurationTicks, 0, 1) : 0;

    [Ignore]
    public bool IsRunning => State == TimerState.Running;

    [Ignore]
    public bool IsNotRunning => State != TimerState.Running;

    [Ignore]
    public bool IsPaused => State == TimerState.Paused;

    [Ignore]
    public bool IsFinished => State == TimerState.Finished;

    [Ignore]
    public bool IsNotFinished => State != TimerState.Finished;

    [Ignore]
    public string RemainingTimeFormatted
    {
        get
        {
            var ts = RemainingTime;
            if ((int)ts.TotalHours > 0)
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
    }
}
