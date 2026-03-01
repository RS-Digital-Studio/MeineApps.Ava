using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Audio;
using ZeitManager.Models;

namespace ZeitManager.ViewModels;

public partial class StopwatchViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localization;
    private readonly Stopwatch _stopwatch = new();
    private System.Timers.Timer? _uiTimer;
    private TimeSpan _offset = TimeSpan.Zero;

    [ObservableProperty]
    private string _elapsedTimeFormatted = "00:00.00";

    /// <summary>Gesamte verstrichene Zeit in Sekunden (fuer SkiaSharp-Rendering, vermeidet String-Parsing).</summary>
    [ObservableProperty]
    private double _totalElapsedSeconds;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private ObservableCollection<StopwatchLap> _laps = [];

    private TimeSpan _lastLapTime = TimeSpan.Zero;

    // Undo state
    private TimeSpan _undoElapsedTime;
    private List<StopwatchLap>? _undoLaps;
    private TimeSpan _undoLastLapTime;
    private TimeSpan _undoOffset;

    [ObservableProperty]
    private bool _canUndo;

    // Localized strings
    public string TitleText => _localization.GetString("StopwatchTitle");
    public string LapText => _localization.GetString("Lap");
    public string LapTimesText => _localization.GetString("LapTimes");
    public string NoLapsText => _localization.GetString("NoLaps");
    public string StartText => _localization.GetString("Start");
    public string StopText => _localization.GetString("Stop");
    public string ResetText => _localization.GetString("Reset");
    public string UndoResetText => _localization.GetString("UndoReset");

    public string StopwatchEmptyHintText => _localization.GetString("StopwatchEmptyHint");

    public event Action<string, string>? FloatingTextRequested;

    public bool HasLaps => Laps.Count > 0;

    public StopwatchViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    [RelayCommand]
    private void StartStop()
    {
        if (IsRunning) Stop(); else Start();
    }

    private TimeSpan TotalElapsed => _stopwatch.Elapsed + _offset;

    [RelayCommand]
    private void Start()
    {
        _stopwatch.Start();
        IsRunning = true;
        CanUndo = false;
        EnsureUiTimer();
    }

    [RelayCommand]
    private void Stop()
    {
        _stopwatch.Stop();
        IsRunning = false;
        CheckStopUiTimer();
        UpdateDisplay();
    }

    [RelayCommand]
    private void Reset()
    {
        // Save undo state
        _undoElapsedTime = TotalElapsed;
        _undoLaps = [.. Laps];
        _undoLastLapTime = _lastLapTime;
        _undoOffset = _offset;
        CanUndo = true;

        _stopwatch.Reset();
        _offset = TimeSpan.Zero;
        IsRunning = false;
        Laps.Clear();
        _lastLapTime = TimeSpan.Zero;
        ElapsedTimeFormatted = "00:00.00";
        TotalElapsedSeconds = 0;
        OnPropertyChanged(nameof(HasLaps));
        CheckStopUiTimer();
    }

    [RelayCommand]
    private void Undo()
    {
        if (!CanUndo || _undoLaps == null) return;

        _stopwatch.Reset();
        _offset = _undoElapsedTime;
        Laps = new ObservableCollection<StopwatchLap>(_undoLaps);
        _lastLapTime = _undoLastLapTime;
        ElapsedTimeFormatted = TimeFormatHelper.Format(_undoElapsedTime);
        TotalElapsedSeconds = _undoElapsedTime.TotalSeconds;
        CanUndo = false;
        OnPropertyChanged(nameof(HasLaps));
    }

    [RelayCommand]
    private void Lap()
    {
        if (!IsRunning) return;

        var totalTime = TotalElapsed;
        var lapTime = totalTime - _lastLapTime;
        _lastLapTime = totalTime;

        // Delta zur vorherigen Runde berechnen
        TimeSpan? delta = null;
        if (Laps.Count > 0)
        {
            // Laps[0] ist die zuletzt hinzugefügte Runde (neueste zuerst)
            delta = lapTime - Laps[0].LapTime;
        }

        var lap = new StopwatchLap(Laps.Count + 1, lapTime, totalTime, DateTime.Now)
        {
            DeltaToPrevious = delta
        };
        Laps.Insert(0, lap);
        RecalculateLapMarkers();
        OnPropertyChanged(nameof(HasLaps));
        FloatingTextRequested?.Invoke($"#{Laps.Count}", "info");
    }

    /// <summary>Markiert die schnellste und langsamste Runde (bei 2+ Runden).</summary>
    private void RecalculateLapMarkers()
    {
        if (Laps.Count < 2) return;

        var best = Laps.MinBy(l => l.LapTime);
        var worst = Laps.MaxBy(l => l.LapTime);

        foreach (var lap in Laps)
        {
            lap.IsBestLap = false;
            lap.IsWorstLap = false;
        }

        if (best != null) best.IsBestLap = true;
        if (worst != null && worst != best) worst.IsWorstLap = true;

        // Collection neu zuweisen für UI-Update
        Laps = new ObservableCollection<StopwatchLap>(Laps);
        OnPropertyChanged(nameof(HasLaps));
    }

    private void EnsureUiTimer()
    {
        if (_uiTimer != null) return;
        _uiTimer = new System.Timers.Timer(50); // 50ms for centisecond precision
        _uiTimer.Elapsed += (_, _) => UpdateDisplay();
        _uiTimer.Start();
    }

    private void CheckStopUiTimer()
    {
        if (!IsRunning && _uiTimer != null)
        {
            _uiTimer.Stop();
            _uiTimer.Dispose();
            _uiTimer = null;
        }
    }

    private void UpdateDisplay()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var elapsed = TotalElapsed;
            ElapsedTimeFormatted = TimeFormatHelper.Format(elapsed);
            TotalElapsedSeconds = elapsed.TotalSeconds;
        });
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localization.LanguageChanged -= OnLanguageChanged;
        _uiTimer?.Stop();
        _uiTimer?.Dispose();
        _uiTimer = null;

        GC.SuppressFinalize(this);
    }
}
