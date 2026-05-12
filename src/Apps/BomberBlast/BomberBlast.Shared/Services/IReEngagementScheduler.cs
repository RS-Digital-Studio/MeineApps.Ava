namespace BomberBlast.Services;

/// <summary>
/// Re-Engagement-Notification-Scheduler (Sprint 2.3 AAA-Audit #3).
///
/// <para>
/// Plant lokale Notifications fuer inaktive Spieler. Wird beim App-Background gesetzt,
/// beim Foreground wieder gecancelt — keine Notification kommt fuer aktive Spieler.
/// </para>
///
/// <para>
/// Trigger:
/// <list type="bullet">
/// <item><b>D1</b> (24h): "Deine taegliche Belohnung wartet" — nur wenn DailyReward nicht abgeholt</item>
/// <item><b>D3</b> (72h): "Dein Battle-Pass laeuft in {days} Tagen ab" — nur wenn BP-Tier &lt; Max</item>
/// <item><b>D7</b> (168h): "Wir haben dich vermisst! Hier sind 100 Gems" — one-shot pro Saison</item>
/// </list>
/// </para>
///
/// <para>
/// DSGVO: Respektiert Notification-Permission (POST_NOTIFICATIONS auf Android 13+).
/// Wird nichts geplant wenn der User Notifications abgelehnt hat.
/// </para>
/// </summary>
public interface IReEngagementScheduler
{
    /// <summary>
    /// Plant alle drei D1/D3/D7-Notifications. Idempotent — re-scheduled bestehende
    /// Trigger (alter Zeitstempel wird ueberschrieben). Aufruf bei App-Background.
    /// </summary>
    void ScheduleAll();

    /// <summary>
    /// Cancelt alle drei D1/D3/D7-Notifications. Aufruf bei App-Foreground —
    /// User ist aktiv, wir wollen keine Reminder schicken die nicht relevant sind.
    /// </summary>
    void CancelAll();
}

/// <summary>Notification-IDs (statisch — eindeutig fuer Cancel-Operationen).</summary>
public static class ReEngagementNotificationIds
{
    public const string D1 = "re_engagement_d1";
    public const string D3 = "re_engagement_d3";
    public const string D7 = "re_engagement_d7";
}
