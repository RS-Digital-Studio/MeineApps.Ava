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
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", currentToken);
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var refreshed = await _pairingProvider().RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
                return response; // Refresh fehlgeschlagen — dem Client die 401 zurueckgeben

            response.Dispose();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", connection.CurrentToken);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refreshLock.Dispose();
        base.Dispose(disposing);
    }
}
