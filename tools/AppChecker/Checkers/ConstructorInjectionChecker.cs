using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Constructor-Injection in ViewModels UND Services:
/// - Klasse mit NUR parameterlosem Ctor → WARN (vermutlich kein DI, Service-Locator-Verdacht)
/// - Klasse OHNE Ctor (Default) → INFO (nur fuer Concrete Classes — Interfaces/abstract werden uebersprungen)
/// - Ctor-Body mit "new SomeService()" → WARN (Hard-Dependency umgeht DI)
/// - Ctor-Body mit ".Instance" Property-Zugriff auf Service-Singletons → INFO (Service-Locator)
///
/// Ausgeschlossen:
/// - Interfaces (haben per Definition keinen Ctor)
/// - Abstract Classes (haben oft protected default-Ctor als API-Schutz)
/// - Static Classes (haben keinen Instance-Ctor)
/// - Designer-Ctors: parameterloser Ctor + zweiter Ctor mit Parametern → Pattern fuer Design-Mode-Preview, OK
///
/// Pueft Files: Shared/ViewModels/*ViewModel.cs UND Shared/Services/*Service.cs
/// </summary>
class ConstructorInjectionChecker : IChecker
{
    public string Category => "Constructor-Injection";

    static readonly Regex CtorRegex = new(
        @"^\s*public\s+(\w+)\s*\(([\s\S]*?)\)\s*(?::|$|\{)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Klassen-/Interface-Deklaration mit Modifier-Erkennung
    static readonly Regex TypeDeclRegex = new(
        @"^\s*(?<modifiers>(?:public|internal|sealed|partial|abstract|static|\s)+?)\s*(?<kind>class|interface|record|struct)\s+(?<name>\w+)\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex NewServiceRegex = new(
        @"=\s*new\s+(\w+(?:Service|Repository|Manager|Provider|Client|Helper))\s*\(",
        RegexOptions.Compiled);

    static readonly Regex InstanceAccessRegex = new(
        @"\b(\w+)\.Instance\b",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int classesParameterless = 0;
        int classesNoExplicitCtor = 0;
        int classesNewServiceInCtor = 0;
        int classesInstanceAccessInCtor = 0;
        int classesChecked = 0;

        // ViewModels UND Services pruefen
        var candidates = ctx.SharedCsFiles
            .Where(f =>
            {
                var path = f.FullPath;
                return (path.Contains("ViewModels") && path.EndsWith("ViewModel.cs"))
                    || (path.Contains("Services") && path.EndsWith("Service.cs"));
            })
            .ToList();

        foreach (var file in candidates)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.FullPath);
            var content = file.Content;

            // Type-Deklarations sammeln (in Reihenfolge des Vorkommens)
            var typeDecls = TypeDeclRegex.Matches(content).Cast<Match>().ToList();
            if (typeDecls.Count == 0) continue;

            // Nur die "Haupt-Klasse" pro Datei pruefen (die mit Dateiname)
            var primary = typeDecls.FirstOrDefault(m => m.Groups["name"].Value == fileName);
            if (primary == null) continue;

            var kind = primary.Groups["kind"].Value;
            var modifiers = primary.Groups["modifiers"].Value;
            var className = primary.Groups["name"].Value;

            // Interface/Record/Struct ausschliessen (haben andere Ctor-Semantik)
            if (kind == "interface" || kind == "record" || kind == "struct") continue;
            // Static Classes ausschliessen
            if (modifiers.Contains("static")) continue;
            // Abstract Classes: nur INFO bei missing-Ctor (kein Service-Locator-Verdacht)
            bool isAbstract = modifiers.Contains("abstract");

            classesChecked++;

            // Alle Ctors dieser Klasse sammeln (Name == className)
            var ctorMatches = CtorRegex.Matches(content)
                .Where(m => m.Groups[1].Value == className)
                .ToList();

            if (ctorMatches.Count == 0)
            {
                classesNoExplicitCtor++;
                var hint = isAbstract ? "abstract — Subklasse kuemmert sich um DI" : "vermutlich kein DI";
                results.Add(new(Severity.Info, Category,
                    $"{className} hat keinen expliziten Konstruktor in {file.RelativePath} → Default-Ctor parameterlos, {hint}"));
                continue;
            }

            bool hasParameterless = ctorMatches.Any(m => string.IsNullOrWhiteSpace(m.Groups[2].Value));
            bool hasParametrized = ctorMatches.Any(m => !string.IsNullOrWhiteSpace(m.Groups[2].Value));

            // NUR parameterloser Ctor und nicht abstract: kritisch
            if (hasParameterless && !hasParametrized && !isAbstract)
            {
                classesParameterless++;
                results.Add(new(Severity.Warn, Category,
                    $"{className} hat NUR parameterlosen Konstruktor in {file.RelativePath} → kein DI moeglich, Service-Locator-Verdacht"));
            }

            // Ctor-Bodys auf "new XxxService()" und ".Instance" pruefen
            foreach (var ctor in ctorMatches)
            {
                var ctorStart = ctor.Index + ctor.Length;
                var ctorBody = ExtractMethodBody(content, ctorStart);
                if (string.IsNullOrEmpty(ctorBody)) continue;

                foreach (Match m in NewServiceRegex.Matches(ctorBody))
                {
                    var serviceName = m.Groups[1].Value;
                    if (IsAllowedNewInstantiation(serviceName)) continue;

                    classesNewServiceInCtor++;
                    var lineNum = GetLineNumber(content, ctorStart + m.Index);
                    if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                    results.Add(new(Severity.Warn, Category,
                        $"{className} instanziiert 'new {serviceName}()' im Ctor in {file.RelativePath}:{lineNum} → Hard-Dependency, Service per DI injizieren"));
                }

                foreach (Match m in InstanceAccessRegex.Matches(ctorBody))
                {
                    var typeName = m.Groups[1].Value;
                    if (IsAllowedInstanceAccess(typeName)) continue;

                    classesInstanceAccessInCtor++;
                    var lineNum = GetLineNumber(content, ctorStart + m.Index);
                    if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                    results.Add(new(Severity.Info, Category,
                        $"{className} greift auf '{typeName}.Instance' im Ctor zu in {file.RelativePath}:{lineNum} → Service-Locator-Pattern, DI bevorzugen"));
                }
            }
        }

