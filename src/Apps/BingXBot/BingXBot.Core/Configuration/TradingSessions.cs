namespace BingXBot.Core.Configuration;

/// <summary>
/// Phase 18 / A7 — Bitmask-Enum fuer User-erlaubte Trading-Sessions (Crypto).
/// Crypto handelt 24/7, aber Liquiditaet/Setup-Qualitaet schwanken stark zwischen Asia/EU/US.
/// Default = <see cref="All"/> (= keine zeitliche Einschraenkung).
/// </summary>
[Flags]
public enum TradingSessions
{
    None = 0,
    /// <summary>00:00-08:00 UTC + 22:00-00:00 UTC (Asia-Sessions).</summary>
    Asia = 1,
    /// <summary>08:00-13:00 UTC (London-Open bis NY-Pre-Market).</summary>
    Eu = 2,
    /// <summary>13:00-16:00 UTC (London-NY-Overlap, hoechste Liquiditaet).</summary>
    EuUsOverlap = 4,
    /// <summary>16:00-22:00 UTC (NY-Session bis Asia-Pre-Market).</summary>
    Us = 8,
    All = Asia | Eu | EuUsOverlap | Us
}
