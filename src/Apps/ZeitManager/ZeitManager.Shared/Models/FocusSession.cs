using SQLite;

namespace ZeitManager.Models;

/// <summary>
/// Abgeschlossene Pomodoro-Arbeitssitzung für Statistiken.
/// </summary>
[Table("FocusSessions")]
public class FocusSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>Datum der Sitzung (nur Datum, ohne Uhrzeit).</summary>
    public string Date { get; set; } = DateTime.Today.ToString("O");

    /// <summary>Dauer in Minuten.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Typ der Sitzung (Work, ShortBreak, LongBreak).</summary>
    public string Type { get; set; } = "Work";

    /// <summary>Optionaler Name der Sitzung.</summary>
    public string? Name { get; set; }

    /// <summary>Zeitpunkt des Abschlusses.</summary>
    public string CompletedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
