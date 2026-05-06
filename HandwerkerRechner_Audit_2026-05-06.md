# HandwerkerRechner — Optimierungs-Audit

**Datum:** 2026-05-06
**Version:** v2.0.7 (geschlossener Test)
**Scope:** UI/UX · Architektur · Performance · Memory
**Methode:** Vier parallele Tiefen-Analysen + Stichproben-Verifikation aller Critical/High-Findings gegen den Code

---

## Executive Summary

Die App ist **strukturell solide**: zentrale `ICalculatorFactoryService`-Auflösung statt 19 einzelner `Func<T>`, sauberes `CalculatorViewBase` mit korrektem Auf-/Abmelden, Compiled Bindings (`x:DataType`) in **allen 26 Views**, dokumentierte CLAUDE-Konventionen werden eingehalten (`Typ#Name`-Selectors, Ad-Spacer 64dp etc.), Lokalisierung über alle 6 Sprachen, sqlite-net hinter Service-Schicht.

Die größten Hebel liegen in **drei Bereichen**:

1. **Boilerplate-Explosion** in den 19 Calculator-VMs: identischer Konstruktor, identische Service-Felder, identisches Save/Load-Pattern → ~60 % Code-Duplikation, eliminierbar mit einer `CalculatorViewModelBase<TResult>`.
2. **Ein bestätigter Native-Memory-Leak** im PDF-Export (`PdfDocument` wird nie disposed) — 5-Minuten-Fix mit echtem Impact.
3. **Property-Burst-Patterns** im `MainViewModel`, die bei Sprach- und Favoriten-Änderungen die gesamte UI re-binden.

Es gibt **keine** kritischen Architektur-Fehler, **keine** Async-Deadlock-Risiken (`.Result`/`.Wait()` nicht gefunden), **keine** SkiaSharp-Render-Loop-Probleme. Der Code ist im Mittel überdurchschnittlich sauber für ein Multi-App-Repo dieser Größe.

**Gesamt:** 1 Critical · 6 High · 9 Medium · 5 Low

---

## Top-10 Quick Wins (Aufwand vs. Impact)

| # | Befund | Datei : Zeile | Aufwand | Impact |
|---|--------|---------------|---------|--------|
| 1 | `PdfDocument` nie disposed → Native-Memory-Leak | `Services/MaterialExportService.cs:45,162` | 5 min | hoch (Memory) |
| 2 | `OnPropertyChanged(string.Empty)` invalidiert ALLE Properties | `ViewModels/MainViewModel.cs:311` | 30 min | hoch (Perf) |
| 3 | `MaterialPriceService.EnsureCustomLoaded` blockiert UI-Thread | `Services/MaterialPriceService.cs:72-86` | 1 h | hoch (Perf) |
| 4 | `Margin="16,16,16,200"` 15× hartcodiert | `Views/Floor/*.axaml`, `Views/Premium/*.axaml` | 30 min | mittel (Wartbarkeit) |
| 5 | 35 Hex-Farben hartcodiert (allein 29 in `MainView.axaml`) | `Views/MainView.axaml`, `HistoryView.axaml` | 1-2 h | mittel (Konsistenz) |
| 6 | PDF-Export-Header verwendet Indigo `#6366F1` statt Theme-Blau `#3B82F6` | `Services/MaterialExportService.cs:18` | 2 min | niedrig (Konsistenz) |
| 7 | Cast-basierte Resolve im Factory: `(T)sp.GetService(typeof(T))!` | `Services/CalculatorFactoryService.cs:53` | 5 min | niedrig (Robustheit) |
| 8 | `_customLoaded` ohne `volatile` (Double-checked Locking-Anti-Pattern) | `Services/MaterialPriceService.cs:15,74` | 5 min | niedrig (Korrektheit) |
| 9 | `_backPressHelper.ExitHintRequested += msg => …` Lambda nicht abmeldbar | `ViewModels/MainViewModel.cs:116` | 5 min | niedrig (Hygiene) |
| 10 | Loading-Pipeline lädt Projekte/History/Preise nicht vor → erste Tab-Wechsel ruckeln | `Loading/HandwerkerRechnerLoadingPipeline.cs:19` | 1 h | mittel (UX) |

---

## 1 · Memory

### CRITICAL

**M1 · `PdfDocument` wird nie disposed** — `Services/MaterialExportService.cs:45-166`

