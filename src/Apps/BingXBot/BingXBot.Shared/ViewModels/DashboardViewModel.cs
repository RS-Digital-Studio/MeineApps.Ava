using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Strategies;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly AdaptiveTradingIntelligence? _ati;
    private readonly BotSettings _botSettings;
    private bool _isInitializing = true; // Unterdrückt Preset-Override beim Konstruktor
    private PeriodicTimer? _equityTimer;
    private PeriodicTimer? _accountUpdateTimer;

    // === Live-Trading (delegiert an LiveTradingManager) ===
    private readonly LiveTradingManager _liveManager;
    // === Multi-Mode Orchestrator (alle 3 Modi gleichzeitig) ===
    private readonly MultiModeOrchestrator _orchestrator;
    private bool _isMultiMode;
    // CancellationToken für Account-Update-Timer (wird bei Stop gecancelled)
    private CancellationTokenSource? _accountUpdateCts;

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

    // === Strategie-Auswahl + Trading-Modus (direkt im Dashboard) ===
    [ObservableProperty] private string _selectedStrategy = "CryptoTrendPro";
    [ObservableProperty] private string _strategyDescription = "";
    [ObservableProperty] private string _selectedTradingMode = "Swing";
    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;
    public string[] AvailableTradingModes => ["Scalping", "Day-Trading", "Swing", "Alle Modi"];

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
    partial void OnBotStatusStateChanged(BotState value) => OnPropertyChanged(nameof(StatusDotColor));

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
    public Dictionary<DateTime, decimal> DailyPnl { get; } = new();
    // Korrelations-Matrix: Symbole + Matrix
    public string[] CorrelationSymbols { get; set; } = [];
    public float[,] CorrelationMatrix { get; set; } = new float[0, 0];
    // Strategie-Gewichte (aus ATI AdaptiveEnsemble)
    public List<(string Name, decimal Weight)> StrategyWeights { get; set; } = [];

    /// <summary>
    /// Wird ausgeloest wenn Widget-Daten (DailyPnl, StrategyWeights, FearGreed) aktualisiert wurden
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

    public DashboardViewModel(
        BotEventBus eventBus,
        StrategyManager strategyManager,
        PaperTradingService paperService,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotSettings botSettings,
        LiveTradingManager liveManager,
        MultiModeOrchestrator orchestrator,
        IPublicMarketDataClient? publicClient = null,
        BotDatabaseService? dbService = null,
        ISecureStorageService? secureStorage = null,
        AdaptiveTradingIntelligence? ati = null)
    {
        _eventBus = eventBus;
        _strategyManager = strategyManager;
        _paperService = paperService;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _botSettings = botSettings;
        _liveManager = liveManager;
        _orchestrator = orchestrator;
        _publicClient = publicClient;
        _dbService = dbService;
        _secureStorage = secureStorage;
        _ati = ati;

        // Sub-ViewModels erstellen
        BtcTicker = new BtcTickerViewModel(publicClient, eventBus);
        Activity = new ActivityFeedViewModel(eventBus);

        // Keine Fake-Daten! Zeige ehrlichen Zustand.
        HasAccountData = false;
        HasOpenPositions = false;

        // API-Key-Status prüfen
        HasApiKeys = _secureStorage?.HasCredentials ?? false;
        if (HasApiKeys)
            LiveStatusText = "API-Keys vorhanden";

        // Letzte Strategie + Trading-Modus aus persistierten Settings laden
        if (!string.IsNullOrEmpty(_botSettings.LastStrategyName))
            SelectedStrategy = _botSettings.LastStrategyName;
        OnSelectedStrategyChanged(SelectedStrategy);

        SelectedTradingMode = _botSettings.LastTradingModePreset switch
        {
            Core.Enums.TradingModePreset.Scalping => "Scalping",
            Core.Enums.TradingModePreset.DayTrading => "Day-Trading",
            Core.Enums.TradingModePreset.Custom => "Alle Modi",
            _ => "Swing"
        };

        // Initiale Watchlist aus ScannerSettings laden (falls vorher gesetzt)
        foreach (var sym in _scannerSettings.Whitelist)
            WatchlistSymbols.Add(sym);
        IsWatchlistActive = WatchlistSymbols.Count > 0;

        // Verfügbare Symbole im Hintergrund laden (für AutoComplete)
        _ = LoadAvailableSymbolsAsync();

        // Trade-Markers: Bei Trade-Abschluss BTC-Marker hinzufügen
        _eventBus.TradeCompleted += (_, trade) =>
        {
            if (trade.Symbol != "BTC-USDT") return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                BtcTicker.TradeMarkers.Add(new TradeMarker(trade.EntryTime, trade.EntryPrice, trade.Side, true));
                BtcTicker.TradeMarkers.Add(new TradeMarker(trade.ExitTime, trade.ExitPrice, trade.Side, false, trade.Pnl));
                // Max 50 Marker behalten
                while (BtcTicker.TradeMarkers.Count > 50)
                    BtcTicker.TradeMarkers.RemoveAt(0);
            });
        };

        _isInitializing = false; // Ab jetzt überschreiben Modus-Wechsel die Settings
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        try
        {
            var strategy = StrategyFactory.Create(value);
            StrategyDescription = strategy.Description;
            _botSettings.LastStrategyName = value;
            _ = App.SaveAllSettingsAsync();
        }
        catch
        {
            StrategyDescription = "";
        }
    }

    partial void OnSelectedTradingModeChanged(string value)
    {
        var preset = value switch
        {
            "Scalping" => Core.Enums.TradingModePreset.Scalping,
            "Day-Trading" => Core.Enums.TradingModePreset.DayTrading,
            "Alle Modi" => Core.Enums.TradingModePreset.Custom, // Custom = alle 3 Modi parallel
            _ => Core.Enums.TradingModePreset.Swing
        };

        _botSettings.LastTradingModePreset = preset;

        // Scanner-Settings IMMER aus dem Preset setzen (Timeframe MUSS zum Modus passen)
        var scannerPresetInit = Core.Configuration.TradingModeDefaults.GetScannerPreset(preset);
        _scannerSettings.ScanTimeFrame = scannerPresetInit.ScanTimeFrame;
        _scannerSettings.UseM15EntryTiming = scannerPresetInit.UseM15EntryTiming;

        // Beim App-Start: Risk-Settings (Leverage, PositionSize) NICHT überschreiben
        // (die wurden aus der DB geladen und enthalten User-Anpassungen)
        if (_isInitializing)
        {
            _ = App.SaveAllSettingsAsync();
            return;
        }

        // Bei manuellem Modus-Wechsel im UI: ALLE Settings aus Preset anwenden
        var riskPreset = Core.Configuration.TradingModeDefaults.GetRiskPreset(preset);
        _riskSettings.MaxPositionSizePercent = riskPreset.MaxPositionSizePercent;
        _riskSettings.MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent;
        _riskSettings.MaxLeverage = riskPreset.MaxLeverage;
        _riskSettings.CooldownHours = riskPreset.CooldownHours;
        _riskSettings.MaxCooldownHours = riskPreset.MaxCooldownHours;
        _riskSettings.MaxHoldHours = riskPreset.MaxHoldHours;
        _riskSettings.MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1;
        _riskSettings.Tp1CloseRatio = riskPreset.Tp1CloseRatio;
        _riskSettings.Tp2CloseRatio = riskPreset.Tp2CloseRatio;
        _riskSettings.SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier;
        _riskSettings.MinRiskRewardRatio = riskPreset.MinRiskRewardRatio;

        var scannerPreset = Core.Configuration.TradingModeDefaults.GetScannerPreset(preset);
        _scannerSettings.ScanTimeFrame = scannerPreset.ScanTimeFrame;
        _scannerSettings.MinVolume24h = scannerPreset.MinVolume24h;
        _scannerSettings.MinPriceChange = scannerPreset.MinPriceChange;
        _scannerSettings.MaxResults = scannerPreset.MaxResults;
        _scannerSettings.UseM15EntryTiming = scannerPreset.UseM15EntryTiming;

        _botSettings.LastTradingModePreset = preset;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Trading-Modus gewechselt: {value} ({scannerPreset.ScanTimeFrame}, Risiko {riskPreset.MaxPositionSizePercent}%, Hebel {riskPreset.MaxLeverage}x)"));

        _ = App.SaveAllSettingsAsync();
    }

    [RelayCommand]
    private async Task StartBot()
    {
        if (IsPaperMode)
        {
            await StartPaperTradingAsync();
        }
        else
        {
            // Live-Trading erfordert explizite Bestätigung
            ConfirmDialogTitle = "Live-Trading starten?";
            ConfirmDialogMessage = "Du bist dabei, den Bot mit ECHTEM GELD zu starten.\n\nDer Bot wird automatisch Trades auf BingX eröffnen und schließen. Stelle sicher, dass dein Risikomanagement korrekt konfiguriert ist.";
            _confirmDialogAction = StartLiveTradingAsync;
            ShowConfirmDialog = true;
        }
    }

    /// <summary>
    /// Startet den Paper-Trading-Modus mit simuliertem Kapital.
    /// </summary>
    private async Task StartPaperTradingAsync()
    {
        // "Alle Modi": 3 Services parallel über den Orchestrator starten
        if (_botSettings.LastTradingModePreset == Core.Enums.TradingModePreset.Custom)
        {
            _isMultiMode = true;

            // ATI-Events verdrahten + State laden (gleich wie Single-Mode)
            await WireUpAtiEventsAsync();

            _orchestrator.StartPaper(_botSettings.PaperInitialBalance);
            BotStatusText = "Paper (Alle Modi)";
            BotStatusState = BotState.Running;
            IsRunning = true;
            CanStart = false;
            ShowWelcomeHint = false;
            HasAccountData = true;
            Balance = _botSettings.PaperInitialBalance;
            AvailableBalance = _botSettings.PaperInitialBalance;
            UnrealizedPnl = 0m;
            TotalPnl = 0m;
            _eventBus.PublishBotState(BotState.Running);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                "Alle 3 Modi gestartet: Scalping (M15/90s) + Day-Trading (H1/3min) + Swing (H4/5min)"));
            _ = StartEquitySnapshotTimerAsync();
            _ = StartAccountUpdateAsync();
            return;
        }

        _isMultiMode = false;
        // Strategie aktivieren + Trading-Modus-Preset anwenden
        var strategy = StrategyFactory.Create(SelectedStrategy);
        if (strategy is CryptoTrendProStrategy ctp)
        {
            var preset = _botSettings.LastTradingModePreset;
            ctp.ApplyPreset(preset);
        }
        _strategyManager.SetStrategy(strategy);

        // ATI: Alle Strategien im Ensemble registrieren und an Service übergeben
        if (_ati != null)
        {
            _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
            _paperService.ATI = _ati;
            await WireUpAtiEventsAsync();
        }

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Strategie: {SelectedStrategy}"));

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
        _ = StartAccountUpdateAsync();
    }

    /// <summary>
    /// Startet den Live-Trading-Modus. Bei Custom-Preset (Alle Modi) werden 3 parallele
    /// LiveTradingServices über den Orchestrator gestartet, sonst ein einzelner über den LiveTradingManager.
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

            // Multi-Mode Live: 3 parallele LiveTradingServices (Scalping + DayTrading + Swing)
            if (_botSettings.LastTradingModePreset == Core.Enums.TradingModePreset.Custom)
            {
                _isMultiMode = true;
                await WireUpAtiEventsAsync();
                _orchestrator.StartLive(_liveManager.RestClient!);

                BotStatusText = "LIVE (Alle Modi) - Handelt aktiv!";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                    "LIVE MULTI-MODE: Scalping (M15/90s) + Day-Trading (H1/3min) + Swing (H4/5min) - Echtes Geld!"));
            }
            else
            {
                _isMultiMode = false;
                await _liveManager.StartAsync(SelectedStrategy);

                if (result.Positions.Count > 0)
                    await _liveManager.RestorePositionSignalsAsync(result.Positions);

                BotStatusText = "LIVE - Handelt aktiv!";
            }

            IsRunning = true;
            CanStart = false;
            BotStatusState = BotState.Running;
            ShowWelcomeHint = false;
            LiveStatusText = "Handelt aktiv";
            IsLiveActive = true;

            _ = StartEquitySnapshotTimerAsync();
            _ = StartAccountUpdateAsync();
        }
        catch (Exception ex)
        {
            BotStatusText = ex.Message.Contains("API-Keys") ? ex.Message : "Verbindung fehlgeschlagen";
            BotStatusState = BotState.Error;
            CanStart = true;
            _isMultiMode = false;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"Live-Trading fehlgeschlagen: {ex.Message}"));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        // Multi-Mode: Alle Services pausieren/fortsetzen
        if (_isMultiMode)
        {
            if (_orchestrator.IsAnyPaused)
            {
                _orchestrator.ResumeAll();
                BotStatusText = IsPaperMode ? "Paper (Alle Modi)" : "LIVE (Alle Modi) - Handelt aktiv!";
                BotStatusState = BotState.Running;
                _ = StartAccountUpdateAsync();
            }
            else
            {
                _orchestrator.PauseAll();
                _accountUpdateTimer?.Dispose();
                _accountUpdateTimer = null;
                BotStatusText = IsPaperMode ? "Pausiert (Alle Modi)" : "LIVE (Alle Modi) - Pausiert";
                BotStatusState = BotState.Paused;
            }
            return;
        }

        if (!IsPaperMode && _liveManager.Service != null)
        {
            // Live Single-Modus: Pause/Resume über LiveTradingService
            if (_liveManager.Service!.IsPaused)
            {
                _liveManager.Service!.Resume();
                BotStatusText = "LIVE - Handelt aktiv!";
                BotStatusState = BotState.Running;
                _ = StartAccountUpdateAsync();
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

        if (_isMultiMode)
        {
            await _liveManager.SaveAtiStateAsync();
            await _orchestrator.StopAllAsync();
            _isMultiMode = false;
            if (!IsPaperMode)
            {
                LiveStatusText = "Getrennt";
                IsLiveActive = false;
            }
        }
        else if (IsPaperMode)
        {
            await _liveManager.SaveAtiStateAsync();
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

        if (_isMultiMode && !IsPaperMode)
        {
            // Live Multi-Mode: Alle 3 Services Emergency-Stop
            await _orchestrator.EmergencyStopAllAsync();
            _isMultiMode = false;
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
        }
        else if (_isMultiMode)
        {
            // Paper Multi-Mode
            await _orchestrator.EmergencyStopAllAsync();
            _isMultiMode = false;
        }
        else if (IsPaperMode)
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
            if (IsPaperMode && _isMultiMode)
            {
                // Paper Multi-Mode: Position in allen Services suchen und schließen
                foreach (var service in _orchestrator.ActiveServices.Values.OfType<PaperTradingService>())
                {
                    if (service.Exchange == null) continue;
                    var pos = (await service.Exchange.GetPositionsAsync()).FirstOrDefault(p => p.Symbol == position.Symbol && p.Side == side);
                    if (pos != null)
                    {
                        service.Exchange.SetCurrentPrice(position.Symbol, position.MarkPrice);
                        await service.Exchange.ClosePositionAsync(position.Symbol, side);
                        service.RemovePositionSignal(position.Symbol, side);
                        break;
                    }
                }
            }
            else if (IsPaperMode && _paperService.Exchange != null)
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
                _liveManager.Service?.RemovePositionSignal(position.Symbol, side);

                // CompletedTrade erstellen damit ATI + RiskManager Feedback bekommen
                var fee = qty * entryPrice * 0.0005m + qty * exitPrice * 0.0005m;
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
        _ = App.SaveAllSettingsAsync();
    }

    /// <summary>Aktualisiert die Trade-Markers und Positions-Overlay auf dem BTC-Chart.</summary>
    private void UpdateChartOverlay()
    {
        // Aktive BTC-Position als Overlay anzeigen
        var btcPos = OpenPositions.FirstOrDefault(p => p.Symbol == "BTC-USDT");
        if (btcPos != null)
        {
            var signal = _paperService.GetPositionSignal(btcPos.Symbol, btcPos.Side);
            BtcTicker.ActiveOverlay = new ActivePositionOverlay(
                btcPos.EntryPrice, signal?.StopLoss, signal?.TakeProfit, signal?.TakeProfit2, btcPos.Side);
        }
        else
        {
            BtcTicker.ActiveOverlay = null;
        }
    }

    /// <summary>
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

        // SL/TP aus dem Service laden (programmatisch, OHNE PropertyChanged-Handler zu triggern)
        // Flag verhindert, dass die initiale Zuweisung das gespeicherte Signal überschreibt
        var suppressSlTpEvents = true;

        if (IsPaperMode)
        {
            var signal = _paperService.GetPositionSignal(p.Symbol, p.Side);
            if (signal != null)
            {
                item.StopLoss = signal.StopLoss;
                item.TakeProfit = signal.TakeProfit;
            }
        }
        else if (_liveManager.Service != null)
        {
            var signal = _liveManager.Service.GetPositionSignal(p.Symbol, p.Side);
            if (signal != null)
            {
                item.StopLoss = signal.StopLoss;
                item.TakeProfit = signal.TakeProfit;
            }
        }

        suppressSlTpEvents = false;

        // SL/TP-Änderungen an den Service zurückschreiben (NUR bei User-Edits, nicht bei programmatischer Zuweisung)
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
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
    /// Verdrahtet ATI-Events (Lernen, Training, Audit-Logging) und lädt gespeicherten State.
    /// Wird sowohl im Multi-Mode als auch im Single-Mode aufgerufen.
    /// Guard verhindert doppelte Subscription bei mehrfachem Start.
    /// </summary>
    private bool _atiEventsWired;
    private async Task WireUpAtiEventsAsync()
    {
        if (_ati == null || _atiEventsWired) return;
        _atiEventsWired = true;

        // ATI-Lernzustand aus DB laden (Training bleibt über Neustarts erhalten)
        await _liveManager.LoadAtiStateAsync();

        // Feature-Snapshots bei Trade-Close in DB speichern (für ML-Training)
        if (_dbService != null)
        {
            _ati.FeatureSnapshotCompleted += async (snapshot, trade, vote) =>
            {
                try
                {
                    var entity = Core.Data.FeatureSnapshotEntity.FromSnapshot(snapshot,
                        (int)trade.Side, vote.AgreeingCount, vote.TotalCount, vote.WeightedConfidence);
                    entity.Outcome = trade.Pnl > 0 ? 1 : -1;
                    entity.Pnl = trade.Pnl;
                    entity.HoldTimeMinutes = (int)(trade.ExitTime - trade.EntryTime).TotalMinutes;
                    await _dbService.SaveFeatureSnapshotAsync(entity);
                }
                catch { /* DB-Fehler nicht an Trading-Pipeline propagieren */ }

                // Auto-Training prüfen (alle 10 Trades oder 24h)
                try
                {
                    var labeled = await _dbService.GetLabeledSnapshotsAsync(5000);
                    _ati.CheckAutoTraining(labeled);
                }
                catch { /* Training-Fehler nicht propagieren */ }
            };
        }

        // Auto-Training-Events loggen
        _ati.AutoTrainingCompleted += msg =>
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ML", msg));

        // ATI Audit-Trail: Jede Entscheidung loggen (Annahme + Ablehnung mit Grund)
        _ati.AuditCreated += audit =>
        {
            var level = audit.WasAccepted ? Core.Enums.LogLevel.Trade : Core.Enums.LogLevel.Debug;
            var status = audit.WasAccepted ? "AKZEPTIERT" : "ABGELEHNT";
            var msg = $"{audit.Symbol}: {audit.SignalDirection} {status} | " +
                $"Regime={audit.Regime} ({audit.RegimeConfidence:P0}), " +
                $"Ensemble={audit.StrategiesAgreeing}/{audit.StrategiesTotal} ({audit.AgreeingStrategies}), " +
                $"ML={audit.MlConfidence:P0}";

            if (!audit.WasAccepted && audit.RejectionReason != null)
                msg += $" | Grund: {audit.RejectionReason}";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, level, "ATI", msg, audit.Symbol));
        };

        // ONNX-Modell-Status loggen
        if (_ati.OnnxModel is { IsModelLoaded: true })
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ML",
                $"ONNX-Modell geladen: {_ati.OnnxModel.GetModelInfo()}"));

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ATI",
            $"Adaptive Trading Intelligence aktiviert ({StrategyFactory.AvailableStrategies.Length} Strategien im Ensemble)"));
    }

    /// <summary>
    /// Aktualisiert Account-Daten und offene Positionen alle 5 Sekunden.
    /// Nutzt je nach Modus die SimulatedExchange (Paper) oder den echten BingXRestClient (Live).
    /// </summary>
    private async Task StartAccountUpdateAsync()
    {
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
                        // Live-Modus (Single + Multi): Echte Daten von BingX
                        account = await _liveManager.RestClient!.GetAccountInfoAsync();
                        positions = await _liveManager.RestClient!.GetPositionsAsync();
                    }
                    else if (IsPaperMode && _isMultiMode)
                    {
                        // Paper Multi-Mode: Aggregierte Daten aus allen 3 Services
                        var result = await _orchestrator.GetAggregatedPaperAccountAsync();
                        if (result != null)
                        {
                            account = result.Value.Account;
                            positions = result.Value.Positions;
                        }
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
        if (_dbService == null) return;

        StopEquitySnapshotTimer();
        _equityTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            // Ersten Snapshot sofort speichern
            await SaveEquitySnapshotAsync();

            while (await _equityTimer.WaitForNextTickAsync())
                await SaveEquitySnapshotAsync();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Speichert einen einzelnen Equity-Snapshot in der DB.
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
        // RiskManager des aktiven Modus verwenden (Paper oder Live)
        var rm = IsPaperMode ? _paperService.RiskManager : _liveManager.Service?.RiskManager;
        if (rm == null) return;

        // Daten auf dem Timer-Thread vorbereiten (Snapshots erstellen, nicht das Dictionary direkt mutieren)
        var winRate = rm.RollingWinRate * 100m;
        var sharpe = rm.RollingSharpeRatio;
        var profitFactor = rm.RollingProfitFactor;
        var health = rm.CheckStrategyHealth();

        // DailyPnl aus Trades berechnen (Snapshot auf Timer-Thread, Zuweisung auf UI-Thread)
        var dailyPnlSnapshot = BuildDailyPnlSnapshot(rm);

        // ATI Strategie-Gewichte berechnen (Snapshot)
        var weightsSnapshot = BuildStrategyWeightsSnapshot();

        // Fear & Greed Index vom TradingService propagieren
        var fgValue = GetFearGreedValueFromService();
        var fgLabel = GetFearGreedLabelFromValue(fgValue);

        // ALLE Mutationen auf dem UI-Thread (DailyPnl + StrategyWeights werden von Renderern gelesen)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RollingWinRate = winRate;
            RollingSharpe = sharpe;
            RollingProfitFactor = profitFactor;
            HasStrategyWarning = health != null;
            StrategyHealthText = health ?? "OK";

            // DailyPnl Dictionary aktualisieren (thread-safe, da jetzt auf UI-Thread)
            DailyPnl.Clear();
            foreach (var (day, pnl) in dailyPnlSnapshot)
                DailyPnl[day] = pnl;

            // StrategyWeights aktualisieren
            StrategyWeights = weightsSnapshot;

            // Fear & Greed Index ans BtcTicker-SubVM propagieren
            BtcTicker.FearGreedValue = fgValue;
            BtcTicker.FearGreedLabel = fgLabel;

            // Widget-Canvases muessen invalidiert werden, da DailyPnl/StrategyWeights/FearGreed
            // keine ObservableProperties sind und kein CollectionChanged feuern.
            // (EquityData.CollectionChanged invalidiert zwar auch, aber das Post laeuft
            // in einem separaten Dispatcher-Tick VOR dieser Mutation.)
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

    /// <summary>Erstellt einen Snapshot der ATI-Strategie-Gewichte (thread-safe).</summary>
    private List<(string Name, decimal Weight)> BuildStrategyWeightsSnapshot()
    {
        if (_ati is not { IsEnabled: true }) return [];
        try
        {
            var regime = _ati.RegimeDetector.CurrentRegime;
            var raw = _ati.Ensemble.GetStrategyWeights(regime);
            return raw.Select(kv => (kv.Key, (decimal)kv.Value.Weight)).ToList();
        }
        catch { return []; }
    }

    /// <summary>Holt den aktuellen Fear & Greed Wert vom TradingService (0-100 Skala).</summary>
    private float GetFearGreedValueFromService()
    {
        // TradingServiceBase cached den Wert als [0,1]-normalisiert, wir brauchen 0-100
        if (IsPaperMode)
            return _paperService.CachedFearGreedIndex * 100f;
        // Live: TradingServiceBase des LiveTradingService
        if (_liveManager.Service is { } live)
            return live.CachedFearGreedIndex * 100f;
        return 0f;
    }

    private static string GetFearGreedLabelFromValue(float value) => value switch
    {
        <= 0 => "n/a", // Noch keine Daten
        < 25 => "Extreme Fear",
        < 45 => "Fear",
        < 55 => "Neutral",
        < 75 => "Greed",
        _ => "Extreme Greed"
    };

    /// <summary>
    /// Stoppt den Equity-Snapshot-Timer.
    /// </summary>
    private void StopEquitySnapshotTimer()
    {
        _equityTimer?.Dispose();
        _equityTimer = null;
    }

    /// <summary>Aktualisiert HasOpenPositions + PositionsStatusText + Chart-Overlay (3x genutzt).</summary>
    private void UpdatePositionsStatus()
    {
        HasOpenPositions = OpenPositions.Count > 0;
        PositionsStatusText = HasOpenPositions
            ? $"{OpenPositions.Count} offene Position(en)"
            : "Keine offenen Positionen";
        UpdateChartOverlay();
    }
}

