using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

// ============================================================================
//  Entry-Sweep — variiert die ENTRY-Stellschrauben (Donchian/EMA/ADX + Chop-/
//  Breakout-Filter) ueber das EINE gemeinsame Konto (PortfolioBacktestEngine,
//  alle Gates). Gegenstueck zum PortfolioSweep (der die EXITS SL/BE/RRR/TP1
//  drehte und Donchian/EMA/ADX fix liess): hier bleiben die Exits FIX auf Live
//  (SL2.75/RRR1.5-3.0, BE/TP1-Split aus den Settings) und die Einstiege drehen.
//
//  Anti-Overfitting eingebaut: nach dem Voll-Zeitraum-Ranking werden die Top-K
//  Kandidaten (+ Baseline) automatisch ueber die 4 disjunkten Marktphasen
//  (PhaseScreen.DefaultPhases) gegengeprueft — ein Kandidat, der nur von der
//  juengsten Bull-Phase lebt, faellt dort auf (Phasen-Gotcha, siehe CLAUDE.md).
// ============================================================================

/// <summary>Eine Entry-Sweep-Kombination. Exits (SL/RRR/BE/TP1) sind fix auf Live.</summary>
internal readonly record struct EntryCombo(
    int Donchian, int Ema, decimal AdxMin, bool RisingAdx, decimal BoAtr)
{
    public string Label =>
        $"Don{Donchian}/EMA{Ema}/ADX{AdxMin:0}" +
        (RisingAdx ? "/Chop" : "") +
        (BoAtr > 0m ? $"/BO{BoAtr:0.00}" : "");
}

/// <summary>Ergebnis EINES Portfolio-Laufs einer Entry-Kombi (1 gemeinsames Konto, alle Gates).</summary>
internal sealed record EntryComboResult(
    EntryCombo Combo, int Trades, decimal WinRate, decimal ProfitFactor,
    decimal TotalPnl, decimal MaxDrawdownPercent, int MaxConcurrentOpen);

/// <summary>Phasen-Zeile eines Kandidaten: PnL je Marktphase (USDT) + Anzahl positiver Phasen.</summary>
internal sealed record EntryPhaseRow(
    EntryCombo Combo, decimal TotalPnl4y, IReadOnlyList<(string Phase, decimal Pnl, int Trades)> Phases)
{
    public int PositivePhases => Phases.Count(p => p.Pnl > 0m);
    public decimal WorstPhasePnl => Phases.Count > 0 ? Phases.Min(p => p.Pnl) : 0m;
}

internal static class EntrySweep
{
    /// <summary>Live-Baseline (TrendFollow-Fast): Don10/EMA34/ADX18, keine Entry-Filter.</summary>
    public static readonly EntryCombo Baseline = new(
        Donchian: 10, Ema: 34, AdxMin: 18m, RisingAdx: false, BoAtr: 0m);

    // Live-fixe Exit-Achsen (TrendFollow-Fast) — vom PortfolioSweep bereits durchgekaemmt.
    private const decimal LiveAtrSl = 2.75m;
    private const decimal LiveTp1Rrr = 1.5m;
    private const decimal LiveTp2Rrr = 3.0m;

    /// <summary>Baut das Entry-Grid. full = 4×3×4×2×3 = 288 Kombis, focused = 16 (Schnelldurchlauf).</summary>
    public static List<EntryCombo> BuildGrid(string scope)
    {
        int[] donchian;
        int[] ema;
        decimal[] adxMin;
        bool[] risingAdx;
        decimal[] boAtr;

        if (scope == "focused")
        {
            donchian = [10, 14];
            ema = [34];
            adxMin = [18m, 22m];
            risingAdx = [false, true];
            boAtr = [0m, 0.5m];
        }
        else
        {
            donchian = [8, 10, 14, 20];
            ema = [21, 34, 50];
            adxMin = [15m, 18m, 22m, 25m];
            risingAdx = [false, true];
            boAtr = [0m, 0.25m, 0.5m];
        }

        var grid = new List<EntryCombo>();
        foreach (var d in donchian)
        foreach (var e in ema)
        foreach (var a in adxMin)
        foreach (var r in risingAdx)
        foreach (var b in boAtr)
            grid.Add(new EntryCombo(d, e, a, r, b));

        if (!grid.Contains(Baseline))
            grid.Insert(0, Baseline);
        return grid;
    }

