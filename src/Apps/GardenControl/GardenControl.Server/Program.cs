using GardenControl.Core.DTOs;
using GardenControl.Core.Enums;
using GardenControl.Server.Hardware;
using GardenControl.Server.Hubs;
using GardenControl.Server.Services;
using GardenControl.Server.Services.Weather;

var builder = WebApplication.CreateBuilder(args);

// --- Services registrieren ---

// Hardware: Echte GPIO/Sensoren auf dem Pi, Mock auf Desktop
if (OperatingSystem.IsLinux() && Directory.Exists("/sys/class/gpio"))
{
    // Raspberry Pi erkannt
    builder.Services.AddSingleton<IGpioService, GpioService>();
    builder.Services.AddSingleton<ISensorService, SensorService>();
}
else
{
    // Desktop/Entwicklung: Mock-Hardware
    builder.Services.AddSingleton<IGpioService, MockGpioService>();
    builder.Services.AddSingleton<ISensorService, MockSensorService>();
}

// Datenbank + Bewässerungslogik + Wetter
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IWeatherService, WeatherService>();
builder.Services.AddSingleton<IIrrigationService, IrrigationService>();

// Background-Service für Sensor-Polling
builder.Services.AddHostedService<SensorPollingWorker>();

// SignalR für Echtzeit-Updates
builder.Services.AddSignalR();

// CORS: Lokales Netzwerk erlauben
// SignalR braucht AllowCredentials(), das ist inkompatibel mit AllowAnyOrigin().
// Stattdessen SetIsOriginAllowed verwenden.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Alle Origins erlauben (lokales Netzwerk)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// --- SignalR Hub ---
app.MapHub<GardenHub>("/hub/garden");

// --- REST API (Minimal API) ---

// Status
app.MapGet("/api/status", async (IIrrigationService irrigation) =>
    Results.Ok(await irrigation.GetStatusAsync()));

// Zonen
app.MapGet("/api/zones", async (IDatabaseService db) =>
    Results.Ok(await db.GetZonesAsync()));

app.MapPut("/api/zones/{id}", async (int id, ZoneConfigDto config, IDatabaseService db) =>
{
    var zone = await db.GetZoneAsync(id);
    if (zone == null) return Results.NotFound();

    // Eingabevalidierung: Sinnvolle Grenzwerte erzwingen
    if (config.ThresholdPercent.HasValue && (config.ThresholdPercent < 5 || config.ThresholdPercent > 95))
        return Results.BadRequest("Schwellenwert muss zwischen 5 und 95 liegen");
    if (config.WateringDurationSeconds.HasValue && (config.WateringDurationSeconds < 5 || config.WateringDurationSeconds > 3600))
        return Results.BadRequest("Bewässerungsdauer muss zwischen 5 und 3600 Sekunden liegen");
    if (config.CooldownSeconds.HasValue && (config.CooldownSeconds < 60 || config.CooldownSeconds > 7200))
        return Results.BadRequest("Abkühlzeit muss zwischen 60 und 7200 Sekunden liegen");

    if (config.Name != null) zone.Name = config.Name;
    if (config.ThresholdPercent.HasValue) zone.ThresholdPercent = config.ThresholdPercent.Value;
    if (config.WateringDurationSeconds.HasValue) zone.WateringDurationSeconds = config.WateringDurationSeconds.Value;
    if (config.CooldownSeconds.HasValue) zone.CooldownSeconds = config.CooldownSeconds.Value;
    if (config.IsEnabled.HasValue) zone.IsEnabled = config.IsEnabled.Value;

    await db.SaveZoneAsync(zone);
    return Results.Ok(zone);
});

// Bewässerung steuern
app.MapPost("/api/zones/{id}/water", async (int id, ZoneCommandDto cmd, IIrrigationService irrigation) =>
{
    return cmd.Action.ToLower() switch
    {
        "start" => await irrigation.StartWateringAsync(id, cmd.DurationSeconds)
            ? Results.Ok() : Results.Conflict("Zone bereits aktiv oder in Abkühlphase"),
        "stop" => Results.Ok(await irrigation.StopWateringAsync(id).ContinueWith(_ => true)),
        _ => Results.BadRequest("Unbekannte Aktion. Erlaubt: start, stop")
    };
});

