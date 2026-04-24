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
    }
}
