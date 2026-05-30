# AppChecker

Automatisches Pruef-Tool fuer die Avalonia-Apps (aktuell 9, siehe Tabelle "Bekannte Apps").
**33 Checker-Klassen, 200+ Pruefungen pro App.** Modulare Architektur mit `IChecker`-Interface
und Datei-Caching via `CheckContext`. Vollstaendige MVVM-Pattern-Abdeckung gemaess Architektur
in der Haupt-CLAUDE.md.

## Verwendung

```bash
# Alle 9 Apps (interaktiv) oder einzelne App
dotnet run --project tools/AppChecker
dotnet run --project tools/AppChecker RechnerPlus

# CI/Pre-Commit: nur Warnungen + Fehler anzeigen
dotnet run --project tools/AppChecker --quiet
dotnet run --project tools/AppChecker -q

# Nur kritische Fehler
dotnet run --project tools/AppChecker --fail-only
dotnet run --project tools/AppChecker -f

# Strukturierter JSON-Output (CI/Tooling)
dotnet run --project tools/AppChecker --json > report.json

# Hilfe
dotnet run --project tools/AppChecker --help
```

## Exit Codes

| Code | Bedeutung |
|------|-----------|
| 0 | Keine Warnungen, keine Fehler |
| 1 | Warnungen vorhanden |
| 2 | Fehler vorhanden |

## Architektur

```
tools/AppChecker/
├── Program.cs                       # Orchestrator + CLI-Parsing (--quiet/-f/--json/--help)
├── Models.cs                        # Severity, CheckResult, AppDef, CheckContext, CsFile, AxamlFile
├── IChecker.cs                      # Interface mit Check() + CheckGlobal()
├── Helpers/
│   ├── ConsoleHelpers.cs            # WriteColor, PrintResult, PrintCategory
│   ├── FileHelpers.cs               # FindSolutionRoot, LoadCsFiles, LoadAxamlFiles, CreateContext, IsSuppressed
│   ├── ResxHelpers.cs               # ExtractResxKeys
│   └── DiHelpers.cs                 # ExtractConstructorVmParameters, ExtractDiRegistrations,
│                                    # ExtractAllConstructorParameterTypes, ExtractGetServiceTypes
└── Checkers/                        # 33 Checker-Klassen
    │
    │  === Projekt + Build ===
    ├── ProjectBuildChecker.cs       # csproj, Versionen, RuntimeIdentifiers, TargetFramework
    ├── BuildConfigChecker.cs        # Directory.Build.targets AOT-Flags (global)
    │
    │  === Android ===
    ├── AndroidChecker.cs            # Manifest, Permissions, Icons, Mipmaps, AdMob Lifecycle
    ├── AndroidGotchasChecker.cs     # grantUriPermissions, ${applicationId}, BackButton
    │
    │  === Avalonia UI ===
    ├── AvaloniaUiChecker.cs         # MaterialIconStyles, SkiaThemeHelper, LocalizationService
    ├── AvaloniaGotchasChecker.cs    # translate px, Selector, ScrollViewer, RenderTransform, …
    ├── ThemeChecker.cs              # StaticResource Brush, AppPalette
    │
    │  === Assets + Lokalisierung ===
    ├── LocalizationChecker.cs       # resx-Vollstaendigkeit, ueberfluessige Keys
    ├── AssetsChecker.cs             # icon.png, MainWindow Icon-Referenz
    │
    │  === DI + ViewModel-Architektur ===
    ├── DiRegistrationChecker.cs     # ConfigureServices, Cross-Check + ungenutzte Registrierungen
    ├── VmWiringChecker.cs           # Tabs, Commands, LanguageChanged, UpdateLocalizedTexts
    ├── ViewBindingsChecker.cs       # x:DataType (FAIL wenn fehlt + Bindings), View↔ViewModel-Paar
    ├── NavigationChecker.cs         # Tab-Buttons, Tab-Count, Overlays, NavigationRequested
    │
    │  === MVVM-Pattern (komplette Abdeckung) ===
    ├── MvvmStrictChecker.cs              # App.Services-Locator, DataContext=, Click=, x:CompileBindings="False"
    ├── ConstructorInjectionChecker.cs    # Parameterlose Ctors, new XxxService(), .Instance-Zugriff
    ├── CommunityToolkitChecker.cs        # [ObservableProperty]/[RelayCommand]-Konsistenz, rohes INPC
    ├── AsyncRelayCommandChecker.cs       # async void [RelayCommand], CanExecute-Existenz
    ├── CodeBehindHygieneChecker.cs       # Code-Behind > 200/400 LOC, Service-Felder, async void
    ├── DataContextChangedPatternChecker.cs # Views mit VM-Events brauchen DataContextChanged/Detached
    ├── EventNamingConventionChecker.cs   # NavigationRequested/MessageRequested/... Naming + Signaturen
    ├── ServiceConventionChecker.cs       # I{Name}Service-Konvention, Async-Suffix, Lifetime-Dup
    ├── DispatcherUIThreadChecker.cs      # Task.Run mit Collection-Mutationen ohne Dispatcher
    │
    │  === Ad-Layout ===
    ├── AdLayoutChecker.cs           # Ad-Spacer 64dp, ScrollViewer Bottom-Margin, ShowBanner
    │
    │  === Code-Qualitaet + Patterns ===
    ├── CodeQualityChecker.cs        # Debug.WriteLine + Console.WriteLine, ungenutzte Exception-Variablen
    ├── AsyncPatternsChecker.cs      # async void, fire-and-forget, leere catch-Bloecke
    ├── DateTimeChecker.cs           # DateTime.Now, Parse ohne RoundtripKind/InvariantCulture
    ├── SqliteChecker.cs             # InsertAsync ID-Bug, SemaphoreSlim
    ├── SkiaSharpChecker.cs          # DPI (LocalClipBounds), InvalidateSurface, ArcTo 360
    ├── BillingChecker.cs            # Play Billing v8 API-Aenderungen
    ├── UriLauncherChecker.cs        # Process.Start statt UriLauncher in Shared
    ├── EventCleanupChecker.cs       # += ohne -=, statische Event-Handler
    ├── DisposableChecker.cs         # IDisposable-Felder ohne Dispose-Methode
    └── HardcodedStringChecker.cs    # XAML Text=/Content=/ToolTip.Tip=/Title= mit deutschen User-Strings
```

