using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Enums;

namespace BingXBot.Server.Api;

public static class BotControlEndpoints
{
    public static void MapBotControlEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.BotStart, async (BotStartRequest req, IBotControlService control, CancellationToken ct) =>
        {
            var status = await control.StartAsync(req, ct);
            return Results.Accepted(value: status);
        });

        app.MapPost(ApiRoutes.BotStop, async (IBotControlService control, CancellationToken ct) =>
        {
            var status = await control.StopAsync(ct);
            return Results.Accepted(value: status);
        });

        app.MapPost(ApiRoutes.BotEmergencyStop, async (IBotControlService control, CancellationToken ct) =>
        {
            var status = await control.EmergencyStopAsync(ct);
            return Results.Accepted(value: status);
        });

        app.MapPost(ApiRoutes.PositionClose, async (string symbol, PositionCloseRequest req, IBotControlService control, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return Results.BadRequest(new ErrorResponse("invalid_request", "Symbol fehlt"));
            await control.ClosePositionAsync(symbol, req.Side, ct);
            return Results.Accepted();
        });
    }
}

public record PositionCloseRequest(Side Side);
