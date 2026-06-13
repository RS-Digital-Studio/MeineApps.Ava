using System.Globalization;
using System.Text;
using System.Text.Json;
using BingXBacktestLab;
using BingXBot.Backtest;
using BingXBot.Backtest.Portfolio;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;

// ============================================================================
//  BingX Backtest-Lab — vergleicht Strategien empirisch auf echten BingX-Daten.
//  Laedt Klines ueber den Public-Client (kein API-Key), cached sie auf Platte,
//  rechnet Strategie x Symbol x TF durch und aggregiert die PerformanceReports
//  zu einer Vergleichstabelle (Console + Markdown + JSON).
// ============================================================================

var argMap = ParseArgs(args);
var projectDir = AppContext.BaseDirectory;
// Projekt-Root (tools/BingXBacktestLab) relativ zur Exe finden — fuer Default-Pfade.
var toolDir = FindToolDir() ?? Directory.GetCurrentDirectory();

var strategies = GetList(argMap, "strategies", "SK-System");
var tfs = GetList(argMap, "tfs", "H4,H1").Select(ParseTf).ToList();
var symbols = ResolveSymbols(GetArg(argMap, "symbols", null), GetArg(argMap, "preset", "may-live"));
var from = DateTime.SpecifyKind(DateTime.Parse(GetArg(argMap, "from", "2025-11-01")!, CultureInfo.InvariantCulture), DateTimeKind.Utc);
var to = DateTime.SpecifyKind(DateTime.Parse(GetArg(argMap, "to", "2026-05-31")!, CultureInfo.InvariantCulture), DateTimeKind.Utc);
var settingsPath = GetArg(argMap, "settings", Path.Combine(toolDir, "live-settings.json"))!;
var cacheDir = GetArg(argMap, "cache", Path.Combine(toolDir, ".kline-cache"))!;
var outDir = GetArg(argMap, "out", Path.Combine(toolDir, "reports"))!;
var label = GetArg(argMap, "label", "run");

Console.WriteLine("=== BingX Backtest-Lab ===");
Console.WriteLine($"Strategien : {string.Join(", ", strategies)}");
Console.WriteLine($"Symbole    : {symbols.Count} ({string.Join(", ", symbols.Take(8))}{(symbols.Count > 8 ? ", ..." : "")})");
Console.WriteLine($"TFs        : {string.Join(", ", tfs)}");
Console.WriteLine($"Zeitraum   : {from:yyyy-MM-dd} bis {to:yyyy-MM-dd}");
Console.WriteLine($"Settings   : {settingsPath}");
Console.WriteLine();

// --- Settings laden (exakte Live-Config, falls vorhanden) ---
BotSettings botSettings;
if (File.Exists(settingsPath))
{
    var json = File.ReadAllText(settingsPath);
    botSettings = JsonSerializer.Deserialize<BotSettings>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    }) ?? new BotSettings();
    Console.WriteLine($"Settings geladen: MinRRR={botSettings.Risk.MinRiskRewardRatio}, ScannerMode={botSettings.Scanner.Mode}");
}
else
{
    botSettings = new BotSettings();
    Console.WriteLine("WARNUNG: keine settings.json gefunden — nutze Defaults.");
}

// Runner-Trailing per Flag aktivieren (A/B-Test: fester TP2 vs ATR-Trailing nach TP1).
if (GetArg(argMap, "enable-runner", null) == "true")
{
    botSettings.Risk.EnableRunner = true;
    Console.WriteLine($"RUNNER-TRAILING aktiv (Rest nach TP1 mit ATR×{botSettings.Risk.RunnerTrailingAtrMultiplier}-Trailing statt festem TP2)");
}
Console.WriteLine();

// --- Clients aufbauen ---
using var http = new HttpClient();
var rateLimiter = new SimpleRateLimiter(TimeSpan.FromMilliseconds(120));
var realClient = new BingXPublicClient(http, rateLimiter, NullLogger<BingXPublicClient>.Instance);
var dataClient = new CachingPublicClient(realClient, cacheDir);

// --- Top-N-Coins nach 24h-Volumen (spiegelt Live-Scanner OnlyTopByVolume/TopCoinsCount) ---
//     Ueberschreibt --symbols/--preset. --include-tradfi steuert, ob TradFi-Perps (NC-Prefix) mitlaufen.
if (GetArg(argMap, "top-coins", null) != null)
{
    var topN = int.Parse(GetArg(argMap, "top-coins", "100")!, CultureInfo.InvariantCulture);
    var includeTradFi = GetArg(argMap, "include-tradfi", "true") != "false";
    // Sub-Kategorie-Ausschluss: TradFi behalten, aber Aktien (NCSK) raus — beantwortet, ob der
    // Xsec-Dispersions-Edge an den Aktien-Perps haengt oder an Rohstoffen/Indizes/Forex.
    var excludeStocks = GetArg(argMap, "exclude-stocks", "false") == "true";
    Console.WriteLine($"Lade Top-{topN} Symbole nach 24h-Volumen (TradFi={includeTradFi}, ExcludeStocks={excludeStocks})...");
    var allTickers = await realClient.GetAllTickersAsync();
    var ranked = allTickers
        .Where(t => t.Symbol.EndsWith("-USDT", StringComparison.OrdinalIgnoreCase)
                    && SymbolClassifier.IsApiTradeable(t.Symbol)
                    && (includeTradFi || !SymbolClassifier.IsTradFi(t.Symbol))
                    && (!excludeStocks || SymbolClassifier.Classify(t.Symbol) != MarketCategory.Stock))
        .OrderByDescending(t => t.Volume24h)
        .Take(topN)
        .Select(t => t.Symbol)
        .ToList();
    if (ranked.Count > 0)
    {
        symbols = ranked;
        var nTradFi = symbols.Count(SymbolClassifier.IsTradFi);
        Console.WriteLine($"  -> {symbols.Count} Symbole ({symbols.Count - nTradFi} Crypto + {nTradFi} TradFi)");
    }
    else
        Console.WriteLine("  WARNUNG: keine Ticker geladen — nutze --preset/--symbols-Fallback.");
}

