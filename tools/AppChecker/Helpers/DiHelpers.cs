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
        // sowie .AddSingleton<IService, Service>() (zweiter Typ = Impl)
        var matches = Regex.Matches(appAxamlCsContent, @"\.Add(Singleton|Transient|Scoped)<(\w+)(?:\s*,\s*(\w+))?>");
        foreach (Match m in matches)
        {
            types.Add(m.Groups[2].Value);
            if (m.Groups[3].Success && !string.IsNullOrWhiteSpace(m.Groups[3].Value))
                types.Add(m.Groups[3].Value);
        }

        return types;
    }

    /// <summary>
    /// Sammelt ALLE Konstruktor-Parameter-Typen (egal welche Klasse) aus der Shared-Codebase.
    /// Beruecksichtigt Generics wie Lazy&lt;T&gt;, IEnumerable&lt;T&gt; (extrahiert auch T).
    /// </summary>
    public static HashSet<string> ExtractAllConstructorParameterTypes(IEnumerable<CsFile> files)
    {
        var types = new HashSet<string>();
        // Mehrzeilen-faehig: \s matcht Newline, [\s\S]*? matcht alles inkl. Newline non-greedy
        var ctorRegex = new Regex(
            @"public\s+(\w+)\s*\(([\s\S]*?)\)",
            RegexOptions.Compiled);

        foreach (var file in files)
        {
            // Klassen-Namen aus public class XXX sammeln (auch partial)
            var classNames = Regex.Matches(file.Content, @"\b(?:partial\s+)?class\s+(\w+)\b")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToHashSet();

            foreach (Match ctor in ctorRegex.Matches(file.Content))
            {
                var name = ctor.Groups[1].Value;
                // Heuristik: nur echte Konstruktoren (Name == Klassenname in dieser Datei)
                // ODER der Name endet mit ViewModel/Service/Manager/Repository/View/Window (typische Klassen)
                bool looksLikeCtor = classNames.Contains(name)
                                    || Regex.IsMatch(name, @"(ViewModel|Service|Manager|Repository|View|Window|Activity|App)$");
                if (!looksLikeCtor) continue;

                var paramBlock = ctor.Groups[2].Value;
                if (string.IsNullOrWhiteSpace(paramBlock)) continue;

                ExtractParamTypes(paramBlock, types);
            }
        }
        return types;
    }

    /// <summary>Zerlegt einen Param-Block und extrahiert die Typ-Namen.</summary>
    static void ExtractParamTypes(string paramBlock, HashSet<string> types)
    {
        // Top-Level-Komma-Split (Generics nicht zerschneiden)
        var parameters = SplitTopLevelCommas(paramBlock);
        var typeRegex = new Regex(
            @"^\s*(?:\[[^\]]+\]\s*)*(?:params\s+|ref\s+|out\s+|in\s+|this\s+)?(?<outer>[A-Za-z_]\w*)(?:<\s*(?<inner>[A-Za-z_]\w*)\s*(?:,|>))?",
            RegexOptions.Compiled);

        foreach (var p in parameters)
        {
            var m = typeRegex.Match(p);
            if (!m.Success) continue;
            var outer = m.Groups["outer"].Value;
            var inner = m.Groups["inner"].Value;

            // Primitive ausfiltern
            if (IsPrimitive(outer)) continue;

            if (!string.IsNullOrEmpty(inner) && IsContainerType(outer))
                types.Add(inner);
            else
                types.Add(outer);
        }
    }

    static List<string> SplitTopLevelCommas(string input)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '<' || c == '(' || c == '[') depth++;
            else if (c == '>' || c == ')' || c == ']') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(input.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (start < input.Length)
            result.Add(input.Substring(start).Trim());
        return result;
    }

    static bool IsPrimitive(string typeName) => typeName switch
    {
        "string" or "int" or "long" or "short" or "byte" or "bool" or "char"
        or "double" or "float" or "decimal" or "object" or "void" or "uint"
        or "ulong" or "ushort" or "sbyte" or "nint" or "nuint" or "dynamic"
        or "String" or "Int32" or "Int64" or "Boolean" or "Object" => true,
        _ => false
    };

    /// <summary>
    /// Sammelt alle Typen, die per GetService/GetRequiredService aufgeloest werden.
    /// </summary>
    public static HashSet<string> ExtractGetServiceTypes(IEnumerable<CsFile> files)
    {
        var types = new HashSet<string>();
        var pattern = new Regex(
            @"\.Get(Required)?Service<(\w+)>",
            RegexOptions.Compiled);

        foreach (var file in files)
            foreach (Match m in pattern.Matches(file.Content))
                types.Add(m.Groups[2].Value);

        return types;
    }

    static bool IsContainerType(string typeName) => typeName switch
    {
        "Lazy" or "Func" or "IEnumerable" or "IReadOnlyList" or "IReadOnlyCollection"
        or "ICollection" or "IList" or "List" or "Task" or "ValueTask" => true,
        _ => false
    };
}
