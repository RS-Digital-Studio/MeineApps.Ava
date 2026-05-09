using BingXBot.Server.Auth;

namespace BingXBot.Server.Services;

/// <summary>
/// Phase 18 / G3 — Periodischer Cleanup-Job fuer abgelaufene Bearer- und Refresh-Tokens.
/// Default 24h-Tick. Ohne Cleanup waechst <c>tokens.json</c> monoton — bei vielen Refresh-
/// Zyklen pro Device summiert sich das, und beim Pi-Restart wird der ganze File geladen.
/// Konfigurierbar via <c>Server:AuthTokenCleanupIntervalHours</c>.
/// </summary>
public sealed class AuthTokenCleanupService : BackgroundService
{
    private readonly AuthTokenStore _store;
    private readonly ILogger<AuthTokenCleanupService> _logger;
    private readonly TimeSpan _interval;

    public AuthTokenCleanupService(
        AuthTokenStore store,
        IConfiguration config,
        ILogger<AuthTokenCleanupService> logger)
    {
        _store = store;
        _logger = logger;
        var hours = Math.Max(1, config.GetValue<int>("Server:AuthTokenCleanupIntervalHours", 24));
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuthTokenCleanupService gestartet (Intervall {Hours}h)", _interval.TotalHours);

        // Initial-Delay: 1h, damit der Server-Boot nicht sofort einen Cleanup-Tick anstoesst.
        try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purged = _store.PurgeExpired();
                if (purged > 0)
                    _logger.LogInformation("AuthTokenCleanup: {Purged} abgelaufene Tokens entfernt", purged);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AuthTokenCleanup-Tick fehlgeschlagen — naechster Versuch in {Hours}h", _interval.TotalHours);
            }

            try { await Task.Delay(_interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
}
