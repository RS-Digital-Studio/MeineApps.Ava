using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft bekannte Avalonia-Gotchas aus CLAUDE.md und MEMORY.md</summary>
class AvaloniaGotchasChecker : IChecker
{
    public string Category => "Avalonia-Gotchas";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        CheckAxamlGotchas(results, ctx);
        CheckCsGotchas(results, ctx);

        return results;
    }

    void CheckAxamlGotchas(List<CheckResult> results, CheckContext ctx)
    {
        int translateNoPx = 0;
        int selectorNoType = 0;
        int scrollViewerPadding = 0;
        int gridPadding = 0;
        int unicodeEscape = 0;
        int tapGesture = 0;
        int buttonText = 0;
        int itemsRepeater = 0;
        int renderTransformKeyframe = 0;

        foreach (var file in ctx.AxamlFiles)
        {
            var content = file.Content;
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // translate() ohne px-Einheiten (z.B. translate(0, 400) statt translate(0px, 400px))
                if (Regex.IsMatch(line, @"translate\s*\(\s*-?\d+\s*,\s*-?\d+\s*\)"))
                {
                    translateNoPx++;
                    results.Add(new(Severity.Fail, Category, $"translate() ohne px-Einheiten in {file.RelativePath}:{i + 1} → FormatException"));
                }

                // Style Selector #Name ohne Typ-Prefix
                if (Regex.IsMatch(line, @"Selector\s*=\s*""[^""]*(?<!\w)#\w+"))
                {
                    // Prüfen ob vor dem # ein Typ steht
                    var selectorMatch = Regex.Match(line, @"Selector\s*=\s*""([^""]*)""");
                    if (selectorMatch.Success)
                    {
                        var selector = selectorMatch.Groups[1].Value;
                        // Pattern: #Name ohne vorangehenden Typ (z.B. > #Foo statt > Grid#Foo)
                        if (Regex.IsMatch(selector, @"(?:^|\s|>)\s*#\w+"))
                        {
                            selectorNoType++;
                            results.Add(new(Severity.Warn, Category, $"Style Selector '#Name' ohne Typ-Prefix in {file.RelativePath}:{i + 1} → AVLN2200"));
                        }
                    }
                }

                // ScrollViewer mit Padding (nur auf dem ScrollViewer-Element selbst)
                if (line.Contains("<ScrollViewer"))
                {
                    // Einzeiliges Element: <ScrollViewer ... Padding="..." ...>
                    if (Regex.IsMatch(line, @"<ScrollViewer\b[^>]*\bPadding\s*="))
                    {
                        scrollViewerPadding++;
                        results.Add(new(Severity.Fail, Category, $"ScrollViewer mit Padding in {file.RelativePath}:{i + 1} → verhindert Scrollen, Margin auf Kind-Element verwenden"));
                    }
                    else if (!line.Contains("/>") && !line.TrimEnd().EndsWith(">"))
                    {
                        // Mehrzeiliges Element: Padding auf Folgezeilen bis > oder />
                        for (int j = i + 1; j < Math.Min(i + 8, lines.Length); j++)
                        {
                            var nextLine = lines[j].TrimStart();
                            if (nextLine.StartsWith("Padding=") || Regex.IsMatch(nextLine, @"^\s*Padding\s*="))
                            {
                                scrollViewerPadding++;
                                results.Add(new(Severity.Fail, Category, $"ScrollViewer mit Padding in {file.RelativePath}:{j + 1} → verhindert Scrollen, Margin auf Kind-Element verwenden"));
                                break;
                            }
                            // Ende des ScrollViewer-Tags erreicht
                            if (nextLine.Contains("/>") || nextLine.TrimEnd().EndsWith(">"))
                                break;
                        }
                    }
                }

                // Grid mit Padding
                if (Regex.IsMatch(line, @"<Grid\b[^>]*Padding\s*="))
                {
                    gridPadding++;
                    results.Add(new(Severity.Warn, Category, $"Grid mit Padding in {file.RelativePath}:{i + 1} → Grid hat kein Padding, Margin verwenden"));
                }

                // \u20ac in XAML
                if (line.Contains(@"\u20ac") || line.Contains(@"\u20AC"))
                {
                    unicodeEscape++;
                    results.Add(new(Severity.Warn, Category, $"\\u20ac in XAML in {file.RelativePath}:{i + 1} → direkt € oder &#x20AC; verwenden"));
                }

                // TapGestureRecognizer
                if (line.Contains("TapGestureRecognizer"))
                {
                    tapGesture++;
                    results.Add(new(Severity.Fail, Category, $"TapGestureRecognizer in {file.RelativePath}:{i + 1} → Button mit transparentem Background verwenden"));
                }

                // Button.Text statt Button.Content
                if (Regex.IsMatch(line, @"<Button\b[^>]*\bText\s*="))
                {
                    buttonText++;
                    results.Add(new(Severity.Warn, Category, $"Button.Text in {file.RelativePath}:{i + 1} → Button.Content verwenden"));
                }

                // ItemsRepeater statt ItemsControl
                if (line.Contains("<ItemsRepeater") || line.Contains("</ItemsRepeater"))
                {
                    itemsRepeater++;
                    if (itemsRepeater == 1) // Nur einmal pro Datei melden
                        results.Add(new(Severity.Info, Category, $"ItemsRepeater in {file.RelativePath}:{i + 1} → ItemsControl bevorzugen (einfacher)"));
                }
            }

            // RenderTransform / TransformOperations als animierte Property in Style.Animations KeyFrames
            // Erlaubt: RotateTransform.Angle, Opacity etc. (double-Properties) + TransformOperationsTransition in <Transitions>
            // Verboten: Property="RenderTransform" in <Setter> innerhalb <KeyFrame> (kein Animator)
            var animBlocks = Regex.Matches(content, @"<Style\.Animations>([\s\S]*?)</Style\.Animations>");
            foreach (Match block in animBlocks)
            {
                var blockContent = block.Groups[1].Value;
                // Innerhalb des Animations-Blocks nach RenderTransform/TransformOperations als Setter-Property suchen
                if (Regex.IsMatch(blockContent, @"Property\s*=\s*""(RenderTransform|TransformOperations)"""))
                {
                    renderTransformKeyframe++;
                    results.Add(new(Severity.Fail, Category, $"RenderTransform/TransformOperations als Property in Style.Animations in {file.RelativePath} → Crash 'No animator registered'"));
                    break;
                }
            }
        }

        // Zusammenfassungen
        if (translateNoPx == 0) results.Add(new(Severity.Pass, Category, "Alle translate() mit px-Einheiten"));
        if (scrollViewerPadding == 0) results.Add(new(Severity.Pass, Category, "Kein ScrollViewer mit Padding"));
        if (tapGesture == 0) results.Add(new(Severity.Pass, Category, "Kein TapGestureRecognizer verwendet"));
        if (renderTransformKeyframe == 0) results.Add(new(Severity.Pass, Category, "Kein RenderTransform in KeyFrame-Animations"));
    }

    void CheckCsGotchas(List<CheckResult> results, CheckContext ctx)
    {
        int isAttachedCount = 0;
        int tryFindResourceCount = 0;
        int isAnimatingCount = 0;

        foreach (var file in ctx.SharedCsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // IsAttachedToVisualTree (entfernt in Avalonia 11.3)
                if (trimmed.Contains("IsAttachedToVisualTree"))
                {
                    isAttachedCount++;
                    results.Add(new(Severity.Fail, Category, $"IsAttachedToVisualTree in {file.RelativePath}:{i + 1} → entfernt in Avalonia 11.3, GetVisualRoot() != null verwenden"));
                }

                // TryFindResource statt TryGetResource
                if (trimmed.Contains("TryFindResource"))
                {
                    tryFindResourceCount++;
                    results.Add(new(Severity.Warn, Category, $"TryFindResource in {file.RelativePath}:{i + 1} → TryGetResource verwenden (Avalonia API)"));
                }

                // Property IsAnimating (kollidiert mit AvaloniaObject.IsAnimating())
                if (Regex.IsMatch(trimmed, @"\bbool\s+IsAnimating\b"))
                {
                    isAnimatingCount++;
                    results.Add(new(Severity.Warn, Category, $"Property 'IsAnimating' in {file.RelativePath}:{i + 1} → kollidiert mit AvaloniaObject.IsAnimating()"));
                }
            }
        }

        if (isAttachedCount == 0) results.Add(new(Severity.Pass, Category, "Kein IsAttachedToVisualTree (korrekt fuer Avalonia 11.3)"));
        if (tryFindResourceCount == 0) results.Add(new(Severity.Pass, Category, "Kein TryFindResource (TryGetResource wird verwendet)"));
    }
}
