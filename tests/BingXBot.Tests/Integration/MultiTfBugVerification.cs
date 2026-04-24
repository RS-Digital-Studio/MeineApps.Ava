using System.Diagnostics;
using System.Text;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BingXBot.Tests.Integration;

/// <summary>
/// Verifiziert empirisch die Hypothese dass der Backtest-Ingenieur-Bug
/// (MarketContext.NavigatorTimeframe wird nie gesetzt -> bleibt Default H4)
/// die Strategy-Signale substantiell verfaelscht.
///
/// Fuer ein Symbol (BTC-USDT) x 4 TFs (D1/H4/H1/M15) werden auf den gleichen
/// Kerzen zwei MarketContext-Varianten gebildet:
///   A) "Buggy": NavigatorTimeframe=H4 (Default), ScannerSettings/RiskSettings=null
///   B) "Fixed": NavigatorTimeframe=tf, ScannerSettings/RiskSettings=neue Default-Instanzen
///
/// Die Strategie wird auf beiden Varianten durchgescrollt und die Anzahl
/// Long/Short-Signale gezaehlt. Delta = empirischer Beweis des Bugs.
///
/// Ausfuehrung:
///   dotnet test --filter "FullyQualifiedName~MultiTfBugVerification" -c Release
/// </summary>
[Trait("Category", "LiveBacktest")]
public class MultiTfBugVerification
{
    private readonly ITestOutputHelper _out;

    private const string Symbol = "BTC-USDT";
    private static readonly TimeFrame[] TimeFrames =
        { TimeFrame.D1, TimeFrame.H4, TimeFrame.H1, TimeFrame.M15 };

    private const string ReportPath =
        @"F:\Meine_Apps_Ava\Releases\BingXBot\v1.2.0\BACKTEST-BUG-VERIFICATION.md";

