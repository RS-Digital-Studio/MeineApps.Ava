using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BingXBot.Backtest.Portfolio;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

// ============================================================================
//  Portfolio-Sweep — variiert die Strategie-/Risk-Stellschrauben (SL/BE/TP-RRR/TP1-Split)
//  ueber das EINE gemeinsame Konto (PortfolioBacktestEngine, alle Gates), um zu finden,
//  ob IRGENDEINE Parameter-Kombination das live-getreue Portfolio-Ergebnis ins Plus dreht.
//
//  Abgrenzung zum Single-Symbol-Sweep (Sweep.cs): der laeuft auf isolierten 1000-USDT-Konten
//  pro Symbol (Gates feuern NIE) — unrealistisch. Dieser Sweep nutzt fuer JEDE Kombi einen
//  vollen PortfolioBacktestEngine-Lauf ueber ALLE Symbole auf EINEM Konto (= Spiegelbild des
//  Live-Bots). Jede Kombi ist teuer (1 Voll-Lauf), daher ein FOKUSSIERTES Grid, nicht das
//  volle Don/EMA/ADX-Kreuzprodukt: Donchian/EMA/ADX bleiben FIX auf Live (10/34/18), weil der
//  Live-Bot diese nicht variiert.
//
//  WICHTIG (Backtest-Engine-Eigenheiten, wie Sweep.cs):
//   - SL/RRR sind Strategie-Konstruktor-Argumente → via PortfolioBacktestEngine.trendFollowOverride.
//   - BE-Trigger = RiskSettings.BreakevenTriggerRMultiple.
//   - TP1-Teilschliessungs-Anteil = BacktestSettings.Tp1CloseRatio (NICHT RiskSettings!).
// ============================================================================

/// <summary>Eine Portfolio-Sweep-Kombination. Donchian/EMA/ADX sind fix auf Live (10/34/18).</summary>
internal readonly record struct PortfolioCombo(
    decimal AtrSl, decimal Rrr1, decimal Rrr2, decimal BeTrigger, decimal Tp1Split)
{
    public string Label => $"SL{AtrSl:0.00}/RRR{Rrr1:0.0}-{Rrr2:0.0}/BE{BeTrigger:0.0}/TP1×{Tp1Split:0.00}";
}

/// <summary>Ergebnis EINES Portfolio-Laufs einer Kombi (1 gemeinsames Konto, alle Gates).</summary>
internal sealed record PortfolioComboResult(
    PortfolioCombo Combo, int Trades, decimal WinRate, decimal ProfitFactor,
    decimal TotalPnl, decimal MaxDrawdownPercent, int MaxConcurrentOpen);

internal static class PortfolioSweep
{
    /// <summary>Live-Baseline (TrendFollow-Fast + RiskSettings-Defaults): SL2.75/RRR1.5-3.0/BE2.0/TP1×0.5.</summary>
    public static readonly PortfolioCombo Baseline = new(
        AtrSl: 2.75m, Rrr1: 1.5m, Rrr2: 3.0m, BeTrigger: 2.0m, Tp1Split: 0.5m);

    // Live-fixe Strategie-Achsen (TrendFollow-Fast) — werden nicht variiert.
    private const int LiveDonchian = 10;
    private const int LiveEma = 34;
    private const decimal LiveAdxMin = 18m;

    /// <summary>
    /// Baut das Sweep-Grid. <paramref name="scope"/> = focused (kompakt) ODER full (das im Auftrag
    /// spezifizierte 5×3×3×3 = 135er-Grid). Die Baseline-Kombi ist immer enthalten.
    /// </summary>
    public static List<PortfolioCombo> BuildGrid(string scope)
    {
        decimal[] atrSl;
        decimal[] beTrigger;
        (decimal R1, decimal R2)[] rrr;
        decimal[] tp1Split;

        if (scope == "focused")
        {
            // Kompaktes Set fuer den Schnelldurchlauf: 3×2×2×2 = 24 Kombis (Baseline-Achsen + naechste Nachbarn).
            atrSl = [2.5m, 2.75m, 3.0m];
            beTrigger = [1.5m, 2.0m];
            rrr = [(1.5m, 3.0m), (2.0m, 4.0m)];
            tp1Split = [0.3m, 0.5m];
        }
        else
        {
            // full = das im Auftrag definierte Grid: 5×3×3×3 = 135 Kombis.
            atrSl = [2.0m, 2.5m, 2.75m, 3.0m, 3.5m];
            beTrigger = [1.5m, 2.0m, 2.5m];
            rrr = [(1.5m, 3.0m), (2.0m, 4.0m), (1.5m, 4.0m)];
            tp1Split = [0.3m, 0.5m, 0.7m];
        }

        var grid = new List<PortfolioCombo>();
        foreach (var sl in atrSl)
        foreach (var (r1, r2) in rrr)
        foreach (var be in beTrigger)
        foreach (var sp in tp1Split)
            grid.Add(new PortfolioCombo(sl, r1, r2, be, sp));

        // Baseline-Kombi garantiert im Grid (zum Vergleich), auch wenn ihre Achsen-Werte nicht alle im Set sind.
        if (!grid.Contains(Baseline))
            grid.Insert(0, Baseline);
        return grid;
    }

