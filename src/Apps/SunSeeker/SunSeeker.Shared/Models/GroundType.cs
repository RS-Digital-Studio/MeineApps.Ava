namespace SunSeeker.Shared.Models;

/// <summary>
/// Untergrund unter dem Panel. Entscheidend fuer bifaziale Module: Die Rueckseite erntet
/// vom Boden reflektiertes Licht — je heller der Untergrund (hoehere Albedo), desto hoeher
/// der Mehrertrag. Albedo-Werte sind Mittelwerte aus der Literatur (RatedPower, TheGreenWatt,
/// NREL); sie schwanken mit Feuchte, Alter und Verschmutzung.
/// </summary>
public enum GroundType
{
    Grass,
    Soil,
    Asphalt,
    Concrete,
    Sand,
    Gravel,
    WhiteSurface,
    Snow,
    Water,
}

public static class GroundTypeExtensions
{
    /// <summary>Albedo (Anteil des reflektierten Sonnenlichts, 0..1).</summary>
    public static double Albedo(this GroundType ground) => ground switch
    {
        GroundType.Grass => 0.20,
        GroundType.Soil => 0.17,
        GroundType.Asphalt => 0.12,
        GroundType.Concrete => 0.30,
        GroundType.Sand => 0.35,
        GroundType.Gravel => 0.55,        // heller Kies
        GroundType.WhiteSurface => 0.75,  // weisse Plane / Membran
        GroundType.Snow => 0.85,          // frischer Schnee
        GroundType.Water => 0.08,
        _ => 0.20,
    };

    /// <summary>Deutscher Anzeigename.</summary>
    public static string DisplayName(this GroundType ground) => ground switch
    {
        GroundType.Grass => "Gras / Rasen",
        GroundType.Soil => "Erde / Mutterboden",
        GroundType.Asphalt => "Asphalt (dunkel)",
        GroundType.Concrete => "Beton",
        GroundType.Sand => "Sand",
        GroundType.Gravel => "Heller Kies",
        GroundType.WhiteSurface => "Weisse Plane / Flaeche",
        GroundType.Snow => "Schnee",
        GroundType.Water => "Wasser",
        _ => ground.ToString(),
    };
}
