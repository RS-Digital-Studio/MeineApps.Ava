using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine;

/// <summary>
/// Zentrale Zustandsmaschine des Trading Bots.
/// Orchestriert Scanner, Strategien, RiskManager und Exchange.
/// </summary>
public class TradingEngine
{
    private readonly IExchangeClient _exchangeClient;
    private readonly IDataFeed _dataFeed;
    private readonly IMarketScanner _scanner;
    private readonly IRiskManager _riskManager;
    private readonly StrategyManager _strategyManager;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly ILogger<TradingEngine> _logger;

    private CancellationTokenSource? _cts;
    private Task? _mainLoop;
    private DateTime _lastDataReceived = DateTime.UtcNow;

    // Cache für Ticker-Daten pro Scan-Durchlauf (vermeidet wiederholte API-Aufrufe)
    private IReadOnlyList<Ticker>? _cachedTickers;
    private DateTime _tickersCachedAt;

    public BotState State { get; private set; } = BotState.Stopped;
    public TradingMode Mode { get; private set; }

    // Events für UI-Binding
    public event EventHandler<BotState>? StateChanged;
    public event EventHandler<Order>? OrderPlaced;
    public event EventHandler<LogEntry>? LogEmitted;
    public event EventHandler<string>? ErrorOccurred;

    public TradingEngine(
        IExchangeClient exchangeClient,
        IDataFeed dataFeed,
        IMarketScanner scanner,
        IRiskManager riskManager,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        ILogger<TradingEngine> logger)
    {
        _exchangeClient = exchangeClient;
        _dataFeed = dataFeed;
        _scanner = scanner;
        _riskManager = riskManager;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _logger = logger;
    }

