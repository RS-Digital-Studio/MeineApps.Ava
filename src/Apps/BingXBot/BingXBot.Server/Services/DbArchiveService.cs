using BingXBot.Core.Interfaces;
using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// v1.6.1 Phase 11 — Monatliche DB-Archivierung (Trades + Decisions + Settings-History).
///
/// Laeuft als HostedService, prueft alle 6 h ob ein Archive-Run faellig ist (am 1. des Monats
/// um 04:00 UTC, nach DbBackupService 03:00 UTC). Konfigurierbar:
///   - Server:ArchiveRetentionMonths (Default 12) — Trades aelter werden archiviert
///   - Server:ArchiveDecisionDays (Default 30) — Decisions aelter werden geloescht
///   - Server:ArchiveSettingsDays (Default 90) — Settings-History aelter wird geloescht
///
/// Best-effort — Fehler werden geloggt, Server laeuft weiter. Zweimal-in-Folge-Run ist idempotent.
/// </summary>
public sealed class DbArchiveService : IHostedService, IDisposable
{
    private readonly BotDatabaseService _db;
    private readonly IAppPaths _paths;
    private readonly IConfiguration _config;
    private readonly ILogger<DbArchiveService> _logger;
    private Timer? _timer;
    private DateTime _lastRunUtc = DateTime.MinValue;

    public DbArchiveService(BotDatabaseService db, IAppPaths paths, IConfiguration config, ILogger<DbArchiveService> logger)
    {
        _db = db;
        _paths = paths;
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 6-h-Tick — eng genug damit der 04:00-UTC-Slot am 1. des Monats getroffen wird.
        _timer = new Timer(_ => _ = TryRunAsync(), null, TimeSpan.FromMinutes(15), TimeSpan.FromHours(6));
        _logger.LogInformation("DbArchiveService gestartet (Run am 1. des Monats 04:00 UTC).");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    private async Task TryRunAsync()
    {
        var nowUtc = DateTime.UtcNow;
        // Trigger nur am 1. des Monats zwischen 04:00 und 09:00 UTC, max 1× pro Tag.
        if (nowUtc.Day != 1 || nowUtc.Hour < 4 || nowUtc.Hour >= 10) return;
        if ((nowUtc - _lastRunUtc).TotalHours < 12) return;

        _lastRunUtc = nowUtc;
        try
        {
            var retentionMonths = _config.GetValue<int?>("Server:ArchiveRetentionMonths") ?? 12;
            var settingsDays = _config.GetValue<int?>("Server:ArchiveSettingsDays") ?? 90;

            var tradeCutoff = nowUtc.AddMonths(-retentionMonths);
            var settingsCutoff = nowUtc.AddDays(-settingsDays);

            var archiveDir = Path.Combine(Path.GetDirectoryName(_paths.DatabasePath) ?? ".", "archives");

            var archived = await _db.ArchiveTradesAsync(tradeCutoff, archiveDir).ConfigureAwait(false);
            await _db.PurgeOldSettingsChangesAsync(settingsCutoff).ConfigureAwait(false);

            _logger.LogInformation(
                "DbArchive erfolgreich: {Archived} Trades archiviert (cutoff {TradeCutoff:yyyy-MM-dd}), " +
                "Settings-History gepurged (cutoff {SetCutoff:yyyy-MM-dd}).",
                archived, tradeCutoff, settingsCutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbArchive-Run fehlgeschlagen — Server laeuft weiter.");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
