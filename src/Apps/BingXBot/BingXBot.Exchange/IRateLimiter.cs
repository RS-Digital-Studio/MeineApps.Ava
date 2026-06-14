namespace BingXBot.Exchange;

/// <summary>
/// Rate-Limiter-Abstraktion fuer Exchange-Requests. Implementierungen koennen Token-Bucket,
/// Leaky-Bucket oder No-Op (fuer Tests) sein.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Wartet bis ein Slot fuer die gegebene Kategorie frei ist. Kategorien typischerweise
    /// <c>"orders"</c> oder <c>"queries"</c> (BingX-spezifische Separat-Limits).
    /// </summary>
    Task WaitForSlotAsync(string category, CancellationToken ct);
}
