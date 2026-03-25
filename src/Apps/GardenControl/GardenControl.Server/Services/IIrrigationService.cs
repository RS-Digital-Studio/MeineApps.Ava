using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;

namespace GardenControl.Server.Services;

/// <summary>
/// Zentrale Bewässerungslogik - verwaltet Zonen, steuert Ventile/Pumpe,
/// prüft Schwellenwerte und koordiniert automatische Bewässerung.
/// </summary>
public interface IIrrigationService
{
    /// <summary>Initialisiert Hardware und lädt Konfiguration aus DB</summary>
    Task InitializeAsync();

    /// <summary>Startet Bewässerung einer Zone (manuell oder automatisch)</summary>
    Task<bool> StartWateringAsync(int zoneId, int? durationSeconds = null, IrrigationTrigger trigger = IrrigationTrigger.Manual);

    /// <summary>Stoppt Bewässerung einer Zone</summary>
    Task StopWateringAsync(int zoneId);

    /// <summary>Stoppt ALLES (Notfall)</summary>
    Task EmergencyStopAsync();

    /// <summary>Liest alle Sensoren und gibt aktuellen Status zurück</summary>
    Task<SystemStatusDto> GetStatusAsync();

    /// <summary>Liest Sensoren, speichert Werte, prüft Schwellenwerte</summary>
    Task<SensorDataDto> PollSensorsAsync();

    /// <summary>Prüft alle Zonen auf Bewässerungsbedarf (Automatik-Modus)</summary>
    Task CheckAndWaterAsync();

    /// <summary>Aktueller Betriebsmodus</summary>
    SystemMode CurrentMode { get; }

    /// <summary>Betriebsmodus ändern</summary>
    Task SetModeAsync(SystemMode mode);
}
