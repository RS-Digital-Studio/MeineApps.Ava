namespace BingXBot.Engine.News;

/// <summary>
/// Task 1.2 — Stub-Implementierung ohne externe Datenquelle.
/// Liefert immer eine leere Liste → kein News-Blackout wird ausgelöst (graceful degradation).
/// Wird ersetzt sobald eine konkrete API (forexfactory/fmpcloud/tradingeconomics) gewählt ist.
/// </summary>
public sealed class StubEconomicCalendarService : IEconomicCalendarService
{
    public Task<IReadOnlyList<EconomicEvent>> GetEventsAsync(
        DateTime fromUtc, DateTime toUtc,
        EconomicEventImpact minImpact = EconomicEventImpact.High,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EconomicEvent>>(Array.Empty<EconomicEvent>());

    public Task<EconomicEvent?> GetActiveBlackoutEventAsync(
        DateTime nowUtc, int blackoutMinutes, CancellationToken ct = default)
        => Task.FromResult<EconomicEvent?>(null);
}
