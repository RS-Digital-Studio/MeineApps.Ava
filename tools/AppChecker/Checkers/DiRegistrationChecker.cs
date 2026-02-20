using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft DI-Registrierung: ConfigureServices, Services, MainViewModel, Constructor Cross-Check</summary>
class DiRegistrationChecker : IChecker
{
    public string Category => "DI-Registrierung";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        var app = ctx.App;

        var appAxamlCs = ctx.SharedCsFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml.cs");
        if (appAxamlCs == null)
        {
            results.Add(new(Severity.Fail, Category, "App.axaml.cs fehlt"));
            return results;
        }

        var content = appAxamlCs.Content;

        // ConfigureServices Methode
        if (Regex.IsMatch(content, @"void\s+ConfigureServices\s*\(\s*IServiceCollection"))
            results.Add(new(Severity.Pass, Category, "ConfigureServices Methode vorhanden"));
        else
        {
            results.Add(new(Severity.Fail, Category, "ConfigureServices Methode fehlt"));
            return results;
        }

        // IPreferencesService mit korrektem AppName
        var prefsMatch = Regex.Match(content, @"new\s+PreferencesService\s*\(\s*""(\w+)""\s*\)");
        if (prefsMatch.Success)
        {
            if (prefsMatch.Groups[1].Value == app.Name)
                results.Add(new(Severity.Pass, Category, $"IPreferencesService mit AppName '{app.Name}'"));
            else
                results.Add(new(Severity.Warn, Category, $"PreferencesService AppName '{prefsMatch.Groups[1].Value}' erwartet '{app.Name}'"));
        }
        else
            results.Add(new(Severity.Warn, Category, "IPreferencesService Registrierung nicht gefunden"));

        // IThemeService
        if (Regex.IsMatch(content, @"AddSingleton<IThemeService"))
            results.Add(new(Severity.Pass, Category, "IThemeService registriert"));
        else
            results.Add(new(Severity.Fail, Category, "IThemeService nicht im DI registriert"));

        // ILocalizationService mit AppStrings.ResourceManager
        if (content.Contains("AppStrings.ResourceManager"))
            results.Add(new(Severity.Pass, Category, "ILocalizationService mit AppStrings.ResourceManager"));
        else
            results.Add(new(Severity.Fail, Category, "ILocalizationService/AppStrings.ResourceManager nicht gefunden"));

        // Ad-Apps: AddMeineAppsPremium()
        if (app.IsAdSupported)
        {
            if (content.Contains("AddMeineAppsPremium"))
                results.Add(new(Severity.Pass, Category, "AddMeineAppsPremium() vorhanden (Ad-App)"));
            else
                results.Add(new(Severity.Fail, Category, "AddMeineAppsPremium() fehlt (Ad-App!)"));
        }

        // MainViewModel registriert
        if (Regex.IsMatch(content, @"Add(Singleton|Transient)<MainViewModel>"))
            results.Add(new(Severity.Pass, Category, "MainViewModel im DI registriert"));
        else
            results.Add(new(Severity.Fail, Category, "MainViewModel nicht im DI registriert"));

        // Cross-Check: MainVM Constructor-Parameter vs. DI-Registrierungen
        var mainVmFile = ctx.SharedCsFiles.FirstOrDefault(f =>
            f.FullPath.EndsWith("MainViewModel.cs") && f.FullPath.Contains("ViewModels"));
        if (mainVmFile != null)
        {
            var constructorParams = DiHelpers.ExtractConstructorVmParameters(mainVmFile.FullPath);
            var diRegistrations = DiHelpers.ExtractDiRegistrations(content);

            foreach (var param in constructorParams)
            {
                if (diRegistrations.Contains(param))
                    results.Add(new(Severity.Pass, Category, $"Constructor-VM '{param}' im DI registriert"));
                else
                    results.Add(new(Severity.Warn, Category, $"Constructor-VM '{param}' NICHT im DI registriert"));
            }
        }

        return results;
    }
}
