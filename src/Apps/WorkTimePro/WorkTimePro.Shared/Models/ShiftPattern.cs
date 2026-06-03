using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein Schichtmuster (Premium-Feature)
/// </summary>
[Table("ShiftPatterns")]
public class ShiftPattern
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Name des Schichtmusters (z.B. "Frühschicht")
    /// </summary>
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Typ der Schicht
    /// </summary>
    public ShiftType Type { get; set; }

    /// <summary>
    /// Startzeit (Ticks)
    /// </summary>
    public long StartTimeTicks { get; set; }

    /// <summary>
    /// Endzeit (Ticks)
    /// </summary>
    public long EndTimeTicks { get; set; }

    /// <summary>
    /// Pausendauer in Minuten
    /// </summary>
    public int BreakMinutes { get; set; } = 30;

    /// <summary>
    /// Farbe (Hex)
    /// </summary>
    [MaxLength(9)]
    public string Color { get; set; } = "#1565C0";

    /// <summary>
    /// Optionaler Arbeitgeber
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// Ist aktiv?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Startzeit als TimeOnly
    /// </summary>
    [Ignore]
    public TimeOnly StartTime
    {
        get => new(StartTimeTicks);
        set => StartTimeTicks = value.Ticks;
    }

    /// <summary>
    /// Endzeit als TimeOnly
    /// </summary>
    [Ignore]
    public TimeOnly EndTime
    {
        get => new(EndTimeTicks);
        set => EndTimeTicks = value.Ticks;
    }

    /// <summary>
    /// Arbeitszeit (ohne Pause)
    /// </summary>
    [Ignore]
    public TimeSpan WorkDuration
    {
        get
        {
            var duration = EndTime.ToTimeSpan() - StartTime.ToTimeSpan();
            if (duration < TimeSpan.Zero)
                duration += TimeSpan.FromHours(24); // Nachtschicht
            return duration - TimeSpan.FromMinutes(BreakMinutes);
        }
    }

    /// <summary>
    /// Formatierte Zeitspanne (z.B. "06:00 - 14:00")
    /// </summary>
    [Ignore]
    public string TimeRangeDisplay => $"{StartTime:HH:mm} - {EndTime:HH:mm}";

    /// <summary>
    /// Formatierte Dauer
    /// </summary>
    [Ignore]
    public string DurationDisplay => $"{(int)WorkDuration.TotalHours}:{WorkDuration.Minutes:D2}";

    /// <summary>
    /// Standard-Schichtmuster erstellen
    /// </summary>
    public static List<ShiftPattern> GetDefaultPatterns()
    {
        return new List<ShiftPattern>
        {
            new()
            {
                Name = "Frühschicht",
                Type = ShiftType.Early,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(14, 0),
                BreakMinutes = 30,
                Color = "#FF9800"
            },
            new()
            {
                Name = "Spätschicht",
                Type = ShiftType.Late,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(22, 0),
                BreakMinutes = 30,
                Color = "#2196F3"
            },
            new()
            {
                Name = "Nachtschicht",
                Type = ShiftType.Night,
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
                BreakMinutes = 45,
                Color = "#673AB7"
            },
            new()
            {
                Name = "Normalschicht",
                Type = ShiftType.Normal,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 30),
                BreakMinutes = 30,
                Color = "#4CAF50"
            }
        };
    }
}

/// <summary>
/// Zuordnung einer Schicht zu einem Tag
/// </summary>
[Table("ShiftAssignments")]
public class ShiftAssignment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Datum
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }

    /// <summary>
    /// Referenz zum Schichtmuster
    /// </summary>
    public int ShiftPatternId { get; set; }

    /// <summary>
    /// Optionaler Arbeitgeber
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// Notiz
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Navigation ===

    [Ignore]
    public ShiftPattern? ShiftPattern { get; set; }
}
