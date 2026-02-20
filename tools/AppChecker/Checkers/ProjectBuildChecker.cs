using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Projektstruktur, csproj-Einstellungen, Versionen und RuntimeIdentifiers</summary>
class ProjectBuildChecker : IChecker
{
    public string Category => "Projekt/Build";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        var app = ctx.App;

        // 3 Projekte existieren
        var sharedExists = Directory.Exists(ctx.SharedDir);
        var androidExists = Directory.Exists(ctx.AndroidDir);
        var desktopExists = Directory.Exists(ctx.DesktopDir);
        if (sharedExists && androidExists && desktopExists)
            results.Add(new(Severity.Pass, Category, "3 Projekte vorhanden (Shared, Android, Desktop)"));
        else
        {
            if (!sharedExists) results.Add(new(Severity.Fail, Category, $"{app.Name}.Shared Verzeichnis fehlt"));
            if (!androidExists) results.Add(new(Severity.Fail, Category, $"{app.Name}.Android Verzeichnis fehlt"));
            if (!desktopExists) results.Add(new(Severity.Fail, Category, $"{app.Name}.Desktop Verzeichnis fehlt"));
            return results;
        }

        // Android .csproj pruefen
        var androidCsproj = Path.Combine(ctx.AndroidDir, $"{app.Name}.Android.csproj");
        if (File.Exists(androidCsproj))
        {
            var content = File.ReadAllText(androidCsproj);
            CheckAndroidCsproj(results, app, content);
        }
        else
            results.Add(new(Severity.Fail, Category, $"{app.Name}.Android.csproj fehlt"));

        // Shared .csproj pruefen
        var sharedCsproj = Path.Combine(ctx.SharedDir, $"{app.Name}.Shared.csproj");
        if (File.Exists(sharedCsproj))
        {
            var content = File.ReadAllText(sharedCsproj);
            CheckSharedCsproj(results, app, content);
        }
        else
            results.Add(new(Severity.Fail, Category, $"{app.Name}.Shared.csproj fehlt"));

        // TargetFramework in Android-csproj
        if (File.Exists(androidCsproj))
        {
            var content = File.ReadAllText(androidCsproj);
            var tfmMatch = Regex.Match(content, @"<TargetFramework>(.*?)</TargetFramework>");
            if (tfmMatch.Success)
            {
                var tfm = tfmMatch.Groups[1].Value;
                if (tfm.Contains("android"))
                    results.Add(new(Severity.Pass, Category, $"TargetFramework: {tfm}"));
                else
                    results.Add(new(Severity.Fail, Category, $"TargetFramework '{tfm}' enthält kein 'android'"));
            }
        }

        return results;
    }

    void CheckAndroidCsproj(List<CheckResult> results, AppDef app, string content)
    {
        // ApplicationId
        var appIdMatch = Regex.Match(content, @"<ApplicationId>(.*?)</ApplicationId>");
        if (appIdMatch.Success)
        {
            var appId = appIdMatch.Groups[1].Value;
            if (appId == app.ExpectedAppId)
                results.Add(new(Severity.Pass, Category, $"ApplicationId: {appId}"));
            else
                results.Add(new(Severity.Warn, Category, $"ApplicationId '{appId}' erwartet '{app.ExpectedAppId}'"));

            if (!Regex.IsMatch(appId, @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$"))
                results.Add(new(Severity.Warn, Category, $"ApplicationId '{appId}' enthaelt ungueltige Zeichen"));
        }
        else
            results.Add(new(Severity.Fail, Category, "ApplicationId fehlt in Android.csproj"));

        // ApplicationVersion (muss int sein)
        var versionMatch = Regex.Match(content, @"<ApplicationVersion>(.*?)</ApplicationVersion>");
        if (versionMatch.Success)
        {
            if (int.TryParse(versionMatch.Groups[1].Value, out var ver) && ver > 0)
                results.Add(new(Severity.Pass, Category, $"ApplicationVersion: {ver}"));
            else
                results.Add(new(Severity.Fail, Category, $"ApplicationVersion '{versionMatch.Groups[1].Value}' ist keine gueltige Ganzzahl"));
        }
        else
            results.Add(new(Severity.Fail, Category, "ApplicationVersion fehlt in Android.csproj"));

        // ApplicationDisplayVersion (semver-like)
        var displayVerMatch = Regex.Match(content, @"<ApplicationDisplayVersion>(.*?)</ApplicationDisplayVersion>");
        if (displayVerMatch.Success)
        {
            var dv = displayVerMatch.Groups[1].Value;
            if (Regex.IsMatch(dv, @"^\d+\.\d+\.\d+$"))
                results.Add(new(Severity.Pass, Category, $"ApplicationDisplayVersion: {dv}"));
            else
                results.Add(new(Severity.Warn, Category, $"ApplicationDisplayVersion '{dv}' ist kein Semver-Format"));
        }
        else
            results.Add(new(Severity.Fail, Category, "ApplicationDisplayVersion fehlt in Android.csproj"));

        // RuntimeIdentifiers
        var rtiMatch = Regex.Match(content, @"<RuntimeIdentifiers>(.*?)</RuntimeIdentifiers>");
        if (rtiMatch.Success)
        {
            var rti = rtiMatch.Groups[1].Value;
            var hasArm64 = rti.Contains("android-arm64");
            var hasX64 = rti.Contains("android-x64");
            if (hasArm64 && hasX64)
                results.Add(new(Severity.Pass, Category, $"RuntimeIdentifiers: {rti}"));
            else
            {
                if (!hasArm64) results.Add(new(Severity.Fail, Category, "android-arm64 fehlt in RuntimeIdentifiers"));
                if (!hasX64) results.Add(new(Severity.Warn, Category, "android-x64 fehlt in RuntimeIdentifiers"));
            }
        }
        else
            results.Add(new(Severity.Fail, Category, "RuntimeIdentifiers fehlt in Android.csproj"));
    }

    void CheckSharedCsproj(List<CheckResult> results, AppDef app, string content)
    {
        // RootNamespace
        var nsMatch = Regex.Match(content, @"<RootNamespace>(.*?)</RootNamespace>");
        if (nsMatch.Success)
        {
            if (nsMatch.Groups[1].Value == app.Name)
                results.Add(new(Severity.Pass, Category, $"RootNamespace: {app.Name}"));
            else
                results.Add(new(Severity.Warn, Category, $"RootNamespace '{nsMatch.Groups[1].Value}' erwartet '{app.Name}'"));
        }
        else
            results.Add(new(Severity.Info, Category, "RootNamespace nicht explizit gesetzt (Default wird verwendet)"));

        // AvaloniaResource Include="Assets\**"
        if (content.Contains("AvaloniaResource") && content.Contains("Assets"))
            results.Add(new(Severity.Pass, Category, "AvaloniaResource Include Assets vorhanden"));
        else
            results.Add(new(Severity.Fail, Category, "AvaloniaResource Include=\"Assets\\**\" fehlt in Shared.csproj"));
    }
}
