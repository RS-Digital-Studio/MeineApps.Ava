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
//  Phasen-Screen — testet JEDE Strategie ueber 4 disjunkte Marktphasen auf dem
//  live-getreuen Portfolio-Mirror (EIN Konto, alle Gates). Ziel: eine Strategie
//  finden, die NICHT nur aggregiert, sondern in JEDER Phase profitabel ist
//  (Robustheit statt Bull-Overfitting). Rangmetrik = schlechteste Phasen-Rendite.
//
//  Laeuft auf dem GEFIXTEN Mirror (Bug A/B/B2 + 3-stufiger Runner) — alle
//  frueheren kein-Edge-Schluesse fuer Strategien liefen auf dem verzerrten Mirror.
// ============================================================================

/// <summary>Eine disjunkte Marktphase (Name + Zeitfenster).</summary>
internal readonly record struct Phase(string Name, DateTime From, DateTime To);

/// <summary>Ergebnis EINER Strategie in EINER Phase (1 Konto, alle Gates).</summary>
internal sealed record PhaseCell(
    int Trades, decimal WinRate, decimal ProfitFactor, decimal TotalPnl, decimal Pct, decimal MaxDrawdownPercent);

/// <summary>Alle Phasen-Ergebnisse EINER Strategie + Robustheits-Kennzahlen.</summary>
internal sealed record StrategyRow(string Strategy, PhaseCell[] Cells)
{
    public decimal MinPhasePct => Cells.Min(c => c.Pct);
    public decimal SumPct => Cells.Sum(c => c.Pct);
    public int TotalTrades => Cells.Sum(c => c.Trades);
    public bool AllPhasesPositive => Cells.All(c => c.Pct > 0m);
    public int PositivePhases => Cells.Count(c => c.Pct > 0m);
}

internal static class PhaseScreen
{
    /// <summary>4 disjunkte Phasen ~1 Jahr (BingX-Perps ab ~April 2022): Bear/Recovery/Bull/Recent.</summary>
    public static Phase[] DefaultPhases() =>
    [
        new("2022-Bear",     U(2022, 6, 1),  U(2023, 6, 1)),
        new("2023-Recovery", U(2023, 6, 1),  U(2024, 6, 1)),
        new("2024-Bull",     U(2024, 6, 1),  U(2025, 6, 1)),
        new("2025-Recent",   U(2025, 6, 1),  U(2026, 5, 31)),
    ];

    /// <summary>Default-Strategie-Set: bestehende TrendFollow-Familie + MeanReversion (alles aus StrategyFactory).</summary>
    public static readonly string[] DefaultStrategies =
    [
        "TrendFollow", "TrendFollow-Fast", "TrendFollow-Fast-Chop", "TrendFollow-Fast-BO",
        "TrendFollow-Fast-ChopBO", "TrendFollow-Wide", "TrendFollow-Strong", "MeanReversion",
    ];

    private static DateTime U(int y, int m, int d) =>
        DateTime.SpecifyKind(new DateTime(y, m, d), DateTimeKind.Utc);

    private static async Task<PhaseCell> EvaluateAsync(
        string strategy, Phase phase, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings settings, decimal balance, IPublicMarketDataClient data,
        ISymbolInfoProvider? symbolInfo, CancellationToken ct)
    {
        var engine = new PortfolioBacktestEngine(data, symbolInfo, NullLogger<PortfolioBacktestEngine>.Instance);
        var report = await engine.RunAsync(
            symbols, navTf, phase.From, phase.To, settings,
            strategyName: strategy, ct: ct).ConfigureAwait(false);
        var pct = balance > 0 ? report.TotalPnl / balance * 100m : 0m;
        return new PhaseCell(report.TotalTrades, report.WinRate, report.ProfitFactor,
            report.TotalPnl, pct, report.MaxDrawdownPercent);
    }

