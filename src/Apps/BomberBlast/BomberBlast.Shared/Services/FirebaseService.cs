using System.Text;
using System.Text.Json;
using BomberBlast.Models.Firebase;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Firebase REST API Client: Anonymous Auth + Realtime Database.
/// Funktioniert auf Android und Desktop ohne native SDKs.
/// </summary>
public class FirebaseService : IFirebaseService
{
    // Firebase-Projekt: bomberblast-league
    private const string ApiKey = "AIzaSyDr63VL86diNabbjQjeXX9Dal02cQF6CHs";
    private const string DatabaseUrl = "https://bomberblast-league-default-rtdb.europe-west1.firebasedatabase.app";
    private const string AuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string TokenRefreshUrl = "https://securetoken.googleapis.com/v1/token?key=";

    private const string PrefKeyUid = "firebase_uid";
    private const string PrefKeyRefreshToken = "firebase_refresh_token";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55); // 5min vor Ablauf refreshen

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

                // Token refreshen
                if (await TryRefreshTokenAsync())
                    return;
            }

            // Neuen anonymen Account erstellen
            await SignUpAnonymouslyAsync();
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

    public async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";

            var response = await _httpClient.GetAsync(url);

            // 401 → Token refreshen und nochmal versuchen
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.GetAsync(url);
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
            return JsonSerializer.Deserialize<T>(json);
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
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.PutAsync(url, content);
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
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(updates);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                response = await _httpClient.SendAsync(request);
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
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.PostAsync(url, content);
            }

            if (!response.IsSuccessStatusCode)
            {
                IsOnline = false;
                return null;
            }

            IsOnline = true;
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(responseJson);
            return result?.GetValueOrDefault("name");
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
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";

            var response = await _httpClient.DeleteAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth();
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.DeleteAsync(url);
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
