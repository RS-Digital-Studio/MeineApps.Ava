# Meine Apps Avalonia - Projektübersicht

Multi-Plattform Apps (Android + Windows + Linux) mit Avalonia 11.3 + .NET 10.
Migriert von MAUI mit verbesserter UX, modernem Design und behobenen MAUI-Bugs.

---

## Build-Befehle

```bash
# Gesamte Solution bauen
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln

# Einzelne App (Shared/Desktop/Android) - {App} ersetzen
dotnet build src/Apps/{App}/{App}.Shared
dotnet run --project src/Apps/{App}/{App}.Desktop
dotnet build src/Apps/{App}/{App}.Android

# Desktop Release
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r linux-x64

# Android Release (AAB)
dotnet publish src/Apps/{App}/{App}.Android -c Release

# AppChecker v2.0 (22 Checker, 150+ Prüfungen) - Alle 9 Apps / Einzelne App
dotnet run --project tools/AppChecker
dotnet run --project tools/AppChecker {App}

# StoreAssetGenerator - Alle / Gefiltert
dotnet run --project tools/StoreAssetGenerator
dotnet run --project tools/StoreAssetGenerator {Filter}

# SocialPostGenerator - Posts + Promo-Bilder
dotnet run --project tools/SocialPostGenerator
dotnet run --project tools/SocialPostGenerator post {App} <x|reddit>
dotnet run --project tools/SocialPostGenerator image <{App}|portfolio>
```

---

## Projektstruktur

```
F:\Meine_Apps_Ava\
├── MeineApps.Ava.sln
├── Directory.Build.props           # Globale Build-Settings
├── Directory.Packages.props        # Central Package Management
├── CLAUDE.md
├── Releases/
│   └── meineapps.keystore
│
├── src/
│   ├── Libraries/
│   │   ├── MeineApps.CalcLib/      # Calculator Engine (net10.0)
│   │   ├── MeineApps.Core.Ava/     # Themes, Services, Converters
│   │   └── MeineApps.Core.Premium.Ava/  # Ads, IAP, Trial
│   │
│   ├── UI/
│   │   └── MeineApps.UI/           # Shared UI Components
│   │
│   └── Apps/                       # 11 Apps, jeweils Shared/Android/Desktop
│       ├── RechnerPlus/            # Taschenrechner (werbefrei)
│       ├── ZeitManager/            # Timer/Stoppuhr/Alarm (werbefrei)
│       ├── FinanzRechner/          # 6 Finanzrechner + Budget-Tracker
│       ├── FitnessRechner/         # BMI/Kalorien/Barcode-Scanner
│       ├── HandwerkerRechner/      # 11 Bau-Rechner (5 Free + 6 Premium)
│       ├── WorkTimePro/            # Arbeitszeiterfassung + Export
│       ├── HandwerkerImperium/     # Idle-Game (Werkstätten + Arbeiter)
│       ├── BomberBlast/            # Bomberman-Klon (SkiaSharp, Landscape)
│       ├── RebornSaga/             # Anime Isekai-RPG (Volle SkiaSharp-Engine)
│       ├── BingXBot/               # Trading Bot (Pi-Server 24/7 + Desktop + Android Remote)
│       ├── GardenControl/          # Bewässerungssteuerung (Pi-Server + Desktop + Android)
│       └── SmartMeasure/          # 3D-Grundstücksvermessung + Gartenplanung (RTK-GPS, privat)
│
├── tools/
│   ├── AppChecker/              # 10 Check-Kategorien, 100+ Prüfungen
│   ├── StoreAssetGenerator/     # Play Store Assets (SkiaSharp)
│   └── SocialPostGenerator/     # Social-Media Posts + Promo-Bilder
│
└── tests/
```

---

## Status (18. April 2026)

7 Apps im geschlossenen Test. HandwerkerImperium in Produktion. RebornSaga + BingXBot in Entwicklung.