        // Zusammenfassungen
        if (classesChecked == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine ViewModels/Services gefunden"));
            return results;
        }

        if (classesParameterless == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {classesChecked} Klassen (VMs/Services) haben Constructor-Injection oder Designer-Ctor"));
        if (classesNoExplicitCtor == 0)
            results.Add(new(Severity.Pass, Category, $"Alle Konkret-Klassen haben expliziten Konstruktor (Interfaces/abstract uebersprungen)"));
        if (classesNewServiceInCtor == 0)
            results.Add(new(Severity.Pass, Category, "Keine 'new XxxService()' Hard-Dependencies in Konstruktoren"));
        if (classesInstanceAccessInCtor == 0)
            results.Add(new(Severity.Pass, Category, "Keine '.Instance'-Service-Locator-Zugriffe in Konstruktoren"));

        return results;
    }

    static string ExtractMethodBody(string content, int startIndex)
    {
        int braceStart = content.IndexOf('{', startIndex);
        if (braceStart < 0) return string.Empty;
        int depth = 1;
        int pos = braceStart + 1;
        while (pos < content.Length && depth > 0)
        {
            char c = content[pos];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            pos++;
        }
        if (depth != 0) return string.Empty;
        return content.Substring(braceStart, pos - braceStart);
    }

    static int GetLineNumber(string content, int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > content.Length) offset = content.Length;
        int line = 1;
        for (int i = 0; i < offset; i++)
            if (content[i] == '\n') line++;
        return line;
    }

    static bool IsAllowedNewInstantiation(string typeName) => typeName switch
    {
        "BackPressHelper" => true,
        "Random" => true,
        "Stopwatch" => true,
        "PerformanceTimer" => true,
        _ => false
    };

    static bool IsAllowedInstanceAccess(string typeName) => typeName switch
    {
        "Random" => true,
        "Stopwatch" => true,
        "Dispatcher" => true,
        "Avalonia" => true,
        "App" => true,
        "Process" => true,
        "Thread" => true,
        "Environment" => true,
        _ => false
    };
}
