using SQLite;
using WorkTimePro.Helpers;
using static WorkTimePro.Helpers.TimeFormatter;

namespace WorkTimePro.Models;

/// <summary>
/// Zusammenfassung eines Arbeitstages
/// </summary>
[Table("WorkDays")]
public class WorkDay
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Das Datum des Arbeitstages
    /// </summary>
    [Unique]
    public DateTime Date { get; set; }

    /// <summary>
    /// Status des Tages (Arbeitstag, Urlaub, Feiertag, etc.)
    /// </summary>
    public DayStatus Status { get; set; } = DayStatus.WorkDay;

    /// <summary>
    /// Soll-Arbeitszeit in Minuten
    /// </summary>
    public int TargetWorkMinutes { get; set; } = 480; // 8 Stunden

    /// <summary>
    /// Tatsächliche Arbeitszeit in Minuten (ohne Pausen)
    /// </summary>
    public int ActualWorkMinutes { get; set; }

    /// <summary>
    /// Manuelle Pausenzeit in Minuten (echte Pausen)
    /// </summary>
    public int ManualPauseMinutes { get; set; }

    /// <summary>
    /// Auto-Pause Ergänzung in Minuten (gesetzlich)
    /// </summary>
    public int AutoPauseMinutes { get; set; }

    /// <summary>
    /// Plus/Minus Saldo in Minuten
    /// </summary>
    public int BalanceMinutes { get; set; }

    /// <summary>
    /// Erster Check-In des Tages
    /// </summary>
    public DateTime? FirstCheckIn { get; set; }

    /// <summary>
    /// Letzter Check-Out des Tages
    /// </summary>
    public DateTime? LastCheckOut { get; set; }

    /// <summary>
    /// Optionaler Arbeitgeber (für mehrere Arbeitgeber)
    /// </summary>
    public int? EmployerId { get; set; }

    /// <summary>
    /// Ist der Tag durch Monatsabschluss gesperrt?
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Notiz zum Tag
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Erstellt am
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Zuletzt geändert
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // === Berechnete Properties ===

    /// <summary>
    /// Soll-Arbeitszeit als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan TargetWorkTime => TimeSpan.FromMinutes(TargetWorkMinutes);

    /// <summary>
    /// Tatsächliche Arbeitszeit als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan ActualWorkTime => TimeSpan.FromMinutes(ActualWorkMinutes);

    /// <summary>
    /// Manuelle Pausenzeit als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan ManualPauseTime => TimeSpan.FromMinutes(ManualPauseMinutes);

    /// <summary>
    /// Auto-Pause Zeit als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan AutoPauseTime => TimeSpan.FromMinutes(AutoPauseMinutes);

    /// <summary>
    /// Gesamte Pausenzeit (manuell + auto)
    /// </summary>
    [Ignore]
    public TimeSpan TotalPauseTime => TimeSpan.FromMinutes(ManualPauseMinutes + AutoPauseMinutes);

    /// <summary>
    /// Saldo als TimeSpan
    /// </summary>
    [Ignore]
    public TimeSpan Balance => TimeSpan.FromMinutes(BalanceMinutes);

    /// <summary>
    /// Datum nur (ohne Uhrzeit)
    /// </summary>
    [Ignore]
    public DateOnly DateOnly => DateOnly.FromDateTime(Date);

    /// <summary>
    /// Short day name (e.g. "Mo", "Di")
    /// </summary>
    [Ignore]
    public string DayName => Date.ToString("ddd");

    /// <summary>
    /// Short date display (e.g. "05.02.")
    /// </summary>
    [Ignore]
    public string DateShortDisplay => Date.ToString("dd.MM.");

    /// <summary>
    /// Formatierte Soll-Zeit für Anzeige
    /// </summary>
    [Ignore]
    public string TargetWorkDisplay => FormatMinutes(TargetWorkMinutes);

    /// <summary>
    /// Formatierte Ist-Zeit für Anzeige
    /// </summary>
    [Ignore]
    public string ActualWorkDisplay => FormatMinutes(ActualWorkMinutes);

    /// <summary>
    /// Formatierter Saldo für Anzeige (mit +/-)
    /// </summary>
    [Ignore]
    public string BalanceDisplay => FormatBalance(BalanceMinutes);

    /// <summary>
    /// Farbe für Saldo (grün = plus, rot = minus)
    /// </summary>
    [Ignore]
    public string BalanceColor => BalanceMinutes >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative;

    /// <summary>
    /// Ist der Tag abgeschlossen? (Hat Check-Out)
    /// </summary>
    [Ignore]
    public bool IsCompleted => LastCheckOut != null;

    /// <summary>
    /// Hat Auto-Pause?
    /// </summary>
    [Ignore]
    public bool HasAutoPause => AutoPauseMinutes > 0;

    /// <summary>
    /// Status-Icon
    /// </summary>
    [Ignore]
    public string StatusIcon => Status switch
    {
        DayStatus.WorkDay => Icons.Briefcase,
        DayStatus.Weekend => Icons.Sleep,
        DayStatus.Vacation => Icons.Beach,
        DayStatus.Holiday => Icons.PartyPopper,
        DayStatus.Sick => Icons.Thermometer,
        DayStatus.HomeOffice => Icons.HomeAccount,
        DayStatus.BusinessTrip => Icons.Airplane,
        DayStatus.OvertimeCompensation => Icons.ClockAlert,
        DayStatus.SpecialLeave => Icons.Gift,
        _ => Icons.CalendarMonth
    };
}
