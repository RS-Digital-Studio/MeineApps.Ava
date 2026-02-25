namespace BomberBlast.Services;

/// <summary>
/// Globaler Cooldown-Tracker für Rewarded Ads (60s zwischen Ads).
/// Verhindert Ad-Spam und verbessert Nutzererfahrung.
/// Statisch, damit alle ViewModels denselben Cooldown teilen.
/// </summary>
public static class RewardedAdCooldownTracker
{
    /// <summary>Cooldown-Dauer in Sekunden</summary>
    public const int CooldownSeconds = 60;

    /// <summary>Zeitpunkt der letzten erfolgreichen Ad-Anzeige (UTC)</summary>
    private static DateTime _lastAdUtc = DateTime.MinValue;

    /// <summary>Ob aktuell ein Cooldown aktiv ist</summary>
    public static bool IsOnCooldown =>
        (DateTime.UtcNow - _lastAdUtc).TotalSeconds < CooldownSeconds;

    /// <summary>Verbleibende Cooldown-Sekunden (0 wenn kein Cooldown)</summary>
    public static int RemainingSeconds =>
        Math.Max(0, CooldownSeconds - (int)(DateTime.UtcNow - _lastAdUtc).TotalSeconds);

    /// <summary>
    /// Nach erfolgreicher Ad-Anzeige aufrufen um den Cooldown zu starten.
    /// </summary>
    public static void RecordAdShown()
    {
        _lastAdUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Prüft ob eine Ad angezeigt werden kann (kein Cooldown aktiv).
    /// </summary>
    public static bool CanShowAd => !IsOnCooldown;
}