// --- Phasen-Screen: jede Strategie ueber 4 disjunkte Marktphasen auf dem Portfolio-Mirror. Eigener Pfad. ---
//     Findet eine Strategie, die in JEDER Phase profitabel ist (Robustheit), nicht nur aggregiert (Overfit).
if (GetArg(argMap, "phase-screen", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;
    botSettings.Backtest.EnableScannerPrefilter = GetArg(argMap, "scanner-filter", "true") != "false";
    botSettings.Backtest.EnableBtcHealthScale = GetArg(argMap, "btc-health", "true") != "false";
    // --strategies (kommagetrennt) ueberschreibt das Default-Set; sonst PhaseScreen.DefaultStrategies.
    var screenStrategies = GetArg(argMap, "strategies", null) is { Length: > 0 } sList
        ? sList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : PhaseScreen.DefaultStrategies;
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    var memData = new MemoryKlineCache(dataClient);
    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    return await PhaseScreen.RunAsync(screenStrategies, PhaseScreen.DefaultPhases(), symbols, navTf,
        botSettings, memData, symbolInfo, balance, parallelism, outDir, label);
}

// --- Xsec-Screen: Cross-Sectional-Momentum-Configs ueber 4 Phasen (strukturell phasen-robust). Eigener Pfad. ---
if (GetArg(argMap, "xsec", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    var memData = new MemoryKlineCache(dataClient);
    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    // --xsec-levs "1,2,3,5": Leverage-Sweep auf dem Live-Profil (L120/R126/3L-3S/radj) statt
    // des Default-Config-Sets — isoliert den Hebel-Effekt auf der produktiven Config.
    // --xsec-stops "0,2,3,4": ATR-Stop-Sweep auf dem Live-Profil (lev2) — prueft, ob ein
    // Per-Position-Stop zwischen den Rebalances das Tail-Risiko senkt ohne den Edge zu fressen.
    var configs = GetArg(argMap, "xsec-levs", null) is { Length: > 0 } levList
        ? levList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => new BingXBot.Backtest.Portfolio.XsecParams(
                LookbackCandles: 120, RebalanceEveryCandles: 126, LongK: 3, ShortK: 3,
                RiskAdjusted: true, AtrStopMultiplier: 0m,
                LeverageCap: int.Parse(l, CultureInfo.InvariantCulture)))
            .ToArray()
        : GetArg(argMap, "xsec-stops", null) is { Length: > 0 } stopList
        ? stopList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => new BingXBot.Backtest.Portfolio.XsecParams(
                LookbackCandles: 120, RebalanceEveryCandles: 126, LongK: 3, ShortK: 3,
                RiskAdjusted: true,
                AtrStopMultiplier: decimal.Parse(s, CultureInfo.InvariantCulture),
                LeverageCap: 2))
            .ToArray()
        : GetArg(argMap, "xsec-grid", null) is "research"
        ? XsecScreen.ResearchConfigs()
        : GetArg(argMap, "xsec-grid", null) is "strategies"
        ? XsecScreen.StrategyConfigs()
        : GetArg(argMap, "xsec-grid", null) is "fine"
        ? XsecScreen.FineConfigs()
        : GetArg(argMap, "xsec-grid", null) is "final"
        ? XsecScreen.FinalConfigs()
        : XsecScreen.DefaultConfigs();
    return await XsecScreen.RunAsync(configs, PhaseScreen.DefaultPhases(), symbols, navTf,
        botSettings, memData, symbolInfo, balance, parallelism, outDir, label);
}

