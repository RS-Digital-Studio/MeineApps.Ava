using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
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
public record ActivityItem(DateTime Time, string Category, string Message, string Level, string? Symbol)
{
    public string TimeText => Time.ToLocalTime().ToString("HH:mm:ss");
    public string LevelColor => Level switch
    {
        "Error" => "#EF4444",
        "Warning" => "#F59E0B",
        "Trade" => "#10B981",
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
    private PeriodicTimer? _equityTimer;
    private PeriodicTimer? _accountUpdateTimer;

    // === Live-Trading Client (erstellt zur Laufzeit mit API-Keys) ===
    private BingXRestClient? _liveClient;

    // === Modus ===
    [ObservableProperty] private bool _isPaperMode = true;
    [ObservableProperty] private string _modeText = "Paper-Modus";
    [ObservableProperty] private string _modeDescription = "Simuliertes Trading ohne echtes Geld";

    // === Live-Trading Zustand ===
    [ObservableProperty] private bool _hasApiKeys;
    [ObservableProperty] private string _liveStatusText = "API-Keys nicht konfiguriert";

    // === Bot-Status ===
    [ObservableProperty] private string _botStatusText = "Gestoppt";
    [ObservableProperty] private string _botStatusColor = "#EF4444"; // Rot
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

    // === BTC Live-Chart ===
    public ObservableCollection<Candle> BtcCandles { get; } = new();
    [ObservableProperty] private decimal _btcPrice;
    [ObservableProperty] private decimal _btcPriceChange;
    [ObservableProperty] private bool _isBtcLoading = true;
    [ObservableProperty] private string _btcStatusText = "Lade BTC-Daten...";

    // === Equity-Kurve ===
    public ObservableCollection<EquityPoint> EquityData { get; } = new();

    // === Activity-Feed: Letzte 20 Aktionen des Bots ===
    public ObservableCollection<ActivityItem> RecentActivity { get; } = new();

    // === Hinweise/Onboarding ===
    [ObservableProperty] private bool _showWelcomeHint = true;
    [ObservableProperty] private string _welcomeHintText = "Willkommen! Starte mit einem Backtest um eine Strategie zu testen, oder konfiguriere deine API-Keys in den Einstellungen.";

    public DashboardViewModel(
        BotEventBus eventBus,
        StrategyManager strategyManager,
        PaperTradingService paperService,
        IPublicMarketDataClient? publicClient = null,
        BotDatabaseService? dbService = null,
        ISecureStorageService? secureStorage = null)
    {
        _eventBus = eventBus;
        _strategyManager = strategyManager;
        _paperService = paperService;
        _publicClient = publicClient;
        _dbService = dbService;
        _secureStorage = secureStorage;

        // Keine Fake-Daten! Zeige ehrlichen Zustand.
        HasAccountData = false;
        HasOpenPositions = false;

        // API-Key-Status prüfen
        HasApiKeys = _secureStorage?.HasCredentials ?? false;
        if (HasApiKeys)
            LiveStatusText = "API-Keys vorhanden";

        // Activity-Feed: Auf BotEventBus subscriben (nur relevante Log-Kategorien)
        _eventBus.LogEmitted += OnLogEmitted;

        // Initiale Strategie-Beschreibung laden
        OnSelectedStrategyChanged(SelectedStrategy);

        // BTC-Daten laden (echt, kein Fake)
        _ = LoadBtcDataAsync();
        _ = StartAutoRefreshAsync();
    }

    /// <summary>
    /// Empfängt Log-Einträge vom EventBus und fügt relevante in den Activity-Feed ein.
    /// </summary>
    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        // Nur relevante Kategorien anzeigen (kein Debug-Spam)
        if (entry.Level == Core.Enums.LogLevel.Debug) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RecentActivity.Insert(0, new ActivityItem(
                entry.Timestamp,
                entry.Category,
                entry.Message,
                entry.Level.ToString(),
                entry.Symbol));

            // Max 20 Einträge
            while (RecentActivity.Count > 20)
                RecentActivity.RemoveAt(RecentActivity.Count - 1);
        });
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
            StartPaperTrading();
        }
        else
        {
            await StartLiveTradingAsync();
        }
    }

    /// <summary>
    /// Startet den Paper-Trading-Modus mit simuliertem Kapital.
    /// </summary>
    private void StartPaperTrading()
    {
        // Strategie aktivieren
        var strategy = StrategyFactory.Create(SelectedStrategy);
        _strategyManager.SetStrategy(strategy);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Strategie: {SelectedStrategy}"));

        // Paper-Trading-Service starten
        _paperService.Start(10_000m);

        IsRunning = true;
        CanStart = false;
        BotStatusText = "Laeuft (Paper)";
        BotStatusColor = "#10B981"; // Gruen
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
    /// Startet den Live-Trading-Modus: Verbindet sich mit BingX und zeigt echte Daten an.
    /// v1.0: NUR Anzeige (Account, Positionen, Signale), KEIN automatisches Handeln.
    /// </summary>
    private async Task StartLiveTradingAsync()
    {
        // API-Keys prüfen
        if (_secureStorage == null || !_secureStorage.HasCredentials)
        {
            BotStatusText = "API-Keys fehlen";
            BotStatusColor = "#EF4444";
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
            BotStatusColor = "#EF4444";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                "API-Keys konnten nicht entschlüsselt werden. Bitte erneut eingeben."));
            return;
        }

        // BingXRestClient erstellen
        BotStatusText = "Verbinde mit BingX...";
        BotStatusColor = "#F59E0B";
        CanStart = false;

        try
        {
            var httpClient = App.Services.GetRequiredService<HttpClient>();
            var rateLimiter = App.Services.GetRequiredService<RateLimiter>();
            var logger = NullLogger<BingXRestClient>.Instance;
            _liveClient = new BingXRestClient(creds.Value.ApiKey, creds.Value.ApiSecret, httpClient, rateLimiter, logger);

            // Verbindung testen: Account-Info abrufen
            var account = await _liveClient.GetAccountInfoAsync();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Balance = account.Balance;
                AvailableBalance = account.AvailableBalance;
                UnrealizedPnl = account.UnrealizedPnl;
                TotalPnl = account.UnrealizedPnl; // Live: Unrealized als Gesamt-PnL (kein Startkapital bekannt)
                HasAccountData = true;
            });

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                $"Live-Verbindung hergestellt. Balance: {account.Balance:N2} USDT"));

            // Offene Positionen laden
            var positions = await _liveClient.GetPositionsAsync();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OpenPositions.Clear();
                foreach (var p in positions)
                {
                    OpenPositions.Add(new PositionDisplayItem
                    {
                        Symbol = p.Symbol,
                        Side = p.Side.ToString(),
                        EntryPrice = p.EntryPrice,
                        MarkPrice = p.MarkPrice,
                        Quantity = p.Quantity,
                        Pnl = p.UnrealizedPnl,
                        Leverage = p.Leverage
                    });
                }
                HasOpenPositions = OpenPositions.Count > 0;
                PositionsStatusText = HasOpenPositions
                    ? $"{OpenPositions.Count} offene Position(en)"
                    : "Keine offenen Positionen";
            });

            if (positions.Count > 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                    $"{positions.Count} offene Position(en) gefunden"));
            }

            // WARNUNG: Live-Trading ist EXPERIMENTELL
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "LIVE-MODUS aktiv - Echte Account-Daten. Automatisches Trading ist deaktiviert (v1.0)."));

            IsRunning = true;
            BotStatusText = "Verbunden (Live) - Nur Anzeige";
            BotStatusColor = "#10B981";
            ShowWelcomeHint = false;
            LiveStatusText = "Verbunden";

            // Account-Update Timer starten (alle 5 Sekunden echte Daten)
            _ = StartAccountUpdateAsync();
        }
        catch (Exception ex)
        {
            BotStatusText = $"Verbindung fehlgeschlagen";
            BotStatusColor = "#EF4444";
            CanStart = true;
            _liveClient = null;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"Live-Verbindung fehlgeschlagen: {ex.Message}"));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        if (!IsPaperMode)
        {
            // Live-Modus: Pause unterbricht Account-Updates
            if (BotStatusText.Contains("Pausiert"))
            {
                BotStatusText = "Verbunden (Live) - Nur Anzeige";
                BotStatusColor = "#10B981";
                _ = StartAccountUpdateAsync();
            }
            else
            {
                _accountUpdateTimer?.Dispose();
                _accountUpdateTimer = null;
                BotStatusText = "Pausiert (Live)";
                BotStatusColor = "#F59E0B";
            }
            return;
        }

        if (_paperService.IsPaused)
        {
            // Resume
            _paperService.Resume();
            BotStatusText = "Laeuft (Paper)";
            BotStatusColor = "#10B981"; // Gruen
        }
        else
        {
            // Pause
            _paperService.Pause();
            BotStatusText = "Pausiert";
            BotStatusColor = "#F59E0B"; // Amber
        }
    }

    [RelayCommand]
    private async Task StopBot()
    {
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        if (IsPaperMode)
        {
            await _paperService.StopAsync();
        }
        else
        {
            // Live-Modus: Client freigeben
            _liveClient = null;
            LiveStatusText = "Getrennt";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                "Live-Verbindung getrennt"));
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Gestoppt";
        BotStatusColor = "#EF4444"; // Rot
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
            // Live-Modus v1.0: KEIN automatisches Schliessen von echten Positionen!
            // Das wäre zu riskant ohne ausreichendes Testing.
            _liveClient = null;
            LiveStatusText = "Notfall-Trennung";

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "NOTFALL-STOP (Live): Verbindung getrennt. Offene Positionen manuell auf BingX schliessen!"));
        }

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgefuehrt";
        BotStatusColor = "#EF4444";

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
                ? "Echtes Trading mit BingX - Nur Anzeige (v1.0)"
                : "API-Keys erforderlich! Gehe zu Einstellungen.";
        }

        // Account-Daten zuruecksetzen bei Modus-Wechsel
        HasAccountData = false;
        Balance = 0;
        AvailableBalance = 0;
        UnrealizedPnl = 0;
        TotalPnl = 0;
        _liveClient = null;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Modus gewechselt zu: {ModeText}"));
    }

    [RelayCommand]
    private void DismissWelcomeHint()
    {
        ShowWelcomeHint = false;
    }

    private PeriodicTimer? _refreshTimer;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _eventBus.LogEmitted -= OnLogEmitted;
        _refreshTimer?.Dispose();
        _equityTimer?.Dispose();
        _accountUpdateTimer?.Dispose();
        _paperService.Dispose();
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
                        Balance = acct.Balance;
                        AvailableBalance = acct.AvailableBalance;
                        UnrealizedPnl = acct.UnrealizedPnl;
                        TotalPnl = isPaper
                            ? acct.Balance - 10_000m + acct.UnrealizedPnl
                            : acct.UnrealizedPnl;

                        // Positionen aktualisieren
                        OpenPositions.Clear();
                        foreach (var p in pos)
                        {
                            OpenPositions.Add(new PositionDisplayItem
                            {
                                Symbol = p.Symbol,
                                Side = p.Side.ToString(),
                                EntryPrice = p.EntryPrice,
                                MarkPrice = p.MarkPrice,
                                Quantity = p.Quantity,
                                Pnl = p.UnrealizedPnl,
                                Leverage = p.Leverage
                            });
                        }
                        HasOpenPositions = OpenPositions.Count > 0;
                        PositionsStatusText = HasOpenPositions
                            ? $"{OpenPositions.Count} offene Position(en)"
                            : "Keine offenen Positionen";
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Account-Update Fehler: {ex.Message}");

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

    /// <summary>
    /// Laedt BTC-Klinendaten von BingX (oeffentlich, kein API-Key noetig).
    /// </summary>
    private async Task LoadBtcDataAsync()
    {
        try
        {
            if (_publicClient == null)
            {
                IsBtcLoading = false;
                BtcStatusText = "Keine Verbindung";
                return;
            }

            var candles = await _publicClient.GetKlinesAsync(
                "BTC-USDT", TimeFrame.H1,
                DateTime.UtcNow.AddHours(-100), DateTime.UtcNow);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                BtcCandles.Clear();
                foreach (var c in candles)
                    BtcCandles.Add(c);

                if (candles.Count > 0)
                {
                    BtcPrice = candles[^1].Close;
                    BtcPriceChange = candles.Count > 1 && candles[0].Close != 0
                        ? (candles[^1].Close - candles[0].Close) / candles[0].Close * 100m
                        : 0m;
                    BtcStatusText = $"BTC-USDT | {candles.Count} Candles (1h)";
                }
                IsBtcLoading = false;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BTC-Daten Ladefehler: {ex.Message}");
            IsBtcLoading = false;
            BtcStatusText = "Daten nicht verfuegbar (offline?)";
        }
    }

    /// <summary>
    /// Aktualisiert BTC-Preis alle 10 Sekunden, volle Candles alle 60 Sekunden.
    /// </summary>
    private async Task StartAutoRefreshAsync()
    {
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        var tickCount = 0;
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync())
            {
                tickCount++;

                if (tickCount % 6 == 0)
                {
                    // Alle 60s: Volle Candle-Daten laden
                    await LoadBtcDataAsync();
                }
                else
                {
                    // Alle 10s: Nur den aktuellen BTC-Preis aktualisieren
                    await UpdateBtcPriceAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Aktualisiert nur den BTC-Preis (schnell, ein API-Call).
    /// </summary>
    private async Task UpdateBtcPriceAsync()
    {
        if (_publicClient == null) return;
        try
        {
            var tickers = await _publicClient.GetAllTickersAsync();
            var btc = tickers.FirstOrDefault(t => t.Symbol == "BTC-USDT");
            if (btc != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BtcPrice = btc.LastPrice;
                    BtcPriceChange = btc.PriceChangePercent24h;
                });
            }
        }
        catch { /* Stille Fehlerbehandlung - nächster Tick versucht es erneut */ }
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

            // Auch in die ObservableCollection für das UI
            Avalonia.Threading.Dispatcher.UIThread.Post(() => EquityData.Add(point));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Equity-Snapshot speichern fehlgeschlagen: {ex.Message}");
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
}

/// <summary>
/// Anzeige-Modell fuer eine offene Position im Dashboard.
/// </summary>
public class PositionDisplayItem
{
    public string Symbol { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Pnl { get; set; }
    public decimal Leverage { get; set; }
    public bool IsProfit => Pnl > 0;
}
