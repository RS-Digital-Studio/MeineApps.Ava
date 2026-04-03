# HandwerkerRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Handwerker-App mit 19 Rechnern (5 Free Floor + 14 Premium), Projektverwaltung, Angebots-Generator, Vorlagen und Einheiten-Umrechnung.

**Version:** 2.0.6 | **Package-ID:** com.meineapps.handwerkerrechner | **Status:** Geschlossener Test

## Features

### 16 Rechner in 2 Kategorien

**Free Floor Calculators (5):**
1. TileCalculator - Fliesenbedarf (Raum, Verschnitt, Fugenmasse)
2. WallpaperCalculator - Tapetenrollen (Wandhoehe, Muster-Rapport, Tür-/Fenster-Abzüge)
3. PaintCalculator - Farbbedarf (Anstriche, Deckfaehigkeit, Tür-/Fenster-Abzüge)
4. FlooringCalculator - Laminat/Parkett (Raumform, Verschnitt)
5. ConcreteCalculator - Beton (Platte/Fundament/Säule, Volumen, Säcke, Mischverhältnis)

**Premium Calculators (14):**
6. DrywallCalculator - Trockenbau (Platten, Profile, Schrauben)
7. ElectricalCalculator - Elektrik (Kabel, Kosten, Ohm'sches Gesetz)
8. MetalCalculator - Metall (Gewicht, Gewindegroesse, Bohrung)
9. GardenCalculator - Garten (Erde, Mulch, Pflaster, Rasen)
10. RoofSolarCalculator - Dach+Solar (Dachflaeche, Solarpanel, Amortisation)
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
- **Einheiten-Umrechnung**: Laenge, Flaeche, Volumen, Gewicht (Metrisch/Imperial)
- **Material-Liste PDF Export**: PdfSharpCore-basiert (A4, Header, Inputs, Results, Footer)
- **Material-Liste CSV Export**: Semikolon-getrennt, UTF-8-BOM, Excel-kompatibel. Alle 19 Calculator-VMs haben `ExportCsvCommand` (gleiche inputs/results wie PDF, selbes Ad-Gate)
- **Angebots-Generator**: QuoteView - Kundendaten, Positions-Tabelle, Marge+MwSt, PDF-Export
- **Projekt-Vorlagen**: ProjectTemplatesView - 2 Sektionen (Eingebaut/Eigene), Anwenden-Dialog
- **Foto-Dokumentation**: Max. 5 Fotos pro Projekt im Notizen-Editor (Desktop: StorageProvider, Android: Factory)

## App-spezifische Services

- **ProjectService**: JSON-Persistenz (Project Model), DateTime.UtcNow
- **CalculationHistoryService**: MaxItemsPerCalculator 30 (5 free / 30 extended)
- **UnitConverterService**: Laenge, Flaeche, Volumen, Gewicht
- **IMaterialExportService / MaterialExportService**: PdfSharpCore A4 Export + CSV Export (Semikolon, UTF-8-BOM)
- **IPhotoPickerService / DesktopPhotoPickerService**: Foto-Auswahl via StorageProvider, kopiert nach AppData/photos/ mit GUID-Name. Factory-Pattern in App.axaml.cs für Android-Override
- **IPremiumAccessService / PremiumAccessService**: 30-Min temporaerer Zugang zu Premium-Rechnern, 24h Extended History

## Premium & Ads

### Ad-Placements (Rewarded)
1. **premium_access**: 30 Minuten Zugang zu 11 Premium-Rechnern (HomeView)
2. **extended_history**: 24h-Zugang zu 30 statt 10 History-Eintraegen (HomeView)
3. **material_pdf**: Material-Liste PDF Export (alle 16 Calculator Views)
4. **project_export**: Projekt-Export als PDF (ProjectsView)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads, permanenter Premium-Rechner-Zugang, unbegrenzte History, direkter PDF-Export

## SkiaSharp Visualisierungen (Graphics/)

18 statische Renderer-Klassen + 1 Splash-Renderer + 1 Background-Renderer in `HandwerkerRechner.Shared/Graphics/`, alle nutzen `SkiaBlueprintCanvas` + `SkiaThemeHelper` aus MeineApps.UI.

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
| `BlueprintBackgroundRenderer.cs` | Background | Animierter Blueprint-Hintergrund (5 Layer: Gradient, Blueprint-Grid mit Drift, Massband-Markierungen, 8 Tool-Silhouetten, Vignette). Instance-basiert, IDisposable, 0 GC/Frame, ~5fps DispatcherTimer |
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

**CalculatorViewBase** (`Views/CalculatorViewBase.cs`): Abstrakte Basisklasse fuer alle 19 Calculator-Views. Kapselt das gemeinsame PropertyChanged-Subscription-Pattern (`_currentVm`/`_resultHandler`, An-/Abmeldung bei DataContext-Wechsel). Abgeleitete Klassen ueberschreiben:
- `ShouldInvalidateOnPropertyChanged(propertyName)`: Filter-Logik (Standard: `Contains("Result")`)
- `OnResultPropertyChanged()`: Reaktion (Animation starten oder Canvas invalidieren)
- `RequestAnimationFrame(sender)`: Statische Hilfsmethode fuer Animation-Loop (NeedsRedraw → InvalidateSurface)

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

### Calculator Overlay via DataTemplates
- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties
- `MainView`: DataTemplates fuer automatische View-Zuordnung per VM-Typ (19 VMs)
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectHistoryTab/SelectSettingsTab` setzen `CurrentPage=null`

### History-Tab
- `HistoryViewModel`: Lädt alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach CalculatorId
- `HistoryView`: Gruppierte Liste mit Expander pro Rechner-Typ, SwipeToReveal zum Löschen
- Free: 5 Einträge pro Rechner, Extended (24h per Rewarded Ad): 30 Einträge
- Navigation: Tap auf Eintrag öffnet den entsprechenden Rechner (mit Premium-Check)
- 4 neue RESX-Keys in 6 Sprachen: TabHistory, HistoryEmpty, HistoryExtendedHint, WatchAdForHistory

### ICalculatorViewModel Interface
- Alle 19 Calculator-VMs implementieren `ICalculatorViewModel` (NavigationRequested, MessageRequested, FloatingTextRequested, ClipboardRequested, ShowSaveDialog, Cleanup(), LoadFromProjectIdAsync())
- MainViewModel nutzt Interface-Polymorphie statt 19-facher switch/case-Bloecke fuer WireCalculatorEvents, CleanupCurrentCalculator, HandleBackPressed
- Factory-Dictionary `_calculatorFactories` (Route → Func<ObservableObject>) ersetzt 19 einzelne Func<T>-Fields
- Interface-Datei: `ViewModels/ICalculatorViewModel.cs`

### Projekt-Navigation
- ProjectsVM.NavigationRequested → MainVM.OnProjectNavigation (mit Premium-Check via `IsPremiumRoute`)
- HistoryVM.NavigationRequested → MainVM.OnHistoryNavigation (mit Premium-Check via `IsPremiumRoute`)
- SelectProjectsTab loest automatisch Reload der Projektliste aus

### Floor vs Premium VMs
- Floor-VMs (5): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Floor`
- Premium-VMs (11): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Premium`
- Premium-VMs (3): HourlyRate/MaterialCompare/AreaMeasure erben von `ObservableObject`
- CalculatorViewModelBase existiert als Abstract Base Class, wird aber NICHT verwendet

### Live-Berechnung (Debounce)
- Alle 16 Calculator-VMs berechnen automatisch 300ms nach letzter Eingabe-Änderung
- Pattern: `partial void OnXxxChanged() => ScheduleAutoCalculate()` + `Timer.Change()` (wiederverwendet statt Dispose/New)
- Timer-Callback: `Dispatcher.UIThread.Post(() => _ = Calculate())` (async Task → fire-and-forget Lambda)
- `Reset()` disposed Timer, alle VMs implementieren `IDisposable`
- Premium-VMs haben zusätzlich `Cleanup()` Methode für Timer-Dispose bei Navigation weg
- Kein manuelles "Berechnen"-Drücken mehr nötig (Button bleibt aber als Fallback)

### Game Juice
- **FloatingText**: "Projekt wurde gespeichert!" nach ConfirmSaveProject
- **Celebration**: Confetti bei erfolgreichem Save