    private static readonly JsonSerializerOptions JsonClone = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Klont die Basis-Settings und ueberschreibt BE-Trigger (Risk) + TP1/TP2-Split (Backtest!).</summary>
    private static BotSettings ApplyCombo(BotSettings baseSettings, PortfolioCombo c)
    {
        var clone = JsonSerializer.Deserialize<BotSettings>(
            JsonSerializer.Serialize(baseSettings, JsonClone), JsonClone)!;
        clone.Risk.BreakevenTriggerRMultiple = c.BeTrigger;
        clone.Backtest.Tp1CloseRatio = c.Tp1Split;          // Backtest liest DIESEN Wert (nicht Risk.Tp1CloseRatio).
        clone.Backtest.Tp2CloseRatio = 1m - c.Tp1Split;
        return clone;
    }

    /// <summary>Ein voller Portfolio-Lauf fuer EINE Kombi (1 gemeinsames Konto, alle Gates).</summary>
    private static async Task<PortfolioComboResult> EvaluateAsync(
        PortfolioCombo combo, IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo, CancellationToken ct)
    {
        var settings = ApplyCombo(baseSettings, combo);
        var engine = new PortfolioBacktestEngine(data, symbolInfo, NullLogger<PortfolioBacktestEngine>.Instance);
        var ovr = new TrendFollowParams(
            DonchianPeriod: LiveDonchian, EmaPeriod: LiveEma, AdxMin: LiveAdxMin,
            AtrSlMultiplier: combo.AtrSl, Tp1Rrr: combo.Rrr1, Tp2Rrr: combo.Rrr2);

        var maxConcurrent = 0;
        var report = await engine.RunAsync(
            symbols, navTf, from, to, settings,
            strategyName: "TrendFollow-Fast",
            ct: ct,
            onStepOpenPositions: c => maxConcurrent = Math.Max(maxConcurrent, c),
            trendFollowOverride: ovr).ConfigureAwait(false);

        return new PortfolioComboResult(
            combo, report.TotalTrades, report.WinRate, report.ProfitFactor,
            report.TotalPnl, report.MaxDrawdownPercent, maxConcurrent);
    }

