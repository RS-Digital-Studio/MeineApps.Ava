using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace FitnessRechner.ViewModels;

/// <summary>
/// ViewModel für den Intervallfasten-Timer.
/// Verwaltet Plan-Auswahl, Countdown und History.
/// </summary>
public sealed partial class FastingViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly IFastingService _fastingService;
    private readonly ILocalizationService _localization;
    private readonly IHapticService _hapticService;
    private readonly IFitnessSoundService _soundService;
    private DispatcherTimer? _countdownTimer;

    /// <summary>Navigation anfordern (z.B. ".." für zurück).</summary>
    public event Action<string>? NavigationRequested;

    /// <summary>Floating Text anzeigen (text, category).</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Confetti-Celebration auslösen.</summary>
    public event Action? CelebrationRequested;

    public FastingViewModel(
        IFastingService fastingService,
        ILocalizationService localization,
        IHapticService hapticService,
        IFitnessSoundService soundService)
    {
        _fastingService = fastingService;
        _localization = localization;
        _hapticService = hapticService;
        _soundService = soundService;

        // Events vom Service verdrahten
        _fastingService.FastingStarted += OnFastingStarted;
        _fastingService.FastingCompleted += OnFastingCompleted;

        // Initialen Zustand laden
        SelectedPlanIndex = (int)_fastingService.SelectedPlan;
        CustomFastingHours = _fastingService.FastingHours;
        UpdateDisplay();
        LoadHistory();

        // Timer starten wenn bereits aktiv
        if (_fastingService.IsActive)
            StartCountdownTimer();
    }

    #region Properties

    /// <summary>Index des gewählten Plans (0=16:8, 1=18:6, 2=20:4, 3=Custom).</summary>
    [ObservableProperty]
    private int _selectedPlanIndex;

    /// <summary>Custom-Fastenstunden (nur bei Custom-Plan editierbar).</summary>
    [ObservableProperty]
    private int _customFastingHours = 16;

    /// <summary>Ob gerade gefastet wird.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Countdown-Anzeige (HH:mm:ss).</summary>
    [ObservableProperty]
    private string _timeDisplay = "00:00:00";

    /// <summary>Fortschritt 0.0 bis 1.0 für den Ring.</summary>
    [ObservableProperty]
    private double _progressFraction;

    /// <summary>Anzeige der Fasten-Stunden.</summary>
    [ObservableProperty]
    private string _fastingHoursDisplay = "16h";

    /// <summary>Anzeige der Ess-Stunden.</summary>
    [ObservableProperty]
    private string _eatingHoursDisplay = "8h";

    /// <summary>Status-Text (z.B. "Fasten aktiv" oder "Bereit").</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Ob der Custom-Plan ausgewählt ist.</summary>
    public bool IsCustomPlan => SelectedPlanIndex == 3;

    /// <summary>Ob History-Einträge vorhanden sind.</summary>
    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>Ob keine History-Einträge vorhanden sind.</summary>
    public bool HasNoHistory => !HasHistory;

    /// <summary>Vergangene Zeit Anzeige.</summary>
    [ObservableProperty]
    private string _elapsedDisplay = "";

    /// <summary>Verbleibende Zeit Anzeige.</summary>
    [ObservableProperty]
    private string _remainingDisplay = "";

    /// <summary>History der Fasten-Perioden.</summary>
    public ObservableCollection<FastingRecordDisplay> History { get; } = [];

    #endregion

    #region Lokalisierte Labels

    public string TitleText => _localization.GetString("FastingTitle") ?? "Intermittent Fasting";
    public string StartButtonText => _localization.GetString("FastingStart") ?? "Start Fasting";
    public string StopButtonText => _localization.GetString("FastingStop") ?? "Stop";
    public string HistoryText => _localization.GetString("FastingHistory") ?? "History";
    public string FastingHoursLabel => _localization.GetString("FastingHours") ?? "Fasting";
    public string EatingHoursLabel => _localization.GetString("EatingHours") ?? "Eating";
    public string ElapsedLabel => _localization.GetString("FastingElapsed") ?? "Elapsed";
    public string RemainingLabel => _localization.GetString("FastingRemaining") ?? "Remaining";
    public string NoHistoryText => _localization.GetString("FastingNoHistory") ?? "No fasting sessions yet";
    public string Plan168Text => _localization.GetString("FastingPlan168") ?? "16:8";
    public string Plan186Text => _localization.GetString("FastingPlan186") ?? "18:6";
    public string Plan204Text => _localization.GetString("FastingPlan204") ?? "20:4";
    public string PlanCustomText => _localization.GetString("FastingPlanCustom") ?? "Custom";

    public void UpdateLocalizedTexts()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(StopButtonText));
        OnPropertyChanged(nameof(HistoryText));
        OnPropertyChanged(nameof(FastingHoursLabel));
        OnPropertyChanged(nameof(EatingHoursLabel));
        OnPropertyChanged(nameof(ElapsedLabel));
        OnPropertyChanged(nameof(RemainingLabel));
        OnPropertyChanged(nameof(NoHistoryText));
        OnPropertyChanged(nameof(Plan168Text));
        OnPropertyChanged(nameof(Plan186Text));
        OnPropertyChanged(nameof(Plan204Text));
        OnPropertyChanged(nameof(PlanCustomText));
        UpdateDisplay();
        LoadHistory();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void SelectPlan(string planIndex)
    {
        if (!int.TryParse(planIndex, out var index)) return;
        if (IsActive) return; // Plan nicht während aktivem Fasten wechseln

        SelectedPlanIndex = index;
        _hapticService.Tick();
    }

    [RelayCommand]
    private void StartFasting()
    {
        // Plan und Stunden übernehmen
        _fastingService.SelectedPlan = (FastingPlan)SelectedPlanIndex;
        _fastingService.FastingHours = IsCustomPlan ? CustomFastingHours : GetFastingHoursForPlan(SelectedPlanIndex);

        _fastingService.StartFasting();
        _hapticService.Click();
    }

    [RelayCommand]
    private void StopFasting()
    {
        _fastingService.StopFasting();
        StopCountdownTimer();
        UpdateDisplay();
        LoadHistory();
        _hapticService.Click();

        var text = _localization.GetString("FastingStop") ?? "Stopped";
        FloatingTextRequested?.Invoke(text, "info");
    }

    [RelayCommand]
    private void NavigateBack()
    {
        NavigationRequested?.Invoke("..");
    }

    #endregion

    #region Plan-Wechsel

    partial void OnSelectedPlanIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomPlan));

        if (!IsCustomPlan)
        {
            CustomFastingHours = GetFastingHoursForPlan(value);
        }
        UpdateDisplay();
    }

    partial void OnCustomFastingHoursChanged(int value)
    {
        // Clamping: 1-23 Stunden
        if (value < 1) { CustomFastingHours = 1; return; }
        if (value > 23) { CustomFastingHours = 23; return; }
        UpdateDisplay();
    }

    partial void OnHasHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoHistory));
    }

    #endregion

    #region Timer

    private void StartCountdownTimer()
    {
        StopCountdownTimer();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void StopCountdownTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer.Stop();
            _countdownTimer = null;
        }
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        // Explizit prüfen und ggf. abschließen (kein Seiteneffekt im Getter)
        _fastingService.CheckAndCompleteIfDone();

        if (!_fastingService.IsActive)
        {
            // Fasten-Periode ist abgelaufen (automatisch abgeschlossen)
            StopCountdownTimer();
            UpdateDisplay();
            LoadHistory();
            return;
        }

        UpdateTimerDisplay();
    }

    #endregion

    #region Display-Updates

    private void UpdateDisplay()
    {
        var hours = IsCustomPlan ? CustomFastingHours : GetFastingHoursForPlan(SelectedPlanIndex);
        var eatingH = 24 - hours;

        FastingHoursDisplay = $"{hours}h";
        EatingHoursDisplay = $"{eatingH}h";

        IsActive = _fastingService.IsActive;

        if (IsActive)
        {
            UpdateTimerDisplay();
            StatusText = _localization.GetString("FastingActive") ?? "Fasting active";
        }
        else
        {
            TimeDisplay = $"{hours:D2}:00:00";
            ProgressFraction = 0;
            ElapsedDisplay = "";
            RemainingDisplay = "";
            StatusText = "";
        }
    }

    private void UpdateTimerDisplay()
    {
        var remaining = _fastingService.RemainingTime;
        var elapsed = _fastingService.ElapsedTime;

        TimeDisplay = $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        ProgressFraction = _fastingService.Progress;
        ElapsedDisplay = FormatTimeSpan(elapsed);
        RemainingDisplay = FormatTimeSpan(remaining);
        IsActive = _fastingService.IsActive;
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }

    private void LoadHistory()
    {
        History.Clear();
        var records = _fastingService.GetHistory();
        foreach (var record in records)
        {
            History.Add(new FastingRecordDisplay(record, _localization));
        }
        HasHistory = History.Count > 0;
    }

    #endregion

    #region Event-Handler

    private void OnFastingStarted()
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateDisplay();
            StartCountdownTimer();

            var text = _localization.GetString("FastingStart") ?? "Fasting started";
            FloatingTextRequested?.Invoke(text, "success");
        });
    }

    private void OnFastingCompleted()
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopCountdownTimer();
            UpdateDisplay();
            LoadHistory();

            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            CelebrationRequested?.Invoke();

            var text = _localization.GetString("FastingCompletedCongrats") ?? "Fasting completed!";
            FloatingTextRequested?.Invoke(text, "success");
        });
    }

    #endregion

    #region Hilfsmethoden

    private static int GetFastingHoursForPlan(int index) => index switch
    {
        0 => 16,
        1 => 18,
        2 => 20,
        _ => 16
    };

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopCountdownTimer();
        _fastingService.FastingStarted -= OnFastingStarted;
        _fastingService.FastingCompleted -= OnFastingCompleted;
    }

    #endregion
}

