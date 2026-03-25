using GardenControl.Core.Models;
using SQLite;

namespace GardenControl.Server.Services;

/// <summary>
/// SQLite-Datenbank auf dem Raspberry Pi.
/// Speichert Sensorverlauf, Bewässerungsereignisse, Zonen-Konfiguration.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private SQLiteAsyncConnection? _db;
    private readonly string _dbPath;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration config)
    {
        _logger = logger;
        _dbPath = config.GetValue<string>("Database:Path") ?? "garden.db";
    }

    public async Task InitializeAsync()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _db = new SQLiteAsyncConnection(_dbPath);

        await _db.CreateTableAsync<Zone>();
        await _db.CreateTableAsync<SensorReading>();
        await _db.CreateTableAsync<IrrigationEvent>();
        await _db.CreateTableAsync<IrrigationSchedule>();
        await _db.CreateTableAsync<SystemConfigEntry>();

        // Standard-Zonen anlegen wenn DB leer
        var zones = await _db.Table<Zone>().CountAsync();
        if (zones == 0)
        {
            await _db.InsertAsync(new Zone
            {
                Name = "Beet 1", SensorChannel = 0, RelayGpioPin = 17,
                ThresholdPercent = 40, WateringDurationSeconds = 30
            });
            await _db.InsertAsync(new Zone
            {
                Name = "Beet 2", SensorChannel = 1, RelayGpioPin = 27,
                ThresholdPercent = 40, WateringDurationSeconds = 30
            });
            await _db.InsertAsync(new Zone
            {
                Name = "Beet 3", SensorChannel = 2, RelayGpioPin = 22,
                ThresholdPercent = 40, WateringDurationSeconds = 30
            });

            _logger.LogInformation("3 Standard-Zonen angelegt");
        }

        // Verwaiste Bewässerungsereignisse schließen (Pi-Absturz während Bewässerung)
        var orphanedEvents = await _db.ExecuteAsync(
            "UPDATE IrrigationEvents SET EndedAtUtc = ?, ActualDurationSeconds = PlannedDurationSeconds " +
            "WHERE EndedAtUtc IS NULL", DateTime.UtcNow);
        if (orphanedEvents > 0)
            _logger.LogWarning("{Count} verwaiste Bewässerungsereignisse nach Neustart geschlossen", orphanedEvents);

        // Standard-Konfiguration
        await SetConfigDefaultAsync(ConfigKeys.PollIntervalSeconds, "30");
        await SetConfigDefaultAsync(ConfigKeys.SystemMode, "Manual");
        await SetConfigDefaultAsync(ConfigKeys.PumpGpioPin, "23");
        await SetConfigDefaultAsync(ConfigKeys.HistoryRetentionDays, "30");
        await SetConfigDefaultAsync(ConfigKeys.MinPauseBetweenWateringMinutes, "30");

        _logger.LogInformation("Datenbank initialisiert: {Path}", _dbPath);
    }

    // --- Zonen ---

    public async Task<List<Zone>> GetZonesAsync()
        => await _db!.Table<Zone>().ToListAsync();

    public async Task<Zone?> GetZoneAsync(int id)
        => await _db!.FindAsync<Zone>(id);

    public async Task SaveZoneAsync(Zone zone)
    {
        if (zone.Id == 0)
            await _db!.InsertAsync(zone);
        else
            await _db!.UpdateAsync(zone);
    }

    // --- Sensorwerte ---

    public async Task SaveReadingAsync(SensorReading reading)
        => await _db!.InsertAsync(reading);

    public async Task<List<SensorReading>> GetReadingsAsync(
        int? zoneId, DateTime fromUtc, DateTime toUtc, int maxResults = 1000)
    {
        var query = _db!.Table<SensorReading>()
            .Where(r => r.TimestampUtc >= fromUtc && r.TimestampUtc <= toUtc);

        if (zoneId.HasValue)
            query = query.Where(r => r.ZoneId == zoneId.Value);

        return await query
            .OrderByDescending(r => r.TimestampUtc)
            .Take(maxResults)
            .ToListAsync();
    }

    // --- Bewässerungsereignisse ---

    public async Task SaveEventAsync(IrrigationEvent evt)
    {
        if (evt.Id == 0)
            await _db!.InsertAsync(evt);
        else
            await _db!.UpdateAsync(evt);
    }

    public async Task<IrrigationEvent?> GetActiveEventAsync(int zoneId)
        => await _db!.Table<IrrigationEvent>()
            .Where(e => e.ZoneId == zoneId && e.EndedAtUtc == null)
            .OrderByDescending(e => e.StartedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<List<IrrigationEvent>> GetEventsAsync(
        int? zoneId, DateTime fromUtc, DateTime toUtc, int maxResults = 100)
    {
        var query = _db!.Table<IrrigationEvent>()
            .Where(e => e.StartedAtUtc >= fromUtc && e.StartedAtUtc <= toUtc);

        if (zoneId.HasValue)
            query = query.Where(e => e.ZoneId == zoneId.Value);

        return await query
            .OrderByDescending(e => e.StartedAtUtc)
            .Take(maxResults)
            .ToListAsync();
    }

    // --- Konfiguration ---

    public async Task<string?> GetConfigAsync(string key)
    {
        var entry = await _db!.FindAsync<SystemConfigEntry>(key);
        return entry?.Value;
    }

    public async Task SetConfigAsync(string key, string value)
    {
        var entry = await _db!.FindAsync<SystemConfigEntry>(key);
        if (entry != null)
        {
            entry.Value = value;
            await _db!.UpdateAsync(entry);
        }
        else
        {
            await _db!.InsertAsync(new SystemConfigEntry { Key = key, Value = value });
        }
    }

    public async Task<Dictionary<string, string>> GetAllConfigAsync()
    {
        var entries = await _db!.Table<SystemConfigEntry>().ToListAsync();
        return entries.ToDictionary(e => e.Key, e => e.Value);
    }

    // --- Zeitpläne ---

    public async Task<List<IrrigationSchedule>> GetSchedulesAsync(int? zoneId = null)
    {
        var query = _db!.Table<IrrigationSchedule>();
        if (zoneId.HasValue)
            query = query.Where(s => s.ZoneId == zoneId.Value);
        return await query.ToListAsync();
    }

    public async Task<IrrigationSchedule?> GetScheduleAsync(int id)
        => await _db!.FindAsync<IrrigationSchedule>(id);

    public async Task SaveScheduleAsync(IrrigationSchedule schedule)
    {
        if (schedule.Id == 0)
            await _db!.InsertAsync(schedule);
        else
            await _db!.UpdateAsync(schedule);
    }

    public async Task DeleteScheduleAsync(int id)
        => await _db!.DeleteAsync<IrrigationSchedule>(id);

    // --- Wartung ---

    public async Task CleanupOldDataAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var deletedReadings = await _db!.ExecuteAsync(
            "DELETE FROM SensorReadings WHERE TimestampUtc < ?", cutoff);
        var deletedEvents = await _db!.ExecuteAsync(
            "DELETE FROM IrrigationEvents WHERE StartedAtUtc < ?", cutoff);

        if (deletedReadings > 0 || deletedEvents > 0)
        {
            _logger.LogInformation("Alte Daten bereinigt: {Readings} Messwerte, {Events} Ereignisse gelöscht",
                deletedReadings, deletedEvents);
        }
    }

    private async Task SetConfigDefaultAsync(string key, string defaultValue)
    {
        var existing = await _db!.FindAsync<SystemConfigEntry>(key);
        if (existing == null)
        {
            await _db!.InsertAsync(new SystemConfigEntry { Key = key, Value = defaultValue });
        }
    }
}
