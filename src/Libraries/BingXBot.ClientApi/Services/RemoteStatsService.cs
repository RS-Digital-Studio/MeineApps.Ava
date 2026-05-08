using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;

namespace BingXBot.ClientApi.Services;

/// <summary>
/// v1.5.3 Phase 5 — RemoteStatsService: HTTP-Wrapper fuer GET /api/v1/stats/breakdown.
/// </summary>
public sealed class RemoteStatsService : IStatsService
{
    private readonly ServerConnection _connection;

    public RemoteStatsService(ServerConnection connection)
    {
        _connection = connection;
    }

    public Task<StatsBreakdownDto> GetBreakdownAsync(CancellationToken ct = default) =>
        _connection.HttpClient.GetJsonAsync<StatsBreakdownDto>(ApiRoutes.StatsBreakdown, ct);
}
