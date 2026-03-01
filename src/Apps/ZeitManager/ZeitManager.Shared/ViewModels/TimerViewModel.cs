using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class TimerViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
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

    // Initialisierung abwarten bevor Timer erstellt werden (Race Condition verhindern)
    private Task _initTask = null!;

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

    /// <summary>Ob die grosse Timer-Visualisierung angezeigt werden soll (genau 1 laufender Timer).</summary>
    public bool ShowVisualization => _activeRunningTimer != null;

    /// <summary>Fortschritt des aktiven laufenden Timers als Fraktion (0.0-1.0).</summary>
    public float VisualizationProgress => (float)(_activeRunningTimer?.ProgressFraction ?? 0);

    /// <summary>Ob der visualisierte Timer gerade laeuft.</summary>
    public bool VisualizationIsRunning => _activeRunningTimer?.IsRunning ?? false;

    /// <summary>Ob der visualisierte Timer abgelaufen ist.</summary>
    public bool VisualizationIsFinished => _activeRunningTimer?.IsFinished ?? false;

    /// <summary>Formatierte Restzeit des visualisierten Timers.</summary>
    public string VisualizationRemainingFormatted => _activeRunningTimer?.RemainingTimeFormatted ?? "";

    /// <summary>Name des visualisierten Timers.</summary>
    public string? VisualizationTimerName => _activeRunningTimer?.Name;

    /// <summary>Verbleibende Sekunden des visualisierten Timers (fuer Countdown-Anzeige).</summary>
    public double VisualizationRemainingSeconds => _activeRunningTimer?.RemainingTime.TotalSeconds ?? -1;

    private TimerItem? _activeRunningTimer;

    public TimerViewModel(ITimerService timerService, ILocalizationService localization, IDatabaseService database)
    {
        _timerService = timerService;
        _localization = localization;
        _database = database;
        _localization.LanguageChanged += OnLanguageChanged;
        _timerService.TimersChanged += OnTimersChanged;
        _timerService.TimerTick += OnTimerTick;

        _initTask = InitializeAsync();
    }

    /// <summary>
    /// Wartet auf Abschluss der Initialisierung (Timer + Presets aus DB).
    /// </summary>
    public Task WaitForInitializationAsync() => _initTask;

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
        UpdateActiveRunningTimer();
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
        await _initTask; // Warten bis LoadTimersAsync fertig ist
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
        await _initTask; // Warten bis LoadTimersAsync fertig ist
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
        await _initTask; // Warten bis LoadTimersAsync fertig ist
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

    /// <summary>Bestimmt den aktiven laufenden Timer fuer die Visualisierung (erster laufender Timer).</summary>
    private void UpdateActiveRunningTimer()
    {
        var running = Timers.FirstOrDefault(t => t.State == TimerState.Running);
        var changed = running != _activeRunningTimer;
        _activeRunningTimer = running;

        if (changed)
            OnPropertyChanged(nameof(ShowVisualization));

        // Visualisierungs-Properties aktualisieren wenn ein Timer laeuft
        if (_activeRunningTimer != null)
        {
            OnPropertyChanged(nameof(VisualizationProgress));
            OnPropertyChanged(nameof(VisualizationIsRunning));
            OnPropertyChanged(nameof(VisualizationIsFinished));
            OnPropertyChanged(nameof(VisualizationRemainingFormatted));
            OnPropertyChanged(nameof(VisualizationTimerName));
            OnPropertyChanged(nameof(VisualizationRemainingSeconds));
        }
    }

    private void OnTimersChanged(object? sender, EventArgs e) => LoadTimers();

    private void OnTimerTick(object? sender, TimerItem timer)
    {
        timer.RemainingTimeTicks = _timerService.GetRemainingTime(timer).Ticks;
        UpdateActiveRunningTimer();
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
        _timerService.TimersChanged -= OnTimersChanged;
        _timerService.TimerTick -= OnTimerTick;

        GC.SuppressFinalize(this);
    }
}