// --- Funding-Screen: Carry-Faktor + Momentum+Carry-Kombi (echte Funding-Historie). Eigener Pfad. ---
if (GetArg(argMap, "funding-carry", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    var memData = new MemoryKlineCache(dataClient);
    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    using var fundingHttp = new HttpClient();
    var fundingProvider = new FundingHistoryProvider(fundingHttp, Path.Combine(toolDir, ".funding-cache"));
    return await FundingScreen.RunAsync(FundingScreen.DefaultConfigs(), PhaseScreen.DefaultPhases(), symbols, navTf,
        botSettings, memData, fundingProvider, symbolInfo, balance, parallelism, outDir, label);
}

// --- Pairs-Screen: Distance-Method Statistical Arbitrage ueber 4 Phasen. Eigener Pfad. ---
if (GetArg(argMap, "pairs", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    var memData = new MemoryKlineCache(dataClient);
    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    return await PairsScreen.RunAsync(PairsScreen.DefaultConfigs(), PhaseScreen.DefaultPhases(), symbols, navTf,
        botSettings, memData, symbolInfo, balance, parallelism, outDir, label);
}

// --- Sweep-Modus (Parameter-Grid + Walk-Forward)? Eigener Pfad, beendet danach. ---
if (GetArg(argMap, "sweep", null) != null)
{
    var memData = new MemoryKlineCache(dataClient);
    var scope = (GetArg(argMap, "sweep-grid", "extended") ?? "extended").ToLowerInvariant();
    var trainSplit = decimal.Parse(GetArg(argMap, "train-split", "0.65")!, CultureInfo.InvariantCulture);
    var topN = int.Parse(GetArg(argMap, "sweep-top", "20")!, CultureInfo.InvariantCulture);
    var minTrades = int.Parse(GetArg(argMap, "sweep-min-trades", "50")!, CultureInfo.InvariantCulture);
    var rankKey = (GetArg(argMap, "sweep-rank", "expectancy") ?? "expectancy").ToLowerInvariant();
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    return await Sweep.RunAsync(symbols, tfs, from, to, botSettings, memData,
        scope, trainSplit, topN, minTrades, rankKey, parallelism, outDir, label);
}

// --- Compare-Modus (rollierender Walk-Forward, mehrere SL-Werte)? Eigener Pfad, beendet danach. ---
if (GetArg(argMap, "compare", null) != null)
{
    var memData = new MemoryKlineCache(dataClient);
    var slValues = GetList(argMap, "compare-sl", "2.5,3.0")
        .Select(s => decimal.Parse(s, CultureInfo.InvariantCulture)).ToArray();
    var windowDays = int.Parse(GetArg(argMap, "window-days", "180")!, CultureInfo.InvariantCulture);
    var stepDays = int.Parse(GetArg(argMap, "step-days", "60")!, CultureInfo.InvariantCulture);
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    return await Sweep.CompareAsync(symbols, tfs, from, to, botSettings, memData,
        slValues, windowDays, stepDays, parallelism, outDir, label);
}

// --- Full-Modus (durchgehender Voll-Zeitraum, mehrere SL-Werte)? Eigener Pfad, beendet danach. ---
if (GetArg(argMap, "full", null) != null)
{
    var memData = new MemoryKlineCache(dataClient);
    var slValues = GetList(argMap, "compare-sl", "2.5,2.75,3.0,3.25")
        .Select(s => decimal.Parse(s, CultureInfo.InvariantCulture)).ToArray();
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    return await Sweep.FullAsync(symbols, tfs, from, to, botSettings, memData,
        slValues, parallelism, outDir, label);
}

// --- Achsen-Sweep (--axis be|tp|sl|tp1split): isolierte OFAT-Kurve EINER Stellschraube ---
//     --axis-values ueberschreibt die Default-Werteliste. Alle anderen Achsen = Live-Baseline.
if (GetArg(argMap, "axis", null) != null)
{
    var memData = new MemoryKlineCache(dataClient);
    var axis = (GetArg(argMap, "axis", "be") ?? "be").ToLowerInvariant();
    var valuesArg = GetArg(argMap, "axis-values", null);
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));
    var (title, variants) = BuildAxisVariants(axis, valuesArg);
    return await Sweep.AxisAsync(title, variants, symbols, tfs, from, to, botSettings, memData,
        parallelism, outDir, label);
}

