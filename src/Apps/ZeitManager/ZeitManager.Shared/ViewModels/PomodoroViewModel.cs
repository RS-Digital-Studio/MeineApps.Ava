using System.Collections.ObjectModel;
using System.Timers;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using ZeitManager.Models;
using ZeitManager.Services;
using Timer = System.Timers.Timer;

namespace ZeitManager.ViewModels;

public partial class PomodoroViewModel : ObservableObject, IDisposable
{
    private readonly IDatabaseService _database;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localization;
    private readonly IPreferencesService _preferences;
    private Timer? _timer;

    // Konfiguration (in Preferences gespeichert)
    [ObservableProperty]
    private int _workMinutes = 25;

    [ObservableProperty]
    private int _shortBreakMinutes = 5;

    [ObservableProperty]
    private int _longBreakMinutes = 15;

    [ObservableProperty]
    private int _cyclesBeforeLongBreak = 4;

    [ObservableProperty]
    private bool _autoStartNext = true;

    // Zustand
    [ObservableProperty]
    private PomodoroPhase _currentPhase = PomodoroPhase.Work;

    [ObservableProperty]
    private int _currentCycle = 1;

    [ObservableProperty]
    private int _completedSessions;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private TimeSpan _remainingTime;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private bool _isConfigVisible;

    // Statistiken-Ansicht
    [ObservableProperty]
    private bool _isStatisticsView;

    [ObservableProperty]
    private int _todaySessions;

    [ObservableProperty]
    private int _todayMinutes;

    [ObservableProperty]
    private int _weekSessions;

    [ObservableProperty]
    private ObservableCollection<DayStatistic> _weekDays = [];

    // Streak
    [ObservableProperty]
    private int _currentStreak;

    // CycleDots
    [ObservableProperty]
    private ObservableCollection<CycleDot> _cycleDots = [];

    private TimeSpan _totalPhaseTime;
    private DateTime _phaseStartedAt;

    // Lokalisierte Strings
    public string PomodoroTitleText => _localization.GetString("PomodoroTitle");
    public string WorkText => _localization.GetString("Work");
    public string ShortBreakText => _localization.GetString("ShortBreak");
    public string LongBreakText => _localization.GetString("LongBreak");
    public string StartWorkText => _localization.GetString("StartWork");
    public string PauseText => _localization.GetString("Pause");
    public string ResetText => _localization.GetString("Reset");
    public string SkipPhaseText => _localization.GetString("SkipPhase");
    public string SessionsCompletedText => _localization.GetString("SessionsCompleted");
    public string PomodoroConfigText => _localization.GetString("PomodoroConfig");
    public string WorkDurationText => _localization.GetString("WorkDuration");
    public string ShortBreakDurationText => _localization.GetString("ShortBreakDuration");
    public string LongBreakDurationText => _localization.GetString("LongBreakDuration");
    public string CyclesBeforeLongBreakText => _localization.GetString("CyclesBeforeLongBreak");
    public string AutoStartNextText => _localization.GetString("AutoStartNext");
    public string SaveText => _localization.GetString("Save");
    public string CancelText => _localization.GetString("Cancel");
    public string OnText => _localization.GetString("On");
    public string OffText => _localization.GetString("Off");
    public string MinutesText => _localization.GetString("Minutes");
    public string StatisticsText => _localization.GetString("Statistics");
    public string NoSessionsYetText => _localization.GetString("NoSessionsYet");
    public string ThisWeekText => _localization.GetString("ThisWeek");

    public string TodaySessionsText => string.Format(_localization.GetString("TodaySessions"), TodaySessions);
    public string TodayMinutesText => string.Format(_localization.GetString("TodayMinutes"), TodayMinutes);
    public string StreakText => string.Format(_localization.GetString("StreakDays"), CurrentStreak);
    public bool HasStreak => CurrentStreak > 1;

    public string PhaseText => CurrentPhase switch
    {
        PomodoroPhase.Work => WorkText,
        PomodoroPhase.ShortBreak => ShortBreakText,
        PomodoroPhase.LongBreak => LongBreakText,
        _ => ""
    };

    public string CycleText => string.Format(_localization.GetString("CycleFormat"), CurrentCycle, CyclesBeforeLongBreak);

    public string RemainingFormatted
    {
        get
        {
            var ts = RemainingTime;
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }
    }

    /// <summary>Phasenfarbe: Arbeit=Rot, Kurze Pause=Grün, Lange Pause=Blau.</summary>
    public string PhaseColor => CurrentPhase switch
    {
        PomodoroPhase.Work => "#EF4444",
        PomodoroPhase.ShortBreak => "#22C55E",
        PomodoroPhase.LongBreak => "#3B82F6",
        _ => "#EF4444"
    };

    /// <summary>Brush für den Fortschrittsring, passt sich der aktuellen Phase an.</summary>
    public IBrush PhaseBrush => new SolidColorBrush(Color.Parse(PhaseColor));

