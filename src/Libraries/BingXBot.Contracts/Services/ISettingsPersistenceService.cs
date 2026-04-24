namespace BingXBot.Contracts.Services;

/// <summary>
/// Kapselt das Persistieren der aktuellen DI-Singleton-Settings (Risk/Scanner/Bot/Backtest)
/// in die DB bzw. zum Server (im Remote-Modus). Ersetzt die statische
/// <c>App.SaveAllSettingsAsync()</c>-Methode und erlaubt ViewModels, die Persistenz
/// per Constructor-Injection sauber zu testen.
/// </summary>
public interface ISettingsPersistenceService
{
    /// <summary>
    /// Serialisiert alle aktuellen Settings und persistiert sie über <see cref="ISettingsService"/>.
    /// Mehrfach-Aufrufe werden intern per Semaphore entkoppelt (skip wenn bereits am Speichern).
    /// </summary>
    Task SaveAllAsync();
}
