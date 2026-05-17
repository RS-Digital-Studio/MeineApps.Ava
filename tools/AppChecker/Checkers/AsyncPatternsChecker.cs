using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Async-Patterns: async void, try-catch, fire-and-forget, leere catch-Bloecke</summary>
class AsyncPatternsChecker : IChecker
{
    public string Category => "Async-Patterns";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int asyncVoidCount = 0;
        int fireAndForgetCount = 0;
        int emptyCatchCount = 0;

        foreach (var file in ctx.SharedCsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // async void (ausser Event-Handler die mit On beginnen oder _Xxx Eventhandler)
                if (Regex.IsMatch(trimmed, @"\basync\s+void\s+(\w+)"))
                {
                    var methodMatch = Regex.Match(trimmed, @"\basync\s+void\s+(\w+)");
                    var methodName = methodMatch.Groups[1].Value;

                    // Event-Handler ausgenommen (On*, *_Click, *_Tapped, *_Changed, *EventHandler)
                    bool isEventHandler = methodName.StartsWith("On")
                        || methodName.Contains("_")
                        || methodName.EndsWith("Handler")
                        || methodName.EndsWith("Callback");

                    if (!isEventHandler)
                    {
                        // Prüfen ob try-catch vorhanden (naechste 5 Zeilen)
                        bool hasTryCatch = false;
                        for (int j = i + 1; j < Math.Min(i + 6, file.Lines.Length); j++)
                        {
                            if (file.Lines[j].TrimStart().StartsWith("try"))
                            {
                                hasTryCatch = true;
                                break;
                            }
                        }

                        if (!hasTryCatch)
                        {
                            asyncVoidCount++;
                            results.Add(new(Severity.Warn, Category, $"async void '{methodName}' ohne try-catch in {file.RelativePath}:{i + 1}"));
                        }
                    }
                }

                // public async void das Task sein sollte
                if (Regex.IsMatch(trimmed, @"\bpublic\s+async\s+void\s+(\w+)") && !trimmed.Contains("override"))
                {
                    var methodMatch = Regex.Match(trimmed, @"\bpublic\s+async\s+void\s+(\w+)");
                    var methodName = methodMatch.Groups[1].Value;
                    bool isEventHandler = methodName.StartsWith("On")
                        || methodName.Contains("_")
                        || methodName.EndsWith("Handler")
                        || methodName.EndsWith("Callback");

                    if (!isEventHandler)
                        results.Add(new(Severity.Warn, Category, $"public async void '{methodName}' sollte Task zurueckgeben in {file.RelativePath}:{i + 1}"));
                }

                // Fire-and-forget: _ = SomethingAsync() — INFO statt WARN, weil oft legitim (Loading-Pipeline,
                // Auto-Save, Ctor-Init). Sauberer Pattern: .SafeFireAndForget() Extension verwenden.
                // Wird ignoriert wenn die Methode SafeFireAndForget oder FireAndForget heisst.
                if (Regex.IsMatch(trimmed, @"_\s*=\s*\w+Async\s*\(")
                    && !trimmed.Contains("SafeFireAndForget")
                    && !trimmed.Contains(".FireAndForget("))
                {
                    fireAndForgetCount++;
                    results.Add(new(Severity.Info, Category, $"Fire-and-forget '_ = ...Async()' in {file.RelativePath}:{i + 1} → '.SafeFireAndForget()' Extension bevorzugen"));
                }

                // Leere catch-Bloecke (catch { } oder catch (Exception) { })
                if (Regex.IsMatch(trimmed, @"catch\s*(\([^)]*\))?\s*$"))
                {
                    // Naechste nicht-leere Zeile prüfen
                    for (int j = i + 1; j < Math.Min(i + 4, file.Lines.Length); j++)
                    {
                        var nextLine = file.Lines[j].TrimStart();
                        if (string.IsNullOrWhiteSpace(nextLine)) continue;
                        if (nextLine == "{") continue;
                        if (nextLine == "}")
                        {
                            emptyCatchCount++;
                            results.Add(new(Severity.Warn, Category, $"Leerer catch-Block in {file.RelativePath}:{i + 1}"));
                        }
                        break;
                    }
                }
            }
        }

        if (asyncVoidCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine unsicheren async void Methoden"));
        if (fireAndForgetCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine fire-and-forget Async-Aufrufe (oder alle via SafeFireAndForget)"));
        else
            results.Add(new(Severity.Info, Category, $"{fireAndForgetCount} fire-and-forget Aufrufe — SafeFireAndForget-Extension einfuehren fuer zentrale Exception-Handling"));
        if (emptyCatchCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine leeren catch-Bloecke"));

        return results;
    }
}
