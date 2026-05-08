using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;

namespace BingXBot.Server.Api;

public static class BacktestEndpoints
{
    public static void MapBacktestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.BacktestStart, async (BacktestRequestDto req, IBacktestControlService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Symbol) || string.IsNullOrWhiteSpace(req.StrategyName))
                return Results.BadRequest(new ErrorResponse("invalid_request", "Symbol/Strategy fehlt"));
            if (req.StartUtc >= req.EndUtc)
                return Results.BadRequest(new ErrorResponse("invalid_request", "Start muss vor End liegen"));

            var job = await svc.StartAsync(req, ct);
            return Results.Accepted(value: job);
        });

        app.MapGet(ApiRoutes.BacktestStatus, async (string jobId, IBacktestControlService svc, CancellationToken ct) =>
        {
            try
            {
                var status = await svc.GetStatusAsync(jobId, ct);
                return Results.Ok(status);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new ErrorResponse("not_found", $"Job {jobId} nicht gefunden"));
            }
        });

        app.MapGet(ApiRoutes.BacktestResult, async (string jobId, IBacktestControlService svc, CancellationToken ct) =>
        {
            var result = await svc.GetResultAsync(jobId, ct);
            if (result == null) return Results.NotFound(new ErrorResponse("not_ready", "Ergebnis noch nicht verfuegbar"));
            return Results.Ok(result);
        });

        app.MapPost(ApiRoutes.BacktestCancel, async (string jobId, IBacktestControlService svc, CancellationToken ct) =>
        {
            await svc.CancelAsync(jobId, ct);
            return Results.NoContent();
        });

        // v1.6.4 Phase 13 — Trade-Replay-Endpoint
        app.MapPost(ApiRoutes.BacktestReplayTrade, async (
            long tradeId,
            BingXBot.Trading.BotDatabaseService db,
            BingXBot.Backtest.BacktestEngine engine,
            BingXBot.Engine.StrategyManager strategyManager,
            BingXBot.Core.Configuration.BotSettings currentSettings,
            CancellationToken ct) =>
        {
            // Trade aus DB holen.
            var allTrades = await db.GetTradesAsync(modeFilter: null, limit: 5000).ConfigureAwait(false);
            // CompletedTrade hat keine Id-Spalte als Domain-Feld — wir nutzen die DB-Reihenfolge:
            // tradeId wird als Index in der ExitTime-DESC-Liste interpretiert (1-basiert).
            var liveTrade = tradeId > 0 && tradeId <= allTrades.Count ? allTrades[(int)(tradeId - 1)] : null;
            if (liveTrade == null)
                return Results.NotFound(new ErrorResponse("not_found", $"Trade #{tradeId} nicht gefunden"));

            // Settings-Snapshot zur Trade-Zeit holen (Phase 14). Fallback: aktuelle Settings.
            var snapSettings = await db.GetSettingsSnapshotAtAsync(liveTrade.EntryTime).ConfigureAwait(false);
            var effectiveSettings = snapSettings ?? currentSettings;

            var runner = new BingXBot.Backtest.TradeReplayRunner(engine);
            var report = await runner.ReplayAsync(
                liveTrade,
                effectiveSettings,
                strategyFactory: () => strategyManager.GetOrCreateForSymbol(liveTrade.Symbol, liveTrade.NavigatorTimeframe),
                riskManagerFactory: () => new BingXBot.Engine.Risk.RiskManager(effectiveSettings.Risk,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<BingXBot.Engine.Risk.RiskManager>.Instance),
                ct: ct).ConfigureAwait(false);

            var dto = new TradeReplayResultDto(
                LiveTradeId: tradeId,
                Symbol: liveTrade.Symbol,
                EntryTimeUtc: liveTrade.EntryTime,
                EntryPriceDriftPercent: report.EntryPriceDriftPercent,
                PnlDriftPercent: report.PnlDriftPercent,
                ExitReasonSame: report.ExitReasonSame,
                Verdict: report.Verdict.ToString(),
                ErrorDetail: report.ErrorDetail);
            return Results.Ok(dto);
        });
    }
}
