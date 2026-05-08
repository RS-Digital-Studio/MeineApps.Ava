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
}
