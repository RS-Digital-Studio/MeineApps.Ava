using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft ViewModel-Verdrahtung: Tabs, Commands, LanguageChanged, Events</summary>
class VmWiringChecker : IChecker
{
    public string Category => "VM-Verdrahtung";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        var mainVm = ctx.SharedCsFiles.FirstOrDefault(f =>
            f.FullPath.EndsWith("MainViewModel.cs") && f.FullPath.Contains("ViewModels"));
        if (mainVm == null)
        {
            results.Add(new(Severity.Fail, Category, "MainViewModel.cs fehlt"));
            return results;
        }

        results.Add(new(Severity.Pass, Category, "MainViewModel.cs vorhanden"));
        var content = mainVm.Content;

        // Tab/Screen Properties
        var activeProps = Regex.Matches(content, @"bool\s+Is(\w+)Active\b");
        var activeFields = Regex.Matches(content, @"bool\s+_is(\w+)Active\b");
        var tabProps = Regex.Matches(content, @"bool\s+Is(\w+)Tab\b");
        var activeNames = activeProps.Select(m => m.Groups[1].Value)
            .Union(activeFields.Select(m => m.Groups[1].Value))
            .Union(tabProps.Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        if (activeNames.Count >= 2)
            results.Add(new(Severity.Pass, Category, $"{activeNames.Count} Tab/Screen Properties ({string.Join(", ", activeNames)})"));
        else
            results.Add(new(Severity.Warn, Category, $"Nur {activeNames.Count} Tab/Screen Properties (erwartet >= 2)"));

        // SelectedTab
        if (Regex.IsMatch(content, @"_selectedTab\w*\s*;") || Regex.IsMatch(content, @"SelectedTab\w*\s*\{"))
            results.Add(new(Severity.Pass, Category, "SelectedTab/SelectedTabIndex Property vorhanden"));
        else
            results.Add(new(Severity.Info, Category, "Kein SelectedTab Property (evtl. Screen-basiert)"));

        // NavigateTo Commands
        if (Regex.IsMatch(content, @"\[RelayCommand\]") && Regex.IsMatch(content, @"void\s+(NavigateTo\w*|Select\w*Tab\w*)\s*\("))
            results.Add(new(Severity.Pass, Category, "NavigateTo/SelectTab Commands vorhanden"));
        else if (Regex.IsMatch(content, @"NavigateTo\w*\s*\("))
            results.Add(new(Severity.Pass, Category, "NavigateTo Methode vorhanden"));
        else
            results.Add(new(Severity.Warn, Category, "Keine NavigateTo/SelectTab Commands gefunden"));

        // LanguageChanged
        if (Regex.IsMatch(content, @"LanguageChanged\s*\+="))
            results.Add(new(Severity.Pass, Category, "LanguageChanged Event abonniert"));
        else
            results.Add(new(Severity.Warn, Category, "LanguageChanged Event nicht abonniert"));

        // UpdateLocalizedTexts Cross-Check
        CheckUpdateLocalizedTexts(results, ctx, content);

        // MessageRequested
        var msgCount = Regex.Matches(content, @"\.MessageRequested\s*\+=").Count;
        if (msgCount > 0)
            results.Add(new(Severity.Pass, Category, $"{msgCount}x MessageRequested Events verdrahtet"));
        else
            results.Add(new(Severity.Info, Category, "Keine MessageRequested Events verdrahtet"));

        // Tab-Wechsel schliesst Overlays
        bool hasOverlays = content.Contains("CurrentPage") || content.Contains("CurrentCalculatorVm") || content.Contains("IsOverlay");
        if (hasOverlays)
        {
            if (Regex.IsMatch(content, @"On\w*SelectedTab\w*Changed|partial\s+void\s+On\w*Tab\w*Changed"))
                results.Add(new(Severity.Pass, Category, "Tab-Wechsel Handler vorhanden (Overlay-Schliessung)"));
            else if (content.Contains("CurrentPage") && content.Contains("= null"))
                results.Add(new(Severity.Pass, Category, "Overlay-Schliessung via CurrentPage = null"));
            else
                results.Add(new(Severity.Warn, Category, "Overlays vorhanden aber kein Tab-Wechsel Handler fuer Schliessung"));
        }

        // GoBackAction/NavigationRequested
        var goBackCount = Regex.Matches(content, @"\.GoBackAction\s*=").Count;
        var navRequestedCount = Regex.Matches(content, @"\.NavigationRequested\s*\+=").Count;
        if (goBackCount + navRequestedCount > 0)
            results.Add(new(Severity.Pass, Category, $"{goBackCount}x GoBackAction + {navRequestedCount}x NavigationRequested verdrahtet"));
        else
            results.Add(new(Severity.Info, Category, "Keine GoBackAction/NavigationRequested verdrahtet"));

        return results;
    }

    void CheckUpdateLocalizedTexts(List<CheckResult> results, CheckContext ctx, string mainVmContent)
    {
        var childVmFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith("ViewModel.cs"))
            .Where(f => !f.FullPath.EndsWith("MainViewModel.cs"))
            .ToList();

        foreach (var childVm in childVmFiles)
        {
            if (!childVm.Content.Contains("void UpdateLocalizedTexts")) continue;

            var childName = Path.GetFileNameWithoutExtension(childVm.FullPath);
            var shortName = childName.Replace("ViewModel", "");
            if (Regex.IsMatch(mainVmContent, $@"{childName}\.UpdateLocalizedTexts|{shortName}\w*\.UpdateLocalizedTexts|{shortName}\w*Vm\.UpdateLocalizedTexts"))
                results.Add(new(Severity.Pass, Category, $"{childName}.UpdateLocalizedTexts() wird aufgerufen"));
            else
                results.Add(new(Severity.Warn, Category, $"{childName}.UpdateLocalizedTexts() wird NICHT in MainVM aufgerufen"));
        }
    }
}
