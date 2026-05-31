using System.Globalization;
using System.Text;
using System.Text.Json;
using BingXBacktestLab;
using BingXBot.Backtest;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
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
    Console.WriteLine($"Settings geladen: MinConfluenceScore={botSettings.Risk.MinConfluenceScore}, EntryMode={botSettings.Risk.EntryMode}, MinRRR={botSettings.Risk.MinRiskRewardRatio}, ScannerMode={botSettings.Scanner.Mode}");
}
else
{
    botSettings = new BotSettings();
    Console.WriteLine("WARNUNG: keine settings.json gefunden — nutze Defaults.");
}
Console.WriteLine();

// --- Clients aufbauen ---
using var http = new HttpClient();
var rateLimiter = new SimpleRateLimiter(TimeSpan.FromMilliseconds(120));
var realClient = new BingXPublicClient(http, rateLimiter, NullLogger<BingXPublicClient>.Instance);
var dataClient = new CachingPublicClient(realClient, cacheDir);

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
