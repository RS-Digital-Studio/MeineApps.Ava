# MeineApps.Core.Ava — Shared Core Library

Plattform-Abstraktionen und gemeinsame Bausteine für alle 12 Avalonia-Apps: Preferences,
Lokalisierung, haptisches Feedback, Datei-Sharing, Calculator-History, Einheiten-Umrechnung,
Converters, Design-Tokens, ViewLocator sowie Async- und ViewModel-Bausteine.

> Architektur, DI-/MVVM-/Naming-/DateTime-/Localization-Conventions und das App-Portfolio
> (inkl. Farbpaletten) stehen in der [Haupt-CLAUDE.md](../../../CLAUDE.md) — hier nicht wiederholt.
> Diese Datei beschreibt nur die konkreten Bausteine dieser Library und die generischen
> Framework-Fallstricke (deren Heimat laut Doktrin hier ist). Rendering/SkiaSharp →
> [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md), Monetarisierung →
> [MeineApps.Core.Premium.Ava](../MeineApps.Core.Premium.Ava/CLAUDE.md), App-Logik → App-CLAUDE.md.

## Struktur

```
MeineApps.Core.Ava/
├── Services/
│   ├── IPreferencesService + PreferencesService           # JSON, debounced (500ms), Suspend/Resume/Flush
│   ├── IHapticService + NoOpHapticService                 # Vibrations-Abstraktion (Desktop = NoOp)
│   ├── BackPressHelper                                     # Double-Back-to-Exit (Android)
│   ├── ICalculationHistoryService + CalculationHistoryService  # History pro Rechner (Calculator-Apps)
│   ├── IUnitConverterService + UnitConverterService        # Länge/Fläche/Volumen/Gewicht (Metrisch/Imperial)
│   ├── IFileShareService + DesktopFileShareService         # Datei-Sharing (Android-Override per Factory)
│   ├── AtomicFileWriter                                    # Static: atomares Schreiben (Temp + Rename)
│   └── UriLauncher                                         # Static: OpenUri + ShareText (plattformübergreifend)
├── Localization/
│   ├── ILocalizationService + LocalizationService          # ResourceManager-Wrapper, LanguageChanged-Event
│   ├── ILocalizable                                        # Marker-Interface: VM.UpdateLocalizedTexts()
│   └── TranslateExtension                                  # XAML Markup-Extension für Lokalisierung
├── ViewModels/
│   ├── ViewModelBase                                       # abstract : ObservableObject (leer)
│   ├── INavigationSource                                   # Marker: event Action<string> NavigationRequested
│   └── IMessageSource                                      # Marker: event Action<string,string> MessageRequested
├── Async/
│   ├── AsyncDebouncer                                      # CTS-basiertes Debounce für async Aktionen
│   └── ForgetExtensions                                    # Task.Forget() / RunForget() mit Fehler-Logging
├── ViewLocator.cs                                          # VM → View per Namens-Konvention
├── Themes/
│   └── ThemeColors.axaml                                   # Design-Tokens (Spacing, Radius, Fonts)
└── Converters/
    ├── BoolConverters.cs       # BoolToVisibility, BoolToString, BoolToBrush, BoolToOpacity, InverseBool
    ├── StringConverters.cs     # StringIsNullOrEmpty, StringIsNotNullOrEmpty, StringFormat, StringTruncate
    ├── NumberConverters.cs     # NumberFormat, Currency, Percentage, IsPositive
    ├── DateTimeConverters.cs   # DateTimeFormat, RelativeTime (UTC), TimeSpanFormat, Duration
    └── ColorConverters.cs      # StringToColorBrush, ColorToBrush, StringToColor (alle mit statischer Instance)
```

Abhängigkeiten (Versionen zentral in `Directory.Packages.props`): `Avalonia`,
`CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection.Abstractions`.

## Services

### `IPreferencesService` / `PreferencesService`

JSON-Datei in `{ApplicationData}/{appName ?? "MeineApps"}/preferences.json`. Generische
`Get<T>(key, default)` / `Set<T>(key, value)`-API. **Debounced**: jeder Set startet/resettet
einen 500ms-Timer; `Dispose()` flusht synchron.

