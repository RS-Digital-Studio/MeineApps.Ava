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
/// </summary>
public class SensorPollingWorker : BackgroundService
{
    private readonly ILogger<SensorPollingWorker> _logger;
    private readonly IIrrigationService _irrigation;
    private readonly IDatabaseService _db;
    private readonly IHubContext<GardenHub> _hubContext;
    private DateTime _lastCleanup = DateTime.UtcNow;
    private DateTime _lastScheduleCheck = DateTime.UtcNow;

    public SensorPollingWorker(
        ILogger<SensorPollingWorker> logger,
        IIrrigationService irrigation,
        IDatabaseService db,
        IHubContext<GardenHub> hubContext)
    {
        _logger = logger;
        _irrigation = irrigation;
        _db = db;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sensor-Polling gestartet");

        await _db.InitializeAsync();
        await _irrigation.InitializeAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Sensoren lesen und speichern
                var sensorData = await _irrigation.PollSensorsAsync();
                await _hubContext.Clients.All.SendAsync("SensorData", sensorData, stoppingToken);

                // Schwellenwert-basierte Automatik
                await _irrigation.CheckAndWaterAsync();

                // Zeitplan-basierte Bewässerung (jede Minute prüfen)
                if ((DateTime.UtcNow - _lastScheduleCheck).TotalSeconds >= 55)
                {
                    await CheckSchedulesAsync();
                    _lastScheduleCheck = DateTime.UtcNow;
                }

                // Status an Clients pushen
                var status = await _irrigation.GetStatusAsync();
                await _hubContext.Clients.All.SendAsync("SystemStatus", status, stoppingToken);

                // Tägliche Bereinigung
                if ((DateTime.UtcNow - _lastCleanup).TotalHours >= 24)
                {
                    var retentionStr = await _db.GetConfigAsync(ConfigKeys.HistoryRetentionDays);
                    var retention = int.TryParse(retentionStr, out var r) ? r : 30;
                    await _db.CleanupOldDataAsync(retention);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Sensor-Polling");
            }

            var intervalStr = await _db.GetConfigAsync(ConfigKeys.PollIntervalSeconds);
            var interval = int.TryParse(intervalStr, out var i) ? i : 30;
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }

        await _irrigation.EmergencyStopAsync();
        _logger.LogInformation("Sensor-Polling beendet, alle Ausgänge aus");
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
