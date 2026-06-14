using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using BingXBot.Trading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// Anzeige-Modell fuer einen einzelnen Activity-Feed-Eintrag.
/// </summary>
public record ActivityItem(DateTime Time, string Category, string Message, Core.Enums.LogLevel Level, string? Symbol)
{
    public string TimeText => Time.ToLocalTime().ToString("HH:mm:ss");
    public string LevelText => Level.ToString();
    public string LevelColor => Level switch
    {
        Core.Enums.LogLevel.Error => "#EF4444",
        Core.Enums.LogLevel.Warning => "#F59E0B",
        Core.Enums.LogLevel.Trade => "#10B981",
        _ => "#94A3B8"
    };
}

/// <summary>
/// ViewModel fuer das Dashboard - ehrliche Zustandsanzeige ohne Fake-Daten.
/// Zeigt nur echte Daten an (BTC-Kurs live, Account nur wenn Bot laeuft).
/// Publiziert Bot-Status und Log-Einträge über den BotEventBus.
/// Enthält Strategie-Auswahl, PaperTradingService-Verdrahtung und Live-Trading.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private readonly StrategyManager _strategyManager;
    private readonly PaperTradingService _paperService;
    private readonly ISecureStorageService? _secureStorage;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotSettings _botSettings;
    private bool _isInitializing = true; // Unterdrückt Preset-Override beim Konstruktor
    private PeriodicTimer? _equityTimer;
    private CancellationTokenSource? _equityCts;
    private PeriodicTimer? _accountUpdateTimer;

    // === Live-Trading (delegiert an LiveTradingManager) ===
    private readonly LiveTradingManager _liveManager;
    // CancellationToken für Account-Update-Timer (wird bei Stop gecancelled)
    private CancellationTokenSource? _accountUpdateCts;
    // Task-Referenz: Verhindert parallele Timer-Loops bei schnellem Start/Stop
    private Task? _accountUpdateTask;
    // Hintergrund-Init-Tasks (gespeichert statt fire-and-forget für Debugging)
    private Task? _initSymbolsTask;
    private Task? _initEquityTask;

    // === Sub-ViewModels (delegierte Verantwortlichkeiten) ===

    /// <summary>BTC-USDT Live-Ticker und Chart (vollständig unabhängig).</summary>
    public BtcTickerViewModel BtcTicker { get; }

    /// <summary>Activity-Feed: Letzte 20 Bot-Aktionen (vollständig unabhängig).</summary>
    public ActivityFeedViewModel Activity { get; }

    // === Modus ===
    [ObservableProperty] private bool _isPaperMode = true;
    [ObservableProperty] private string _modeText = "Paper-Modus";
    [ObservableProperty] private string _modeDescription = "Simuliertes Trading ohne echtes Geld";

    /// <summary>
    /// Engine-Wahl: false = Scalper (TrendFollow-Fast, per-Symbol-Scan), true = Cross-Sectional-Momentum
    /// (market-neutraler Korb, monatlicher Rebalance). Wirkt im Remote-Modus (Pi) ueber BotStartRequest.Engine.
    /// </summary>
    [ObservableProperty] private bool _isCrossSectional;

    // === Live-Trading Zustand ===
    [ObservableProperty] private bool _hasApiKeys;
    [ObservableProperty] private string _liveStatusText = "API-Keys nicht konfiguriert";
    [ObservableProperty] private bool _isLiveActive; // true wenn Live-Trading aktiv handelt (für roten UI-Rahmen)

    // === Bot-Status ===
    [ObservableProperty] private string _botStatusText = "Gestoppt";
    [ObservableProperty] private BotState _botStatusState = BotState.Stopped;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _canStart = true;

    // === Strategie-Auswahl + aktive Timeframes (Multi-TF Standalone) ===
    [ObservableProperty] private string _selectedStrategy = "TrendFollow-Fast";
    [ObservableProperty] private string _strategyDescription = "";
    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;

    // TF-Checkboxen für Multi-TF Standalone
    [ObservableProperty] private bool _tfD1Active = true;
    [ObservableProperty] private bool _tfH4Active = true;
    [ObservableProperty] private bool _tfH1Active = true;
    [ObservableProperty] private bool _tfM15Active = true;

    // === v1.6.0 Phase 10B — Stats-Breakdown-Card (TF × Category × Mode) ===
    private readonly IStatsService? _statsService;
    private Avalonia.Threading.DispatcherTimer? _statsRefreshTimer;
    /// <summary>Aggregierte Stats pro (TF × MarketCategory × Mode) — wird alle 30 s refreshed.</summary>
    public ObservableCollection<BingXBot.Contracts.Dto.TradeStatsBreakdownRowDto> StatsBreakdown { get; } = new();
    [ObservableProperty] private bool _isStatsLoading;
    [ObservableProperty] private string _statsStatusText = "";


    // === Account (nur anzeigen wenn Daten vorhanden) ===
    [ObservableProperty] private bool _hasAccountData;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _availableBalance;
    [ObservableProperty] private decimal _unrealizedPnl;
    [ObservableProperty] private decimal _totalPnl;

    public bool IsUnrealizedPnlPositive => UnrealizedPnl >= 0;
    public bool IsTotalPnlPositive => TotalPnl >= 0;

    // === Rolling Live-Metriken (30 Trades) ===
    [ObservableProperty] private decimal _rollingWinRate;
    [ObservableProperty] private decimal _rollingSharpe;
    [ObservableProperty] private decimal _rollingProfitFactor;
    [ObservableProperty] private string _strategyHealthText = "";
    [ObservableProperty] private bool _hasStrategyWarning;
    public string StatusDotColor => BotStatusState switch
    {
        BotState.Running => "#10B981",
        BotState.Paused => "#F59E0B",
        BotState.Starting => "#3B82F6",
        BotState.Error or BotState.EmergencyStop => "#EF4444",
        _ => "#64748B"
    };

    partial void OnUnrealizedPnlChanged(decimal value) => OnPropertyChanged(nameof(IsUnrealizedPnlPositive));
    partial void OnTotalPnlChanged(decimal value) => OnPropertyChanged(nameof(IsTotalPnlPositive));
    partial void OnBotStatusStateChanged(BotState value)
    {
        OnPropertyChanged(nameof(StatusDotColor));
    }

    // Markt-Kategorie-Änderungen an ScannerSettings weiterleiten
    partial void OnIsCommodityEnabledChanged(bool value) => UpdateEnabledCategories();
    partial void OnIsIndexEnabledChanged(bool value) => UpdateEnabledCategories();
    partial void OnIsForexEnabledChanged(bool value) => UpdateEnabledCategories();
    partial void OnIsStockEnabledChanged(bool value) => UpdateEnabledCategories();

    /// <summary>
    /// Synchronisiert die UI-Checkboxen mit ScannerSettings.EnabledCategories.
    /// Krypto ist immer aktiv. TradFi wird aktiviert sobald min. eine Kategorie gewählt.
    /// </summary>
    private void UpdateEnabledCategories()
    {
        _scannerSettings.EnableTradFi = IsCommodityEnabled || IsIndexEnabled || IsForexEnabled || IsStockEnabled;
        _scannerSettings.EnabledCategories.Clear();
        _scannerSettings.EnabledCategories.Add(MarketCategory.Crypto);
        if (IsCommodityEnabled) _scannerSettings.EnabledCategories.Add(MarketCategory.Commodity);
        if (IsIndexEnabled) _scannerSettings.EnabledCategories.Add(MarketCategory.Index);
        if (IsForexEnabled) _scannerSettings.EnabledCategories.Add(MarketCategory.Forex);
        if (IsStockEnabled) _scannerSettings.EnabledCategories.Add(MarketCategory.Stock);
        _ = _settingsPersistence.SaveAllAsync();
    }

    // === Offene Positionen ===
    public ObservableCollection<PositionDisplayItem> OpenPositions { get; } = new();
    [ObservableProperty] private bool _hasOpenPositions;
    [ObservableProperty] private string _positionsStatusText = "Keine offenen Positionen";

    // === Activity-Feed Expand/Collapse ===
    [ObservableProperty] private bool _isActivityExpanded = true;

    // === Equity-Kurve ===
    public ObservableCollection<EquityPoint> EquityData { get; } = new();

    // === Dashboard-Widgets ===
    // Drawdown-Chart (nutzt EquityData, wird im selben Refresh-Zyklus aktualisiert)
    // PnL-Kalender: Tägliche PnL aus Trade-History
    // Setter für atomaren Swap (Thread-Safety: SkiaSharp-Renderer liest auf Render-Thread)
    public Dictionary<DateTime, decimal> DailyPnl { get; private set; } = new();
    // Korrelations-Matrix: Symbole + Matrix
    public string[] CorrelationSymbols { get; set; } = [];
    public float[,] CorrelationMatrix { get; set; } = new float[0, 0];

    /// <summary>
    /// Wird ausgeloest wenn Widget-Daten (DailyPnl) aktualisiert wurden
    /// und die SkiaSharp-Canvases neu gezeichnet werden muessen.
    /// </summary>
    public event Action? WidgetCanvasInvalidationRequested;

    // === Bestätigungs-Dialog ===
    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _confirmDialogTitle = "";
    [ObservableProperty] private string _confirmDialogMessage = "";
    private Func<Task>? _confirmDialogAction;

    // === Hinweise/Onboarding ===
    [ObservableProperty] private bool _showWelcomeHint = true;
    [ObservableProperty] private string _welcomeHintText = "Willkommen! Starte mit einem Backtest um eine Strategie zu testen, oder konfiguriere deine API-Keys in den Einstellungen.";

    // === Markt-Kategorie-Toggles (steuern ScannerSettings.EnabledCategories) ===
    [ObservableProperty] private bool _isCryptoEnabled = true;
    [ObservableProperty] private bool _isCommodityEnabled = true;
    [ObservableProperty] private bool _isIndexEnabled = true;
    [ObservableProperty] private bool _isForexEnabled = true;
    [ObservableProperty] private bool _isStockEnabled = true;

    // === Watchlist (Crypto-Auswahl für Scanner) ===
    [ObservableProperty] private string _watchlistInput = "";
    [ObservableProperty] private bool _isWatchlistActive;
    /// <summary>Aktive Watchlist-Symbole als Chips.</summary>
    public ObservableCollection<string> WatchlistSymbols { get; } = new();
    /// <summary>Alle verfügbaren Symbole von BingX (für AutoComplete). Sofort mit Top-Symbolen befüllt, wird async ersetzt.</summary>
    public ObservableCollection<string> AvailableSymbols { get; } = new(DefaultSymbols);

    private static readonly string[] DefaultSymbols =
    [
        "BTC-USDT", "ETH-USDT", "SOL-USDT", "XRP-USDT", "DOGE-USDT", "ADA-USDT",
        "AVAX-USDT", "DOT-USDT", "LINK-USDT", "UNI-USDT", "ATOM-USDT", "NEAR-USDT",
        "SUI-USDT", "AAVE-USDT", "TRX-USDT", "HBAR-USDT", "ALGO-USDT", "XLM-USDT",
        "WIF-USDT", "PEPE-USDT", "BONK-USDT", "SHIB-USDT", "NOT-USDT", "BRETT-USDT",
        "KAS-USDT", "KAIA-USDT", "MOVE-USDT", "EGLD-USDT", "LISTA-USDT",
        "LTC-USDT", "FIL-USDT", "ARB-USDT", "OP-USDT", "APT-USDT", "SEI-USDT",
        "INJ-USDT", "TIA-USDT", "FET-USDT", "RNDR-USDT", "WLD-USDT", "JUP-USDT",
        "MKR-USDT", "SNX-USDT", "CRV-USDT", "RUNE-USDT", "STX-USDT", "IMX-USDT",
        "GALA-USDT", "SAND-USDT", "BNB-USDT", "TON-USDT", "VET-USDT", "MATIC-USDT",
        "MANA-USDT", "AXS-USDT", "ENS-USDT", "ALTCOIN-USDT"
    ];

    private readonly IBotEventStream _eventStream;
    private readonly IBotControlService _botControl;
    private readonly ISettingsService _settingsService;
    private readonly IAccountService _accountService;
    // App-Lifecycle-Broker (Akku): stoppt Remote-Poll-Loop + Stats-Timer im Hintergrund.
    private readonly IAppLifecycleService? _lifecycle;
    // Remote-Mode Polling-Loop — liest alle 5s AccountSnapshot vom Server
    private CancellationTokenSource? _remoteAccountPollCts;

    /// <summary>
    /// Zentrale Helper-Property für den Remote/Local-Mode-Check.
    /// Eine einzige Stelle die <c>BotSettings.UseRemoteMode</c> abfragt — verhindert Drift
    /// und macht klar wo die Mode-Entscheidung herkommt. Ersetzt 7× verstreute Direkt-Zugriffe
    /// (Code-Review-Hinweis #10: God-ViewModel).
    /// </summary>
    private bool IsRemoteMode => _botSettings.UseRemoteMode;

    private readonly ISettingsPersistenceService _settingsPersistence;

    public DashboardViewModel(
        BotEventBus eventBus,
        IBotEventStream eventStream,
        IBotControlService botControl,
        ISettingsService settingsService,
        IAccountService accountService,
        StrategyManager strategyManager,
        PaperTradingService paperService,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotSettings botSettings,
        LiveTradingManager liveManager,
        ISettingsPersistenceService settingsPersistence,
        IPublicMarketDataClient? publicClient = null,
        BotDatabaseService? dbService = null,
        ISecureStorageService? secureStorage = null,
        IStatsService? statsService = null,
        IAppLifecycleService? lifecycle = null)
    {
        _eventBus = eventBus;
        _eventStream = eventStream;
        _botControl = botControl;
        _settingsService = settingsService;
        _accountService = accountService;
        _strategyManager = strategyManager;
        _paperService = paperService;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _botSettings = botSettings;
        _liveManager = liveManager;
        _settingsPersistence = settingsPersistence;
        _publicClient = publicClient;
        _dbService = dbService;
        _secureStorage = secureStorage;
        _statsService = statsService;
        _lifecycle = lifecycle;

        // Sub-ViewModels erstellen — Lifecycle-Broker an den Ticker durchreichen (Akku: Poll-Stopp im Hintergrund).
        BtcTicker = new BtcTickerViewModel(publicClient, eventBus, lifecycle);
        Activity = new ActivityFeedViewModel(eventStream);

        // Remote-Modus: Account/Positionen vom Server beziehen (Polling + Push-Events).
        if (IsRemoteMode)
        {
            _eventStream.PositionUpdated += OnRemotePositionUpdated;
            _eventStream.EquityUpdate += OnRemoteEquityUpdate;
            _botControl.StatusChanged += OnRemoteStatusChanged;
            _ = StartRemoteAccountPollingAsync();
        }

        // Akku: Bei App-Pause die Client-seitigen Loops/Timer stoppen, bei Resume neu starten.
        // SignalR liefert Echtzeit-Updates ohnehin im Vordergrund; der Poll ist nur Lueckenfueller.
        // Desktop liefert keinen Broker (null) → unveraendertes Verhalten.
        if (_lifecycle != null)
        {
            _lifecycle.Paused += OnAppPaused;
            _lifecycle.Resumed += OnAppResumed;
        }


        // Keine Fake-Daten! Zeige ehrlichen Zustand.
        HasAccountData = false;
        HasOpenPositions = false;

        // API-Key-Status prüfen
        HasApiKeys = _secureStorage?.HasCredentials ?? false;
        if (HasApiKeys)
            LiveStatusText = "API-Keys vorhanden";

        // Trading-Modus aus persistierten Settings übernehmen (Paper vs Live).
        // Ohne das Load war der User nach App-Neustart immer wieder im Paper-Mode, obwohl zuletzt Live lief.
        IsPaperMode = _botSettings.LastMode != TradingMode.Live;
        // Engine-Wahl (Scalper/Cross-Sectional) ebenfalls aus den persistierten Settings uebernehmen.
        IsCrossSectional = _botSettings.LastEngineMode == Core.Enums.EngineMode.CrossSectional;
        ModeText = IsPaperMode ? "Paper-Modus" : "Live-Modus";
        ModeDescription = IsPaperMode
            ? "Simuliertes Trading ohne echtes Geld"
            : (HasApiKeys ? "Echtes Trading mit BingX - Handelt automatisch!" : "API-Keys erforderlich! Gehe zu Einstellungen.");

        // Letzte Strategie aus persistierten Settings laden.
        // TrendFollow ist die einzige Strategie — unbekannte/veraltete Alt-Namen (z.B. das
        // entfernte SK-System) werden auf den Live-Default TrendFollow-Fast gemappt.
        if (!string.IsNullOrEmpty(_botSettings.LastStrategyName)
            && StrategyFactory.AvailableStrategies.Contains(_botSettings.LastStrategyName))
        {
            SelectedStrategy = _botSettings.LastStrategyName;
        }
        else
        {
            SelectedStrategy = "TrendFollow-Fast";
            _botSettings.LastStrategyName = "TrendFollow-Fast";
        }
        OnSelectedStrategyChanged(SelectedStrategy);

        // Multi-TF Standalone: Checkboxen aus ScannerSettings.ActiveTimeframes ableiten
        TfD1Active = _scannerSettings.ActiveTimeframes.Contains(TimeFrame.D1);
        TfH4Active = _scannerSettings.ActiveTimeframes.Contains(TimeFrame.H4);
        TfH1Active = _scannerSettings.ActiveTimeframes.Contains(TimeFrame.H1);
        TfM15Active = _scannerSettings.ActiveTimeframes.Contains(TimeFrame.M15);

        // Initiale Watchlist aus ScannerSettings laden (falls vorher gesetzt)
        foreach (var sym in _scannerSettings.Whitelist)
            WatchlistSymbols.Add(sym);
        IsWatchlistActive = WatchlistSymbols.Count > 0;

        // Hintergrund-Init (Exceptions intern gefangen, kein Trading-Einfluss)
        // Tasks gespeichert statt fire-and-forget → debugbar bei Problemen
        _initSymbolsTask = LoadAvailableSymbolsAsync();
        _initEquityTask = LoadEquityFromDbAsync();

        // Trade-Markers + Metriken-Refresh bei jedem Trade-Abschluss
        _eventBus.TradeCompleted += OnTradeCompletedForMarkers;

        // Initialen Trading-Modus an MainViewModel melden (Statusleiste)
        _eventBus.PublishTradingMode(IsPaperMode);

        // v1.6.0 Phase 10B — Stats-Breakdown 30 s Refresh.
        if (_statsService != null)
        {
            _statsRefreshTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _statsRefreshTimer.Tick += async (_, _) => await RefreshStatsAsync().ConfigureAwait(false);
            _statsRefreshTimer.Start();
            // Initial-Load (fire-and-forget — Fehler werden im StatusText angezeigt).
            _ = RefreshStatsAsync();
        }

        _isInitializing = false; // Ab jetzt überschreiben Modus-Wechsel die Settings
    }

    /// <summary>
    /// v1.6.0 Phase 10B — Holt aktuelle Stats-Aggregat vom Server (Remote) oder direkt
    /// vom <see cref="BingXBot.Trading.Stats.TradeStatsAggregator"/> (Local). Nicht-blockierend.
    /// </summary>
    [RelayCommand]
    public async Task RefreshStatsAsync()
    {
        if (_statsService == null) return;
        try
        {
            IsStatsLoading = true;
            var dto = await _statsService.GetBreakdownAsync().ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatsBreakdown.Clear();
                foreach (var row in dto.Rows.OrderByDescending(r => r.TotalPnl))
                    StatsBreakdown.Add(row);
                StatsStatusText = StatsBreakdown.Count == 0
                    ? "Noch keine Trades aggregiert"
                    : $"{StatsBreakdown.Count} Buckets";
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatsStatusText = $"Stats-Fehler: {ex.Message}";
            });
        }
        finally
        {
            IsStatsLoading = false;
        }
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        try
        {
            var strategy = StrategyFactory.Create(value);
            StrategyDescription = strategy.Description;
            _botSettings.LastStrategyName = value;

            _ = _settingsPersistence.SaveAllAsync();
        }
        catch
        {
            StrategyDescription = "";
        }
    }

    // Multi-TF Standalone: TF-Checkboxen updaten ScannerSettings.ActiveTimeframes
    partial void OnTfD1ActiveChanged(bool value) => SyncActiveTimeframes();
    partial void OnTfH4ActiveChanged(bool value) => SyncActiveTimeframes();
    partial void OnTfH1ActiveChanged(bool value) => SyncActiveTimeframes();
    partial void OnTfM15ActiveChanged(bool value) => SyncActiveTimeframes();

    private void SyncActiveTimeframes()
    {
        if (_isInitializing) return;
        var tfs = new List<TimeFrame>();
        if (TfD1Active) tfs.Add(TimeFrame.D1);
        if (TfH4Active) tfs.Add(TimeFrame.H4);
        if (TfH1Active) tfs.Add(TimeFrame.H1);
        if (TfM15Active) tfs.Add(TimeFrame.M15);
        // Mindestens eine TF muss aktiv sein — Fallback auf H4
        if (tfs.Count == 0)
        {
            TfH4Active = true;
            tfs.Add(TimeFrame.H4);
        }
        _scannerSettings.ActiveTimeframes = tfs;
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Aktive Timeframes: {string.Join(", ", tfs)}"));
        _ = _settingsPersistence.SaveAllAsync();
    }

    /// <summary>Lädt alle verfügbaren Symbole von BingX für AutoComplete. Ersetzt die Default-Liste bei Erfolg.</summary>
    private async Task LoadAvailableSymbolsAsync()
    {
        if (_publicClient == null) return;
        try
        {
            var symbols = await _publicClient.GetAllSymbolsAsync();
            if (symbols.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AvailableSymbols.Clear();
                    foreach (var s in symbols.Take(300))
                        AvailableSymbols.Add(s);
                });
            }
        }
        catch { /* Default-Liste bleibt aktiv */ }
    }

    /// <summary>Lädt persistierte Equity-Snapshots aus der DB (letzte 30 Tage).</summary>
    private async Task LoadEquityFromDbAsync()
    {
        if (_dbService == null) return;
        try
        {
            var snapshots = await _dbService.GetEquitySnapshotsAsync(from: DateTime.UtcNow.AddDays(-30));
            if (snapshots.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    foreach (var point in snapshots.TakeLast(500))
                        EquityData.Add(point);
                });
            }
        }
        catch { /* Equity-Chart startet leer — kein kritischer Fehler */ }
    }

    /// <summary>Fügt ein Symbol zur Watchlist hinzu (aus AutoComplete oder manuelle Eingabe).</summary>
    [RelayCommand]
    private void AddToWatchlist()
    {
        var symbol = WatchlistInput?.Trim().ToUpperInvariant() ?? "";
        if (string.IsNullOrEmpty(symbol)) return;

        // Sicherstellen dass es ein gültiges Symbol ist (muss -USDT enthalten)
        if (!symbol.EndsWith("-USDT") && !symbol.Contains("-"))
            symbol += "-USDT";

        if (WatchlistSymbols.Contains(symbol)) return;

        WatchlistSymbols.Add(symbol);
        WatchlistInput = "";
        SyncWatchlistToScanner();
    }

    /// <summary>Entfernt ein einzelnes Symbol aus der Watchlist (Chip-X-Button).</summary>
    [RelayCommand]
    private void RemoveFromWatchlist(string symbol)
    {
        WatchlistSymbols.Remove(symbol);
        SyncWatchlistToScanner();
    }

    /// <summary>Entfernt alle Symbole aus der Watchlist.</summary>
    [RelayCommand]
    private void ClearWatchlist()
    {
        WatchlistSymbols.Clear();
        SyncWatchlistToScanner();
    }

    /// <summary>Synchronisiert die Watchlist-Symbole mit den ScannerSettings und persistiert in DB.</summary>
    private void SyncWatchlistToScanner()
    {
        _scannerSettings.Whitelist = WatchlistSymbols.ToList();
        IsWatchlistActive = WatchlistSymbols.Count > 0;

        var msg = IsWatchlistActive
            ? $"Watchlist: {string.Join(", ", WatchlistSymbols)}"
            : "Watchlist deaktiviert (alle Symbole)";
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Scanner", msg));

        // Watchlist dauerhaft speichern
        _ = _settingsPersistence.SaveAllAsync();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // App-Lifecycle-Broker abmelden (symmetrisch zum Ctor-Abo).
        if (_lifecycle != null)
        {
            _lifecycle.Paused -= OnAppPaused;
            _lifecycle.Resumed -= OnAppResumed;
        }

        // Remote-Mode: Event-Handler + Polling abmelden
        _remoteAccountPollCts?.Cancel();
        _remoteAccountPollCts?.Dispose();
        if (IsRemoteMode)
        {
            _eventStream.PositionUpdated -= OnRemotePositionUpdated;
            _eventStream.EquityUpdate -= OnRemoteEquityUpdate;
            _botControl.StatusChanged -= OnRemoteStatusChanged;
        }

        // EventBus-Handler sauber abmelden (verhindert Zugriff auf disposed-te Objekte)
        _eventBus.TradeCompleted -= OnTradeCompletedForMarkers;

        if (_statsRefreshTimer != null)
        {
            _statsRefreshTimer.Stop();
            _statsRefreshTimer = null;
        }

        _accountUpdateCts?.Cancel();
        _accountUpdateCts?.Dispose();
        _equityTimer?.Dispose();
        _accountUpdateTimer?.Dispose();
        BtcTicker.Dispose();
        Activity.Dispose();
        _paperService.Dispose();
        _liveManager.Dispose();
    }

}
