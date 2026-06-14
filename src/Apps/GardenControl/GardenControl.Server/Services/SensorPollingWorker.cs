using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Core.Models;
using GardenControl.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GardenControl.Server.Services;

/// <summary>
/// Background-Service der periodisch:
/// 1. Sensoren liest und in DB speichert
/// 2. Werte über SignalR an Clients pusht
/// 3. Schwellenwerte prüft (Automatik-Modus)
/// 4. Zeitpläne ausführt (z.B. "Mo-Fr 7:00 Uhr")
/// 5. Alte Verlaufsdaten bereinigt (täglich)
/// 6. Wetterdaten in einem eigenen langsamen Loop aktualisiert (max. alle 30 Min.)
///
/// Performance-Hinweise (Pi, 24/7):
/// - Sensoren werden pro Zyklus nur EINMAL gelesen (PollSensorsAsync), der Snapshot wird an
///   CheckAndWaterAsync + GetStatusAsync durchgereicht → reduziert die I2C-Last.
/// - SignalR-Broadcasts laufen nur bei einer relevanten Änderung oder bei verbundenen Clients
///   (Heartbeat) → spart Serialisierungs-/Netzaufwand, wenn niemand zuhört oder sich nichts ändert.
/// - Das Abfrageintervall + die Retention kommen aus dem PollConfigCache statt pro Tick aus SQLite.
///
/// Die Bewässerungslogik selbst (Reihenfolge Poll → CheckAndWater → Zeitplan, Schwellwerte,
/// Cooldowns, Sicherheits-Abschaltungen) bleibt unverändert — optimiert wird nur drumherum.
/// </summary>
public class SensorPollingWorker : BackgroundService
{
    private readonly ILogger<SensorPollingWorker> _logger;
    private readonly IIrrigationService _irrigation;
    private readonly IDatabaseService _db;
    private readonly IHubContext<GardenHub> _hubContext;
    private readonly PollConfigCache _pollConfig;

    private DateTime _lastCleanup = DateTime.UtcNow;
    private DateTime _lastScheduleCheck = DateTime.UtcNow;
    private DateTime _lastWeatherRefresh = DateTime.MinValue;

    // Diff-Zustand: zuletzt an Clients gesendete Werte, um redundante Broadcasts zu vermeiden.
    private SensorDataDto? _lastSentSensorData;
    private SystemStatusDto? _lastSentStatus;
    private DateTime _lastStatusBroadcast = DateTime.MinValue;

    // --- Schwellwerte/Intervalle für die Broadcast-Entscheidung ---

    /// <summary>Wetter-Aktualisierung höchstens alle 30 Min. (deckt den WeatherService-Cache).</summary>
    private static readonly TimeSpan WeatherRefreshInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Solange Clients verbunden sind, mindestens alle 60s einen Status pushen (Heartbeat),
    /// damit Uptime/Serverzeit in der Client-UI weiterlaufen, auch wenn sich sonst nichts ändert.
    /// </summary>
    private static readonly TimeSpan StatusHeartbeatInterval = TimeSpan.FromSeconds(60);

    /// <summary>ADC-Rohwert-Änderung ab der ein SensorData-Broadcast erfolgt (~1 Prozentpunkt Feuchte).</summary>
    private const int RawValueBroadcastThreshold = 150;

    /// <summary>Feuchte-Änderung in Prozentpunkten ab der ein Status-Broadcast erfolgt.</summary>
    private const double MoistureBroadcastThreshold = 1.0;

