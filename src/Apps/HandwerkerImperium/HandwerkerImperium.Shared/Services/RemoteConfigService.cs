using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Firebase-Remote-Config-Wrapper. Liest aus <c>remote_config/*</c> in der Firebase-Realtime-DB.
/// Cached das letzte erfolgreiche Ergebnis im <see cref="IPreferencesService"/> (JSON-String) —
/// ein Offline-Start fallback auf die zuletzt bekannten Werte, ein kalter Erststart fallback auf die Defaults.
/// </summary>
public sealed class RemoteConfigService : IRemoteConfigService
{
    private const string PrefKeyCache = "remote_config_cache_json";
    private const string PrefKeyLastFetched = "remote_config_last_fetched";
    private const string FirebasePath = "remote_config";

    private readonly IFirebaseService _firebase;
    private readonly IPreferencesService _preferences;
    private readonly ILogService _log;

    private Dictionary<string, object?> _values = new();

    public RemoteConfigService(IFirebaseService firebase, IPreferencesService preferences, ILogService log)
    {
        _firebase = firebase;
        _preferences = preferences;
        _log = log;

        // Cache aus Preferences laden (vor dem ersten Download nutzbar)
        TryLoadFromCache();
    }

    public DateTime? LastFetchedAt { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            var json = await _firebase.QueryAsync(FirebasePath, "").ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
            {
                _log.Info("RemoteConfig: Kein Datensatz in Firebase — verwende Cache/Defaults.");
                return;
            }

            var parsed = ParseJson(json);
            if (parsed == null) return;

            _values = parsed;
            LastFetchedAt = DateTime.UtcNow;

            _preferences.Set(PrefKeyCache, json);
            _preferences.Set(PrefKeyLastFetched, LastFetchedAt.Value.ToString("O"));
        }
        catch (Exception ex)
        {
            _log.Error("RemoteConfig-Download fehlgeschlagen — verwende Cache/Defaults.", ex);
        }
    }

    public int GetInt(string key, int defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) => r,
            _ => defaultValue
        };
    }

    public decimal GetDecimal(string key, decimal defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        return raw switch
        {
            decimal dec => dec,
            int i => i,
            long l => l,
            double d => (decimal)d,
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var r) => r,
            _ => defaultValue
        };
    }

    public bool GetBool(string key, bool defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        return raw switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var r) => r,
            int i => i != 0,
            long l => l != 0,
            _ => defaultValue
        };
    }

    public string GetString(string key, string defaultValue)
    {
        if (!_values.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        return raw.ToString() ?? defaultValue;
    }

    private void TryLoadFromCache()
    {
        try
        {
            var cached = _preferences.Get<string?>(PrefKeyCache, null);
            if (string.IsNullOrEmpty(cached)) return;
            var parsed = ParseJson(cached);
            if (parsed != null) _values = parsed;

            var fetchedIso = _preferences.Get<string?>(PrefKeyLastFetched, null);
            if (!string.IsNullOrEmpty(fetchedIso) &&
                DateTime.TryParse(fetchedIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                LastFetchedAt = dt;
            }
        }
        catch (Exception ex)
        {
            _log.Error("RemoteConfig-Cache konnte nicht gelesen werden.", ex);
        }
    }

    /// <summary>
    /// Parst das verschachtelte Remote-Config-JSON in eine flache Map mit Dot-Keys
    /// (z.B. <c>{"balancing":{"foo":1}}</c> → <c>balancing.foo = 1</c>).
    /// </summary>
    private static Dictionary<string, object?>? ParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            Walk(doc.RootElement, "", result);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static void Walk(JsonElement element, string prefix, Dictionary<string, object?> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var childKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    Walk(prop.Value, childKey, result);
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) result[prefix] = l;
                else if (element.TryGetDecimal(out var dec)) result[prefix] = dec;
                else result[prefix] = element.GetDouble();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                result[prefix] = element.GetBoolean();
                break;

            case JsonValueKind.Null:
                result[prefix] = null;
                break;

            case JsonValueKind.Array:
                // Arrays nicht unterstuetzt in dieser Version — als JSON-String ablegen
                result[prefix] = element.GetRawText();
                break;
        }
    }
}
