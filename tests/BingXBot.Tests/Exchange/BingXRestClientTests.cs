using System.Net;
using System.Text;
using BingXBot.Exchange;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Exchange;

public class BingXRestClientTests
{
    [Fact]
    public void GenerateSignature_ShouldProduceValidHmac()
    {
        var signature = BingXRestClient.GenerateSignature("timestamp=1234567890", "testsecret");
        signature.Should().NotBeNullOrEmpty();
        signature.Length.Should().Be(64); // SHA256 hex = 64 chars
    }

    [Fact]
    public void GenerateSignature_SameInput_ShouldProduceSameOutput()
    {
        var s1 = BingXRestClient.GenerateSignature("param=value", "secret");
        var s2 = BingXRestClient.GenerateSignature("param=value", "secret");
        s1.Should().Be(s2);
    }

    [Fact]
    public void GenerateSignature_DifferentInput_ShouldProduceDifferentOutput()
    {
        var s1 = BingXRestClient.GenerateSignature("param=value1", "secret");
        var s2 = BingXRestClient.GenerateSignature("param=value2", "secret");
        s1.Should().NotBe(s2);
    }

    // === Neue Tests: Retry-Logik (17.03.2026) ===

    /// <summary>
    /// Handler der bei den ersten N Aufrufen einen HTTP-Fehler zurückgibt,
    /// beim letzten eine valide Antwort.
    /// </summary>
    private sealed class AnzahlFehlerHandler : HttpMessageHandler
    {
        private readonly int _fehlerAnzahl;
        private readonly HttpStatusCode _fehlerCode;
        private readonly string _erfolgsAntwort;
        private int _aufrufZaehler;

        public int GesamtAufrufe => _aufrufZaehler;

        public AnzahlFehlerHandler(int fehlerAnzahl, HttpStatusCode fehlerCode, string erfolgsAntwort)
        {
            _fehlerAnzahl = fehlerAnzahl;
            _fehlerCode = fehlerCode;
            _erfolgsAntwort = erfolgsAntwort;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _aufrufZaehler++;
            if (_aufrufZaehler <= _fehlerAnzahl)
            {
                return Task.FromResult(new HttpResponseMessage(_fehlerCode)
                {
                    Content = new StringContent("Server Error")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_erfolgsAntwort, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Handler der immer denselben Statuscode zurückgibt.</summary>
    private sealed class KonstanteFehlerHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public int GesamtAufrufe { get; private set; }

        public KonstanteFehlerHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            GesamtAufrufe++;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent("Permanent Error")
            });
        }
    }

    private static BingXRestClient ErstelleClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var rateLimiter = new RateLimiter();
        return new BingXRestClient("testkey", "testsecret", httpClient, rateLimiter, NullLogger<BingXRestClient>.Instance);
    }

    [Fact]
    public async Task Retry_TransienterFehler500_WirdErfolgreichRetried()
    {
        // 1x HTTP 500, dann Erfolg → muss nach 1 Retry klappen
        var balanceJson = """{"code":0,"msg":"OK","data":{"balance":{"balance":"10000","availableMargin":"8000","unrealizedProfit":"200","usedMargin":"2000"}}}""";
        var handler = new AnzahlFehlerHandler(fehlerAnzahl: 1, HttpStatusCode.InternalServerError, balanceJson);
        var client = ErstelleClient(handler);

        var result = await client.GetAccountInfoAsync();

        handler.GesamtAufrufe.Should().Be(2, "1 fehlgeschlagener Versuch + 1 erfolgreicher Retry");
        result.Balance.Should().Be(10000m);
    }

    [Fact]
    public async Task Retry_Http429RateLimit_WirdRetried()
    {
        // HTTP 429 ist ein transienter Fehler → Retry
        var balanceJson = """{"code":0,"msg":"OK","data":{"balance":{"balance":"5000","availableMargin":"4000","unrealizedProfit":"0","usedMargin":"1000"}}}""";
        var handler = new AnzahlFehlerHandler(fehlerAnzahl: 1, HttpStatusCode.TooManyRequests, balanceJson);
        var client = ErstelleClient(handler);

        var result = await client.GetAccountInfoAsync();

        handler.GesamtAufrufe.Should().Be(2, "HTTP 429 muss ebenfalls retried werden");
        result.Balance.Should().Be(5000m);
    }

    [Fact]
    public async Task Retry_DreiAufeinanderfolgendeServerFehler_WirftException()
    {
        // Alle 4 Versuche (1 original + 3 Retries) schlagen fehl → BingXApiException
        var handler = new KonstanteFehlerHandler(HttpStatusCode.ServiceUnavailable);
        var client = ErstelleClient(handler);

        var act = async () => await client.GetAccountInfoAsync();

        await act.Should().ThrowAsync<BingXApiException>(
            "Nach 3 fehlgeschlagenen Retries muss eine Exception geworfen werden");

        // MaxRetries = 3 → 4 Gesamtversuche (1 original + 3 Retries)
        handler.GesamtAufrufe.Should().Be(4,
            "Es müssen genau 4 Versuche unternommen werden (1 original + 3 Retries)");
    }

    [Fact]
    public async Task Retry_Http400ClientFehler_WirdNichtRetried()
    {
        // HTTP 400 ist kein transienter Fehler → kein Retry, sofort Exception
        var handler = new KonstanteFehlerHandler(HttpStatusCode.BadRequest);
        var client = ErstelleClient(handler);

        var act = async () => await client.GetAccountInfoAsync();

        await act.Should().ThrowAsync<BingXApiException>();
        // Nur 1 Versuch, kein Retry bei Client-Fehler
        handler.GesamtAufrufe.Should().Be(1,
            "HTTP 4xx (außer 429) sind keine transienten Fehler → kein Retry");
    }

    [Fact]
    public async Task Retry_NetzwerkFehler_WirdRetried()
    {
        // HttpRequestException simuliert Netzwerkproblem → wird ebenfalls retried
        var aufrufZaehler = 0;
        var netzwerkFehlerHandler = new LambdaHandler(request =>
        {
            aufrufZaehler++;
            if (aufrufZaehler <= 1)
                throw new HttpRequestException("Simulierter Netzwerkfehler");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"code":0,"msg":"OK","data":{"balance":{"balance":"1000","availableMargin":"1000","unrealizedProfit":"0","usedMargin":"0"}}}""",
                    Encoding.UTF8, "application/json")
            };
        });
        var client = ErstelleClient(netzwerkFehlerHandler);

        var result = await client.GetAccountInfoAsync();

        aufrufZaehler.Should().Be(2, "Netzwerkfehler muss retried werden");
        result.Balance.Should().Be(1000m);
    }

    /// <summary>HttpMessageHandler der eine Lambda-Funktion ausführt.</summary>
    private sealed class LambdaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _func;
        public LambdaHandler(Func<HttpRequestMessage, HttpResponseMessage> func) => _func = func;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_func(request));
    }
}
