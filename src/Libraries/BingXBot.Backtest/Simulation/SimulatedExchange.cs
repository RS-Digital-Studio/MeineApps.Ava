using System.Collections.Concurrent;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;

namespace BingXBot.Backtest.Simulation;

/// <summary>
/// Simulierte Exchange für Paper-Trading und Backtesting (Thread-safe).
/// Implementiert IExchangeClient mit internem State statt echten API-Aufrufen.
/// Nutzt ReaderWriterLockSlim für bessere Parallelität bei Leseoperationen.
/// </summary>
public class SimulatedExchange : IExchangeClient, IDisposable
{
    private readonly ReaderWriterLockSlim _rwLock = new();
    private decimal _balance;
    private readonly List<Position> _positions = [];
    private readonly List<Order> _openOrders = [];
    private readonly Dictionary<string, decimal> _currentPrices = new();
    private readonly BacktestSettings _settings;
    private readonly List<CompletedTrade> _completedTrades = [];
    private readonly Dictionary<string, int> _leverageSettings = new();
    /// <summary>Speichert die Opening-Fee pro Position fuer korrekte Fee-Auswertung im CompletedTrade.</summary>
    private readonly Dictionary<string, decimal> _positionOpenFees = new();
    private int _orderCounter;
    // Gecachter Positions-Snapshot: Wird nur bei Preis-/Positions��nderung invalidiert
    private IReadOnlyList<Position>? _cachedPositions;
    private bool _positionsDirty = true;
    // Dynamisches Slippage-Modell: Aktuelle ATR und Volumen-Ratio pro Symbol
    // ConcurrentDictionary: SetMarketConditions() wird außerhalb des _rwLock aufgerufen,
    // während ApplySlippage() innerhalb des _rwLock liest
    private readonly ConcurrentDictionary<string, decimal> _currentAtr = new();
    private readonly ConcurrentDictionary<string, decimal> _currentVolumeRatio = new();
    private readonly Random _rng = new(42); // Deterministisch für reproduzierbare Backtests

    public SimulatedExchange(BacktestSettings settings)
    {
        _settings = settings;
        _balance = settings.InitialBalance;
    }

    /// <summary>
    /// Setzt ATR und Volume-Ratio für dynamische Slippage-Berechnung (vom BacktestEngine pro Candle).
    /// </summary>
    public void SetMarketConditions(string symbol, decimal atr, decimal volumeRatio)
    {
        _currentAtr[symbol] = atr;
        _currentVolumeRatio[symbol] = volumeRatio;
    }

    /// <summary>
    /// Setzt den aktuellen Preis für ein Symbol (wird vom Backtester pro Candle aufgerufen).
    /// </summary>
    public void SetCurrentPrice(string symbol, decimal price)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _currentPrices[symbol] = price;
            // Positions-Cache invalidieren wenn sich der Preis eines gehaltenen Symbols ändert
            if (_positions.Any(p => p.Symbol == symbol))
                _positionsDirty = true;

            // Limit-Order-Matching: Prüfe ob offene Orders getriggert werden
            MatchOpenOrdersLocked(symbol, price);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    /// <summary>Matcht offene Limit-Orders gegen den aktuellen Preis (muss im Write-Lock aufgerufen werden).</summary>
    private void MatchOpenOrdersLocked(string symbol, decimal price)
    {
        var toFill = new List<Order>();
        foreach (var order in _openOrders.Where(o => o.Symbol == symbol && o.Status == OrderStatus.New))
        {
            var triggered = order.Type switch
            {
                OrderType.Limit when order.Side == Side.Buy && price <= order.Price => true,
                OrderType.Limit when order.Side == Side.Sell && price >= order.Price => true,
                OrderType.StopMarket when order.Side == Side.Buy && price >= (order.StopPrice ?? order.Price) => true,
                OrderType.StopMarket when order.Side == Side.Sell && price <= (order.StopPrice ?? order.Price) => true,
                _ => false
            };

            if (triggered) toFill.Add(order);
        }

        foreach (var order in toFill)
        {
            _openOrders.Remove(order);
            // Ausführung wie Market-Order zum Order-Preis (mit Slippage)
            var fillPrice = ApplySlippage(order.Price, order.Side);
            ExecuteOrderLocked(order.Symbol, order.Side, order.Quantity, fillPrice);
        }
    }

