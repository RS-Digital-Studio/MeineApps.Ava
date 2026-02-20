using System.Diagnostics;
using AppChecker;
using AppChecker.Checkers;
using AppChecker.Helpers;
using static AppChecker.Helpers.ConsoleHelpers;

// AppChecker v2.0 - Automatisches Pruef-Tool fuer alle 8 Avalonia Android Apps
// 22 Checker-Klassen, 150+ Pruefungen

var solutionRoot = FileHelpers.FindSolutionRoot();
if (solutionRoot == null)
{
    WriteColor("[FAIL] Solution-Root nicht gefunden (MeineApps.Ava.sln)", ConsoleColor.Red, true);
    return 2;
}

var apps = new AppDef[]
{
    new("RechnerPlus", "com.meineapps.rechnerplus", false),
    new("ZeitManager", "com.meineapps.zeitmanager", false),
    new("FinanzRechner", "com.meineapps.finanzrechner", true),
    new("FitnessRechner", "com.meineapps.fitnessrechner", true),
    new("HandwerkerRechner", "com.meineapps.handwerkerrechner", true),
    new("WorkTimePro", "com.meineapps.worktimepro", true),
    new("HandwerkerImperium", "com.meineapps.handwerkerimperium", true),
    new("BomberBlast", "org.rsdigital.bomberblast", true),
};

// Alle Checker instanziieren (Reihenfolge = Ausgabe-Reihenfolge)
IChecker[] checkers =
[
    // Bestehende (Phase 1)
    new ProjectBuildChecker(),
    new BuildConfigChecker(),           // NEU - global
    new AndroidChecker(),
    new AndroidGotchasChecker(),        // NEU
    new AvaloniaUiChecker(),
    new AvaloniaGotchasChecker(),       // NEU
    new ThemeChecker(),                 // NEU
    new LocalizationChecker(),
    new AssetsChecker(),
    new DiRegistrationChecker(),
    new VmWiringChecker(),
    new ViewBindingsChecker(),
    new NavigationChecker(),
    new AdLayoutChecker(),              // NEU
    new CodeQualityChecker(),
    new AsyncPatternsChecker(),         // NEU
    new DateTimeChecker(),              // NEU
    new SqliteChecker(),                // NEU
    new SkiaSharpChecker(),             // NEU
    new BillingChecker(),               // NEU
    new UriLauncherChecker(),           // NEU
    new EventCleanupChecker(),          // NEU
];

// CLI-Filter oder interaktiver Modus
if (args.Length > 0)
{
    var filter = args[0];
    var match = apps.FirstOrDefault(a => a.Name.Equals(filter, StringComparison.OrdinalIgnoreCase));
    if (match == null)
    {
        WriteColor($"[FAIL] App '{filter}' nicht gefunden. Verfuegbar: {string.Join(", ", apps.Select(a => a.Name))}", ConsoleColor.Red, true);
        return 2;
    }
    apps = [match];
}
else
{
    Console.WriteLine();
    WriteColor($"=== AppChecker v2.0 ({checkers.Length} Checker, 150+ Pruefungen) ===", ConsoleColor.White, true);
    Console.WriteLine();
    Console.WriteLine("Welche App(s) pruefen?");
    Console.WriteLine();
    Console.WriteLine("  [0] Alle 8 Apps");
    for (int i = 0; i < apps.Length; i++)
        Console.WriteLine($"  [{i + 1}] {apps[i].Name}");
    Console.WriteLine();

    while (true)
    {
        Console.Write("Auswahl (0-8, oder mehrere komma-getrennt z.B. 1,3,5): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input) || input == "0")
            break;

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selectedApps = new List<AppDef>();
        bool valid = true;
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var idx) && idx >= 1 && idx <= apps.Length)
                selectedApps.Add(apps[idx - 1]);
            else
            {
                WriteColor($"  Ungueltige Eingabe: '{part}' (erwartet 0-{apps.Length})", ConsoleColor.Red, true);
                Console.WriteLine();
                valid = false;
                break;
            }
        }
        if (valid && selectedApps.Count > 0)
        {
            apps = selectedApps.ToArray();
            break;
        }
    }
}

var sw = Stopwatch.StartNew();
int totalPass = 0, totalInfo = 0, totalWarn = 0, totalFail = 0, totalChecks = 0;

// Globale Checks (einmal fuer die gesamte Solution)
var globalResults = new List<CheckResult>();
foreach (var checker in checkers)
{
    var gResults = checker.CheckGlobal(solutionRoot);
    globalResults.AddRange(gResults);
}

if (globalResults.Count > 0)
{
    Console.WriteLine();
    WriteColor("= Global =", ConsoleColor.White, true);
    var grouped = globalResults.GroupBy(r => r.Category);
    foreach (var group in grouped)
        PrintCategory(group.Key, group);

    totalPass += globalResults.Count(r => r.Severity == Severity.Pass);
    totalInfo += globalResults.Count(r => r.Severity == Severity.Info);
    totalWarn += globalResults.Count(r => r.Severity == Severity.Warn);
    totalFail += globalResults.Count(r => r.Severity == Severity.Fail);
    totalChecks += globalResults.Count;
}

// Pro-App Checks
foreach (var app in apps)
{
    Console.WriteLine();
    WriteColor($"= {app.Name} =", ConsoleColor.White, true);

    var ctx = FileHelpers.CreateContext(app, solutionRoot);

    var allResults = new List<CheckResult>();
    foreach (var checker in checkers)
    {
        var results = checker.Check(ctx);
        allResults.AddRange(results);
    }

    // Gruppierte Ausgabe
    var grouped = allResults.GroupBy(r => r.Category).Where(g => g.Any());
    foreach (var group in grouped)
        PrintCategory(group.Key, group);

    // Pro-App Kompakt-Zeile
    var appPass = allResults.Count(r => r.Severity == Severity.Pass);
    var appInfo = allResults.Count(r => r.Severity == Severity.Info);
    var appWarn = allResults.Count(r => r.Severity == Severity.Warn);
    var appFail = allResults.Count(r => r.Severity == Severity.Fail);
    Console.Write("  → ");
    WriteColor($"{appPass}P", ConsoleColor.Green);
    Console.Write(" ");
    WriteColor($"{appInfo}I", ConsoleColor.Cyan);
    Console.Write(" ");
    WriteColor($"{appWarn}W", ConsoleColor.Yellow);
    Console.Write(" ");
    WriteColor($"{appFail}F", ConsoleColor.Red);
    Console.WriteLine($" ({allResults.Count} Checks)");

    totalPass += appPass;
    totalInfo += appInfo;
    totalWarn += appWarn;
    totalFail += appFail;
    totalChecks += allResults.Count;
}

sw.Stop();

// Summary
Console.WriteLine();
WriteColor("= Summary =", ConsoleColor.White, true);
Console.Write("  PASS: ");
WriteColor(totalPass.ToString(), ConsoleColor.Green);
Console.Write("  INFO: ");
WriteColor(totalInfo.ToString(), ConsoleColor.Cyan);
Console.Write("  WARN: ");
WriteColor(totalWarn.ToString(), ConsoleColor.Yellow);
Console.Write("  FAIL: ");
WriteColor(totalFail.ToString(), ConsoleColor.Red);
Console.WriteLine();
Console.WriteLine($"  {totalChecks} Checks in {sw.ElapsedMilliseconds}ms ({checkers.Length} Checker, {apps.Length} App{(apps.Length > 1 ? "s" : "")})");

return totalFail > 0 ? 2 : totalWarn > 0 ? 1 : 0;
