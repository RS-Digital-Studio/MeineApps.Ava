using WorkTimePro.Resources.Strings;
using static WorkTimePro.Helpers.TimeFormatter;

namespace WorkTimePro.Models;

/// <summary>
/// Zusammenfassung einer Arbeitswoche
/// </summary>
public class WorkWeek
{
    /// <summary>
    /// Kalenderwoche (ISO 8601)
    /// </summary>
    public int WeekNumber { get; set; }

    /// <summary>
    /// Jahr
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Erster Tag der Woche (Montag)
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Letzter Tag der Woche (Sonntag)
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Soll-Arbeitszeit in Minuten (z.B. 2400 = 40h)
    /// </summary>
    public int TargetWorkMinutes { get; set; } = 2400;

    /// <summary>
    /// Tatsächliche Arbeitszeit in Minuten
    /// </summary>
    public int ActualWorkMinutes { get; set; }

    /// <summary>
    /// Gesamte Pausenzeit in Minuten
    /// </summary>
    public int TotalPauseMinutes { get; set; }

    /// <summary>
    /// Saldo in Minuten (Ist - Soll)
    /// </summary>
    public int BalanceMinutes { get; set; }

    /// <summary>
    /// Anzahl gearbeiteter Tage
    /// </summary>
    public int WorkedDays { get; set; }

    /// <summary>
    /// Urlaubstage in dieser Woche
    /// </summary>
    public int VacationDays { get; set; }

    /// <summary>
    /// Krankheitstage in dieser Woche
    /// </summary>
    public int SickDays { get; set; }

    /// <summary>
    /// Feiertage in dieser Woche
    /// </summary>
    public int HolidayDays { get; set; }

    /// <summary>
    /// Liste der einzelnen Tage
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
    /// Fortschritt in Prozent (0-100, kann über 100 gehen)
    /// </summary>
    public double ProgressPercent
    {
        get
        {
            if (TargetWorkMinutes == 0) return 0;
            return Math.Min(100, (ActualWorkMinutes * 100.0) / TargetWorkMinutes);
        }
    }

    /// <summary>
    /// Formatierter Zeitraum, kulturspezifisch (z.B. "20.01. - 26.01." oder "01/20 - 01/26")
    /// </summary>
    public string DateRangeDisplay
    {
        get
        {
            var start = StartDate.ToDateTime(TimeOnly.MinValue).ToString("d");
            var end = EndDate.ToDateTime(TimeOnly.MinValue).ToString("d");
            return $"{start} - {end}";
        }
    }

    /// <summary>
    /// Formatierte Woche, lokalisiert (z.B. "KW 4 / 2026" oder "CW 4 / 2026")
    /// </summary>
    public string WeekDisplay => string.Format(
        Resources.Strings.AppStrings.WeekNumberFormat ?? "CW {0} / {1}",
        WeekNumber, Year);

    /// <summary>
    /// Formatierte Soll-Zeit
    /// </summary>
    public string TargetWorkDisplay => FormatMinutes(TargetWorkMinutes);

    /// <summary>
    /// Formatierte Ist-Zeit
    /// </summary>
    public string ActualWorkDisplay => FormatMinutes(ActualWorkMinutes);

    /// <summary>
    /// Formatierter Saldo (mit +/-)
    /// </summary>
    public string BalanceDisplay => FormatBalance(BalanceMinutes);

    /// <summary>
    /// Farbe für Saldo
    /// </summary>
    public string BalanceColor => BalanceMinutes >= 0 ? AppColors.BalancePositive : AppColors.BalanceNegative;
}
