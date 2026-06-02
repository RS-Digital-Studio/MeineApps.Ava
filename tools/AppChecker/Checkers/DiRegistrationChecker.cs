using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft DI-Registrierung: ConfigureServices, AppName-Konsistenz, MainViewModel,
/// Constructor-Cross-Check und seit Erweiterung auch ungenutzte Registrierungen
/// (Service registriert, aber kein Ctor-Parameter und kein GetService-Aufruf).
/// </summary>
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

        // Composition Root: entweder eine ConfigureServices(IServiceCollection)-Methode ODER
        // eine inline ServiceCollection + BuildServiceProvider() direkt in App.axaml.cs
        // (GardenControl verdrahtet DI inline in OnFrameworkInitializationCompleted).
        bool hasConfigureServices = Regex.IsMatch(content, @"void\s+ConfigureServices\s*\(\s*IServiceCollection");
        bool hasInlineComposition = Regex.IsMatch(content, @"new\s+ServiceCollection\s*\(") && content.Contains("BuildServiceProvider");
        if (hasConfigureServices)
            results.Add(new(Severity.Pass, Category, "ConfigureServices Methode vorhanden"));
        else if (hasInlineComposition)
            results.Add(new(Severity.Pass, Category, "DI-Composition-Root inline (ServiceCollection + BuildServiceProvider)"));
        else
        {
            results.Add(new(Severity.Fail, Category, "Composition Root fehlt (weder ConfigureServices(IServiceCollection) noch ServiceCollection + BuildServiceProvider)"));
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
        var (mainVmFile, _) = FileHelpers.GetMainViewModel(ctx);

        var diRegistrations = DiHelpers.ExtractDiRegistrations(content);

        if (mainVmFile != null)
        {
            var constructorParams = DiHelpers.ExtractConstructorVmParameters(mainVmFile.FullPath);
            foreach (var param in constructorParams)
            {
                if (diRegistrations.Contains(param))
                    results.Add(new(Severity.Pass, Category, $"Constructor-VM '{param}' im DI registriert"));
                else
                    results.Add(new(Severity.Warn, Category, $"Constructor-VM '{param}' NICHT im DI registriert"));
            }
        }

        // NEUE PRUEFUNG: Ungenutzte DI-Registrierungen
        CheckUnusedRegistrations(results, ctx, diRegistrations);

        return results;
    }

    void CheckUnusedRegistrations(List<CheckResult> results, CheckContext ctx, HashSet<string> diRegistrations)
    {
        if (diRegistrations.Count == 0) return;

        // Alle Ctor-Params + GetService<T>-Typen aus der gesamten Shared+Android-Codebase sammeln
        var allFiles = ctx.SharedCsFiles.Concat(ctx.AndroidCsFiles);
        var usedTypes = new HashSet<string>(DiHelpers.ExtractAllConstructorParameterTypes(allFiles));
        foreach (var t in DiHelpers.ExtractGetServiceTypes(allFiles))
            usedTypes.Add(t);

        // Allow-List: Typen die selbst-aufloesend sind (z.B. IServiceProvider) oder
        // ueber Factory-Pattern verwendet werden
        var alwaysUsed = new HashSet<string>
        {
            "IServiceProvider", "MainViewModel", "App",
            // Premium-Library registriert intern - wird nicht direkt im App-Code referenziert
            "IRewardedAdService", "IPurchaseService", "IAdService",
            "IFileShareService", "IUriLauncher",
            // Lokalisierung wird oft via App.axaml.cs-Hook genutzt
            "ILocalizationService", "IPreferencesService",
            // Cross-Promo, Trial werden teilweise nur ueber Premium-Lib genutzt
            "ITrialService", "ICrossPromoService"
        };

        var unused = diRegistrations
            .Where(t => !usedTypes.Contains(t))
            .Where(t => !alwaysUsed.Contains(t))
            // Interfaces, deren Impl-Klasse genutzt wird, koennten "unused" wirken - tolerieren
            .Where(t => !(t.StartsWith('I') && t.Length > 1 && char.IsUpper(t[1]) && usedTypes.Contains(t[1..])))
            .ToList();

        if (unused.Count == 0)
            results.Add(new(Severity.Pass, Category, "Alle DI-Registrierungen werden verwendet"));
        else
        {
            foreach (var t in unused.Take(10))
                results.Add(new(Severity.Info, Category, $"DI-Registrierung '{t}' wird nirgendwo per Ctor/GetService verwendet (evtl. toter Code oder Factory-Pattern)"));
            if (unused.Count > 10)
                results.Add(new(Severity.Info, Category, $"...und {unused.Count - 10} weitere potenziell ungenutzte Registrierungen"));
        }
    }
}
