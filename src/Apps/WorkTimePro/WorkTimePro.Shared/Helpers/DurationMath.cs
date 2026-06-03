namespace WorkTimePro.Helpers;

/// <summary>
/// DST-bewusste Dauer-Berechnung für Arbeitszeiten.
///
/// Arbeitszeiten werden in WorkTimePro bewusst als Ortszeit (DateTime.Now, Kind=Local/Unspecified)
/// gespeichert (menschenlesbar im UI). Eine naive Subtraktion zweier Wall-Clock-Zeitpunkte
/// ignoriert jedoch den DST-Sprung: Bei einer Schicht über die Sommer-/Winterzeit-Umstellung
/// (z.B. CheckIn 22:00 vor Spring-Forward, CheckOut 06:00 danach) liefert die naive Differenz
/// immer 8h, obwohl real nur 7h (Spring-Forward) bzw. 9h (Fall-Back) vergangen sind.
///
/// Lösung: die UTC-Offsets beider Zeitpunkte abziehen — die so korrigierte Differenz ist die
/// tatsächlich verstrichene Zeit. <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> wirft im
/// Gegensatz zu ConvertTimeToUtc bei mehrdeutigen/ungültigen Zeiten keine Exception.
/// </summary>
public static class DurationMath
{
    /// <summary>
    /// Tatsächlich verstrichene Zeit zwischen zwei (lokalen) Zeitpunkten, DST-korrigiert.
    /// </summary>
    public static TimeSpan RealElapsed(DateTime start, DateTime end)
    {
        // Beide bereits UTC → direkte Differenz ist exakt.
        if (start.Kind == DateTimeKind.Utc && end.Kind == DateTimeKind.Utc)
            return end - start;

        var tz = TimeZoneInfo.Local;
        // elapsedUtc = (end - offset(end)) - (start - offset(start))
        //            = (end - start) - (offset(end) - offset(start))
        var offsetDelta = tz.GetUtcOffset(end) - tz.GetUtcOffset(start);
        return (end - start) - offsetDelta;
    }

    /// <summary>
    /// Tatsächlich verstrichene Minuten zwischen zwei (lokalen) Zeitpunkten, DST-korrigiert.
    /// </summary>
    public static double RealElapsedMinutes(DateTime start, DateTime end)
        => RealElapsed(start, end).TotalMinutes;
}
