using System.Net;
using BingXBot.Core.Interfaces;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBacktestLab;

/// <summary>
/// Duenner Loader fuer den <see cref="SymbolInfoCache"/> im Lab — analog <see cref="CachingPublicClient"/>.
/// Laedt einmalig die BingX-Contract-Details (oeffentlicher Endpoint, kein API-Key) und cached die
/// rohe Response als JSON unter <c>.symbolinfo-cache/contracts.json</c>. Bei Re-Runs wird die Datei
/// statt eines erneuten HTTP-Calls genutzt.
///
/// Warum ueber einen Stub-HttpMessageHandler statt direkt JSON in den Cache zu schieben:
/// <see cref="SymbolInfoCache"/> (Produktivklasse, von <c>BingXRestClient</c> mitgenutzt) bietet nur
/// <see cref="SymbolInfoCache.InitializeAsync"/> zum Befuellen — bewusst keine JSON-Bulk-Load-API, um die
/// Live-Klasse nicht zu erweitern. Beim Cache-Hit wird die gespeicherte Response durch einen In-Process-
/// Handler an dasselbe <c>InitializeAsync</c> zurueckgegeben (gleicher Parse-Pfad wie live, kein Netz).
///
/// Rueckgabe ist die fertige <see cref="SymbolInfoCache"/>-Instanz (implementiert <see cref="ISymbolInfoProvider"/>).
/// </summary>
public static class BingXSymbolInfoProvider
{
    private const string ContractsUrl = "https://open-api.bingx.com/openApi/swap/v2/quote/contracts";

    /// <summary>
    /// Liefert einen befuellten <see cref="SymbolInfoCache"/> (= <see cref="ISymbolInfoProvider"/>).
    /// Bei vorhandenem Disk-Cache ohne HTTP-Call, sonst genau ein Request gegen den public Endpoint.
    /// </summary>
    public static async Task<ISymbolInfoProvider> LoadAsync(string cacheDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheDir);
        var path = Path.Combine(cacheDir, "contracts.json");

        var cache = new SymbolInfoCache(NullLogger.Instance);

        // Cache-Hit: gespeicherte Response durch einen Stub-Handler an InitializeAsync zuruechgeben.
        if (File.Exists(path))
        {
            var cachedBody = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            using var stubHttp = new HttpClient(new StubResponseHandler(cachedBody));
            await cache.InitializeAsync(stubHttp).ConfigureAwait(false);
            if (cache.IsInitialized)
                return cache;
            // Korrupter/leerer Cache → frisch laden.
        }

        // Cache-Miss: einmal echt laden, rohe Response fuer kuenftige Runs auf die Platte schreiben.
        using var realHttp = new HttpClient();
        using var captureHttp = new HttpClient(new CapturingHandler(realHttp, async body =>
        {
            try { await File.WriteAllTextAsync(path, body, ct).ConfigureAwait(false); }
            catch { /* Cache-Schreibfehler ist nicht fatal */ }
        }));
        await cache.InitializeAsync(captureHttp).ConfigureAwait(false);
        return cache;
    }

    /// <summary>Antwortet auf jeden Request mit dem gecachten Body (kein Netz).</summary>
    private sealed class StubResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    /// <summary>Reicht den Request an den echten Handler durch und meldet den Response-Body zum Cachen.</summary>
    private sealed class CapturingHandler(HttpClient inner, Func<string, Task> onBody) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await inner.GetAsync(ContractsUrl, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await onBody(body).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }
}
