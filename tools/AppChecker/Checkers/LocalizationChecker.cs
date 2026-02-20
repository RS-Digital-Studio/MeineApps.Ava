using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Lokalisierung: resx-Dateien, Key-Vergleich, Designer.cs</summary>
class LocalizationChecker : IChecker
{
    public string Category => "Lokalisierung";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        var stringsDir = Path.Combine(ctx.SharedDir, "Resources", "Strings");
        if (!Directory.Exists(stringsDir))
        {
            results.Add(new(Severity.Fail, Category, "Resources/Strings Verzeichnis fehlt"));
            return results;
        }

        var baseResx = Path.Combine(stringsDir, "AppStrings.resx");
        var languages = new[] { "de", "es", "fr", "it", "pt" };
        var langFiles = languages.Select(l => (Lang: l, Path: Path.Combine(stringsDir, $"AppStrings.{l}.resx"))).ToList();

        if (File.Exists(baseResx))
            results.Add(new(Severity.Pass, Category, "AppStrings.resx (Base) vorhanden"));
        else
        {
            results.Add(new(Severity.Fail, Category, "AppStrings.resx (Base) fehlt"));
            return results;
        }

        var missingLangs = langFiles.Where(l => !File.Exists(l.Path)).Select(l => l.Lang).ToList();
        if (missingLangs.Count == 0)
            results.Add(new(Severity.Pass, Category, "Alle 5 Sprachdateien vorhanden (de/es/fr/it/pt)"));
        else
            foreach (var lang in missingLangs)
                results.Add(new(Severity.Fail, Category, $"AppStrings.{lang}.resx fehlt"));

        // Designer.cs
        var designerCs = Path.Combine(stringsDir, "AppStrings.Designer.cs");
        if (File.Exists(designerCs))
        {
            var fileInfo = new FileInfo(designerCs);
            if (fileInfo.Length > 100)
                results.Add(new(Severity.Pass, Category, "AppStrings.Designer.cs vorhanden und nicht leer"));
            else
                results.Add(new(Severity.Warn, Category, "AppStrings.Designer.cs ist fast leer"));
        }
        else
            results.Add(new(Severity.Warn, Category, "AppStrings.Designer.cs fehlt (wird beim Build generiert)"));

        // Key-Vergleich
        var baseKeys = ResxHelpers.ExtractResxKeys(baseResx);
        if (baseKeys.Count > 0)
        {
            results.Add(new(Severity.Info, Category, $"Base hat {baseKeys.Count} Keys"));

            foreach (var langFile in langFiles.Where(l => File.Exists(l.Path)))
            {
                var langKeys = ResxHelpers.ExtractResxKeys(langFile.Path);
                var missing = baseKeys.Except(langKeys).ToList();
                if (missing.Count == 0)
                    results.Add(new(Severity.Pass, Category, $"{langFile.Lang}: Alle {baseKeys.Count} Keys vorhanden"));
                else
                    results.Add(new(Severity.Warn, Category, $"{langFile.Lang}: {missing.Count} Keys fehlen ({string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "..." : "")})"));
            }
        }

        // Ueberflüssige Keys in Sprach-Dateien (Keys die in Sprache aber nicht in Base existieren)
        if (baseKeys.Count > 0)
        {
            foreach (var langFile in langFiles.Where(l => File.Exists(l.Path)))
            {
                var langKeys = ResxHelpers.ExtractResxKeys(langFile.Path);
                var extra = langKeys.Except(baseKeys).ToList();
                if (extra.Count > 0)
                    results.Add(new(Severity.Info, Category, $"{langFile.Lang}: {extra.Count} ueberflüssige Keys ({string.Join(", ", extra.Take(5))}{(extra.Count > 5 ? "..." : "")})"));
            }
        }

        return results;
    }
}
