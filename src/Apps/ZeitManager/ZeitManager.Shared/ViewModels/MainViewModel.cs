using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localization;
    private readonly ITimerService _timerService;
    private readonly IAlarmSchedulerService _alarmScheduler;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimerActive))]
    [NotifyPropertyChangedFor(nameof(IsStopwatchActive))]
    [NotifyPropertyChangedFor(nameof(IsAlarmActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private int _selectedTabIndex;

    [ObservableProperty]
    private TimerViewModel _timerViewModel;

    [ObservableProperty]
    private StopwatchViewModel _stopwatchViewModel;

    [ObservableProperty]
    private AlarmViewModel _alarmViewModel;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private AlarmOverlayViewModel _alarmOverlayViewModel;

    [ObservableProperty]
    private bool _isAlarmOverlayVisible;

    [ObservableProperty]
    private string? _snackbarMessage;

    [ObservableProperty]
    private bool _isSnackbarVisible;

    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // Localized tab labels
    public string NavTimerText => _localization.GetString("Timer");
    public string NavStopwatchText => _localization.GetString("Stopwatch");
    public string NavAlarmText => _localization.GetString("Alarm");
    public string NavSettingsText => _localization.GetString("Settings");

    // Active tab indicators
    public bool IsTimerActive => SelectedTabIndex == 0;
    public bool IsStopwatchActive => SelectedTabIndex == 1;
    public bool IsAlarmActive => SelectedTabIndex == 2;
    public bool IsSettingsActive => SelectedTabIndex == 3;

    public MainViewModel(
        IThemeService themeService,
        ILocalizationService localization,
        ITimerService timerService,
        IAlarmSchedulerService alarmScheduler,
        AlarmOverlayViewModel alarmOverlayViewModel,
        TimerViewModel timerViewModel,
        StopwatchViewModel stopwatchViewModel,
        AlarmViewModel alarmViewModel,
        SettingsViewModel settingsViewModel)
    {
        _themeService = themeService;
        _localization = localization;
        _timerService = timerService;
        _alarmScheduler = alarmScheduler;
        _timerViewModel = timerViewModel;
        _stopwatchViewModel = stopwatchViewModel;
        _alarmViewModel = alarmViewModel;
        _settingsViewModel = settingsViewModel;
        _alarmOverlayViewModel = alarmOverlayViewModel;

        _localization.LanguageChanged += OnLanguageChanged;

        // Wire up timer finished event to show overlay
        _timerService.TimerFinished += OnTimerFinished;

        // Wire up alarm triggered event to show overlay
        _alarmScheduler.AlarmTriggered += OnAlarmTriggered;

        // Wire up overlay dismiss
        _alarmOverlayViewModel.DismissRequested += (_, _) => IsAlarmOverlayVisible = false;

        // Floating Text Events von Kind-ViewModels weiterleiten
        _stopwatchViewModel.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);

        // Wire up MessageRequested from child ViewModels
        _settingsViewModel.MessageRequested += OnChildMessageRequested;
    }

    private void OnTimerFinished(object? sender, TimerItem timer)
    {
        FloatingTextRequested?.Invoke(_localization.GetString("TimerDone"), "success");
        CelebrationRequested?.Invoke();
        AlarmOverlayViewModel.ShowForTimer(timer);
        IsAlarmOverlayVisible = true;
    }

    private void OnAlarmTriggered(object? sender, AlarmItem alarm)
    {
        AlarmOverlayViewModel.ShowForAlarm(alarm);
        IsAlarmOverlayVisible = true;
    }

    private void OnChildMessageRequested(object? sender, string message)
    {
        ShowSnackbar(message);
    }

    private CancellationTokenSource? _snackbarCts;

    private async void ShowSnackbar(string message)
    {
        _snackbarCts?.Cancel();
        _snackbarCts = new CancellationTokenSource();
        var token = _snackbarCts.Token;

        SnackbarMessage = message;
        IsSnackbarVisible = true;

        try
        {
            await Task.Delay(3000, token);
            IsSnackbarVisible = false;
        }
        catch (OperationCanceledException) { }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NavTimerText));
        OnPropertyChanged(nameof(NavStopwatchText));
        OnPropertyChanged(nameof(NavAlarmText));
        OnPropertyChanged(nameof(NavSettingsText));
    }

    [RelayCommand]
    private void NavigateToTimer() => SelectedTabIndex = 0;

    [RelayCommand]
    private void NavigateToStopwatch() => SelectedTabIndex = 1;

    [RelayCommand]
    private void NavigateToAlarm() => SelectedTabIndex = 2;

    [RelayCommand]
    private void NavigateToSettings() => SelectedTabIndex = 3;
}
