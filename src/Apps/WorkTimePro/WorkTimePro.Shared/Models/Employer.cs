using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein Arbeitgeber (für Nebenjobs/Freelancer)
/// </summary>
[Table("Employers")]
public class Employer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Name des Arbeitgebers
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Wöchentliche Soll-Stunden für diesen Arbeitgeber
    /// </summary>
    public double WeeklyHours { get; set; } = 40.0;

    /// <summary>
    /// Farbe für Charts (Hex-Code)
    /// </summary>
    [MaxLength(9)]
    public string Color { get; set; } = "#1565C0";

    /// <summary>
    /// Ist dies der Standard-Arbeitgeber?
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Ist aktiv?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Notizen
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Initialen für Avatar
    /// </summary>
    [Ignore]
    public string Initials
    {
        get
        {
            if (string.IsNullOrEmpty(Name)) return "?";
            var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();
            return Name.Length >= 2 ? Name[..2].ToUpper() : Name.ToUpper();
        }
    }

    /// <summary>
    /// Wöchentliche Stunden als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan WeeklyWorkTime => TimeSpan.FromHours(WeeklyHours);

    /// <summary>
    /// Tägliche Stunden (5-Tage-Woche)
    /// </summary>
    [Ignore]
    public double DailyHours => WeeklyHours / 5.0;
}
