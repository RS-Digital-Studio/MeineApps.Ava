using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft Navigation: Tab-Buttons, Tab-Count Cross-Check, Overlays, Ad-Spacer</summary>
class NavigationChecker : IChecker
{
    public string Category => "Navigation";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        var app = ctx.App;

        var mainView = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainView.axaml");
        var mainVm = ctx.SharedCsFiles.FirstOrDefault(f =>
            f.FullPath.EndsWith("MainViewModel.cs") && f.FullPath.Contains("ViewModels"));

        if (mainView == null)
        {
            results.Add(new(Severity.Warn, Category, "MainView.axaml nicht gefunden"));
            return results;
        }

        var viewContent = mainView.Content;
        var vmContent = mainVm?.Content ?? "";

        // Tab-Buttons mit Command Binding
        var tabCommandMatches = Regex.Matches(viewContent, @"Command=""\{Binding\s+\w*(Navigate|Select)\w*Command\}""");
        var tabButtonCount = tabCommandMatches.Count;

        if (tabButtonCount > 0)
            results.Add(new(Severity.Pass, Category, $"{tabButtonCount} Tab-Buttons mit Command Binding"));
        else
        {
            bool hasScreenNav = mainVm != null && Regex.IsMatch(vmContent, @"void\s+NavigateTo\s*\(\s*string");
            results.Add(new(hasScreenNav ? Severity.Info : Severity.Warn, Category,
                hasScreenNav ? "Screen-basierte Navigation (keine Tab-Buttons)" : "Keine Tab-Buttons mit Navigate/Select Command gefunden"));
        }

        // Tab-Count Cross-Check
        if (mainVm != null)
        {
            var vmActiveProps = Regex.Matches(vmContent, @"bool\s+Is(\w+)Active\b");
            var vmActiveFields = Regex.Matches(vmContent, @"bool\s+_is(\w+)Active\b");
            var vmTabProps = Regex.Matches(vmContent, @"bool\s+Is(\w+)Tab\b");
            var vmTabCount = vmActiveProps.Select(m => m.Groups[1].Value)
                .Union(vmActiveFields.Select(m => m.Groups[1].Value))
                .Union(vmTabProps.Select(m => m.Groups[1].Value))
                .Distinct().Count();

            if (tabButtonCount > 0 && vmTabCount > 0)
            {
                if (tabButtonCount == vmTabCount)
                    results.Add(new(Severity.Pass, Category, $"Tab-Count stimmt ueberein: {tabButtonCount} Tabs in View = {vmTabCount} IsXxxActive in VM"));
                else
                    results.Add(new(Severity.Info, Category, $"{vmTabCount} IsXxxActive in VM vs. {tabButtonCount} Navigate-Commands in View (Calculator/Sub-Page Buttons mitzaehlend)"));
            }
        }

        // Overlay-Panel mit ZIndex
        var zindexCount = Regex.Matches(viewContent, @"ZIndex=""\d+""").Count;
        if (zindexCount > 0)
            results.Add(new(Severity.Pass, Category, $"{zindexCount} Elemente mit ZIndex (Overlay-Panels)"));
        else
            results.Add(new(Severity.Info, Category, "Keine ZIndex-Elemente gefunden (evtl. keine Overlays)"));

        // Ad-Spacer
        if (app.IsAdSupported)
        {
            if (viewContent.Contains("IsAdBannerVisible") || viewContent.Contains("AdBanner") || viewContent.Contains("AdSpacer"))
                results.Add(new(Severity.Pass, Category, "Ad-Spacer/Banner Referenz vorhanden"));
            else
                results.Add(new(Severity.Warn, Category, "Kein Ad-Spacer/Banner in MainView (Ad-App!)"));
        }

        // Calculator-Overlay Cross-Check
        if (mainVm != null && (vmContent.Contains("CurrentPage") || vmContent.Contains("CurrentCalculatorVm")))
        {
            if (viewContent.Contains("CurrentPage") || viewContent.Contains("CurrentCalculatorVm") || viewContent.Contains("DataTemplate"))
                results.Add(new(Severity.Pass, Category, "Calculator-Overlay mit DataTemplate/ContentControl verdrahtet"));
            else
                results.Add(new(Severity.Warn, Category, "CurrentPage/CurrentCalculatorVm in VM aber nicht in View verdrahtet"));
        }

        // NavigationRequested in Child-VMs verdrahtet
        if (mainVm != null)
        {
            var childVmsWithNav = ctx.SharedCsFiles
                .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith("ViewModel.cs"))
                .Where(f => !f.FullPath.EndsWith("MainViewModel.cs"))
                .Where(f => f.Content.Contains("NavigationRequested?.Invoke"))
                .Select(f => Path.GetFileNameWithoutExtension(f.FullPath))
                .ToList();

            foreach (var childName in childVmsWithNav)
            {
                var shortName = childName.Replace("ViewModel", "");
                if (Regex.IsMatch(vmContent, $@"{shortName}\w*\.NavigationRequested\s*\+="))
                    results.Add(new(Severity.Pass, Category, $"{childName}.NavigationRequested verdrahtet"));
                else
                    results.Add(new(Severity.Warn, Category, $"{childName}.NavigationRequested NICHT in MainVM verdrahtet"));
            }
        }

        return results;
    }
}