/// <summary>
/// Anzeige-Modell fuer eine offene Position im Dashboard.
/// ObservableObject damit editierbare SL/TP/Trailing-Felder korrekt binden.
/// </summary>
public partial class PositionDisplayItem : ObservableObject
{
    // Basis-Daten (werden bei jedem Account-Update gesetzt)
    [ObservableProperty] private string _symbol = "";
    [ObservableProperty] private Side _side;
    [ObservableProperty] private decimal _entryPrice;
    [ObservableProperty] private decimal _markPrice;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _pnl;
    [ObservableProperty] private decimal _leverage;

    // SL/TP/Trailing (editierbar vom User)
    [ObservableProperty] private decimal? _stopLoss;
    [ObservableProperty] private decimal? _takeProfit;
    [ObservableProperty] private decimal? _trailingStop; // In Prozent

    // Berechnete Properties
    public bool IsProfit => Pnl > 0;
    public string PnlColor => Pnl >= 0 ? "#10B981" : "#EF4444";
    public string SideText => Side.ToString();
    public string SideColor => Side == Side.Buy ? "#10B981" : "#EF4444";
    public string PnlText => $"{Pnl:+0.00;-0.00}";
    public decimal PnlPercent => EntryPrice > 0 && Quantity > 0
        ? (MarkPrice - EntryPrice) / EntryPrice * 100m * (Side == Side.Buy ? 1 : -1)
        : 0m;
    public string PnlPercentText => $"{PnlPercent:+0.00;-0.00}%";

    // Fuer den Key im PaperTradingService/LiveTradingService
    public string PositionKey => $"{Symbol}_{Side}";

    // Close-Action: Wird vom DashboardViewModel gesetzt
    public Func<PositionDisplayItem, Task>? CloseRequested { get; set; }

    [RelayCommand]
    private async Task RequestClose()
    {
        if (CloseRequested != null)
            await CloseRequested(this);
    }

    // Benachrichtigt berechnete Properties bei Pnl/Side/Price-Aenderungen
    partial void OnPnlChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsProfit));
        OnPropertyChanged(nameof(PnlColor));
        OnPropertyChanged(nameof(PnlText));
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnSideChanged(Side value)
    {
        OnPropertyChanged(nameof(SideText));
        OnPropertyChanged(nameof(SideColor));
        OnPropertyChanged(nameof(PositionKey));
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnMarkPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }

    partial void OnEntryPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(PnlPercent));
        OnPropertyChanged(nameof(PnlPercentText));
    }
}
