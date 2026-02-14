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
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IHapticService _haptic;

    private System.Timers.Timer? _updateTimer;
    private bool _disposed;
    private Task? _initTask;

    // Undo-Mechanismus (5 Sekunden Fenster nach CheckIn/CheckOut)
    private CancellationTokenSource? _undoCts;
    private TimeEntry? _lastUndoEntry;
    private TrackingStatus _statusBeforeUndo;

    // Event-Handler-Referenzen für sauberes Dispose
    private readonly List<(ObservableObject Vm, string EventName, Delegate Handler)> _wiredEvents = new();

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
        ShiftPlanViewModel shiftPlanVm,
        IRewardedAdService rewardedAdService,
        IHapticService haptic)
    {
        _timeTracking = timeTracking;
        _calculation = calculation;
        _database = database;
        _localization = localization;
        _trialService = trialService;
        _purchaseService = purchaseService;
        _adService = adService;
        _rewardedAdService = rewardedAdService;
        _haptic = haptic;
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += OnAdsStateChanged;

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

        // Navigation-Events verdrahten (Sub-Pages + Tab-VMs die navigieren können)
        WireSubPageNavigation(dayDetailVm);
        WireSubPageNavigation(monthVm);
        WireSubPageNavigation(yearVm);
        WireSubPageNavigation(vacationVm);
        WireSubPageNavigation(shiftPlanVm);
        // Tab-VMs die DayDetail-Navigation auslösen können
        WireSubPageNavigation(weekVm);
        WireSubPageNavigation(calendarVm);

        // Tab-VMs ohne Navigation aber mit MessageRequested
        WireSubPageNavigation(statisticsVm);
        WireSubPageNavigation(settingsVm);

        // Settings-Änderungen propagieren
        SettingsVm.SettingsChanged += OnSettingsChanged;

        // Event handler
        _timeTracking.StatusChanged += OnStatusChanged;
        _localization.LanguageChanged += OnLanguageChanged;

        // Timer for live updates (1 second) - only started when tracking is active
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += OnUpdateTimerElapsed;
        // Timer is NOT started immediately - starts on status change

        // Initiale Daten laden (Status aus DB, Today-Ansicht)
        _initTask = InitializeAsync();
    }

    /// <summary>
    /// Initialisierung mit Fehlerbehandlung (statt Fire-and-Forget).
    /// Stellt sicher, dass die Datenbank bereit ist, bevor Daten geladen werden.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // DB explizit initialisieren (Tabellen + Indizes erstellen)
            await _database.InitializeAsync();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Init-Fehler: {ex}");
            // Fehler dem User anzeigen statt verschlucken
            MessageRequested?.Invoke(
                AppStrings.Error,
                string.Format(AppStrings.ErrorLoading, ex.Message));
        }
    }

    /// <summary>
    /// Stellt sicher, dass initiale Daten geladen sind bevor Commands ausgeführt werden
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initTask != null) await _initTask;
    }

    /// <summary>
    /// Settings-Änderungen propagieren: aktiven Tab neu laden
    /// </summary>
    private async void OnSettingsChanged(object? sender, EventArgs e)
    {
        try { await LoadTabDataAsync(CurrentTab); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Settings-Reload Fehler: {ex.Message}"); }
    }

    private void WireSubPageNavigation(ObservableObject vm)
    {
        // Sub-page VMs navigieren per Route-Strings
        var navEvent = vm.GetType().GetEvent("NavigationRequested");
        if (navEvent != null)
        {
            var handler = new Action<string>(route => HandleNavigation(route));
            navEvent.AddEventHandler(vm, handler);
            _wiredEvents.Add((vm, "NavigationRequested", handler));
        }

        // MessageRequested weiterleiten (Fehlermeldungen der Sub-VMs dem User anzeigen)
        var msgEvent = vm.GetType().GetEvent("MessageRequested");
        if (msgEvent != null)
        {
            var handler = new Action<string, string>((title, msg) => MessageRequested?.Invoke(title, msg));
            msgEvent.AddEventHandler(vm, handler);
            _wiredEvents.Add((vm, "MessageRequested", handler));
        }
    }

    /// <summary>
    /// Zentrale Navigations-Behandlung für alle Sub-Page-Routes
    /// </summary>
    private async void HandleNavigation(string route)
    {
        // Zurück-Navigation
        if (route == ".." || route.Contains("back", StringComparison.OrdinalIgnoreCase))
        {
            GoBack();
            return;
        }

        // DayDetail-Navigation (z.B. "DayDetailPage?date=2026-02-13")
        if (route.StartsWith("DayDetailPage", StringComparison.OrdinalIgnoreCase))
        {
            var dateParam = route.Split("date=", StringSplitOptions.RemoveEmptyEntries);
            if (dateParam.Length > 1 && DateTime.TryParse(dateParam[1], out var date))
            {
                DayDetailVm.SelectedDate = date;
            }
            CloseAllSubPages();
            IsDayDetailActive = true;
            OnPropertyChanged(nameof(IsSubPageActive));
            await DayDetailVm.LoadDataAsync();
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

        // Daten für den jeweiligen Tab neu laden
        _ = LoadTabDataAsync(value);
    }

    private async Task LoadTabDataAsync(int tab)
    {
        switch (tab)
        {
            case 0: await LoadDataAsync(); break;
            case 1: await WeekVm.LoadDataAsync(); break;
            case 2: await CalendarVm.LoadDataAsync(); break;
            case 3: await StatisticsVm.LoadDataAsync(); break;
            case 4: await SettingsVm.LoadDataAsync(); break;
        }
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

    // Tages-Fortschritt als Prozent-Text
    public string DayProgressPercent => $"{DayProgress:F0}%";

    partial void OnDayProgressChanged(double value) => OnPropertyChanged(nameof(DayProgressPercent));

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

    // === Verdienst (Stundenlohn) ===

    [ObservableProperty]
    private string _todayEarnings = "";

    [ObservableProperty]
    private bool _hasEarnings;

    // === Undo ===

    [ObservableProperty]
    private bool _isUndoVisible;

    [ObservableProperty]
    private string _undoMessage = "";

    // === Streak (aufeinanderfolgende Arbeitstage) ===

    [ObservableProperty]
    private int _streakCount;

    public bool HasStreak => StreakCount >= 2;

    partial void OnStreakCountChanged(int value) => OnPropertyChanged(nameof(HasStreak));

    // Wochenziel-Celebration (einmal pro Session)
    private bool _weekGoalCelebrated;

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
        await EnsureInitializedAsync();
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            switch (CurrentStatus)
            {
                case TrackingStatus.Idle:
                    var checkInEntry = await _timeTracking.CheckInAsync();
                    _haptic.Click();
                    ShowUndo(checkInEntry, TrackingStatus.Idle, AppStrings.CheckIn);
                    break;

                case TrackingStatus.Working:
                    var statusBefore = CurrentStatus;
                    var checkOutEntry = await _timeTracking.CheckOutAsync();
                    _haptic.HeavyClick();

                    // Feierabend-Celebration
                    FloatingTextRequested?.Invoke(AppStrings.EndOfDay, "success");
                    CelebrationRequested?.Invoke();

                    // Überstunden anzeigen falls vorhanden
                    var workTime = await _timeTracking.GetCurrentWorkTimeAsync();
                    var today = await _timeTracking.GetTodayAsync();
                    var overtime = workTime - today.TargetWorkTime;
                    if (overtime.TotalMinutes > 1)
                        FloatingTextRequested?.Invoke($"+{overtime.TotalHours:F1}h", "overtime");

                    ShowUndo(checkOutEntry, statusBefore, AppStrings.CheckOut);
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
        await EnsureInitializedAsync();
        if (IsLoading || CurrentStatus == TrackingStatus.Idle) return;

        try
        {
            IsLoading = true;

            if (CurrentStatus == TrackingStatus.Working)
            {
                await _timeTracking.StartPauseAsync();
                _haptic.Click();
            }
            else if (CurrentStatus == TrackingStatus.OnBreak)
            {
                await _timeTracking.EndPauseAsync();
                _haptic.Click();
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

            // Streak berechnen (aufeinanderfolgende Arbeitstage)
            StreakCount = await CalculateStreakAsync();

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
    private async Task NavigateToDayDetailAsync()
    {
        // Immer den heutigen Tag anzeigen wenn von TodayView aus navigiert
        DayDetailVm.SelectedDate = DateTime.Today;
        CloseAllSubPages();
        IsDayDetailActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await DayDetailVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToMonthAsync()
    {
        CloseAllSubPages();
        IsMonthActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await MonthVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToYearAsync()
    {
        CloseAllSubPages();
        IsYearActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await YearVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToVacationAsync()
    {
        CloseAllSubPages();
        IsVacationActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await VacationVm.LoadDataAsync();
    }

    [RelayCommand]
    private async Task NavigateToShiftPlanAsync()
    {
        CloseAllSubPages();
        IsShiftPlanActive = true;
        OnPropertyChanged(nameof(IsSubPageActive));
        await ShiftPlanVm.LoadDataAsync();
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

    // === Zurück-Taste (Double-Back-to-Exit) ===

    private DateTime _lastBackPressTime;

    /// <summary>
    /// Verarbeitet den Zurück-Button des Geräts.
    /// Gibt true zurück wenn behandelt, false wenn die App beendet werden soll.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Sub-Page offen → schließen
        if (IsSubPageActive)
        {
            GoBack();
            return true;
        }

        // 2. Nicht auf Today-Tab → zurück zu Today
        if (CurrentTab != 0)
        {
            CurrentTab = 0;
            return true;
        }

        // 3. Auf Today-Tab → Double-Back prüfen (2 Sekunden Fenster)
        var now = DateTime.UtcNow;
        if ((now - _lastBackPressTime).TotalMilliseconds < 2000)
        {
            return false; // App beenden
        }

        _lastBackPressTime = now;
        FloatingTextRequested?.Invoke(AppStrings.PressBackAgainToExit, "info");
        return true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    // === Undo Mechanismus ===

    /// <summary>
    /// Zeigt den Undo-Button für 5 Sekunden nach CheckIn/CheckOut
    /// </summary>
    private void ShowUndo(TimeEntry entry, TrackingStatus previousStatus, string actionText)
    {
        _undoCts?.Cancel();
        _lastUndoEntry = entry;
        _statusBeforeUndo = previousStatus;
        UndoMessage = $"{actionText} - {AppStrings.Undo}?";
        IsUndoVisible = true;

        _undoCts = new CancellationTokenSource();
        var token = _undoCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000, token);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsUndoVisible = false;
                    _lastUndoEntry = null;
                });
            }
            catch (TaskCanceledException) { }
        });
    }

    [RelayCommand]
    private async Task UndoLastActionAsync()
    {
        if (_lastUndoEntry == null) return;

        try
        {
            _undoCts?.Cancel();
            IsUndoVisible = false;

            var entryToDelete = _lastUndoEntry;
            _lastUndoEntry = null;

            // Eintrag löschen
            await _database.DeleteTimeEntryAsync(entryToDelete.Id);

            // WorkDay neu berechnen
            var workDay = await _database.GetWorkDayAsync(entryToDelete.Timestamp.Date);
            if (workDay != null)
            {
                await _calculation.RecalculateWorkDayAsync(workDay);
            }

            // Status neu laden
            await _timeTracking.LoadStatusAsync();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    // === Helper methods ===

    private void OnAdUnavailable()
    {
        MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);
    }

    private void OnAdsStateChanged(object? sender, EventArgs e)
    {
        IsAdBannerVisible = _adService.BannerVisible;
    }

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
            var settings = await _database.GetSettingsAsync();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentWorkTime = FormatTimeSpan(workTime);
                CurrentPauseTime = FormatTimeSpan(pauseTime);
                TimeUntilEnd = timeUntilEnd.HasValue ? FormatTimeSpan(timeUntilEnd.Value) : "--:--";

                var prefix = balance.TotalMinutes >= 0 ? "+" : "";
                BalanceTime = $"{prefix}{FormatTimeSpan(balance)}";
                BalanceColor = SolidColorBrush.Parse(balance.TotalMinutes >= 0 ? "#4CAF50" : "#F44336");

                // Tages-Fortschritt
                if (today.TargetWorkMinutes > 0)
                {
                    DayProgress = Math.Min(100, (workTime.TotalMinutes * 100) / today.TargetWorkMinutes);
                }

                // Wochenziel-Celebration (einmal pro Session wenn Ziel erreicht)
                if (WeekProgress >= 100 && !_weekGoalCelebrated)
                {
                    _weekGoalCelebrated = true;
                    CelebrationRequested?.Invoke();
                    FloatingTextRequested?.Invoke(AppStrings.WeekGoalReached ?? "Wochenziel erreicht!", "success");
                }

                // Verdienst berechnen (falls Stundenlohn konfiguriert)
                if (settings.HourlyRate > 0)
                {
                    var earnings = workTime.TotalHours * settings.HourlyRate;
                    TodayEarnings = earnings.ToString("C2");
                    HasEarnings = true;
                }
                else
                {
                    HasEarnings = false;
                }
            });
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    /// <summary>
    /// Berechnet die Anzahl aufeinanderfolgender Arbeitstage (Streak).
    /// Wochenenden (Sa/So) werden übersprungen.
    /// </summary>
    private async Task<int> CalculateStreakAsync()
    {
        var streak = 0;
        var date = DateTime.Today;

        // Maximal 60 Tage zurückschauen
        for (int i = 0; i < 60; i++)
        {
            // Wochenende überspringen
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(-1);
                continue;
            }

            var workDay = await _database.GetWorkDayAsync(date);

            if (workDay != null && workDay.ActualWorkMinutes > 0)
            {
                streak++;
                date = date.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        var totalHours = (int)Math.Abs(ts.TotalHours);
        var minutes = Math.Abs(ts.Minutes);
        var sign = ts.TotalMinutes < 0 ? "-" : "";
        return $"{sign}{totalHours}:{minutes:D2}";
    }

    private async void OnUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        await UpdateLiveDataAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _updateTimer?.Stop();
        if (_updateTimer != null)
            _updateTimer.Elapsed -= OnUpdateTimerElapsed;
        _updateTimer?.Dispose();
        _undoCts?.Cancel();
        _undoCts?.Dispose();
        _timeTracking.StatusChanged -= OnStatusChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        _rewardedAdService.AdUnavailable -= OnAdUnavailable;
        _adService.AdsStateChanged -= OnAdsStateChanged;
        SettingsVm.SettingsChanged -= OnSettingsChanged;

        // Sub-Page Navigation Events abmelden
        foreach (var (vm, eventName, handler) in _wiredEvents)
        {
            var navEvent = vm.GetType().GetEvent(eventName);
            navEvent?.RemoveEventHandler(vm, handler);
        }
        _wiredEvents.Clear();

        GC.SuppressFinalize(this);
    }
}
