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
using MeineApps.Core.Ava.ViewModels;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace BingXBot.ViewModels;

/// <summary>Multi-TF Standalone: Eine Zeile der SK-Ampel-Tabelle (pro Navigator-TF).</summary>
public record SkAmpelRow(string Timeframe, string Status);

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
    [ObservableProperty] private string _selectedStrategy = "SK-System";
    [ObservableProperty] private string _strategyDescription = "";
    /// <summary>True wenn SK-System aktiv (Multi-TF Standalone hat immer SK-System).</summary>
    [ObservableProperty] private bool _isSkSystem = true;
    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;

    // TF-Checkboxen für Multi-TF Standalone
    [ObservableProperty] private bool _tfD1Active = true;
    [ObservableProperty] private bool _tfH4Active = true;
    [ObservableProperty] private bool _tfH1Active = true;
    [ObservableProperty] private bool _tfM15Active = true;

    /// <summary>Multi-TF Standalone: Sequence-Ampel pro Navigator-TF (eine Zeile pro aktiver TF).</summary>
    public ObservableCollection<SkAmpelRow> SkAmpelRows { get; } = new();

    // === Watchdog (24.04.2026) ===
    /// <summary>
    /// Schwelle ab wann ein SK-Ampel-Update als "veraltet" gilt.
    /// Reaction auf Bug 24.04.2026: Engine war 3 Tage idle und UI zeigte "sucheB" als ob es live waere.
    /// </summary>
    private static readonly TimeSpan AmpelStaleThreshold = TimeSpan.FromMinutes(5);
    private DateTime _lastAmpelUpdateUtc = DateTime.MinValue;
    private Avalonia.Threading.DispatcherTimer? _watchdogTimer;

    /// <summary>True wenn der Bot nicht im Running-State ist ODER seit ≥ 5 min kein Engine-Update kam.</summary>
    [ObservableProperty] private bool _isAmpelStale = true;

    /// <summary>Erklaert dem User warum die Ampel veraltet ist (deutsch, in-VM lokalisiert).</summary>
    [ObservableProperty] private string _idleHintText = "Bot läuft nicht — Status veraltet. Auf Start drücken.";

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
        // Watchdog: State-Wechsel triggert sofortige Re-Evaluation (z.B. Stop -> stale=true sofort).
        EvaluateAmpelStaleness();
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
        ISecureStorageService? secureStorage = null)
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

        // Sub-ViewModels erstellen
        BtcTicker = new BtcTickerViewModel(publicClient, eventBus);
        Activity = new ActivityFeedViewModel(eventStream);

        // Remote-Modus: Account/Positionen vom Server beziehen (Polling + Push-Events).
        if (IsRemoteMode)
        {
            _eventStream.PositionUpdated += OnRemotePositionUpdated;
            _eventStream.EquityUpdate += OnRemoteEquityUpdate;
            _botControl.StatusChanged += OnRemoteStatusChanged;
            _ = StartRemoteAccountPollingAsync();
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
        ModeText = IsPaperMode ? "Paper-Modus" : "Live-Modus";
        ModeDescription = IsPaperMode
            ? "Simuliertes Trading ohne echtes Geld"
            : (HasApiKeys ? "Echtes Trading mit BingX - Handelt automatisch!" : "API-Keys erforderlich! Gehe zu Einstellungen.");

        // Letzte Strategie + Trading-Modus aus persistierten Settings laden.
        // Nach Buch-Refactoring (12.04.2026) ist SK-System die einzige Strategie —
        // unbekannte persistierte Alt-Namen werden auf SK-System gemappt.
        if (!string.IsNullOrEmpty(_botSettings.LastStrategyName)
            && StrategyFactory.AvailableStrategies.Contains(_botSettings.LastStrategyName))
        {
            SelectedStrategy = _botSettings.LastStrategyName;
        }
        else
        {
            SelectedStrategy = "SK-System";
            _botSettings.LastStrategyName = "SK-System";
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

        // Multi-TF Standalone: SK-Ampel pro TF empfangen + in UI-Collection pflegen
        _eventBus.SkAmpelUpdated += OnSkAmpelUpdated;

        // Initialen Trading-Modus an MainViewModel melden (Statusleiste)
        _eventBus.PublishTradingMode(IsPaperMode);

        // Watchdog (24.04.2026): Re-Evaluiere Stale-Status alle 30 s.
        _watchdogTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _watchdogTimer.Tick += (_, _) => EvaluateAmpelStaleness();
        _watchdogTimer.Start();
        EvaluateAmpelStaleness();

        _isInitializing = false; // Ab jetzt überschreiben Modus-Wechsel die Settings
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        try
        {
            var strategy = StrategyFactory.Create(value);
            StrategyDescription = strategy.Description;
            _botSettings.LastStrategyName = value;

            // SK-System: Buch hat feste W1/D1/H4/H1/M30 Hierarchie → Trading-Mode irrelevant
            IsSkSystem = value == "SK-System";

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

    [RelayCommand]
    private async Task StartBot()
    {
        // Remote-Modus: Engine laeuft auf dem Pi — Start per HTTP delegieren
        if (IsRemoteMode)
        {
            if (!IsPaperMode)
            {
                ConfirmDialogTitle = "Live-Trading auf Server starten?";
                ConfirmDialogMessage = "Der Pi-Server wird mit ECHTEM GELD handeln.\n\nStelle sicher, dass dein Risikomanagement korrekt konfiguriert ist.";
                _confirmDialogAction = StartRemoteAsync;
                ShowConfirmDialog = true;
                return;
            }
            await StartRemoteAsync();
            return;
        }

        if (IsPaperMode)
        {
            await StartPaperTradingAsync();
        }
        else
        {
            ConfirmDialogTitle = "Live-Trading starten?";
            ConfirmDialogMessage = "Du bist dabei, den Bot mit ECHTEM GELD zu starten.\n\nDer Bot wird automatisch Trades auf BingX eröffnen und schließen. Stelle sicher, dass dein Risikomanagement korrekt konfiguriert ist.";
            _confirmDialogAction = StartLiveTradingAsync;
            ShowConfirmDialog = true;
        }
    }

    /// <summary>Remote-Start (Server uebernimmt die komplette Orchestrierung).</summary>
    private async Task StartRemoteAsync()
    {
        try
        {
            BotStatusText = "Sende Start-Request an Pi...";
            BotStatusState = BotState.Starting;
            CanStart = false;

            var mode = IsPaperMode ? Core.Enums.TradingMode.Paper : Core.Enums.TradingMode.Live;
            var req = new BingXBot.Contracts.Dto.BotStartRequest(
                Mode: mode,
                InitialBalance: IsPaperMode ? _botSettings.PaperInitialBalance : null,
                ActiveTimeframes: _scannerSettings.ActiveTimeframes.ToList());

            var status = await _botControl.StartAsync(req);
            IsRunning = true;
            CanStart = false;
            BotStatusState = status.State;
            BotStatusText = status.State == BotState.Running
                ? (IsPaperMode ? "Paper (Remote)" : "LIVE (Remote) - Handelt aktiv!")
                : status.State.ToString();
            ShowWelcomeHint = false;
            if (!IsPaperMode) { LiveStatusText = "Handelt aktiv (Remote)"; IsLiveActive = true; }
        }
        catch (Exception ex)
        {
            BotStatusText = $"Start fehlgeschlagen: {ex.Message}";
            BotStatusState = BotState.Error;
            CanStart = true;
        }
    }

    /// <summary>
    /// Startet den Paper-Trading-Modus mit simuliertem Kapital.
    /// </summary>
    private async Task StartPaperTradingAsync()
    {
        // Strategie aktivieren (Multi-TF Standalone: kein Preset)
        var strategy = StrategyFactory.Create(SelectedStrategy);
        _strategyManager.SetStrategy(strategy);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Strategie: {SelectedStrategy}"));

        // Paper-Trading unterstützt immer Hedge-Modus (SimulatedExchange erlaubt Long+Short)
        // Ohne dieses Flag werden TradFi-Symbole (Commodities, Stocks, Indices, Forex) komplett ignoriert
        _scannerSettings.IsHedgeModeActive = true;

        // Paper-Trading-Service starten
        _paperService.Start(_botSettings.PaperInitialBalance);

        IsRunning = true;
        CanStart = false;
        BotStatusText = "Läuft (Paper)";
        BotStatusState = BotState.Running;
        ShowWelcomeHint = false;

        // Account-Daten anzeigen
        HasAccountData = true;
        Balance = _botSettings.PaperInitialBalance;
        AvailableBalance = _botSettings.PaperInitialBalance;
        UnrealizedPnl = 0m;
        TotalPnl = 0m;

        // Equity-Snapshots alle 5 Minuten in DB persistieren
        _ = StartEquitySnapshotTimerAsync();

        // Account-Update Timer starten (alle 5 Sekunden)
        _accountUpdateTask = StartAccountUpdateAsync();
    }

    /// <summary>
    /// Startet den Live-Trading-Modus über den LiveTradingManager (Multi-TF Standalone seit 15.04.2026 —
    /// ein Service scannt alle aktiven Navigator-Timeframes D1/H4/H1/M15 parallel pro Symbol).
    /// </summary>
    private async Task StartLiveTradingAsync()
    {
        BotStatusText = "Verbinde mit BingX...";
        BotStatusState = BotState.Starting;
        CanStart = false;

        try
        {
            var result = await _liveManager.ConnectAsync();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Balance = result.Account.Balance;
                AvailableBalance = result.Account.AvailableBalance;
                UnrealizedPnl = result.Account.UnrealizedPnl;
                TotalPnl = result.Account.RealizedPnl + result.Account.UnrealizedPnl;
                HasAccountData = true;

                OpenPositions.Clear();
                foreach (var p in result.Positions)
                    OpenPositions.Add(CreatePositionItem(p));
                UpdatePositionsStatus();
            });

            await _liveManager.StartAsync(SelectedStrategy);

            if (result.Positions.Count > 0)
                await _liveManager.RestorePositionSignalsAsync(result.Positions);

            BotStatusText = "LIVE - Handelt aktiv!";

            IsRunning = true;
            CanStart = false;
            BotStatusState = BotState.Running;
            ShowWelcomeHint = false;
            LiveStatusText = "Handelt aktiv";
            IsLiveActive = true;

            _ = StartEquitySnapshotTimerAsync();
            _accountUpdateTask = StartAccountUpdateAsync();
        }
        catch (Exception ex)
        {
            BotStatusText = ex.Message.Contains("API-Keys") ? ex.Message : "Verbindung fehlgeschlagen";
            BotStatusState = BotState.Error;
            CanStart = true;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"Live-Trading fehlgeschlagen: {ex.Message}"));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        if (!IsPaperMode && _liveManager.Service != null)
        {
            // Live-Modus: Pause/Resume über LiveTradingService (Multi-TF Standalone)
            if (_liveManager.Service!.IsPaused)
            {
                _liveManager.Service!.Resume();
                BotStatusText = "LIVE - Handelt aktiv!";
                BotStatusState = BotState.Running;
                _accountUpdateTask = StartAccountUpdateAsync();
            }
            else
            {
                _liveManager.Service!.Pause();
                _accountUpdateTimer?.Dispose();
                _accountUpdateTimer = null;
                BotStatusText = "LIVE - Pausiert";
                BotStatusState = BotState.Paused;
            }
            return;
        }

        if (_paperService.IsPaused)
        {
            // Resume
            _paperService.Resume();
            BotStatusText = "Läuft (Paper)";
            BotStatusState = BotState.Running;
        }
        else
        {
            // Pause
            _paperService.Pause();
            BotStatusText = "Pausiert";
            BotStatusState = BotState.Paused;
        }
    }

    [RelayCommand]
    private async Task StopBot()
    {
        _accountUpdateCts?.Cancel();
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        // Remote-Modus: Stop per HTTP delegieren
        if (IsRemoteMode)
        {
            try { await _botControl.StopAsync(); }
            catch (Exception ex) { BotStatusText = $"Stop fehlgeschlagen: {ex.Message}"; return; }
            IsRunning = false;
            CanStart = true;
            BotStatusText = "Gestoppt (Remote)";
            BotStatusState = BotState.Stopped;
            PositionsStatusText = "Keine offenen Positionen";
            LiveStatusText = "Getrennt";
            IsLiveActive = false;
            return;
        }

        if (IsPaperMode)
        {
            await _paperService.StopAsync();
        }
        else
        {
            if (_liveManager.IsRunning)
                await _liveManager.StopAsync();
            LiveStatusText = "Getrennt";
            IsLiveActive = false;
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Gestoppt";
        BotStatusState = BotState.Stopped;
        PositionsStatusText = "Keine offenen Positionen";
    }

    [RelayCommand]
    private async Task EmergencyStop()
    {
        // Live-Modus: Bestätigung erforderlich (schließt ALLE echten Positionen!)
        if (!IsPaperMode && IsLiveActive)
        {
            ConfirmDialogTitle = "NOTFALL-STOP ausführen?";
            ConfirmDialogMessage = "ALLE echten Positionen auf BingX werden SOFORT geschlossen!\n\nDies kann nicht rückgängig gemacht werden.";
            _confirmDialogAction = ExecuteEmergencyStopAsync;
            ShowConfirmDialog = true;
            return;
        }

        await ExecuteEmergencyStopAsync();
    }

    private async Task ExecuteEmergencyStopAsync()
    {
        _accountUpdateCts?.Cancel();
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        // Remote-Modus: EmergencyStop per HTTP delegieren (Server schliesst ALLE Positionen serverseitig)
        if (IsRemoteMode)
        {
            try { await _botControl.EmergencyStopAsync(); }
            catch (Exception ex) { BotStatusText = $"Notfall-Stop fehlgeschlagen: {ex.Message}"; return; }
            IsRunning = false;
            CanStart = true;
            BotStatusText = "Notfall-Stop (Remote)";
            BotStatusState = BotState.EmergencyStop;
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
            OpenPositions.Clear();
            HasOpenPositions = false;
            PositionsStatusText = "Alle Positionen geschlossen";
            return;
        }

        if (IsPaperMode)
        {
            await _paperService.EmergencyStopAsync();
        }
        else
        {
            if (_liveManager.IsRunning)
                await _liveManager.EmergencyStopAsync();
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgeführt";
        BotStatusState = BotState.Error;

        // Positionen aus UI entfernen
        OpenPositions.Clear();
        HasOpenPositions = false;
        PositionsStatusText = "Alle Positionen geschlossen";
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (IsRunning)
        {
            // Kann Modus nicht wechseln waehrend Bot laeuft
            return;
        }

        IsPaperMode = !IsPaperMode;
        if (IsPaperMode)
        {
            ModeText = "Paper-Modus";
            ModeDescription = "Simuliertes Trading ohne echtes Geld";
        }
        else
        {
            // API-Key-Status aktualisieren
            HasApiKeys = _secureStorage?.HasCredentials ?? false;
            ModeText = "Live-Modus";
            ModeDescription = HasApiKeys
                ? "Echtes Trading mit BingX - Handelt automatisch!"
                : "API-Keys erforderlich! Gehe zu Einstellungen.";
        }

        // Account-Daten zuruecksetzen bei Modus-Wechsel
        HasAccountData = false;
        Balance = 0;
        AvailableBalance = 0;
        UnrealizedPnl = 0;
        TotalPnl = 0;
        IsLiveActive = false;

        // MainViewModel über Modus-Wechsel informieren (Statusleiste unten rechts)
        _eventBus.PublishTradingMode(IsPaperMode);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Modus gewechselt zu: {ModeText}"));
    }

    [RelayCommand]
    private void ToggleActivityExpanded()
    {
        IsActivityExpanded = !IsActivityExpanded;
    }

    [RelayCommand]
    private void DismissWelcomeHint()
    {
        ShowWelcomeHint = false;
    }

    /// <summary>
    /// Schliesst eine einzelne Position (Paper oder Live).
    /// </summary>
    [RelayCommand]
    /// <summary>
    /// Zeigt den Bestätigungs-Dialog bevor eine einzelne Position geschlossen wird.
    /// </summary>
    private void RequestClosePosition(PositionDisplayItem? position)
    {
        if (position == null) return;

        var pnlText = position.Pnl >= 0 ? $"+{position.Pnl:N2}" : $"{position.Pnl:N2}";
        ConfirmDialogTitle = "Position schließen?";
        ConfirmDialogMessage = $"{position.Symbol} ({position.Side}, {position.Leverage}x)\nPnL: {pnlText} USDT ({position.PnlPercentText})";
        _confirmDialogAction = async () => await ExecuteClosePosition(position);
        ShowConfirmDialog = true;
    }

    /// <summary>
    /// Führt das Schließen einer Position tatsächlich aus (nach Bestätigung).
    /// </summary>
    private async Task ExecuteClosePosition(PositionDisplayItem position)
    {
        var side = position.Side;

        try
        {
            if (IsPaperMode && _paperService.Exchange != null)
            {
                _paperService.Exchange.SetCurrentPrice(position.Symbol, position.MarkPrice);
                await _paperService.Exchange.ClosePositionAsync(position.Symbol, side);
                _paperService.RemovePositionSignal(position.Symbol, side);
            }
            else if (!IsPaperMode && _liveManager.RestClient != null)
            {
                // Position-Daten für CompletedTrade merken
                var entryPrice = position.EntryPrice;
                var exitPrice = position.MarkPrice;
                var qty = position.Quantity;

                await _liveManager.RestClient!.ClosePositionAsync(position.Symbol, side);

                // Signal entfernen (Multi-TF Standalone: ein Service reicht)
                {
                    _liveManager.Service?.RemovePositionSignal(position.Symbol, side);
                }

                // CompletedTrade erstellen damit RiskManager Feedback bekommt
                // Echte Commission-Rate vom BingX-Account (je nach VIP-Level 0.02%-0.075%)
                var feeRate = _liveManager.CommissionTakerRate;
                var fee = qty * entryPrice * feeRate + qty * exitPrice * feeRate;
                var rawPnl = side == Side.Buy
                    ? (exitPrice - entryPrice) * qty
                    : (entryPrice - exitPrice) * qty;
                var trade = new CompletedTrade(position.Symbol, side, entryPrice, exitPrice,
                    qty, rawPnl - fee, fee, DateTime.UtcNow, DateTime.UtcNow,
                    "Manuell geschlossen", Core.Enums.TradingMode.Live);
                _eventBus.PublishTrade(trade);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                $"{position.Symbol}: Position manuell geschlossen ({position.Side})", position.Symbol));

            OpenPositions.Remove(position);
            UpdatePositionsStatus();
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                $"{position.Symbol}: Schließen fehlgeschlagen - {ex.Message}", position.Symbol));
        }
    }

    /// <summary>
    /// Zeigt den Bestätigungs-Dialog bevor alle Positionen geschlossen werden.
    /// </summary>
    [RelayCommand]
    private void CloseAllPositions()
    {
        if (OpenPositions.Count == 0) return;

        var totalPnl = OpenPositions.Sum(p => p.Pnl);
        var pnlText = totalPnl >= 0 ? $"+{totalPnl:N2}" : $"{totalPnl:N2}";
        ConfirmDialogTitle = "Alle Positionen schließen?";
        ConfirmDialogMessage = $"{OpenPositions.Count} offene Position(en)\nGesamter unrealisierter PnL: {pnlText} USDT";
        _confirmDialogAction = async () =>
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Trade",
                $"Schließe alle {OpenPositions.Count} Positionen..."));

            var positionsCopy = OpenPositions.ToList();
            foreach (var pos in positionsCopy)
                await ExecuteClosePosition(pos);
        };
        ShowConfirmDialog = true;
    }

    /// <summary>Bestätigungs-Dialog: Ja → Aktion ausführen.</summary>
    [RelayCommand]
    private async Task ConfirmDialogYes()
    {
        ShowConfirmDialog = false;
        if (_confirmDialogAction != null)
            await _confirmDialogAction();
        _confirmDialogAction = null;
    }

    /// <summary>Bestätigungs-Dialog: Abbrechen.</summary>
    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        ShowConfirmDialog = false;
        _confirmDialogAction = null;
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

    /// <summary>
    /// Sucht das Signal für eine Position im aktiven Service-Modus
    /// (Paper-Service oder Live-Service — Multi-TF Standalone seit 15.04.2026,
    /// MultiModeOrchestrator ist entfernt).
    /// </summary>
    private SignalResult? FindPositionSignal(string symbol, Side side)
    {
        if (IsPaperMode)
            return _paperService.GetPositionSignal(symbol, side);
        return _liveManager.Service?.GetPositionSignal(symbol, side);
    }

    private DateTime? FindEntryTime(string symbol, Side side)
    {
        if (IsPaperMode)
            return _paperService.GetEntryTime(symbol, side);
        return _liveManager.Service?.GetEntryTime(symbol, side);
    }

    // Ausgewählte Position für Chart-Overlay
    [ObservableProperty] private PositionDisplayItem? _selectedPosition;

    /// <summary>Wählt eine Position aus und zeigt ihren Chart + SK-Overlay an.</summary>
    [RelayCommand]
    private async Task SelectPosition(PositionDisplayItem? pos)
    {
        // Alte Auswahl deselektieren
        if (SelectedPosition != null)
            SelectedPosition.IsSelected = false;

        if (pos == null || pos == SelectedPosition)
        {
            // Deselektieren → zurück zu BTC
            SelectedPosition = null;
            await BtcTicker.SwitchSymbolCommand.ExecuteAsync("BTC-USDT");
            BtcTicker.SequenceOverlay = null;
            UpdateChartOverlay();
            return;
        }

        SelectedPosition = pos;
        pos.IsSelected = true;

        // Chart auf das Symbol der Position wechseln
        await BtcTicker.SwitchSymbolCommand.ExecuteAsync(pos.Symbol);

        // Position-Overlay (Entry/SL/TP Linien)
        var signal = FindPositionSignal(pos.Symbol, pos.Side);
        BtcTicker.ActiveOverlay = new ActivePositionOverlay(
            pos.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, pos.Side);

        // SK-Sequenz-Overlay: On-demand aus den gerade geladenen Chart-Candles berechnen
        BtcTicker.SequenceOverlay = BuildSequenceOverlay(BtcTicker.BtcCandles);
    }

    /// <summary>Aktualisiert die Trade-Markers und Positions-Overlay auf dem Chart.</summary>
    private void UpdateChartOverlay()
    {
        // Wenn eine Position ausgewählt ist → deren Overlay anzeigen
        if (SelectedPosition != null)
        {
            var signal = FindPositionSignal(SelectedPosition.Symbol, SelectedPosition.Side);
            BtcTicker.ActiveOverlay = new ActivePositionOverlay(
                SelectedPosition.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, SelectedPosition.Side);
            // SequenceOverlay bleibt vom SelectPosition-Call erhalten (wird dort berechnet)
            return;
        }

        // Sonst: Aktive BTC-Position als Default-Overlay (alle Modi)
        var btcPos = OpenPositions.FirstOrDefault(p => p.Symbol == BtcTicker.SelectedSymbol);
        if (btcPos != null)
        {
            var signal = FindPositionSignal(btcPos.Symbol, btcPos.Side);
            BtcTicker.ActiveOverlay = new ActivePositionOverlay(
                btcPos.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, btcPos.Side);
        }
        else
        {
            BtcTicker.ActiveOverlay = null;
        }
        BtcTicker.SequenceOverlay = null;
    }

    /// <summary>Berechnet SK-Sequenz-Overlay on-demand aus den Chart-Candles.</summary>
    private static SequenceOverlay? BuildSequenceOverlay(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 50) return null;

        var seq = Engine.Indicators.SequenceDetector.DetectSequence(candles, 5, 0.5m, true);
        if (seq == null) return null;

        return new SequenceOverlay(
            seq.Point0.Price, seq.PointA.Price, seq.PointB?.Price,
            seq.Retracement500, seq.Retracement559, seq.Retracement618,
            seq.Retracement667, seq.Retracement71, seq.Retracement786,
            seq.Extension1618, seq.Extension200,
            seq.Extension2618, seq.Extension4236,
            seq.IsLong);
    }

    /// <summary>
    /// <summary>Multi-TF Standalone: Aktualisiert die SK-Ampel-Tabelle im UI (1 Zeile pro TF).</summary>
    private void OnSkAmpelUpdated(object? sender, Dictionary<Core.Enums.TimeFrame, string> ampel)
    {
        // Watchdog: Engine-Lebenszeichen registrieren (auch fuer EvaluateAmpelStaleness).
        _lastAmpelUpdateUtc = DateTime.UtcNow;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Reihenfolge: absteigend (D1 → H4 → H1 → M15)
            var ordered = ampel.OrderByDescending(kv =>
                kv.Key switch
                {
                    Core.Enums.TimeFrame.W1 => 7,
                    Core.Enums.TimeFrame.D1 => 6,
                    Core.Enums.TimeFrame.H4 => 5,
                    Core.Enums.TimeFrame.H1 => 4,
                    Core.Enums.TimeFrame.M30 => 3,
                    Core.Enums.TimeFrame.M15 => 2,
                    Core.Enums.TimeFrame.M5 => 1,
                    _ => 0
                }).ToList();

            SkAmpelRows.Clear();
            foreach (var kv in ordered)
            {
                var label = kv.Key switch
                {
                    Core.Enums.TimeFrame.D1 => "1D",
                    Core.Enums.TimeFrame.H4 => "4H",
                    Core.Enums.TimeFrame.H1 => "1H",
                    Core.Enums.TimeFrame.M15 => "15m",
                    Core.Enums.TimeFrame.M5 => "5m",
                    _ => kv.Key.ToString()
                };
                SkAmpelRows.Add(new SkAmpelRow(label, kv.Value));
            }

            EvaluateAmpelStaleness();
        });
    }

    /// <summary>
    /// Watchdog-Logik (24.04.2026): Setzt <see cref="IsAmpelStale"/> + <see cref="IdleHintText"/>
    /// abhaengig vom Bot-State und Alter des letzten SK-Ampel-Events.
    /// </summary>
    private void EvaluateAmpelStaleness()
    {
        var ageOk = _lastAmpelUpdateUtc != DateTime.MinValue
                    && (DateTime.UtcNow - _lastAmpelUpdateUtc) <= AmpelStaleThreshold;
        var botRunning = BotStatusState == BotState.Running;
        var stale = !botRunning || !ageOk;

        if (stale)
        {
            if (!botRunning)
            {
                IdleHintText = "Bot läuft nicht — angezeigte Ampel-Werte sind veraltet. Auf Start drücken.";
            }
            else
            {
                var ageMin = _lastAmpelUpdateUtc == DateTime.MinValue
                    ? 0
                    : (int)(DateTime.UtcNow - _lastAmpelUpdateUtc).TotalMinutes;
                IdleHintText = ageMin <= 0
                    ? "Bot läuft, aber noch keine Engine-Updates. Bitte einen Moment warten."
                    : $"Letztes Engine-Update vor {ageMin} min — Engine prüft nichts. Logs prüfen.";
            }
        }

        IsAmpelStale = stale;
    }

    /// Erstellt ein PositionDisplayItem mit CloseRequested-Verdrahtung und SL/TP aus dem Service.
    /// </summary>
    private PositionDisplayItem CreatePositionItem(Position p)
    {
        var item = new PositionDisplayItem
        {
            Symbol = p.Symbol,
            Side = p.Side,
            EntryPrice = p.EntryPrice,
            MarkPrice = p.MarkPrice,
            Quantity = p.Quantity,
            Pnl = p.UnrealizedPnl,
            Leverage = p.Leverage
        };

        // Close-Action verdrahten
        item.CloseRequested = (pos) => { RequestClosePosition(pos); return Task.CompletedTask; };

        // SL/TP + erweiterte Infos aus dem Signal laden
        var suppressSlTpEvents = true;

        var signal = FindPositionSignal(p.Symbol, p.Side);
        if (signal != null)
        {
            item.StopLoss = signal.StopLoss;
            item.TakeProfit = signal.TakeProfit;
            item.ConfluenceScore = signal.ConfluenceScore;
            item.StrategyName = signal.DisableSmartBreakeven ? "SK" : "CTP";
        }

        // Multi-TF Standalone: Navigator-TF aus ExitState → Badge
        var navTf = IsPaperMode
            ? _paperService.GetExitStatesSnapshot().GetValueOrDefault($"{p.Symbol}_{p.Side}")?.NavigatorTimeframe
            : _liveManager.Service?.GetExitStatesSnapshot().GetValueOrDefault($"{p.Symbol}_{p.Side}")?.NavigatorTimeframe;
        item.TimeframeBadge = navTf switch
        {
            Core.Enums.TimeFrame.D1 => "1D",
            Core.Enums.TimeFrame.H4 => "4H",
            Core.Enums.TimeFrame.H1 => "1H",
            Core.Enums.TimeFrame.M5 => "5m",
            Core.Enums.TimeFrame.M15 => "15m",
            Core.Enums.TimeFrame.M30 => "30m",
            _ => ""
        };

        suppressSlTpEvents = false;

        // SL/TP-Änderungen an den richtigen Service zurückschreiben
        item.PropertyChanged += (_, e) =>
        {
            if (suppressSlTpEvents) return;
            if (e.PropertyName is not (nameof(PositionDisplayItem.StopLoss) or nameof(PositionDisplayItem.TakeProfit)))
                return;
            if (!OpenPositions.Contains(item)) return;

            var side = item.Side;
            if (IsPaperMode)
            {
                _paperService.UpdatePositionSignal(item.Symbol, side, item.StopLoss, item.TakeProfit);
            }
            else
            {
                _liveManager.Service?.UpdatePositionSignal(item.Symbol, side, item.StopLoss, item.TakeProfit);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Trade",
                $"{item.Symbol}: SL={item.StopLoss?.ToString("F8") ?? "---"} / TP={item.TakeProfit?.ToString("F8") ?? "---"}", item.Symbol));
        };

        return item;
    }


    /// <summary>
    /// Aktualisiert Positionen direkt aus Position-Daten:
    /// - Bestehende Items: Nur volatile Werte updaten (MarkPrice, Pnl, Qty, Leverage)
    /// - Neue Positionen: CreatePositionItem nur für wirklich neue Positionen aufrufen
    /// - Geschlossene Positionen: entfernen
    /// Vermeidet Wegwerf-Objekte + Event-Handler-Leaks bei jedem 5s-Update.
    /// </summary>
    private void UpdatePositionsFromData(IReadOnlyList<Position> positions)
    {
        // Map der neuen Positionen nach Symbol_Side
        var posMap = new Dictionary<string, Position>();
        foreach (var p in positions)
            posMap[$"{p.Symbol}_{p.Side}"] = p;

        // Bestehende Items updaten oder entfernen
        for (int i = OpenPositions.Count - 1; i >= 0; i--)
        {
            var existing = OpenPositions[i];
            if (posMap.TryGetValue(existing.PositionKey, out var updated))
            {
                // Update: Nur volatile Werte aktualisieren (SL/TP + PropertyChanged-Handler bleiben erhalten)
                existing.MarkPrice = updated.MarkPrice;
                existing.Pnl = updated.UnrealizedPnl;
                existing.Quantity = updated.Quantity;
                existing.Leverage = updated.Leverage;

                // Haltezeit aus ExitState berechnen
                var entryTime = FindEntryTime(existing.Symbol, existing.Side);
                if (entryTime.HasValue)
                {
                    var hold = DateTime.UtcNow - entryTime.Value;
                    existing.HoldTimeText = hold.TotalHours >= 24
                        ? $"{hold.Days}d {hold.Hours}h"
                        : hold.TotalHours >= 1
                            ? $"{(int)hold.TotalHours}h {hold.Minutes}m"
                            : $"{hold.Minutes}m";
                }

                // Liquidationspreis berechnen (Isolated-Margin-Formel)
                if (updated.Leverage > 0 && updated.EntryPrice > 0)
                {
                    const decimal mmr = 0.004m; // BingX Maintenance Margin Rate
                    var liqDist = (1m - mmr) / updated.Leverage;
                    existing.LiquidationPrice = updated.Side == Side.Buy
                        ? updated.EntryPrice * (1m - liqDist)
                        : updated.EntryPrice * (1m + liqDist);
                }
                posMap.Remove(existing.PositionKey);
            }
            else
            {
                // Position geschlossen: entfernen
                OpenPositions.RemoveAt(i);
            }
        }

        // Nur für wirklich neue Positionen Items erstellen (mit Event-Handler + SL/TP)
        foreach (var p in posMap.Values)
            OpenPositions.Add(CreatePositionItem(p));
    }

    private bool _disposed;

    /// <summary>
    /// Benannter Handler für TradeCompleted (statt anonymem Lambda, damit -= in Dispose möglich).
    /// </summary>
    private void OnTradeCompletedForMarkers(object? sender, CompletedTrade trade)
    {
        // Rolling-Metriken sofort aktualisieren (nicht 5 Min warten)
        if (IsRunning)
            UpdateRollingMetrics();

        if (trade.Symbol != "BTC-USDT") return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            BtcTicker.TradeMarkers.Add(new TradeMarker(trade.EntryTime, trade.EntryPrice, trade.Side, true));
            BtcTicker.TradeMarkers.Add(new TradeMarker(trade.ExitTime, trade.ExitPrice, trade.Side, false, trade.Pnl));
            // Max 50 Marker behalten
            while (BtcTicker.TradeMarkers.Count > 50)
                BtcTicker.TradeMarkers.RemoveAt(0);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

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
        _eventBus.SkAmpelUpdated -= OnSkAmpelUpdated;

        // Watchdog-Timer (24.04.2026) sauber stoppen — sonst feuert er nach Dispose weiter.
        if (_watchdogTimer != null)
        {
            _watchdogTimer.Stop();
            _watchdogTimer = null;
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

    /// <summary>
    /// Aktualisiert Account-Daten und offene Positionen alle 5 Sekunden.
    /// Nutzt je nach Modus die SimulatedExchange (Paper) oder den echten BingXRestClient (Live).
    /// </summary>
    private async Task StartAccountUpdateAsync()
    {
        // Im Remote-Modus laeuft die Engine server-seitig — Account-Updates kommen via SignalR-Push.
        // Ein lokaler Polling-Timer hat nichts zu tun und wuerde nur Paper/Live-Services-Pfade
        // triggern, die keine Daten haben.
        if (IsRemoteMode) return;

        _accountUpdateCts?.Cancel();
        _accountUpdateCts?.Dispose();
        _accountUpdateCts = new CancellationTokenSource();
        var ct = _accountUpdateCts.Token;

        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (_accountUpdateTimer != null && await _accountUpdateTimer.WaitForNextTickAsync(ct))
            {
                if (!IsRunning) continue;

                try
                {
                    AccountInfo? account = null;
                    IReadOnlyList<Position>? positions = null;

                    if (!IsPaperMode && _liveManager.RestClient != null)
                    {
                        // Live-Modus: Echte Daten von BingX
                        account = await _liveManager.RestClient!.GetAccountInfoAsync();
                        positions = await _liveManager.RestClient!.GetPositionsAsync();
                    }
                    else if (IsPaperMode && _paperService.Exchange != null)
                    {
                        // Paper Single-Mode: Simulierte Daten
                        account = await _paperService.Exchange.GetAccountInfoAsync();
                        positions = await _paperService.Exchange.GetPositionsAsync();
                    }

                    if (account == null) continue;

                    var acct = account;
                    var pos = positions ?? Array.Empty<Position>();
                    var isPaper = IsPaperMode;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Paper: Balance ist Equity (Wallet + Unrealisiert) → Wallet extrahieren
                        // Live: BingX liefert Wallet-Balance direkt im "balance" Feld
                        var walletBalance = acct.Balance - acct.UnrealizedPnl;
                        Balance = isPaper ? walletBalance : acct.Balance;
                        AvailableBalance = acct.AvailableBalance;
                        UnrealizedPnl = acct.UnrealizedPnl;
                        // TotalPnl: Paper = Equity - Startkapital, Live = Realisiert + Unrealisiert
                        TotalPnl = isPaper
                            ? acct.Balance - _botSettings.PaperInitialBalance
                            : acct.RealizedPnl + acct.UnrealizedPnl;

                        // Inkrementell: bestehende Items updaten, nur für neue CreatePositionItem aufrufen
                        UpdatePositionsFromData(pos);
                        UpdatePositionsStatus();
                    });
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Dashboard",
                        $"Account-Update Fehler: {ex.Message}"));

                    // Im Live-Modus bei Verbindungsproblemen Warnung zeigen
                    if (!IsPaperMode)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            LiveStatusText = $"Fehler: {ex.Message}";
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // Timer wurde disposed während WaitForNextTickAsync
    }

    /// <summary>
    /// Startet periodische Equity-Snapshots (alle 5 Minuten) in die DB.
    /// </summary>
    private async Task StartEquitySnapshotTimerAsync()
    {
        // Im Remote-Modus wird Equity serverseitig getrackt — kein lokaler Timer noetig.
        if (IsRemoteMode) return;
        if (_dbService == null) return;

        StopEquitySnapshotTimer();
        _equityCts = new CancellationTokenSource();
        _equityTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            // Ersten Snapshot sofort speichern
            await SaveEquitySnapshotAsync();

            // Eigener CTS: Unabhängig von _accountUpdateCts, wird bei StopBot/Dispose gecancelt
            while (await _equityTimer.WaitForNextTickAsync(_equityCts.Token))
                await SaveEquitySnapshotAsync();
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // Timer wurde disposed bevor CancellationToken feuerte
    }

    /// <summary>
    /// Speichert einen einzelnen Equity-Snapshot in der DB.
    /// Wird alle 5 Minuten aufgerufen (Paper + Live). Läuft nur im Desktop-Standalone-/Local-Mode
    /// (Server hat kein DashboardViewModel). `EventBus.PublishEquity` bleibt bewusst bei
    /// `PaperTradingService.PublishNewTrades` — doppeltes Publishing vom Dashboard aus wäre
    /// im Standalone-Modus nur lokaler Lärm ohne Abnehmer und im Server-Modus läuft der Code
    /// ohnehin nicht. Remote-Equity-Kurve erfordert einen HostedService-basierten Tracker.
    /// </summary>
    private async Task SaveEquitySnapshotAsync()
    {
        if (_dbService == null || !HasAccountData) return;
        try
        {
            var point = new EquityPoint(DateTime.UtcNow, Balance + UnrealizedPnl);
            await _dbService.SaveEquitySnapshotAsync(point);

            // Auch in die ObservableCollection für das UI (max 500 Punkte, älteste entfernen)
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                EquityData.Add(point);
                while (EquityData.Count > 500)
                    EquityData.RemoveAt(0);
            });

            // Rolling-Metriken vom RiskManager aktualisieren
            UpdateRollingMetrics();
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Dashboard",
                $"Equity-Snapshot speichern fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>Aktualisiert Rolling-Metriken + Widget-Daten aus dem aktiven Trading-Service.</summary>
    private void UpdateRollingMetrics()
    {
        // RiskManager des aktiven Services (Multi-TF Standalone: ein Service)
        Engine.Risk.RiskManager? rm;
        if (IsPaperMode)
            rm = _paperService.RiskManager;
        else
            rm = _liveManager.Service?.RiskManager;

        if (rm == null) return;

        // Daten auf dem Timer-Thread vorbereiten (Snapshots erstellen, nicht das Dictionary direkt mutieren)
        var winRate = rm.RollingWinRate * 100m;
        var sharpe = rm.RollingSharpeRatio;
        var profitFactor = rm.RollingProfitFactor;
        var health = rm.CheckStrategyHealth();

        // DailyPnl aus Trades berechnen (Snapshot auf Timer-Thread, Zuweisung auf UI-Thread)
        var dailyPnlSnapshot = BuildDailyPnlSnapshot(rm);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RollingWinRate = winRate;
            RollingSharpe = sharpe;
            RollingProfitFactor = profitFactor;
            HasStrategyWarning = health != null;
            StrategyHealthText = health ?? "OK";

            DailyPnl = dailyPnlSnapshot.ToDictionary(x => x.Day, x => x.Pnl);
            WidgetCanvasInvalidationRequested?.Invoke();
        });
    }

    /// <summary>Erstellt einen Snapshot der täglichen PnL aus RiskManager-Trades (thread-safe).</summary>
    private static List<(DateTime Day, decimal Pnl)> BuildDailyPnlSnapshot(Engine.Risk.RiskManager rm)
    {
        try
        {
            var result = new Dictionary<DateTime, decimal>();
            foreach (var trade in rm.RecentTrades)
            {
                var day = trade.ExitTime.Date;
                result.TryGetValue(day, out var existing);
                result[day] = existing + trade.Pnl;
            }
            return result.Select(kv => (kv.Key, kv.Value)).ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Stoppt den Equity-Snapshot-Timer.
    /// </summary>
    private void StopEquitySnapshotTimer()
    {
        _equityCts?.Cancel();
        _equityCts?.Dispose();
        _equityCts = null;
        _equityTimer?.Dispose();
        _equityTimer = null;
    }

    /// <summary>Aktualisiert HasOpenPositions + PositionsStatusText + Chart-Overlay (3x genutzt).</summary>
    private void UpdatePositionsStatus()
    {
        HasOpenPositions = OpenPositions.Count > 0;
        PositionsStatusText = HasOpenPositions
            ? $"{OpenPositions.Count} offene Position{(OpenPositions.Count > 1 ? "en" : "")}"
            : "Keine offenen Positionen";
    }

    // ═══════════════════════════════════════════════════════════════
    // Remote-Mode Event-Handler (Client/Server-Architektur)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Remote: Position wurde updated (SignalR-Push vom Server).</summary>
    private void OnRemotePositionUpdated(BingXBot.Contracts.Dto.PositionDto pos)
    {
        // SignalR-Callback kann auf beliebigem Thread feuern → UI-Operationen MÜSSEN marshalled werden.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(OpenPositions));
        });
    }

    /// <summary>Remote: Equity-Snapshot vom Server (SignalR-Push).</summary>
    private void OnRemoteEquityUpdate(BingXBot.Contracts.Dto.EquityPointDto pt)
    {
        // SignalR-Callback → UI-Thread-Marshalling für Property-Setter mit Bindings.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Balance = pt.Equity;
        });
    }

    /// <summary>Remote: Bot-Status-Change (Started/Stopped/Paused). Übernimmt auch Paper/Live-Modus vom Pi.</summary>
    private void OnRemoteStatusChanged(BingXBot.Contracts.Dto.BotStatusDto status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var running = status.State == BotState.Running;
            IsRunning = running;
            CanStart = !running;  // Start-Button deaktivieren wenn Bot auf Pi bereits läuft
            BotStatusText = running
                ? (status.Mode == Core.Enums.TradingMode.Paper ? "Paper (Remote)" : "LIVE (Remote) - Handelt aktiv!")
                : status.State.ToString();
            BotStatusState = status.State;

            // BotEventBus feuern — damit MainViewModel.TradingMode + Statusleiste synchron bleiben
            _eventBus.PublishBotState(status.State);

            // Modus vom Server übernehmen (Paper/Live) — Statusleiste + interne Flags konsistent halten
            var isPaper = status.Mode == Core.Enums.TradingMode.Paper;
            if (IsPaperMode != isPaper)
            {
                IsPaperMode = isPaper;
                ModeText = isPaper ? "Paper-Modus" : "Live-Modus";
            }
            // Fix 17.04.2026: Client-lokalen BotSettings.LastMode auf Server-Authority syncen,
            // damit SettingsPersistenceService.SaveAllAsync den aktuellen Mode mitsendet und
            // nicht versehentlich den Default (Paper) ueberschreibt.
            _botSettings.LastMode = status.Mode;
            IsLiveActive = !isPaper && running;
            _eventBus.PublishTradingMode(isPaper);

            // Welcome-Hint ausblenden wenn Bot aktiv
            if (running) ShowWelcomeHint = false;
        });
    }

    /// <summary>Remote: Polling-Loop für Account-Snapshot + Status (alle 5s).</summary>
    private async Task StartRemoteAccountPollingAsync()
    {
        _remoteAccountPollCts = new CancellationTokenSource();
        var ct = _remoteAccountPollCts.Token;

        // Initialen Status sofort holen (nicht auf ersten SignalR-Push warten)
        try
        {
            var initialStatus = await _botControl.GetStatusAsync(ct).ConfigureAwait(false);
            if (initialStatus != null) OnRemoteStatusChanged(initialStatus);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Remote",
                $"Initialer Status-Call fehlgeschlagen (Retry im Poll): {ex.Message}"));
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var snap = await _accountService.GetSnapshotAsync(ct).ConfigureAwait(false);
                    if (snap != null)
                    {
                        Balance = snap.Balance;
                        AvailableBalance = snap.Available;
                        UnrealizedPnl = snap.UnrealizedPnl;
                        TotalPnl = snap.RealizedPnlToday;  // heute realisierter PnL
                        // "Bot starten um Account-Daten zu sehen"-Hinweis ausblenden sobald echte Daten da sind
                        if (Balance > 0 || AvailableBalance > 0) HasAccountData = true;
                    }

                    // Status ebenfalls im Poll-Zyklus nachziehen (deckt verpasste SignalR-Pushes ab)
                    var status = await _botControl.GetStatusAsync(ct).ConfigureAwait(false);
                    if (status != null) OnRemoteStatusChanged(status);

                    // Offene Positionen holen — SignalR pusht nur bei Änderung, beim App-Start sind sonst keine da
                    var positions = await _accountService.GetPositionsAsync(ct).ConfigureAwait(false);
                    if (positions != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Nur neu aufbauen wenn Anzahl oder Keys unterschiedlich sind (verhindert Flackern)
                            var newKeys = positions.Select(p => $"{p.Symbol}_{p.Side}").OrderBy(k => k).ToList();
                            var oldKeys = OpenPositions.Select(p => $"{p.Symbol}_{p.Side}").OrderBy(k => k).ToList();
                            if (!newKeys.SequenceEqual(oldKeys))
                            {
                                OpenPositions.Clear();
                                foreach (var p in positions)
                                {
                                    OpenPositions.Add(new PositionDisplayItem
                                    {
                                        Symbol = p.Symbol,
                                        Side = p.Side,
                                        EntryPrice = p.EntryPrice,
                                        MarkPrice = p.MarkPrice,
                                        Quantity = p.Quantity,
                                        Pnl = p.UnrealizedPnl,
                                        Leverage = p.Leverage,
                                        StopLoss = p.StopLoss,
                                        TakeProfit = p.TakeProfit,
                                        LiquidationPrice = p.LiquidationPrice ?? 0m
                                    });
                                }
                                HasOpenPositions = OpenPositions.Count > 0;
                            }
                            else
                            {
                                // Gleiche Keys — nur Preise/PnL aktualisieren (keine Clear/Add)
                                foreach (var p in positions)
                                {
                                    var item = OpenPositions.FirstOrDefault(x => x.Symbol == p.Symbol && x.Side == p.Side);
                                    if (item == null) continue;
                                    item.MarkPrice = p.MarkPrice;
                                    item.Pnl = p.UnrealizedPnl;
                                    item.StopLoss = p.StopLoss;
                                    item.TakeProfit = p.TakeProfit;
                                }
                            }
                        });
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException)
                {
                    // Nur Netzwerk-Fehler schlucken — echte Bugs sollen durchschlagen
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Remote",
                        $"Poll-Fehler, Retry in 5s: {ex.Message}"));
                }

                await Task.Delay(5000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* Stopp */ }
    }
}
