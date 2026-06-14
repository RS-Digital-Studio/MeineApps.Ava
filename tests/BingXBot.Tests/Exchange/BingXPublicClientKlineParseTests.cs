using System.Net;
using System.Text;
using BingXBot.Core.Enums;
using BingXBot.Exchange;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Exchange;

/// <summary>
/// Deterministische Parse-Tests fuer <see cref="BingXPublicClient.GetKlinesAsync"/> (kein Netz).
/// Sichern PERF-4 ab: Die Kline-Deserialisierung liest den bereits geparsten <c>data</c>-JsonElement
/// direkt (statt den Roh-String ein zweites Mal zu deserialisieren). Der per-Property annotierte
/// <see cref="BingXBot.Exchange.Models.FlexibleStringConverter"/> muss dabei sowohl String- als auch
/// Zahl-OHLCV korrekt verarbeiten — sonst kaemen falsche Preise in den Backtest/Scanner.
/// </summary>
public class BingXPublicClientKlineParseTests
{
    /// <summary>
    /// Handler: Liefert beim ersten Aufruf den uebergebenen Kline-Batch, danach leeres <c>data</c>
    /// (beendet die Rueckwaerts-Pagination deterministisch — BingX liefert irgendwann keine
    /// aelteren Kerzen mehr).
    /// </summary>
    private sealed class EinmalKlineHandler : HttpMessageHandler
    {
        private readonly string _ersterBatchJson;
        private int _aufrufZaehler;

        public EinmalKlineHandler(string ersterBatchJson) => _ersterBatchJson = ersterBatchJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _aufrufZaehler++;
            var json = _aufrufZaehler == 1
                ? _ersterBatchJson
                : """{"code":0,"msg":"","data":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private static BingXPublicClient ErstelleClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        return new BingXPublicClient(http, new RateLimiter(), NullLogger<BingXPublicClient>.Instance);
    }

    // Drei H4-Kerzen, OHLCV bewusst als STRING (BingX-Normalfall).
    private static long ZeitMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    [Fact]
    public async Task GetKlines_StringOhlcv_WirdKorrektGeparst()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddHours(4);
        var t2 = t0.AddHours(8);

        var json = $$"""
        {"code":0,"msg":"","data":[
          {"open":"100.5","high":"110.0","low":"99.0","close":"108.25","volume":"1234.5","time":{{ZeitMs(t0)}}},
          {"open":"108.25","high":"112.0","low":"107.0","close":"111.0","volume":"2000","time":{{ZeitMs(t1)}}},
          {"open":"111.0","high":"115.0","low":"110.5","close":"114.0","volume":"3000","time":{{ZeitMs(t2)}}}
        ]}
        """;

        var client = ErstelleClient(new EinmalKlineHandler(json));
        var candles = await client.GetKlinesAsync("BTC-USDT", TimeFrame.H4, t0, t2.AddHours(1));

        candles.Should().HaveCount(3);
        // Chronologisch sortiert (Rueckwaerts-Paging liefert absteigend, GetKlinesAsync sortiert).
        candles[0].OpenTime.Should().Be(t0);
        candles[0].Open.Should().Be(100.5m);
        candles[0].Close.Should().Be(108.25m);
        candles[0].High.Should().Be(110.0m);
        candles[0].Low.Should().Be(99.0m);
        candles[0].Volume.Should().Be(1234.5m);
        candles[2].OpenTime.Should().Be(t2);
        candles[2].Close.Should().Be(114.0m);
    }

    [Fact]
    public async Task GetKlines_ZahlOhlcv_WirdViaFlexibleConverterGeparst()
    {
        // BingX liefert manche Felder als ZAHL statt String — der FlexibleStringConverter muss
        // das beim Deserialize aus dem JsonElement (PERF-4) genauso normalisieren wie beim
        // frueheren String-Deserialize.
        var t0 = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddHours(4);

        var json = $$"""
        {"code":0,"msg":"","data":[
          {"open":100.5,"high":110,"low":99,"close":108.25,"volume":1234.5,"time":{{ZeitMs(t0)}}},
          {"open":108.25,"high":112,"low":107,"close":111,"volume":2000,"time":{{ZeitMs(t1)}}}
        ]}
        """;

        var client = ErstelleClient(new EinmalKlineHandler(json));
        var candles = await client.GetKlinesAsync("ETH-USDT", TimeFrame.H4, t0, t1.AddHours(1));

        candles.Should().HaveCount(2);
        candles[0].Open.Should().Be(100.5m);
        candles[0].High.Should().Be(110m);
        candles[0].Low.Should().Be(99m);
        candles[0].Close.Should().Be(108.25m);
        candles[0].Volume.Should().Be(1234.5m);
        candles[1].Close.Should().Be(111m);
    }

    [Fact]
    public async Task GetKlines_DataKeinArray_LiefertLeer()
    {
        // Geschlossene TradFi-Maerkte liefern `data` als Nicht-Array (null/string/object).
        // Die JsonDocument-Vorpruefung muss das abfangen, BEVOR deserialisiert wird.
        var t0 = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var json = """{"code":0,"msg":"","data":null}""";

        var client = ErstelleClient(new EinmalKlineHandler(json));
        var candles = await client.GetKlinesAsync("NCSIEWJ-USDT", TimeFrame.H4, t0, t0.AddDays(1));

        candles.Should().BeEmpty();
    }
}
