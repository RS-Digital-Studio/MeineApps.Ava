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
/// Erbt die gesamte Scan-/SL/TP-/Trailing-Stop-Logik von TradingServiceBase.
/// Nutzt SimulatedExchange als IExchangeClient.
/// </summary>
public class PaperTradingService : TradingServiceBase
{
    private SimulatedExchange? _exchange;

    protected override string LogPrefix => "";
    protected override string ModeName => "Paper-Trading";

    /// <summary>Zugriff auf die simulierte Exchange für Account-Abfragen.</summary>
    public SimulatedExchange? Exchange => _exchange;

    public PaperTradingService(
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus)
        : base(publicClient, strategyManager, riskSettings, scannerSettings, eventBus)
    {
    }

    /// <summary>Startet das Paper-Trading mit dem angegebenen Startkapital.</summary>
    public void Start(decimal initialBalance = 10_000m)
    {
        if (_isRunning) return;

        _exchange = new SimulatedExchange(new BacktestSettings { InitialBalance = initialBalance });

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"Paper-Trading gestartet (Startkapital: {initialBalance:N0} USDT)"));

        StartBase(new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance));
    }

    /// <summary>Stoppt das Paper-Trading und schließt alle offenen Positionen.</summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();

        // Alle Positionen schließen
        if (_exchange != null)
            await CloseAllAndPublishAsync();

        StopBase(BotState.Stopped, "Paper-Trading gestoppt");
    }

    /// <summary>Notfall-Stop: Sofort alle Positionen schließen.</summary>
    public async Task EmergencyStopAsync()
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

    protected override async Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null)
    {
        await _exchange!.SetLeverageAsync(ticker.Symbol, (int)_riskSettings.MaxLeverage, side);
        var order = await _exchange.PlaceOrderAsync(new OrderRequest(
            ticker.Symbol, side, OrderType.Market, quantity));

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

    // ═══════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Publiziert alle neuen CompletedTrades seit prevCount.</summary>
    private void PublishNewTrades(int prevCount)
    {
        var allTrades = _exchange!.GetCompletedTrades();
        for (int i = prevCount; i < allTrades.Count; i++)
        {
            _eventBus.PublishTrade(allTrades[i]);
            ProcessCompletedTrade(allTrades[i]);
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
