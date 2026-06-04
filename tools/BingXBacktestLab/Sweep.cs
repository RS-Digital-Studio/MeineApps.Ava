using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BingXBot.Backtest;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

// ============================================================================
//  Parameter-Sweep mit Walk-Forward-Validierung (Anchored Train/Test-Split).
//
//  Spannt ein Grid ueber die TrendFollow-Stellschrauben (Entry/SL/TP) + Break-Even +
//  TP1-Split auf, optimiert jede Kombi auf dem Train-Zeitraum, validiert die Besten
//  auf einem ungesehenen OOS-Test-Zeitraum. Der Train->Test-Abfall (Degradation) ist
//  das Overfitting-Mass: eine Kombi, die nur im Train glaenzt, faellt im Test durch.
//
//  WICHTIG (Backtest-Engine-Eigenheit): Der TP1-Teilschliessungs-Anteil kommt im
//  Backtest aus BacktestSettings.Tp1CloseRatio, NICHT aus RiskSettings — der Sweep
//  dreht daher den richtigen Knopf (siehe ApplyCombo).
// ============================================================================

/// <summary>Eine Parameter-Kombination im Sweep-Grid.</summary>
internal readonly record struct ParamCombo(
    int Donchian, int Ema, decimal AdxMin, decimal AtrSl, decimal Rrr1, decimal Rrr2,
    decimal BeTrigger, decimal Tp1Split)
{
    public string Label => $"Don{Donchian}/EMA{Ema}/ADX{AdxMin:0}/SL{AtrSl:0.00}/RRR{Rrr1:0.0}-{Rrr2:0.0}/BE{BeTrigger:0.0}/TP1×{Tp1Split:0.00}";
}

/// <summary>Aggregierte Kennzahlen einer Kombi ueber alle Symbole/TFs einer Phase (Train ODER Test).</summary>
internal sealed record PhaseResult(
    int Trades, decimal WinRate, decimal ProfitFactor, decimal Expectancy, decimal TotalPnl,
    int LongTrades, decimal LongWinRate, decimal LongPnl,
    int ShortTrades, decimal ShortWinRate, decimal ShortPnl)
{
    public static readonly PhaseResult Empty = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public decimal Score(string rankKey) => rankKey switch
    {
        "pf" => ProfitFactor,
        "totalpnl" => TotalPnl,
        _ => Expectancy, // Default: Expectancy/Trade — durch risiko-basiertes Sizing size-normalisiert.
    };
}

/// <summary>Train- + Test-Ergebnis einer Kombi nebeneinander.</summary>
internal sealed record ComboResult(ParamCombo Combo, PhaseResult Train, PhaseResult Test)
{
    /// <summary>Relativer Train->Test-Abfall des Rank-Scores in % (positiv = Test schlechter = Overfitting-Verdacht).</summary>
    public decimal Degradation(string rankKey)
    {
        var tr = Train.Score(rankKey);
        var te = Test.Score(rankKey);
        if (tr == 0m) return 0m;
        return (tr - te) / Math.Abs(tr) * 100m;
    }

    /// <summary>Robust = in beiden Phasen profitabel.</summary>
    public bool ProfitableBoth => Train.TotalPnl > 0m && Test.TotalPnl > 0m;
}

internal static class Sweep
{
    /// <summary>Live-Default (TrendFollow-Fast) als Vergleichs-Baseline — spiegelt exakt
    /// <c>StrategyFactory.Create("TrendFollow-Fast")</c> + RiskSettings-Defaults:
    /// Don10/EMA34/ADX18/SL×2.75/RRR1.5-3.0/BE2.0/TP1-Split50%. SL ist 2.75 (nicht 2.5) —
    /// das ist der live deployte Wert (StrategyFactory), den der --full-Sweep als robustes
    /// SL-Optimum bestaetigt hat.</summary>
    public static readonly ParamCombo Baseline = new(
        Donchian: 10, Ema: 34, AdxMin: 18m, AtrSl: 2.75m, Rrr1: 1.5m, Rrr2: 3.0m,
        BeTrigger: 2.0m, Tp1Split: 0.5m);

