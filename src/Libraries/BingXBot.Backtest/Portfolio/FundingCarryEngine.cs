using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Reports;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Portfolio;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>Funding-/Carry-Faktor-Parameter (cross-sectional, market-neutral, auf Perps).</summary>
/// <param name="LookbackSettlements">Anzahl juengster 8h-Funding-Settlements fuer das Ranking (Mittelwert).</param>
/// <param name="RebalanceEveryCandles">Rebalance-Intervall in Nav-Kerzen (H4). 6 ≈ taeglich, 42 ≈ woechentlich.</param>
/// <param name="LongK">Long-Slots.</param>
/// <param name="ShortK">Short-Slots.</param>
/// <param name="MinAbsFunding">Mindest-|Funding| (pro 8h) fuer einen Slot — filtert Rauschen um 0.</param>
/// <param name="LeverageCap">Per-Bein-Leverage-Cap (0 = Kategorie-Leverage).</param>
/// <param name="LongHighFunding">true = akademischer Carry-Faktor (long HOCH-Funding / short NIEDRIG-Funding;
///   Literatur Sharpe ~0.74). false = Funding-Harvest (long neg / short pos; auf CEX historisch NEGATIV).</param>
/// <param name="MomentumWeight">0 = reiner Carry; &gt;0 = kombinierter z-Score (1-w)·z(Carry)+w·z(Momentum)
///   (Literatur: Momentum+Carry-Kombination hebt Sharpe ueber Einzelfaktoren via negativer Korrelation).</param>
/// <param name="MomentumLookback">Lookback-Kerzen fuer die Momentum-Komponente (nur bei MomentumWeight&gt;0).</param>
public readonly record struct FundingCarryParams(
    int LookbackSettlements, int RebalanceEveryCandles, int LongK, int ShortK,
    decimal MinAbsFunding, int LeverageCap = 1,
    bool LongHighFunding = true, decimal MomentumWeight = 0m, int MomentumLookback = 84)
{
    public string Label =>
        (MomentumWeight > 0m ? $"COMBO(c{1 - MomentumWeight:0.0}+m{MomentumWeight:0.0})" : "CARRY")
        + $"/{(LongHighFunding ? "hi" : "harvest")}/lb{LookbackSettlements}/R{RebalanceEveryCandles}"
        + $"/{LongK}L-{ShortK}S/min{MinAbsFunding:0.0000}/lev{LeverageCap}";
}

