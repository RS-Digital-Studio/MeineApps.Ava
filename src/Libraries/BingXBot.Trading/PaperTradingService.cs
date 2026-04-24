using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Trading;

/// <summary>
/// Paper-Trading-Service: Echtzeit-Simulation mit REST-Polling.
/// Erbt die gesamte Scan-/SL/TP-/BE-Logik von TradingServiceBase.
/// Nutzt SimulatedExchange als IExchangeClient.
/// </summary>
public class PaperTradingService : TradingServiceBase
{
    private SimulatedExchange? _exchange;

    protected override string LogPrefix => ModePrefix;
    protected override string ModeName => "Paper-Trading";

    /// <summary>
    /// Modus-Prefix für Log-Nachrichten (z.B. "[S] " für Scalping).
    /// Im Multi-Mode-Betrieb unterscheidbar, im Single-Mode leer.
    /// </summary>
    public string ModePrefix { get; set; } = "";

    /// <summary>Zugriff auf die simulierte Exchange für Account-Abfragen.</summary>
    public SimulatedExchange? Exchange => _exchange;

    public PaperTradingService(
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings)
        : base(publicClient, strategyManager, riskSettings, scannerSettings, eventBus, botSettings)
    {
    }

    /// <summary>Startet das Paper-Trading mit dem angegebenen Startkapital.</summary>
    public void Start(decimal initialBalance = 10_000m)
    {
        if (_isRunning) return;

        // Alte SimulatedExchange disposen (enthält ReaderWriterLockSlim)
        _exchange?.Dispose();

        _exchange = new SimulatedExchange(new BacktestSettings
        {
            InitialBalance = initialBalance,
            SimulatedFundingRatePercent = _botSettings.SimulatedFundingRatePercent
        });
        _exchange.SetFundingRate(_botSettings.SimulatedFundingRatePercent / 100m);

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"Paper-Trading gestartet (Startkapital: {initialBalance:N0} USDT)"));

        StartBase(new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance));
    }

    /// <summary>Stoppt das Paper-Trading und schließt alle offenen Positionen.</summary>
    public override async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();

        // Alle Positionen schließen
        if (_exchange != null)
            await CloseAllAndPublishAsync();

        StopBase(BotState.Stopped, "Paper-Trading gestoppt");
    }

    /// <summary>Notfall-Stop: Sofort alle Positionen schließen.</summary>
    public override async Task EmergencyStopAsync()
    {
        _cts?.Cancel();

        if (_exchange != null)
            await CloseAllAndPublishAsync();

        StopBase(BotState.EmergencyStop, "NOTFALL-STOP: Alle Positionen geschlossen");
    }

    // ═══════════════════════════════════════════════════════════════
    // Abstrakte Methoden implementieren
    // ═══════════════════════════════════════════════════════════════

    protected override Task<AccountInfo> GetAccountAsync() =>
        _exchange!.GetAccountInfoAsync();

    protected override Task<IReadOnlyList<Position>> GetPositionsForScanAsync() =>
        _exchange!.GetPositionsAsync();

    protected override Task<IReadOnlyList<Position>> GetPositionsForTickerAsync() =>
        _exchange!.GetPositionsAsync();

    protected override void SetCurrentPriceIfNeeded(string symbol, decimal price) =>
        _exchange?.SetCurrentPrice(symbol, price);

    protected override async Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null, int adaptiveLeverage = 0)
    {
        var category = Core.Helpers.SymbolClassifier.Classify(ticker.Symbol);
        var catMaxLev = (int)_riskSettings.GetCategorySettings(category).MaxLeverage;
        var leverage = adaptiveLeverage > 0
            ? Math.Min(adaptiveLeverage, catMaxLev)
            : catMaxLev;
        await _exchange!.SetLeverageAsync(ticker.Symbol, leverage, side);

        // Dynamische Slippage: ATR/Volume aus gecachten Klines berechnen
        UpdateMarketConditionsForSymbol(ticker.Symbol, ticker.LastPrice);

        // Limit-Order-Simulation (18.04.2026 v1.2.4): Paper-Mode nutzt den vom Signal geplanten
        // Limit-Preis (SignalResult.EntryPrice) statt des aktuellen Tickers als Fill-Preis.
        // Vollstaendige Pending-Queue-Simulation (Wait-for-Retrace + Invalidation-Cancel) ist
        // Live-only — Paper approximiert den Fill-Preis korrekt, unterschlaegt aber den
        // Invalidation-Cancel vor dem Fill (optimistischer Bias fuer Pre-Fill-Invalidierungen).
        // Live-Discrepancy: Wenn im Live die Sequenz vor dem Retrace invalidiert, wird kein Trade
        // ausgeloest — Paper zeigt diesen Trade trotzdem. Backtests sind dadurch pessimistischer als noetig.
        var isLimit = signal?.PreferLimitOrder == true
                      && signal.EntryPrice.HasValue && signal.EntryPrice.Value > 0;
        if (isLimit)
        {
            _exchange.SetCurrentPrice(ticker.Symbol, signal!.EntryPrice!.Value);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                $"{ModePrefix}{ticker.Symbol}: Paper simuliert Limit-Fill @ {signal.EntryPrice:F8} " +
                $"(Ticker={ticker.LastPrice:F8}) — Invalidation vor Fill wird nicht simuliert",
                ticker.Symbol));
        }

        var order = await _exchange.PlaceOrderAsync(new OrderRequest(
            ticker.Symbol, side, OrderType.Market, quantity));

        // Nach dem Fill den Ticker-Preis zuruecksetzen, damit nachfolgende Positionen/Scans den
        // tatsaechlichen Marktpreis sehen, nicht den artifiziellen Limit-Level.
        if (isLimit)
            _exchange.SetCurrentPrice(ticker.Symbol, ticker.LastPrice);

        if (order.Status == OrderStatus.Rejected)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                $"{ticker.Symbol}: Order abgelehnt (nicht genug Margin)", ticker.Symbol));
            return false;
        }

        return true;
    }

    protected override async Task ClosePositionAndPublishAsync(string symbol, Side side)
    {
        var prevCount = _exchange!.GetCompletedTrades().Count;
        await _exchange.ClosePositionAsync(symbol, side);
        RemoveSignalByKey($"{symbol}_{side}");

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
            $"{symbol}: Position geschlossen ({side})", symbol));

        PublishNewTrades(prevCount);
    }

    protected override async Task OnSlTpHitAsync(Position pos, decimal price, string key, string reason, bool isStopLoss)
    {
        // Preis auf den SL/TP-Wert setzen bevor wir schließen
        if (_positionSignals.TryGetValue(key, out var signal))
        {
            if (isStopLoss && signal.StopLoss.HasValue)
                _exchange!.SetCurrentPrice(pos.Symbol, signal.StopLoss.Value);
            else if (!isStopLoss && signal.TakeProfit.HasValue)
                _exchange!.SetCurrentPrice(pos.Symbol, signal.TakeProfit.Value);
        }

        var prevCount = _exchange!.GetCompletedTrades().Count;
        await _exchange.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
        RemoveSignalByKey(key);

        // Preis zurücksetzen auf aktuellen Marktwert
        _exchange.SetCurrentPrice(pos.Symbol, price);

        // Trades publizieren + RiskManager aktualisieren
        PublishNewTrades(prevCount);

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
            $"{pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));
    }

    protected override async Task OnPartialCloseAsync(Position pos, decimal price, decimal quantityToClose)
    {
        if (_exchange == null) return;
        _exchange.SetCurrentPrice(pos.Symbol, price);
        var prevCount = _exchange.GetCompletedTrades().Count;
        await _exchange.ReducePositionAsync(pos.Symbol, pos.Side, quantityToClose);
        PublishNewTrades(prevCount);
        _exchange.SetCurrentPrice(pos.Symbol, price); // Preis bleibt aktuell
    }

    // ═══════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Setzt dynamische Slippage-Parameter auf der SimulatedExchange basierend auf aktuellen Marktdaten.
    /// Nutzt den Indikator-Cache (wird während des Scan-Zyklus befüllt).
    /// </summary>
    private void UpdateMarketConditionsForSymbol(string symbol, decimal price)
    {
        if (_exchange == null || price <= 0) return;

        // ATR-Schätzung: 1.5% des Preises (typisch für H4-Krypto)
        var estimatedAtr = price * 0.015m;
        _exchange.SetMarketConditions(symbol, estimatedAtr, 1.0m);
    }

    /// <summary>Publiziert alle neuen CompletedTrades seit prevCount + neuen Equity-Punkt fuer Remote-UI.</summary>
    private void PublishNewTrades(int prevCount)
    {
        var allTrades = _exchange!.GetCompletedTrades();
        for (int i = prevCount; i < allTrades.Count; i++)
        {
            // Trade-Outcome ZUERST verarbeiten, DANN EventBus → Dashboard-Snapshot sieht aktuelle Counter
            ProcessCompletedTrade(allTrades[i]);
            _eventBus.PublishTrade(allTrades[i]);
        }

        // v1.3.0 K1: Nach Trade-Close neuen Equity-Punkt fuer Live-Chart publishen.
        // Paper: Balance = initial + realized PnL aller abgeschlossenen Trades (Exchange rechnet das).
        if (allTrades.Count > prevCount)
        {
            _eventBus.PublishEquity(new EquityPoint(DateTime.UtcNow, _exchange.Balance));
        }
    }

    /// <summary>Schließt alle Positionen und publiziert die resultierenden Trades.</summary>
    private async Task CloseAllAndPublishAsync()
    {
        var previousTradeCount = _exchange!.GetCompletedTrades().Count;
        await _exchange.CloseAllPositionsAsync();
        PublishNewTrades(previousTradeCount);
    }
}
