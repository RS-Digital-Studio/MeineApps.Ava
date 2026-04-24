using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace BingXBot.Server.Api;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Status, async (IBotControlService control, CancellationToken ct) =>
        {
            var status = await control.GetStatusAsync(ct);
            return Results.Ok(status);
        });

        app.MapGet(ApiRoutes.Account, async (IAccountService account, CancellationToken ct) =>
        {
            var snap = await account.GetSnapshotAsync(ct);
            return Results.Ok(snap);
        });

        app.MapGet(ApiRoutes.Positions, async (IAccountService account, CancellationToken ct) =>
        {
            var positions = await account.GetPositionsAsync(ct);
            return Results.Ok(positions);
        });

        app.MapGet(ApiRoutes.OpenOrders, async (IAccountService account, string? symbol, CancellationToken ct) =>
        {
            var orders = await account.GetOpenOrdersAsync(symbol, ct);
            return Results.Ok(orders);
        });

        app.MapGet(ApiRoutes.Equity, async (IAccountService account, int hours, CancellationToken ct) =>
        {
            var h = hours > 0 ? hours : 24;
            var points = await account.GetEquityCurveAsync(h, ct);
            return Results.Ok(points);
        });

        app.MapGet(ApiRoutes.CredentialsStatus, async (IAccountService account, CancellationToken ct) =>
            Results.Ok(await account.GetCredentialsStatusAsync(ct)))
            .RequireRateLimiting("credentials-read");

        app.MapPut(ApiRoutes.Credentials, async (
            SetCredentialsRequest req,
            IAccountService account,
            Auth.AuthTokenStore tokens,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ApiKey) || string.IsNullOrWhiteSpace(req.ApiSecret))
                return Results.BadRequest(new ErrorResponse("invalid_request", "ApiKey/ApiSecret fehlt"));
            await account.SetCredentialsAsync(req, ct);

            // Sicherheit: Nach Credentials-Change alle Tokens ausser dem requestenden revoken.
            // Verhindert, dass ein gestohlener Token nach Key-Wechsel weiter benutzt werden kann.
            var currentToken = ExtractCurrentToken(ctx);
            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.RevokeAllExcept(currentToken);
            }
            return Results.NoContent();
        }).RequireRateLimiting("credentials-write");

        // FCM-Device-Registrierung (Android-Client sendet seinen FCM-Token nach Login).
        app.MapPut(ApiRoutes.DevicesFcm, (FcmDeviceRegistrationDto dto, Services.FcmDeviceStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.DeviceId) || string.IsNullOrWhiteSpace(dto.FcmToken))
                return Results.BadRequest(new ErrorResponse("invalid_request", "DeviceId/FcmToken fehlt"));
            store.Register(dto);
            return Results.NoContent();
        });
    }

    private static string? ExtractCurrentToken(HttpContext ctx)
    {
        var h = ctx.Request.Headers.Authorization.ToString();
        return !string.IsNullOrWhiteSpace(h) && h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? h["Bearer ".Length..].Trim()
            : null;
    }
}
