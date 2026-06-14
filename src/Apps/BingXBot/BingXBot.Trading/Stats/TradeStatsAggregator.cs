using System.Collections.Concurrent;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Stats;

/// <summary>
/// v1.5.3 Phase 5 — Per-TF + Per-Category Trade-Stats.
///
/// Aggregiert <see cref="CompletedTrade"/>-Events nach <see cref="TimeFrame"/> ×
/// <see cref="MarketCategory"/> × <see cref="TradingMode"/> in-memory. Replay aus der
/// Trades-DB beim Server-Boot, damit nach Pi-Restart keine Daten verloren sind.
///
/// Subscribed auf <see cref="BotEventBus.TradeCompleted"/> — als DI-Singleton im
/// Server-Composition-Root registriert.
/// Thread-safe ueber ConcurrentDictionary; Aggregat-Updates sind idempotent (jede CompletedTrade
/// wird genau einmal eingerechnet).
/// </summary>
public sealed class TradeStatsAggregator : IDisposable
{
    private readonly ConcurrentDictionary<StatsGroupKey, MutableStats> _aggregates = new();
    private readonly BotEventBus? _eventBus;
    private readonly EventHandler<CompletedTrade>? _handler;

    public TradeStatsAggregator(BotEventBus? eventBus = null)
    {
        _eventBus = eventBus;
        if (_eventBus != null)
        {
            _handler = OnTradeCompleted;
            _eventBus.TradeCompleted += _handler;
        }
    }

    private void OnTradeCompleted(object? sender, CompletedTrade trade) => Apply(trade);

    /// <summary>
    /// Wendet einen einzelnen Trade auf das Aggregat an. Public fuer Replay aus der DB.
    /// </summary>
    public void Apply(CompletedTrade trade)
    {
        var category = SymbolClassifier.Classify(trade.Symbol);
        var key = new StatsGroupKey(trade.NavigatorTimeframe, category, trade.Mode);
        var stats = _aggregates.GetOrAdd(key, _ => new MutableStats());
        lock (stats.Lock)
        {
            stats.TotalTrades++;
            if (trade.Pnl > 0m) stats.WinTrades++;
            stats.TotalPnl += trade.Pnl;
            stats.TotalFees += trade.Fee;
            var holdingMin = (trade.ExitTime - trade.EntryTime).TotalMinutes;
            if (holdingMin > 0) stats.HoldingMinutesSum += holdingMin;

            // MaxDD-Tracking (kumulativ, peak-to-trough auf der Equity-Kurve dieser Gruppe)
            stats.RunningEquity += trade.Pnl;
            if (stats.RunningEquity > stats.PeakEquity) stats.PeakEquity = stats.RunningEquity;
            var dd = stats.PeakEquity - stats.RunningEquity;
            if (dd > stats.MaxDrawdown) stats.MaxDrawdown = dd;
        }
    }

    /// <summary>
    /// Replay aus einer Trade-Liste (typisch via DB-Load beim Server-Boot).
    /// </summary>
    public void ReplayFromTrades(IEnumerable<CompletedTrade> trades)
    {
        _aggregates.Clear();
        foreach (var t in trades) Apply(t);
    }

    /// <summary>Liefert einen unveraenderlichen Snapshot aller aktuellen Aggregate.</summary>
    public IReadOnlyList<TradeStatsBreakdown> GetSnapshot()
    {
        var result = new List<TradeStatsBreakdown>(_aggregates.Count);
        foreach (var kvp in _aggregates)
        {
            var k = kvp.Key;
            var s = kvp.Value;
            lock (s.Lock)
            {
                var winRate = s.TotalTrades > 0 ? (decimal)s.WinTrades / s.TotalTrades : 0m;
                var avgPnl = s.TotalTrades > 0 ? s.TotalPnl / s.TotalTrades : 0m;
                var avgHolding = s.TotalTrades > 0 ? s.HoldingMinutesSum / s.TotalTrades : 0d;
                result.Add(new TradeStatsBreakdown(
                    NavigatorTimeframe: k.Tf,
                    Category: k.Category,
                    Mode: k.Mode,
                    TotalTrades: s.TotalTrades,
                    WinTrades: s.WinTrades,
                    WinRate: winRate,
                    TotalPnl: s.TotalPnl,
                    AvgPnl: avgPnl,
                    TotalFees: s.TotalFees,
                    AvgHoldingTimeMinutes: avgHolding,
                    MaxDrawdown: s.MaxDrawdown));
            }
        }
        return result;
    }

    public void Dispose()
    {
        if (_eventBus != null && _handler != null)
            _eventBus.TradeCompleted -= _handler;
    }

    private readonly record struct StatsGroupKey(TimeFrame Tf, MarketCategory Category, TradingMode Mode);

    private sealed class MutableStats
    {
        public readonly object Lock = new();
        public int TotalTrades;
        public int WinTrades;
        public decimal TotalPnl;
        public decimal TotalFees;
        public double HoldingMinutesSum;
        public decimal RunningEquity;
        public decimal PeakEquity;
        public decimal MaxDrawdown;
    }
}

/// <summary>
/// v1.5.3 Phase 5 — Public Snapshot-Record fuer eine Stats-Gruppe.
/// </summary>
public sealed record TradeStatsBreakdown(
    TimeFrame NavigatorTimeframe,
    MarketCategory Category,
    TradingMode Mode,
    int TotalTrades,
    int WinTrades,
    decimal WinRate,
    decimal TotalPnl,
    decimal AvgPnl,
    decimal TotalFees,
    double AvgHoldingTimeMinutes,
    decimal MaxDrawdown);
