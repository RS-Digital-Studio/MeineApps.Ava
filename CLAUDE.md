# Meine Apps — Workspace-Architektur

Multi-Plattform App-Studio. **13 Avalonia-Apps** (Android + Windows + Linux, Avalonia 12 +
.NET 10, C# 14 — migriert von MAUI) und **3 Unity-6-Projekte**. Diese Datei beschreibt
**Architektur & Struktur** des Workspaces. Generische Arbeitsweise → globale CLAUDE.md.
Domänen-Details, Gotchas und Troubleshooting → jeweilige Library-/App-CLAUDE.md.

> **Unity-Projekte** (eigener Stack, **nicht** von `dotnet build` erfasst, eigene CLAUDE.md):
> - **ArcaneKingdom** — TCG + RPG, native Unity-Neuentwicklung (Designplan v4).
> - **BomberBlast.Unity** — modernes 3D-Bomberman auf Basis des produktiven BomberBlast (Unity 6 + URP), klassisch **aktiv gespielt**, mit **neuer Story** (Neo-Grid/Overseer/Reborn). **Kein Idle/AFK**, kein striktes 1:1-Remake; **reiner Single-Player** (kein Multiplayer).
> - **HandwerkerImperium.Unity** — Neuentwicklung parallel zur produktiven Avalonia-Version.
>
> Die Avalonia-Versionen von BomberBlast und HandwerkerImperium bleiben produktiv und werden
> weiter gepflegt; die Unity-Varianten laufen als parallele Beta.

---

## 1. Architektur (Pflicht)

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

**Abhängigkeits-Regeln (unverhandelbar):**

- App-Shared darf **nicht** auf andere Apps referenzieren.
- Libraries dürfen **nicht** auf Apps referenzieren.
- `MeineApps.UI` darf **nicht** auf `MeineApps.Core.Premium.Ava` referenzieren
  (Premium hängt von UI ab, nicht umgekehrt).
- Android-spezifische Klassen (`AndroidRewardedAdService`, `AndroidPurchaseService`, …) leben in
  `MeineApps.Core.Premium.Ava/Android/` und werden per `<Compile Include … Link="…" />` in jedes
  Android-Projekt eingebunden (Linked-File-Pattern → Premium-Library-CLAUDE.md).

### Domain vs. UI Logic

Views, Code-Behind und ViewModels enthalten **ausschließlich UI-Logik**. Domänenlogik
(Berechnungen, Persistenz, Geschäftsregeln) gehört in Services.

| Kategorie | Beispiel | Gehört nach |
|-----------|----------|-------------|
| Berechnung | Tilgungsplan, BMI, Material-Bedarf | `MeineApps.CalcLib` oder `{App}.Shared/Calculators/` |
| Persistenz | Save/Load JSON, SQLite | `{App}.Shared/Services/` (z.B. `SaveGameService`) |
| Geschäftsregeln | "Letzter Marker löscht Gruppe", Game-Balancing | `{App}.Shared/Services/` (z.B. `OrderGeneratorService`) |
| Plattform-API | AdMob, Billing, Haptics, FileShare | Interface in `MeineApps.Core.Ava`, Android-Impl in `MeineApps.Core.Premium.Ava/Android/` |

**Faustregel:** Braucht eine Methode kein Avalonia-API, gehört sie nicht in ein ViewModel.
**UI darf:** Service-Methoden aufrufen und Ergebnisse anzeigen, Confirmation-Dialoge vor
destruktiven Operationen zeigen, UI-Properties nach Service-Aufrufen aktualisieren.

### ViewModel-First

Alle neuen Controls, Pages und Popups folgen ViewModel-First:

1. **VM vor View** — ViewModel wird immer vor der View per DI im Composition Root erzeugt.
2. **Views erzeugen keine VMs** — View nimmt das VM via `DataContext`-Binding oder Constructor.
3. **DataContext VOR `InitializeComponent()`** — Pflicht für Compiled Bindings.
4. **Navigation via VM-Property** — `MainViewModel.CurrentPage = "route"` triggert
   ContentControl-Binding mit ViewLocator.

```csharp
// RICHTIG: ViewLocator-Konvention (BomberBlast.ViewModels.DashboardViewModel → BomberBlast.Views.DashboardView)
public DashboardView(DashboardViewModel vm)
{
    DataContext = vm;
    InitializeComponent();
}

// FALSCH: Code-Behind erzeugt VM / nutzt Service-Locator
public DashboardView()
{
    DataContext = new DashboardViewModel(ServiceLocator.Get<IFoo>());  // verboten
    InitializeComponent();
}
```

Parameterloser Designer-Fallback ist OK, darf aber kein VM erzeugen:
`public DashboardView() { InitializeComponent(); }`.

### Anti-Patterns (verboten)

| Anti-Pattern | Warum verboten | Stattdessen |
|--------------|----------------|-------------|
| `ServiceLocator.Resolve<T>()` außerhalb Composition Root | Versteckte Abhängigkeiten, untestbar | Constructor Injection |
| Statische Singletons (`Xxx.Instance`) | Keine Test-Isolation, Lifetime-Bugs | Interface via DI |
| God Interfaces (>5 Methoden) | Verstößt gegen ISP | Pro Verantwortlichkeit ein Interface |
| Parameterloser VM-Ctor mit `Resolve`-Delegation | Service-Locator-Anti-Pattern | Constructor Injection (parameterloser Ctor nur für Designer) |
| Hardcoded Werte in XAML (FontSize, Padding, Colors) | Verhindert Theming/Skalierung | `{StaticResource …}`/`{DynamicResource …}` aus `ThemeColors.axaml` |
| `Avalonia.Controls.Primitives.Popup` für Picker/Dropdowns | Desktop = eigenes OS-Fenster, Android = In-App → inkonsistent | Inline `Border`/`Panel` mit `IsVisible`-Binding |
| `DateTime.Now` für Persistenz | UTC-Konvertierungs-Bugs bei Timezone-Wechsel | `DateTime.UtcNow` + `"O"`-Format + `RoundtripKind` |
| Direkte AdMob-/Billing-Calls in ViewModels | Plattform-Lock-In, untestbar | `IRewardedAdService`/`IPurchaseService` via DI |

### Feature-Ordner (Vertical Slice)

Neue Features bekommen einen eigenen Ordner mit View + ViewModel + optional Service:

```
{App}.Shared/Views/MeinFeature/
├── MeinFeatureView.axaml(.cs)
└── (ggf.) MeinFeatureSubControl.axaml(.cs)
{App}.Shared/ViewModels/MeinFeatureViewModel.cs
{App}.Shared/Services/MeinFeatureService.cs   (wenn Service-Logik nötig)
```

Namespace: `{App}.Views.MeinFeature`, `{App}.ViewModels.MeinFeatureViewModel`.
Keine Dateien in Sammelordnern wie `ViewModels/Misc/` ablegen.

### Service-Extraktion + Event-Cleanup

Wenn ein ViewModel ~300 Zeilen überschreitet oder eine zusammenhängende Teilverantwortung hat:

1. **Partial-Split**, wenn die Logik kohärent bleibt (siehe `MainViewModel.cs`-Partials in
   HandwerkerImperium, BomberBlast).
2. **Service-Extraktion**, wenn die Logik unabhängig ist — neuer Service in
   `{App}.Shared/Services/`, Interface in `{App}.Shared/Services/Interfaces/`.
3. **Constructor Injection** statt Service-Locator.
4. **`IDisposable` implementieren**, wenn der Service Events abonniert.

```csharp
// Services, die Events abonnieren, MÜSSEN sich wieder abmelden:
public class MyService(IEventBus eventBus) : IDisposable
{
    public void Dispose() => eventBus.SomeEvent -= OnSomeEvent;
}

// Controls: DetachedFromVisualTree-Cleanup (Gegenstück zu AttachedToVisualTree)
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    _service.Changed -= OnChanged;
    base.OnDetachedFromVisualTree(e);
}
```

### Testbarkeit

`tests/` enthält **14 Test-Projekte** — eines je App plus `MeineApps.CalcLib.Tests`. Stack:
**xUnit v3** + NSubstitute (Mocks) + FluentAssertions + coverlet (Coverage). UI-nahe Tests
laufen headless über `Avalonia.Headless` + `Avalonia.Headless.XUnit` (z.B. HandwerkerImperium,
BomberBlast, RechnerPlus).

> **Test-Stack auf xUnit v3:** Die Solution nutzt durchgehend die `xunit.v3`-Linie (inkl.
> `Avalonia.Headless.XUnit`, `Verify.XunitV3`, `FsCheck.Xunit.v3`). Die frühere v2/v3-Kollision
> ist mit der Solution-weiten Umstellung auf v3 aufgelöst — neue Test-Projekte daher gegen
> `xunit.v3` referenzieren, nicht gegen die alte 2.x-Linie.

**Beim Hinzufügen oder Ändern testbarer Logik** (Berechnungen, Konvertierungen,
Zustandsverwaltung, Parser, Algorithmen) gehören passende Unit-Tests ins zugehörige
Test-Projekt. Nicht nötig für reine UI-Verdrahtung, triviale Property-Wrapper oder Code, der
ausschließlich Avalonia/Android-APIs aufruft. Domain-Code (`GameLoopService`,
`OrderGeneratorService`, BingXBot SK-System, …) per Interface von Plattform-APIs entkoppeln,
damit er testbar bleibt.

---

## 2. Projektstruktur

```
F:\Meine_Apps_Ava\
├── MeineApps.Ava.sln
├── Directory.Build.props           # Globale Build-Settings (net10.0, C#14, Nullable, Compiled Bindings)
├── Directory.Build.targets         # Android-Settings (Signing, AAB, Full AOT, Symbol-Stripping)
├── Directory.Packages.props        # Central Package Management
├── CLAUDE.md                       # diese Datei
├── Releases/                       # meineapps.keystore, CHANGELOGs, AABs
│
├── src/
│   ├── Libraries/                       # Geteilte Bibliotheken (keine Referenz auf Apps)
│   │   ├── MeineApps.CalcLib/            # Calculator-Engine (Tokenizer + Parser + Evaluator)
│   │   ├── MeineApps.Core.Ava/          # Preferences, Lokalisierung, Themes, Converters, ViewLocator
│   │   └── MeineApps.Core.Premium.Ava/  # AdMob, Google Play Billing, Trial (Android-Linked-Files)
│   │
│   ├── UI/
│   │   └── MeineApps.UI/                # Custom Controls, Behaviors, SkiaSharp, GPU-Shader, Loading-Pipeline
│   │
│   └── Apps/                            # 13 Avalonia-Apps + 3 Unity-Projekte
│       ├── RechnerPlus/                 # Taschenrechner (werbefrei)
│       ├── ZeitManager/                 # Timer/Stoppuhr/Alarm (werbefrei)
│       ├── FinanzRechner/               # 6 Finanzrechner + Budget-Tracker
│       ├── FitnessRechner/              # BMI/Kalorien/Barcode-Scanner
│       ├── HandwerkerRechner/           # 19 Rechner (5 Floor + 14 Premium, alle frei)
│       ├── WorkTimePro/                 # Arbeitszeiterfassung + Export
│       ├── HandwerkerImperium/          # Idle-Game (Werkstätten + Arbeiter)
│       ├── BomberBlast/                 # Bomberman-Klon (SkiaSharp, Landscape)
│       ├── RebornSaga/                  # Anime Isekai-RPG (volle SkiaSharp-Engine)
│       ├── BingXBot/                    # Trading Bot — .Shared .Android .Desktop + .Server (Pi 24/7)
│       │                                #   + 7 app-eigene Backend-Libs: .Core .Contracts .Engine .Exchange .Trading .Backtest .ClientApi
│       ├── GardenControl/               # Bewässerung — .Core .Shared .Android .Desktop + .Server (Pi)
│       ├── SmartMeasure/                # 3D-Grundstücksvermessung (RTK-GPS, privat)
│       ├── SunSeeker/                   # Solarpanel-Ausrichtung (Sonnenstand, Bifazial, privat)
│       ├── ArcaneKingdom/               # TCG + RPG  (Unity 6)
│       ├── BomberBlast.Unity/           # Treuer 3D-Remake des Originals (Unity 6 + URP)
│       └── HandwerkerImperium.Unity/    # Neuentwicklung parallel zur Avalonia-Version (Unity 6)
│
├── tools/                               # .NET-Tools (via dotnet run) + Python-Skripte
│   ├── AppChecker/                      # .NET — 34 Checker, 200+ Prüfungen
│   ├── StoreAssetGenerator/             # .NET — Play-Store-Assets (SkiaSharp)
│   ├── SocialPostGenerator/             # .NET — Social-Media-Posts + Promo-Bilder
│   ├── BingXBacktestLab/                # .NET — Strategie-Backtest auf echten Klines (standalone, nicht in .sln)
│   ├── BingXBotTrainer/                 # Python — ONNX-Modell-Training (BingXBot)
│   ├── ContentPipeline/                 # Python — Google-Sheets-Sync für Game-Content/Übersetzungen
│   ├── SkAnalytics/                     # Python — Positions-/Snapshot-Analyse
│   ├── SoundForge/                      # Python — Audio-Generierung (+ lufs-mastering.sh)
│   └── screenshot-mcp/                  # MCP-Server für Screenshots
│
└── tests/                               # xUnit v3 — 14 Projekte (je App + MeineApps.CalcLib)
```

---

## 3. Conventions & Patterns

### Naming

| Element | Convention | Beispiel |
|---------|-----------|----------|
| ViewModel | Suffix `ViewModel` | `MainViewModel`, `TileCalculatorViewModel` |
| View | Suffix `View` | `MainView.axaml`, `SettingsView.axaml` |
| Service-Interface | `I{Name}Service` | `IPreferencesService`, `ILocalizationService` |
| Service-Impl | `{Name}Service` | `PreferencesService`, `LocalizationService` |
| Navigation-Event | `NavigationRequested` | `Action<string>` |
| Message-Event | `MessageRequested` | `Action<string, string>` |
| UI-Feedback-Event | `FloatingTextRequested` | `EventHandler<(string, string)>` |
| Celebration-Event | `CelebrationRequested` | `EventHandler` |

### Icon-Strategie (verbindlich)

**Keine Unicode-Symbole als UI-Text** (▼ ▲ ★ ← →). Drei zugelassene Quellen:

1. `<ui:SvgIcon Kind="…"/>` aus `MeineApps.UI/Assets/Icons/AppIcons.axaml` (geteilt, erste Wahl).
2. `<materialIcons:MaterialIcon Kind="…"/>` via `Material.Icons.Avalonia` (7000+ Icons).
3. App-spezifische Icon-Klassen (`BomberBlast.Icons.GameIcon`, `RebornSaga.Icons.SagaIcon`) — nur
   wenn das visuelle Konzept (Neon-Arcade/Anime) es rechtfertigt. Generische Apps führen keine
   eigenen Icon-Systeme ein.

Neue Glyphen zuerst in `AppIcons.axaml` als StreamGeometry (24×24 ViewBox) ergänzen und dort
dokumentieren. Details → `MeineApps.UI/CLAUDE.md`.

### DI-Pattern

**Lifetimes:** Services → Singleton (Preferences, Localization, Database). MainViewModel →
Singleton (hält Child-VMs). Child-ViewModels → Transient oder Singleton (je nach App). Spät
freigeschaltete VMs → `Lazy<T>` (Startup-Performance, siehe Core.Ava-CLAUDE.md).

**Constructor Injection immer** — Child-VMs werden in MainViewModel per Constructor injiziert.
Keine Property Injection, kein Service-Locator.

**Android Platform-Services (Factory-Pattern)** — plattformspezifische Services per statischer
Factory in `App.axaml.cs`, gesetzt in `MainActivity.cs`. Hält das Shared-Projekt frei von
Android-Abhängigkeiten:

```csharp
// App.axaml.cs (Shared)
public static Func<IServiceProvider, IRewardedAdService>? RewardedAdServiceFactory { get; set; }
// MainActivity.cs (Android)
App.RewardedAdServiceFactory = sp => new AndroidRewardedAdService(helper, sp.GetRequiredService<IPurchaseService>());
```

### Localization Pattern

- ResourceManager-basiert via `ILocalizationService.GetString("Key")`.
- 6 Sprachen: DE, EN, ES, FR, IT, PT. Jede App hat eigenes `AppStrings.resx`.
- `AppStrings.Designer.cs` manuell pflegen (nicht auto-generiert bei CLI-Build).
- `LanguageChanged`-Event → MainViewModel benachrichtigt alle Child-VMs via `UpdateLocalizedTexts()`.
- Alle View-Strings lokalisiert, keine hardcodierten Texte. Englisch ist Basis-Sprache für Fallbacks.

### Navigation Pattern (Event-basiert, kein Shell)

```csharp
// Child-ViewModel
public event Action<string>? NavigationRequested;
NavigationRequested?.Invoke("route");
// MainViewModel
_childVM.NavigationRequested += route => CurrentPage = route;
```

`".."` = zurück zum Parent · `"../subpage"` = zum Parent, dann zu subpage.

### Android Back-Button Pattern (einheitlich)

`BackPressHelper` (Core.Ava) für Double-Back-to-Exit. `MainViewModel.HandleBackPressed()`:
(1) App-Overlays/Dialoge schließen, (2) Sub-Navigation zurück, (3) am Ende
`_backPressHelper.HandleDoubleBack(msg)`. `MainActivity.OnBackPressed()` delegiert an
`_mainVm.HandleBackPressed()`. WorkTimePro nutzt `FloatingTextRequested` statt `ExitHintRequested`.

### DateTime Pattern

- **Persistenz:** immer `DateTime.UtcNow` (nie `DateTime.Now`).
- **Format:** ISO 8601 `"O"` → `dateTime.ToString("O")`.
- **Parse:** immer `DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`.
- **Tages-Tracking:** `DateTime.UtcNow.Date` für datumsbasierte Gruppierung.

### Thread-Safety

```csharp
// Async: SemaphoreSlim
private readonly SemaphoreSlim _semaphore = new(1, 1);
await _semaphore.WaitAsync(); try { /* ... */ } finally { _semaphore.Release(); }
// UI-Thread: Dispatcher
Dispatcher.UIThread.Post(() => SomeProperty = newValue);
```

VM-Objektgraphen **nie** auf einem Background-Thread instanziieren — ViewModels erzeugen im Ctor
UI-Objekte (Brushes) und brauchen UI-Thread-Affinität (Details → Core.Ava-CLAUDE.md).

### Tab-Navigation (UI)

MainView: `Border.TabContent` + `.Active`-CSS-Klassen, Tab-Switching via `IsXxxActive`-bools,
Fade-Transition (`DoubleTransition` auf Opacity, 150ms). Border-Wrapping nötig (Child-Views
haben eigenen DataContext).

### Error-Handling Pattern

```csharp
public event Action<string, string>? MessageRequested;   // statt Debug.WriteLine
try { /* ... */ } catch (Exception) { MessageRequested?.Invoke("Fehler", "Speichern fehlgeschlagen"); }
```

### Avalonia 12 — Plattform- & API-Conventions

**Android-Lifecycle** (Avalonia 12):

```csharp
// {App}.Android/AndroidApp.cs (pro App)
[Application]
public class AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer) { }

// {App}.Android/MainActivity.cs — KEIN <App>-Generic mehr
[Activity(...)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        App.AppPathsFactory = () => new AndroidAppPaths(this);   // Factories vor base.OnCreate
        base.OnCreate(savedInstanceState);
    }
}
```

`CustomizeAppBuilder()` zog von `MainActivity` nach `AvaloniaAndroidApplication<TApp>.OnCreate`.
`ISingleViewApplicationLifetime` funktioniert weiter als Android-Fallback.

> **Platform-Factory-Registrierung MUSS lazy sein (Avalonia-12-Reihenfolge).**
> `OnFrameworkInitializationCompleted` (→ `ConfigureServices` + `BuildServiceProvider`) läuft in
> `AvaloniaAndroidApplication<App>.OnCreate` auf **Application-Ebene VOR `MainActivity.OnCreate`**,
> wo die `App.*Factory`-Properties gesetzt werden. Die DI-Registrierung der Platform-Services
> (Ads, Purchase, Audio, Sound, Notification, PlayGames, Vibration, CloudSave, FileShare, Haptic, …)
> darf die Factory daher **niemals zur Build-Zeit prüfen** (`if (Factory != null) AddSingleton(...)`)
> — der Guard greift immer auf `null` und brennt den Desktop-/Null-Default ein (Rewarded-Ad gibt
> Belohnung ohne Video, IAP tot, kein Sound/Haptik …). Stattdessen die Factory **im Resolve-Lambda**
> lesen, das erst beim ersten Resolve (nach `MainActivity.OnCreate`) läuft:
> ```csharp
> services.AddSingleton<IYyy>(sp =>
>     YyyFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance<DefaultImpl>(sp));
> ```
> Registrierungs-Details + warum → [Premium-Library-CLAUDE.md](src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) (Factory-Override).

**Clipboard (Avalonia 12):**

```csharp
var data = new Avalonia.Input.DataTransfer();
data.Add(Avalonia.Input.DataTransferItem.CreateText(text));
await clipboard.SetDataAsync(data);
// Lesen: var d = await clipboard.TryGetDataAsync(); var t = await d.TryGetTextAsync();
```

`IDataObject`/`DataFormats` → `DataTransfer` + `DataTransferItem` + `DataFormat` (Singular).
**Nie** `Dispose()` auf `IAsyncDataTransfer` aufrufen — Avalonia übernimmt das.

**Weitere API-Migrationen:** `control.GetVisualRoot()` → `TopLevel.GetTopLevel(control)` ·
`Window.SystemDecorations` → `Window.WindowDecorations` · `AttachDevTools()` →
`AttachDeveloperTools()` (Paket `AvaloniaUI.DiagnosticsSupport`) · `FuncMultiValueConverter`-
Parameter `IEnumerable<TIn>` → `IReadOnlyList<TIn>`.

### UriLauncher (plattformübergreifend)

`UriLauncher.OpenUri(uri)` statt `Process.Start` (Android kennt kein `UseShellExecute`).
`UriLauncher.ShareText(text, title)` für natives Share-Sheet (Android) bzw. Clipboard (Desktop).
Plattform-Hooks `App.PlatformOpenUri`/`App.PlatformShareText` werden in `MainActivity.cs` gesetzt.

### DebugHelper + AutomationIds (Pflicht)

Helper in `MeineApps.UI/DebugTools/`:
- `debug:DebugHelper.ShowName="True"` auf jedem Control-Root (Debug-only).
- `AutomationProperties.AutomationId="…"` auf allen interaktiven Elementen (Buttons, TextBoxes,
  ListBoxes, ComboBoxes, CheckBoxes), Naming `[Kontext][Aktion/Zweck][ControlTyp]` in PascalCase
  (`LoginButton`, `CalculatePaymentButton`). Details → `MeineApps.UI/CLAUDE.md`.

### C# 14 / .NET 10 (verbindlich)

Primary Constructors · Collection Expressions (`["A","B"]`) · Pattern Matching / switch
expressions · Records für immutable DTOs/Events · File-scoped Namespaces · Raw String Literals
(`"""…"""`) für JSON/SQL · Required Members.

---

## 4. App-Portfolio

| App | Version | Ads | Premium | Status |
|-----|---------|-----|---------|--------|
| RechnerPlus | v2.0.7 | Nein | Nein | Geschlossener Test |
| ZeitManager | v2.0.7 | Nein | Nein | Geschlossener Test |
| HandwerkerRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FinanzRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| FitnessRechner | v2.0.7 | Banner + Rewarded | 3,99 remove_ads | Geschlossener Test |
| WorkTimePro | v2.0.7 | Banner + Rewarded | 3,99/Mo oder 19,99 Lifetime | Geschlossener Test |
| HandwerkerImperium | v2.1.4 | Rewarded (kein Banner) | 4,99 Premium | Produktion |
| BomberBlast | v2.0.65 | Rewarded (kein Banner) | 1,99 remove_ads | Produktion |
| RebornSaga | v1.0.0 | Rewarded (kein Banner) | Gold-Pakete + remove_ads | Entwicklung |
| BingXBot | v1.8.1 | Nein | Nein | Entwicklung (Pi + Desktop + Android Remote) |
| GardenControl | v1.0.0 | Nein | Nein | Entwicklung (Pi + Desktop + Android) |
| SmartMeasure | v1.1.9 | Nein | Nein | Entwicklung (privat, RTK-GPS) |
| SunSeeker | v0.1.0 | Nein | Nein | Entwicklung (privat, Solar-Ausrichtung) |
| ArcaneKingdom (Unity) | v0.0.2 | TBD | Diamanten-Packs | Pre-MVP |
| BomberBlast.Unity | — | TBD | TBD | Pre-MVP (Beta parallel) |
| HandwerkerImperium.Unity | — | TBD | TBD | Pre-MVP (Beta parallel) |

### App-Farbpaletten

Jede App hat eine eigene `Themes/AppPalette.axaml` im Shared-Projekt, statisch in App.axaml
geladen (kein dynamischer Theme-Wechsel). Alle `DynamicResource`-Keys bleiben identisch;
Design-Tokens (Spacing, Radius, Fonts) kommen aus `MeineApps.Core.Ava/Themes/ThemeColors.axaml`.

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
| SmartMeasure | #FF6B00 Orange | Technisch-Professionell |
| SunSeeker | #FFB300 Sonnen-Amber | Solar / Dämmerungs-Dashboard |
| ArcaneKingdom (Unity) | #f5c842 Gold (UI-Leitfarbe) · #6B46C1 Royal-Purple (Sekundär-Akzent) | Dark-Fantasy TCG, "Arcane Realm"-Design (eigenes USS-Theme) |

---

## 5. Build & Konfiguration

### Build-Befehle

```bash
# Gesamte Solution
dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln

# Einzelne App ({App} ersetzen)
dotnet build src/Apps/{App}/{App}.Shared
dotnet run   --project src/Apps/{App}/{App}.Desktop
dotnet build src/Apps/{App}/{App}.Android

# Desktop Release
dotnet publish src/Apps/{App}/{App}.Desktop -c Release -r win-x64     # bzw. linux-x64
# Android Release (AAB) → bin/Release/net10.0-android/publish/
dotnet publish src/Apps/{App}/{App}.Android -c Release

# Tests (xUnit v3 — je App ein Projekt + CalcLib)
dotnet test tests/{App}.Tests                    # einzelnes Test-Projekt
dotnet test MeineApps.Ava.sln                    # alle Tests

# AppChecker (34 Checker, 200+ Prüfungen)
dotnet run --project tools/AppChecker            # alle Apps
dotnet run --project tools/AppChecker {App}      # einzelne App
dotnet run --project tools/AppChecker --quiet | --fail-only | --json

# StoreAssetGenerator / SocialPostGenerator
dotnet run --project tools/StoreAssetGenerator [Filter]
dotnet run --project tools/SocialPostGenerator post {App} <x|reddit>
dotnet run --project tools/SocialPostGenerator image <{App}|portfolio>
```

### Build-Konfiguration (Directory.Build.props / .targets)

**`Directory.Build.props`** (alle Projekte): `net10.0` (Default), `LangVersion=latest`,
`Nullable=enable`, `ImplicitUsings=enable`, `AvaloniaUseCompiledBindingsByDefault=true`.
`NoWarn` für `NU1902;NU1903` (ImageSharp-CVE, nur transitiv über PdfSharpCore, keine
User-Bild-Verarbeitung). Company/Copyright: RS Digital.

**`Directory.Build.targets`** (nur `*-android`, ausgewertet *nach* den Projektdateien):

| Setting | Wert / Grund |
|---------|--------------|
| Signing | Keystore `Releases\meineapps.keystore`, Alias `meineapps` |
| `AndroidPackageFormat` | `aab` (Play Store erfordert AAB) |
| `AndroidEnableProfiledAot=false` | **Full AOT** — alle Methoden kompiliert, kein JIT-Fallback. Behebt Mono-JIT-Assertion `!ji->async` (z.B. Huawei P30). `UseInterpreter` ist mit AOT inkompatibel (XA0119). |
| Debug-Symbole | Release: `DebugType=embedded` + `AndroidIncludeDebugSymbols=false` → Symbole **nicht** in der AAB (erschwert Reverse-Engineering), aber lokal für Play-Console-Upload erzeugt. Debug: `portable`. |
| D8/DEX | `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` mit `ExcludeAssets=all` (Duplicate-Class-Fix gegen `…Annotation.Android`). |

### Packages (Central Package Management)

Versionen zentral in `Directory.Packages.props`. Kern:

| Package | Version | Zweck |
|---------|---------|-------|
| Avalonia | 12.0.4 | UI-Framework (migriert von MAUI) |
| Material.Icons.Avalonia | 3.0.2 | 7000+ SVG-Icons |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM |
| Xaml.Behaviors.Avalonia | 12.0.0.1 | Behaviors |
| SkiaSharp (+ Skottie) | 3.119.4 | 2D-Graphics + SkSL GPU-Shader |
| Avalonia.Labs.Lottie | 12.0.2 | Lottie-Animationen |
| AvaloniaUI.DiagnosticsSupport | 2.2.2 | DevTools (Debug-only) |
| sqlite-net-pcl | 1.9.172 | Datenbank |
| **Premium (Android)** | | |
| Xamarin.GooglePlayServices.Ads.Lite | 124.0.0.5 | AdMob (+ UserMessagingPlatform 4.0.0.2) |
| Xamarin.Android.Google.BillingClient | 9.0.0 | Google Play Billing v9 |
| Xamarin.Google.Android.Play.Review | 2.0.2.7 | In-App Review |
| Xamarin.GooglePlayServices.Games.V2 | 121.0.0.3 | Play Games Services v2 |
| Xamarin.Firebase.Messaging / .Config | 125.0.2 / 123.1.0 | Push + Remote Config (kein Crashlytics/Analytics) |
| **Feature-spezifisch** | | |
| Xamarin.AndroidX.Camera.* / MLKit.BarcodeScanning | 1.6.1 / 117.3.0.7 | Kamera + Barcode (FitnessRechner) |
| PdfSharpCore / ClosedXML | 1.3.67 / 0.105.0 | PDF-/Excel-Export (WorkTimePro) |
| Skender.Stock.Indicators | 2.7.1 | Trading-Indikatoren (BingXBot) |
| Microsoft.AspNetCore.SignalR.Client | 10.0.8 | Server-Remote (BingXBot, GardenControl) |
| System.Device.Gpio / Iot.Device.Bindings | 4.2.0 | Raspberry-Pi-GPIO (GardenControl) |
| Mapsui.Avalonia12 / InTheHand.BluetoothLE / Vapolia.Google.ARCore | 5.1.0 / 4.0.44 / 1.47.1 | Karten + BLE + AR (SmartMeasure) |
| MQTTnet | 5.0.1.1416 | Anker-Cloud-Live-Watt via mTLS-MQTT (SunSeeker) |
| **Test** | | |
| xunit.v3 (+ runner.visualstudio) | 3.2.2 / 3.1.5 | Test-Framework (v3-Linie, inkl. Avalonia.Headless.XUnit) |
| NSubstitute / FluentAssertions / coverlet.collector | 5.3.0 / 8.10.0 / 10.0.1 | Mocks / Assertions / Coverage |

### Keystore

`F:\Meine_Apps_Ava\Releases\meineapps.keystore` · Alias `meineapps` · Passwort `MeineApps2025`
(in `Directory.Build.targets`).

---

## 6. Werkzeuge: Workflows, Skills, Hooks

Agent-Roster (Modell/Effort) → **globale CLAUDE.md**. Projekt-spezifische Verkettung:

| Szenario | Ablauf |
|----------|--------|
| **Neue View** | `planner` → Skill `new-view` → `mvvm-auditor` → `code-review` |
| **Neuer Service** | `planner` → Skill `new-service` → `code-review` → `tester` |
| **Bug fixen** | `debugger` → fixen → `code-review` → ggf. `tester` (Regression) |
| **Release (App)** | `pre-release` → `localize` → `deploy` |
| **Release (Server)** | `pre-release` → Skill `server-deploy` → `server-ops` (Verifikation) |
| **BingXBot-Problem** | `bingxbot` (Domain) → ggf. `debugger` / `server-ops` |
| **MVVM-Sanierung** | `mvvm-auditor` (App-weit) → `code-review` → Build-Verifikation |
| **Refactoring** | `health` → `refactor` → `code-review` → `tester` |
| **Game-Update** | `game-audit` → implementieren → `skiasharp` (falls Rendering) → `pre-release` |

**Skills (projekt-lokal):** `build-check`, `app-status`, `new-view`, `new-service`,
`mvvm-check`, `localize-check`, `release`, `server-deploy`, `changelog`.

**Hooks (User-Settings):** *SessionStart* — MVVM-Strict-Reminder, Auto-Commit-Erlaubnis,
deutsche Umlaute, CLAUDE.md-Pflicht. *PostToolUse* (Write/Edit auf `View*.axaml.cs`) —
Code-Behind-Hygiene-Reminder.

---

## 7. Dokumentations-Karte

**Doktrin:** Oben (diese Datei) lebt Architektur & Struktur. Nach unten wird jede CLAUDE.md
konkreter zu ihrem Gebiet. **Gotchas/Troubleshooting leben in der Domänen-Datei, nicht hier:**

| Thema | Heimat |
|-------|--------|
| Rendering, SkiaSharp, SKCanvasView, Custom Controls, Behaviors, Shader | `MeineApps.UI/CLAUDE.md` |
| AdMob, Billing, Rewarded, Ad-Banner-Layout, Play-Review, Trial | `MeineApps.Core.Premium.Ava/CLAUDE.md` |
| Avalonia-/MVVM-/Daten-Framework-Fallstricke (Value-Precedence, CommandParameter, Lazy-VMs, sqlite, …) | `MeineApps.Core.Ava/CLAUDE.md` |
| App-spezifische Logik (Cloud-Save, SK-System, Sprite-Cache, BLE/RTK, …) | jeweilige App-CLAUDE.md |
| Datierte Erkenntnisse / Build-Historie | Memory `lessons-learned.md`, Git, `Releases/{App}/CHANGELOG` |

**Libraries:**

| Projekt | Details |
|---------|---------|
| MeineApps.Core.Ava | [src/Libraries/MeineApps.Core.Ava/CLAUDE.md](src/Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| MeineApps.Core.Premium.Ava | [src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md](src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| MeineApps.CalcLib | [src/Libraries/MeineApps.CalcLib/CLAUDE.md](src/Libraries/MeineApps.CalcLib/CLAUDE.md) |
| MeineApps.UI | [src/UI/MeineApps.UI/CLAUDE.md](src/UI/MeineApps.UI/CLAUDE.md) |

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
| SunSeeker | [src/Apps/SunSeeker/CLAUDE.md](src/Apps/SunSeeker/CLAUDE.md) |
| ArcaneKingdom (Unity) | [src/Apps/ArcaneKingdom/CLAUDE.md](src/Apps/ArcaneKingdom/CLAUDE.md) |
| BomberBlast.Unity | [src/Apps/BomberBlast.Unity/CLAUDE.md](src/Apps/BomberBlast.Unity/CLAUDE.md) |
| HandwerkerImperium.Unity | [src/Apps/HandwerkerImperium.Unity/CLAUDE.md](src/Apps/HandwerkerImperium.Unity/CLAUDE.md) |

**Tools (.NET, eigene CLAUDE.md):** [AppChecker](tools/AppChecker/CLAUDE.md) ·
[StoreAssetGenerator](tools/StoreAssetGenerator/CLAUDE.md) ·
[SocialPostGenerator](tools/SocialPostGenerator/CLAUDE.md) ·
[BingXBacktestLab](tools/BingXBacktestLab/CLAUDE.md)
**Python-Skripte (README statt CLAUDE.md):** `BingXBotTrainer`, `ContentPipeline`, `SkAnalytics`, `SoundForge`.

Die `BingXBot.*`-Backend-Libraries (`src/Apps/BingXBot/`, app-eigenes Subsystem) und die `*.Server`-Projekte sind in der
[BingXBot-App-CLAUDE.md](src/Apps/BingXBot/CLAUDE.md) bzw.
[GardenControl-App-CLAUDE.md](src/Apps/GardenControl/CLAUDE.md) dokumentiert (keine eigene Lib-CLAUDE.md).

Firebase-Security-Rules: `database.rules.json` (Root, aktuell nur HandwerkerImperium).
