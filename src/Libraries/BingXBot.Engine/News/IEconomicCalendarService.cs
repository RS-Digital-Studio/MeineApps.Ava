namespace BingXBot.Engine.News;

/// <summary>
/// Task 1.2 — Service zum Abruf bevorstehender Wirtschafts-News.
/// Konkrete Implementierungen können forexfactory.com, fmpcloud.io oder tradingeconomics.com nutzen.
/// Default-Impl <see cref="StubEconomicCalendarService"/> liefert leere Liste (kein Block, graceful degradation).
/// </summary>
public interface IEconomicCalendarService
{
    /// <summary>
    /// Liefert alle Events im Zeitfenster [fromUtc, toUtc], gefiltert nach Mindest-Impact.
    /// Implementierungen sollen intern cachen (Buch-Empfehlung: 24h Cache, 4h Refresh).
    /// </summary>
    Task<IReadOnlyList<EconomicEvent>> GetEventsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        EconomicEventImpact minImpact = EconomicEventImpact.High,
        CancellationToken ct = default);

    /// <summary>
    /// Prüft ob der übergebene Zeitpunkt im Blackout-Fenster ±blackoutMinutes eines High-Impact-Events liegt.
    /// Nutzt intern <see cref="GetEventsAsync"/> mit einem Puffer-Fenster.
    /// </summary>
    Task<EconomicEvent?> GetActiveBlackoutEventAsync(
        DateTime nowUtc,
        int blackoutMinutes,
        CancellationToken ct = default);
}
