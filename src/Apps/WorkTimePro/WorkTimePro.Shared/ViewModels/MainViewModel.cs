using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using Avalonia.Media;
using WorkTimePro.Resources.Strings;
using WorkTimePro.Services;

namespace WorkTimePro.ViewModels;

/// <summary>
/// ViewModel for the main page (Today view)
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ITimeTrackingService _timeTracking;
    private readonly ICalculationService _calculation;
    private readonly IDatabaseService _database;
    private readonly ILocalizationService _localization;
    private readonly ITrialService _trialService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;

    private System.Timers.Timer? _updateTimer;
    private bool _disposed;

    // === Sub-Page Navigation ===

    [ObservableProperty]
    private bool _isDayDetailActive;

    [ObservableProperty]
    private bool _isMonthActive;

    [ObservableProperty]
    private bool _isYearActive;

    [ObservableProperty]
    private bool _isVacationActive;

    [ObservableProperty]
    private bool _isShiftPlanActive;

    public bool IsSubPageActive => IsDayDetailActive || IsMonthActive || IsYearActive || IsVacationActive || IsShiftPlanActive;

    // === Child ViewModels (for tab pages and sub-pages) ===
    public WeekOverviewViewModel WeekVm { get; }
    public CalendarViewModel CalendarVm { get; }
    public StatisticsViewModel StatisticsVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public DayDetailViewModel DayDetailVm { get; }
    public MonthOverviewViewModel MonthVm { get; }
    public YearOverviewViewModel YearVm { get; }
    public VacationViewModel VacationVm { get; }
    public ShiftPlanViewModel ShiftPlanVm { get; }

    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    public MainViewModel(
        ITimeTrackingService timeTracking,
        ICalculationService calculation,
        IDatabaseService database,
        ILocalizationService localization,
        ITrialService trialService,
        IPurchaseService purchaseService,
        IAdService adService,
        WeekOverviewViewModel weekVm,
        CalendarViewModel calendarVm,
        StatisticsViewModel statisticsVm,
        SettingsViewModel settingsVm,
        DayDetailViewModel dayDetailVm,
        MonthOverviewViewModel monthVm,
        YearOverviewViewModel yearVm,
        VacationViewModel vacationVm,
        ShiftPlanViewModel shiftPlanVm)
    {
        _timeTracking = timeTracking;
        _calculation = calculation;
        _database = database;
        _localization = localization;
        _trialService = trialService;
        _purchaseService = purchaseService;
        _adService = adService;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        // Child VMs
        WeekVm = weekVm;
        CalendarVm = calendarVm;
        StatisticsVm = statisticsVm;
        SettingsVm = settingsVm;
        DayDetailVm = dayDetailVm;
        MonthVm = monthVm;
        YearVm = yearVm;
        VacationVm = vacationVm;
        ShiftPlanVm = shiftPlanVm;

        // Wire up GoBack from sub-page VMs
        WireSubPageNavigation(dayDetailVm);
        WireSubPageNavigation(monthVm);
        WireSubPageNavigation(yearVm);
        WireSubPageNavigation(vacationVm);
        WireSubPageNavigation(shiftPlanVm);

        // Event handler
        _timeTracking.StatusChanged += OnStatusChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        // Timer for live updates (1 second) - only started when tracking is active
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += async (s, e) => await UpdateLiveDataAsync();
        // Timer is NOT started immediately - starts on status change
    }

    private void WireSubPageNavigation(ObservableObject vm)
    {
        // Sub-page VMs fire NavigationRequested(".." or back) - we close overlays
        var navEvent = vm.GetType().GetEvent("NavigationRequested");
        if (navEvent != null)
        {
            navEvent.AddEventHandler(vm, new Action<string>(route =>
            {
                if (route == ".." || route.Contains("back", StringComparison.OrdinalIgnoreCase))
                    GoBack();
            }));
        }
    }

    // === Tab Navigation ===

    [ObservableProperty]
    private int _currentTab;

    public bool IsTodayActive => CurrentTab == 0;
    public bool IsWeekActive => CurrentTab == 1;
    public bool IsCalendarActive => CurrentTab == 2;
    public bool IsStatisticsActive => CurrentTab == 3;
    public bool IsSettingsActive => CurrentTab == 4;

    partial void OnCurrentTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTodayActive));
        OnPropertyChanged(nameof(IsWeekActive));
        OnPropertyChanged(nameof(IsCalendarActive));
        OnPropertyChanged(nameof(IsStatisticsActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    [RelayCommand]
    private void SelectTodayTab() => CurrentTab = 0;

    [RelayCommand]
    private void SelectWeekTab() => CurrentTab = 1;

    [RelayCommand]
    private void SelectCalendarTab() => CurrentTab = 2;

    [RelayCommand]
    private void SelectStatisticsTab() => CurrentTab = 3;

    [RelayCommand]
    private void SelectSettingsTab() => CurrentTab = 4;

    // === Observable Properties ===

    [ObservableProperty]
    private TrackingStatus _currentStatus = TrackingStatus.Idle;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _statusIcon = Icons.Play;

    [ObservableProperty]
    private IBrush _statusColor = SolidColorBrush.Parse("#9E9E9E");

    [ObservableProperty]
    private string _currentWorkTime = "0:00";

    [ObservableProperty]
    private string _currentPauseTime = "0:00";

    [ObservableProperty]
    private string _targetWorkTime = "8:00";

    [ObservableProperty]
    private string _balanceTime = "+0:00";

    [ObservableProperty]
    private IBrush _balanceColor = SolidColorBrush.Parse("#4CAF50");

    [ObservableProperty]
    private string _timeUntilEnd = "--:--";

    [ObservableProperty]
    private double _dayProgress;

    [ObservableProperty]
    private double _weekProgress;

    [ObservableProperty]
    private string _weekProgressText = "0%";

    [ObservableProperty]
    private string _todayDateDisplay = DateTime.Today.ToString("dddd, dd. MMMM");

    [ObservableProperty]
    private ObservableCollection<TimeEntry> _todayEntries = new();

    [ObservableProperty]
    private ObservableCollection<PauseEntry> _todayPauses = new();

    [ObservableProperty]
    private string _firstCheckIn = "--:--";

    [ObservableProperty]
    private string _lastCheckOut = "--:--";

    [ObservableProperty]
    private bool _hasAutoPause;

    [ObservableProperty]
    private string _autoPauseInfo = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds = true;

    // Derived properties
    public bool IsWorking => CurrentStatus == TrackingStatus.Working || CurrentStatus == TrackingStatus.OnBreak;
    public bool HasCheckedIn => CurrentStatus != TrackingStatus.Idle;

    public MaterialIconKind StatusIconKind => CurrentStatus switch
    {
        TrackingStatus.Idle => MaterialIconKind.Play,
        TrackingStatus.Working => MaterialIconKind.Stop,
        TrackingStatus.OnBreak => MaterialIconKind.Play,
        _ => MaterialIconKind.Play
    };

    // Localized Button Texts
    public string PauseButtonText => CurrentStatus == TrackingStatus.OnBreak
        ? $"{Icons.Coffee} {AppStrings.EndPause}"
        : $"{Icons.Coffee} {AppStrings.Break}";

    public string ShowDayDetailsText => $"{Icons.FileDocument} {AppStrings.ShowDayDetails}";

    // === Commands ===

    [RelayCommand]
    private async Task ToggleTrackingAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            switch (CurrentStatus)
            {
                case TrackingStatus.Idle:
                    await _timeTracking.CheckInAsync();
                    break;

                case TrackingStatus.Working:
                    await _timeTracking.CheckOutAsync();

                    // Feierabend-Celebration
                    FloatingTextRequested?.Invoke(AppStrings.EndOfDay, "success");
                    CelebrationRequested?.Invoke();

                    // Ueberstunden anzeigen falls vorhanden
                    var workTime = await _timeTracking.GetCurrentWorkTimeAsync();
                    var today = await _timeTracking.GetTodayAsync();
                    var overtime = workTime - today.TargetWorkTime;
                    if (overtime.TotalMinutes > 1)
                        FloatingTextRequested?.Invoke($"+{overtime.TotalHours:F1}h", "overtime");

                    break;

                case TrackingStatus.OnBreak:
                    await _timeTracking.EndPauseAsync();
                    break;
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        if (IsLoading || CurrentStatus == TrackingStatus.Idle) return;

        try
        {
            IsLoading = true;

            if (CurrentStatus == TrackingStatus.Working)
            {
                await _timeTracking.StartPauseAsync();
            }
            else if (CurrentStatus == TrackingStatus.OnBreak)
            {
                await _timeTracking.EndPauseAsync();
            }

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;

            // Load status
            await _timeTracking.LoadStatusAsync();

            // Today
            var today = await _timeTracking.GetTodayAsync();

            // Load entries
            var entries = await _database.GetTimeEntriesAsync(today.Id);
            TodayEntries = new ObservableCollection<TimeEntry>(entries);

            var pauses = await _database.GetPauseEntriesAsync(today.Id);
            TodayPauses = new ObservableCollection<PauseEntry>(pauses);

            // Times
            TargetWorkTime = FormatTimeSpan(today.TargetWorkTime);
            FirstCheckIn = today.FirstCheckIn?.ToString("HH:mm") ?? "--:--";
            LastCheckOut = today.LastCheckOut?.ToString("HH:mm") ?? "--:--";

            // Auto-Pause Info
            HasAutoPause = today.HasAutoPause;
            if (HasAutoPause)
            {
                AutoPauseInfo = $"+{today.AutoPauseMinutes} min ({AppStrings.LegalSourceArbZG})";
            }

            // Week progress
            WeekProgress = await _calculation.GetWeekProgressAsync();
            WeekProgressText = $"{WeekProgress:F0}%";

            // Premium status
            ShowAds = !_purchaseService.IsPremium && !_trialService.IsTrialActive;

            // Start timer if active
            if (CurrentStatus != TrackingStatus.Idle)
            {
                _updateTimer?.Start();
            }

            await UpdateLiveDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void NavigateToDayDetail()
    {
        CloseAllSubPages();
        IsDayDetailActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    private void NavigateToMonth()
    {
        CloseAllSubPages();
        IsMonthActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    private void NavigateToYear()
    {
        CloseAllSubPages();
        IsYearActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    private void NavigateToVacation()
    {
        CloseAllSubPages();
        IsVacationActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    private void NavigateToShiftPlan()
    {
        CloseAllSubPages();
        IsShiftPlanActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    public void GoBack()
    {
        CloseAllSubPages();
    }

    private void CloseAllSubPages()
    {
        IsDayDetailActive = false;
        IsMonthActive = false;
        IsYearActive = false;
        IsVacationActive = false;
        IsShiftPlanActive = false;
        OnPropertyChanged(nameof(IsSubPageActive));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    // === Helper methods ===

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateStatusDisplay();
            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(ShowDayDetailsText));
            TodayDateDisplay = DateTime.Today.ToString("dddd, dd. MMMM");
        });
    }

    private void OnStatusChanged(object? sender, TrackingStatus status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentStatus = status;
            UpdateStatusDisplay();
            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(IsWorking));
            OnPropertyChanged(nameof(HasCheckedIn));
            OnPropertyChanged(nameof(StatusIconKind));

            // Timer only when tracking is active
            if (status == TrackingStatus.Idle)
            {
                _updateTimer?.Stop();
            }
            else
            {
                _updateTimer?.Start();
            }
        });
    }

    private void UpdateStatusDisplay()
    {
        switch (CurrentStatus)
        {
            case TrackingStatus.Idle:
                StatusText = _localization.GetString("Status_Idle") ?? AppStrings.Status_Idle;
                StatusIcon = Icons.Play;
                StatusColor = SolidColorBrush.Parse("#9E9E9E");
                break;

            case TrackingStatus.Working:
                StatusText = _localization.GetString("Status_Working") ?? AppStrings.Status_Working;
                StatusIcon = Icons.Stop;
                StatusColor = SolidColorBrush.Parse("#4CAF50");
                break;

            case TrackingStatus.OnBreak:
                StatusText = _localization.GetString("Status_OnBreak") ?? AppStrings.Status_OnBreak;
                StatusIcon = Icons.Play;
                StatusColor = SolidColorBrush.Parse("#FF9800");
                break;
        }
    }

    private async Task UpdateLiveDataAsync()
    {
        if (_disposed) return;

        try
        {
            var workTime = await _timeTracking.GetCurrentWorkTimeAsync();
            var pauseTime = await _timeTracking.GetCurrentPauseTimeAsync();
            var timeUntilEnd = await _timeTracking.GetTimeUntilEndAsync();

            var today = await _timeTracking.GetTodayAsync();
            var balance = workTime - today.TargetWorkTime;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentWorkTime = FormatTimeSpan(workTime);
                CurrentPauseTime = FormatTimeSpan(pauseTime);
                TimeUntilEnd = timeUntilEnd.HasValue ? FormatTimeSpan(timeUntilEnd.Value) : "--:--";

                var prefix = balance.TotalMinutes >= 0 ? "+" : "";
                BalanceTime = $"{prefix}{FormatTimeSpan(balance)}";
                BalanceColor = SolidColorBrush.Parse(balance.TotalMinutes >= 0 ? "#4CAF50" : "#F44336");

                // Day progress
                if (today.TargetWorkMinutes > 0)
                {
                    DayProgress = Math.Min(100, (workTime.TotalMinutes * 100) / today.TargetWorkMinutes);
                }
            });
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        var totalHours = (int)Math.Abs(ts.TotalHours);
        var minutes = Math.Abs(ts.Minutes);
        var sign = ts.TotalMinutes < 0 ? "-" : "";
        return $"{sign}{totalHours}:{minutes:D2}";
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _timeTracking.StatusChanged -= OnStatusChanged;
        _localization.LanguageChanged -= OnLanguageChanged;

        GC.SuppressFinalize(this);
    }
}
