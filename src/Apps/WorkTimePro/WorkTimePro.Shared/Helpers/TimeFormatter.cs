using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Helpers;

/// <summary>
/// Gemeinsame Formatierungs- und Status-Hilfsmethoden (zentral statt Code-Duplikation)
/// </summary>
public static class TimeFormatter
{
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
