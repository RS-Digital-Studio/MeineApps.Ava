using BingXBot.Backtest;
using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Live-Backtest gegen echte BingX-Daten (Public API, kein Key nötig).
/// Testet CryptoTrendPro mit den neuen optimierten Parametern auf allen 3 Modi.
/// ACHTUNG: Braucht Internet-Verbindung! Kann 30-60s pro Test dauern.
/// </summary>
public class LiveBacktestRunner
{
    private readonly ITestOutputHelper _output;

    public LiveBacktestRunner(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("BTC-USDT", TradingModePreset.Swing, 90)]
    [InlineData("BTC-USDT", TradingModePreset.Swing, 180)]
    [InlineData("ETH-USDT", TradingModePreset.Swing, 90)]
    [InlineData("SOL-USDT", TradingModePreset.Swing, 90)]
    [InlineData("XRP-USDT", TradingModePreset.Swing, 90)]
    [InlineData("DOGE-USDT", TradingModePreset.Swing, 90)]
    [InlineData("AVAX-USDT", TradingModePreset.Swing, 90)]
    [InlineData("LINK-USDT", TradingModePreset.Swing, 90)]
    public async Task Backtest_MitNeuenParametern(string symbol, TradingModePreset mode, int tage)
    {
        // BingX Public API Client (kein Key nötig für Klines)
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var rateLimiter = new RateLimiter();
        var publicClient = new BingXPublicClient(httpClient, rateLimiter, NullLogger<BingXPublicClient>.Instance);

        // SK-System mit Mode-Preset
        var strategy = StrategyFactory.Create("SK-System");
        if (strategy is SequenzKonzeptStrategy sk)
            sk.ApplyPreset(mode);

        // RiskSettings aus Preset
        var riskPreset = TradingModeDefaults.GetRiskPreset(mode);
        var scanPreset = TradingModeDefaults.GetScannerPreset(mode);
        var riskSettings = new RiskSettings
        {
            MaxPositionSizePercent = riskPreset.MaxPositionSizePercent,
            MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent,
            MaxLeverage = riskPreset.MaxLeverage,
            EnableMultiStageExit = true,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            MinRiskRewardRatio = 0m, // Deaktiviert für Backtest (Signalqualität messen)
            MaxHoldHours = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            EnableTrailingStop = true,
            TrailingStopPercent = 2.5m,
            MaxOpenPositions = 10,
            MinLiquidationDistancePercent = 0m,
            MaxNetExposurePercent = 99999m,
            ConsiderFundingRate = false,
            MaxDailyDrawdownPercent = 0m,       // 0 = deaktiviert
        };
        var riskManager = new RiskManager(riskSettings, NullLogger<RiskManager>.Instance);

        // BacktestSettings
        var backtestSettings = new BacktestSettings
        {
            InitialBalance = 10_000m,
            TakerFee = 0.0005m,
            MakerFee = 0.0002m,
            SpreadPercent = 0.08m,
            UseDynamicSlippage = true,
            SimulateMultiStageExit = true,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            TrailingAtrMultiplier = 2.5m,
            MaxHoldHoursInitial = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            HtfTimeFrame = scanPreset.ScanTimeFrame switch
            {
                TimeFrame.M15 => TimeFrame.H1,
                TimeFrame.H1 => TimeFrame.H4,
                _ => TimeFrame.D1
            }
        };

        // Zeitraum
        var to = DateTime.UtcNow;
        var from = to.AddDays(-tage);

        _output.WriteLine($"=== BACKTEST: {symbol} | {mode} ({scanPreset.ScanTimeFrame}) | {tage} Tage ===");
        _output.WriteLine($"Zeitraum: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");

        // Holy Trinity: H4 + H1 + M15 laden (M15 in Batches wegen API-Limit 1440)
        var testCandles = await publicClient.GetKlinesAsync(symbol, TimeFrame.H4, from, to);
        var h1Candles = await publicClient.GetKlinesAsync(symbol, TimeFrame.H1, from, to);

        // M15: In 14-Tage-Batches laden und zusammenfügen (1440 Candles = ~15 Tage)
        var m15Candles = new List<BingXBot.Core.Models.Candle>();
        var batchStart = from;
        while (batchStart < to)
        {
            var batchEnd = batchStart.AddDays(14) < to ? batchStart.AddDays(14) : to;
            try
            {
                var batch = await publicClient.GetKlinesAsync(symbol, TimeFrame.M15, batchStart, batchEnd);
                m15Candles.AddRange(batch);
            }
            catch { /* Einzelne Batches können fehlschlagen */ }
            batchStart = batchEnd;
        }
        // Duplikate entfernen (Batch-Überlappung) und sortieren
        m15Candles = m15Candles.GroupBy(c => c.OpenTime).Select(g => g.First()).OrderBy(c => c.OpenTime).ToList();
        _output.WriteLine($"Klines geladen: {testCandles.Count} H4 + {h1Candles.Count} H1 + {m15Candles.Count} M15");
        if (testCandles.Count == 0)
        {
            _output.WriteLine("FEHLER: Keine Candles von BingX erhalten!");
            return;
        }

        // Holy Trinity: Iteriere auf M15-Takt (nicht H4!) um 15m-Trigger zu fangen
        // Bei jedem M15-Schritt werden H4 + H1 als Kontext-Slices mitgegeben
        int signalCount = 0;
        var reasonCounts = new Dictionary<string, int>();
        for (int i = 200; i < m15Candles.Count; i++)
        {
            var m15Slice = m15Candles.Skip(Math.Max(0, i - 200)).Take(Math.Min(200, i + 1)).ToList();
            var currentTime = m15Slice[^1].CloseTime;
            // H4 + H1 Candles bis zum aktuellen M15-Zeitpunkt
            var h4Slice = testCandles.Where(c => c.CloseTime <= currentTime).TakeLast(200).ToList();
            var h1Slice = h1Candles.Where(c => c.CloseTime <= currentTime).TakeLast(200).ToList();
            if (h4Slice.Count < 30) continue; // Brauche genug H4-Daten
            var ticker2 = new BingXBot.Core.Models.Ticker(symbol, m15Slice[^1].Close, m15Slice[^1].Close, m15Slice[^1].Close, m15Slice[^1].Volume, 0m, currentTime);
            var account2 = new BingXBot.Core.Models.AccountInfo(10000m, 10000m, 0m, 0m);
            // context.Candles=H4, HigherTimeframeCandles=H1, EntryTimeframeCandles=M15
            var ctx = new BingXBot.Core.Models.MarketContext(symbol, h4Slice, ticker2, new List<BingXBot.Core.Models.Position>(), account2,
                h1Slice.Count > 20 ? h1Slice : null, BingXBot.Core.Enums.MarketCategory.Crypto,
                m15Slice.Count > 20 ? m15Slice : null);
            var sig = strategy.Evaluate(ctx);
            if (sig.Signal != BingXBot.Core.Enums.Signal.None)
                signalCount++;
            else
            {
                // Reason kürzen auf Kernaussage
                var r = sig.Reason;
                var pipe = r.IndexOf('|'); if (pipe > 0) r = r[..pipe].Trim();
                var paren = r.IndexOf('('); if (paren > 0) r = r[..paren].Trim();
                reasonCounts.TryGetValue(r, out var c); reasonCounts[r] = c + 1;
            }
        }
        var totalNoSignal = reasonCounts.Values.Sum();
        _output.WriteLine($"Direkte Evaluation: {signalCount} Signale, {totalNoSignal} NoSignal");
        _output.WriteLine("Ablehnungsgründe:");
        foreach (var (reason, count) in reasonCounts.OrderByDescending(x => x.Value).Take(8))
            _output.WriteLine($"  {count,5}x ({count * 100 / Math.Max(1, totalNoSignal),2}%) {reason}");

        // Signale wurden bereits oben gesammelt — Deduplizierung verhindert Wiederholung
        // (Die Strategie gibt für gleiche Sequenz nur einmal ein Signal)
        var signalDetails = new List<string>();
        strategy.Reset(); // Reset für zweiten Durchlauf
        for (int i = 200; i < m15Candles.Count; i += 4) // Jede 4. M15-Kerze (= 1h Takt, schnellerer Durchlauf)
        {
            var m15s2 = m15Candles.Skip(Math.Max(0, i - 200)).Take(Math.Min(200, i + 1)).ToList();
            var curTime2 = m15s2[^1].CloseTime;
            var h4s2 = testCandles.Where(c => c.CloseTime <= curTime2).TakeLast(200).ToList();
            var h1s2 = h1Candles.Where(c => c.CloseTime <= curTime2).TakeLast(200).ToList();
            if (h4s2.Count < 30) continue;
            var tk = new BingXBot.Core.Models.Ticker(symbol, m15s2[^1].Close, m15s2[^1].Close, m15s2[^1].Close, m15s2[^1].Volume, 0m, curTime2);
            var ac = new BingXBot.Core.Models.AccountInfo(10000m, 10000m, 0m, 0m);
            var cx = new BingXBot.Core.Models.MarketContext(symbol, h4s2, tk, new List<BingXBot.Core.Models.Position>(), ac,
                h1s2.Count > 20 ? h1s2 : null, BingXBot.Core.Enums.MarketCategory.Crypto,
                m15s2.Count > 20 ? m15s2 : null);
            var sg = strategy.Evaluate(cx);
            if (sg.Signal is BingXBot.Core.Enums.Signal.Long or BingXBot.Core.Enums.Signal.Short)
            {
                var entry = m15s2[^1].Close;
                var slDist = Math.Abs(entry - (sg.StopLoss ?? 0));
                var tpDist = Math.Abs((sg.TakeProfit ?? 0) - entry);
                var rrr = slDist > 0 ? tpDist / slDist : 0;
                var slPct = entry > 0 ? slDist / entry * 100m : 0m;
                signalDetails.Add($"  {m15s2[^1].CloseTime:dd.MM HH:mm} {sg.Signal,-5} E={entry:F1} SL={sg.StopLoss:F1} ({slPct:F2}%) TP={sg.TakeProfit:F1} RRR={rrr:F1} | {sg.Reason[..Math.Min(80, sg.Reason.Length)]}");
            }
        }
        _output.WriteLine($"Alle {signalDetails.Count} Signale:");
        foreach (var d in signalDetails.Take(15)) _output.WriteLine(d);

        // Backtest ausführen
        var engine = new BacktestEngine(publicClient, NullLogger<BacktestEngine>.Instance);
        var report = await engine.RunAsync(strategy, riskManager, symbol, scanPreset.ScanTimeFrame,
            from, to, backtestSettings, ct: CancellationToken.None);

        // Ergebnisse ausgeben
        _output.WriteLine($"");
        _output.WriteLine($"Trades:         {report.TotalTrades}");
        _output.WriteLine($"Win-Rate:       {report.WinRate:P1}");
        _output.WriteLine($"Profit-Faktor:  {report.ProfitFactor:F2}");
        var returnPct = backtestSettings.InitialBalance > 0 ? report.TotalPnl / backtestSettings.InitialBalance * 100m : 0m;
        _output.WriteLine($"Gesamt-PnL:     {report.TotalPnl:+0.00;-0.00} USDT ({returnPct:+0.0;-0.0}%)");
        _output.WriteLine($"Max Drawdown:   {report.MaxDrawdownPercent:F1}%");
        _output.WriteLine($"Sharpe:         {report.SharpeRatio:F2}");
        _output.WriteLine($"Sortino:        {report.SortinoRatio:F2}");
        _output.WriteLine($"Avg Win:        {report.AverageWin:+0.00} USDT");
        _output.WriteLine($"Avg Loss:       {report.AverageLoss:+0.00;-0.00} USDT");
        _output.WriteLine($"Max ConsecLoss: {report.MaxConsecutiveLosses}");

        // Mindest-Erwartungen (nicht zu streng — Marktbedingungen variieren)
        if (report.TotalTrades >= 5)
        {
            _output.WriteLine($"");
            _output.WriteLine(report.WinRate >= 0.4m ? "WinRate >= 40%: OK" : "WinRate < 40%: WARNUNG");
            _output.WriteLine(report.ProfitFactor >= 1.0m ? "ProfitFactor >= 1.0: OK" : "ProfitFactor < 1.0: WARNUNG — Strategie verliert Geld");
            _output.WriteLine(report.MaxDrawdownPercent <= 15m ? "MaxDD <= 15%: OK" : "MaxDD > 15%: WARNUNG");
        }
        else
        {
            _output.WriteLine($"Zu wenig Trades ({report.TotalTrades}) für aussagekräftige Metriken.");
        }
    }
}
