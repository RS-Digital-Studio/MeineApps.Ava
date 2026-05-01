# HandwerkerRechner (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Handwerker-App mit 19 Rechnern (5 Free Floor + 14 Premium-Tools), Projektverwaltung, Angebots-Generator, Vorlagen und Einheiten-Umrechnung. Premium = nur "remove_ads".

**Version:** 2.0.7 | **Package-ID:** com.meineapps.handwerkerrechner | **Status:** Geschlossener Test

## Wichtige Domänen-Patterns

- **Plaster/Screed nutzen Enum-Routing** (`PlasterType`/`ScreedType`), nicht deutsche Strings — typsicher gegen Refactor-Falle
- **Drehstrom-Erkennung in CableSizing/VoltageDrop**: `Voltage >= 380V` → Faktor `√3` statt `2` (DIN-konform für 400V-Drehstrom)
- **Project-Templates** (`ProjectTemplateService`): Built-in Templates verwenden EXAKT die Property-Keys, die die Calculator-VMs in `LoadProjectAsync` via `project.GetValue<T>("Key")` erwarten. `ProjectTemplatesViewModel.ParseTemplateValue` castet String-Defaults nach `bool`/`int`/`double` damit der JSON-Roundtrip funktioniert
- **Renderer-Lokalisierung**: 4 Visualisierungen (Tile/Grout/Garden/Concrete) holen Labels via `LocalizationManager.Service?.GetString("VizXxx")`. Hardcoded deutsche Strings sind verboten (englische User würden „Verschnitt" statt „Waste" sehen)
- **CraftEngine.Clamp(value, max)**: Plausibilitäts-Bounds gegen `Infinity`/`NaN` aus User-Inputs. Pattern in `CalculateTiles` etabliert; weitere Methoden bei Bedarf erweitern
- **InsulationVisualization** nutzt `_epsRandoms`/`_woodFiberRandoms` als pre-computed static Arrays (Random.Shared.NextDouble() pro Frame würde sichtbar wackeln)

## Features

### 16 Rechner in 2 Kategorien

**Free Floor Calculators (5):**
1. TileCalculator - Fliesenbedarf (Raum, Verschnitt, Fugenmasse)
2. WallpaperCalculator - Tapetenrollen (Wandhöhe, Muster-Rapport, Tür-/Fenster-Abzüge)
3. PaintCalculator - Farbbedarf (Anstriche, Deckfähigkeit, Tür-/Fenster-Abzüge)
4. FlooringCalculator - Laminat/Parkett (Raumform, Verschnitt)
5. ConcreteCalculator - Beton (Platte/Fundament/Säule, Volumen, Säcke, Mischverhältnis)