```csharp
_prefs.Set("key", value);
var val = _prefs.Get<string>("key", "default");
```

`SuspendPersistence()` / `ResumePersistence()` / `FlushPending()`: Disk-Writes für
performance-kritische Phasen (laufendes Spiel) pausieren — In-Memory-Stand bleibt aktuell,
aufgestaute Änderungen werden beim Resume bzw. via `FlushPending()` (z.B. App-Background-Hook)
einmalig geschrieben. Suspend ist idempotent.

### `IHapticService` / `NoOpHapticService`

Haptik-Abstraktion. `NoOpHapticService` ist der Desktop-Fallback (keine Vibrations-Hardware).
Android-Apps registrieren `AndroidHapticService` via Factory-Pattern in `App.axaml.cs`.

```csharp
_haptic.IsEnabled = true;
_haptic.Tick();        // Leicht  (Ziffern, Tab-Wechsel)
_haptic.Click();       // Mittel  (Speichern, CheckIn/CheckOut)
_haptic.HeavyClick();  // Stark   (Berechnung, Achievement, Alarm-Dismiss)
```

### `BackPressHelper`

Double-Back-to-Exit-Logik für Android (siehe Back-Button-Pattern in der Haupt-CLAUDE.md).
App-MainViewModels nutzen diese Klasse statt eigener Felder.

```csharp
private readonly BackPressHelper _backPressHelper = new();
_backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg); // im Ctor
// Am Ende von HandleBackPressed() (nach app-spezifischen Checks):
return _backPressHelper.HandleDoubleBack(exitMessage);
```

WorkTimePro-Sonderfall: feuert `FloatingTextRequested` statt `ExitHintRequested`.

### `ICalculationHistoryService` / `CalculationHistoryService`

Persistierte History pro Calculator in `{ApplicationData}/MeineApps/calculation_history/{id}.json`
(max 30 Items/Calculator). Verwendet von `RechnerPlus`, `HandwerkerRechner`, `FinanzRechner`,
`FitnessRechner`.

- **Live-Calculate-Stutter-Fix:** `ScheduleDebouncedSave(id, title, data, delayMs=2000)` statt
  `AddCalculationAsync` direkt — Timer-Debounce, nur das letzte Resultat pro Calculator wird
  geschrieben (jede Live-Iteration überschreibt den Snapshot).
- **Parallel-Load:** `GetAllHistoryAsync` liest alle JSON-Files parallel via `Task.WhenAll`.
- **Static `JsonSerializerOptions`** (`WriteIndented=false`): keine Allokation pro Save.
- Thread-safe via `SemaphoreSlim`; alle Fehler werden still verschluckt (History ist nicht kritisch).

### `IUnitConverterService` / `UnitConverterService`

Einheiten-Umrechnung Länge/Fläche/Volumen/Gewicht (Metrisch/Imperial). Verwendet von
`HandwerkerRechner`.

### `IFileShareService` / `DesktopFileShareService`

Datei-Sharing-Abstraktion. Desktop-Default kopiert in die Zwischenablage. Android-Apps
überschreiben via Factory in `App.axaml.cs` mit `AndroidFileShareService` (Linked File aus
[MeineApps.Core.Premium.Ava](../MeineApps.Core.Premium.Ava/CLAUDE.md)).

### `AtomicFileWriter` (Static)

Atomares Schreiben gegen Daten-Verlust bei Crash/Power-Loss: schreibt in `{path}.tmp`, dann
`File.Move(.tmp, target, overwrite: true)` — der Verzeichnis-Eintrag wird atomar ersetzt, die
Ziel-Datei bleibt bei Abbruch unverändert. Überladungen für Text (optional mit `Encoding`) und Bytes.

### `UriLauncher` (Static)

Plattformübergreifender Wrapper für URL-Öffnen und Text-Sharing.

```csharp
UriLauncher.OpenUri(url);            // Desktop: Process.Start (UseShellExecute) · Android: Intent.ActionView
UriLauncher.ShareText(text, title);  // Desktop: Clipboard · Android: Intent.ActionSend
```

