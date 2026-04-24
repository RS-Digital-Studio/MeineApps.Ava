using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: Taegliches Backup der bot.db (rotierend, 7 Tage Retention). Schuetzt vor
/// SD-Karten-Korruption auf dem Pi. Backup-Ordner: <c>{DataDirectory}/backups/bot-YYYY-MM-DD.db</c>.
///
/// Zeitplan: Initial-Delay bis 03:00 UTC (Low-Activity-Zeit, Server-Neustarts haeufig gegen Mitternacht
/// lokal), danach alle 24 h. Best-Effort — wenn ein Backup fehlschlaegt, wird nur geloggt, der Server
/// laeuft weiter.
///
/// Konfiguration via <c>Server:BackupRetentionDays</c> (Default 7), <c>Server:DataDirectory</c>
/// (Basis-Pfad, gleicher wie die DB).
/// </summary>
public sealed class DbBackupService : BackgroundService
{
    private readonly BotDatabaseService _db;
    private readonly IConfiguration _config;
    private readonly ILogger<DbBackupService> _logger;
    private readonly int _retentionDays;

    public DbBackupService(BotDatabaseService db, IConfiguration config, ILogger<DbBackupService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _retentionDays = Math.Max(1, config.GetValue<int>("Server:BackupRetentionDays", 7));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbBackupService gestartet (Retention {Days} Tage)", _retentionDays);

        // Erster Delay bis 03:00 UTC, oder 5 Minuten wenn wir heute schon drueber sind
        // (dann wartet der Service bis morgen 03:00 UTC via TimeSpan.FromHours(24) Loop).
        var firstDelay = CalculateDelayUntilNextBackup();
        _logger.LogInformation("Naechstes DB-Backup in {Hours:F1} h", firstDelay.TotalHours);

        try { await Task.Delay(firstDelay, stoppingToken).ConfigureAwait(false); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunBackupOnceAsync().ConfigureAwait(false);

            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
        }
    }

    private static TimeSpan CalculateDelayUntilNextBackup()
    {
        var now = DateTime.UtcNow;
        var today03 = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0, DateTimeKind.Utc);
        var target = now < today03 ? today03 : today03.AddDays(1);
        return target - now;
    }

    private async Task RunBackupOnceAsync()
    {
        try
        {
            var dbPath = _db.ResolveDatabasePath();
            var backupDir = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", "backups");
            Directory.CreateDirectory(backupDir);

            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var targetPath = Path.Combine(backupDir, $"bot-{stamp}.db");

            await _db.BackupAsync(targetPath).ConfigureAwait(false);

            var info = new FileInfo(targetPath);
            _logger.LogInformation("DB-Backup geschrieben: {Path} ({Size:F1} MB)", targetPath, info.Length / 1024d / 1024d);

            PruneOldBackups(backupDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB-Backup fehlgeschlagen — naechster Versuch in 24 h");
        }
    }

    private void PruneOldBackups(string backupDir)
    {
        try
        {
            var files = Directory.GetFiles(backupDir, "bot-*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToList();

            // Neueste _retentionDays behalten, Rest loeschen.
            foreach (var old in files.Skip(_retentionDays))
            {
                try
                {
                    old.Delete();
                    _logger.LogDebug("Alter Backup geloescht: {Name}", old.Name);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Backup-Loesch fehlgeschlagen: {Name}", old.Name); }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Backup-Retention-Cleanup fehlgeschlagen"); }
    }
}
