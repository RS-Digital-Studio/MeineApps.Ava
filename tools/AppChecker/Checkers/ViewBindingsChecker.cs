using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft View-Bindings: x:DataType, xmlns:vm, View↔ViewModel Paar-Check.
/// Verschaerft: x:DataType auf View-Root ist FAIL wenn die View Bindings enthaelt.
/// Begruendung: AvaloniaUseCompiledBindingsByDefault=true ist in Directory.Build.props global aktiv
/// → ohne x:DataType findet der Compiler den Binding-Pfad nicht, alle Bindings bleiben tot/silent.
/// </summary>
class ViewBindingsChecker : IChecker
{
    public string Category => "View-Bindings";

    static readonly Regex BindingUsageRegex = new(@"\{(Binding|CompiledBinding|TemplateBinding|x:Static)\b", RegexOptions.Compiled);
    static readonly Regex VmNsRegex = new(@"xmlns:(vm|viewmodels)\s*=\s*""using:", RegexOptions.Compiled);
    static readonly Regex DataTypeRegex = new(@"x:DataType\s*=\s*""", RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        var viewFiles = ctx.AxamlFiles
            .Where(f => !Path.GetFileName(f.FullPath).Contains("MainWindow"))
            .ToList();

        var vmNames = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith("ViewModel.cs"))
            .Select(f => Path.GetFileNameWithoutExtension(f.FullPath))
            .ToHashSet();

        if (viewFiles.Count == 0)
        {
            results.Add(new(Severity.Warn, Category, "Keine View-Dateien gefunden"));
            return results;
        }

        // MainView x:DataType Check
        var mainView = viewFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainView.axaml");
        if (mainView != null)
        {
            if (mainView.Content.Contains("x:DataType=\"vm:MainViewModel\"")
                || mainView.Content.Contains("x:DataType=\"viewmodels:MainViewModel\""))
                results.Add(new(Severity.Pass, Category, "MainView hat x:DataType=MainViewModel"));
            else if (DataTypeRegex.IsMatch(mainView.Content))
                results.Add(new(Severity.Pass, Category, "MainView hat x:DataType gesetzt"));
            else if (BindingUsageRegex.IsMatch(mainView.Content))
                results.Add(new(Severity.Fail, Category, "MainView nutzt Bindings hat aber KEIN x:DataType → Compiled Bindings tot (Directory.Build.props: AvaloniaUseCompiledBindingsByDefault=true)"));
            else
                results.Add(new(Severity.Info, Category, "MainView hat kein x:DataType (nutzt aber auch keine Bindings)"));
        }

        int viewsWithDataType = 0;
        var viewsBindingNoDataType = new List<string>();
        int viewsWithoutDataType = 0;
        int viewsWithVmNs = 0;
        int viewsWithoutVmNs = 0;

        foreach (var view in viewFiles)
        {
            bool hasDataType = DataTypeRegex.IsMatch(view.Content);
            bool hasBindings = BindingUsageRegex.IsMatch(view.Content);
            bool hasVmNs = VmNsRegex.IsMatch(view.Content);

            if (hasDataType) viewsWithDataType++;
            else viewsWithoutDataType++;

            // FAIL: View nutzt Bindings hat aber KEIN x:DataType → Compiled Bindings tot
            if (hasBindings && !hasDataType)
                viewsBindingNoDataType.Add(Path.GetFileName(view.FullPath));

            if (hasVmNs) viewsWithVmNs++;
            else viewsWithoutVmNs++;
        }

        if (viewsBindingNoDataType.Count == 0)
            results.Add(new(Severity.Pass, Category, $"Alle Views mit Bindings haben x:DataType → Compiled Bindings aktiv"));
        else
            results.Add(new(Severity.Fail, Category, $"{viewsBindingNoDataType.Count} Views mit Bindings ABER ohne x:DataType (Compiled Bindings tot): {string.Join(", ", viewsBindingNoDataType.Take(5))}{(viewsBindingNoDataType.Count > 5 ? "..." : "")}"));

        if (viewsWithoutDataType == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewsWithDataType} Views haben x:DataType"));
        else
            results.Add(new(Severity.Info, Category, $"{viewsWithoutDataType}/{viewFiles.Count} Views ohne x:DataType (binding-frei: ok)"));

        if (viewsWithoutVmNs == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewsWithVmNs} Views haben xmlns:vm"));
        else
            results.Add(new(Severity.Info, Category, $"{viewsWithoutVmNs}/{viewFiles.Count} Views ohne xmlns:vm (nicht alle brauchen es)"));

        // View ↔ ViewModel Paar-Check
        int viewsWithVm = 0;
        var viewsWithoutVmList = new List<string>();
        foreach (var view in viewFiles)
        {
            var viewName = Path.GetFileNameWithoutExtension(view.FullPath);
            var expectedVm = viewName.Replace("View", "ViewModel");
            if (vmNames.Contains(expectedVm))
                viewsWithVm++;
            else
                viewsWithoutVmList.Add(viewName);
        }

        if (viewsWithoutVmList.Count == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewsWithVm} Views haben ein passendes ViewModel"));
        else
            results.Add(new(Severity.Warn, Category, $"{viewsWithoutVmList.Count} Views ohne ViewModel: {string.Join(", ", viewsWithoutVmList.Take(5))}{(viewsWithoutVmList.Count > 5 ? "..." : "")}"));

        // VMs ohne View (INFO)
        var viewNames = viewFiles.Select(f => Path.GetFileNameWithoutExtension(f.FullPath).Replace("View", "ViewModel")).ToHashSet();
        var vmsWithoutView = vmNames.Where(vm => !viewNames.Contains(vm) && vm != "MainViewModel").ToList();
        if (vmsWithoutView.Count > 0)
            results.Add(new(Severity.Info, Category, $"{vmsWithoutView.Count} VMs ohne eigene View: {string.Join(", ", vmsWithoutView.Take(5))}{(vmsWithoutView.Count > 5 ? "..." : "")}"));

        return results;
    }
}
