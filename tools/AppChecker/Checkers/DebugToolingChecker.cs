using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft die verbindlichen Debug-/Test-Tooling-Conventions (Haupt-CLAUDE.md
/// "DebugHelper + AutomationIds (Pflicht)"):
/// - AutomationProperties.AutomationId auf interaktiven Elementen (Buttons/TextBoxes/...)
/// - debug:DebugHelper.ShowName="True" auf View-Roots (Debug-only)
///
/// Beides ist eine Soll-Convention, kein funktionaler Fehler → INFO, aggregiert pro App.
/// </summary>
class DebugToolingChecker : IChecker
{
    public string Category => "Debug-Tooling";

    // Interaktive Open-Tags, die laut Convention eine AutomationId tragen sollten
    static readonly Regex InteractiveTagRegex = new(
        @"<(Button|TextBox|ListBox|ComboBox|CheckBox|ToggleButton|RadioButton|Slider|ToggleSwitch)\b",
        RegexOptions.Compiled);

    static readonly Regex AutomationIdRegex = new(
        @"AutomationProperties\.AutomationId\s*=",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        // Nur echte View-AXAMLs (keine Resource-Dictionaries / Styles)
        var viewFiles = ctx.AxamlFiles
            .Where(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f.FullPath);
                return name is not ("App" or "AppPalette") && !name.EndsWith("Theme") && !name.EndsWith("Styles");
            })
            .ToList();

        if (viewFiles.Count == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine View-AXAML-Dateien gefunden"));
            return results;
        }

        // === AutomationId-Abdeckung (aggregiert) ===
        int interactiveCount = 0;
        int automationIdCount = 0;
        foreach (var view in viewFiles)
        {
            interactiveCount += InteractiveTagRegex.Matches(view.Content).Count;
            automationIdCount += AutomationIdRegex.Matches(view.Content).Count;
        }

        if (interactiveCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine interaktiven Controls (keine AutomationIds noetig)"));
        else if (automationIdCount >= interactiveCount)
            results.Add(new(Severity.Pass, Category, $"AutomationId-Abdeckung ausreichend ({automationIdCount} AutomationIds / {interactiveCount} interaktive Controls)"));
        else
            results.Add(new(Severity.Info, Category, $"{interactiveCount - automationIdCount} von {interactiveCount} interaktiven Controls evtl. ohne AutomationProperties.AutomationId (Test-Automatisierung)"));

        // === DebugHelper.ShowName auf View-Roots (aggregiert) ===
        var viewsWithoutDebugHelper = viewFiles
            .Where(v => !v.Content.Contains("DebugHelper.ShowName"))
            .Select(v => Path.GetFileNameWithoutExtension(v.FullPath))
            .ToList();

        if (viewsWithoutDebugHelper.Count == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewFiles.Count} Views nutzen debug:DebugHelper.ShowName"));
        else
            results.Add(new(Severity.Info, Category, $"{viewsWithoutDebugHelper.Count}/{viewFiles.Count} Views ohne debug:DebugHelper.ShowName (Debug-Overlay): {string.Join(", ", viewsWithoutDebugHelper.Take(5))}{(viewsWithoutDebugHelper.Count > 5 ? "..." : "")}"));

        return results;
    }
}
