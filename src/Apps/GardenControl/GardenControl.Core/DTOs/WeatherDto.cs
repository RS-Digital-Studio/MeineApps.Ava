namespace GardenControl.Core.DTOs;

/// <summary>
/// Wetterdaten von OpenWeatherMap - bestimmt ob bewässert werden soll.
/// </summary>
public class WeatherDto
{
    /// <summary>Aktuelle Temperatur in °C</summary>
    public double TemperatureCelsius { get; set; }

    /// <summary>Luftfeuchtigkeit in %</summary>
    public int HumidityPercent { get; set; }

    /// <summary>Wetterbeschreibung (z.B. "Leichter Regen")</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Wetter-Icon Code (z.B. "10d" für Regen)</summary>
    public string IconCode { get; set; } = string.Empty;

    /// <summary>Windgeschwindigkeit in m/s</summary>
    public double WindSpeed { get; set; }

    /// <summary>Regnet es gerade?</summary>
    public bool IsRaining { get; set; }

    /// <summary>Regenmenge letzte Stunde in mm (0 wenn kein Regen)</summary>
    public double RainLastHourMm { get; set; }

    /// <summary>Wird es in den nächsten 6h regnen? (Vorhersage)</summary>
    public bool RainExpectedSoon { get; set; }

    /// <summary>Erwartete Regenmenge in den nächsten 6h in mm</summary>
    public double RainExpected6hMm { get; set; }

    /// <summary>Zeitpunkt der Wetterdaten</summary>
    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Empfehlung: Bewässerung heute überspringen?</summary>
    public bool ShouldSkipWatering { get; set; }

    /// <summary>Begründung für die Empfehlung</summary>
    public string SkipReason { get; set; } = string.Empty;
}
