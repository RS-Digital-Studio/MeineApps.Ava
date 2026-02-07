using System.Text.RegularExpressions;
using System.Xml.Linq;

// AppChecker - Automatisches Pruef-Tool fuer alle 8 Avalonia Android Apps
// Prueft Projektstruktur, Android-Konfiguration, Avalonia-UI, Lokalisierung, Code-Qualitaet und Assets

var solutionRoot = FindSolutionRoot();
if (solutionRoot == null)
{
    WriteColor("[FAIL] Solution-Root nicht gefunden (MeineApps.Ava.sln)", ConsoleColor.Red);
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

// CLI-Filter oder interaktiver Modus
if (args.Length > 0)
{
    // CLI-Modus: App-Name als Argument
    var filter = args[0];
    var match = apps.FirstOrDefault(a => a.Name.Equals(filter, StringComparison.OrdinalIgnoreCase));
    if (match == null)
    {
        WriteColor($"[FAIL] App '{filter}' nicht gefunden. Verfuegbar: {string.Join(", ", apps.Select(a => a.Name))}", ConsoleColor.Red);
        return 2;
    }
    apps = [match];
}
else
{
    // Interaktiver Modus
    Console.WriteLine();
    WriteColor("=== AppChecker ===", ConsoleColor.White);
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
            break; // Alle Apps

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selectedApps = new List<AppDef>();
        bool valid = true;
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var idx) && idx >= 1 && idx <= apps.Length)
                selectedApps.Add(apps[idx - 1]);
            else
            {
                WriteColor($"  Ungueltige Eingabe: '{part}' (erwartet 0-{apps.Length})", ConsoleColor.Red);
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

int totalPass = 0, totalInfo = 0, totalWarn = 0, totalFail = 0;

foreach (var app in apps)
{
    Console.WriteLine();
    WriteColor($"= {app.Name} =", ConsoleColor.White);

    var appBase = Path.Combine(solutionRoot, "src", "Apps", app.Name);
    var sharedDir = Path.Combine(appBase, $"{app.Name}.Shared");
    var androidDir = Path.Combine(appBase, $"{app.Name}.Android");
    var desktopDir = Path.Combine(appBase, $"{app.Name}.Desktop");

    var results = new List<CheckResult>();
    results.AddRange(CheckProjectStructure(app, sharedDir, androidDir, desktopDir));
    results.AddRange(CheckAndroid(app, androidDir));
    results.AddRange(CheckAvaloniaUI(app, sharedDir));
    results.AddRange(CheckLocalization(app, sharedDir));
    results.AddRange(CheckCodeQuality(app, sharedDir, androidDir, desktopDir));
    results.AddRange(CheckAssets(app, sharedDir));
    results.AddRange(CheckDependencyInjection(app, sharedDir));
    results.AddRange(CheckViewModelWiring(app, sharedDir));
    results.AddRange(CheckViewBindings(app, sharedDir));
    results.AddRange(CheckNavigation(app, sharedDir));

    // Gruppierte Ausgabe
    var grouped = results.GroupBy(r => r.Category);
    foreach (var group in grouped)
    {
        WriteColor($"  [{group.Key}]", ConsoleColor.Gray);
        foreach (var r in group)
        {
            var (color, label) = r.Severity switch
            {
                Severity.Pass => (ConsoleColor.Green, "PASS"),
                Severity.Info => (ConsoleColor.Cyan, "INFO"),
                Severity.Warn => (ConsoleColor.Yellow, "WARN"),
                Severity.Fail => (ConsoleColor.Red, "FAIL"),
                _ => (ConsoleColor.White, "????")
            };
            Console.Write("    [");
            WriteColor(label, color);
            Console.WriteLine($"] {r.Message}");
        }
    }

    totalPass += results.Count(r => r.Severity == Severity.Pass);
    totalInfo += results.Count(r => r.Severity == Severity.Info);
    totalWarn += results.Count(r => r.Severity == Severity.Warn);
    totalFail += results.Count(r => r.Severity == Severity.Fail);
}

// Summary
Console.WriteLine();
WriteColor("= Summary =", ConsoleColor.White);
Console.Write("  PASS: ");
WriteColor(totalPass.ToString(), ConsoleColor.Green);
Console.Write("  INFO: ");
WriteColor(totalInfo.ToString(), ConsoleColor.Cyan);
Console.Write("  WARN: ");
WriteColor(totalWarn.ToString(), ConsoleColor.Yellow);
Console.Write("  FAIL: ");
WriteColor(totalFail.ToString(), ConsoleColor.Red);
Console.WriteLine();

return totalFail > 0 ? 2 : totalWarn > 0 ? 1 : 0;

// === Hilfsfunktionen ===

static string? FindSolutionRoot()
{
    // Versuche zuerst das bekannte Verzeichnis
    if (File.Exists(@"F:\Meine_Apps_Ava\MeineApps.Ava.sln"))
        return @"F:\Meine_Apps_Ava";

    // Fallback: vom aktuellen Verzeichnis aufwaerts suchen
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "MeineApps.Ava.sln")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static void WriteColor(string text, ConsoleColor color)
{
    var prev = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = prev;
    // Newline nur bei ganzen Zeilen (= und Summary-Header)
    if (text.StartsWith("=") || text.StartsWith("  ["))
        Console.WriteLine();
}

// === Check 1: Projekt/Build ===

static List<CheckResult> CheckProjectStructure(AppDef app, string sharedDir, string androidDir, string desktopDir)
{
    const string cat = "Projekt/Build";
    var results = new List<CheckResult>();

    // 3 Projekte existieren
    var sharedExists = Directory.Exists(sharedDir);
    var androidExists = Directory.Exists(androidDir);
    var desktopExists = Directory.Exists(desktopDir);
    if (sharedExists && androidExists && desktopExists)
        results.Add(new(Severity.Pass, cat, "3 Projekte vorhanden (Shared, Android, Desktop)"));
    else
    {
        if (!sharedExists) results.Add(new(Severity.Fail, cat, $"{app.Name}.Shared Verzeichnis fehlt"));
        if (!androidExists) results.Add(new(Severity.Fail, cat, $"{app.Name}.Android Verzeichnis fehlt"));
        if (!desktopExists) results.Add(new(Severity.Fail, cat, $"{app.Name}.Desktop Verzeichnis fehlt"));
        return results; // Kein Sinn weiterzupruefen
    }

    // Android .csproj pruefen
    var androidCsproj = Path.Combine(androidDir, $"{app.Name}.Android.csproj");
    if (File.Exists(androidCsproj))
    {
        var content = File.ReadAllText(androidCsproj);

        // ApplicationId
        var appIdMatch = Regex.Match(content, @"<ApplicationId>(.*?)</ApplicationId>");
        if (appIdMatch.Success)
        {
            var appId = appIdMatch.Groups[1].Value;
            if (appId == app.ExpectedAppId)
                results.Add(new(Severity.Pass, cat, $"ApplicationId: {appId}"));
            else
                results.Add(new(Severity.Warn, cat, $"ApplicationId '{appId}' erwartet '{app.ExpectedAppId}'"));

            // Lowercase domain check
            if (!Regex.IsMatch(appId, @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$"))
                results.Add(new(Severity.Warn, cat, $"ApplicationId '{appId}' enthaelt ungueltige Zeichen"));
        }
        else
            results.Add(new(Severity.Fail, cat, "ApplicationId fehlt in Android.csproj"));

        // ApplicationVersion (muss int sein)
        var versionMatch = Regex.Match(content, @"<ApplicationVersion>(.*?)</ApplicationVersion>");
        if (versionMatch.Success)
        {
            if (int.TryParse(versionMatch.Groups[1].Value, out var ver) && ver > 0)
                results.Add(new(Severity.Pass, cat, $"ApplicationVersion: {ver}"));
            else
                results.Add(new(Severity.Fail, cat, $"ApplicationVersion '{versionMatch.Groups[1].Value}' ist keine gueltige Ganzzahl"));
        }
        else
            results.Add(new(Severity.Fail, cat, "ApplicationVersion fehlt in Android.csproj"));

        // ApplicationDisplayVersion (semver-like)
        var displayVerMatch = Regex.Match(content, @"<ApplicationDisplayVersion>(.*?)</ApplicationDisplayVersion>");
        if (displayVerMatch.Success)
        {
            var dv = displayVerMatch.Groups[1].Value;
            if (Regex.IsMatch(dv, @"^\d+\.\d+\.\d+$"))
                results.Add(new(Severity.Pass, cat, $"ApplicationDisplayVersion: {dv}"));
            else
                results.Add(new(Severity.Warn, cat, $"ApplicationDisplayVersion '{dv}' ist kein Semver-Format"));
        }
        else
            results.Add(new(Severity.Fail, cat, "ApplicationDisplayVersion fehlt in Android.csproj"));

        // RuntimeIdentifiers
        var rtiMatch = Regex.Match(content, @"<RuntimeIdentifiers>(.*?)</RuntimeIdentifiers>");
        if (rtiMatch.Success)
        {
            var rti = rtiMatch.Groups[1].Value;
            var hasArm64 = rti.Contains("android-arm64");
            var hasX64 = rti.Contains("android-x64");
            if (hasArm64 && hasX64)
                results.Add(new(Severity.Pass, cat, $"RuntimeIdentifiers: {rti}"));
            else
            {
                if (!hasArm64) results.Add(new(Severity.Fail, cat, "android-arm64 fehlt in RuntimeIdentifiers"));
                if (!hasX64) results.Add(new(Severity.Warn, cat, "android-x64 fehlt in RuntimeIdentifiers"));
            }
        }
        else
            results.Add(new(Severity.Fail, cat, "RuntimeIdentifiers fehlt in Android.csproj"));
    }
    else
        results.Add(new(Severity.Fail, cat, $"{app.Name}.Android.csproj fehlt"));

    // Shared .csproj pruefen
    var sharedCsproj = Path.Combine(sharedDir, $"{app.Name}.Shared.csproj");
    if (File.Exists(sharedCsproj))
    {
        var content = File.ReadAllText(sharedCsproj);

        // RootNamespace
        var nsMatch = Regex.Match(content, @"<RootNamespace>(.*?)</RootNamespace>");
        if (nsMatch.Success)
        {
            if (nsMatch.Groups[1].Value == app.Name)
                results.Add(new(Severity.Pass, cat, $"RootNamespace: {app.Name}"));
            else
                results.Add(new(Severity.Warn, cat, $"RootNamespace '{nsMatch.Groups[1].Value}' erwartet '{app.Name}'"));
        }
        else
            results.Add(new(Severity.Info, cat, "RootNamespace nicht explizit gesetzt (Default wird verwendet)"));

        // AvaloniaResource Include="Assets\**"
        if (content.Contains("AvaloniaResource") && content.Contains("Assets"))
            results.Add(new(Severity.Pass, cat, "AvaloniaResource Include Assets vorhanden"));
        else
            results.Add(new(Severity.Fail, cat, "AvaloniaResource Include=\"Assets\\**\" fehlt in Shared.csproj"));
    }
    else
        results.Add(new(Severity.Fail, cat, $"{app.Name}.Shared.csproj fehlt"));

    return results;
}

// === Check 2: Android ===

static List<CheckResult> CheckAndroid(AppDef app, string androidDir)
{
    const string cat = "Android";
    var results = new List<CheckResult>();

    if (!Directory.Exists(androidDir))
    {
        results.Add(new(Severity.Fail, cat, "Android-Verzeichnis nicht gefunden"));
        return results;
    }

    // AndroidManifest.xml
    var manifestPath = Path.Combine(androidDir, "AndroidManifest.xml");
    if (File.Exists(manifestPath))
    {
        var content = File.ReadAllText(manifestPath);

        // INTERNET Permission
        if (content.Contains("android.permission.INTERNET"))
            results.Add(new(Severity.Pass, cat, "INTERNET Permission vorhanden"));
        else
            results.Add(new(Severity.Fail, cat, "INTERNET Permission fehlt in AndroidManifest.xml"));

        // Icon und RoundIcon
        if (content.Contains("@mipmap/appicon"))
            results.Add(new(Severity.Pass, cat, "Icon auf @mipmap/appicon gesetzt"));
        else
            results.Add(new(Severity.Warn, cat, "android:icon nicht auf @mipmap/appicon gesetzt"));

        if (content.Contains("@mipmap/appicon_round"))
            results.Add(new(Severity.Pass, cat, "RoundIcon auf @mipmap/appicon_round gesetzt"));
        else
            results.Add(new(Severity.Warn, cat, "android:roundIcon fehlt"));

        // Ad-spezifische Checks
        if (app.IsAdSupported)
        {
            if (content.Contains("com.google.android.gms.ads.APPLICATION_ID"))
                results.Add(new(Severity.Pass, cat, "AdMob APPLICATION_ID meta-data vorhanden"));
            else
                results.Add(new(Severity.Fail, cat, "AdMob APPLICATION_ID meta-data fehlt (Ad-App!)"));
        }
        else
        {
            if (content.Contains("AD_ID") && content.Contains("tools:node=\"remove\""))
                results.Add(new(Severity.Pass, cat, "AD_ID Permission korrekt entfernt (tools:node=\"remove\")"));
            else
                results.Add(new(Severity.Warn, cat, "AD_ID Permission nicht explizit entfernt (Non-Ad-App)"));
        }
    }
    else
        results.Add(new(Severity.Fail, cat, "AndroidManifest.xml fehlt"));

    // Mipmap-Verzeichnisse
    var resourcesDir = Path.Combine(androidDir, "Resources");
    var expectedMipmaps = new[] { "mipmap-mdpi", "mipmap-hdpi", "mipmap-xhdpi", "mipmap-xxhdpi", "mipmap-xxxhdpi", "mipmap-anydpi-v26" };
    if (Directory.Exists(resourcesDir))
    {
        var missingMipmaps = expectedMipmaps.Where(m => !Directory.Exists(Path.Combine(resourcesDir, m))).ToList();
        if (missingMipmaps.Count == 0)
            results.Add(new(Severity.Pass, cat, "Alle 6 Mipmap-Verzeichnisse vorhanden"));
        else
            foreach (var m in missingMipmaps)
                results.Add(new(Severity.Warn, cat, $"Mipmap-Verzeichnis fehlt: {m}"));

        // styles.xml
        var valuesDir = Path.Combine(resourcesDir, "values");
        if (Directory.Exists(valuesDir) && File.Exists(Path.Combine(valuesDir, "styles.xml")))
            results.Add(new(Severity.Pass, cat, "styles.xml vorhanden"));
        else
            results.Add(new(Severity.Warn, cat, "Resources/values/styles.xml fehlt"));
    }
    else
        results.Add(new(Severity.Fail, cat, "Resources-Verzeichnis fehlt"));

    // MainActivity.cs AdMob Lifecycle
    var mainActivityPath = Path.Combine(androidDir, "MainActivity.cs");
    if (app.IsAdSupported && File.Exists(mainActivityPath))
    {
        var content = File.ReadAllText(mainActivityPath);
        var hasInit = content.Contains("AdMobHelper.Initialize") || content.Contains("AdMobHelper");
        var hasResume = content.Contains("OnResume");
        var hasPause = content.Contains("OnPause");
        var hasDestroy = content.Contains("OnDestroy");

        if (hasInit && hasResume && hasPause && hasDestroy)
            results.Add(new(Severity.Pass, cat, "AdMob Lifecycle komplett (Init/Resume/Pause/Destroy)"));
        else
        {
            if (!hasInit) results.Add(new(Severity.Fail, cat, "AdMobHelper.Initialize fehlt in MainActivity"));
            if (!hasResume) results.Add(new(Severity.Warn, cat, "OnResume (AdMob) fehlt in MainActivity"));
            if (!hasPause) results.Add(new(Severity.Warn, cat, "OnPause (AdMob) fehlt in MainActivity"));
            if (!hasDestroy) results.Add(new(Severity.Warn, cat, "OnDestroy (AdMob) fehlt in MainActivity"));
        }
    }

    return results;
}

// === Check 3: Avalonia/UI ===

static List<CheckResult> CheckAvaloniaUI(AppDef app, string sharedDir)
{
    const string cat = "Avalonia/UI";
    var results = new List<CheckResult>();

    if (!Directory.Exists(sharedDir))
    {
        results.Add(new(Severity.Fail, cat, "Shared-Verzeichnis nicht gefunden"));
        return results;
    }

    // App.axaml
    var appAxaml = Path.Combine(sharedDir, "App.axaml");
    if (File.Exists(appAxaml))
    {
        var content = File.ReadAllText(appAxaml);

        if (content.Contains("MaterialIconStyles"))
            results.Add(new(Severity.Pass, cat, "MaterialIconStyles registriert in App.axaml"));
        else
            results.Add(new(Severity.Fail, cat, "MaterialIconStyles NICHT registriert in App.axaml → Icons unsichtbar!"));
    }
    else
        results.Add(new(Severity.Fail, cat, "App.axaml fehlt"));

    // App.axaml.cs
    var appAxamlCs = Path.Combine(sharedDir, "App.axaml.cs");
    if (File.Exists(appAxamlCs))
    {
        var content = File.ReadAllText(appAxamlCs);

        // ThemeService beim Start aufgeloest
        if (content.Contains("GetRequiredService<IThemeService>") || content.Contains("GetService<IThemeService>"))
            results.Add(new(Severity.Pass, cat, "IThemeService beim Start aufgeloest"));
        else
            results.Add(new(Severity.Warn, cat, "IThemeService wird nicht beim Start aufgeloest (Theme wird nicht geladen)"));

        // ILocalizationService registriert
        if (content.Contains("ILocalizationService"))
            results.Add(new(Severity.Pass, cat, "ILocalizationService registriert"));
        else
            results.Add(new(Severity.Fail, cat, "ILocalizationService nicht gefunden in App.axaml.cs"));
    }
    else
        results.Add(new(Severity.Fail, cat, "App.axaml.cs fehlt"));

    return results;
}

// === Check 4: Lokalisierung ===

static List<CheckResult> CheckLocalization(AppDef app, string sharedDir)
{
    const string cat = "Lokalisierung";
    var results = new List<CheckResult>();

    var stringsDir = Path.Combine(sharedDir, "Resources", "Strings");
    if (!Directory.Exists(stringsDir))
    {
        results.Add(new(Severity.Fail, cat, "Resources/Strings Verzeichnis fehlt"));
        return results;
    }

    // 6 .resx Dateien
    var baseResx = Path.Combine(stringsDir, "AppStrings.resx");
    var languages = new[] { "de", "es", "fr", "it", "pt" };
    var langFiles = languages.Select(l => (Lang: l, Path: Path.Combine(stringsDir, $"AppStrings.{l}.resx"))).ToList();

    if (File.Exists(baseResx))
        results.Add(new(Severity.Pass, cat, "AppStrings.resx (Base) vorhanden"));
    else
    {
        results.Add(new(Severity.Fail, cat, "AppStrings.resx (Base) fehlt"));
        return results;
    }

    var missingLangs = langFiles.Where(l => !File.Exists(l.Path)).Select(l => l.Lang).ToList();
    if (missingLangs.Count == 0)
        results.Add(new(Severity.Pass, cat, "Alle 5 Sprachdateien vorhanden (de/es/fr/it/pt)"));
    else
        foreach (var lang in missingLangs)
            results.Add(new(Severity.Fail, cat, $"AppStrings.{lang}.resx fehlt"));

    // Designer.cs
    var designerCs = Path.Combine(stringsDir, "AppStrings.Designer.cs");
    if (File.Exists(designerCs))
    {
        var fileInfo = new FileInfo(designerCs);
        if (fileInfo.Length > 100)
            results.Add(new(Severity.Pass, cat, "AppStrings.Designer.cs vorhanden und nicht leer"));
        else
            results.Add(new(Severity.Warn, cat, "AppStrings.Designer.cs ist fast leer"));
    }
    else
        results.Add(new(Severity.Warn, cat, "AppStrings.Designer.cs fehlt (wird beim Build generiert)"));

    // Key-Vergleich: Fehlende Keys in Sprach-Dateien
    var baseKeys = ExtractResxKeys(baseResx);
    if (baseKeys.Count > 0)
    {
        results.Add(new(Severity.Info, cat, $"Base hat {baseKeys.Count} Keys"));

        foreach (var langFile in langFiles.Where(l => File.Exists(l.Path)))
        {
            var langKeys = ExtractResxKeys(langFile.Path);
            var missing = baseKeys.Except(langKeys).ToList();
            if (missing.Count == 0)
                results.Add(new(Severity.Pass, cat, $"{langFile.Lang}: Alle {baseKeys.Count} Keys vorhanden"));
            else
                results.Add(new(Severity.Warn, cat, $"{langFile.Lang}: {missing.Count} Keys fehlen ({string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? "..." : "")})"));
        }
    }

    return results;
}

static HashSet<string> ExtractResxKeys(string resxPath)
{
    var keys = new HashSet<string>();
    try
    {
        var doc = XDocument.Load(resxPath);
        foreach (var data in doc.Descendants("data"))
        {
            var name = data.Attribute("name")?.Value;
            if (name != null)
                keys.Add(name);
        }
    }
    catch
    {
        // Fehler beim Parsen - leeres Set zurueck
    }
    return keys;
}

// === Check 5: Code Quality ===

static List<CheckResult> CheckCodeQuality(AppDef app, string sharedDir, string androidDir, string desktopDir)
{
    const string cat = "Code Quality";
    var results = new List<CheckResult>();

    var dirs = new[] { sharedDir, androidDir, desktopDir }.Where(Directory.Exists).ToArray();
    var csFiles = dirs.SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories))
        // obj/bin Verzeichnisse ausschliessen
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                  && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
        .ToList();

    int debugWriteLineCount = 0;
    int unusedExCount = 0;
    int invalidateVisualCount = 0;
    int dateTimeParseCount = 0;

    foreach (var file in csFiles)
    {
        var lines = File.ReadAllLines(file);
        var relPath = GetRelativePath(file, Path.GetDirectoryName(Path.GetDirectoryName(sharedDir))!);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Auskommentierte Zeilen ueberspringen
            if (trimmed.StartsWith("//"))
                continue;

            // Debug.WriteLine
            if (trimmed.Contains("Debug.WriteLine"))
            {
                debugWriteLineCount++;
                results.Add(new(Severity.Warn, cat, $"Debug.WriteLine in {relPath}:{i + 1}"));
            }

            // catch (Exception ex) mit ungenutztem ex
            if (Regex.IsMatch(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)"))
            {
                var varMatch = Regex.Match(trimmed, @"catch\s*\(\s*Exception\s+(\w+)\s*\)");
                if (varMatch.Success)
                {
                    var varName = varMatch.Groups[1].Value;
                    // Pruefen ob Variable in den naechsten 5 Zeilen verwendet wird
                    bool used = false;
                    for (int j = i + 1; j < Math.Min(i + 6, lines.Length); j++)
                    {
                        var checkLine = lines[j].TrimStart();
                        if (checkLine.StartsWith("//")) continue;
                        // Variable ist verwendet wenn sie vorkommt (aber nicht in einem weiteren catch)
                        if (checkLine.Contains(varName) && !checkLine.Contains("catch"))
                        {
                            used = true;
                            break;
                        }
                        // Block-Ende erkennen
                        if (checkLine.TrimStart() == "}")
                            break;
                    }
                    if (!used)
                    {
                        unusedExCount++;
                        results.Add(new(Severity.Warn, cat, $"Ungenutztes 'ex' in catch at {relPath}:{i + 1}"));
                    }
                }
            }

            // InvalidateVisual() in SKCanvasView-Dateien
            if (trimmed.Contains("InvalidateVisual()"))
            {
                // Pruefen ob es eine SKCanvasView-Datei ist
                var fileContent = string.Join('\n', lines);
                if (fileContent.Contains("SKCanvasView") || fileContent.Contains("PaintSurface"))
                {
                    invalidateVisualCount++;
                    results.Add(new(Severity.Fail, cat, $"InvalidateVisual() statt InvalidateSurface() in {relPath}:{i + 1}"));
                }
            }

            // DateTime.Parse ohne RoundtripKind
            if (trimmed.Contains("DateTime.Parse(") || trimmed.Contains("DateTime.Parse ("))
            {
                // Pruefen ob RoundtripKind in der gleichen oder naechsten Zeile steht
                var contextLines = string.Join(' ', lines.Skip(i).Take(3));
                if (!contextLines.Contains("RoundtripKind"))
                {
                    dateTimeParseCount++;
                    results.Add(new(Severity.Warn, cat, $"DateTime.Parse ohne RoundtripKind in {relPath}:{i + 1}"));
                }
            }
        }
    }

    // Zusammenfassung wenn keine Issues
    if (debugWriteLineCount == 0)
        results.Add(new(Severity.Pass, cat, "Keine Debug.WriteLine Reste gefunden"));
    if (unusedExCount == 0)
        results.Add(new(Severity.Pass, cat, "Keine ungenutzten Exception-Variablen"));
    if (invalidateVisualCount == 0)
        results.Add(new(Severity.Pass, cat, "Keine InvalidateVisual() in Canvas-Dateien"));
    if (dateTimeParseCount == 0)
        results.Add(new(Severity.Pass, cat, "Keine DateTime.Parse ohne RoundtripKind"));

    return results;
}

// === Check 6: Assets ===

static List<CheckResult> CheckAssets(AppDef app, string sharedDir)
{
    const string cat = "Assets";
    var results = new List<CheckResult>();

    if (!Directory.Exists(sharedDir))
    {
        results.Add(new(Severity.Fail, cat, "Shared-Verzeichnis nicht gefunden"));
        return results;
    }

    // icon.png in Assets/
    var assetsDir = Path.Combine(sharedDir, "Assets");
    if (Directory.Exists(assetsDir))
    {
        var iconPath = Path.Combine(assetsDir, "icon.png");
        if (File.Exists(iconPath))
            results.Add(new(Severity.Pass, cat, "icon.png in Assets/ vorhanden"));
        else
            results.Add(new(Severity.Warn, cat, "icon.png fehlt in Assets/"));
    }
    else
        results.Add(new(Severity.Fail, cat, "Assets-Verzeichnis fehlt"));

    // MainWindow.axaml referenziert Icon via avares://
    var mainWindowPaths = Directory.GetFiles(sharedDir, "MainWindow.axaml", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
        .ToList();

    if (mainWindowPaths.Count > 0)
    {
        var content = File.ReadAllText(mainWindowPaths[0]);
        if (content.Contains("avares://") && content.Contains("icon"))
            results.Add(new(Severity.Pass, cat, "MainWindow.axaml referenziert Icon via avares://"));
        else
            results.Add(new(Severity.Warn, cat, "MainWindow.axaml: Icon-Referenz via avares:// nicht gefunden"));
    }
    else
    {
        // MainWindow kann auch im Desktop-Projekt liegen
        results.Add(new(Severity.Info, cat, "MainWindow.axaml nicht im Shared-Projekt (evtl. im Desktop-Projekt)"));
    }

    return results;
}

static string GetRelativePath(string fullPath, string basePath)
{
    if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        return fullPath[(basePath.Length + 1)..];
    return Path.GetFileName(fullPath);
}

// === Check 7: DI-Registrierung ===

static List<CheckResult> CheckDependencyInjection(AppDef app, string sharedDir)
{
    const string cat = "DI-Registrierung";
    var results = new List<CheckResult>();

    var appAxamlCs = Path.Combine(sharedDir, "App.axaml.cs");
    if (!File.Exists(appAxamlCs))
    {
        results.Add(new(Severity.Fail, cat, "App.axaml.cs fehlt"));
        return results;
    }

    var content = File.ReadAllText(appAxamlCs);

    // ConfigureServices Methode existiert
    if (Regex.IsMatch(content, @"void\s+ConfigureServices\s*\(\s*IServiceCollection"))
        results.Add(new(Severity.Pass, cat, "ConfigureServices Methode vorhanden"));
    else
    {
        results.Add(new(Severity.Fail, cat, "ConfigureServices Methode fehlt"));
        return results;
    }

    // IPreferencesService mit korrektem AppName
    var prefsMatch = Regex.Match(content, @"new\s+PreferencesService\s*\(\s*""(\w+)""\s*\)");
    if (prefsMatch.Success)
    {
        if (prefsMatch.Groups[1].Value == app.Name)
            results.Add(new(Severity.Pass, cat, $"IPreferencesService mit AppName '{app.Name}'"));
        else
            results.Add(new(Severity.Warn, cat, $"PreferencesService AppName '{prefsMatch.Groups[1].Value}' erwartet '{app.Name}'"));
    }
    else
        results.Add(new(Severity.Warn, cat, "IPreferencesService Registrierung nicht gefunden"));

    // IThemeService registriert
    if (Regex.IsMatch(content, @"AddSingleton<IThemeService"))
        results.Add(new(Severity.Pass, cat, "IThemeService registriert"));
    else
        results.Add(new(Severity.Fail, cat, "IThemeService nicht im DI registriert"));

    // ILocalizationService mit AppStrings.ResourceManager
    if (content.Contains("AppStrings.ResourceManager"))
        results.Add(new(Severity.Pass, cat, "ILocalizationService mit AppStrings.ResourceManager"));
    else
        results.Add(new(Severity.Fail, cat, "ILocalizationService/AppStrings.ResourceManager nicht gefunden"));

    // Ad-Apps: AddMeineAppsPremium()
    if (app.IsAdSupported)
    {
        if (content.Contains("AddMeineAppsPremium"))
            results.Add(new(Severity.Pass, cat, "AddMeineAppsPremium() vorhanden (Ad-App)"));
        else
            results.Add(new(Severity.Fail, cat, "AddMeineAppsPremium() fehlt (Ad-App!)"));
    }

    // MainViewModel registriert
    if (Regex.IsMatch(content, @"Add(Singleton|Transient)<MainViewModel>"))
        results.Add(new(Severity.Pass, cat, "MainViewModel im DI registriert"));
    else
        results.Add(new(Severity.Fail, cat, "MainViewModel nicht im DI registriert"));

    // Cross-Check: MainVM Constructor-Parameter vs. DI-Registrierungen
    var mainVmPath = Path.Combine(sharedDir, "ViewModels", "MainViewModel.cs");
    if (File.Exists(mainVmPath))
    {
        var constructorParams = ExtractConstructorVmParameters(mainVmPath);
        var diRegistrations = ExtractDiRegistrations(content);

        foreach (var param in constructorParams)
        {
            if (diRegistrations.Contains(param))
                results.Add(new(Severity.Pass, cat, $"Constructor-VM '{param}' im DI registriert"));
            else
                results.Add(new(Severity.Warn, cat, $"Constructor-VM '{param}' NICHT im DI registriert"));
        }
    }

    return results;
}

// === Check 8: ViewModel-Verdrahtung ===

static List<CheckResult> CheckViewModelWiring(AppDef app, string sharedDir)
{
    const string cat = "VM-Verdrahtung";
    var results = new List<CheckResult>();

    var mainVmPath = Path.Combine(sharedDir, "ViewModels", "MainViewModel.cs");
    if (!File.Exists(mainVmPath))
    {
        results.Add(new(Severity.Fail, cat, "MainViewModel.cs fehlt"));
        return results;
    }

    results.Add(new(Severity.Pass, cat, "MainViewModel.cs vorhanden"));
    var content = File.ReadAllText(mainVmPath);

    // Tab/Screen-Navigation Properties (IsXxxActive, _isXxxActive, IsXxxTab)
    var activeProps = Regex.Matches(content, @"bool\s+Is(\w+)Active\b");
    var activeFields = Regex.Matches(content, @"bool\s+_is(\w+)Active\b");
    var tabProps = Regex.Matches(content, @"bool\s+Is(\w+)Tab\b");
    // Alle zusammenfuehren, Duplikate entfernen
    var activeNames = activeProps.Select(m => m.Groups[1].Value)
        .Union(activeFields.Select(m => m.Groups[1].Value))
        .Union(tabProps.Select(m => m.Groups[1].Value))
        .Distinct().ToList();
    if (activeNames.Count >= 2)
        results.Add(new(Severity.Pass, cat, $"{activeNames.Count} Tab/Screen Properties ({string.Join(", ", activeNames)})"));
    else
        results.Add(new(Severity.Warn, cat, $"Nur {activeNames.Count} Tab/Screen Properties (erwartet >= 2)"));

    // SelectedTab Property
    if (Regex.IsMatch(content, @"_selectedTab\w*\s*;") || Regex.IsMatch(content, @"SelectedTab\w*\s*\{"))
        results.Add(new(Severity.Pass, cat, "SelectedTab/SelectedTabIndex Property vorhanden"));
    else
        results.Add(new(Severity.Info, cat, "Kein SelectedTab Property (evtl. Screen-basiert)"));

    // NavigateTo Commands
    if (Regex.IsMatch(content, @"\[RelayCommand\]") && Regex.IsMatch(content, @"void\s+(NavigateTo\w*|Select\w*Tab\w*)\s*\("))
        results.Add(new(Severity.Pass, cat, "NavigateTo/SelectTab Commands vorhanden"));
    else if (Regex.IsMatch(content, @"NavigateTo\w*\s*\("))
        results.Add(new(Severity.Pass, cat, "NavigateTo Methode vorhanden"));
    else
        results.Add(new(Severity.Warn, cat, "Keine NavigateTo/SelectTab Commands gefunden"));

    // LanguageChanged abonniert
    if (Regex.IsMatch(content, @"LanguageChanged\s*\+="))
        results.Add(new(Severity.Pass, cat, "LanguageChanged Event abonniert"));
    else
        results.Add(new(Severity.Warn, cat, "LanguageChanged Event nicht abonniert"));

    // UpdateLocalizedTexts Cross-Check
    var vmDir = Path.Combine(sharedDir, "ViewModels");
    if (Directory.Exists(vmDir))
    {
        var childVmFiles = Directory.GetFiles(vmDir, "*ViewModel.cs")
            .Where(f => !f.EndsWith("MainViewModel.cs"))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var childVmFile in childVmFiles)
        {
            var childContent = File.ReadAllText(childVmFile);
            var childName = Path.GetFileNameWithoutExtension(childVmFile);

            // Nur pruefen wenn der Child-VM UpdateLocalizedTexts hat
            if (!childContent.Contains("void UpdateLocalizedTexts"))
                continue;

            // Pruefen ob MainVM diesen Child-VM's UpdateLocalizedTexts aufruft
            // Pattern: PropertyName.UpdateLocalizedTexts() oder vmName.UpdateLocalizedTexts()
            var shortName = childName.Replace("ViewModel", "");
            if (Regex.IsMatch(content, $@"{childName}\.UpdateLocalizedTexts|{shortName}\w*\.UpdateLocalizedTexts|{shortName}\w*Vm\.UpdateLocalizedTexts"))
                results.Add(new(Severity.Pass, cat, $"{childName}.UpdateLocalizedTexts() wird aufgerufen"));
            else
                results.Add(new(Severity.Warn, cat, $"{childName}.UpdateLocalizedTexts() wird NICHT in MainVM aufgerufen"));
        }
    }

    // MessageRequested verdrahtet
    var msgRequestedCount = Regex.Matches(content, @"\.MessageRequested\s*\+=").Count;
    if (msgRequestedCount > 0)
        results.Add(new(Severity.Pass, cat, $"{msgRequestedCount}x MessageRequested Events verdrahtet"));
    else
        results.Add(new(Severity.Info, cat, "Keine MessageRequested Events verdrahtet"));

    // Tab-Wechsel schliesst Overlays (nur relevant wenn Overlays existieren)
    bool hasOverlays = content.Contains("CurrentPage") || content.Contains("CurrentCalculatorVm") || content.Contains("IsOverlay");
    if (hasOverlays)
    {
        if (Regex.IsMatch(content, @"On\w*SelectedTab\w*Changed|partial\s+void\s+On\w*Tab\w*Changed"))
            results.Add(new(Severity.Pass, cat, "Tab-Wechsel Handler vorhanden (Overlay-Schliessung)"));
        else if (content.Contains("CurrentPage") && content.Contains("= null"))
            results.Add(new(Severity.Pass, cat, "Overlay-Schliessung via CurrentPage = null"));
        else
            results.Add(new(Severity.Warn, cat, "Overlays vorhanden aber kein Tab-Wechsel Handler fuer Schliessung"));
    }

    // GoBackAction/NavigationRequested verdrahtet
    var goBackCount = Regex.Matches(content, @"\.GoBackAction\s*=").Count;
    var navRequestedCount = Regex.Matches(content, @"\.NavigationRequested\s*\+=").Count;
    if (goBackCount + navRequestedCount > 0)
        results.Add(new(Severity.Pass, cat, $"{goBackCount}x GoBackAction + {navRequestedCount}x NavigationRequested verdrahtet"));
    else
        results.Add(new(Severity.Info, cat, "Keine GoBackAction/NavigationRequested verdrahtet"));

    return results;
}

// === Check 9: View-ViewModel Binding ===

static List<CheckResult> CheckViewBindings(AppDef app, string sharedDir)
{
    const string cat = "View-Bindings";
    var results = new List<CheckResult>();

    var viewsDir = Path.Combine(sharedDir, "Views");
    var vmDir = Path.Combine(sharedDir, "ViewModels");

    if (!Directory.Exists(viewsDir))
    {
        results.Add(new(Severity.Warn, cat, "Views-Verzeichnis fehlt"));
        return results;
    }

    // View- und ViewModel-Dateien sammeln
    var viewFiles = Directory.GetFiles(viewsDir, "*.axaml", SearchOption.AllDirectories)
        .Where(f => !f.Contains("MainWindow") && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
        .ToList();

    var vmFiles = Directory.Exists(vmDir)
        ? Directory.GetFiles(vmDir, "*ViewModel.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet()
        : new HashSet<string>();

    // MainView x:DataType Check
    var mainViewPath = viewFiles.FirstOrDefault(f => Path.GetFileName(f) == "MainView.axaml");
    if (mainViewPath != null)
    {
        var mainViewContent = File.ReadAllText(mainViewPath);
        if (mainViewContent.Contains("x:DataType=\"vm:MainViewModel\"") || mainViewContent.Contains("x:DataType=\"viewmodels:MainViewModel\""))
            results.Add(new(Severity.Pass, cat, "MainView hat x:DataType=MainViewModel"));
        else if (mainViewContent.Contains("x:DataType="))
            results.Add(new(Severity.Pass, cat, "MainView hat x:DataType gesetzt"));
        else
            results.Add(new(Severity.Warn, cat, "MainView hat KEIN x:DataType gesetzt"));
    }

    int viewsWithDataType = 0, viewsWithoutDataType = 0;
    int viewsWithVmNs = 0, viewsWithoutVmNs = 0;
    int staticResourceBrushCount = 0, dynamicResourceBrushCount = 0;

    foreach (var viewFile in viewFiles)
    {
        var content = File.ReadAllText(viewFile);
        var fileName = Path.GetFileNameWithoutExtension(viewFile);

        // x:DataType Check
        if (content.Contains("x:DataType="))
            viewsWithDataType++;
        else
            viewsWithoutDataType++;

        // xmlns:vm Namespace
        if (Regex.IsMatch(content, @"xmlns:(vm|viewmodels)\s*=\s*""using:"))
            viewsWithVmNs++;
        else
            viewsWithoutVmNs++;

        // StaticResource vs DynamicResource fuer Brushes
        staticResourceBrushCount += Regex.Matches(content, @"\{StaticResource\s+\w*Brush\}").Count;
        dynamicResourceBrushCount += Regex.Matches(content, @"\{DynamicResource\s+\w*Brush\}").Count;
    }

    // View x:DataType Zusammenfassung
    if (viewsWithoutDataType == 0)
        results.Add(new(Severity.Pass, cat, $"Alle {viewsWithDataType} Views haben x:DataType"));
    else
        results.Add(new(Severity.Warn, cat, $"{viewsWithoutDataType}/{viewFiles.Count} Views OHNE x:DataType"));

    // xmlns:vm Zusammenfassung
    if (viewsWithoutVmNs == 0)
        results.Add(new(Severity.Pass, cat, $"Alle {viewsWithVmNs} Views haben xmlns:vm"));
    else
        results.Add(new(Severity.Info, cat, $"{viewsWithoutVmNs}/{viewFiles.Count} Views ohne xmlns:vm (nicht alle brauchen es)"));

    // StaticResource Brush Warnungen
    if (staticResourceBrushCount > 0)
        results.Add(new(Severity.Warn, cat, $"{staticResourceBrushCount}x StaticResource fuer Brush (sollte DynamicResource sein fuer Theme-Wechsel)"));
    if (dynamicResourceBrushCount > 0)
        results.Add(new(Severity.Pass, cat, $"{dynamicResourceBrushCount}x DynamicResource fuer Brush (korrekt)"));

    // View ↔ ViewModel Paar-Check
    int viewsWithVm = 0, viewsWithoutVm = 0;
    var viewsWithoutVmList = new List<string>();
    foreach (var viewFile in viewFiles)
    {
        var viewName = Path.GetFileNameWithoutExtension(viewFile); // z.B. "CalculatorView"
        var expectedVm = viewName.Replace("View", "ViewModel");   // z.B. "CalculatorViewModel"
        if (vmFiles.Contains(expectedVm))
            viewsWithVm++;
        else
        {
            viewsWithoutVm++;
            viewsWithoutVmList.Add(viewName);
        }
    }

    if (viewsWithoutVm == 0)
        results.Add(new(Severity.Pass, cat, $"Alle {viewsWithVm} Views haben ein passendes ViewModel"));
    else
        results.Add(new(Severity.Warn, cat, $"{viewsWithoutVm} Views ohne ViewModel: {string.Join(", ", viewsWithoutVmList.Take(5))}{(viewsWithoutVmList.Count > 5 ? "..." : "")}"));

    // ViewModel ohne View (nur INFO)
    var viewNames = viewFiles.Select(f => Path.GetFileNameWithoutExtension(f).Replace("View", "ViewModel")).ToHashSet();
    var vmsWithoutView = vmFiles.Where(vm => !viewNames.Contains(vm) && vm != "MainViewModel").ToList();
    if (vmsWithoutView.Count > 0)
        results.Add(new(Severity.Info, cat, $"{vmsWithoutView.Count} VMs ohne eigene View: {string.Join(", ", vmsWithoutView.Take(5))}{(vmsWithoutView.Count > 5 ? "..." : "")}"));

    return results;
}

// === Check 10: Navigation-Vollstaendigkeit ===

static List<CheckResult> CheckNavigation(AppDef app, string sharedDir)
{
    const string cat = "Navigation";
    var results = new List<CheckResult>();

    var mainViewPath = FindMainView(sharedDir);
    var mainVmPath = Path.Combine(sharedDir, "ViewModels", "MainViewModel.cs");

    if (mainViewPath == null)
    {
        results.Add(new(Severity.Warn, cat, "MainView.axaml nicht gefunden"));
        return results;
    }

    var viewContent = File.ReadAllText(mainViewPath);
    var vmExists = File.Exists(mainVmPath);
    var vmContent = vmExists ? File.ReadAllText(mainVmPath) : "";

    // Tab-Buttons mit Command Binding zaehlen
    var tabCommandMatches = Regex.Matches(viewContent, @"Command=""\{Binding\s+\w*(Navigate|Select)\w*Command\}""");
    var tabButtonCount = tabCommandMatches.Count;

    if (tabButtonCount > 0)
        results.Add(new(Severity.Pass, cat, $"{tabButtonCount} Tab-Buttons mit Command Binding"));
    else
    {
        // Screen-basierte Navigation (BomberBlast, HandwerkerImperium) - kein WARN
        bool hasScreenNav = vmExists && Regex.IsMatch(vmContent, @"void\s+NavigateTo\s*\(\s*string");
        results.Add(new(hasScreenNav ? Severity.Info : Severity.Warn, cat,
            hasScreenNav ? "Screen-basierte Navigation (keine Tab-Buttons)" : "Keine Tab-Buttons mit Navigate/Select Command gefunden"));
    }

    // Tab-Count Cross-Check (VM IsXxxActive vs. View Tab-Buttons)
    if (vmExists)
    {
        var vmActivePropsExplicit = Regex.Matches(vmContent, @"bool\s+Is(\w+)Active\b");
        var vmActiveFieldsExplicit = Regex.Matches(vmContent, @"bool\s+_is(\w+)Active\b");
        var vmTabPropsExplicit = Regex.Matches(vmContent, @"bool\s+Is(\w+)Tab\b");
        var vmTabCount = vmActivePropsExplicit.Select(m => m.Groups[1].Value)
            .Union(vmActiveFieldsExplicit.Select(m => m.Groups[1].Value))
            .Union(vmTabPropsExplicit.Select(m => m.Groups[1].Value))
            .Distinct().Count();

        if (tabButtonCount > 0 && vmTabCount > 0)
        {
            if (tabButtonCount == vmTabCount)
                results.Add(new(Severity.Pass, cat, $"Tab-Count stimmt ueberein: {tabButtonCount} Tabs in View = {vmTabCount} IsXxxActive in VM"));
            else
                results.Add(new(Severity.Info, cat, $"{vmTabCount} IsXxxActive in VM vs. {tabButtonCount} Navigate-Commands in View (Calculator/Sub-Page Buttons mitzaehlend)"));
        }
    }

    // Overlay-Panel mit ZIndex
    var overlayMatches = Regex.Matches(viewContent, @"IsVisible=""[^""]*""\s*[^>]*ZIndex=""\d+""");
    var overlayMatches2 = Regex.Matches(viewContent, @"ZIndex=""\d+""\s*[^>]*IsVisible=""[^""]*""");
    var overlayCount = overlayMatches.Count + overlayMatches2.Count;
    // Alternative: ZIndex allgemein suchen
    var zindexCount = Regex.Matches(viewContent, @"ZIndex=""\d+""").Count;
    if (zindexCount > 0)
        results.Add(new(Severity.Pass, cat, $"{zindexCount} Elemente mit ZIndex (Overlay-Panels)"));
    else
        results.Add(new(Severity.Info, cat, "Keine ZIndex-Elemente gefunden (evtl. keine Overlays)"));

    // Ad-Spacer (fuer Ad-Apps)
    if (app.IsAdSupported)
    {
        if (viewContent.Contains("IsAdBannerVisible") || viewContent.Contains("AdBanner") || viewContent.Contains("AdSpacer"))
            results.Add(new(Severity.Pass, cat, "Ad-Spacer/Banner Referenz vorhanden"));
        else
            results.Add(new(Severity.Warn, cat, "Kein Ad-Spacer/Banner in MainView (Ad-App!)"));
    }

    // Calculator-Overlay Cross-Check (DataTemplate oder ContentControl mit VM-Binding)
    if (vmExists && vmContent.Contains("CurrentPage") || vmContent.Contains("CurrentCalculatorVm"))
    {
        if (viewContent.Contains("CurrentPage") || viewContent.Contains("CurrentCalculatorVm") || viewContent.Contains("DataTemplate"))
            results.Add(new(Severity.Pass, cat, "Calculator-Overlay mit DataTemplate/ContentControl verdrahtet"));
        else
            results.Add(new(Severity.Warn, cat, "CurrentPage/CurrentCalculatorVm in VM aber nicht in View verdrahtet"));
    }

    return results;
}

// === Hilfsfunktionen fuer die neuen Checks ===

static HashSet<string> ExtractConstructorVmParameters(string filePath)
{
    var vmParams = new HashSet<string>();
    var content = File.ReadAllText(filePath);

    // Constructor finden: public MainViewModel(...)
    var ctorMatch = Regex.Match(content, @"public\s+MainViewModel\s*\(([\s\S]*?)\)", RegexOptions.Multiline);
    if (!ctorMatch.Success) return vmParams;

    var paramBlock = ctorMatch.Groups[1].Value;
    // Alle ViewModel-Parameter extrahieren
    var paramMatches = Regex.Matches(paramBlock, @"(\w+ViewModel)\s+\w+");
    foreach (Match m in paramMatches)
        vmParams.Add(m.Groups[1].Value);

    return vmParams;
}

static HashSet<string> ExtractDiRegistrations(string appAxamlCsContent)
{
    var types = new HashSet<string>();
    // services.AddSingleton<Type>() oder services.AddTransient<Type>() oder services.AddScoped<Type>()
    var matches = Regex.Matches(appAxamlCsContent, @"\.Add(Singleton|Transient|Scoped)<(\w+)>");
    foreach (Match m in matches)
        types.Add(m.Groups[2].Value);

    // Factory-Pattern: services.AddTransient<Type>(sp => ...)
    var factoryMatches = Regex.Matches(appAxamlCsContent, @"\.Add(Singleton|Transient|Scoped)<(\w+)>\s*\(");
    foreach (Match m in factoryMatches)
        types.Add(m.Groups[2].Value);

    return types;
}

static string? FindMainView(string sharedDir)
{
    var viewsDir = Path.Combine(sharedDir, "Views");
    if (!Directory.Exists(viewsDir)) return null;

    var mainView = Directory.GetFiles(viewsDir, "MainView.axaml", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
        .FirstOrDefault();

    return mainView;
}

// === Records ===

enum Severity { Pass, Info, Warn, Fail }

record CheckResult(Severity Severity, string Category, string Message);

record AppDef(string Name, string ExpectedAppId, bool IsAdSupported);
