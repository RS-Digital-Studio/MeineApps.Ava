using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Service-Konventionen:
/// - Service-Klasse `{Name}Service` hat ein Interface `I{Name}Service`?
/// - Klasse implementiert dieses Interface tatsaechlich?
/// - Async-Methoden enden mit "Async"-Suffix
/// - Lifetime-Konsistenz: nur ein Add-Singleton/Add-Transient pro Service-Typ (kein Doppel-Reg)
/// </summary>
class ServiceConventionChecker : IChecker
{
    public string Category => "Service-Convention";

    static readonly Regex ClassDeclRegex = new(
        @"^\s*(?:public|internal|sealed|partial|\s)+\s*class\s+(?<name>\w+Service)\b(?<inh>[^{]*)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex AsyncMethodRegex = new(
        @"^\s*(?:public|protected|internal|private)\s+(?:override\s+|virtual\s+|static\s+)*async\s+(?:Task|ValueTask)(?:<[^>]+>)?\s+(?<name>\w+)\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int servicesChecked = 0;
        int missingInterface = 0;
        int interfaceNotImplemented = 0;
        int asyncMissingSuffix = 0;
        int duplicateRegistration = 0;

        var serviceFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("Services") && f.FullPath.EndsWith("Service.cs")
                     && !Path.GetFileName(f.FullPath).StartsWith("I"))
            .ToList();

        var interfaceFiles = ctx.SharedCsFiles
            .Where(f => Path.GetFileName(f.FullPath).StartsWith("I")
                     && f.FullPath.EndsWith("Service.cs"))
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f.FullPath), f => f, StringComparer.Ordinal);

        // === 1. Interface-Konvention pro Service ===
        foreach (var file in serviceFiles)
        {
            servicesChecked++;
            var className = Path.GetFileNameWithoutExtension(file.FullPath);
            var expectedInterface = "I" + className;

            var classMatch = ClassDeclRegex.Match(file.Content);
            if (!classMatch.Success) continue;

            var inheritance = classMatch.Groups["inh"].Value;
            bool implementsInterface = Regex.IsMatch(inheritance, $@"\b{Regex.Escape(expectedInterface)}\b");
            bool interfaceFileExists = interfaceFiles.ContainsKey(expectedInterface);

            if (!interfaceFileExists)
            {
                missingInterface++;
                results.Add(new(Severity.Info, Category,
                    $"{className} hat KEIN passendes Interface {expectedInterface}.cs in {file.RelativePath}"));
            }
            else if (!implementsInterface)
            {
                interfaceNotImplemented++;
                results.Add(new(Severity.Warn, Category,
                    $"{className} implementiert {expectedInterface} NICHT (Interface existiert) in {file.RelativePath}"));
            }
        }

        // === 2. Async-Methoden-Naming (in Services + ViewModels + Helpers) ===
        var asyncCandidates = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("Services") || f.FullPath.Contains("ViewModels"))
            .ToList();

        foreach (var file in asyncCandidates)
        {
            foreach (Match m in AsyncMethodRegex.Matches(file.Content))
            {
                var methodName = m.Groups["name"].Value;
                if (methodName.EndsWith("Async", StringComparison.Ordinal)) continue;

                // Manche Patterns sind explizit erlaubt: Lifecycle-Methods
                if (IsAllowedAsyncMethodName(methodName)) continue;

                asyncMissingSuffix++;
                var lineNum = GetLineNumber(file.Content, m.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                if (asyncMissingSuffix <= 10)
                    results.Add(new(Severity.Info, Category,
                        $"async Methode '{methodName}' ohne 'Async'-Suffix in {file.RelativePath}:{lineNum}"));
            }
        }

        // === 3. Lifetime-Doppel-Registrierung (gleicher Service-Typ 2x mit unterschiedlichem Lifetime) ===
        var appAxamlCs = ctx.SharedCsFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "App.axaml.cs");
        if (appAxamlCs != null)
        {
            var regMatches = Regex.Matches(appAxamlCs.Content,
                @"\.Add(?<lifetime>Singleton|Transient|Scoped)<(?<iface>\w+)(?:\s*,\s*(?<impl>\w+))?>");
            var regs = regMatches.Cast<Match>()
                .Select(m => new { Lifetime = m.Groups["lifetime"].Value, Type = m.Groups["iface"].Value })
                .GroupBy(r => r.Type)
                .Where(g => g.Select(r => r.Lifetime).Distinct().Count() > 1)
                .ToList();

            foreach (var dup in regs)
            {
                duplicateRegistration++;
                results.Add(new(Severity.Warn, Category,
                    $"{dup.Key} mehrfach mit unterschiedlichen Lifetimes registriert: {string.Join(", ", dup.Select(r => r.Lifetime).Distinct())}"));
            }
        }

        // === Zusammenfassungen ===
        if (servicesChecked == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine konkreten Service-Klassen gefunden"));
            return results;
        }

        if (missingInterface == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {servicesChecked} Services haben passende I{{Name}}Service-Interfaces"));
        if (interfaceNotImplemented == 0)
            results.Add(new(Severity.Pass, Category, "Alle Services implementieren ihre I-Interfaces"));
        if (asyncMissingSuffix == 0)
            results.Add(new(Severity.Pass, Category, "Alle async-Methoden enden mit 'Async'"));
        else if (asyncMissingSuffix > 10)
            results.Add(new(Severity.Info, Category, $"...und {asyncMissingSuffix - 10} weitere async-Methoden ohne 'Async'-Suffix"));
        if (duplicateRegistration == 0)
            results.Add(new(Severity.Pass, Category, "Keine Doppel-Registrierungen mit unterschiedlichem Lifetime"));

        return results;
    }

    static bool IsAllowedAsyncMethodName(string name) => name switch
    {
        "Start" or "Stop" or "Pause" or "Resume" => true,         // Lifecycle
        "Initialize" or "Dispose" or "DisposeAsync" => true,      // Init/Cleanup
        "Main" or "Run" or "Execute" => true,                      // Entry-Points
        "Invoke" or "InvokeAsync" => true,                         // Delegate-Invocation
        _ => false
    };

    static int GetLineNumber(string content, int offset)
    {
        if (offset < 0) offset = 0;
        if (offset > content.Length) offset = content.Length;
        int line = 1;
        for (int i = 0; i < offset; i++)
            if (content[i] == '\n') line++;
        return line;
    }
}