/// <summary>
/// Funding-Carry-Backtest auf EINEM gemeinsamen Konto. Market-neutral: long die Symbole mit dem
/// negativsten Funding (als Long ERHAELT man Funding bei negativer Rate), short die mit dem
/// positivsten (als Short ERHAELT man Funding bei positiver Rate) → Funding-Harvest auf beiden
/// Beinen, Preis-Risiko weitgehend neutralisiert. Nutzt ECHTE historische Funding-Raten pro Symbol
/// (per <see cref="SimulatedExchange.ApplyFundingRatesPerSymbol"/>), nicht eine Konstante.
///
/// Reine Perp-Konstruktion (kein Spot) — anders als der klassische delta-neutrale Cash-and-Carry
/// (long Spot + short Perp) traegt diese Variante Rest-Basis-/Richtungs-Risiko, ist aber mit dem
/// perp-only-Bot umsetzbar. Fees/Min-Order/Margin live-treu ueber die SimulatedExchange.
/// </summary>
public sealed class FundingCarryEngine(
    IPublicMarketDataClient publicClient,
    IReadOnlyDictionary<string, List<(DateTime Time, decimal Rate)>> fundingBySymbol,
    ISymbolInfoProvider? symbolInfo, ILogger logger)
{
    private const int MinCandles = 50;
    private const int EquitySnapshotEverySteps = 6;
    private const decimal MarginUtilization = 0.75m;

    public async Task<PerformanceReport> RunAsync(
        IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings settings, FundingCarryParams p, CancellationToken ct = default)
    {
        var states = new List<PortfolioSymbolState>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            if (!fundingBySymbol.ContainsKey(symbol)) continue;   // ohne Funding-Historie kein Carry
            var nav = await publicClient.GetKlinesAsync(symbol, navTf, from, to, ct).ConfigureAwait(false);
            if (nav.Count < MinCandles) continue;
            states.Add(new PortfolioSymbolState(symbol, nav, NullStrat.Instance)
            {
                TradingStartCloseTime = nav[Math.Min(MinCandles, nav.Count - 1)].CloseTime
            });
        }
        if (states.Count == 0) return new PerformanceReport();
        logger.LogInformation("Funding-Carry-Backtest: {Symbols} Symbole, {Cfg}, {From}..{To}",
            states.Count, p.Label, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
        return await RunCoreAsync(states, settings, p, ct).ConfigureAwait(false);
    }

    private async Task<PerformanceReport> RunCoreAsync(
        List<PortfolioSymbolState> states, BotSettings settings, FundingCarryParams p, CancellationToken ct)
    {
        var bt = settings.Backtest;
        using var sim = new SimulatedExchange(bt, symbolInfo);

        var timeline = MergedTimeline.Build(states.Select(s => (IReadOnlyList<Candle>)s.Nav));
        if (timeline.Count == 0) return new PerformanceReport();

        var equityCurve = new List<EquityPoint>();
        var lastFundingTime = timeline[0];
        var leverageSet = new HashSet<string>();
        var slots = Math.Min(p.LongK + p.ShortK, settings.Risk.MaxOpenPositions);
        var lastRebalanceStep = int.MinValue;

        for (var step = 0; step < timeline.Count; step++)
        {
            ct.ThrowIfCancellationRequested();
            var t = timeline[step];
            sim.SetCurrentBacktestTime(t);
            foreach (var s in states)
            {
                s.AdvanceTo(t);
                if (s.NavIdx >= 0) sim.SetCurrentPrice(s.Symbol, s.CurrentCandle.Close);
            }

            // Funding alle 8h: ECHTE per-Symbol-Rate des aktuellen Settlements anwenden.
            if (bt.SimulateFundingRate && (t - lastFundingTime).TotalHours >= 8)
            {
                var rates = new Dictionary<string, decimal>();
                foreach (var s in states)
                {
                    var rate = FundingAt(s.Symbol, t);
                    if (rate.HasValue) rates[s.Symbol] = rate.Value;
                }
                sim.ApplyFundingRatesPerSymbol(rates);
                lastFundingTime = t;
            }

            // Rebalance: nach erwartetem Funding ranken.
            if (lastRebalanceStep == int.MinValue || step - lastRebalanceStep >= p.RebalanceEveryCandles)
            {
                await RebalanceAsync(sim, states, settings, p, slots, leverageSet, t, ct).ConfigureAwait(false);
                lastRebalanceStep = step;
            }

            if (step % EquitySnapshotEverySteps == 0)
            {
                var acc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
                equityCurve.Add(new EquityPoint(t, acc.Balance + acc.UnrealizedPnl));
            }
        }

        await sim.CloseAllPositionsAsync().ConfigureAwait(false);
        var finalAcc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
        equityCurve.Add(new EquityPoint(timeline[^1], finalAcc.Balance + finalAcc.UnrealizedPnl));
        var completed = sim.GetCompletedTrades().Select(tr => tr with { Mode = TradingMode.Backtest }).ToList();
        return PerformanceReport.FromTrades(completed, equityCurve, bt.InitialBalance);
    }

    private async Task RebalanceAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, BotSettings settings, FundingCarryParams p,
        int slots, HashSet<string> leverageSet, DateTime t, CancellationToken ct)
    {
        // Erwartetes Funding je Symbol = Mittel der letzten LookbackSettlements 8h-Raten vor t.
        var eligible = states
            .Where(s => s.NavIdx >= p.MomentumLookback && t >= s.TradingStartCloseTime)
            .Select(s => (State: s, Avg: AvgFunding(s.Symbol, t, p.LookbackSettlements)))
            .Where(x => x.Avg.HasValue && Math.Abs(x.Avg!.Value) >= p.MinAbsFunding)
            .ToList();
        if (eligible.Count == 0) return;

        // Carry-Signal: long-high-funding (Carry-Faktor) → hoeher = long-attraktiver; sonst negiert (Harvest).
        var carrySig = eligible.ToDictionary(
            x => x.State.Symbol,
            x => p.LongHighFunding ? x.Avg!.Value : -x.Avg!.Value);

        Dictionary<string, decimal> score;
        if (p.MomentumWeight <= 0m)
        {
            score = carrySig;
        }
        else
        {
            // Kombinierter z-Score: (1-w)·z(Carry) + w·z(Momentum). Negative Faktor-Korrelation
            // hebt laut Literatur den Sharpe ueber jeden Einzelfaktor.
            var momSig = new Dictionary<string, decimal>();
            foreach (var x in eligible)
            {
                var mom = MomentumBasketCalculator.Momentum(
                    x.State.ContextSlice(p.MomentumLookback + 20), p.MomentumLookback, riskAdjusted: true);
                if (mom.HasValue) momSig[x.State.Symbol] = mom.Value;
            }
            var common = carrySig.Keys.Where(momSig.ContainsKey).ToList();
            if (common.Count == 0) return;
            var zc = ZScores(common.ToDictionary(s => s, s => carrySig[s]));
            var zm = ZScores(common.ToDictionary(s => s, s => momSig[s]));
            var w = p.MomentumWeight;
            score = common.ToDictionary(s => s, s => (1 - w) * zc[s] + w * zm[s]);
        }

        var ordered = score.OrderByDescending(kv => kv.Value).ToList();
        var target = new Dictionary<string, Side>();
        foreach (var kv in ordered.Take(p.LongK)) target[kv.Key] = Side.Buy;
        foreach (var kv in ordered.AsEnumerable().Reverse().Take(p.ShortK)) target.TryAdd(kv.Key, Side.Sell);

        // Close-vor-Open.
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        foreach (var pos in positions)
            if (!target.TryGetValue(pos.Symbol, out var want) || want != pos.Side)
                await sim.ClosePositionAsync(pos.Symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);

        positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        var held = positions.Select(pp => $"{pp.Symbol}_{pp.Side}").ToHashSet();
        var acc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
        var equity = acc.Balance + acc.UnrealizedPnl;
        if (equity <= 0m || slots <= 0) return;
        var perSlotMargin = equity * MarginUtilization / slots;

        foreach (var (symbol, side) in target)
        {
            ct.ThrowIfCancellationRequested();
            if (held.Contains($"{symbol}_{side}")) continue;
            var state = states.First(s => s.Symbol == symbol);
            var price = state.CurrentCandle.Close;
            if (price <= 0m) continue;
            var catLev = (int)settings.Risk.GetCategorySettings(state.Category).MaxLeverage;
            var leverage = Math.Max(1, p.LeverageCap > 0 ? Math.Min(catLev, p.LeverageCap) : catLev);
            var levKey = $"{symbol}_{side}";
            if (leverageSet.Add(levKey))
                await sim.SetLeverageAsync(symbol, leverage, side).ConfigureAwait(false);
            var qty = perSlotMargin * leverage / price;
            if (qty <= 0m) continue;
            await sim.PlaceOrderAsync(new OrderRequest(symbol, side, OrderType.Market, qty)).ConfigureAwait(false);
        }
    }

    /// <summary>Funding-Rate des Settlements bei/kurz vor t (Toleranz ±4h um den 8h-Punkt).</summary>
    private decimal? FundingAt(string symbol, DateTime t)
    {
        if (!fundingBySymbol.TryGetValue(symbol, out var hist) || hist.Count == 0) return null;
        // Letzter Punkt mit Time <= t + 4h (das gerade faellige Settlement).
        decimal? best = null;
        var bestDiff = TimeSpan.FromHours(4);
        foreach (var (time, rate) in hist)
        {
            var diff = (t - time).Duration();
            if (diff <= bestDiff) { bestDiff = diff; best = rate; }
        }
        return best;
    }

    /// <summary>Mittelwert der letzten <paramref name="n"/> Funding-Settlements vor t.</summary>
    private decimal? AvgFunding(string symbol, DateTime t, int n)
    {
        if (!fundingBySymbol.TryGetValue(symbol, out var hist) || hist.Count == 0) return null;
        var recent = hist.Where(x => x.Time <= t).TakeLast(n).Select(x => x.Rate).ToList();
        return recent.Count > 0 ? recent.Average() : null;
    }

    /// <summary>z-Standardisierung (x−μ)/σ ueber die Werte; σ≤0 → alle 0.</summary>
    private static Dictionary<string, decimal> ZScores(Dictionary<string, decimal> raw)
    {
        if (raw.Count == 0) return raw;
        var mean = raw.Values.Average();
        var variance = raw.Values.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, raw.Count - 1);
        var std = (decimal)Math.Sqrt((double)variance);
        return std <= 0m
            ? raw.ToDictionary(kv => kv.Key, _ => 0m)
            : raw.ToDictionary(kv => kv.Key, kv => (kv.Value - mean) / std);
    }

    private sealed class NullStrat : IStrategy
    {
        public static readonly NullStrat Instance = new();
        public string Name => "null";
        public string Description => "";
        public IReadOnlyList<StrategyParameter> Parameters => [];
        public bool RequiresHigherTimeframeContext => false;
        public SignalResult Evaluate(MarketContext context) => new(Signal.None, 0m, null, null, null, "carry");
        public void WarmUp(IReadOnlyList<Candle> history) { }
        public void Reset() { }
        public IStrategy Clone() => Instance;
    }
}