    public bool HasSessions => TodaySessions > 0 || WeekSessions > 0;

    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    public PomodoroViewModel(
        IDatabaseService database,
        IAudioService audioService,
        ILocalizationService localization,
        IPreferencesService preferences)
    {
        _database = database;
        _audioService = audioService;
        _localization = localization;
        _preferences = preferences;
        _localization.LanguageChanged += (_, _) => OnPropertyChanged(string.Empty);

        LoadConfig();
        ResetPhase();
        UpdateCycleDots();
        _ = LoadStatisticsAsync();
    }

    private void LoadConfig()
    {
        WorkMinutes = _preferences.Get("pomodoro_work", 25);
        ShortBreakMinutes = _preferences.Get("pomodoro_short_break", 5);
        LongBreakMinutes = _preferences.Get("pomodoro_long_break", 15);
        CyclesBeforeLongBreak = _preferences.Get("pomodoro_cycles", 4);
        AutoStartNext = _preferences.Get("pomodoro_auto_start", true);
    }

    private void SaveConfig()
    {
        _preferences.Set("pomodoro_work", WorkMinutes);
        _preferences.Set("pomodoro_short_break", ShortBreakMinutes);
        _preferences.Set("pomodoro_long_break", LongBreakMinutes);
        _preferences.Set("pomodoro_cycles", CyclesBeforeLongBreak);
        _preferences.Set("pomodoro_auto_start", AutoStartNext);
    }

    [RelayCommand]
    private void ToggleTimer()
    {
        if (IsRunning)
            PauseTimer();
        else
            StartTimer();
    }

    private void StartTimer()
    {
        IsRunning = true;
        _phaseStartedAt = DateTime.UtcNow;
        _timer?.Dispose();
        _timer = new Timer(1000);
        _timer.Elapsed += OnTimerTick;
        _timer.Start();
    }

    private void PauseTimer()
    {
        IsRunning = false;
        _timer?.Stop();
    }

