using Android.Content;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Android.Services;

/// <summary>
/// Android-spezifische Implementierung von <see cref="IAppPaths"/>.
/// Verwendet <c>Context.FilesDir</c> statt <c>Environment.SpecialFolder</c>, weil letzteres
/// auf manchen Android-Versionen/ROMs unzuverlässige Pfade liefert.
/// Alle Pfade liegen im App-eigenen Sandbox-Verzeichnis — garantiert schreibbar.
/// </summary>
public sealed class AndroidAppPaths : IAppPaths
{
    public string AppDataFolder { get; }
    public string DatabasePath => Path.Combine(AppDataFolder, "smartmeasure.db");
    public string ExportFolder { get; }

    public AndroidAppPaths(Context context)
    {
        // Context.FilesDir: /data/user/0/{package}/files — sandbox-sicher, immer schreibbar
        var filesDir = context.FilesDir?.AbsolutePath ?? "/data/local/tmp";
        AppDataFolder = Path.Combine(filesDir, "SmartMeasure");

        // Exports im öffentlichen Downloads-Ordner (damit User die Dateien sieht)
        // ExternalFilesDir ist scoped storage-kompatibel ohne zusätzliche Permissions
        var externalDir = context.GetExternalFilesDir("Exports")?.AbsolutePath
            ?? Path.Combine(AppDataFolder, "Exports");
        ExportFolder = externalDir;

        try
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(ExportFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidAppPaths: CreateDir failed: {ex.Message}");
        }
    }
}
