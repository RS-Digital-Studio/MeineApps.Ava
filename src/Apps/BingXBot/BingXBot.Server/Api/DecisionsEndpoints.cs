using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using BingXBot.Trading;

namespace BingXBot.Server.Api;

/// <summary>
/// v1.5.2 Phase 4 — Decision-Trail REST-Endpoint.
/// </summary>
public static class DecisionsEndpoints
{
    public static void MapDecisionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Decisions, async (
            BotDatabaseService db,
            DecisionTrailBuffer buffer,
            string? symbol, string? tf, string? reason,
            DateTime? since, int? limit, CancellationToken ct) =>
        {
            TimeFrame? tfParsed = null;
            if (!string.IsNullOrWhiteSpace(tf) && Enum.TryParse<TimeFrame>(tf, ignoreCase: true, out var parsed))
                tfParsed = parsed;
            var lim = limit is > 0 ? Math.Min(limit.Value, 1_000) : 200;

            // 1) Aktuelle In-Memory-Decisions (juengste, ohne DB-Roundtrip).
            var memHits = buffer.Filter(symbol, tfParsed, reason, since, lim);
            // 2) Wenn die In-Memory-Cap nicht reicht: aus DB nachladen (ueberlebt Server-Restart).
            IReadOnlyList<EvaluationDecision> result;
            if (memHits.Count >= lim)
            {
                result = memHits;
            }
            else
            {
                var dbHits = await db.LoadDecisionsAsync(symbol, tfParsed, reason, since, lim).ConfigureAwait(false);
                // Mem hat immer Vorrang — DB-Hits ergaenzen die fehlende Tail.
                var seen = new HashSet<DateTime>(memHits.Select(d => d.UtcTimestamp));
                result = memHits
                    .Concat(dbHits.Where(d => !seen.Contains(d.UtcTimestamp)))
                    .Take(lim)
                    .ToList();
            }

            var dto = new DecisionsQueryResultDto(
                Items: result.Select(ToDto).ToList(),
                TotalCount: result.Count);
            return Results.Ok(dto);
        });
    }

    private static EvaluationDecisionDto ToDto(EvaluationDecision d) => new(
        UtcTimestamp: d.UtcTimestamp,
        Symbol: d.Symbol,
        Tf: (int)d.Tf,
        SequenceState: d.SequenceState,
        Point0: d.Point0,
        PointA: d.PointA,
        PointB: d.PointB,
        Triggered: d.Triggered,
        RejectionReason: d.RejectionReason,
        ConfluenceScore: d.ConfluenceScore,
        ConfluenceCategories: d.ConfluenceCategories,
        HardFiltersFailed: d.HardFiltersFailed);
}
