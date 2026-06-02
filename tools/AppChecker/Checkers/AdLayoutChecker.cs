using System.Globalization;
using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft Ad-Layout: Spacer 64dp, ScrollViewer Bottom-Margin, ShowBanner</summary>
class AdLayoutChecker : IChecker
{
    public string Category => "Ad-Layout";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        var app = ctx.App;

        if (!app.IsAdSupported)
        {
            results.Add(new(Severity.Info, Category, "Keine Ads (werbefreie App)"));
            return results;
        }

        var mainView = ctx.AxamlFiles.FirstOrDefault(f => Path.GetFileName(f.FullPath) == "MainView.axaml");

        // Ad-Spacer Hoehe pruefen (muss >= 64dp sein)
        if (mainView != null)
        {
            CheckAdSpacer(results, mainView);
            CheckScrollViewerMargin(results, ctx);
        }

        // ShowBanner() in MainViewModel
        CheckShowBanner(results, ctx);

        return results;
    }

    void CheckAdSpacer(List<CheckResult> results, AxamlFile mainView)
    {
        var content = mainView.Content;

        // Suche nach dem Ad-Spacer (Border/Panel mit Hoehe fuer Banner)
        // Typisches Pattern: Height="64" oder MinHeight="64" bei einem AdSpacer/AdBanner Element
        var heightMatches = Regex.Matches(content, @"(?:Height|MinHeight)\s*=\s*""(\d+)""");
        bool foundAdSpacer = false;

        // Suche spezifisch nach Ad-Spacer Bereichen
        if (content.Contains("AdSpacer") || content.Contains("AdBanner") || content.Contains("IsAdBannerVisible"))
        {
            foundAdSpacer = true;

            // Prüfe ob 64dp verwendet wird
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("AdSpacer") || lines[i].Contains("AdBanner") || lines[i].Contains("IsAdBannerVisible"))
                {
                    // Kontext pruefen (5 Zeilen drumherum)
                    var context = string.Join('\n', lines.Skip(Math.Max(0, i - 3)).Take(8));
                    var spacerHeight = Regex.Match(context, @"(?:Height|MinHeight)\s*=\s*""(\d+)""");
                    if (spacerHeight.Success)
                    {
                        var height = int.Parse(spacerHeight.Groups[1].Value);
                        if (height >= 64)
                            results.Add(new(Severity.Pass, Category, $"Ad-Spacer Hoehe: {height}dp (>= 64dp)"));
                        else
                            results.Add(new(Severity.Fail, Category, $"Ad-Spacer Hoehe: {height}dp → muss mindestens 64dp sein (adaptive Banner 50-60dp+)"));
                        return;
                    }
                }
            }
        }

        if (!foundAdSpacer)
            results.Add(new(Severity.Warn, Category, "Kein Ad-Spacer Element in MainView gefunden"));
    }

    void CheckScrollViewerMargin(List<CheckResult> results, CheckContext ctx)
    {
        // Sub-Views mit ScrollViewer prüfen ob Bottom-Margin vorhanden
        int missingMarginCount = 0;
        var subViews = ctx.AxamlFiles
            .Where(f => Path.GetFileName(f.FullPath) != "MainView.axaml"
                     && Path.GetFileName(f.FullPath) != "MainWindow.axaml"
                     && Path.GetFileName(f.FullPath) != "App.axaml")
            .ToList();

        foreach (var view in subViews)
        {
            if (!view.Content.Contains("ScrollViewer")) continue;

            // Bottom-Margin korrekt parsen (4. bzw. 2. Margin-Komponente >= 60dp), statt nur
            // die Substrings 60/64/70/80 zu suchen (sonst werden 90/100dp uebersehen und reine
            // Horizontal-Margins faelschlich als Bottom-Margin gewertet).
            var hasBottomMargin = HasSufficientBottomMargin(view.Content);
            if (!hasBottomMargin)
            {
                missingMarginCount++;
                if (missingMarginCount <= 3) // Nur erste 3 melden
                    results.Add(new(Severity.Warn, Category, $"ScrollViewer in {view.RelativePath} ohne 60dp+ Bottom-Margin (Content kann hinter Ad-Banner verschwinden)"));
            }
        }

        if (missingMarginCount == 0)
            results.Add(new(Severity.Pass, Category, "Alle ScrollViewer Sub-Views haben Bottom-Margin"));
        else if (missingMarginCount > 3)
            results.Add(new(Severity.Warn, Category, $"...und {missingMarginCount - 3} weitere ScrollViewer ohne Bottom-Margin"));
    }

    void CheckShowBanner(List<CheckResult> results, CheckContext ctx)
    {
        // Inhalt ueber ALLE MainViewModel-Partials aggregieren (ShowBanner liegt oft in .EventHandlers.cs).
        var (mainVm, content) = FileHelpers.GetMainViewModel(ctx);

        if (mainVm == null) return;

        if (content.Contains("ShowBanner"))
            results.Add(new(Severity.Pass, Category, "ShowBanner() in MainViewModel vorhanden"));
        else
            results.Add(new(Severity.Warn, Category, "ShowBanner() fehlt in MainViewModel (Banner wird nicht angezeigt)"));
    }

    /// <summary>
    /// Prueft, ob irgendein Margin-Attribut einen Bottom-Wert &gt;= minBottom dp hat.
    /// Avalonia-Margin: "uniform" | "horizontal,vertical" | "left,top,right,bottom".
    /// </summary>
    static bool HasSufficientBottomMargin(string content, int minBottom = 60)
    {
        foreach (Match m in Regex.Matches(content, @"Margin\s*=\s*""([^""]+)"""))
        {
            var parts = m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string bottomStr = parts.Length switch
            {
                1 => parts[0],   // uniform
                2 => parts[1],   // horizontal,vertical → vertical gilt fuer top+bottom
                4 => parts[3],   // left,top,right,bottom
                _ => ""
            };
            if (double.TryParse(bottomStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var bottom) && bottom >= minBottom)
                return true;
        }
        return false;
    }
}