**Premium Calculators (14):**
6. DrywallCalculator - Trockenbau (Platten, Profile, Schrauben)
7. ElectricalCalculator - Elektrik (Kabel, Kosten, Ohm'sches Gesetz)
8. MetalCalculator - Metall (Gewicht, Gewindegröße, Bohrung)
9. GardenCalculator - Garten (Erde, Mulch, Pflaster, Rasen)
10. RoofSolarCalculator - Dach+Solar (Dachfläche, Solarpanel, Amortisation)
11. StairsCalculator - Treppen (DIN 18065, Schrittmaß, Stufenhöhe, Komfort)
12. PlasterCalculator - Putz (Wandfläche, Putzdicke, Putzart, 30kg-Säcke)
13. ScreedCalculator - Estrich (Bodenfläche, Dicke, Typ, Volumen, 40kg-Säcke, Trocknungszeit)
14. InsulationCalculator - Dämmung (U-Wert, Dämmstofftyp, Dicke, Platten, Kosten)
15. CableSizingCalculator - Leitungsquerschnitt (Strom, Länge, Spannung, Material → Querschnitt, Spannungsabfall, VDE)
16. GroutCalculator - Fugenmasse (Fläche, Fliesenmaße, Fugenbreite/-tiefe → kg, Eimer, Kosten)
17. HourlyRateCalculator - Stundenrechner (Stundensatz, Pause, Overhead, MwSt → Lohnkosten brutto/netto)
18. MaterialCompareCalculator - Material-Vergleich (Fläche, Produkt A vs B, Verbrauch, Verschnitt → Ersparnis)
19. AreaMeasureCalculator - Aufmaß-Rechner (6 Formen: Rechteck/L/T/Trapez/Dreieck/Kreis, Teilflächen-Summierung)

### Weitere Features
- **Projektverwaltung**: CRUD mit JSON-Persistenz + SemaphoreSlim
- **Einheiten-Umrechnung**: Länge, Fläche, Volumen, Gewicht (Metrisch/Imperial)
- **Material-Liste PDF Export**: PdfSharpCore-basiert (A4, Header, Inputs, Results, Footer)
- **Material-Liste CSV Export**: Semikolon-getrennt, UTF-8-BOM, Excel-kompatibel. Alle 19 Calculator-VMs haben `ExportCsvCommand` (gleiche inputs/results wie PDF, selbes Ad-Gate)
- **Angebots-Generator**: QuoteView - Kundendaten, Positions-Tabelle, Marge+MwSt, PDF-Export
- **Projekt-Vorlagen**: ProjectTemplatesView - 2 Sektionen (Eingebaut/Eigene), Anwenden-Dialog
- **Foto-Dokumentation**: Max. 5 Fotos pro Projekt im Notizen-Editor (Desktop: StorageProvider, Android: Factory)

## App-spezifische Services

- **ProjectService**: JSON-Persistenz (Project Model), DateTime.UtcNow
- **CalculationHistoryService** (Core.Ava): MaxItemsPerCalculator 30, alle Nutzer ohne Premium-Gate
  - Live-Calculate-Stutter-Fix: `ScheduleDebouncedSave()` statt `AddCalculationAsync()` direkt → 2s-Debounce, nur letztes Resultat pro Calculator wird geschrieben
  - `GetAllHistoryAsync` liest 19 JSON-Files parallel via `Task.WhenAll` (~30-50ms statt 150-300ms beim History-Tab-Open)
  - Static `JsonSerializerOptions` (kein Allocate pro Save)
- **UnitConverterService**: Länge, Fläche, Volumen, Gewicht
- **IMaterialExportService / MaterialExportService**:
  - PdfSharpCore A4 Export + CSV Export (Semikolon, UTF-8-BOM)
  - CSV-Header lokalisiert (ExportType/Date/Parameter/Value/Result-RESX-Keys statt deutsch hardcodiert)
  - Formula-Injection-Schutz in `EscapeCsv()`: führendes `=`/`+`/`-`/`@` mit Apostroph präfixt
- **IPhotoPickerService / DesktopPhotoPickerService**: Foto-Auswahl via StorageProvider, kopiert nach AppData/photos/ mit GUID-Name. Factory-Pattern in App.axaml.cs für Android-Override
  - Path-Traversal-Schutz in `DeletePhotoAsync`: `Path.GetFullPath`-Vergleich mit erwartetem PhotoDirectory verhindert Löschung von Files außerhalb der App-Sandbox bei manipulierten projects.json

## Premium & Ads

### Ad-Placements (Rewarded)
1. **material_pdf**: Material-Liste PDF Export (alle 19 Calculator Views)
2. **project_export**: Projekt-Export als PDF (ProjectsView)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads (Banner + Rewarded entfallen), direkter PDF-Export
- Alle 19 Rechner sind ohne Premium-Gate frei zugänglich; PremiumAccessService wurde in v2.0.8 entfernt (war ungenutzt)

## SkiaSharp Visualisierungen (Graphics/)

18 statische Renderer-Klassen + 1 Splash-Renderer + 1 Background-Renderer in `HandwerkerRechner.Shared/Graphics/`, alle nutzen `SkiaBlueprintCanvas` + `SkiaThemeHelper` aus MeineApps.UI.

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
| `BlueprintBackgroundRenderer.cs` | Background | Animierter Blueprint-Hintergrund (5 Layer: Gradient, Blueprint-Grid mit Drift, Maßband-Markierungen, 8 Tool-Silhouetten, Vignette). Instance-basiert, IDisposable, 0 GC/Frame, ~5fps DispatcherTimer |
| `TileVisualization.cs` | Floor | 2D-Grundriss mit Fliesengitter, Verschnitt-Fliesen rot schraffiert, Verschnitt-Info-Box (Prozent+Fliesengröße), Einzelfliesen-Bemaßung |
| `FlooringVisualization.cs` | Floor | Dielen-Verlegung mit 50%-Versatz, 3 Holzfarben, Verschnitt-Zone rot schraffiert an Rändern, Gesamtflächenbedarf+Verschnitt als Formel |
| `WallpaperVisualization.cs` | Floor | Wand-Abwicklung mit vertikalen Bahnen, Rapport-Versatz gestrichelt |
| `PaintVisualization.cs` | Floor | Wand mit Farbschichten + Kannen-Icons (2.5L Standard, max 10 Icons, ×N bei mehr) |
| `ConcreteVisualization.cs` | Floor | 3 Sub-Typen + Mischverhältnis-Leiste (Zement:Sand:Kies farbige Segmente, Labels, konfigurierbar) |
| `StairsVisualization.cs` | Premium | Seitenansicht Treppenprofil, Winkel-Arc, DIN-Farbcode (Grün/Gelb/Rot) |
| `RoofSolarVisualization.cs` | Premium | 3 Sub-Typen: Dachdreieck+Winkel, Ziegelraster, Solar-Panel-Layout mit Kompass |
| `DrywallVisualization.cs` | Premium | Wandschnitt mit CW/UW-Ständerwerk, Plattenaufteilung |
| `ElectricalVisualization.cs` | Premium | 3 Sub-Typen: Spannungsabfall-Kurve, Kosten-Balken, Ohmsches Dreieck |
| `MetalVisualization.cs` | Premium | 2 Sub-Typen: 6 Profil-Querschnitte, Gewindebohrung-Kreis |
| `GardenVisualization.cs` | Premium | 3 Sub-Typen: Pflastermuster, Erdschichten-Profil (3 Schichten: Mutterboden+Sand+Kies, Grasnarbe, Wurzeln, Stein-Textur), Teichfolie-Draufsicht |
| `PlasterVisualization.cs` | Premium | Wandquerschnitt mit Mauerwerk-Textur + proportionaler Putzschicht, Bemaßung, Sack-Info |
| `ScreedVisualization.cs` | Premium | Bodenquerschnitt mit Untergrund (Kies-Textur) + Estrichschicht (proportional, farblich nach Typ), Bemaßung, Sack-Info |
| `InsulationVisualization.cs` | Premium | Wandquerschnitt mit Mauerwerk-Textur + Dämmschicht (proportional, 4 Materialtypen mit spezifischen Texturen: EPS-Kreise, XPS-Linien, Mineralwolle-Wellen, Holzfaser-Striche), Bemaßung, Platten-Info |
| `CableSizingVisualization.cs` | Premium | Kabelquerschnitt (Kupfer/Alu-Kreis mit Isolierung), Spannungsabfall-Balken (VDE-Grenzlinie), Durchmesser-Bemaßung |
| `GroutVisualization.cs` | Premium | Fliesengitter mit proportionalen Fugenlinien, Bemaßungen (Fliesen+Fugen), Info-Box (kg, Eimer, Kosten) |
| `CostBreakdownVisualization.cs` | Shared | Horizontale gestapelte Kostenbalken mit Segmenten, Prozent-Labels, Legende, Gesamtsumme. Wiederverwendbar für alle Rechner |
| `MaterialStackVisualization.cs` | Shared | Material-Icon-Reihe (10 Typen: Eimer, Sack, Rolle, Paket, Box, Platte, Kabel, Stange) mit Mengenangaben |
| `HandwerkerRechnerSplashRenderer.cs` | Splash | "Das Maßband": Holz-Hintergrund mit Maserungslinien, gelbes Maßband als Fortschrittsbalken (cm-Markierungen, Bleistift), 12 Sägespäne-Partikel. Erbt von SplashRendererBase |

**Pattern**: Alle `public static void Render(SKCanvas, SKRect, ...)` mit gecachten `SKPaint` (static readonly), inkl. `_layerPaint` für Alpha-Fade-In (SaveLayer). Views haben `OnPaintVisualization` Code-Behind Handler mit Named-Handler-Pattern (explizites Unsubscribe bei DataContext-Wechsel). Visualisierung in `<Border Classes="Card" Height="220" ClipToBounds="True">` mit `IsVisible="{Binding HasResult}"`.

**CalculatorViewBase** (`Views/CalculatorViewBase.cs`): Abstrakte Basisklasse für alle 19 Calculator-Views. Kapselt das gemeinsame PropertyChanged-Subscription-Pattern (`_currentVm`/`_resultHandler`, An-/Abmeldung bei DataContext-Wechsel). Abgeleitete Klassen überschreiben:
- `ShouldInvalidateOnPropertyChanged(propertyName)`: Filter-Logik (Standard: `Contains("Result")`)
- `OnResultPropertyChanged()`: Reaktion (Animation starten oder Canvas invalidieren)
- `RequestAnimationFrame(sender)`: Statische Hilfsmethode für Animation-Loop (NeedsRedraw → InvalidateSurface)

**Background Render-Loop**: MainView: DispatcherTimer 200ms (~5fps), `_backgroundRenderer.Update(0.2f)` + `BackgroundCanvas.InvalidateSurface()`. SKCanvasView mit `Grid.RowSpan="3"` + `IsHitTestVisible="False"` hinter Content. UserControl Background=Transparent (Gradient kommt vom Renderer). Start in `OnDataContextChanged`, Stop+Dispose in `OnDetachedFromVisualTree`.

**Einschwing-Animation**: 17 Renderer erben von `AnimatedVisualizationBase` (StartAnimation/NeedsRedraw). Die 3 neuen Visualisierungsklassen (HourlyRate, MaterialCompare, AreaMeasure) sind einfache statische Klassen OHNE Animation-Basisklasse → Code-Behind ruft direkt `InvalidateSurface()` bei PropertyChanged auf (kein StartAnimation/NeedsRedraw).

| Datei | Animation |
|-------|-----------|
| `HourlyRateVisualization.cs` | Nein - direkt statisch, keine hardcodierten Strings (Labels als Parameter: netLabel, overheadLabel, vatLabel, totalLabel) |
| `MaterialCompareVisualization.cs` | Nein - direkt statisch |
| `AreaMeasureVisualization.cs` | Nein - direkt statisch |

## Besondere Architektur

### Tab-Navigation (4 Tabs)
- **Tab 0**: Home (Rechner-Übersicht)
- **Tab 1**: Projects (Projektverwaltung)
- **Tab 2**: History (Berechnungshistorie, gruppiert nach Rechner-Typ)
- **Tab 3**: Settings (Einstellungen)

### Calculator-VM Factory Pattern
- 19 Calculator-VMs als Transient registriert in DI
- MainViewModel erhält `Func<T>` Factories per Constructor Injection (kein Service-Locator)
- Jedes Öffnen eines Rechners erzeugt eine frische VM-Instanz via `_xxxVmFactory()`
- Func<T> Factories als Singleton registriert in `App.axaml.cs` (analog FitnessRechner)

### Calculator Overlay via ViewLocator
- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties
- `MainView`: KEINE lokalen DataTemplates — globaler `ViewLocator` (App.axaml) löst alle 19 Calculator-VMs per Konvention auf (`ViewModels.Floor.TileCalculatorViewModel` → `Views.Floor.TileCalculatorView`)
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectHistoryTab/SelectSettingsTab` setzen `CurrentPage=null`
- Render-Performance: BlueprintBackground-Timer pausiert wenn `CurrentPage != null` (Hintergrund verdeckt → keine GPU-Arbeit)

### History-Tab
- `HistoryViewModel`: Lädt alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach CalculatorId
- `HistoryView`: Gruppierte Liste mit Expander pro Rechner-Typ, SwipeToReveal zum Löschen
- Free: 5 Einträge pro Rechner, Extended (24h per Rewarded Ad): 30 Einträge
- Navigation: Tap auf Eintrag öffnet den entsprechenden Rechner (mit Premium-Check)
- 4 neue RESX-Keys in 6 Sprachen: TabHistory, HistoryEmpty, HistoryExtendedHint, WatchAdForHistory

### ICalculatorViewModel Interface
- Alle 19 Calculator-VMs implementieren `ICalculatorViewModel` (NavigationRequested, MessageRequested, FloatingTextRequested, ClipboardRequested, ShowSaveDialog, Cleanup(), LoadFromProjectIdAsync())
- MainViewModel nutzt Interface-Polymorphie statt 19-facher switch/case-Blöcke für WireCalculatorEvents, CleanupCurrentCalculator, HandleBackPressed
- Factory-Dictionary `_calculatorFactories` (Route → Func<ObservableObject>) ersetzt 19 einzelne Func<T>-Fields
- Interface-Datei: `ViewModels/ICalculatorViewModel.cs`

### Projekt-Navigation
- ProjectsVM.NavigationRequested → MainVM.OnProjectNavigation (mit Premium-Check via `IsPremiumRoute`)
- HistoryVM.NavigationRequested → MainVM.OnHistoryNavigation (mit Premium-Check via `IsPremiumRoute`)
- SelectProjectsTab loest automatisch Reload der Projektliste aus

### Floor vs Premium VMs
- Floor-VMs (5): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Floor`
- Premium-VMs (14): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Premium`
- Alle 19 VMs implementieren `ICalculatorViewModel` (Cleanup räumt Debounce-Timer + Event-Subscriptions auf)
- Alle 19 VMs haben `[ObservableProperty] private bool _isCalculating;` als Reentrancy-Schutz in `Calculate()`

### Live-Berechnung (Debounce)
- Alle 19 Calculator-VMs berechnen automatisch 300ms nach letzter Eingabe-Änderung
- Pattern: `partial void OnXxxChanged() => ScheduleAutoCalculate()` + `Timer.Change()` (wiederverwendet statt Dispose/New)
- Timer-Callback: `Dispatcher.UIThread.Post(() => _ = Calculate())` (async Task → fire-and-forget Lambda)
- History-Save NICHT pro Live-Calculate-Iteration (10-30ms Stutter pro Tastendruck) → `ScheduleDebouncedSave` mit 2s-Debounce
- `Reset()` disposed Timer, alle VMs implementieren `IDisposable`
- Cleanup() ist API-konsistent über alle 19 VMs: räumt Event-Subscription UND Debounce-Timer ab
- Kein manuelles "Berechnen"-Drücken mehr nötig (Button bleibt aber als Fallback)

### UI-Feedback
- **FloatingText**: Dezentes Feedback bei Aktionen (Save, Export, Clipboard) - seriöse App, kein Confetti
