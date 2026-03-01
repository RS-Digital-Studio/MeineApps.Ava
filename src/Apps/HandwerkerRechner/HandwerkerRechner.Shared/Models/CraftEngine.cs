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
        if (tileArea <= 0) tileArea = 0.001; // Schutz gegen Division durch 0
        wastePercent = Math.Max(0, wastePercent); // Negativer Verschnitt nicht erlaubt
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
        if (rollWidth <= 0) rollWidth = 0.1; // Schutz gegen Division durch 0
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
        if (coveragePerLiter <= 0) coveragePerLiter = 0.001; // Schutz gegen Division durch 0
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
        if (boardArea <= 0) boardArea = 0.001; // Schutz gegen Division durch 0
        wastePercent = Math.Max(0, wastePercent); // Negativer Verschnitt nicht erlaubt
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
        return Math.Max(0, perimeter - (doorWidth * doorCount));
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
        if (crossSection <= 0) crossSection = 0.001; // Schutz gegen Division durch 0
        var voltageDrop = 2 * current * length * resistivity / crossSection;
        var percentDrop = voltage > 0 ? (voltageDrop / voltage) * 100 : 0; // Schutz gegen Division durch 0

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
            // P = I² * R → I = sqrt(P/R) - Math.Abs verhindert NaN bei negativen Werten
            i = r != 0 ? Math.Sqrt(Math.Abs(p / r)) : 0;
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
                var innerR = Math.Max(0, (dimension1 - 2 * wallThickness) / 2 / 1000); // Wandstärke darf nicht größer als halber Durchmesser sein
                volume = Math.PI * (outerR * outerR - innerR * innerR) * length;
                break;

            case ProfileType.SquareTube:
                var outer = dimension1 / 1000;
                var inner = Math.Max(0, (dimension1 - 2 * wallThickness) / 1000); // Wandstärke darf nicht größer als halbe Kantenlänge sein
                volume = (outer * outer - inner * inner) * length;
                break;

            case ProfileType.Angle:
                // L-Profil: 2 Schenkel
                var width = dimension1 / 1000;
                var height = dimension2 / 1000;
                var thick = Math.Min(wallThickness / 1000, Math.Min(width, height)); // Wandstärke begrenzen
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
    // Statisches Dictionary fuer Kernloch-Tabelle (vermeidet Neuallokation bei jedem Aufruf)
    private static readonly Dictionary<string, double> ThreadDrillSizes = new()
    {
        { "M3", 2.5 }, { "M4", 3.3 }, { "M5", 4.2 }, { "M6", 5.0 },
        { "M8", 6.8 }, { "M10", 8.5 }, { "M12", 10.2 }, { "M14", 12.0 },
        { "M16", 14.0 }, { "M18", 15.5 }, { "M20", 17.5 }, { "M22", 19.5 },
        { "M24", 21.0 }, { "M27", 24.0 }, { "M30", 26.5 }
    };

    public ThreadDrillResult GetThreadDrill(string threadSize)
    {

        var key = threadSize.ToUpperInvariant();
        if (ThreadDrillSizes.TryGetValue(key, out var drill))
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
        // Negative Fugenbreite nicht erlaubt
        if (jointWidth < 0) jointWidth = 0;
        // Steinmaße inkl. Fuge
        var stoneLengthWithJoint = (stoneLength + jointWidth) / 100;
        var stoneWidthWithJoint = (stoneWidth + jointWidth) / 100;
        var stoneArea = stoneLengthWithJoint * stoneWidthWithJoint;
        if (stoneArea <= 0) stoneArea = 0.001; // Schutz gegen Division durch 0

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
        if (bagLiters <= 0) bagLiters = 0.001; // Schutz gegen Division durch 0
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

    #region Beton (FREE)

    /// <summary>
    /// Berechnet Betonbedarf für verschiedene Formen
    /// </summary>
    /// <param name="shape">0=Platte, 1=Fundament (Streifen), 2=Säule (rund)</param>
    /// <param name="dim1">Platte/Fundament: Länge in m, Säule: Durchmesser in cm</param>
    /// <param name="dim2">Platte: Breite in m, Fundament: Breite in cm, Säule: ignoriert</param>
    /// <param name="height">Höhe/Tiefe in cm</param>
    /// <param name="bagWeight">Fertigbeton-Sackgewicht in kg (25 oder 40)</param>
    public ConcreteResult CalculateConcrete(int shape, double dim1, double dim2, double height, double bagWeight = 25)
    {
        double volumeM3;

        switch (shape)
        {
            case 0: // Platte: L x B x H
                volumeM3 = dim1 * dim2 * (height / 100);
                break;
            case 1: // Streifenfundament: Umfang x Breite x Tiefe
                // dim1 = Gesamtlänge des Streifens in m, dim2 = Breite in cm
                volumeM3 = dim1 * (dim2 / 100) * (height / 100);
                break;
            case 2: // Rundsäule: π * r² * h
                var radiusM = (dim1 / 100) / 2; // cm zu m, dann Radius
                volumeM3 = Math.PI * radiusM * radiusM * (height / 100);
                break;
            default:
                volumeM3 = 0;
                break;
        }

        // Standard-Mischverhältnis C25/30 pro m³
        var cementKg = volumeM3 * 300;    // 300 kg Zement pro m³
        var sandKg = volumeM3 * 700;      // 700 kg Sand (0-4mm) pro m³
        var gravelKg = volumeM3 * 1100;   // 1100 kg Kies (4-16mm) pro m³
        var waterL = volumeM3 * 150;      // 150 L Wasser pro m³

        // Fertigbeton-Säcke (1 m³ ≈ 2300 kg Fertigbeton)
        var totalWeight = volumeM3 * 2300;
        if (bagWeight <= 0) bagWeight = 25;
        var bags = (int)Math.Ceiling(totalWeight / bagWeight);

        return new ConcreteResult
        {
            VolumeM3 = volumeM3,
            CementKg = cementKg,
            SandKg = sandKg,
            GravelKg = gravelKg,
            WaterLiters = waterL,
            BagWeight = bagWeight,
            BagsNeeded = bags
        };
    }

    #endregion

    #region Treppen (PREMIUM)

    /// <summary>
    /// Berechnet Treppenmaße nach DIN 18065 (Schrittmaßregel)
    /// </summary>
    /// <param name="floorHeight">Geschosshöhe in cm</param>
    /// <param name="stairWidth">Treppenbreite in cm</param>
    /// <param name="customStepCount">Manuelle Stufenanzahl (0 = automatisch berechnen)</param>
    // DIN 18065 Treppen-Konstanten
    private const double OptimalStepHeightCm = 17.5;
    private const double StepMeasureOptimalCm = 63.0; // Schrittmaßregel: 2h + g = 63 cm
    private const double StepMeasureMinCm = 59.0;
    private const double StepMeasureMaxCm = 65.0;
    private const double MinTreadDepthCm = 15.0;
    private const double DinMinStepHeightCm = 14.0;
    private const double DinMaxStepHeightCm = 21.0;
    private const double DinMinTreadDepthCm = 21.0;
    private const double DinMaxTreadDepthCm = 35.0;

    public StairsResult CalculateStairs(double floorHeight, double stairWidth, int customStepCount = 0)
    {
        // Stufenanzahl: automatisch basierend auf optimaler Stufenhöhe
        int stepCount;
        if (customStepCount > 0)
            stepCount = customStepCount;
        else
            stepCount = (int)Math.Round(floorHeight / OptimalStepHeightCm);

        if (stepCount < 1) stepCount = 1;

        // Stufenhöhe = Geschosshöhe / Stufenanzahl
        var stepHeight = floorHeight / stepCount;

        // Schrittmaßregel: 2h + g = 63 cm (DIN 18065)
        var treadDepth = StepMeasureOptimalCm - 2 * stepHeight;
        if (treadDepth < MinTreadDepthCm) treadDepth = MinTreadDepthCm;

        // Lauflänge = (Stufenanzahl - 1) * Auftrittstiefe (letzte Stufe = Podest)
        var runLength = (stepCount - 1) * treadDepth;

        // Steigungswinkel
        var angle = treadDepth > 0 ? Math.Atan(stepHeight / treadDepth) * 180 / Math.PI : 90;

        // Schrittmaß-Check (DIN 18065)
        var stepMeasure = 2 * stepHeight + treadDepth;
        var isComfortable = stepMeasure >= StepMeasureMinCm && stepMeasure <= StepMeasureMaxCm;
        var isDinCompliant = stepHeight >= DinMinStepHeightCm && stepHeight <= DinMaxStepHeightCm
                          && treadDepth >= DinMinTreadDepthCm && treadDepth <= DinMaxTreadDepthCm;

        // Treppenlänge (Hypotenuse)
        var totalHeight = floorHeight / 100; // in m
        var totalRun = runLength / 100;      // in m
        var stairLength = Math.Sqrt(totalHeight * totalHeight + totalRun * totalRun);

        return new StairsResult
        {
            StepCount = stepCount,
            StepHeight = stepHeight,
            TreadDepth = treadDepth,
            RunLength = runLength,
            StairWidth = stairWidth,
            FloorHeight = floorHeight,
            Angle = angle,
            StepMeasure = stepMeasure,
            StairLength = stairLength * 100, // zurück in cm
            IsComfortable = isComfortable,
            IsDinCompliant = isDinCompliant
        };
    }

    #endregion

    #region Putz (PREMIUM)

    /// <summary>
    /// Berechnet den Putzbedarf für eine Wandfläche
    /// </summary>
    /// <param name="areaSqm">Wandfläche in m²</param>
    /// <param name="thicknessMm">Putzdicke in mm</param>
    /// <param name="plasterType">Putzart: Innen/Außen/Kalk/Gips</param>
    public PlasterResult CalculatePlaster(double areaSqm, double thicknessMm, string plasterType)
    {
        if (areaSqm <= 0) areaSqm = 0.1;
        if (thicknessMm <= 0) thicknessMm = 1;

        // Verbrauch pro m² und mm Dicke (kg/m²/mm)
        var densityPerMmPerSqm = plasterType switch
        {
            "Außen" => 1.2,  // Zementputz
            "Kalk" => 0.9,   // Kalkputz
            "Gips" => 0.8,   // Gipsputz
            _ => 1.0          // Innenputz (Kalk-Zement)
        };

        var totalKg = areaSqm * thicknessMm * densityPerMmPerSqm;
        var bagsNeeded = (int)Math.Ceiling(totalKg / 30.0); // 30kg Standardsäcke

        return new PlasterResult
        {
            Area = areaSqm,
            ThicknessMm = thicknessMm,
            PlasterType = plasterType,
            PlasterKg = totalKg,
            BagsNeeded = bagsNeeded
        };
    }

    #endregion

    #region Estrich (PREMIUM)

    /// <summary>
    /// Berechnet den Estrichbedarf für eine Bodenfläche
    /// </summary>
    /// <param name="areaSqm">Bodenfläche in m²</param>
    /// <param name="thicknessCm">Estrichdicke in cm</param>
    /// <param name="screedType">Estrich-Typ: Zement/Fließ/Anhydrit</param>
    public ScreedResult CalculateScreed(double areaSqm, double thicknessCm, string screedType)
    {
        if (areaSqm <= 0) areaSqm = 0.1;
        if (thicknessCm <= 0) thicknessCm = 0.1;

        var volumeM3 = areaSqm * thicknessCm / 100.0;

        // Dichte je nach Estrich-Typ (kg/m³)
        var density = screedType switch
        {
            "Fließ" => 2000.0,
            "Anhydrit" => 2200.0,
            _ => 2100.0  // Zement
        };

        var weightKg = volumeM3 * density;
        var bagsNeeded = (int)Math.Ceiling(weightKg / 40.0); // 40kg Säcke

        // Trocknungszeit: bis 40mm ca. 1 Tag/mm, darüber 2 Tage/mm
        var thicknessMm = thicknessCm * 10;
        int dryingDays;
        if (thicknessMm <= 40)
            dryingDays = (int)Math.Ceiling(thicknessMm);
        else
            dryingDays = 40 + (int)Math.Ceiling((thicknessMm - 40) * 2);

        return new ScreedResult
        {
            Area = areaSqm,
            ThicknessCm = thicknessCm,
            ScreedType = screedType,
            VolumeM3 = volumeM3,
            WeightKg = weightKg,
            BagsNeeded = bagsNeeded,
            DryingDays = dryingDays
        };
    }

    #endregion

    #region Dämmung (PREMIUM)

    /// <summary>
    /// Berechnet die benötigte Dämmstoffdicke anhand von U-Werten
    /// </summary>
    /// <param name="areaSqm">Fläche in m²</param>
    /// <param name="currentUValue">Ist-U-Wert in W/(m²·K)</param>
    /// <param name="targetUValue">Soll-U-Wert in W/(m²·K)</param>
    /// <param name="insulationType">0=EPS, 1=XPS, 2=Mineralwolle, 3=Holzfaser</param>
    public InsulationResult CalculateInsulation(double areaSqm, double currentUValue, double targetUValue, int insulationType)
    {
        if (areaSqm <= 0) areaSqm = 0.1;
        if (currentUValue <= 0) currentUValue = 0.01;
        if (targetUValue <= 0) targetUValue = 0.01;
        if (targetUValue >= currentUValue) targetUValue = currentUValue * 0.5; // Soll < Ist

        // Wärmeleitfähigkeit Lambda (W/(m·K))
        double lambda = insulationType switch
        {
            1 => 0.035,  // XPS
            2 => 0.040,  // Mineralwolle
            3 => 0.045,  // Holzfaser
            _ => 0.032   // EPS (Styropor)
        };

        // Formel: d = lambda * (1/U_soll - 1/U_ist)
        double thicknessM = lambda * (1.0 / targetUValue - 1.0 / currentUValue);
        double thicknessCm = Math.Max(1, Math.Ceiling(thicknessM * 100));

        // Platten/Rollen: Standard 120x60cm = 0.72 m²
        double pieceArea = 0.72;
        int piecesNeeded = (int)Math.Ceiling(areaSqm / pieceArea);

        // Kosten pro m² je Material
        double costPerSqm = insulationType switch
        {
            1 => 15.0,  // XPS
            2 => 12.0,  // Mineralwolle
            3 => 20.0,  // Holzfaser
            _ => 8.0    // EPS
        };
        double estimatedCost = areaSqm * costPerSqm;

        return new InsulationResult
        {
            Area = areaSqm,
            CurrentUValue = currentUValue,
            TargetUValue = targetUValue,
            InsulationType = insulationType,
            Lambda = lambda,
            ThicknessCm = thicknessCm,
            PiecesNeeded = piecesNeeded,
            EstimatedCostPerSqm = costPerSqm,
            EstimatedTotalCost = estimatedCost
        };
    }

    #endregion

    #region Leitungsquerschnitt (PREMIUM)

    /// <summary>
    /// Berechnet den mindestens benötigten Leitungsquerschnitt
    /// </summary>
    /// <param name="currentAmps">Strom in Ampere</param>
    /// <param name="lengthM">Kabellänge (einfach) in Meter</param>
    /// <param name="voltageV">Nennspannung (230 oder 400V)</param>
    /// <param name="materialType">0=Kupfer, 1=Aluminium</param>
    /// <param name="maxDropPercent">Maximaler Spannungsabfall in % (Standard 3%)</param>
    public CableSizingResult CalculateCableSize(double currentAmps, double lengthM, double voltageV, int materialType, double maxDropPercent = 3.0)
    {
        if (currentAmps <= 0) currentAmps = 0.1;
        if (lengthM <= 0) lengthM = 0.1;
        if (voltageV <= 0) voltageV = 230;
        if (maxDropPercent <= 0) maxDropPercent = 3.0;

        // Spezifischer Widerstand (Ohm·mm²/m)
        double resistivity = materialType switch
        {
            1 => 0.0287,  // Aluminium
            _ => 0.0178   // Kupfer
        };

        // Formel: A = (2 × I × L × ρ) / (U × ΔU%/100)
        double minCrossSection = (2 * currentAmps * lengthM * resistivity) / (voltageV * maxDropPercent / 100.0);

        // Standardquerschnitte nach DIN VDE
        double[] standardSizes = { 1.5, 2.5, 4.0, 6.0, 10.0, 16.0, 25.0, 35.0, 50.0, 70.0, 95.0, 120.0 };
        double recommendedSize = standardSizes[^1]; // Fallback: größter
        foreach (var size in standardSizes)
        {
            if (size >= minCrossSection)
            {
                recommendedSize = size;
                break;
            }
        }

        // Tatsächlicher Spannungsabfall mit empfohlenem Querschnitt
        double actualDropV = (2 * currentAmps * lengthM * resistivity) / recommendedSize;
        double actualDropPercent = (actualDropV / voltageV) * 100;

        // VDE-konform: max 3% für Steckdosen, max 5% für Beleuchtung
        bool isVdeCompliant = actualDropPercent <= maxDropPercent;

        return new CableSizingResult
        {
            CurrentAmps = currentAmps,
            LengthM = lengthM,
            VoltageV = voltageV,
            MaterialType = materialType,
            Resistivity = resistivity,
            MinCrossSection = minCrossSection,
            RecommendedCrossSection = recommendedSize,
            ActualDropV = actualDropV,
            ActualDropPercent = actualDropPercent,
            MaxDropPercent = maxDropPercent,
            IsVdeCompliant = isVdeCompliant
        };
    }

    #endregion

    #region Fugenmasse (Grout)

    /// <summary>
    /// Berechnet den Fugenmasse-Bedarf
    /// Formel: Masse = Fläche * ((L+B)/(L×B)) × Fugenbreite × Fugentiefe × Dichte
    /// Alle Maße werden intern in mm umgerechnet
    /// </summary>
    public static GroutResult CalculateGrout(double areaSqm, double tileLengthCm, double tileWidthCm, double groutWidthMm, double groutDepthMm, double pricePerKg = 2.5)
    {
        if (areaSqm <= 0 || tileLengthCm <= 0 || tileWidthCm <= 0 || groutWidthMm <= 0 || groutDepthMm <= 0)
            return new GroutResult();

        // Fliesenmaße in mm umrechnen
        double tileLMm = tileLengthCm * 10;
        double tileWMm = tileWidthCm * 10;

        // Industrieformel: V = Fläche × ((L+B)/(L×B)) × Fugenbreite × Fugentiefe
        // Ergebnis in mm³ pro m² → umrechnen in cm³ → kg mit Dichte
        double groutDensity = 1.6; // g/cm³ (Standard-Fugenmasse)

        // Fugenmasse-Verbrauch in kg/m²
        double consumptionPerSqm = ((tileLMm + tileWMm) / (tileLMm * tileWMm)) * groutWidthMm * groutDepthMm * groutDensity / 1000.0;

        double totalKg = areaSqm * consumptionPerSqm;

        // 10% Reserve
        double totalWithReserve = totalKg * 1.10;

        // Eimer à 5 kg
        int bucketsNeeded = (int)Math.Ceiling(totalWithReserve / 5.0);

        // Kosten
        double totalCost = totalWithReserve * pricePerKg;

        return new GroutResult
        {
            AreaSqm = areaSqm,
            TileLengthCm = tileLengthCm,
            TileWidthCm = tileWidthCm,
            GroutWidthMm = groutWidthMm,
            GroutDepthMm = groutDepthMm,
            ConsumptionPerSqm = consumptionPerSqm,
            TotalKg = totalKg,
            TotalWithReserveKg = totalWithReserve,
            BucketsNeeded = bucketsNeeded,
            PricePerKg = pricePerKg,
            TotalCost = totalCost
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

public record ConcreteResult
{
    public double VolumeM3 { get; init; }
    public double CementKg { get; init; }
    public double SandKg { get; init; }
    public double GravelKg { get; init; }
    public double WaterLiters { get; init; }
    public double BagWeight { get; init; }
    public int BagsNeeded { get; init; }
}

public record StairsResult
{
    public int StepCount { get; init; }
    public double StepHeight { get; init; }     // cm
    public double TreadDepth { get; init; }     // cm (Auftrittstiefe)
    public double RunLength { get; init; }      // cm (Lauflänge)
    public double StairWidth { get; init; }     // cm
    public double FloorHeight { get; init; }    // cm (Geschosshöhe)
    public double Angle { get; init; }          // Grad
    public double StepMeasure { get; init; }    // 2h+g (Schrittmaß)
    public double StairLength { get; init; }    // cm (Hypotenuse)
    public bool IsComfortable { get; init; }    // Schrittmaß 59-65
    public bool IsDinCompliant { get; init; }   // DIN 18065 konform
}

public record PlasterResult
{
    public double Area { get; init; }
    public double ThicknessMm { get; init; }
    public string PlasterType { get; init; } = "";
    public double PlasterKg { get; init; }
    public int BagsNeeded { get; init; }
}

public record ScreedResult
{
    public double Area { get; init; }
    public double ThicknessCm { get; init; }
    public string ScreedType { get; init; } = "";
    public double VolumeM3 { get; init; }
    public double WeightKg { get; init; }
    public int BagsNeeded { get; init; }
    public int DryingDays { get; init; }
}

public record InsulationResult
{
    public double Area { get; init; }
    public double CurrentUValue { get; init; }
    public double TargetUValue { get; init; }
    public int InsulationType { get; init; }
    public double Lambda { get; init; }
    public double ThicknessCm { get; init; }
    public int PiecesNeeded { get; init; }
    public double EstimatedCostPerSqm { get; init; }
    public double EstimatedTotalCost { get; init; }
}

public record CableSizingResult
{
    public double CurrentAmps { get; init; }
    public double LengthM { get; init; }
    public double VoltageV { get; init; }
    public int MaterialType { get; init; }
    public double Resistivity { get; init; }
    public double MinCrossSection { get; init; }
    public double RecommendedCrossSection { get; init; }
    public double ActualDropV { get; init; }
    public double ActualDropPercent { get; init; }
    public double MaxDropPercent { get; init; }
    public bool IsVdeCompliant { get; init; }
}

public record GroutResult
{
    public double AreaSqm { get; init; }
    public double TileLengthCm { get; init; }
    public double TileWidthCm { get; init; }
    public double GroutWidthMm { get; init; }
    public double GroutDepthMm { get; init; }
    public double ConsumptionPerSqm { get; init; }
    public double TotalKg { get; init; }
    public double TotalWithReserveKg { get; init; }
    public int BucketsNeeded { get; init; }
    public double PricePerKg { get; init; }
    public double TotalCost { get; init; }
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
