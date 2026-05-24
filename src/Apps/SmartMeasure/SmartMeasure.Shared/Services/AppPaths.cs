namespace SmartMeasure.Shared.Services;

/// <summary>
/// Standard-Implementierung von <see cref="IAppPaths"/> für Desktop (Windows/Linux).
/// Nutzt <c>Environment.SpecialFolder.ApplicationData</c> — auf Windows ist das <c>%APPDATA%</c>,
/// auf Linux <c>~/.config</c>.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public string AppDataFolder { get; }
    public string DatabasePath => Path.Combine(AppDataFolder, "smartmeasure.db");
    public string ExportFolder => Path.Combine(AppDataFolder, "Exports");
    public string PhotosFolder => Path.Combine(AppDataFolder, "Photos");

    public AppPaths()
    {
        // ApplicationData ist auf Desktop plattformübergreifend korrekt:
        // Windows → %APPDATA%, Linux → ~/.config
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Fallback wenn SpecialFolder leer ist (selten, z.B. headless Linux ohne HOME)
        if (string.IsNullOrWhiteSpace(baseFolder))
            baseFolder = Path.Combine(AppContext.BaseDirectory, "data");

        AppDataFolder = Path.Combine(baseFolder, "SmartMeasure");

        // Ordner sicherstellen, Fehler still schlucken — Consumer macht eigenes try-catch bei File-Zugriff
        try
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(ExportFolder);
            Directory.CreateDirectory(PhotosFolder);
        }
        catch
        {
            // Permissions, readonly FS, etc. — Error wird beim ersten File-Write sichtbar
        }
    }
}
