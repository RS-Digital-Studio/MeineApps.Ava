using System.Collections.Concurrent;

namespace BingXBot.Exchange;

/// <summary>
/// Token-Bucket Rate Limiter mit separaten Buckets pro Kategorie.
/// Default-Kategorien: "orders" = 10 Requests/Sekunde, "queries" = 20 Requests/Sekunde, Default = 20/s.
/// Phase 18 / B3 — neue Kategorien "trade" + "account" als BingX-Endpoint-spezifische Buckets
/// (BingX-Doku: 100/10s pro IP fuer Trade-Endpoints → 10/s sustained). Limits konfigurierbar
/// per <see cref="SetLimit"/>-Aufruf bei Bootstrap (z.B. fuer Pi-Mobile-LTE-Profil).
/// Zugriff auf Queue{DateTime} ist durch SemaphoreSlim pro Kategorie geschützt.
/// </summary>
public class RateLimiter : IRateLimiter, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _timestamps = new();
    private readonly ConcurrentDictionary<string, int> _limits = new()
    {
        ["orders"] = 10,
        ["queries"] = 20,
        ["trade"] = 10,    // Phase 18 / B3 — BingX 100/10s = 10/s sustained
        ["account"] = 10   // Phase 18 / B3 — Account-Endpoints (Balance, Positions etc.)
    };
    private bool _disposed;

    /// <summary>
    /// Phase 18 / B3 — Setzt das Limit (Requests/s) fuer eine Kategorie zur Laufzeit.
    /// Wird vom Server-Bootstrap aus IConfiguration aufgerufen. Bei nicht existierender
    /// Kategorie wird sie angelegt; bestehende werden ueberschrieben.
    /// </summary>
    public void SetLimit(string category, int requestsPerSecond)
    {
        if (string.IsNullOrEmpty(category)) return;
        if (requestsPerSecond < 1) requestsPerSecond = 1;
        _limits[category] = requestsPerSecond;
    }

    /// <summary>Phase 18 / B3 — Liefert das aktuelle Limit fuer eine Kategorie (oder Default 20).</summary>
    public int GetLimit(string category) => _limits.GetValueOrDefault(category, 20);

    /// <summary>
    /// Wartet bis ein Rate-Limit-Slot frei ist. Thread-safe pro Kategorie.
    /// </summary>
    public async Task WaitForSlotAsync(string category, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var limit = _limits.GetValueOrDefault(category, 20);
        var semaphore = _semaphores.GetOrAdd(category, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Queue-Zugriff innerhalb des Semaphore-Locks - thread-safe
            var timestamps = _timestamps.GetOrAdd(category, _ => new Queue<DateTime>());
            var now = DateTime.UtcNow;

            // Alte Timestamps entfernen (älter als 1 Sekunde)
            while (timestamps.Count > 0 && timestamps.Peek() < now.AddSeconds(-1))
                timestamps.Dequeue();

            // Warten wenn Limit erreicht
            if (timestamps.Count >= limit)
            {
                var oldest = timestamps.Peek();
                var waitTime = oldest.AddSeconds(1) - now;
                if (waitTime > TimeSpan.Zero)
                    await Task.Delay(waitTime, ct).ConfigureAwait(false);
                timestamps.Dequeue();
            }

            timestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var semaphore in _semaphores.Values)
            semaphore.Dispose();
        _semaphores.Clear();
        _timestamps.Clear();
    }
}