| App | Version | Ads | Premium | Status |
|-----|---------|-----|---------|--------|
| RechnerPlus | v2.0.7 | Nein | Nein | Geschlossener Test |
| ZeitManager | v2.0.7 | Nein | Nein | Geschlossener Test |
| HandwerkerRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FinanzRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FitnessRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| WorkTimePro | v2.0.7 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime | Geschlossener Test |
| HandwerkerImperium | v2.1.0 | Banner + Rewarded | 4,99 Premium | Produktion |
| BomberBlast | v2.0.55 | Rewarded (Landscape, kein Banner) | 1,99 remove_ads | Produktion (AAA-Audit Phase 1-15 09.05.2026: TIER-1+2 + Mode-Plugin-Framework + ComboSystem + FixedTimestepRunner + Firebase-Android-Stubs. **v2.0.55 Phase 15 4-Subagent-Review:** 2 P0 (Liga-Rules-Mismatch seit v2.0.34, DSGVO-Consent-UI) + 4 P1 (AccountDeletion-Reihenfolge, CloudSave-Version, Memory-Telemetry-Background-Thread, Cinematic-Stop) gefixt. = 286 Tests grün) |
| RebornSaga | v1.0.0 | Rewarded (kein Banner) | Gold-Pakete + remove_ads | Entwicklung |
| BingXBot | v1.7.0 + Phase 18 (09.05.2026) | Nein | Nein | Entwicklung (Pi-Server + Desktop + Android Remote, OPTIMIZATION_PLAN_2026-05 Phasen 0-17 umgesetzt 06.05.2026, **Phase 18 (09.05.2026): A1-A7 Risk-Hardening (GetPositionScalingFactor-Bug, Idempotency-Keys, Clock-Drift, Korrelations-Filter, Vol-Targeting, Tick-Size-Awareness, Session-Filter), B1-B5 Resilience (Sync-over-Async, Heartbeat+Missing-TP, Rate-Limit, News-Health), C1+C2+C3 Performance (Allocation-Pressure, ServerTime-Probe statt 80kB), D1+D2 Quality (Magic Numbers, MaxScore dynamisch), F2+F5 Operations (FCM-Cleanup, Funding-Threshold pro Category). Tests: 713/713 grün. E-Refactor + F1/F3/F4 + D3/D4 als Folge-Iteration vermerkt** — Pi-Deploy + Live-Verifikation = naechste Schritte) |
| GardenControl | v1.0.0 | Nein | Nein | Entwicklung (Pi + Desktop + Android) |
| SmartMeasure | v1.1.4 | Nein | Nein | Entwicklung (privat, RTK-GPS Vermessung) |

---

## App-spezifische Farbpaletten

Jede App hat eine eigene `Themes/AppPalette.axaml` im Shared-Projekt, statisch in App.axaml geladen. Kein dynamischer Theme-Wechsel, kein ThemeService.

| App | Primary | Charakter |
|-----|---------|-----------|
| RechnerPlus | #7C7FF7 Indigo | Retro-Tech Calculator |
| ZeitManager | #F7A833 Amber | Warme Zeitverwaltung |
| FinanzRechner | #10B981 Smaragd | Living Finance |
| FitnessRechner | #06B6D4 Cyan | VitalOS Medical |
| HandwerkerRechner | #3B82F6 Blau | Blueprint Professional |
| WorkTimePro | #4F8BF9 Blau | Professional Workspace |
| HandwerkerImperium | #D97706 Amber | Warme Werkstatt |
| BomberBlast | #FF6B35 Orange | Neon Arcade |
| RebornSaga | #4A90D9 Blau | Isekai System Blue |
| BingXBot | #3B82F6 Blau | Dark Trading Terminal |
| GardenControl | #2E7D32 Grün | Natur/Garten Dashboard |
| SmartMeasure | #FF6B00 Orange | Technisch-Professionell, Vermessung |

Implementierung: Jede App lädt `<StyleInclude Source="/Themes/AppPalette.axaml" />` in App.axaml. Alle DynamicResource-Keys bleiben identisch. Design-Tokens (Spacing, Radius, Fonts) kommen weiterhin aus `MeineApps.Core.Ava/Themes/ThemeColors.axaml`.

---

## Packages (Avalonia 11.3.13)

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 11.3.13 | UI Framework |
| Material.Icons.Avalonia | 3.0.0 | 7000+ SVG Icons (Auto-Sizing, Caching) |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM |
| Xaml.Behaviors.Avalonia | 11.3.9.5 | Behaviors |
| SkiaSharp | 3.119.2 | 2D Graphics + SkSL GPU-Shader |
| SkiaSharp.Skottie | 3.119.2 | Lottie-Animations-Backend |
| Avalonia.Labs.Lottie | 11.3.1 | Lottie-Animationen (JSON) |
| Xamarin.Android.Google.BillingClient | 8.3.0.2 | Google Play Billing |
| Xamarin.Google.Android.Play.Review | 2.0.2.7 | Google In-App Review |
| sqlite-net-pcl | 1.9.172 | Database |

---

## Keystore

| Eigenschaft | Wert |
|-------------|------|
| Speicherort | `F:\Meine_Apps_Ava\Releases\meineapps.keystore` |
| Alias | `meineapps` |
| Passwort | `MeineApps2025` |