Die Plattform-Hooks `App.PlatformOpenUri` / `App.PlatformShareText` werden in `MainActivity.cs`
(Android) auf die nativen Intents gesetzt.

## Async-Bausteine

### `AsyncDebouncer`

CTS-basiertes Debounce für asynchrone Aktionen (cancelt vorherige Trigger, führt erst nach
`delay` aus, reicht das Token an die Aktion durch). Thread-safe; `Pause()` liefert ein
`IDisposable`, das Trigger deterministisch unterdrückt (ersetzt fehleranfällige Suppress-Flags).
Verwenden statt selbstgebauter Timer-/Bool-Konstrukte.

```csharp
private readonly AsyncDebouncer _saveDebouncer = new(TimeSpan.FromMilliseconds(800));
_saveDebouncer.Trigger(async ct => await SaveAsync(value, ct));
using (_saveDebouncer.Pause()) { /* programmatische Änderungen ohne Save */ }
```

### `ForgetExtensions`

Fire-and-Forget mit einheitlicher Fehlerbehandlung — ersetzt das verstreute
`_ = X().ContinueWith(..., OnlyOnFaulted)`-Muster. Exceptions werden **nicht** verschluckt,
sondern via `onError` oder `Debug.WriteLine` (mit `[CallerMemberName]`) gemeldet. `RunForget`
fängt zusätzlich synchrone Würfe vor dem ersten `await`.

```csharp
SaveAsync().Forget();
SaveAsync().Forget(ex => MessageRequested?.Invoke("Fehler", ex.Message));
```

## Lokalisierung

### `ILocalizationService` / `LocalizationService`

ResourceManager-basiert; jede App injiziert ihren eigenen `ResourceManager` (`AppStrings.resx`,
6 Sprachen DE/EN/ES/FR/IT/PT). `Initialize()` lädt die gespeicherte Sprache bzw. erkennt die
Gerätesprache und persistiert sie sofort (sonst fallen Komponenten auf Englisch zurück).
`SetLanguage(code)` feuert das `LanguageChanged`-Event (`EventHandler`); der MainViewModel-Forwarder
ruft daraufhin `UpdateLocalizedTexts()` auf allen instanziierten VMs.

```csharp
var text = _localization.GetString("Key");
_localization.SetLanguage("en");   // → LanguageChanged → VMs aktualisieren ihre Strings
```

**Gotcha:** `GetString(key)` liefert bei fehlendem Key den **Key-Namen** zurück (nie null) —
`?? "fallback"` ist daher toter Code und die rohe Key-ID erscheint im UI. Optionale/dynamische
Keys explizit behandeln: `var v = GetString(key); return v == key ? fallback : v;`.

### `ILocalizable`

Marker-Interface (`void UpdateLocalizedTexts()`) für VMs mit lokalisierten String-Properties.
Der Forwarder kann so alle injizierten VMs iterieren und nur die instanziierten neu lokalisieren,
statt eine Ad-hoc-Liste im `LanguageChanged`-Handler zu pflegen.

### `TranslateExtension`

XAML-Markup-Extension für Inline-Lokalisierung. Selten verwendet (Texte werden meist über
VM-Properties gebunden).

```xml
<TextBlock Text="{loc:Translate Key=WelcomeMessage}" />
```

## ViewModels-Bausteine

- **`ViewModelBase`** — `abstract : ObservableObject`. Basis aller App-VMs (statt direkt von
  `ObservableObject`), Voraussetzung für den `ViewLocator.Match`.
- **`INavigationSource`** — `event Action<string>? NavigationRequested`. Marker statt
  Reflection-Event-Wiring im MainViewModel. Routen: `"Page?param=x"`, `".."` (zurück),
  `"../subpage"` (zum Parent, dann subpage).
- **`IMessageSource`** — `event Action<string,string>? MessageRequested` (Titel, Nachricht).

## ViewLocator