    /// <summary>Führt eine Order aus und erstellt/erweitert die Position (muss im Write-Lock aufgerufen werden).</summary>
    private void ExecuteOrderLocked(string symbol, Side side, decimal quantity, decimal fillPrice)
    {
        var fee = _settings.TakerFee * quantity * fillPrice;
        _balance -= fee;

        var leverageKey = $"{symbol}_{side}";
        var leverage = _leverageSettings.GetValueOrDefault(leverageKey, 10);
        var feeKey = $"{symbol}_{side}";

        // Opening-Fee tracken (für korrekte PnL-Berechnung bei Limit-Order-Fills)
        var existingOpenFee = _positionOpenFees.GetValueOrDefault(feeKey, 0m);
        _positionOpenFees[feeKey] = existingOpenFee + fee;

        // Bestehende Position für dieses Symbol + Seite suchen
        var existingIdx = _positions.FindIndex(p => p.Symbol == symbol && p.Side == side);
        if (existingIdx >= 0)
        {
            // Position erweitern: Neuen Durchschnittspreis berechnen
            var pos = _positions[existingIdx];
            var totalQty = pos.Quantity + quantity;
            var avgPrice = (pos.EntryPrice * pos.Quantity + fillPrice * quantity) / totalQty;
            _positions[existingIdx] = pos with { EntryPrice = avgPrice, Quantity = totalQty };
        }
        else
        {
            // Neue Position eröffnen
            _positions.Add(new Position(symbol, side, fillPrice, fillPrice, quantity,
                0m, leverage, MarginType.Cross, DateTime.UtcNow));
        }
        _positionsDirty = true;
    }

    public Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var orderId = $"SIM-{++_orderCounter}";

            if (request.Type == OrderType.Market)
            {
                // Market-Order sofort fuellen
                var basePrice = GetPriceLocked(request.Symbol);
                var fillPrice = ApplySlippage(basePrice, request.Side, request.Symbol);
                var fee = _settings.TakerFee * request.Quantity * fillPrice;

                // Leverage aus Settings holen (Default: 10x)
                var leverageKey = $"{request.Symbol}_{request.Side}";
                var leverage = _leverageSettings.GetValueOrDefault(leverageKey, 10);
                var effectiveLeverage = leverage > 0 ? leverage : 1;

                // Margin-Check: Benoetigte Margin berechnen und pruefen ob genug verfuegbar
                var requiredMargin = request.Quantity * fillPrice / effectiveLeverage;
                var unrealizedPnl = _positions.Sum(p =>
                {
                    var cp = _currentPrices.GetValueOrDefault(p.Symbol, p.EntryPrice);
                    return CalculatePnl(p.Side, p.EntryPrice, cp, p.Quantity);
                });
                var usedMargin = _positions.Sum(p => p.Quantity * p.EntryPrice / (p.Leverage > 0 ? p.Leverage : 1));
                var equity = _balance + unrealizedPnl;
                var availableBalance = Math.Max(0, equity - usedMargin);

                if (requiredMargin + fee > availableBalance)
                {
                    // Nicht genug Margin verfuegbar - Order ablehnen
                    var rejectedOrder = new Order(orderId, request.Symbol, request.Side, request.Type,
                        fillPrice, request.Quantity, request.StopPrice, DateTime.UtcNow, OrderStatus.Rejected);
                    return Task.FromResult(rejectedOrder);
                }

                _balance -= fee;

                // Bestehende Position suchen
                var existing = _positions.FindIndex(p =>
                    p.Symbol == request.Symbol && p.Side == request.Side);

                if (existing >= 0)
                {
                    // Position vergroessern (Durchschnittspreis berechnen)
                    var pos = _positions[existing];
                    var totalQty = pos.Quantity + request.Quantity;
                    var avgPrice = (pos.EntryPrice * pos.Quantity + fillPrice * request.Quantity) / totalQty;
                    // Gesamte Opening-Fee akkumulieren (bestehende + neue)
                    var existingOpenFee = _positionOpenFees.GetValueOrDefault($"{pos.Symbol}_{pos.Side}", 0m);
                    _positionOpenFees[$"{pos.Symbol}_{pos.Side}"] = existingOpenFee + fee;
                    _positions[existing] = pos with
                    {
                        EntryPrice = avgPrice,
                        Quantity = totalQty,
                        MarkPrice = fillPrice,
                        Leverage = effectiveLeverage  // Aktuellen Leverage uebernehmen
                    };
                }
                else
                {
                    // Opening-Fee speichern fuer spaetere Auswertung im CompletedTrade
                    _positionOpenFees[$"{request.Symbol}_{request.Side}"] = fee;

                    // Neue Position erstellen
                    _positions.Add(new Position(
                        request.Symbol,
                        request.Side,
                        fillPrice,
                        fillPrice,
                        request.Quantity,
                        0m,
                        effectiveLeverage,
                        MarginType.Cross,
                        DateTime.UtcNow));
                }

                _positionsDirty = true;
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
            // Gecachten Snapshot zurückgeben wenn keine Preis-/Positionsänderung
            if (!_positionsDirty && _cachedPositions != null)
                return Task.FromResult(_cachedPositions);
        }
        finally { _rwLock.ExitReadLock(); }

