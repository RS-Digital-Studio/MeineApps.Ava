namespace AppChecker.Checkers;

/// <summary>Prueft Assets: icon.png, MainWindow Icon-Referenz</summary>
class AssetsChecker : IChecker
{
    public string Category => "Assets";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        if (!Directory.Exists(ctx.SharedDir))
        {
            results.Add(new(Severity.Fail, Category, "Shared-Verzeichnis nicht gefunden"));
            return results;
        }

        // icon.png in Assets/
        var assetsDir = Path.Combine(ctx.SharedDir, "Assets");
        if (Directory.Exists(assetsDir))
        {
            var iconPath = Path.Combine(assetsDir, "icon.png");
            if (File.Exists(iconPath))
                results.Add(new(Severity.Pass, Category, "icon.png in Assets/ vorhanden"));
            else
                results.Add(new(Severity.Warn, Category, "icon.png fehlt in Assets/"));
        }
        else
            results.Add(new(Severity.Fail, Category, "Assets-Verzeichnis fehlt"));

        // MainWindow.axaml referenziert Icon via avares://
        var mainWindowAxaml = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainWindow.axaml");
        if (mainWindowAxaml != null)
        {
            if (mainWindowAxaml.Content.Contains("avares://") && mainWindowAxaml.Content.Contains("icon"))
                results.Add(new(Severity.Pass, Category, "MainWindow.axaml referenziert Icon via avares://"));
            else
                results.Add(new(Severity.Warn, Category, "MainWindow.axaml: Icon-Referenz via avares:// nicht gefunden"));
        }
        else
            results.Add(new(Severity.Info, Category, "MainWindow.axaml nicht im Shared-Projekt (evtl. im Desktop-Projekt)"));

        return results;
    }
}