    private static readonly JsonSerializerOptions JsonClone = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static BotSettings CloneSettings(BotSettings baseSettings) =>
        JsonSerializer.Deserialize<BotSettings>(
            JsonSerializer.Serialize(baseSettings, JsonClone), JsonClone)!;

    /// <summary>Ein voller Portfolio-Lauf fuer EINE Entry-Kombi (Exits fix auf Live).</summary>
    private static async Task<EntryComboResult> EvaluateAsync(
        EntryCombo combo, IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo, CancellationToken ct)
    {
        var settings = CloneSettings(baseSettings);
        var engine = new PortfolioBacktestEngine(data, symbolInfo, NullLogger<PortfolioBacktestEngine>.Instance);
        var ovr = new TrendFollowParams(
            DonchianPeriod: combo.Donchian, EmaPeriod: combo.Ema, AdxMin: combo.AdxMin,
            AtrSlMultiplier: LiveAtrSl, Tp1Rrr: LiveTp1Rrr, Tp2Rrr: LiveTp2Rrr,
            RequireRisingAdx: combo.RisingAdx, MinBreakoutAtr: combo.BoAtr);

        var maxConcurrent = 0;
        var report = await engine.RunAsync(
            symbols, navTf, from, to, settings,
            strategyName: "TrendFollow-Fast",
            ct: ct,
            onStepOpenPositions: c => maxConcurrent = Math.Max(maxConcurrent, c),
            trendFollowOverride: ovr).ConfigureAwait(false);

        return new EntryComboResult(
            combo, report.TotalTrades, report.WinRate, report.ProfitFactor,
            report.TotalPnl, report.MaxDrawdownPercent, maxConcurrent);
    }