## MVVM-Pattern-Abdeckung (vollstaendig)

| MVVM-Aspekt aus Haupt-CLAUDE.md | Checker |
|----------------------------------|---------|
| `x:CompileBindings`/`x:DataType` auf jeder View-Root | `ViewBindingsChecker` |
| Kein `App.Services.GetRequiredService<T>()` im View-Ctor | `MvvmStrictChecker` |
| Kein `DataContext = ...` im Code-Behind | `MvvmStrictChecker` |
| Services per Constructor Injection (keine Property/Service-Locator) | `ConstructorInjectionChecker` |
| Commands per `[RelayCommand]` (kein Click-Handler) | `MvvmStrictChecker`, `CommunityToolkitChecker`, `AsyncRelayCommandChecker` |
| Sub-VMs als DI-Properties im MainViewModel | `DiRegistrationChecker`, `VmWiringChecker` |
| `DataContextChanged`-Pattern fuer Views mit VM-Events | `DataContextChangedPatternChecker` |
| Event-Naming: `NavigationRequested`/`MessageRequested`/... | `EventNamingConventionChecker` |
| Service-Konvention: `I{Name}Service` + `{Name}Service` | `ServiceConventionChecker` |
| Dispatcher.UIThread fuer Cross-Thread-Mutationen | `DispatcherUIThreadChecker` |
| Parameterlose Konstruktoren (Anti-DI) | `ConstructorInjectionChecker` |
| `[ObservableProperty]` statt manuelles `OnPropertyChanged` | `CommunityToolkitChecker` |
| Async-Methoden enden mit `Async` | `ServiceConventionChecker` |
| `[RelayCommand]` auf `async void` (Fire-and-Forget) | `AsyncRelayCommandChecker` |
| `IDisposable`-Felder werden disposed | `DisposableChecker` |
| User-facing Strings sind lokalisiert | `HardcodedStringChecker` |
| `LanguageChanged` → `UpdateLocalizedTexts()` Cross-Check | `VmWiringChecker` |
| Tab-Count View vs. ViewModel-Properties | `NavigationChecker` |
| Android Back-Button + ExitHintRequested + Double-Back | `AndroidGotchasChecker` |

