using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Reports;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Indicators;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Cross-Sectional-Momentum-Parameter. Bewusst parameterarm (Overfitting-Schutz).
/// </summary>
/// <param name="LookbackCandles">Momentum-Lookback in Nav-Kerzen (H4). ROC = Close[t]/Close[t-Lookback]-1.</param>
/// <param name="RebalanceEveryCandles">Rebalance-Intervall in Nav-Kerzen (H4). 42 ≈ 1 Woche.</param>
/// <param name="LongK">Anzahl Long-Slots (staerkste Momentum-Symbole). 0 = keine Longs.</param>
/// <param name="ShortK">Anzahl Short-Slots (schwaechste). 0 = Long-only.</param>
/// <param name="RiskAdjusted">Momentum durch ATR% normalisieren (vol-bereinigtes Ranking).</param>
/// <param name="AtrStopMultiplier">Per-Position-ATR-Stop zwischen Rebalances. 0 = reiner Rebalance ohne Stop.</param>
/// <param name="LeverageCap">Obergrenze fuer das Per-Position-Leverage (0 = Kategorie-Leverage aus den Settings).
///   Begrenzt das Gross-Leverage des Korbs — entscheidend auf dem Mini-Konto, wo 5× × Auslastung in
///   volatilen Crypto-Crashes zu Totalverlust-naher Varianz fuehrt.</param>
public readonly record struct XsecParams(
    int LookbackCandles, int RebalanceEveryCandles, int LongK, int ShortK,
    bool RiskAdjusted, decimal AtrStopMultiplier, int LeverageCap = 0)
{
    public int Slots => LongK + ShortK;
    public string Label =>
        $"L{LookbackCandles}/R{RebalanceEveryCandles}/{LongK}L-{ShortK}S{(RiskAdjusted ? "/radj" : "")}{(AtrStopMultiplier > 0 ? $"/stop{AtrStopMultiplier:0.0}" : "")}{(LeverageCap > 0 ? $"/lev{LeverageCap}" : "")}";
}