// --- Portfolio-Sweep: variiert SL/BE/TP-RRR/TP1-Split ueber das EINE gemeinsame Konto. ---
//     Jede Kombi = ein voller PortfolioBacktestEngine-Lauf ueber alle Symbole (teuer → fokussiertes Grid,
//     Klines einmal in den RAM-Cache, Kombis parallel). Findet, ob IRGENDEINE Kombi das live-getreue
//     Portfolio-Ergebnis ins Plus dreht. Donchian/EMA/ADX bleiben fix auf Live (der Live-Bot variiert sie nicht).
if (GetArg(argMap, "portfolio-sweep", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;
    var scope = (GetArg(argMap, "sweep-grid", "full") ?? "full").ToLowerInvariant();
    var parallelism = Math.Max(1, int.Parse(GetArg(argMap, "sweep-parallel", Environment.ProcessorCount.ToString())!, CultureInfo.InvariantCulture));

    // Live-Spiegel-Vorfilter (GAP 11 + GAP 4): im Sweep standardmaessig AN ("alles wie in live"),
    // per --scanner-filter false / --btc-health false abschaltbar (Diagnose).
    botSettings.Backtest.EnableScannerPrefilter = GetArg(argMap, "scanner-filter", "true") != "false";
    botSettings.Backtest.EnableBtcHealthScale = GetArg(argMap, "btc-health", "true") != "false";

    var memData = new MemoryKlineCache(dataClient);
    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    return await PortfolioSweep.RunAsync(symbols, navTf, from, to, botSettings, memData, symbolInfo,
        balance, scope, parallelism, outDir, label);
}

// --- Portfolio-Modus: EIN gemeinsames Konto ueber alle Symbole, zeitlich gemergt. Eigener Pfad, beendet danach. ---
//     Macht den Backtest zum Spiegelbild des Live-Bots: konto-weite Risk-Gates (MaxOpenPositions,
//     MaxTotalMargin, Korrelation, Daily-Loss/Drawdown) feuern, Sizing teilt sich die EINE Equity.
if (GetArg(argMap, "portfolio", null) != null)
{
    var balance = decimal.Parse(GetArg(argMap, "balance", "158")!, CultureInfo.InvariantCulture);
    botSettings.Backtest.InitialBalance = balance;
    // Portfolio-Fokus = Live-Strategie TrendFollow-Fast (H4-only). Nur wenn der User explizit
    // --strategies setzt, wird dessen erster Eintrag genutzt (der --strategies-Default "SK-System"
    // existiert in der StrategyFactory nicht mehr → wuerde sonst werfen).
    var portfolioStrategy = GetArg(argMap, "strategies", null) is { Length: > 0 } explicitStrat
        ? explicitStrat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
        : "TrendFollow-Fast";
    var navTf = tfs.Count > 0 ? tfs[0] : TimeFrame.H4;

    // GAP 11 + GAP 4: Live-Spiegel-Vorfilter. Im --portfolio-Modus standardmaessig AN ("alles wie in live"),
    // per --scanner-filter false / --btc-health false fuer Diagnose abschaltbar.
    botSettings.Backtest.EnableScannerPrefilter = GetArg(argMap, "scanner-filter", "true") != "false";
    botSettings.Backtest.EnableBtcHealthScale = GetArg(argMap, "btc-health", "true") != "false";

    Console.WriteLine($"PORTFOLIO-Modus: 1 Konto ({balance:F0} USDT) ueber {symbols.Count} Symbole, Strategie {portfolioStrategy}, Nav-TF {navTf}");
    Console.WriteLine($"  Gates: MaxOpenPositions={botSettings.Risk.MaxOpenPositions} | MaxTotalMargin={botSettings.Risk.MaxTotalMarginPercent}% | MaxCorrelated={botSettings.Risk.MaxCorrelatedExposurePercent}%");
    Console.WriteLine($"  Live-Spiegel: Scanner-Vorfilter (GAP 11)={(botSettings.Backtest.EnableScannerPrefilter ? "AN" : "aus")} | BTC-Health-Scale + SK-Score (GAP 4)={(botSettings.Backtest.EnableBtcHealthScale ? "AN" : "aus")} | Sessions={botSettings.EnabledSessions}");
    Console.WriteLine();

    var symbolInfo = await BingXSymbolInfoProvider.LoadAsync(Path.Combine(toolDir, ".symbolinfo-cache"));
    var portfolioEngine = new PortfolioBacktestEngine(dataClient, symbolInfo, NullLogger<PortfolioBacktestEngine>.Instance);

    // Beobachte die maximal gleichzeitig offenen Positionen — Beweis, dass der konto-weite
    // MaxOpenPositions-Gate greift (GAP 1: die alte Single-Symbol-Summe hatte effektiv unbegrenzt viele).
    var maxConcurrentOpen = 0;
    var pReport = await portfolioEngine.RunAsync(symbols, navTf, from, to, botSettings, portfolioStrategy,
        onStepOpenPositions: c => maxConcurrentOpen = Math.Max(maxConcurrentOpen, c));

    Console.WriteLine($"Cache: {dataClient.CacheHits} Hits / {dataClient.CacheMisses} Misses");
    Console.WriteLine($"Max. gleichzeitig offene Positionen: {maxConcurrentOpen} (Gate MaxOpenPositions={botSettings.Risk.MaxOpenPositions})\n");
    WritePortfolioReport(pReport, symbols, navTf, from, to, balance, botSettings, portfolioStrategy, label!, outDir, maxConcurrentOpen);
    return 0;
}

// --- Backtest-Matrix ---
var results = new List<RunResult>();
int total = strategies.Count * symbols.Count * tfs.Count;
int done = 0;

foreach (var stratName in strategies)
{
    foreach (var tf in tfs)
    {
        foreach (var symbol in symbols)
        {
            done++;
            try
            {
                var strategy = StrategyFactory.Create(stratName);
                var risk = new RiskManager(botSettings.Risk, NullLogger<RiskManager>.Instance);
                var engine = new BacktestEngine(dataClient, NullLogger<BacktestEngine>.Instance);

                var report = await engine.RunAsync(
                    strategy, risk, symbol, tf, from, to, botSettings.Backtest,
                    scannerSettings: botSettings.Scanner, riskSettings: botSettings.Risk);

                var rr = new RunResult(stratName, symbol, tf, report.TotalTrades, report.WinningTrades,
                    report.LosingTrades, report.WinRate, report.ProfitFactor, report.TotalPnl,
                    report.AverageRrr, report.AverageWin, report.AverageLoss, report.MaxDrawdownPercent,
                    report.SharpeRatio, report.MaxConsecutiveLosses,
                    report.Trades.Select(t => new TradeRec(t.Pnl, t.Side == Side.Buy,
                        t.Reason.Contains("Stop", StringComparison.OrdinalIgnoreCase) || t.Reason.Contains("SL", StringComparison.OrdinalIgnoreCase))).ToList());
                results.Add(rr);

                Console.WriteLine($"[{done}/{total}] {stratName,-16} {symbol,-22} {tf,-4} " +
                    $"Trades={report.TotalTrades,3} WR={report.WinRate,5:F1}% PF={FmtPf(report.ProfitFactor),5} PnL={report.TotalPnl,8:F2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{done}/{total}] {stratName,-16} {symbol,-22} {tf,-4} FEHLER: {ex.Message}");
            }
        }
    }
}

Console.WriteLine($"\nCache: {dataClient.CacheHits} Hits / {dataClient.CacheMisses} Misses\n");

// --- Aggregation pro Strategie ---
Console.WriteLine("=== AGGREGAT pro Strategie (alle Symbole/TFs) ===");
var sb = new StringBuilder();
sb.AppendLine($"# Backtest-Lab Report — {label}");
sb.AppendLine($"Zeitraum: {from:yyyy-MM-dd} bis {to:yyyy-MM-dd} | Symbole: {symbols.Count} | TFs: {string.Join("/", tfs)}");
sb.AppendLine();
sb.AppendLine("## Aggregat pro Strategie");
sb.AppendLine("| Strategie | Trades | WinRate | PF | Expectancy/Trade | Σ PnL | Ø RRR | Ø MaxDD% | Ø Sharpe | Long (n@WR/PnL) | Short (n@WR/PnL) |");
sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

var aggregates = new List<object>();
foreach (var stratName in strategies)
{
    var runs = results.Where(r => r.Strategy == stratName).ToList();
    var allTrades = runs.SelectMany(r => r.Trades).ToList();
    var allPnls = allTrades.Select(t => t.Pnl).ToList();
    int n = allPnls.Count;
    int wins = allPnls.Count(p => p > 0);
    int losses = allPnls.Count(p => p <= 0);
    decimal wr = n > 0 ? 100m * wins / n : 0;
    decimal grossWin = allPnls.Where(p => p > 0).Sum();
    decimal grossLoss = Math.Abs(allPnls.Where(p => p < 0).Sum());
    decimal pf = grossLoss > 0 ? grossWin / grossLoss : (grossWin > 0 ? 999m : 0m);
    decimal totalPnl = allPnls.Sum();
    decimal expectancy = n > 0 ? totalPnl / n : 0;
    decimal avgWin = wins > 0 ? allPnls.Where(p => p > 0).Average() : 0;
    decimal avgLoss = losses > 0 ? Math.Abs(allPnls.Where(p => p <= 0).Average()) : 0;
    decimal avgRrr = avgLoss > 0 ? avgWin / avgLoss : 0;
    decimal avgMaxDd = runs.Count > 0 ? runs.Average(r => r.MaxDrawdownPercent) : 0;
    decimal avgSharpe = runs.Count > 0 ? runs.Average(r => r.SharpeRatio) : 0;

    // Long/Short-Aufschluesselung — deckt Bull-Market-Long-Bias auf.
    var longs = allTrades.Where(t => t.IsLong).ToList();
    var shorts = allTrades.Where(t => !t.IsLong).ToList();
    decimal longWr = longs.Count > 0 ? 100m * longs.Count(t => t.Pnl > 0) / longs.Count : 0;
    decimal shortWr = shorts.Count > 0 ? 100m * shorts.Count(t => t.Pnl > 0) / shorts.Count : 0;
    decimal longPnl = longs.Sum(t => t.Pnl), shortPnl = shorts.Sum(t => t.Pnl);
    decimal stopExitShare = n > 0 ? 100m * allTrades.Count(t => t.IsStopExit) / n : 0;

    Console.WriteLine($"  {stratName,-18} Trades={n,4} WR={wr,5:F1}% PF={FmtPf(pf),5} E/Trade={expectancy,7:F3} ΣPnL={totalPnl,9:F2} RRR={avgRrr,4:F2} | Long {longs.Count,3}@{longWr,4:F0}% ΣP={longPnl,8:F1} | Short {shorts.Count,3}@{shortWr,4:F0}% ΣP={shortPnl,8:F1} | SL-Exit {stopExitShare,3:F0}%");
    sb.AppendLine($"| {stratName} | {n} | {wr:F1}% | {FmtPf(pf)} | {expectancy:F3} | {totalPnl:F2} | {avgRrr:F2} | {avgMaxDd:F1} | {avgSharpe:F2} | {longs.Count}@{longWr:F0}%/{longPnl:F0} | {shorts.Count}@{shortWr:F0}%/{shortPnl:F0} |");

    aggregates.Add(new { strategy = stratName, trades = n, winRate = wr, profitFactor = pf, expectancy, totalPnl, avgRrr, avgMaxDd, avgSharpe,
        longCount = longs.Count, longWinRate = longWr, longPnl, shortCount = shorts.Count, shortWinRate = shortWr, shortPnl, stopExitShare });
}

// --- Detail pro (Strategie, TF) ---
sb.AppendLine();
sb.AppendLine("## Detail pro Strategie × TF");
sb.AppendLine("| Strategie | TF | Trades | WinRate | PF | Σ PnL |");
sb.AppendLine("|---|---|---|---|---|---|");
foreach (var stratName in strategies)
foreach (var tf in tfs)
{
    var runs = results.Where(r => r.Strategy == stratName && r.Tf == tf).ToList();
    var pnls = runs.SelectMany(r => r.Trades).Select(t => t.Pnl).ToList();
    int n = pnls.Count;
    decimal wr = n > 0 ? 100m * pnls.Count(p => p > 0) / n : 0;
    decimal gw = pnls.Where(p => p > 0).Sum(), gl = Math.Abs(pnls.Where(p => p < 0).Sum());
    decimal pf = gl > 0 ? gw / gl : (gw > 0 ? 999m : 0m);
    sb.AppendLine($"| {stratName} | {tf} | {n} | {wr:F1}% | {FmtPf(pf)} | {pnls.Sum():F2} |");
}

// --- Output schreiben ---
Directory.CreateDirectory(outDir);
var stamp = label;
var mdPath = Path.Combine(outDir, $"report-{stamp}.md");
File.WriteAllText(mdPath, sb.ToString());
var jsonPath = Path.Combine(outDir, $"report-{stamp}.json");
File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
{
    from, to, symbols, tfs = tfs.Select(t => t.ToString()),
    aggregates,
    runs = results.Select(r => new { r.Strategy, r.Symbol, tf = r.Tf.ToString(), r.TotalTrades, r.WinRate, r.ProfitFactor, r.TotalPnl })
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"\nReport: {mdPath}");
Console.WriteLine($"JSON  : {jsonPath}");

return 0;

// ============================================================================
//  Helpers
// ============================================================================
static string FmtPf(decimal pf) => pf >= 999m ? "inf" : pf.ToString("F2", CultureInfo.InvariantCulture);

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var key = args[i][2..];
        var val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
        map[key] = val;
    }
    return map;
}