    public async Task StartAsync(TradingMode mode)
    {
        if (State != BotState.Stopped && State != BotState.Error)
            throw new InvalidOperationException($"Kann nicht starten im Zustand {State}");

        Mode = mode;
        SetState(BotState.Starting);
        EmitLog(Core.Enums.LogLevel.Info, "Engine", $"Starte im {mode}-Modus...");

        try
        {
            // Leverage setzen (nur Live)
            if (mode == TradingMode.Live)
            {
                var symbols = await _exchangeClient.GetAllSymbolsAsync();
                // Leverage wird pro Symbol beim ersten Trade gesetzt
            }

            _cts = new CancellationTokenSource();
            SetState(BotState.Running);
            EmitLog(Core.Enums.LogLevel.Info, "Engine", "Bot läuft");

            // Haupt-Loop auf Background-Task
            _mainLoop = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten");
            SetState(BotState.Error);
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }

    public Task PauseAsync()
    {
        if (State != BotState.Running) return Task.CompletedTask;
        SetState(BotState.Paused);
        EmitLog(Core.Enums.LogLevel.Info, "Engine", "Bot pausiert (offene Positionen bleiben)");
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (State != BotState.Paused) return Task.CompletedTask;
        SetState(BotState.Running);
        EmitLog(Core.Enums.LogLevel.Info, "Engine", "Bot fortgesetzt");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (State == BotState.Stopped) return;

        EmitLog(Core.Enums.LogLevel.Info, "Engine", "Stoppe Bot...");
        _cts?.Cancel();

        if (_mainLoop != null)
        {
            try { await _mainLoop; }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _cts = null;
        _mainLoop = null;

        SetState(BotState.Stopped);
        EmitLog(Core.Enums.LogLevel.Info, "Engine", "Bot gestoppt");
    }

    public async Task EmergencyStopAsync()
    {
        EmitLog(Core.Enums.LogLevel.Warning, "Engine", "EMERGENCY STOP - Schließe alle Positionen!");

        _cts?.Cancel();
        if (_mainLoop != null)
        {
            try { await _mainLoop; }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _cts = null;
        _mainLoop = null;

        try
        {
            await _exchangeClient.CloseAllPositionsAsync();
            EmitLog(Core.Enums.LogLevel.Warning, "Engine", "Alle Positionen geschlossen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Schließen aller Positionen");
            ErrorOccurred?.Invoke(this, $"Emergency Stop Fehler: {ex.Message}");
        }

        SetState(BotState.EmergencyStop);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (State == BotState.Paused)
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                    continue;
                }

                // Ticker-Cache aktualisieren für diesen Scan-Durchlauf
                _cachedTickers = await _exchangeClient.GetAllTickersAsync().ConfigureAwait(false);
                _tickersCachedAt = DateTime.UtcNow;

                // Scanner laufen lassen
                var candidates = new List<ScanResult>();
                await foreach (var result in _scanner.ScanAsync(_scannerSettings, ct).ConfigureAwait(false))
                {
                    candidates.Add(result);
                }

                if (candidates.Count == 0)
                {
                    await Task.Delay(5000, ct).ConfigureAwait(false); // 5s warten wenn nichts gefunden
                    continue;
                }

                _lastDataReceived = DateTime.UtcNow;

                // Für jeden Kandidaten: Strategie evaluieren
                foreach (var candidate in candidates)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        await ProcessCandidateAsync(candidate, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fehler bei {Symbol}", candidate.Symbol);
                        EmitLog(Core.Enums.LogLevel.Warning, "Engine", $"Fehler bei {candidate.Symbol}: {ex.Message}");
                    }
                }

                // Stale-Data-Detection
                if (DateTime.UtcNow - _lastDataReceived > TimeSpan.FromSeconds(60))
                {
                    EmitLog(Core.Enums.LogLevel.Warning, "Engine", "Keine neuen Daten seit 60s - keine neuen Trades");
                }

                // Scan-Intervall (30s)
                await Task.Delay(30000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler in der Haupt-Loop");
                EmitLog(Core.Enums.LogLevel.Error, "Engine", $"Loop-Fehler: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);

                // Nicht crashen, nur pausieren
                SetState(BotState.Paused);
                await Task.Delay(5000, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessCandidateAsync(ScanResult candidate, CancellationToken ct)
    {
        var symbol = candidate.Symbol;

        // Klines laden für Indikator-Berechnung
        var candles = await _exchangeClient.GetKlinesAsync(symbol, _scannerSettings.ScanTimeFrame, 200).ConfigureAwait(false);
        if (candles.Count < 50) return;

        var positions = await _exchangeClient.GetPositionsAsync().ConfigureAwait(false);
        var account = await _exchangeClient.GetAccountInfoAsync().ConfigureAwait(false);

        // Ticker aus Cache (wird pro Scan-Durchlauf einmal geladen, siehe RunLoopAsync)
        var tickers = _cachedTickers ?? await _exchangeClient.GetAllTickersAsync().ConfigureAwait(false);
        var ticker = tickers.FirstOrDefault(t => t.Symbol == symbol);
        if (ticker == null) return;

        var context = new MarketContext(symbol, candles, ticker, positions, account);

        // Symbol-spezifische Strategie-Instanz
        var strategy = _strategyManager.GetOrCreateForSymbol(symbol);
        var signal = strategy.Evaluate(context);

        if (signal.Signal == Signal.None) return;

        EmitLog(Core.Enums.LogLevel.Trade, "Scanner", $"{symbol}: {signal.Signal} (Confidence: {signal.Confidence:P0}) - {signal.Reason}");

        // Risk-Check
        var riskCheck = _riskManager.ValidateTrade(signal, context);
        if (!riskCheck.IsAllowed)
        {
            EmitLog(Core.Enums.LogLevel.Info, "Risk", $"{symbol}: Trade abgelehnt - {riskCheck.RejectionReason}");
            return;
        }

        // Order platzieren
        var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
        if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
        {
            var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
            if (positions.Any(p => p.Symbol == symbol && p.Side == closeSide))
            {
                await _exchangeClient.ClosePositionAsync(symbol, closeSide).ConfigureAwait(false);
                EmitLog(Core.Enums.LogLevel.Trade, "Trade", $"{symbol}: Position geschlossen ({closeSide})");
            }
            return;
        }

        try
        {
            // Leverage setzen (Live-Modus)
            if (Mode == TradingMode.Live)
            {
                await _exchangeClient.SetLeverageAsync(symbol, (int)_riskSettings.MaxLeverage, side).ConfigureAwait(false);
            }

            var order = await _exchangeClient.PlaceOrderAsync(new OrderRequest(
                symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize,
                StopLoss: signal.StopLoss, TakeProfit: signal.TakeProfit)).ConfigureAwait(false);

            EmitLog(Core.Enums.LogLevel.Trade, "Trade", $"{symbol}: {side} {riskCheck.AdjustedPositionSize:F6} @ {ticker.LastPrice}");
            OrderPlaced?.Invoke(this, order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Order fehlgeschlagen für {Symbol}", symbol);
            EmitLog(Core.Enums.LogLevel.Error, "Trade", $"{symbol}: Order fehlgeschlagen - {ex.Message}");
        }
    }

    private void SetState(BotState newState)
    {
        State = newState;
        StateChanged?.Invoke(this, newState);
    }

    private void EmitLog(Core.Enums.LogLevel level, string category, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, category, message);
        LogEmitted?.Invoke(this, entry);

        // Auch an ILogger weiterleiten
        var msLevel = level switch
        {
            Core.Enums.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            Core.Enums.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            Core.Enums.LogLevel.Trade => Microsoft.Extensions.Logging.LogLevel.Information,
            Core.Enums.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            Core.Enums.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
        _logger.Log(msLevel, "[{Category}] {Message}", category, message);
    }
}
