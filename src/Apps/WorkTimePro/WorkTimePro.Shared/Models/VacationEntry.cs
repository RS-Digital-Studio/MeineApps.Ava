using Material.Icons;
using SQLite;
using WorkTimePro.Helpers;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Models;

/// <summary>
/// Ein Urlaubseintrag (Premium-Feature)
/// </summary>
[Table("VacationEntries")]
public class VacationEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Jahr für Urlaubskontingent
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Startdatum
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Enddatum (wird automatisch auf StartDate korrigiert falls kleiner)
    /// </summary>
    private DateTime _endDate;
    public DateTime EndDate
    {
        get => _endDate;
        set => _endDate = value < StartDate ? StartDate : value;
    }

    /// <summary>
    /// Anzahl Urlaubstage (Werktage)
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    /// Status des Urlaubs
    /// </summary>
    public DayStatus Type { get; set; } = DayStatus.Vacation;

    /// <summary>
    /// Notiz / Beschreibung
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Wurde genehmigt?
    /// </summary>
    public bool IsApproved { get; set; } = true;

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Formatierter Zeitraum
    /// </summary>
    [Ignore]
    public string DateRangeDisplay
    {
        get
        {
            if (StartDate.Date == EndDate.Date)
                return StartDate.ToString("dd.MM.yyyy");
            return $"{StartDate:dd.MM.} - {EndDate:dd.MM.yyyy}";
        }
    }

    /// <summary>
    /// Tage-Anzeige (z.B. "3 Tage")
    /// </summary>
    [Ignore]
    public string DaysDisplay => string.Format(AppStrings.DaysFormat, Days);

    /// <summary>
    /// Type-Icon
    /// </summary>
    [Ignore]
    public string TypeIcon => Type switch
    {
        DayStatus.Vacation => Icons.Beach,
        DayStatus.Sick => Icons.Thermometer,
        DayStatus.SpecialLeave => Icons.Gift,
        DayStatus.UnpaidLeave => Icons.PowerSleep,
        _ => Icons.CalendarMonth
    };

    /// <summary>
    /// MaterialIconKind for vacation type
    /// </summary>
    [Ignore]
    public MaterialIconKind TypeIconKind => Type switch
    {
        DayStatus.Vacation => MaterialIconKind.Beach,
        DayStatus.Sick => MaterialIconKind.Thermometer,
        DayStatus.SpecialLeave => MaterialIconKind.Gift,
        DayStatus.UnpaidLeave => MaterialIconKind.PowerSleep,
        _ => MaterialIconKind.CalendarMonth
    };

    /// <summary>
    /// Has a note
    /// </summary>
    [Ignore]
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}

/// <summary>
/// Jährliches Urlaubskontingent
/// </summary>
[Table("VacationQuotas")]
public class VacationQuota
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Jahr
    /// </summary>
    [Indexed]
    public int Year { get; set; }

    /// <summary>
    /// Urlaubstage gesamt
    /// </summary>
    public int TotalDays { get; set; } = 30;

    /// <summary>
    /// Resturlaub vom Vorjahr
    /// </summary>
    public int CarryOverDays { get; set; } = 0;

    /// <summary>
    /// Optionaler Arbeitgeber
    /// </summary>
    public int? EmployerId { get; set; }

    // === Berechnete Properties ===

    /// <summary>
    /// Verfügbare Tage gesamt
    /// </summary>
    [Ignore]
    public int AvailableDays => TotalDays + CarryOverDays;

    /// <summary>
    /// Genommene Tage (wird beim Laden berechnet)
    /// </summary>
    [Ignore]
    public int TakenDays { get; set; }

    /// <summary>
    /// Verbleibende Tage
    /// </summary>
    [Ignore]
    public int RemainingDays => AvailableDays - TakenDays;

    /// <summary>
    /// Geplante Tage (noch nicht angetreten)
    /// </summary>
    [Ignore]
    public int PlannedDays { get; set; }

    /// <summary>
    /// Prozent verbraucht
    /// </summary>
    [Ignore]
    public double UsedPercent
    {
        get
        {
            if (AvailableDays == 0) return 0;
            return (TakenDays * 100.0) / AvailableDays;
        }
    }
}
