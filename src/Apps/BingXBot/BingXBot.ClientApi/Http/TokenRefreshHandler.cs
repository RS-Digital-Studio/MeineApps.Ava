using System.Net;
using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Pairing;

namespace BingXBot.ClientApi.Http;

/// <summary>
/// DelegatingHandler: Faengt 401-Responses ab, versucht Token-Refresh per RefreshToken,
/// und sendet den Original-Request danach mit dem neuen Token nochmal.
///
/// Thread-Safety: SemaphoreSlim verhindert Thundering-Herd bei parallelen 401.
/// DI-Zirkel-Aufloesung: Die Connection wird via `Func&lt;ServerConnection&gt;` injiziert
/// (Late-Binding). So kann HttpClient vor ServerConnection instanziiert werden, ohne
/// dass der Handler die Connection vorab kennen muss.
/// </summary>
public sealed class TokenRefreshHandler : DelegatingHandler
{
    private readonly Func<ServerConnection> _connectionProvider;
    private readonly Func<PairingClient> _pairingProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public TokenRefreshHandler(Func<ServerConnection> connectionProvider, Func<PairingClient> pairingProvider)
    {
        _connectionProvider = connectionProvider;
        _pairingProvider = pairingProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Content in-memory puffern, damit der Request bei einem 401-Retry wiederholbar ist. Ohne das
        // war bei PUT/POST der Body nach dem ersten Send konsumiert → Settings-Saves/Bot-Start brachen
        // bei 401 ab (zudem kann ein HttpRequestMessage nicht zweimal gesendet werden → Clone beim Retry).
        if (request.Content != null)
            await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var connection = _connectionProvider();
        if (response.StatusCode != HttpStatusCode.Unauthorized || connection.Profile == null)
            return response;

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Vielleicht hat ein anderer Request den Token schon erneuert?
            var currentToken = connection.CurrentToken;
            if (currentToken != null && request.Headers.Authorization?.Parameter != currentToken)
            {
                response.Dispose();
                return await RetryWithTokenAsync(request, currentToken, cancellationToken).ConfigureAwait(false);
            }

            var refreshed = await _pairingProvider().RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
                return response; // Refresh fehlgeschlagen — dem Client die 401 zurueckgeben

            response.Dispose();
            return await RetryWithTokenAsync(request, connection.CurrentToken, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Klont den Request (inkl. gepuffertem Content) und sendet ihn mit dem neuen Token erneut.</summary>
    private async Task<HttpResponseMessage> RetryWithTokenAsync(HttpRequestMessage original, string? token, CancellationToken ct)
    {
        var clone = await CloneRequestAsync(original, ct).ConfigureAwait(false);
        if (token != null)
            clone.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(clone, ct).ConfigureAwait(false);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri) { Version = req.Version };

        if (req.Content != null)
        {
            var bytes = await req.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (var h in req.Content.Headers)
                content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Content = content;
        }

        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        foreach (var opt in req.Options)
            ((IDictionary<string, object?>)clone.Options)[opt.Key] = opt.Value;

        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshLock.Dispose();
        base.Dispose(disposing);
    }
}
