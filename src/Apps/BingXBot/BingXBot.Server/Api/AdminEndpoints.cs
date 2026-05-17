using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Server.Services;

namespace BingXBot.Server.Api;

/// <summary>
/// Admin-only Operations: Service-Probleme post-hoc beheben. Aktuell nur Trade-Backfill
/// (Snapshot-Report-Fix Befund 1 / A0.4) — die DB hatte 0 Trades, obwohl 19 abgeschlossen
/// auf BingX-Seite existierten. Mit diesem Endpoint kann der Admin das nachholen, sobald
/// die Persistenz-Pipeline gefixt ist und neue Trades wieder in die DB laufen.
///
/// Auth: laufende Bearer-Token-Validierung (kein Public-Path). Es genuegt also ein gepairter
/// Desktop-Client zum Triggern. Wenn das missbrauchbar wird, kann der Endpoint spaeter mit
/// einer separaten "Admin"-Token-Klasse abgesichert werden — aktuell ist der Server intern
/// (Tailscale-only) und Trade-Backfill ist non-destructive (dedup-aware).
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.AdminBackfillTrades, async (
            BackfillRequest req,
            BotAutoResumeService resumeService,
            CancellationToken ct) =>
        {
            if (req.FromUtc == default)
                return Results.BadRequest(new ErrorResponse("invalid_request", "fromUtc fehlt oder ungueltig"));

            var to = req.ToUtc == default ? (DateTime?)null : req.ToUtc;
            var summary = await resumeService.BackfillFromBingxAsync(req.FromUtc, to, ct);
            if (summary.ErrorMessage != null)
                return Results.BadRequest(new BackfillResponseDto(0, 0, 0, summary.ErrorMessage));
            return Results.Ok(new BackfillResponseDto(
                summary.Backfilled, summary.Skipped, summary.TodayApplied, null));
        });
    }
}

/// <summary>Request-Body fuer den Trade-Backfill. <c>ToUtc=default</c> → bis jetzt.</summary>
public sealed record BackfillRequest(DateTime FromUtc, DateTime ToUtc = default);

/// <summary>Response fuer den Trade-Backfill.</summary>
public sealed record BackfillResponseDto(int Backfilled, int Skipped, int TodayApplied, string? ErrorMessage);
