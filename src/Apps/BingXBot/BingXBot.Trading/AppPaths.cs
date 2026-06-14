using BingXBot.Core.Interfaces;

namespace BingXBot.Trading;

/// <summary>
/// Standard-Implementierung von <see cref="IAppPaths"/> für Desktop (Windows/Linux) und Android.
/// Nutzt <c>Environment.SpecialFolder.ApplicationData</c> — auf Windows ist das <c>%APPDATA%</c>,
/// auf Linux <c>~/.config</c>, auf Android <c>/data/user/0/{package}/files/.config</c>.
/// Einheitlicher Pfad pro Plattform, keine Sonderbehandlung nötig.
/// </summary>
public class AppPaths : IAppPaths
{
    public string AppDataFolder { get; }
    public string DatabasePath => Path.Combine(AppDataFolder, "bot.db");
    public string CredentialsPath => Path.Combine(AppDataFolder, "credentials.dat");
    public string ClientProfileFolder => Path.Combine(AppDataFolder, "Client");
    public string ClientProfilePath => Path.Combine(ClientProfileFolder, "connection.json");

    public AppPaths()
    {
        // ApplicationData ist plattformübergreifend korrekt:
        // Windows → %APPDATA%, Linux → ~/.config, Android → filesDir/.config
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Fallback wenn SpecialFolder leer ist (selten, z.B. headless Linux-Service ohne HOME)
        if (string.IsNullOrWhiteSpace(baseFolder))
            baseFolder = Path.Combine(AppContext.BaseDirectory, "data");

        AppDataFolder = Path.Combine(baseFolder, "BingXBot");

        // Ordner sicherstellen, Fehler still schlucken — Consumer macht eigenes try-catch bei File-Zugriff
        try { Directory.CreateDirectory(AppDataFolder); }
        catch { /* Permissions, readonly FS, etc. — Error wird beim ersten File-Write sichtbar */ }
    }
}