        // Cache aktualisieren unter Write-Lock (da _positionsDirty geschrieben wird)
        _rwLock.EnterWriteLock();
        try
        {
            // Double-Check nach Lock-Upgrade
            if (!_positionsDirty && _cachedPositions != null)
                return Task.FromResult(_cachedPositions);

            IReadOnlyList<Position> result = _positions
                .Select(p => UpdatePositionPnlLocked(p))
                .ToList()
                .AsReadOnly();

            _cachedPositions = result;
            _positionsDirty = false;
            return Task.FromResult(result);
        }
        finally { _rwLock.ExitWriteLock(); }
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
            var exitPriceWithSlippage = ApplySlippage(exitPrice, side == Side.Buy ? Side.Sell : Side.Buy, symbol);

            // PnL berechnen
            var pnl = CalculatePnl(pos.Side, pos.EntryPrice, exitPriceWithSlippage, pos.Quantity);
            var closingFee = _settings.TakerFee * pos.Quantity * exitPriceWithSlippage;

            // Opening-Fee aus dem Speicher holen (wurde bei PlaceOrderAsync bereits von _balance abgezogen)
            var feeKey = $"{pos.Symbol}_{pos.Side}";
            var openingFee = _positionOpenFees.GetValueOrDefault(feeKey, 0m);
            _positionOpenFees.Remove(feeKey);

            // Nur PnL und Closing-Fee auf Balance anrechnen (Opening-Fee wurde bereits abgezogen)
            _balance += pnl - closingFee;

            // CompletedTrade: PnL NACH Fees (netto), damit Trade-History und RiskManager korrekte Werte haben
            var totalFee = openingFee + closingFee;
            var netPnl = pnl - totalFee;
            _completedTrades.Add(new CompletedTrade(
                pos.Symbol,
                pos.Side,
                pos.EntryPrice,
                exitPriceWithSlippage,
                pos.Quantity,
                netPnl,
                totalFee,
                pos.OpenTime,
                DateTime.UtcNow,
                "Manuell geschlossen",
                TradingMode.Paper));

            _positions.RemoveAt(index);
            _positionsDirty = true;
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

    /// <summary>
    /// Reduziert eine offene Position um die angegebene Menge (Partial Close für Multi-Stage TP1).
    /// Erstellt einen CompletedTrade für den geschlossenen Teil, behält den Rest als Position.
    /// </summary>
    public Task ReducePositionAsync(string symbol, Side side, decimal quantityToClose)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var index = _positions.FindIndex(p => p.Symbol == symbol && p.Side == side);
            if (index < 0)
                return Task.CompletedTask;

            var pos = _positions[index];
            if (quantityToClose >= pos.Quantity)
            {
                // Voller Close inline (Lock wird NICHT freigegeben, kein Re-Entrant-Aufruf)
                var fullExitPrice = GetPriceLocked(symbol);
                var fullExitSlippage = ApplySlippage(fullExitPrice, side == Side.Buy ? Side.Sell : Side.Buy, symbol);
                var fullPnl = CalculatePnl(pos.Side, pos.EntryPrice, fullExitSlippage, pos.Quantity);
                var fullClosingFee = _settings.TakerFee * pos.Quantity * fullExitSlippage;
                var fullFeeKey = $"{pos.Symbol}_{pos.Side}";
                var fullOpeningFee = _positionOpenFees.GetValueOrDefault(fullFeeKey, 0m);
                _positionOpenFees.Remove(fullFeeKey);
                _balance += fullPnl - fullClosingFee;
                var fullTotalFee = fullOpeningFee + fullClosingFee;
                var fullNetPnl = fullPnl - fullTotalFee;
                _completedTrades.Add(new CompletedTrade(
                    pos.Symbol, pos.Side, pos.EntryPrice, fullExitSlippage,
                    pos.Quantity, fullNetPnl, fullTotalFee, pos.OpenTime, DateTime.UtcNow,
                    "Manuell geschlossen", TradingMode.Paper));
                _positions.RemoveAt(index);
                _positionsDirty = true;
                return Task.CompletedTask;
            }

            var exitPrice = GetPriceLocked(symbol);
            var exitPriceWithSlippage = ApplySlippage(exitPrice, side == Side.Buy ? Side.Sell : Side.Buy, symbol);

            // PnL nur für den geschlossenen Teil
            var pnl = CalculatePnl(pos.Side, pos.EntryPrice, exitPriceWithSlippage, quantityToClose);
            var closingFee = _settings.TakerFee * quantityToClose * exitPriceWithSlippage;

