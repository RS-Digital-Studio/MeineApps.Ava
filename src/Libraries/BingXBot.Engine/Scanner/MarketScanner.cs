using System.Runtime.CompilerServices;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine.Scanner;

public class MarketScanner : IMarketScanner
{
    private readonly IExchangeClient _exchangeClient;
    private readonly IDataFeed _dataFeed;
    private readonly ILogger<MarketScanner> _logger;

    public MarketScanner(IExchangeClient exchangeClient, IDataFeed dataFeed, ILogger<MarketScanner> logger)
    {
        _exchangeClient = exchangeClient;
        _dataFeed = dataFeed;
        _logger = logger;
    }

    public async IAsyncEnumerable<ScanResult> ScanAsync(
        ScannerSettings settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Alle Ticker holen
        var tickers = await _exchangeClient.GetAllTickersAsync();

        // Filtern
        var filtered = tickers
            .Where(t => !settings.Blacklist.Contains(t.Symbol))
            .Where(t => settings.Whitelist.Count == 0 || settings.Whitelist.Contains(t.Symbol))
            .Where(t => t.Volume24h >= settings.MinVolume24h)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= settings.MinPriceChange)
            .ToList();

        // Scoren und sortieren
        var scored = filtered
            .Select(t => new ScanResult(
                t.Symbol,
                CalculateScore(t, settings.Mode),
                settings.Mode.ToString(),
                new Dictionary<string, decimal>
                {
                    ["Volume24h"] = t.Volume24h,
                    ["PriceChange"] = t.PriceChangePercent24h,
                    ["LastPrice"] = t.LastPrice
                }))
            .OrderByDescending(r => r.Score)
            .Take(settings.MaxResults);

        foreach (var result in scored)
        {
            ct.ThrowIfCancellationRequested();
            yield return result;
        }
    }

    private static decimal CalculateScore(Ticker ticker, ScanMode mode)
    {
        return mode switch
        {
            ScanMode.Momentum => Math.Abs(ticker.PriceChangePercent24h) * (ticker.Volume24h / 1_000_000m),
            ScanMode.Reversal => (100m - Math.Abs(ticker.PriceChangePercent24h)) * (ticker.Volume24h / 1_000_000m),
            ScanMode.Breakout => Math.Abs(ticker.PriceChangePercent24h) * 2m * (ticker.Volume24h / 1_000_000m),
            ScanMode.VolumeSurge => ticker.Volume24h / 1_000_000m,
            _ => ticker.Volume24h / 1_000_000m
        };
    }
}
