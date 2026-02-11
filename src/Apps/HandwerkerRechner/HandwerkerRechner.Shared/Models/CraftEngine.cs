namespace HandwerkerRechner.Models;

/// <summary>
/// Berechnungs-Engine für alle Handwerker-Rechner
/// </summary>
public class CraftEngine
{
    #region Boden & Wand (FREE)

    /// <summary>
    /// Berechnet den Fliesenbedarf
    /// </summary>
    /// <param name="roomLength">Raumlänge in m</param>
    /// <param name="roomWidth">Raumbreite in m</param>
    /// <param name="tileLength">Fliesenlänge in cm</param>
    /// <param name="tileWidth">Fliesenbreite in cm</param>
    /// <param name="wastePercent">Verschnitt in %</param>
    /// <returns>Anzahl benötigter Fliesen</returns>
    public TileResult CalculateTiles(double roomLength, double roomWidth, double tileLength, double tileWidth, double wastePercent = 10)
    {
        var roomArea = roomLength * roomWidth;
        var tileArea = (tileLength / 100) * (tileWidth / 100);
        var tilesNeeded = roomArea / tileArea;
        var tilesWithWaste = tilesNeeded * (1 + wastePercent / 100);

        return new TileResult
        {
            RoomArea = roomArea,
            TileArea = tileArea,
            TilesNeeded = (int)Math.Ceiling(tilesNeeded),
            TilesWithWaste = (int)Math.Ceiling(tilesWithWaste),
            WastePercent = wastePercent
        };
    }

    /// <summary>
    /// Berechnet den Tapetenbedarf
    /// </summary>
    /// <param name="roomLength">Raumlänge in m</param>
    /// <param name="roomWidth">Raumbreite in m</param>
    /// <param name="roomHeight">Raumhöhe in m</param>
    /// <param name="rollLength">Rollenlänge in m (Standard: 10.05)</param>
    /// <param name="rollWidth">Rollenbreite in cm (Standard: 53)</param>
    /// <param name="patternRepeat">Rapport in cm (0 = kein Muster)</param>
    /// <returns>Anzahl benötigter Rollen</returns>
    public WallpaperResult CalculateWallpaper(double roomLength, double roomWidth, double roomHeight,
        double rollLength = 10.05, double rollWidth = 53, double patternRepeat = 0)
    {
        var perimeter = 2 * (roomLength + roomWidth);
        var rollWidthM = rollWidth / 100;

        // Bahnen pro Rolle (unter Berücksichtigung von Rapport)
        var effectiveHeight = roomHeight;
        if (patternRepeat > 0)
        {
            var repeatM = patternRepeat / 100;
            effectiveHeight = Math.Ceiling(roomHeight / repeatM) * repeatM;
        }

        var stripsPerRoll = Math.Floor(rollLength / effectiveHeight);
        if (stripsPerRoll < 1) stripsPerRoll = 1; // Schutz gegen Division durch 0 (Raumhöhe > Rollenlänge)
        var totalStrips = Math.Ceiling(perimeter / rollWidthM);
        var rollsNeeded = Math.Ceiling(totalStrips / stripsPerRoll);

        return new WallpaperResult
        {
            Perimeter = perimeter,
            WallArea = perimeter * roomHeight,
            StripsNeeded = (int)totalStrips,
            StripsPerRoll = (int)stripsPerRoll,
            RollsNeeded = (int)rollsNeeded
        };
    }

    /// <summary>
    /// Berechnet den Farbbedarf
    /// </summary>
    /// <param name="area">Fläche in m²</param>
    /// <param name="coveragePerLiter">Ergiebigkeit in m²/L (Standard: 10)</param>
    /// <param name="coats">Anzahl Anstriche (Standard: 2)</param>
    /// <returns>Benötigte Liter</returns>
    public PaintResult CalculatePaint(double area, double coveragePerLiter = 10, int coats = 2)
    {
        var totalArea = area * coats;
        var litersNeeded = totalArea / coveragePerLiter;

        return new PaintResult
        {
            Area = area,
            TotalArea = totalArea,
            Coats = coats,
            CoveragePerLiter = coveragePerLiter,
            LitersNeeded = Math.Ceiling(litersNeeded * 10) / 10 // Auf 0.1L runden
        };
    }

