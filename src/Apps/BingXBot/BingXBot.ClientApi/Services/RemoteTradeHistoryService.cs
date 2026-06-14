using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;

namespace BingXBot.ClientApi.Services;

public sealed class RemoteTradeHistoryService : ITradeHistoryService
{
    private readonly ServerConnection _connection;

    public RemoteTradeHistoryService(ServerConnection connection)
    {
        _connection = connection;
    }

    public Task<PagedResult<TradeDto>> QueryAsync(TradeQueryDto query, CancellationToken ct = default)
    {
        var qs = new List<string>
        {
            $"page={query.Page}",
            $"pageSize={query.PageSize}"
        };
        if (query.Mode.HasValue) qs.Add($"mode={query.Mode}");
        if (!string.IsNullOrWhiteSpace(query.Symbol)) qs.Add($"symbol={Uri.EscapeDataString(query.Symbol)}");
        if (query.FromUtc.HasValue) qs.Add($"from={Uri.EscapeDataString(query.FromUtc.Value.ToString("O"))}");
        if (query.ToUtc.HasValue) qs.Add($"to={Uri.EscapeDataString(query.ToUtc.Value.ToString("O"))}");

        var path = ApiRoutes.Trades + "?" + string.Join("&", qs);
        return _connection.HttpClient.GetJsonAsync<PagedResult<TradeDto>>(path, ct);
    }

    public async Task<IReadOnlyList<ScannerResultDto>> GetScannerResultsAsync(CancellationToken ct = default)
    {
        var list = await _connection.HttpClient.GetJsonAsync<List<ScannerResultDto>>(ApiRoutes.ScannerResults, ct).ConfigureAwait(false);
        return list;
    }

    public Task<TradeSummaryDto> GetSummaryAsync(Core.Enums.TradingMode? mode, CancellationToken ct = default)
    {
        var path = ApiRoutes.TradesSummary + (mode.HasValue ? $"?mode={mode}" : "");
        return _connection.HttpClient.GetJsonAsync<TradeSummaryDto>(path, ct);
    }
}

public sealed class RemoteBacktestService : IBacktestControlService
{
    private readonly ServerConnection _connection;
    private readonly IBotEventStream _eventStream;

    public event Action<BacktestProgressDto>? ProgressReceived;
    public event Action<BacktestResultDto>? Completed;

    public RemoteBacktestService(ServerConnection connection, IBotEventStream eventStream)
    {
        _connection = connection;
        _eventStream = eventStream;
        _eventStream.BacktestProgress += dto => ProgressReceived?.Invoke(dto);
        _eventStream.BacktestCompleted += dto => Completed?.Invoke(dto);
    }

    public Task<BacktestJobDto> StartAsync(BacktestRequestDto request, CancellationToken ct = default) =>
        _connection.HttpClient.PostJsonAsync<BacktestRequestDto, BacktestJobDto>(ApiRoutes.BacktestStart, request, ct);

    public Task<BacktestStatusDto> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        var path = ApiRoutes.BacktestStatus.Replace("{jobId}", Uri.EscapeDataString(jobId));
        return _connection.HttpClient.GetJsonAsync<BacktestStatusDto>(path, ct);
    }

    public async Task<BacktestResultDto?> GetResultAsync(string jobId, CancellationToken ct = default)
    {
        var path = ApiRoutes.BacktestResult.Replace("{jobId}", Uri.EscapeDataString(jobId));
        try { return await _connection.HttpClient.GetJsonAsync<BacktestResultDto>(path, ct).ConfigureAwait(false); }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
    {
        var path = ApiRoutes.BacktestCancel.Replace("{jobId}", Uri.EscapeDataString(jobId));
        return _connection.HttpClient.PostEmptyAsync(path, ct);
    }
}