    [RelayCommand]
    private void ResetPomodoro()
    {
        IsRunning = false;
        _timer?.Stop();
        CurrentPhase = PomodoroPhase.Work;
        CurrentCycle = 1;
        CompletedSessions = 0;
        ResetPhase();
        UpdateCycleDots();
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(CycleText));
        OnPropertyChanged(nameof(PhaseColor));
        OnPropertyChanged(nameof(PhaseBrush));
    }

    [RelayCommand]
    private async Task SkipPhase()
    {
        await CompletePhaseAsync();
    }

    [RelayCommand]
    private void ShowConfig() => IsConfigVisible = true;

    [RelayCommand]
    private void SaveConfigAndClose()
    {
        SaveConfig();
        IsConfigVisible = false;
        if (!IsRunning)
            ResetPhase();
    }

    [RelayCommand]
    private void CancelConfig()
    {
        LoadConfig();
        IsConfigVisible = false;
    }

    [RelayCommand]
    private void ToggleStatistics()
    {
        IsStatisticsView = !IsStatisticsView;
        if (IsStatisticsView)
            _ = LoadStatisticsAsync();
    }

    private void ResetPhase()
    {
        _totalPhaseTime = TimeSpan.FromMinutes(CurrentPhase switch
        {
            PomodoroPhase.Work => WorkMinutes,
            PomodoroPhase.ShortBreak => ShortBreakMinutes,
            PomodoroPhase.LongBreak => LongBreakMinutes,
            _ => WorkMinutes
        });
        RemainingTime = _totalPhaseTime;
        ProgressFraction = 1.0;
        OnPropertyChanged(nameof(RemainingFormatted));
    }

    private void OnTimerTick(object? sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsRunning) return;

            RemainingTime -= TimeSpan.FromSeconds(1);
            if (_totalPhaseTime.TotalSeconds > 0)
                ProgressFraction = RemainingTime.TotalSeconds / _totalPhaseTime.TotalSeconds;
            OnPropertyChanged(nameof(RemainingFormatted));

            if (RemainingTime <= TimeSpan.Zero)
            {
                _ = CompletePhaseAsync();
            }
        });
    }

    private async Task CompletePhaseAsync()
    {
        _timer?.Stop();
        IsRunning = false;

        // Sound bei Phasenwechsel abspielen
        _ = _audioService.PlayAsync("default");

        if (CurrentPhase == PomodoroPhase.Work)
        {
            // Abgeschlossene Work-Session in DB speichern
            CompletedSessions++;
            var session = new FocusSession
            {
                DurationMinutes = WorkMinutes,
                Type = "Work",
                CompletedAt = DateTime.UtcNow.ToString("O"),
                Date = DateTime.Today.ToString("O")
            };
            await _database.SaveFocusSessionAsync(session);
            await LoadStatisticsAsync();

            FloatingTextRequested?.Invoke(_localization.GetString("SessionsCompleted"), "success");
            CelebrationRequested?.Invoke();

            OnPropertyChanged(nameof(CycleText));

            // Nächste Phase bestimmen: Lange Pause nach X Zyklen, sonst kurze
            if (CurrentCycle >= CyclesBeforeLongBreak)
            {
                CurrentPhase = PomodoroPhase.LongBreak;
                CurrentCycle = 1;
            }
            else
            {
                CurrentPhase = PomodoroPhase.ShortBreak;
            }
        }
        else
        {
            // Pause vorbei → zurück zur Arbeit
            if (CurrentPhase == PomodoroPhase.ShortBreak)
                CurrentCycle++;
            CurrentPhase = PomodoroPhase.Work;
        }

        ResetPhase();
        UpdateCycleDots();
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(CycleText));
        OnPropertyChanged(nameof(PhaseColor));
        OnPropertyChanged(nameof(PhaseBrush));

        // Automatisch nächste Phase starten falls aktiviert
        if (AutoStartNext)
            StartTimer();
    }

    /// <summary>Lädt Statistiken für heute und die aktuelle Woche.</summary>
    private async Task LoadStatisticsAsync()
    {
        try
        {
            var today = DateTime.Today;

            // Heutige Sessions laden
            var todaySessions = await _database.GetFocusSessionsAsync(today, today.AddDays(1));
            TodaySessions = todaySessions.Count(s => s.Type == "Work");
            TodayMinutes = todaySessions.Where(s => s.Type == "Work").Sum(s => s.DurationMinutes);

            // Wochenstart berechnen (Montag als Wochenanfang)
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (today.DayOfWeek == DayOfWeek.Sunday)
                startOfWeek = startOfWeek.AddDays(-7);
            var endOfWeek = startOfWeek.AddDays(7);

            var weekSessions = await _database.GetFocusSessionsAsync(startOfWeek, endOfWeek);
            WeekSessions = weekSessions.Count(s => s.Type == "Work");

            // Tageweise aufteilen für Balkendiagramm
            var days = new ObservableCollection<DayStatistic>();
            for (int i = 0; i < 7; i++)
            {
                var day = startOfWeek.AddDays(i);
                var dayStr = day.ToString("O");
                var nextDayStr = day.AddDays(1).ToString("O");
                var count = weekSessions.Count(s => s.Type == "Work" &&
                    string.Compare(s.CompletedAt, dayStr, StringComparison.Ordinal) >= 0 &&
                    string.Compare(s.CompletedAt, nextDayStr, StringComparison.Ordinal) < 0);
                days.Add(new DayStatistic
                {
                    DayName = day.ToString("ddd"),
                    Sessions = count,
                    IsToday = day.Date == today.Date
                });
            }

            // Max für proportionale Höhe im Balkendiagramm berechnen
            var max = days.Max(d => d.Sessions);
            foreach (var d in days)
                d.HeightFraction = max > 0 ? (double)d.Sessions / max : 0;

            WeekDays = days;

            // Streak berechnen: Aufeinanderfolgende Tage mit Work-Sessions
            var streak = 0;
            var checkDay = today;
            // Wenn heute noch keine Session: ab gestern zählen
            if (TodaySessions == 0)
                checkDay = today.AddDays(-1);
            for (int i = 0; i < 365; i++)
            {
                var dayStart = checkDay.AddDays(-i);
                var dayEnd = dayStart.AddDays(1);
                var daySessions = await _database.GetFocusSessionsAsync(dayStart, dayEnd);
                if (daySessions.Any(s => s.Type == "Work"))
                    streak++;
                else
                    break;
            }
            CurrentStreak = streak;
            OnPropertyChanged(nameof(StreakText));
            OnPropertyChanged(nameof(HasStreak));

            OnPropertyChanged(nameof(HasSessions));
            OnPropertyChanged(nameof(TodaySessionsText));
            OnPropertyChanged(nameof(TodayMinutesText));
        }
        catch
        {
            // Statistik-Laden ist best-effort, Fehler nicht propagieren
        }
    }

    /// <summary>Erstellt die CycleDot-Anzeige basierend auf aktuellem Zyklus und Phase.</summary>
    private void UpdateCycleDots()
    {
        var dots = new ObservableCollection<CycleDot>();
        for (int i = 1; i <= CyclesBeforeLongBreak; i++)
        {
            var isCompleted = i < CurrentCycle;
            var isCurrent = i == CurrentCycle && CurrentPhase == PomodoroPhase.Work;
            dots.Add(new CycleDot
            {
                IsCompleted = isCompleted,
                IsCurrent = isCurrent,
                DotBrush = isCompleted
                    ? new SolidColorBrush(Color.Parse("#22C55E"))
                    : isCurrent
                        ? new SolidColorBrush(Color.Parse(PhaseColor))
                        : new SolidColorBrush(Color.Parse("#4B5563"))
            });
        }
        CycleDots = dots;
    }

    /// <summary>Lokalisierte Texte aktualisieren (wird von MainViewModel aufgerufen).</summary>
    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }
}

/// <summary>Tagesstatistik für das Balkendiagramm in der Wochenübersicht.</summary>
public class DayStatistic
{
    public string DayName { get; set; } = "";
    public int Sessions { get; set; }
    public bool IsToday { get; set; }
    public double HeightFraction { get; set; }
    public Avalonia.Media.FontWeight DayFontWeight => IsToday ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
}

/// <summary>Einzelner Punkt in der Zyklus-Anzeige des Pomodoro-Timers.</summary>
public class CycleDot
{
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
    public IBrush DotBrush { get; set; } = new SolidColorBrush(Color.Parse("#4B5563"));
}