/// <summary>
/// Cross-Sectional-Momentum-Backtest auf EINEM gemeinsamen Konto (Spiegelbild-Infrastruktur des
/// <see cref="PortfolioBacktestEngine"/>: <see cref="MergedTimeline"/>, <see cref="PortfolioSymbolState"/>,
/// <see cref="SimulatedExchange"/> mit echten Fees/Funding/Min-Order/Margin-Gates).
///
/// Anders als die direktionalen IStrategy-Drop-ins (die in einer Phase kippen — siehe Phasen-Screen)
/// ist dieser Ansatz STRUKTURELL phasen-robust: er handelt RELATIV (long die staerksten, short die
/// schwaechsten Symbole), statt eine absolute Richtung zu wetten. In einer Baisse short man die
/// Schwaechsten und long die „am wenigsten Schwachen", in einer Hausse umgekehrt.
///
/// Mechanik: alle RebalanceEveryCandles wird das Universum nach Momentum gerankt; Ziel-Korb = Top-LongK
/// long + Bottom-ShortK short, gleichgewichtet nach Equity×Leverage. Positionen ausserhalb des Korbs
/// werden geschlossen, neue eroeffnet. Optionaler ATR-Stop zwischen Rebalances.
/// </summary>
public sealed class CrossSectionalMomentumEngine(
    IPublicMarketDataClient publicClient, ISymbolInfoProvider? symbolInfo, ILogger logger)
{
    private const int MinCandles = 50;
    private const int EquitySnapshotEverySteps = 6;
    /// <summary>Equity-Auslastung pro Rebalance (≤ MaxTotalMargin/100, lässt Puffer für Fees/Slippage).</summary>
    private const decimal MarginUtilization = 0.75m;

    public async Task<PerformanceReport> RunAsync(
        IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings settings, XsecParams p, CancellationToken ct = default)
    {
        // 1. Pro Symbol Nav-Kerzen laden (genug Vorlauf fuer LookbackCandles).
        var states = new List<PortfolioSymbolState>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            var nav = await publicClient.GetKlinesAsync(symbol, navTf, from, to, ct).ConfigureAwait(false);
            if (nav.Count < Math.Max(MinCandles, p.LookbackCandles + 5)) continue;
            // Keine Strategie-Instanz noetig (kein IStrategy) — Platzhalter, der Slot bleibt ungenutzt.
            var warmupSize = Math.Min(Math.Max(p.LookbackCandles, 50), nav.Count - 1);
            states.Add(new PortfolioSymbolState(symbol, nav, NullStrategy.Instance)
            {
                TradingStartCloseTime = nav[warmupSize].CloseTime
            });
        }
        if (states.Count == 0) return new PerformanceReport();
        logger.LogInformation("Xsec-Backtest: {Symbols} Symbole, {Cfg}, {From}..{To}",
            states.Count, p.Label, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        var bt = settings.Backtest;
        using var sim = new SimulatedExchange(bt, symbolInfo);
        var fundingRate = bt.SimulatedFundingRatePercent / 100m;
        sim.SetFundingRate(fundingRate);

        var timeline = MergedTimeline.Build(states.Select(s => (IReadOnlyList<Candle>)s.Nav));
        if (timeline.Count == 0) return new PerformanceReport();

        var equityCurve = new List<EquityPoint>();
        var lastFundingTime = timeline[0];
        var leverageSet = new HashSet<string>();
        var slots = Math.Min(p.Slots, settings.Risk.MaxOpenPositions);
        var lastRebalanceStep = int.MinValue;

        for (var step = 0; step < timeline.Count; step++)
        {
            ct.ThrowIfCancellationRequested();
            var t = timeline[step];
            sim.SetCurrentBacktestTime(t);

            // a. Preise ALLER Symbole mit bekannter Kerze setzen (konto-weite Equity/Funding korrekt).
            foreach (var s in states)
            {
                s.AdvanceTo(t);
                if (s.NavIdx >= 0) sim.SetCurrentPrice(s.Symbol, s.CurrentCandle.Close);
            }

            // b. Funding alle 8h auf alle offenen Positionen.
            if (bt.SimulateFundingRate && (t - lastFundingTime).TotalHours >= 8)
            {
                sim.ApplyFundingRate(fundingRate);
                lastFundingTime = t;
            }

            // c. ATR-Stop zwischen Rebalances (optional): Position schliessen, wenn die Kerze den Stop reisst.
            if (p.AtrStopMultiplier > 0m)
                await ApplyAtrStopsAsync(sim, states, p, ct).ConfigureAwait(false);

            // d. Rebalance faellig?
            var eligibleStart = step >= 0 && states.Any(s => s.NavIdx >= p.LookbackCandles && t >= s.TradingStartCloseTime);
            if (eligibleStart && (lastRebalanceStep == int.MinValue || step - lastRebalanceStep >= p.RebalanceEveryCandles))
            {
                await RebalanceAsync(sim, states, settings, p, slots, leverageSet, t, ct).ConfigureAwait(false);
                lastRebalanceStep = step;
            }

            // e. Equity-Snapshot ~1×/Tag.
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

    /// <summary>Berechnet Momentum (ROC, optional ATR%-bereinigt) fuer ein Symbol am aktuellen NavIdx.</summary>
    private static decimal? Momentum(PortfolioSymbolState s, XsecParams p)
    {
        var i = s.NavIdx;
        if (i < p.LookbackCandles) return null;
        var past = s.Nav[i - p.LookbackCandles].Close;
        var now = s.Nav[i].Close;
        if (past <= 0m || now <= 0m) return null;
        var roc = now / past - 1m;
        if (!p.RiskAdjusted) return roc;
        // Vol-bereinigt: ROC / (ATR/Close). Normalisiert ueber unterschiedlich volatile Symbole.
        var atr = IndicatorHelper.CalculateAtr(s.ContextSlice(p.LookbackCandles + 20), 14);
        var lastAtr = atr.Count > 0 && atr[^1].HasValue ? atr[^1]!.Value : 0m;
        var atrPct = lastAtr > 0m ? lastAtr / now : 0m;
        return atrPct > 0m ? roc / atrPct : roc;
    }

    private async Task RebalanceAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, BotSettings settings, XsecParams p,
        int slots, HashSet<string> leverageSet, DateTime t, CancellationToken ct)
    {
        // 1. Momentum aller eligiblen Symbole.
        var ranked = states
            .Where(s => s.NavIdx >= p.LookbackCandles && t >= s.TradingStartCloseTime)
            .Select(s => (State: s, Mom: Momentum(s, p)))
            .Where(x => x.Mom.HasValue)
            .OrderByDescending(x => x.Mom!.Value)
            .ToList();
        if (ranked.Count == 0) return;

        // 2. Ziel-Korb: Top-LongK (Momentum > 0) long, Bottom-ShortK (Momentum < 0) short.
        var target = new Dictionary<string, Side>();
        foreach (var x in ranked.Where(x => x.Mom!.Value > 0m).Take(p.LongK))
            target[x.State.Symbol] = Side.Buy;
        foreach (var x in ranked.Where(x => x.Mom!.Value < 0m).OrderBy(x => x.Mom!.Value).Take(p.ShortK))
            target[x.State.Symbol] = Side.Sell;

        // 3. Bestehende Positionen, die NICHT (mehr) zum Ziel-Korb passen (Symbol raus ODER Seite gedreht), schliessen.
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        foreach (var pos in positions)
        {
            if (!target.TryGetValue(pos.Symbol, out var wantSide) || wantSide != pos.Side)
                await sim.ClosePositionAsync(pos.Symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
        }

        // 4. Ziel-Positionen eroeffnen, die noch nicht gehalten werden (gleichgewichtet nach Equity×Leverage).
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

            var category = state.Category;
            var catLev = (int)settings.Risk.GetCategorySettings(category).MaxLeverage;
            var leverage = Math.Max(1, p.LeverageCap > 0 ? Math.Min(catLev, p.LeverageCap) : catLev);
            var levKey = $"{symbol}_{side}";
            if (leverageSet.Add(levKey))
                await sim.SetLeverageAsync(symbol, leverage, side).ConfigureAwait(false);

            var notional = perSlotMargin * leverage;
            var qty = notional / price;
            if (qty <= 0m) continue;

            // Market-Entry — SimulatedExchange lehnt unter Min-Order/Margin ab (live-treu), dann kein Slot.
            await sim.PlaceOrderAsync(new OrderRequest(symbol, side, OrderType.Market, qty)).ConfigureAwait(false);
        }
    }

    /// <summary>Per-Position-ATR-Stop: schliesst eine Position, wenn die aktuelle Kerze den Stop reisst.</summary>
    private async Task ApplyAtrStopsAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, XsecParams p, CancellationToken ct)
    {
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        if (positions.Count == 0) return;
        foreach (var pos in positions)
        {
            var s = states.FirstOrDefault(x => x.Symbol == pos.Symbol);
            if (s is null || s.NavIdx < 0) continue;
            var atrList = IndicatorHelper.CalculateAtr(s.ContextSlice(40), 14);
            var atr = atrList.Count > 0 && atrList[^1].HasValue ? atrList[^1]!.Value : 0m;
            if (atr <= 0m) continue;
            var candle = s.CurrentCandle;
            var stopDist = p.AtrStopMultiplier * atr;
            var hit = pos.Side == Side.Buy
                ? candle.Low <= pos.EntryPrice - stopDist
                : candle.High >= pos.EntryPrice + stopDist;
            if (hit)
            {
                var fill = pos.Side == Side.Buy ? pos.EntryPrice - stopDist : pos.EntryPrice + stopDist;
                sim.SetCurrentPrice(pos.Symbol, fill);
                await sim.ClosePositionAsync(pos.Symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
                sim.SetCurrentPrice(pos.Symbol, candle.Close);
            }
        }
    }

    /// <summary>Platzhalter-Strategie fuer <see cref="PortfolioSymbolState"/> (Cross-Sectional nutzt kein IStrategy).</summary>
    private sealed class NullStrategy : IStrategy
    {
        public static readonly NullStrategy Instance = new();
        public string Name => "null";
        public string Description => "";
        public IReadOnlyList<StrategyParameter> Parameters => [];
        public bool RequiresHigherTimeframeContext => false;
        public SignalResult Evaluate(MarketContext context) => new(Signal.None, 0m, null, null, null, "xsec");
        public void WarmUp(IReadOnlyList<Candle> history) { }
        public void Reset() { }
        public IStrategy Clone() => Instance;
    }
}
