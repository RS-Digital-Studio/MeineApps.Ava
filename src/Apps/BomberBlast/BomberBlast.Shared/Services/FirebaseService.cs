using System.Text;
using System.Text.Json;
using BomberBlast.Models.Firebase;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Firebase REST API Client: Anonymous Auth + Realtime Database.
/// Funktioniert auf Android und Desktop ohne native SDKs.
/// </summary>
public sealed class FirebaseService : IFirebaseService
{
    // Firebase-Projekt: bomberblast-league
    // Web-API-Key: Bewusst im Quellcode - in jeder APK (google-services.json) ohnehin
    // extrahierbar. Schutz über Firebase Security Rules, nicht Key-Geheimhaltung.
    private const string ApiKey = "AIzaSyDr63VL86diNabbjQjeXX9Dal02cQF6CHs";
    private const string DatabaseUrl = "https://bomberblast-league-default-rtdb.europe-west1.firebasedatabase.app";
    private const string AuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string TokenRefreshUrl = "https://securetoken.googleapis.com/v1/token?key=";

    private const string PrefKeyUid = "firebase_uid";
    private const string PrefKeyRefreshToken = "firebase_refresh_token";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55); // 5min vor Ablauf refreshen

    /// <summary>
    /// JSON-Options mit CamelCase-Naming. Bestand-Convention bei BomberBlast:
    /// LeagueEntry { Name, Points } → JSON { "name": ..., "points": ... }.
    /// Konsistent mit Firebase-Rules-Schema (database.rules.bomberblast.json).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IPreferencesService _preferences;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private string? _uid;
    private string? _idToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string? Uid => _uid;
    public bool IsOnline { get; private set; }

    public FirebaseService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _httpClient = new HttpClient { Timeout = RequestTimeout };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTH
    // ═══════════════════════════════════════════════════════════════════════

    public async Task EnsureAuthenticatedAsync()
    {
        await _authLock.WaitAsync();
        try
        {
            // Token noch gültig?
            if (_idToken != null && DateTime.UtcNow < _tokenExpiry)
                return;

            // Gespeicherte Credentials laden
            var savedUid = _preferences.Get<string?>(PrefKeyUid, null);
            var savedRefreshToken = _preferences.Get<string?>(PrefKeyRefreshToken, null);

            if (!string.IsNullOrEmpty(savedUid) && !string.IsNullOrEmpty(savedRefreshToken))
            {
                _uid = savedUid;
                _refreshToken = savedRefreshToken;

                // Token refreshen (kein Exception-Wurf — TryRefreshTokenAsync hat eigenes try/catch)
                if (await TryRefreshTokenAsync())
                    return;
            }

            // Neuen anonymen Account erstellen
            // Audit H10: try/catch um SignUp/Refresh — Captive-Portal/Offline darf nicht in
            // ungefangener HttpRequestException muenden (TaskScheduler.UnobservedTaskException
            // koennte den Release-Process killen). IsOnline=false signalisiert Offline-Status.
            try
            {
                await SignUpAnonymouslyAsync();
            }
            catch (HttpRequestException)
            {
                IsOnline = false;
            }
            catch (TaskCanceledException)
            {
                IsOnline = false; // Timeout (5s) — Captive Portal oder Offline
            }
        }
        finally
        {
            _authLock.Release();
        }
    }

    private async Task SignUpAnonymouslyAsync()
    {
        var requestBody = JsonSerializer.Serialize(new { returnSecureToken = true });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(AuthUrl + ApiKey, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(json);

        if (authResponse == null)
            throw new InvalidOperationException("Firebase Auth fehlgeschlagen");

        _uid = authResponse.LocalId;
        _idToken = authResponse.IdToken;
        _refreshToken = authResponse.RefreshToken;
        _tokenExpiry = DateTime.UtcNow.Add(TokenLifetime);
        IsOnline = true;

        // Credentials speichern
        _preferences.Set(PrefKeyUid, _uid);
        _preferences.Set(PrefKeyRefreshToken, _refreshToken);
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            var requestBody = $"grant_type=refresh_token&refresh_token={_refreshToken}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.PostAsync(TokenRefreshUrl + ApiKey, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<FirebaseTokenResponse>(json);

            if (tokenResponse == null)
                return false;

            _idToken = tokenResponse.IdToken;
            _refreshToken = tokenResponse.RefreshToken;
            _uid = tokenResponse.UserId;
            _tokenExpiry = DateTime.UtcNow.Add(TokenLifetime);
            IsOnline = true;

            // Neuen Refresh-Token speichern
            _preferences.Set(PrefKeyRefreshToken, _refreshToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATABASE OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════
    //
    // Auth: Firebase REST API unterstuetzt Authorization: Bearer <token> Header
    // (https://firebase.google.com/docs/database/rest/auth). Bearer-Header statt
    // ?auth=<token>-Query-Parameter verhindert Token-Leak in Proxy-Logs, Crashlytics-
    // Stacktraces (URL als Context) und Firebase-Audit-Logs (Audit C06).

    /// <summary>Erstellt eine Request-Message mit Authorization-Bearer-Header (kein Token in URL).</summary>
    private HttpRequestMessage BuildAuthenticatedRequest(HttpMethod method, string path, HttpContent? content = null)
    {
        var url = $"{DatabaseUrl}/{path}.json";
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_idToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _idToken);
        if (content != null)
            request.Content = content;
        return request;
    }

    public async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var response = await _httpClient.SendAsync(BuildAuthenticatedRequest(HttpMethod.Get, path));

            // 401 → Token refreshen und nochmal versuchen
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                response = await _httpClient.SendAsync(BuildAuthenticatedRequest(HttpMethod.Get, path));
            }

            if (!response.IsSuccessStatusCode)
            {
                IsOnline = false;
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;

            IsOnline = true;
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            IsOnline = false;
            return null;
        }
    }

    public async Task SetAsync<T>(string path, T data)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                HttpMethod.Put, path, new StringContent(json, Encoding.UTF8, "application/json")));

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                // Neuer StringContent — HttpContent darf nicht zweimal gesendet werden (Audit H13)
                response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                    HttpMethod.Put, path, new StringContent(json, Encoding.UTF8, "application/json")));
            }

            IsOnline = response.IsSuccessStatusCode;
        }
        catch
        {
            IsOnline = false;
        }
    }

    public async Task UpdateAsync(string path, Dictionary<string, object> updates)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            // PATCH-Updates: Dictionary-Keys werden 1:1 als JSON-Properties uebernommen
            // (kein NamingPolicy-Mapping noetig — Caller setzt bereits camelCase).
            var json = JsonSerializer.Serialize(updates);
            var response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                new HttpMethod("PATCH"), path, new StringContent(json, Encoding.UTF8, "application/json")));

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                    new HttpMethod("PATCH"), path, new StringContent(json, Encoding.UTF8, "application/json")));
            }

            IsOnline = response.IsSuccessStatusCode;
        }
        catch
        {
            IsOnline = false;
        }
    }

    public async Task<string?> PushAsync<T>(string path, T data)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                HttpMethod.Post, path, new StringContent(json, Encoding.UTF8, "application/json")));

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                response = await _httpClient.SendAsync(BuildAuthenticatedRequest(
                    HttpMethod.Post, path, new StringContent(json, Encoding.UTF8, "application/json")));
            }

            if (!response.IsSuccessStatusCode)
            {
                IsOnline = false;
                return null;
            }

            IsOnline = true;
            var responseJson = await response.Content.ReadAsStringAsync();
            // Firebase POST-Response: {"name": "<auto-generated-key>"} — fix-Schema, kein NamingPolicy noetig.
            // Audit L09: Robuster gegen API-Erweiterung — JsonNode statt Dictionary<string,string>,
            // damit zukuenftige neue Felder (z.B. "etag") nicht silent als KeyValuePair geparst werden.
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(responseJson);
                return node?["name"]?.GetValue<string>();
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            IsOnline = false;
            return null;
        }
    }

    public async Task DeleteAsync(string path)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var response = await _httpClient.SendAsync(BuildAuthenticatedRequest(HttpMethod.Delete, path));

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                response = await _httpClient.SendAsync(BuildAuthenticatedRequest(HttpMethod.Delete, path));
            }

            IsOnline = response.IsSuccessStatusCode;
        }
        catch
        {
            IsOnline = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task ForceRefreshAndRetryAuth()
    {
        _tokenExpiry = DateTime.MinValue;
        await EnsureAuthenticatedAsync();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _authLock.Dispose();
    }
}
