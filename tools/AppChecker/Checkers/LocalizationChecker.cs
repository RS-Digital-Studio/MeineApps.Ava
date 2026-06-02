using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Lokalisierung: resx-Dateien, Key-Vergleich, Designer.cs</summary>
class LocalizationChecker : IChecker
{
    public string Category => "Lokalisierung";

    // Die 6 Zielsprachen des Studios. Genau EINE davon ist die Base-Kultur (AppStrings.resx),
    // die uebrigen liegen als Overlay AppStrings.{lang}.resx vor. Welche Sprache die Base ist,
    // unterscheidet sich pro App (meist en als Base + de/es/fr/it/pt; SmartMeasure: de als Base + en/...).
    static readonly string[] TargetLangs = ["de", "en", "es", "fr", "it", "pt"];

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

        if (File.Exists(baseResx))
            results.Add(new(Severity.Pass, Category, "AppStrings.resx (Base) vorhanden"));
        else
        {
            results.Add(new(Severity.Fail, Category, "AppStrings.resx (Base) fehlt"));
            return results;
        }

        // 6-Sprachen-Abdeckung: Base + 5 Overlays. Die eine fehlende Overlay-Sprache ist die Base-Kultur.
        var presentOverlays = TargetLangs.Where(l => File.Exists(Path.Combine(stringsDir, $"AppStrings.{l}.resx"))).ToList();
        var missingOverlays = TargetLangs.Where(l => !File.Exists(Path.Combine(stringsDir, $"AppStrings.{l}.resx"))).ToList();
        const int requiredOverlays = 5; // 6 Zielsprachen minus 1 Base-Kultur
        if (presentOverlays.Count >= requiredOverlays)
            results.Add(new(Severity.Pass, Category, $"6-Sprachen-Abdeckung: Base + {presentOverlays.Count} Overlay-Sprachen ({string.Join("/", presentOverlays)})"));
        else
            results.Add(new(Severity.Fail, Category, $"Nur {presentOverlays.Count}/{requiredOverlays} Overlay-Sprachen ({string.Join("/", presentOverlays)}) — fehlend (eine darf die Base-Kultur sein): {string.Join(", ", missingOverlays)}"));

        // Alle tatsaechlich vorhandenen Overlay-resx (inkl. Extra-Sprachen wie ja/ko/zh-CN bei HandwerkerImperium)
        var langFiles = Directory.GetFiles(stringsDir, "AppStrings.*.resx")
            .Select(p => (Lang: ExtractCulture(p), Path: p))
            .Where(t => t.Lang != null)
            .Select(t => (Lang: t.Lang!, t.Path))
            .OrderBy(t => t.Lang)
            .ToList();

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

            foreach (var langFile in langFiles)
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
            foreach (var langFile in langFiles)
            {
                var langKeys = ResxHelpers.ExtractResxKeys(langFile.Path);
                var extra = langKeys.Except(baseKeys).ToList();
                if (extra.Count > 0)
                    results.Add(new(Severity.Info, Category, $"{langFile.Lang}: {extra.Count} ueberflüssige Keys ({string.Join(", ", extra.Take(5))}{(extra.Count > 5 ? "..." : "")})"));
            }
        }

        return results;
    }

    /// <summary>Extrahiert die Kultur aus einem Overlay-Dateinamen (AppStrings.de.resx -> "de", AppStrings.zh-CN.resx -> "zh-CN"). Base (AppStrings.resx) liefert null.</summary>
    static string? ExtractCulture(string path)
    {
        var m = Regex.Match(Path.GetFileName(path), @"^AppStrings\.([\w-]+)\.resx$");
        return m.Success ? m.Groups[1].Value : null;
    }
}