            // Anteilige Opening-Fee (proportional zur geschlossenen Menge)
            var feeKey = $"{pos.Symbol}_{pos.Side}";
            var totalOpenFee = _positionOpenFees.GetValueOrDefault(feeKey, 0m);
            var openFeeRatio = pos.Quantity > 0 ? quantityToClose / pos.Quantity : 0m;
            var partialOpenFee = totalOpenFee * openFeeRatio;
            _positionOpenFees[feeKey] = totalOpenFee - partialOpenFee;

            _balance += pnl - closingFee;

            var totalFee = partialOpenFee + closingFee;
            var netPnl = pnl - totalFee;
            _completedTrades.Add(new CompletedTrade(
                pos.Symbol, pos.Side, pos.EntryPrice, exitPriceWithSlippage,
                quantityToClose, netPnl, totalFee,
                pos.OpenTime, DateTime.UtcNow,
                "Partial Close (TP1)", TradingMode.Paper));

            // Position verkleinern (Rest bleibt offen)
            _positions[index] = pos with { Quantity = pos.Quantity - quantityToClose };
            _positionsDirty = true;
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
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
        return Task.FromResult(_simulatedFundingRate);
    }

    /// <summary>Simulierte Funding-Rate (Standard: 0.01% = 0.0001 als Dezimal).</summary>
    private decimal _simulatedFundingRate = 0.0001m;

    /// <summary>Setzt die simulierte Funding-Rate für alle Symbole.</summary>
    public void SetFundingRate(decimal rate) => _simulatedFundingRate = rate;

    /// <summary>
    /// Wendet Funding-Rate auf alle offenen Positionen an.
    /// Positive Rate: Longs zahlen an Shorts. Negative Rate: Shorts zahlen an Longs.
    /// Wird im Backtest alle 8h aufgerufen, im Paper-Trading periodisch.
    /// </summary>
    public void ApplyFundingRate(decimal fundingRate)
    {
        _rwLock.EnterWriteLock();
        try
        {
            foreach (var pos in _positions)
            {
                // Aktuellen Marktpreis verwenden (nicht stale MarkPrice vom letzten Fill)
                var currentPrice = GetPriceLocked(pos.Symbol);
                var positionValue = pos.Quantity * currentPrice;
                decimal fundingPayment;

                if (pos.Side == Side.Buy)
                {
                    // Long zahlt bei positiver Funding, erhält bei negativer
                    fundingPayment = positionValue * fundingRate;
                }
                else
                {
                    // Short erhält bei positiver Funding, zahlt bei negativer
                    fundingPayment = -(positionValue * fundingRate);
                }

                _balance -= fundingPayment;
            }
            _positionsDirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
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
    /// Dynamische Slippage + Spread anwenden: Buy = höherer Preis, Sell = niedrigerer Preis.
    /// Bei aktiviertem dynamischen Modell: Slippage skaliert mit ATR-Perzentil und inversem Volumen.
    /// Spread wird zusätzlich als Bid-Ask-Kosten aufgeschlagen.
    /// </summary>
    private decimal ApplySlippage(decimal price, Side side, string? symbol = null)
    {
        decimal slippageFactor;

        if (_settings.UseDynamicSlippage && symbol != null
            && _currentAtr.TryGetValue(symbol, out var atr) && atr > 0 && price > 0)
        {
            // ATR-basierte Slippage: Prozent des Preises den ATR ausmacht
            var atrPercent = atr / price;
            // Volumen-Faktor: niedriges Volumen = höhere Slippage (illiquider Markt)
            var volRatio = _currentVolumeRatio.GetValueOrDefault(symbol, 1m);
            var volumeMultiplier = volRatio > 0 ? Math.Min(2m, 1m / volRatio) : 2m;

            // Slippage = ATR-Anteil * Multiplikator * Volumen-Adjustierung + Random-Komponente
            var baseSlippage = atrPercent * Lerp(
                _settings.MinSlippageAtrMultiplier, _settings.MaxSlippageAtrMultiplier,
                (decimal)_rng.NextDouble());
            slippageFactor = baseSlippage * volumeMultiplier;

            // Clamp: Mindestens 0.02%, maximal 2% Slippage
            slippageFactor = Math.Clamp(slippageFactor, 0.0002m, 0.02m);
        }
        else
        {
            // Fallback: Fester Slippage-Prozentsatz
            slippageFactor = _settings.SlippagePercent / 100m;
        }

        // Spread als zusätzliche Kosten (halber Spread pro Seite)
        var halfSpread = _settings.SpreadPercent / 100m / 2m;

        var totalImpact = slippageFactor + halfSpread;
        return side == Side.Buy
            ? price * (1m + totalImpact)
            : price * (1m - totalImpact);
    }

    private static decimal Lerp(decimal a, decimal b, decimal t)
        => a + (b - a) * t;

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

    public void Dispose()
    {
        _rwLock.Dispose();
    }
}
