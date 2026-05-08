using System.Collections.Concurrent;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Trading.Stats;

namespace BingXBot.Server.Services;

/// <summary>
/// v1.6.6 Phase 17 — Adaptive TF-Disable.
///
/// Liest <see cref="TradeStatsAggregator"/> alle 60 Minuten und disabled TFs deren WinRate
/// unter <see cref="ScannerSettings.AdaptiveTfMinWinRate"/> liegt UND deren Trade-Count
/// >= <see cref="ScannerSettings.AdaptiveTfMinTrades"/> ist. Disable-Periode aus
/// <see cref="ScannerSettings.AdaptiveTfDisableHours"/>. Re-Probing nach Ablauf.
///
/// Public Snapshot via <see cref="IsTfDisabled"/> + <see cref="GetDisabledUntil"/> — der
/// Scanner-Pfad in der Engine kann das nutzen, um disablete TFs aus dem Scan-Loop auszuklammern.
/// Default-State: Service-Singleton hat ein leeres Disable-Set (kein TF disabled).
/// </summary>
public sealed class AdaptiveTfDisableService : IHostedService, IDisposable
{
    private readonly TradeStatsAggregator _aggregator;
    private readonly ScannerSettings _scannerSettings;
    private readonly ILogger<AdaptiveTfDisableService> _logger;
    private Timer? _timer;
    private readonly ConcurrentDictionary<TimeFrame, DateTime> _disabledUntil = new();

    public AdaptiveTfDisableService(
        TradeStatsAggregator aggregator,
        ScannerSettings scannerSettings,
        ILogger<AdaptiveTfDisableService> logger)
    {
        _aggregator = aggregator;
        _scannerSettings = scannerSettings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => Run(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(60));
        _logger.LogInformation("AdaptiveTfDisableService gestartet (60-min-Tick).");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    /// <summary>True wenn die TF aktuell auto-disabled ist (Skip-Filter im Scanner).</summary>
    public bool IsTfDisabled(TimeFrame tf)
    {
        if (!_disabledUntil.TryGetValue(tf, out var until)) return false;
        if (DateTime.UtcNow >= until)
        {
            _disabledUntil.TryRemove(tf, out _);
            return false;
        }
        return true;
    }

    /// <summary>Cutoff-Zeit fuer Disable-Periode (UTC). Null wenn nicht disabled.</summary>
    public DateTime? GetDisabledUntil(TimeFrame tf) =>
        _disabledUntil.TryGetValue(tf, out var until) && DateTime.UtcNow < until ? until : null;

    /// <summary>Public fuer Tests + manuelle CLI-Trigger.</summary>
    public void Run()
    {
        if (!_scannerSettings.EnableAdaptiveTfDisable) return;

        try
        {
            var snapshot = _aggregator.GetSnapshot();
            var nowUtc = DateTime.UtcNow;
            var disableUntil = nowUtc.AddHours(_scannerSettings.AdaptiveTfDisableHours);
            var minTrades = _scannerSettings.AdaptiveTfMinTrades;
            var minWinRate = _scannerSettings.AdaptiveTfMinWinRate;

            // Aggregate per TF (Modes/Categories werden zusammengefasst — wir wollen TF-Verhalten,
            // nicht Markt-Verhalten).
            var byTf = snapshot
                .GroupBy(s => s.NavigatorTimeframe)
                .ToDictionary(g => g.Key, g => new
                {
                    Trades = g.Sum(x => x.TotalTrades),
                    Wins = g.Sum(x => x.WinTrades),
                });

            foreach (var (tf, data) in byTf)
            {
                if (data.Trades < minTrades) continue;
                var winRate = (decimal)data.Wins / data.Trades;
                if (winRate < minWinRate)
                {
                    if (!_disabledUntil.TryGetValue(tf, out var existing) || existing < nowUtc)
                    {
                        _disabledUntil[tf] = disableUntil;
                        _logger.LogWarning(
                            "AdaptiveTfDisable: {Tf} disabled bis {Until:HH:mm} UTC (WinRate {Wr:P1} < {Threshold:P0}, {Trades} Trades)",
                            tf, disableUntil, winRate, minWinRate, data.Trades);
                    }
                }
            }

            // Re-Enable abgelaufene TFs.
            foreach (var kvp in _disabledUntil.ToList())
            {
                if (nowUtc >= kvp.Value)
                {
                    _disabledUntil.TryRemove(kvp.Key, out _);
                    _logger.LogInformation("AdaptiveTfDisable: {Tf} re-enabled (24h Periode abgelaufen).", kvp.Key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AdaptiveTfDisableService-Iteration fehlgeschlagen");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