static string? GetArg(Dictionary<string, string> map, string key, string? def) => map.TryGetValue(key, out var v) ? v : def;
static List<string> GetList(Dictionary<string, string> map, string key, string def) =>
    (GetArg(map, key, def) ?? def).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

static TimeFrame ParseTf(string s) => Enum.Parse<TimeFrame>(s, ignoreCase: true);

static List<decimal> ParseDecList(string s) =>
    s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
     .Select(x => decimal.Parse(x, CultureInfo.InvariantCulture)).ToList();

// Baut die OFAT-Varianten fuer eine Achse: variiert nur diese eine Stellschraube auf der
// Live-Baseline. RRR-Achse erwartet "r1/r2"-Paare (z.B. "1.5/3.0,2.0/4.0").
static (string Title, List<(string Label, ParamCombo Combo)> Variants) BuildAxisVariants(string axis, string? valuesArg)
{
    var b = Sweep.Baseline;
    switch (axis)
    {
        case "sl":
            return ("Stop-Loss (ATR-Multiplikator)",
                ParseDecList(valuesArg ?? "2.0,2.5,2.75,3.0,3.25,3.5")
                    .Select(v => ($"SL×{v:0.00}", b with { AtrSl = v })).ToList());
        case "be":
            // 0 = BE-Distanz-Trigger aus (nur A-Bruch, bei TrendFollow NavPointA=0 → nie BE).
            return ("Break-Even-Trigger (R-Multiple)",
                ParseDecList(valuesArg ?? "0,1.0,1.5,2.0,2.5,3.0")
                    .Select(v => ($"BE{v:0.0}R", b with { BeTrigger = v })).ToList());
        case "tp1split":
            // Anteil, der bei TP1 geschlossen wird (1.0 = alles bei TP1, kein TP2-Runner-Rest).
            return ("TP1-Teilschliessung (Close-Ratio)",
                ParseDecList(valuesArg ?? "0.3,0.5,0.7,1.0")
                    .Select(v => ($"TP1x{v:0.00}", b with { Tp1Split = v })).ToList());
        case "tp":
            var pairs = (valuesArg ?? "1.0/2.0,1.2/2.5,1.5/3.0,1.5/4.0,2.0/4.0,2.0/5.0,2.5/5.0")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tpVariants = pairs.Select(p =>
            {
                var xy = p.Split('/', StringSplitOptions.TrimEntries);
                var r1 = decimal.Parse(xy[0], CultureInfo.InvariantCulture);
                var r2 = decimal.Parse(xy[1], CultureInfo.InvariantCulture);
                return ($"RRR{r1:0.0}/{r2:0.0}", b with { Rrr1 = r1, Rrr2 = r2 });
            }).ToList();
            return ("Take-Profit (RRR1/RRR2)", tpVariants);
        default:
            throw new ArgumentException($"Unbekannte Achse: {axis} (erlaubt: sl, be, tp, tp1split)");
    }
}

