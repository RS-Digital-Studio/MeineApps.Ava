namespace HandwerkerImperium.Helpers;

/// <summary>
/// Hilfsmethoden für Zeitbeschleunigung (Rewarded Ads / Premium).
/// </summary>
public static class TimeSkipHelper
{
    /// <summary>
    /// Berechnet die übersprungenen Minuten für Zeitbeschleunigung.
    /// Bis 10 Min komplett, darüber 70% Effizienz.
    /// </summary>
    public static double CalculateTimeSkipMinutes(double totalMinutes)
    {
        if (totalMinutes <= 10) return totalMinutes;
        return 10 + (totalMinutes - 10) * 0.7;
    }
}
