using System.Net.Http.Headers;
using System.Text.Json;
using BingXBot.Core.Interfaces;

namespace BingXBot.ClientApi.Connection;

/// <summary>
/// Haelt die laufzeitabhaengigen Verbindungsdaten (BaseUrl, Token, DeviceId) an einer zentralen
/// Stelle. Remote-Impls fragen hier nach dem HttpClient und dem aktuellen Token.
///
/// Wird im Client als Singleton registriert. Beim Pairing wird Token gesetzt, beim Logout geloescht.
/// Persistierung: ClientProfileFolder (plattformabhängig, aus IAppPaths).
/// </summary>
public sealed class ServerConnection
{
    private readonly HttpClient _httpClient;
    private readonly IAppPaths _paths;
    private readonly object _sync = new();
    private ServerProfile? _profile;

    public ServerConnection(HttpClient httpClient, IAppPaths paths)
    {
        _httpClient = httpClient;
        _paths = paths;
    }

    public HttpClient HttpClient => _httpClient;

    public ServerProfile? Profile
    {
        get { lock (_sync) return _profile; }
    }

    public bool IsPaired
    {
        get { lock (_sync) return _profile is not null && !string.IsNullOrEmpty(_profile.Token); }
    }

    public string? CurrentToken
    {
        get { lock (_sync) return _profile?.Token; }
    }

    public string? CurrentBaseUrl
    {
        get { lock (_sync) return _profile?.BaseUrl; }
    }

    public string DeviceId
    {
        get { lock (_sync) return _profile?.DeviceId ?? EnsureDeviceId(); }
    }

    public void SetProfile(ServerProfile profile)
    {
        lock (_sync)
        {
            _profile = profile;
            _httpClient.BaseAddress = new Uri(profile.BaseUrl.TrimEnd('/') + "/");
            _httpClient.DefaultRequestHeaders.Authorization =
                string.IsNullOrEmpty(profile.Token)
                    ? null
                    : new AuthenticationHeaderValue("Bearer", profile.Token);
            PersistProfile(profile);
        }
        Changed?.Invoke(profile);
    }

    public void UpdateTokens(string token, string refreshToken)
    {
        lock (_sync)
        {
            if (_profile == null) return;
            _profile = _profile with { Token = token, RefreshToken = refreshToken };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            PersistProfile(_profile);
        }
        Changed?.Invoke(_profile!);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _profile = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
            DeleteProfileFile();
        }
        Changed?.Invoke(null);
    }

    public event Action<ServerProfile?>? Changed;

    public void LoadPersistedProfile()
    {
        try
        {
            var path = GetProfilePath();
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<ServerProfile>(json);
            if (profile != null) SetProfile(profile);
        }
        catch { /* best effort */ }
    }

    private void PersistProfile(ServerProfile profile)
    {
        try
        {
            var path = GetProfilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
            }
        }
        catch { /* best effort */ }
    }

    private void DeleteProfileFile()
    {
        try
        {
            var path = GetProfilePath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    private string GetProfilePath() => _paths.ClientProfilePath;

    private string EnsureDeviceId()
    {
        // Permanenter DeviceId, an die Maschine gebunden (nicht am Token — Token kann rotieren)
        var folder = _paths.ClientProfileFolder;
        Directory.CreateDirectory(folder);
        var devFile = Path.Combine(folder, "device-id.txt");
        if (File.Exists(devFile)) return File.ReadAllText(devFile).Trim();
        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(devFile, id);
        return id;
    }
}

public sealed record ServerProfile(
    string BaseUrl,
    string Token,
    string RefreshToken,
    string DeviceId,
    string DeviceName,
    int SchemaVersion,
    DateTime PairedAtUtc);
