using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft Theme-Patterns: StaticResource fuer Brush/Color, AppPalette vorhanden</summary>
class ThemeChecker : IChecker
{
    public string Category => "Theme";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int staticResourceBrushCount = 0;
        int dynamicResourceBrushCount = 0;

        foreach (var view in ctx.AxamlFiles)
        {
            // App.axaml und AppPalette.axaml ausschliessen (dort sind StaticResource OK)
            var fileName = Path.GetFileName(view.FullPath);
            if (fileName == "App.axaml" || fileName == "AppPalette.axaml") continue;

            staticResourceBrushCount += Regex.Matches(view.Content, @"\{StaticResource\s+\w*Brush\}").Count;
            dynamicResourceBrushCount += Regex.Matches(view.Content, @"\{DynamicResource\s+\w*Brush\}").Count;
        }

        if (staticResourceBrushCount > 0)
            results.Add(new(Severity.Warn, Category, $"{staticResourceBrushCount}x StaticResource fuer Brush (sollte DynamicResource sein)"));
        else
            results.Add(new(Severity.Pass, Category, "Keine StaticResource fuer Brush (DynamicResource korrekt)"));

        if (dynamicResourceBrushCount > 0)
            results.Add(new(Severity.Pass, Category, $"{dynamicResourceBrushCount}x DynamicResource fuer Brush (korrekt)"));

        // AppPalette.axaml vorhanden
        var appPalette = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "AppPalette.axaml");
        if (appPalette != null)
            results.Add(new(Severity.Pass, Category, "AppPalette.axaml vorhanden (app-spezifische Farbpalette)"));
        else
            results.Add(new(Severity.Fail, Category, "AppPalette.axaml fehlt → jede App braucht eine eigene Farbpalette in Themes/AppPalette.axaml"));

        // AppPalette in App.axaml eingebunden
        var appAxaml = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml");
        if (appAxaml != null)
        {
            if (Regex.IsMatch(appAxaml.Content, @"<StyleInclude\s+Source=""/Themes/AppPalette\.axaml"""))
                results.Add(new(Severity.Pass, Category, "AppPalette.axaml in App.axaml eingebunden"));
            else
                results.Add(new(Severity.Fail, Category, "AppPalette.axaml nicht in App.axaml eingebunden → StyleInclude hinzufuegen"));
        }

        return results;
    }
}
