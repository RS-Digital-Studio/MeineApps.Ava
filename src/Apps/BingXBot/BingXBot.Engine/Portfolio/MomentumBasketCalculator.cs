using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Portfolio;

/// <summary>
/// Reine, exchange- und backtest-agnostische Kernlogik der Cross-Sectional-Momentum-Strategie:
/// Momentum-Berechnung je Symbol + Bildung des Ziel-Korbs (long Top-K / short Bottom-K).
///
/// Bewusst getrennt von der Order-Ausfuehrung: Backtest (<c>CrossSectionalMomentumEngine</c> gegen
/// <c>SimulatedExchange</c>) UND der spaetere Live-Service rufen denselben Calculator fuer das WAS
/// (welche Symbole long/short), fuehren das WIE (Order/Reconciliation) aber getrennt aus. So bleibt
/// die Backtest-Live-Paritaet genau dort minimal, wo sie entsteht — beim Ranking/Korb.
/// </summary>
public static class MomentumBasketCalculator
{
    /// <summary>
    /// Momentum eines Symbols. Die Kerzen enden bei „jetzt" (<c>candles[^1]</c> = aktuelle, geschlossene
    /// Kerze); es wird die ROC ueber <paramref name="lookback"/> Kerzen gerechnet. Bei
    /// <paramref name="riskAdjusted"/> wird durch ATR% (ATR/Close, 14er-ATR ueber die uebergebenen Kerzen)
    /// normalisiert — so sind unterschiedlich volatile Symbole vergleichbar. <c>null</c>, wenn zu wenige
    /// Kerzen oder ungueltige Preise.
    /// </summary>
    /// <param name="skip">Skip-Period in Kerzen: ROC ueber [now-skip-lookback .. now-skip] statt bis jetzt.
    ///   Schliesst die juengsten <paramref name="skip"/> Kerzen aus, um die kurzfristige Reversal-
    ///   Kontamination am kurzen Ende zu vermeiden (klassischer 12-1-Momentum-Skip). Default 0 =
    ///   bisheriges Verhalten (Live unveraendert).</param>
    public static decimal? Momentum(IReadOnlyList<Candle> candles, int lookback, bool riskAdjusted, int skip = 0)
    {
        if (candles.Count <= lookback + skip) return null;
        var now = candles[^(1 + skip)].Close;
        var past = candles[candles.Count - 1 - lookback - skip].Close;
        if (past <= 0m || now <= 0m) return null;
        var roc = now / past - 1m;
        if (!riskAdjusted) return roc;

        var atr = IndicatorHelper.CalculateAtr(candles, 14);
        var lastAtr = atr.Count > 0 && atr[^1].HasValue ? atr[^1]!.Value : 0m;
        var atrPct = lastAtr > 0m ? lastAtr / now : 0m;
        return atrPct > 0m ? roc / atrPct : roc;
    }

    /// <summary>
    /// Bildet den Ziel-Korb: die <paramref name="longK"/> Symbole mit dem hoechsten POSITIVEN Momentum
    /// werden long, die <paramref name="shortK"/> mit dem niedrigsten NEGATIVEN Momentum short. Symbole
    /// ohne berechenbares Momentum (zu wenig Historie) fallen raus. Ergebnis: <c>Symbol → Side</c>.
    /// </summary>
    public static Dictionary<string, Side> ComputeBasket(
        IEnumerable<(string Symbol, IReadOnlyList<Candle> Candles)> universe,
        int lookback, int longK, int shortK, bool riskAdjusted, int skip = 0)
        => ComputeBasket(universe, lookback, longK, shortK, riskAdjusted, skip,
            currentBasket: null, exitRankBuffer: 0, clusterDiversify: false);

