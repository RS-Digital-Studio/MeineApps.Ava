using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>
/// Prueft konsistente Nutzung von CommunityToolkit.Mvvm Patterns:
/// - Manuelle RelayCommand-Instanziierung (`new RelayCommand(...)`) statt `[RelayCommand]`
/// - Manuelle OnPropertyChanged-Aufrufe statt `[ObservableProperty]`
/// - Direkte INotifyPropertyChanged-Implementierung statt ObservableObject
/// - Verwendung von `set => SetProperty(...)` (alt) statt `[ObservableProperty]`
/// Nur ViewModels werden geprueft (Services duerfen die alte Form nutzen).
/// </summary>
class CommunityToolkitChecker : IChecker
{
    public string Category => "CommunityToolkit";

    // new RelayCommand(...) / new AsyncRelayCommand(...) - manuelle Instanziierung
    static readonly Regex ManualRelayCommandRegex = new(
        @"=\s*new\s+(Async)?RelayCommand(<[\w\s,?]+>)?\s*\(",
        RegexOptions.Compiled);

    // OnPropertyChanged() Aufruf im Setter (manuelles MVVM)
    static readonly Regex ManualOnPropertyChangedRegex = new(
        @"\bOnPropertyChanged\s*\(",
        RegexOptions.Compiled);

    // SetProperty(ref _field, value) - alte CommunityToolkit-Form, kann auch ok sein
    static readonly Regex SetPropertyRegex = new(
        @"\bSetProperty\s*\(\s*ref\b",
        RegexOptions.Compiled);

    // Vererbung von INotifyPropertyChanged ohne ObservableObject
    static readonly Regex INPCInheritanceRegex = new(
        @":\s*[^{]*\bINotifyPropertyChanged\b",
        RegexOptions.Compiled);

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        int manualCommandCount = 0;
        int manualOnPropertyChangedCount = 0;
        int setPropertyCount = 0;
        int rawInpcCount = 0;
        int vmsChecked = 0;

        var vmFiles = ctx.SharedCsFiles
            .Where(f => f.FullPath.Contains("ViewModels") && f.FullPath.EndsWith("ViewModel.cs"))
            .ToList();

        foreach (var file in vmFiles)
        {
            vmsChecked++;
            var className = Path.GetFileNameWithoutExtension(file.FullPath);
            var content = file.Content;

            // Datei nutzt CommunityToolkit.Mvvm? Sonst nicht relevant
            bool usesToolkit = content.Contains("CommunityToolkit.Mvvm");

            // 1. Manuelles RelayCommand
            var rcMatches = ManualRelayCommandRegex.Matches(content);
            foreach (Match m in rcMatches)
            {
                var lineNum = GetLineNumber(content, m.Index);
                if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                manualCommandCount++;
                results.Add(new(Severity.Info, Category,
                    $"{className} nutzt 'new RelayCommand(...)' in {file.RelativePath}:{lineNum} → [RelayCommand]-Attribut auf eine Methode bevorzugen"));
            }

            // 2. SetProperty (alte API, valider aber inkonsistent wenn [ObservableProperty] vorhanden)
            bool hasObservableProperty = content.Contains("[ObservableProperty]");
            var spMatches = SetPropertyRegex.Matches(content);
            if (hasObservableProperty && spMatches.Count > 0)
            {
                foreach (Match m in spMatches)
                {
                    var lineNum = GetLineNumber(content, m.Index);
                    if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                    setPropertyCount++;
                    // Nur erste 3 melden pro Datei
                    if (setPropertyCount <= 3)
                        results.Add(new(Severity.Info, Category,
                            $"{className} mischt SetProperty(ref ...) und [ObservableProperty] in {file.RelativePath}:{lineNum} → einheitlich [ObservableProperty] verwenden"));
                }
            }

            // 3. Manuelles OnPropertyChanged() innerhalb eines Setters
            // (nicht der vom Toolkit generierte Aufruf - ist nur in Setter zu finden)
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // OnPropertyChanged() ohne Argumente oder mit Property-Name -
                // kommt typisch in custom-Settern vor
                if (ManualOnPropertyChangedRegex.IsMatch(trimmed))
                {
                    // Heuristik: nicht in Property-Body sondern in Methoden-Body (Partial-Hook)
                    // -> Wenn Datei [ObservableProperty] nutzt, ist explizites OnPropertyChanged ueberfluessig
                    // -> Wenn nicht, ist ein manueller Setter im Spiel
                    bool isPartialHook = trimmed.Contains("partial void On")
                                       || trimmed.Contains("=> OnPropertyChanged(")
                                       || trimmed.StartsWith("base.OnPropertyChanged");
                    if (isPartialHook) continue;

                    // Pruefen ob in einem Setter (Heuristik: vorherige Zeilen enthalten "set {" oder "set =>")
                    bool inSetter = IsInSetter(file.Lines, i);
                    if (inSetter)
                    {
                        manualOnPropertyChangedCount++;
                        if (manualOnPropertyChangedCount <= 5) // Begrenzen
                            results.Add(new(Severity.Info, Category,
                                $"{className} ruft OnPropertyChanged() manuell im Setter in {file.RelativePath}:{i + 1} → [ObservableProperty] generiert das automatisch"));
                    }
                }
            }

