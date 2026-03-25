using GardenControl.Core.DTOs;
using GardenControl.Core.Models;

namespace GardenControl.Shared.Services;

/// <summary>
/// REST-API Client für Konfiguration, Verlaufsdaten und Befehle.
/// </summary>
public interface IApiService
{
    /// <summary>Server-URL setzen</summary>
    void SetServerUrl(string url);

    /// <summary>Verbindung testen</summary>
    Task<bool> TestConnectionAsync();

    /// <summary>Alle Zonen abrufen</summary>
    Task<List<Zone>> GetZonesAsync();

    /// <summary>Zone-Konfiguration aktualisieren</summary>
    Task<Zone?> UpdateZoneAsync(ZoneConfigDto config);

    /// <summary>Kalibrierung durchführen (trocken/nass)</summary>
    Task<Zone?> CalibrateAsync(int zoneId, string type);

    /// <summary>Verlaufsdaten (Messwerte) abrufen</summary>
    Task<List<SensorReading>> GetReadingsAsync(int? zoneId, DateTime from, DateTime to, int maxResults = 1000);

    /// <summary>Bewässerungsereignisse abrufen</summary>
    Task<List<IrrigationEvent>> GetEventsAsync(int? zoneId, DateTime from, DateTime to, int maxResults = 100);

    /// <summary>Systemkonfiguration abrufen</summary>
    Task<Dictionary<string, string>> GetConfigAsync();

    /// <summary>Systemkonfiguration aktualisieren</summary>
    Task UpdateConfigAsync(SystemConfigDto config);

    /// <summary>Zeitpläne abrufen (optional nach Zone filtern)</summary>
    Task<List<IrrigationSchedule>> GetSchedulesAsync(int? zoneId = null);

    /// <summary>Zeitplan erstellen oder aktualisieren</summary>
    Task<IrrigationSchedule?> SaveScheduleAsync(IrrigationSchedule schedule);

    /// <summary>Zeitplan löschen</summary>
    Task<bool> DeleteScheduleAsync(int id);

    /// <summary>Wird bei API-Fehlern gefeuert (für UI-Feedback)</summary>
    event Action<string>? ErrorOccurred;
}
