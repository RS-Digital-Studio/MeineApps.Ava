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
}
