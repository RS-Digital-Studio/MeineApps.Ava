namespace FitnessRechner.Services;

/// <summary>
/// Interface für Erfolgs-Sounds (Achievement, Level-Up, Tagesziel).
/// Minimaler Sound-Service mit einem einzigen Effekt.
/// </summary>
public interface IFitnessSoundService
{
    /// <summary>Sound-Effekte aktiviert/deaktiviert.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Spielt einen kurzen Erfolgs-Sound ab.</summary>
    void PlaySuccess();
}

/// <summary>
/// Desktop-Implementierung: Kein Sound verfügbar.
/// </summary>
public class NoOpFitnessSoundService : IFitnessSoundService
{
    public bool IsEnabled { get; set; } = true;
    public void PlaySuccess() { }
}
