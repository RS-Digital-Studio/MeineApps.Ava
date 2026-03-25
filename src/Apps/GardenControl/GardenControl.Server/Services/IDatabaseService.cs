using GardenControl.Core.Models;

namespace GardenControl.Server.Services;

/// <summary>
/// SQLite-Datenbank für Sensorverlauf, Bewässerungsereignisse und Konfiguration.
/// Datei: /home/pi/gardencontrol/garden.db
/// </summary>
public interface IDatabaseService
{
    Task InitializeAsync();

    // Zonen
    Task<List<Zone>> GetZonesAsync();
    Task<Zone?> GetZoneAsync(int id);
    Task SaveZoneAsync(Zone zone);

    // Sensorwerte
    Task SaveReadingAsync(SensorReading reading);
    Task<List<SensorReading>> GetReadingsAsync(int? zoneId, DateTime fromUtc, DateTime toUtc, int maxResults = 1000);

    // Bewässerungsereignisse
    Task SaveEventAsync(IrrigationEvent evt);
    Task<IrrigationEvent?> GetActiveEventAsync(int zoneId);
    Task<List<IrrigationEvent>> GetEventsAsync(int? zoneId, DateTime fromUtc, DateTime toUtc, int maxResults = 100);

    // Konfiguration
    Task<string?> GetConfigAsync(string key);
    Task SetConfigAsync(string key, string value);
    Task<Dictionary<string, string>> GetAllConfigAsync();

    // Zeitpläne
    Task<List<IrrigationSchedule>> GetSchedulesAsync(int? zoneId = null);
    Task<IrrigationSchedule?> GetScheduleAsync(int id);
    Task SaveScheduleAsync(IrrigationSchedule schedule);
    Task DeleteScheduleAsync(int id);

    // Wartung
    Task CleanupOldDataAsync(int retentionDays);
}