    /// <summary>
    /// Berechnet den Dielenbedarf
    /// </summary>
    /// <param name="roomLength">Raumlänge in m</param>
    /// <param name="roomWidth">Raumbreite in m</param>
    /// <param name="boardLength">Dielenlänge in m</param>
    /// <param name="boardWidth">Dielenbreite in cm</param>
    /// <param name="wastePercent">Verschnitt in %</param>
    /// <returns>Anzahl benötigter Dielen</returns>
    public FlooringResult CalculateFlooring(double roomLength, double roomWidth, double boardLength, double boardWidth, double wastePercent = 10)
    {
        var roomArea = roomLength * roomWidth;
        var boardArea = boardLength * (boardWidth / 100);
        var boardsNeeded = roomArea / boardArea;
        var boardsWithWaste = boardsNeeded * (1 + wastePercent / 100);

        return new FlooringResult
        {
            RoomArea = roomArea,
            BoardArea = boardArea,
            BoardsNeeded = (int)Math.Ceiling(boardsNeeded),
            BoardsWithWaste = (int)Math.Ceiling(boardsWithWaste),
            WastePercent = wastePercent
        };
    }

    #endregion

    #region Raum/Trockenbau (PREMIUM)

    /// <summary>
    /// Berechnet Trockenbau-Materialbedarf
    /// </summary>
    public DrywallResult CalculateDrywall(double wallLength, double wallHeight, bool doublePlated = false)
    {
        var wallArea = wallLength * wallHeight;
        var plateArea = 2.5 * 1.25; // Standard Gipskartonplatte 250x125cm

        // Profile: CW alle 62.5cm, UW oben und unten
        var cwCount = (int)Math.Ceiling(wallLength / 0.625) + 1;
        var uwLength = wallLength * 2; // Oben und unten

        // Platten: Vorder- und Rückseite, optional doppelt
        var platesPerSide = (int)Math.Ceiling(wallArea / plateArea);
        var totalPlates = platesPerSide * 2 * (doublePlated ? 2 : 1);

        // Schrauben: ca. 25 pro Platte
        var screws = totalPlates * 25;

        return new DrywallResult
        {
            WallArea = wallArea,
            CwProfiles = cwCount,
            UwLengthMeters = uwLength,
            Plates = totalPlates,
            Screws = screws,
            IsDoublePlated = doublePlated
        };
    }

    /// <summary>
    /// Berechnet Sockelleistenbedarf
    /// </summary>
    public double CalculateBaseboard(double perimeter, double doorWidth, int doorCount)
    {
        return perimeter - (doorWidth * doorCount);
    }

    #endregion

    #region Elektriker (PREMIUM)

    /// <summary>
    /// Berechnet Spannungsabfall in Kabeln
    /// </summary>
    /// <param name="voltage">Spannung in V</param>
    /// <param name="current">Strom in A</param>
    /// <param name="length">Kabellänge (einfach) in m</param>
    /// <param name="crossSection">Querschnitt in mm²</param>
    /// <param name="isCopper">true = Kupfer, false = Aluminium</param>
    /// <returns>Spannungsabfall in V und %</returns>
    public VoltageDropResult CalculateVoltageDrop(double voltage, double current, double length, double crossSection, bool isCopper = true)
    {
        // Spezifischer Widerstand: Kupfer = 0.0178, Aluminium = 0.0287 Ohm*mm²/m
        var resistivity = isCopper ? 0.0178 : 0.0287;

        // Formel: U = 2 * I * L * rho / A (Faktor 2 für Hin- und Rückleiter)
        var voltageDrop = 2 * current * length * resistivity / crossSection;
        var percentDrop = (voltageDrop / voltage) * 100;

        return new VoltageDropResult
        {
            VoltageDrop = voltageDrop,
            PercentDrop = percentDrop,
            IsAcceptable = percentDrop <= 3, // Max 3% nach VDE
            Voltage = voltage,
            Current = current,
            Length = length,
            CrossSection = crossSection
        };
    }

    /// <summary>
    /// Berechnet Stromkosten
    /// </summary>
    public PowerCostResult CalculatePowerCost(double watts, double hoursPerDay, double pricePerKwh)
    {
        var kw = watts / 1000;
        var kwhPerDay = kw * hoursPerDay;
        var costPerDay = kwhPerDay * pricePerKwh;

        return new PowerCostResult
        {
            Watts = watts,
            KwhPerDay = kwhPerDay,
            CostPerDay = costPerDay,
            CostPerMonth = costPerDay * 30,
            CostPerYear = costPerDay * 365
        };
    }

