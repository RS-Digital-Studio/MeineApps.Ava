namespace MeineApps.UI.Loading;

/// <summary>
/// Interface für app-spezifische Lade-Pipelines.
/// Jede App definiert ihre Ladeschritte mit Gewichtung.
/// </summary>
public interface ILoadingPipeline
{
    /// <summary>
    /// Wird bei Fortschrittsänderung gefeuert.
    /// Parameter: (float progress 0.0-1.0, string statusText)
    /// </summary>
    event Action<float, string>? ProgressChanged;

    /// <summary>
    /// Führt alle Ladeschritte sequentiell aus und meldet gewichteten Fortschritt.
    /// </summary>
    Task ExecuteAsync();
}
