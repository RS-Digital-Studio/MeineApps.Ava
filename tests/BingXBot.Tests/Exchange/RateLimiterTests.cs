using System.Diagnostics;
using BingXBot.Exchange;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Exchange;

public class RateLimiterTests
{
    [Fact]
    public async Task WaitForSlot_UnderLimit_ShouldNotBlock()
    {
        var limiter = new RateLimiter();
        var sw = Stopwatch.StartNew();
        await limiter.WaitForSlotAsync("orders", CancellationToken.None);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task WaitForSlot_OverLimit_ShouldThrottle()
    {
        var limiter = new RateLimiter();
        for (int i = 0; i < 10; i++)
            await limiter.WaitForSlotAsync("orders", CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await limiter.WaitForSlotAsync("orders", CancellationToken.None);
        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task DifferentCategories_ShouldBeIndependent()
    {
        var limiter = new RateLimiter();
        for (int i = 0; i < 10; i++)
            await limiter.WaitForSlotAsync("orders", CancellationToken.None);

        // queries Bucket sollte noch frei sein
        var sw = Stopwatch.StartNew();
        await limiter.WaitForSlotAsync("queries", CancellationToken.None);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    // === Phase 18 / B3 — neue Kategorien + konfigurierbare Limits ===

    [Fact]
    public void DefaultLimits_TradeAndAccount_AreSet()
    {
        var limiter = new RateLimiter();
        limiter.GetLimit("trade").Should().Be(10);
        limiter.GetLimit("account").Should().Be(10);
    }

    [Fact]
    public void GetLimit_UnknownCategory_ReturnsDefault20()
    {
        var limiter = new RateLimiter();
        limiter.GetLimit("unbekannt").Should().Be(20);
    }

    [Fact]
    public void SetLimit_OverridesExistingCategory()
    {
        var limiter = new RateLimiter();
        limiter.SetLimit("orders", 25);
        limiter.GetLimit("orders").Should().Be(25);
    }

    [Fact]
    public void SetLimit_ZeroOrNegative_ClampedToOne()
    {
        var limiter = new RateLimiter();
        limiter.SetLimit("test", 0);
        limiter.GetLimit("test").Should().Be(1);
        limiter.SetLimit("test", -5);
        limiter.GetLimit("test").Should().Be(1);
    }

    [Fact]
    public void SetLimit_EmptyCategory_NoOp()
    {
        var limiter = new RateLimiter();
        limiter.SetLimit("", 50);
        limiter.GetLimit("").Should().Be(20); // Default
    }
}
