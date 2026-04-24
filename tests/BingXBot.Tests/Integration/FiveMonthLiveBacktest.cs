using System.Diagnostics;
using System.Text;
using BingXBot.Backtest;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BingXBot.Tests.Integration;

/// <summary>
/// Fuenf-Monats-Live-Backtest gegen die echte BingX Public-API.
/// Laeuft die aktuelle BacktestEngine + SequenzKonzeptStrategy auf:
///   - 11 Symbolen (Krypto + Rohstoffe + Indices + Forex + Aktien)
///   - 4 Navigator-TFs (D1/H4/H1/M15)
///   - Zeitraum: 150 Tage
/// Total: 44 Backtest-Runs. Dauer je nach Netz ~1-3h.
///
/// KEIN API-Key noetig. Braucht Internet.
///
/// Ausfuehrung:
///   dotnet test --filter "FullyQualifiedName~FiveMonthLiveBacktest" -v:n
///
/// Report wird geschrieben nach: F:/Meine_Apps_Ava/Releases/BingXBot/v1.2.0/BACKTEST-ANALYSIS-5M.md
/// Rohdaten (CSV) daneben: BACKTEST-ANALYSIS-5M.csv
/// </summary>
[Trait("Category", "LiveBacktest")]
[Trait("Duration", "Long")]
public class FiveMonthLiveBacktest
{
    private readonly ITestOutputHelper _out;

    // Repraesentative Symbol-Auswahl pro Kategorie (Top-Volume laut CLAUDE.md)
    private static readonly (string Symbol, string Label, MarketCategory Category)[] Symbols =
    {
        // Krypto (Top 3 nach Volume)
        ("BTC-USDT",                "Bitcoin",       MarketCategory.Crypto),
        ("ETH-USDT",                "Ethereum",      MarketCategory.Crypto),
        ("SOL-USDT",                "Solana",        MarketCategory.Crypto),
        // Rohstoffe (Top 2 - Gold 494M, Oil 43M)
        ("NCCOGOLD2USD-USDT",       "Gold",          MarketCategory.Commodity),
        ("NCCO1OILWTI2USD-USDT",    "WTI Oil",       MarketCategory.Commodity),
        // Indices (Top 2)
        ("NCSINASDAQ1002USD-USDT",  "Nasdaq 100",    MarketCategory.Index),
        ("NCSISP5002USD-USDT",      "S&P 500",       MarketCategory.Index),
        // Forex (Top 2)
        ("NCFXEUR2USD-USDT",        "EUR/USD",       MarketCategory.Forex),
        ("NCFXGBP2USD-USDT",        "GBP/USD",       MarketCategory.Forex),
        // Aktien (Top 2 nach Vol)
        ("NCSKTSLA2USD-USDT",       "Tesla",         MarketCategory.Stock),
        ("NCSKNVDA2USD-USDT",       "Nvidia",        MarketCategory.Stock),
    };

    private static readonly TimeFrame[] TimeFrames =
    {
        TimeFrame.D1, TimeFrame.H4, TimeFrame.H1, TimeFrame.M15
    };

    private const string ReportPath = @"F:\Meine_Apps_Ava\Releases\BingXBot\v1.2.0\BACKTEST-ANALYSIS-5M.md";
    private const string CsvPath = @"F:\Meine_Apps_Ava\Releases\BingXBot\v1.2.0\BACKTEST-ANALYSIS-5M.csv";