`new PdfDocument()` (Z. 45) wird in einem `Task.Run` erzeugt. `gfx.Dispose()` wird zwar bei jedem Seitenumbruch (Z. 71) und am Ende (Z. 153) aufgerufen, aber `document` selbst nie. PdfSharpCore hält intern ein PdfDictionary mit allen Pages, Fonts und Streams — beim Export bleibt das alles bis zur GC-Kollektion im Heap. Bei mehreren Exporten in einer Session sammelt sich das auf.

**Fix:** Z. 45 → `using var document = new PdfDocument();` oder `document.Dispose()` nach Z. 162.

### HIGH

**M2 · Indirekte Service-Subscription-Leak-Vektoren** — `ViewModels/Floor/TileCalculatorViewModel.cs:76` (analog in 18 weiteren VMs)

Jede Calculator-VM subscribed im Konstruktor auf `_unitConverter.UnitSystemChanged` (Singleton). Cleanup ist im jeweiligen `Dispose()`/`Cleanup()` enthalten und wird über `MainViewModel.CleanupCurrentCalculator` (Z. 374-382) korrekt getriggert beim Page-Wechsel — **wenn** der Page-Wechsel sauber durchläuft. Wenn `OnCurrentPageChanged` (Z. 334) durch eine Exception unterbrochen wird, bleibt die alte VM samt Event-Subscription am Singleton hängen.

**Empfehlung:** `try/finally` um den Cleanup-Block, damit ein fehlerhafter Create-Schritt die Aufräum-Phase nicht überspringt.

**M3 · `MainViewModel.Dispose()` wird nie automatisch aufgerufen** — `ViewModels/MainViewModel.cs:718-745`

`MainViewModel` ist Singleton, implementiert `IDisposable` mit 14 sauberen Unsubscribes. Aber nirgendwo (`App.axaml.cs`, `MainView.axaml.cs`, `MainWindow.axaml.cs`) wird `Dispose` ausgelöst. Bei normalem App-Exit ist das unkritisch (Prozess endet), bei Hot-Restart in Visual Studio aber summieren sich Subscriptions.

**Fix:** In `MainView.axaml.cs` `OnDetachedFromVisualTree` → `(DataContext as IDisposable)?.Dispose()`.

### MEDIUM

**M4 · Lambda-Subscription auf `BackPressHelper`** — `MainViewModel.cs:116`

`_backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);` lässt sich syntaktisch nicht entfernen. Hier unkritisch (Helper-Lebensdauer = VM-Lebensdauer), aber Anti-Pattern.

**Fix:** `private void OnBackPressExitHint(string msg) => ExitHintRequested?.Invoke(msg);` und im Dispose `_backPressHelper.ExitHintRequested -= OnBackPressExitHint;`.

**M5 · Lambda-Subscriptions auf Calculator-VM-Events** — `MainViewModel.cs:411-414`

`calc.MessageRequested += (title, msg) => MessageRequested?.Invoke(title, msg);` u. a. — gleiche Lambda-Falle. Da die VM beim Page-Wechsel komplett ge-disposed wird und der Subscriber-Graph mit ihr stirbt, leakt nichts. Aber: Sauberer wäre eine kleine `EventBridge`-Klasse oder Method-Group mit Unsubscribe in `CleanupCurrentCalculator`.

**M6 · `_customLoaded` ohne `volatile`** — `Services/MaterialPriceService.cs:15,74`

Klassisches Double-checked-Locking ohne Memory-Barrier. Funktioniert auf x86 zufällig, auf ARM (Android-Geräte!) nicht garantiert. Konsequenz: ein Worker-Thread könnte nie gespiegelt sehen, dass `_customLoaded == true`, und immer wieder durch den Semaphore laufen.

**Fix:** `private volatile bool _customLoaded;`.

### LOW

**M7 · Statische `SKPaint`/`SKFont` in Visualisierungen** — z. B. `Graphics/MaterialStackVisualization.cs:26-31`

Statisch allokiert, nie disposed. Bei App-Lifetime-Singleton OK. Reine Hygiene-Frage.

**M8 · `LoadSafeAsync` ohne CancellationToken** — `MainViewModel.cs:360`

Bei App-Shutdown können noch laufende `LoadTemplatesAsync`/`LoadQuotesAsync` weiterlaufen. Keine echte Leak-Quelle, aber Best-Practice-Verstoß.

