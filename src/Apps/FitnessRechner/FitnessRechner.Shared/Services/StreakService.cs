using System.Globalization;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Verwaltet Logging-Streaks (aufeinanderfolgende Tage mit Aktivität).
/// Preferences-basiert, kein extra File nötig.
/// </summary>
public class StreakService
{
    private readonly IPreferencesService _preferences;

    // Meilensteine für Confetti
    private static readonly int[] Milestones = [3, 7, 14, 21, 30, 50, 75, 100, 150, 200, 365];

    public StreakService(IPreferencesService preferences)
    {
        _preferences = preferences;
    }

    /// <summary>
    /// Aktuelle Streak-Länge (Tage).
    /// </summary>
    public int CurrentStreak => _preferences.Get(PreferenceKeys.StreakCurrent, 0);

    /// <summary>
    /// Beste Streak aller Zeiten.
    /// </summary>
    public int BestStreak => _preferences.Get(PreferenceKeys.StreakBest, 0);

    /// <summary>
    /// Wurde heute schon geloggt?
    /// </summary>
    public bool IsLoggedToday
    {
        get
        {
            var lastDateStr = _preferences.Get(PreferenceKeys.StreakLastLogDate, "");
            if (string.IsNullOrEmpty(lastDateStr)) return false;
            return lastDateStr == DateTime.Today.ToString("yyyy-MM-dd");
        }
    }

    /// <summary>
    /// Registriert eine Logging-Aktivität für heute.
    /// Gibt true zurück wenn ein Meilenstein erreicht wurde (→ Confetti).
    /// </summary>
    public bool RecordActivity()
    {
        var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
        var lastDateStr = _preferences.Get(PreferenceKeys.StreakLastLogDate, "");

        // Heute schon geloggt → kein Update nötig
        if (lastDateStr == todayStr)
            return false;

        var currentStreak = _preferences.Get(PreferenceKeys.StreakCurrent, 0);
        var bestStreak = _preferences.Get(PreferenceKeys.StreakBest, 0);

        if (!string.IsNullOrEmpty(lastDateStr))
        {
            if (!DateTime.TryParseExact(lastDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var lastDate))
            {
                // Korrupte Preference → Streak neu starten
                currentStreak = 1;
                bestStreak = Math.Max(bestStreak, currentStreak);
                _preferences.Set(PreferenceKeys.StreakCurrent, currentStreak);
                _preferences.Set(PreferenceKeys.StreakBest, bestStreak);
                _preferences.Set(PreferenceKeys.StreakLastLogDate, todayStr);
                return Array.IndexOf(Milestones, currentStreak) >= 0;
            }

            var daysDiff = (DateTime.Today - lastDate).Days;

            if (daysDiff == 1)
            {
                // Gestern geloggt → Streak fortsetzen
                currentStreak++;
            }
            else if (daysDiff > 1)
            {
                // Streak unterbrochen → Neustart
                currentStreak = 1;
            }
            // daysDiff == 0 kann hier nicht passieren (oben abgefangen)
        }
        else
        {
            // Erste Aktivität überhaupt
            currentStreak = 1;
        }

        // Best-Streak aktualisieren
        if (currentStreak > bestStreak)
            bestStreak = currentStreak;

        // Speichern
        _preferences.Set(PreferenceKeys.StreakCurrent, currentStreak);
        _preferences.Set(PreferenceKeys.StreakBest, bestStreak);
        _preferences.Set(PreferenceKeys.StreakLastLogDate, todayStr);

        // Meilenstein-Check
        return Array.IndexOf(Milestones, currentStreak) >= 0;
    }

    /// <summary>
    /// Gibt alle aktiven Tage zurück (Tage an denen geloggt wurde).
    /// Wird für die Heatmap-Kalender-Anzeige benötigt.
    /// </summary>
    public HashSet<DateTime> GetLoggedDates(IReadOnlyList<DateTime> trackingDates, IReadOnlyList<DateTime> foodLogDates)
    {
        var result = new HashSet<DateTime>();
        foreach (var d in trackingDates)
            result.Add(d.Date);
        foreach (var d in foodLogDates)
            result.Add(d.Date);
        return result;
    }

    /// <summary>
    /// Nächster Meilenstein ab aktuellem Streak.
    /// </summary>
    public int NextMilestone
    {
        get
        {
            var current = CurrentStreak;
            foreach (var m in Milestones)
            {
                if (m > current) return m;
            }
            return current + 50; // Nach 365: nächste 50er-Schritte
        }
    }
}
