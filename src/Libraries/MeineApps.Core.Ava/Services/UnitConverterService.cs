namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Unit converter service implementation (Metric/Imperial)
/// </summary>
public sealed class UnitConverterService : IUnitConverterService
{
    private const string UnitSystemKey = "unit_system";
    private readonly IPreferencesService _preferences;
    private UnitSystem _currentSystem;

    public UnitSystem CurrentSystem
    {
        get => _currentSystem;
        set
        {
            if (_currentSystem != value)
            {
                _currentSystem = value;
                _preferences.Set(UnitSystemKey, (int)value);
                UnitSystemChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? UnitSystemChanged;

    public UnitConverterService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _currentSystem = (UnitSystem)_preferences.Get(UnitSystemKey, 0);
    }

    #region Length (1 m = 3.28084 ft)

    private const double MetersToFeet = 3.28084;

    public double ConvertLength(double meters, bool toDisplay = true)
        => CurrentSystem == UnitSystem.Metric ? meters : toDisplay ? meters * MetersToFeet : meters / MetersToFeet;

    public string GetLengthUnit(bool abbreviated = true)
        => CurrentSystem == UnitSystem.Metric ? (abbreviated ? "m" : "Meter") : (abbreviated ? "ft" : "Feet");

    public string FormatLength(double meters, int decimals = 2)
        => $"{ConvertLength(meters).ToString($"F{decimals}")} {GetLengthUnit()}";

    #endregion

    #region Area (1 m² = 10.7639 ft²)

    private const double SqmToSqft = 10.7639;

    public double ConvertArea(double squareMeters, bool toDisplay = true)
        => CurrentSystem == UnitSystem.Metric ? squareMeters : toDisplay ? squareMeters * SqmToSqft : squareMeters / SqmToSqft;

    public string GetAreaUnit(bool abbreviated = true)
        => CurrentSystem == UnitSystem.Metric ? (abbreviated ? "m\u00b2" : "Quadratmeter") : (abbreviated ? "ft\u00b2" : "Square Feet");

    public string FormatArea(double squareMeters, int decimals = 2)
        => $"{ConvertArea(squareMeters).ToString($"F{decimals}")} {GetAreaUnit()}";

    #endregion

    #region Volume (1 L = 0.264172 gal)

    private const double LitersToGallons = 0.264172;

    public double ConvertVolume(double liters, bool toDisplay = true)
        => CurrentSystem == UnitSystem.Metric ? liters : toDisplay ? liters * LitersToGallons : liters / LitersToGallons;

    public string GetVolumeUnit(bool abbreviated = true)
        => CurrentSystem == UnitSystem.Metric ? (abbreviated ? "L" : "Liter") : (abbreviated ? "gal" : "Gallons");

    public string FormatVolume(double liters, int decimals = 2)
        => $"{ConvertVolume(liters).ToString($"F{decimals}")} {GetVolumeUnit()}";

    #endregion

    #region Weight (1 kg = 2.20462 lbs)

    private const double KgToLbs = 2.20462;

    public double ConvertWeight(double kilograms, bool toDisplay = true)
        => CurrentSystem == UnitSystem.Metric ? kilograms : toDisplay ? kilograms * KgToLbs : kilograms / KgToLbs;

    public string GetWeightUnit(bool abbreviated = true)
        => CurrentSystem == UnitSystem.Metric ? (abbreviated ? "kg" : "Kilogramm") : (abbreviated ? "lbs" : "Pounds");

    public string FormatWeight(double kilograms, int decimals = 2)
        => $"{ConvertWeight(kilograms).ToString($"F{decimals}")} {GetWeightUnit()}";

    #endregion

    #region Temperature

    public double ConvertTemperature(double celsius, bool toDisplay = true)
        => CurrentSystem == UnitSystem.Metric ? celsius : toDisplay ? (celsius * 9.0 / 5.0) + 32 : (celsius - 32) * 5.0 / 9.0;

    public string GetTemperatureUnit(bool abbreviated = true)
        => CurrentSystem == UnitSystem.Metric ? (abbreviated ? "\u00b0C" : "Celsius") : (abbreviated ? "\u00b0F" : "Fahrenheit");

    public string FormatTemperature(double celsius, int decimals = 1)
        => $"{ConvertTemperature(celsius).ToString($"F{decimals}")} {GetTemperatureUnit()}";

    #endregion
}
