using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Art der Benachrichtigung. Steuert Icon, Sortier-Prioritaet und Klick-Aktion.
/// </summary>
public enum NotificationKind
{
    /// <summary>Offline-Einkommen verfuegbar — wird IMMER als Modal angezeigt, nicht in der Bell.</summary>
    OfflineEarnings,
    /// <summary>Tagesbelohnung wartet — sammelbar in der Bell.</summary>
    DailyReward,
    /// <summary>Willkommen-Zurueck-Angebot (Premium-Bundle) — sammelbar in der Bell.</summary>
    WelcomeBackOffer,
    /// <summary>Erfolg freigeschaltet — sammelbar in der Bell.</summary>
    AchievementUnlocked,
    /// <summary>Login-Streak erfolgreich gerettet — sammelbar in der Bell.</summary>
    StreakSaved,
    /// <summary>Neues Story-Kapitel verfuegbar — sammelbar in der Bell, mit Pulse-Akzent.</summary>
    NewStoryChapter,
    /// <summary>Live-/Premium-Auftrag verfuegbar (durch AutoAcceptOnlyStandard nicht angenommen).</summary>
    LiveOrderAvailable
}

/// <summary>
/// Ein einzelnes Notification-Item im Notification-Center (Bell).
/// IDs sind dedupliziert: gleiche Ids fuegen nichts hinzu, sondern aktualisieren das vorhandene Item.
/// </summary>
public sealed class NotificationItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kind")]
    public NotificationKind Kind { get; set; }

    /// <summary>
    /// Resource-Key fuer den Titel. Wird vom ViewModel zur Laufzeit lokalisiert.
    /// </summary>
    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = "";

    /// <summary>
    /// Optionale Format-Argumente fuer den Titel (z.B. Achievement-Name).
    /// </summary>
    [JsonPropertyName("titleArg")]
    public string? TitleArg { get; set; }

    /// <summary>
    /// Resource-Key fuer den Body-Text.
    /// </summary>
    [JsonPropertyName("bodyKey")]
    public string BodyKey { get; set; } = "";

    /// <summary>
    /// Optionale Format-Argumente fuer den Body.
    /// </summary>
    [JsonPropertyName("bodyArg")]
    public string? BodyArg { get; set; }

    /// <summary>
    /// Erstellungs-Zeitpunkt (UTC, ISO 8601 "O").
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Wurde das Item bereits in der Bell angezeigt (gesehen)?
    /// </summary>
    [JsonPropertyName("seen")]
    public bool Seen { get; set; }

    /// <summary>
    /// Optionaler GameIcon-Name fuer das Item. Wird vom ViewModel auf das passende Bitmap gemappt.
    /// </summary>
    [JsonPropertyName("iconKind")]
    public string? IconKind { get; set; }
}