    public SensorPollingWorker(
        ILogger<SensorPollingWorker> logger,
        IIrrigationService irrigation,
        IDatabaseService db,
        IHubContext<GardenHub> hubContext,
        PollConfigCache pollConfig)
    {
        _logger = logger;
        _irrigation = irrigation;
        _db = db;
        _hubContext = hubContext;
        _pollConfig = pollConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor-Polling gestartet");

        await _db.InitializeAsync();
        await _irrigation.InitializeAsync();

        // Config-Cache einmalig aus der DB befüllen (danach nur noch über PUT /api/config aktualisiert).
        var startIntervalStr = await _db.GetConfigAsync(ConfigKeys.PollIntervalSeconds);
        if (int.TryParse(startIntervalStr, out var startInterval))
            _pollConfig.PollIntervalSeconds = startInterval;
        var startRetentionStr = await _db.GetConfigAsync(ConfigKeys.HistoryRetentionDays);
        if (int.TryParse(startRetentionStr, out var startRetention))
            _pollConfig.HistoryRetentionDays = startRetention;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wetter in einem eigenen langsamen Takt aktualisieren (kein HTTP im Status-Hot-Path).
                // Vor dem ersten CheckAndWaterAsync ausgeführt, damit _lastWeather für die Automatik
                // bereitsteht (Skip-Check + wetter-adaptiver Schwellenwert nutzen denselben Wert).
                if (DateTime.UtcNow - _lastWeatherRefresh >= WeatherRefreshInterval)
                {
                    await _irrigation.RefreshWeatherAsync();
                    _lastWeatherRefresh = DateTime.UtcNow;
                }

                // Sensoren EINMAL lesen + speichern; Snapshot für den restlichen Zyklus wiederverwenden.
                var (sensorData, snapshot) = await _irrigation.PollSensorsAsync();
                await BroadcastSensorDataIfChangedAsync(sensorData, stoppingToken);

                // Schwellenwert-basierte Automatik (gleiche Logik, nur auf dem Zyklus-Snapshot).
                await _irrigation.CheckAndWaterAsync(snapshot);

                // Zeitplan-basierte Bewässerung (jede Minute prüfen)
                if ((DateTime.UtcNow - _lastScheduleCheck).TotalSeconds >= 55)
                {
                    await CheckSchedulesAsync();
                    _lastScheduleCheck = DateTime.UtcNow;
                }

                // Status an Clients pushen (nur bei relevanter Änderung oder als Heartbeat).
                var status = await _irrigation.GetStatusAsync(snapshot);
                await BroadcastStatusIfChangedAsync(status, stoppingToken);

                // Tägliche Bereinigung
                if ((DateTime.UtcNow - _lastCleanup).TotalHours >= 24)
                {
                    await _db.CleanupOldDataAsync(_pollConfig.HistoryRetentionDays);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Sensor-Polling");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollConfig.PollIntervalSeconds), stoppingToken);
        }

        await _irrigation.EmergencyStopAsync();
        _logger.LogInformation("Sensor-Polling beendet, alle Ausgänge aus");
    }

    /// <summary>
    /// Sendet SensorData nur, wenn ein Client verbunden ist UND sich ein Rohwert spürbar
    /// geändert hat, ein Connect/Disconnect-Wechsel vorliegt oder sich die Zonen-Menge ändert.
    /// </summary>
    private async Task BroadcastSensorDataIfChangedAsync(SensorDataDto data, CancellationToken ct)
    {
        if (GardenHub.ConnectionCount == 0)
        {
            // Niemand verbunden — Snapshot trotzdem merken, damit nach einem Reconnect der erste
            // echte Vergleich gegen die zuletzt tatsächlich gemessenen Werte läuft.
            _lastSentSensorData = data;
            return;
        }

        if (SensorDataChanged(data))
        {
            await _hubContext.Clients.All.SendAsync("SensorData", data, ct);
            _lastSentSensorData = data;
        }
    }

    /// <summary>
    /// Sendet SystemStatus nur, wenn ein Client verbunden ist UND sich ein relevanter Wert
    /// geändert hat — oder als Heartbeat (mind. alle 60s), damit Uptime/Serverzeit weiterlaufen.
    /// </summary>
    private async Task BroadcastStatusIfChangedAsync(SystemStatusDto status, CancellationToken ct)
    {
        if (GardenHub.ConnectionCount == 0)
        {
            _lastSentStatus = status;
            return;
        }

        var changed = StatusChanged(status);
        var heartbeatDue = DateTime.UtcNow - _lastStatusBroadcast >= StatusHeartbeatInterval;

        if (changed || heartbeatDue)
        {
            await _hubContext.Clients.All.SendAsync("SystemStatus", status, ct);
            _lastSentStatus = status;
            _lastStatusBroadcast = DateTime.UtcNow;
        }
    }

