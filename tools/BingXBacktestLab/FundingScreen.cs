using System.Text;
using System.Text.Json;
using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

// ============================================================================
//  Funding-Screen — Funding-Carry-Faktor + Momentum+Carry-Kombination ueber die
//  4 Phasen. Nutzt ECHTE historische Funding-Raten (FundingHistoryProvider).
// ============================================================================

internal static class FundingScreen
{
    /// <summary>
    /// Carry-Faktor (beide Richtungen), Momentum+Carry-Kombi (versch. Gewichte), versch. Rebalance.
    /// Carry-Faktor laut Literatur = long high-funding / short low-funding (Sharpe ~0.74); naive
    /// Harvest (long neg) auf CEX historisch negativ — beide werden getestet.
    /// </summary>
    public static FundingCarryParams[] DefaultConfigs() =>
    [
        // Reiner Carry-Faktor (akademische Richtung: long high-funding / short low)
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 6,  LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true),
        new(LookbackSettlements: 9,  RebalanceEveryCandles: 42, LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true),
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 6,  LongK: 5, ShortK: 5, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true),
        // Funding-Harvest (Gegenrichtung — Erwartung negativ, zur Bestaetigung)
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 6,  LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: false),
        // Momentum+Carry-Kombination (50/50 und 70/30 Momentum)
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 42, LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true, MomentumWeight: 0.5m, MomentumLookback: 84),
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 42, LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true, MomentumWeight: 0.7m, MomentumLookback: 84),
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 42, LongK: 3, ShortK: 3, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true, MomentumWeight: 0.3m, MomentumLookback: 84),
        // Combo long-only (Long-Bias laut Literatur bevorzugt)
        new(LookbackSettlements: 3,  RebalanceEveryCandles: 42, LongK: 3, ShortK: 0, MinAbsFunding: 0m, LeverageCap: 1, LongHighFunding: true, MomentumWeight: 0.5m, MomentumLookback: 84),
    ];

    private sealed record Cell(int Trades, decimal WinRate, decimal ProfitFactor, decimal TotalPnl, decimal Pct, decimal MaxDd);
    private sealed record Row(FundingCarryParams Cfg, Cell[] Cells)
    {
        public decimal MinPhasePct => Cells.Min(c => c.Pct);
        public decimal SumPct => Cells.Sum(c => c.Pct);
        public int TotalTrades => Cells.Sum(c => c.Trades);
        public bool AllPositive => Cells.All(c => c.Pct > 0m);
        public int PositivePhases => Cells.Count(c => c.Pct > 0m);
    }

    private static async Task<Cell> EvaluateAsync(
        FundingCarryParams cfg, Phase phase, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings settings, decimal balance, IPublicMarketDataClient data,
        IReadOnlyDictionary<string, List<(DateTime, decimal)>> funding,
        ISymbolInfoProvider? symbolInfo, CancellationToken ct)
    {
        var engine = new FundingCarryEngine(data, funding, symbolInfo, NullLogger<FundingCarryEngine>.Instance);
        var report = await engine.RunAsync(symbols, navTf, phase.From, phase.To, settings, cfg, ct).ConfigureAwait(false);
        var pct = balance > 0 ? report.TotalPnl / balance * 100m : 0m;
        return new Cell(report.TotalTrades, report.WinRate, report.ProfitFactor, report.TotalPnl, pct, report.MaxDrawdownPercent);
    }

    public static async Task<int> RunAsync(
        IReadOnlyList<FundingCarryParams> configs, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings baseSettings, IPublicMarketDataClient data, FundingHistoryProvider fundingProvider,
        ISymbolInfoProvider? symbolInfo, decimal balance, int parallelism, string outDir, string? label)
    {
        baseSettings.Backtest.InitialBalance = balance;
        Console.WriteLine("=== Funding-Screen (Carry-Faktor + Momentum+Carry-Kombi, echte Funding-Historie) ===");
        Console.WriteLine($"Configs    : {configs.Count} | Symbole: {symbols.Count} | Nav-TF {navTf} | Balance {balance:F2} USDT");

        // Funding-Historie ueber den gesamten Phasen-Bereich vorab laden (einmal, gecached).
        var fullFrom = phases.Min(p => p.From).AddDays(-30);
        var fullTo = phases.Max(p => p.To);
        Console.WriteLine($"Lade Funding-Historie {symbols.Count} Symbole ({fullFrom:yyyy-MM-dd}..{fullTo:yyyy-MM-dd})...");
        var funding = new Dictionary<string, List<(DateTime, decimal)>>();
        var loaded = 0;
        foreach (var sym in symbols)
        {
            var pts = await fundingProvider.GetAsync(sym, fullFrom, fullTo).ConfigureAwait(false);
            if (pts.Count > 0) funding[sym] = pts.Select(x => (x.TimeUtc, x.Rate)).ToList();
            if (++loaded % 20 == 0) Console.WriteLine($"  Funding {loaded}/{symbols.Count}");
        }
        Console.WriteLine($"  -> {funding.Count} Symbole mit Funding-Historie\n");

        var jobs = (from c in configs from p in phases select (c, p)).ToArray();
        var cells = new Dictionary<(string, string), Cell>();
        var lockObj = new object();
        var done = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(jobs, pOpts, async (job, ct) =>
        {
            var cell = await EvaluateAsync(job.c, job.p, symbols, navTf, baseSettings, balance, data, funding, symbolInfo, ct);
            lock (lockObj) cells[(job.c.Label, job.p.Name)] = cell;
            var d = Interlocked.Increment(ref done);
            if (d % 4 == 0 || d == jobs.Length) Console.WriteLine($"  [{d}/{jobs.Length}]");
        });

        var rows = configs
            .Select(c => new Row(c, phases.Select(p => cells[(c.Label, p.Name)]).ToArray()))
            .OrderByDescending(r => r.MinPhasePct)
            .ToList();

        WriteReport(outDir, label, phases, symbols, navTf, balance, rows);

        Console.WriteLine($"\n=== ROBUSTHEITS-RANKING (nach schlechtester Phasen-Rendite) ===");
        Console.WriteLine($"{"Config",-52} " + string.Join(" ", phases.Select(p => $"{p.Name,14}")) + $" {"min%",8} {"Σ%",8} {"n",5} {"robust",7}");
        foreach (var r in rows)
        {
            var cellsStr = string.Join(" ", r.Cells.Select(c => $"{c.Pct,12:F1}% "));
            var robust = r.AllPositive ? "JA" : $"{r.PositivePhases}/{phases.Length}";
            Console.WriteLine($"{r.Cfg.Label,-52} {cellsStr} {r.MinPhasePct,7:F1}% {r.SumPct,7:F1}% {r.TotalTrades,5} {robust,7}");
        }
        var robustRows = rows.Where(r => r.AllPositive).ToList();
        Console.WriteLine();
        Console.WriteLine(robustRows.Count > 0
            ? $"ROBUST (in ALLEN {phases.Length} Phasen positiv): {string.Join(", ", robustRows.Select(r => $"{r.Cfg.Label} (min {r.MinPhasePct:F1}%)"))}"
            : $"KEINE Config in allen {phases.Length} Phasen positiv. Bester min-Phasen-Wert: {rows[0].Cfg.Label} mit {rows[0].MinPhasePct:F1}%.");
        return 0;
    }

    private static void WriteReport(
        string outDir, string? label, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        decimal balance, List<Row> rows)
    {
        var stamp = label ?? "funding-screen";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Funding-Screen (Carry-Faktor + Momentum+Carry) — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"**1 gemeinsames Konto** ({balance:F2} USDT) ueber {symbols.Count} Symbole, echte Funding-Historie. Nav-TF {navTf}.");
        sb.AppendLine($"Phasen: {string.Join(" | ", phases.Select(p => $"{p.Name} ({p.From:yyyy-MM-dd}..{p.To:yyyy-MM-dd})"))}");
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
            foreach (var c in r.Cells) sb.Append($"{c.Pct:F1}% (n{c.Trades}) | ");
            sb.AppendLine($"{r.MinPhasePct:F1}% | {r.SumPct:F1}% | {r.TotalTrades} | {(r.AllPositive ? "JA" : $"{r.PositivePhases}/{phases.Length}")} |");
        }
        File.WriteAllText(Path.Combine(outDir, $"funding-screen-{stamp}.md"), sb.ToString());
        Console.WriteLine($"\nReport: {Path.Combine(outDir, $"funding-screen-{stamp}.md")}");
    }
}
