using System.Reflection;
using System.Text.Json;

namespace BomberBlast.Services;

/// <summary>
/// Standard-Implementation von <see cref="IRemoteConfigService"/> die Werte aus einer
/// eingebetteten JSON-Datei laedt (Sprint 2.1 AAA-Audit #1).
///
/// <para>
/// Liefert produktionsreife Defaults — App funktioniert vollstaendig auch ohne Firebase-Backend.
/// FirebaseRemoteConfigService (Android) erweitert diese Klasse spaeter und ueberschreibt
/// einzelne Werte via Cloud-Fetch.
/// </para>
///
/// <para>
/// JSON-Pfad: <c>BomberBlast.Resources.remote_config_defaults.json</c> (eingebettet als
/// EmbeddedResource via csproj). Bei Parse-Fehler: Logger-Warning, Service liefert die im
/// Code definierten Hard-Defaults (per <see cref="GetXxx"/>-Parameter).
/// </para>
///
/// <para>
/// Thread-safe: <see cref="SetOverride"/> nutzt einen internen Lock, GetXxx ist lock-frei
/// (lese-only nachdem Init durch ist). Override-Pattern erlaubt zukuenftigem
/// FirebaseRemoteConfigService einzelne Keys zu ueberschreiben ohne diese Klasse zu erweitern.
/// </para>
/// </summary>
public sealed class DefaultsRemoteConfigService : IRemoteConfigService
{
    private const string DefaultsResourceName = "BomberBlast.Resources.remote_config_defaults.json";

    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private readonly object _writeLock = new();
    private readonly IAppLogger _logger;

    public event Action? ConfigChanged;

    public DefaultsRemoteConfigService(IAppLogger logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        try
        {
            var asm = typeof(DefaultsRemoteConfigService).Assembly;
            using var stream = asm.GetManifestResourceStream(DefaultsResourceName);
            if (stream is null)
            {
                _logger.LogWarning($"RemoteConfig: Embedded-Resource '{DefaultsResourceName}' nicht gefunden — laufe mit Hard-Defaults.");
                return Task.CompletedTask;
            }

            using var doc = JsonDocument.Parse(stream);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("$", StringComparison.Ordinal))
                    continue;  // $comment, $version etc. ueberspringen
                _values[prop.Name] = prop.Value.Clone();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("RemoteConfig: Defaults-JSON konnte nicht geparst werden — laufe mit Hard-Defaults.", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// NullImpl fuer Defaults-only-Service: kein Server-Fetch moeglich. Liefert immer false.
    /// FirebaseRemoteConfigService ueberschreibt diese Methode mit echtem Firebase-Fetch.
    /// </summary>
    public Task<bool> FetchAndActivateAsync() => Task.FromResult(false);

    public bool GetBool(string key, bool defaultValue)
    {
        if (_values.TryGetValue(key, out var el))
        {
            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => el.GetInt64() != 0,
                    _ => defaultValue,
                };
            }
            catch { return defaultValue; }
        }
        return defaultValue;
    }

    public long GetLong(string key, long defaultValue)
    {
        if (_values.TryGetValue(key, out var el))
        {
            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetInt64(),
                    JsonValueKind.True => 1L,
                    JsonValueKind.False => 0L,
                    _ => defaultValue,
                };
            }
            catch { return defaultValue; }
        }
        return defaultValue;
    }

    public double GetDouble(string key, double defaultValue)
    {
        if (_values.TryGetValue(key, out var el))
        {
            try
            {
                return el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetDouble(),
                    JsonValueKind.True => 1.0,
                    JsonValueKind.False => 0.0,
                    _ => defaultValue,
                };
            }
            catch { return defaultValue; }
        }
        return defaultValue;
    }

    public string GetString(string key, string defaultValue)
    {
        if (_values.TryGetValue(key, out var el))
        {
            try
            {
                return el.ValueKind == JsonValueKind.String
                    ? el.GetString() ?? defaultValue
                    : defaultValue;
            }
            catch { return defaultValue; }
        }
        return defaultValue;
    }

    /// <summary>
    /// Ueberschreibt einen Key zur Laufzeit (z.B. nach Firebase-Fetch).
    /// FirebaseRemoteConfigService nutzt das nach erfolgreichem FetchAndActivate.
    /// </summary>
    public void SetOverride(string key, JsonElement value)
    {
        lock (_writeLock)
        {
            _values[key] = value.Clone();
        }
    }

    /// <summary>Loest <see cref="ConfigChanged"/> aus — von FirebaseImpl nach Bulk-Update aufgerufen.</summary>
    internal void RaiseConfigChanged() => ConfigChanged?.Invoke();
}
