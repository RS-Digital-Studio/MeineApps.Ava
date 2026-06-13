using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Reports;
using BingXBot.Backtest.Simulation;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Portfolio;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Selektions-Modus des Cross-Sectional-Korbs (Lab-Forschung, Live nutzt ausschliesslich Momentum).
/// </summary>
public enum XsecMode
{
    /// <summary>Long Top-K Momentum / short Bottom-K (klassisch, Live-Default).</summary>
    Momentum,
    /// <summary>Long Bottom-K Momentum / short Top-K — wettet auf Reversal (Literatur: Krypto-CS kippt
    /// jenseits ~1 Monat in Reversal; starker Tages-Reversal im breiten Querschnitt).</summary>
    Reversal,
    /// <summary>Long niedrigste Vol (ATR%) / short hoechste Vol — Betting-against-Beta / Low-Vol-Praemie
    /// (Literatur: positive Low-Vol-Praemie in Krypto post-2017, negativer Vol-Risk-Premium).</summary>
    LowVol,
}

/// <summary>
/// Cross-Sectional-Korb-Parameter. Bewusst parameterarm (Overfitting-Schutz).
/// </summary>
/// <param name="LookbackCandles">Lookback in Nav-Kerzen (H4). Momentum/Reversal: ROC ueber Lookback. LowVol: ATR-Fenster.</param>
/// <param name="RebalanceEveryCandles">Rebalance-Intervall in Nav-Kerzen (H4). 42 ≈ 1 Woche.</param>
/// <param name="LongK">Anzahl Long-Slots. 0 = keine Longs.</param>
/// <param name="ShortK">Anzahl Short-Slots. 0 = Long-only.</param>
/// <param name="RiskAdjusted">Momentum durch ATR% normalisieren (vol-bereinigtes Ranking). Nur Momentum/Reversal.</param>
/// <param name="AtrStopMultiplier">Per-Position-ATR-Stop zwischen Rebalances. 0 = reiner Rebalance ohne Stop.</param>
/// <param name="LeverageCap">Obergrenze fuer das Per-Position-Leverage (0 = Kategorie-Leverage aus den Settings).
///   Begrenzt das Gross-Leverage des Korbs — entscheidend auf dem Mini-Konto, wo 5× × Auslastung in
///   volatilen Crypto-Crashes zu Totalverlust-naher Varianz fuehrt.</param>
/// <param name="Mode">Selektions-Modus (Momentum/Reversal/LowVol). Live = Momentum.</param>
/// <param name="InverseVolWeight">Slots nach inverser Vol (1/ATR%) gewichten statt gleichgewichtet
///   (Risk-Parity-Overlay; Literatur: Vol-Targeting/Risk-Management hebt Krypto-Momentum-Sharpe).</param>
public readonly record struct XsecParams(
    int LookbackCandles, int RebalanceEveryCandles, int LongK, int ShortK,
    bool RiskAdjusted, decimal AtrStopMultiplier, int LeverageCap = 0,
    XsecMode Mode = XsecMode.Momentum, bool InverseVolWeight = false)
{
    public int Slots => LongK + ShortK;
    public string Label =>
        $"L{LookbackCandles}/R{RebalanceEveryCandles}/{LongK}L-{ShortK}S"
        + $"{(Mode != XsecMode.Momentum ? $"/{Mode}" : "")}"
        + $"{(RiskAdjusted && Mode != XsecMode.LowVol ? "/radj" : "")}"
        + $"{(InverseVolWeight ? "/ivw" : "")}"
        + $"{(AtrStopMultiplier > 0 ? $"/stop{AtrStopMultiplier:0.0}" : "")}"
        + $"{(LeverageCap > 0 ? $"/lev{LeverageCap}" : "")}";
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

    private async Task RebalanceAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, BotSettings settings, XsecParams p,
        int slots, HashSet<string> leverageSet, DateTime t, CancellationToken ct)
    {
        // 1.+2. Ziel-Korb. Momentum nutzt den GETEILTEN MomentumBasketCalculator (identisch zur
        // Live-Logik); Reversal/LowVol sind Lab-Forschungs-Modi (lokal, Live unberuehrt).
        // Slice = letzte Lookback+20 Kerzen bis zur aktuellen (candles[^1] = jetzt).
        var universe = states
            .Where(s => s.NavIdx >= p.LookbackCandles && t >= s.TradingStartCloseTime)
            .Select(s => (s.Symbol, (IReadOnlyList<Candle>)s.ContextSlice(p.LookbackCandles + 20)))
            .ToList();
        if (universe.Count == 0) return;
        var target = BuildTargetBasket(universe, p);

        // 3. Bestehende Positionen, die NICHT (mehr) zum Ziel-Korb passen (Symbol raus ODER Seite gedreht), schliessen.
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        foreach (var pos in positions)
        {
            if (!target.TryGetValue(pos.Symbol, out var wantSide) || wantSide != pos.Side)
                await sim.ClosePositionAsync(pos.Symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
        }

        // 4. Ziel-Positionen eroeffnen, die noch nicht gehalten werden.
        //    Gewichtung: gleichgewichtet (Default) ODER inverse-Vol (Risk-Parity-Overlay).
        positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        var held = positions.Select(pp => $"{pp.Symbol}_{pp.Side}").ToHashSet();
        var acc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
        var equity = acc.Balance + acc.UnrealizedPnl;
        if (equity <= 0m || slots <= 0) return;
        var totalMargin = equity * MarginUtilization;

        // Inverse-Vol-Gewichte (1/ATR%) je Korb-Symbol, normalisiert auf Summe 1 → perSymbolMargin.
        // Ohne Flag: Gleichgewicht (jedes Symbol 1/Slots). Geteilt fuer Long+Short (Gross-Exposure-Split).
        var weights = ComputeWeights(target, states, p);

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

            var symbolMargin = totalMargin * weights[symbol];
            var notional = symbolMargin * leverage;
            var qty = notional / price;
            if (qty <= 0m) continue;

            // Market-Entry — SimulatedExchange lehnt unter Min-Order/Margin ab (live-treu), dann kein Slot.
            await sim.PlaceOrderAsync(new OrderRequest(symbol, side, OrderType.Market, qty)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Baut den Ziel-Korb je nach <see cref="XsecMode"/>. Momentum = geteilter Live-Calculator;
    /// Reversal = long schwaechstes / short staerkstes Momentum; LowVol = long niedrigste / short
    /// hoechste ATR%-Vol (Betting-against-Beta).
    /// </summary>
    private static Dictionary<string, Side> BuildTargetBasket(
        List<(string Symbol, IReadOnlyList<Candle> Candles)> universe, XsecParams p)
    {
        if (p.Mode == XsecMode.Momentum)
            return MomentumBasketCalculator.ComputeBasket(universe, p.LookbackCandles, p.LongK, p.ShortK, p.RiskAdjusted);

        if (p.Mode == XsecMode.Reversal)
        {
            // Gleiches Momentum-Ranking, aber Seiten gespiegelt: long die schwaechsten (Bottom-K,
            // negatives Momentum), short die staerksten (Top-K, positives Momentum).
            var ranked = universe
                .Select(u => (u.Symbol, Mom: MomentumBasketCalculator.Momentum(u.Candles, p.LookbackCandles, p.RiskAdjusted)))
                .Where(x => x.Mom.HasValue)
                .OrderBy(x => x.Mom!.Value)
                .ToList();
            var basket = new Dictionary<string, Side>();
            foreach (var x in ranked.Where(x => x.Mom!.Value < 0m).Take(p.LongK))
                basket[x.Symbol] = Side.Buy;                       // long die groessten Verlierer
            foreach (var x in ranked.Where(x => x.Mom!.Value > 0m).OrderByDescending(x => x.Mom!.Value).Take(p.ShortK))
                basket[x.Symbol] = Side.Sell;                      // short die groessten Gewinner
            return basket;
        }

        // LowVol: rank nach ATR% (ATR/Close) aufsteigend → long niedrigste Vol, short hoechste.
        var byVol = universe
            .Select(u => (u.Symbol, Vol: AtrPercent(u.Candles)))
            .Where(x => x.Vol > 0m)
            .OrderBy(x => x.Vol)
            .ToList();
        var lv = new Dictionary<string, Side>();
        foreach (var x in byVol.Take(p.LongK)) lv[x.Symbol] = Side.Buy;
        foreach (var x in byVol.AsEnumerable().Reverse().Take(p.ShortK)) lv.TryAdd(x.Symbol, Side.Sell);
        return lv;
    }

    /// <summary>Gleichgewicht (1/Slots je Symbol) oder inverse-Vol (1/ATR%, normalisiert auf Σ=1).</summary>
    private static Dictionary<string, decimal> ComputeWeights(
        Dictionary<string, Side> target, List<PortfolioSymbolState> states, XsecParams p)
    {
        var n = target.Count;
        if (!p.InverseVolWeight || n == 0)
            return target.ToDictionary(kv => kv.Key, _ => n > 0 ? 1m / n : 0m);

        var inv = new Dictionary<string, decimal>();
        foreach (var sym in target.Keys)
        {
            var s = states.First(x => x.Symbol == sym);
            var vol = AtrPercent(s.ContextSlice(40));
            inv[sym] = vol > 0m ? 1m / vol : 0m;
        }
        var sum = inv.Values.Sum();
        return sum > 0m
            ? inv.ToDictionary(kv => kv.Key, kv => kv.Value / sum)
            : target.ToDictionary(kv => kv.Key, _ => 1m / n);
    }

    /// <summary>ATR%(14) = ATR/letzter Close — vergleichbare Volatilitaet ueber Symbole hinweg.</summary>
    private static decimal AtrPercent(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 16) return 0m;
        var atrList = IndicatorHelper.CalculateAtr(candles, 14);
        var atr = atrList.Count > 0 && atrList[^1].HasValue ? atrList[^1]!.Value : 0m;
        var close = candles[^1].Close;
        return close > 0m && atr > 0m ? atr / close : 0m;
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
