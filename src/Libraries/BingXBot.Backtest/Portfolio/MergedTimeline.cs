using BingXBot.Core.Models;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Baut die gemeinsame, zeitlich gemergte Timeline ueber alle Symbole eines Portfolio-Backtests.
/// Jeder Eintrag ist ein H4-CloseTime, an dem MINDESTENS ein Symbol eine Kerze abschliesst.
/// Aufsteigend sortiert + dedupliziert — der <see cref="PortfolioBacktestEngine"/> iteriert genau
/// diese Zeitpunkte und schaltet pro Schritt die Symbole frei, deren Kerze bei <c>t</c> schliesst.
/// </summary>
internal static class MergedTimeline
{
    /// <summary>
    /// Vereinigt alle Kerzen-CloseTimes ueber alle Symbol-Serien zu einer aufsteigend sortierten,
    /// duplikatfreien Zeitachse. KEIN Look-Ahead — es werden nur reale CloseTimes der gelieferten
    /// Kerzen aufgenommen, keine kuenstlichen/zukuenftigen Zeitpunkte.
    /// </summary>
    public static IReadOnlyList<DateTime> Build(IEnumerable<IReadOnlyList<Candle>> navSeries)
    {
        var set = new HashSet<DateTime>();
        foreach (var series in navSeries)
            for (var i = 0; i < series.Count; i++)
                set.Add(series[i].CloseTime);

        var timeline = set.ToList();
        timeline.Sort();
        return timeline;
    }
}
