using GardenControl.Core.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace GardenControl.Shared.Services;

/// <summary>
/// SignalR-Client für Echtzeit-Kommunikation mit dem Pi-Server.
/// Automatischer Reconnect bei Verbindungsverlust.
/// </summary>
public class ConnectionService : IConnectionService
{
    private HubConnection? _hub;
    private bool _disposed;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public string? ServerUrl { get; private set; }

    public event Action<SensorDataDto>? SensorDataReceived;
    public event Action<SystemStatusDto>? SystemStatusReceived;
    public event Action<int>? WateringStarted;
    public event Action<int>? WateringStopped;
    public event Action<bool>? ConnectionChanged;

    public async Task ConnectAsync(string serverUrl)
    {
        // Bestehende Verbindung trennen
        if (_hub != null)
            await DisconnectAsync();

        ServerUrl = serverUrl.TrimEnd('/');
        var hubUrl = $"{ServerUrl}/hub/garden";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30) })
            .Build();

        // Events registrieren
        _hub.On<SensorDataDto>("SensorData", data =>
            SensorDataReceived?.Invoke(data));

        _hub.On<SystemStatusDto>("SystemStatus", status =>
            SystemStatusReceived?.Invoke(status));

        _hub.On<int>("WateringStarted", zoneId =>
            WateringStarted?.Invoke(zoneId));

        _hub.On<int>("WateringStopped", zoneId =>
            WateringStopped?.Invoke(zoneId));

        // Verbindungsstatus
        _hub.Reconnecting += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hub.Reconnected += _ =>
        {
            ConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        _hub.Closed += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        await _hub.StartAsync();
        ConnectionChanged?.Invoke(true);
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
            _hub = null;
            ConnectionChanged?.Invoke(false);
        }
    }

    public async Task<SystemStatusDto?> GetStatusAsync()
    {
        if (_hub == null || !IsConnected) return null;
        return await _hub.InvokeAsync<SystemStatusDto>("GetStatus");
    }

    public async Task<bool> StartWateringAsync(int zoneId, int? durationSeconds = null)
    {
        if (_hub == null || !IsConnected) return false;
        return await _hub.InvokeAsync<bool>("StartWatering", zoneId, durationSeconds);
    }

    public async Task StopWateringAsync(int zoneId)
    {
        if (_hub == null || !IsConnected) return;
        await _hub.InvokeAsync("StopWatering", zoneId);
    }

    public async Task EmergencyStopAsync()
    {
        if (_hub == null || !IsConnected) return;
        await _hub.InvokeAsync("EmergencyStop");
    }

    public async Task SetModeAsync(string mode)
    {
        if (_hub == null || !IsConnected) return;
        await _hub.InvokeAsync("SetMode", mode);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}
