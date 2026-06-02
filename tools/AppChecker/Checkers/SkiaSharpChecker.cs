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

        CheckPerFrameAllocations(results, skiaFiles);

        return results;
    }

    // Paint-/Render-Methoden, deren Body pro Frame laeuft
    static readonly Regex PaintMethodRegex = new(
        @"\b(?:PaintSurface|OnPaint\w*|Render\w*|Draw\w*)\s*\([^)]*\)\s*",
        RegexOptions.Compiled);

    // Per-Frame-Allokationen, die GC-Druck/Stutter erzeugen (Paints/Filter gehoeren in Felder).
    // SKColor/SKPoint/SKRect sind structs (keine Allokation) → bewusst NICHT erfasst.
    static readonly Regex PerFrameAllocRegex = new(
        @"new\s+SKPaint\b|SKMaskFilter\.Create\w*\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Findet Per-Frame-Allokationen (new SKPaint / SKMaskFilter.Create*) innerhalb von Paint-/Render-Methoden.
    /// Paints und Filter muessen als Felder gecacht werden — pro Frame neu erzeugen verursacht GC-Druck + UI-Stutter
    /// (MeineApps.UI/CLAUDE.md). Feld-Initializer (private SKPaint _p = new(...)) werden NICHT erfasst.
    /// </summary>
    void CheckPerFrameAllocations(List<CheckResult> results, List<CsFile> skiaFiles)
    {
        int perFrameAllocs = 0;
        foreach (var file in skiaFiles)
        {
            var content = file.Content;
            foreach (Match m in PaintMethodRegex.Matches(content))
            {
                int braceStart = content.IndexOf('{', m.Index + m.Length);
                if (braceStart < 0) continue;
                // Zwischen Signatur und { darf nur Whitespace stehen (sonst Aufruf/Expression-Body, keine Methode)
                if (content[(m.Index + m.Length)..braceStart].Any(c => !char.IsWhiteSpace(c))) continue;

                int braceEnd = FindMatchingBrace(content, braceStart + 1);
                if (braceEnd <= braceStart) continue;
                var body = content.Substring(braceStart, braceEnd - braceStart);

                foreach (Match a in PerFrameAllocRegex.Matches(body))
                {
                    var lineNum = GetLineNumber(content, braceStart + a.Index);
                    if (FileHelpers.IsSuppressed(file.Lines, lineNum - 1)) continue;
                    perFrameAllocs++;
                    if (perFrameAllocs <= 10)
                        results.Add(new(Severity.Warn, Category,
                            $"Per-Frame-Allokation '{a.Value.Trim()}' in Paint-/Render-Methode in {file.RelativePath}:{lineNum} → Paint/Filter als Feld cachen (GC-Druck/UI-Stutter)"));
                }
            }
        }
        if (perFrameAllocs == 0)
            results.Add(new(Severity.Pass, Category, "Keine Per-Frame-Allokationen (new SKPaint/SKMaskFilter) in Paint-Methoden"));
        else if (perFrameAllocs > 10)
            results.Add(new(Severity.Warn, Category, $"...und {perFrameAllocs - 10} weitere Per-Frame-Allokationen"));
    }

    static int FindMatchingBrace(string content, int startIndex)
    {
        int depth = 1;
        for (int i = startIndex; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
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
