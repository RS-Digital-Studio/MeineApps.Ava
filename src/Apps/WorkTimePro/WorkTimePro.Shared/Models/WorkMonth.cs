using WorkTimePro.Resources.Strings;
using static WorkTimePro.Helpers.TimeFormatter;

namespace WorkTimePro.Models;

/// <summary>
/// Zusammenfassung eines Arbeitsmonats
/// </summary>
public class WorkMonth
{
    /// <summary>
    /// Monat (1-12)
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Jahr
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Soll-Arbeitszeit in Minuten
    /// </summary>
    public int TargetWorkMinutes { get; set; }

    /// <summary>
    /// Tatsächliche Arbeitszeit in Minuten
    /// </summary>
    public int ActualWorkMinutes { get; set; }

    /// <summary>
    /// Gesamte Pausenzeit in Minuten
    /// </summary>
    public int TotalPauseMinutes { get; set; }

    /// <summary>
    /// Saldo in Minuten
    /// </summary>
    public int BalanceMinutes { get; set; }

    /// <summary>
    /// Kumulierter Saldo (inkl. Vormonat)
    /// </summary>
    public int CumulativeBalanceMinutes { get; set; }

    /// <summary>
    /// Anzahl gearbeiteter Tage
    /// </summary>
    public int WorkedDays { get; set; }

    /// <summary>
    /// Soll-Arbeitstage
    /// </summary>
    public int TargetWorkDays { get; set; }

    /// <summary>
    /// Urlaubstage
    /// </summary>
    public int VacationDays { get; set; }

    /// <summary>
    /// Krankheitstage
    /// </summary>
    public int SickDays { get; set; }

    /// <summary>
    /// Feiertage
    /// </summary>
    public int HolidayDays { get; set; }

    /// <summary>
    /// Homeoffice-Tage
    /// </summary>
    public int HomeOfficeDays { get; set; }

    /// <summary>
    /// Ist der Monat abgeschlossen (gesperrt)?
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Datum des Abschlusses
    /// </summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>
    /// Liste der Wochen in diesem Monat
    /// </summary>
    public List<WorkWeek> Weeks { get; set; } = new();

    /// <summary>
    /// Liste der Tage in diesem Monat
    /// </summary>
    public List<WorkDay> Days { get; set; } = new();

    // === Berechnete Properties ===

    /// <summary>
    /// Soll-Arbeitszeit als TimeSpan
    /// </summary>
    public TimeSpan TargetWorkTime => TimeSpan.FromMinutes(TargetWorkMinutes);

    /// <summary>
    /// Tatsächliche Arbeitszeit als TimeSpan
    /// </summary>
    public TimeSpan ActualWorkTime => TimeSpan.FromMinutes(ActualWorkMinutes);

    /// <summary>
    /// Saldo als TimeSpan
    /// </summary>
    public TimeSpan Balance => TimeSpan.FromMinutes(BalanceMinutes);

    /// <summary>
    /// Kumulierter Saldo als TimeSpan
    /// </summary>
    public TimeSpan CumulativeBalance => TimeSpan.FromMinutes(CumulativeBalanceMinutes);

    /// <summary>
    /// Monatsname (z.B. "Januar 2026")
    /// </summary>
    public string MonthDisplay
    {
        get
        {
            var date = new DateTime(Year, Month, 1);
            return date.ToString("MMMM yyyy");
        }
    }

    /// <summary>
    /// Kurzform (z.B. "Jan 26")
    /// </summary>
    public string ShortDisplay
    {
        get
        {
            var date = new DateTime(Year, Month, 1);
            return date.ToString("MMM yy");
        }
    }

    /// <summary>
    /// Formatierte Soll-Zeit
    /// </summary>
    public string TargetWorkDisplay => FormatMinutes(TargetWorkMinutes);

    /// <summary>
    /// Formatierte Ist-Zeit
    /// </summary>
    public string ActualWorkDisplay => FormatMinutes(ActualWorkMinutes);

    /// <summary>
    /// Formatierter Saldo
    /// </summary>
    public string BalanceDisplay => FormatBalance(BalanceMinutes);

    /// <summary>
    /// Formatierter kumulierter Saldo
    /// </summary>
    public string CumulativeBalanceDisplay => FormatBalance(CumulativeBalanceMinutes);

    /// <summary>
    /// Farbe für Saldo
    /// </summary>
    public string BalanceColor => BalanceMinutes >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative;

    /// <summary>
    /// Farbe für kumulierten Saldo
    /// </summary>
    public string CumulativeBalanceColor => CumulativeBalanceMinutes >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative;

    /// <summary>
    /// Durchschnittliche tägliche Arbeitszeit
    /// </summary>
    public TimeSpan AverageDailyWorkTime
    {
        get
        {
            if (WorkedDays == 0) return TimeSpan.Zero;
            return TimeSpan.FromMinutes((double)ActualWorkMinutes / WorkedDays);
        }
    }

    /// <summary>
    /// Status-Text für Abschluss
    /// </summary>
    public string LockStatusDisplay => IsLocked
        ? $"{Helpers.Icons.Lock} {AppStrings.MonthLocked}"
        : $"{Helpers.Icons.Pencil} {AppStrings.MonthOpen}";

}
