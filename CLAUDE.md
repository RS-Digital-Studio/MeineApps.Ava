# Meine Apps Avalonia - Projektübersicht

Multi-Plattform Apps (Android + Windows + Linux) mit Avalonia 12 + .NET 10.
Migriert von MAUI mit verbesserter UX, modernem Design und behobenen MAUI-Bugs.

> **Sonderfall ArcaneKingdom:** Einzige App, die **Unity 6** statt Avalonia nutzt — TCG/RPG mit Echtzeit-PvP- und Live-Service-Anspruechen. Designplan v4 in [DESIGN.md](src/Apps/ArcaneKingdom/DESIGN.md) eingearbeitet: 5 Rassen (Ritter/Goetter/Elfen/Tiergeister/Daemonen), 6 Elemente Doppel-Dreieck, 131 Standard-Karten + 27 Oekosystem, 10 Welten mit Story-Mythologie, Prestige-System, Sternkarten-Login. Eigene Tech-Stack-Doku unter [src/Apps/ArcaneKingdom/](src/Apps/ArcaneKingdom/CLAUDE.md). Wird **nicht** von `dotnet build` erfasst.

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

# AppChecker v2.1 (33 Checker, 200+ Prüfungen) - Alle 9 Apps / Einzelne App / CI-Modi
dotnet run --project tools/AppChecker
dotnet run --project tools/AppChecker {App}
dotnet run --project tools/AppChecker --quiet      # Nur WARN + FAIL
dotnet run --project tools/AppChecker --fail-only  # Nur FAIL
dotnet run --project tools/AppChecker --json       # JSON-Output für CI

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
│   └── Apps/                       # 12 Avalonia-Apps + 1 Unity-App
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
│       ├── SmartMeasure/           # 3D-Grundstücksvermessung + Gartenplanung (RTK-GPS, privat)
│       └── ArcaneKingdom/          # TCG + RPG (NUR Unity 2022 LTS — kein Avalonia/.NET!)
│
├── tools/
│   ├── AppChecker/              # 10 Check-Kategorien, 100+ Prüfungen
│   ├── StoreAssetGenerator/     # Play Store Assets (SkiaSharp)
│   └── SocialPostGenerator/     # Social-Media Posts + Promo-Bilder
│
└── tests/
```

---

## Status

7 Apps im geschlossenen Test. HandwerkerImperium + BomberBlast in Produktion. RebornSaga + BingXBot in Entwicklung. ArcaneKingdom in Konzept-Phase (Unity statt Avalonia).

| App | Version | Ads | Premium | Status |
|-----|---------|-----|---------|--------|
| RechnerPlus | v2.0.7 | Nein | Nein | Geschlossener Test |
| ZeitManager | v2.0.7 | Nein | Nein | Geschlossener Test |
| HandwerkerRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FinanzRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FitnessRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| WorkTimePro | v2.0.7 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime | Geschlossener Test |
| HandwerkerImperium | v2.1.1 | Banner + Rewarded | 4,99 Premium | Produktion |
| BomberBlast | v2.0.62 | Rewarded (Landscape, kein Banner) | 1,99 remove_ads | Produktion |
| RebornSaga | v1.0.0 | Rewarded (kein Banner) | Gold-Pakete + remove_ads | Entwicklung |
| BingXBot | v1.8.0 | Nein | Nein | Entwicklung (Pi-Server + Desktop + Android Remote) |
| GardenControl | v1.0.0 | Nein | Nein | Entwicklung (Pi + Desktop + Android) |
| SmartMeasure | v1.1.4 | Nein | Nein | Entwicklung (privat, RTK-GPS Vermessung) |
| ArcaneKingdom | v0.0.2 | TBD | Diamanten-Packs (TCG-typisch) | Pre-MVP (Unity 6, Designplan v4 eingearbeitet: 5 Rassen, 6 Elemente, 131+27 Karten, 10 Welten, Story-Mythologie, Prestige, Sternkarten) |

---

## Projekt-CLAUDE.md-Index

Pflichtlektüre vor Änderungen am jeweiligen Projekt. Jede Sub-CLAUDE.md beschreibt
Zielframework, Build, Struktur, Conventions und ggf. Test-Projekte.

**Libraries:**

| Projekt | Beschreibung | Details |
|---------|--------------|---------|
| `MeineApps.Core.Ava` | Preferences, Lokalisierung, Themes, Converters, ViewLocator | [src/Libraries/MeineApps.Core.Ava/CLAUDE.md](src/Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| `MeineApps.Core.Premium.Ava` | AdMob, Google Play Billing, Trial, Android-Linked-Files | [src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md](src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| `MeineApps.CalcLib` | Calculator-Engine (Tokenizer + Parser + Evaluator) | [src/Libraries/MeineApps.CalcLib/CLAUDE.md](src/Libraries/MeineApps.CalcLib/CLAUDE.md) |
| `MeineApps.UI` | Custom Controls, Behaviors, Skia-Helpers, AppIcons.axaml | [src/UI/MeineApps.UI/CLAUDE.md](src/UI/MeineApps.UI/CLAUDE.md) |

**Apps:**

| App | Details |
|-----|---------|
| RechnerPlus | [src/Apps/RechnerPlus/CLAUDE.md](src/Apps/RechnerPlus/CLAUDE.md) |
| ZeitManager | [src/Apps/ZeitManager/CLAUDE.md](src/Apps/ZeitManager/CLAUDE.md) |
| FinanzRechner | [src/Apps/FinanzRechner/CLAUDE.md](src/Apps/FinanzRechner/CLAUDE.md) |
| FitnessRechner | [src/Apps/FitnessRechner/CLAUDE.md](src/Apps/FitnessRechner/CLAUDE.md) |
| HandwerkerRechner | [src/Apps/HandwerkerRechner/CLAUDE.md](src/Apps/HandwerkerRechner/CLAUDE.md) |
| WorkTimePro | [src/Apps/WorkTimePro/CLAUDE.md](src/Apps/WorkTimePro/CLAUDE.md) |
| HandwerkerImperium | [src/Apps/HandwerkerImperium/CLAUDE.md](src/Apps/HandwerkerImperium/CLAUDE.md) |
| BomberBlast | [src/Apps/BomberBlast/CLAUDE.md](src/Apps/BomberBlast/CLAUDE.md) |
| RebornSaga | [src/Apps/RebornSaga/CLAUDE.md](src/Apps/RebornSaga/CLAUDE.md) |
| BingXBot | [src/Apps/BingXBot/CLAUDE.md](src/Apps/BingXBot/CLAUDE.md) |
| GardenControl | [src/Apps/GardenControl/CLAUDE.md](src/Apps/GardenControl/CLAUDE.md) |
| SmartMeasure | [src/Apps/SmartMeasure/CLAUDE.md](src/Apps/SmartMeasure/CLAUDE.md) |
| ArcaneKingdom (Unity) | [src/Apps/ArcaneKingdom/CLAUDE.md](src/Apps/ArcaneKingdom/CLAUDE.md) |

**Tools:**

| Tool | Details |
|------|---------|
| AppChecker | [tools/AppChecker/CLAUDE.md](tools/AppChecker/CLAUDE.md) |
| StoreAssetGenerator | [tools/StoreAssetGenerator/CLAUDE.md](tools/StoreAssetGenerator/CLAUDE.md) |
| SocialPostGenerator | [tools/SocialPostGenerator/CLAUDE.md](tools/SocialPostGenerator/CLAUDE.md) |

---

## Architektur (Pflicht)

### Projekt-Referenzen

```
{App}.Android ──┬──> {App}.Shared
                ├──> MeineApps.Core.Premium.Ava   (Android-Linked-Files)
                └──> Android-Plattform-Bindings

