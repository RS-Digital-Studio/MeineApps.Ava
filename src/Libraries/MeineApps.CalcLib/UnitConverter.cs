namespace MeineApps.CalcLib;

public enum UnitCategory
{
    Length,
    Weight,
    Temperature,
    Area,
    Volume,
    Speed,
    Time,
    Currency
}

public record UnitInfo(string Name, string Symbol, double ToBaseMultiplier, double? Offset = null);

public static class UnitConverter
{
    private static readonly Dictionary<UnitCategory, List<UnitInfo>> Units = new()
    {
        [UnitCategory.Length] = new()
        {
            new("Millimeter", "mm", 0.001),
            new("Centimeter", "cm", 0.01),
            new("Meter", "m", 1),
            new("Kilometer", "km", 1000),
            new("Inch", "in", 0.0254),
            new("Foot", "ft", 0.3048),
            new("Yard", "yd", 0.9144),
            new("Mile", "mi", 1609.34),
            new("Nautical Mile", "nmi", 1852)
        },
        [UnitCategory.Weight] = new()
        {
            new("Milligram", "mg", 0.000001),
            new("Gram", "g", 0.001),
            new("Kilogram", "kg", 1),
            new("Tonne", "t", 1000),
            new("Ounce", "oz", 0.0283495),
            new("Pound", "lb", 0.453592),
            new("Stone", "st", 6.35029)
        },
        [UnitCategory.Temperature] = new()
        {
            new("Celsius", "°C", 1, 0),
            new("Fahrenheit", "°F", 5.0/9.0, -32),
            new("Kelvin", "K", 1, -273.15)
        },
        [UnitCategory.Area] = new()
        {
            new("Square Millimeter", "mm²", 0.000001),
            new("Square Centimeter", "cm²", 0.0001),
            new("Square Meter", "m²", 1),
            new("Hectare", "ha", 10000),
            new("Square Kilometer", "km²", 1000000),
            new("Square Inch", "in²", 0.00064516),
            new("Square Foot", "ft²", 0.092903),
            new("Acre", "ac", 4046.86),
            new("Square Mile", "mi²", 2589988)
        },
        [UnitCategory.Volume] = new()
        {
            new("Milliliter", "ml", 0.001),
            new("Liter", "l", 1),
            new("Cubic Meter", "m³", 1000),
            new("Gallon (US)", "gal", 3.78541),
            new("Gallon (UK)", "gal (UK)", 4.54609),
            new("Fluid Ounce (US)", "fl oz", 0.0295735),
            new("Pint (US)", "pt", 0.473176),
            new("Quart (US)", "qt", 0.946353)
        },
        [UnitCategory.Speed] = new()
        {
            new("Meters per Second", "m/s", 1),
            new("Kilometers per Hour", "km/h", 1.0 / 3.6),
            new("Miles per Hour", "mph", 0.44704),
            new("Knots", "kn", 0.514444),
            new("Feet per Second", "ft/s", 0.3048)
        },
        [UnitCategory.Time] = new()
        {
            new("Milliseconds", "ms", 0.001),
            new("Seconds", "s", 1),
            new("Minutes", "min", 60),
            new("Hours", "h", 3600),
            new("Days", "d", 86400),
            new("Weeks", "wk", 604800),
            new("Years", "yr", 31536000)
        },
        [UnitCategory.Currency] = new()
        {
            // Hinweis: Diese Werte sind nur Platzhalter. Für echte Währungsumrechnung
            // sollte eine API verwendet werden (z.B. exchangerate-api.com)
            new("EUR", "€", 1),
            new("USD", "$", 1.09),
            new("GBP", "£", 0.86),
            new("JPY", "¥", 162),
            new("CHF", "CHF", 0.95),
            new("CAD", "CA$", 1.46),
            new("AUD", "A$", 1.66),
            new("CNY", "¥", 7.85)
        }
    };

    public static List<UnitInfo> GetUnitsForCategory(UnitCategory category)
    {
        return Units.TryGetValue(category, out var units) ? units : [];
    }

    public static double Convert(double value, UnitInfo fromUnit, UnitInfo toUnit)
    {
        // Spezialbehandlung für Temperatur (mit Offset)
        if (fromUnit.Offset.HasValue && toUnit.Offset.HasValue)
        {
            // Temperatur: Erst in Celsius umrechnen, dann in Zieleinheit
            // Formel: celsius = (value + Offset) * ToBaseMultiplier
            // z.B. Fahrenheit→Celsius: (100 + (-32)) * 5/9 = 37.78
            var celsius = (value + fromUnit.Offset.Value) * fromUnit.ToBaseMultiplier;
            return celsius / toUnit.ToBaseMultiplier - toUnit.Offset.Value;
        }

        // Normale Einheiten: Erst zu Basis, dann zu Ziel
        var baseValue = value * fromUnit.ToBaseMultiplier;
        return baseValue / toUnit.ToBaseMultiplier;
    }
}
