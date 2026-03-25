using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
public partial class DashboardViewModel : ObservableObject, IDisposable
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
    private PeriodicTimer? _equityTimer;
    private PeriodicTimer? _accountUpdateTimer;

    // === Live-Trading Client + Service (erstellt zur Laufzeit mit API-Keys) ===
    private BingXRestClient? _liveClient;
    private LiveTradingService? _liveService;

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

    // === Strategie-Auswahl (direkt im Dashboard) ===
    [ObservableProperty] private string _selectedStrategy = "Trend-Following";
    [ObservableProperty] private string _strategyDescription = "";
    public string[] AvailableStrategies => StrategyFactory.AvailableStrategies;

    // === Account (nur anzeigen wenn Daten vorhanden) ===
    [ObservableProperty] private bool _hasAccountData;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private decimal _availableBalance;
    [ObservableProperty] private decimal _unrealizedPnl;
    [ObservableProperty] private decimal _totalPnl;

    // === Offene Positionen ===
    public ObservableCollection<PositionDisplayItem> OpenPositions { get; } = new();
    [ObservableProperty] private bool _hasOpenPositions;
    [ObservableProperty] private string _positionsStatusText = "Keine offenen Positionen";

    // === Equity-Kurve ===
    public ObservableCollection<EquityPoint> EquityData { get; } = new();

    // === Bestätigungs-Dialog ===
    [ObservableProperty] private bool _showConfirmDialog;
    [ObservableProperty] private string _confirmDialogTitle = "";
    [ObservableProperty] private string _confirmDialogMessage = "";
    private Func<Task>? _confirmDialogAction;

    // === Hinweise/Onboarding ===
    [ObservableProperty] private bool _showWelcomeHint = true;
    [ObservableProperty] private string _welcomeHintText = "Willkommen! Starte mit einem Backtest um eine Strategie zu testen, oder konfiguriere deine API-Keys in den Einstellungen.";

    public DashboardViewModel(
        BotEventBus eventBus,
        StrategyManager strategyManager,
        PaperTradingService paperService,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
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

        // Initiale Strategie-Beschreibung laden
        OnSelectedStrategyChanged(SelectedStrategy);
    }

    partial void OnSelectedStrategyChanged(string value)
    {
        try
        {
            var strategy = StrategyFactory.Create(value);
            StrategyDescription = strategy.Description;
        }
        catch
        {
            StrategyDescription = "";
        }
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
            await StartLiveTradingAsync();
        }
    }

    /// <summary>
    /// Startet den Paper-Trading-Modus mit simuliertem Kapital.
    /// </summary>
    private async Task StartPaperTradingAsync()
    {
        // Strategie aktivieren
        var strategy = StrategyFactory.Create(SelectedStrategy);
        _strategyManager.SetStrategy(strategy);

        // ATI: Alle Strategien im Ensemble registrieren und an Service übergeben
        if (_ati != null)
        {
            _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
            _paperService.ATI = _ati;

            // ATI-Lernzustand aus DB laden (Training bleibt über Neustarts erhalten)
            await LoadAtiStateAsync();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ATI",
                $"Adaptive Trading Intelligence aktiviert ({StrategyFactory.AvailableStrategies.Length} Strategien im Ensemble)"));
        }

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Strategie: {SelectedStrategy}"));

        // Paper-Trading-Service starten
        _paperService.Start(10_000m);

        IsRunning = true;
        CanStart = false;
        BotStatusText = "Laeuft (Paper)";
        BotStatusState = BotState.Running;
        ShowWelcomeHint = false;

        // Account-Daten anzeigen
        HasAccountData = true;
        Balance = 10_000m;
        AvailableBalance = 10_000m;
        UnrealizedPnl = 0m;
        TotalPnl = 0m;

        // Equity-Snapshots alle 5 Minuten in DB persistieren
        _ = StartEquitySnapshotTimerAsync();

        // Account-Update Timer starten (alle 5 Sekunden)
        _ = StartAccountUpdateAsync();
    }

    /// <summary>
    /// Startet den Live-Trading-Modus: Verbindet sich mit BingX, erstellt LiveTradingService
    /// und handelt automatisch mit echtem Geld.
    /// </summary>
    private async Task StartLiveTradingAsync()
    {
        // API-Keys prüfen
        if (_secureStorage == null || !_secureStorage.HasCredentials)
        {
            BotStatusText = "API-Keys fehlen";
            BotStatusState = BotState.Error;
            WelcomeHintText = "Gehe zu Einstellungen und hinterlege deine BingX API-Keys für Live-Trading.";
            ShowWelcomeHint = true;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "Live-Trading erfordert API-Keys. Bitte in den Einstellungen konfigurieren."));
            return;
        }

        // Credentials laden
        var creds = await _secureStorage.LoadCredentialsAsync();
        if (creds == null)
        {
            BotStatusText = "API-Keys konnten nicht geladen werden";
            BotStatusState = BotState.Error;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                "API-Keys konnten nicht entschlüsselt werden. Bitte erneut eingeben."));
            return;
        }

        // BingXRestClient erstellen + Verbindungstest
        BotStatusText = "Verbinde mit BingX...";
        BotStatusState = BotState.Starting;
        CanStart = false;

        try
        {
            // Eigener HttpClient für Live-Client (teilt nicht den globalen mit PublicClient,
            // da Timeout-Änderungen sonst alle Requests betreffen)
            var httpClient = new HttpClient();
            var liveRateLimiter = new RateLimiter();
            var logger = NullLogger<BingXRestClient>.Instance;
            _liveClient = new BingXRestClient(creds.Value.ApiKey, creds.Value.ApiSecret, httpClient, liveRateLimiter, logger);

            // Verbindung testen: Account-Info abrufen
            var account = await _liveClient.GetAccountInfoAsync();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Balance = account.Balance;
                AvailableBalance = account.AvailableBalance;
                UnrealizedPnl = account.UnrealizedPnl;
                TotalPnl = account.RealizedPnl + account.UnrealizedPnl;
                HasAccountData = true;
            });

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                $"Live-Verbindung hergestellt. Balance: {account.Balance:N2} USDT"));

            // Offene Positionen laden
            var positions = await _liveClient.GetPositionsAsync();

            // Positionen als Items erstellen (auf Background-Thread, damit API-Calls nicht UI blockieren)
            var posItems = positions.Select(p => CreatePositionItem(p)).ToList();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OpenPositions.Clear();
                foreach (var item in posItems)
                    OpenPositions.Add(item);
                UpdatePositionsStatus();
            });

            if (positions.Count > 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    $"{positions.Count} offene Position(en) gefunden"));
            }

            // PublicClient wird benoetigt fuer Marktdaten-Scans im Live-Trading
            if (_publicClient == null)
            {
                BotStatusText = "Marktdaten-Client nicht verfuegbar";
                BotStatusState = BotState.Error;
                CanStart = true;
                _liveClient = null;
                return;
            }

            // LiveTradingService erstellen
            _liveService = new LiveTradingService(
                _liveClient,
                _publicClient,
                _strategyManager,
                _riskSettings,
                _scannerSettings,
                _eventBus);

            // Strategie aktivieren
            var strategy = StrategyFactory.Create(SelectedStrategy);
            _strategyManager.SetStrategy(strategy);

            // ATI: Alle Strategien im Ensemble registrieren und an Service übergeben
            if (_ati != null)
            {
                _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
                _liveService.ATI = _ati;

                // ATI-Lernzustand aus DB laden (Training bleibt über Neustarts erhalten)
                await LoadAtiStateAsync();

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ATI",
                    $"Adaptive Trading Intelligence aktiviert ({StrategyFactory.AvailableStrategies.Length} Strategien im Ensemble)"));
            }

            // WARNUNG im Activity-Feed
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                $"LIVE-TRADING AKTIV mit Strategie '{SelectedStrategy}'. Echtes Geld!"));

            // Live-Trading starten
            _liveService.Start();

            // Bestehende Positionen mit SL/TP-Orders abgleichen und im Service registrieren
            if (positions.Count > 0)
            {
                await RestorePositionSignalsAsync(positions);
            }

            IsRunning = true;
            CanStart = false;
            BotStatusText = "LIVE - Handelt aktiv!";
            BotStatusState = BotState.Running; // Live-Trading aktiv
            ShowWelcomeHint = false;
            LiveStatusText = "Handelt aktiv";
            IsLiveActive = true;

            // Account-Update Timer starten (alle 5 Sekunden echte Daten)
            _ = StartAccountUpdateAsync();
        }
        catch (Exception ex)
        {
            BotStatusText = "Verbindung fehlgeschlagen";
            BotStatusState = BotState.Error;
            CanStart = true;
            _liveClient = null;
            _liveService = null;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"Live-Verbindung fehlgeschlagen: {ex.Message}"));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        if (!IsPaperMode && _liveService != null)
        {
            // Live-Modus: Pause/Resume über LiveTradingService
            if (_liveService.IsPaused)
            {
                _liveService.Resume();
                BotStatusText = "LIVE - Handelt aktiv!";
                BotStatusState = BotState.Running;
                _ = StartAccountUpdateAsync();
            }
            else
            {
                _liveService.Pause();
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
            BotStatusText = "Laeuft (Paper)";
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
        // ATI-Lernzustand in DB speichern bevor der Service gestoppt wird
        await SaveAtiStateAsync();

        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        if (IsPaperMode)
        {
            await _paperService.StopAsync();
        }
        else
        {
            // Live-Modus: LiveTradingService stoppen + Client freigeben
            if (_liveService != null)
            {
                await _liveService.StopAsync();
                _liveService.Dispose();
                _liveService = null;
            }
            _liveClient = null;
            LiveStatusText = "Getrennt";
            IsLiveActive = false;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                "Live-Trading gestoppt. Offene Positionen bleiben auf BingX bestehen."));
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
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        if (IsPaperMode)
        {
            await _paperService.EmergencyStopAsync();
        }
        else
        {
            // Live-Modus: LiveTradingService Notfall-Stop (schließt ALLE echten Positionen!)
            if (_liveService != null)
            {
                await _liveService.EmergencyStopAsync();
                _liveService.Dispose();
                _liveService = null;
            }
            _liveClient = null;
            LiveStatusText = "Notfall-Stop";
            IsLiveActive = false;
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgefuehrt";
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
        _liveClient = null;
        IsLiveActive = false;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Modus gewechselt zu: {ModeText}"));
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
            else if (!IsPaperMode && _liveClient != null)
            {
                // Position-Daten für CompletedTrade merken
                var entryPrice = position.EntryPrice;
                var exitPrice = position.MarkPrice;
                var qty = position.Quantity;

                await _liveClient.ClosePositionAsync(position.Symbol, side);
                _liveService?.RemovePositionSignal(position.Symbol, side);

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
        else if (_liveService != null)
        {
            var signal = _liveService.GetPositionSignal(p.Symbol, p.Side);
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
                _liveService?.UpdatePositionSignal(item.Symbol, side, item.StopLoss, item.TakeProfit);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Trade",
                $"{item.Symbol}: SL={item.StopLoss?.ToString("G8") ?? "---"} / TP={item.TakeProfit?.ToString("G8") ?? "---"}", item.Symbol));
        };

        return item;
    }

    /// <summary>
    /// Stellt SL/TP-Signale für bestehende Positionen wieder her (nach App-Neustart).
    /// Liest offene Conditional Orders (STOP_MARKET / TAKE_PROFIT_MARKET) von BingX
    /// und registriert sie im LiveTradingService für clientseitiges Monitoring + Trailing-Stop.
    /// </summary>
    private async Task RestorePositionSignalsAsync(IReadOnlyList<Position> positions)
    {
        if (_liveClient == null || _liveService == null) return;

        try
        {
            // Alle offenen Orders holen (inkl. SL/TP Conditional Orders)
            var openOrders = await _liveClient.GetOpenOrdersAsync();

            var restored = 0;
            foreach (var pos in positions)
            {
                // SL/TP für diese Position finden
                decimal? sl = null;
                decimal? tp = null;

                foreach (var order in openOrders)
                {
                    if (order.Symbol != pos.Symbol) continue;

                    if (order.Type == Core.Enums.OrderType.StopMarket && order.StopPrice.HasValue)
                        sl = order.StopPrice.Value;
                    else if (order.Type == Core.Enums.OrderType.TakeProfitMarket && order.StopPrice.HasValue)
                        tp = order.StopPrice.Value;
                }

                if (sl.HasValue || tp.HasValue)
                {
                    // Signal im Service registrieren (für PriceTicker-Loop + Trailing-Stop)
                    var signal = new SignalResult(
                        pos.Side == Side.Buy ? Core.Enums.Signal.Long : Core.Enums.Signal.Short,
                        0.5m, pos.EntryPrice, sl, tp,
                        "Wiederhergestellt nach Neustart");

                    _liveService.RegisterPositionSignal(pos.Symbol, pos.Side, signal, pos.MarkPrice);

                    restored++;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Trade",
                        $"{pos.Symbol}: SL/TP wiederhergestellt (SL={sl?.ToString("G8") ?? "---"} TP={tp?.ToString("G8") ?? "---"})",
                        pos.Symbol));
                }
            }

            if (restored > 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    $"{restored} Position(en) mit SL/TP wiederhergestellt"));
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                $"SL/TP-Wiederherstellung fehlgeschlagen: {ex.Message}"));
        }
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
        _equityTimer?.Dispose();
        _accountUpdateTimer?.Dispose();
        BtcTicker.Dispose();
        Activity.Dispose();
        _paperService.Dispose();
        _liveService?.Dispose();
        _liveService = null;
        _liveClient = null;
    }

    /// <summary>
    /// Aktualisiert Account-Daten und offene Positionen alle 5 Sekunden.
    /// Nutzt je nach Modus die SimulatedExchange (Paper) oder den echten BingXRestClient (Live).
    /// </summary>
    private async Task StartAccountUpdateAsync()
    {
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await _accountUpdateTimer.WaitForNextTickAsync())
            {
                if (!IsRunning) continue;

                try
                {
                    AccountInfo? account = null;
                    IReadOnlyList<Position>? positions = null;

                    if (!IsPaperMode && _liveClient != null)
                    {
                        // Live-Modus: Echte Daten von BingX
                        account = await _liveClient.GetAccountInfoAsync();
                        positions = await _liveClient.GetPositionsAsync();
                    }
                    else if (IsPaperMode && _paperService.Exchange != null)
                    {
                        // Paper-Modus: Simulierte Daten
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
                            ? acct.Balance - 10_000m
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
    }

    // === ATI-Persistenz ===

    private async Task LoadAtiStateAsync()
    {
        if (_ati == null || _dbService == null) return;
        try
        {
            var json = await _dbService.LoadAtiStateAsync();
            if (!string.IsNullOrEmpty(json))
            {
                _ati.DeserializeState(json);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ATI",
                    "Lernzustand wiederhergestellt"));
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "ATI",
                $"Lernzustand konnte nicht geladen werden: {ex.Message}"));
        }
    }

    private async Task SaveAtiStateAsync()
    {
        if (_ati == null || _dbService == null) return;
        try
        {
            var json = _ati.SerializeState();
            await _dbService.SaveAtiStateAsync(json);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "ATI",
                "Lernzustand gespeichert"));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "ATI",
                $"Lernzustand konnte nicht gespeichert werden: {ex.Message}"));
        }
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
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Dashboard",
                $"Equity-Snapshot speichern fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>
    /// Stoppt den Equity-Snapshot-Timer.
    /// </summary>
    private void StopEquitySnapshotTimer()
    {
        _equityTimer?.Dispose();
        _equityTimer = null;
    }

    /// <summary>Aktualisiert HasOpenPositions + PositionsStatusText (3x genutzt).</summary>
    private void UpdatePositionsStatus()
    {
        HasOpenPositions = OpenPositions.Count > 0;
        PositionsStatusText = HasOpenPositions
            ? $"{OpenPositions.Count} offene Position(en)"
            : "Keine offenen Positionen";
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
