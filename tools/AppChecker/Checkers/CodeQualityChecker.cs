using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Code-Qualitaet: Debug.WriteLine, ungenutzte Exception-Variablen</summary>
class CodeQualityChecker : IChecker
{
    public string Category => "Code Quality";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int debugWriteLineCount = 0;
        int unusedExCount = 0;

        foreach (var file in ctx.CsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();

                // Kommentare und Suppress ueberspringen
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // Debug.WriteLine
                if (trimmed.Contains("Debug.WriteLine"))
                {
                    debugWriteLineCount++;
                    results.Add(new(Severity.Warn, Category, $"Debug.WriteLine in {file.RelativePath}:{i + 1}"));
                }

                // catch (Exception ex) mit ungenutztem ex
                if (Regex.IsMatch(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)"))
                {
                    var varMatch = Regex.Match(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)");
                    if (varMatch.Success)
                    {
                        var varName = varMatch.Groups[1].Value;
                        bool used = false;
                        for (int j = i + 1; j < Math.Min(i + 6, file.Lines.Length); j++)
                        {
                            var checkLine = file.Lines[j].TrimStart();
                            if (checkLine.StartsWith("//")) continue;
                            if (checkLine.Contains(varName) && !checkLine.Contains("catch"))
                            {
                                used = true;
                                break;
                            }
                            if (checkLine.TrimStart() == "}")
                                break;
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
            results.Add(new(Severity.Pass, Category, "Keine Debug.WriteLine Reste gefunden"));
        if (unusedExCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine ungenutzten Exception-Variablen"));

        return results;
    }
}
