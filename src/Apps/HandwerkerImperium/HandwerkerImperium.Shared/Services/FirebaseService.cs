using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Firebase REST API Client: Anonymous Auth + Realtime Database.
/// Funktioniert auf Android und Desktop ohne native SDKs.
/// </summary>
public sealed class FirebaseService : IFirebaseService, IDisposable
{
    // Firebase-Projekt: handwerkerimperium-487917
    private const string ApiKey = "AIzaSyCyfSD0g7TZR1CNgjPlc9L3SyfNwbEst9k";
    private const string DatabaseUrl = "https://handwerkerimperium-487917-default-rtdb.europe-west1.firebasedatabase.app";
    private const string AuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string TokenRefreshUrl = "https://securetoken.googleapis.com/v1/token?key=";

    private const string PrefKeyUid = "firebase_uid";
    private const string PrefKeyRefreshToken = "firebase_refresh_token";
    private const string PrefKeyPlayerId = "player_id";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55); // 5min vor Ablauf refreshen
    private const int RetryDelayMs = 500;

    private readonly IPreferencesService _preferences;
    private readonly ILogService _log;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private string? _uid;
    private string? _playerId;
    private string? _idToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string? Uid => _uid;
    public string? PlayerId => _playerId;
    public bool IsOnline { get; private set; }

    public FirebaseService(IPreferencesService preferences, ILogService log)
    {
        _preferences = preferences;
        _log = log;
        _httpClient = new HttpClient { Timeout = RequestTimeout };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER ID (stabile Identität, überlebt Firebase-Account-Wechsel)
    // ═══════════════════════════════════════════════════════════════════════

    public void InitializePlayerId(string? gameStateBackup)
    {
        if (!string.IsNullOrEmpty(_playerId)) return;

        // 1. Aus Preferences laden
        var saved = _preferences.Get<string?>(PrefKeyPlayerId, null);
        if (!string.IsNullOrEmpty(saved))
        {
            _playerId = saved;
            return;
        }

        // 2. Aus GameState-Backup wiederherstellen
        if (!string.IsNullOrEmpty(gameStateBackup))
        {
            _playerId = gameStateBackup;
            _preferences.Set(PrefKeyPlayerId, _playerId);
            return;
        }

        // 3. Neue GUID generieren (Erststart)
        _playerId = Guid.NewGuid().ToString("N");
        _preferences.Set(PrefKeyPlayerId, _playerId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTH
    // ═══════════════════════════════════════════════════════════════════════

    public async Task EnsureAuthenticatedAsync()
    {
        if (!await _authLock.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false))
            throw new InvalidOperationException("Firebase Auth-Lock Timeout");
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

                // Token refreshen (mit Retry bei Netzwerk-Problemen)
                if (await TryRefreshTokenAsync().ConfigureAwait(false))
                {
                    // auth_to_player Mapping aktualisieren (fire-and-forget)
                    SyncAuthToPlayerMappingAsync().SafeFireAndForget();
                    return;
                }

                await Task.Delay(RetryDelayMs).ConfigureAwait(false);
                if (await TryRefreshTokenAsync().ConfigureAwait(false))
                {
                    SyncAuthToPlayerMappingAsync().SafeFireAndForget();
                    return;
                }

                // Refresh fehlgeschlagen → neuen anonymen Account als Fallback erstellen.
                // Sicher seit PlayerId-Migration: Spielerdaten sind an PlayerId gebunden, nicht an UID.
                _log.Error("Token-Refresh fehlgeschlagen, erstelle neuen anonymen Account als Fallback");
                try
                {
                    await SignUpAnonymouslyAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Auch Signup fehlgeschlagen → wirklich offline
                    _log.Error("Fallback-Signup fehlgeschlagen", ex);
                    IsOnline = false;
                    throw new InvalidOperationException("Firebase Auth komplett fehlgeschlagen", ex);
                }
            }
            else
            {
                // Erster Start ohne gespeicherten Account → neuen anonymen Account erstellen
                await SignUpAnonymouslyAsync().ConfigureAwait(false);
            }

            // auth_to_player Mapping schreiben (fire-and-forget)
            SyncAuthToPlayerMappingAsync().SafeFireAndForget();
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

        var response = await _httpClient.PostAsync(AuthUrl + ApiKey, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
            // Sicherheit: Refresh-Token URL-kodieren (kann Sonderzeichen enthalten)
            var requestBody = $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(_refreshToken ?? "")}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.PostAsync(TokenRefreshUrl + ApiKey, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var tokenResponse = JsonSerializer.Deserialize<FirebaseTokenResponse>(json);

            if (tokenResponse == null)
                return false;

            _idToken = tokenResponse.IdToken;
            _refreshToken = tokenResponse.RefreshToken;
            _uid = tokenResponse.UserId;
            _tokenExpiry = DateTime.UtcNow.Add(TokenLifetime);
            IsOnline = true;

            // Credentials speichern (UID + Refresh-Token)
            _preferences.Set(PrefKeyUid, _uid);
            _preferences.Set(PrefKeyRefreshToken, _refreshToken);

            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Firebase Token-Refresh fehlgeschlagen", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTH-TO-PLAYER MAPPING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schreibt das Mapping auth.uid → PlayerId in Firebase.
    /// Security Rules nutzen dieses Mapping um PlayerId-basierte Pfade zu autorisieren.
    /// </summary>
    public async Task SyncAuthToPlayerMappingAsync()
    {
        if (string.IsNullOrEmpty(_uid) || string.IsNullOrEmpty(_playerId) || string.IsNullOrEmpty(_idToken))
            return;

        try
        {
            var url = $"{DatabaseUrl}/auth_to_player/{_uid}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(_playerId);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(url, content);
        }
        catch (Exception ex)
        {
            _log.Error("Auth-to-Player Mapping fehlgeschlagen", ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATABASE OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";

            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

            // Jede HTTP-Antwort beweist: Server erreichbar
            IsOnline = true;

            // 401 → Token refreshen und nochmal versuchen
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.GetAsync(url);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase GET {path}: {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase GET {path} fehlgeschlagen", ex);
            IsOnline = false;
            return null;
        }
    }

    public async Task<bool> SetAsync<T>(string path, T data)
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content).ConfigureAwait(false);
            IsOnline = true;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PutAsync(url, content);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase SET {path}: {(int)response.StatusCode}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase SET {path} fehlgeschlagen", ex);
            IsOnline = false;
            return false;
        }
    }

    public async Task<bool> UpdateAsync(string path, Dictionary<string, object> updates)
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(updates);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            IsOnline = true;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                response = await _httpClient.SendAsync(request);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase UPDATE {path}: {(int)response.StatusCode}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase UPDATE {path} fehlgeschlagen", ex);
            IsOnline = false;
            return false;
        }
    }

    public async Task<string?> PushAsync<T>(string path, T data)
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content).ConfigureAwait(false);
            IsOnline = true;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.PostAsync(url, content);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase PUSH {path}: {(int)response.StatusCode}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(responseJson);
            return result?.GetValueOrDefault("name");
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase PUSH {path} fehlgeschlagen", ex);
            IsOnline = false;
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string path)
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";

            var response = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            IsOnline = true;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}";
                response = await _httpClient.DeleteAsync(url);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase DELETE {path}: {(int)response.StatusCode}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase DELETE {path} fehlgeschlagen", ex);
            IsOnline = false;
            return false;
        }
    }

    public async Task<string?> QueryAsync(string path, string queryParams)
    {
        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            var url = $"{DatabaseUrl}/{path}.json?auth={_idToken}&{queryParams}";

            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            IsOnline = true;

            // 401 → Token refreshen und nochmal versuchen
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await ForceRefreshAndRetryAuth().ConfigureAwait(false);
                url = $"{DatabaseUrl}/{path}.json?auth={_idToken}&{queryParams}";
                response = await _httpClient.GetAsync(url);
                IsOnline = true;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Error($"Firebase QUERY {path}: {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(json) || json == "null")
                return null;

            return json;
        }
        catch (Exception ex)
        {
            _log.Error($"Firebase QUERY {path} fehlgeschlagen", ex);
            IsOnline = false;
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task ForceRefreshAndRetryAuth()
    {
        // Cooldown: Nicht bei jedem Request den Token refreshen wenn der Refresh fehlschlägt
        _tokenExpiry = DateTime.UtcNow.AddMinutes(-1);
        _idToken = null;

        try
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false); // Refresh-Versuch
        }
        catch (InvalidOperationException)
        {
            // Token-Refresh dauerhaft fehlgeschlagen → Cooldown setzen damit nicht jeder
            // Request den Refresh erneut triggert (sonst sind alle Firebase-Features tot).
            // _idToken muss gesetzt sein, sonst greift der Guard in EnsureAuthenticatedAsync
            // (Zeile: _idToken != null && UtcNow < _tokenExpiry) nicht und der Cooldown ist wirkungslos.
            _idToken = "expired_cooldown";
            _tokenExpiry = DateTime.UtcNow.AddMinutes(5);
            IsOnline = false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _authLock.Dispose();
    }
}
