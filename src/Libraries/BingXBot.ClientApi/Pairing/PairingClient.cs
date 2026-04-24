using BingXBot.ClientApi.Connection;
using BingXBot.ClientApi.Http;
using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;

namespace BingXBot.ClientApi.Pairing;

/// <summary>
/// Steuert den 6-stelligen Pairing-Flow zum BingXBot-Server.
/// Der Code wird auf dem Pi generiert und direkt dort abgelesen (SSH/Display/Log).
/// Nach erfolgreichem Pairing wird der Token in der ServerConnection persistiert.
/// </summary>
public sealed class PairingClient : IDisposable
{
    private readonly ServerConnection _connection;
    // Wiederverwendeter HttpClient — vermeidet Socket-Exhaustion bei haeufigem Pairing (Health-Checks alle paar Sekunden).
    // Kein DefaultRequestHeaders.Authorization — Pairing-Endpoints laufen ohne Token.
    private readonly HttpClient _httpClient;

    public PairingClient(ServerConnection connection)
    {
        _connection = connection;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public void Dispose() => _httpClient.Dispose();

    /// <summary>Ruft /pair/init am Server auf.</summary>
    public async Task<PairInitResponse> InitiateAsync(string baseUrl, string deviceName, CancellationToken ct = default)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + ApiRoutes.PairInit);
        var response = await _httpClient.PostAsync(uri, JsonContent(new PairInitRequest(deviceName)), ct).ConfigureAwait(false);
        return await HttpResponseJson<PairInitResponse>(response, ct).ConfigureAwait(false);
    }

    /// <summary>Bricht ein laufendes Pairing ab (z.B. wenn User den Code verlegt hat).
    /// Exceptions werden geschluckt — Cancel ist best-effort.</summary>
    public async Task CancelAsync(string baseUrl, string pairingId, CancellationToken ct = default)
    {
        try
        {
            var uri = new Uri(baseUrl.TrimEnd('/') + ApiRoutes.PairCancel);
            await _httpClient.PostAsync(uri, JsonContent(new PairCancelRequest(pairingId)), ct).ConfigureAwait(false);
        }
        catch { /* best-effort: Client kann den Code auf Server-Seite auch per Timeout erloeschen lassen */ }
    }

    /// <summary>Vollendet das Pairing: Gibt den eingegebenen Code + DeviceId an den Server.</summary>
    public async Task CompleteAsync(string baseUrl, string pairingId, string code, string deviceName, CancellationToken ct = default)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + ApiRoutes.PairComplete);
        var request = new PairCompleteRequest(pairingId, code, _connection.DeviceId);
        var response = await _httpClient.PostAsync(uri, JsonContent(request), ct).ConfigureAwait(false);
        var result = await HttpResponseJson<PairCompleteResponse>(response, ct).ConfigureAwait(false);

        var profile = new ServerProfile(
            BaseUrl: baseUrl.TrimEnd('/'),
            Token: result.Token,
            RefreshToken: result.RefreshToken,
            DeviceId: _connection.DeviceId,
            DeviceName: deviceName,
            SchemaVersion: result.SchemaVersion,
            PairedAtUtc: DateTime.UtcNow);
        _connection.SetProfile(profile);
    }

    /// <summary>Validiert ob die aktuelle BaseUrl erreichbar ist (Health-Check ohne Auth).</summary>
    public async Task<bool> PingHealthAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            var uri = new Uri(baseUrl.TrimEnd('/') + ApiRoutes.Health);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(5));
            var resp = await _httpClient.GetAsync(uri, linkedCts.Token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>Erneuert Token anhand des persistierten RefreshTokens.</summary>
    public async Task<bool> RefreshTokenAsync(CancellationToken ct = default)
    {
        if (_connection.Profile == null) return false;
        var baseUrl = _connection.Profile.BaseUrl;
        var refreshToken = _connection.Profile.RefreshToken;
        try
        {
            var uri = new Uri(baseUrl.TrimEnd('/') + ApiRoutes.AuthRefresh);
            var response = await _httpClient.PostAsync(uri, JsonContent(new TokenRefreshRequest(refreshToken)), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            var result = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<TokenRefreshResponse>(response.Content, options: null, ct).ConfigureAwait(false);
            if (result == null) return false;
            _connection.UpdateTokens(result.Token, result.RefreshToken);
            return true;
        }
        catch { return false; }
    }

    private static HttpContent JsonContent<T>(T payload) =>
        System.Net.Http.Json.JsonContent.Create(payload);

    private static async Task<T> HttpResponseJson<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? err = null;
            try { err = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<ErrorResponse>(response.Content, (System.Text.Json.JsonSerializerOptions?)null, ct).ConfigureAwait(false); }
            catch { }
            throw ApiException.From(response.StatusCode, err, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}");
        }
        var data = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(response.Content, (System.Text.Json.JsonSerializerOptions?)null, ct).ConfigureAwait(false);
        if (data == null) throw new ApiException(response.StatusCode, "Leere Antwort");
        return data;
    }
}

internal static class JsonContentHelper
{
    public static async Task<T?> ReadFromJsonAsync<T>(this HttpContent content, CancellationToken ct) =>
        await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(content, cancellationToken: ct).ConfigureAwait(false);
}
