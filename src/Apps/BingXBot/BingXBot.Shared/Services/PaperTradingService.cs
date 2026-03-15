using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Simulation;
using BingXBot.Engine;
using BingXBot.Engine.Risk;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Services;

/// <summary>
/// Paper-Trading-Service: Echtzeit-Simulation mit REST-Polling.
/// Alle 30 Sekunden: Ticker holen, Scanner filtern, Klines laden, Strategie evaluieren,
/// bei Signal: RiskManager pruefen, Order auf SimulatedExchange platzieren.
/// Events werden ueber den BotEventBus publiziert.
/// </summary>
public class PaperTradingService : IDisposable
{
    private readonly IPublicMarketDataClient _publicClient;
    private readonly StrategyManager _strategyManager;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotEventBus _eventBus;

    private SimulatedExchange? _exchange;
    private RiskManager? _riskManager;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private bool _disposed;

    /// <summary>Zugriff auf die simulierte Exchange fuer Account-Abfragen.</summary>
    public SimulatedExchange? Exchange => _exchange;

    /// <summary>Ob der Service gerade laeuft.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Ob der Service pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    public PaperTradingService(
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus)
    {
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
    }

    /// <summary>Startet das Paper-Trading mit dem angegebenen Startkapital.</summary>
    public void Start(decimal initialBalance = 10_000m)
    {
        if (_isRunning) return;

        _exchange = new SimulatedExchange(new BacktestSettings { InitialBalance = initialBalance });
        _riskManager = new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Paper-Trading gestartet (Startkapital: {initialBalance:N0} USDT)"));

        _ = RunLoopAsync(_cts.Token);
        _ = PriceTickerLoopAsync(_cts.Token);
    }

    /// <summary>Pausiert das Paper-Trading (Loop laeuft weiter, ueberspringt aber Scans).</summary>
    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;

        _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading pausiert"));
    }

    /// <summary>Setzt das Paper-Trading nach Pause fort.</summary>
    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading fortgesetzt"));
    }

    /// <summary>Stoppt das Paper-Trading und schliesst alle offenen Positionen.</summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        // Alle Positionen schliessen
        if (_exchange != null)
        {
            await _exchange.CloseAllPositionsAsync();
            var completedTrades = _exchange.GetCompletedTrades();
            foreach (var trade in completedTrades)
                _eventBus.PublishTrade(trade);
        }

        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.Stopped);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading gestoppt"));
    }

    /// <summary>Notfall-Stop: Sofort alle Positionen schliessen (async statt blockierend).</summary>
    public async Task EmergencyStopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        if (_exchange != null)
            await _exchange.CloseAllPositionsAsync();

        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.EmergencyStop);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
            "NOTFALL-STOP: Alle Positionen geschlossen"));
    }

    /// <summary>Hauptschleife: Alle 30 Sekunden scannen und handeln.</summary>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Bei Pause: Loop laeuft weiter, ueberspringt aber den Scan
                if (!_isPaused)
                    await ScanAndTradeAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"Fehler in der Trading-Loop: {ex.Message}"));
            }

            // 30 Sekunden warten bis zum naechsten Scan
            try { await Task.Delay(30_000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Schneller Preis-Ticker: Aktualisiert alle 5 Sekunden die Preise offener Positionen.
    /// Damit PnL und MarkPrice in der UI flüssig aktualisiert werden statt nur alle 30s beim Scan.
    /// </summary>
    private async Task PriceTickerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            if (_isPaused || _exchange == null) continue;

            try
            {
                // Prüfe ob offene Positionen vorhanden
                var positions = await _exchange.GetPositionsAsync().ConfigureAwait(false);
                if (positions.Count == 0) continue;

                // Aktuelle Ticker für offene Symbole holen (ein einzelner API-Call)
                var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
                if (tickers.Count == 0) continue;

                var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

                // Preise auf der SimulatedExchange aktualisieren
                foreach (var pos in positions)
                {
                    if (tickerMap.TryGetValue(pos.Symbol, out var price))
                        _exchange.SetCurrentPrice(pos.Symbol, price);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PriceTicker Fehler: {ex.Message}");
            }
        }
    }

    /// <summary>Ein Scan-Zyklus: Ticker laden, filtern, Strategie evaluieren, handeln.</summary>
    private async Task ScanAndTradeAsync(CancellationToken ct)
    {
        if (_exchange == null || _riskManager == null) return;
        if (_strategyManager.CurrentTemplate == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "Keine Strategie ausgewaehlt"));
            return;
        }

        // 1. Alle Ticker holen
        var tickers = await _publicClient.GetAllTickersAsync(ct);
        if (tickers.Count == 0) return;

        // 2. Nach Scanner-Kriterien filtern
        var candidates = tickers
            .Where(t => t.Volume24h >= _scannerSettings.MinVolume24h)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= _scannerSettings.MinPriceChange)
            .Where(t => _scannerSettings.Blacklist.Count == 0 || !_scannerSettings.Blacklist.Contains(t.Symbol))
            .Where(t => _scannerSettings.Whitelist.Count == 0 || _scannerSettings.Whitelist.Contains(t.Symbol))
            .OrderByDescending(t => t.Volume24h)
            .Take(_scannerSettings.MaxResults)
            .ToList();

        if (candidates.Count == 0) return;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Scanner",
            $"{candidates.Count} Kandidaten gefunden"));

        // 3. Fuer jeden Kandidaten: Klines laden, Strategie evaluieren
        var account = await _exchange.GetAccountInfoAsync();
        var positions = await _exchange.GetPositionsAsync();

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Klines laden (letzte 100 Stunden-Candles)
                var candles = await _publicClient.GetKlinesAsync(
                    ticker.Symbol, _scannerSettings.ScanTimeFrame,
                    DateTime.UtcNow.AddHours(-100), DateTime.UtcNow, ct);

                if (candles.Count < 50) continue;

                // Aktuellen Preis setzen
                _exchange.SetCurrentPrice(ticker.Symbol, ticker.LastPrice);

                // Strategie evaluieren
                var strategy = _strategyManager.GetOrCreateForSymbol(ticker.Symbol);
                var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account);
                var signal = strategy.Evaluate(context);

                if (signal.Signal == Signal.None) continue;

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Scanner",
                    $"{ticker.Symbol}: {signal.Signal} Signal (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
                    ticker.Symbol));

                // Close-Signale verarbeiten
                if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                {
                    var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                    if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                    {
                        await _exchange.ClosePositionAsync(ticker.Symbol, closeSide);
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                            $"{ticker.Symbol}: Position geschlossen ({closeSide})", ticker.Symbol));

                        // Abgeschlossene Trades publizieren
                        foreach (var trade in _exchange.GetCompletedTrades())
                            _eventBus.PublishTrade(trade);
                    }
                    continue;
                }

                // Risk-Check
                var riskCheck = _riskManager.ValidateTrade(signal, context);
                if (!riskCheck.IsAllowed)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
                        $"{ticker.Symbol}: Trade abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                    continue;
                }

                // Leverage setzen (aus RiskSettings) und Order platzieren
                var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                await _exchange.SetLeverageAsync(ticker.Symbol, (int)_riskSettings.MaxLeverage, side);
                await _exchange.PlaceOrderAsync(new OrderRequest(
                    ticker.Symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize));

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                    $"{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:F6} @ {ticker.LastPrice:N2}",
                    ticker.Symbol));

                // Account + Positionen aktualisieren
                account = await _exchange.GetAccountInfoAsync();
                positions = await _exchange.GetPositionsAsync();
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"{ticker.Symbol}: Fehler - {ex.Message}", ticker.Symbol));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
