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

# AppChecker - Alle 8 Apps / Einzelne App
dotnet run --project tools/AppChecker
dotnet run --project tools/AppChecker {App}

# StoreAssetGenerator - Alle / Gefiltert
dotnet run --project tools/StoreAssetGenerator
dotnet run --project tools/StoreAssetGenerator {Filter}
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
│   └── Apps/                       # 8 Apps, jeweils Shared/Android/Desktop
│       ├── RechnerPlus/            # Taschenrechner (werbefrei)
│       ├── ZeitManager/            # Timer/Stoppuhr/Alarm (werbefrei)
│       ├── FinanzRechner/          # 5 Finanzrechner + Budget-Tracker
│       ├── FitnessRechner/         # BMI/Kalorien/Barcode-Scanner
│       ├── HandwerkerRechner/      # 9 Bau-Rechner (4 Free + 5 Premium)
│       ├── WorkTimePro/            # Arbeitszeiterfassung + Export
│       ├── HandwerkerImperium/     # Idle-Game (Werkstaetten + Arbeiter)
│       └── BomberBlast/            # Bomberman-Klon (SkiaSharp, Landscape)
│
├── tools/
│   ├── AppChecker/              # 10 Check-Kategorien, 100+ Pruefungen
│   └── StoreAssetGenerator/     # Play Store Assets (SkiaSharp)
│
└── tests/
```

---

## Status (09. Februar 2026)

Alle 8 Apps im geschlossenen Test, warten auf 12 Tester fuer Produktion.

| App | Version | Ads | Premium |
|-----|---------|-----|---------|
| RechnerPlus | v2.0.2 | Nein | Nein |
| ZeitManager | v2.0.2 | Nein | Nein |
| HandwerkerRechner | v2.0.2 | Banner + Rewarded | 3,99 remove_ads |
| FinanzRechner | v2.0.2 | Banner + Rewarded | 3,99 remove_ads |
| FitnessRechner | v2.0.2 | Banner + Rewarded | 3,99 remove_ads |
| WorkTimePro | v2.0.2 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime |
| HandwerkerImperium | v2.0.3 | Banner + Rewarded | 4,99 Premium |
| BomberBlast | v2.0.2 | Banner + Rewarded | 3,99 remove_ads |

---

## 4 Themes

| Theme | Beschreibung |
|-------|--------------|
| Midnight (Default) | Dark, Indigo Primary |
| Aurora | Dark, Pink/Violet/Cyan Gradient |
| Daylight | Light, Blue Primary |
| Forest | Dark, Green Primary |

Implementierung: `MeineApps.Core.Ava/Themes/` - ThemeService laedt dynamisch via `app.Styles.Add(StyleInclude)`. KEIN statisches Theme in App.axaml.

---

## Packages (Avalonia 11.3.11)

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 11.3.11 | UI Framework |
| Material.Avalonia | 3.13.4 | Material Design |
| Material.Icons.Avalonia | 2.4.1 | 7000+ SVG Icons |
| DialogHost.Avalonia | 0.10.4 | Dialogs |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM |
| Xaml.Behaviors.Avalonia | 11.3.9.3 | Behaviors |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.0-rc6.1 | Charts |
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
| Service Interface | `I{Name}Service` | `IThemeService`, `ILocalizationService` |
| Service Implementation | `{Name}Service` | `ThemeService`, `PreferencesService` |
| Events (Navigation) | `NavigationRequested` | `Action<string>` |
| Events (Messages) | `MessageRequested` | `Action<string, string>` |
| Events (UI-Feedback) | `FloatingTextRequested` | `EventHandler<(string, string)>` |
| Events (Celebration) | `CelebrationRequested` | `EventHandler` |

### DI-Pattern

**Service Lifetimes:**
- Services → Singleton (IPreferences, ITheme, ILocalization, Database)
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
- `".."` = zurueck zum Parent
- `"../subpage"` = zum Parent, dann zu subpage

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
- **Tages-Tracking**: `DateTime.Today` fuer datumsbasierte Gruppierung

### Thread-Safety

```csharp
// Async: SemaphoreSlim
private readonly SemaphoreSlim _semaphore = new(1, 1);
await _semaphore.WaitAsync();
try { /* ... */ } finally { _semaphore.Release(); }

