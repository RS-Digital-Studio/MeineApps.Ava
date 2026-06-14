using System.Net.Http.Json;
using System.Text.Json;
using BingXBot.Contracts.Dto;

namespace BingXBot.ClientApi.Http;

/// <summary>
/// Hilfsmethoden fuer REST-Calls. Uniformes Fehlerhandling: Liest ErrorResponse und wirft ApiException.
/// </summary>
public static class HttpRequestExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string path, CancellationToken ct = default)
    {
        using var response = await client.GetAsync(path, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response, ct).ConfigureAwait(false);
    }

    public static async Task PutJsonAsync<TRequest>(this HttpClient client, string path, TRequest payload, CancellationToken ct = default)
    {
        using var response = await client.PutAsJsonAsync(path, payload, JsonOptions, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
            throw ApiException.From(response.StatusCode, err, $"PUT {path} fehlgeschlagen");
        }
    }

    public static async Task<T> PostJsonAsync<TRequest, T>(this HttpClient client, string path, TRequest payload, CancellationToken ct = default)
    {
        using var response = await client.PostAsJsonAsync(path, payload, JsonOptions, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response, ct).ConfigureAwait(false);
    }

    public static async Task PostEmptyAsync(this HttpClient client, string path, CancellationToken ct = default)
    {
        using var response = await client.PostAsync(path, content: null, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
            throw ApiException.From(response.StatusCode, err, $"POST {path} fehlgeschlagen");
        }
    }

    public static async Task<T> PostAsync<T>(this HttpClient client, string path, CancellationToken ct = default)
    {
        using var response = await client.PostAsync(path, content: null, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
            throw ApiException.From(response.StatusCode, err, $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} fehlgeschlagen");
        }
        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        if (data == null) throw new ApiException(response.StatusCode, "Leere Antwort");
        return data;
    }

    private static async Task<ErrorResponse?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct).ConfigureAwait(false); }
        catch { return null; }
    }
}
