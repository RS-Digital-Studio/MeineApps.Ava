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

    /// <summary>Alle .cs Dateien in einem Verzeichnis laden (ohne obj/bin)</summary>
    public static List<CsFile> LoadCsFiles(string directory, string basePath)
    {
        if (!Directory.Exists(directory)) return [];

        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(f =>
            {
                var lines = File.ReadAllLines(f);
                var content = string.Join('\n', lines);
                var relPath = GetRelativePath(f, basePath);
                return new CsFile(f, relPath, lines, content);
            })
            .ToList();
    }

    /// <summary>Alle .axaml Dateien in einem Verzeichnis laden (ohne obj/bin)</summary>
    public static List<AxamlFile> LoadAxamlFiles(string directory, string basePath)
    {
        if (!Directory.Exists(directory)) return [];

        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(directory, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(f =>
            {
                var content = File.ReadAllText(f);
                var relPath = GetRelativePath(f, basePath);
                return new AxamlFile(f, relPath, content);
            })
            .ToList();
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