static List<string> ResolveSymbols(string? explicitList, string? preset)
{
    if (!string.IsNullOrWhiteSpace(explicitList))
        return explicitList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    return preset switch
    {
        // Die Symbole, die im Mai live getradet wurden — fuer die Realismus-Validierung.
        "may-live" => ["LTC-USDT", "BNB-USDT", "DOGE-USDT", "JUP-USDT", "LINK-USDT", "ONDO-USDT",
            "SUI-USDT", "CRV-USDT", "AAVE-USDT", "BCH-USDT", "BTC-USDT", "ENS-USDT", "ETHFI-USDT",
            "HYPE-USDT", "ICP-USDT", "ZEC-USDT", "JTO-USDT", "PENDLE-USDT", "KAS-USDT", "NEAR-USDT", "TAO-USDT"],
        "crypto-major" => ["BTC-USDT", "ETH-USDT", "BNB-USDT", "SOL-USDT", "XRP-USDT", "ADA-USDT",
            "DOGE-USDT", "AVAX-USDT", "LINK-USDT", "DOT-USDT"],
        _ => ["BTC-USDT", "ETH-USDT", "SOL-USDT"]
    };
}

static string? FindToolDir()
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 6 && dir != null; i++)
    {
        if (File.Exists(Path.Combine(dir, "BingXBacktestLab.csproj"))) return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}

