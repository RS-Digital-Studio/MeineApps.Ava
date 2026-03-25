using GardenControl.Core.DTOs;

namespace GardenControl.Shared.Services;

/// <summary>
/// SignalR-Verbindung zum GardenControl-Server auf dem Raspberry Pi.
/// Empfängt Echtzeit-Sensorwerte und Statusupdates.
/// </summary>
public interface IConnectionService : IAsyncDisposable
{
    /// <summary>Verbindung zum Server herstellen</summary>
    Task ConnectAsync(string serverUrl);

    /// <summary>Verbindung trennen</summary>
    Task DisconnectAsync();

    /// <summary>Ist die Verbindung aktiv?</summary>
    bool IsConnected { get; }

    /// <summary>Verbindungs-URL</summary>
    string? ServerUrl { get; }

    // --- Events ---

    /// <summary>Neue Sensordaten empfangen</summary>
    event Action<SensorDataDto>? SensorDataReceived;

    /// <summary>Systemstatus-Update empfangen</summary>
    event Action<SystemStatusDto>? SystemStatusReceived;

    /// <summary>Bewässerung einer Zone gestartet</summary>
    event Action<int>? WateringStarted;

    /// <summary>Bewässerung einer Zone gestoppt</summary>
    event Action<int>? WateringStopped;

    /// <summary>Verbindungsstatus geändert</summary>
    event Action<bool>? ConnectionChanged;

    // --- Befehle (via SignalR) ---

    /// <summary>Aktuellen Status abrufen</summary>
    Task<SystemStatusDto?> GetStatusAsync();

    /// <summary>Manuelle Bewässerung starten</summary>
    Task<bool> StartWateringAsync(int zoneId, int? durationSeconds = null);

    /// <summary>Bewässerung stoppen</summary>
    Task StopWateringAsync(int zoneId);

    /// <summary>Notstopp</summary>
    Task EmergencyStopAsync();

    /// <summary>Betriebsmodus ändern</summary>
    Task SetModeAsync(string mode);
}
