using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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
public class DefaultsRemoteConfigService : IRemoteConfigService
{
    private const string DefaultsResourceName = "BomberBlast.Resources.remote_config_defaults.json";

    private readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);
    private readonly object _writeLock = new();

    /// <summary>
    /// Logger — auch fuer abgeleitete Implementierungen (FirebaseRemoteConfigService).
    /// Non-generic <see cref="ILogger"/> damit Subclasses ihren eigenen typisierten Logger
    /// durchreichen koennen (z.B. <c>ILogger&lt;FirebaseRemoteConfigService&gt;</c>).
    /// </summary>
    protected ILogger Logger { get; }

    public event Action? ConfigChanged;

    public DefaultsRemoteConfigService(ILogger<DefaultsRemoteConfigService> logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Geschuetzter Ctor fuer abgeleitete Klassen (z.B. FirebaseRemoteConfigService),
    /// die einen typisierten Logger fuer ihren eigenen Typ durchreichen wollen.
    /// </summary>
    protected DefaultsRemoteConfigService(ILogger logger)
    {
        Logger = logger;
    }

    public virtual Task InitializeAsync()
    {
        try
        {
            var asm = typeof(DefaultsRemoteConfigService).Assembly;
            using var stream = asm.GetManifestResourceStream(DefaultsResourceName);
            if (stream is null)
            {
                Logger.LogWarning("RemoteConfig: Embedded-Resource '{Resource}' nicht gefunden — laufe mit Hard-Defaults.", DefaultsResourceName);
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
            Logger.LogError(ex, "RemoteConfig: Defaults-JSON konnte nicht geparst werden — laufe mit Hard-Defaults.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// NullImpl fuer Defaults-only-Service: kein Server-Fetch moeglich. Liefert immer false.
    /// FirebaseRemoteConfigService ueberschreibt diese Methode mit echtem Firebase-Fetch.
    /// </summary>
    public virtual Task<bool> FetchAndActivateAsync() => Task.FromResult(false);

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

    /// <summary>Typsicherer Override fuer Boolean-Werte (Event-Toggles, Feature-Flags).</summary>
    public void SetOverride(string key, bool value)
        => SetOverrideRaw(key, value ? "true" : "false");

    /// <summary>Typsicherer Override fuer Long-Werte (Schwellenwerte, Preise in Cents).</summary>
    public void SetOverride(string key, long value)
        => SetOverrideRaw(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Typsicherer Override fuer Double-Werte (Drop-Raten, Multiplikatoren).</summary>
    public void SetOverride(string key, double value)
        => SetOverrideRaw(key, value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Typsicherer Override fuer String-Werte (URLs, Konfigurations-IDs).</summary>
    public void SetOverride(string key, string value)
        => SetOverrideRaw(key, JsonSerializer.Serialize(value));

    /// <summary>
    /// Parst einen rohen JSON-Literal-String zu einem JsonElement und speichert ihn.
    /// <see cref="SetOverride(string, JsonElement)"/> klont das Element — der temporaere
    /// JsonDocument darf danach disposed werden.
    /// </summary>
    private void SetOverrideRaw(string key, string jsonLiteral)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonLiteral);
            SetOverride(key, doc.RootElement);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "RemoteConfig: Override-Wert fuer '{Key}' nicht parsebar ('{Literal}').", key, jsonLiteral);
        }
    }

    /// <summary>
    /// Uebernimmt einen rohen Remote-Wert (String, wie ihn Firebase Remote Config liefert) und
    /// konvertiert ihn anhand des Typs des bestehenden Default-Werts. Der Override-Provider
    /// (FirebaseRemoteConfigService) muss so keine eigene Typ-Map pflegen — die eingebetteten
    /// JSON-Defaults sind die Single-Source-of-Truth fuer den Typ jedes Keys.
    /// </summary>
    protected void ApplyRawRemoteValue(string key, string rawValue)
    {
        if (string.IsNullOrEmpty(key) || rawValue is null) return;

        JsonValueKind defaultKind;
        lock (_writeLock)
        {
            defaultKind = _values.TryGetValue(key, out var existing)
                ? existing.ValueKind
                : JsonValueKind.Undefined;
        }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        switch (defaultKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (bool.TryParse(rawValue, out var b)) SetOverride(key, b);
                break;

            case JsonValueKind.Number:
                // Ganzzahl bevorzugen (Preise/Schwellen), sonst Double (Drop-Raten/Multiplikatoren).
                if (long.TryParse(rawValue, System.Globalization.NumberStyles.Integer, inv, out var l))
                    SetOverride(key, l);
                else if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float, inv, out var d))
                    SetOverride(key, d);
                break;

            default:
                // String-Default oder unbekannter Key → als String uebernehmen (best-effort).
                SetOverride(key, rawValue);
                break;
        }
    }

    /// <summary>Loest <see cref="ConfigChanged"/> aus — von FirebaseImpl nach Bulk-Update aufgerufen.</summary>
    protected void RaiseConfigChanged() => ConfigChanged?.Invoke();
}
