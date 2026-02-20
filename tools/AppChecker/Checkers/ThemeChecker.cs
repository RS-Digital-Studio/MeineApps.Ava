using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft Theme-Patterns: StaticResource fuer Brush/Color, statisches Theme</summary>
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
            // App.axaml ausschliessen (dort sind StaticResource OK fuer Theme-Definitionen)
            if (Path.GetFileName(view.FullPath) == "App.axaml") continue;

            staticResourceBrushCount += Regex.Matches(view.Content, @"\{StaticResource\s+\w*Brush\}").Count;
            dynamicResourceBrushCount += Regex.Matches(view.Content, @"\{DynamicResource\s+\w*Brush\}").Count;
        }

        if (staticResourceBrushCount > 0)
            results.Add(new(Severity.Warn, Category, $"{staticResourceBrushCount}x StaticResource fuer Brush (sollte DynamicResource sein fuer Theme-Wechsel)"));
        else
            results.Add(new(Severity.Pass, Category, "Keine StaticResource fuer Brush (DynamicResource korrekt)"));

        if (dynamicResourceBrushCount > 0)
            results.Add(new(Severity.Pass, Category, $"{dynamicResourceBrushCount}x DynamicResource fuer Brush (korrekt)"));

        // Statisches Theme in App.axaml
        var appAxaml = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml");
        if (appAxaml != null)
        {
            // Prüfen ob ein Theme direkt in App.axaml eingebunden ist (statt dynamisch via ThemeService)
            if (Regex.IsMatch(appAxaml.Content, @"<StyleInclude\s+Source="".*Theme.*"""))
                results.Add(new(Severity.Warn, Category, "Statisches Theme in App.axaml → ThemeService laedt Themes dynamisch"));
            else
                results.Add(new(Severity.Pass, Category, "Kein statisches Theme in App.axaml (dynamisches Loading via ThemeService)"));
        }

        return results;
    }
}