`IDataTemplate`, `Match` greift auf `ViewModelBase`. Auflösung per Namens-Konvention im **selben
Assembly** wie das VM, mit Typ-Cache (`ConcurrentDictionary`):

```
{App}.ViewModels.DashboardViewModel  →  {App}.Views.DashboardView
```

Ersetzt `.ViewModels.`→`.Views.` und `ViewModel`→`View`. Findet sich keine View, wird ein
`TextBlock` mit Diagnose-Text gerendert (Debug-Log über `Debug.WriteLine`). Als globale Resource
in `App.axaml` registrieren: `<local:ViewLocator />`.

> Mobile-Shell-Varianten (`…ViewMobile`-Fallback via `IsMobileShell`) sind **kein** Feature
> dieses ViewLocators — BingXBot hat dafür einen eigenen abgeleiteten ViewLocator in seinem
> Shared-Projekt.

## Design-Tokens (`ThemeColors.axaml`)

| Gruppe | Werte |
|--------|-------|
| Spacing | `SpacingSm: 8`, `SpacingMd: 12`, `SpacingLg: 16`, `SpacingXl: 24` |
| Radius | `RadiusSm: 4`, `RadiusMd: 8`, `RadiusLg: 12` |
| Typography | `FontSizeBodyMd: 14`, `FontSizeTitleLg: 22`, `FontSizeHeadlineMd: 28` |

App-spezifische Primary-Farben kommen aus `Themes/AppPalette.axaml` im jeweiligen Shared-Projekt
(statisch geladen, kein Theme-Wechsel). Alle `DynamicResource`-Keys bleiben app-übergreifend identisch.

## Converters

Alle Konverter sind `sealed` und bieten eine statische `Instance` für `{x:Static}`-Bindings.

| Konverter | Funktion |
|-----------|----------|
| `BoolToVisibilityConverter` | Bool → IsVisible |
| `InverseBoolConverter` | `!Bool` |
| `BoolToOpacityConverter` | Bool → Opacity (Default `TrueOpacity=1.0`, `FalseOpacity=0.4`, konfigurierbar) |
| `BoolToBrushConverter` | Bool → Brush |
| `BoolToStringConverter` | Bool → String (parametrisierbar) |
| `NumberFormatConverter` | Double → formatierte Zahl |
| `CurrencyConverter` | Decimal → Währungs-String |
| `PercentageConverter` | Double → Prozent-String |
| `IsPositiveConverter` | Zahl → Bool (>0) |
| `DateTimeFormatConverter` | DateTime → formatiertes Datum |
| `RelativeTimeConverter` | DateTime → Kurzformat ("2 h", UTC-basiert) |
| `TimeSpanFormatConverter` | TimeSpan → formatierte Dauer |
| `DurationConverter` | Dauer-Formatierung |
| `StringFormatConverter` | String.Format mit Parameter |
| `StringTruncateConverter` | langer Text → gekürzt mit Ellipse |
| `StringIsNullOrEmptyConverter` / `StringIsNotNullOrEmptyConverter` | String → Bool |
| `StringToColorBrushConverter` | "#RRGGBB" → SolidColorBrush (Gray-Fallback) |
| `StringToColorConverter` | "#RRGGBB" → Color (Gray-Fallback, für `SolidColorBrush.Color`-Bindings mit Opacity) |
| `ColorToBrushConverter` | Color → SolidColorBrush (VM exponiert Value-Type Color statt Brush) |

## Behaviors

Behaviors leben in `MeineApps.UI.Behaviors` (kanonische Quelle), nicht hier.
XAML-Import: `xmlns:behaviors="using:MeineApps.UI.Behaviors"`.

## Framework-Fallstricke (Avalonia 12 / MVVM / Daten)

Generische Stolperfallen, die alle Apps auf dieser Library betreffen. (Rendering-/SkiaSharp-
spezifisches → [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md), Monetarisierung →
[MeineApps.Core.Premium.Ava](../MeineApps.Core.Premium.Ava/CLAUDE.md), App-Logik → App-CLAUDE.md.)

### MVVM & Bindings