    // ========================================================================
    //  Haupt-Einstiegspunkt
    // ========================================================================
    public static async Task<int> RunAsync(
        IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo,
        decimal balance, string scope, int parallelism, int minTrades, int phaseTopK,
        string outDir, string? label)
    {
        baseSettings.Backtest.InitialBalance = balance;
        var grid = BuildGrid(scope);

        Console.WriteLine("=== Entry-Sweep (1 gemeinsames Konto, alle Gates) ===");
        Console.WriteLine($"Grid       : {grid.Count} Kombis (scope={scope}) — Exits FIX auf Live (SL{LiveAtrSl}/RRR{LiveTp1Rrr}-{LiveTp2Rrr}/BE+TP1 aus Settings)");
        Console.WriteLine($"Zeitraum   : {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} | Nav-TF {navTf}");
        Console.WriteLine($"Symbole    : {symbols.Count} | Start-Balance {balance:F0} USDT");
        Console.WriteLine($"Live-Spiegel: Scanner-Vorfilter={(baseSettings.Backtest.EnableScannerPrefilter ? "AN" : "aus")} | BTC-Health-Scale={(baseSettings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        Console.WriteLine($"Parallel   : {parallelism} | MinTrades {minTrades} | Phasen-Check Top-{phaseTopK} | Baseline: {Baseline.Label}");
        Console.WriteLine();

        // --- Preload + Baseline: sequenziell, warmt den RAM-Kline-Cache (alle Symbole + BTC-D1/H4) ---
        Console.WriteLine("Preload + Baseline (sequenziell, warmt Kline-Cache)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var baselineResult = await EvaluateAsync(Baseline, symbols, navTf, from, to, baseSettings, data, symbolInfo, default);
        sw.Stop();
        Console.WriteLine($"  Baseline: {Fmt(baselineResult)}  ({sw.Elapsed.TotalSeconds:F1}s/Lauf)");
        Console.WriteLine($"  Geschaetzt fuer {grid.Count} Kombis bei {parallelism}× parallel: ~{grid.Count * sw.Elapsed.TotalSeconds / parallelism / 60:F1} min");
        Console.WriteLine();

        // --- Alle Kombis parallel (Klines warm im RAM, Backtest deterministisch → parallel-sicher) ---
        Console.WriteLine($"Entry-Sweep ueber {grid.Count} Kombis...");
        var results = new EntryComboResult[grid.Count];
        var doneCount = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(Enumerable.Range(0, grid.Count), pOpts, async (i, ct) =>
        {
            results[i] = await EvaluateAsync(grid[i], symbols, navTf, from, to, baseSettings, data, symbolInfo, ct);
            var d = Interlocked.Increment(ref doneCount);
            if (d % 20 == 0 || d == grid.Count) Console.WriteLine($"  [{d}/{grid.Count}]");
        });

        // --- Ranking nach Σ PnL absteigend ---
        var ranked = results.OrderByDescending(r => r.TotalPnl).ToList();
        var positive = ranked.Where(r => r.TotalPnl > 0m).ToList();
        var best = ranked[0];
        var baselineRank = ranked.IndexOf(ranked.First(r => r.Combo.Equals(Baseline))) + 1;

        // --- Phasen-Gegenpruefung: Top-K (MinTrades-Guard gegen Zufallssieger mit Mini-Stichprobe) + Baseline ---
        var candidates = ranked
            .Where(r => r.Trades >= minTrades && !r.Combo.Equals(Baseline))
            .Take(phaseTopK)
            .Select(r => r.Combo)
            .ToList();
        candidates.Insert(0, Baseline);

        var phases = PhaseScreen.DefaultPhases();
        Console.WriteLine($"\nPhasen-Gegenpruefung: {candidates.Count} Kandidaten × {phases.Length} Phasen...");
        var phaseRows = new EntryPhaseRow[candidates.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, candidates.Count), pOpts, async (i, ct) =>
        {
            var combo = candidates[i];
            var cells = new (string Phase, decimal Pnl, int Trades)[phases.Length];
            for (int p = 0; p < phases.Length; p++)
            {
                var r = await EvaluateAsync(combo, symbols, navTf, phases[p].From, phases[p].To,
                    baseSettings, data, symbolInfo, ct);
                cells[p] = (phases[p].Name, r.TotalPnl, r.Trades);
            }
            var total = ranked.First(r => r.Combo.Equals(combo)).TotalPnl;
            phaseRows[i] = new EntryPhaseRow(combo, total, cells);
        });
        var phaseList = phaseRows.ToList();

        WriteReport(outDir, label, scope, from, to, navTf, symbols, balance, baseSettings,
            ranked, baselineResult, positive, best, baselineRank, minTrades, phaseList);

        // --- Console-Zusammenfassung ---
        Console.WriteLine($"\n=== TOP 10 nach Σ PnL ({grid.Count} Kombis, Start {balance:F0} USDT) ===");
        Console.WriteLine($"{"#",-3} {"Kombi",-32} {"n",4} {"WR",7} {"PF",6} {"ΣPnL",10} {"%",7} {"MaxDD%",7} {"",3}");
        for (int i = 0; i < Math.Min(10, ranked.Count); i++)
        {
            var r = ranked[i];
            var pct = balance > 0 ? r.TotalPnl / balance * 100m : 0m;
            var mark = r.Combo.Equals(Baseline) ? " <Baseline" : "";
            Console.WriteLine($"{i + 1,-3} {r.Combo.Label,-32} {r.Trades,4} {r.WinRate,6:F1}% {FmtPf(r.ProfitFactor),6} " +
                $"{r.TotalPnl,10:F2} {pct,6:F1}% {r.MaxDrawdownPercent,6:F1}%{mark}");
        }

        var basePct = balance > 0 ? baselineResult.TotalPnl / balance * 100m : 0m;
        Console.WriteLine($"\nBaseline (Live): {Baseline.Label}");
        Console.WriteLine($"  ΣPnL {baselineResult.TotalPnl:F2} USDT ({basePct:F1}%) | Rang {baselineRank}/{grid.Count} | PF {FmtPf(baselineResult.ProfitFactor)} | n={baselineResult.Trades}");

        Console.WriteLine($"\n=== Phasen-Gegenpruefung (PnL USDT je Phase) ===");
        var phaseNames = phases.Select(p => p.Name).ToArray();
        Console.WriteLine($"{"Kombi",-32} {string.Join(" ", phaseNames.Select(n => $"{n,13}"))} {"pos",4} {"Σ4y",10}");
        foreach (var row in phaseList)
        {
            var mark = row.Combo.Equals(Baseline) ? " <Baseline" : "";
            Console.WriteLine($"{row.Combo.Label,-32} {string.Join(" ", row.Phases.Select(p => $"{p.Pnl,13:F2}"))} {row.PositivePhases,3}/{phases.Length} {row.TotalPnl4y,10:F2}{mark}");
        }

        Console.WriteLine($"\n=== AUSWERTUNG ===");
        Console.WriteLine($"Beste Kombi     : {best.Combo.Label} (ΣPnL {best.TotalPnl:F2} USDT, n={best.Trades})");
        Console.WriteLine($"Schlaegt Baseline: {(best.TotalPnl > baselineResult.TotalPnl ? "JA" : "NEIN")} (Δ {best.TotalPnl - baselineResult.TotalPnl:+0.00;-0.00} USDT)");
        var baseRow = phaseList.First(r => r.Combo.Equals(Baseline));
        var robustBetter = phaseList
            .Where(r => !r.Combo.Equals(Baseline)
                        && r.TotalPnl4y > baseRow.TotalPnl4y
                        && r.PositivePhases >= baseRow.PositivePhases
                        && r.WorstPhasePnl >= baseRow.WorstPhasePnl)
            .OrderByDescending(r => r.TotalPnl4y)
            .ToList();
        if (robustBetter.Count > 0)
            Console.WriteLine($"Robust besser als Baseline (Σ4y UND Phasen-Bilanz UND Worst-Phase): " +
                string.Join(", ", robustBetter.Select(r => r.Combo.Label)));
        else
            Console.WriteLine("Robust besser als Baseline: KEINER der Phasen-Kandidaten (Σ4y+Phasen-Bilanz+Worst-Phase).");
        return 0;
    }

    // ========================================================================
    //  Report (Markdown + JSON)
    // ========================================================================
    private static void WriteReport(
        string outDir, string? label, string scope, DateTime from, DateTime to, TimeFrame navTf,
        IReadOnlyList<string> symbols, decimal balance, BotSettings settings,
        List<EntryComboResult> ranked, EntryComboResult baseline,
        List<EntryComboResult> positive, EntryComboResult best, int baselineRank,
        int minTrades, List<EntryPhaseRow> phaseRows)
    {
        var stamp = label ?? "entry-sweep";
        Directory.CreateDirectory(outDir);

        var basePct = balance > 0 ? baseline.TotalPnl / balance * 100m : 0m;
        var bestPct = balance > 0 ? best.TotalPnl / balance * 100m : 0m;

        var sb = new StringBuilder();
        sb.AppendLine($"# Entry-Sweep — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"**1 gemeinsames Konto** ({balance:F0} USDT) ueber {symbols.Count} Symbole, alle konto-weiten Gates aktiv.");
        sb.AppendLine($"Exits FIX auf Live (SL{LiveAtrSl}/RRR{LiveTp1Rrr}-{LiveTp2Rrr}, BE/TP1-Split aus Settings) — variiert: Donchian / EMA / ADX / Chop-Filter / Breakout-Distanz.");
        sb.AppendLine();
        sb.AppendLine($"- Grid: **{ranked.Count} Kombis** (scope={scope}), MinTrades-Guard fuer Phasen-Check: {minTrades}");
        sb.AppendLine($"- Zeitraum: {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} | Nav-TF {navTf}");
        sb.AppendLine($"- Live-Spiegel: Scanner-Vorfilter={(settings.Backtest.EnableScannerPrefilter ? "AN" : "aus")}, BTC-Health-Scale={(settings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        sb.AppendLine($"- Gates: MaxOpenPositions={settings.Risk.MaxOpenPositions}, MaxTotalMargin={settings.Risk.MaxTotalMarginPercent}%, MaxCorrelated={settings.Risk.MaxCorrelatedExposurePercent}%");
        sb.AppendLine();

        sb.AppendLine("## Auswertung");
        sb.AppendLine($"- **Baseline (Live)** `{Baseline.Label}`: ΣPnL **{baseline.TotalPnl:F2} USDT** ({basePct:F1}%), PF {FmtPf(baseline.ProfitFactor)}, n={baseline.Trades} — Rang **{baselineRank}/{ranked.Count}**.");
        sb.AppendLine($"- **Beste Kombi** `{best.Combo.Label}`: ΣPnL **{best.TotalPnl:F2} USDT** ({bestPct:F1}%), PF {FmtPf(best.ProfitFactor)}, n={best.Trades}, MaxDD {best.MaxDrawdownPercent:F1}%.");
        sb.AppendLine($"- **Schlaegt die beste Kombi die Baseline?** {(best.TotalPnl > baseline.TotalPnl ? "JA" : "NEIN")} (Δ {best.TotalPnl - baseline.TotalPnl:+0.00;-0.00} USDT).");
        sb.AppendLine($"- **Positiv (ΣPnL > 0):** {positive.Count}/{ranked.Count} Kombis.");
        sb.AppendLine();

        sb.AppendLine("## Phasen-Gegenpruefung (Top-Kandidaten + Baseline)");
        sb.AppendLine();
        sb.AppendLine("Anti-Bull-Overfitting: PnL je disjunkter Marktphase. Ein Kandidat ist nur dann eine echte");
        sb.AppendLine("Verbesserung, wenn Σ4y besser ist UND Phasen-Bilanz/Worst-Phase nicht schlechter als Baseline.");
        sb.AppendLine();
        var phaseNames = phaseRows.Count > 0 ? phaseRows[0].Phases.Select(p => p.Phase).ToArray() : [];
        sb.AppendLine($"| Kombi | {string.Join(" | ", phaseNames)} | pos. Phasen | Worst | Σ 4y | |");
        sb.AppendLine($"|---|{string.Concat(Enumerable.Repeat("---|", phaseNames.Length))}---|---|---|---|");
        foreach (var row in phaseRows)
        {
            var mark = row.Combo.Equals(Baseline) ? "**Baseline**" : "";
            sb.AppendLine($"| {row.Combo.Label} | {string.Join(" | ", row.Phases.Select(p => $"{p.Pnl:F2} (n={p.Trades})"))} | {row.PositivePhases}/{phaseNames.Length} | {row.WorstPhasePnl:F2} | {row.TotalPnl4y:F2} | {mark} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Alle Kombis nach Σ PnL (absteigend)");
        sb.AppendLine("| # | Kombi | n | WinRate | PF | Σ PnL (USDT) | % | MaxDD% | MaxOpen | |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < ranked.Count; i++)
        {
            var r = ranked[i];
            var pct = balance > 0 ? r.TotalPnl / balance * 100m : 0m;
            var mark = r.Combo.Equals(Baseline) ? "**Baseline**" : "";
            sb.AppendLine($"| {i + 1} | {r.Combo.Label} | {r.Trades} | {r.WinRate:F1}% | {FmtPf(r.ProfitFactor)} | {r.TotalPnl:F2} | {pct:F1}% | {r.MaxDrawdownPercent:F1}% | {r.MaxConcurrentOpen} | {mark} |");
        }

        var mdPath = Path.Combine(outDir, $"entry-sweep-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());

        var jsonPath = Path.Combine(outDir, $"entry-sweep-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            scope, from, to, navTf = navTf.ToString(), symbolCount = symbols.Count, startBalance = balance,
            scannerFilter = settings.Backtest.EnableScannerPrefilter,
            btcHealth = settings.Backtest.EnableBtcHealthScale,
            gridSize = ranked.Count, minTrades,
            baseline = new { combo = Baseline, result = baseline, rank = baselineRank, pct = basePct },
            best = new { result = best, pct = bestPct, beatsBaseline = best.TotalPnl > baseline.TotalPnl },
            positiveCount = positive.Count,
            phaseCheck = phaseRows.Select(r => new
            {
                r.Combo, r.TotalPnl4y, r.PositivePhases, r.WorstPhasePnl,
                phases = r.Phases.Select(p => new { p.Phase, p.Pnl, p.Trades }),
                isBaseline = r.Combo.Equals(Baseline)
            }),
            ranked = ranked.Select(r => new
            {
                r.Combo, r.Trades, r.WinRate, r.ProfitFactor, r.TotalPnl,
                pct = balance > 0 ? r.TotalPnl / balance * 100m : 0m,
                r.MaxDrawdownPercent, r.MaxConcurrentOpen,
                isBaseline = r.Combo.Equals(Baseline)
            })
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"\nReport: {mdPath}");
        Console.WriteLine($"JSON  : {jsonPath}");
    }

    private static string Fmt(EntryComboResult r) =>
        $"n={r.Trades,4} WR={r.WinRate,5:F1}% PF={FmtPf(r.ProfitFactor),5} ΣPnL={r.TotalPnl,9:F2} MaxDD={r.MaxDrawdownPercent,5:F1}% MaxOpen={r.MaxConcurrentOpen}";

    private static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);
}
