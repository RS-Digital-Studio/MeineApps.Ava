namespace AppChecker.Checkers;

/// <summary>Prueft AndroidManifest, Permissions, Icons, Mipmap-Verzeichnisse, AdMob Lifecycle</summary>
class AndroidChecker : IChecker
{
    public string Category => "Android";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        var app = ctx.App;

        if (!Directory.Exists(ctx.AndroidDir))
        {
            results.Add(new(Severity.Fail, Category, "Android-Verzeichnis nicht gefunden"));
            return results;
        }

        // AndroidManifest.xml
        var manifestPath = Path.Combine(ctx.AndroidDir, "AndroidManifest.xml");
        if (File.Exists(manifestPath))
        {
            var content = File.ReadAllText(manifestPath);
            CheckManifest(results, app, content);
        }
        else
            results.Add(new(Severity.Fail, Category, "AndroidManifest.xml fehlt"));

        // Mipmap-Verzeichnisse
        CheckMipmaps(results, ctx.AndroidDir);

        // MainActivity.cs AdMob Lifecycle
        CheckAdMobLifecycle(results, ctx, app);

        return results;
    }

    void CheckManifest(List<CheckResult> results, AppDef app, string content)
    {
        // INTERNET Permission
        if (content.Contains("android.permission.INTERNET"))
            results.Add(new(Severity.Pass, Category, "INTERNET Permission vorhanden"));
        else
            results.Add(new(Severity.Fail, Category, "INTERNET Permission fehlt in AndroidManifest.xml"));

        // Icon und RoundIcon
        if (content.Contains("@mipmap/appicon"))
            results.Add(new(Severity.Pass, Category, "Icon auf @mipmap/appicon gesetzt"));
        else
            results.Add(new(Severity.Warn, Category, "android:icon nicht auf @mipmap/appicon gesetzt"));

        if (content.Contains("@mipmap/appicon_round"))
            results.Add(new(Severity.Pass, Category, "RoundIcon auf @mipmap/appicon_round gesetzt"));
        else
            results.Add(new(Severity.Warn, Category, "android:roundIcon fehlt"));

        // Ad-spezifische Checks
        if (app.IsAdSupported)
        {
            if (content.Contains("com.google.android.gms.ads.APPLICATION_ID"))
                results.Add(new(Severity.Pass, Category, "AdMob APPLICATION_ID meta-data vorhanden"));
            else
                results.Add(new(Severity.Fail, Category, "AdMob APPLICATION_ID meta-data fehlt (Ad-App!)"));
        }
        else
        {
            if (content.Contains("AD_ID") && content.Contains("tools:node=\"remove\""))
                results.Add(new(Severity.Pass, Category, "AD_ID Permission korrekt entfernt (tools:node=\"remove\")"));
            else
                results.Add(new(Severity.Warn, Category, "AD_ID Permission nicht explizit entfernt (Non-Ad-App)"));
        }
    }

    void CheckMipmaps(List<CheckResult> results, string androidDir)
    {
        var resourcesDir = Path.Combine(androidDir, "Resources");
        var expectedMipmaps = new[] { "mipmap-mdpi", "mipmap-hdpi", "mipmap-xhdpi", "mipmap-xxhdpi", "mipmap-xxxhdpi", "mipmap-anydpi-v26" };
        if (Directory.Exists(resourcesDir))
        {
            var missingMipmaps = expectedMipmaps.Where(m => !Directory.Exists(Path.Combine(resourcesDir, m))).ToList();
            if (missingMipmaps.Count == 0)
                results.Add(new(Severity.Pass, Category, "Alle 6 Mipmap-Verzeichnisse vorhanden"));
            else
                foreach (var m in missingMipmaps)
                    results.Add(new(Severity.Warn, Category, $"Mipmap-Verzeichnis fehlt: {m}"));

            // styles.xml
            var valuesDir = Path.Combine(resourcesDir, "values");
            if (Directory.Exists(valuesDir) && File.Exists(Path.Combine(valuesDir, "styles.xml")))
                results.Add(new(Severity.Pass, Category, "styles.xml vorhanden"));
            else
                results.Add(new(Severity.Warn, Category, "Resources/values/styles.xml fehlt"));
        }
        else
            results.Add(new(Severity.Fail, Category, "Resources-Verzeichnis fehlt"));
    }

    void CheckAdMobLifecycle(List<CheckResult> results, CheckContext ctx, AppDef app)
    {
        if (!app.IsAdSupported) return;

        var mainActivity = ctx.AndroidCsFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainActivity.cs");
        if (mainActivity == null) return;

        var content = mainActivity.Content;
        var hasInit = content.Contains("AdMobHelper.Initialize") || content.Contains("AdMobHelper");
        var hasResume = content.Contains("OnResume");
        var hasPause = content.Contains("OnPause");
        var hasDestroy = content.Contains("OnDestroy");

        if (hasInit && hasResume && hasPause && hasDestroy)
            results.Add(new(Severity.Pass, Category, "AdMob Lifecycle komplett (Init/Resume/Pause/Destroy)"));
        else
        {
            if (!hasInit) results.Add(new(Severity.Fail, Category, "AdMobHelper.Initialize fehlt in MainActivity"));
            if (!hasResume) results.Add(new(Severity.Warn, Category, "OnResume (AdMob) fehlt in MainActivity"));
            if (!hasPause) results.Add(new(Severity.Warn, Category, "OnPause (AdMob) fehlt in MainActivity"));
            if (!hasDestroy) results.Add(new(Severity.Warn, Category, "OnDestroy (AdMob) fehlt in MainActivity"));
        }
    }
}
