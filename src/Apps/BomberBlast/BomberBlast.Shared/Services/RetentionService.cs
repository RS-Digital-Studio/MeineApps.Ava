using System.Globalization;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Default-Implementation für <see cref="IRetentionService"/> (Phase 24 — O3-O5).
///
/// <para>Pure Date-Logic + Preferences-Persistenz. Kein Firebase, kein Backend.
/// FirstSession-Datum wird beim ersten <see cref="TouchSession"/>-Aufruf gesetzt und nie wieder
/// überschrieben — daraus berechnet sich D1/D7. LastSessionDate wird bei jedem Touch aktualisiert
/// (Comeback-Detection-Source).</para>
/// </summary>
public sealed class RetentionService : IRetentionService
{
    private const string KeyFirstWin = "Retention_FirstWin";
    private const string KeyFtueSkin = "Retention_FtueSkin";
    private const string KeyFirstSessionUtc = "Retention_FirstSessionUtc";
    private const string KeyLastSessionUtc = "Retention_LastSessionUtc";
    private const string KeyComebackLastClaimUtc = "Retention_ComebackLastClaim";
    // Audit H14: Monotoner Tick-Anchor fuer Anti-Cheat (Comeback-Bonus nicht durch Datum-Manipulation farmbar)
    private const string KeyComebackLastClaimTicks = "Retention_ComebackLastClaimTicks";
    // 3 Tage minus 4h Puffer (Zeitzone/DST) als Tick-Schwellwert
    private const long MinComebackUptimeTicks = (3L * 24 - 4) * 60 * 60 * 1000;

    private readonly IPreferencesService _prefs;

    public RetentionService(IPreferencesService prefs)
    {
        _prefs = prefs;
    }

    public bool HasFirstWin => _prefs.Get(KeyFirstWin, false);
    public bool HasFtueSkinClaimed => _prefs.Get(KeyFtueSkin, false);

    public int DaysSinceLastSession
    {
        get
        {
            var lastRaw = _prefs.Get(KeyLastSessionUtc, string.Empty);
            if (string.IsNullOrEmpty(lastRaw)) return 0;
            try
            {
                var last = DateTime.Parse(lastRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var diff = (DateTime.UtcNow.Date - last.Date).TotalDays;
                return (int)Math.Max(0, diff);
            }
            catch { return 0; }
        }
    }

    public bool IsD1WindowActive => DaysSinceFirstSession() == 1;
    public bool IsD7WindowActive => DaysSinceFirstSession() == 7;

    public bool IsComebackEligible
    {
        get
        {
            // Audit H14: Hybrid-Check. Mindestens 3 Tage inaktiv UND letzter Comeback-Claim ist >= 3 Tage her
            // (verhindert Multi-Comeback-Spam) UND monotone Tick-Differenz >= ~3 Tagen
            // (verhindert Datum-Manipulation: vorstellen → claim → zurueck → re-claim).
            if (DaysSinceLastSession < 3) return false;

            var lastClaimRaw = _prefs.Get(KeyComebackLastClaimUtc, string.Empty);
            if (string.IsNullOrEmpty(lastClaimRaw)) return true;

            try
            {
                var lastClaim = DateTime.Parse(lastClaimRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if ((DateTime.UtcNow - lastClaim).TotalDays < 3) return false;
            }
            catch { return true; }

            // Monotone Tick-Check
            var lastClaimTicks = _prefs.Get(KeyComebackLastClaimTicks, 0L);
            long now = Environment.TickCount64;
            // Bei Reboot/Counter-Wrap (now < lastClaimTicks) erlauben — das Datum hat die Last-Claim-Pruefung bereits bestanden.
            if (lastClaimTicks > now) return true;
            return (now - lastClaimTicks) >= MinComebackUptimeTicks;
        }
    }

    public bool RegisterFirstWin()
    {
        if (HasFirstWin) return false;
        _prefs.Set(KeyFirstWin, true);
        return true;
    }

    public void MarkFtueSkinClaimed() => _prefs.Set(KeyFtueSkin, true);

    public void MarkComebackClaimed()
    {
        _prefs.Set(KeyComebackLastClaimUtc, DateTime.UtcNow.ToString("O"));
        // Audit H14: Tick-Anchor speichern fuer Anti-Cheat-Hybrid.
        _prefs.Set(KeyComebackLastClaimTicks, Environment.TickCount64);
    }

    public void TouchSession()
    {
        // First-Session-Datum NIEMALS überschreiben — D1/D7 würden brechen.
        var firstRaw = _prefs.Get(KeyFirstSessionUtc, string.Empty);
        if (string.IsNullOrEmpty(firstRaw))
        {
            _prefs.Set(KeyFirstSessionUtc, DateTime.UtcNow.ToString("O"));
        }
        // Last-Session-Datum bei jedem Touch aktualisieren.
        _prefs.Set(KeyLastSessionUtc, DateTime.UtcNow.ToString("O"));
    }

    private int DaysSinceFirstSession()
    {
        var firstRaw = _prefs.Get(KeyFirstSessionUtc, string.Empty);
        if (string.IsNullOrEmpty(firstRaw)) return 0;
        try
        {
            var first = DateTime.Parse(firstRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var diff = (DateTime.UtcNow.Date - first.Date).TotalDays;
            return (int)Math.Max(0, diff);
        }
        catch { return 0; }
    }
}
