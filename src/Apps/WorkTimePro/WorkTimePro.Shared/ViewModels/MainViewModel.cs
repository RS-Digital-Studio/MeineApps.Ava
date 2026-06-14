using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using MeineApps.Core.Ava.Async;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
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
public sealed partial class MainViewModel : ViewModelBase, IDisposable
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

    // Reentrancy-Guard für den 1s-Timer: verhindert überlappende UpdateLiveDataAsync-Ticks
    // bei DB-Latenz > 1s (System.Timers.Timer feuert mit AutoReset=true unabhängig vom Handler).
    private int _liveUpdateGate;

    // Aktuell angezeigtes Datum — für Mitternachts-Rollover bei dauerhaft offener App
    // (Nachtschicht-Zielgruppe): wechselt das Datum, wird der Tag neu geladen.
    private DateTime _trackedDate = DateTime.Today;

    // Gecachte Settings (werden in LoadDataAsync und OnSettingsChanged aktualisiert)
    private WorkSettings? _cachedSettings;

    // Gecachte SolidColorBrush-Instanzen (vermeidet Parse() im 1s-Timer)
    private static readonly SolidColorBrush s_statusIdleBrush = SolidColorBrush.Parse(AppColors.StatusIdle);
    private static readonly SolidColorBrush s_statusActiveBrush = SolidColorBrush.Parse(AppColors.StatusActive);
    private static readonly SolidColorBrush s_statusPausedBrush = SolidColorBrush.Parse(AppColors.StatusPaused);
    private static readonly SolidColorBrush s_balancePositiveBrush = SolidColorBrush.Parse(AppColors.BalancePositive);
    private static readonly SolidColorBrush s_balanceNegativeBrush = SolidColorBrush.Parse(AppColors.BalanceNegative);

    // Undo-Mechanismus (5 Sekunden Fenster nach CheckIn/CheckOut)
    private CancellationTokenSource? _undoCts;
    // AsyncDebouncer statt manuelles CancellationTokenSource — saubere Pause-Semantik
    // (vorher: bool _suppressNoteDebounce konnte bei Exception in LoadDataAsync hängen bleiben).
    private readonly AsyncDebouncer _noteDebouncer = new(TimeSpan.FromMilliseconds(1500));
    private TimeEntry? _lastUndoEntry;

    // Event-Handler-Referenzen für sauberes Dispose
    // Typed Wiring statt Reflection: jeder Subscriber-VM merkt sich seine eigenen Handler-Delegates,
    // damit Dispose sie korrekt abmelden kann.
    private readonly List<(INavigationSource Source, Action<string> Handler)> _navHandlers = [];
    private readonly List<(IMessageSource Source, Action<string, string> Handler)> _msgHandlers = [];

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

        // Banner-Sichtbarkeit zentral steuern: Premium UND aktiver Trial sind werbefrei
        // (Trial verspricht "ohne Werbung"). Status-Wechsel (Kauf, Trial-Start/-Ablauf)
        // aktualisieren den Banner live.
        UpdateBannerVisibility();
        _trialService.TrialStatusChanged += OnTrialStatusChanged;
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

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

        // Back-Press Helper verdrahten (WorkTimePro nutzt FloatingText statt ExitHint)
        _backPressHelper.ExitHintRequested += msg => FloatingTextRequested?.Invoke(msg, "info");

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
    /// DB ist bereits von App.InitializeAndStartAsync() initialisiert.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Init-Fehler: {ex}");
            MessageRequested?.Invoke(
                AppStrings.Error,
                string.Format(AppStrings.ErrorLoading, ex.Message));
        }
    }

    /// <summary>
    /// Wartet auf Abschluss der initialen Datenladung.
    /// Wird von der Loading-Pipeline aufgerufen.
    /// </summary>
    public async Task WaitForInitializationAsync()
    {
        if (_initTask != null) await _initTask;
    }

    /// <summary>
    /// Stellt sicher, dass initiale Daten geladen sind bevor Commands ausgeführt werden
    /// </summary>
    private Task EnsureInitializedAsync() => WaitForInitializationAsync();

    /// <summary>
    /// Settings-Änderungen propagieren. requiresDataReload=true bei strukturellen
    /// Änderungen (Arbeitszeit-Settings, Backup-Restore). Bei kosmetischen Änderungen
    /// (Reminder-Zeit, Stundenlohn) reicht das Settings-Cache-Update.
    /// </summary>
    private void OnSettingsChanged(object? sender, bool requiresDataReload)
    {
        // async void → Forget mit zentraler Fehlerbehandlung
        ForgetExtensions.RunForget(async () =>
        {
            _cachedSettings = await _database.GetSettingsAsync();
            if (requiresDataReload)
                await LoadTabDataAsync(CurrentTab);
        }, ex => MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorLoading, ex.Message)));
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

    // === Observable Properties ===

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingStartColor))]
    [NotifyPropertyChangedFor(nameof(RingEndColor))]
    [NotifyPropertyChangedFor(nameof(IsActivelyWorking))]
    private TrackingStatus _currentStatus = TrackingStatus.Idle;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private IBrush _statusColor = s_statusIdleBrush;

    [ObservableProperty]
    private string _currentWorkTime = "0:00";

    [ObservableProperty]
    private string _currentPauseTime = "0:00";

    [ObservableProperty]
    private string _targetWorkTime = "8:00";

    [ObservableProperty]
    private string _balanceTime = "+0:00";

    [ObservableProperty]
    private IBrush _balanceColor = s_balancePositiveBrush;

    /// <summary>
    /// True wenn die Tagesbalance negativ ist (für pulsierende Animation).
    /// </summary>
    [ObservableProperty]
    private bool _isBalanceNegative;

    [ObservableProperty]
    private string _timeUntilEnd = "--:--";

    [ObservableProperty]
    private double _dayProgress;

    // Tages-Fortschritt als Prozent-Text
    public string DayProgressPercent => $"{DayProgress:F0}%";

    /// <summary>Fortschritt als Fraktion (0.0-1.0) für SkiaGradientRing.</summary>
    public double DayProgressFraction => Math.Clamp(DayProgress / 100.0, 0.0, 1.0);

    partial void OnDayProgressChanged(double value)
    {
        OnPropertyChanged(nameof(DayProgressPercent));
        OnPropertyChanged(nameof(DayProgressFraction));
    }

    [ObservableProperty]
    private double _weekProgress;

    [ObservableProperty]
    private string _weekProgressText = "0%";

    [ObservableProperty]
    private string _todayDateDisplay = DateTime.Today.ToString("D");

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


    // === Predictive Insights (TodayView) ===

    /// <summary>
    /// Geschätztes Arbeitsende basierend auf CheckIn + Soll-Stunden (z.B. "~17:23")
    /// </summary>
    [ObservableProperty]
    private string _estimatedEndTime = "";

    /// <summary>
    /// Verbleibende Zeit bis Soll erfüllt (z.B. "Noch 2:15")
    /// </summary>
    [ObservableProperty]
    private string _remainingTodayText = "";

    /// <summary>
    /// True wenn Insight-Card angezeigt werden soll (nur bei aktivem Tracking)
    /// </summary>
    [ObservableProperty]
    private bool _hasInsight;

    // === Verdienst (Stundenlohn) ===

    [ObservableProperty]
    private string _todayEarnings = "";

    /// <summary>
    /// Roh-Wert des Tagesverdienst (double). Wird vom TodayView für die CountUp-Animation
    /// genutzt — vermeidet das fehleranfällige String-Reparsing pro Sekunde.
    /// </summary>
    [ObservableProperty]
    private double _todayEarningsValue;

    [ObservableProperty]
    private bool _hasEarnings;

    // === Undo ===

    [ObservableProperty]
    private bool _isUndoVisible;

    [ObservableProperty]
    private string _undoMessage = "";

    // === Tagesnotiz ===

    [ObservableProperty]
    private string _todayNote = "";

    [ObservableProperty]
    private bool _isNoteExpanded;

    partial void OnTodayNoteChanged(string value)
    {
        // Debounce: Notiz nach 1.5s Inaktivität automatisch speichern.
        // Während LoadDataAsync wird der Debouncer per Pause()-Scope deaktiviert
        // (siehe LoadDataAsync) → kein Race, kein hängendes Suppress-Flag.
        _noteDebouncer.Trigger(async _ =>
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await SaveNoteAsync();
            });
        });
    }

    // Wochenziel-Celebration (einmal pro Session)
    private bool _weekGoalCelebrated;

    // Derived properties
    public bool IsWorking => CurrentStatus == TrackingStatus.Working || CurrentStatus == TrackingStatus.OnBreak;
    public bool HasCheckedIn => CurrentStatus != TrackingStatus.Idle;

    // Nur echtes Arbeiten (nicht Pause) — steuert Glow/Pulsation des Status-Rings,
    // damit der Ring in der Pause ruht (vorher an IsWorking, das auch in der Pause true ist).
    public bool IsActivelyWorking => CurrentStatus == TrackingStatus.Working;

    // Status-Ring-Farben (gebunden an SkiaGradientRing.StartColor/EndColor). Folgen der
    // gleichen Status-Semantik wie Badge/Button: Idle grau, Working grün, Pause orange
    // (vorher fest grün → optischer Bruch in Pause/Idle).
    public Color RingStartColor => Color.Parse(CurrentStatus switch
    {
        TrackingStatus.Working => AppColors.StatusActive,
        TrackingStatus.OnBreak => AppColors.StatusPaused,
        _ => AppColors.StatusIdle
    });

    public Color RingEndColor => Color.Parse(CurrentStatus switch
    {
        TrackingStatus.Working => AppColors.StatusActiveLight,
        TrackingStatus.OnBreak => AppColors.StatusPausedLight,
        _ => AppColors.StatusIdleLight
    });

    public MaterialIconKind StatusIconKind => CurrentStatus switch
    {
        TrackingStatus.Idle => MaterialIconKind.Play,
        TrackingStatus.Working => MaterialIconKind.Stop,
        TrackingStatus.OnBreak => MaterialIconKind.Play,
        _ => MaterialIconKind.Play
    };

    // Localized Button Texts (Icon kommt als MaterialIcon im XAML, nicht als MDI-Glyph im Text)
    public string PauseButtonText => CurrentStatus == TrackingStatus.OnBreak
        ? AppStrings.EndPause
        : AppStrings.Break;

    public string ShowDayDetailsText => AppStrings.ShowDayDetails;

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
                    ShowUndo(checkInEntry, AppStrings.CheckIn);
                    break;

                case TrackingStatus.Working:
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

                    ShowUndo(checkOutEntry, AppStrings.CheckOut);
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

            // Aktiven Arbeitstag laden (bei über Mitternacht laufender Nachtschicht der Vortag
            // mit dem offenen Check-In) — so passen Einträge, Zeiten und Datum zur laufenden
            // Schicht statt einen leeren neuen Tag zu zeigen, während der Live-Timer schon läuft.
            var today = await _timeTracking.GetActiveWorkDayAsync();

            // _trackedDate bleibt der Kalendertag (Mitternachts-Rollover-Trigger in
            // UpdateLiveDataAsync); die Anzeige zeigt das Datum des aktiven Tages.
            _trackedDate = DateTime.Today;
            TodayDateDisplay = today.Date.ToString("D");

            // Load entries
            var entries = await _database.GetTimeEntriesAsync(today.Id);
            TodayEntries = new ObservableCollection<TimeEntry>(entries);

            var pauses = await _database.GetPauseEntriesAsync(today.Id);
            TodayPauses = new ObservableCollection<PauseEntry>(pauses);

            // Times
            TargetWorkTime = TimeFormatter.FormatMinutes(today.TargetWorkMinutes);
            FirstCheckIn = today.FirstCheckIn?.ToString("HH:mm") ?? "--:--";
            LastCheckOut = today.LastCheckOut?.ToString("HH:mm") ?? "--:--";

            // Tagesnotiz laden (Debouncer pausieren statt Suppress-Flag → kein Leak bei Exception)
            using (_noteDebouncer.Pause())
            {
                TodayNote = today.Note ?? "";
            }

            // Auto-Pause Info
            HasAutoPause = today.HasAutoPause;
            if (HasAutoPause)
            {
                AutoPauseInfo = $"+{today.AutoPauseMinutes} min ({AppStrings.LegalSourceArbZG})";
            }

            // Settings ZUERST holen + cachen, dann an Subroutinen durchreichen.
            // (Vorher: GetWeekProgressAsync lädt Settings intern, danach erneutes
            // GetSettingsAsync für den Cache → 2 unnötige Round-Trips.)
            _cachedSettings = await _database.GetSettingsAsync();

            // Week progress
            WeekProgress = await _calculation.GetWeekProgressAsync(_cachedSettings);
            WeekProgressText = $"{WeekProgress:F0}%";


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

    // === Zurück-Taste (Double-Back-to-Exit) ===

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Verarbeitet den Zurück-Button des Geräts.
    /// Gibt true zurück wenn behandelt, false wenn die App beendet werden soll.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offenes Overlay → über den jeweiligen Cancel-Pfad schließen
        //    (räumt auch _pendingAction der Rewarded-Overlays auf)
        if (TryCloseOpenOverlays())
            return true;

        // 2. Sub-Page offen → schließen
        if (IsSubPageActive)
        {
            GoBack();
            return true;
        }

        // 3. Nicht auf Today-Tab → zurück zu Today
        if (CurrentTab != 0)
        {
            CurrentTab = 0;
            return true;
        }

        // 4. Auf Today-Tab → Double-Back prüfen (2 Sekunden Fenster)
        return _backPressHelper.HandleDoubleBack(AppStrings.PressBackAgainToExit);
    }

    /// <summary>
    /// Schließt das aktuell offene Overlay eines Child-VMs über dessen Cancel-Command
    /// (Schritt 1 des Back-Patterns). Gibt true zurück, wenn ein Overlay offen war.
    /// </summary>
    private bool TryCloseOpenOverlays()
    {
        if (StatisticsVm.ShowExportFormatOverlay) { StatisticsVm.CancelExportFormatCommand.Execute(null); return true; }
        if (StatisticsVm.ShowRewardedAdOverlay) { StatisticsVm.CancelAdOverlayCommand.Execute(null); return true; }

        if (VacationVm.IsEditingQuota) { VacationVm.CancelEditQuotaCommand.Execute(null); return true; }
        if (VacationVm.ShowRewardedAdOverlay) { VacationVm.CancelAdOverlayCommand.Execute(null); return true; }

        if (YearVm.ShowRewardedAdOverlay) { YearVm.CancelAdOverlayCommand.Execute(null); return true; }

        if (CalendarVm.IsOverlayVisible) { CalendarVm.CancelOverlayCommand.Execute(null); return true; }

        if (DayDetailVm.IsTimeEntryOverlayVisible) { DayDetailVm.CancelTimeEntryOverlayCommand.Execute(null); return true; }
        if (DayDetailVm.IsPauseOverlayVisible) { DayDetailVm.CancelPauseOverlayCommand.Execute(null); return true; }
        if (DayDetailVm.IsConfirmDeleteVisible) { DayDetailVm.CancelDeleteCommand.Execute(null); return true; }
        if (DayDetailVm.IsStatusSelectionVisible) { DayDetailVm.CancelStatusSelectionCommand.Execute(null); return true; }

        if (SettingsVm.IsImportConfirmVisible) { SettingsVm.CancelImportCommand.Execute(null); return true; }
        if (SettingsVm.IsPurchaseOptionsVisible) { SettingsVm.CancelPurchaseOptionsCommand.Execute(null); return true; }

        return false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// Vom Stempel-QR-Deep-Link (worktimepro://stamp) ausgelöst: stempelt ein bzw. aus —
    /// identisches Verhalten zum CheckIn/Out-Button (inkl. Initialisierungs-Wait,
    /// Undo-Fenster und Status-Feedback). Aufrufer: MainActivity (Intent-Filter).
    /// </summary>
    public async Task HandleStampScanAsync()
    {
        // Zum Today-Tab wechseln, damit der Nutzer das Stempel-Ergebnis sieht
        CloseAllSubPages();
        CurrentTab = 0;
        await ToggleTrackingAsync();
    }

    // === Undo Mechanismus ===

    /// <summary>
    /// Zeigt den Undo-Button für 5 Sekunden nach CheckIn/CheckOut
    /// </summary>
    private void ShowUndo(TimeEntry entry, string actionText)
    {
        _undoCts?.Cancel();
        _lastUndoEntry = entry;
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

            // Wird ein Check-Out rückgängig gemacht, der beim Auschecken eine laufende Pause
            // beendet hat (CheckOutAsync setzt deren EndTime auf den Check-Out-Zeitpunkt), die
            // Pause wieder öffnen — sonst meldet LoadStatusAsync "arbeitend" statt "in Pause"
            // (die Pause ging beim Undo sonst stillschweigend verloren).
            if (entryToDelete.Type == EntryType.CheckOut)
            {
                var pauses = await _database.GetPauseEntriesAsync(entryToDelete.WorkDayId);
                var pauseEndedByCheckOut = pauses.FirstOrDefault(p =>
                    p.EndTime != null && Math.Abs((p.EndTime.Value - entryToDelete.Timestamp).TotalSeconds) < 1);
                if (pauseEndedByCheckOut != null)
                {
                    pauseEndedByCheckOut.EndTime = null;
                    await _database.SavePauseEntryAsync(pauseEndedByCheckOut);
                }
            }

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

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        try
        {
            var today = await _timeTracking.GetTodayAsync();
            today.Note = string.IsNullOrWhiteSpace(TodayNote) ? null : TodayNote.Trim();
            await _database.SaveWorkDayAsync(today);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notiz-Speicher-Fehler: {ex.Message}");
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

    private void OnTrialStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBannerVisibility);
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBannerVisibility);
    }

    /// <summary>
    /// Banner anzeigen, solange weder Premium gekauft noch ein Trial aktiv ist.
    /// </summary>
    private void UpdateBannerVisibility()
    {
        if (_adService.AdsEnabled && !_purchaseService.IsPremium && !_trialService.IsTrialActive)
            _adService.ShowBanner();
        else
            _adService.HideBanner();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateStatusDisplay();
            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(ShowDayDetailsText));
            TodayDateDisplay = DateTime.Today.ToString("D");
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
                StatusColor = s_statusIdleBrush;
                break;

            case TrackingStatus.Working:
                StatusText = _localization.GetString("Status_Working") ?? AppStrings.Status_Working;
                StatusColor = s_statusActiveBrush;
                break;

            case TrackingStatus.OnBreak:
                StatusText = _localization.GetString("Status_OnBreak") ?? AppStrings.Status_OnBreak;
                StatusColor = s_statusPausedBrush;
                break;
        }
    }

    private async Task UpdateLiveDataAsync()
    {
        if (_disposed) return;

        // Mitternachts-Rollover: läuft die App über Mitternacht offen (Nachtschicht),
        // auf den neuen Tag umstellen und neu laden, damit Datum/IsToday/Live-Daten stimmen.
        if (DateTime.Today != _trackedDate)
        {
            _trackedDate = DateTime.Today;
            // Nur dispatchen wenn vom Timer (Background-Thread) aufgerufen. Im Init-Pfad
            // (Loading-Pipeline) läuft diese Methode bereits auf dem UI-Thread — ein dortiges
            // InvokeAsync würde während der noch synchron verketteten Startsequenz deadlocken
            // (Job wird gequeued, aber die Dispatcher-Loop pumpt ihn erst nach Init-Abschluss).
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                await LoadDataAsync();
            else
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(LoadDataAsync);
            return;
        }

        try
        {
            // Ein einziger Snapshot statt 5+ separater DB-Queries pro Sekunde
            var snapshot = await _timeTracking.GetLiveDataSnapshotAsync();
            var workTime = snapshot.WorkTime;
            var pauseTime = snapshot.PauseTime;
            var timeUntilEnd = snapshot.TimeUntilEnd;
            var today = snapshot.Today;
            var balance = workTime - today.TargetWorkTime;
            var settings = _cachedSettings ?? await _database.GetSettingsAsync();

            // UI-Properties aktualisieren. Im Init-Pfad (Loading-Pipeline) läuft diese Methode
            // bereits auf dem UI-Thread — dann direkt ausführen statt erneut zu dispatchen.
            // Ein InvokeAsync von innerhalb der noch synchron verketteten Startsequenz deadlockt
            // sonst (der Job wird in die Dispatcher-Queue eingereiht, aber die Loop pumpt ihn
            // erst nach Init-Abschluss — der wiederum auf diesen Job wartet). Beim 1s-Timer
            // (System.Timers.Timer, Background-Thread) ist das Dispatchen weiterhin nötig.
            void ApplyLiveData()
            {
                CurrentWorkTime = TimeFormatter.FormatMinutes((int)workTime.TotalMinutes);
                CurrentPauseTime = TimeFormatter.FormatMinutes((int)pauseTime.TotalMinutes);
                TimeUntilEnd = timeUntilEnd.HasValue ? TimeFormatter.FormatMinutes((int)timeUntilEnd.Value.TotalMinutes) : "--:--";

                BalanceTime = TimeFormatter.FormatBalance((int)balance.TotalMinutes);
                BalanceColor = balance.TotalMinutes >= 0 ? s_balancePositiveBrush : s_balanceNegativeBrush;
                IsBalanceNegative = balance.TotalMinutes < -1; // Nur bei deutlich negativer Balance

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

                // Predictive Insights: Geschätztes Ende + verbleibende Zeit
                if (CurrentStatus != TrackingStatus.Idle && today.FirstCheckIn.HasValue && today.TargetWorkMinutes > 0)
                {
                    var remainingMinutes = today.TargetWorkMinutes - workTime.TotalMinutes;
                    if (remainingMinutes > 0)
                    {
                        // Geschätztes Ende = Jetzt + verbleibende Arbeitszeit
                        var estimatedEnd = DateTime.Now.AddMinutes(remainingMinutes);
                        EstimatedEndTime = $"~{estimatedEnd:HH:mm}";
                        var remHours = (int)remainingMinutes / 60;
                        var remMins = (int)remainingMinutes % 60;
                        // Fallback ist nur Defensive — RESX-Key existiert in allen 6 Sprachen.
                        // Mit deutschem Default um nicht englisch durchzudringen wenn der Key mal verloren geht.
                        RemainingTodayText = string.Format(
                            AppStrings.RemainingTodayFormat ?? "{0} {1}:{2:D2} h",
                            AppStrings.Remaining ?? "Noch", remHours, remMins);
                        HasInsight = true;
                    }
                    else
                    {
                        // Soll bereits erfüllt
                        HasInsight = false;
                    }
                }
                else
                {
                    HasInsight = false;
                }

                // Verdienst berechnen (falls Stundenlohn konfiguriert)
                if (settings.HourlyRate > 0)
                {
                    var earnings = workTime.TotalHours * settings.HourlyRate;
                    TodayEarningsValue = earnings;
                    // Explizit aktuelle Kultur verwenden (konsistent mit App-Spracheinstellung)
                    TodayEarnings = earnings.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);
                    HasEarnings = true;
                }
                else
                {
                    HasEarnings = false;
                }
            }

            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                ApplyLiveData();
            else
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(ApplyLiveData);
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(AppStrings.Error, string.Format(AppStrings.ErrorGeneric, ex.Message));
        }
    }

    private void OnUpdateTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Timer-Event sekündlich → zentraler Forget-Helper statt async void mit try/catch.
        // Nach Dispose ist UpdateLiveDataAsync ein No-Op (_disposed-Guard); Forget meldet sonstige
        // Fehler an die zentrale Stelle ohne sekündlich MessageRequested zu spammen.
        // Reentrancy-Guard: läuft der vorherige Tick noch (DB-Latenz > 1s), diesen Tick überspringen.
        if (Interlocked.CompareExchange(ref _liveUpdateGate, 1, 0) != 0)
            return;
        ForgetExtensions.RunForget(
            async () =>
            {
                try { await UpdateLiveDataAsync(); }
                finally { Interlocked.Exchange(ref _liveUpdateGate, 0); }
            });
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
        _noteDebouncer.Dispose();
        _timeTracking.StatusChanged -= OnStatusChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        _rewardedAdService.AdUnavailable -= OnAdUnavailable;
        _adService.AdsStateChanged -= OnAdsStateChanged;
        _trialService.TrialStatusChanged -= OnTrialStatusChanged;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        SettingsVm.SettingsChanged -= OnSettingsChanged;

        // Sub-Page Navigation/Message Events abmelden (typed statt Reflection)
        foreach (var (source, handler) in _navHandlers)
            source.NavigationRequested -= handler;
        _navHandlers.Clear();

        foreach (var (source, handler) in _msgHandlers)
            source.MessageRequested -= handler;
        _msgHandlers.Clear();

        // Sub-VMs die IDisposable implementieren disposen — Sub-VMs sind Singletons,
        // ihr Cleanup-Pfad muss explizit aufgerufen werden (sonst leakt z.B. der
        // AutoSave-CancellationTokenSource in SettingsVm bis zum Prozessende).
        (SettingsVm as IDisposable)?.Dispose();
        (DayDetailVm as IDisposable)?.Dispose();
        (StatisticsVm as IDisposable)?.Dispose();
        (VacationVm as IDisposable)?.Dispose();
        (WeekVm as IDisposable)?.Dispose();

        GC.SuppressFinalize(this);
    }
}
