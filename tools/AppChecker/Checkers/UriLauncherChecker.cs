using System.Text.RegularExpressions;
using AppChecker.Helpers;

namespace AppChecker.Checkers;

/// <summary>Prueft UriLauncher: Process.Start mit UseShellExecute in Shared-Projekten</summary>
class UriLauncherChecker : IChecker
{
    public string Category => "UriLauncher";

    public List<CheckResult> Check(CheckContext ctx)
    {
        var results = new List<CheckResult>();
        int processStartCount = 0;

        // Nur Shared-Dateien prüfen (Desktop darf Process.Start verwenden)
        foreach (var file in ctx.SharedCsFiles)
        {
            for (int i = 0; i < file.Lines.Length; i++)
            {
                var trimmed = file.Lines[i].TrimStart();
                if (trimmed.StartsWith("//")) continue;
                if (FileHelpers.IsSuppressed(file.Lines, i)) continue;

                // Process.Start mit UseShellExecute in Shared-Projekten
                if (trimmed.Contains("Process.Start") && !file.FullPath.Contains("Desktop"))
                {
                    processStartCount++;
                    results.Add(new(Severity.Warn, Category, $"Process.Start in Shared-Projekt {file.RelativePath}:{i + 1} → UriLauncher.OpenUri() verwenden (Android-kompatibel)"));
                }
            }
        }

        if (processStartCount == 0)
            results.Add(new(Severity.Pass, Category, "Kein Process.Start in Shared-Projekten (UriLauncher wird korrekt verwendet)"));

        return results;
    }
}