    /// <summary>Erkennt eine relevante Änderung der Sensorwerte gegenüber dem zuletzt gesendeten Stand.</summary>
    private bool SensorDataChanged(SensorDataDto data)
    {
        var previous = _lastSentSensorData;
        if (previous == null) return true; // Erster Versand nach Start/Reconnect

        if (previous.Values.Count != data.Values.Count) return true;

        foreach (var current in data.Values)
        {
            var old = previous.Values.FirstOrDefault(v => v.ZoneId == current.ZoneId);
            if (old == null) return true; // Zone neu hinzugekommen

            // Connect/Disconnect-Wechsel (Vorzeichen des Rohwerts) ist immer relevant.
            var oldConnected = old.RawValue >= 0;
            var newConnected = current.RawValue >= 0;
            if (oldConnected != newConnected) return true;

            // Spürbare Änderung des Rohwerts (nur bei verbundenem Sensor sinnvoll).
            if (newConnected && Math.Abs(current.RawValue - old.RawValue) >= RawValueBroadcastThreshold)
                return true;
        }

        return false;
    }

    /// <summary>Erkennt eine relevante Zustands-/Wertänderung des Systemstatus gegenüber dem letzten Versand.</summary>
    private bool StatusChanged(SystemStatusDto status)
    {
        var previous = _lastSentStatus;
        if (previous == null) return true;

        // System-weite Zustände
        if (previous.Mode != status.Mode) return true;
        if (previous.PumpActive != status.PumpActive) return true;
        if (previous.WeatherPaused != status.WeatherPaused) return true;
        if (previous.WeatherPauseReason != status.WeatherPauseReason) return true;

        if (previous.Zones.Count != status.Zones.Count) return true;

        foreach (var current in status.Zones)
        {
            var old = previous.Zones.FirstOrDefault(z => z.ZoneId == current.ZoneId);
            if (old == null) return true; // Zone neu

            // Zustandswechsel pro Zone (Watering/Cooldown/Idle, Sensor-Status, Aktivierung).
            if (old.State != current.State) return true;
            if (old.SensorStatus != current.SensorStatus) return true;
            if (old.IsEnabled != current.IsEnabled) return true;
            if (old.ThresholdPercent != current.ThresholdPercent) return true;

            // Restlaufzeit-Wechsel von/zu "nicht aktiv" (z.B. Bewässerung gerade beendet).
            if (old.RemainingWateringSeconds.HasValue != current.RemainingWateringSeconds.HasValue) return true;

            // Spürbare Feuchte-Änderung
            if (Math.Abs(old.MoisturePercent - current.MoisturePercent) >= MoistureBroadcastThreshold) return true;
        }

        return false;
    }

    /// <summary>
    /// Prüft ob Zeitpläne fällig sind und startet Bewässerung.
    /// Zeitpläne sind unabhängig vom Automatik-Modus (funktionieren auch bei "Manual").
    /// Wetter-Skip wird berücksichtigt.
    /// </summary>
    private async Task CheckSchedulesAsync()
    {
        var schedules = await _db.GetSchedulesAsync();
        var now = DateTime.Now; // Lokale Zeit für Zeitpläne
        var today = now.DayOfWeek;

        foreach (var schedule in schedules.Where(s => s.IsEnabled))
        {
            // Richtiger Wochentag?
            if (!schedule.IsActiveOnDay(today)) continue;

            // Richtige Uhrzeit? (±2 Minuten Toleranz)
            var scheduledTime = new TimeSpan(schedule.Hour, schedule.Minute, 0);
            var diff = (now.TimeOfDay - scheduledTime).TotalMinutes;
            if (diff < 0 || diff > 2) continue;

            // Heute schon ausgeführt? (verhindert Doppelausführung)
            if (schedule.LastExecutedUtc.HasValue &&
                schedule.LastExecutedUtc.Value.Date == DateTime.UtcNow.Date)
                continue;

            _logger.LogInformation(
                "Zeitplan ausführen: Zone {ZoneId}, {Hour}:{Minute:D2}, {Duration}s",
                schedule.ZoneId, schedule.Hour, schedule.Minute, schedule.DurationSeconds);

            var success = await _irrigation.StartWateringAsync(
                schedule.ZoneId, schedule.DurationSeconds, IrrigationTrigger.Scheduled);

            if (success)
            {
                schedule.LastExecutedUtc = DateTime.UtcNow;
                await _db.SaveScheduleAsync(schedule);
            }
        }
    }
}