- **`CommandParameter` ist in XAML immer `string`.** `CommandParameter="0"` übergibt `"0"`, nicht
  `int 0` → `RelayCommand<int>.CanExecute()` wirft `ArgumentException` beim View-Attach. Fix:
  VM-Methode auf `string` + `int.TryParse()`, oder `<sys:Int32>0</sys:Int32>` im XAML.
- **Value-Precedence: Code-Behind-Setter verdrängt ein Binding dauerhaft.** `control.X = …`
  (`SetValue(LocalValue)`) belegt denselben Slot wie ein `{Binding}` ohne explizite Priorität und
  disposed die Binding-Subscription (anders als WPF). Eine Property nur an **einer** Stelle
  steuern — für Overlay-Hit-Test ein vollständiges Aggregat-Binding im XAML
  (`IsHitTestVisible="{Binding !IsAnyOverlayOpen}"`), keinen parallelen Code-Behind-Setter.
- **Compiled Bindings lösen verschachtelte DataContext-/`x:DataType`-Ebenen korrekt auf** (äußeres
  Element setzt `DataContext`, inneres Kind setzt `x:DataType`) — sowohl explizit als auch geerbt.
  Bei leeren Bindings die Ursache woanders suchen (fehlende RESX-Keys, null-VM).
- **Enum-Werte nie direkt in XAML binden** — lokalisierte Display-Property im VM (`GetString()`).

### XAML / Styles / Animationen

- **CSS `translate()` braucht px-Einheiten:** `translate(0px, 400px)`, sonst FormatException.
- **Style-Selector mit `#Name` braucht Typ-Prefix:** `Grid#ModeSelector`, nicht `#ModeSelector` (AVLN2200).
- **KeyFrame-Animationen: kein `RenderTransform`** — nur `Opacity`/`Width`/`Height` (double) in
  KeyFrames. Für animierte Transforms `TransformOperationsTransition` in `Transitions` verwenden.
- **`€` direkt schreiben** (oder `&#x20AC;`) — XAML interpretiert C#-Unicode-Escapes (`€`) nicht.
- **`IsAttachedToVisualTree` entfernt** → `using Avalonia.VisualTree;` + `control.GetVisualRoot() != null`.
- **`ScrollViewer`-`Padding` verhindert Scrollen** — stattdessen `Margin` aufs Kind-Element +
  `VerticalScrollBarVisibility="Auto"`.
- **`ZIndex` greift auf Android nicht fürs Hit-Testing** — Touch geht durch Overlays hindurch.
  Content-Swap (normalen Content `IsVisible=false`, Overlay als Ersatz) statt ZIndex-Overlay.
- **Material-Icons unsichtbar**, wenn `<materialIcons:MaterialIconStyles />` (+ xmlns) nicht in
  `App.axaml` registriert ist.

### Threading & Lifecycle

- **VM-Objektgraph nie auf Background-Thread instanziieren.** `Task.Run(() => GetRequiredService
  <MainViewModel>())` erzeugt UI-Objekte (Brushes) mit falscher Thread-Affinität → Crash beim
  ersten Render (`The calling thread cannot access this object`). Fix:
  `Dispatcher.UIThread.InvokeAsync(() => GetRequiredService<MainViewModel>()).GetTask()`; schwere
  Background-Arbeit (Shader/Asset-Preload) bleibt auf `Task.Run`.
- **Startup-Last bei vielen Child-VMs:** spät freigeschaltete VMs als `Lazy<T>` injizieren
  (`AddLazyResolution()`), in `EnsureXxxVm()` beim ersten Navigations-Ziel instanziieren +
  verdrahten. Public Property `XxxViewModel?` (`[ObservableProperty]`) feuert dann das Binding;
  `LanguageChanged`-Handler null-safe (`Xxx?.UpdateLocalizedTexts()`).
- **Fade-Flimmern bei View-/Tab-Wechsel:** Opacity=0 muss **vor** dem Binding-Update gesetzt
  werden — `PageTransitionStarting`/`OnActivePageChanging()` feuert vor dem Wertwechsel.
