using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Code-Qualitaet: Debug.WriteLine, Console.WriteLine, ungenutzte Exception-Variablen</summary>
class CodeQualityChecker : IChecker
{
    public string Category => "Code Quality";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int debugWriteLineCount = 0;
        int consoleWriteLineCount = 0;
        int unusedExCount = 0;

        foreach (var file in ctx.CsFiles)
        {
            // Console.WriteLine im Desktop-Programmcode (Program.cs / .Desktop) ist legitim
            bool isDesktopMain = file.FullPath.Contains(".Desktop")
                              && (Path.GetFileName(file.FullPath) == "Program.cs"
                                  || file.FullPath.EndsWith(".Desktop.cs"));
            // Server/Pi-Console-Apps: BingXBot.Server, GardenControl.Server haben echte Console-Output
            bool isServerApp = file.FullPath.Contains(".Server") && Path.GetFileName(file.FullPath) == "Program.cs";

            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();

                // Kommentare und Suppress ueberspringen
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // Debug.WriteLine — nicht melden wenn in #if DEBUG-Block
                // Hinweis: Debug.WriteLine ist [Conditional("DEBUG")] → in Release wegoptimiert.
                // Bleibt als INFO (Dev-Artefakt-Hinweis), nicht als WARN.
                if (trimmed.Contains("Debug.WriteLine") && !IsInsideDebugBlock(file.Lines, i))
                {
                    debugWriteLineCount++;
                    results.Add(new(Severity.Info, Category, $"Debug.WriteLine in {file.RelativePath}:{i + 1} (Conditional[DEBUG] - in Release wegoptimiert)"));
                }

                // Console.WriteLine (nicht in Desktop-Program.cs / Server-Program.cs)
                if (!isDesktopMain && !isServerApp && Regex.IsMatch(trimmed, @"\bConsole\.(WriteLine|Write|Error\.WriteLine)\b"))
                {
                    consoleWriteLineCount++;
                    results.Add(new(Severity.Warn, Category, $"Console.WriteLine in {file.RelativePath}:{i + 1} → ILogger oder Debug.WriteLine verwenden"));
                }

                // catch (Exception ex) mit ungenutztem ex
                if (Regex.IsMatch(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)"))
                {
                    var varMatch = Regex.Match(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)");
                    if (varMatch.Success)
                    {
                        var varName = varMatch.Groups[1].Value;

                        // 1. Prüfen ob `ex` im REST der gleichen Zeile vorkommt (Inline-catch)
                        var idxAfterCatch = trimmed.IndexOf(')', varMatch.Index) + 1;
                        var restOfLine = idxAfterCatch < trimmed.Length ? trimmed[idxAfterCatch..] : "";
                        bool used = Regex.IsMatch(restOfLine, $@"\b{Regex.Escape(varName)}\b");

                        // 2. Sonst Block-catch: bis zum schließenden } durchscannen, max 30 Zeilen
                        if (!used)
                        {
                            int depth = restOfLine.Count(c => c == '{') - restOfLine.Count(c => c == '}');
                            int scanLimit = Math.Min(i + 31, file.Lines.Length);
                            for (int j = i + 1; j < scanLimit && depth >= 0; j++)
                            {
                                var checkLine = file.Lines[j];
                                var checkTrim = checkLine.TrimStart();
                                if (checkTrim.StartsWith("//")) continue;

                                depth += checkLine.Count(c => c == '{') - checkLine.Count(c => c == '}');

                                if (Regex.IsMatch(checkLine, $@"\b{Regex.Escape(varName)}\b") && !checkLine.Contains("catch"))
                                {
                                    used = true;
                                    break;
                                }
                                if (depth < 0) break;
                            }
                        }

                        if (!used)
                        {
                            unusedExCount++;
                            results.Add(new(Severity.Warn, Category, $"Ungenutztes 'ex' in catch at {file.RelativePath}:{i + 1}"));
                        }
                    }
                }
            }
        }

        if (debugWriteLineCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine Debug.WriteLine ausserhalb #if DEBUG"));
        else
            results.Add(new(Severity.Info, Category, $"{debugWriteLineCount} Debug.WriteLine-Aufrufe ausserhalb #if DEBUG (wegoptimiert in Release, aber Aufraeum-Kandidaten)"));
        if (consoleWriteLineCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine Console.WriteLine in App-Code (Desktop/Server Program.cs ausgenommen)"));
        if (unusedExCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine ungenutzten Exception-Variablen"));

        return results;
    }

    /// <summary>Steht Zeile lineIndex innerhalb eines #if DEBUG / #endif Blocks?</summary>
    static bool IsInsideDebugBlock(string[] lines, int lineIndex)
    {
        // Rueckwaerts scannen: Welche Direktive war zuletzt aktiv?
        int debugDepth = 0;
        for (int i = lineIndex - 1; i >= 0; i--)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("#endif")) debugDepth--;
            else if (t.StartsWith("#if DEBUG") || t.StartsWith("#if (DEBUG"))
            {
                if (debugDepth >= 0) return true;
                debugDepth++;
            }
            else if (t.StartsWith("#if ") || t.StartsWith("#elif ") || t.StartsWith("#else"))
            {
                // anderes #if oder #else - kein DEBUG-Block aktiv
                if (debugDepth >= 0) return false;
            }
        }
        return false;
    }
}