{App}.Desktop ──┬──> {App}.Shared
                └──> Avalonia.Desktop

{App}.Shared  ──┬──> MeineApps.Core.Ava           (Services, Themes, ViewLocator)
                ├──> MeineApps.Core.Premium.Ava   (Ads, IAP, Trial — werbe-Apps)
                ├──> MeineApps.UI                 (Controls, Behaviors, AppIcons)
                └──> MeineApps.CalcLib            (Calculator-Apps)
```

**WICHTIG:**

- App-Shared darf NICHT auf andere Apps referenzieren.
- Libraries dürfen NICHT auf Apps referenzieren.
- `MeineApps.UI` darf NICHT auf `MeineApps.Core.Premium.Ava` referenzieren (Premium hängt von UI ab, nicht umgekehrt).
- Android-spezifische Klassen (`AndroidRewardedAdService`, `AndroidPurchaseService`) leben in `MeineApps.Core.Premium.Ava/Android/` und werden per `<Compile Include … Link="…" />` in jedes Android-Projekt eingebunden.

### Domain vs UI Logic

Views, Code-Behind und ViewModels enthalten **ausschließlich UI-Logik**. Domänenlogik (Berechnungen, Persistenz, Geschäftsregeln) gehört in Services unter `MeineApps.Core.Ava/Services/` oder app-spezifische Services im `{App}.Shared/Services/`-Ordner.

| Kategorie | Beispiel | Gehört nach |
|-----------|----------|-------------|
| Berechnung | Tilgungsplan, BMI, Material-Bedarf | `MeineApps.CalcLib` oder `{App}.Shared/Calculators/` |
| Persistenz | Save/Load JSON, SQLite | `{App}.Shared/Services/` (z.B. `SaveGameService`) |
| Geschäftsregeln | "Letzter Marker löscht Gruppe", Game-Balancing | `{App}.Shared/Services/` (z.B. `OrderGeneratorService`) |
| Plattform-API | AdMob, Billing, Haptics, FileShare | Interface in `MeineApps.Core.Ava`, Android-Impl in `MeineApps.Core.Premium.Ava/Android/` |

**Faustregel:** Wenn eine Methode kein Avalonia-API braucht, gehört sie nicht in ein ViewModel.

**UI darf:**
- Service-Methoden aufrufen und Ergebnisse anzeigen
- Confirmation-Dialoge vor destruktiven Service-Operationen zeigen
- UI-Properties nach Service-Aufrufen aktualisieren

### ViewModel-First

Alle neuen Controls, Pages und Popups folgen ViewModel-First:

1. **VM vor View:** ViewModel wird immer vor der View erzeugt (per DI im Composition Root).
2. **Views erzeugen keine VMs:** Views nehmen das VM via `DataContext`-Binding oder Constructor entgegen.
3. **DataContext VOR `InitializeComponent()`:** Pflicht für Compiled Bindings.
4. **Navigation via VM-Property:** `MainViewModel.CurrentPage = "route"` triggert ContentControl-Binding mit ViewLocator.

```csharp
// RICHTIG: ViewLocator-Konvention (BomberBlast.ViewModels.DashboardViewModel → BomberBlast.Views.DashboardView)
public DashboardView(DashboardViewModel vm)
{
    DataContext = vm;
    InitializeComponent();
}

