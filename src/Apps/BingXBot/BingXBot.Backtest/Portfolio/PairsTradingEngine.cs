using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Reports;
using BingXBot.Backtest.Simulation;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Parameter fuer den Distance-Method-Pairs-Trading-Backtest (Gatev/Goetzmann/Rouwenhorst 2006).
/// </summary>
/// <param name="FormationCandles">Formations-Fenster (Nav-Kerzen): Paar-Auswahl + Spread-Mittel/Std. 180 H4 ≈ 30 d.</param>
/// <param name="TradingCandles">Handels-Fenster pro Zyklus, danach Re-Formation. 84 H4 ≈ 14 d.</param>
/// <param name="TopPairs">Anzahl gehandelter Paare (niedrigste SSD = staerkstes Co-Movement).</param>
/// <param name="EntryZ">Eintritts-Schwelle: |Spread-z| &gt; EntryZ → long Underperformer / short Outperformer.</param>
/// <param name="ExitZ">Austritts-Schwelle: |Spread-z| &lt; ExitZ → beide Beine schliessen (Reversion erfolgt).</param>
/// <param name="StopZ">Stop: |Spread-z| &gt; StopZ → schliessen (Divergenz laeuft weg, kein Reversion-Trade mehr).</param>
/// <param name="LeverageCap">Per-Bein-Leverage-Cap (0 = Kategorie-Leverage).</param>
public readonly record struct PairsParams(
    int FormationCandles, int TradingCandles, int TopPairs,
    decimal EntryZ, decimal ExitZ, decimal StopZ, int LeverageCap = 1)
{
    public string Label =>
        $"PAIRS/F{FormationCandles}/T{TradingCandles}/{TopPairs}p/e{EntryZ:0.0}/x{ExitZ:0.0}/s{StopZ:0.0}/lev{LeverageCap}";
}

