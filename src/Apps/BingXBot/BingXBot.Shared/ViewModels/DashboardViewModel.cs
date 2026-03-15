using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using BingXBot.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BingXBot.ViewModels;

/// <summary>
/// ViewModel fuer das Dashboard - ehrliche Zustandsanzeige ohne Fake-Daten.
/// Zeigt nur echte Daten an (BTC-Kurs live, Account nur wenn Bot laeuft).
/// Publiziert Bot-Status und Log-Einträge über den BotEventBus.
/// Enthält Strategie-Auswahl und PaperTradingService-Verdrahtung.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private readonly StrategyManager _strategyManager;
    private readonly PaperTradingService _paperService;
    private PeriodicTimer? _equityTimer;
    private PeriodicTimer? _accountUpdateTimer;

    // === Modus ===
    [ObservableProperty] private bool _isPaperMode = true;
    [ObservableProperty] private string _modeText = "Paper-Modus";
    [ObservableProperty] private string _modeDescription = "Simuliertes Trading ohne echtes Geld";

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

    // === Hinweise/Onboarding ===
    [ObservableProperty] private bool _showWelcomeHint = true;
    [ObservableProperty] private string _welcomeHintText = "Willkommen! Starte mit einem Backtest um eine Strategie zu testen, oder konfiguriere deine API-Keys in den Einstellungen.";

    public DashboardViewModel(
        BotEventBus eventBus,
        StrategyManager strategyManager,
        PaperTradingService paperService,
        IPublicMarketDataClient? publicClient = null,
        BotDatabaseService? dbService = null)
    {
        _eventBus = eventBus;
        _strategyManager = strategyManager;
        _paperService = paperService;
        _publicClient = publicClient;
        _dbService = dbService;

        // Keine Fake-Daten! Zeige ehrlichen Zustand.
        HasAccountData = false;
        HasOpenPositions = false;

        // Initiale Strategie-Beschreibung laden
        OnSelectedStrategyChanged(SelectedStrategy);

        // BTC-Daten laden (echt, kein Fake)
        _ = LoadBtcDataAsync();
        _ = StartAutoRefreshAsync();
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
    private void StartBot()
    {
        if (IsPaperMode)
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
        else
        {
            // Live-Modus: Braucht API-Keys
            BotStatusText = "API-Keys benoetigt";
            BotStatusColor = "#F59E0B"; // Amber
            WelcomeHintText = "Fuer Live-Trading: Gehe zu Einstellungen und hinterlege deine BingX API-Keys.";
            ShowWelcomeHint = true;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "Live-Trading erfordert API-Keys. Bitte in den Einstellungen konfigurieren."));
        }
    }

    [RelayCommand]
    private void PauseBot()
    {
        BotStatusText = "Pausiert";
        BotStatusColor = "#F59E0B"; // Amber

        _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Bot pausiert"));
    }

    [RelayCommand]
    private async void StopBot()
    {
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        await _paperService.StopAsync();

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Gestoppt";
        BotStatusColor = "#EF4444"; // Rot
        PositionsStatusText = "Keine offenen Positionen";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = null;
        StopEquitySnapshotTimer();

        _paperService.EmergencyStop();

        IsRunning = false;
        CanStart = true;
        BotStatusText = "Notfall-Stop ausgefuehrt";
        BotStatusColor = "#EF4444";

        // Alle Positionen schliessen
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
            ModeText = "Live-Modus";
            ModeDescription = "Echtes Trading mit BingX - API-Keys erforderlich!";
        }

        // Account-Daten zuruecksetzen bei Modus-Wechsel
        HasAccountData = false;
        Balance = 0;
        AvailableBalance = 0;
        UnrealizedPnl = 0;
        TotalPnl = 0;

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
        _refreshTimer?.Dispose();
        _equityTimer?.Dispose();
        _accountUpdateTimer?.Dispose();
    }

    /// <summary>
    /// Aktualisiert Account-Daten und offene Positionen alle 5 Sekunden vom PaperTradingService.
    /// </summary>
    private async Task StartAccountUpdateAsync()
    {
        _accountUpdateTimer?.Dispose();
        _accountUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await _accountUpdateTimer.WaitForNextTickAsync())
            {
                if (!IsRunning || _paperService.Exchange == null) continue;

                try
                {
                    var account = await _paperService.Exchange.GetAccountInfoAsync();
                    var positions = await _paperService.Exchange.GetPositionsAsync();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Balance = account.Balance;
                        AvailableBalance = account.AvailableBalance;
                        UnrealizedPnl = account.UnrealizedPnl;
                        TotalPnl = account.Balance - 10_000m + account.UnrealizedPnl;

                        // Positionen aktualisieren
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Account-Update Fehler: {ex.Message}");
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
    /// Aktualisiert BTC-Daten alle 60 Sekunden automatisch.
    /// </summary>
    private async Task StartAutoRefreshAsync()
    {
        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync())
                await LoadBtcDataAsync();
        }
        catch (OperationCanceledException) { }
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
