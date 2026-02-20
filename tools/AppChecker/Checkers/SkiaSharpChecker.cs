using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft SkiaSharp-Patterns: DPI, InvalidateSurface, ArcTo 360</summary>
class SkiaSharpChecker : IChecker
{
    public string Category => "SkiaSharp";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();

        // Prüfen ob App SkiaSharp verwendet
        var skiaFiles = ctx.SharedCsFiles
            .Where(f => f.Content.Contains("SKCanvasView") || f.Content.Contains("PaintSurface") || f.Content.Contains("SKCanvas"))
            .ToList();

        if (skiaFiles.Count == 0)
        {
            results.Add(new(Severity.Info, Category, "Kein SkiaSharp in dieser App"));
            return results;
        }

        int dpiIssues = 0;
        int invalidateVisualCount = 0;
        int arcTo360Count = 0;

        foreach (var file in skiaFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // e.Info.Width/Height statt LocalClipBounds → DPI-Problem
                if (Regex.IsMatch(trimmed, @"e\.Info\.(Width|Height)") || Regex.IsMatch(trimmed, @"args\.Info\.(Width|Height)"))
                {
                    // Prüfen ob LocalClipBounds irgendwo in der Datei verwendet wird
                    if (!file.Content.Contains("LocalClipBounds"))
                    {
                        dpiIssues++;
                        results.Add(new(Severity.Fail, Category, $"e.Info.Width/Height statt LocalClipBounds in {file.RelativePath}:{i + 1} → DPI-Clipping"));
                    }
                }

                // InvalidateVisual() statt InvalidateSurface()
                if (trimmed.Contains("InvalidateVisual()"))
                {
                    invalidateVisualCount++;
                    results.Add(new(Severity.Fail, Category, $"InvalidateVisual() statt InvalidateSurface() in {file.RelativePath}:{i + 1}"));
                }

                // ArcTo bei 360 Grad (leerer Path)
                if (Regex.IsMatch(trimmed, @"ArcTo\s*\(") && trimmed.Contains("360"))
                {
                    arcTo360Count++;
                    results.Add(new(Severity.Info, Category, $"ArcTo mit 360° in {file.RelativePath}:{i + 1} → kann leeren Path erzeugen, in 2x180° aufteilen"));
                }
            }
        }

        if (dpiIssues == 0) results.Add(new(Severity.Pass, Category, "Keine DPI-Probleme (LocalClipBounds wird korrekt verwendet)"));
        if (invalidateVisualCount == 0) results.Add(new(Severity.Pass, Category, "Kein InvalidateVisual() (InvalidateSurface() wird korrekt verwendet)"));
        if (arcTo360Count == 0) results.Add(new(Severity.Pass, Category, "Kein ArcTo mit 360° Problem"));

        return results;
    }
}
