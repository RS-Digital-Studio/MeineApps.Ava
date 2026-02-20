using System.Text.RegularExpressions;

namespace AppChecker.Checkers;

/// <summary>Globaler Checker: Prueft Directory.Build.targets AOT-Flags und Build-Konfiguration</summary>
class BuildConfigChecker : IChecker
{
    public string Category => "Build-Config";

    public List<CheckResult> Check(CheckContext ctx) => []; // Nur global

    public List<CheckResult> CheckGlobal(string solutionRoot)
    {
        var results = new List<CheckResult>();

        var targetsPath = Path.Combine(solutionRoot, "Directory.Build.targets");
        if (!File.Exists(targetsPath))
        {
            results.Add(new(Severity.Warn, Category, "Directory.Build.targets nicht gefunden"));
            return results;
        }

        var content = File.ReadAllText(targetsPath);

        // AndroidEnableProfiledAot sollte false sein (Full AOT gegen JIT-Crash)
        if (content.Contains("<AndroidEnableProfiledAot>false</AndroidEnableProfiledAot>"))
            results.Add(new(Severity.Pass, Category, "Full AOT aktiv (AndroidEnableProfiledAot=false)"));
        else if (content.Contains("<AndroidEnableProfiledAot>true</AndroidEnableProfiledAot>"))
            results.Add(new(Severity.Fail, Category, "AndroidEnableProfiledAot=true → JIT-Crash auf manchen Geraeten (Huawei P30 etc.)"));
        else
            results.Add(new(Severity.Info, Category, "AndroidEnableProfiledAot nicht gesetzt (SDK-Default: Profiled AOT)"));

        // UseInterpreter + AOT gleichzeitig geht nicht
        if (content.Contains("<UseInterpreter>true</UseInterpreter>"))
        {
            if (content.Contains("AOT") || content.Contains("Aot"))
                results.Add(new(Severity.Fail, Category, "UseInterpreter=true zusammen mit AOT → XA0119 Warnung, Interpreter wird ignoriert"));
            else
                results.Add(new(Severity.Warn, Category, "UseInterpreter=true gesetzt"));
        }
        else
            results.Add(new(Severity.Pass, Category, "Kein UseInterpreter=true (korrekt)"));

        // AndroidLinkMode=None
        if (content.Contains("<AndroidLinkMode>None</AndroidLinkMode>"))
            results.Add(new(Severity.Warn, Category, "AndroidLinkMode=None → APK/AAB deutlich groesser, Linking empfohlen"));
        else
            results.Add(new(Severity.Pass, Category, "Kein AndroidLinkMode=None (Linking aktiv)"));

        // AndroidPackageFormat=aab
        if (content.Contains("<AndroidPackageFormat>aab</AndroidPackageFormat>"))
            results.Add(new(Severity.Pass, Category, "AndroidPackageFormat=aab (Play Store konform)"));
        else
            results.Add(new(Severity.Warn, Category, "AndroidPackageFormat nicht auf 'aab' gesetzt"));

        return results;
    }
}