    /// <summary>
    /// Erweiterte Korb-Bildung mit zwei optionalen Struktur-Mechanismen (beide neutral bei
    /// Default-Werten — der 3-Parameter-Basispfad bleibt bit-identisch):
    /// <list type="bullet">
    /// <item><b>Rank-Buffer/Hysterese</b> (<paramref name="exitRankBuffer"/> &gt; 0 + <paramref name="currentBasket"/>):
    ///   ein bereits gehaltenes Symbol behaelt seinen Slot, solange es noch innerhalb der
    ///   Top-(K+Buffer) seiner Seite rankt (und das Momentum-Vorzeichen stimmt) — klassische
    ///   Momentum-Turnover-Senkung, spart Fees/Slippage bei jedem Rebalance.</item>
    /// <item><b>Cluster-Diversifikation</b> (<paramref name="clusterDiversify"/>): max. 1 NEUES Symbol je
    ///   <see cref="AssetCluster"/> pro Seite (gehaltene Symbole werden nicht zwangsgetauscht, zaehlen
    ///   aber als belegtes Cluster). Verhindert 3 Longs mit identischem Beta (z.B. 3× AltL1).
    ///   Reichen die Cluster nicht fuer K Slots, wird nach Rang aufgefuellt (Exposure-Paritaet).</item>
    /// </list>
    /// </summary>
    public static Dictionary<string, Side> ComputeBasket(
        IEnumerable<(string Symbol, IReadOnlyList<Candle> Candles)> universe,
        int lookback, int longK, int shortK, bool riskAdjusted, int skip,
        IReadOnlyDictionary<string, Side>? currentBasket, int exitRankBuffer, bool clusterDiversify)
    {
        var ranked = universe
            .Select(u => (u.Symbol, Mom: Momentum(u.Candles, lookback, riskAdjusted, skip)))
            .Where(x => x.Mom.HasValue)
            .OrderByDescending(x => x.Mom!.Value)
            .ToList();

        var basket = new Dictionary<string, Side>();
        SelectSide(ranked.Where(x => x.Mom!.Value > 0m).Select(x => x.Symbol).ToList(),
            longK, Side.Buy, currentBasket, exitRankBuffer, clusterDiversify, basket);
        SelectSide(ranked.Where(x => x.Mom!.Value < 0m).OrderBy(x => x.Mom!.Value).Select(x => x.Symbol).ToList(),
            shortK, Side.Sell, currentBasket, exitRankBuffer, clusterDiversify, basket);
        return basket;
    }

    // ─────────── BTC-Dominanz-Spread (Struktur-Analyse + Harness-Validierung 11.08.2026) ───────────

    /// <summary>Fester Long-Anker des Dominanz-Spreads.</summary>
    public const string DominanceAnchorSymbol = "BTC-USDT";

    /// <summary>
    /// BTC-Dominanz-Spread-Korb: Long BTC (Anker) / Short die <paramref name="shortK"/>
    /// volumenstaerksten Krypto-Alts (KEIN Momentum-Ranking — der Edge ist der strukturelle
    /// Alt-Decay: 78 % der Alts underperformen BTC ueber die Lebenszeit, equal-weight-Alt-Index
    /// −73 % vs. BTC 2022–2026). TradFi-Perps und tokenisierte Edelmetalle sind ausgeschlossen.
    /// <paramref name="longK"/>: 1 = mit BTC-Anker (fehlt BTC im Universum → LEERER Korb, sonst
    /// waere der Rest ein net-short-Direktionaltrade); 0 = nur Short-Seite (Drift-Refill).
    /// Geteilt zwischen Live (<c>CrossSectionalTradingService</c>) und Backtest
    /// (<c>CrossSectionalMomentumEngine</c>, Mode DominanceSpread) — Paritaet wie ComputeBasket.
    /// </summary>
    /// <param name="canTrade">
    /// Optionaler Erreichbarkeits-Filter (Live): Symbole, deren Exchange-Min-Order groesser ist als
    /// der Slot, werden UEBERSPRUNGEN und der naechste Alt rueckt nach — aus "Top-ShortK nach Volumen"
    /// wird "Top-ShortK erreichbare nach Volumen". Ohne den Filter blieben solche Slots dauerhaft Cash
    /// (live 11.08.2026 bei ~95 USDT Equity: ETH/AAVE/BNB unerreichbar → Short-Seite 40 % unter-
    /// investiert, der Spread lief netto long). <c>null</c> im Backtest — dort gibt es keine
    /// Min-Order-Daten, und die Paritaet zum validierten Screen bleibt bit-identisch.
    /// </param>
    public static Dictionary<string, Side> ComputeDominanceBasket(
        IEnumerable<(string Symbol, IReadOnlyList<Candle> Candles)> universe, int longK, int shortK,
        Func<string, bool>? canTrade = null)
    {
        var basket = new Dictionary<string, Side>();
        var list = universe as IReadOnlyCollection<(string Symbol, IReadOnlyList<Candle> Candles)> ?? universe.ToList();
        if (longK > 0)
        {
            if (!list.Any(u => u.Symbol == DominanceAnchorSymbol)) return basket;
            basket[DominanceAnchorSymbol] = Side.Buy;
        }
        var alts = list
            .Where(u => u.Symbol != DominanceAnchorSymbol && IsCryptoAlt(u.Symbol))
            .Select(u => (u.Symbol, Vol: QuoteVolume(u.Candles, 42)))
            .Where(x => x.Vol > 0m)
            .OrderByDescending(x => x.Vol)
            .Where(x => canTrade == null || canTrade(x.Symbol))
            .Take(Math.Max(0, shortK));
        foreach (var x in alts) basket[x.Symbol] = Side.Sell;
        return basket;
    }

