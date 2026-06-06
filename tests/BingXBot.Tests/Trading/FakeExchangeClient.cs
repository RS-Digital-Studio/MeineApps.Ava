using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Tests.Trading;

/// <summary>
/// Test-Double fuer <see cref="IExchangeClient"/>. Konfigurierbarer In-Memory-State
/// (Positionen, Orders) und Call-Recorder fuer Assertions.
///
/// Design-Prinzipien:
/// - Thread-safe (ConcurrentDictionary/List-Locks), weil LiveTradingService aus mehreren Loops zugreift.
/// - Keine Netzwerk/HTTP-Simulation — direkte State-Rueckgabe.
/// - Write-Operationen (Place/Cancel/ClosePosition) aendern den State, damit Folge-Calls
///   konsistent sind.
/// - Recorder speichert alle Calls mit Parametern, abrufbar fuer Assertions.
/// </summary>
public sealed class FakeExchangeClient : IExchangeClient
{
    private readonly object _lock = new();
    private readonly List<Position> _positions = new();
    private readonly List<Order> _openOrders = new();
    private int _orderIdCounter = 1;

    // ────────────────── Call-Recorder ──────────────────
    public List<string> CallLog { get; } = new();
    public List<(string Symbol, Side Side)> ClosePositionCalls { get; } = new();
    public List<(string Symbol, Side Side, decimal Qty)> PlaceOrderCalls { get; } = new();
    public List<(string Symbol, int Leverage, Side Side)> SetLeverageCalls { get; } = new();
    public List<(string Symbol, Side Side, decimal? Sl, decimal? Tp)> SetSlTpCalls { get; } = new();
    public List<(string Symbol, Side Side, decimal Qty, decimal Price)> PlaceTpCalls { get; } = new();
    public List<(string OrderId, string Symbol)> CancelOrderCalls { get; } = new();

    /// <summary>Test-Setup: Mindest-Order-Menge fuer den Min-Qty-Split-Guard (0 = keine Restriktion).</summary>
    public decimal MinOrderQty { get; set; }
    public bool MeetsMinimumOrder(string symbol, decimal quantity, decimal price) => quantity >= MinOrderQty;

    /// <summary>Test-Setup: wenn true, entfernt <see cref="ClosePositionAsync"/> die Position NICHT
    /// (simuliert einen fehlgeschlagenen Close fuer Rebalancer-Safety-Tests).</summary>
    public bool FailCloses { get; set; }

    /// <summary>Test-Setup: Konto-Equity, die <see cref="GetAccountInfoAsync"/> meldet.</summary>
    public decimal AccountEquity { get; set; } = 10000m;

    // ────────────────── Test-Setup-Helpers ──────────────────
    public FakeExchangeClient WithPosition(string symbol, Side side, decimal qty, decimal entry, decimal markPrice = 0m)
    {
        lock (_lock)
        {
            _positions.Add(new Position(
                Symbol: symbol,
                Side: side,
                EntryPrice: entry,
                MarkPrice: markPrice > 0 ? markPrice : entry,
                Quantity: qty,
                UnrealizedPnl: 0m,
                Leverage: 10,
                MarginType: MarginType.Isolated,
                OpenTime: DateTime.UtcNow));
        }
        return this;
    }

    public FakeExchangeClient WithOpenOrder(string symbol, Side side, OrderType type, decimal qty, decimal price, bool reduceOnly = false)
    {
        lock (_lock)
        {
            _openOrders.Add(new Order(
                OrderId: $"fake-{Interlocked.Increment(ref _orderIdCounter)}",
                Symbol: symbol,
                Side: side,
                Type: type,
                Price: price,
                Quantity: qty,
                StopPrice: null,
                CreateTime: DateTime.UtcNow,
                Status: OrderStatus.New,
                ReduceOnly: reduceOnly));
        }
        return this;
    }

    /// <summary>
    /// Direkter Insert eines Order-Records (fuer Tests die das ReduceOnly/Side-Detail
    /// vorgeben muessen). Order-ID wird beibehalten — kein Auto-Generate.
    /// </summary>
    public FakeExchangeClient WithOpenOrderInstance(Order order)
    {
        lock (_lock) _openOrders.Add(order);
        return this;
    }

    public void ClearPositions() { lock (_lock) _positions.Clear(); }
    public void ClearOrders() { lock (_lock) _openOrders.Clear(); }

