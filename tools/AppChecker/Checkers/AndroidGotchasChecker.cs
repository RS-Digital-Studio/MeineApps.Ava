using System.Text.RegularExpressions;
using AppChecker.Helpers;

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

        // ${applicationId} → wird in .NET Android NUR ersetzt, wenn der Placeholder via MSBuild-Property
        // <AndroidManifestPlaceholders>applicationId=$(ApplicationId)</...> definiert ist. Ohne diese
        // Definition bleibt der Platzhalter als Literal stehen (z.B. broken FileProvider-Authority).
        if (content.Contains("${applicationId}"))
        {
            bool placeholderDefined = HasManifestPlaceholder(ctx, "applicationId");
            if (placeholderDefined)
                results.Add(new(Severity.Pass, Category, "${applicationId} im Manifest, aber via AndroidManifestPlaceholders definiert (wird ersetzt)"));
            else
                results.Add(new(Severity.Fail, Category, "${applicationId} in AndroidManifest ohne <AndroidManifestPlaceholders>applicationId=...</> → bleibt unersetzt. Hardcodierten Package-Namen verwenden (wie alle anderen Apps) oder Placeholder im csproj definieren"));
        }
        else
            results.Add(new(Severity.Pass, Category, "Keine ${applicationId} Gradle-Placeholder"));
    }

    /// <summary>Prueft ob ein AndroidManifestPlaceholder-Key im Android-csproj oder Directory.Build.* definiert ist.</summary>
    static bool HasManifestPlaceholder(CheckContext ctx, string key)
    {
        var candidates = new List<string>
        {
            Path.Combine(ctx.AndroidDir, $"{ctx.App.Name}.Android.csproj"),
            Path.Combine(ctx.SolutionRoot, "Directory.Build.targets"),
            Path.Combine(ctx.SolutionRoot, "Directory.Build.props"),
        };
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            var text = File.ReadAllText(path);
            if (Regex.IsMatch(text, @"<AndroidManifestPlaceholders>[^<]*\b" + Regex.Escape(key) + @"\s*="))
                return true;
        }
        return false;
    }

    void CheckBackButton(List<CheckResult> results, CheckContext ctx)
    {
        // Inhalt ueber ALLE MainViewModel-Partials aggregieren (HandleBackPressed liegt oft in .Navigation.cs).
        var (mainVm, content) = FileHelpers.GetMainViewModel(ctx);

        if (mainVm == null) return;

        // HandleBackPressed in MainViewModel
        if (content.Contains("HandleBackPressed"))
            results.Add(new(Severity.Pass, Category, "HandleBackPressed in MainViewModel vorhanden"));
        else
            results.Add(new(Severity.Warn, Category, "HandleBackPressed fehlt in MainViewModel (Android Back-Button wird nicht behandelt)"));

        // ExitHintRequested Event
        if (content.Contains("ExitHintRequested"))
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
