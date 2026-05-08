using System.Net;

namespace BingXBot.Trading.Resilience;

/// <summary>
/// v1.5.5 Phase 8 — Retry-Policy fuer Order-Placement bei transienten Fehlern.
///
/// Exponentielles Backoff (100 ms / 300 ms / 1 s / 3 s, max 4 Versuche). Retry-tauglich:
/// HTTP 429, 5xx, <see cref="TaskCanceledException"/> (Timeout), spezifische BingX-Error-Codes
/// (109400 = "service busy", 100410 = "request rate too high"). KEIN Retry bei 4xx (ausser 429),
/// "insufficient margin" oder "invalid symbol" — diese sollen sofort scheitern.
///
/// Pure Funktion + statische Konfiguration — bewusst kein Polly-Pattern, weil das Trading-Stack
/// minimale externe Abhaengigkeiten haben soll. Tests gegen die statische
/// <see cref="ShouldRetry(Exception)"/>-Logik + die Backoff-Sequenz.
/// </summary>
public static class OrderRetryPolicy
{
    /// <summary>Maximale Anzahl Retry-Versuche (inkl. erstem Versuch).</summary>
    public const int MaxAttempts = 4;

    /// <summary>Backoff-Sequenz in Millisekunden (vor Versuch 2/3/4).</summary>
    public static readonly int[] BackoffMs = { 100, 300, 1_000, 3_000 };

    /// <summary>
    /// BingX-spezifische Error-Codes die als transient gelten (Retry sinnvoll).
    /// 109400 = "service busy", 100410 = "request rate too high".
    /// </summary>
    public static readonly int[] RetryableBingxCodes = { 109400, 100410 };

    /// <summary>
    /// Liefert das Backoff in Millisekunden vor dem n-ten Versuch (1-basiert: Attempt 1 = 0 ms).
    /// </summary>
    public static int GetBackoffMs(int attemptNumber)
    {
        if (attemptNumber <= 1) return 0;
        var idx = attemptNumber - 2;
        return idx < BackoffMs.Length ? BackoffMs[idx] : BackoffMs[^1];
    }

    /// <summary>
    /// Pruet ob ein Fehler retry-tauglich ist. Erlaubt: HTTP 429/5xx, Timeouts, spezifische
    /// BingX-Error-Codes. Verboten: 4xx ausser 429 (Permanent — Order wuerde sich selbst zerstoeren).
    /// </summary>
    public static bool ShouldRetry(Exception ex)
    {
        if (ex is TaskCanceledException) return true;
        if (ex is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode status)
            return ShouldRetryStatus((int)status);
        if (ex is OrderApiException apiEx)
        {
            if (apiEx.StatusCode.HasValue && ShouldRetryStatus(apiEx.StatusCode.Value))
                return true;
            if (apiEx.BingxCode.HasValue && IsRetryableBingxCode(apiEx.BingxCode.Value))
                return true;
            return false;
        }
        // Generische Exceptions: vorsichtig — nur retry wenn explizit als transient markiert.
        return false;
    }

    /// <summary>Pruet ob ein HTTP-Statuscode retry-tauglich ist.</summary>
    public static bool ShouldRetryStatus(int statusCode)
    {
        if (statusCode == 429) return true; // Too Many Requests
        if (statusCode >= 500 && statusCode <= 599) return true; // 5xx Server-Errors
        return false;
    }

    /// <summary>Pruet ob ein BingX-Error-Code retry-tauglich ist.</summary>
    public static bool IsRetryableBingxCode(int bingxCode)
    {
        foreach (var c in RetryableBingxCodes)
            if (c == bingxCode) return true;
        return false;
    }

    /// <summary>
    /// Fuehrt eine asynchrone Aktion mit Retry + Backoff aus. Bei <see cref="MaxAttempts"/> Fehlern
    /// wird die letzte Exception weitergeworfen.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        Action<int, Exception>? onRetry = null,
        CancellationToken ct = default)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt >= MaxAttempts || !ShouldRetry(ex))
                    throw;
                onRetry?.Invoke(attempt, ex);
                var delay = GetBackoffMs(attempt + 1);
                if (delay > 0)
                    await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        // Unreachable
        throw lastException ?? new InvalidOperationException("OrderRetryPolicy: unreachable");
    }
}

/// <summary>
/// Strukturierte Exception fuer Order-Place-API-Fehler. Faellt mit HttpStatusCode/BingX-Error-Code
/// an, sodass <see cref="OrderRetryPolicy.ShouldRetry(Exception)"/> retry-tauglich entscheiden kann.
/// </summary>
public sealed class OrderApiException : Exception
{
    public int? StatusCode { get; }
    public int? BingxCode { get; }

    public OrderApiException(string message, int? statusCode = null, int? bingxCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        BingxCode = bingxCode;
    }
}