- **`OnFrameworkInitializationCompleted` läuft auf Android VOR `MainActivity.OnCreate`.** In
  Avalonia 12 ruft `AvaloniaAndroidApplication.OnCreate` (Application-Ebene) via
  `SetupWithLifetime` → `OnFrameworkInitializationCompleted` auf — also bevor `MainActivity.OnCreate`
  die Platform-Factories (`App.XxxServiceFactory`) setzt. Zwei Konsequenzen für den Composition Root:
  (1) **Platform-Services lazy registrieren** — Factory-Prüfung im Resolve-Lambda
  (`AddSingleton<IFoo>(sp => XxxFactory != null ? XxxFactory(sp) : new Fallback())`), **nie** als
  Build-Zeit-`if (XxxFactory != null)` (sonst wird beim `BuildServiceProvider` der Mock/Fallback
  fest eingebrannt, weil die Factory dann noch null ist). (2) **MainViewModel nicht sofort in
  `OnFrameworkInitializationCompleted` auflösen** — sonst friert der ganze Objektgraph die
  Fallbacks ein. Auf Android das `IActivityApplicationLifetime.MainViewFactory`-Lambda nutzen und
  das MainViewModel **darin** auflösen: `AvaloniaActivity` ruft die Factory aus
  `InitializeAvaloniaView` (in `MainActivity.OnCreate.base`) auf, also deterministisch NACH der
  Factory-Setzung. (`ISingleViewApplicationLifetime.MainView` ist auf Android laut Avalonia nur
  eingeschränkt unterstützt; ein `Dispatcher.UIThread.Post`-Resolve ist NICHT zuverlässig, weil
  der Post je nach Looper-Timing vor `MainActivity.OnCreate` laufen kann.) Branch-Reihenfolge:
  `IActivityApplicationLifetime` VOR `ISingleViewApplicationLifetime` prüfen (das Android-Lifetime
  implementiert beide). HandwerkerImperium umgeht das Problem implizit über seine Loading-Pipeline.
  Symptom bei Missachtung: Mock-Services laufen auf echter Hardware (z.B. SmartMeasure zog den
  `MockArCaptureService` statt ARCore → 10 simulierte Punkte ohne Kamera).

### Daten & Persistenz

- **`DateTime`:** Persistenz immer `DateTime.UtcNow` + `"O"`-Format + `RoundtripKind` beim Parsen
  (sonst UTC→Lokal-Konvertierung → Timer/Timestamps verschieben sich).
- **sqlite-net `InsertAsync()` gibt den Zeilen-Count zurück (immer 1), nicht die Auto-Increment-ID.**
  Die ID wird direkt aufs Objekt gesetzt → `entity.Id` danach verwenden, nie
  `entity.Id = await db.InsertAsync(entity)`.
- **Fire-and-Forget async → Race:** `_ = InitializeAsync()` mit `_list.Clear()` kollidiert mit
  User-Aktionen (Daten erscheinen kurz, verschwinden). Task speichern: `_initTask = …`, in
  Methoden `await _initTask`. Für reines Forget mit Logging `ForgetExtensions` nutzen.
- **`JsonSerializer.Serialize` auf Background-Thread** kann mit laufender State-Mutation
  kollidieren (Collection-Crash). State ist klein → auf dem UI-Thread serialisieren, oder vorher
  DeepCopy. Für crash-sicheres Schreiben `AtomicFileWriter` verwenden.
- **Erkannte Gerätesprache in Preferences persistieren** (`LocalizationService.Initialize()`),
  sonst fallen andere Komponenten auf Englisch zurück.
- **Assembly-Version ist sonst 1.0.0** — wenn die Version zur Laufzeit ausgelesen wird,
  `<Version>X.Y.Z</Version>` in der Shared-`.csproj` setzen.

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Architektur, Conventions, App-Portfolio (Farbpaletten)
- [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) — Custom Controls, Behaviors, Skia-Helpers, Rendering-Gotchas
- [MeineApps.Core.Premium.Ava](../MeineApps.Core.Premium.Ava/CLAUDE.md) — `AndroidFileShareService` (Linked File), Monetarisierung