    /// <summary>Krypto-Alt = kein NC-TradFi-Perp und kein tokenisiertes Edelmetall (XAUT/PAXG →
    /// AssetCluster.TradFiCommodity) — der Dominanz-Spread shortet nur echte Alts.</summary>
    public static bool IsCryptoAlt(string symbol) =>
        !SymbolClassifier.IsTradFi(symbol)
        && AssetClusterClassifier.Classify(symbol) is not (AssetCluster.TradFiCommodity
            or AssetCluster.TradFiForex or AssetCluster.TradFiIndex or AssetCluster.TradFiStock);

    /// <summary>Quote-Volumen-Proxy (Σ Volume×Close) ueber die letzten <paramref name="candleCount"/> Kerzen.</summary>
    public static decimal QuoteVolume(IReadOnlyList<Candle> candles, int candleCount)
    {
        var start = Math.Max(0, candles.Count - candleCount);
        var sum = 0m;
        for (var i = start; i < candles.Count; i++)
            sum += candles[i].Volume * candles[i].Close;
        return sum;
    }

    /// <summary>Slot-Auswahl EINER Seite: erst Hysterese (gehaltene im Buffer-Fenster), dann Rang-Reihenfolge.</summary>
    private static void SelectSide(
        List<string> sideRanked, int k, Side side,
        IReadOnlyDictionary<string, Side>? currentBasket, int exitRankBuffer, bool clusterDiversify,
        Dictionary<string, Side> basket)
    {
        if (k <= 0 || sideRanked.Count == 0) return;
        var picked = new List<string>(k);
        var usedClusters = new HashSet<AssetCluster>();

        // 1. Hysterese: gehaltene Symbole innerhalb Top-(K+Buffer) behalten (kein unnoetiger Tausch).
        if (currentBasket is not null && exitRankBuffer > 0)
        {
            var window = Math.Min(sideRanked.Count, k + exitRankBuffer);
            for (var i = 0; i < window && picked.Count < k; i++)
            {
                var sym = sideRanked[i];
                if (currentBasket.TryGetValue(sym, out var heldSide) && heldSide == side)
                {
                    picked.Add(sym);
                    if (clusterDiversify) usedClusters.Add(AssetClusterClassifier.Classify(sym));
                }
            }
        }

        // 2. Rest-Slots streng nach Rang; bei ClusterDiversify max. 1 Symbol je Cluster (Other blockt nie).
        foreach (var sym in sideRanked)
        {
            if (picked.Count >= k) break;
            if (picked.Contains(sym)) continue;
            if (clusterDiversify)
            {
                var cluster = AssetClusterClassifier.Classify(sym);
                if (cluster != AssetCluster.Other && !usedClusters.Add(cluster)) continue;
            }
            picked.Add(sym);
        }

        // 3. Fallback: reichen die Cluster nicht fuer K Slots, nach Rang auffuellen (Exposure-Paritaet).
        foreach (var sym in sideRanked)
        {
            if (picked.Count >= k) break;
            if (!picked.Contains(sym)) picked.Add(sym);
        }

        foreach (var sym in picked) basket[sym] = side;
    }
}