---

## Conventions & Patterns

### Naming Conventions

| Element | Convention | Beispiel |
|---------|-----------|----------|
| ViewModel | Suffix `ViewModel` | `MainViewModel`, `TileCalculatorViewModel` |
| View | Suffix `View` | `MainView.axaml`, `SettingsView.axaml` |
| Service Interface | `I{Name}Service` | `IPreferencesService`, `ILocalizationService` |
| Service Implementation | `{Name}Service` | `PreferencesService`, `LocalizationService` |
| Events (Navigation) | `NavigationRequested` | `Action<string>` |
| Events (Messages) | `MessageRequested` | `Action<string, string>` |
| Events (UI-Feedback) | `FloatingTextRequested` | `EventHandler<(string, string)>` |
| Events (Celebration) | `CelebrationRequested` | `EventHandler` |

### DI-Pattern

**Service Lifetimes:**
- Services → Singleton (IPreferences, ILocalization, Database)
- MainViewModel → Singleton (haelt Child-VMs)
- Child-ViewModels → Transient oder Singleton (je nach App)

**Constructor Injection (immer):**
- Child-VMs werden in MainViewModel per Constructor injiziert
- Keine Property Injection, keine Service-Locator

**Android Platform-Services (Factory-Pattern):**
```csharp
// App.axaml.cs
public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }
// MainActivity.cs
App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(helper, sp.GetRequiredService<IPurchaseService>());
```

### Localization Pattern

- ResourceManager-basiert via `ILocalizationService.GetString("Key")`
- AppStrings.Designer.cs: Manuell erstellt (nicht auto-generiert bei CLI-Build)
- 6 Sprachen: DE, EN, ES, FR, IT, PT
- `LanguageChanged` Event → MainViewModel benachrichtigt alle Child-VMs via `UpdateLocalizedTexts()`
- Alle View-Strings lokalisiert (keine hardcodierten Texte)

### Navigation Pattern (Event-basiert, kein Shell)

```csharp
// Child-ViewModel
public event Action<string>? NavigationRequested;
NavigationRequested?.Invoke("route");

// MainViewModel
_childVM.NavigationRequested += route => CurrentPage = route;
```
- `".."` = zurück zum Parent
- `"../subpage"` = zum Parent, dann zu subpage

### Android Back-Button Pattern (einheitlich, alle 9 Apps)

```csharp
// MainViewModel: Double-Back-to-Exit via BackPressHelper (MeineApps.Core.Ava.Services)
public event Action<string>? ExitHintRequested;
private readonly BackPressHelper _backPressHelper = new();

// Im Konstruktor:
_backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
// WorkTimePro Sonderfall: nutzt FloatingTextRequested statt ExitHintRequested
// _backPressHelper.ExitHintRequested += msg => FloatingTextRequested?.Invoke(msg, "info");

public bool HandleBackPressed()
{
    // 1. App-spezifische Overlays/Dialoge schließen
    // 2. Sub-Navigation zurück
    // 3. Double-Back-to-Exit (am Ende):
    var msg = _localization.GetString("PressBackAgainToExit") ?? "...";
    return _backPressHelper.HandleDoubleBack(msg);
}
```

```csharp
// MainActivity: VM holen, Event verdrahten, OnBackPressed delegieren
_mainVm = App.Services.GetService<MainViewModel>();
if (_mainVm != null)
    _mainVm.ExitHintRequested += msg =>
        RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Short)?.Show());

public override void OnBackPressed()
{
    if (_mainVm != null && _mainVm.HandleBackPressed()) return;
    base.OnBackPressed();
}
```

### Error-Handling Pattern

```csharp
// MessageRequested statt Debug.WriteLine
public event Action<string, string>? MessageRequested;
try { /* ... */ }
catch (Exception) { MessageRequested?.Invoke("Fehler", "Speichern fehlgeschlagen"); }
```

### DateTime Pattern

- **Persistenz**: IMMER `DateTime.UtcNow` (NIE `DateTime.Now`)
- **Format**: ISO 8601 "O" → `dateTime.ToString("O")`
- **Parse**: IMMER `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`
- **Tages-Tracking**: `DateTime.Today` für datumsbasierte Gruppierung

### Thread-Safety

```csharp
// Async: SemaphoreSlim
private readonly SemaphoreSlim _semaphore = new(1, 1);
await _semaphore.WaitAsync();
try { /* ... */ } finally { _semaphore.Release(); }

// UI-Thread: Dispatcher
Dispatcher.UIThread.Post(() => { SomeProperty = newValue; });
```

