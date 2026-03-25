using System.Text.Json;
using GardenControl.Core.DTOs;

namespace GardenControl.Server.Services.Weather;

/// <summary>
/// OpenWeatherMap Integration für wetterbasierte Bewässerungsentscheidungen.
///
/// Benötigt einen kostenlosen API-Key von https://openweathermap.org/api
/// Konfiguration in appsettings.json:
///   "Weather": { "ApiKey": "...", "Latitude": 50.1, "Longitude": 8.7 }
///
/// Logik:
/// - Regnet es gerade → NICHT bewässern
/// - Regen in den nächsten 6h erwartet (>2mm) → NICHT bewässern
/// - Luftfeuchtigkeit >85% → Schwellenwert um 10% senken
/// - Temperatur >30°C → Schwellenwert um 5% erhöhen (mehr Wasser nötig)
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly ILogger<WeatherService> _logger;
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly double _latitude;
    private readonly double _longitude;
    private WeatherDto? _cachedWeather;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public WeatherService(ILogger<WeatherService> logger, IConfiguration config)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _apiKey = config.GetValue<string>("Weather:ApiKey");
        _latitude = config.GetValue<double>("Weather:Latitude");
        _longitude = config.GetValue<double>("Weather:Longitude");

        if (!IsConfigured)
        {
            _logger.LogWarning("Wetter-Service nicht konfiguriert (kein API-Key). " +
                "Setze 'Weather:ApiKey' in appsettings.json für intelligente Bewässerung.");
        }
    }

    public async Task<WeatherDto?> GetCurrentWeatherAsync()
    {
        if (!IsConfigured) return null;

        // Cache prüfen
        if (_cachedWeather != null && DateTime.UtcNow - _lastFetch < _cacheDuration)
            return _cachedWeather;

        try
        {
            // Aktuelle Wetterdaten
            var currentUrl = $"https://api.openweathermap.org/data/2.5/weather" +
                $"?lat={_latitude}&lon={_longitude}&appid={_apiKey}&units=metric&lang=de";

            var currentJson = await _http.GetStringAsync(currentUrl);
            using var currentDoc = JsonDocument.Parse(currentJson);
            var root = currentDoc.RootElement;

            var weather = new WeatherDto
            {
                TemperatureCelsius = root.GetProperty("main").GetProperty("temp").GetDouble(),
                HumidityPercent = root.GetProperty("main").GetProperty("humidity").GetInt32(),
                WindSpeed = root.GetProperty("wind").GetProperty("speed").GetDouble(),
                FetchedAtUtc = DateTime.UtcNow
            };

            // Wetter-Beschreibung
            var weatherArray = root.GetProperty("weather");
            if (weatherArray.GetArrayLength() > 0)
            {
                weather.Description = weatherArray[0].GetProperty("description").GetString() ?? "";
                weather.IconCode = weatherArray[0].GetProperty("icon").GetString() ?? "";

                var mainWeather = weatherArray[0].GetProperty("main").GetString() ?? "";
                weather.IsRaining = mainWeather is "Rain" or "Drizzle" or "Thunderstorm";
            }

            // Regenmenge
            if (root.TryGetProperty("rain", out var rain) && rain.TryGetProperty("1h", out var rain1h))
                weather.RainLastHourMm = rain1h.GetDouble();

            // Vorhersage abrufen (nächste 6 Stunden)
            await FetchForecastAsync(weather);

            // Bewässerungsempfehlung berechnen
            EvaluateSkipRecommendation(weather);

            _cachedWeather = weather;
            _lastFetch = DateTime.UtcNow;

            _logger.LogInformation("Wetter aktualisiert: {Temp}°C, {Desc}, Regen={IsRaining}, Vorhersage-Regen={ExpectedRain}mm",
                weather.TemperatureCelsius, weather.Description,
                weather.IsRaining, weather.RainExpected6hMm);

            return weather;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Abrufen der Wetterdaten");
            return _cachedWeather; // Letzten Cache zurückgeben
        }
    }

    public async Task<bool> ShouldSkipWateringAsync()
    {
        var weather = await GetCurrentWeatherAsync();
        return weather?.ShouldSkipWatering ?? false;
    }

    private async Task FetchForecastAsync(WeatherDto weather)
    {
        try
        {
            var forecastUrl = $"https://api.openweathermap.org/data/2.5/forecast" +
                $"?lat={_latitude}&lon={_longitude}&appid={_apiKey}&units=metric&cnt=3";

            var forecastJson = await _http.GetStringAsync(forecastUrl);
            using var forecastDoc = JsonDocument.Parse(forecastJson);

            var totalRain = 0.0;
            var rainExpected = false;

            foreach (var item in forecastDoc.RootElement.GetProperty("list").EnumerateArray())
            {
                if (item.TryGetProperty("rain", out var rain) && rain.TryGetProperty("3h", out var rain3h))
                {
                    totalRain += rain3h.GetDouble();
                    rainExpected = true;
                }

                var weatherArr = item.GetProperty("weather");
                if (weatherArr.GetArrayLength() > 0)
                {
                    var main = weatherArr[0].GetProperty("main").GetString() ?? "";
                    if (main is "Rain" or "Drizzle" or "Thunderstorm")
                        rainExpected = true;
                }
            }

            weather.RainExpectedSoon = rainExpected;
            weather.RainExpected6hMm = totalRain;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vorhersage konnte nicht abgerufen werden");
        }
    }

    /// <summary>
    /// Bewertet ob die Bewässerung übersprungen werden sollte.
    /// </summary>
    private static void EvaluateSkipRecommendation(WeatherDto weather)
    {
        if (weather.IsRaining)
        {
            weather.ShouldSkipWatering = true;
            weather.SkipReason = $"Es regnet gerade ({weather.RainLastHourMm:F1}mm/h)";
            return;
        }

        if (weather.RainExpectedSoon && weather.RainExpected6hMm >= 2.0)
        {
            weather.ShouldSkipWatering = true;
            weather.SkipReason = $"Regen erwartet ({weather.RainExpected6hMm:F1}mm in 6h)";
            return;
        }

        if (weather.HumidityPercent >= 90)
        {
            weather.ShouldSkipWatering = true;
            weather.SkipReason = $"Sehr hohe Luftfeuchtigkeit ({weather.HumidityPercent}%)";
            return;
        }

        weather.ShouldSkipWatering = false;
        weather.SkipReason = string.Empty;
    }
}