            // 4. INotifyPropertyChanged direkt implementiert ohne ObservableObject
            if (usesToolkit)
            {
                var inpcMatches = INPCInheritanceRegex.Matches(content);
                foreach (Match m in inpcMatches)
                {
                    // Wenn ObservableObject in derselben Vererbungsliste → ok (Mehrfach-Interface)
                    var inheritanceStart = m.Index;
                    var inheritanceEnd = content.IndexOf('{', inheritanceStart);
                    if (inheritanceEnd < 0) inheritanceEnd = inheritanceStart + m.Length;
                    var inheritanceBlock = content.Substring(inheritanceStart, inheritanceEnd - inheritanceStart);
                    if (inheritanceBlock.Contains("ObservableObject") || inheritanceBlock.Contains("ViewModelBase"))
                        continue;

                    var lineNum = GetLineNumber(content, m.Index);
                    if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                    rawInpcCount++;
                    results.Add(new(Severity.Info, Category,
                        $"{className} implementiert INotifyPropertyChanged direkt in {file.RelativePath}:{lineNum} → von ObservableObject erben (oder ViewModelBase)"));
                }
            }
        }

        // Zusammenfassungen
        if (vmsChecked == 0)
        {
            results.Add(new(Severity.Info, Category, "Keine ViewModels gefunden"));
            return results;
        }

        if (manualCommandCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle Commands nutzen [RelayCommand]-Attribut"));
        if (setPropertyCount == 0)
            results.Add(new(Severity.Pass, Category, "Konsistente [ObservableProperty]-Nutzung (kein Mix mit SetProperty)"));
        if (manualOnPropertyChangedCount == 0)
            results.Add(new(Severity.Pass, Category, "Keine manuellen OnPropertyChanged-Aufrufe in Settern"));
        if (rawInpcCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle VMs erben von ObservableObject/ViewModelBase (kein rohes INotifyPropertyChanged)"));

        return results;
    }

    /// <summary>Heuristik: Steht diese Zeile innerhalb eines C#-Property-Setters?</summary>
    static bool IsInSetter(string[] lines, int currentLine)
    {
        // Scan rueckwaerts: finde "set" oder "}" oder "set =>" innerhalb der naechsten 15 Zeilen
        for (int i = currentLine - 1; i >= Math.Max(0, currentLine - 15); i--)
        {
            var t = lines[i].TrimStart();
            // Setter-Beginn
            if (Regex.IsMatch(t, @"^\s*set\s*[\{=]")) return true;
            // Setter-Ende erreicht (vor unserem Treffer): nicht im Setter
            if (Regex.IsMatch(t, @"^\s*\}\s*$") && i < currentLine - 2) return false;
            // Get-Body? Auch interessant aber kein Setter
            if (Regex.IsMatch(t, @"^\s*get\s*[\{=]")) return false;
        }
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
