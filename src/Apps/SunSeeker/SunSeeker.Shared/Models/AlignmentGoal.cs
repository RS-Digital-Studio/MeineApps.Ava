namespace SunSeeker.Shared.Models;

/// <summary>
/// Ziel der Ausrichtung. Ein mobiles Panel wird anders ausgerichtet als ein quasi-fest
/// installiertes — daher mehrere Modi.
/// </summary>
public enum AlignmentGoal
{
    /// <summary>Maximale Leistung JETZT: Panel direkt auf die aktuelle Sonne (Azimut = Sonnen-Azimut,
    /// Neigung = Zenitwinkel). Sinnvoll für ein mobiles Panel, das man tagsüber neu ausrichten kann.</summary>
    NowMaximum,

    /// <summary>Bester Festwinkel für den heutigen Tag: Süd, Neigung = Zenitwinkel der Mittagssonne.</summary>
    TodayYield,

    /// <summary>Bester Festwinkel fürs Sommerhalbjahr (Nordhalbkugel: April–Oktober) — Panel wird
    /// einmal aufgestellt und bleibt die ganze Saison stehen: Süd, flacher als das Jahresoptimum.</summary>
    SeasonYield,

    /// <summary>Bester Festwinkel für den Jahresertrag (quasi-feste Aufstellung): Süd, flacher Winkel.</summary>
    AnnualYield,

    /// <summary>Steiler Winterwinkel (autarke Winter-Nutzung, tiefstehende Sonne): Süd, steil.</summary>
    WinterYield,
}
