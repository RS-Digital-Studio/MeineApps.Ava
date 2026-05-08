using BingXBot.Backtest.Reports;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Engine.Strategies;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest;

/// <summary>
/// v1.5.3 Phase 6 — Walk-Forward-Backtest-Runner.
///
/// Erzeugt N ueberlappende Backtest-Fenster (Window-Size + Step-Size), laeuft den
/// <see cref="BacktestEngine"/> pro Fenster und liefert einen <see cref="WalkForwardReport"/>
/// mit per-Window-Metriken + einem Robustheits-Score (Standardabweichung der WinRate).
///
/// Ohne Walk-Forward bestaetigt ein einzelner Backtest nur, dass die Strategie auf
/// einem konkreten Zeitraum profitabel war — ueber 15 user-tunbare Schwellen ist
/// Overfitting wahrscheinlich. Walk-Forward zeigt ob die Strategie ueber unterschiedliche
/// Markt-Regimes hinweg konsistent performt.
///
/// UI/REST sind in v1.5.3 Phase 6 nicht enthalten — der Runner ist als Library-Klasse
/// nutzbar, Tests gegen reine Berechnungen.
/// </summary>
public sealed class WalkForwardRunner
{
    private readonly BacktestEngine _engine;
    private readonly ILogger<WalkForwardRunner>? _logger;

    public WalkForwardRunner(BacktestEngine engine, ILogger<WalkForwardRunner>? logger = null)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Erzeugt Window-Ranges (From, To) — uebersetzt das Plan-Konzept "ueberlappende
    /// Fenster der Groesse <paramref name="windowSize"/> mit Schrittweite <paramref name="stepSize"/>"
    /// in konkrete Datums-Bereiche. Public + static fuer Unit-Tests.
    /// </summary>
    public static IReadOnlyList<(DateTime From, DateTime To)> GenerateWindows(
        DateTime totalFrom, DateTime totalTo, TimeSpan windowSize, TimeSpan stepSize)
    {
        if (windowSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(windowSize));
        if (stepSize <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(stepSize));
        if (totalTo <= totalFrom) throw new ArgumentException("totalTo muss > totalFrom sein");

        var windows = new List<(DateTime, DateTime)>();
        var cursor = totalFrom;
        while (cursor + windowSize <= totalTo)
        {
            windows.Add((cursor, cursor + windowSize));
            cursor = cursor + stepSize;
        }
        return windows;
    }

    /// <summary>
    /// Fuehrt den Walk-Forward fuer einen Symbol-/TF-Bereich aus.
    /// Wirft <see cref="InvalidOperationException"/> wenn die Range zu kurz ist (&lt; 2 Fenster).
    /// </summary>
    public async Task<WalkForwardReport> RunAsync(
        string symbol,
        TimeFrame timeFrame,
        DateTime from,
        DateTime to,
        TimeSpan windowSize,
        TimeSpan stepSize,
        BacktestSettings settings,
        Func<IStrategy> strategyFactory,
        Func<IRiskManager> riskManagerFactory,
        ScannerSettings? scannerSettings = null,
        RiskSettings? riskSettings = null,
        IProgress<(int Window, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var windows = GenerateWindows(from, to, windowSize, stepSize);
        if (windows.Count < 2)
            throw new InvalidOperationException(
                $"Walk-Forward braucht mindestens 2 Fenster — aktuelle Range ergibt nur {windows.Count}. " +
                $"Range erweitern oder windowSize/stepSize verkleinern.");

        var perWindow = new List<WalkForwardWindowResult>(windows.Count);
        for (var i = 0; i < windows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (winFrom, winTo) = windows[i];
            progress?.Report((i + 1, windows.Count));

            try
            {
                var report = await _engine.RunAsync(
                    strategyFactory(), riskManagerFactory(), symbol, timeFrame,
                    winFrom, winTo, settings,
                    scannerSettings: scannerSettings, riskSettings: riskSettings,
                    ct: ct).ConfigureAwait(false);

                perWindow.Add(new WalkForwardWindowResult(
                    Index: i,
                    From: winFrom,
                    To: winTo,
                    TotalTrades: report.TotalTrades,
                    WinRate: report.WinRate,
                    NetPnl: report.TotalPnl,
                    MaxDrawdown: report.MaxDrawdown,
                    SharpeRatio: report.SharpeRatio));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Walk-Forward Window {Index} ({From}..{To}) fehlgeschlagen", i, winFrom, winTo);
                perWindow.Add(new WalkForwardWindowResult(
                    Index: i, From: winFrom, To: winTo,
                    TotalTrades: 0, WinRate: 0m, NetPnl: 0m, MaxDrawdown: 0m, SharpeRatio: 0m));
            }
        }

        return WalkForwardReport.FromWindows(symbol, timeFrame, windowSize, stepSize, perWindow);
    }
}

/// <summary>
/// v1.5.3 Phase 6 — Pro-Window-Ergebnis.
/// </summary>
public sealed record WalkForwardWindowResult(
    int Index,
    DateTime From,
    DateTime To,
    int TotalTrades,
    decimal WinRate,
    decimal NetPnl,
    decimal MaxDrawdown,
    decimal SharpeRatio);

/// <summary>
/// v1.5.3 Phase 6 — Aggregat-Report ueber alle Walk-Forward-Fenster.
/// <see cref="RobustnessScore"/> = Standardabweichung der WinRate ueber alle Fenster — niedrige
/// Werte bedeuten konsistente Performance, hohe Werte deuten auf Overfitting hin.
/// </summary>
public sealed record WalkForwardReport(
    string Symbol,
    TimeFrame TimeFrame,
    TimeSpan WindowSize,
    TimeSpan StepSize,
    int WindowCount,
    IReadOnlyList<WalkForwardWindowResult> Windows,
    decimal AvgWinRate,
    decimal RobustnessScore,
    decimal TotalNetPnl,
    decimal MaxDrawdownAcrossWindows)
{
    public static WalkForwardReport FromWindows(string symbol, TimeFrame tf,
        TimeSpan windowSize, TimeSpan stepSize, IReadOnlyList<WalkForwardWindowResult> windows)
    {
        if (windows.Count == 0)
            return new WalkForwardReport(symbol, tf, windowSize, stepSize, 0, windows,
                AvgWinRate: 0m, RobustnessScore: 0m, TotalNetPnl: 0m, MaxDrawdownAcrossWindows: 0m);

        var winRates = windows.Select(w => w.WinRate).ToList();
        var avg = winRates.Average();
        var variance = winRates.Sum(r => (r - avg) * (r - avg)) / winRates.Count;
        var std = (decimal)Math.Sqrt((double)variance);

        return new WalkForwardReport(
            symbol, tf, windowSize, stepSize,
            WindowCount: windows.Count,
            Windows: windows,
            AvgWinRate: avg,
            RobustnessScore: std,
            TotalNetPnl: windows.Sum(w => w.NetPnl),
            MaxDrawdownAcrossWindows: windows.Max(w => w.MaxDrawdown));
    }
}