// Notstopp
app.MapPost("/api/emergency-stop", async (IIrrigationService irrigation) =>
{
    await irrigation.EmergencyStopAsync();
    return Results.Ok();
});

// Betriebsmodus
app.MapPost("/api/mode/{mode}", async (string mode, IIrrigationService irrigation) =>
{
    if (!Enum.TryParse<SystemMode>(mode, true, out var m))
        return Results.BadRequest("Ungültiger Modus. Erlaubt: Off, Manual, Automatic");

    await irrigation.SetModeAsync(m);
    return Results.Ok();
});

// Kalibrierung
app.MapPost("/api/zones/{id}/calibrate/{type}", async (int id, string type, IDatabaseService db, ISensorService sensor) =>
{
    var zone = await db.GetZoneAsync(id);
    if (zone == null) return Results.NotFound();

    var rawValue = sensor.ReadRawValue(zone.SensorChannel);
    if (rawValue < 0) return Results.Problem("Sensor nicht erreichbar");

    switch (type.ToLower())
    {
        case "dry":
            zone.CalibrationDryValue = rawValue;
            break;
        case "wet":
            zone.CalibrationWetValue = rawValue;
            break;
        default:
            return Results.BadRequest("Typ muss 'dry' oder 'wet' sein");
    }

    zone.CalibratedAtUtc = DateTime.UtcNow;
    await db.SaveZoneAsync(zone);
    return Results.Ok(zone);
});

// Verlaufsdaten
app.MapGet("/api/history/readings", async (int? zoneId, DateTime? from, DateTime? to, int? limit, IDatabaseService db) =>
{
    var fromUtc = from ?? DateTime.UtcNow.AddHours(-24);
    var toUtc = to ?? DateTime.UtcNow;
    var maxResults = Math.Min(limit ?? 1000, 5000);
    return Results.Ok(await db.GetReadingsAsync(zoneId, fromUtc, toUtc, maxResults));
});

app.MapGet("/api/history/events", async (int? zoneId, DateTime? from, DateTime? to, int? limit, IDatabaseService db) =>
{
    var fromUtc = from ?? DateTime.UtcNow.AddDays(-7);
    var toUtc = to ?? DateTime.UtcNow;
    var maxResults = Math.Min(limit ?? 100, 500);
    return Results.Ok(await db.GetEventsAsync(zoneId, fromUtc, toUtc, maxResults));
});

// Konfiguration
app.MapGet("/api/config", async (IDatabaseService db) =>
    Results.Ok(await db.GetAllConfigAsync()));

app.MapPut("/api/config", async (SystemConfigDto config, IDatabaseService db, IIrrigationService irrigation) =>
{
    if (config.PollIntervalSeconds.HasValue)
        await db.SetConfigAsync("poll_interval_seconds", config.PollIntervalSeconds.Value.ToString());

    if (config.Mode != null && Enum.TryParse<SystemMode>(config.Mode, true, out var mode))
        await irrigation.SetModeAsync(mode);

    return Results.Ok(await db.GetAllConfigAsync());
});

// Zeitpläne
app.MapGet("/api/schedules", async (int? zoneId, IDatabaseService db) =>
    Results.Ok(await db.GetSchedulesAsync(zoneId)));

app.MapPost("/api/schedules", async (GardenControl.Core.Models.IrrigationSchedule schedule, IDatabaseService db) =>
{
    if (schedule.Hour < 0 || schedule.Hour > 23) return Results.BadRequest("Stunde muss 0-23 sein");
    if (schedule.Minute < 0 || schedule.Minute > 59) return Results.BadRequest("Minute muss 0-59 sein");
    if (schedule.DurationSeconds < 5 || schedule.DurationSeconds > 3600) return Results.BadRequest("Dauer muss 5-3600s sein");

    schedule.Id = 0;
    await db.SaveScheduleAsync(schedule);
    return Results.Created($"/api/schedules/{schedule.Id}", schedule);
});