    public static List<ParamCombo> BuildGrid(string scope)
    {
        int[] donchian; int[] ema; decimal[] adxMin; decimal[] atrSl;
        (decimal R1, decimal R2)[] rrr; decimal[] beTrigger; decimal[] tp1Split;

        if (scope == "sl-fine")
        {
            // SL-Nachsweep: feine SL-Aufloesung um die im extended-Lauf bestaetigten Sieger-Achsen
            // (Don8-10/EMA34-50/ADX18/RRR1.5-3.0/BE2.0). Klaert, ob das SL-Optimum bei 3.0 liegt
            // oder hoeher (3.0 war der Grid-Rand des extended-Laufs).
            donchian = [8, 10];
            ema = [34, 50];
            adxMin = [18m];
            atrSl = [2.75m, 3.0m, 3.25m, 3.5m, 4.0m];
            rrr = [(1.5m, 3.0m)];
            beTrigger = [2.0m];
            tp1Split = [0.5m];
        }
        else
        {
            donchian = [8, 10, 12, 15];
            ema = [21, 34, 50];
            adxMin = [15m, 18m, 22m];
            atrSl = [2.0m, 2.5m, 3.0m];
            rrr = [(1.5m, 3.0m), (2.0m, 4.0m)];
            // focused: BE + TP1-Split fix auf dem Live-Stand. extended: beide als zusaetzliche Achsen.
            beTrigger = scope == "focused" ? [2.0m] : [1.5m, 2.0m, 2.5m];
            tp1Split = scope == "focused" ? [0.5m] : [0.5m, 0.7m];
        }

        var grid = new List<ParamCombo>();
        foreach (var d in donchian)
        foreach (var e in ema)
        foreach (var a in adxMin)
        foreach (var s in atrSl)
        foreach (var (r1, r2) in rrr)
        foreach (var be in beTrigger)
        foreach (var sp in tp1Split)
            grid.Add(new ParamCombo(d, e, a, s, r1, r2, be, sp));
        return grid;
    }

    /// <summary>Erzeugt die TrendFollow-Strategie mit den Grid-Parametern (Konstruktor-Parameter, nicht via Factory-Name).</summary>
    private static IStrategy MakeStrategy(ParamCombo c) => new TrendFollowStrategy(
        donchianPeriod: c.Donchian, emaPeriod: c.Ema, atrPeriod: 14, adxPeriod: 14,
        adxMin: c.AdxMin, atrSlMultiplier: c.AtrSl, tp1Rrr: c.Rrr1, tp2Rrr: c.Rrr2);

    /// <summary>Klont die Basis-Settings und ueberschreibt BE-Trigger (Risk) + TP1/TP2-Split (Backtest!).</summary>
    private static BotSettings ApplyCombo(BotSettings baseSettings, ParamCombo c)
    {
        var clone = JsonSerializer.Deserialize<BotSettings>(
            JsonSerializer.Serialize(baseSettings, JsonOpts), JsonOpts)!;
        clone.Risk.BreakevenTriggerRMultiple = c.BeTrigger;
        clone.Backtest.Tp1CloseRatio = c.Tp1Split;          // Backtest liest DIESEN Wert (nicht Risk.Tp1CloseRatio).
        clone.Backtest.Tp2CloseRatio = 1m - c.Tp1Split;
        return clone;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Backtestet eine Kombi ueber alle Symbole/TFs in einem Zeitfenster und aggregiert die Trades.</summary>
    private static async Task<PhaseResult> EvaluateAsync(
        ParamCombo combo, IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs,
        DateTime from, DateTime to, IPublicMarketDataClient data, BotSettings baseSettings, CancellationToken ct)
    {
        var settings = ApplyCombo(baseSettings, combo);
        var pnls = new List<decimal>();
        var isLong = new List<bool>();

        foreach (var tf in tfs)
        foreach (var symbol in symbols)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var engine = new BacktestEngine(data, NullLogger<BacktestEngine>.Instance);
                var risk = new RiskManager(settings.Risk, NullLogger<RiskManager>.Instance);
                var report = await engine.RunAsync(
                    MakeStrategy(combo), risk, symbol, tf, from, to, settings.Backtest,
                    scannerSettings: settings.Scanner, riskSettings: settings.Risk, ct: ct).ConfigureAwait(false);

                foreach (var t in report.Trades)
                {
                    pnls.Add(t.Pnl);
                    isLong.Add(t.Side == Side.Buy);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* einzelnes Symbol kann fehlen (nicht gelistet im Fenster) — ueberspringen */ }
        }

        return Aggregate(pnls, isLong);
    }

    private static PhaseResult Aggregate(List<decimal> pnls, List<bool> isLong)
    {
        int n = pnls.Count;
        if (n == 0) return PhaseResult.Empty;

        int wins = pnls.Count(p => p > 0);
        decimal grossWin = pnls.Where(p => p > 0).Sum();
        decimal grossLoss = Math.Abs(pnls.Where(p => p < 0).Sum());
        decimal pf = grossLoss > 0 ? grossWin / grossLoss : (grossWin > 0 ? 999m : 0m);
        decimal total = pnls.Sum();

        decimal longPnl = 0m, shortPnl = 0m;
        int longN = 0, shortN = 0, longWins = 0, shortWins = 0;
        for (int i = 0; i < n; i++)
        {
            if (isLong[i]) { longN++; longPnl += pnls[i]; if (pnls[i] > 0) longWins++; }
            else { shortN++; shortPnl += pnls[i]; if (pnls[i] > 0) shortWins++; }
        }

        return new PhaseResult(
            Trades: n,
            WinRate: 100m * wins / n,
            ProfitFactor: pf,
            Expectancy: total / n,
            TotalPnl: total,
            LongTrades: longN, LongWinRate: longN > 0 ? 100m * longWins / longN : 0m, LongPnl: longPnl,
            ShortTrades: shortN, ShortWinRate: shortN > 0 ? 100m * shortWins / shortN : 0m, ShortPnl: shortPnl);
    }