// Schreibt den Portfolio-Report (Console + Markdown + JSON): Σ PnL, echte Konto-MaxDD% (aus
// Equity-Curve via PerformanceReport.FromTrades), WinRate, PF, Long/Short-Split, Trade-Anzahl,
// plus pro-Symbol-Breakdown (Trades.GroupBy(Symbol)).
static void WritePortfolioReport(PerformanceReport report, List<string> symbols, TimeFrame navTf,
    DateTime from, DateTime to, decimal balance, BotSettings settings, string strategyName, string label,
    string outDir, int maxConcurrentOpen)
{
    var trades = report.Trades;
    var longs = trades.Where(t => t.Side == Side.Buy).ToList();
    var shorts = trades.Where(t => t.Side == Side.Sell).ToList();
    decimal longPnl = longs.Sum(t => t.Pnl), shortPnl = shorts.Sum(t => t.Pnl);
    decimal longWr = longs.Count > 0 ? 100m * longs.Count(t => t.Pnl > 0) / longs.Count : 0m;
    decimal shortWr = shorts.Count > 0 ? 100m * shorts.Count(t => t.Pnl > 0) / shorts.Count : 0m;

    Console.WriteLine("=== PORTFOLIO-Ergebnis (1 gemeinsames Konto) ===");
    Console.WriteLine($"  Start-Balance : {balance:F2} USDT");
    Console.WriteLine($"  End-Balance   : {balance + report.TotalPnl:F2} USDT");
    Console.WriteLine($"  Σ PnL         : {report.TotalPnl:F2} USDT ({(balance > 0 ? report.TotalPnl / balance * 100m : 0m):F1}%)");
    Console.WriteLine($"  Trades        : {report.TotalTrades} (Win {report.WinningTrades} / Loss {report.LosingTrades})");
    Console.WriteLine($"  WinRate       : {report.WinRate:F1}%");
    Console.WriteLine($"  ProfitFactor  : {FmtPf(report.ProfitFactor)}");
    Console.WriteLine($"  Konto-MaxDD   : {report.MaxDrawdownPercent:F1}% ({report.MaxDrawdown:F2} USDT)");
    Console.WriteLine($"  Sharpe        : {report.SharpeRatio:F2}");
    Console.WriteLine($"  Long          : {longs.Count} @ {longWr:F0}% WR, ΣPnL {longPnl:F2}");
    Console.WriteLine($"  Short         : {shorts.Count} @ {shortWr:F0}% WR, ΣPnL {shortPnl:F2}");
    Console.WriteLine();

    // Pro-Symbol-Breakdown.
    var bySymbol = trades.GroupBy(t => t.Symbol)
        .Select(g =>
        {
            var n = g.Count();
            var wins = g.Count(t => t.Pnl > 0);
            var gw = g.Where(t => t.Pnl > 0).Sum(t => t.Pnl);
            var gl = Math.Abs(g.Where(t => t.Pnl < 0).Sum(t => t.Pnl));
            return new
            {
                Symbol = g.Key,
                Trades = n,
                WinRate = n > 0 ? 100m * wins / n : 0m,
                ProfitFactor = gl > 0 ? gw / gl : (gw > 0 ? 999m : 0m),
                Pnl = g.Sum(t => t.Pnl)
            };
        })
        .OrderByDescending(x => x.Pnl)
        .ToList();

    Console.WriteLine("  Pro Symbol (Σ PnL absteigend):");
    foreach (var s in bySymbol)
        Console.WriteLine($"    {s.Symbol,-14} Trades={s.Trades,3} WR={s.WinRate,5:F1}% PF={FmtPf(s.ProfitFactor),5} ΣPnL={s.Pnl,9:F2}");

    // --- Markdown ---
    var sb = new StringBuilder();
    sb.AppendLine($"# Portfolio-Backtest — {label}");
    sb.AppendLine($"1 gemeinsames Konto | Strategie {strategyName} | Nav-TF {navTf}");
    sb.AppendLine($"Zeitraum {from:yyyy-MM-dd}..{to:yyyy-MM-dd} | {symbols.Count} Symbole | Start-Balance {balance:F2} USDT");
    sb.AppendLine($"Gates: MaxOpenPositions={settings.Risk.MaxOpenPositions}, MaxTotalMargin={settings.Risk.MaxTotalMarginPercent}%, MaxCorrelated={settings.Risk.MaxCorrelatedExposurePercent}%");
    sb.AppendLine();
    sb.AppendLine("## Konto-Ergebnis");
    sb.AppendLine("| Metrik | Wert |");
    sb.AppendLine("|---|---|");
    sb.AppendLine($"| Start-Balance | {balance:F2} USDT |");
    sb.AppendLine($"| End-Balance | {balance + report.TotalPnl:F2} USDT |");
    sb.AppendLine($"| Σ PnL | {report.TotalPnl:F2} USDT ({(balance > 0 ? report.TotalPnl / balance * 100m : 0m):F1}%) |");
    sb.AppendLine($"| Trades | {report.TotalTrades} (Win {report.WinningTrades} / Loss {report.LosingTrades}) |");
    sb.AppendLine($"| WinRate | {report.WinRate:F1}% |");
    sb.AppendLine($"| ProfitFactor | {FmtPf(report.ProfitFactor)} |");
    sb.AppendLine($"| Konto-MaxDD% | {report.MaxDrawdownPercent:F1}% ({report.MaxDrawdown:F2} USDT) |");
    sb.AppendLine($"| Max. gleichzeitig offen | {maxConcurrentOpen} (Gate {settings.Risk.MaxOpenPositions}) |");
    sb.AppendLine($"| Sharpe | {report.SharpeRatio:F2} |");
    sb.AppendLine($"| Ø Haltezeit | {report.AverageHoldTime:hh\\:mm} |");
    sb.AppendLine($"| Long | {longs.Count} @ {longWr:F0}% WR / ΣPnL {longPnl:F2} |");
    sb.AppendLine($"| Short | {shorts.Count} @ {shortWr:F0}% WR / ΣPnL {shortPnl:F2} |");
    sb.AppendLine();
    sb.AppendLine("## Pro Symbol");
    sb.AppendLine("| Symbol | Trades | WinRate | PF | Σ PnL |");
    sb.AppendLine("|---|---|---|---|---|");
    foreach (var s in bySymbol)
        sb.AppendLine($"| {s.Symbol} | {s.Trades} | {s.WinRate:F1}% | {FmtPf(s.ProfitFactor)} | {s.Pnl:F2} |");

    Directory.CreateDirectory(outDir);
    var mdPath = Path.Combine(outDir, $"portfolio-{label}.md");
    File.WriteAllText(mdPath, sb.ToString());
    var jsonPath = Path.Combine(outDir, $"portfolio-{label}.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        from, to, navTf = navTf.ToString(), symbolCount = symbols.Count, startBalance = balance,
        totalPnl = report.TotalPnl, endBalance = balance + report.TotalPnl,
        report.TotalTrades, report.WinningTrades, report.LosingTrades, report.WinRate,
        report.ProfitFactor, report.MaxDrawdownPercent, report.MaxDrawdown, report.SharpeRatio,
        maxConcurrentOpen, maxOpenGate = settings.Risk.MaxOpenPositions,
        longCount = longs.Count, longWinRate = longWr, longPnl,
        shortCount = shorts.Count, shortWinRate = shortWr, shortPnl,
        perSymbol = bySymbol
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"\nReport: {mdPath}");
    Console.WriteLine($"JSON  : {jsonPath}");
}

// ============================================================================
//  Typen
// ============================================================================
internal sealed record TradeRec(decimal Pnl, bool IsLong, bool IsStopExit);

internal sealed record RunResult(
    string Strategy, string Symbol, TimeFrame Tf, int TotalTrades, int WinningTrades, int LosingTrades,
    decimal WinRate, decimal ProfitFactor, decimal TotalPnl, decimal AverageRrr, decimal AverageWin,
    decimal AverageLoss, decimal MaxDrawdownPercent, decimal SharpeRatio, int MaxConsecutiveLosses,
    List<TradeRec> Trades);

/// <summary>Minimaler Rate-Limiter mit fixem Delay zwischen Requests (BingX Public ist grosszuegig).</summary>
internal sealed class SimpleRateLimiter(TimeSpan delay) : IRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _last = DateTime.MinValue;

    public async Task WaitForSlotAsync(string category, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var elapsed = DateTime.UtcNow - _last;
            if (elapsed < delay) await Task.Delay(delay - elapsed, ct).ConfigureAwait(false);
            _last = DateTime.UtcNow;
        }
        finally { _gate.Release(); }
    }
}