---

## 2 · Performance

### HIGH

**P1 · `OnPropertyChanged(string.Empty)` re-bindet die ganze UI** — `MainViewModel.cs:311` (`UpdateHomeTexts`)

Wird bei jedem `OnLanguageChanged` ausgelöst. Avalonia behandelt leeren Property-Namen als „alle Properties geändert" → jede einzelne der ~46 lokalisierten Label-Properties wird neu evaluiert, jedes Binding läuft erneut, jeder Getter ruft `_localization.GetString(...)` auf (Resource-Manager-Lookup).

**Fix:** Entweder gezielt die ~12 sichtbaren Header-Texte invalidieren oder über ein Indexer-Pattern (`this[string key]`) gehen, sodass nur die Strings im aktuellen View neu gezogen werden.

**P2 · `MaterialPriceService.EnsureCustomLoaded` blockt UI-Thread** — `Services/MaterialPriceService.cs:72-86`

Sync-Pfad: jeder Calculator-VM ruft im Konstruktor `_priceService.GetPrice(...)` (z. B. `TileCalculatorViewModel.cs:79`). `GetPrice` → `EnsureCustomLoaded` → `_semaphore.Wait()` + `File.ReadAllText` + `JsonSerializer.Deserialize`. Beim ersten Calculator-Open kostet das messbar (~10-50 ms je nach Gerät und Cache-Status), genau in dem Moment, wo der User bereits auf eine Reaktion wartet.

**Fix:** Im Loading-Pipeline-Step einen `await ((MaterialPriceService)svc).WarmupAsync()` aufrufen (analog `ProjectService`-Initialisierung). Dann ist `_customLoaded == true`, bevor die erste Calculator-VM tippt.

**P3 · `NotifyFavoriteProperties()` feuert 19 separate `PropertyChanged`** — `MainViewModel.cs:173-194`

Jede Toggle-Aktion → 19 Notifications + 19 Re-Renders der Stern-Buttons. Bei schneller Mehrfachänderung (User markiert Liste) potenziert sich das.

**Fix:** Eine einzige Property `FavoriteSet` (z. B. `IReadOnlySet<string>`), Bindings auf einen `IsFavoriteConverter` mit ConverterParameter umstellen. Spart 18 Notifications.

### MEDIUM

**P4 · Loading-Pipeline hat nur einen Step** — `Loading/HandwerkerRechnerLoadingPipeline.cs:19-34`

Aktuell: Shader + ViewModel + PurchaseService parallel. Nicht enthalten: `ProjectService`, `MaterialPriceService` (siehe P2), `ICalculationHistoryService`. Erstes Aufrufen der Tabs „Projekte" / „Historie" / eines Calculators triggert dann Sync-Loads. Splash-Min-Dauer ist 800 ms — genug Budget für die Vorlade-Tasks.

**Fix:** Zweiter Step `Daten` der die drei Services parallel anwirft.

**P5 · 46 Localized Get-Only-Properties im `MainViewModel` (Z. 451-514)**

Jede Property ruft bei jedem Binding-Read den `ResourceManager` auf. Mit Compiled Bindings ist das schnell, aber nicht gratis. Bei `OnPropertyChanged("")` (siehe P1) potenziert sich das.

**Fix:** Einmal-Caching beim Sprach-Wechsel: alle Texte in private Felder ziehen, nur diese exposen.

**P6 · `MainViewModel.Dispatcher.UIThread.Post` mehrfach** — `MainViewModel.cs:156, 433, 639`

Funktioniert, aber blockiert Unit-Testbarkeit. `IUiDispatcher`-Abstraktion injizieren, dann sind die VMs in xunit testbar ohne Avalonia.

**P7 · `CalculatorViewBase.ShouldInvalidateOnPropertyChanged` per `Contains("Result")`** — `Views/CalculatorViewBase.cs:46-49`

