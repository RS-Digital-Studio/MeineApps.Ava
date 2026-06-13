using BingXBot.Core.Enums;
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
    {
        var ranked = universe
            .Select(u => (u.Symbol, Mom: Momentum(u.Candles, lookback, riskAdjusted, skip)))
            .Where(x => x.Mom.HasValue)
            .OrderByDescending(x => x.Mom!.Value)
            .ToList();

        var basket = new Dictionary<string, Side>();
        foreach (var x in ranked.Where(x => x.Mom!.Value > 0m).Take(longK))
            basket[x.Symbol] = Side.Buy;
        foreach (var x in ranked.Where(x => x.Mom!.Value < 0m).OrderBy(x => x.Mom!.Value).Take(shortK))
            basket[x.Symbol] = Side.Sell;
        return basket;
    }
}