    // ========================================================================
    //  Haupt-Einstiegspunkt
    // ========================================================================
    public static async Task<int> RunAsync(
        IReadOnlyList<string> symbols, TimeFrame navTf, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data, ISymbolInfoProvider? symbolInfo,
        decimal balance, string scope, int parallelism, string outDir, string? label)
    {
        baseSettings.Backtest.InitialBalance = balance;
        var grid = BuildGrid(scope);

        Console.WriteLine("=== Portfolio-Sweep (1 gemeinsames Konto, alle Gates) ===");
        Console.WriteLine($"Grid       : {grid.Count} Kombis (scope={scope}) — Donchian/EMA/ADX FIX auf Live ({LiveDonchian}/{LiveEma}/{LiveAdxMin:0})");
        Console.WriteLine($"Zeitraum   : {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} | Nav-TF {navTf}");
        Console.WriteLine($"Symbole    : {symbols.Count} | Start-Balance {balance:F0} USDT");
        Console.WriteLine($"Live-Spiegel: Scanner-Vorfilter={(baseSettings.Backtest.EnableScannerPrefilter ? "AN" : "aus")} | BTC-Health-Scale={(baseSettings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        Console.WriteLine($"Parallel   : {parallelism} | Baseline: {Baseline.Label}");
        Console.WriteLine();

        // --- Preload + Baseline: sequenziell, warmt den RAM-Kline-Cache (alle Symbole + BTC-D1/H4) ---
        Console.WriteLine("Preload + Baseline (sequenziell, warmt Kline-Cache)...");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var baselineResult = await EvaluateAsync(Baseline, symbols, navTf, from, to, baseSettings, data, symbolInfo, default);
        sw.Stop();
        Console.WriteLine($"  Baseline: {Fmt(baselineResult)}  ({sw.Elapsed.TotalSeconds:F1}s/Lauf)");
        Console.WriteLine($"  Geschaetzt fuer {grid.Count} Kombis bei {parallelism}× parallel: ~{grid.Count * sw.Elapsed.TotalSeconds / parallelism / 60:F1} min");
        Console.WriteLine();

        // --- Alle Kombis parallel (Klines liegen warm im RAM, Backtest ist deterministisch → parallel-sicher) ---
        Console.WriteLine($"Portfolio-Sweep ueber {grid.Count} Kombis...");
        var results = new PortfolioComboResult[grid.Count];
        var doneCount = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(Enumerable.Range(0, grid.Count), pOpts, async (i, ct) =>
        {
            results[i] = await EvaluateAsync(grid[i], symbols, navTf, from, to, baseSettings, data, symbolInfo, ct);
            var d = Interlocked.Increment(ref doneCount);
            if (d % 10 == 0 || d == grid.Count) Console.WriteLine($"  [{d}/{grid.Count}]");
        });

        // --- Ranking nach Σ PnL absteigend ---
        var ranked = results.OrderByDescending(r => r.TotalPnl).ToList();
        var positive = ranked.Where(r => r.TotalPnl > 0m).ToList();
        var best = ranked[0];
        var baselineRank = ranked.IndexOf(ranked.First(r => r.Combo.Equals(Baseline))) + 1;

        WriteReport(outDir, label, scope, from, to, navTf, symbols, balance, baseSettings,
            ranked, baselineResult, positive, best);

        // --- Console-Zusammenfassung: Top-10 ---
        Console.WriteLine($"\n=== TOP 10 nach Σ PnL ({grid.Count} Kombis, Start {balance:F0} USDT) ===");
        Console.WriteLine($"{"#",-3} {"Kombi",-40} {"n",4} {"WR",7} {"PF",6} {"ΣPnL",10} {"%",7} {"MaxDD%",7} {"MaxOpen",7} {"",3}");
        for (int i = 0; i < Math.Min(10, ranked.Count); i++)
        {
            var r = ranked[i];
            var pct = balance > 0 ? r.TotalPnl / balance * 100m : 0m;
            var mark = r.Combo.Equals(Baseline) ? " <Baseline" : "";
            Console.WriteLine($"{i + 1,-3} {r.Combo.Label,-40} {r.Trades,4} {r.WinRate,6:F1}% {FmtPf(r.ProfitFactor),6} " +
                $"{r.TotalPnl,10:F2} {pct,6:F1}% {r.MaxDrawdownPercent,6:F1}% {r.MaxConcurrentOpen,7}{mark}");
        }

        var basePct = balance > 0 ? baselineResult.TotalPnl / balance * 100m : 0m;
        Console.WriteLine($"\nBaseline (Live): {Baseline.Label}");
        Console.WriteLine($"  ΣPnL {baselineResult.TotalPnl:F2} USDT ({basePct:F1}%) | Rang {baselineRank}/{grid.Count} | PF {FmtPf(baselineResult.ProfitFactor)} | n={baselineResult.Trades}");

        var bestPct = balance > 0 ? best.TotalPnl / balance * 100m : 0m;
        Console.WriteLine($"\n=== AUSWERTUNG ===");
        Console.WriteLine($"Beste Kombi    : {best.Combo.Label}");
        Console.WriteLine($"  ΣPnL {best.TotalPnl:F2} USDT ({bestPct:F1}%) | PF {FmtPf(best.ProfitFactor)} | n={best.Trades} | MaxDD {best.MaxDrawdownPercent:F1}%");
        Console.WriteLine($"Schlaegt Baseline: {(best.TotalPnl > baselineResult.TotalPnl ? "JA" : "NEIN")} (Δ {best.TotalPnl - baselineResult.TotalPnl:+0.00;-0.00} USDT)");
        if (positive.Count > 0)
            Console.WriteLine($"Positiv (ΣPnL>0): JA — {positive.Count}/{grid.Count} Kombis. Beste positive: {positive[0].Combo.Label} mit {positive[0].TotalPnl:F2} USDT");
        else
            Console.WriteLine($"Positiv (ΣPnL>0): NEIN — KEINE der {grid.Count} Kombis dreht das Konto ins Plus (beste = {best.TotalPnl:F2} USDT).");
        return 0;
    }

    // ========================================================================
    //  Report (Markdown + JSON)
    // ========================================================================
    private static void WriteReport(
        string outDir, string? label, string scope, DateTime from, DateTime to, TimeFrame navTf,
        IReadOnlyList<string> symbols, decimal balance, BotSettings settings,
        List<PortfolioComboResult> ranked, PortfolioComboResult baseline,
        List<PortfolioComboResult> positive, PortfolioComboResult best)
    {
        var stamp = label ?? "portfolio-sweep";
        Directory.CreateDirectory(outDir);

        var basePct = balance > 0 ? baseline.TotalPnl / balance * 100m : 0m;
        var bestPct = balance > 0 ? best.TotalPnl / balance * 100m : 0m;
        var baselineRank = ranked.IndexOf(ranked.First(r => r.Combo.Equals(Baseline))) + 1;

        var sb = new StringBuilder();
        sb.AppendLine($"# Portfolio-Sweep — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"**1 gemeinsames Konto** ({balance:F0} USDT) ueber {symbols.Count} Symbole, alle konto-weiten Gates aktiv.");
        sb.AppendLine($"Donchian/EMA/ADX FIX auf Live (10/34/18) — variiert: SL / BE / TP-RRR / TP1-Split.");
        sb.AppendLine();
        sb.AppendLine($"- Grid: **{ranked.Count} Kombis** (scope={scope})");
        sb.AppendLine($"- Zeitraum: {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} | Nav-TF {navTf}");
        sb.AppendLine($"- Live-Spiegel: Scanner-Vorfilter={(settings.Backtest.EnableScannerPrefilter ? "AN" : "aus")}, BTC-Health-Scale={(settings.Backtest.EnableBtcHealthScale ? "AN" : "aus")}");
        sb.AppendLine($"- Gates: MaxOpenPositions={settings.Risk.MaxOpenPositions}, MaxTotalMargin={settings.Risk.MaxTotalMarginPercent}%, MaxCorrelated={settings.Risk.MaxCorrelatedExposurePercent}%");
        sb.AppendLine();

        sb.AppendLine("## Auswertung");
        sb.AppendLine($"- **Baseline (Live)** `{Baseline.Label}`: ΣPnL **{baseline.TotalPnl:F2} USDT** ({basePct:F1}%), PF {FmtPf(baseline.ProfitFactor)}, n={baseline.Trades} — Rang **{baselineRank}/{ranked.Count}**.");
        sb.AppendLine($"- **Beste Kombi** `{best.Combo.Label}`: ΣPnL **{best.TotalPnl:F2} USDT** ({bestPct:F1}%), PF {FmtPf(best.ProfitFactor)}, n={best.Trades}, MaxDD {best.MaxDrawdownPercent:F1}%.");
        sb.AppendLine($"- **Schlaegt die beste Kombi die Baseline?** {(best.TotalPnl > baseline.TotalPnl ? "JA" : "NEIN")} (Δ {best.TotalPnl - baseline.TotalPnl:+0.00;-0.00} USDT).");
        if (positive.Count > 0)
            sb.AppendLine($"- **Dreht IRGENDEINE Kombi ins Plus (ΣPnL > 0)?** JA — **{positive.Count}/{ranked.Count}** Kombis sind positiv. Beste positive: `{positive[0].Combo.Label}` mit {positive[0].TotalPnl:F2} USDT.");
        else
            sb.AppendLine($"- **Dreht IRGENDEINE Kombi ins Plus (ΣPnL > 0)?** NEIN — KEINE der {ranked.Count} Kombis ist positiv (beste = {best.TotalPnl:F2} USDT). Das live-getreue Portfolio bleibt ueber JEDE Parameter-Kombination negativ.");
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

        var mdPath = Path.Combine(outDir, $"portfolio-sweep-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());

        var jsonPath = Path.Combine(outDir, $"portfolio-sweep-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            scope, from, to, navTf = navTf.ToString(), symbolCount = symbols.Count, startBalance = balance,
            scannerFilter = settings.Backtest.EnableScannerPrefilter,
            btcHealth = settings.Backtest.EnableBtcHealthScale,
            gridSize = ranked.Count,
            baseline = new { combo = Baseline, result = baseline, rank = baselineRank, pct = basePct },
            best = new { result = best, pct = bestPct, beatsBaseline = best.TotalPnl > baseline.TotalPnl },
            positiveCount = positive.Count,
            anyPositive = positive.Count > 0,
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

    private static string Fmt(PortfolioComboResult r) =>
        $"n={r.Trades,4} WR={r.WinRate,5:F1}% PF={FmtPf(r.ProfitFactor),5} ΣPnL={r.TotalPnl,9:F2} MaxDD={r.MaxDrawdownPercent,5:F1}% MaxOpen={r.MaxConcurrentOpen}";

    private static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);
}
