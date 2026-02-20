using System.Text.RegularExpressions;

namespace AppChecker.Helpers;

static class DiHelpers
{
    /// <summary>Extrahiert ViewModel-Parameter aus dem MainViewModel Constructor</summary>
    public static HashSet<string> ExtractConstructorVmParameters(string filePath)
    {
        var vmParams = new HashSet<string>();
        var content = File.ReadAllText(filePath);

        // Constructor finden: public MainViewModel(...)
        var ctorMatch = Regex.Match(content, @"public\s+MainViewModel\s*\(([\s\S]*?)\)", RegexOptions.Multiline);
        if (!ctorMatch.Success) return vmParams;

        var paramBlock = ctorMatch.Groups[1].Value;
        // Alle ViewModel-Parameter extrahieren
        var paramMatches = Regex.Matches(paramBlock, @"(\w+ViewModel)\s+\w+");
        foreach (Match m in paramMatches)
            vmParams.Add(m.Groups[1].Value);

        return vmParams;
    }

    /// <summary>Extrahiert DI-registrierte Typen aus App.axaml.cs Inhalt</summary>
    public static HashSet<string> ExtractDiRegistrations(string appAxamlCsContent)
    {
        var types = new HashSet<string>();

        // services.AddSingleton<Type>() oder services.AddTransient<Type>()
        var matches = Regex.Matches(appAxamlCsContent, @"\.Add(Singleton|Transient|Scoped)<(\w+)>");
        foreach (Match m in matches)
            types.Add(m.Groups[2].Value);

        // Factory-Pattern: services.AddTransient<Type>(sp => ...)
        var factoryMatches = Regex.Matches(appAxamlCsContent, @"\.Add(Singleton|Transient|Scoped)<(\w+)>\s*\(");
        foreach (Match m in factoryMatches)
            types.Add(m.Groups[2].Value);

        return types;
    }
}
