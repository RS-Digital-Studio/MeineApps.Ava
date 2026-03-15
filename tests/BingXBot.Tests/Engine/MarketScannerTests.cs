using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Scanner;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BingXBot.Tests.Engine;

public class MarketScannerTests
{
    [Fact]
    public async Task Scan_ShouldFilterByVolume()
    {
        var client = Substitute.For<IExchangeClient>();
        client.GetAllTickersAsync().Returns(new List<Ticker>
        {
            new("BTC-USDT", 50000m, 49999m, 50001m, 50_000_000m, 5m, DateTime.UtcNow),
            new("SHIB-USDT", 0.00001m, 0.00001m, 0.00001m, 100m, 1m, DateTime.UtcNow),
        });
        var feed = Substitute.For<IDataFeed>();
        var scanner = new MarketScanner(client, feed, NullLogger<MarketScanner>.Instance);

        var results = new List<ScanResult>();
        await foreach (var r in scanner.ScanAsync(new ScannerSettings { MinVolume24h = 1_000_000m }, CancellationToken.None))
            results.Add(r);

        results.Should().ContainSingle(r => r.Symbol == "BTC-USDT");
        results.Should().NotContain(r => r.Symbol == "SHIB-USDT");
    }

    [Fact]
    public async Task Scan_ShouldRespectBlacklist()
    {
        var client = Substitute.For<IExchangeClient>();
        client.GetAllTickersAsync().Returns(new List<Ticker>
        {
            new("BTC-USDT", 50000m, 49999m, 50001m, 50_000_000m, 5m, DateTime.UtcNow),
            new("ETH-USDT", 3000m, 2999m, 3001m, 20_000_000m, 3m, DateTime.UtcNow),
        });
        var feed = Substitute.For<IDataFeed>();
        var scanner = new MarketScanner(client, feed, NullLogger<MarketScanner>.Instance);

        var results = new List<ScanResult>();
        await foreach (var r in scanner.ScanAsync(new ScannerSettings { MinVolume24h = 1m, Blacklist = new() { "BTC-USDT" } }, CancellationToken.None))
            results.Add(r);

        results.Should().NotContain(r => r.Symbol == "BTC-USDT");
        results.Should().Contain(r => r.Symbol == "ETH-USDT");
    }

    [Fact]
    public async Task Scan_ShouldRespectMaxResults()
    {
        var client = Substitute.For<IExchangeClient>();
        client.GetAllTickersAsync().Returns(Enumerable.Range(1, 20)
            .Select(i => new Ticker($"SYM{i}-USDT", 100m, 99m, 101m, 50_000_000m, 5m, DateTime.UtcNow))
            .ToList());
        var feed = Substitute.For<IDataFeed>();
        var scanner = new MarketScanner(client, feed, NullLogger<MarketScanner>.Instance);

        var results = new List<ScanResult>();
        await foreach (var r in scanner.ScanAsync(new ScannerSettings { MinVolume24h = 1m, MaxResults = 5 }, CancellationToken.None))
            results.Add(r);

        results.Should().HaveCount(5);
    }
}
