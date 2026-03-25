using GardenControl.Core.DTOs;

namespace GardenControl.Server.Services.Weather;

/// <summary>
/// Wetter-Service für intelligente Bewässerungsentscheidungen.
/// Fragt OpenWeatherMap API ab und entscheidet ob bewässert werden soll.
/// </summary>
public interface IWeatherService
{
    /// <summary>Aktuelle Wetterdaten + Vorhersage abrufen</summary>
    Task<WeatherDto?> GetCurrentWeatherAsync();

    /// <summary>Soll die Bewässerung übersprungen werden? (Regen, hohe Feuchtigkeit)</summary>
    Task<bool> ShouldSkipWateringAsync();

    /// <summary>Ist der Service konfiguriert? (API-Key vorhanden)</summary>
    bool IsConfigured { get; }
}