app.MapPut("/api/schedules/{id}", async (int id, GardenControl.Core.Models.IrrigationSchedule update, IDatabaseService db) =>
{
    var schedule = await db.GetScheduleAsync(id);
    if (schedule == null) return Results.NotFound();

    schedule.IsEnabled = update.IsEnabled;
    schedule.Hour = Math.Clamp(update.Hour, 0, 23);
    schedule.Minute = Math.Clamp(update.Minute, 0, 59);
    schedule.DurationSeconds = Math.Clamp(update.DurationSeconds, 5, 3600);
    schedule.DayOfWeekFlags = update.DayOfWeekFlags;

    await db.SaveScheduleAsync(schedule);
    return Results.Ok(schedule);
});

app.MapDelete("/api/schedules/{id}", async (int id, IDatabaseService db) =>
{
    var schedule = await db.GetScheduleAsync(id);
    if (schedule == null) return Results.NotFound();
    await db.DeleteScheduleAsync(id);
    return Results.Ok();
});

// Wetterdaten
app.MapGet("/api/weather", async (IWeatherService weather) =>
{
    var data = await weather.GetCurrentWeatherAsync();
    return data != null ? Results.Ok(data) : Results.NotFound("Wetter nicht konfiguriert (API-Key fehlt)");
});

// Zone erstellen
app.MapPost("/api/zones", async (GardenControl.Core.Models.Zone zone, IDatabaseService db) =>
{
    zone.Id = 0; // Auto-Increment
    await db.SaveZoneAsync(zone);
    return Results.Created($"/api/zones/{zone.Id}", zone);
});

// Zone löschen
app.MapDelete("/api/zones/{id}", async (int id, IDatabaseService db, IIrrigationService irrigation) =>
{
    var zone = await db.GetZoneAsync(id);
    if (zone == null) return Results.NotFound();

    // Laufende Bewaesserung stoppen bevor Zone deaktiviert wird
    await irrigation.StopWateringAsync(id);

    zone.IsEnabled = false; // Soft-Delete (Daten bleiben erhalten)
    await db.SaveZoneAsync(zone);
    return Results.Ok();
});

// Statistiken
app.MapGet("/api/stats", async (IDatabaseService db) =>
{
    var now = DateTime.UtcNow;
    var today = now.Date;
    var week = now.AddDays(-7);

    var todayEvents = await db.GetEventsAsync(null, today, now, 500);
    var weekEvents = await db.GetEventsAsync(null, week, now, 500);

    return Results.Ok(new
    {
        today = new
        {
            wateringCount = todayEvents.Count,
            totalDurationSeconds = todayEvents.Sum(e => e.ActualDurationSeconds),
            avgMoistureAtStart = todayEvents.Count > 0 ? todayEvents.Average(e => e.MoistureAtStart) : 0
        },
        week = new
        {
            wateringCount = weekEvents.Count,
            totalDurationSeconds = weekEvents.Sum(e => e.ActualDurationSeconds),
            avgMoistureAtStart = weekEvents.Count > 0 ? weekEvents.Average(e => e.MoistureAtStart) : 0
        }
    });
});

// CSV-Export
app.MapGet("/api/export/csv", async (int? zoneId, DateTime? from, DateTime? to, IDatabaseService db) =>
{
    var fromUtc = from ?? DateTime.UtcNow.AddDays(-7);
    var toUtc = to ?? DateTime.UtcNow;
    var readings = await db.GetReadingsAsync(zoneId, fromUtc, toUtc, 10000);

    var csv = "Zeitpunkt;Zone;ADC-Rohwert;Feuchtigkeit%;Bewässerung\n" +
              string.Join("\n", readings.Select(r =>
                  $"{r.TimestampUtc:yyyy-MM-dd HH:mm:ss};{r.ZoneId};{r.RawValue};{r.MoisturePercent:F1};{(r.WasWatering ? "Ja" : "Nein")}"));

    return Results.Text(csv, "text/csv");
});

app.Logger.LogInformation("GardenControl Server gestartet auf {Urls}", string.Join(", ", app.Urls));

app.Run();