// FALSCH: Code-Behind erzeugt VM
public DashboardView()
{
    DataContext = new DashboardViewModel(ServiceLocator.Get<IFoo>());  // Service-Locator → verboten
    InitializeComponent();
}
```

**Parameterloser Designer-Fallback** ist OK, darf aber kein VM erzeugen:
```csharp
public DashboardView() { InitializeComponent(); }
```

### Anti-Patterns (Verboten)

Neuer Code mit folgenden Mustern wird abgelehnt:

| Anti-Pattern | Warum verboten | Stattdessen |
|--------------|----------------|-------------|
| `ServiceLocator.Resolve<T>()` außerhalb Composition Root | Versteckte Abhängigkeiten, untestbar | Constructor Injection |
| Statische Singletons (`Xxx.Instance`) | Keine Test-Isolation, Lifetime-Bugs | Interface via DI |
| God Interfaces (>5 Methoden) | Verstößt gegen ISP | Pro Verantwortlichkeit ein Interface |
| Parameterloser VM-Ctor mit `Resolve`-Delegation | Service-Locator-Anti-Pattern | Constructor Injection, parameterloser Ctor nur für Designer |
| Hardcoded Werte in XAML (FontSize, Padding, Colors) | Verhindert Theming, Plattform-Skalierung | `{StaticResource …}` aus `MeineApps.Core.Ava/Themes/ThemeColors.axaml` |
| `Avalonia.Controls.Primitives.Popup` für Picker/Dropdowns | Auf Desktop = eigenes OS-Fenster, auf Android = In-App → inkonsistent | Inline `Border`/`Panel` mit `IsVisible`-Binding |
| `DateTime.Now` für Persistenz | UTC-Konvertierungs-Bugs bei Timezone-Wechsel | `DateTime.UtcNow` + `"O"`-Format + `DateTimeStyles.RoundtripKind` |
| Direkte AdMob-/Billing-Calls in ViewModels | Plattform-Lock-In, untestbar | `IRewardedAdService` / `IPurchaseService` via DI |

### Testbarkeit

`tests/`-Ordner ist vorgesehen für xUnit-v3-Test-Projekte (.NET 10).

**Beim Hinzufügen oder Ändern von testbarer Logik** (Berechnungen, Konvertierungen, Zustandsverwaltung, Parser, Algorithmen) müssen passende Unit-Tests im zugehörigen Test-Projekt geschrieben werden. Tests sind nicht nötig für reine UI-Verdrahtung, triviale Property-Wrapper oder Code der ausschließlich Avalonia/Android-APIs aufruft.

**Aktuell:** Nur `MeineApps.CalcLib` hat Tests. Domain-Code in Apps (z.B. `GameLoopService`, `OrderGeneratorService`, BingXBot SK-System) sollte sukzessive testbar gemacht werden — Services per Interface von Platform-APIs entkoppeln.

### Feature-Ordner (Vertical Slice)

Neue Features bekommen einen eigenen Ordner mit View + ViewModel + optional Service zusammen:

```
{App}.Shared/Views/MeinFeature/
├── MeinFeatureView.axaml(.cs)
└── (eventuell) MeinFeatureSubControl.axaml(.cs)

