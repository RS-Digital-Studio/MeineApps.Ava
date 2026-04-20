using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Globaler Cooldown-Tracker fuer Rewarded Ads (60s zwischen Ads).
/// Verhindert Ad-Spam und verbessert Nutzererfahrung.
/// Statisch, damit alle ViewModels denselben Cooldown teilen.
///
/// Hybrid-Ansatz fuer Robustheit gegen beide Exploit-Vektoren:
/// 1. Environment.TickCount64 (monoton, Process-lokal): Schutz gegen Clock-Skew rueckwaerts.
/// 2. DateTime.UtcNow in Preferences: Schutz gegen App-Restart-Bypass (User killt App → Process-Tick weg).
///
/// Ein Cooldown gilt als aktiv, wenn mindestens EINE der beiden Uhren noch im Fenster liegt.
/// </summary>
public static class RewardedAdCooldownTracker
{
    /// <summary>Cooldown-Dauer in Sekunden</summary>
    public const int CooldownSeconds = 60;

    private const string PREF_KEY_LAST_AD_UTC = "RewardedAd_LastShownUtc";

    // Statische Preferences-Referenz, wird von App.axaml.cs nach DI-Build gesetzt.
    public static IPreferencesService? Preferences { get; set; }

    // Monotone Uhr, Process-lokal. Schutz gegen Clock-Skew rueckwaerts.
    private static long _lastAdTicksMs = long.MinValue / 2;

    /// <summary>Letzter Ad-Zeitpunkt als UTC (persistiert in Preferences). Schutz gegen App-Restart.</summary>
    private static DateTime GetLastAdUtc()
    {
        if (Preferences == null) return DateTime.MinValue;
        var iso = Preferences.Get<string>(PREF_KEY_LAST_AD_UTC, "");
        if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
        try
        {
            return DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch { return DateTime.MinValue; }
    }

    /// <summary>Ob aktuell ein Cooldown aktiv ist (OR-Verknuepfung: beide Uhren muessen abgelaufen sein).</summary>
    public static bool IsOnCooldown
    {
        get
        {
            // Monotone Uhr: Process-Lifetime-Schutz gegen Clock-Skew rueckwaerts.
            bool monotonicCooldown = (Environment.TickCount64 - _lastAdTicksMs) < CooldownSeconds * 1000L;

            // Persistierte UTC: Cross-App-Restart-Schutz. Clamp gegen Clock-Skew rueckwaerts via Abs.
            var lastUtc = GetLastAdUtc();
            var elapsedUtc = (DateTime.UtcNow - lastUtc).TotalSeconds;
            // Negative Werte (User hat Uhr zurueckgestellt) gelten als "gerade geschehen" → Cooldown aktiv.
            bool utcCooldown = lastUtc != DateTime.MinValue && (elapsedUtc < 0 || elapsedUtc < CooldownSeconds);

            return monotonicCooldown || utcCooldown;
        }
    }

    /// <summary>Verbleibende Cooldown-Sekunden (Maximum beider Uhren).</summary>
    public static int RemainingSeconds
    {
        get
        {
            long monotonicElapsedMs = Environment.TickCount64 - _lastAdTicksMs;
            long monotonicRemainingMs = (CooldownSeconds * 1000L) - monotonicElapsedMs;
            int monotonicRemaining = monotonicRemainingMs <= 0 ? 0 : (int)(monotonicRemainingMs / 1000L);

            var lastUtc = GetLastAdUtc();
            int utcRemaining = 0;
            if (lastUtc != DateTime.MinValue)
            {
                var elapsed = (DateTime.UtcNow - lastUtc).TotalSeconds;
                if (elapsed < 0)
                    utcRemaining = CooldownSeconds; // Clock-Skew rueckwaerts → volle Dauer
                else if (elapsed < CooldownSeconds)
                    utcRemaining = (int)(CooldownSeconds - elapsed);
            }

            return Math.Max(monotonicRemaining, utcRemaining);
        }
    }

    /// <summary>Nach erfolgreicher Ad-Anzeige aufrufen um den Cooldown zu starten.</summary>
    public static void RecordAdShown()
    {
        _lastAdTicksMs = Environment.TickCount64;
        Preferences?.Set(PREF_KEY_LAST_AD_UTC, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>Prueft ob eine Ad angezeigt werden kann (kein Cooldown aktiv).</summary>
    public static bool CanShowAd => !IsOnCooldown;
}