    // ────────────────── Market Data ──────────────────
    public Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit)
    {
        CallLog.Add($"GetKlinesAsync({symbol},{tf},{limit})");
        return Task.FromResult<IReadOnlyList<Candle>>(Array.Empty<Candle>());
    }

    public Task<IReadOnlyList<Ticker>> GetAllTickersAsync()
    {
        CallLog.Add("GetAllTickersAsync");
        return Task.FromResult<IReadOnlyList<Ticker>>(Array.Empty<Ticker>());
    }

    public Task<decimal> GetFundingRateAsync(string symbol)
    {
        CallLog.Add($"GetFundingRateAsync({symbol})");
        return Task.FromResult(0m);
    }

    public Task<IReadOnlyList<string>> GetAllSymbolsAsync()
    {
        CallLog.Add("GetAllSymbolsAsync");
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    // ────────────────── Account / Config ──────────────────
    public Task<AccountInfo> GetAccountInfoAsync()
    {
        CallLog.Add("GetAccountInfoAsync");
        return Task.FromResult(new AccountInfo(Balance: AccountEquity, AvailableBalance: AccountEquity, UnrealizedPnl: 0m, UsedMargin: 0m, Equity: AccountEquity, RealizedPnl: 0m));
    }

    public Task<(decimal TakerRate, decimal MakerRate)> GetCommissionRateAsync()
    {
        CallLog.Add("GetCommissionRateAsync");
        return Task.FromResult((0.0005m, 0.0002m));
    }

    public Task SetLeverageAsync(string symbol, int leverage, Side side)
    {
        CallLog.Add($"SetLeverageAsync({symbol},{leverage},{side})");
        SetLeverageCalls.Add((symbol, leverage, side));
        return Task.CompletedTask;
    }

    public Task SetMarginTypeAsync(string symbol, MarginType marginType)
    {
        CallLog.Add($"SetMarginTypeAsync({symbol},{marginType})");
        return Task.CompletedTask;
    }

    public Task<bool> IsHedgeModeAsync() { CallLog.Add("IsHedgeModeAsync"); return Task.FromResult(true); }
    public Task<bool> SetHedgeModeAsync(bool e) { CallLog.Add($"SetHedgeModeAsync({e})"); return Task.FromResult(e); }
    public Task SyncServerTimeAsync() { CallLog.Add("SyncServerTimeAsync"); return Task.CompletedTask; }
    public Task InitializeSymbolInfoAsync() { CallLog.Add("InitializeSymbolInfoAsync"); return Task.CompletedTask; }

    // ────────────────── Positions ──────────────────
    public Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        CallLog.Add("GetPositionsAsync");
        lock (_lock) return Task.FromResult<IReadOnlyList<Position>>(_positions.ToList());
    }

    public Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken ct) => GetPositionsAsync();

    public Task ClosePositionAsync(string symbol, Side side)
    {
        CallLog.Add($"ClosePositionAsync({symbol},{side})");
        ClosePositionCalls.Add((symbol, side));
        if (!FailCloses)
            lock (_lock) _positions.RemoveAll(p => p.Symbol == symbol && p.Side == side);
        return Task.CompletedTask;
    }

    public Task ClosePartialAsync(string symbol, Side originalSide, decimal quantity)
    {
        CallLog.Add($"ClosePartialAsync({symbol},{originalSide},{quantity})");
        return Task.CompletedTask;
    }

    public Task CloseAllPositionsAsync() { CallLog.Add("CloseAllPositionsAsync"); lock (_lock) _positions.Clear(); return Task.CompletedTask; }
    public Task CloseAllPositionsAsync(CancellationToken ct) => CloseAllPositionsAsync();

    // ────────────────── Orders ──────────────────
    public Task<Order> PlaceOrderAsync(OrderRequest request, decimal lastPrice = 0m)
    {
        CallLog.Add($"PlaceOrderAsync({request.Symbol},{request.Side},{request.Quantity})");
        PlaceOrderCalls.Add((request.Symbol, request.Side, request.Quantity));
        var order = new Order(
            OrderId: $"fake-{Interlocked.Increment(ref _orderIdCounter)}",
            Symbol: request.Symbol,
            Side: request.Side,
            Type: request.Type,
            Price: request.Price ?? lastPrice,
            Quantity: request.Quantity,
            StopPrice: request.StopPrice,
            CreateTime: DateTime.UtcNow,
            Status: OrderStatus.New);
        lock (_lock) _openOrders.Add(order);
        return Task.FromResult(order);
    }

    public Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        CallLog.Add($"CancelOrderAsync({orderId},{symbol})");
        CancelOrderCalls.Add((orderId, symbol));
        lock (_lock) _openOrders.RemoveAll(o => o.OrderId == orderId);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null)
    {
        CallLog.Add($"GetOpenOrdersAsync({symbol ?? "all"})");
        lock (_lock)
        {
            var list = symbol == null
                ? _openOrders.ToList()
                : _openOrders.Where(o => o.Symbol == symbol).ToList();
            return Task.FromResult<IReadOnlyList<Order>>(list);
        }
    }

    public Task<Order> AmendOrderAsync(string orderId, string symbol, decimal? newPrice = null, decimal? newStopPrice = null, decimal? newQuantity = null)
    {
        CallLog.Add($"AmendOrderAsync({orderId},{symbol})");
        lock (_lock)
        {
            var existing = _openOrders.FirstOrDefault(o => o.OrderId == orderId)
                ?? throw new InvalidOperationException($"Order {orderId} nicht gefunden");
            return Task.FromResult(existing);
        }
    }

    // ────────────────── SL / TP Management ──────────────────
    public Task SetPositionSlTpAsync(string symbol, Side positionSide, decimal? stopLoss, decimal? takeProfit)
    {
        CallLog.Add($"SetPositionSlTpAsync({symbol},{positionSide},sl={stopLoss},tp={takeProfit})");
        SetSlTpCalls.Add((symbol, positionSide, stopLoss, takeProfit));
        return Task.CompletedTask;
    }

    public Task<Order> PlaceTpLimitOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice)
    {
        CallLog.Add($"PlaceTpLimitOrderAsync({symbol},{positionSide},{quantity},{triggerPrice})");
        PlaceTpCalls.Add((symbol, positionSide, quantity, triggerPrice));
        return Task.FromResult(new Order($"fake-tp-{Interlocked.Increment(ref _orderIdCounter)}", symbol,
            positionSide == Side.Buy ? Side.Sell : Side.Buy, OrderType.Limit, triggerPrice, quantity, null,
            DateTime.UtcNow, OrderStatus.New));
    }

    public Task<Order> PlaceTpMarketOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice)
    {
        CallLog.Add($"PlaceTpMarketOrderAsync({symbol},{positionSide},{quantity},{triggerPrice})");
        return Task.FromResult(new Order($"fake-tpm-{Interlocked.Increment(ref _orderIdCounter)}", symbol,
            positionSide == Side.Buy ? Side.Sell : Side.Buy, OrderType.Market, 0m, quantity, triggerPrice,
            DateTime.UtcNow, OrderStatus.New));
    }

    public Task<Order> PlaceTpReduceOnlyLimitAsync(string symbol, Side positionSide, decimal quantity, decimal limitPrice)
    {
        CallLog.Add($"PlaceTpReduceOnlyLimitAsync({symbol},{positionSide},{quantity},{limitPrice})");
        PlaceTpCalls.Add((symbol, positionSide, quantity, limitPrice));
        var closeSide = positionSide == Side.Buy ? Side.Sell : Side.Buy;
        var order = new Order(
            OrderId: $"fake-tp-ro-{Interlocked.Increment(ref _orderIdCounter)}",
            Symbol: symbol,
            Side: closeSide,
            Type: OrderType.Limit,
            Price: limitPrice,
            Quantity: quantity,
            StopPrice: null,
            CreateTime: DateTime.UtcNow,
            Status: OrderStatus.New,
            ReduceOnly: true);
        lock (_lock) _openOrders.Add(order);
        return Task.FromResult(order);
    }

    // ────────────────── Safety / Kill-Switch ──────────────────
    public Task ActivateKillSwitchAsync(int timeoutMs = 120_000) { CallLog.Add($"ActivateKillSwitchAsync({timeoutMs})"); return Task.CompletedTask; }
    public Task DeactivateKillSwitchAsync() { CallLog.Add("DeactivateKillSwitchAsync"); return Task.CompletedTask; }

    // ────────────────── Listen-Key ──────────────────
    public Task<string> CreateListenKeyAsync() { CallLog.Add("CreateListenKeyAsync"); return Task.FromResult("fake-listen-key"); }
    public Task RenewListenKeyAsync(string listenKey) { CallLog.Add($"RenewListenKeyAsync({listenKey})"); return Task.CompletedTask; }
    public Task DeleteListenKeyAsync(string listenKey) { CallLog.Add($"DeleteListenKeyAsync({listenKey})"); return Task.CompletedTask; }

    // ────────────────── Income / Fees ──────────────────
    public Task<List<IncomeRecord>> GetIncomeHistoryAsync(string? symbol = null, string? incomeType = null, DateTime? startTime = null, DateTime? endTime = null, int limit = 100)
    {
        CallLog.Add("GetIncomeHistoryAsync");
        return Task.FromResult(new List<IncomeRecord>());
    }
}
