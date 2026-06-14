using System.Globalization;

namespace BomberBlast.Services;

/// <summary>
/// Schlanker Crash-Recovery-Zähler in einer eigenen Mini-Datei (<c>crashcount.txt</c>).
///
/// <para>Läuft VOR dem DI-Build in <c>App.OnFrameworkInitializationCompleted</c>: vor jedem
/// Init-Versuch wird inkrementiert, nach erfolgreichem Splash-Abschluss zurückgesetzt. Überlebt
/// einen Init-Crash persistent — beim 3. Versuch in Folge greift der Safe-Mode.</para>
///
/// <para><b>Warum eine eigene Datei statt PreferencesService?</b> Früher wurde dafür ein
/// kompletter <c>new PreferencesService("BomberBlast")</c> vor dem DI-Build erzeugt — der lädt
/// synchron die gesamte (größte) <c>preferences.json</c> nur für einen einzigen int, und der
/// DI-PreferencesService liest dieselbe Datei direkt danach ein zweites Mal. Diese Mini-Datei
/// liest/schreibt nur die Zahl selbst (wenige Bytes) und hält den Startup-Pfad schlank.</para>
///
/// <para>Liegt im selben App-Daten-Ordner wie <c>preferences.json</c>
/// (<c>{ApplicationData}/BomberBlast/</c>), damit Crash-State und Spielstand zusammen liegen.</para>
/// </summary>
internal static class CrashCounter
{
    private const string AppFolderName = "BomberBlast";
    private const string FileName = "crashcount.txt";

    private static string GetFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, AppFolderName);
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, FileName);
    }

    /// <summary>Liest den aktuellen Zählerstand (0 wenn Datei fehlt oder unlesbar).</summary>
    public static int Read()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return 0;
            var text = File.ReadAllText(path).Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : 0;
        }
        catch
        {
            // Crash-Counter ist Best-Effort — ein Lesefehler darf den App-Start nicht verhindern.
            return 0;
        }
    }

    /// <summary>Schreibt den Zählerstand atomar (Temp + Move) — synchron, weil pre-DI im Init-Pfad.</summary>
    public static void Write(int value)
    {
        try
        {
            var path = GetFilePath();
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, value.ToString(CultureInfo.InvariantCulture));
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // Schreibfehler ignorieren (Best-Effort) — schlimmstenfalls greift der Safe-Mode später.
        }
    }

    /// <summary>Inkrementiert den Zähler und gibt den neuen Stand zurück.</summary>
    public static int Increment()
    {
        var next = Read() + 1;
        Write(next);
        return next;
    }

    /// <summary>Setzt den Zähler auf 0 (nach erfolgreichem Start).</summary>
    public static void Reset() => Write(0);
}
