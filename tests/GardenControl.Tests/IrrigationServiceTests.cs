using FluentAssertions;
using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Core.Models;
using GardenControl.Server.Hardware;
using GardenControl.Server.Services;
using GardenControl.Server.Services.Weather;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GardenControl.Tests;

/// <summary>
/// Tests der Bewässerungs-Entscheidungslogik (Schwellenwert, Wetter-Adaption, Modus, Cooldown).
/// Hardware (GPIO/Sensor), Datenbank und Wetter sind über Interfaces gemockt (NSubstitute).
/// </summary>
public class IrrigationServiceTests
{
    // Test-Zonen nutzen die Default-Kalibrierung Dry=26000 (0 %), Wet=12000 (100 %), linear.
    // moisture% = (26000 - raw) / 14000 * 100.
    private const int RawDryBelowThreshold = 22000;   // ≈ 28,6 % → unter Default-Schwelle 40
    private const int RawMoistAboveThreshold = 16000; // ≈ 71,4 % → über Default-Schwelle 40
    private const int RawModerate42Percent = 20100;   // ≈ 42,1 % (zwischen 40 und 45)
    private const int RawModerate37Percent = 20800;   // ≈ 37,1 % (zwischen 35 und 40)

    private static Zone MakeZone(int id = 1, int threshold = 40, int channel = 0, int relayPin = 17)
        => new()
        {
            Id = id,
            Name = $"Zone{id}",
            SensorChannel = channel,
            RelayGpioPin = relayPin,
            ThresholdPercent = threshold,
            WateringDurationSeconds = 30,
            CooldownSeconds = 300,
            IsEnabled = true,
            CalibrationDryValue = 26000,
            CalibrationWetValue = 12000
        };

    private sealed record Harness(
        IrrigationService Service,
        IGpioService Gpio,
        ISensorService Sensor,
        IDatabaseService Db,
        IWeatherService Weather);

    private static Harness CreateHarness(params Zone[] zones)
    {
        var logger = Substitute.For<ILogger<IrrigationService>>();
        var gpio = Substitute.For<IGpioService>();
        var sensor = Substitute.For<ISensorService>();
        var db = Substitute.For<IDatabaseService>();
        var weather = Substitute.For<IWeatherService>();

        db.GetZonesAsync().Returns(zones.ToList());
        db.GetConfigAsync(Arg.Any<string>()).Returns((string?)null);

        return new Harness(new IrrigationService(logger, gpio, sensor, db, weather), gpio, sensor, db, weather);
    }

    private static async Task<Harness> CreateInitializedAsync(params Zone[] zones)
    {
        var h = CreateHarness(zones);
        await h.Service.InitializeAsync();
        return h;
    }

    // ── Automatik: Schwellenwert ────────────────────────────────────────────

    [Fact]
    public async Task CheckAndWater_StartsWatering_WhenMoistureBelowThreshold()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawDryBelowThreshold);
        await h.Service.SetModeAsync(SystemMode.Automatic);

        await h.Service.CheckAndWaterAsync();

        h.Gpio.Received().SetPin(zone.RelayGpioPin, true);
        h.Gpio.Received().SetPump(true);
    }

    [Fact]
    public async Task CheckAndWater_DoesNotWater_WhenMoistureAboveThreshold()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawMoistAboveThreshold);
        await h.Service.SetModeAsync(SystemMode.Automatic);

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(zone.RelayGpioPin, true);
    }

    [Fact]
    public async Task CheckAndWater_DoesNothing_InManualMode()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone); // Default-Modus = Manual (keine DB-Config)
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawDryBelowThreshold);

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(Arg.Any<int>(), true);
    }

    [Fact]
    public async Task CheckAndWater_IgnoresDisconnectedSensor()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(-1); // Sensor nicht erreichbar
        await h.Service.SetModeAsync(SystemMode.Automatic);

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(zone.RelayGpioPin, true);
    }

    [Fact]
    public async Task CheckAndWater_SkipsDisabledZone()
    {
        var zone = MakeZone();
        zone.IsEnabled = false;
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawDryBelowThreshold);
        await h.Service.SetModeAsync(SystemMode.Automatic);

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(zone.RelayGpioPin, true);
    }

    // ── Automatik: Wetter ───────────────────────────────────────────────────

    [Fact]
    public async Task CheckAndWater_SkipsWatering_WhenWeatherRecommendsSkip()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawDryBelowThreshold);
        h.Weather.GetCurrentWeatherAsync().Returns(new WeatherDto { ShouldSkipWatering = true, SkipReason = "Regen" });
        await h.Service.SetModeAsync(SystemMode.Automatic);
        await h.Service.GetStatusAsync(); // setzt _lastWeather (sonst null)

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(zone.RelayGpioPin, true);
    }

    [Fact]
    public async Task CheckAndWater_HotWeather_WatersAtModerateMoisture()
    {
        // 42 % liegt über der Default-Schwelle 40 (normal kein Watering).
        // Temp > 30 °C hebt die Schwelle auf 45 → 42 < 45 → bewässern.
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawModerate42Percent);
        h.Weather.GetCurrentWeatherAsync().Returns(new WeatherDto { TemperatureCelsius = 35 });
        await h.Service.SetModeAsync(SystemMode.Automatic);
        await h.Service.GetStatusAsync();

        await h.Service.CheckAndWaterAsync();

        h.Gpio.Received().SetPin(zone.RelayGpioPin, true);
    }

    [Fact]
    public async Task CheckAndWater_HumidWeather_SkipsAtModerateMoisture()
    {
        // 37 % liegt unter der Default-Schwelle 40 (normal Watering).
        // Luftfeuchte > 80 % senkt die Schwelle auf 35 → 37 >= 35 → nicht bewässern.
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawModerate37Percent);
        h.Weather.GetCurrentWeatherAsync().Returns(new WeatherDto { HumidityPercent = 85 });
        await h.Service.SetModeAsync(SystemMode.Automatic);
        await h.Service.GetStatusAsync();

        await h.Service.CheckAndWaterAsync();

        h.Gpio.DidNotReceive().SetPin(zone.RelayGpioPin, true);
    }

    // ── Manuelle Steuerung + Notstopp ───────────────────────────────────────

    [Fact]
    public async Task StartWatering_OpensValveAndPump_AndPersistsEvent()
    {
        var zone = MakeZone();
        var h = await CreateInitializedAsync(zone);
        h.Sensor.ReadRawValue(zone.SensorChannel).Returns(RawDryBelowThreshold);

        var result = await h.Service.StartWateringAsync(zone.Id, durationSeconds: 3600);

        result.Should().BeTrue();
        h.Gpio.Received().SetPin(zone.RelayGpioPin, true);
        h.Gpio.Received().SetPump(true);
        await h.Db.Received().SaveEventAsync(Arg.Is<IrrigationEvent>(e => e.ZoneId == zone.Id));
    }

    [Fact]
    public async Task StartWatering_ReturnsFalse_ForUnknownZone()
    {
        var h = await CreateInitializedAsync(MakeZone());

        var result = await h.Service.StartWateringAsync(zoneId: 999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetMode_Off_TriggersEmergencyStop()
    {
        var h = await CreateInitializedAsync(MakeZone());

        await h.Service.SetModeAsync(SystemMode.Off);

        h.Gpio.Received().AllOff();
        h.Service.CurrentMode.Should().Be(SystemMode.Off);
    }

    [Fact]
    public async Task EmergencyStop_TurnsAllOff()
    {
        var h = await CreateInitializedAsync(MakeZone());

        await h.Service.EmergencyStopAsync();

        h.Gpio.Received().AllOff();
    }
}