/// <summary>
/// Display-Model für einen FastingRecord in der History-Liste.
/// Formatiert Datum, Dauer und Status für die Anzeige.
/// </summary>
public sealed class FastingRecordDisplay
{
    public FastingRecordDisplay(FastingRecord record, ILocalizationService localization)
    {
        Id = record.Id;
        PlanText = record.Plan switch
        {
            FastingPlan.Plan16_8 => "16:8",
            FastingPlan.Plan18_6 => "18:6",
            FastingPlan.Plan20_4 => "20:4",
            FastingPlan.Custom => $"{record.FastingHours}:{24 - record.FastingHours}",
            _ => "16:8"
        };

        // Datum in Lokalzeit anzeigen
        DateText = record.StartTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        // Dauer berechnen
        if (record.EndTime.HasValue)
        {
            var duration = record.EndTime.Value - record.StartTime;
            DurationText = duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}h {duration.Minutes:D2}m"
                : $"{duration.Minutes}m";
        }
        else
        {
            DurationText = "-";
        }

        IsCompleted = record.IsCompleted;
        StatusText = record.IsCompleted
            ? (localization.GetString("FastingCompleted") ?? "Completed")
            : (localization.GetString("FastingStop") ?? "Stopped");
    }

    public string Id { get; }
    public string PlanText { get; }
    public string DateText { get; }
    public string DurationText { get; }
    public bool IsCompleted { get; }
    public string StatusText { get; }
}