{App}.Shared/ViewModels/
├── MeinFeatureViewModel.cs

{App}.Shared/Services/
└── MeinFeatureService.cs   (wenn Service-Logik benötigt)
```

Namespace: `{App}.Views.MeinFeature`, `{App}.ViewModels.MeinFeatureViewModel`. Keine Dateien in Sammelordner wie `ViewModels/Misc/` ablegen.

### Service-Extraktion + Event-Cleanup

Wenn ein ViewModel ~300 Zeilen überschreitet oder eine zusammenhängende Teilverantwortung hat:

1. **Partial-Split** wenn die Logik kohärent bleibt (siehe `MainViewModel.cs`-Partials in HandwerkerImperium, BomberBlast).
2. **Service-Extraktion** wenn die Logik unabhängig ist — neuer Service in `{App}.Shared/Services/`, Interface in `{App}.Shared/Services/Interfaces/`.
3. **Constructor Injection** statt Service-Locator.
4. **`IDisposable` implementieren** wenn der Service Events abonniert.

```csharp
// Services die Events abonnieren MÜSSEN sich wieder abmelden:
public class MyService(IEventBus eventBus) : IDisposable
{
    public void Dispose()
    {
        eventBus.SomeEvent -= OnSomeEvent;
    }
}

// Controls: DetachedFromVisualTree-Cleanup
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    _service.Changed += OnChanged;
}

protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    _service.Changed -= OnChanged;
    base.OnDetachedFromVisualTree(e);
}
```

### C# 14 / .NET 10 Features (verbindlich nutzen)

- **Primary Constructors** für Klassen und Structs: `public class MyService(IFoo foo) { ... }`
- **Collection Expressions**: `string[] names = ["A", "B"];`
- **Pattern Matching** mit `is`, `switch expressions`
- **Records** für immutable Datenstrukturen (DTOs, Events)
- **File-scoped namespaces**: `namespace BomberBlast.ViewModels;`
- **Raw String Literals**: `"""..."""` für JSON/SQL
- **Required Members**: `public required string Name { get; init; }`

### Avalonia Popup-Primitive vermeiden

`Avalonia.Controls.Primitives.Popup` erzeugt auf Desktop ein separates natives OS-Fenster (eigenes HWND), auf Android dagegen ein In-App-Overlay. Dieses inkonsistente Verhalten ist unerwünscht.

**Regel:** Für Picker-Panels, Dropdown-Listen und expandierende Bereiche **inline Panels** verwenden (z.B. `Border`/`Panel` mit `IsVisible`-Binding), nicht `Popup`. Ausnahme: `ContextMenu` und `ToolTip`.

### DebugHelper + AutomationIds

Beide Pflicht-Helper liegen in `MeineApps.UI/DebugTools/`:

- **`debug:DebugHelper.ShowName="True"`** auf jedem Control-Root (Debug-only, zeigt Control-Namen oben links).
- **`AutomationProperties.AutomationId="…"`** auf allen interaktiven Elementen (Buttons, TextBoxes, ListBoxes, ComboBoxes, CheckBoxes) — Naming: `[Kontext][Aktion/Zweck][ControlTyp]` in PascalCase.

Beispiele: `LoginButton`, `EmailTextBox`, `CalculatePaymentButton`, `SettingsListBox`.

Details: [src/UI/MeineApps.UI/CLAUDE.md](src/UI/MeineApps.UI/CLAUDE.md).

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
| ArcaneKingdom (Unity) | #6B46C1 Royal-Purple + #F59E0B Gold | Magisches TCG, Fantasy-Premium (vorlaeufig) |

Implementierung: Jede Avalonia-App lädt `<StyleInclude Source="/Themes/AppPalette.axaml" />` in App.axaml. Alle DynamicResource-Keys bleiben identisch. Design-Tokens (Spacing, Radius, Fonts) kommen weiterhin aus `MeineApps.Core.Ava/Themes/ThemeColors.axaml`. ArcaneKingdom verwendet stattdessen ein eigenes Unity-USS-Theme — die hier dokumentierten Farben sind die Brand-Referenz.

---

## Packages (Avalonia 12.0.2 + .NET 10)

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 12.0.2 | UI Framework |
| Material.Icons.Avalonia | 3.0.2 | 7000+ SVG Icons (Auto-Sizing, Caching) |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM |
| Xaml.Behaviors.Avalonia | 12.0.0 | Behaviors |
| SkiaSharp | 3.119.4-preview.1.1 | 2D Graphics + SkSL GPU-Shader (Preview von Avalonia 12 erzwungen) |
| SkiaSharp.Skottie | 3.119.4-preview.1.1 | Lottie-Animations-Backend |
| Avalonia.Labs.Lottie | 12.0.2 | Lottie-Animationen (JSON) |
| AvaloniaUI.DiagnosticsSupport | 2.2.1 | DevTools (Debug-only, ersetzt Avalonia.Diagnostics) |
| Xamarin.Android.Google.BillingClient | 8.3.0.2 | Google Play Billing v8 |
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

### Icon-Strategie (verbindlich)

**Keine Unicode-Symbole als UI-Text** (▼ ▲ ★ ← →). Drei zugelassene Quellen:

1. `<ui:SvgIcon Kind="..."/>` aus `MeineApps.UI/Assets/Icons/AppIcons.axaml` (geteilt fuer alle Apps).
2. `<materialIcons:MaterialIcon Kind="..."/>` via `Material.Icons.Avalonia` (7000+ Icons).
3. App-spezifische Icon-Klassen (`BomberBlast.Icons.GameIcon`, `RebornSaga.Icons.SagaIcon`) —
   nur wenn das visuelle Konzept (Neon-Arcade / Anime / etc.) das rechtfertigt. Generische Apps
   duerfen keine eigenen Icon-Systeme einfuehren.

Neue Glyphen werden zuerst in `MeineApps.UI/Assets/Icons/AppIcons.axaml` als StreamGeometry
ergaenzt (24x24 ViewBox-Standard) und dort dokumentiert. Details: `src/UI/MeineApps.UI/CLAUDE.md`.

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

### Avalonia 12 Android-Lifecycle-Pattern

```csharp
// {App}.Android/AndroidApp.cs (NEU pro App):
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    public AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer) { }
}

// {App}.Android/MainActivity.cs:
[Activity(...)]
public class MainActivity : AvaloniaMainActivity   // KEIN <App>-Generic mehr!
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Factory-Setups die `this` brauchen bleiben hier (vor base.OnCreate moeglich):
        App.AppPathsFactory = () => new AndroidAppPaths(this);
        base.OnCreate(savedInstanceState);
    }
}
```

`CustomizeAppBuilder()` zog von `MainActivity` in `AvaloniaAndroidApplication<TApp>.OnCreate` um.
`ISingleViewApplicationLifetime` funktioniert in Avalonia 12 weiterhin als Fallback fuer
Android — keine Pflicht, auf `IActivityApplicationLifetime + MainViewFactory` zu wechseln.

### Avalonia 12 Clipboard-API

```csharp
// Vor (Avalonia 11):
await clipboard.SetTextAsync(text);

// Nach (Avalonia 12):
var data = new Avalonia.Input.DataTransfer();
data.Add(Avalonia.Input.DataTransferItem.CreateText(text));
await clipboard.SetDataAsync(data);

