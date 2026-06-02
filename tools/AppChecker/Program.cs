using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AppChecker;
using AppChecker.Checkers;
using AppChecker.Helpers;
using static AppChecker.Helpers.ConsoleHelpers;

// AppChecker v2.2 - Automatisches Pruef-Tool fuer alle 12 Avalonia Android Apps
// 33 Checker-Klassen, 200+ Pruefungen
//
// CLI: dotnet run --project tools/AppChecker [APP_NAME] [--quiet|-q|--fail-only|-f|--json]

var solutionRoot = FileHelpers.FindSolutionRoot();
if (solutionRoot == null)
{
    WriteColor("[FAIL] Solution-Root nicht gefunden (MeineApps.Ava.sln)", ConsoleColor.Red, true);
    return 2;
}

var allApps = new AppDef[]
{
    new("RechnerPlus", "com.meineapps.rechnerplus", false),
    new("ZeitManager", "com.meineapps.zeitmanager", false),
    new("FinanzRechner", "com.meineapps.finanzrechner", true),
    new("FitnessRechner", "com.meineapps.fitnessrechner", true),
    new("HandwerkerRechner", "com.meineapps.handwerkerrechner", true),
    new("WorkTimePro", "com.meineapps.worktimepro", true),
    new("HandwerkerImperium", "com.meineapps.handwerkerimperium", true),
    new("BomberBlast", "org.rsdigital.bomberblast", true),
    new("RebornSaga", "org.rsdigital.rebornsaga", true),
    new("BingXBot", "com.rsdigital.bingxbot", false),
    new("GardenControl", "com.rsdigital.gardencontrol", false),
    new("SmartMeasure", "com.rsdigital.smartmeasure", false),
};

IChecker[] checkers =
[
    // Projekt + Build
    new ProjectBuildChecker(),
    new BuildConfigChecker(),               // global

    // Android
    new AndroidChecker(),
    new AndroidGotchasChecker(),

    // Avalonia UI
    new AvaloniaUiChecker(),
    new AvaloniaGotchasChecker(),
    new ThemeChecker(),

    // Assets + Lokalisierung
    new LocalizationChecker(),
    new AssetsChecker(),

    // DI + ViewModel-Architektur
    new DiRegistrationChecker(),
    new VmWiringChecker(),
    new ViewBindingsChecker(),
    new NavigationChecker(),

    // MVVM-Pattern (komplett)
    new MvvmStrictChecker(),                // Service-Locator, DataContext im CB, Click-Handler
    new ConstructorInjectionChecker(),      // Parameterlose Ctors + Hard-Dependencies
    new CommunityToolkitChecker(),          // [ObservableProperty]/[RelayCommand] Konsistenz
    new AsyncRelayCommandChecker(),         // async void [RelayCommand] + CanExecute-Existenz
    new CodeBehindHygieneChecker(),         // Code-Behind-Komplexitaet + Service-Felder
    new DataContextChangedPatternChecker(), // View ↔ VM-Event Cleanup-Pattern
    new EventNamingConventionChecker(),     // NavigationRequested/MessageRequested/... Signaturen
    new ServiceConventionChecker(),         // I{Name}Service + Async-Suffix + Lifetime-Dups
    new DispatcherUIThreadChecker(),        // Cross-Thread-UI-Mutationen in Task.Run

    // Ad-Layout
    new AdLayoutChecker(),

    // Code-Qualitaet + Patterns
    new CodeQualityChecker(),
    new AsyncPatternsChecker(),
    new DateTimeChecker(),
    new SqliteChecker(),
    new SkiaSharpChecker(),
    new BillingChecker(),
    new UriLauncherChecker(),
    new EventCleanupChecker(),
    new DisposableChecker(),
    new HardcodedStringChecker(),
];

// === CLI-Parsing ===
var positionalArgs = new List<string>();
bool quietMode = false;
bool failOnlyMode = false;
bool jsonMode = false;

foreach (var arg in args)
{
    switch (arg)
    {
        case "--quiet": case "-q": quietMode = true; break;
        case "--fail-only": case "-f": failOnlyMode = true; break;
        case "--json": jsonMode = true; break;
        case "--help": case "-h": PrintHelp(); return 0;
        default: positionalArgs.Add(arg); break;
    }
}

// App-Filter (positional)
AppDef[] selectedApps = allApps;
if (positionalArgs.Count > 0)
{
    var filter = positionalArgs[0];
    var match = allApps.FirstOrDefault(a => a.Name.Equals(filter, StringComparison.OrdinalIgnoreCase));
    if (match == null)
    {
        WriteColor($"[FAIL] App '{filter}' nicht gefunden. Verfuegbar: {string.Join(", ", allApps.Select(a => a.Name))}",
                   ConsoleColor.Red, true);
        return 2;
    }
    selectedApps = [match];
}
else if (!jsonMode)
{
    selectedApps = InteractiveAppSelection(allApps, checkers.Length);
}

