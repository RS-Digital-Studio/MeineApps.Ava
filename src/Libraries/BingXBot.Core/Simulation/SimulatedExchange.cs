using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Core.Simulation;

/// <summary>
/// Simulierte Exchange für Paper-Trading und Backtesting (Thread-safe).
/// Implementiert IExchangeClient mit internem State statt echten API-Aufrufen.
/// Nutzt ReaderWriterLockSlim für bessere Parallelität bei Leseoperationen.
/// </summary>
public class SimulatedExchange : IExchangeClient
{
    private readonly ReaderWriterLockSlim _rwLock = new();
    private decimal _balance;
    private readonly List<Position> _positions = [];
    private readonly List<Order> _openOrders = [];
    private readonly Dictionary<string, decimal> _currentPrices = new();
    private readonly BacktestSettings _settings;
    private readonly List<CompletedTrade> _completedTrades = [];
    private readonly Dictionary<string, int> _leverageSettings = new();
    private int _orderCounter;

    public SimulatedExchange(BacktestSettings settings)
    {
        _settings = settings;
        _balance = settings.InitialBalance;
    }

    /// <summary>
    /// Setzt den aktuellen Preis für ein Symbol (wird vom Backtester pro Candle aufgerufen).
    /// </summary>
    public void SetCurrentPrice(string symbol, decimal price)
    {
        _rwLock.EnterWriteLock();
        try { _currentPrices[symbol] = price; }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var orderId = $"SIM-{++_orderCounter}";

            if (request.Type == OrderType.Market)
            {
                // Market-Order sofort füllen
                var basePrice = GetPriceLocked(request.Symbol);
                var fillPrice = ApplySlippage(basePrice, request.Side);
                var fee = _settings.TakerFee * request.Quantity * fillPrice;

                _balance -= fee;

                // Bestehende Position suchen
                var existing = _positions.FindIndex(p =>
                    p.Symbol == request.Symbol && p.Side == request.Side);

                if (existing >= 0)
                {
                    // Position vergrößern (Durchschnittspreis berechnen)
                    var pos = _positions[existing];
                    var totalQty = pos.Quantity + request.Quantity;
                    var avgPrice = (pos.EntryPrice * pos.Quantity + fillPrice * request.Quantity) / totalQty;
                    _positions[existing] = pos with
                    {
                        EntryPrice = avgPrice,
                        Quantity = totalQty,
                        MarkPrice = fillPrice
                    };
                }
                else
                {
                    // Leverage aus Settings holen (Default: 10x)
                    var leverageKey = $"{request.Symbol}_{request.Side}";
                    var leverage = _leverageSettings.GetValueOrDefault(leverageKey, 10);

                    // Neue Position erstellen
                    _positions.Add(new Position(
                        request.Symbol,
                        request.Side,
                        fillPrice,
                        fillPrice,
                        request.Quantity,
                        0m,
                        leverage,
                        MarginType.Cross,
                        DateTime.UtcNow));
                }

                var filledOrder = new Order(orderId, request.Symbol, request.Side, request.Type,
                    fillPrice, request.Quantity, request.StopPrice, DateTime.UtcNow, OrderStatus.Filled);

                return Task.FromResult(filledOrder);
            }

            // Limit/Stop-Orders als offene Orders speichern
            var pendingOrder = new Order(orderId, request.Symbol, request.Side, request.Type,
                request.Price ?? GetPriceLocked(request.Symbol), request.Quantity,
                request.StopPrice, DateTime.UtcNow, OrderStatus.New);

            _openOrders.Add(pendingOrder);
            return Task.FromResult(pendingOrder);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var index = _openOrders.FindIndex(o => o.OrderId == orderId);
            if (index < 0)
                return Task.FromResult(false);

            _openOrders.RemoveAt(index);
            return Task.FromResult(true);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null)
    {
        _rwLock.EnterReadLock();
        try
        {
            IReadOnlyList<Order> result = symbol == null
                ? _openOrders.ToList().AsReadOnly()
                : _openOrders.Where(o => o.Symbol == symbol).ToList().AsReadOnly();

            return Task.FromResult(result);
        }
        finally { _rwLock.ExitReadLock(); }
    }

