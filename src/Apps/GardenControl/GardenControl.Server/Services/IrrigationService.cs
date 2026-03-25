using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Core.Models;
using GardenControl.Server.Hardware;
using GardenControl.Server.Services.Weather;

namespace GardenControl.Server.Services;

/// <summary>
/// Zentrale Bewässerungssteuerung.
///
/// Ablauf bei automatischer Bewässerung:
/// 1. SensorPollingWorker ruft PollSensorsAsync() alle X Sekunden auf
/// 2. Sensorwerte werden gelesen und in DB gespeichert
/// 3. CheckAndWaterAsync() prüft ob Feuchtigkeit unter Schwellenwert
/// 4. Wenn ja: Ventil öffnen → Pumpe ein → Timer starten
/// 5. Nach Ablauf: Ventil schließen → wenn kein Ventil mehr offen: Pumpe aus
/// 6. Cooldown-Zeit abwarten bevor Zone erneut bewässert wird
/// </summary>
public class IrrigationService : IIrrigationService
{
    private readonly ILogger<IrrigationService> _logger;
    private readonly IGpioService _gpio;
    private readonly ISensorService _sensor;
    private readonly IDatabaseService _db;
    private readonly IWeatherService _weather;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly DateTime _startTime = DateTime.UtcNow;
    private List<Zone> _zones = [];
    private readonly Dictionary<int, ZoneRuntime> _runtimes = [];
    private WeatherDto? _lastWeather;

    public SystemMode CurrentMode { get; private set; } = SystemMode.Manual;

    public IrrigationService(
        ILogger<IrrigationService> logger,
        IGpioService gpio,
        ISensorService sensor,
        IDatabaseService db,
        IWeatherService weather)
    {
        _logger = logger;
        _gpio = gpio;
        _sensor = sensor;
        _db = db;
        _weather = weather;
    }

    public async Task InitializeAsync()
    {
        // Zonen aus DB laden
        _zones = await _db.GetZonesAsync();

        // Betriebsmodus laden
        var modeStr = await _db.GetConfigAsync(ConfigKeys.SystemMode);
        if (Enum.TryParse<SystemMode>(modeStr, out var mode))
            CurrentMode = mode;

        // Pumpen-Pin laden
        var pumpPinStr = await _db.GetConfigAsync(ConfigKeys.PumpGpioPin);
        var pumpPin = int.TryParse(pumpPinStr, out var p) ? p : 23;

        // GPIO initialisieren
        _gpio.Initialize(pumpPin, _zones.Select(z => z.RelayGpioPin));
        _sensor.Initialize();

        // Runtime-Tracking pro Zone
        foreach (var zone in _zones)
        {
            _runtimes[zone.Id] = new ZoneRuntime();
        }

        _logger.LogInformation("Bewässerung initialisiert: {Count} Zonen, Modus={Mode}",
            _zones.Count, CurrentMode);
    }

