namespace SunSeeker.Shared.Models;

/// <summary>
/// Ziel der Ausrichtung. Ein mobiles Panel wird anders ausgerichtet als ein quasi-fest
/// installiertes — daher mehrere Modi.
/// </summary>
public enum AlignmentGoal
{
    /// <summary>Maximale Leistung JETZT: Panel direkt auf die aktuelle Sonne (Azimut = Sonnen-Azimut,
    /// Neigung = Zenitwinkel). Sinnvoll fuer ein mobiles Panel, das man tagsueber neu ausrichten kann.</summary>
    NowMaximum,

    /// <summary>Bester Festwinkel fuer den heutigen Tag: Sued, Neigung = Zenitwinkel der Mittagssonne.</summary>
    TodayYield,

    /// <summary>Bester Festwinkel fuer den Jahresertrag (quasi-feste Aufstellung): Sued, flacher Winkel.</summary>
    AnnualYield,

    /// <summary>Steiler Winterwinkel (autarke Winter-Nutzung, tiefstehende Sonne): Sued, steil.</summary>
    WinterYield,
}