    public MultiTfBugVerification(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Verify_NavigatorTimeframe_BugCausesSignalDelta()
    {
        var from = DateTime.UtcNow.AddDays(-150);
        var to = DateTime.UtcNow;

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var rateLimiter = new RateLimiter();
        var client = new BingXPublicClient(http, rateLimiter,
            NullLogger<BingXPublicClient>.Instance);

        var resultsByTf = new List<(TimeFrame Tf, int Buggy, int Fixed, int NavCandleCount, int FilterCandleCount)>();

        // Weekly + Daily einmalig laden (die sind TF-unabhaengig fuer den Fahrplan)
        var weeklyCandles = await client.GetKlinesAsync(Symbol, TimeFrame.W1,
            from.AddDays(-60), to);
        var dailyCandles = await client.GetKlinesAsync(Symbol, TimeFrame.D1,
            from.AddDays(-14), to);

        _out.WriteLine($"Weekly: {weeklyCandles.Count} Kerzen, Daily: {dailyCandles.Count} Kerzen");

        foreach (var tf in TimeFrames)
        {
            _out.WriteLine($"\n=== TF: {tf} ===");

            // Navigator-Kerzen (primary)
            var navCandles = await client.GetKlinesAsync(Symbol, tf, from.AddDays(-7), to);
            // Filter-Kerzen (naechst-tiefere TF)
            var filterTf = SequenzKonzeptStrategy.GetFilterTimeframe(tf);
            List<Candle>? filterCandles = null;
            if (filterTf.HasValue)
                filterCandles = await client.GetKlinesAsync(Symbol, filterTf.Value,
                    from.AddDays(-7), to);

            _out.WriteLine($"  Navigator: {navCandles.Count} Kerzen | Filter ({filterTf}): {filterCandles?.Count ?? 0}");

            if (navCandles.Count < 100)
            {
                _out.WriteLine($"  SKIP: Zu wenig Kerzen ({navCandles.Count} < 100)");
                resultsByTf.Add((tf, 0, 0, navCandles.Count, filterCandles?.Count ?? 0));
                continue;
            }

            // VARIANTE A: Buggy (NavigatorTimeframe=H4 Default, Settings=null)
            int buggyCount = CountSignals(Symbol, tf, navCandles, filterCandles,
                dailyCandles, weeklyCandles,
                navigatorOverride: TimeFrame.H4,   // Bug: bleibt Default
                useSettings: false);

            // VARIANTE B: Fixed (NavigatorTimeframe=tf, Settings gesetzt)
            int fixedCount = CountSignals(Symbol, tf, navCandles, filterCandles,
                dailyCandles, weeklyCandles,
                navigatorOverride: tf,             // Korrekt
                useSettings: true);

            _out.WriteLine($"  Signale Buggy (H4-Annahme):   {buggyCount}");
            _out.WriteLine($"  Signale Fixed ({tf}):          {fixedCount}");
            _out.WriteLine($"  Delta:                          {(fixedCount - buggyCount):+0;-0;0}");

            resultsByTf.Add((tf, buggyCount, fixedCount, navCandles.Count, filterCandles?.Count ?? 0));
        }

        // Report schreiben
        WriteReport(resultsByTf);
    }

    private static int CountSignals(
        string symbol,
        TimeFrame tf,
        List<Candle> navCandles,
        List<Candle>? filterCandles,
        List<Candle> dailyCandles,
        List<Candle> weeklyCandles,
        TimeFrame navigatorOverride,
        bool useSettings)
    {
        var strategy = new SequenzKonzeptStrategy();
        ScannerSettings? scanner = useSettings ? new ScannerSettings() : null;
        RiskSettings? risk = useSettings ? new RiskSettings() : null;

        // Warmup: erste 50 Kerzen
        var warmupSize = Math.Min(50, navCandles.Count / 4);
        strategy.WarmUp(navCandles.Take(warmupSize).ToList());
        strategy.Reset();

        int signalCount = 0;
        var category = SymbolClassifier.Classify(symbol);

        // Account ohne Balance-Limit (Signal-Erzeugung von Risk-Check getrennt)
        var account = new AccountInfo(
            Balance: 10000m,
            AvailableBalance: 10000m,
            UnrealizedPnl: 0m,
            UsedMargin: 0m,
            Equity: 10000m);
        var emptyPositions = Array.Empty<Position>();

        for (int i = warmupSize; i < navCandles.Count; i++)
        {
            var currentCandle = navCandles[i];

            // Nav-Context (letzte 200 Kerzen bis i)
            var contextStart = Math.Max(0, i - 199);
            var contextCount = i - contextStart + 1;
            var contextCandles = navCandles.GetRange(contextStart, contextCount);

            // Filter-Kerzen bis zum Zeitpunkt der aktuellen Nav-Kerze
            List<Candle>? filterUpTo = null;
            if (filterCandles != null && filterCandles.Count > 0)
            {
                var lastFilterIdx = filterCandles.FindLastIndex(c => c.CloseTime <= currentCandle.CloseTime);
                if (lastFilterIdx >= 20)
                {
                    var fStart = Math.Max(0, lastFilterIdx - 199);
                    filterUpTo = filterCandles.GetRange(fStart, lastFilterIdx - fStart + 1);
                }
            }

            // Daily und Weekly bis zum Zeitpunkt (nur wenn die primary TF nicht selbst D1/W1 ist)
            List<Candle>? dailyUpTo = null;
            if (tf != TimeFrame.D1 && tf != TimeFrame.W1)
            {
                var lastDailyIdx = dailyCandles.FindLastIndex(c => c.CloseTime <= currentCandle.CloseTime);
                if (lastDailyIdx >= 20)
                {
                    var dStart = Math.Max(0, lastDailyIdx - 199);
                    dailyUpTo = dailyCandles.GetRange(dStart, lastDailyIdx - dStart + 1);
                }
            }

            List<Candle>? weeklyUpTo = null;
            if (tf != TimeFrame.W1)
            {
                var lastWeeklyIdx = weeklyCandles.FindLastIndex(c => c.CloseTime <= currentCandle.CloseTime);
                if (lastWeeklyIdx >= 10)
                {
                    var wStart = Math.Max(0, lastWeeklyIdx - 99);
                    weeklyUpTo = weeklyCandles.GetRange(wStart, lastWeeklyIdx - wStart + 1);
                }
            }

            var ticker = new Ticker(
                Symbol: symbol,
                LastPrice: currentCandle.Close,
                BidPrice: currentCandle.Close,
                AskPrice: currentCandle.Close,
                Volume24h: currentCandle.Volume,
                PriceChangePercent24h: 0m,
                Timestamp: currentCandle.CloseTime);

            var context = new MarketContext(
                Symbol: symbol,
                Candles: contextCandles,
                CurrentTicker: ticker,
                OpenPositions: emptyPositions,
                Account: account,
                FilterTimeframeCandles: filterUpTo,
                Category: category,
                DailyCandles: dailyUpTo,
                WeeklyCandles: weeklyUpTo,
                NavigatorTimeframe: navigatorOverride,
                ScannerSettings: scanner,
                RiskSettings: risk,
                NowUtc: currentCandle.CloseTime);

            var signal = strategy.Evaluate(context);
            if (signal.Signal is Signal.Long or Signal.Short)
                signalCount++;
        }

        return signalCount;
    }

    private static void WriteReport(List<(TimeFrame Tf, int Buggy, int Fixed, int NavCount, int FilterCount)> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# BingXBot — Backtest-Engine-Bug-Verifikation");
        sb.AppendLine();
        sb.AppendLine($"**Generiert**: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"**Symbol**: BTC-USDT");
        sb.AppendLine($"**Zeitraum**: letzte 150 Tage");
        sb.AppendLine();
        sb.AppendLine("## Frage");
        sb.AppendLine();
        sb.AppendLine("Macht der BacktestEngine-Bug (NavigatorTimeframe=H4-Default, ScannerSettings/RiskSettings=null)");
        sb.AppendLine("einen messbaren Unterschied in der Signal-Anzahl, die die Strategy generiert?");
        sb.AppendLine();
        sb.AppendLine("## Methode");
        sb.AppendLine();
        sb.AppendLine("Pro TF werden die gleichen Kerzen durch die Strategy gescrollt. Zwei Varianten:");
        sb.AppendLine();
        sb.AppendLine("- **Buggy**: `MarketContext.NavigatorTimeframe = H4` (Default), `ScannerSettings = null`, `RiskSettings = null` — so macht es die BacktestEngine heute.");
        sb.AppendLine("- **Fixed**: `NavigatorTimeframe = tf` (korrekt gesetzt), `ScannerSettings = new()`, `RiskSettings = new()` — so sollte es sein.");
        sb.AppendLine();
        sb.AppendLine("Gezaehlt werden nur **Long/Short-Signale** von `strategy.Evaluate()`. Risk-Filter sind nicht aktiv (reine Signal-Generierung).");
        sb.AppendLine();
        sb.AppendLine("## Ergebnis");
        sb.AppendLine();
        sb.AppendLine("| TF | Nav-Kerzen | Filter-Kerzen | Signale Buggy | Signale Fixed | Delta | Aenderung |");
        sb.AppendLine("|----|-----------:|--------------:|--------------:|--------------:|------:|-----------|");
        foreach (var (tf, buggy, fix, navC, filterC) in results)
        {
            var delta = fix - buggy;
            var changePct = buggy == 0 ? (fix == 0 ? "—" : "∞") : $"{(decimal)delta / buggy:+0%;-0%;0%}";
            var marker = tf == TimeFrame.H4
                ? " (Default-TF, Bug matcht zufaellig)"
                : "";
            sb.AppendLine($"| {tf}{marker} | {navC} | {filterC} | {buggy} | {fix} | {delta:+0;-0;0} | {changePct} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Interpretation");
        sb.AppendLine();

        var h4Row = results.FirstOrDefault(r => r.Tf == TimeFrame.H4);
        var d1Row = results.FirstOrDefault(r => r.Tf == TimeFrame.D1);
        var h1Row = results.FirstOrDefault(r => r.Tf == TimeFrame.H1);
        var m15Row = results.FirstOrDefault(r => r.Tf == TimeFrame.M15);

        sb.AppendLine("### H4 (Sanity-Check)");
        sb.AppendLine();
        sb.AppendLine($"Buggy-Variante hat `NavigatorTimeframe=H4`, Fixed-Variante auch — also sollte der Delta bei H4 minimal sein (nur von `ScannerSettings`/`RiskSettings` beeinflusst).");
        sb.AppendLine();
        if (h4Row.Buggy > 0 || h4Row.Fixed > 0)
        {
            var h4Delta = h4Row.Fixed - h4Row.Buggy;
            sb.AppendLine($"Beobachtet: {h4Row.Buggy} vs {h4Row.Fixed} ({h4Delta:+0;-0;0}). {(Math.Abs(h4Delta) <= 2 ? "OK — praktisch identisch." : "Bemerkbar — ScannerSettings/RiskSettings alleine haben Einfluss.")}");
        }
        sb.AppendLine();

        sb.AppendLine("### D1 / H1 / M15 (Kritisch)");
        sb.AppendLine();
        sb.AppendLine("Hier ist der `NavigatorTimeframe` im Buggy-Fall `H4`, im Fixed-Fall die richtige TF. Wenn der Bug echt ist, erwarten wir substantielle Deltas.");
        sb.AppendLine();
        foreach (var row in new[] { d1Row, h1Row, m15Row })
        {
            var delta = row.Fixed - row.Buggy;
            sb.AppendLine($"- **{row.Tf}**: Buggy {row.Buggy} Signale, Fixed {row.Fixed} Signale, Delta {delta:+0;-0;0}.");
        }
        sb.AppendLine();

        var totalBuggy = results.Sum(r => r.Buggy);
        var totalFixed = results.Sum(r => r.Fixed);
        var totalDelta = totalFixed - totalBuggy;
        sb.AppendLine("### Gesamt");
        sb.AppendLine();
        sb.AppendLine($"- Summe Buggy-Signale: **{totalBuggy}**");
        sb.AppendLine($"- Summe Fixed-Signale: **{totalFixed}**");
        sb.AppendLine($"- Gesamt-Delta: **{totalDelta:+0;-0;0}** ({(totalBuggy == 0 ? "—" : $"{(decimal)totalDelta / totalBuggy:+0%;-0%}")})");
        sb.AppendLine();
        sb.AppendLine("Je groesser der Delta, desto klarer der Bug-Effekt. Bei substantiellem Delta (>30%) ist der Fix in `BacktestEngine.RunAsync` erstrangig (P0).");
        sb.AppendLine();
        sb.AppendLine("## Reproduzierbarkeit");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("cd F:/Meine_Apps_Ava");
        sb.AppendLine("dotnet test tests/BingXBot.Tests/BingXBot.Tests.csproj -c Release \\");
        sb.AppendLine("  --filter \"FullyQualifiedName~MultiTfBugVerification\"");
        sb.AppendLine("```");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
    }
}
