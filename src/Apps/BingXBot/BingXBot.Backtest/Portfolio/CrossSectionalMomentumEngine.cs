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
    /// <summary>Long-Seite fest = BTC (voller 50%-Long-Anteil), Short-Seite wie Momentum-Modus
    /// Bottom-ShortK (volles Universum inkl. TradFi). Testet die Hypothese aus der Top-50-Analyse
    /// 11.08.2026: der Xsec-Edge sitzt in der Short-Seite, die Momentum-Long-Slots kaempfen gegen
    /// den strukturellen Alt-Decay — BTC ist das einzige phasen-robuste Long-Leg.</summary>
    AnchorBtc,
    /// <summary>BTC-Dominanz-Spread: Long BTC (50%) / Short die ShortK volumenstaerksten Krypto-Alts
    /// (equal-weight, KEIN Momentum-Ranking, TradFi + tokenisierte Edelmetalle ausgeschlossen).
    /// Handelbare Annaeherung an den Alt-Decay-Struktur-Trade (Alt-Index verlor −73 % vs. BTC
    /// 2022–2026, in jedem vollen Jahr negativ). Lookback/RiskAdjusted sind hier bedeutungslos.</summary>
    DominanceSpread,
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
/// <param name="SkipCandles">Skip-Period: juengste N Kerzen vom Momentum-Fenster ausschliessen
///   (klassischer 12-1-Momentum-Skip gegen kurzfristige Reversal-Kontamination). Nur Momentum/Reversal.</param>
/// <param name="VolTargetAnnualPct">Portfolio-Vol-Targeting: Ziel-Jahres-Vol der Strategie-Equity in %
///   (0 = aus). Skaliert die GESAMT-Exposure zeitvariabel (Ziel/realisiert, gekappt 0.25..2.0) — in
///   ruhigen Phasen hoch, in volatilen runter. Anders als InverseVolWeight (within-basket): das hier
///   ist die zeitvariable Gesamt-Exposure (Literatur: hebt TSMOM-Sharpe ~3x, aber conditional).</param>
/// <param name="ExitRankBuffer">Rank-Buffer/Hysterese: gehaltenes Symbol behaelt seinen Slot, solange es
///   noch in den Top-(K+Buffer) seiner Seite rankt (0 = aus). Senkt den Turnover → Fees/Slippage.</param>
/// <param name="ClusterDiversify">Max. 1 neues Symbol je Asset-Cluster pro Seite (verhindert 3 Longs mit
///   identischem Beta). Fallback nach Rang, wenn Cluster nicht fuer K Slots reichen.</param>
/// <param name="PumpFadeSlots">Pump-Fade-Overlay (Event-Setup aus der Top-50-Analyse 11.08.2026):
///   max. N gleichzeitige Event-Shorts ZUSAETZLICH zum Korb (0 = aus). Event = Krypto-Alt juenger
///   als <see cref="PumpFadeMaxAgeDays"/> mit 24h-Rendite &gt; <see cref="PumpFadeThreshold24h"/> →
///   Short fuer <see cref="PumpFadeHoldCandles"/> Kerzen mit Stop bei +<see cref="PumpFadeStopPct"/>.
///   Jeder Slot bindet 7,5 % Equity-Margin; die Korb-Utilization sinkt entsprechend.</param>
/// <param name="PumpFadeThreshold24h">24h-Pump-Schwelle (0.20 = +20 %).</param>
/// <param name="PumpFadeStopPct">Stop-Distanz gegen den Short (0.15 = +15 % ueber Entry, High-basiert).</param>
/// <param name="PumpFadeHoldCandles">Zeit-Exit nach N Nav-Kerzen (6 = 24 h auf H4).</param>
/// <param name="PumpFadeMaxAgeDays">Max. Coin-Alter seit Erstlisting (D1-Probe ab 2021).</param>
public readonly record struct XsecParams(
    int LookbackCandles, int RebalanceEveryCandles, int LongK, int ShortK,
    bool RiskAdjusted, decimal AtrStopMultiplier, int LeverageCap = 0,
    XsecMode Mode = XsecMode.Momentum, bool InverseVolWeight = false, int SkipCandles = 0,
    decimal VolTargetAnnualPct = 0m, int ExitRankBuffer = 0, bool ClusterDiversify = false,
    int PumpFadeSlots = 0, decimal PumpFadeThreshold24h = 0.20m, decimal PumpFadeStopPct = 0.15m,
    int PumpFadeHoldCandles = 6, int PumpFadeMaxAgeDays = 180)
{
    public int Slots => LongK + ShortK;
    public string Label =>
        $"L{LookbackCandles}/R{RebalanceEveryCandles}/{LongK}L-{ShortK}S"
        + $"{(Mode != XsecMode.Momentum ? $"/{Mode}" : "")}"
        + $"{(RiskAdjusted && Mode != XsecMode.LowVol ? "/radj" : "")}"
        + $"{(SkipCandles > 0 ? $"/skip{SkipCandles}" : "")}"
        + $"{(InverseVolWeight ? "/ivw" : "")}"
        + $"{(VolTargetAnnualPct > 0 ? $"/vt{VolTargetAnnualPct:0}" : "")}"
        + $"{(AtrStopMultiplier > 0 ? $"/stop{AtrStopMultiplier:0.0}" : "")}"
        + $"{(ExitRankBuffer > 0 ? $"/buf{ExitRankBuffer}" : "")}"
        + $"{(ClusterDiversify ? "/cdiv" : "")}"
        + $"{(PumpFadeSlots > 0 ? $"/pf{PumpFadeSlots}x{PumpFadeThreshold24h * 100:0}" : "")}"
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

        // Pump-Fade-Overlay: Erstlisting je Symbol via D1-Probe ab 2021 (Phasen-Klines beginnen am
        // Phasenstart und taugen nicht als Alter). Symbole, die schon 2021 existieren, sind nie "jung".
        var listingTime = new Dictionary<string, DateTime>();
        if (p.PumpFadeSlots > 0)
        {
            var probeFrom = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (var s in states)
            {
                if (!IsCryptoAlt(s.Symbol)) continue;
                var d1 = await publicClient.GetKlinesAsync(s.Symbol, TimeFrame.D1, probeFrom, to, ct).ConfigureAwait(false);
                if (d1.Count > 0) listingTime[s.Symbol] = d1[0].OpenTime;
            }
        }

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
        // Pump-Fade-Overlay-State: Symbol → (Entry-Schritt, Entry-Preis).
        var pumpFade = new Dictionary<string, (int EntryStep, decimal EntryPrice)>();

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

            // c2. Pump-Fade-Overlay: offene Event-Shorts verwalten (Stop zuerst, dann Zeit-Exit).
            if (pumpFade.Count > 0)
                await ManagePumpFadeAsync(sim, states, p, pumpFade, step).ConfigureAwait(false);

            // d. Rebalance faellig?
            var eligibleStart = step >= 0 && states.Any(s => s.NavIdx >= p.LookbackCandles + p.SkipCandles && t >= s.TradingStartCloseTime);
            if (eligibleStart && (lastRebalanceStep == int.MinValue || step - lastRebalanceStep >= p.RebalanceEveryCandles))
            {
                // Portfolio-Vol-Targeting: Gesamt-Exposure nach realisierter Equity-Vol skalieren.
                var volScale = p.VolTargetAnnualPct > 0m ? ComputeVolScale(equityCurve, p.VolTargetAnnualPct) : 1m;
                await RebalanceAsync(sim, states, settings, p, slots, leverageSet, t, volScale, pumpFade.Keys, ct).ConfigureAwait(false);
                lastRebalanceStep = step;
            }

            // d2. Pump-Fade-Overlay: neue Events shorten (nach dem Rebalance — Korb hat Vorrang).
            if (p.PumpFadeSlots > 0 && pumpFade.Count < p.PumpFadeSlots)
                await ScanPumpFadeEntriesAsync(sim, states, settings, p, listingTime, leverageSet, pumpFade, step, t).ConfigureAwait(false);

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
        int slots, HashSet<string> leverageSet, DateTime t, decimal volScale,
        IReadOnlyCollection<string> pumpFadeSymbols, CancellationToken ct)
    {
        // 1.+2. Ziel-Korb. Momentum nutzt den GETEILTEN MomentumBasketCalculator (identisch zur
        // Live-Logik); Reversal/LowVol sind Lab-Forschungs-Modi (lokal, Live unberuehrt).
        // Slice = letzte Lookback+20 Kerzen bis zur aktuellen (candles[^1] = jetzt).
        var universe = states
            .Where(s => s.NavIdx >= p.LookbackCandles + p.SkipCandles && t >= s.TradingStartCloseTime)
            .Select(s => (s.Symbol, (IReadOnlyList<Candle>)s.ContextSlice(p.LookbackCandles + p.SkipCandles + 20)))
            .ToList();
        if (universe.Count == 0) return;

        // Ist-Korb VOR der Ziel-Bildung erfassen — der Rank-Buffer (Hysterese) braucht die aktuell
        // gehaltenen Symbole, um sie innerhalb des Buffer-Fensters im Slot zu lassen.
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        var currentBasket = new Dictionary<string, Side>();
        foreach (var pos in positions)
            if (!pumpFadeSymbols.Contains(pos.Symbol)) currentBasket[pos.Symbol] = pos.Side;

        var target = BuildTargetBasket(universe, p, currentBasket);
        // Pump-Fade-Symbole gehoeren dem Overlay: der Korb besetzt sie nicht (kein Seiten-Mix).
        foreach (var pf in pumpFadeSymbols) target.Remove(pf);

        // 3. Bestehende Positionen, die NICHT (mehr) zum Ziel-Korb passen (Symbol raus ODER Seite gedreht), schliessen.
        foreach (var pos in positions)
        {
            // Offene Pump-Fade-Shorts verwaltet ausschliesslich das Overlay (Stop/Zeit-Exit).
            if (pos.Side == Side.Sell && pumpFadeSymbols.Contains(pos.Symbol)) continue;
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
        // Pump-Fade-Slots reservieren je 7,5 % Equity-Margin — der Korb weicht entsprechend zurueck.
        var utilization = Math.Max(0.10m, MarginUtilization - PumpFadeMarginPerSlot * p.PumpFadeSlots);
        var totalMargin = equity * utilization * volScale;   // volScale=1 wenn Vol-Targeting aus

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
        List<(string Symbol, IReadOnlyList<Candle> Candles)> universe, XsecParams p,
        IReadOnlyDictionary<string, Side> currentBasket)
    {
        if (p.Mode == XsecMode.Momentum)
            return MomentumBasketCalculator.ComputeBasket(universe, p.LookbackCandles, p.LongK, p.ShortK,
                p.RiskAdjusted, p.SkipCandles, currentBasket, p.ExitRankBuffer, p.ClusterDiversify);

        if (p.Mode is XsecMode.AnchorBtc or XsecMode.DominanceSpread)
        {
            // Ohne Anchor im Universum KEIN Korb (sonst waere der Rest ein net-short-Direktionaltrade).
            var basket = new Dictionary<string, Side>();
            if (!universe.Any(u => u.Symbol == AnchorSymbol)) return basket;
            basket[AnchorSymbol] = Side.Buy;

            if (p.Mode == XsecMode.AnchorBtc)
            {
                // Short-Seite identisch zur Live-Momentum-Logik (Bottom-K, nur negatives Momentum).
                var worst = universe
                    .Where(u => u.Symbol != AnchorSymbol)
                    .Select(u => (u.Symbol, Mom: MomentumBasketCalculator.Momentum(u.Candles, p.LookbackCandles, p.RiskAdjusted, p.SkipCandles)))
                    .Where(x => x.Mom.HasValue && x.Mom!.Value < 0m)
                    .OrderBy(x => x.Mom!.Value)
                    .Take(p.ShortK);
                foreach (var x in worst) basket[x.Symbol] = Side.Sell;
            }
            else
            {
                // DominanceSpread: geteilte Live-Logik (Paritaet wie beim Momentum-Modus).
                return MomentumBasketCalculator.ComputeDominanceBasket(universe, longK: 1, shortK: p.ShortK);
            }
            return basket;
        }

        if (p.Mode == XsecMode.Reversal)
        {
            // Gleiches Momentum-Ranking, aber Seiten gespiegelt: long die schwaechsten (Bottom-K,
            // negatives Momentum), short die staerksten (Top-K, positives Momentum).
            var ranked = universe
                .Select(u => (u.Symbol, Mom: MomentumBasketCalculator.Momentum(u.Candles, p.LookbackCandles, p.RiskAdjusted, p.SkipCandles)))
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

    /// <summary>Gleichgewicht (1/Slots je Symbol) oder inverse-Vol (1/ATR%, normalisiert auf Σ=1).
    /// Anker-Modi (AnchorBtc/DominanceSpread): seitenweise 50/50 — der eine Long (BTC) traegt die
    /// vollen 50 %, jeder Short 0.5/ShortK (fehlende Shorts bleiben Cash statt die Seite zu
    /// konzentrieren). InverseVolWeight wird in den Anker-Modi ignoriert.</summary>
    private static Dictionary<string, decimal> ComputeWeights(
        Dictionary<string, Side> target, List<PortfolioSymbolState> states, XsecParams p)
    {
        if (p.Mode is XsecMode.AnchorBtc or XsecMode.DominanceSpread)
        {
            var shortSlots = Math.Max(1, p.ShortK);
            return target.ToDictionary(
                kv => kv.Key,
                kv => kv.Value == Side.Buy ? 0.5m : 0.5m / shortSlots);
        }

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

    /// <summary>
    /// Portfolio-Vol-Targeting-Skalierung: realisierte annualisierte Vol der Strategie-Equity (aus den
    /// letzten ~30 Tages-Snapshots) gegen die Ziel-Vol; Faktor = Ziel/realisiert, gekappt 0.25..2.0.
    /// Bei zu wenig Historie oder Null-Vol = 1 (kein Eingriff). Snapshots ~1/Tag (EquitySnapshotEverySteps).
    /// </summary>
    private static decimal ComputeVolScale(List<EquityPoint> equity, decimal targetAnnualPct)
    {
        const int window = 30;   // ~30 Tage
        if (equity.Count < window + 1) return 1m;
        var recent = equity.Skip(equity.Count - (window + 1)).ToList();
        var rets = new List<double>();
        for (var i = 1; i < recent.Count; i++)
        {
            var prev = (double)recent[i - 1].Equity;
            if (prev > 0) rets.Add((double)recent[i].Equity / prev - 1.0);
        }
        if (rets.Count < 5) return 1m;
        var mean = rets.Average();
        var variance = rets.Sum(r => (r - mean) * (r - mean)) / (rets.Count - 1);
        var dailyVol = Math.Sqrt(variance);
        var annualVol = dailyVol * Math.Sqrt(365.0) * 100.0;   // in %
        if (annualVol <= 0.01) return 1m;
        var scale = (double)targetAnnualPct / annualVol;
        return (decimal)Math.Clamp(scale, 0.25, 2.0);
    }

    /// <summary>Fester Long-Anker der Modi AnchorBtc/DominanceSpread (geteilte Live-Konstante).</summary>
    private const string AnchorSymbol = MomentumBasketCalculator.DominanceAnchorSymbol;

    /// <summary>Equity-Margin-Anteil je Pump-Fade-Slot (2 Slots = 15 %; der Korb weicht zurueck).</summary>
    private const decimal PumpFadeMarginPerSlot = 0.075m;

    /// <summary>
    /// Pump-Fade-Verwaltung: Stop-Check (High der aktuellen Kerze reisst Entry×(1+Stop)) VOR dem
    /// Zeit-Exit (HoldCandles Schritte nach Entry, Close zum Kerzen-Close). Entry-Kerze selbst wird
    /// nicht gestoppt (Event wird am Kerzen-Close erkannt — das High lag davor).
    /// </summary>
    private static async Task ManagePumpFadeAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, XsecParams p,
        Dictionary<string, (int EntryStep, decimal EntryPrice)> pumpFade, int step)
    {
        foreach (var (symbol, pf) in pumpFade.ToList())
        {
            var s = states.FirstOrDefault(x => x.Symbol == symbol);
            if (s is null || s.NavIdx < 0) continue;
            var candle = s.CurrentCandle;

            if (step > pf.EntryStep)
            {
                var stopPrice = pf.EntryPrice * (1m + p.PumpFadeStopPct);
                if (candle.High >= stopPrice)
                {
                    sim.SetCurrentPrice(symbol, stopPrice);
                    await sim.ClosePositionAsync(symbol, Side.Sell, isMakerClose: false).ConfigureAwait(false);
                    sim.SetCurrentPrice(symbol, candle.Close);
                    pumpFade.Remove(symbol);
                    continue;
                }
            }

            if (step - pf.EntryStep >= p.PumpFadeHoldCandles)
            {
                await sim.ClosePositionAsync(symbol, Side.Sell, isMakerClose: false).ConfigureAwait(false);
                pumpFade.Remove(symbol);
            }
        }
    }

    /// <summary>
    /// Pump-Fade-Entry-Scan: Krypto-Alt juenger als MaxAgeDays (Erstlisting via D1-Probe), 24h-Rendite
    /// ueber der Schwelle, weder im Korb noch als Overlay offen → Market-Short mit 7,5 % Equity-Margin.
    /// Kandidaten werden nach QUALITAET (Pump-Staerke absteigend) gefuellt, nicht nach Scan-Reihenfolge —
    /// der Edge steigt monoton mit der Pump-Groesse (Analyse 11.08.2026: +20 % → 180 bp, +50 % → 486 bp).
    /// Slot gilt erst als belegt, wenn die Position tatsaechlich existiert (Min-Order-Reject = kein Slot).
    /// </summary>
    private static async Task ScanPumpFadeEntriesAsync(
        SimulatedExchange sim, List<PortfolioSymbolState> states, BotSettings settings, XsecParams p,
        IReadOnlyDictionary<string, DateTime> listingTime, HashSet<string> leverageSet,
        Dictionary<string, (int EntryStep, decimal EntryPrice)> pumpFade, int step, DateTime t)
    {
        var positions = await sim.GetPositionsAsync().ConfigureAwait(false);
        var heldSymbols = positions.Select(pos => pos.Symbol).ToHashSet();
        var acc = await sim.GetAccountInfoAsync().ConfigureAwait(false);
        var equity = acc.Balance + acc.UnrealizedPnl;
        if (equity <= 0m) return;

        // 1. Alle gueltigen Events dieses Schritts sammeln.
        var candidates = new List<(PortfolioSymbolState State, decimal R24, decimal Close)>();
        foreach (var s in states)
        {
            if (s.NavIdx < 6 || pumpFade.ContainsKey(s.Symbol) || heldSymbols.Contains(s.Symbol)) continue;
            if (!listingTime.TryGetValue(s.Symbol, out var listed)) continue;
            var ageDays = (t - listed).TotalDays;
            if (ageDays < 1 || ageDays >= p.PumpFadeMaxAgeDays) continue;

            var closeNow = s.Nav[s.NavIdx].Close;
            var close24hAgo = s.Nav[s.NavIdx - 6].Close;
            if (close24hAgo <= 0m || closeNow <= 0m) continue;
            var r24 = closeNow / close24hAgo - 1m;
            if (r24 <= p.PumpFadeThreshold24h) continue;
            candidates.Add((s, r24, closeNow));
        }

        // 2. Staerkste Pumps zuerst in die freien Slots.
        foreach (var (s, _, closeNow) in candidates.OrderByDescending(c => c.R24))
        {
            if (pumpFade.Count >= p.PumpFadeSlots) break;

            var catLev = (int)settings.Risk.GetCategorySettings(s.Category).MaxLeverage;
            var leverage = Math.Max(1, p.LeverageCap > 0 ? Math.Min(catLev, p.LeverageCap) : catLev);
            if (leverageSet.Add($"{s.Symbol}_{Side.Sell}"))
                await sim.SetLeverageAsync(s.Symbol, leverage, Side.Sell).ConfigureAwait(false);

            var qty = equity * PumpFadeMarginPerSlot * leverage / closeNow;
            if (qty <= 0m) continue;
            await sim.PlaceOrderAsync(new OrderRequest(s.Symbol, Side.Sell, OrderType.Market, qty)).ConfigureAwait(false);

            // Reject-sicher: Slot nur belegen, wenn die Short-Position wirklich existiert.
            var after = await sim.GetPositionsAsync().ConfigureAwait(false);
            if (after.Any(pos => pos.Symbol == s.Symbol && pos.Side == Side.Sell))
                pumpFade[s.Symbol] = (step, closeNow);
        }
    }

    /// <summary>Krypto-Alt-Filter — geteilte Live-Logik (MomentumBasketCalculator).</summary>
    private static bool IsCryptoAlt(string symbol) => MomentumBasketCalculator.IsCryptoAlt(symbol);

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
