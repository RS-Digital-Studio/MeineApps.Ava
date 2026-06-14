using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;

namespace BingXBot.ClientApi.Services;

// 24.04.2026 Phase-4-Audit m4: Push-Events aus IAccountService entfernt — sie waren tot:
// - Server (LocalAccountService) hatte keinen Producer fuer die 4 Events
// - Client (hier) verdrahtete IBotEventStream → IAccountService-Events, aber niemand subscribte
// Konsumenten muessen direkt auf <see cref="IBotEventStream"/> subscribieren (gleiche DTOs,
// einziger Update-Pfad, kein Wrapping mehr). Reduziert Wiring-Komplexitaet und Memory-Leaks
// durch vergessene Event-Unsubscribes.
public sealed class RemoteAccountService : IAccountService
{
    private readonly ServerConnection _connection;

    public RemoteAccountService(ServerConnection connection)
    {
        _connection = connection;
    }

    public Task<AccountSnapshotDto> GetSnapshotAsync(CancellationToken ct = default) =>
        _connection.HttpClient.GetJsonAsync<AccountSnapshotDto>(ApiRoutes.Account, ct);

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken ct = default)
    {
        var list = await _connection.HttpClient.GetJsonAsync<List<PositionDto>>(ApiRoutes.Positions, ct).ConfigureAwait(false);
        return list;
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        var path = ApiRoutes.OpenOrders + (string.IsNullOrWhiteSpace(symbol) ? "" : $"?symbol={Uri.EscapeDataString(symbol)}");
        var list = await _connection.HttpClient.GetJsonAsync<List<OpenOrderDto>>(path, ct).ConfigureAwait(false);
        return list;
    }

    public async Task<IReadOnlyList<EquityPointDto>> GetEquityCurveAsync(int hoursBack = 24, CancellationToken ct = default)
    {
        var path = ApiRoutes.Equity + $"?hours={hoursBack}";
        var list = await _connection.HttpClient.GetJsonAsync<List<EquityPointDto>>(path, ct).ConfigureAwait(false);
        return list;
    }

    public Task<CredentialsStatusDto> GetCredentialsStatusAsync(CancellationToken ct = default) =>
        _connection.HttpClient.GetJsonAsync<CredentialsStatusDto>(ApiRoutes.CredentialsStatus, ct);

    public Task SetCredentialsAsync(SetCredentialsRequest request, CancellationToken ct = default) =>
        _connection.HttpClient.PutJsonAsync(ApiRoutes.Credentials, request, ct);
}
