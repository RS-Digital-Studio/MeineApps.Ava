using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft DateTime-Patterns: DateTime.Now, Parse ohne RoundtripKind, InvariantCulture</summary>
class DateTimeChecker : IChecker
{
    public string Category => "DateTime";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int dateTimeNowCount = 0;
        int parseNoRoundtripCount = 0;
        int parseNoInvariantCount = 0;

        foreach (var file in ctx.SharedCsFiles)
        {
            // .axaml.cs Dateien (Code-behind) fuer DateTime.Now ausgenommen
            bool isCodeBehind = file.FullPath.EndsWith(".axaml.cs");

            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // DateTime.Now in Services/VMs (nicht Code-behind)
                if (!isCodeBehind && trimmed.Contains("DateTime.Now") && !trimmed.Contains("DateTime.Now.Date"))
                {
                    // DateTime.Today ist OK, nur DateTime.Now prüfen
                    if (!trimmed.Contains("DateTime.Now.") || Regex.IsMatch(trimmed, @"DateTime\.Now[^.]"))
                    {
                        dateTimeNowCount++;
                        results.Add(new(Severity.Warn, Category, $"DateTime.Now statt UtcNow in {file.RelativePath}:{i + 1}"));
                    }
                }

                // DateTime.Parse ohne RoundtripKind
                if (trimmed.Contains("DateTime.Parse(") || trimmed.Contains("DateTime.Parse ("))
                {
                    var contextLines = string.Join(' ', file.Lines.Skip(i).Take(3));
                    if (!contextLines.Contains("RoundtripKind"))
                    {
                        parseNoRoundtripCount++;
                        results.Add(new(Severity.Warn, Category, $"DateTime.Parse ohne RoundtripKind in {file.RelativePath}:{i + 1}"));
                    }

                    // InvariantCulture Check
                    if (!contextLines.Contains("InvariantCulture"))
                    {
                        parseNoInvariantCount++;
                        results.Add(new(Severity.Info, Category, $"DateTime.Parse ohne InvariantCulture in {file.RelativePath}:{i + 1}"));
                    }
                }
            }
        }

        if (dateTimeNowCount == 0)
            results.Add(new(Severity.Pass, Category, "Kein DateTime.Now (UtcNow wird korrekt verwendet)"));
        if (parseNoRoundtripCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle DateTime.Parse mit RoundtripKind"));
        if (parseNoInvariantCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle DateTime.Parse mit InvariantCulture"));

        return results;
    }
}
