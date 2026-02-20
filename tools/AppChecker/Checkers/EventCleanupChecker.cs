using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Event-Cleanup: += ohne -=, statische Event-Handler</summary>
class EventCleanupChecker : IChecker
{
    public string Category => "Event-Cleanup";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int staticHandlerCount = 0;

        foreach (var file in ctx.SharedCsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // Statische Event-Handler (static void Handler)
                if (Regex.IsMatch(trimmed, @"\bstatic\s+void\s+\w+_\w+\s*\("))
                {
                    staticHandlerCount++;
                    if (staticHandlerCount <= 3)
                        results.Add(new(Severity.Warn, Category, $"Statischer Event-Handler in {file.RelativePath}:{i + 1} → kann Memory-Leak verursachen"));
                }
            }

            // Event-Subscriptions zaehlen (nur echte Event-Handler, keine numerischen +=)
            // Echte Events: .EventName += handler/lambda, NICHT .Score += 10 oder .X += delta
            var subscriptions = Regex.Matches(file.Content, @"\.(\w+)\s*\+=\s*(?!null)(?!\s*\d)(?!\s*[\w.]+\s*[;,)\]])(.)")
                .Where(m =>
                {
                    var afterPlus = m.Groups[2].Value;
                    // Numerische/arithmetische Ausdruecke ausfiltern (nicht Event-Handler)
                    return afterPlus != "-" && afterPlus != "(" // kein -=, kein (int)cast
                        && !char.IsDigit(afterPlus[0]); // kein .Prop += 5
                })
                .Select(m => m.Groups[1].Value)
                .Where(name => !name.StartsWith("_")) // Felder ausschliessen
                .Where(name => char.IsUpper(name[0])) // Events beginnen mit Grossbuchstabe
                .ToList();

            var unsubscriptions = Regex.Matches(file.Content, @"\.(\w+)\s*-=")
                .Select(m => m.Groups[1].Value)
                .ToHashSet();

            // Events die subscribed aber nie unsubscribed werden (nur fuer VMs die nicht Singleton sind)
            var fileName = Path.GetFileNameWithoutExtension(file.FullPath);
            bool isMainVm = fileName == "MainViewModel";

            // MainViewModel ist Singleton, da ist -= nicht noetig
            if (!isMainVm && subscriptions.Count > 0)
            {
                var unsubscribedEvents = subscriptions.Where(s => !unsubscriptions.Contains(s)).Distinct().ToList();
                // Nur als INFO, da nicht alle Events cleanup brauchen (z.B. in Singletons)
                foreach (var evt in unsubscribedEvents.Take(3))
                    results.Add(new(Severity.Info, Category, $"Event '.{evt} +=' ohne '-=' in {file.RelativePath} (evtl. Singleton)"));
            }
        }

        if (staticHandlerCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine statischen Event-Handler"));
        else if (staticHandlerCount > 3)
            results.Add(new(Severity.Warn, Category, $"...und {staticHandlerCount - 3} weitere statische Event-Handler"));

        return results;
    }
}