    /// <summary>
    /// Ohmsches Gesetz: Berechnet fehlende Werte
    /// </summary>
    public OhmsLawResult CalculateOhmsLaw(double? voltage, double? current, double? resistance, double? power)
    {
        double v = voltage ?? 0, i = current ?? 0, r = resistance ?? 0, p = power ?? 0;

        if (voltage.HasValue && current.HasValue)
        {
            r = i != 0 ? v / i : 0;
            p = v * i;
        }
        else if (voltage.HasValue && resistance.HasValue)
        {
            i = r != 0 ? v / r : 0;
            p = v * i;
        }
        else if (current.HasValue && resistance.HasValue)
        {
            v = i * r;
            p = v * i;
        }
        else if (power.HasValue && voltage.HasValue)
        {
            i = v != 0 ? p / v : 0;
            r = i != 0 ? v / i : 0;
        }
        else if (power.HasValue && current.HasValue)
        {
            v = i != 0 ? p / i : 0;
            r = i != 0 ? v / i : 0;
        }
        else if (power.HasValue && resistance.HasValue)
        {
            // P = I² * R → I = sqrt(P/R)
            i = r != 0 ? Math.Sqrt(p / r) : 0;
            v = i * r;
        }

        return new OhmsLawResult
        {
            Voltage = v,
            Current = i,
            Resistance = r,
            Power = p
        };
    }

    #endregion

    #region Schlosser/Metall (PREMIUM)

    /// <summary>
    /// Berechnet Metallgewicht
    /// </summary>
    public MetalWeightResult CalculateMetalWeight(MetalType metal, ProfileType profile, double length,
        double dimension1, double dimension2 = 0, double wallThickness = 0)
    {
        var density = GetMetalDensity(metal);
        double volume = 0;

        switch (profile)
        {
            case ProfileType.RoundBar:
                var radius = dimension1 / 2 / 1000; // mm zu m
                volume = Math.PI * radius * radius * length;
                break;

            case ProfileType.FlatBar:
                volume = (dimension1 / 1000) * (dimension2 / 1000) * length;
                break;

            case ProfileType.SquareBar:
                volume = (dimension1 / 1000) * (dimension1 / 1000) * length;
                break;

            case ProfileType.RoundTube:
                var outerR = dimension1 / 2 / 1000;
                var innerR = (dimension1 - 2 * wallThickness) / 2 / 1000;
                volume = Math.PI * (outerR * outerR - innerR * innerR) * length;
                break;

            case ProfileType.SquareTube:
                var outer = dimension1 / 1000;
                var inner = (dimension1 - 2 * wallThickness) / 1000;
                volume = (outer * outer - inner * inner) * length;
                break;

            case ProfileType.Angle:
                // L-Profil: 2 Schenkel
                var width = dimension1 / 1000;
                var height = dimension2 / 1000;
                var thick = wallThickness / 1000;
                volume = (width * thick + (height - thick) * thick) * length;
                break;
        }

        var weight = volume * density;

        return new MetalWeightResult
        {
            Metal = metal,
            Profile = profile,
            Length = length,
            Volume = volume * 1000000, // m³ zu cm³
            Weight = weight
        };
    }

    private double GetMetalDensity(MetalType metal) => metal switch
    {
        MetalType.Steel => 7850,
        MetalType.StainlessSteel => 7900,
        MetalType.Aluminum => 2700,
        MetalType.Copper => 8960,
        MetalType.Brass => 8500,
        MetalType.Bronze => 8800,
        _ => 7850
    };

    /// <summary>
    /// Gibt die Kernlochgröße für ein Gewinde zurück
    /// </summary>
    public ThreadDrillResult GetThreadDrill(string threadSize)
    {
        var drillSizes = new Dictionary<string, double>
        {
            { "M3", 2.5 }, { "M4", 3.3 }, { "M5", 4.2 }, { "M6", 5.0 },
            { "M8", 6.8 }, { "M10", 8.5 }, { "M12", 10.2 }, { "M14", 12.0 },
            { "M16", 14.0 }, { "M18", 15.5 }, { "M20", 17.5 }, { "M22", 19.5 },
            { "M24", 21.0 }, { "M27", 24.0 }, { "M30", 26.5 }
        };

        var key = threadSize.ToUpperInvariant();
        if (drillSizes.TryGetValue(key, out var drill))
        {
            return new ThreadDrillResult { ThreadSize = key, DrillSize = drill, Found = true };
        }

        return new ThreadDrillResult { ThreadSize = key, Found = false };
    }

