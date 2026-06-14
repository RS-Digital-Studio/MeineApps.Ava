using BingXBot.Contracts.Dto;

namespace BingXBot.Contracts.Services;

/// <summary>
/// Backtest-Steuerung: Job-Start + Progress-Polling + Result.
/// Im Server: Wrappt BacktestEngine (startet Job im BackgroundService).
/// Im Client: HTTP + Progress-Events via SignalR.
/// </summary>
public interface IBacktestControlService
{
    Task<BacktestJobDto> StartAsync(BacktestRequestDto request, CancellationToken ct = default);
    Task<BacktestStatusDto> GetStatusAsync(string jobId, CancellationToken ct = default);
    Task<BacktestResultDto?> GetResultAsync(string jobId, CancellationToken ct = default);
    Task CancelAsync(string jobId, CancellationToken ct = default);

    event Action<BacktestProgressDto>? ProgressReceived;
    event Action<BacktestResultDto>? Completed;
}
