using Material.Icons;
using SQLite;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Models;

/// <summary>
/// Ein einzelner Zeiteintrag (Check-In oder Check-Out)
/// </summary>
[Table("TimeEntries")]
public class TimeEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Referenz zum Arbeitstag
    /// </summary>
    [Indexed]
    public int WorkDayId { get; set; }

    /// <summary>
    /// Optionale Referenz zum Arbeitgeber (für Nebenjobs)
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// Optionale Referenz zum Projekt
    /// </summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// Zeitpunkt des Eintrags
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Typ: Check-In oder Check-Out
    /// </summary>
    public EntryType Type { get; set; }

    /// <summary>
    /// Optionale Notiz (z.B. "Arzttermin")
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Wurde manuell korrigiert?
    /// </summary>
    public bool IsManuallyEdited { get; set; }

    /// <summary>
    /// Ursprünglicher Zeitpunkt (vor Korrektur)
    /// </summary>
    public DateTime? OriginalTimestamp { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Formatierte Uhrzeit für Anzeige
    /// </summary>
    [Ignore]
    public string TimeDisplay => Timestamp.ToString("HH:mm");

    /// <summary>
    /// Icon basierend auf Typ (nutze TypeIconKind für MaterialIcon in Views)
    /// </summary>
    [Ignore]
    public string TypeIcon => Type == EntryType.CheckIn ? "▶" : "■";

    /// <summary>
    /// Typ als Text (Arbeitsbeginn/Arbeitsende)
    /// </summary>
    [Ignore]
    public string TypeText => Type == EntryType.CheckIn ? AppStrings.CheckIn : AppStrings.CheckOut;

    /// <summary>
    /// Vollständige Anzeige (z.B. "Arbeitsbeginn: 15:00")
    /// </summary>
    [Ignore]
    public string FullDisplay => $"{TypeText}: {TimeDisplay}";

    /// <summary>
    /// MaterialIconKind for time entry type
    /// </summary>
    [Ignore]
    public MaterialIconKind TypeIconKind => Type == EntryType.CheckIn ? MaterialIconKind.Play : MaterialIconKind.Stop;

    /// <summary>
    /// Has a note
    /// </summary>
    [Ignore]
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