    // ========================================================================
    //  Haupt-Einstiegspunkt
    // ========================================================================
    public static async Task<int> RunAsync(
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data,
        string scope, decimal trainSplit, int topN, int minTrades, string rankKey, int parallelism,
        string outDir, string? label)
    {
        var grid = BuildGrid(scope);
        var splitDate = from + TimeSpan.FromTicks((long)((to - from).Ticks * (double)trainSplit));

        Console.WriteLine("=== Parameter-Sweep (Walk-Forward) ===");
        Console.WriteLine($"Grid       : {grid.Count} Kombis (scope={scope})");
        Console.WriteLine($"Train      : {from:yyyy-MM-dd} .. {splitDate:yyyy-MM-dd}");
        Console.WriteLine($"Test (OOS) : {splitDate:yyyy-MM-dd} .. {to:yyyy-MM-dd}");
        Console.WriteLine($"Symbole    : {symbols.Count} | TFs: {string.Join(",", tfs)}");
        Console.WriteLine($"Rank-Key   : {rankKey} | Min-Trades-Gate: {minTrades} | Top-N: {topN} | Parallel: {parallelism}");
        Console.WriteLine();

        // --- Preload + Baseline: sequenziell, warmt den RAM-Cache und misst den Live-Stand ---
        Console.WriteLine("Preload + Baseline (sequenziell, warmt Cache)...");
        var baseTrain = await EvaluateAsync(Baseline, symbols, tfs, from, splitDate, data, baseSettings, default);
        var baseTest = await EvaluateAsync(Baseline, symbols, tfs, splitDate, to, data, baseSettings, default);
        Console.WriteLine($"  Baseline Train: {Fmt(baseTrain)}");
        Console.WriteLine($"  Baseline Test : {Fmt(baseTest)}");
        Console.WriteLine();

        // --- TRAIN: gesamtes Grid parallel auf dem Train-Fenster ---
        Console.WriteLine($"Train-Sweep ueber {grid.Count} Kombis...");
        var trainResults = new PhaseResult[grid.Count];
        var doneCount = 0;
        var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
        await Parallel.ForEachAsync(Enumerable.Range(0, grid.Count), pOpts, async (i, ct) =>
        {
            trainResults[i] = await EvaluateAsync(grid[i], symbols, tfs, from, splitDate, data, baseSettings, ct);
            var d = Interlocked.Increment(ref doneCount);
            if (d % 50 == 0 || d == grid.Count) Console.WriteLine($"  [{d}/{grid.Count}]");
        });

        // Gate (genug Trades) + Ranking nach Train-Score.
        var ranked = Enumerable.Range(0, grid.Count)
            .Select(i => (Combo: grid[i], Train: trainResults[i]))
            .Where(x => x.Train.Trades >= minTrades)
            .OrderByDescending(x => x.Train.Score(rankKey))
            .Take(topN)
            .ToList();

        Console.WriteLine($"\n{ranked.Count} Kombis ueber dem Trade-Gate → OOS-Test der Top {ranked.Count}...");

        // --- TEST: nur die Train-Sieger auf dem ungesehenen OOS-Fenster ---
        var combos = new List<ComboResult>(ranked.Count);
        var testResults = new PhaseResult[ranked.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, ranked.Count), pOpts, async (i, ct) =>
        {
            testResults[i] = await EvaluateAsync(ranked[i].Combo, symbols, tfs, splitDate, to, data, baseSettings, ct);
        });
        for (int i = 0; i < ranked.Count; i++)
            combos.Add(new ComboResult(ranked[i].Combo, ranked[i].Train, testResults[i]));

        // Final-Sortierung nach Robustheit = Worst-of-both (min Train/Test-Score). Reines OOS-Ranking
        // belohnt "Test-Glueck" (negative Degradation, Test ≫ Train) genauso wie Overfitting Train ≫ Test
        // bestraft wird — beides ist Instabilitaet. Wer in BEIDEN Phasen gut ist, ist live verlaesslich.
        combos = combos.OrderByDescending(c => Math.Min(c.Train.Score(rankKey), c.Test.Score(rankKey))).ToList();

        // Empfehlung: robusteste in-beiden-profitable Kombi (Fallback: robusteste ueberhaupt).
        var recommendation = combos.FirstOrDefault(c => c.ProfitableBoth) ?? combos.FirstOrDefault();

        WriteReport(outDir, label, scope, from, splitDate, to, symbols, tfs, rankKey, minTrades, grid.Count,
            baseTrain, baseTest, combos, recommendation);

