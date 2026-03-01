namespace FitnessRechner.Services;

/// <summary>
/// Interface fuer den Logging-Streak-Service.
/// Verwaltet aufeinanderfolgende Tage mit Aktivitaet.
/// </summary>
public interface IStreakService
{
    /// <summary>
    /// Aktuelle Streak-Laenge (Tage).
    /// </summary>
    int CurrentStreak { get; }

    /// <summary>
    /// Beste Streak aller Zeiten.
    /// </summary>
    int BestStreak { get; }

    /// <summary>
    /// Wurde heute schon geloggt?
    /// </summary>
    bool IsLoggedToday { get; }

    /// <summary>
    /// Naechster Meilenstein ab aktuellem Streak.
    /// </summary>
    int NextMilestone { get; }

    /// <summary>
    /// Registriert eine Logging-Aktivitaet fuer heute.
    /// Gibt true zurueck wenn ein Meilenstein erreicht wurde (Confetti).
    /// </summary>
    bool RecordActivity();

    /// <summary>
    /// Gibt alle aktiven Tage zurueck (Tage an denen geloggt wurde).
    /// Wird fuer die Heatmap-Kalender-Anzeige benoetigt.
    /// </summary>
    HashSet<DateTime> GetLoggedDates(IReadOnlyList<DateTime> trackingDates, IReadOnlyList<DateTime> foodLogDates);
}
