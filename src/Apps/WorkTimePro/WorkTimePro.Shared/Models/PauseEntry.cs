using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein Pauseneintrag mit Unterscheidung zwischen manuell und automatisch
/// </summary>
[Table("PauseEntries")]
public class PauseEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Referenz zum Arbeitstag
    /// </summary>
    [Indexed]
    public int WorkDayId { get; set; }

    /// <summary>
    /// Startzeitpunkt der Pause
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Endzeitpunkt der Pause (null wenn noch in Pause)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Typ der Pause (Manuell oder Auto)
    /// </summary>
    public PauseType Type { get; set; }

    /// <summary>
    /// Wurde automatisch vom System ergänzt
    /// </summary>
    public bool IsAutoPause { get; set; }

    /// <summary>
    /// Optionale Notiz (z.B. "Mittagessen")
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dauer der Pause
    /// </summary>
    [Ignore]
    public TimeSpan Duration
    {
        get
        {
            if (EndTime == null)
                return TimeSpan.Zero;
            var duration = EndTime.Value - StartTime;
            // Negative Dauer bei Mitternachts-Übergang abfangen
            if (duration < TimeSpan.Zero)
                duration += TimeSpan.FromHours(24);
            // Unplausible Pausendauer begrenzen (max 12h)
            if (duration > TimeSpan.FromHours(12))
            {
                System.Diagnostics.Debug.WriteLine($"PauseEntry.Duration unplausibel ({duration}) - auf 0 gesetzt");
                return TimeSpan.Zero;
            }
            return duration;
        }
    }

    /// <summary>
    /// Formatierte Dauer für Anzeige
    /// </summary>
    [Ignore]
    public string DurationDisplay
    {
        get
        {
            var duration = Duration;
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}:{duration.Minutes:D2}";
            return $"{duration.Minutes} min";
        }
    }

    /// <summary>
    /// Formatierte Startzeit für Anzeige
    /// </summary>
    [Ignore]
    public string StartTimeDisplay => StartTime.ToString("HH:mm");

    /// <summary>
    /// Formatierte Endzeit für Anzeige
    /// </summary>
    [Ignore]
    public string EndTimeDisplay => EndTime?.ToString("HH:mm") ?? "--:--";

    /// <summary>
    /// Icon for auto-pause (Lightning) or empty
    /// </summary>
    [Ignore]
    public string AutoPauseIcon => IsAutoPause ? Helpers.Icons.Lightning : "";

    /// <summary>
    /// Läuft die Pause noch?
    /// </summary>
    [Ignore]
    public bool IsActive => EndTime == null;
}