// GetText analog:
var data = await clipboard.TryGetDataAsync();
var text = await data.TryGetTextAsync();
```

`IDataObject` + `DataFormats` (Plural) entfernt → `DataTransfer` + `DataTransferItem` + `DataFormat` (Singular).
**WICHTIG:** Niemals `Dispose()` auf `IAsyncDataTransfer` aufrufen — Avalonia uebernimmt das.

### Avalonia 12 weitere API-Migrationen

- `control.GetVisualRoot()` → `TopLevel.GetTopLevel(control)`
- `Window.SystemDecorations` → `Window.WindowDecorations`
- `Avalonia.Diagnostics`-Paket → `AvaloniaUI.DiagnosticsSupport`
- `AttachDevTools()` → `AttachDeveloperTools()`
- `FuncMultiValueConverter`-Parameter `IEnumerable<TIn>` → `IReadOnlyList<TIn>`

### Android Back-Button Pattern (einheitlich, alle 12 Apps)

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

## Claude-Code Agents (24 Agents)

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

- **SessionStart**: MVVM-Strict-Reminder, Auto-Commit erlaubt (sinnvoll/logisch nach Änderungen), deutsche Umlaute (kein ae/oe/ue/ss), CLAUDE.md-Pflicht
- **PostToolUse Write/Edit auf `View*.axaml.cs`**: Injizierter Reminder für Code-Behind-Hygiene

---

## Troubleshooting

| Problem | Ursache | Lösung |
|---------|---------|---------|
| Material Icons unsichtbar | `MaterialIconStyles` nicht in App.axaml registriert | `<materialIcons:MaterialIconStyles />` in `<Application.Styles>` |
| DateTime Timer falsch (1h) | UTC→Lokal Konvertierung | `DateTimeStyles.RoundtripKind` bei Parse |
| Release-Build crasht (Debug OK) | Meist stale Build-Artefakte oder falsche Flags | obj/bin löschen, clean rebuild |
| Mono JIT Assertion `!ji->async` Crash | Profiled AOT (SDK-Default) kompiliert nur hot Methods, Rest fällt auf JIT zurück → JIT-Bug auf manchen Geräten (z.B. Huawei P30) | `AndroidEnableProfiledAot=false` in Directory.Build.targets → Full AOT (alle Methoden kompiliert, kein JIT). `UseInterpreter=true` geht NICHT zusammen mit AOT (XA0119) |
| Render-Crash "calling thread cannot access this object" | VM-Graph auf Background-Thread instanziiert (z.B. `Task.Run(() => GetRequiredService<MainViewModel>())` in Lade-Pipeline). ViewModels erzeugen im Ctor UI-Objekte (`new SolidColorBrush`, `[ObservableProperty] IBrush`) → falsche Thread-Affinity → Crash beim 1. Render (`Brush.get_Transform` → `VerifyAccess`) | VM-Erzeugung IMMER auf dem UI-Thread: `Dispatcher.UIThread.InvokeAsync(() => GetRequiredService<MainViewModel>()).GetTask()`. Schwere Background-Arbeit (Shader/Asset-Preload) bleibt parallel auf `Task.Run` |
| CSS translate() Exception | Fehlende px-Einheiten | `translate(0px, 400px)` statt `translate(0, 400)` |
| AAPT2260 Fehler | grantUriPermissions ohne 's' | `android:grantUriPermissions="true"` (mit 's') |
| ${applicationId} geht nicht | .NET Android kennt keine Gradle-Placeholder | Hardcodierte Package-Namen verwenden |
| Icons in Tab-Leiste fehlen | Material.Icons xmlns fehlt | `xmlns:materialIcons="using:Material.Icons.Avalonia"` |
| VersionCode Ablehnung | Code bereits im Play Store | VOR Release aktuelle Codes im Play Store prüfen |
| Release-App schließt sich beim 1. Start (VS) | VS kann in Release keinen Debugger anhängen | App manuell starten - funktioniert. Kein App-Bug, VS-Verhalten |
| Process.Start PlatformNotSupportedException | Android unterstützt UseShellExecute nicht | `UriLauncher.OpenUri(uri)` verwenden (MeineApps.Core.Ava) |
| `\u20ac` als Text in XAML | XAML interpretiert C#-Unicode-Escapes nicht | Direkt `€` schreiben oder `&#x20AC;` verwenden |
| TransformOperations CS0103 | `Avalonia.Media` reicht nicht | `using Avalonia.Media.Transformation;` hinzufügen |
| IsAnimating Property-Warnung | Kollidiert mit `AvaloniaObject.IsAnimating()` | Property umbenennen (z.B. `IsPulsing`) oder `new` Keyword |
| KeyFrame-Animation Crash "No animator" | `Style.Animations` hat keinen Animator für `RenderTransform` | NUR `Opacity`/`Width`/`Height` (double) in KeyFrames verwenden. `TransformOperationsTransition` in `Transitions` funktioniert |
| Style Selector AVLN2200 "Can not find parent" | `#Name` ohne Typ-Prefix | IMMER `Typ#Name` schreiben: `Grid#ModeSelector`, `Border#DisplayBorder` |
| Enum-Werte englisch in UI | Direktes Binding an Enum-Property | Display-Property mit lokalisiertem Text verwenden, im ViewModel per `GetString()` setzen |
| Daten erscheinen kurz, verschwinden | `_ = InitializeAsync()` mit `_list.Clear()` raced mit User-Aktionen | Task speichern: `_initTask = InitializeAsync()`, in Methoden `await _initTask` |
| Sprache immer Englisch trotz Geräteeinstellung | Erkannte Sprache nicht in Preferences gespeichert | `_preferences.Set(key, lang)` nach Gerätesprach-Erkennung in `Initialize()` |
| `IsAttachedToVisualTree` Kompilierfehler | Property in Avalonia 11.3 entfernt | `using Avalonia.VisualTree;` + `control.GetVisualRoot() != null` verwenden |
| InsertAsync gibt falsche ID zurück | sqlite-net `InsertAsync()` gibt Zeilen-Count zurück (immer 1), NICHT Auto-Increment-ID. sqlite-net setzt ID direkt auf dem Objekt | Nach `await db.InsertAsync(entity)` NICHT `entity.Id = result` schreiben - `entity.Id` ist bereits korrekt gesetzt |
| ScrollViewer scrollt nicht | `Padding` auf `ScrollViewer` verhindert Scrollen in Avalonia | `Padding` entfernen, stattdessen `Margin` auf das direkte Kind-Element setzen + `VerticalScrollBarVisibility="Auto"` |
| ZIndex-Overlay Touch geht durch (Android) | Avalonia `ZIndex` auf Grid-Kindern funktioniert NICHT für Hit-Testing auf Android - Touch-Events gehen durch Overlay hindurch | Content-Swap statt Overlay: Normalen Content per `IsVisible=false` verstecken, Overlay-Content als Ersatz anzeigen. KEIN ZIndex verwenden für interaktive Overlays |
| Assembly-Version 1.0.0 (Default) | Shared-Projekt hat keine `<Version>` Property → Assembly-Version ist 1.0.0.0 | `<Version>X.Y.Z</Version>` in Shared .csproj setzen wenn Assembly-Version zur Laufzeit ausgelesen wird |
| CommandParameter string→int Crash | XAML `CommandParameter="0"` ist IMMER `string`. `RelayCommand<int>` wirft `ArgumentException` in `CanExecute()` bei View-Attach | Methoden von `int` auf `string` ändern + `int.TryParse()` intern. Oder `<sys:Int32>0</sys:Int32>` im XAML. Betroffen: Alle hardcodierten CommandParameter-Werte |
| JsonSerializer.Serialize auf Background-Thread → Collection-Crash | `Task.Run(() => Serialize(state))` während GameLoop den State modifiziert | Serialisierung auf dem UI-Thread belassen (State ist klein, ~5-20ms). Alternative: DeepCopy vor Serialize |
| Bildschirm flimmert bei Tab-/View-Wechsel | `FadeInContentPanel()` setzt `Opacity=0` NACH Binding-Update → neuer View kurz sichtbar bei voller Opacity → schwarzer Blitz | `PageTransitionStarting` Event via `OnActivePageChanging()` — feuert VOR dem Wert-Wechsel. View setzt `Opacity=0` bevor Bindings die neue View einblenden |
| Startup langsam bei vielen Child-ViewModels | MainViewModel mit N Child-VMs als Singletons löst beim ersten `GetRequiredService<MainViewModel>()` alle Services+VMs transitiv auf — 200-500ms auf Mid-Tier-Android wenn N>15 | Spät-unlocked VMs als `Lazy<T>` injizieren, in `EnsureXxxVm()`-Methode beim ersten Navigations-Ziel instanziieren + verdrahten. Public Property `XxxViewModel?` ist nullable → `[ObservableProperty]` feuert OnPropertyChanged bei Ensure → XAML ContentControl bindet dann ein. `AddLazyResolution()` Extension registriert `Lazy<T>` im DI-Container. LanguageChanged-Handler nur `Xxx?.UpdateLocalizedTexts()` (null-safe). Pattern in BomberBlast v2.0.34 eingeführt |
| Firebase ServerValue.TIMESTAMP für Anti-Spoofing | Client-gesetzte Zeitstempel (`DateTime.UtcNow.ToString("O")`) sind manipulierbar → Leaderboards können gefälschte "Letzte Aktivität"-Werte bekommen | Firebase-Sentinel `{".sv":"timestamp"}` als Payload-Wert verwenden: `private static readonly Dictionary<string,string> FirebaseServerTimestamp = new() { [".sv"] = "timestamp" };`. Firebase löst das serverseitig in ms-Timestamp auf. Security-Rules können darauf Rate-Limits setzen. Keine client-seitige Logik darauf verlassen — nur als Anzeige/Rate-Limit |
| `GetString(key) ?? "fallback"` zeigt rohe Key-IDs in der UI | `LocalizationService.GetString` gibt bei FEHLENDEM RESX-Key den Key-NAMEN zurück (nie null) → das verbreitete `?? "fallback"` ist toter Code, die rohe Key-ID (z.B. `WhatsNew_2_0_62_Title`) erscheint im UI | Helper der den Miss erkennt (`var v = GetString(key); return v == key ? fallback : v;`) ODER RESX-Key in alle 6 Sprachen ergänzen. NIEMALS auf `?? default` bei GetString verlassen wenn der Key fehlen könnte |
| Property im Code-Behind gesetzt obwohl im XAML gebunden (z.B. `IsHitTestVisible`) | Avalonia-Value-Precedence: ein CLR-Setter (`control.X = ...` → `SetValue(LocalValue)`) belegt denselben Slot wie ein `{Binding}` ohne explizite Priorität und verdrängt es DAUERHAFT (Binding-Subscription wird disposed). Anders als WPF | Property NUR an EINER Stelle steuern. Für Overlay-Hit-Test: vollständiges Aggregat-Binding im XAML (`IsHitTestVisible="{Binding !IsAnyOverlayOpen}"`), KEIN Code-Behind-Setter daneben |
| Vermuteter "DataContext-Bug" bei verschachteltem x:DataType-Wechsel | Compiled Bindings lösen getrennte DataContext-/x:DataType-Ebenen (äußeres Element setzt `DataContext`, inneres Kind setzt `x:DataType`) in Avalonia 12 KORREKT auf — sowohl bei explizit gesetztem als auch geerbtem DataContext | Kein Fix am Binding nötig. Ursache leerer Bindings woanders suchen (fehlende RESX-Keys, null-VM). Belegt durch Headless-Test `tests/BomberBlast.Tests/Ui/DataContextPatternTests` |