    #endregion

    #region Garten & Landschaft (PREMIUM)

    /// <summary>
    /// Berechnet Pflastersteinbedarf
    /// </summary>
    public PavingResult CalculatePaving(double area, double stoneLength, double stoneWidth, double jointWidth = 3)
    {
        // Steinmaße inkl. Fuge
        var stoneLengthWithJoint = (stoneLength + jointWidth) / 100;
        var stoneWidthWithJoint = (stoneWidth + jointWidth) / 100;
        var stoneArea = stoneLengthWithJoint * stoneWidthWithJoint;

        var stonesNeeded = area / stoneArea;
        var stonesWithWaste = stonesNeeded * 1.05; // 5% Reserve

        return new PavingResult
        {
            Area = area,
            StonesNeeded = (int)Math.Ceiling(stonesNeeded),
            StonesWithReserve = (int)Math.Ceiling(stonesWithWaste)
        };
    }

    /// <summary>
    /// Berechnet Erdbedarf (Mulch, Blumenerde)
    /// </summary>
    public SoilResult CalculateSoil(double area, double depthCm, double bagLiters = 40)
    {
        var volumeLiters = area * (depthCm / 100) * 1000;
        var bags = Math.Ceiling(volumeLiters / bagLiters);

        return new SoilResult
        {
            Area = area,
            DepthCm = depthCm,
            VolumeLiters = volumeLiters,
            BagsNeeded = (int)bags,
            BagLiters = bagLiters
        };
    }

    /// <summary>
    /// Berechnet Teichfolienbedarf
    /// </summary>
    public PondLinerResult CalculatePondLiner(double length, double width, double depth, double overlap = 0.5)
    {
        // Formel: Länge + 2*Tiefe + 2*Überstand
        var linerLength = length + 2 * depth + 2 * overlap;
        var linerWidth = width + 2 * depth + 2 * overlap;
        var linerArea = linerLength * linerWidth;

        return new PondLinerResult
        {
            PondLength = length,
            PondWidth = width,
            PondDepth = depth,
            LinerLength = linerLength,
            LinerWidth = linerWidth,
            LinerArea = linerArea
        };
    }

    #endregion

    #region Dach & Solar (PREMIUM)

    /// <summary>
    /// Berechnet Dachneigung
    /// </summary>
    public RoofPitchResult CalculateRoofPitch(double run, double rise)
    {
        if (run <= 0) run = 0.001; // Schutz gegen Division durch 0
        var pitchRadians = Math.Atan(rise / run);
        var pitchDegrees = pitchRadians * 180 / Math.PI;
        var pitchPercent = (rise / run) * 100;

        return new RoofPitchResult
        {
            Run = run,
            Rise = rise,
            PitchDegrees = pitchDegrees,
            PitchPercent = pitchPercent
        };
    }

    /// <summary>
    /// Berechnet Dachziegelbedarf
    /// </summary>
    public RoofTilesResult CalculateRoofTiles(double roofArea, double tilesPerSqm = 10)
    {
        var tiles = roofArea * tilesPerSqm;
        var tilesWithReserve = tiles * 1.05;

        return new RoofTilesResult
        {
            RoofArea = roofArea,
            TilesPerSqm = tilesPerSqm,
            TilesNeeded = (int)Math.Ceiling(tiles),
            TilesWithReserve = (int)Math.Ceiling(tilesWithReserve)
        };
    }

    /// <summary>
    /// Schätzt Solar-Ertrag
    /// </summary>
    public SolarYieldResult EstimateSolarYield(double roofArea, double panelEfficiency = 0.2, Orientation orientation = Orientation.South, double tiltDegrees = 30)
    {
        // Vereinfachte Berechnung für Deutschland
        var baseYield = 1000; // kWh/m²/Jahr (optimal)

        // Orientierungsfaktor
        var orientationFactor = orientation switch
        {
            Orientation.South => 1.0,
            Orientation.SouthEast or Orientation.SouthWest => 0.95,
            Orientation.East or Orientation.West => 0.85,
            Orientation.NorthEast or Orientation.NorthWest => 0.65,
            Orientation.North => 0.55,
            _ => 1.0
        };

        // Neigungsfaktor (optimal ca. 30-35°)
        var tiltFactor = 1.0 - Math.Abs(tiltDegrees - 32) * 0.005;
        tiltFactor = Math.Max(0.7, Math.Min(1.0, tiltFactor));

        var usableArea = roofArea * 0.7; // ca. 70% nutzbar
        var kwPeak = usableArea * panelEfficiency;
        var annualYield = kwPeak * baseYield * orientationFactor * tiltFactor;

        return new SolarYieldResult
        {
            RoofArea = roofArea,
            UsableArea = usableArea,
            KwPeak = kwPeak,
            AnnualYieldKwh = annualYield,
            Orientation = orientation,
            TiltDegrees = tiltDegrees
        };
    }

