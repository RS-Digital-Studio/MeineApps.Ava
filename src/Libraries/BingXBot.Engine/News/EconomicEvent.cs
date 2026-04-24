namespace BingXBot.Engine.News;

/// <summary>
/// Task 1.2 — Wirtschaftskalender-Event nach SK-Buch Masterclass Step 1.
/// Buch: "Vor extrem wichtigen Ereignissen (US-Leitzinsentscheide, NFP-Arbeitsmarktdaten, CPI)
/// solltest du keine Limit-Orders offen in Korrekturboxen liegen haben."
/// </summary>
public sealed record EconomicEvent(
    DateTime TimeUtc,
    string Country,
    string Name,
    EconomicEventImpact Impact,
    string? Currency = null);

/// <summary>
/// Task 1.2 — Impact-Klassifikation eines News-Events. Nur High (red-coded) wird im
/// Default-NewsBlackout blockiert. Medium/Low werden ignoriert.
/// </summary>
public enum EconomicEventImpact
{
    Low,
    Medium,
    /// <summary>Red-coded Events (FOMC, NFP, CPI, ECB, BoE, BoJ) — lösen News-Blackout aus.</summary>
    High
}
