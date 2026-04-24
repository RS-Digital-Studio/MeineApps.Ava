using BingXBot.Contracts.Dto;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Trade-Historie + Scanner-Resultate.
/// Im Server: Liest aus BotDatabaseService + Scanner-Cache.
/// Im Client: HTTP GET.
/// </summary>
public interface ITradeHistoryService
{
    Task<PagedResult<TradeDto>> QueryAsync(TradeQueryDto query, CancellationToken ct = default);

    /// <summary>Letzte Scanner-Resultate (pro Modus) — kommt vom Cache, nicht aus DB.</summary>
    Task<IReadOnlyList<ScannerResultDto>> GetScannerResultsAsync(CancellationToken ct = default);
}