    public async Task<bool> StartWateringAsync(int zoneId, int? durationSeconds = null, IrrigationTrigger trigger = IrrigationTrigger.Manual)
    {
        await _semaphore.WaitAsync();
        try
        {
            var zone = _zones.FirstOrDefault(z => z.Id == zoneId);
            if (zone == null) return false;

            var runtime = _runtimes[zoneId];
            if (runtime.State == ZoneState.Watering) return false; // Schon aktiv

            // Cooldown prüfen
            if (runtime.State == ZoneState.Cooldown)
            {
                _logger.LogInformation("Zone {Name} noch in Abkühlphase", zone.Name);
                return false;
            }

            var duration = durationSeconds ?? zone.WateringDurationSeconds;

            // Ventil öffnen
            _gpio.SetPin(zone.RelayGpioPin, true);
            // Pumpe einschalten (falls noch nicht an)
            _gpio.SetPump(true);

            // Runtime aktualisieren
            runtime.State = ZoneState.Watering;
            runtime.WateringStartedUtc = DateTime.UtcNow;
            runtime.PlannedDurationSeconds = duration;

            // Bewässerungsereignis in DB
            var currentMoisture = ReadZoneMoisture(zone);
            var evt = new IrrigationEvent
            {
                ZoneId = zoneId,
                StartedAtUtc = DateTime.UtcNow,
                PlannedDurationSeconds = duration,
                MoistureAtStart = currentMoisture,
                Trigger = trigger
            };
            await _db.SaveEventAsync(evt);
            runtime.ActiveEventId = evt.Id;

            // Timer für automatisches Stoppen
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(duration));
                    await StopWateringAsync(zoneId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler im Bewässerungstimer für Zone {ZoneId}", zoneId);
                }
            });

            _logger.LogInformation("Bewässerung gestartet: Zone={Name}, Dauer={Duration}s, Auslöser={Trigger}",
                zone.Name, duration, trigger);

            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopWateringAsync(int zoneId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var zone = _zones.FirstOrDefault(z => z.Id == zoneId);
            if (zone == null) return;

            var runtime = _runtimes[zoneId];
            if (runtime.State != ZoneState.Watering) return;

            // Ventil schließen
            _gpio.SetPin(zone.RelayGpioPin, false);

            // Bewässerungsereignis abschließen - aktives Event per ID laden
            if (runtime.ActiveEventId > 0)
            {
                var evt = await _db.GetActiveEventAsync(zoneId);
                if (evt != null)
                {
                    evt.EndedAtUtc = DateTime.UtcNow;
                    evt.ActualDurationSeconds = (int)(DateTime.UtcNow - evt.StartedAtUtc).TotalSeconds;
                    evt.MoistureAtEnd = ReadZoneMoisture(zone);
                    await _db.SaveEventAsync(evt);
                }
            }

            // Cooldown starten
            runtime.State = ZoneState.Cooldown;
            runtime.CooldownEndsUtc = DateTime.UtcNow.AddSeconds(zone.CooldownSeconds);
            runtime.ActiveEventId = 0;

            // Pumpe ausschalten wenn kein Ventil mehr offen
            if (!_runtimes.Values.Any(r => r.State == ZoneState.Watering))
            {
                _gpio.SetPump(false);
            }

            // Cooldown-Timer - State-Aenderung unter Semaphore
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(zone.CooldownSeconds));
                    await _semaphore.WaitAsync();
                    try
                    {
                        if (runtime.State == ZoneState.Cooldown)
                            runtime.State = ZoneState.Idle;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler im Cooldown-Timer für Zone {Name}", zone.Name);
                }
            });

            _logger.LogInformation("Bewässerung gestoppt: Zone={Name}", zone.Name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task EmergencyStopAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _gpio.AllOff();

            // Offene Bewässerungsereignisse in der DB abschließen
            foreach (var (zoneId, rt) in _runtimes)
            {
                if (rt.State == ZoneState.Watering && rt.ActiveEventId > 0)
                {
                    var evt = await _db.GetActiveEventAsync(zoneId);
                    if (evt != null)
                    {
                        evt.EndedAtUtc = DateTime.UtcNow;
                        evt.ActualDurationSeconds = (int)(DateTime.UtcNow - evt.StartedAtUtc).TotalSeconds;
                        await _db.SaveEventAsync(evt);
                    }
                }
                rt.State = ZoneState.Idle;
                rt.ActiveEventId = 0;
            }
            _logger.LogWarning("NOTSTOPP - alle Ventile und Pumpe ausgeschaltet");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SystemStatusDto> GetStatusAsync()
    {
        // Wetterdaten aktualisieren (gecacht, max. alle 30 Min.)
        // Exception darf GetStatusAsync nicht blockieren
        try
        {
            _lastWeather = await _weather.GetCurrentWeatherAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wetterdaten konnten nicht aktualisiert werden");
        }

        await _semaphore.WaitAsync();
        try
        {
        var status = new SystemStatusDto
        {
            Mode = CurrentMode,
            PumpActive = _gpio.IsPumpActive,
            ServerTimeUtc = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - _startTime,
            Weather = _lastWeather,
            WeatherPaused = _lastWeather?.ShouldSkipWatering ?? false,
            WeatherPauseReason = _lastWeather?.SkipReason
        };

        foreach (var zone in _zones)
        {
            var runtime = _runtimes.GetValueOrDefault(zone.Id);
            var rawValue = _sensor.ReadRawValue(zone.SensorChannel);
            // Bei disconnected Sensor (-1) nicht CalculateMoisturePercent aufrufen
            // (wuerde falschen Wert liefern und den Disconnect maskieren)
            var moisture = rawValue >= 0 ? zone.CalculateMoisturePercent(rawValue) : 0;

            status.Zones.Add(new ZoneStatusDto
            {
                ZoneId = zone.Id,
                Name = zone.Name,
                State = runtime?.State ?? ZoneState.Idle,
                MoisturePercent = rawValue >= 0 ? Math.Round(moisture, 1) : 0,
                RawAdcValue = rawValue,
                ThresholdPercent = zone.ThresholdPercent,
                SensorStatus = rawValue < 0 ? SensorStatus.Disconnected : SensorStatus.Ok,
                IsEnabled = zone.IsEnabled,
                LastWateredUtc = runtime?.WateringStartedUtc,
                RemainingWateringSeconds = runtime?.State == ZoneState.Watering
                    ? Math.Max(0, runtime.PlannedDurationSeconds -
                        (int)(DateTime.UtcNow - runtime.WateringStartedUtc!.Value).TotalSeconds)
                    : null
            });
        }

        return status;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SensorDataDto> PollSensorsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var data = new SensorDataDto { TimestampUtc = DateTime.UtcNow };

            foreach (var zone in _zones.Where(z => z.IsEnabled))
            {
                var rawValue = _sensor.ReadRawValue(zone.SensorChannel);

                // Disconnected Sensor: Nicht in DB speichern (verfaelscht Verlauf)
                if (rawValue < 0)
                {
                    data.Values.Add(new SensorValueDto
                    {
                        ZoneId = zone.Id, RawValue = rawValue, MoisturePercent = 0
                    });
                    continue;
                }

                var moisture = zone.CalculateMoisturePercent(rawValue);

                // In DB speichern (nur gueltige Werte)
                var reading = new SensorReading
                {
                    ZoneId = zone.Id,
                    TimestampUtc = DateTime.UtcNow,
                    RawValue = rawValue,
                    MoisturePercent = Math.Round(moisture, 1),
                    WasWatering = _runtimes.GetValueOrDefault(zone.Id)?.State == ZoneState.Watering
                };
                await _db.SaveReadingAsync(reading);

                data.Values.Add(new SensorValueDto
                {
                    ZoneId = zone.Id,
                    RawValue = rawValue,
                    MoisturePercent = Math.Round(moisture, 1)
                });
            }

            return data;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CheckAndWaterAsync()
    {
        if (CurrentMode != SystemMode.Automatic) return;

        // Wetter-Check: Bei Regen oder hoher Luftfeuchtigkeit nicht bewässern
        if (_lastWeather?.ShouldSkipWatering == true)
        {
            _logger.LogInformation("Automatik pausiert: {Reason}", _lastWeather.SkipReason);
            return;
        }

        // Zu bewässernde Zonen unter Semaphore ermitteln, dann ohne Semaphore starten
        // (StartWateringAsync holt eigene Semaphore)
        var zonesToWater = new List<int>();

        await _semaphore.WaitAsync();
        try
        {
            foreach (var zone in _zones.Where(z => z.IsEnabled))
            {
                var runtime = _runtimes.GetValueOrDefault(zone.Id);
                if (runtime == null || runtime.State != ZoneState.Idle) continue;

                var rawValue = _sensor.ReadRawValue(zone.SensorChannel);
                if (rawValue < 0) continue; // Sensor nicht erreichbar

                var moisture = zone.CalculateMoisturePercent(rawValue);

                // Wetter-adaptiver Schwellenwert:
                // Bei hoher Temperatur (>30°C) → Schwellenwert +5% (früher bewässern)
                // Bei hoher Luftfeuchtigkeit (>80%) → Schwellenwert -5% (später bewässern)
                var adjustedThreshold = zone.ThresholdPercent;
                if (_lastWeather != null)
                {
                    if (_lastWeather.TemperatureCelsius > 30)
                        adjustedThreshold += 5;
                    if (_lastWeather.HumidityPercent > 80)
                        adjustedThreshold -= 5;
                }

                if (moisture < adjustedThreshold)
                {
                    _logger.LogInformation(
                        "Zone {Name}: Feuchtigkeit {Moisture:F0}% unter Schwellenwert {Threshold}% " +
                        "(angepasst: {Adjusted}%, Temp: {Temp}°C) → Bewässerung starten",
                        zone.Name, moisture, zone.ThresholdPercent, adjustedThreshold,
                        _lastWeather?.TemperatureCelsius ?? 0);

                    zonesToWater.Add(zone.Id);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }

        // Bewässerung starten - außerhalb der Semaphore (StartWateringAsync holt eigene)
        foreach (var zoneId in zonesToWater)
        {
            await StartWateringAsync(zoneId, trigger: IrrigationTrigger.Automatic);
        }
    }

    public async Task SetModeAsync(SystemMode mode)
    {
        CurrentMode = mode;
        await _db.SetConfigAsync(ConfigKeys.SystemMode, mode.ToString());

        if (mode == SystemMode.Off)
            await EmergencyStopAsync();

        _logger.LogInformation("Betriebsmodus geändert: {Mode}", mode);
    }

    /// <summary>Liest aktuelle Feuchtigkeit einer Zone</summary>
    private double ReadZoneMoisture(Zone zone)
    {
        var raw = _sensor.ReadRawValue(zone.SensorChannel);
        return raw >= 0 ? zone.CalculateMoisturePercent(raw) : 0;
    }

    /// <summary>Laufzeit-Tracking pro Zone (nicht persistiert)</summary>
    private class ZoneRuntime
    {
        public ZoneState State { get; set; } = ZoneState.Idle;
        public DateTime? WateringStartedUtc { get; set; }
        public int PlannedDurationSeconds { get; set; }
        public DateTime? CooldownEndsUtc { get; set; }
        public int ActiveEventId { get; set; }
    }
}
