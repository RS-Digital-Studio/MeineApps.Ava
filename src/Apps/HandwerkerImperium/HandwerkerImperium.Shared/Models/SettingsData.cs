using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Spieler-Einstellungen (Sound, Grafik, Benachrichtigungen etc.).
/// Extrahiert aus GameState (V4) fuer bessere Strukturierung.
/// </summary>
public sealed class SettingsData
{
    [JsonPropertyName("soundEnabled")]
    public bool SoundEnabled { get; set; } = true;

    [JsonPropertyName("musicEnabled")]
    public bool MusicEnabled { get; set; } = true;

    [JsonPropertyName("hapticsEnabled")]
    public bool HapticsEnabled { get; set; } = true;

    [JsonPropertyName("notificationsEnabled")]
    public bool NotificationsEnabled { get; set; } = true;

    [JsonPropertyName("graphicsQuality")]
    public GraphicsQuality GraphicsQuality { get; set; } = GraphicsQuality.High;

    [JsonPropertyName("cloudSaveEnabled")]
    public bool CloudSaveEnabled { get; set; } = true;

    [JsonPropertyName("lastCloudSaveTime")]
    public DateTime LastCloudSaveTime { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    /// <summary>
    /// DSGVO-Consent fuer Analytics-Events. Default false (Opt-In).
    /// Wird beim allerersten Start durch den Consent-Dialog gesetzt.
    /// </summary>
    [JsonPropertyName("analyticsEnabled")]
    public bool AnalyticsEnabled { get; set; }

    /// <summary>
    /// Ob der DSGVO-Consent-Dialog bereits einmal angezeigt wurde.
    /// Verhindert dass der Dialog bei jedem Start aufploppt.
    /// </summary>
    [JsonPropertyName("analyticsConsentShown")]
    public bool AnalyticsConsentShown { get; set; }
}
