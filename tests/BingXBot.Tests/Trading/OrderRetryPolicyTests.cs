using BingXBot.Trading.Resilience;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

// v1.5.5 Phase 8 — API-Retry-mit-Backoff fuer Order-Placement.
public class OrderRetryPolicyTests
{
    [Fact]
    public void ShouldRetry_HttpStatus429_True()
    {
        OrderRetryPolicy.ShouldRetryStatus(429).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_HttpStatus5xx_True()
    {
        OrderRetryPolicy.ShouldRetryStatus(500).Should().BeTrue();
        OrderRetryPolicy.ShouldRetryStatus(503).Should().BeTrue();
        OrderRetryPolicy.ShouldRetryStatus(599).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_HttpStatus4xx_KeinRetry()
    {
        // 400 Bad Request, 401 Unauthorized, 404 Not Found, 422 Unprocessable
        // → kein Retry, weil die Order-Anfrage strukturell falsch ist.
        OrderRetryPolicy.ShouldRetryStatus(400).Should().BeFalse();
        OrderRetryPolicy.ShouldRetryStatus(401).Should().BeFalse();
        OrderRetryPolicy.ShouldRetryStatus(404).Should().BeFalse();
        OrderRetryPolicy.ShouldRetryStatus(422).Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_BingxRetryableCodes_True()
    {
        OrderRetryPolicy.IsRetryableBingxCode(109400).Should().BeTrue();
        OrderRetryPolicy.IsRetryableBingxCode(100410).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_BingxOtherCodes_False()
    {
        OrderRetryPolicy.IsRetryableBingxCode(100421).Should().BeFalse(); // Server-Time-Drift
        OrderRetryPolicy.IsRetryableBingxCode(80012).Should().BeFalse();  // Insufficient margin
        OrderRetryPolicy.IsRetryableBingxCode(0).Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_TaskCanceledException_True()
    {
        OrderRetryPolicy.ShouldRetry(new TaskCanceledException()).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_OrderApiException_BingxRetryable_True()
    {
        var ex = new OrderApiException("service busy", bingxCode: 109400);
        OrderRetryPolicy.ShouldRetry(ex).Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_OrderApiException_400_False()
    {
        var ex = new OrderApiException("bad request", statusCode: 400);
        OrderRetryPolicy.ShouldRetry(ex).Should().BeFalse();
    }

    [Fact]
    public void GetBackoffMs_FirstAttempt_Zero()
    {
        OrderRetryPolicy.GetBackoffMs(1).Should().Be(0);
    }

    [Fact]
    public void GetBackoffMs_BackoffSequenz()
    {
        OrderRetryPolicy.GetBackoffMs(2).Should().Be(100);
        OrderRetryPolicy.GetBackoffMs(3).Should().Be(300);
        OrderRetryPolicy.GetBackoffMs(4).Should().Be(1_000);
        OrderRetryPolicy.GetBackoffMs(5).Should().Be(3_000); // Cap auf letztes Intervall
    }

    [Fact]
    public async Task ExecuteAsync_ErfolgImErstenVersuch_KeinRetry()
    {
        var attempts = 0;
        var result = await OrderRetryPolicy.ExecuteAsync(() =>
        {
            attempts++;
            return Task.FromResult("ok");
        });
        result.Should().Be("ok");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_RetryOn429_SucceedsOnThirdAttempt()
    {
        var attempts = 0;
        var result = await OrderRetryPolicy.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts < 3)
                throw new OrderApiException("rate limit", statusCode: 429);
            return Task.FromResult("ok");
        });
        result.Should().Be("ok");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_NoRetryOn400_ThrowsImmediately()
    {
        var attempts = 0;
        Func<Task> act = () => OrderRetryPolicy.ExecuteAsync<string>(() =>
        {
            attempts++;
            throw new OrderApiException("bad request", statusCode: 400);
        });

        await act.Should().ThrowAsync<OrderApiException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MaxRetriesExhausted_ThrowsAfterFourth()
    {
        var attempts = 0;
        Func<Task> act = () => OrderRetryPolicy.ExecuteAsync<string>(() =>
        {
            attempts++;
            throw new OrderApiException("rate limit", statusCode: 429);
        });

        await act.Should().ThrowAsync<OrderApiException>();
        attempts.Should().Be(OrderRetryPolicy.MaxAttempts);
    }

    // === Phase 18 / A2 — IdempotencyCheck (Doppel-Order-Schutz nach TaskCanceledException) ===

    [Fact]
    public async Task ExecuteAsync_IdempotencyCheck_NotCalledOnFirstAttempt()
    {
        // Erster Versuch ist erfolgreich → Probe darf nicht laufen.
        var probeCalls = 0;
        var result = await OrderRetryPolicy.ExecuteAsync<string>(
            action: () => Task.FromResult("ok"),
            idempotencyCheck: () => { probeCalls++; return Task.FromResult<string?>(null); });
        result.Should().Be("ok");
        probeCalls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotencyCheck_TimeoutThenProbeFindsExisting_AvoidsDoublePlace()
    {
        // Disaster-Szenario: Place 1 timeoutet, Order liegt aber bei BingX → Retry darf NICHT 2. Order erzeugen.
        var attempts = 0;
        var probeCalls = 0;
        var result = await OrderRetryPolicy.ExecuteAsync<string>(
            action: () =>
            {
                attempts++;
                if (attempts == 1) throw new TaskCanceledException("HTTP timeout");
                return Task.FromResult("DOPPEL-PLACE!"); // Soll nie erreicht werden
            },
            idempotencyCheck: () =>
            {
                probeCalls++;
                return Task.FromResult<string?>("existing-order-id"); // Probe findet bereits liegende Order
            });
        result.Should().Be("existing-order-id", "Probe-Treffer muss als Erfolg gelten — kein Doppel-Place");
        attempts.Should().Be(1);
        probeCalls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotencyCheck_ProbeReturnsNull_ContinuesRetry()
    {
        // Probe findet keine bestehende Order → Retry läuft normal.
        var attempts = 0;
        var probeCalls = 0;
        var result = await OrderRetryPolicy.ExecuteAsync<string>(
            action: () =>
            {
                attempts++;
                if (attempts == 1) throw new TaskCanceledException("HTTP timeout");
                return Task.FromResult("placed-after-retry");
            },
            idempotencyCheck: () =>
            {
                probeCalls++;
                return Task.FromResult<string?>(null);
            });
        result.Should().Be("placed-after-retry");
        attempts.Should().Be(2);
        probeCalls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotencyCheck_ProbeThrows_SwallowsAndRetries()
    {
        // Probe selbst wirft (z.B. Netzfehler beim GetOpenOrdersAsync) → Retry trotzdem fortsetzen.
        var attempts = 0;
        var probeCalls = 0;
        var result = await OrderRetryPolicy.ExecuteAsync<string>(
            action: () =>
            {
                attempts++;
                if (attempts == 1) throw new TaskCanceledException();
                return Task.FromResult("placed");
            },
            idempotencyCheck: () =>
            {
                probeCalls++;
                throw new HttpRequestException("probe failed");
            });
        result.Should().Be("placed");
        attempts.Should().Be(2);
        probeCalls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_IdempotencyCheck_RunsBeforeEachRetry()
    {
        // Mehrere Retries → Probe muss vor JEDEM Retry-Versuch laufen.
        var attempts = 0;
        var probeCalls = 0;
        Func<Task> act = () => OrderRetryPolicy.ExecuteAsync<string>(
            action: () =>
            {
                attempts++;
                throw new TaskCanceledException();
            },
            idempotencyCheck: () =>
            {
                probeCalls++;
                return Task.FromResult<string?>(null);
            });
        await act.Should().ThrowAsync<TaskCanceledException>();
        attempts.Should().Be(OrderRetryPolicy.MaxAttempts);
        probeCalls.Should().Be(OrderRetryPolicy.MaxAttempts - 1, "Probe läuft vor Retry 2..MaxAttempts");
    }
}
