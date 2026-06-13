using System.Globalization;
using System.Text.RegularExpressions;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Helpers;

/// <summary>
/// Gemeinsame Formatierungs- und Status-Hilfsmethoden (zentral statt Code-Duplikation)
/// </summary>
public static class TimeFormatter
{
    private static CultureInfo? _dayMonthCulture;
    private static string _dayMonthPattern = "dd.MM";

    /// <summary>
    /// Kulturbasiertes Kurzformat "Tag und Monat" ohne Jahr (de: "13.06", en-US: "6/13").
    /// Abgeleitet aus dem ShortDatePattern der aktuellen Kultur (Jahr-Anteil entfernt) —
    /// hartkodiertes "dd.MM." würde EN/ES/FR-Nutzern ein deutsches Format zeigen.
    /// </summary>
    public static string FormatDayMonth(DateTime date)
    {
        var culture = CultureInfo.CurrentCulture;
        if (!Equals(culture, _dayMonthCulture))
        {
            var p = Regex.Replace(culture.DateTimeFormat.ShortDatePattern, "y+", "");
            p = p.Trim('/', '-', '.', ',', ' ');
            p = Regex.Replace(p, @"([./\-, ])\1+", "$1"); // doppelte Trenner (Jahr stand mittig)
            _dayMonthPattern = p.Length > 0 ? p : "dd.MM";
            _dayMonthCulture = culture;
        }

        return date.ToString(_dayMonthPattern, culture);
    }

    /// <summary>
    /// Formatiert Minuten als "H:MM" mit korrektem Vorzeichen
    /// </summary>
    public static string FormatMinutes(int minutes)
    {
        var sign = minutes < 0 ? "-" : "";
        var absMinutes = Math.Abs(minutes);
        var hours = absMinutes / 60;
        var mins = absMinutes % 60;
        return $"{sign}{hours}:{mins:D2}";
    }

    /// <summary>
    /// Formatiert Minuten als Saldo mit Vorzeichen "+H:MM" / "-H:MM"
    /// </summary>
    public static string FormatBalance(int minutes)
    {
        var sign = minutes >= 0 ? "+" : "";
        return sign + FormatMinutes(minutes);
    }

    /// <summary>
    /// Lokalisierter Name für DayStatus
    /// </summary>
    public static string GetStatusName(DayStatus status) => status switch
    {
        DayStatus.Weekend => AppStrings.DayStatus_Weekend,
        DayStatus.Vacation => AppStrings.DayStatus_Vacation,
        DayStatus.Sick => AppStrings.DayStatus_Sick,
        DayStatus.HomeOffice => AppStrings.DayStatus_HomeOffice,
        DayStatus.BusinessTrip => AppStrings.DayStatus_BusinessTrip,
        DayStatus.SpecialLeave => AppStrings.SpecialLeave,
        DayStatus.UnpaidLeave => AppStrings.UnpaidLeave,
        DayStatus.OvertimeCompensation => AppStrings.OvertimeCompensation,
        DayStatus.Holiday => AppStrings.DayStatus_Holiday,
        DayStatus.Training => AppStrings.DayStatus_Training,
        DayStatus.CompensatoryTime => AppStrings.DayStatus_CompensatoryTime,
        _ => AppStrings.DayStatus_WorkDay
    };
}
