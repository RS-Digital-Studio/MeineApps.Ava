using System.Collections.Concurrent;
using BingXBot.Core.Configuration;
using BingXBot.Core.Models;
using BingXBot.Trading;
using CoreLogLevel = BingXBot.Core.Enums.LogLevel;

namespace BingXBot.Server.Services;

/// <summary>
/// HostedService: persistiert <see cref="BotEventBus.LogEmitted"/>-Events in die
/// <c>LogEntries</c>-Tabelle der bot.db.
///
/// Snapshot-Report-Fix Befund 1 / A0.3: Vorher schrieb NIEMAND die EventBus-Logs in die DB —
/// die <c>LogEntries</c>-Tabelle hatte 0 Zeilen, obwohl der Bot durchlaufen Tausende Logs produzierte.
/// <c>LogBufferService</c> haelt nur einen In-Memory-Ringpuffer (Reset bei jedem systemd-Restart).
///
/// Architektur:
/// - Subscribed <see cref="BotEventBus.LogEmitted"/> synchron, schiebt jeden Eintrag in eine
///   <see cref="ConcurrentQueue{T}"/>. Synchron schiebt nur ein Pointer rein → kein Hot-Path-Block.
/// - Background-Loop draint die Queue alle 250 ms in Batches und persistiert via
///   <see cref="BotDatabaseService.SaveLogAsync"/>.
/// - Filter via <see cref="BotSettings.DbLogPersistenceMinLevel"/> (Default Info) — Debug/Trace
///   landen NICHT in der DB, sonst flutet ein einzelner Scan-Loop die Tabelle binnen Stunden.
/// - Settings-gated via <see cref="BotSettings.EnableDbLogPersistence"/> — User kann global aus.
/// - Hard-Cap: Queue auf 10.000 Eintraege begrenzt. Bei Overflow werden aelteste verworfen
///   (BotDb-Write soll keinen OOM erzeugen wenn die DB langsam wird).
/// </summary>
public sealed class DbLogPersistenceService : BackgroundService
{
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService _db;
    private readonly BotSettings _botSettings;
    private readonly ILogger<DbLogPersistenceService> _logger;
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private const int MaxQueueSize = 10_000;
    private const int DrainBatchSize = 200;
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(250);

    public DbLogPersistenceService(
        BotEventBus eventBus,
        BotDatabaseService db,
        BotSettings botSettings,
        ILogger<DbLogPersistenceService> logger)
    {
        _eventBus = eventBus;
        _db = db;
        _botSettings = botSettings;
        _logger = logger;
        _eventBus.LogEmitted += OnLogEmitted;
    }

    private void OnLogEmitted(object? sender, LogEntry entry)
    {
        if (!_botSettings.EnableDbLogPersistence) return;
        if ((int)entry.Level < (int)_botSettings.DbLogPersistenceMinLevel) return;

        // Hard-Cap: bei Overflow den aeltesten Eintrag verwerfen, damit die Queue nicht unbegrenzt waechst.
        // ConcurrentQueue.Count ist O(1) in modernen .NET-Runtimes.
        while (_queue.Count >= MaxQueueSize && _queue.TryDequeue(out _)) { }
        _queue.Enqueue(entry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbLogPersistenceService gestartet (MinLevel {Level}, Drain {Ms} ms)",
            _botSettings.DbLogPersistenceMinLevel, DrainInterval.TotalMilliseconds);
        using var timer = new PeriodicTimer(DrainInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (!_botSettings.EnableDbLogPersistence)
                {
                    // Toggle wurde umgelegt — Queue droppen damit nichts mehr persistiert wird.
                    while (_queue.TryDequeue(out _)) { }
                    continue;
                }
                await DrainOnceAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* Shutdown */ }

        // Letzter Drain beim Shutdown, damit Reboot-Logs nicht verloren gehen.
        try { await DrainOnceAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
    }

    private async Task DrainOnceAsync()
    {
        int written = 0;
        while (written < DrainBatchSize && _queue.TryDequeue(out var entry))
        {
            try
            {
                await _db.SaveLogAsync(entry).ConfigureAwait(false);
                written++;
            }
            catch (Exception ex)
            {
                // DB-Fehler: nicht weiterversuchen mit demselben Eintrag, sonst Endlos-Loop.
                // Wir loggen es aus dem .NET-Logger (NICHT ueber EventBus.PublishLog — sonst Re-Entry).
                _logger.LogWarning(ex, "DB-Log-Persist fehlgeschlagen, Eintrag verworfen");
            }
        }
    }

    public override void Dispose()
    {
        _eventBus.LogEmitted -= OnLogEmitted;
        base.Dispose();
    }
}
