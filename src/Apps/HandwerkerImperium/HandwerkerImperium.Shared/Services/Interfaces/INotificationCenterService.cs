using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Sammelt nicht-kritische Notifications (Daily Reward, Welcome Back, Achievement,
/// Streak Saved, Story Chapter) in einer Bell-UI im Header. v2.0.36 ersetzt das
/// Dialog-Stacking beim Re-Open: nur OfflineEarnings bleibt direktes Modal, alles
/// andere sammelt sich.
/// </summary>
public interface INotificationCenterService
{
    /// <summary>
    /// Aktuelle Items, sortiert nach CreatedAt absteigend.
    /// </summary>
    IReadOnlyList<NotificationItem> Items { get; }

    /// <summary>
    /// Anzahl ungesehener Items (Seen == false).
    /// </summary>
    int UnseenCount { get; }

    /// <summary>
    /// Wird ausgeloest, wenn sich Items oder UnseenCount aendern.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Fuegt ein Item hinzu. Bei gleicher Id wird das vorhandene Item aktualisiert
    /// (keine Duplikate). Beibehalt der Seen-Flag, damit ein bereits gesehenes Item
    /// nicht plötzlich wieder ungesehen wird.
    /// </summary>
    void Add(NotificationItem item);

    /// <summary>
    /// Entfernt ein Item per Id.
    /// </summary>
    void Dismiss(string id);

    /// <summary>
    /// Entfernt alle Items.
    /// </summary>
    void Clear();

    /// <summary>
    /// Markiert alle Items als gesehen (Seen = true). UnseenCount geht auf 0.
    /// </summary>
    void MarkAllSeen();

    /// <summary>
    /// Prueft, ob ein Item mit der Id existiert.
    /// </summary>
    bool Contains(string id);
}