// === Globale Checks ===
var sw = Stopwatch.StartNew();
int totalPass = 0, totalInfo = 0, totalWarn = 0, totalFail = 0, totalChecks = 0;
var perAppStats = new List<(string Name, int P, int I, int W, int F, int Total)>();
var jsonReport = new List<object>();

var globalResults = new List<CheckResult>();
foreach (var checker in checkers)
    globalResults.AddRange(checker.CheckGlobal(solutionRoot));

if (globalResults.Count > 0 && !jsonMode)
{
    Console.WriteLine();
    WriteColor("= Global =", ConsoleColor.White, true);
    PrintAppGroup(globalResults, quietMode, failOnlyMode);

    totalPass += globalResults.Count(r => r.Severity == Severity.Pass);
    totalInfo += globalResults.Count(r => r.Severity == Severity.Info);
    totalWarn += globalResults.Count(r => r.Severity == Severity.Warn);
    totalFail += globalResults.Count(r => r.Severity == Severity.Fail);
    totalChecks += globalResults.Count;
}

// === Pro-App Checks ===
foreach (var app in selectedApps)
{
    var ctx = FileHelpers.CreateContext(app, solutionRoot);

    var allResults = new List<CheckResult>();
    foreach (var checker in checkers)
        allResults.AddRange(checker.Check(ctx));

    var appPass = allResults.Count(r => r.Severity == Severity.Pass);
    var appInfo = allResults.Count(r => r.Severity == Severity.Info);
    var appWarn = allResults.Count(r => r.Severity == Severity.Warn);
    var appFail = allResults.Count(r => r.Severity == Severity.Fail);
    perAppStats.Add((app.Name, appPass, appInfo, appWarn, appFail, allResults.Count));

    totalPass += appPass;
    totalInfo += appInfo;
    totalWarn += appWarn;
    totalFail += appFail;
    totalChecks += allResults.Count;

    if (jsonMode)
    {
        jsonReport.Add(new
        {
            App = app.Name,
            Counts = new { Pass = appPass, Info = appInfo, Warn = appWarn, Fail = appFail, Total = allResults.Count },
            Findings = allResults
                .Where(r => r.Severity != Severity.Pass) // PASS-Spam in JSON ausblenden
                .Select(r => new { Severity = r.Severity.ToString(), r.Category, r.Message })
                .ToList()
        });
        continue;
    }

    Console.WriteLine();
    WriteColor($"= {app.Name} =", ConsoleColor.White, true);
    PrintAppGroup(allResults, quietMode, failOnlyMode);

    // Pro-App Kompakt-Zeile
    Console.Write("  → ");
    WriteColor($"{appPass}P", ConsoleColor.Green); Console.Write(" ");
    WriteColor($"{appInfo}I", ConsoleColor.Cyan);  Console.Write(" ");
    WriteColor($"{appWarn}W", ConsoleColor.Yellow); Console.Write(" ");
    WriteColor($"{appFail}F", ConsoleColor.Red);
    Console.WriteLine($" ({allResults.Count} Checks)");
}

sw.Stop();