Tieferanalyse / Auto-Fix: Agent `mvvm-auditor` (opus, max).

## Datei-Caching

`CheckContext` laedt alle .cs und .axaml Dateien einmal pro App und reicht sie an alle Checker durch:
- `CsFiles` - Alle .cs Dateien (Shared + Android + Desktop)
- `SharedCsFiles` - Nur Shared .cs
- `AndroidCsFiles` - Nur Android .cs
- `AxamlFiles` - Alle .axaml Dateien (Shared) — **AxamlFile hat KEIN `Lines`-Property**, bei Bedarf `file.Content.Split('\n')`

## Suppress-Kommentar

- C#: `// AppChecker:ignore` auf der Zeile **vor** dem Problem
- AXAML: `<!-- AppChecker:ignore -->` auf der Zeile **vor** dem Problem

## CLI-Filter (CI/Pre-Commit)

| Flag | Wirkung |
|------|---------|
| (kein Flag) | Komplette Ausgabe inkl. PASS/INFO |
| `--quiet` / `-q` | Nur WARN + FAIL (Per-App-Summary bleibt) |
| `--fail-only` / `-f` | Nur FAIL (CI-Fail-Gate) |
| `--json` | Strukturierter JSON-Output (PASS gefiltert) |
| `--help` / `-h` | Hilfe |

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
| RebornSaga | org.rsdigital.rebornsaga | Ja |

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
  → 95P 23I 7W 0F (125 Checks)

= Per-App Summary =
  App                 | PASS | INFO | WARN | FAIL | Checks
  --------------------+------+------+------+------+--------
  HandwerkerImperium  |  106 |  111 |  126 |    1 |    344
  BomberBlast         |   83 |   74 |   96 |    1 |    254
  ...

= Summary =
  PASS: 926  INFO: 443  WARN: 558  FAIL: 2
  1929 Checks in 6568ms (33 Checker, 9 Apps)
```

## Neuen Checker hinzufuegen

1. Neue Klasse in `Checkers/` erstellen die `IChecker` implementiert
2. `Category` Property setzen (Ausgabe-Header)
3. `Check(CheckContext ctx)` implementieren (pro App) und/oder `CheckGlobal(string solutionRoot)` (einmalig)
4. In `Program.cs` zum `checkers` Array hinzufuegen (Reihenfolge = Ausgabe-Reihenfolge)
5. Gecachte Dateien aus `ctx.CsFiles`/`ctx.AxamlFiles` verwenden (NICHT erneut laden)
6. `FileHelpers.IsSuppressed()` fuer `// AppChecker:ignore` Support
7. **AxamlFile-Lines:** bei Bedarf `file.Content.Split('\n')` (kein Lines-Property)

## Abhaengigkeiten

- .NET 10.0
- Keine externen NuGet-Packages (nur System.Text.RegularExpressions, System.Xml.Linq)

---

## Verweise

- [Haupt-CLAUDE.md](../../CLAUDE.md) — Build, Conventions, Architektur (die Checker prüfen genau diese)
- Skill `/build-check` — Baut Solution + führt AppChecker aus
