using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein gesetzlicher Feiertag
/// </summary>
[Table("Holidays")]
public class HolidayEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Datum des Feiertags
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }

    /// <summary>
    /// Name des Feiertags (lokalisiert)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Region/Bundesland (z.B. "DE-BY")
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Ist bundesweit?
    /// </summary>
    public bool IsNational { get; set; }

    /// <summary>
    /// Jahr
    /// </summary>
    [Indexed]
    public int Year { get; set; }

    // === Berechnete Properties ===

    /// <summary>
    /// Formatiertes Datum
    /// </summary>
    [Ignore]
    public string DateDisplay => Date.ToString("d");

    /// <summary>
    /// Wochentag
    /// </summary>
    [Ignore]
    public string WeekdayDisplay => Date.ToString("dddd");

    /// <summary>
    /// Icon
    /// </summary>
    [Ignore]
    public string Icon => Helpers.Icons.PartyPopper;
}

