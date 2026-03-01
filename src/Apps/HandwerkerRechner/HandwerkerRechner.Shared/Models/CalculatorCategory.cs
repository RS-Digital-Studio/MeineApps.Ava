namespace HandwerkerRechner.Models;

/// <summary>
/// Kategorien der Handwerker-Rechner
/// </summary>
public enum CalculatorCategory
{
    /// <summary>Boden und Wand (FREE)</summary>
    FloorWall,

    /// <summary>Raum/Trockenbau (PREMIUM)</summary>
    Drywall,

    /// <summary>Elektriker (PREMIUM)</summary>
    Electrical,

    /// <summary>Schlosser/Metall (PREMIUM)</summary>
    Metal,

    /// <summary>Garten und Landschaft (PREMIUM)</summary>
    Garden,

    /// <summary>Dach und Solar (PREMIUM)</summary>
    RoofSolar
}

/// <summary>
/// Einzelne Rechner innerhalb einer Kategorie
/// </summary>
public enum CalculatorType
{
    // Boden & Wand (FREE)
    Tiles,          // Fliesenbedarf
    Wallpaper,      // Tapetenbedarf
    Paint,          // Farbbedarf
    Flooring,       // Holz/Dielen
    ConcreteSlab,   // Betonplatte
    ConcreteStrip,  // Streifenfundament
    ConcreteColumn, // Betonsäule

    // Raum/Trockenbau (PREMIUM)
    DrywallFraming, // Ständerwerk & Platten
    Baseboard,      // Sockelleisten

    // Elektriker (PREMIUM)
    VoltageDrop,    // Spannungsabfall
    PowerCost,      // Stromkosten
    OhmsLaw,        // Ohmsches Gesetz

    // Schlosser/Metall (PREMIUM)
    MetalWeight,    // Gewichtskalkulator
    ThreadDrill,    // Kernlochbohrung

    // Garten & Landschaft (PREMIUM)
    Paving,         // Pflastersteine
    Soil,           // Blumenerde/Mulch
    PondLiner,      // Teichfolie

    // Dach & Solar (PREMIUM)
    RoofPitch,      // Dachneigung
    RoofTiles,      // Ziegelbedarf
    SolarYield,     // Solar-Ertrag

    // Treppen (PREMIUM)
    Stairs,         // Treppenberechnung

    // Putz (PREMIUM)
    Plaster,        // Putzbedarf

    // Estrich (PREMIUM)
    Screed,         // Estrichbedarf

    // Dämmung (PREMIUM)
    Insulation,     // Dämmstoffberechnung

    // Leitungsquerschnitt (PREMIUM)
    CableSizing,    // Leitungsquerschnitt

    // Fugenmasse (PREMIUM)
    Grout           // Fugenmasse
}