    #endregion
}

#region Result Types

public record TileResult
{
    public double RoomArea { get; init; }
    public double TileArea { get; init; }
    public int TilesNeeded { get; init; }
    public int TilesWithWaste { get; init; }
    public double WastePercent { get; init; }
}

public record WallpaperResult
{
    public double Perimeter { get; init; }
    public double WallArea { get; init; }
    public int StripsNeeded { get; init; }
    public int StripsPerRoll { get; init; }
    public int RollsNeeded { get; init; }
}

public record PaintResult
{
    public double Area { get; init; }
    public double TotalArea { get; init; }
    public int Coats { get; init; }
    public double CoveragePerLiter { get; init; }
    public double LitersNeeded { get; init; }
}

public record FlooringResult
{
    public double RoomArea { get; init; }
    public double BoardArea { get; init; }
    public int BoardsNeeded { get; init; }
    public int BoardsWithWaste { get; init; }
    public double WastePercent { get; init; }
}

public record DrywallResult
{
    public double WallArea { get; init; }
    public int CwProfiles { get; init; }
    public double UwLengthMeters { get; init; }
    public int Plates { get; init; }
    public int Screws { get; init; }
    public bool IsDoublePlated { get; init; }
}

public record VoltageDropResult
{
    public double VoltageDrop { get; init; }
    public double PercentDrop { get; init; }
    public bool IsAcceptable { get; init; }
    public double Voltage { get; init; }
    public double Current { get; init; }
    public double Length { get; init; }
    public double CrossSection { get; init; }
}

public record PowerCostResult
{
    public double Watts { get; init; }
    public double KwhPerDay { get; init; }
    public double CostPerDay { get; init; }
    public double CostPerMonth { get; init; }
    public double CostPerYear { get; init; }
}

public record OhmsLawResult
{
    public double Voltage { get; init; }
    public double Current { get; init; }
    public double Resistance { get; init; }
    public double Power { get; init; }
}

public record MetalWeightResult
{
    public MetalType Metal { get; init; }
    public ProfileType Profile { get; init; }
    public double Length { get; init; }
    public double Volume { get; init; }
    public double Weight { get; init; }
}

public record ThreadDrillResult
{
    public string ThreadSize { get; init; } = "";
    public double DrillSize { get; init; }
    public bool Found { get; init; }
}

public record PavingResult
{
    public double Area { get; init; }
    public int StonesNeeded { get; init; }
    public int StonesWithReserve { get; init; }
}

public record SoilResult
{
    public double Area { get; init; }
    public double DepthCm { get; init; }
    public double VolumeLiters { get; init; }
    public int BagsNeeded { get; init; }
    public double BagLiters { get; init; }
}

public record PondLinerResult
{
    public double PondLength { get; init; }
    public double PondWidth { get; init; }
    public double PondDepth { get; init; }
    public double LinerLength { get; init; }
    public double LinerWidth { get; init; }
    public double LinerArea { get; init; }
}

public record RoofPitchResult
{
    public double Run { get; init; }
    public double Rise { get; init; }
    public double PitchDegrees { get; init; }
    public double PitchPercent { get; init; }
}

public record RoofTilesResult
{
    public double RoofArea { get; init; }
    public double TilesPerSqm { get; init; }
    public int TilesNeeded { get; init; }
    public int TilesWithReserve { get; init; }
}

public record SolarYieldResult
{
    public double RoofArea { get; init; }
    public double UsableArea { get; init; }
    public double KwPeak { get; init; }
    public double AnnualYieldKwh { get; init; }
    public Orientation Orientation { get; init; }
    public double TiltDegrees { get; init; }
}

#endregion

#region Enums

public enum MetalType
{
    Steel,
    StainlessSteel,
    Aluminum,
    Copper,
    Brass,
    Bronze
}

public enum ProfileType
{
    RoundBar,
    FlatBar,
    SquareBar,
    RoundTube,
    SquareTube,
    Angle
}

public enum Orientation
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest
}

#endregion
