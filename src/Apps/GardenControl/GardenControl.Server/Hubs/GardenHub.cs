using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace GardenControl.Server.Hubs;

/// <summary>
/// SignalR Hub für Echtzeit-Kommunikation mit den Client-Apps.
///
/// Server → Client Events:
/// - SensorData(SensorDataDto) - periodische Sensorwerte
/// - SystemStatus(SystemStatusDto) - Komplettstatus
/// - WateringStarted(int zoneId) - Bewässerung gestartet
/// - WateringStopped(int zoneId) - Bewässerung gestoppt
///
/// Client → Server Methoden:
/// - GetStatus() - aktuellen Status abrufen
/// - StartWatering(int zoneId, int? duration) - manuell bewässern
/// - StopWatering(int zoneId) - Bewässerung stoppen
/// - EmergencyStop() - Notstopp
/// - SetMode(string mode) - Betriebsmodus ändern
/// </summary>
public class GardenHub : Hub
{
    private readonly IIrrigationService _irrigation;
    private readonly ILogger<GardenHub> _logger;

    public GardenHub(IIrrigationService irrigation, ILogger<GardenHub> logger)
    {
        _irrigation = irrigation;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client verbunden: {ConnectionId}", Context.ConnectionId);

        // Sofort aktuellen Status senden
        var status = await _irrigation.GetStatusAsync();
        await Clients.Caller.SendAsync("SystemStatus", status);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client getrennt: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Aktuellen Systemstatus abrufen</summary>
    public async Task<SystemStatusDto> GetStatus()
        => await _irrigation.GetStatusAsync();

    /// <summary>Manuelle Bewässerung starten</summary>
    public async Task<bool> StartWatering(int zoneId, int? durationSeconds)
    {
        var success = await _irrigation.StartWateringAsync(zoneId, durationSeconds);
        if (success)
        {
            await Clients.All.SendAsync("WateringStarted", zoneId);
            var status = await _irrigation.GetStatusAsync();
            await Clients.All.SendAsync("SystemStatus", status);
        }
        return success;
    }

    /// <summary>Bewässerung einer Zone stoppen</summary>
    public async Task StopWatering(int zoneId)
    {
        await _irrigation.StopWateringAsync(zoneId);
        await Clients.All.SendAsync("WateringStopped", zoneId);
        var status = await _irrigation.GetStatusAsync();
        await Clients.All.SendAsync("SystemStatus", status);
    }

    /// <summary>NOTSTOPP - alles aus</summary>
    public async Task EmergencyStop()
    {
        _logger.LogWarning("NOTSTOPP ausgelöst von Client {ConnectionId}", Context.ConnectionId);
        await _irrigation.EmergencyStopAsync();
        var status = await _irrigation.GetStatusAsync();
        await Clients.All.SendAsync("SystemStatus", status);
    }

    /// <summary>Betriebsmodus ändern (Off, Manual, Automatic)</summary>
    public async Task SetMode(string modeStr)
    {
        if (Enum.TryParse<SystemMode>(modeStr, true, out var mode))
        {
            await _irrigation.SetModeAsync(mode);
            var status = await _irrigation.GetStatusAsync();
            await Clients.All.SendAsync("SystemStatus", status);
        }
    }
}
