using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Strikter MVVM-Auditor (Pre-Commit-Screen).
/// Prueft: Service-Locator in View-Ctors, DataContext-Zuweisung im Code-Behind,
/// Click-Handler in XAML, x:CompileBindings="False" Heimlich-Disables,
/// public static Instance in ViewModels (Singleton-Anti-Pattern fuer VMs).
///
/// App.axaml.cs ist die Composition Root und darf DataContext = ... setzen.
/// MainView.axaml.cs darf das ausnahmsweise auch, weil es manche Apps so handhaben.
/// Alle anderen .axaml.cs muessen DataContext via ViewLocator beziehen.
/// </summary>
class MvvmStrictChecker : IChecker
{
    public string Category => "MVVM-Strict";

    // Service-Locator-Pattern: App.Services.GetRequiredService<T> ODER ServiceLocator.Resolve/Get<T>
    // (beide aus der Anti-Pattern-Tabelle der Haupt-CLAUDE.md).
    static readonly Regex ServiceLocatorRegex = new(
        @"App\.Services(\?)?\.GetRequiredService\s*<|\bServiceLocator\.(Resolve|Get|GetService|GetRequiredService)\s*<",
        RegexOptions.Compiled);

    // Zuweisung an den EIGENEN View-DataContext am Statement-Anfang (this.DataContext = / DataContext =).
    // Bewusst NICHT erfasst: Object-Initializer (new ChildView { DataContext = ... }) und Member-Access
    // (childControl.DataContext = ...) — das sind legitime, im Code erzeugte Child-Views (z.B. Lazy-MapView).
    static readonly Regex DataContextAssignRegex = new(
        @"^(this\.)?DataContext\s*=\s*(?!=)",
        RegexOptions.Compiled);

    // public static T Instance / public static readonly T Instance in VMs
    static readonly Regex StaticInstanceRegex = new(
        @"public\s+static\s+(readonly\s+)?\w+(ViewModel|VM)\s+Instance\b",
        RegexOptions.Compiled);

    // Click="MethodName" in AXAML (Event-Handler statt Command-Binding)
    static readonly Regex ClickHandlerRegex = new(
        @"\bClick\s*=\s*""[A-Za-z_]\w*""",
        RegexOptions.Compiled);

    // x:CompileBindings="False" als heimlicher Opt-Out vom Default
    static readonly Regex CompileBindingsFalseRegex = new(
        @"x:CompileBindings\s*=\s*""[Ff]alse""",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int viewServiceLocator = 0;
        int viewDataContextAssign = 0;
        int vmStaticInstance = 0;
        int vmServiceLocator = 0;
        int xamlClickHandler = 0;
        int xamlCompileBindingsFalse = 0;

        // === 1. Shared-CS-Dateien: View-Code-Behind + ViewModels ===
        foreach (var file in ctx.SharedCsFiles)
        {
            var fileName = Path.GetFileName(file.FullPath);
            bool isAppAxamlCs = fileName == "App.axaml.cs";
            bool isCodeBehind = file.FullPath.EndsWith(".axaml.cs");
            bool isViewModel = file.FullPath.Contains("ViewModels") && file.FullPath.EndsWith("ViewModel.cs");

            // App.axaml.cs ist Composition Root - hier ist alles erlaubt
            if (isAppAxamlCs) continue;

            for (int i = 0; i < file.Lines.Length; i++)
            {
                var line = file.Lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // --- View-Code-Behind Checks ---
                if (isCodeBehind)
                {
                    if (ServiceLocatorRegex.IsMatch(trimmed))
                    {
                        viewServiceLocator++;
                        results.Add(new(Severity.Fail, Category,
                            $"Service-Locator (App.Services/ServiceLocator) im View-Code-Behind in {file.RelativePath}:{i + 1} → Android-Crash-Pattern, Service ins ViewModel injizieren"));
                    }

                    // Eigener DataContext nur in MainWindow.axaml.cs (Desktop-Composition Root) und
                    // MainView.axaml.cs (manche Apps setzen ihn dort) erlaubt.
                    if (DataContextAssignRegex.IsMatch(trimmed) && fileName != "MainWindow.axaml.cs" && fileName != "MainView.axaml.cs")
                    {
                        viewDataContextAssign++;
                        results.Add(new(Severity.Fail, Category,
                            $"DataContext-Zuweisung an die eigene View im Code-Behind in {file.RelativePath}:{i + 1} → ViewLocator setzt DataContext automatisch"));
                    }
                }

                // --- ViewModel Checks ---
                if (isViewModel)
                {
                    if (ServiceLocatorRegex.IsMatch(trimmed))
                    {
                        vmServiceLocator++;
                        results.Add(new(Severity.Fail, Category,
                            $"Service-Locator (App.Services/ServiceLocator) im ViewModel in {file.RelativePath}:{i + 1} → Service-Locator-Anti-Pattern, per Constructor injizieren"));
                    }

                    if (StaticInstanceRegex.IsMatch(line))
                    {
                        vmStaticInstance++;
                        results.Add(new(Severity.Fail, Category,
                            $"public static Instance in ViewModel in {file.RelativePath}:{i + 1} → Singleton-Anti-Pattern fuer VMs, DI-Singleton verwenden"));
                    }
                }
            }
        }

        // === 2. AXAML-Dateien: Click-Handler + CompileBindings=False ===
        foreach (var file in ctx.AxamlFiles)
        {
            var xamlLines = file.Content.Split('\n');
            for (int i = 0; i < xamlLines.Length; i++)
            {
                var line = xamlLines[i];
                // XAML hat keine // Kommentare - <!-- ... --> ueberspringen
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("<!--")) continue;

                if (ClickHandlerRegex.IsMatch(line))
                {
                    xamlClickHandler++;
                    results.Add(new(Severity.Warn, Category,
                        $"Click=\"...\" Event-Handler in {file.RelativePath}:{i + 1} → [RelayCommand] + Command-Binding verwenden"));
                }

                if (CompileBindingsFalseRegex.IsMatch(line))
                {
                    xamlCompileBindingsFalse++;
                    results.Add(new(Severity.Warn, Category,
                        $"x:CompileBindings=\"False\" in {file.RelativePath}:{i + 1} → schaltet globalen Default ab, Binding-Fehler werden erst zur Laufzeit sichtbar"));
                }
            }
        }

        // === 3. Zusammenfassungen wenn nichts gefunden ===
        if (viewServiceLocator == 0)
            results.Add(new(Severity.Pass, Category, "Kein App.Services-Locator in View-Code-Behind"));
        if (viewDataContextAssign == 0)
            results.Add(new(Severity.Pass, Category, "Kein DataContext= im View-Code-Behind (ausser MainWindow.axaml.cs)"));
        if (vmServiceLocator == 0)
            results.Add(new(Severity.Pass, Category, "Kein App.Services-Locator in ViewModels"));
        if (vmStaticInstance == 0)
            results.Add(new(Severity.Pass, Category, "Keine public static Instance-Pattern in ViewModels"));
        if (xamlClickHandler == 0)
            results.Add(new(Severity.Pass, Category, "Keine Click=-Event-Handler in XAML"));
        if (xamlCompileBindingsFalse == 0)
            results.Add(new(Severity.Pass, Category, "Kein x:CompileBindings=\"False\" Heimlich-Disable"));

        return results;
    }
}
