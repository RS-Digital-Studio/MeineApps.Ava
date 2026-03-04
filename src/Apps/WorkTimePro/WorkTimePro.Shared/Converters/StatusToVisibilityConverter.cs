using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using WorkTimePro.Models;

namespace WorkTimePro.Converters;

/// <summary>
/// Konvertiert TrackingStatus zu Visibility (für Pause-Button)
/// </summary>
public class StatusToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TrackingStatus status)
        {
            // Pause-Button nur sichtbar wenn Working oder OnBreak
            return status != TrackingStatus.Idle;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert Prozent (0-100) zu Progress (0-1)
/// </summary>
public class PercentToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return percent / 100.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert TimeSpan zu String (HH:mm Format)
/// </summary>
public class TimeSpanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            var totalHours = (int)Math.Abs(ts.TotalHours);
            var minutes = Math.Abs(ts.Minutes);
            var sign = ts.TotalMinutes < 0 ? "-" : "";
            return $"{sign}{totalHours}:{minutes:D2}";
        }
        return "0:00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert Balance-Minuten zu Farbe (grün/rot)
/// </summary>
public class BalanceToColorConverter : IValueConverter
{
    private static readonly IBrush PositiveBrush = SolidColorBrush.Parse("#4CAF50");
    private static readonly IBrush NegativeBrush = SolidColorBrush.Parse("#F44336");
    private static readonly IBrush NeutralBrush = SolidColorBrush.Parse("#9E9E9E");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int minutes)
        {
            return minutes >= 0
                ? PositiveBrush
                : NegativeBrush;
        }
        return NeutralBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert DayStatus zu Farbe
/// </summary>
public class DayStatusToColorConverter : IValueConverter
{
    private static readonly IBrush WorkDayBrush = SolidColorBrush.Parse("#4CAF50");
    private static readonly IBrush WeekendBrush = SolidColorBrush.Parse("#9E9E9E");
    private static readonly IBrush VacationBrush = SolidColorBrush.Parse("#2196F3");
    private static readonly IBrush HolidayBrush = SolidColorBrush.Parse("#FF9800");
    private static readonly IBrush SickBrush = SolidColorBrush.Parse("#F44336");
    private static readonly IBrush HomeOfficeBrush = SolidColorBrush.Parse("#9C27B0");
    private static readonly IBrush BusinessTripBrush = SolidColorBrush.Parse("#00BCD4");
    private static readonly IBrush DayStatusDefaultBrush = SolidColorBrush.Parse("#757575");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DayStatus status)
        {
            return status switch
            {
                DayStatus.WorkDay => WorkDayBrush,
                DayStatus.Weekend => WeekendBrush,
                DayStatus.Vacation => VacationBrush,
                DayStatus.Holiday => HolidayBrush,
                DayStatus.Sick => SickBrush,
                DayStatus.HomeOffice => HomeOfficeBrush,
                DayStatus.BusinessTrip => BusinessTripBrush,
                _ => DayStatusDefaultBrush
            };
        }
        return DayStatusDefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert Bool zu Auto-Pause Icon (Lightning oder leer)
/// </summary>
public class AutoPauseToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAutoPause && isAutoPause)
        {
            return WorkTimePro.Helpers.Icons.Lightning;
        }
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert Arbeitsminuten zu Heatmap-Farbe
/// </summary>
public class HeatmapValueToColorConverter : IValueConverter
{
    private static readonly IBrush EmptyBrush = SolidColorBrush.Parse("#EEEEEE");
    private static readonly IBrush LightGreenBrush = SolidColorBrush.Parse("#C8E6C9");
    private static readonly IBrush MediumGreenBrush = SolidColorBrush.Parse("#81C784");
    private static readonly IBrush DarkGreenBrush = SolidColorBrush.Parse("#4CAF50");
    private static readonly IBrush NormalGreenBrush = SolidColorBrush.Parse("#388E3C");
    private static readonly IBrush OvertimeBrush = SolidColorBrush.Parse("#F44336");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int minutes)
        {
            // 0 Minuten = keine Farbe
            if (minutes == 0)
                return EmptyBrush;

            // Bis 4h = hellgrün
            if (minutes < 240)
                return LightGreenBrush;

            // 4-6h = mittelgrün
            if (minutes < 360)
                return MediumGreenBrush;

            // 6-8h = dunkelgrün
            if (minutes < 480)
                return DarkGreenBrush;

            // 8-10h = normal
            if (minutes < 600)
                return NormalGreenBrush;

            // Über 10h = rot (Überstunden)
            return OvertimeBrush;
        }
        return EmptyBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
