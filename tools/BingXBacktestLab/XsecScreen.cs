using System.Globalization;
using System.Text;
using System.Text.Json;
using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

// ============================================================================
//  Xsec-Screen — testet Cross-Sectional-Momentum-Konfigurationen ueber die 4
//  disjunkten Phasen (PhaseScreen.DefaultPhases) auf dem live-getreuen Konto.
//  Ziel: eine Konfiguration finden, die in JEDER Phase profitabel ist —
//  strukturell phasen-robust (relativ statt absolut direktional).
// ============================================================================

internal sealed record XsecCell(int Trades, decimal WinRate, decimal ProfitFactor, decimal TotalPnl, decimal Pct, decimal MaxDd);

internal sealed record XsecRow(XsecParams Cfg, XsecCell[] Cells)
{
    public decimal MinPhasePct => Cells.Min(c => c.Pct);
    public decimal SumPct => Cells.Sum(c => c.Pct);
    public int TotalTrades => Cells.Sum(c => c.Trades);
    public bool AllPositive => Cells.All(c => c.Pct > 0m);
    public int PositivePhases => Cells.Count(c => c.Pct > 0m);
}

internal static class XsecScreen
{
    /// <summary>Parameterarmes, diverses Config-Set (verschiedene Lookbacks/Rebalances/K, MN + Long-only).</summary>
    public static XsecParams[] DefaultConfigs() =>
    [
        // Parameter-Nachbarschaft des Robust-Kandidaten L120/R126/4L-4S/radj/lev1 (Overfitting-Test):
        // bleibt der 2022-Bear positiv, wenn man Lookback/Rebalance/Slots variiert? Plateau = echt, Peak = overfit.
        new(LookbackCandles: 100, RebalanceEveryCandles: 126, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 120, RebalanceEveryCandles: 105, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 120, RebalanceEveryCandles: 126, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 120, RebalanceEveryCandles: 147, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 140, RebalanceEveryCandles: 126, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 120, RebalanceEveryCandles: 126, LongK: 3, ShortK: 3, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 120, RebalanceEveryCandles: 126, LongK: 5, ShortK: 5, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
        new(LookbackCandles: 100, RebalanceEveryCandles: 105, LongK: 4, ShortK: 4, RiskAdjusted: true, AtrStopMultiplier: 0m, LeverageCap: 1),
    ];

    private static async Task<XsecCell> EvaluateAsync(
        XsecParams cfg, Phase phase, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings settings, decimal balance, IPublicMarketDataClient data,
        ISymbolInfoProvider? symbolInfo, CancellationToken ct)
    {
        var engine = new CrossSectionalMomentumEngine(data, symbolInfo, NullLogger<CrossSectionalMomentumEngine>.Instance);
        var report = await engine.RunAsync(symbols, navTf, phase.From, phase.To, settings, cfg, ct).ConfigureAwait(false);
        var pct = balance > 0 ? report.TotalPnl / balance * 100m : 0m;
        return new XsecCell(report.TotalTrades, report.WinRate, report.ProfitFactor, report.TotalPnl, pct, report.MaxDrawdownPercent);
    }

    public static async Task<int> RunAsync(
        IReadOnlyList<XsecParams> configs, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo,
        decimal balance, int parallelism, string outDir, string? label)
    {
        baseSettings.Backtest.InitialBalance = balance;

        Console.WriteLine("=== Xsec-Screen (Cross-Sectional-Momentum, 1 Konto, alle Gates) ===");
        Console.WriteLine($"Configs    : {configs.Count} | Symbole: {symbols.Count} | Nav-TF {navTf} | Balance {balance:F2} USDT");
        Console.WriteLine($"Phasen     : {string.Join(" | ", phases.Select(p => p.Name))}");
        Console.WriteLine();

        Console.WriteLine("Preload (waermt Kline-Cache)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = await EvaluateAsync(configs[0], phases[0], symbols, navTf, baseSettings, balance, data, symbolInfo, default);
        sw.Stop();
        var total = configs.Count * phases.Length;
        Console.WriteLine($"  {sw.Elapsed.TotalSeconds:F1}s/Lauf → ~{total * sw.Elapsed.TotalSeconds / parallelism / 60:F1} min fuer {total} Laeufe\n");

        var jobs = (from c in configs from p in phases select (c, p)).ToArray();
        var cells = new Dictionary<(string, string), XsecCell>();
        var lockObj = new object();
        var done = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(jobs, pOpts, async (job, ct) =>
        {
            var cell = await EvaluateAsync(job.c, job.p, symbols, navTf, baseSettings, balance, data, symbolInfo, ct);
            lock (lockObj) cells[(job.c.Label, job.p.Name)] = cell;
            var d = Interlocked.Increment(ref done);
            if (d % 6 == 0 || d == jobs.Length) Console.WriteLine($"  [{d}/{jobs.Length}]");
        });

        var rows = configs
            .Select(c => new XsecRow(c, phases.Select(p => cells[(c.Label, p.Name)]).ToArray()))
            .OrderByDescending(r => r.MinPhasePct)
            .ToList();

        WriteReport(outDir, label, phases, symbols, navTf, balance, baseSettings, rows);

        Console.WriteLine($"\n=== ROBUSTHEITS-RANKING (nach schlechtester Phasen-Rendite) ===");
        Console.WriteLine($"{"Config",-34} " + string.Join(" ", phases.Select(p => $"{p.Name,14}")) + $" {"min%",8} {"Σ%",8} {"n",5} {"robust",7}");
        foreach (var r in rows)
        {
            var cellsStr = string.Join(" ", r.Cells.Select(c => $"{c.Pct,12:F1}% "));
            var robust = r.AllPositive ? "JA" : $"{r.PositivePhases}/{phases.Length}";
            Console.WriteLine($"{r.Cfg.Label,-34} {cellsStr} {r.MinPhasePct,7:F1}% {r.SumPct,7:F1}% {r.TotalTrades,5} {robust,7}");
        }

        var robustRows = rows.Where(r => r.AllPositive).ToList();
        Console.WriteLine();
        if (robustRows.Count > 0)
            Console.WriteLine($"ROBUST (in ALLEN {phases.Length} Phasen positiv): {string.Join(", ", robustRows.Select(r => $"{r.Cfg.Label} (min {r.MinPhasePct:F1}%)"))}");
        else
            Console.WriteLine($"KEINE Config in allen {phases.Length} Phasen positiv. Bester min-Phasen-Wert: {rows[0].Cfg.Label} mit {rows[0].MinPhasePct:F1}%.");
        return 0;
    }

    private static void WriteReport(
        string outDir, string? label, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        decimal balance, BotSettings settings, List<XsecRow> rows)
    {
        var stamp = label ?? "xsec-screen";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Xsec-Screen (Cross-Sectional-Momentum) — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"**1 gemeinsames Konto** ({balance:F2} USDT) ueber {symbols.Count} Symbole, alle Gates, gefixter Mirror. Nav-TF {navTf}.");
        sb.AppendLine($"Phasen: {string.Join(" | ", phases.Select(p => $"{p.Name} ({p.From:yyyy-MM-dd}..{p.To:yyyy-MM-dd})"))}");
        sb.AppendLine();
        sb.AppendLine("Rangmetrik = **schlechteste Phasen-Rendite**. „robust\" = in JEDER Phase ΣPnL > 0.");
        sb.AppendLine();
        sb.Append("| Config | ");
        foreach (var p in phases) sb.Append($"{p.Name} | ");
        sb.AppendLine("min % | Σ % | Trades | robust |");
        sb.Append("|---|");
        foreach (var _ in phases) sb.Append("---|");
        sb.AppendLine("---|---|---|---|");
        foreach (var r in rows)
        {
            sb.Append($"| {r.Cfg.Label} | ");
            foreach (var c in r.Cells) sb.Append($"{c.Pct:F1}% (n{c.Trades}, PF{FmtPf(c.ProfitFactor)}) | ");
            sb.AppendLine($"{r.MinPhasePct:F1}% | {r.SumPct:F1}% | {r.TotalTrades} | {(r.AllPositive ? "JA" : $"{r.PositivePhases}/{phases.Length}")} |");
        }
        sb.AppendLine();
        var robustRows = rows.Where(r => r.AllPositive).ToList();
        sb.AppendLine("## Auswertung");
        if (robustRows.Count > 0)
            sb.AppendLine($"- **In ALLEN {phases.Length} Phasen positiv:** {string.Join(", ", robustRows.Select(r => $"`{r.Cfg.Label}` (min {r.MinPhasePct:F1}%, Σ {r.SumPct:F1}%)"))}.");
        else
            sb.AppendLine($"- **KEINE** Config ist in allen {phases.Length} Phasen positiv. Bester min-Phasen-Wert: `{rows[0].Cfg.Label}` mit {rows[0].MinPhasePct:F1}%.");

        var mdPath = Path.Combine(outDir, $"xsec-screen-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());
        var jsonPath = Path.Combine(outDir, $"xsec-screen-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            navTf = navTf.ToString(), symbolCount = symbols.Count, startBalance = balance,
            phases = phases.Select(p => new { p.Name, p.From, p.To }),
            rows = rows.Select(r => new
            {
                config = r.Cfg.Label, r.Cfg.LookbackCandles, r.Cfg.RebalanceEveryCandles, r.Cfg.LongK, r.Cfg.ShortK,
                r.Cfg.RiskAdjusted, r.Cfg.AtrStopMultiplier,
                r.MinPhasePct, r.SumPct, r.TotalTrades, r.AllPositive, r.PositivePhases,
                cells = phases.Zip(r.Cells, (p, c) => new { phase = p.Name, c.Trades, c.WinRate, c.ProfitFactor, c.TotalPnl, c.Pct, c.MaxDd })
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nReport: {mdPath}\nJSON  : {jsonPath}");
    }

    private static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);
}
