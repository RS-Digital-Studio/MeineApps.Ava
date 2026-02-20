using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft Android-Gotchas: grantUriPermissions, ${applicationId}, BackButton</summary>
class AndroidGotchasChecker : IChecker
{
    public string Category => "Android-Gotchas";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        if (!Directory.Exists(ctx.AndroidDir))
        {
            results.Add(new(Severity.Info, Category, "Android-Verzeichnis nicht gefunden"));
            return results;
        }

        CheckManifestGotchas(results, ctx);
        CheckBackButton(results, ctx);

        return results;
    }

    void CheckManifestGotchas(List<CheckResult> results, CheckContext ctx)
    {
        var manifestPath = Path.Combine(ctx.AndroidDir, "AndroidManifest.xml");
        if (!File.Exists(manifestPath)) return;

        var content = File.ReadAllText(manifestPath);

        // grantUriPermission ohne 's' → AAPT2260
        if (content.Contains("grantUriPermission=") && !content.Contains("grantUriPermissions="))
            results.Add(new(Severity.Fail, Category, "grantUriPermission ohne 's' → AAPT2260 Fehler. Muss 'grantUriPermissions' heissen"));
        else if (content.Contains("grantUriPermissions="))
            results.Add(new(Severity.Pass, Category, "grantUriPermissions korrekt geschrieben (mit 's')"));

        // ${applicationId} → geht nicht in .NET Android
        if (content.Contains("${applicationId}"))
            results.Add(new(Severity.Fail, Category, "${applicationId} in AndroidManifest → .NET Android kennt keine Gradle-Placeholder, hardcodierten Package-Namen verwenden"));
        else
            results.Add(new(Severity.Pass, Category, "Keine ${applicationId} Gradle-Placeholder"));
    }

    void CheckBackButton(List<CheckResult> results, CheckContext ctx)
    {
        var mainVm = ctx.SharedCsFiles.FirstOrDefault(f =>
            f.FullPath.EndsWith("MainViewModel.cs") && f.FullPath.Contains("ViewModels"));

        if (mainVm == null) return;

        // HandleBackPressed in MainViewModel
        if (mainVm.Content.Contains("HandleBackPressed"))
            results.Add(new(Severity.Pass, Category, "HandleBackPressed in MainViewModel vorhanden"));
        else
            results.Add(new(Severity.Warn, Category, "HandleBackPressed fehlt in MainViewModel (Android Back-Button wird nicht behandelt)"));

        // ExitHintRequested Event
        if (mainVm.Content.Contains("ExitHintRequested"))
            results.Add(new(Severity.Pass, Category, "ExitHintRequested Event vorhanden (Double-Back-to-Exit)"));
        else
            results.Add(new(Severity.Warn, Category, "ExitHintRequested fehlt in MainViewModel"));

        // MainActivity verdrahtet
        var mainActivity = ctx.AndroidCsFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainActivity.cs");
        if (mainActivity != null)
        {
            if (mainActivity.Content.Contains("OnBackPressed") || mainActivity.Content.Contains("HandleBackPressed"))
                results.Add(new(Severity.Pass, Category, "OnBackPressed in MainActivity verdrahtet"));
            else
                results.Add(new(Severity.Warn, Category, "OnBackPressed nicht in MainActivity ueberschrieben"));

            if (mainActivity.Content.Contains("ExitHintRequested"))
                results.Add(new(Severity.Pass, Category, "ExitHintRequested in MainActivity verdrahtet"));
            else
                results.Add(new(Severity.Warn, Category, "ExitHintRequested nicht in MainActivity verdrahtet"));
        }
    }
}