    public static async Task<int> RunAsync(
        IReadOnlyList<string> strategies, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo,
        decimal balance, int parallelism, string outDir, string? label)
    {
        baseSettings.Backtest.InitialBalance = balance;

        Console.WriteLine("=== Phasen-Screen (1 Konto, alle Gates, gefixter Mirror) ===");
        Console.WriteLine($"Strategien : {strategies.Count} ({string.Join(", ", strategies)})");
        Console.WriteLine($"Phasen     : {string.Join(" | ", phases.Select(p => $"{p.Name} {p.From:yyyy-MM}..{p.To:yyyy-MM}"))}");
        Console.WriteLine($"Symbole    : {symbols.Count} | Nav-TF {navTf} | Start-Balance {balance:F2} USDT");
        Console.WriteLine($"Live-Spiegel: Scanner-Vorfilter={(baseSettings.Backtest.EnableScannerPrefilter ? "AN" : "aus")} | BTC-Health={(baseSettings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        Console.WriteLine();

        // Preload (1 Lauf) waermt den RAM-Kline-Cache, dann alles parallel (deterministisch → parallel-sicher).
        Console.WriteLine("Preload (waermt Kline-Cache)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = await EvaluateAsync(strategies[0], phases[0], symbols, navTf, baseSettings, balance, data, symbolInfo, default);
        sw.Stop();
        var total = strategies.Count * phases.Length;
        Console.WriteLine($"  {sw.Elapsed.TotalSeconds:F1}s/Lauf → ~{total * sw.Elapsed.TotalSeconds / parallelism / 60:F1} min fuer {total} Laeufe\n");

        var jobs = (from s in strategies from p in phases select (s, p)).ToArray();
        var cells = new Dictionary<(string, string), PhaseCell>();
        var lockObj = new object();
        var done = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(jobs, pOpts, async (job, ct) =>
        {
            var cell = await EvaluateAsync(job.s, job.p, symbols, navTf, baseSettings, balance, data, symbolInfo, ct);
            lock (lockObj) cells[(job.s, job.p.Name)] = cell;
            var d = Interlocked.Increment(ref done);
            if (d % 8 == 0 || d == jobs.Length) Console.WriteLine($"  [{d}/{jobs.Length}]");
        });

        var rows = strategies
            .Select(s => new StrategyRow(s, phases.Select(p => cells[(s, p.Name)]).ToArray()))
            .OrderByDescending(r => r.MinPhasePct)   // Robustheit: maximiere die SCHLECHTESTE Phase
            .ToList();

        WriteReport(outDir, label, phases, symbols, navTf, balance, baseSettings, rows);

        Console.WriteLine($"\n=== ROBUSTHEITS-RANKING (nach schlechtester Phasen-Rendite) ===");
        Console.WriteLine($"{"Strategie",-26} " + string.Join(" ", phases.Select(p => $"{p.Name,14}")) + $" {"min%",8} {"Σ%",8} {"n",5} {"robust",7}");
        foreach (var r in rows)
        {
            var cellsStr = string.Join(" ", r.Cells.Select(c => $"{c.Pct,12:F1}% "));
            var robust = r.AllPhasesPositive ? "JA" : $"{r.PositivePhases}/4";
            Console.WriteLine($"{r.Strategy,-26} {cellsStr} {r.MinPhasePct,7:F1}% {r.SumPct,7:F1}% {r.TotalTrades,5} {robust,7}");
        }

        var robustRows = rows.Where(r => r.AllPhasesPositive).ToList();
        Console.WriteLine();
        if (robustRows.Count > 0)
            Console.WriteLine($"ROBUST (in ALLEN {phases.Length} Phasen positiv): {string.Join(", ", robustRows.Select(r => $"{r.Strategy} (min {r.MinPhasePct:F1}%)"))}");
        else
            Console.WriteLine($"KEINE Strategie ist in allen {phases.Length} Phasen positiv. Bester min-Phasen-Wert: {rows[0].Strategy} mit {rows[0].MinPhasePct:F1}%.");
        return 0;
    }

    private static void WriteReport(
        string outDir, string? label, Phase[] phases, IReadOnlyList<string> symbols, TimeFrame navTf,
        decimal balance, BotSettings settings, List<StrategyRow> rows)
    {
        var stamp = label ?? "phase-screen";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Phasen-Screen — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"**1 gemeinsames Konto** ({balance:F2} USDT) ueber {symbols.Count} Symbole, alle Gates aktiv, gefixter Mirror.");
        sb.AppendLine($"Nav-TF {navTf} | Scanner-Vorfilter={(settings.Backtest.EnableScannerPrefilter ? "AN" : "aus")} | BTC-Health={(settings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        sb.AppendLine($"Phasen: {string.Join(" | ", phases.Select(p => $"{p.Name} ({p.From:yyyy-MM-dd}..{p.To:yyyy-MM-dd})"))}");
        sb.AppendLine();
        sb.AppendLine("Rangmetrik = **schlechteste Phasen-Rendite** (Robustheit). „robust\" = in JEDER Phase ΣPnL > 0.");
        sb.AppendLine();

        sb.AppendLine("## Ergebnis (ΣPnL % je Phase)");
        sb.Append("| Strategie | ");
        foreach (var p in phases) sb.Append($"{p.Name} | ");
        sb.AppendLine("min % | Σ % | Trades | robust |");
        sb.Append("|---|");
        foreach (var _ in phases) sb.Append("---|");
        sb.AppendLine("---|---|---|---|");
        foreach (var r in rows)
        {
            sb.Append($"| {r.Strategy} | ");
            foreach (var c in r.Cells) sb.Append($"{c.Pct:F1}% (n{c.Trades}, PF{FmtPf(c.ProfitFactor)}) | ");
            sb.AppendLine($"{r.MinPhasePct:F1}% | {r.SumPct:F1}% | {r.TotalTrades} | {(r.AllPhasesPositive ? "JA" : $"{r.PositivePhases}/{phases.Length}")} |");
        }
        sb.AppendLine();

        var robustRows = rows.Where(r => r.AllPhasesPositive).ToList();
        sb.AppendLine("## Auswertung");
        if (robustRows.Count > 0)
            sb.AppendLine($"- **In ALLEN {phases.Length} Phasen positiv:** {string.Join(", ", robustRows.Select(r => $"`{r.Strategy}` (min {r.MinPhasePct:F1}%, Σ {r.SumPct:F1}%)"))}.");
        else
            sb.AppendLine($"- **KEINE** Strategie ist in allen {phases.Length} Phasen positiv. Bester min-Phasen-Wert: `{rows[0].Strategy}` mit {rows[0].MinPhasePct:F1}%.");
        sb.AppendLine();

        var mdPath = Path.Combine(outDir, $"phase-screen-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());
        var jsonPath = Path.Combine(outDir, $"phase-screen-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            navTf = navTf.ToString(), symbolCount = symbols.Count, startBalance = balance,
            phases = phases.Select(p => new { p.Name, p.From, p.To }),
            rows = rows.Select(r => new
            {
                r.Strategy, r.MinPhasePct, r.SumPct, r.TotalTrades, r.AllPhasesPositive, r.PositivePhases,
                cells = phases.Zip(r.Cells, (p, c) => new { phase = p.Name, c.Trades, c.WinRate, c.ProfitFactor, c.TotalPnl, c.Pct, c.MaxDrawdownPercent })
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nReport: {mdPath}\nJSON  : {jsonPath}");
    }

    private static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);
}
