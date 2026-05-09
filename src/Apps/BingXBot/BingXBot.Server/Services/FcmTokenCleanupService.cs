namespace BingXBot.Server.Services;

/// <summary>
/// Phase 18 / F2 — Periodischer Cleanup-Job fuer stale FCM-Devices.
/// Default: 24h-Tick, entfernt alle Devices die seit &gt; 30 Tagen keinen aktiven Touch mehr hatten.
/// Konfigurierbar via <c>Server:FcmCleanupIntervalHours</c> + <c>Server:FcmStaleAfterDays</c>.
/// Zweck: ohne Cleanup wachsen FCM-Token-Listen monoton; jeder Send-Burst loest dann viele 410-Errors
/// fuer App-Deinstallationen aus.
/// </summary>
public sealed class FcmTokenCleanupService : BackgroundService
{
    private readonly FcmDeviceStore _store;
    private readonly ILogger<FcmTokenCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _staleAfter;

    public FcmTokenCleanupService(
        FcmDeviceStore store,
        IConfiguration config,
        ILogger<FcmTokenCleanupService> logger)
    {
        _store = store;
        _logger = logger;
        var hours = Math.Max(1, config.GetValue<int>("Server:FcmCleanupIntervalHours", 24));
        var days = Math.Max(1, config.GetValue<int>("Server:FcmStaleAfterDays", 30));
        _interval = TimeSpan.FromHours(hours);
        _staleAfter = TimeSpan.FromDays(days);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FcmTokenCleanupService gestartet (Intervall {Hours}h, Stale-Threshold {Days}d)",
            _interval.TotalHours, _staleAfter.TotalDays);

        // Initial-Delay: 1h, damit der Server-Boot nicht sofort einen Cleanup-Tick anstoesst.
        try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { _store.PruneStaleDevices(_staleAfter); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FcmTokenCleanup-Tick fehlgeschlagen — naechster Versuch in {Hours}h", _interval.TotalHours);
            }

            try { await Task.Delay(_interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
}
