using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Hygiene der View-Code-Behinds (.axaml.cs):
/// - Effektive Zeilenzahl (ohne Whitespace + Kommentare) → INFO ab 200, WARN ab 400
/// - Service-Felder (z.B. `IExpenseService _service;`) → INFO, Services gehoeren ins VM
/// - async void Event-Handler ohne try-catch → WARN (Android-Crash bei Exception)
/// - Direkte Model-Klassen-Felder → INFO (UI-State sollte ins VM)
///
/// Akzeptierte Patterns (kein Befund):
/// - SkiaSharp-Renderer + DispatcherTimer (Game/Background-Render-Loops)
/// - DataContextChanged + Event-Subscription/-Unsubscription (Cleanup-Pattern)
/// - OnAttached/OnDetached fuer Visual-Tree-Lifecycle
/// </summary>
class CodeBehindHygieneChecker : IChecker
{
    public string Category => "Code-Behind-Hygiene";

    static readonly Regex ServiceFieldRegex = new(
        @"^\s*(private|protected|internal)\s+(readonly\s+)?(I[A-Z]\w+(Service|Repository|Manager|Provider))\s+\w+\s*[=;]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex AsyncVoidHandlerRegex = new(
        @"private\s+async\s+void\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        var codeBehindFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.EndsWith(".axaml.cs"))
            .Where(f => Path.GetFileName(f.FullPath) != "App.axaml.cs")
            .ToList();

        if (codeBehindFiles.Count == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine View-Code-Behind-Dateien gefunden"));
            return results;
        }

        int largeCodeBehind = 0;
        int veryLargeCodeBehind = 0;
        int serviceFieldCount = 0;
        int asyncVoidNoTryCount = 0;
        int filesChecked = 0;

        foreach (var file in codeBehindFiles)
        {
            filesChecked++;
            var fileName = Path.GetFileName(file.FullPath);

            // 1. Effektive Zeilenzahl (ohne Whitespace + reine Kommentar-Zeilen)
            int effectiveLines = 0;
            foreach (var line in file.Lines)
            {
                var t = line.TrimStart();
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (t.StartsWith("//") || t.StartsWith("/*") || t.StartsWith("*")) continue;
                effectiveLines++;
            }

            if (effectiveLines >= 400)
            {
                veryLargeCodeBehind++;
                results.Add(new(Severity.Warn, Category,
                    $"{fileName} hat {effectiveLines} effektive Zeilen in {file.RelativePath} → Logik ins ViewModel oder in Behaviors auslagern"));
            }
            else if (effectiveLines >= 200)
            {
                largeCodeBehind++;
                results.Add(new(Severity.Info, Category,
                    $"{fileName} hat {effectiveLines} effektive Zeilen in {file.RelativePath} → ueberpruefen ob VM-Logik im Code-Behind landet"));
            }

            // 2. Service-Felder im Code-Behind (Service-Locator-Verdacht)
            var serviceMatches = ServiceFieldRegex.Matches(file.Content);
            foreach (Match m in serviceMatches)
            {
                var serviceType = m.Groups[3].Value;
                var lineNum = GetLineNumber(file.Content, m.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;

                serviceFieldCount++;
                results.Add(new(Severity.Info, Category,
                    $"{fileName} hat Service-Feld '{serviceType}' im Code-Behind in {file.RelativePath}:{lineNum} → Service per DI ins ViewModel injizieren"));
            }

            // 3. async void Handler ohne try-catch (Crash-Pattern)
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                var match = AsyncVoidHandlerRegex.Match(trimmed);
                if (!match.Success) continue;

                var methodName = match.Groups[1].Value;

                // Pruefen ob die naechsten ~8 Zeilen ein try-Block enthalten
                bool hasTry = false;
                for (int j = i + 1; j < Math.Min(i + 10, file.Lines.Length); j++)
                {
                    var nextT = file.Lines[j].TrimStart();
                    if (nextT.StartsWith("try")) { hasTry = true; break; }
                    if (nextT.StartsWith("}")) break; // Methoden-Ende erreicht
                }

                if (!hasTry)
                {
                    asyncVoidNoTryCount++;
                    results.Add(new(Severity.Warn, Category,
                        $"{fileName}: async void Handler '{methodName}' ohne try-catch in {file.RelativePath}:{i + 1} → ungefangene Exception crasht App"));
                }
            }
        }

        // Zusammenfassungen
        if (veryLargeCodeBehind == 0)
            results.Add(new(Severity.Pass, Category, "Keine Code-Behinds > 400 Zeilen (alle handhabbar)"));
        if (largeCodeBehind == 0 && veryLargeCodeBehind == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {filesChecked} Code-Behinds < 200 Zeilen"));
        if (serviceFieldCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine Service-Felder im View-Code-Behind"));
        if (asyncVoidNoTryCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle async void Handler im Code-Behind haben try-catch"));

        return results;
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
}