        // --- Console-Zusammenfassung ---
        Console.WriteLine($"\n=== TOP 10 nach Robustheit (min Train/Test-{rankKey}) ===");
        Console.WriteLine($"{"#",-3} {"Kombi",-52} {"Train-Exp",10} {"Test-Exp",10} {"Degr%",7} {"TestPF",7} {"TestWR",7} {"TestΣ",10} {"",3}");
        for (int i = 0; i < Math.Min(10, combos.Count); i++)
        {
            var c = combos[i];
            Console.WriteLine($"{i + 1,-3} {c.Combo.Label,-52} {c.Train.Expectancy,10:F3} {c.Test.Expectancy,10:F3} " +
                $"{c.Degradation(rankKey),7:F0} {FmtPf(c.Test.ProfitFactor),7} {c.Test.WinRate,6:F1}% {c.Test.TotalPnl,10:F1} {(c.ProfitableBoth ? " ⭐" : "")}");
        }

        Console.WriteLine($"\nBaseline (Live)  Test-Exp={baseTest.Expectancy:F3} Test-PF={FmtPf(baseTest.ProfitFactor)} Test-ΣPnL={baseTest.TotalPnl:F1}");
        if (recommendation is not null)
        {
            Console.WriteLine($"\n⭐ EMPFEHLUNG: {recommendation.Combo.Label}");
            Console.WriteLine($"   Test-Exp={recommendation.Test.Expectancy:F3} (Baseline {baseTest.Expectancy:F3}) " +
                $"| Test-PF={FmtPf(recommendation.Test.ProfitFactor)} | Test-ΣPnL={recommendation.Test.TotalPnl:F1} " +
                $"| Degradation={recommendation.Degradation(rankKey):F0}% | profitabel-beide={recommendation.ProfitableBoth}");
        }
        return 0;
    }

    // ========================================================================
    //  Durchgehender Voll-Zeitraum-Vergleich (--full): mehrere SL-Werte (sonst Live-Default)
    //  ueber den GANZEN Zeitraum als EINEN Backtest. 2 Jahre decken alle Phasen ab → die robuste
    //  Config ist die, die ueber den kompletten Zyklus (inkl. der schlechten Phasen) am besten faehrt.
    // ========================================================================
    public static async Task<int> FullAsync(
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data,
        decimal[] slValues, int parallelism, string outDir, string? label)
    {
        var combos = slValues.Select(sl => Baseline with { AtrSl = sl }).ToList();

        Console.WriteLine("=== Durchgehender Voll-Zeitraum-Vergleich (alle Phasen) ===");
        Console.WriteLine($"SL-Werte   : {string.Join(", ", slValues)} (sonst Live-Default Don10/EMA34/ADX18/RRR1.5-3.0/BE2.0/TP1×0.5)");
        Console.WriteLine($"Zeitraum   : {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} (durchgehend, kein Split)");
        Console.WriteLine($"Symbole    : {symbols.Count} | TFs: {string.Join(",", tfs)}");
        Console.WriteLine();

        var res = new PhaseResult[combos.Count];
        Console.WriteLine($"SL {slValues[0]} sequenziell (warmt Cache)...");
        res[0] = await EvaluateAsync(combos[0], symbols, tfs, from, to, data, baseSettings, default);
        if (combos.Count > 1)
        {
            Console.WriteLine($"Restliche {combos.Count - 1} SL-Werte parallel...");
            var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
            await Parallel.ForEachAsync(Enumerable.Range(1, combos.Count - 1), pOpts, async (ci, ct) =>
            {
                res[ci] = await EvaluateAsync(combos[ci], symbols, tfs, from, to, data, baseSettings, ct);
            });
        }

        // Report
        var stamp = label ?? "full";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Durchgehender Voll-Zeitraum-Vergleich — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"- SL-Werte: **{string.Join(", ", slValues)}** (sonst Live-Default: Don10/EMA34/ADX18/RRR1.5-3.0/BE2.0/TP1×0.5)");
        sb.AppendLine($"- Zeitraum: {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} (durchgehend — alle Marktphasen in einem Fenster)");
        sb.AppendLine($"- Symbole: {symbols.Count} | TFs: {string.Join("/", tfs)}");
        sb.AppendLine();
        sb.AppendLine("| SL | n | WinRate | PF | Exp/Trade | ΣPnL | Long (n@WR/PnL) | Short (n@WR/PnL) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        for (int ci = 0; ci < combos.Count; ci++)
        {
            var r = res[ci];
            var marker = slValues[ci] == Baseline.AtrSl ? " *(Baseline)*" : "";
            sb.AppendLine($"| **{slValues[ci]:0.00}**{marker} | {r.Trades} | {r.WinRate:F1}% | {FmtPf(r.ProfitFactor)} | {r.Expectancy:F3} | {r.TotalPnl:F1} | " +
                $"{r.LongTrades}@{r.LongWinRate:F0}%/{r.LongPnl:F0} | {r.ShortTrades}@{r.ShortWinRate:F0}%/{r.ShortPnl:F0} |");
        }
        var mdPath = Path.Combine(outDir, $"full-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());
        var jsonPath = Path.Combine(outDir, $"full-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            from, to, slValues, results = slValues.Select((sl, ci) => new { sl, result = res[ci] })
        }, new JsonSerializerOptions { WriteIndented = true }));

