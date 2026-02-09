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

    // Delete confirmation state
    private TimerItem? _timerToDelete;

    [ObservableProperty]
    private bool _isDeleteConfirmVisible;

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
    public string MinutesText => _localization.GetString("Minutes");
    public string ConfirmDeleteTitleText => _localization.GetString("ConfirmDeleteTitle");
    public string ConfirmDeleteMessageText => _localization.GetString("ConfirmDeleteMessage");
    public string YesText => _localization.GetString("Yes");
    public string NoText => _localization.GetString("No");

    public bool HasTimers => Timers.Count > 0;

    public TimerViewModel(ITimerService timerService, ILocalizationService localization)
    {
        _timerService = timerService;
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;

        _timerService.TimersChanged += (_, _) => LoadTimers();
        _timerService.TimerTick += (_, timer) => timer.RemainingTimeTicks = _timerService.GetRemainingTime(timer).Ticks;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _timerService.LoadTimersAsync();
        LoadTimers();
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
    private void HideCreateTimer() { IsCreatingTimer = false; NewTimerName = ""; NewTimerHours = 0; NewTimerMinutes = 5; NewTimerSeconds = 0; }

    [RelayCommand]
    private async Task CreateTimer()
    {
        var duration = new TimeSpan(NewTimerHours, NewTimerMinutes, NewTimerSeconds);
        if (duration <= TimeSpan.Zero) return;

        await _timerService.CreateTimerAsync(NewTimerName, duration);
        HideCreateTimer();
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(string.Empty);
    }
}
