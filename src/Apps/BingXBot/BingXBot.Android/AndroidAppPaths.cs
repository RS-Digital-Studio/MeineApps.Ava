using Android.Content;
using BingXBot.Core.Interfaces;

namespace BingXBot;

/// <summary>
/// Android-spezifische Implementierung von <see cref="IAppPaths"/>.
/// Verwendet <c>Context.FilesDir</c> statt <c>Environment.SpecialFolder</c>, weil letzteres
/// auf manchen Android-Versionen/ROMs unzuverlässige Pfade liefert.
/// Alle Pfade liegen im App-eigenen Sandbox-Verzeichnis — garantiert schreibbar.
/// </summary>
public sealed class AndroidAppPaths : IAppPaths
{
    public string AppDataFolder { get; }
    public string DatabasePath => Path.Combine(AppDataFolder, "bot.db");
    public string CredentialsPath => Path.Combine(AppDataFolder, "credentials.dat");
    public string ClientProfileFolder => Path.Combine(AppDataFolder, "Client");
    public string ClientProfilePath => Path.Combine(ClientProfileFolder, "connection.json");

    public AndroidAppPaths(Context context)
    {
        // Context.FilesDir: /data/user/0/{package}/files — sandbox-sicher, immer schreibbar
        var filesDir = context.FilesDir?.AbsolutePath ?? "/data/local/tmp";
        AppDataFolder = Path.Combine(filesDir, "BingXBot");

        try { Directory.CreateDirectory(AppDataFolder); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"AndroidAppPaths: CreateDir failed: {ex.Message}"); }
    }
}
