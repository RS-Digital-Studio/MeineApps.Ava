namespace AppChecker.Checkers;

/// <summary>Prueft App.axaml/cs: MaterialIconStyles, SkiaThemeHelper, LocalizationService</summary>
class AvaloniaUiChecker : IChecker
{
    public string Category => "Avalonia/UI";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        if (!Directory.Exists(ctx.SharedDir))
        {
            results.Add(new(Severity.Fail, Category, "Shared-Verzeichnis nicht gefunden"));
            return results;
        }

        // App.axaml
        var appAxaml = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml");
        if (appAxaml != null)
        {
            if (appAxaml.Content.Contains("MaterialIconStyles"))
                results.Add(new(Severity.Pass, Category, "MaterialIconStyles registriert in App.axaml"));
            else
                results.Add(new(Severity.Fail, Category, "MaterialIconStyles NICHT registriert in App.axaml → Icons unsichtbar!"));
        }
        else
            results.Add(new(Severity.Fail, Category, "App.axaml fehlt"));

        // App.axaml.cs
        var appAxamlCs = ctx.SharedCsFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml.cs");
        if (appAxamlCs != null)
        {
            var content = appAxamlCs.Content;

            if (content.Contains("SkiaThemeHelper.RefreshColors"))
                results.Add(new(Severity.Pass, Category, "SkiaThemeHelper.RefreshColors() beim Start aufgerufen"));
            else
                results.Add(new(Severity.Warn, Category, "SkiaThemeHelper.RefreshColors() fehlt in App.axaml.cs"));

            if (content.Contains("ILocalizationService"))
                results.Add(new(Severity.Pass, Category, "ILocalizationService registriert"));
            else
                results.Add(new(Severity.Fail, Category, "ILocalizationService nicht gefunden in App.axaml.cs"));
        }
        else
            results.Add(new(Severity.Fail, Category, "App.axaml.cs fehlt"));

        return results;
    }
}