// UI-Thread: Dispatcher
Dispatcher.UIThread.Post(() => { SomeProperty = newValue; });
```

### Tab-Navigation (UI)

- MainView: `Border.TabContent` + `.Active` CSS-Klassen
- Tab-Switching via `IsXxxActive` bool Properties im MainViewModel
- Fade-Transition: `DoubleTransition` auf Opacity (150ms)
- Border wrapping noetig (Child-Views haben eigenen DataContext/ViewModel)

---

## Ad-Banner Layout (WICHTIG)

- **MainView Grid**: `RowDefinitions="*,Auto,Auto"` → Row 0 Content, Row 1 Ad-Spacer (50dp), Row 2 Tab-Bar
- **Jeder MainViewModel**: Muss `_adService.ShowBanner()` explizit aufrufen (AdMobHelper verschluckt Fehler)
- **ScrollViewer Bottom-Padding**: Mindestens 60dp in ALLEN scrollbaren Sub-Views
- **Tab-Bar Heights**: FinanzRechner/FitnessRechner/HandwerkerRechner/WorkTimePro=56, HandwerkerImperium=64, BomberBlast=0

## AdMob

### Linked-File-Pattern
- `AdMobHelper.cs` + `RewardedAdHelper.cs` + `AndroidRewardedAdService.cs` + `AndroidFileShareService.cs` in Premium-Library unter `Android/`
- Per `<Compile Include="..." Link="..." />` in jedes Android-Projekt eingebunden
- `<Compile Remove="Android\**" />` verhindert Kompilierung im net10.0 Library-Projekt
- **UMP Namespace-Typo**: `Xamarin.Google.UserMesssagingPlatform` (DREIFACHES 's')
- **Java Generics Erasure**: RewardedAdHelper.LoadCallback braucht `[Register]`-Attribut

### Rewarded Ads Multi-Placement
- `AdConfig.cs`: 17 Rewarded Ad-Unit-IDs (aus AdMob.docx)
- `ShowAdAsync(string placement)` → placement-spezifische Ad-Unit-ID via AdConfig
- Jede App hat `RewardedAdServiceFactory` Property in App.axaml.cs

### Publisher-Account
- **ca-app-pub-2588160251469436** fuer alle 6 werbe-unterstuetzten Apps
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

### Android (AAB fuer Play Store)
```bash
dotnet publish src/Apps/{App}/{App}.Android -c Release
# Ausgabe: src/Apps/{App}/{App}.Android/bin/Release/net10.0-android/publish/
```

---

## Troubleshooting

| Problem | Ursache | Loesung |
|---------|---------|---------|
| Material Icons unsichtbar | `MaterialIconStyles` nicht in App.axaml registriert | `<materialIcons:MaterialIconStyles />` in `<Application.Styles>` |
| AdMob Crash auf Android | UMP Namespace hat Typo | `Xamarin.Google.UserMesssagingPlatform` (3x 's') |
| DateTime Timer falsch (1h) | UTC→Lokal Konvertierung | `DateTimeStyles.RoundtripKind` bei Parse |
| Release-Build crasht (Debug OK) | R8/Linker Problem | `AndroidLinkMode=None` + `AndroidEnableProguard=false` |
| SKCanvasView updatet nicht | `InvalidateVisual()` verwendet | `InvalidateSurface()` verwenden |
| CSS translate() Exception | Fehlende px-Einheiten | `translate(0px, 400px)` statt `translate(0, 400)` |
| AAPT2260 Fehler | grantUriPermissions ohne 's' | `android:grantUriPermissions="true"` (mit 's') |
| ${applicationId} geht nicht | .NET Android kennt keine Gradle-Placeholder | Hardcodierte Package-Namen verwenden |
| Icons in Tab-Leiste fehlen | Material.Icons xmlns fehlt | `xmlns:materialIcons="using:Material.Icons.Avalonia"` |
| VersionCode Ablehnung | Code bereits im Play Store | VOR Release aktuelle Codes im Play Store pruefen |

---

## Releases
- **Alle im geschlossenen Test**
- Keystore: `F:\Meine_Apps_Ava\Releases\meineapps.keystore` (Alias: meineapps, Pwd: MeineApps2025)
- Store-Assets: `Releases/{AppName}/` (via StoreAssetGenerator)
