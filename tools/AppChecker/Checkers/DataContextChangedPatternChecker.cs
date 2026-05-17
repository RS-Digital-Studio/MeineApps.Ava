using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft das DataContextChanged-Pattern fuer Views, die VM-Events abonnieren.
///
/// Erwartetes Pattern (siehe CLAUDE.md Avalonia 12 Naming Conventions):
/// - Code-Behind cached VM-Referenz in `_vm`-Field
/// - Ctor verdrahtet `DataContextChanged += OnDataContextChanged`
/// - `OnDataContextChanged`: altes VM abmelden, neues VM merken + anmelden
/// - `OnDetachedFromVisualTree`: Events sauber abmelden + Renderer/Timer disposen
///
/// Findet Anti-Patterns:
/// - View subscribed VM-Events aber hat KEIN `DataContextChanged`-Handling
/// - View hat `_vm += handler` aber kein `_vm -= handler` in Cleanup
/// </summary>
class DataContextChangedPatternChecker : IChecker
{
    public string Category => "DataContext-Pattern";

    static readonly Regex VmEventSubRegex = new(
        @"_\w*[Vv]m\??\.\w+(Requested|Changed|Updated|Completed|Failed)\s*\+=",
        RegexOptions.Compiled);

    static readonly Regex VmEventUnsubRegex = new(
        @"_\w*[Vv]m\??\.\w+(Requested|Changed|Updated|Completed|Failed)\s*-=",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int viewsSubsNoPattern = 0;
        int viewsSubsNoUnsub = 0;
        int viewsChecked = 0;
        int viewsWithCorrectPattern = 0;

        var codeBehindFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.EndsWith(".axaml.cs"))
            .Where(f => Path.GetFileName(f.FullPath) != "App.axaml.cs")
            .ToList();

        foreach (var file in codeBehindFiles)
        {
            var subscriptions = VmEventSubRegex.Matches(file.Content);
            if (subscriptions.Count == 0) continue;

            viewsChecked++;
            var fileName = Path.GetFileName(file.FullPath);
            var content = file.Content;

            bool hasDataContextChangedHook = content.Contains("DataContextChanged += OnDataContextChanged")
                                          || content.Contains("DataContextChanged +=")
                                          || content.Contains("OnDataContextChanged(");
            bool hasDetachedHook = Regex.IsMatch(content, @"override\s+void\s+OnDetachedFromVisualTree");
            int unsubCount = VmEventUnsubRegex.Matches(content).Count;

            if (!hasDataContextChangedHook && !hasDetachedHook)
            {
                viewsSubsNoPattern++;
                results.Add(new(Severity.Warn, Category,
                    $"{fileName} subscribed {subscriptions.Count} VM-Event(s) aber hat WEDER DataContextChanged NOCH OnDetachedFromVisualTree in {file.RelativePath} → Memory-Leak bei DataContext-Wechsel"));
                continue;
            }

            if (unsubCount < subscriptions.Count)
            {
                viewsSubsNoUnsub++;
                results.Add(new(Severity.Warn, Category,
                    $"{fileName} subscribed {subscriptions.Count} VM-Event(s), aber nur {unsubCount} Unsubscription(s) in {file.RelativePath} → Memory-Leak bei DataContext-Wechsel"));
                continue;
            }

            viewsWithCorrectPattern++;
        }

        if (viewsChecked == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine Views mit VM-Event-Subscriptions gefunden"));
            return results;
        }

        if (viewsSubsNoPattern == 0 && viewsSubsNoUnsub == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewsChecked} Views mit VM-Events nutzen DataContextChanged/Detached-Cleanup-Pattern"));
        else
            results.Add(new(Severity.Info, Category, $"{viewsWithCorrectPattern}/{viewsChecked} Views mit korrektem DataContext-Cleanup-Pattern"));

        return results;
    }
}
