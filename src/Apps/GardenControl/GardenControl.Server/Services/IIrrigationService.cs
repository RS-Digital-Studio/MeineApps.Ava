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

    /// <summary>
    /// Liest alle Sensoren und gibt aktuellen Status zurück.
    /// Optional kann ein bereits im selben Zyklus gelesener Sensor-Snapshot übergeben werden,
    /// um redundante ADC-Reads zu vermeiden (Schlüssel = ZoneId, Wert = ADC-Rohwert).
    /// Ohne Snapshot wird wie bisher pro Zone frisch gelesen.
    /// </summary>
    Task<SystemStatusDto> GetStatusAsync(IReadOnlyDictionary<int, int>? sensorSnapshot = null);

    /// <summary>
    /// Liest Sensoren, speichert Werte und gibt zusätzlich einen Snapshot der gelesenen
    /// ADC-Rohwerte zurück (Schlüssel = ZoneId), damit derselbe Messwert im restlichen
    /// Zyklus wiederverwendet werden kann.
    /// </summary>
    Task<(SensorDataDto Data, IReadOnlyDictionary<int, int> Snapshot)> PollSensorsAsync();

    /// <summary>
    /// Prüft alle Zonen auf Bewässerungsbedarf (Automatik-Modus).
    /// Optional kann ein bereits im selben Zyklus gelesener Sensor-Snapshot übergeben werden,
    /// um redundante ADC-Reads zu vermeiden. Ohne Snapshot wird pro Zone frisch gelesen.
    /// </summary>
    Task CheckAndWaterAsync(IReadOnlyDictionary<int, int>? sensorSnapshot = null);

    /// <summary>
    /// Aktualisiert die gecachten Wetterdaten (gecacht im WeatherService, max. alle 30 Min.).
    /// Wird aus einem eigenen langsamen Loop aufgerufen, damit der Status-/Poll-Hot-Path
    /// nicht durch blockierende HTTP-Calls verzögert wird. Exceptions werden geschluckt.
    /// </summary>
    Task RefreshWeatherAsync();

    /// <summary>Aktueller Betriebsmodus</summary>
    SystemMode CurrentMode { get; }

    /// <summary>Betriebsmodus ändern</summary>
    Task SetModeAsync(SystemMode mode);
}
