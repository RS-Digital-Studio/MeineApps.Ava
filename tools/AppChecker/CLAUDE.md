# AppChecker v2.0

Automatisches Pruef-Tool fuer alle 8 Avalonia-Apps. 22 Checker-Klassen, 150+ Pruefungen pro App.
Modulare Architektur mit IChecker-Interface und Datei-Caching via CheckContext.

## Verwendung

```bash
# Alle 8 Apps pruefen
dotnet run --project tools/AppChecker

# Einzelne App pruefen
dotnet run --project tools/AppChecker RechnerPlus

# Interaktiver Modus (ohne Argument): Auswahl per Menue
```

## Exit Codes

| Code | Bedeutung |
|------|-----------|
| 0 | Kein Fehler, keine Warnungen |
| 1 | Warnungen vorhanden |
| 2 | Fehler vorhanden |

## Architektur

```
tools/AppChecker/
├── Program.cs                    # Orchestrator (~140 Zeilen)
├── Models.cs                     # Severity, CheckResult, AppDef, CheckContext, CsFile, AxamlFile
├── IChecker.cs                   # Interface mit Check() + CheckGlobal()
├── Helpers/
│   ├── ConsoleHelpers.cs         # WriteColor, PrintResult, PrintCategory
│   ├── FileHelpers.cs            # FindSolutionRoot, GetRelativePath, LoadCsFiles, LoadAxamlFiles, CreateContext, IsSuppressed
│   ├── ResxHelpers.cs            # ExtractResxKeys
│   └── DiHelpers.cs              # ExtractConstructorVmParameters, ExtractDiRegistrations
└── Checkers/                     # 22 Checker-Klassen
    ├── ProjectBuildChecker.cs    # csproj, Versionen, RuntimeIdentifiers, TargetFramework
    ├── BuildConfigChecker.cs     # Directory.Build.targets AOT-Flags (global)
    ├── AndroidChecker.cs         # Manifest, Permissions, Icons, Mipmaps, AdMob Lifecycle
    ├── AndroidGotchasChecker.cs  # grantUriPermissions, ${applicationId}, BackButton
    ├── AvaloniaUiChecker.cs      # MaterialIconStyles, ThemeService, LocalizationService
    ├── AvaloniaGotchasChecker.cs # 12 Avalonia-Gotchas (translate px, Selector, ScrollViewer etc.)
    ├── ThemeChecker.cs           # StaticResource Brush, statisches Theme
    ├── LocalizationChecker.cs    # resx-Dateien, Key-Vergleich, ueberflüssige Keys
    ├── AssetsChecker.cs          # icon.png, MainWindow Icon-Referenz
    ├── DiRegistrationChecker.cs  # ConfigureServices, Services, Constructor Cross-Check
    ├── VmWiringChecker.cs        # Tabs, Commands, LanguageChanged, UpdateLocalizedTexts
    ├── ViewBindingsChecker.cs    # x:DataType, xmlns:vm, View↔ViewModel Paar-Check
    ├── NavigationChecker.cs      # Tab-Buttons, Tab-Count, Overlays, NavigationRequested
    ├── AdLayoutChecker.cs        # Ad-Spacer 64dp, ScrollViewer Bottom-Margin, ShowBanner
    ├── CodeQualityChecker.cs     # Debug.WriteLine, ungenutzte Exception-Variablen
    ├── AsyncPatternsChecker.cs   # async void, fire-and-forget, leere catch-Bloecke
    ├── DateTimeChecker.cs        # DateTime.Now, Parse ohne RoundtripKind/InvariantCulture
    ├── SqliteChecker.cs          # InsertAsync ID-Bug, SemaphoreSlim
    ├── SkiaSharpChecker.cs       # DPI (LocalClipBounds), InvalidateSurface, ArcTo 360
    ├── BillingChecker.cs         # Play Billing v8 API-Aenderungen
    ├── UriLauncherChecker.cs     # Process.Start statt UriLauncher in Shared
    └── EventCleanupChecker.cs    # += ohne -=, statische Event-Handler
```

## Datei-Caching

`CheckContext` laedt alle .cs und .axaml Dateien einmal pro App und reicht sie an alle Checker durch:
- `CsFiles` - Alle .cs Dateien (Shared + Android + Desktop)
- `SharedCsFiles` - Nur Shared .cs
- `AndroidCsFiles` - Nur Android .cs
- `AxamlFiles` - Alle .axaml Dateien (Shared)

## Suppress-Kommentar

`// AppChecker:ignore` auf der Zeile VOR dem Problem unterdrueckt die Warnung.

## Bekannte Apps

| App | Package-ID | Ads |
|-----|-----------|-----|
| RechnerPlus | com.meineapps.rechnerplus | Nein |
| ZeitManager | com.meineapps.zeitmanager | Nein |
| FinanzRechner | com.meineapps.finanzrechner | Ja |
| FitnessRechner | com.meineapps.fitnessrechner | Ja |
| HandwerkerRechner | com.meineapps.handwerkerrechner | Ja |
| WorkTimePro | com.meineapps.worktimepro | Ja |
| HandwerkerImperium | com.meineapps.handwerkerimperium | Ja |
| BomberBlast | org.rsdigital.bomberblast | Ja |

## Ausgabeformat

```
= Global =
  [Build-Config]
    [PASS] Full AOT aktiv (AndroidEnableProfiledAot=false)

= AppName =
  [Kategorie]
    [PASS] Pruefung bestanden
    [INFO] Information
    [WARN] Warnung
    [FAIL] Fehler gefunden
  → 69P 13I 3W 0F (85 Checks)

= Summary =
  PASS: 650  INFO: 81  WARN: 226  FAIL: 1
  958 Checks in 1379ms (22 Checker, 8 Apps)
```

## Neuen Checker hinzufuegen

1. Neue Klasse in `Checkers/` erstellen die `IChecker` implementiert
2. `Category` Property setzen (Ausgabe-Header)
3. `Check(CheckContext ctx)` implementieren (pro App) und/oder `CheckGlobal(string solutionRoot)` (einmalig)
4. In `Program.cs` zum `checkers` Array hinzufuegen
5. Gecachte Dateien aus `ctx.CsFiles`/`ctx.AxamlFiles` verwenden (NICHT erneut laden)
6. `FileHelpers.IsSuppressed()` fuer `// AppChecker:ignore` Support

## Abhaengigkeiten

- .NET 10.0
- Keine externen NuGet-Packages (nur System.Text.RegularExpressions, System.Xml.Linq)
