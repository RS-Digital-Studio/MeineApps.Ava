using System.Collections.Concurrent;

namespace BingXBot.Exchange;

/// <summary>
/// Token-Bucket Rate Limiter mit separaten Buckets pro Kategorie.
/// "orders" = 10 Requests/Sekunde, "queries" = 20 Requests/Sekunde, Default = 20/s.
/// Zugriff auf Queue<DateTime> ist durch SemaphoreSlim pro Kategorie geschuetzt.
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _timestamps = new();
    private readonly ConcurrentDictionary<string, int> _limits = new()
    {
        ["orders"] = 10,
        ["queries"] = 20
    };

    /// <summary>
    /// Wartet bis ein Rate-Limit-Slot frei ist. Thread-safe pro Kategorie.
    /// </summary>
    public async Task WaitForSlotAsync(string category, CancellationToken ct)
    {
        var limit = _limits.GetValueOrDefault(category, 20);
        var semaphore = _semaphores.GetOrAdd(category, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);
        try
        {
            // Queue-Zugriff innerhalb des Semaphore-Locks - thread-safe
            var timestamps = _timestamps.GetOrAdd(category, _ => new Queue<DateTime>());

            // Alte Timestamps entfernen (aelter als 1 Sekunde)
            while (timestamps.Count > 0 && timestamps.Peek() < DateTime.UtcNow.AddSeconds(-1))
                timestamps.Dequeue();

            // Warten wenn Limit erreicht
            if (timestamps.Count >= limit)
            {
                var oldest = timestamps.Peek();
                var waitTime = oldest.AddSeconds(1) - DateTime.UtcNow;
                if (waitTime > TimeSpan.Zero)
                    await Task.Delay(waitTime, ct);
                timestamps.Dequeue();
            }

            timestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
