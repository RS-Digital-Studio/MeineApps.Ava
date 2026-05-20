using BomberBlast.Models.BattlePass;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation von <see cref="IReEngagementScheduler"/> (.3 .
/// Plant D1/D3/D7-Notifications via <see cref="IPushNotificationService"/>.
/// Texte sind lokalisiert ueber <see cref="ILocalizationService"/>.
/// </summary>
public sealed class ReEngagementScheduler : IReEngagementScheduler
{
    private readonly IPushNotificationService _push;
    private readonly ILocalizationService _localization;
    private readonly IDailyRewardService _dailyReward;
    private readonly IBattlePassService _battlePass;
    private readonly IPreferencesService _prefs;
    private readonly ILogger<ReEngagementScheduler> _logger;

    private const string KeyLastD7Schedule = "ReEngagement_LastD7ScheduleUtc";
    /// <summary>D7-Reminder soll nur 1x pro 7 Tage gefeuert werden (kein Spam).</summary>
    private static readonly TimeSpan D7Cooldown = TimeSpan.FromDays(7);

    /// <summary>
    /// v2.0.60 (B-D11): Smart-Timing. Statt "+24h ab jetzt" wird der nächste lokale
    /// Mittag (12:00 local) verwendet — höhere Engagement-Chance (Lunch-Break)
    /// statt zufälliger Tageszeit basierend auf Login-Stunde.
    /// </summary>
    private static DateTime NextLocalNoon(int daysAhead)
    {
        var todayLocal = DateTime.Now.Date;
        var noonLocal = todayLocal.AddDays(daysAhead).AddHours(12);
        // Falls schon nach 12:00 → erst nächster Tag (für daysAhead=1).
        if (daysAhead == 1 && DateTime.Now >= todayLocal.AddHours(12))
        {
            noonLocal = noonLocal.AddDays(0); // Bleibt bei +1 Tag
        }
        // Konvertiere zu UTC für PushService.
        return noonLocal.ToUniversalTime();
    }

    public ReEngagementScheduler(
        IPushNotificationService push,
        ILocalizationService localization,
        IDailyRewardService dailyReward,
        IBattlePassService battlePass,
        IPreferencesService prefs,
        ILogger<ReEngagementScheduler> logger)
    {
        _push = push;
        _localization = localization;
        _dailyReward = dailyReward;
        _battlePass = battlePass;
        _prefs = prefs;
        _logger = logger;
    }

    public void ScheduleAll()
    {
        if (!_push.ArePermissionsGranted)
        {
            _logger.LogInformation("ReEngagement: Notification-Permission verweigert, plane nichts.");
            return;
        }

        try
        {
            ScheduleD1();
            ScheduleD3();
            ScheduleD7();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReEngagement: Fehler beim Planen der Reminder");
        }
    }

    public void CancelAll()
    {
        try
        {
            _push.CancelLocalNotification(ReEngagementNotificationIds.D1);
            _push.CancelLocalNotification(ReEngagementNotificationIds.D3);
            _push.CancelLocalNotification(ReEngagementNotificationIds.D7);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReEngagement: Fehler beim Cancelen der Reminder");
        }
    }

    /// <summary>
    /// D1-Trigger: 24h ab jetzt. Nur wenn Daily-Reward nicht abgeholt — sonst kein Sinn.
    /// </summary>
    private void ScheduleD1()
    {
        if (!_dailyReward.IsRewardAvailable)
        {
            // Reward bereits eingesammelt — D1-Trigger waere wertlos
            _push.CancelLocalNotification(ReEngagementNotificationIds.D1);
            return;
        }

        // v2.0.60 (B-D11): Smart-Timing — Trigger auf lokalen Mittag des nächsten Tages
        // (statt +24h ab Login). Verpasst nicht den Lunch-Break wenn User 23:59 logged.
        var trigger = NextLocalNoon(daysAhead: 1);
        var title = _localization.GetString("ReEngagementD1Title")
            ?? "Your daily reward is waiting!";
        var body = _localization.GetString("ReEngagementD1Body")
            ?? "Tap to claim your bonus before it resets.";

        _push.ScheduleLocalNotification(
            ReEngagementNotificationIds.D1,
            trigger,
            title,
            body,
            NotificationChannel.DailyRewards);
    }

    /// <summary>
    /// D3-Trigger: 72h ab jetzt. Nur wenn der User noch Battle-Pass-Tier offen hat.
    /// </summary>
    private void ScheduleD3()
    {
        if (_battlePass.CurrentTier >= BattlePassTierDefinitions.MaxTier)
        {
            _push.CancelLocalNotification(ReEngagementNotificationIds.D3);
            return;
        }

        // v2.0.60 (B-D11): D3 auf lokalen Mittag in 3 Tagen.
        var trigger = NextLocalNoon(daysAhead: 3);
        var daysToBpEnd = Math.Max(1, _battlePass.DaysRemaining);
        var titleFormat = _localization.GetString("ReEngagementD3Title")
            ?? "Your Battle Pass ends in {0} days";
        var body = _localization.GetString("ReEngagementD3Body")
            ?? "You still have unclaimed Battle Pass rewards. Don't lose them!";

        _push.ScheduleLocalNotification(
            ReEngagementNotificationIds.D3,
            trigger,
            string.Format(titleFormat, daysToBpEnd),
            body,
            NotificationChannel.LiveOps);
    }

    /// <summary>
    /// D7-Trigger: 168h ab jetzt. One-shot pro 7-Tage-Cooldown — kein Spam fuer Spieler
    /// die gelegentlich zurueckkommen.
    /// </summary>
    private void ScheduleD7()
    {
        // Cooldown-Check: Letzter D7-Trigger weniger als 7 Tage her?
        var lastRaw = _prefs.Get(KeyLastD7Schedule, string.Empty);
        if (!string.IsNullOrEmpty(lastRaw))
        {
            try
            {
                var last = DateTime.Parse(lastRaw,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                if (DateTime.UtcNow - last < D7Cooldown)
                {
                    return;  // Noch im Cooldown
                }
            }
            catch { /* Parse-Fehler → planen */ }
        }

        // v2.0.60 (B-D11): D7 auf lokalen Mittag in 7 Tagen.
        var trigger = NextLocalNoon(daysAhead: 7);  // 7 Tage
        var title = _localization.GetString("ReEngagementD7Title")
            ?? "We miss you!";
        var body = _localization.GetString("ReEngagementD7Body")
            ?? "Come back and claim 100 free Gems as a welcome-back gift.";

        _push.ScheduleLocalNotification(
            ReEngagementNotificationIds.D7,
            trigger,
            title,
            body,
            NotificationChannel.Important);

        _prefs.Set(KeyLastD7Schedule, DateTime.UtcNow.ToString("O"));
    }
}
