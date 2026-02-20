using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Prueft View-Bindings: x:DataType, xmlns:vm, View↔ViewModel Paar-Check</summary>
class ViewBindingsChecker : IChecker
{
    public string Category => "View-Bindings";

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
            if (mainView.Content.Contains("x:DataType=\"vm:MainViewModel\"") || mainView.Content.Contains("x:DataType=\"viewmodels:MainViewModel\""))
                results.Add(new(Severity.Pass, Category, "MainView hat x:DataType=MainViewModel"));
            else if (mainView.Content.Contains("x:DataType="))
                results.Add(new(Severity.Pass, Category, "MainView hat x:DataType gesetzt"));
            else
                results.Add(new(Severity.Warn, Category, "MainView hat KEIN x:DataType gesetzt"));
        }

        int viewsWithDataType = 0, viewsWithoutDataType = 0;
        int viewsWithVmNs = 0, viewsWithoutVmNs = 0;

        foreach (var view in viewFiles)
        {
            if (view.Content.Contains("x:DataType="))
                viewsWithDataType++;
            else
                viewsWithoutDataType++;

            if (Regex.IsMatch(view.Content, @"xmlns:(vm|viewmodels)\s*=\s*""using:"))
                viewsWithVmNs++;
            else
                viewsWithoutVmNs++;
        }

        if (viewsWithoutDataType == 0)
            results.Add(new(Severity.Pass, Category, $"Alle {viewsWithDataType} Views haben x:DataType"));
        else
            results.Add(new(Severity.Warn, Category, $"{viewsWithoutDataType}/{viewFiles.Count} Views OHNE x:DataType"));

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
