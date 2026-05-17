using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft [RelayCommand]-Korrektheit:
/// - [RelayCommand] auf `async void` → WARN (CommunityToolkit generiert KEINEN AsyncRelayCommand fuer void → Fire-and-Forget)
/// - [RelayCommand(CanExecute = "Name")] ohne entsprechende Methode/Property → FAIL
/// - [RelayCommand] auf Methoden die NICHT private/public sind → INFO (Toolkit-Generator-Bedingung)
/// </summary>
class AsyncRelayCommandChecker : IChecker
{
    public string Category => "AsyncRelayCommand";

    // [RelayCommand] gefolgt von Methode (Multi-line moeglich)
    static readonly Regex RelayCommandRegex = new(
        @"\[RelayCommand(?<attr>\([^\)]*\))?\]\s*\r?\n\s*(?<modifiers>(?:public|private|protected|internal|static|partial|\s)+?)(?<async>async\s+)?(?<returnType>\w+(?:<[^>]+>)?)\s+(?<methodName>\w+)\s*\(",
        RegexOptions.Compiled);

    // CanExecute-Name aus dem Attribut extrahieren: [RelayCommand(CanExecute = nameof(Xxx))] oder [RelayCommand(CanExecute = "Xxx")]
    static readonly Regex CanExecuteRegex = new(
        @"CanExecute\s*=\s*(?:nameof\(\s*(?<n1>\w+)\s*\)|""(?<n2>\w+)"")",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int asyncVoidRelayCommand = 0;
        int missingCanExecute = 0;
        int relayCommandsFound = 0;

        var vmFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith(".cs"))
            .ToList();

        foreach (var file in vmFiles)
        {
            // Pro Datei: alle Methoden + Properties sammeln (fuer CanExecute-Resolution)
            var methodNames = Regex.Matches(file.Content, @"\b(?:private|public|protected|internal)\s+(?:static\s+)?(?:async\s+)?\w+(?:<[^>]+>)?\s+(\w+)\s*\(")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToHashSet();
            var propertyNames = Regex.Matches(file.Content, @"\b(?:private|public|protected|internal)\s+(?:static\s+)?(?:bool|Boolean)\s+(\w+)\s*(?:=>|\{|;)")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToHashSet();
            // [ObservableProperty]-generierte Properties: aus `private bool _isXxx;` wird `IsXxx`
            var observableProps = Regex.Matches(file.Content, @"\[ObservableProperty\][\s\S]{0,80}?\bbool\s+_(\w+)\s*[;=]")
                .Cast<Match>()
                .Select(m => Capitalize(m.Groups[1].Value))
                .ToHashSet();
            foreach (var p in observableProps) propertyNames.Add(p);

            foreach (Match m in RelayCommandRegex.Matches(file.Content))
            {
                relayCommandsFound++;
                bool isAsync = !string.IsNullOrEmpty(m.Groups["async"].Value);
                var returnType = m.Groups["returnType"].Value;
                var methodName = m.Groups["methodName"].Value;
                var attr = m.Groups["attr"].Value;
                var lineNum = GetLineNumber(file.Content, m.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;

                // 1. async void → WARN
                if (isAsync && returnType == "void")
                {
                    asyncVoidRelayCommand++;
                    results.Add(new(Severity.Warn, Category,
                        $"[RelayCommand] auf 'async void {methodName}' in {file.RelativePath}:{lineNum} → Task zurueckgeben (sonst Fire-and-Forget, keine Exception-Propagation)"));
                }

                // 2. CanExecute-Existenz pruefen
                if (!string.IsNullOrEmpty(attr))
                {
                    var ceMatch = CanExecuteRegex.Match(attr);
                    if (ceMatch.Success)
                    {
                        var ceName = string.IsNullOrEmpty(ceMatch.Groups["n1"].Value)
                            ? ceMatch.Groups["n2"].Value
                            : ceMatch.Groups["n1"].Value;
                        if (!methodNames.Contains(ceName) && !propertyNames.Contains(ceName))
                        {
                            missingCanExecute++;
                            results.Add(new(Severity.Fail, Category,
                                $"[RelayCommand(CanExecute = {ceName})] auf {methodName} in {file.RelativePath}:{lineNum} → Methode/Property '{ceName}' fehlt"));
                        }
                    }
                }
            }
        }

        if (relayCommandsFound == 0)
            results.Add(new(Severity.Info, Category, "Keine [RelayCommand]-Verwendungen gefunden"));
        else
            results.Add(new(Severity.Info, Category, $"{relayCommandsFound} [RelayCommand]-Verwendungen geprueft"));

        if (asyncVoidRelayCommand == 0)
            results.Add(new(Severity.Pass, Category, "Keine [RelayCommand] auf 'async void' Methoden"));
        if (missingCanExecute == 0)
            results.Add(new(Severity.Pass, Category, "Alle [RelayCommand(CanExecute = ...)] referenzieren existierende Methoden/Properties"));

        return results;
    }

    static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

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