    public Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        _rwLock.EnterReadLock();
        try
        {
            IReadOnlyList<Position> result = _positions
                .Select(p => UpdatePositionPnlLocked(p))
                .ToList()
                .AsReadOnly();

            return Task.FromResult(result);
        }
        finally { _rwLock.ExitReadLock(); }
    }

    public Task ClosePositionAsync(string symbol, Side side)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var index = _positions.FindIndex(p => p.Symbol == symbol && p.Side == side);
            if (index < 0)
                return Task.CompletedTask;

            var pos = _positions[index];
            var exitPrice = GetPriceLocked(symbol);
            var exitPriceWithSlippage = ApplySlippage(exitPrice, side == Side.Buy ? Side.Sell : Side.Buy);

            // PnL berechnen
            var pnl = CalculatePnl(pos.Side, pos.EntryPrice, exitPriceWithSlippage, pos.Quantity);
            var fee = _settings.TakerFee * pos.Quantity * exitPriceWithSlippage;

            _balance += pnl - fee;

            // CompletedTrade erstellen
            _completedTrades.Add(new CompletedTrade(
                pos.Symbol,
                pos.Side,
                pos.EntryPrice,
                exitPriceWithSlippage,
                pos.Quantity,
                pnl,
                fee,
                pos.OpenTime,
                DateTime.UtcNow,
                "Manuell geschlossen",
                TradingMode.Paper));

            _positions.RemoveAt(index);
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public async Task CloseAllPositionsAsync()
    {
        // Kopie der Liste unter ReadLock erstellen
        List<Position> positionsCopy;
        _rwLock.EnterReadLock();
        try { positionsCopy = _positions.ToList(); }
        finally { _rwLock.ExitReadLock(); }

        foreach (var pos in positionsCopy)
        {
            await ClosePositionAsync(pos.Symbol, pos.Side);
        }
    }

    public Task<AccountInfo> GetAccountInfoAsync()
    {
        _rwLock.EnterReadLock();
        try
        {
            var unrealizedPnl = _positions.Sum(p =>
            {
                var currentPrice = GetPriceLocked(p.Symbol);
                return CalculatePnl(p.Side, p.EntryPrice, currentPrice, p.Quantity);
            });

            var usedMargin = _positions.Sum(p => p.Quantity * p.EntryPrice / (p.Leverage > 0 ? p.Leverage : 1));
            var equity = _balance + unrealizedPnl;
            var availableBalance = Math.Max(0, equity - usedMargin);

            var info = new AccountInfo(
                equity,
                availableBalance,
                unrealizedPnl,
                usedMargin);

            return Task.FromResult(info);
        }
        finally { _rwLock.ExitReadLock(); }
    }

    public Task SetLeverageAsync(string symbol, int leverage, Side side)
    {
        _rwLock.EnterWriteLock();
        try { _leverageSettings[$"{symbol}_{side}"] = leverage; }
        finally { _rwLock.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public Task SetMarginTypeAsync(string symbol, MarginType marginType)
    {
        // No-op in der Simulation
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit)
    {
        // Im Backtest nicht benötigt (Daten kommen vom DataFeed)
        IReadOnlyList<Candle> empty = [];
        return Task.FromResult(empty);
    }

    public Task<IReadOnlyList<Ticker>> GetAllTickersAsync()
    {
        IReadOnlyList<Ticker> empty = [];
        return Task.FromResult(empty);
    }

    public Task<decimal> GetFundingRateAsync(string symbol)
    {
        return Task.FromResult(0m);
    }

    public Task<IReadOnlyList<string>> GetAllSymbolsAsync()
    {
        IReadOnlyList<string> empty = [];
        return Task.FromResult(empty);
    }

    /// <summary>
    /// Gibt alle abgeschlossenen Trades zurück (für Backtest-Auswertung).
    /// </summary>
    public List<CompletedTrade> GetCompletedTrades()
    {
        _rwLock.EnterReadLock();
        try { return _completedTrades.ToList(); }
        finally { _rwLock.ExitReadLock(); }
    }

    #region Private Hilfsmethoden

    /// <summary>
    /// Gibt den aktuellen Preis zurück. MUSS unter _rwLock (Read oder Write) aufgerufen werden.
    /// </summary>
    private decimal GetPriceLocked(string symbol)
    {
        if (_currentPrices.TryGetValue(symbol, out var price))
            return price;

        throw new InvalidOperationException($"Kein Preis für {symbol} gesetzt. SetCurrentPrice() zuerst aufrufen.");
    }

    /// <summary>
    /// Slippage anwenden: Buy = höherer Preis, Sell = niedrigerer Preis.
    /// </summary>
    private decimal ApplySlippage(decimal price, Side side)
    {
        var slippageFactor = _settings.SlippagePercent / 100m;
        return side == Side.Buy
            ? price * (1m + slippageFactor)
            : price * (1m - slippageFactor);
    }

    /// <summary>
    /// PnL für eine Position berechnen.
    /// Long: (Exit - Entry) * Qty, Short: (Entry - Exit) * Qty
    /// </summary>
    private static decimal CalculatePnl(Side side, decimal entryPrice, decimal exitPrice, decimal quantity)
    {
        return side == Side.Buy
            ? (exitPrice - entryPrice) * quantity
            : (entryPrice - exitPrice) * quantity;
    }

    /// <summary>
    /// Position mit aktuellem unrealizedPnl aktualisieren. MUSS unter _rwLock (Read oder Write) aufgerufen werden.
    /// </summary>
    private Position UpdatePositionPnlLocked(Position pos)
    {
        if (!_currentPrices.TryGetValue(pos.Symbol, out var currentPrice))
            return pos;

        var unrealizedPnl = CalculatePnl(pos.Side, pos.EntryPrice, currentPrice, pos.Quantity);
        return pos with { MarkPrice = currentPrice, UnrealizedPnl = unrealizedPnl };
    }

    #endregion
}
