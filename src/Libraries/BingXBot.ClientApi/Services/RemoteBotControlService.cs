using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;

namespace BingXBot.ClientApi.Services;

/// <summary>
/// Remote-Impl von IBotControlService: Leitet Start/Stop/EmergencyStop an den Pi.
/// StatusChanged-Event wird vom RemoteBotEventStream getrieben (BotStateChanged-Push via SignalR).
/// </summary>
public sealed class RemoteBotControlService : IBotControlService, IDisposable
{
    private readonly ServerConnection _connection;
    private readonly IBotEventStream _eventStream;
    private BotStatusDto _lastStatus = CreateEmpty();

    public event Action<BotStatusDto>? StatusChanged;

    public RemoteBotControlService(ServerConnection connection, IBotEventStream eventStream)
    {
        _connection = connection;
        _eventStream = eventStream;
        _eventStream.BotStateChanged += OnStateChanged;
    }

    public void Dispose()
    {
        _eventStream.BotStateChanged -= OnStateChanged;
    }

    public BotStatusDto GetStatus() => _lastStatus;

    public async Task<BotStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        _lastStatus = await _connection.HttpClient.GetJsonAsync<BotStatusDto>(ApiRoutes.Status, ct).ConfigureAwait(false);
        return _lastStatus;
    }

    public async Task<BotStatusDto> StartAsync(BotStartRequest request, CancellationToken ct = default)
    {
        _lastStatus = await _connection.HttpClient.PostJsonAsync<BotStartRequest, BotStatusDto>(ApiRoutes.BotStart, request, ct).ConfigureAwait(false);
        StatusChanged?.Invoke(_lastStatus);
        return _lastStatus;
    }

    public async Task<BotStatusDto> StopAsync(CancellationToken ct = default)
    {
        _lastStatus = await _connection.HttpClient.PostAsync<BotStatusDto>(ApiRoutes.BotStop, ct).ConfigureAwait(false);
        StatusChanged?.Invoke(_lastStatus);
        return _lastStatus;
    }

    public async Task<BotStatusDto> EmergencyStopAsync(CancellationToken ct = default)
    {
        _lastStatus = await _connection.HttpClient.PostAsync<BotStatusDto>(ApiRoutes.BotEmergencyStop, ct).ConfigureAwait(false);
        StatusChanged?.Invoke(_lastStatus);
        return _lastStatus;
    }

    public async Task ClosePositionAsync(string symbol, Side side, CancellationToken ct = default)
    {
        var path = ApiRoutes.PositionClose.Replace("{symbol}", Uri.EscapeDataString(symbol));
        await _connection.HttpClient.PostJsonAsync<object, object>(path, new { side = side.ToString() }, ct).ConfigureAwait(false);
    }

    private void OnStateChanged(BotStateChangedDto dto)
    {
        _lastStatus = _lastStatus with { State = dto.State, Mode = dto.Mode, LastError = dto.Reason };
        StatusChanged?.Invoke(_lastStatus);
    }

    private static BotStatusDto CreateEmpty() => new(
        State: Core.Enums.BotState.Stopped,
        Mode: Core.Enums.TradingMode.Paper,
        IsHedgeMode: false,
        ActiveTimeframes: Array.Empty<Core.Enums.TimeFrame>(),
        UptimeSeconds: 0,
        HasCredentials: false,
        IsConnected: false);
}
