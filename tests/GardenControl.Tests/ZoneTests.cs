using FluentAssertions;
using GardenControl.Core.Models;
using Xunit;

namespace GardenControl.Tests;

/// <summary>
/// Tests der Feuchtigkeits-Umrechnung (kapazitiver Sensor: hoher ADC-Wert = trocken).
/// </summary>
public class ZoneTests
{
    [Theory]
    [InlineData(26000, 0)]    // Dry-Kalibrierung → 0 %
    [InlineData(12000, 100)]  // Wet-Kalibrierung → 100 %
    [InlineData(19000, 50)]   // genau Mitte → 50 %
    public void CalculateMoisturePercent_MapsLinearlyBetweenCalibrationPoints(int raw, double expected)
    {
        var zone = new Zone { CalibrationDryValue = 26000, CalibrationWetValue = 12000 };

        zone.CalculateMoisturePercent(raw).Should().BeApproximately(expected, 0.1);
    }

    [Theory]
    [InlineData(30000)] // trockener als Kalibrierung → Clamp auf 0
    [InlineData(5000)]  // nasser als Kalibrierung → Clamp auf 100
    public void CalculateMoisturePercent_ClampsToValidRange(int raw)
    {
        var zone = new Zone { CalibrationDryValue = 26000, CalibrationWetValue = 12000 };

        zone.CalculateMoisturePercent(raw).Should().BeInRange(0, 100);
    }

    [Fact]
    public void CalculateMoisturePercent_ReturnsZero_WhenCalibrationDegenerate()
    {
        // Dry == Wet würde eine Division durch Null verursachen → Service liefert 0.
        var zone = new Zone { CalibrationDryValue = 20000, CalibrationWetValue = 20000 };

        zone.CalculateMoisturePercent(15000).Should().Be(0);
    }
}
