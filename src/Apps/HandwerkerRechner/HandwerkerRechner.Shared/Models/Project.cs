using System.Text.Json;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerRechner.Models;

/// <summary>
/// Repräsentiert ein gespeichertes Handwerker-Projekt
/// </summary>
public class Project
{
    /// <summary>Eindeutige ID (GUID)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Projekt-Name (vom Benutzer vergeben)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Beschreibung (optional)</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Rechner-Typ (z.B. Tiles, Wallpaper, etc.)</summary>
    public CalculatorType CalculatorType { get; set; }

    /// <summary>Projekt-Daten als JSON-String</summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>Erstellungsdatum</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Letzte Änderung</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    // Cache für deserialisierte Daten (vermeidet wiederholte Deserialisierung)
    [System.Text.Json.Serialization.JsonIgnore]
    private Dictionary<string, object>? _cachedData;

    /// <summary>
    /// Setzt die Projekt-Daten aus einem Dictionary
    /// </summary>
    public void SetData(Dictionary<string, object> data)
    {
        DataJson = JsonSerializer.Serialize(data);
        _cachedData = null; // Cache invalidieren
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Liest die Projekt-Daten als Dictionary (gecacht)
    /// </summary>
    public Dictionary<string, object>? GetData()
    {
        if (_cachedData != null) return _cachedData;

        try
        {
            _cachedData = JsonSerializer.Deserialize<Dictionary<string, object>>(DataJson);
            return _cachedData;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Liest einen spezifischen Wert aus den Projekt-Daten
    /// </summary>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        var data = GetData();
        if (data == null || !data.ContainsKey(key))
            return defaultValue;

        try
        {
            var element = (JsonElement)data[key];
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Anzeigename für UI (Name + Rechner-Typ)
    /// </summary>
    public string DisplayName => $"{Name} ({GetCalculatorTypeName()})";

    /// <summary>
    /// Formatiertes Datum für UI (locale-aware)
    /// </summary>
    public string LastModifiedDisplay => LastModified.ToLocalTime().ToString("g");

    private string GetCalculatorTypeName()
    {
        var loc = LocalizationManager.Service;
        if (loc == null) return CalculatorType.ToString();

        var key = "CalcType" + CalculatorType;
        return loc.GetString(key) ?? CalculatorType.ToString();
    }
}
