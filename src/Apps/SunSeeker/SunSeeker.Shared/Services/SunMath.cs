namespace SunSeeker.Shared.Services;

/// <summary>
/// Geteilte Winkel-/Zeit-Mathematik fuer die Sonnenstands- und Ausricht-Berechnung.
/// Reine, deterministische Funktionen ohne Plattform-Abhaengigkeit.
/// </summary>
internal static class SunMath
{
    public const double DegToRad = Math.PI / 180.0;
    public const double RadToDeg = 180.0 / Math.PI;

    public static double Deg2Rad(double deg) => deg * DegToRad;
    public static double Rad2Deg(double rad) => rad * RadToDeg;

    /// <summary>Normalisiert einen Winkel in den Bereich [0, 360).</summary>
    public static double Normalize360(double deg)
    {
        var d = deg % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    /// <summary>Normalisiert eine Winkeldifferenz in den Bereich (-180, 180].</summary>
    public static double NormalizeSigned(double deg)
    {
        var d = (deg + 180.0) % 360.0;
        if (d < 0) d += 360.0;
        return d - 180.0;
    }

    /// <summary>Julianisches Datum (Meeus, gregorianischer Kalender) aus einem UTC-Zeitpunkt,
    /// inklusive Tagesbruchteil.</summary>
    public static double ToJulianDay(DateTime utc)
    {
        var u = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();

        int year = u.Year;
        int month = u.Month;
        double day = u.Day
            + (u.Hour + (u.Minute + (u.Second + u.Millisecond / 1000.0) / 60.0) / 60.0) / 24.0;

        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + a / 4;

        return Math.Floor(365.25 * (year + 4716))
             + Math.Floor(30.6001 * (month + 1))
             + day + b - 1524.5;
    }
}
