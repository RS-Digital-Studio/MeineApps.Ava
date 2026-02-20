using System.Text.RegularExpressions;

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

            // Prüfen ob Margin im ScrollViewer-Kind vorhanden
            // Vereinfachte Heuristik: ScrollViewer vorhanden aber kein Margin="...60" oder Margin="...64" im Kontext
            var hasBottomMargin = Regex.IsMatch(view.Content, @"Margin=""[^""]*(?:60|64|70|80)[^""]*""");
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
        var mainVm = ctx.SharedCsFiles.FirstOrDefault(f =>
            f.FullPath.EndsWith("MainViewModel.cs") && f.FullPath.Contains("ViewModels"));

        if (mainVm == null) return;

        if (mainVm.Content.Contains("ShowBanner"))
            results.Add(new(Severity.Pass, Category, "ShowBanner() in MainViewModel vorhanden"));
        else
            results.Add(new(Severity.Warn, Category, "ShowBanner() fehlt in MainViewModel (Banner wird nicht angezeigt)"));
    }
}