/// <summary>
/// Statistical-Arbitrage-Pairs-Trading-Backtest auf EINEM gemeinsamen Konto (gleiche Spiegelbild-
/// Infrastruktur wie <see cref="CrossSectionalMomentumEngine"/>: <see cref="MergedTimeline"/>,
/// <see cref="SimulatedExchange"/> mit echten Fees/Funding/Min-Order/Margin).
///
/// Distance-Methode (Gatev et al. 2006), market-neutral:
/// <list type="number">
/// <item><b>Formation:</b> Preise je Symbol auf einen Index normalisieren (Preis/Preis am Fensterstart);
///   pairwise Sum-of-Squared-Deviations (SSD) der normalisierten Serien; die <c>TopPairs</c> mit
///   der niedrigsten SSD (= staerkstes Co-Movement) waehlen, ohne Symbol-Ueberschneidung.</item>
/// <item><b>Trading:</b> pro Paar Spread = normA − normB; z = (Spread − μ)/σ aus dem Formations-Fenster.
///   |z| &gt; EntryZ → long das untere / short das obere Bein (gleiches Notional). |z| &lt; ExitZ → schliessen
///   (Reversion). |z| &gt; StopZ → Stop. Nach <c>TradingCandles</c> Re-Formation.</item>
/// </list>
/// Bewusst die Distance-/z-Score-Variante (kein Engle-Granger-ADF) — der etablierte, robuste
/// Standard-Ansatz; reine Preisdaten, keine Cointegrations-Teststatistik noetig.
/// </summary>
public sealed class PairsTradingEngine(
    IPublicMarketDataClient publicClient, ISymbolInfoProvider? symbolInfo, ILogger logger)
{
    private const int MinCandles = 50;
    private const int EquitySnapshotEverySteps = 6;
    private const decimal MarginUtilization = 0.75m;

    private sealed class ActivePair
    {
        public required string A;
        public required string B;
        public required decimal BaseA;   // Preis von A am Formations-Start (Normalisierungs-Anker)
        public required decimal BaseB;
        public required decimal Mean;    // Spread-Mittel im Formations-Fenster
        public required decimal Std;     // Spread-Std im Formations-Fenster
        public int Position;             // 0 = flat, +1 = long A/short B, -1 = short A/long B
    }

    public async Task<PerformanceReport> RunAsync(
        IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings settings, PairsParams p, CancellationToken ct = default)
    {
        var states = new List<PortfolioSymbolState>();
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            var nav = await publicClient.GetKlinesAsync(symbol, navTf, from, to, ct).ConfigureAwait(false);
            if (nav.Count < Math.Max(MinCandles, p.FormationCandles + 5)) continue;
            var warmupSize = Math.Min(Math.Max(p.FormationCandles, 50), nav.Count - 1);
            states.Add(new PortfolioSymbolState(symbol, nav, NullStrategyMarker.Instance)
            {
                TradingStartCloseTime = nav[warmupSize].CloseTime
            });
        }
        if (states.Count < 2) return new PerformanceReport();
        logger.LogInformation("Pairs-Backtest: {Symbols} Symbole, {Cfg}, {From}..{To}",
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
        var active = new List<ActivePair>();
        var lastFormationStep = int.MinValue;

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

            if (bt.SimulateFundingRate && (t - lastFundingTime).TotalHours >= 8)
            {
                sim.ApplyFundingRate(fundingRate);
                lastFundingTime = t;
            }

            // Re-Formation faellig? (alle TradingCandles) — vorher alle offenen Paare schliessen.
            var eligible = states.Count(s => s.NavIdx >= p.FormationCandles && t >= s.TradingStartCloseTime) >= 2;
            if (eligible && (lastFormationStep == int.MinValue || step - lastFormationStep >= p.TradingCandles))
            {
                await CloseAllPairsAsync(sim, active, states, ct).ConfigureAwait(false);
                active = FormPairs(states, p, t);
                lastFormationStep = step;
            }

            // Trading-Logik pro aktivem Paar.
            if (active.Count > 0)
                await TradePairsAsync(sim, active, states, settings, p, leverageSet, ct).ConfigureAwait(false);

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

    /// <summary>Distance-Formation: normalisierte Serien, pairwise SSD, Top-Paare ohne Symbol-Overlap.</summary>
    private static List<ActivePair> FormPairs(List<PortfolioSymbolState> states, PairsParams p, DateTime t)
    {
        // Eligible Symbole + ihre normalisierten Schluss-Serien (Preis/Preis[fensterstart]) ueber das Formations-Fenster.
        var series = new List<(string Symbol, decimal Base, decimal[] Norm)>();
        foreach (var s in states)
        {
            if (s.NavIdx < p.FormationCandles || t < s.TradingStartCloseTime) continue;
            var slice = s.ContextSlice(p.FormationCandles + 1);
            if (slice.Count < p.FormationCandles + 1) continue;
            var baseP = slice[0].Close;
            if (baseP <= 0m) continue;
            var norm = new decimal[slice.Count];
            var ok = true;
            for (var i = 0; i < slice.Count; i++)
            {
                if (slice[i].Close <= 0m) { ok = false; break; }
                norm[i] = slice[i].Close / baseP;
            }
            if (ok) series.Add((s.Symbol, baseP, norm));
        }
        if (series.Count < 2) return [];

        // Pairwise SSD (gleiche Laenge garantiert durch ContextSlice-Fenster).
        var candidates = new List<(int I, int J, decimal Ssd)>();
        for (var i = 0; i < series.Count; i++)
            for (var j = i + 1; j < series.Count; j++)
            {
                var len = Math.Min(series[i].Norm.Length, series[j].Norm.Length);
                decimal ssd = 0m;
                for (var k = 0; k < len; k++)
                {
                    var d = series[i].Norm[k] - series[j].Norm[k];
                    ssd += d * d;
                }
                candidates.Add((i, j, ssd));
            }

        var used = new HashSet<string>();
        var result = new List<ActivePair>();
        foreach (var (i, j, _) in candidates.OrderBy(c => c.Ssd))
        {
            if (result.Count >= p.TopPairs) break;
            var (symA, baseA, normA) = series[i];
            var (symB, baseB, normB) = series[j];
            if (used.Contains(symA) || used.Contains(symB)) continue;   // kein Symbol-Overlap (Diversifikation)

            // Spread-Statistik (normA − normB) im Formations-Fenster.
            var len = Math.Min(normA.Length, normB.Length);
            var spreads = new decimal[len];
            for (var k = 0; k < len; k++) spreads[k] = normA[k] - normB[k];
            var mean = spreads.Average();
            var variance = spreads.Sum(x => (x - mean) * (x - mean)) / Math.Max(1, len - 1);
            var std = (decimal)Math.Sqrt((double)variance);
            if (std <= 0m) continue;   // degeneriert (identische Serien) → unbrauchbar

            used.Add(symA); used.Add(symB);
            result.Add(new ActivePair { A = symA, B = symB, BaseA = baseA, BaseB = baseB, Mean = mean, Std = std });
        }
        return result;
    }

    private async Task TradePairsAsync(
        SimulatedExchange sim, List<ActivePair> active, List<PortfolioSymbolState> states,
        BotSettings settings, PairsParams p, HashSet<string> leverageSet, CancellationToken ct)
    {
        var acc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
        var equity = acc.Balance + acc.UnrealizedPnl;
        if (equity <= 0m) return;
        // Kapital je Paar; pro Paar zwei Beine (long+short) → je Bein die Haelfte.
        var perPairMargin = equity * MarginUtilization / Math.Max(1, p.TopPairs);
        var perLegMargin = perPairMargin / 2m;

        foreach (var pair in active)
        {
            ct.ThrowIfCancellationRequested();
            var sa = states.FirstOrDefault(x => x.Symbol == pair.A);
            var sb = states.FirstOrDefault(x => x.Symbol == pair.B);
            if (sa is null || sb is null || sa.NavIdx < 0 || sb.NavIdx < 0) continue;
            var pa = sa.CurrentCandle.Close;
            var pb = sb.CurrentCandle.Close;
            if (pa <= 0m || pb <= 0m) continue;

            var normA = pa / pair.BaseA;
            var normB = pb / pair.BaseB;
            var z = (normA - normB - pair.Mean) / pair.Std;

            if (pair.Position == 0)
            {
                if (Math.Abs(z) > p.EntryZ)
                {
                    // z > 0: A relativ teuer → short A / long B. z < 0: umgekehrt.
                    var sideA = z > 0 ? Side.Sell : Side.Buy;
                    var sideB = z > 0 ? Side.Buy : Side.Sell;
                    await OpenLegAsync(sim, settings, p, leverageSet, sa, sideA, perLegMargin).ConfigureAwait(false);
                    await OpenLegAsync(sim, settings, p, leverageSet, sb, sideB, perLegMargin).ConfigureAwait(false);
                    pair.Position = z > 0 ? -1 : 1;
                }
            }
            else
            {
                // Exit bei Reversion ODER Stop bei Weglaufen.
                if (Math.Abs(z) < p.ExitZ || Math.Abs(z) > p.StopZ)
                    await ClosePairAsync(sim, pair, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task OpenLegAsync(
        SimulatedExchange sim, BotSettings settings, PairsParams p, HashSet<string> leverageSet,
        PortfolioSymbolState state, Side side, decimal legMargin)
    {
        var price = state.CurrentCandle.Close;
        if (price <= 0m) return;
        var catLev = (int)settings.Risk.GetCategorySettings(state.Category).MaxLeverage;
        var leverage = Math.Max(1, p.LeverageCap > 0 ? Math.Min(catLev, p.LeverageCap) : catLev);
        var levKey = $"{state.Symbol}_{side}";
        if (leverageSet.Add(levKey))
            await sim.SetLeverageAsync(state.Symbol, leverage, side).ConfigureAwait(false);
        var qty = legMargin * leverage / price;
        if (qty <= 0m) return;
        await sim.PlaceOrderAsync(new OrderRequest(state.Symbol, side, OrderType.Market, qty)).ConfigureAwait(false);
    }

    private static async Task ClosePairAsync(SimulatedExchange sim, ActivePair pair, CancellationToken ct)
    {
        var positions = await sim.GetPositionsAsync(ct).ConfigureAwait(false);
        foreach (var sym in new[] { pair.A, pair.B })
            foreach (var pos in positions.Where(x => x.Symbol == sym))
                await sim.ClosePositionAsync(pos.Symbol, pos.Side, isMakerClose: false).ConfigureAwait(false);
        pair.Position = 0;
    }

    private static async Task CloseAllPairsAsync(
        SimulatedExchange sim, List<ActivePair> active, List<PortfolioSymbolState> states, CancellationToken ct)
    {
        foreach (var pair in active.Where(x => x.Position != 0))
            await ClosePairAsync(sim, pair, ct).ConfigureAwait(false);
    }

    /// <summary>Platzhalter-Strategie fuer <see cref="PortfolioSymbolState"/> (Pairs nutzt kein IStrategy).</summary>
    private sealed class NullStrategyMarker : IStrategy
    {
        public static readonly NullStrategyMarker Instance = new();
        public string Name => "null";
        public string Description => "";
        public IReadOnlyList<StrategyParameter> Parameters => [];
        public bool RequiresHigherTimeframeContext => false;
        public SignalResult Evaluate(MarketContext context) => new(Signal.None, 0m, null, null, null, "pairs");
        public void WarmUp(IReadOnlyList<Candle> history) { }
        public void Reset() { }
        public IStrategy Clone() => Instance;
    }
}