**Themen-spezifische Gotchas (NICHT hier dokumentiert):**

- **SkiaSharp / SKCanvasView / Custom Controls / Behaviors** → [src/UI/MeineApps.UI/CLAUDE.md](src/UI/MeineApps.UI/CLAUDE.md) (DonutChart-360°, SKMaskFilter-Leak, Render-Loop, IsVisible-Toggle, PathIcon-Ableitung, TapScaleBehavior-InvalidCastException, Game-Loop-Startup, Button-RenderTransform-Pflicht).
- **AdMob / Billing / Rewarded / Ad-Banner-Layout / Play Review / MediaPlayer / Purchase-Restore** → [src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md](src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) (UMP-Namespace-Typo, Java Generics Erasure, Billing v8, Rewarded-Timeout, Ad-Spacer 64dp, Play-Review-Namespace, MediaPlayer-Binding).

---

## App-spezifische Gotchas

App-spezifische Bug-Patterns und Troubleshooting-Einträge liegen in den jeweiligen App-CLAUDE.md Dateien:

- **HandwerkerImperium** (Service-Caches, Gilden/Firebase, Worker/Mini-Games): `src/Apps/HandwerkerImperium/CLAUDE.md`
- **BingXBot** (SK-System, TP/SL, ATI, Triple-Entry, BingX-API): `src/Apps/BingXBot/CLAUDE.md`
- **BomberBlast** (Cloud-Save, Dungeon-Run, Rewarded-Cooldown): `src/Apps/BomberBlast/CLAUDE.md`
- **SmartMeasure** (BLE, ARCore, Bowyer-Watson, RTK-GPS): `src/Apps/SmartMeasure/CLAUDE.md`
- **RebornSaga** (Sprite-Cache, Scene-Manager, StoryEngine): `src/Apps/RebornSaga/CLAUDE.md`

Firebase-Security-Rules sind in `database.rules.json` (aktuell nur HandwerkerImperium).