### UriLauncher (Plattformübergreifend)

- `UriLauncher.OpenUri(uri)` statt `Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })`
- `UriLauncher.ShareText(text, title)` für natives Share-Sheet (Android) oder Clipboard (Desktop)
- Desktop: Fallback auf Process.Start (OpenUri) bzw. Clipboard (ShareText)
- Android: `PlatformOpenUri` wird in MainActivity auf `Intent.ActionView` gesetzt
- Android: `PlatformShareText` wird in MainActivity auf `Intent.ActionSend` gesetzt
- Datei: `MeineApps.Core.Ava/Services/UriLauncher.cs`

### Tab-Navigation (UI)

- MainView: `Border.TabContent` + `.Active` CSS-Klassen
- Tab-Switching via `IsXxxActive` bool Properties im MainViewModel
- Fade-Transition: `DoubleTransition` auf Opacity (150ms)
- Border wrapping noetig (Child-Views haben eigenen DataContext/ViewModel)

---

## Ad-Banner Layout (WICHTIG)

- **MainView Grid**: `RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (64dp), Row 2 Tab-Bar
- **Ad-Spacer 64dp**: Adaptive Banner (`GetCurrentOrientationAnchoredAdaptiveBannerAdSize`) können 50-60dp+ hoch sein → 64dp als sicherer Spacer
- **Jeder MainViewModel**: Muss `_adService.ShowBanner()` explizit aufrufen (AdMobHelper verschluckt Fehler)
- **ScrollViewer Bottom-Margin**: Mindestens 60dp Margin (NICHT Padding!) auf dem Kind-Element in ALLEN scrollbaren Sub-Views
- **Tab-Bar Heights**: FinanzRechner/FitnessRechner/HandwerkerRechner/WorkTimePro=56, HandwerkerImperium=64 (SkiaSharp GameTabBarRenderer, kein Ad-Banner/Spacer), BomberBlast=0

## AdMob

### Linked-File-Pattern
- `AdMobHelper.cs` + `RewardedAdHelper.cs` + `AndroidRewardedAdService.cs` + `AndroidFileShareService.cs` + `AndroidPurchaseService.cs` in Premium-Library unter `Android/`
- Per `<Compile Include="..." Link="..." />` in jedes Android-Projekt eingebunden
- `<Compile Remove="Android\**" />` verhindert Kompilierung im net10.0 Library-Projekt
- **UMP Namespace-Typo**: `Xamarin.Google.UserMesssagingPlatform` (DREIFACHES 's')
- **Java Generics Erasure (KRITISCH)**: `[Register("...", "")]` mit leerem Connector verdrahtet keinen JNI Delegate → Callback nie aufgerufen. FIX: `FixedRewardedAdLoadCallback` mit `GetOnAdLoadedHandler` Connector

### Rewarded Ads Multi-Placement
- `AdConfig.cs`: 28 Rewarded Ad-Unit-IDs (6 Apps)
- `ShowAdAsync(string placement)` → placement-spezifische Ad-Unit-ID via AdConfig
- Jede App hat `RewardedAdServiceFactory` Property in App.axaml.cs

### Google Play Billing (In-App Purchases)
- `AndroidPurchaseService.cs`: Erbt von `PurchaseService`, implementiert Google Play Billing Client v8
- `Xamarin.Android.Google.BillingClient` 8.3.0.1 (in Directory.Packages.props)
- Jede App hat `PurchaseServiceFactory` Property in App.axaml.cs (analog zu RewardedAdServiceFactory)
- **Java-Callback-Pattern:** `IBillingClientStateListener` und `IPurchasesUpdatedListener` als innere Klassen (erben von `Java.Lang.Object`)
- Unterstützt InApp (non-consumable + consumable), Subscriptions, Auto-Reconnect

### Publisher-Account
- **ca-app-pub-2588160251469436** für alle 6 werbe-unterstützten Apps
- RechnerPlus + ZeitManager sind werbefrei

---

## Desktop Publishing

### Windows
```bash
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64
# Ausgabe: src/Apps/{App}/{App}.Desktop/bin/Release/net10.0/win-x64/publish/
```

### Linux
```bash
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r linux-x64
# Ausgabe: src/Apps/{App}/{App}.Desktop/bin/Release/net10.0/linux-x64/publish/
```

### Android (AAB für Play Store)
```bash
dotnet publish src/Apps/{App}/{App}.Android -c Release
# Ausgabe: src/Apps/{App}/{App}.Android/bin/Release/net10.0-android/publish/
```

---

## Claude-Code Agents (24 Agents, Stand 16.04.2026)

| Kategorie | Agent | Modell | Effort | Zweck |
|-----------|-------|--------|--------|-------|
| **Kritisches Denken** | `code-review` | opus | max | Code-Review mit MVVM-Violations-Check |
|  | `debugger` | opus | max | Bug-Diagnose mit Hypothesen |
|  | `pre-release` | opus | max | Release-Readiness inkl. Server-Checks |
|  | `mvvm-auditor` | opus | max | Strikter MVVM-Audit (Code-Behind, Compiled Bindings, ViewLocator) |
|  | `bingxbot` | opus | max | Trading-Domain-Experte (SK-System, ATI, BingX-API) |
| **Architektur** | `architect` | opus | high | Design-Entscheidungen, Modul-Grenzen |
|  | `planner` | opus | high | Feature-Planung mit Dateiliste + DI + RESX |
|  | `devils-advocate` | opus | high | Ideen stress-testen |
|  | `server-ops` | opus | high | Pi/systemd/SignalR/SSH-Deploy |
| **Qualität** | `tester` | opus | high | Unit-Tests + Edge-Cases (MVVM, Trading-Logik) |
|  | `refactor` | opus | high | Strukturelle Änderungen, Duplikate |
|  | `migrator` | opus | high | Framework-Upgrades (Avalonia, .NET, SkiaSharp) |
|  | `security` | opus | high | Secrets, Manifest, IAP, Compliance |
|  | `performance` | opus | high | CPU/GC/UI-Stutter/Startup |
|  | `skiasharp` | opus | high | Paint-Lifecycle, Shader, DPI |
|  | `health` | opus | high | Makro-Gesundheit der Codebase |
|  | `game-audit` | opus | high | Spieler-Perspektive (Balancing, UX, Economy) |
| **Routine** | `ui` | sonnet | medium | AXAML/Styles/Touch/Bindings + MVVM-Basis |
|  | `localize` | sonnet | medium | RESX-Vollständigkeit, Placeholder |
|  | `documenter` | sonnet | medium | Kommentare, CLAUDE.md, Changelog |
|  | `deploy` | sonnet | medium | Release-Pipeline (AAB) |
|  | `dependency-checker` | sonnet | medium | NuGet-Updates, Vulnerabilities |
|  | `git-detective` | sonnet | medium | Commit-History, Bug-Einführung |
|  | `learn` | sonnet | medium | Code erklaeren, Patterns zeigen |

### Workflows (häufige Szenarien)

| Szenario | Ablauf |
|----------|--------|
| **Neue View bauen** | `planner` → `new-view` (Skill) → `mvvm-auditor` → `code-review` |
| **Neuer Service** | `planner` → `new-service` (Skill) → `code-review` → `tester` |
| **Bug fixen** | `debugger` → fixen → `code-review` → ggf. `tester` (Regression-Test) |
| **Release (App)** | `pre-release` → `localize` → `deploy` |
| **Release (Server)** | `pre-release` → `server-deploy` (Skill) → `server-ops` für Verifikation |
| **BingXBot-Problem** | `bingxbot` (Domain) → ggf. `debugger` oder `server-ops` |
| **MVVM-Sanierung** | `mvvm-auditor` (App-weit) → `code-review` → Build-Verifikation |
| **Refactoring** | `health` → `refactor` → `code-review` → `tester` |

### Skills (Projekt-lokal)

| Skill | Zweck |
|-------|-------|
| `build-check` | Solution bauen + AppChecker |
| `app-status` | Version, Commits, Metriken, TODOs einer App |
| `new-view` | View + ViewModel nach Convention erstellen |
| `new-service` | Interface + Impl + DI erstellen |
| `mvvm-check` | Schneller Grep-MVVM-Audit (Pre-Commit) |
| `localize-check` | RESX-Vollständigkeit (6 Sprachen) |
| `release` | AAB bauen + Version erhöhen + Releases-Ordner |
| `server-deploy` | BingXBot.Server/GardenControl.Server auf Pi deployen |
| `changelog` | Changelog + Social-Posts (X, Reddit) aktualisieren |

### Hooks (User-Settings `~/.claude/settings.json`)

- **SessionStart**: MVVM-Strict-Reminder, auto-commit-Verbot, deutsche Umlaute, CLAUDE.md-Pflicht
- **PostToolUse Write/Edit auf `View*.axaml.cs`**: Injizierter Reminder für Code-Behind-Hygiene

---

## Troubleshooting

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Material Icons unsichtbar | `MaterialIconStyles` nicht in App.axaml registriert | `<materialIcons:MaterialIconStyles />` in `<Application.Styles>` |
| AdMob Crash auf Android | UMP Namespace hat Typo | `Xamarin.Google.UserMesssagingPlatform` (3x 's') |
| DateTime Timer falsch (1h) | UTC→Lokal Konvertierung | `DateTimeStyles.RoundtripKind` bei Parse |
| Release-Build crasht (Debug OK) | Meist stale Build-Artefakte oder falsche Flags | obj/bin löschen, clean rebuild |
| Mono JIT Assertion `!ji->async` Crash | Profiled AOT (SDK-Default) kompiliert nur hot Methods, Rest fällt auf JIT zurück → JIT-Bug auf manchen Geräten (z.B. Huawei P30) | `AndroidEnableProfiledAot=false` in Directory.Build.targets → Full AOT (alle Methoden kompiliert, kein JIT). `UseInterpreter=true` geht NICHT zusammen mit AOT (XA0119) |
| SKCanvasView updatet nicht | `InvalidateVisual()` verwendet | `InvalidateSurface()` verwenden |
| SKCanvasView leer bei IsVisible-Toggle | `InvalidateSurface()` auf unsichtbare Canvas wird ignoriert | Nach Sichtbar-Werden erneut Daten setzen/Calculate() aufrufen, damit PropertyChanged → InvalidateSurface() feuert |
| SKCanvasView Render-Loop tot nach StartRenderLoop() | `StartRenderLoop()` ruft `StopRenderLoop()` auf, das `_gameCanvas = null` setzt. Timer-Lambda captured `this._gameCanvas` → immer null | In `StartRenderLoop()` NUR `_renderTimer?.Stop()` aufrufen, NICHT `StopRenderLoop()` (das nullt die Canvas-Referenz) |
| CSS translate() Exception | Fehlende px-Einheiten | `translate(0px, 400px)` statt `translate(0, 400)` |
| AAPT2260 Fehler | grantUriPermissions ohne 's' | `android:grantUriPermissions="true"` (mit 's') |
| ${applicationId} geht nicht | .NET Android kennt keine Gradle-Placeholder | Hardcodierte Package-Namen verwenden |
| Icons in Tab-Leiste fehlen | Material.Icons xmlns fehlt | `xmlns:materialIcons="using:Material.Icons.Avalonia"` |
| VersionCode Ablehnung | Code bereits im Play Store | VOR Release aktuelle Codes im Play Store prüfen |
| Ads Error Code 0 + "Failed to instantiate ClientApi" | Ads vor SDK-Init geladen | `Initialize(activity, callback)` nutzen, Ads erst im Callback laden |
| Release-App schließt sich beim 1. Start (VS) | VS kann in Release keinen Debugger anhängen | App manuell starten - funktioniert. Kein App-Bug, VS-Verhalten |
| Process.Start PlatformNotSupportedException | Android unterstützt UseShellExecute nicht | `UriLauncher.OpenUri(uri)` verwenden (MeineApps.Core.Ava) |
| `\u20ac` als Text in XAML | XAML interpretiert C#-Unicode-Escapes nicht | Direkt `€` schreiben oder `&#x20AC;` verwenden |
| TransformOperations CS0103 | `Avalonia.Media` reicht nicht | `using Avalonia.Media.Transformation;` hinzufügen |
| IsAnimating Property-Warnung | Kollidiert mit `AvaloniaObject.IsAnimating()` | Property umbenennen (z.B. `IsPulsing`) oder `new` Keyword |
| KeyFrame-Animation Crash "No animator" | `Style.Animations` hat keinen Animator für `RenderTransform` | NUR `Opacity`/`Width`/`Height` (double) in KeyFrames verwenden. `TransformOperationsTransition` in `Transitions` funktioniert |
| TapScaleBehavior Crash "InvalidCastException" | `animation.RunAsync(ScaleTransform)` crasht in `TransformAnimator.Apply` | DispatcherTimer-basierte Animation statt Animation API für ScaleTransform verwenden |
| Content hinter Ad-Banner abgeschnitten | Ad-Spacer 50dp, aber adaptive Banner 50-60dp+ | Ad-Spacer auf 64dp erhöhen (alle 6 MainViews). Adaptive Banner variieren je nach Gerät |
| Style Selector AVLN2200 "Can not find parent" | `#Name` ohne Typ-Prefix | IMMER `Typ#Name` schreiben: `Grid#ModeSelector`, `Border#DisplayBorder` |
| Enum-Werte englisch in UI | Direktes Binding an Enum-Property | Display-Property mit lokalisiertem Text verwenden, im ViewModel per `GetString()` setzen |
| Daten erscheinen kurz, verschwinden | `_ = InitializeAsync()` mit `_list.Clear()` raced mit User-Aktionen | Task speichern: `_initTask = InitializeAsync()`, in Methoden `await _initTask` |
| Sprache immer Englisch trotz Geräteeinstellung | Erkannte Sprache nicht in Preferences gespeichert | `_preferences.Set(key, lang)` nach Gerätesprach-Erkennung in `Initialize()` |
| `IsAttachedToVisualTree` Kompilierfehler | Property in Avalonia 11.3 entfernt | `using Avalonia.VisualTree;` + `control.GetVisualRoot() != null` verwenden |
| InsertAsync gibt falsche ID zurück | sqlite-net `InsertAsync()` gibt Zeilen-Count zurück (immer 1), NICHT Auto-Increment-ID. sqlite-net setzt ID direkt auf dem Objekt | Nach `await db.InsertAsync(entity)` NICHT `entity.Id = result` schreiben - `entity.Id` ist bereits korrekt gesetzt |
| ScrollViewer scrollt nicht | `Padding` auf `ScrollViewer` verhindert Scrollen in Avalonia | `Padding` entfernen, stattdessen `Margin` auf das direkte Kind-Element setzen + `VerticalScrollBarVisibility="Auto"` |
| DonutChart-Segment unsichtbar bei 100% | SkiaSharp `ArcTo` bei 360° erzeugt leeren Path (Start=Ende) | Bei `sweepAngle >= 359°` in zwei 180°-Hälften aufteilen |
| ZIndex-Overlay Touch geht durch (Android) | Avalonia `ZIndex` auf Grid-Kindern funktioniert NICHT für Hit-Testing auf Android - Touch-Events gehen durch Overlay hindurch | Content-Swap statt Overlay: Normalen Content per `IsVisible=false` verstecken, Overlay-Content als Ersatz anzeigen. KEIN ZIndex verwenden für interaktive Overlays |
| Assembly-Version 1.0.0 (Default) | Shared-Projekt hat keine `<Version>` Property → Assembly-Version ist 1.0.0.0 | `<Version>X.Y.Z</Version>` in Shared .csproj setzen wenn Assembly-Version zur Laufzeit ausgelesen wird |
| Custom Control unsichtbar (PathIcon-Ableitung) | Abgeleitete Controls (z.B. `GameIcon : PathIcon`) haben kein ControlTheme → Avalonia 11 findet kein Template → Control rendert nichts | `protected override Type StyleKeyOverride => typeof(PathIcon);` in der Klasse überschreiben. Gilt für ALLE von TemplatedControl abgeleiteten Custom Controls |
| Button.OnAttachedToLogicalTree Crash (Android) | `TransformOperationsTransition` für `RenderTransform` ohne initialen `RenderTransform`-Wert → Transition von null→scale() crasht auf manchen GPU-Treibern | IMMER `RenderTransform="scale(1)"` + `RenderTransformOrigin="50%,50%"` setzen wenn `TransformOperationsTransition Property="RenderTransform"` verwendet wird. Fix in ButtonStyles.axaml |
| CommandParameter string→int Crash | XAML `CommandParameter="0"` ist IMMER `string`. `RelayCommand<int>` wirft `ArgumentException` in `CanExecute()` bei View-Attach | Methoden von `int` auf `string` ändern + `int.TryParse()` intern. Oder `<sys:Int32>0</sys:Int32>` im XAML. Betroffen: Alle hardcodierten CommandParameter-Werte |
| Rewarded Ad Belohnung kommt nicht an | `LoadAndShowAsync()` Timeout (8s) deckt Laden UND Video-Anzeige ab → feuert während User Video schaut → `false` zurück | `CancellationTokenSource` im Callback: Timeout nur für Lade-Phase, wird gecancellt wenn Ad geladen+gezeigt wird |
| Play Review Namespace falsch | `Com.Google.Android.Play.Core.Review` existiert nicht | `Xamarin.Google.Android.Play.Core.Review` verwenden. Task/IOnCompleteListener aus `Android.Gms.Tasks`. `ReviewInfo` (Klasse), NICHT `IReviewInfo` |
| MediaPlayer.PrepareAsync() gibt void zurück | Android Java-Binding: PrepareAsync() ist void, nicht Task | `Prepare()` synchron verwenden oder TaskCompletionSource mit Prepared-Event |
| SKMaskFilter Native Memory Leak (OOM auf Android) | `paint.MaskFilter = SKMaskFilter.CreateBlur(...)` ohne Dispose des vorherigen Filters | Gecachte statische SKMaskFilter verwenden oder `paint.MaskFilter?.Dispose()` vor jeder Neuzuweisung |
| JsonSerializer.Serialize auf Background-Thread → Collection-Crash | `Task.Run(() => Serialize(state))` während GameLoop den State modifiziert | Serialisierung auf dem UI-Thread belassen (State ist klein, ~5-20ms). Alternative: DeepCopy vor Serialize |
| Premium-Nutzer sieht Werbung nach Geräte-/Datenwechsel | `PurchaseService.InitializeAsync()` wurde nie aufgerufen → kein Google-Play-Abgleich → lokaler `is_premium` Key fehlt | `IPurchaseService.InitializeAsync()` in Loading-Pipeline aufrufen (parallel zum ersten Schritt). Stellt Käufe + Abos via Google Play Billing wieder her |
| SKCanvasView Game-Loop startet nicht (Countdown stuck) | ContentControl+ViewLocator setzt DataContext verzögert → `InvalidateCanvasRequested` hat beim `StartGameLoop()` keinen Subscriber → Render-Timer startet nie | 3-stufige VM-Subscription: (1) OnDataContextChanged, (2) OnLoaded als Backup, (3) OnPaintSurface Safety-Net startet Timer nach. `TrySubscribeToViewModel()` als zentrale idempotente Methode |
| Bildschirm flimmert bei Tab-/View-Wechsel | `FadeInContentPanel()` setzt `Opacity=0` NACH Binding-Update → neuer View kurz sichtbar bei voller Opacity → schwarzer Blitz | `PageTransitionStarting` Event via `OnActivePageChanging()` — feuert VOR dem Wert-Wechsel. View setzt `Opacity=0` bevor Bindings die neue View einblenden |
| Startup langsam bei vielen Child-ViewModels | MainViewModel mit N Child-VMs als Singletons löst beim ersten `GetRequiredService<MainViewModel>()` alle Services+VMs transitiv auf — 200-500ms auf Mid-Tier-Android wenn N>15 | Spät-unlocked VMs als `Lazy<T>` injizieren, in `EnsureXxxVm()`-Methode beim ersten Navigations-Ziel instanziieren + verdrahten. Public Property `XxxViewModel?` ist nullable → `[ObservableProperty]` feuert OnPropertyChanged bei Ensure → XAML ContentControl bindet dann ein. `AddLazyResolution()` Extension registriert `Lazy<T>` im DI-Container. LanguageChanged-Handler nur `Xxx?.UpdateLocalizedTexts()` (null-safe). Pattern in BomberBlast v2.0.34 eingeführt |
| Firebase ServerValue.TIMESTAMP für Anti-Spoofing | Client-gesetzte Zeitstempel (`DateTime.UtcNow.ToString("O")`) sind manipulierbar → Leaderboards können gefälschte "Letzte Aktivität"-Werte bekommen | Firebase-Sentinel `{".sv":"timestamp"}` als Payload-Wert verwenden: `private static readonly Dictionary<string,string> FirebaseServerTimestamp = new() { [".sv"] = "timestamp" };`. Firebase löst das serverseitig in ms-Timestamp auf. Security-Rules können darauf Rate-Limits setzen. Keine client-seitige Logik darauf verlassen — nur als Anzeige/Rate-Limit |

---

## App-spezifische Gotchas

App-spezifische Bug-Patterns und Troubleshooting-Einträge liegen in den jeweiligen App-CLAUDE.md Dateien:

- **HandwerkerImperium** (Service-Caches, Gilden/Firebase, Worker/Mini-Games): `src/Apps/HandwerkerImperium/CLAUDE.md`
- **BingXBot** (SK-System, TP/SL, ATI, Triple-Entry, BingX-API): `src/Apps/BingXBot/CLAUDE.md`
- **BomberBlast** (Cloud-Save, Dungeon-Run, Rewarded-Cooldown): `src/Apps/BomberBlast/CLAUDE.md`
- **SmartMeasure** (BLE, ARCore, Bowyer-Watson, RTK-GPS): `src/Apps/SmartMeasure/CLAUDE.md`
- **RebornSaga** (Sprite-Cache, Scene-Manager, StoryEngine): `src/Apps/RebornSaga/CLAUDE.md`

Firebase-Security-Rules sind in `database.rules.json` (aktuell nur HandwerkerImperium).
