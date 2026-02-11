using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein Projekt für Projekt-Tracking (Premium-Feature)
/// </summary>
[Table("Projects")]
public class Project
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Optionaler Arbeitgeber
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// Projektname
    /// </summary>
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kurzbeschreibung
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Farbe für Charts (Hex-Code)
    /// </summary>
    [MaxLength(9)]
    public string Color { get; set; } = "#1565C0";

    /// <summary>
    /// Ist das Projekt aktiv?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Budget in Stunden (optional, >= 0)
    /// </summary>
    private double? _budgetHours;
    public double? BudgetHours
    {
        get => _budgetHours;
        set => _budgetHours = value.HasValue ? Math.Max(0, value.Value) : null;
    }

    /// <summary>
    /// Stundensatz (optional, >= 0)
    /// </summary>
    private decimal? _hourlyRate;
    public decimal? HourlyRate
    {
        get => _hourlyRate;
        set => _hourlyRate = value.HasValue ? Math.Max(0, value.Value) : null;
    }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Archiviert am (wenn nicht mehr aktiv)
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    // === Berechnete Properties ===

    /// <summary>
    /// Geloggte Stunden (wird beim Laden berechnet)
    /// </summary>
    [Ignore]
    public double LoggedHours { get; set; }

    /// <summary>
    /// Verbleibende Budget-Stunden
    /// </summary>
    [Ignore]
    public double? RemainingHours => BudgetHours.HasValue ? BudgetHours.Value - LoggedHours : null;

    /// <summary>
    /// Budget-Fortschritt in Prozent
    /// </summary>
    [Ignore]
    public double? BudgetPercent
    {
        get
        {
            if (!BudgetHours.HasValue || BudgetHours.Value == 0) return null;
            return Math.Min(100, (LoggedHours * 100) / BudgetHours.Value);
        }
    }

    /// <summary>
    /// Berechneter Umsatz (Stunden x Stundensatz)
    /// </summary>
    [Ignore]
    public decimal? TotalRevenue => HourlyRate.HasValue ? HourlyRate.Value * (decimal)LoggedHours : null;

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
    /// Status-Badge
    /// </summary>
    [Ignore]
    public string StatusBadge => IsActive ? Helpers.Icons.CircleSlice8 : Helpers.Icons.CircleOutline;
}

/// <summary>
/// Zeiterfassung für ein Projekt
/// </summary>
[Table("ProjectTimeEntries")]
public class ProjectTimeEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Referenz zum Projekt
    /// </summary>
    [Indexed]
    public int ProjectId { get; set; }

    /// <summary>
    /// Referenz zum Arbeitstag (optional)
    /// </summary>
    public int? WorkDayId { get; set; }

    /// <summary>
    /// Datum der Zeiterfassung
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }

    /// <summary>
    /// Erfasste Minuten
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Beschreibung der Tätigkeit
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Stunden als Dezimalzahl
    /// </summary>
    [Ignore]
    public double Hours => Minutes / 60.0;

    /// <summary>
    /// Formatierte Dauer
    /// </summary>
    [Ignore]
    public string DurationDisplay
    {
        get
        {
            var hours = Minutes / 60;
            var mins = Minutes % 60;
            return $"{hours}:{mins:D2}";
        }
    }

    /// <summary>
    /// Formatiertes Datum
    /// </summary>
    [Ignore]
    public string DateDisplay => Date.ToString("dd.MM.yyyy");
}
