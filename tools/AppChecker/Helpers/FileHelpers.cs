namespace AppChecker.Helpers;

static class FileHelpers
{
    /// <summary>Solution-Root finden (aufwaerts nach MeineApps.Ava.sln suchen)</summary>
    public static string? FindSolutionRoot()
    {
        // Bekanntes Verzeichnis zuerst
        if (File.Exists(@"F:\Meine_Apps_Ava\MeineApps.Ava.sln"))
            return @"F:\Meine_Apps_Ava";

        // Fallback: vom aktuellen Verzeichnis aufwaerts
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MeineApps.Ava.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    /// <summary>Relativen Pfad berechnen</summary>
    public static string GetRelativePath(string fullPath, string basePath)
    {
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return fullPath[(basePath.Length + 1)..];
        return Path.GetFileName(fullPath);
    }

    /// <summary>Prüft ob eine Zeile per Suppress-Kommentar ignoriert werden soll</summary>
    public static bool IsSuppressed(string[] lines, int lineIndex)
    {
        if (lineIndex <= 0) return false;
        return lines[lineIndex - 1].TrimStart().StartsWith("// AppChecker:ignore");
    }

    /// <summary>Alle .cs Dateien in einem Verzeichnis laden (ohne obj/bin) — explizit UTF-8</summary>
    public static List<CsFile> LoadCsFiles(string directory, string basePath)
    {
        if (!Directory.Exists(directory)) return [];

        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(f =>
            {
                var lines = File.ReadAllLines(f, System.Text.Encoding.UTF8);
                var content = string.Join('\n', lines);
                var relPath = GetRelativePath(f, basePath);
                return new CsFile(f, relPath, lines, content);
            })
            .ToList();
    }

    /// <summary>Alle .axaml Dateien in einem Verzeichnis laden (ohne obj/bin) — explizit UTF-8</summary>
    public static List<AxamlFile> LoadAxamlFiles(string directory, string basePath)
    {
        if (!Directory.Exists(directory)) return [];

        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(f =>
            {
                var content = File.ReadAllText(f, System.Text.Encoding.UTF8);
                var relPath = GetRelativePath(f, basePath);
                return new AxamlFile(f, relPath, content);
            })
            .ToList();
    }

    /// <summary>
    /// Aggregiert den Inhalt ALLER MainViewModel-Dateien einer App, inkl. der dokumentierten
    /// Partial-Splits (MainViewModel.Navigation.cs, .Tabs.cs, .EventHandlers.cs, .Properties.cs —
    /// siehe Haupt-CLAUDE.md "Service-Extraktion + Event-Cleanup"). Checker, die nur
    /// "MainViewModel.cs" lesen, uebersehen sonst Logik in den Partials und erzeugen falsche
    /// WARNs gerade bei Apps, die das empfohlene Partial-Pattern befolgen (HandwerkerImperium,
    /// BomberBlast, RebornSaga).
    /// </summary>
    /// <returns>
    /// Primary = die Haupt-Datei MainViewModel.cs (oder die erste gefundene Partial, sonst null).
    /// Content = konkatenierter Inhalt aller MainViewModel*-Dateien (leer wenn keine existiert).
    /// </returns>
    public static (CsFile? Primary, string Content) GetMainViewModel(CheckContext ctx)
    {
        var files = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels")
                     && Path.GetFileName(f.FullPath).StartsWith("MainViewModel.", StringComparison.Ordinal))
            .ToList();

        if (files.Count == 0) return (null, string.Empty);

        var primary = files.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainViewModel.cs")
                      ?? files[0];
        var content = string.Join('\n', files.Select(f => f.Content));
        return (primary, content);
    }

    /// <summary>
    /// Heuristik: Nutzt die App eingebettete Avalonia-Assets (eigenen Shared/Assets-Ordner)?
    /// True, wenn ein Shared/Assets-Ordner existiert ODER eine AXAML/CS eine app-eigene
    /// avares://{App}/-Ressource bzw. einen relativen "/Assets/"-Pfad referenziert. Apps, die
    /// ihre Grafik nur ueber Material.Icons + Android-Mipmaps + prozedurales SkiaSharp beziehen
    /// (z.B. BingXBot/GardenControl/SmartMeasure), brauchen keinen Shared/Assets-Ordner.
    /// </summary>
    public static bool AppUsesEmbeddedAssets(CheckContext ctx)
    {
        if (Directory.Exists(Path.Combine(ctx.SharedDir, "Assets"))) return true;
        bool InContent(string c) =>
            c.Contains($"avares://{ctx.App.Name}/", StringComparison.Ordinal)
            || c.Contains("\"/Assets/", StringComparison.Ordinal);
        return ctx.AxamlFiles.Any(f => InContent(f.Content)) || ctx.SharedCsFiles.Any(f => InContent(f.Content));
    }

    /// <summary>Erstellt den CheckContext fuer eine App (laedt alle Dateien gecacht)</summary>
    public static CheckContext CreateContext(AppDef app, string solutionRoot)
    {
        var appBase = Path.Combine(solutionRoot, "src", "Apps", app.Name);
        var sharedDir = Path.Combine(appBase, $"{app.Name}.Shared");
        var androidDir = Path.Combine(appBase, $"{app.Name}.Android");
        var desktopDir = Path.Combine(appBase, $"{app.Name}.Desktop");

        var sharedCs = LoadCsFiles(sharedDir, appBase);
        var androidCs = LoadCsFiles(androidDir, appBase);
        var desktopCs = LoadCsFiles(desktopDir, appBase);
        var allCs = sharedCs.Concat(androidCs).Concat(desktopCs).ToList();
        var axamlFiles = LoadAxamlFiles(sharedDir, appBase);

        return new CheckContext
        {
            App = app,
            SharedDir = sharedDir,
            AndroidDir = androidDir,
            DesktopDir = desktopDir,
            SolutionRoot = solutionRoot,
            CsFiles = allCs,
            SharedCsFiles = sharedCs,
            AndroidCsFiles = androidCs,
            AxamlFiles = axamlFiles,
        };
    }
}
