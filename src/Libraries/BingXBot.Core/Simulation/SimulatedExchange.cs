using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Core.Simulation;

/// <summary>
/// Simulierte Exchange für Paper-Trading und Backtesting (Thread-safe).
/// Implementiert IExchangeClient mit internem State statt echten API-Aufrufen.
/// </summary>
public class SimulatedExchange : IExchangeClient
{
    private readonly object _lock = new();
    private decimal _balance;
    private readonly List<Position> _positions = [];
    private readonly List<Order> _openOrders = [];
    private readonly Dictionary<string, decimal> _currentPrices = new();
    private readonly BacktestSettings _settings;
    private readonly List<CompletedTrade> _completedTrades = [];
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
        lock (_lock)
        {
            _currentPrices[symbol] = price;
        }
    }

    public Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        lock (_lock)
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
                    // Neue Position erstellen
                    _positions.Add(new Position(
                        request.Symbol,
                        request.Side,
                        fillPrice,
                        fillPrice,
                        request.Quantity,
                        0m,
                        1m,
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
    }

    public Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        lock (_lock)
        {
            var index = _openOrders.FindIndex(o => o.OrderId == orderId);
            if (index < 0)
                return Task.FromResult(false);

            _openOrders.RemoveAt(index);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null)
    {
        lock (_lock)
        {
            IReadOnlyList<Order> result = symbol == null
                ? _openOrders.ToList().AsReadOnly()
                : _openOrders.Where(o => o.Symbol == symbol).ToList().AsReadOnly();

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        lock (_lock)
        {
            IReadOnlyList<Position> result = _positions
                .Select(p => UpdatePositionPnlLocked(p))
                .ToList()
                .AsReadOnly();

            return Task.FromResult(result);
        }
    }

    public Task ClosePositionAsync(string symbol, Side side)
    {
        lock (_lock)
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
    }

    public async Task CloseAllPositionsAsync()
    {
        // Kopie der Liste unter Lock erstellen
        List<Position> positionsCopy;
        lock (_lock)
        {
            positionsCopy = _positions.ToList();
        }

        foreach (var pos in positionsCopy)
        {
            await ClosePositionAsync(pos.Symbol, pos.Side);
        }
    }

    public Task<AccountInfo> GetAccountInfoAsync()
    {
        lock (_lock)
        {
            var unrealizedPnl = _positions.Sum(p =>
            {
                var currentPrice = GetPriceLocked(p.Symbol);
                return CalculatePnl(p.Side, p.EntryPrice, currentPrice, p.Quantity);
            });

            var usedMargin = _positions.Sum(p => p.Quantity * p.EntryPrice / (p.Leverage > 0 ? p.Leverage : 1));
            var equity = _balance + unrealizedPnl;

            var info = new AccountInfo(
                _balance,
                equity - usedMargin,
                unrealizedPnl,
                usedMargin);

            return Task.FromResult(info);
        }
    }

    public Task SetLeverageAsync(string symbol, int leverage, Side side)
    {
        // No-op in der Simulation
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
        lock (_lock)
        {
            return _completedTrades.ToList();
        }
    }

    #region Private Hilfsmethoden

    /// <summary>
    /// Gibt den aktuellen Preis zurück. MUSS unter _lock aufgerufen werden.
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
    /// Position mit aktuellem unrealizedPnl aktualisieren. MUSS unter _lock aufgerufen werden.
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
