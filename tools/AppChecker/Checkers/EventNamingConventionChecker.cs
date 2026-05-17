using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft Event-Naming- und Signatur-Konventionen in ViewModels:
///
/// Erwartete Standard-Events (Naming + Signatur):
/// - NavigationRequested → Action&lt;string&gt;       (Route z.B. "..", "../subpage")
/// - MessageRequested    → Action&lt;string, string&gt; (Titel, Body)
/// - FloatingTextRequested → Action&lt;string, string&gt; (Text, Kategorie)
/// - CelebrationRequested → Action ODER EventHandler
/// - ExitHintRequested   → Action&lt;string&gt;       (Hinweis-Text)
/// - ClipboardRequested  → Action&lt;string&gt;       (zu kopierender Text)
///
/// Suffix-Konvention:
/// - "Requested"  — UI-Anforderung an die View ("zeige Dialog", "navigiere")
/// - "Changed"    — Property-Notification (separater Event-Typ, nicht INotifyPropertyChanged)
/// - "Updated"    — Daten-Update
/// - "Completed"  — Async-Operation fertig
/// - "Failed"     — Async-Operation fehlgeschlagen
/// </summary>
class EventNamingConventionChecker : IChecker
{
    public string Category => "Event-Naming";

    // event TYPE EVENTNAME; (Action/EventHandler/eigenes Delegate)
    static readonly Regex EventRegex = new(
        @"\bevent\s+(?<type>[\w<>?,\s]+?)\s+(?<name>\w+)\s*[;=]",
        RegexOptions.Compiled);

    // Erwartete Signaturen — mehrere zulaessige Alternativen pro Event-Name
    // (z.B. BomberBlast nutzt Action<NavigationRequest> als typsichere Variante).
    static readonly Dictionary<string, string[]> ExpectedSignatures = new(StringComparer.Ordinal)
    {
        ["NavigationRequested"]    = ["Action<string>", "Action<NavigationRequest>"],
        ["MessageRequested"]       = ["Action<string, string>"],
        ["FloatingTextRequested"]  = ["Action<string, string>", "EventHandler<(string, string)>"],
        ["ExitHintRequested"]      = ["Action<string>"],
        ["ClipboardRequested"]     = ["Action<string>"],
    };

    static readonly HashSet<string> AllowedSuffixes =
    [
        "Requested", "Changed", "Updated", "Completed", "Failed",
        "Started", "Stopped", "Triggered", "Loaded", "Saved",
        "Connected", "Disconnected", "Opened", "Closed",
        "Added", "Removed", "Hit", "Tick"
    ];

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int eventCount = 0;
        int signatureMismatch = 0;
        int suffixViolation = 0;
        int vmsChecked = 0;

        var vmFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith("ViewModel.cs"))
            .ToList();

        foreach (var file in vmFiles)
        {
            vmsChecked++;
            var className = Path.GetFileNameWithoutExtension(file.FullPath);

            foreach (Match m in EventRegex.Matches(file.Content))
            {
                eventCount++;
                var typeStr = NormalizeType(m.Groups["type"].Value);
                var eventName = m.Groups["name"].Value;
                var lineNum = GetLineNumber(file.Content, m.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;

                // 1. Signatur-Pruefung (mehrere zulaessige Alternativen)
                if (ExpectedSignatures.TryGetValue(eventName, out var expectedSigs))
                {
                    bool matchesAny = expectedSigs.Any(sig => TypesEquivalent(typeStr, NormalizeType(sig)));
                    if (!matchesAny)
                    {
                        signatureMismatch++;
                        results.Add(new(Severity.Warn, Category,
                            $"{className}.{eventName} hat Typ '{typeStr}', erwartet eine von [{string.Join(", ", expectedSigs)}] in {file.RelativePath}:{lineNum}"));
                    }
                }

                // 2. Suffix-Konvention
                if (!HasAllowedSuffix(eventName))
                {
                    suffixViolation++;
                    if (suffixViolation <= 8)
                        results.Add(new(Severity.Info, Category,
                            $"{className}.{eventName} folgt keiner Standard-Suffix-Konvention ({string.Join("/", AllowedSuffixes.Take(5))}/...) in {file.RelativePath}:{lineNum}"));
                }
            }
        }

        if (vmsChecked == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine ViewModels gefunden"));
            return results;
        }

        if (eventCount == 0)
            results.Add(new(Severity.Info, Category, "Keine Events in ViewModels deklariert"));
        else
            results.Add(new(Severity.Info, Category, $"{eventCount} Events in {vmsChecked} VMs gefunden"));

        if (signatureMismatch == 0)
            results.Add(new(Severity.Pass, Category, "Alle Standard-Events haben die erwartete Signatur (Navigation/Message/FloatingText/...)"));
        if (suffixViolation == 0)
            results.Add(new(Severity.Pass, Category, "Alle Events folgen Suffix-Konvention (Requested/Changed/Updated/...)"));
        else if (suffixViolation > 8)
            results.Add(new(Severity.Info, Category, $"...und {suffixViolation - 8} weitere Events ohne Standard-Suffix"));

        return results;
    }

    static string NormalizeType(string type)
    {
        // Whitespace + Nullables vereinheitlichen
        return Regex.Replace(type, @"\s+", "").TrimEnd('?');
    }

    static bool TypesEquivalent(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static bool HasAllowedSuffix(string eventName)
    {
        foreach (var suffix in AllowedSuffixes)
            if (eventName.EndsWith(suffix, StringComparison.Ordinal)) return true;
        return false;
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