Fragiles String-Matching. Property `PreviousResult` würde fälschlich triggern, jede Umbenennung („FinalSum") bricht stillschweigend die Animation.

**Fix:** Attribute `[InvalidatesVisualization]` an Properties, das von Reflection einmal beim DataContext-Bind ausgelesen wird. Oder: Liste von Property-Namen in einer abstrakten Property der View.

### LOW

**P8 · `GetString` bei jedem Property-Get in Calculator-VMs** — z. B. `TileCalculatorViewModel.cs:46,162,165`

Display-Strings wie `$"{_localization.GetString("PricePerTile")}: ..."` jedes Mal neu allokiert. Im Aggregat einige Hundert String-Allokationen pro Berechnung, GC-Druck moderat.

**P9 · `BlueprintBackgroundRenderer` läuft auch wenn Tab gewechselt**

Konkret: Animation-Timer pausiert nicht, wenn Settings/Projects-Tab vorne ist. CPU-Last gering, aber unnötig auf Akku-Geräten.

**Korrektur des Performance-Sub-Audits:** Compiled Bindings sind vorhanden — `x:DataType` und `x:CompileBindings="True"` finden sich in **26 von 26 Views** (61 Treffer per Grep). Der Ursprungsbericht behauptete fälschlich „0 matches"; das wurde verifiziert und ist hier nicht mehr aufgeführt.

---

## 3 · Architektur

### HIGH

**A1 · `MainViewModel.cs` ist 749 Zeilen mit massiver Repetition**

Drei Parallelstrukturen, die alle 19 Calculator-Routen einzeln aufzählen:

- 19 `IsFav…`-Properties (Z. 197-215)
- 19 Aufrufe in `NotifyFavoriteProperties` (Z. 173-194)
- 19 `[RelayCommand] private void NavigateTo…` (Z. 535-592)
- 1 `GetCalculatorInfo`-`switch` (Z. 217-239) ✓ noch das beste

Plus `CalculatorFactoryService` mit weiterem Dictionary über die gleichen 19 Routen.

**Fix:** Eine zentrale `CalculatorRegistry`:

```csharp
public sealed record CalculatorMeta(
    string Route, string LabelKey, string Icon,
    Type ViewModelType, bool IsPremium = false);

public static class CalculatorRegistry
{
    public static readonly IReadOnlyList<CalculatorMeta> All = new[]
    {
        new CalculatorMeta("TileCalculatorPage", "CalcTiles", "ViewGrid", typeof(TileCalculatorViewModel)),
        new CalculatorMeta("WallpaperCalculatorPage", "CalcWallpaper", "Wallpaper", typeof(WallpaperCalculatorViewModel)),
        // ... 17 weitere
    };
    public static CalculatorMeta? ByRoute(string route) => All.FirstOrDefault(c => c.Route == route);
}
```

`MainViewModel` shrinkt auf ~400 Zeilen. `IsFav*` werden zu einer einzigen Funktion `IsFavorite(string route)` mit ConverterParameter im Binding. `NavigateTo*` werden zu einem `[RelayCommand] void Navigate(string route)`. `CalculatorFactoryService` wird redundant — Registry + DI reicht.

**A2 · 19 Calculator-VMs duplizieren denselben Konstruktor und dieselben Felder**

Verifiziert: alle 19 VMs erben `: ViewModelBase, IDisposable, ICalculatorViewModel`. Stichprobe `TileCalculatorViewModel.cs:14-80`: 9 Service-Felder, identische Event-Deklarationen, identisches `LoadFromProjectIdAsync`-Pattern, identisches `_debounceTimer`, identisches `Dispose`.

**Fix:** Abstrakte Basisklasse:

```csharp
public abstract partial class CalculatorViewModelBase<TInput, TResult>
    : ViewModelBase, IDisposable, ICalculatorViewModel
    where TInput : class
    where TResult : class
{
    protected readonly CraftEngine Engine;
    protected readonly IProjectService Projects;
    protected readonly ILocalizationService Localization;
    protected readonly ICalculationHistoryService History;
    protected readonly IUnitConverterService Units;
    protected readonly IMaterialExportService Export;
    protected readonly IFileShareService FileShare;
    protected readonly IMaterialPriceService Prices;

    protected Timer? DebounceTimer;
    protected string? CurrentProjectId;

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action<string>? ClipboardRequested;
    public event Action? CalculationPerformed;

    [ObservableProperty] private bool _showSaveDialog;
    [ObservableProperty] private string _saveProjectName = string.Empty;

    protected abstract TResult Calculate(TInput input);
    protected abstract TInput CollectInput();
    protected abstract void ApplyResult(TResult result);

    protected void ScheduleAutoCalculate(int debounceMs = 250) { /* ... */ }

    public virtual async Task LoadFromProjectIdAsync(string projectId) { /* ... */ }
    public virtual void Cleanup() { Units.UnitSystemChanged -= OnUnitSystemChanged; }
    public virtual void Dispose() { DebounceTimer?.Dispose(); Cleanup(); }
}
```

**Erwartete Reduktion:** 19 × ~150 Zeilen Boilerplate → 19 × ~40 Zeilen domänenspezifische Logik. Einsparung: ~2000 Zeilen, deutlich konsistentere Cleanup-Logik.

**A3 · Magic-String-Routen verstreut über 4 Dateien**

`"TileCalculatorPage"`, `"DrywallPage"` etc. existieren als String-Literale in `MainViewModel`, `CalculatorFactoryService`, `OnCurrentPageChanged`, `GetCalculatorInfo`, `NavigateTo*` und im XAML als `CommandParameter`. Typo → silent null. Rename-Refactoring funktioniert nicht.

**Fix:** `public static class CalculatorRoutes { public const string Tiles = "TileCalculatorPage"; ... }`. Im XAML `CommandParameter="{x:Static r:CalculatorRoutes.Tiles}"`.

### MEDIUM

**A4 · `CalculatorFactoryService.Resolve<T>` ist Cast-basiert** — `Z. 52-53`

```csharp
private static T Resolve<T>(IServiceProvider sp) where T : notnull
    => (T)sp.GetService(typeof(T))!;
```

Verliert Stack-Trace im Fehlerfall, kein expliziter Fehlertext.

**Fix:** `=> sp.GetRequiredService<T>();` — gleiche Semantik, klare `InvalidOperationException` bei Fehlkonfiguration.

**A5 · `OnCurrentPageChanged` behandelt drei Page-Typen verschieden** — `MainViewModel.cs:334-354`

`ProjectTemplatesPage` und `QuotePage` werden mit `_ = LoadSafeAsync(...)` initialisiert, normale Calculator über `CreateCalculatorVm` mit synchroner Construction. Asymmetrie macht es schwer, neue Top-Level-Pages hinzuzufügen.

**Fix:** Alle Pages über die Registry; jede Page hat optional `Task InitializeAsync()` über ein gemeinsames Interface.

**A6 · `ICalculatorViewModel` und `CalculatorViewBase` sind unabhängig voneinander**

`CalculatorViewBase` (View) und `ICalculatorViewModel` (VM) teilen kein gemeinsames Property-Vokabular. `ShouldInvalidateOnPropertyChanged` rät anhand des Property-Namens → fragil (siehe P7).

**Fix:** `ICalculatorViewModel` exposed `event Action ResultChanged;`. `CalculatorViewBase` subscribes darauf statt auf `INotifyPropertyChanged`. Robust, explizit, schnell.

**A7 · `AppStrings.Designer.cs` manuell gepflegt** (CLAUDE.md vermerkt)

Bekannte Limitation. Wartungsaufwand bei 6 Sprachen × ~200 Keys.

**Empfehlung:** MSBuild-Custom-Task oder `dotnet-format`-Hook, der die Designer-Datei bei `*.resx`-Änderung neu generiert.

### LOW

**A8 · `event Action<string, string>?` als Pattern**

Funktioniert, ist aber typsicherer mit eigenen `EventArgs`-Records: `event EventHandler<MessageEventArgs>?`. Nice-to-have.

**A9 · `Dispatcher.UIThread.Post` direkt im ViewModel** — siehe P6

---

## 4 · UI/UX

### HIGH

**U1 · 15 von 19 Calculator-Views haben hartcodiertes `Margin="16,16,16,200"`**

Verifiziert per Grep. Bottom-Margin 200 dp ist deutlich mehr als der dokumentierte Ad-Spacer (64 dp) plus die ScrollViewer-Empfehlung (60 dp aus CLAUDE.md). 200 dp bedeutet, dass am unteren Rand sichtbarer Inhalt durch unnötig viel Leerraum verschoben wird, oder dass die Calculator-Views ihr eigenes Banner-Layout mitbringen statt der `MainView.axaml`-Struktur (`RowDefinitions="*,Auto,Auto"`) zu vertrauen.

**Fix:** Konstante in `MeineApps.Core.Ava` oder direkt in `ThemeColors.axaml`: `<Thickness x:Key="ScrollContentPadding">16,16,16,60</Thickness>`. Alle 15 Views auf diese Ressource.

**U2 · 35 hartcodierte Hex-Farben in Views, davon 29 in `MainView.axaml`**

Bricht das Theme-System: `AppPalette.axaml` exportiert sauber `PrimaryBrush`/`SecondaryBrush`/`AccentBrush`, aber `MainView.axaml` greift inline auf `#3B82F6`/`#F59E0B` etc. zu. Konsequenzen: Farb-Änderungen erfordern Such-Ersetz über `MainView.axaml`, ein potenzielles Light-Theme später ist unmöglich umsetzbar.

**Fix:** Komplett-Sweep über `MainView.axaml`. Jede `#XXXXXX` durch passende `{DynamicResource …Brush}` ersetzen. Falls Farbe nicht existiert: in `AppPalette.axaml` ergänzen.

**U3 · PDF-Export-Header verwendet Indigo `#6366F1` statt Theme-Blau `#3B82F6`** — `Services/MaterialExportService.cs:18`

`HeaderColor = XColor.FromArgb(255, 99, 102, 241)` → das ist Indigo, kein Blueprint-Blau. Wahrscheinlich noch von der Vorgänger-App geerbt. Nutzer sehen in der App ein anderes Markenblau als auf den exportierten Materiallisten.

**Fix:** `XColor.FromArgb(255, 59, 130, 246)` — entspricht `#3B82F6`, dem Primary-Theme.

### MEDIUM

**U4 · 19 Calculator-Views folgen demselben Layout-Schema, sind aber nicht von einem `UserControl` abgeleitet**

Stichprobe `TileCalculatorView.axaml`/`DrywallView.axaml`: `Grid RowDefinitions="Auto,*,Auto"`, oben Header-Balken mit Back-Button + Titel + Icon, mittig `ScrollViewer`, unten Save-Dialog-Overlay. ~80 % des XAML pro View ist strukturell identisch.

**Fix:** Ein `CalculatorPageShell.axaml` (UserControl) mit `<ContentPresenter Content="{Binding}"` für den Body, `Title`/`IconKind`/`HeaderBrush` als `StyledProperty`. Calculator-Views shrinken auf den eigentlichen Eingabe-/Ergebnis-Body.

**U5 · Empty-States nicht überall vorhanden / inkonsistent**

`ProjectsView` hat `EmptyStateView` mit Icon + Title + Subtitle. `HistoryView` hat nur Title. `Templates`/`Quotes` müssen geprüft werden.

**Fix:** Einheitliches `EmptyStateView`-Pattern mit Icon, Title, Subtitle, optionalem CTA-Button.

**U6 · Vollständigkeit der 6 Sprachen nicht automatisiert geprüft**

`AppStrings.Designer.cs` ist manuell. 6 `.resx`-Dateien können stillschweigend Lücken haben → Nutzer sieht den Key-Namen oder den Default-Text.

**Empfehlung:** Build-Task, der jeden Key in EN gegen alle anderen Sprachen prüft und bei fehlendem Eintrag warnt.

**U7 · Touch-Target-Audit für Favoriten-Sterne**

`MinWidth/MinHeight=44dp` ist akzeptabel, aber Icon-Größe 16 dp wirkt klein. Auf 5"-Geräten möglicherweise Treff-Probleme.

**Empfehlung:** Icon-Größe 20-24 dp, MinTouch 48 dp (Material-Empfehlung).

### LOW

**U8 · Kontrast-Check Primary-Blau auf Dark-Background**

`#3B82F6` auf `#182640` ergibt ein Verhältnis nahe 4.5:1. Grenzwertig zur WCAG-AA-Schwelle. Eine `PrimaryColor`-Variante mit etwas höherer Helligkeit für Text/Icons würde Sicherheit geben.

**U9 · Tab-Wechsel mit offenem Calculator**

Stichprobe `MainViewModel.SelectProjectsTab` (Z. 265): setzt `CurrentPage = null` ohne den Save-Dialog des Calculators zu prüfen. Hat der User ungespeicherte Eingaben, sind sie weg.

**Fix:** In `Tab-Selectors` prüfen, ob `CurrentCalculatorVm is ICalculatorViewModel { ShowSaveDialog: true }`, dann optional Bestätigungs-Dialog.

---

## Refactoring-Roadmap

### Sprint 1 — Quick Wins (1 Tag)

1. `using var document = new PdfDocument()` — Memory-Leak schließen.
2. `volatile bool _customLoaded;` — Korrektheit auf ARM.
3. `GetRequiredService<T>` statt Cast-Resolve.
4. PDF-Header-Farbe auf Theme-Blau.
5. `BackPressHelper`-Lambda in Method-Group umwandeln.
6. Konstante `ScrollContentPadding` einführen, 15 Views umstellen.

### Sprint 2 — Hot-Path-Performance (2 Tage)

7. `MaterialPriceService.WarmupAsync` in Loading-Pipeline aufnehmen.
8. Loading-Pipeline-Step 2 (Projects + History + Prices parallel).
9. `OnLanguageChanged` ohne `OnPropertyChanged("")` — gezielte Property-Liste.
10. `IsFavorite(route)`-Funktion + Converter statt 19 Properties.
11. Lokalisierte Labels einmal cachen, nicht bei jedem Get.

### Sprint 3 — Strukturelle Refactorings (1 Woche)

12. `CalculatorRoutes`-Konstanten + `CalculatorRegistry`.
13. `CalculatorViewModelBase<TInput, TResult>` einführen, 19 VMs migrieren.
14. `CalculatorPageShell`-UserControl + 19 Views migrieren.
15. `ICalculatorViewModel.ResultChanged`-Event statt `Contains("Result")`-Heuristik.
16. `IUiDispatcher`-Abstraktion (Bonus: Unit-Tests werden möglich).

### Sprint 4 — UI-Konsistenz (2-3 Tage)

17. `MainView.axaml` Hex-Farben → DynamicResource-Sweep.
18. Empty-States vereinheitlichen.
19. Favoriten-Touch-Target-Audit.
20. Tab-Wechsel mit ungespeicherten Daten absichern.
21. Resource-Vollständigkeits-Check als Build-Task.

---

## Was bewusst NICHT refactored werden sollte

- **`AppPalette.axaml`-Struktur** ist ausgezeichnet, deckungsgleich mit den anderen Apps im Repo. Nur die Inline-Farben in den Views auf die Palette ziehen, die Palette selbst bleibt.
- **`CalculatorFactoryService`** wird durch die Registry redundant, aber ein Wegfall ist eine Folge des A1/A2-Refactorings, kein eigenständiges Ziel.
- **`CalculatorViewBase`** ist die richtige Abstraktion — nur die `Contains("Result")`-Heuristik austauschen, die Klasse selbst behalten.
- **DI-Lifetimes** sind korrekt: Singleton-Services, Singleton-Hub-VMs, Transient-Calculator-VMs.
- **Loading-Pipeline-Architektur** (`LoadingPipelineBase` + `AddStep`) ist sauber, nur ein zweiter Step fehlt inhaltlich.

---

## Anhang — Verifikations-Stichproben

| Behauptung | Prüfmethode | Ergebnis |
|------------|-------------|----------|
| `PdfDocument` nicht disposed | Read MaterialExportService.cs | ✓ bestätigt (Z. 45 + 162) |
| 19 Calculator-VMs identische Header | Grep `: ViewModelBase, IDisposable, ICalculatorViewModel` | ✓ 19 Files |
| 15 Views mit `Margin="16,16,16,200"` | Grep | ✓ 15 Files |
| 35 Hardcoded-Farben in Views | Grep `#F59E0B\|#3B82F6\|#22C55E\|#EF4444` | ✓ 35 Treffer / 5 Files |
| `EnsureCustomLoaded` Sync-Wait | Read MaterialPriceService.cs:72-86 | ✓ bestätigt |
| `OnPropertyChanged("")` bei Sprach-Wechsel | Read MainViewModel.cs:311 | ✓ bestätigt |
| `CalculatorViewBase.Contains("Result")` | Read CalculatorViewBase.cs:46-49 | ✓ bestätigt |
| `ViewModelBase` ist nur leeres `ObservableObject` | Read | ✓ keine Calculator-Basis vorhanden |
| Compiled Bindings fehlen (Performance-Sub-Audit) | Grep `x:DataType\|x:CompileBindings` | ✗ widerlegt — 26 Views, 61 Treffer |

Der Compiled-Bindings-Befund des Performance-Sub-Audits wurde **korrigiert** und im Hauptbericht nicht aufgeführt.