    public FiveMonthLiveBacktest(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task FiveMonthBacktest_AllSymbols_AllTFs_GeneratesReport()
    {
        var from = DateTime.UtcNow.AddDays(-150);
        var to = DateTime.UtcNow;

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var rateLimiter = new RateLimiter();
        var publicClient = new BingXPublicClient(http, rateLimiter,
            NullLogger<BingXPublicClient>.Instance);

        var results = new List<RunResult>();
        var totalRuns = Symbols.Length * TimeFrames.Length;
        var runIdx = 0;
        var overallSw = Stopwatch.StartNew();

        foreach (var (symbol, label, category) in Symbols)
        {
            foreach (var tf in TimeFrames)
            {
                runIdx++;
                _out.WriteLine($"\n[{runIdx}/{totalRuns}] {label} ({symbol}) {tf} — starte...");

                var strategy = new SequenzKonzeptStrategy();
                var riskSettings = new RiskSettings();
                var riskManager = new RiskManager(riskSettings,
                    NullLogger<RiskManager>.Instance);

                // So wie BacktestViewModel es setzt (Multi-TF-Run)
                var settings = new BacktestSettings
                {
                    InitialBalance = 1000m,
                    Tp1CloseRatio = 0.5m,
                    Tp2CloseRatio = 0.5m,
                    MinRiskRewardRatio = 1.0m,
                    HtfTimeFrame = SequenzKonzeptStrategy.GetFilterTimeframe(tf),
                    EntryTimeFrame = null, // Engine-interner Default (M30 bei H4, M15 bei H1, etc.)
                };

                var engine = new BacktestEngine(publicClient,
                    NullLogger<BacktestEngine>.Instance);

                var sw = Stopwatch.StartNew();
                try
                {
                    var report = await engine.RunAsync(
                        strategy, riskManager, symbol, tf, from, to, settings);
                    sw.Stop();

                    var exitStats = AnalyzeTradeExits(report.Trades);

                    var result = new RunResult(
                        Symbol: symbol,
                        Label: label,
                        Category: category,
                        Tf: tf,
                        Trades: report.TotalTrades,
                        WinningTrades: report.WinningTrades,
                        LosingTrades: report.LosingTrades,
                        WinRate: report.WinRate,
                        TotalPnl: report.TotalPnl,
                        ProfitFactor: report.ProfitFactor,
                        MaxDrawdown: report.MaxDrawdown,
                        MaxDrawdownPercent: report.MaxDrawdownPercent,
                        SharpeRatio: report.SharpeRatio,
                        SortinoRatio: report.SortinoRatio,
                        AverageRrr: report.AverageRrr,
                        AverageWin: report.AverageWin,
                        AverageLoss: report.AverageLoss,
                        AverageHoldMinutes: (decimal)report.AverageHoldTime.TotalMinutes,
                        MaxConsLosses: report.MaxConsecutiveLosses,
                        MaxConsWins: report.MaxConsecutiveWins,
                        Duration: sw.Elapsed,
                        Error: null,
                        TpHits: exitStats.TpHits,
                        SlHits: exitStats.SlHits,
                        BeHits: exitStats.BeHits);
                    results.Add(result);

                    _out.WriteLine(
                        $"  Trades: {report.TotalTrades,4} | WinRate: {report.WinRate,6:F1}% | " +
                        $"PnL: {report.TotalPnl,+9:F2} USDT | PF: {report.ProfitFactor,5:F2} | " +
                        $"MaxDD: {report.MaxDrawdownPercent,5:F1}% | " +
                        $"TP/SL/BE: {exitStats.TpHits}/{exitStats.SlHits}/{exitStats.BeHits} | " +
                        $"Dauer: {sw.Elapsed:mm\\:ss}");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new RunResult(
                        symbol, label, category, tf,
                        0, 0, 0,
                        0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
                        0, 0,
                        sw.Elapsed, ex.Message, 0, 0, 0));
                    _out.WriteLine($"  FEHLER: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        overallSw.Stop();
        _out.WriteLine($"\n=== Alle {totalRuns} Runs abgeschlossen ({overallSw.Elapsed:hh\\:mm\\:ss}) ===");

        // Report schreiben
        WriteMarkdownReport(results, from, to, overallSw.Elapsed);
        WriteCsvReport(results);
        _out.WriteLine($"Report: {ReportPath}");
        _out.WriteLine($"CSV:    {CsvPath}");
    }

    /// <summary>Schaetzt TP/SL/BE-Anteile aus Trades basierend auf Reason-Feld und PnL.</summary>
    private static ExitStats AnalyzeTradeExits(IReadOnlyList<CompletedTrade> trades)
    {
        int tp = 0, sl = 0, be = 0;
        foreach (var t in trades)
        {
            var reason = (t.Reason ?? "").ToLowerInvariant();
            if (reason.Contains("tp") || reason.Contains("take profit") || reason.Contains("take-profit") || reason.Contains("takeprofit"))
                tp++;
            else if (reason.Contains("sl") || reason.Contains("stop loss") || reason.Contains("stop-loss") || reason.Contains("stoploss"))
                sl++;
            else if (reason.Contains("be") || reason.Contains("break") || reason.Contains("breakeven"))
                be++;
            else
            {
                // Fallback: Positiver PnL = TP, Negativer = SL, Nahe 0 = BE
                if (t.Pnl > 0.5m) tp++;
                else if (t.Pnl < -0.5m) sl++;
                else be++;
            }
        }
        return new ExitStats(tp, sl, be);
    }

    private static void WriteMarkdownReport(List<RunResult> results, DateTime from, DateTime to, TimeSpan duration)
    {
        var dir = Path.GetDirectoryName(ReportPath)!;
        Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("# BingXBot — 5-Monats-Backtest-Analyse");
        sb.AppendLine();
        sb.AppendLine($"**Zeitraum**: {from:yyyy-MM-dd} bis {to:yyyy-MM-dd} ({(to - from).TotalDays:F0} Tage)");
        sb.AppendLine($"**Generiert**: {DateTime.Now:yyyy-MM-dd HH:mm} Uhr lokal");
        sb.AppendLine($"**Gesamtdauer**: {duration:hh\\:mm\\:ss}");
        sb.AppendLine($"**Runs total**: {results.Count}");
        sb.AppendLine($"**Erfolgreich**: {results.Count(r => r.Error == null)}");
        sb.AppendLine($"**Fehlgeschlagen**: {results.Count(r => r.Error != null)}");
        sb.AppendLine();

        // Gesamt-Aggregat
        var successful = results.Where(r => r.Error == null).ToList();
        var totalTrades = successful.Sum(r => r.Trades);
        var totalPnl = successful.Sum(r => r.TotalPnl);
        var totalWins = successful.Sum(r => r.WinningTrades);
        var overallWinRate = totalTrades > 0 ? (decimal)totalWins / totalTrades * 100m : 0m;

        sb.AppendLine("## Gesamt-Zusammenfassung");
        sb.AppendLine();
        sb.AppendLine($"- **Total Trades**: {totalTrades}");
        sb.AppendLine($"- **Durchschnitt Trades/Run**: {(successful.Count > 0 ? totalTrades / (decimal)successful.Count : 0m):F1}");
        sb.AppendLine($"- **Total PnL (summiert)**: {totalPnl:+0.00;-0.00} USDT (aus {successful.Count} Runs a 1000 USDT Startkapital)");
        sb.AppendLine($"- **Gesamt-WinRate**: {overallWinRate:F1}%");
        sb.AppendLine();

        // Per Navigator-TF
        sb.AppendLine("## Aggregat pro Navigator-TF");
        sb.AppendLine();
        sb.AppendLine("| TF | Runs | Trades | Trades/Run | WinRate | PnL sum | PnL/Run | Positive Runs | Negative Runs |");
        sb.AppendLine("|----|-----:|-------:|-----------:|--------:|--------:|--------:|--------------:|--------------:|");
        foreach (var tf in TimeFrames)
        {
            var tfRuns = successful.Where(r => r.Tf == tf).ToList();
            if (tfRuns.Count == 0) { sb.AppendLine($"| {tf} | 0 | — | — | — | — | — | — | — |"); continue; }
            var tfTrades = tfRuns.Sum(r => r.Trades);
            var tfWins = tfRuns.Sum(r => r.WinningTrades);
            var tfWinRate = tfTrades > 0 ? (decimal)tfWins / tfTrades * 100m : 0m;
            var tfPnl = tfRuns.Sum(r => r.TotalPnl);
            var tfPositive = tfRuns.Count(r => r.TotalPnl > 0);
            var tfNegative = tfRuns.Count(r => r.TotalPnl < 0);
            sb.AppendLine($"| {tf} | {tfRuns.Count} | {tfTrades} | {(decimal)tfTrades / tfRuns.Count:F1} | " +
                $"{tfWinRate:F1}% | {tfPnl:+0.00;-0.00} | {tfPnl / tfRuns.Count:+0.00;-0.00} | " +
                $"{tfPositive} | {tfNegative} |");
        }
        sb.AppendLine();

        // Per Kategorie
        sb.AppendLine("## Aggregat pro Asset-Kategorie");
        sb.AppendLine();
        sb.AppendLine("| Kategorie | Symbole | Runs | Trades | WinRate | PnL sum | MaxDD avg |");
        sb.AppendLine("|-----------|--------:|-----:|-------:|--------:|--------:|----------:|");
        foreach (var cat in Enum.GetValues<MarketCategory>())
        {
            var catRuns = successful.Where(r => r.Category == cat).ToList();
            if (catRuns.Count == 0) continue;
            var catSymbols = catRuns.Select(r => r.Symbol).Distinct().Count();
            var catTrades = catRuns.Sum(r => r.Trades);
            var catWins = catRuns.Sum(r => r.WinningTrades);
            var catWinRate = catTrades > 0 ? (decimal)catWins / catTrades * 100m : 0m;
            var catPnl = catRuns.Sum(r => r.TotalPnl);
            var avgDd = catRuns.Count > 0 ? catRuns.Average(r => r.MaxDrawdownPercent) : 0m;
            sb.AppendLine($"| {cat} | {catSymbols} | {catRuns.Count} | {catTrades} | " +
                $"{catWinRate:F1}% | {catPnl:+0.00;-0.00} | {avgDd:F1}% |");
        }
        sb.AppendLine();

        // Alle Runs detailliert
        sb.AppendLine("## Alle Runs (Detail)");
        sb.AppendLine();
        sb.AppendLine("| Symbol | TF | Trades | Win% | PnL | PF | MaxDD% | Sharpe | AvgRRR | Hold(min) | TP/SL/BE | Fehler |");
        sb.AppendLine("|--------|----|-------:|-----:|----:|---:|-------:|-------:|-------:|----------:|---------:|--------|");
        foreach (var cat in Enum.GetValues<MarketCategory>())
        {
            var catResults = results.Where(r => r.Category == cat)
                .OrderBy(r => r.Symbol)
                .ThenBy(r => (int)r.Tf)
                .ToList();
            if (catResults.Count == 0) continue;
            sb.AppendLine($"| **{cat}** | | | | | | | | | | | |");
            foreach (var r in catResults)
            {
                if (r.Error != null)
                {
                    sb.AppendLine($"| {r.Label} | {r.Tf} | — | — | — | — | — | — | — | — | — | {TruncateError(r.Error)} |");
                }
                else
                {
                    sb.AppendLine($"| {r.Label} | {r.Tf} | {r.Trades} | " +
                        $"{r.WinRate:F0}% | {r.TotalPnl:+0.00;-0.00} | " +
                        $"{(r.ProfitFactor < 100m ? r.ProfitFactor.ToString("F2") : "∞")} | " +
                        $"{r.MaxDrawdownPercent:F1} | {r.SharpeRatio:F2} | {r.AverageRrr:F2} | " +
                        $"{r.AverageHoldMinutes:F0} | {r.TpHits}/{r.SlHits}/{r.BeHits} | — |");
                }
            }
        }
        sb.AppendLine();

        // Auffälligkeiten
        sb.AppendLine("## Auffaelligkeiten & Hypothesen");
        sb.AppendLine();
        AppendFindings(sb, successful);

        File.WriteAllText(ReportPath, sb.ToString(), Encoding.UTF8);
    }

    private static string TruncateError(string err)
    {
        if (string.IsNullOrEmpty(err)) return "—";
        return err.Length > 60 ? err.Substring(0, 60) + "..." : err;
    }

    private static void AppendFindings(StringBuilder sb, List<RunResult> successful)
    {
        if (successful.Count == 0) { sb.AppendLine("Keine erfolgreichen Runs — Fehler-Tabelle oben pruefen."); return; }

        // 1. Trade-Frequenz
        var avgTradesPerRun = successful.Average(r => r.Trades);
        sb.AppendLine("### Trade-Frequenz");
        sb.AppendLine();
        if (avgTradesPerRun < 5)
            sb.AppendLine($"**AUFFAELLIG**: Nur {avgTradesPerRun:F1} Trades pro Run im Schnitt (5 Monate, 150 Tage). " +
                "Das deutet auf starkes Gate-Over-Blocking hin — die SK-Strategy findet kaum Setups die alle " +
                "Confluence-Schwellen (CWS-Gates, MinConfluenceScoreByTf, Sequence-Max-Age) passieren.");
        else if (avgTradesPerRun < 20)
            sb.AppendLine($"{avgTradesPerRun:F1} Trades pro Run im Schnitt — niedrig aber nicht ungewoehnlich fuer SK.");
        else
            sb.AppendLine($"{avgTradesPerRun:F1} Trades pro Run im Schnitt — normale Frequenz.");
        sb.AppendLine();

        // 2. WinRate-Analyse
        var tradesOnly = successful.Where(r => r.Trades >= 5).ToList();
        if (tradesOnly.Count > 0)
        {
            var avgWinRate = tradesOnly.Average(r => r.WinRate);
            sb.AppendLine("### WinRate");
            sb.AppendLine();
            if (avgWinRate < 35)
                sb.AppendLine($"**KRITISCH**: WinRate {avgWinRate:F1}% unter Break-Even bei 1:1-RRR. " +
                    "Kombiniert mit niedriger Trade-Frequenz = System findet Setups, aber die sind falsch-selektiert.");
            else if (avgWinRate < 45)
                sb.AppendLine($"WinRate {avgWinRate:F1}% — akzeptabel wenn RRR > 1.5 eingehalten wird (Expectancy-Check folgt).");
            else
                sb.AppendLine($"WinRate {avgWinRate:F1}% — ordentlich.");
            sb.AppendLine();
        }

        // 3. TF-Abhaengigkeit (KERN-FINDING bei Multi-TF-Bug)
        sb.AppendLine("### TF-Abhaengigkeit der Performance");
        sb.AppendLine();
        var h4Runs = successful.Where(r => r.Tf == TimeFrame.H4 && r.Trades >= 3).ToList();
        var otherRuns = successful.Where(r => r.Tf != TimeFrame.H4 && r.Trades >= 3).ToList();
        if (h4Runs.Count > 0 && otherRuns.Count > 0)
        {
            var h4WinRate = h4Runs.Average(r => r.WinRate);
            var otherWinRate = otherRuns.Average(r => r.WinRate);
            var h4Pnl = h4Runs.Average(r => r.TotalPnl);
            var otherPnl = otherRuns.Average(r => r.TotalPnl);
            sb.AppendLine($"- **H4**: Avg WinRate {h4WinRate:F1}%, Avg PnL/Run {h4Pnl:+0.00;-0.00} USDT");
            sb.AppendLine($"- **D1/H1/M15**: Avg WinRate {otherWinRate:F1}%, Avg PnL/Run {otherPnl:+0.00;-0.00} USDT");
            if (h4WinRate - otherWinRate > 5)
                sb.AppendLine($"**AUFFAELLIG**: H4 deutlich besser als andere TFs. Konsistent mit Hypothese " +
                    "**NavigatorTimeframe-Bug in BacktestEngine** (Default=H4, andere TFs werden behandelt als " +
                    "waeren sie H4 → Sequenz/Confluence-Kalibrierung passt nicht).");
        }
        sb.AppendLine();

        // 4. Kategorie-Abhängigkeit
        sb.AppendLine("### Kategorie-Abhaengigkeit");
        sb.AppendLine();
        foreach (var cat in Enum.GetValues<MarketCategory>())
        {
            var catRuns = successful.Where(r => r.Category == cat && r.Trades >= 3).ToList();
            if (catRuns.Count == 0) continue;
            var catWinRate = catRuns.Average(r => r.WinRate);
            var catPnl = catRuns.Average(r => r.TotalPnl);
            var sign = catPnl > 0 ? "[+]" : catPnl < 0 ? "[-]" : "[0]";
            sb.AppendLine($"- {sign} **{cat}**: {catRuns.Count} Runs, Avg WinRate {catWinRate:F1}%, Avg PnL/Run {catPnl:+0.00;-0.00}");
        }
        sb.AppendLine();

        // 5. TP vs SL Verteilung
        sb.AppendLine("### Exit-Grund-Verteilung");
        sb.AppendLine();
        var totalTp = successful.Sum(r => r.TpHits);
        var totalSl = successful.Sum(r => r.SlHits);
        var totalBe = successful.Sum(r => r.BeHits);
        var totalExits = totalTp + totalSl + totalBe;
        if (totalExits > 0)
        {
            sb.AppendLine($"- TP: {totalTp} ({(decimal)totalTp / totalExits:P0})");
            sb.AppendLine($"- SL: {totalSl} ({(decimal)totalSl / totalExits:P0})");
            sb.AppendLine($"- BE/offen: {totalBe} ({(decimal)totalBe / totalExits:P0})");
            if (totalSl > totalTp * 1.5m)
                sb.AppendLine("**AUFFAELLIG**: SL-Hits dominieren klar ueber TP-Hits. " +
                    "Entweder falsche Entry-Level (Triple-Entry vs Single-Entry?), zu enger SL, oder TP nicht erreicht.");
        }
        sb.AppendLine();

        // 6. Bekannte Backtest-Engine-Bugs (mit echten Daten verifizieren)
        sb.AppendLine("### Verdacht: Bugs in der Backtest-Infrastruktur");
        sb.AppendLine();
        sb.AppendLine("Die BacktestEngine ist **nicht vollstaendig** auf Multi-TF Standalone umgestellt (Stand 16.04.2026):");
        sb.AppendLine();
        sb.AppendLine("1. `MarketContext.NavigatorTimeframe` wird NIE explizit gesetzt → bleibt Default `H4`. " +
            "Fuer D1/H1/M15-Backtests denkt die Strategie faelschlicherweise sie sei auf H4.");
        sb.AppendLine("2. `MarketContext.ScannerSettings` ist immer `null` → alle per-TF-Schwellen " +
            "(MinConfluenceScoreByTf, MinPoint0CandlesByTf, CWS-Gate-Flags) werden mit Defaults behandelt.");
        sb.AppendLine("3. `MarketContext.RiskSettings` ist immer `null` → `PipScalingByTf` (M15 gedaempft) " +
            "wirkt nicht, M15-Backtest nutzt falsche SL-Distanz.");
        sb.AppendLine("4. `MarketContext.DailyCandles` und `WeeklyCandles` werden nicht korrekt " +
            "befuellt — Fahrplan-Analyse (BLASH, Daily-GKLs) liefert keine verwertbaren Signale.");
        sb.AppendLine();
        sb.AppendLine("**Erwartung bei Fix**: H4-Performance bleibt vergleichbar (Default matched " +
            "dort zufaellig), D1/H1/M15 sollten substantiell zulegen.");
        sb.AppendLine();
    }

    private static void WriteCsvReport(List<RunResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Symbol,Label,Category,TF,Trades,WinningTrades,LosingTrades,WinRate,TotalPnl,ProfitFactor,MaxDrawdown,MaxDrawdownPct,Sharpe,Sortino,AvgRRR,AvgWin,AvgLoss,AvgHoldMinutes,MaxConsLosses,MaxConsWins,TpHits,SlHits,BeHits,DurationSeconds,Error");
        foreach (var r in results)
        {
            sb.AppendLine($"{r.Symbol},{r.Label},{r.Category},{r.Tf}," +
                $"{r.Trades},{r.WinningTrades},{r.LosingTrades}," +
                $"{r.WinRate:F2},{r.TotalPnl:F4},{(r.ProfitFactor < 1000m ? r.ProfitFactor.ToString("F4") : "inf")}," +
                $"{r.MaxDrawdown:F4},{r.MaxDrawdownPercent:F2}," +
                $"{r.SharpeRatio:F4},{r.SortinoRatio:F4},{r.AverageRrr:F4}," +
                $"{r.AverageWin:F4},{r.AverageLoss:F4},{r.AverageHoldMinutes:F1}," +
                $"{r.MaxConsLosses},{r.MaxConsWins}," +
                $"{r.TpHits},{r.SlHits},{r.BeHits}," +
                $"{r.Duration.TotalSeconds:F1},\"{(r.Error ?? "").Replace("\"", "\"\"")}\"");
        }
        File.WriteAllText(CsvPath, sb.ToString(), Encoding.UTF8);
    }

    private record RunResult(
        string Symbol, string Label, MarketCategory Category, TimeFrame Tf,
        int Trades, int WinningTrades, int LosingTrades,
        decimal WinRate, decimal TotalPnl, decimal ProfitFactor,
        decimal MaxDrawdown, decimal MaxDrawdownPercent,
        decimal SharpeRatio, decimal SortinoRatio,
        decimal AverageRrr, decimal AverageWin, decimal AverageLoss,
        decimal AverageHoldMinutes,
        int MaxConsLosses, int MaxConsWins,
        TimeSpan Duration, string? Error,
        int TpHits, int SlHits, int BeHits);

    private record ExitStats(int TpHits, int SlHits, int BeHits);
}
