using System.Text;

namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Zentraler Helper für atomares Datei-Schreiben.
/// Vermeidet Daten-Verlust bei Crashes/Power-Loss durch Temp-Datei + Rename.
/// </summary>
/// <remarks>
/// Pattern: Schreibe in {path}.tmp, dann File.Move(.tmp, target, overwrite). Beim Move
/// ersetzt der Dateisystem-Eintrag atomar — bei Crash mitten im WriteAllTextAsync bleibt
/// die Ziel-Datei unverändert.
/// </remarks>
public static class AtomicFileWriter
{
    /// <summary>
    /// Schreibt UTF-8 Text atomar in eine Datei.
    /// </summary>
    public static async Task WriteAllTextAsync(string targetPath, string content)
    {
        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    /// <summary>
    /// Schreibt Bytes atomar in eine Datei.
    /// </summary>
    public static async Task WriteAllBytesAsync(string targetPath, byte[] content)
    {
        var tempPath = targetPath + ".tmp";
        await File.WriteAllBytesAsync(tempPath, content);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    /// <summary>
    /// Schreibt UTF-8 Text mit explizit angegebener Encoding atomar in eine Datei.
    /// </summary>
    public static async Task WriteAllTextAsync(string targetPath, string content, Encoding encoding)
    {
        var tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, encoding);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}