if (jsonMode)
{
    var jsonOut = new
    {
        Generated = DateTime.UtcNow.ToString("O"),
        ElapsedMs = sw.ElapsedMilliseconds,
        Apps = jsonReport,
        Summary = new { Pass = totalPass, Info = totalInfo, Warn = totalWarn, Fail = totalFail, Total = totalChecks }
    };
    Console.WriteLine(JsonSerializer.Serialize(jsonOut, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    // Per-App-Summary-Tabelle (nur bei >= 2 Apps)
    if (perAppStats.Count >= 2)
        PrintPerAppSummary(perAppStats);

    // Gesamt-Summary
    Console.WriteLine();
    WriteColor("= Summary =", ConsoleColor.White, true);
    Console.Write("  PASS: "); WriteColor(totalPass.ToString(), ConsoleColor.Green);
    Console.Write("  INFO: "); WriteColor(totalInfo.ToString(), ConsoleColor.Cyan);
    Console.Write("  WARN: "); WriteColor(totalWarn.ToString(), ConsoleColor.Yellow);
    Console.Write("  FAIL: "); WriteColor(totalFail.ToString(), ConsoleColor.Red);
    Console.WriteLine();
    Console.WriteLine($"  {totalChecks} Checks in {sw.ElapsedMilliseconds}ms ({checkers.Length} Checker, {selectedApps.Length} App{(selectedApps.Length > 1 ? "s" : "")})");
}

return totalFail > 0 ? 2 : totalWarn > 0 ? 1 : 0;

// === Helpers ===
static void PrintAppGroup(List<CheckResult> results, bool quiet, bool failOnly)
{
    IEnumerable<CheckResult> filtered = results;
    if (failOnly) filtered = results.Where(r => r.Severity == Severity.Fail);
    else if (quiet) filtered = results.Where(r => r.Severity == Severity.Warn || r.Severity == Severity.Fail);

    var grouped = filtered.GroupBy(r => r.Category).Where(g => g.Any());
    foreach (var group in grouped)
        PrintCategory(group.Key, group);
}

static void PrintPerAppSummary(List<(string Name, int P, int I, int W, int F, int Total)> stats)
{
    Console.WriteLine();
    WriteColor("= Per-App Summary =", ConsoleColor.White, true);

    int nameWidth = Math.Max(20, stats.Max(s => s.Name.Length) + 2);
    Console.WriteLine($"  {"App".PadRight(nameWidth)}| PASS | INFO | WARN | FAIL | Checks");
    Console.WriteLine($"  {new string('-', nameWidth)}+------+------+------+------+--------");

    foreach (var s in stats.OrderByDescending(s => s.F).ThenByDescending(s => s.W))
    {
        Console.Write($"  {s.Name.PadRight(nameWidth)}");
        Console.Write($"| {s.P,4} ");
        Console.Write($"| {s.I,4} ");
        // WARN gelb wenn > 0
        Console.Write("| ");
        WriteColor($"{s.W,4}", s.W > 0 ? ConsoleColor.Yellow : ConsoleColor.Gray);
        Console.Write(" | ");
        // FAIL rot wenn > 0
        WriteColor($"{s.F,4}", s.F > 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        Console.WriteLine($" | {s.Total,6}");
    }
}

static AppDef[] InteractiveAppSelection(AppDef[] apps, int checkerCount)
{
    Console.WriteLine();
    WriteColor($"=== AppChecker v2.2 ({checkerCount} Checker, 200+ Pruefungen) ===", ConsoleColor.White, true);
    Console.WriteLine();
    Console.WriteLine("Welche App(s) pruefen?");
    Console.WriteLine();
    Console.WriteLine($"  [0] Alle {apps.Length} Apps");
    for (int i = 0; i < apps.Length; i++)
        Console.WriteLine($"  [{i + 1}] {apps[i].Name}");
    Console.WriteLine();

    while (true)
    {
        Console.Write($"Auswahl (0-{apps.Length}, oder mehrere komma-getrennt z.B. 1,3,5): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input) || input == "0") return apps;

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selected = new List<AppDef>();
        bool valid = true;
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var idx) && idx >= 1 && idx <= apps.Length)
                selected.Add(apps[idx - 1]);
            else
            {
                WriteColor($"  Ungueltige Eingabe: '{part}' (erwartet 0-{apps.Length})", ConsoleColor.Red, true);
                Console.WriteLine();
                valid = false;
                break;
            }
        }
        if (valid && selected.Count > 0) return selected.ToArray();
    }
}

static void PrintHelp()
{
    Console.WriteLine("AppChecker v2.2 - Automatisches Pruef-Tool fuer 12 Avalonia-Apps");
    Console.WriteLine();
    Console.WriteLine("Verwendung:");
    Console.WriteLine("  dotnet run --project tools/AppChecker [APP_NAME] [OPTIONEN]");
    Console.WriteLine();
    Console.WriteLine("Optionen:");
    Console.WriteLine("  --quiet, -q       Nur WARN + FAIL anzeigen (Per-App-Summary bleibt)");
    Console.WriteLine("  --fail-only, -f   Nur FAIL anzeigen");
    Console.WriteLine("  --json            JSON-Output fuer CI/Tooling");
    Console.WriteLine("  --help, -h        Diese Hilfe");
    Console.WriteLine();
    Console.WriteLine("Exit-Codes:");
    Console.WriteLine("  0 = keine Warnungen/Fehler");
    Console.WriteLine("  1 = Warnungen vorhanden");
    Console.WriteLine("  2 = Fehler vorhanden");
    Console.WriteLine();
    Console.WriteLine("Beispiele:");
    Console.WriteLine("  dotnet run --project tools/AppChecker BomberBlast");
    Console.WriteLine("  dotnet run --project tools/AppChecker --quiet");
    Console.WriteLine("  dotnet run --project tools/AppChecker --json > report.json");
}