        // Console-Tabelle
        Console.WriteLine($"\n{"SL",6} {"n",5} {"WR",7} {"PF",6} {"Exp",8} {"ΣPnL",9} {"Long",18} {"Short",18}");
        for (int ci = 0; ci < combos.Count; ci++)
        {
            var r = res[ci];
            Console.WriteLine($"{slValues[ci],6:0.00} {r.Trades,5} {r.WinRate,6:F1}% {FmtPf(r.ProfitFactor),6} {r.Expectancy,8:F3} {r.TotalPnl,9:F1} " +
                $"  {r.LongTrades,3}@{r.LongWinRate,3:F0}%/{r.LongPnl,7:F0}  {r.ShortTrades,3}@{r.ShortWinRate,3:F0}%/{r.ShortPnl,7:F0}");
        }
        Console.WriteLine($"\nReport: {mdPath}");
        return 0;
    }

    // ========================================================================
    //  Isolierter Ein-Achsen-Sweep (--axis): OFAT (One-Factor-At-A-Time). Variiert EINE
    //  Stellschraube (SL / BE / TP-RRR / TP1-Split) ueber den ganzen Zeitraum, alle anderen
    //  Achsen bleiben auf dem Live-Stand (Baseline). Ehrlichste Entscheidungsbasis pro Achse —
    //  zeigt den reinen Effekt einer Stellschraube ohne Achsen-Kopplung (im Gegensatz zum
    //  vollen --sweep-Grid, dessen Sieger eine bestimmte Kombination ist).
    // ========================================================================
    public static async Task<int> AxisAsync(
        string axisTitle, IReadOnlyList<(string Label, ParamCombo Combo)> variants,
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data,
        int parallelism, string outDir, string? label)
    {
        Console.WriteLine($"=== Isolierter Achsen-Sweep (OFAT): {axisTitle} ===");
        Console.WriteLine($"Varianten  : {string.Join(", ", variants.Select(v => v.Label))}");
        Console.WriteLine($"Baseline   : {Baseline.Label}");
        Console.WriteLine($"Zeitraum   : {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} (durchgehend, kein Split)");
        Console.WriteLine($"Symbole    : {symbols.Count} | TFs: {string.Join(",", tfs)}");
        Console.WriteLine();

        var res = new PhaseResult[variants.Count];
        Console.WriteLine($"Variante 1/{variants.Count} ({variants[0].Label}) sequenziell (warmt Cache)...");
        res[0] = await EvaluateAsync(variants[0].Combo, symbols, tfs, from, to, data, baseSettings, default);
        if (variants.Count > 1)
        {
            Console.WriteLine($"Restliche {variants.Count - 1} Varianten parallel...");
            var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
            await Parallel.ForEachAsync(Enumerable.Range(1, variants.Count - 1), pOpts, async (vi, ct) =>
            {
                res[vi] = await EvaluateAsync(variants[vi].Combo, symbols, tfs, from, to, data, baseSettings, ct);
            });
        }

        // Report
        var stamp = label ?? "axis";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Isolierter Achsen-Sweep — {axisTitle} — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"- Variierte Achse: **{axisTitle}** (alle anderen Achsen = Live-Baseline)");
        sb.AppendLine($"- Baseline (Live): {Baseline.Label}");
        sb.AppendLine($"- Zeitraum: {from:yyyy-MM-dd} .. {to:yyyy-MM-dd} (durchgehend — alle Marktphasen in einem Fenster)");
        sb.AppendLine($"- Symbole: {symbols.Count} | TFs: {string.Join("/", tfs)}");
        sb.AppendLine();
        sb.AppendLine("| Variante | n | WinRate | PF | Exp/Trade | ΣPnL | Long (n@WR/PnL) | Short (n@WR/PnL) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        for (int vi = 0; vi < variants.Count; vi++)
        {
            var r = res[vi];
            var marker = variants[vi].Combo.Equals(Baseline) ? " *(Baseline/Live)*" : "";
            sb.AppendLine($"| **{variants[vi].Label}**{marker} | {r.Trades} | {r.WinRate:F1}% | {FmtPf(r.ProfitFactor)} | {r.Expectancy:F3} | {r.TotalPnl:F1} | " +
                $"{r.LongTrades}@{r.LongWinRate:F0}%/{r.LongPnl:F0} | {r.ShortTrades}@{r.ShortWinRate:F0}%/{r.ShortPnl:F0} |");
        }
        var mdPath = Path.Combine(outDir, $"axis-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());
        var jsonPath = Path.Combine(outDir, $"axis-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            axisTitle, from, to, baseline = Baseline.Label,
            results = variants.Select((v, vi) => new { v.Label, isBaseline = v.Combo.Equals(Baseline), v.Combo, result = res[vi] })
        }, new JsonSerializerOptions { WriteIndented = true }));

        // Console-Tabelle
        Console.WriteLine($"\n{"Variante",-14} {"n",5} {"WR",7} {"PF",6} {"Exp",8} {"ΣPnL",9} {"Long",18} {"Short",18}");
        for (int vi = 0; vi < variants.Count; vi++)
        {
            var r = res[vi];
            var marker = variants[vi].Combo.Equals(Baseline) ? " <Baseline" : "";
            Console.WriteLine($"{variants[vi].Label,-14} {r.Trades,5} {r.WinRate,6:F1}% {FmtPf(r.ProfitFactor),6} {r.Expectancy,8:F3} {r.TotalPnl,9:F1} " +
                $"  {r.LongTrades,3}@{r.LongWinRate,3:F0}%/{r.LongPnl,7:F0}  {r.ShortTrades,3}@{r.ShortWinRate,3:F0}%/{r.ShortPnl,7:F0}{marker}");
        }
        Console.WriteLine($"\nReport: {mdPath}");
        return 0;
    }

    // ========================================================================
    //  Rollierender Walk-Forward-Vergleich (--compare): mehrere SL-Werte (sonst Live-Default)
    //  ueber ueberlappende Fenster. Trennt echten Edge von Einzelfenster-Glueck.
    // ========================================================================
    public static async Task<int> CompareAsync(
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, DateTime from, DateTime to,
        BotSettings baseSettings, IPublicMarketDataClient data,
        decimal[] slValues, int windowDays, int stepDays, int parallelism, string outDir, string? label)
    {
        var windows = WalkForwardRunner.GenerateWindows(from, to,
            TimeSpan.FromDays(windowDays), TimeSpan.FromDays(stepDays));
        var combos = slValues.Select(sl => Baseline with { AtrSl = sl }).ToList();

        Console.WriteLine("=== Rollierender Walk-Forward-Vergleich ===");
        Console.WriteLine($"SL-Werte   : {string.Join(", ", slValues)} (sonst Live-Default Don10/EMA34/ADX18/RRR1.5-3.0/BE2.0/TP1×0.5)");
        Console.WriteLine($"Fenster    : {windowDays}d, Schritt {stepDays}d → {windows.Count} Fenster ({from:yyyy-MM-dd} .. {to:yyyy-MM-dd})");
        Console.WriteLine($"Symbole    : {symbols.Count} | TFs: {string.Join(",", tfs)} | Parallel: {parallelism}");
        Console.WriteLine();
        if (windows.Count < 2)
        {
            Console.WriteLine("FEHLER: < 2 Fenster — Zeitraum erweitern oder window/step verkleinern.");
            return 1;
        }

        // results[combo][window]. Config 0 sequenziell (warmt den Disk/RAM-Cache, kein Cold-Race),
        // restliche Configs danach parallel (lesen dieselben Klines aus dem RAM).
        var results = new PhaseResult[combos.Count][];
        for (int ci = 0; ci < combos.Count; ci++) results[ci] = new PhaseResult[windows.Count];

        Console.WriteLine($"SL {slValues[0]} sequenziell (warmt Cache)...");
        for (int wi = 0; wi < windows.Count; wi++)
            results[0][wi] = await EvaluateAsync(combos[0], symbols, tfs, windows[wi].From, windows[wi].To, data, baseSettings, default);

        if (combos.Count > 1)
        {
            Console.WriteLine($"Restliche {combos.Count - 1} SL-Werte parallel...");
            var jobs = (from ci in Enumerable.Range(1, combos.Count - 1)
                        from wi in Enumerable.Range(0, windows.Count)
                        select (ci, wi)).ToList();
            var pOpts = new ParallelOptions { MaxDegreeOfParallelism = parallelism };
            await Parallel.ForEachAsync(jobs, pOpts, async (job, ct) =>
            {
                results[job.ci][job.wi] = await EvaluateAsync(
                    combos[job.ci], symbols, tfs, windows[job.wi].From, windows[job.wi].To, data, baseSettings, ct);
            });
        }

        // --- Aggregat pro SL ueber die Fenster (Fenster = Stichproben; KEINE Summe, da ueberlappend) ---
        var agg = new List<(decimal Sl, int Profitable, decimal AvgExp, decimal MinExp, decimal StdExp, int Wins)>();
        for (int ci = 0; ci < combos.Count; ci++)
        {
            var exps = results[ci].Select(r => r.Expectancy).ToList();
            int profitable = results[ci].Count(r => r.TotalPnl > 0m);
            decimal avg = exps.Average();
            decimal min = exps.Min();
            decimal variance = exps.Sum(e => (e - avg) * (e - avg)) / exps.Count;
            decimal std = (decimal)Math.Sqrt((double)variance);
            // "Wins" = in wie vielen Fenstern dieser SL die hoechste Expectancy aller SL-Werte hatte.
            int wins = Enumerable.Range(0, windows.Count)
                .Count(wi => results[ci][wi].Expectancy == Enumerable.Range(0, combos.Count).Max(cj => results[cj][wi].Expectancy));
            agg.Add((slValues[ci], profitable, avg, min, std, wins));
        }

        WriteCompareReport(outDir, label, windowDays, stepDays, from, to, symbols, tfs, slValues, windows, results, agg);

        // --- Console ---
        Console.WriteLine($"\n=== Pro-Fenster Expectancy/Trade ({windows.Count} Fenster) ===");
        Console.Write($"{"Fenster",-26}");
        foreach (var sl in slValues) Console.Write($" SL{sl,5:0.00}");
        Console.WriteLine();
        for (int wi = 0; wi < windows.Count; wi++)
        {
            Console.Write($"{windows[wi].From:yyyy-MM-dd}..{windows[wi].To:yyyy-MM-dd}");
            for (int ci = 0; ci < combos.Count; ci++) Console.Write($" {results[ci][wi].Expectancy,7:F2}");
            Console.WriteLine();
        }

        Console.WriteLine($"\n=== Robustheit über {windows.Count} Fenster ===");
        Console.WriteLine($"{"SL",6} {"profitabel",11} {"ØExp",8} {"minExp",8} {"StdExp",8} {"beste",6}");
        foreach (var a in agg)
            Console.WriteLine($"{a.Sl,6:0.00} {a.Profitable + "/" + windows.Count,11} {a.AvgExp,8:F2} {a.MinExp,8:F2} {a.StdExp,8:F2} {a.Wins,4}/{windows.Count}");

        var best = agg.OrderByDescending(a => a.Profitable).ThenByDescending(a => a.AvgExp).First();
        Console.WriteLine($"\n→ Robusteste SL (meiste profitable Fenster, dann ØExp): {best.Sl:0.00} " +
            $"({best.Profitable}/{windows.Count} profitabel, ØExp {best.AvgExp:F2}, minExp {best.MinExp:F2})");
        return 0;
    }

    private static void WriteCompareReport(
        string outDir, string? label, int windowDays, int stepDays, DateTime from, DateTime to,
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, decimal[] slValues,
        IReadOnlyList<(DateTime From, DateTime To)> windows, PhaseResult[][] results,
        List<(decimal Sl, int Profitable, decimal AvgExp, decimal MinExp, decimal StdExp, int Wins)> agg)
    {
        var stamp = label ?? "compare";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Rollierender Walk-Forward-Vergleich — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"- SL-Werte: **{string.Join(", ", slValues)}** (sonst Live-Default: Don10/EMA34/ADX18/RRR1.5-3.0/BE2.0/TP1×0.5)");
        sb.AppendLine($"- Fenster: {windowDays}d, Schritt {stepDays}d → **{windows.Count} überlappende Fenster** | {from:yyyy-MM-dd} .. {to:yyyy-MM-dd}");
        sb.AppendLine($"- Symbole: {symbols.Count} | TFs: {string.Join("/", tfs)}");
        sb.AppendLine();

        sb.AppendLine("## Robustheit über alle Fenster (Fenster = unabhängige Stichproben)");
        sb.AppendLine("| SL | Profitable Fenster | Ø Exp/Trade | Min Exp (worst window) | StdDev Exp (Konsistenz↓) | Beste in n Fenstern |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var a in agg)
            sb.AppendLine($"| {a.Sl:0.00} | {a.Profitable}/{windows.Count} | {a.AvgExp:F2} | {a.MinExp:F2} | {a.StdExp:F2} | {a.Wins}/{windows.Count} |");
        sb.AppendLine();

        sb.AppendLine("## Pro-Fenster Expectancy/Trade");
        sb.Append("| Fenster |");
        foreach (var sl in slValues) sb.Append($" SL {sl:0.00} |");
        sb.AppendLine();
        sb.Append("|---|");
        foreach (var _ in slValues) sb.Append("---|");
        sb.AppendLine();
        for (int wi = 0; wi < windows.Count; wi++)
        {
            sb.Append($"| {windows[wi].From:yyyy-MM-dd}..{windows[wi].To:yyyy-MM-dd} |");
            for (int ci = 0; ci < slValues.Length; ci++) sb.Append($" {results[ci][wi].Expectancy:F2} ({results[ci][wi].Trades}) |");
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("> Zahl in Klammern = Trades im Fenster. Eine SL ist robust, wenn sie in *vielen* Fenstern profitabel ist " +
            "und ihre Expectancy *konsistent* (niedrige StdDev) bleibt — nicht, wenn ein einzelnes Fenster den Schnitt rettet.");

        var mdPath = Path.Combine(outDir, $"compare-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());
        var jsonPath = Path.Combine(outDir, $"compare-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            slValues, windowDays, stepDays, from, to, windowCount = windows.Count,
            aggregate = agg.Select(a => new { sl = a.Sl, profitableWindows = a.Profitable, avgExp = a.AvgExp, minExp = a.MinExp, stdExp = a.StdExp, bestInWindows = a.Wins }),
            perWindow = windows.Select((w, wi) => new
            {
                from = w.From, to = w.To,
                bySl = slValues.Select((sl, ci) => new { sl, results[ci][wi].Expectancy, results[ci][wi].Trades, results[ci][wi].ProfitFactor, results[ci][wi].TotalPnl })
            })
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"\nReport: {mdPath}");
        Console.WriteLine($"JSON  : {jsonPath}");
    }

    private static string Fmt(PhaseResult r) =>
        $"n={r.Trades,4} WR={r.WinRate,5:F1}% PF={FmtPf(r.ProfitFactor),5} Exp={r.Expectancy,7:F3} ΣPnL={r.TotalPnl,9:F1} " +
        $"| L {r.LongTrades}@{r.LongWinRate:F0}%/{r.LongPnl:F0} S {r.ShortTrades}@{r.ShortWinRate:F0}%/{r.ShortPnl:F0}";

    private static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);

    private static void WriteReport(
        string outDir, string? label, string scope, DateTime from, DateTime split, DateTime to,
        IReadOnlyList<string> symbols, IReadOnlyList<TimeFrame> tfs, string rankKey, int minTrades, int gridSize,
        PhaseResult baseTrain, PhaseResult baseTest, List<ComboResult> combos, ComboResult? recommendation)
    {
        var stamp = label ?? "sweep";
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine($"# Sweep-Report — {stamp}");
        sb.AppendLine();
        sb.AppendLine($"- Grid: **{gridSize} Kombis** (scope={scope})");
        sb.AppendLine($"- Train: {from:yyyy-MM-dd} .. {split:yyyy-MM-dd} | Test (OOS): {split:yyyy-MM-dd} .. {to:yyyy-MM-dd}");
        sb.AppendLine($"- Symbole: {symbols.Count} | TFs: {string.Join("/", tfs)} | Rank: {rankKey} | Min-Trades-Gate: {minTrades}");
        sb.AppendLine();

        sb.AppendLine("## Baseline (aktueller Live-Stand: TrendFollow-Fast, BE 2.0R, TP1-Split 50%)");
        sb.AppendLine("| Phase | n | WinRate | PF | Exp/Trade | ΣPnL | Long (n@WR/PnL) | Short (n@WR/PnL) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        sb.AppendLine(BaselineRow("Train", baseTrain));
        sb.AppendLine(BaselineRow("Test (OOS)", baseTest));
        sb.AppendLine();

        sb.AppendLine($"## Top {combos.Count} nach Robustheit — min(Train, Test) {rankKey} (⭐ = in beiden Phasen profitabel)");
        sb.AppendLine("| # | Kombi | Train Exp | Train PF | Train ΣPnL | Test Exp | Test PF | Test WR | Test ΣPnL | Degr% | L/S (Test) | |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        for (int i = 0; i < combos.Count; i++)
        {
            var c = combos[i];
            sb.AppendLine($"| {i + 1} | {c.Combo.Label} | {c.Train.Expectancy:F3} | {FmtPf(c.Train.ProfitFactor)} | {c.Train.TotalPnl:F1} | " +
                $"{c.Test.Expectancy:F3} | {FmtPf(c.Test.ProfitFactor)} | {c.Test.WinRate:F1}% | {c.Test.TotalPnl:F1} | {c.Degradation(rankKey):F0} | " +
                $"{c.Test.LongTrades}@{c.Test.LongWinRate:F0}% / {c.Test.ShortTrades}@{c.Test.ShortWinRate:F0}% | {(c.ProfitableBoth ? "⭐" : "")} |");
        }
        sb.AppendLine();

        if (recommendation is not null)
        {
            sb.AppendLine("## Empfehlung");
            sb.AppendLine($"**{recommendation.Combo.Label}**");
            sb.AppendLine();
            sb.AppendLine($"- Test-Exp **{recommendation.Test.Expectancy:F3}** vs Baseline {baseTest.Expectancy:F3} " +
                $"(Δ {recommendation.Test.Expectancy - baseTest.Expectancy:+0.000;-0.000})");
            sb.AppendLine($"- Test-PF {FmtPf(recommendation.Test.ProfitFactor)} | Test-ΣPnL {recommendation.Test.TotalPnl:F1} | " +
                $"Degradation {recommendation.Degradation(rankKey):F0}% | profitabel in beiden Phasen: {recommendation.ProfitableBoth}");
            sb.AppendLine();
            sb.AppendLine("> Overfitting-Hinweis: hohe Degradation (Train ≫ Test) bedeutet, die Kombi ist an den Train-Zeitraum " +
                "angepasst. Eine Kombi mit etwas schwaecherem Train, aber stabilem Test ist live verlaesslicher.");
        }

        var mdPath = Path.Combine(outDir, $"sweep-{stamp}.md");
        File.WriteAllText(mdPath, sb.ToString());

        var jsonPath = Path.Combine(outDir, $"sweep-{stamp}.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            scope, from, split, to, symbols, tfs = tfs.Select(t => t.ToString()), rankKey, minTrades, gridSize,
            baseline = new { train = baseTrain, test = baseTest },
            recommendation = recommendation is null ? null : new { recommendation.Combo, recommendation.Train, recommendation.Test },
            top = combos.Select(c => new { c.Combo, c.Train, c.Test, degradation = c.Degradation(rankKey), c.ProfitableBoth })
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"\nReport: {mdPath}");
        Console.WriteLine($"JSON  : {jsonPath}");
    }

    private static string BaselineRow(string phase, PhaseResult r) =>
        $"| {phase} | {r.Trades} | {r.WinRate:F1}% | {FmtPf(r.ProfitFactor)} | {r.Expectancy:F3} | {r.TotalPnl:F1} | " +
        $"{r.LongTrades}@{r.LongWinRate:F0}%/{r.LongPnl:F0} | {r.ShortTrades}@{r.ShortWinRate:F0}%/{r.ShortPnl:F0} |";
}
