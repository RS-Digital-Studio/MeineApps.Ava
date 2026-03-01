# HandwerkerRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Handwerker-App mit 16 Rechnern (5 Free Floor + 11 Premium), Projektverwaltung und Einheiten-Umrechnung.

**Version:** 2.0.0 | **Package-ID:** com.meineapps.handwerkerrechner | **Status:** Geschlossener Test

## Features

### 16 Rechner in 2 Kategorien

**Free Floor Calculators (5):**
1. TileCalculator - Fliesenbedarf (Raum, Verschnitt, Fugenmasse)
2. WallpaperCalculator - Tapetenrollen (Wandhoehe, Muster-Rapport, Tür-/Fenster-Abzüge)
3. PaintCalculator - Farbbedarf (Anstriche, Deckfaehigkeit, Tür-/Fenster-Abzüge)
4. FlooringCalculator - Laminat/Parkett (Raumform, Verschnitt)
5. ConcreteCalculator - Beton (Platte/Fundament/Säule, Volumen, Säcke, Mischverhältnis)

**Premium Calculators (11):**
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

### Weitere Features
- **Projektverwaltung**: CRUD mit JSON-Persistenz + SemaphoreSlim
- **Einheiten-Umrechnung**: Laenge, Flaeche, Volumen, Gewicht (Metrisch/Imperial)
- **Material-Liste PDF Export**: PdfSharpCore-basiert (A4, Header, Inputs, Results, Footer)

## App-spezifische Services

- **ProjectService**: JSON-Persistenz (Project Model), DateTime.UtcNow
- **CalculationHistoryService**: MaxItemsPerCalculator 30 (5 free / 30 extended)
- **UnitConverterService**: Laenge, Flaeche, Volumen, Gewicht
- **IMaterialExportService / MaterialExportService**: PdfSharpCore A4 Export
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

18 statische Renderer-Klassen in `HandwerkerRechner.Shared/Graphics/`, alle nutzen `SkiaBlueprintCanvas` + `SkiaThemeHelper` aus MeineApps.UI.

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
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

**Pattern**: Alle `public static void Render(SKCanvas, SKRect, ...)` mit gecachten `SKPaint` (static readonly), inkl. `_layerPaint` für Alpha-Fade-In (SaveLayer). Views haben `OnPaintVisualization` Code-Behind Handler mit Named-Handler-Pattern (explizites Unsubscribe bei DataContext-Wechsel). Visualisierung in `<Border Classes="Card" Height="220" ClipToBounds="True">` mit `IsVisible="{Binding HasResult}"`.

**Einschwing-Animation**: Alle 17 Renderer nutzen `AnimatedVisualizationBase` (500ms, EaseOutCubic). `StartAnimation()` wird im Code-Behind bei Property-Changes mit "Result" im Namen aufgerufen. `NeedsRedraw` steuert den Invalidation-Loop via `Dispatcher.UIThread.Post`. Effekte: Tile/Flooring (Reihen-weise), Wallpaper (Alpha Fade-In der Bahnen), Paint (Schichten von unten), Stairs (Stufen von unten), CostBreakdown (Balken-Wachstum), MaterialStack (sequentielles Erscheinen), Concrete/Drywall/Electrical/Metal/Garden/RoofSolar/Plaster/Screed/Insulation/Grout (Global Alpha Fade-In via SaveLayer).

## Besondere Architektur

### Tab-Navigation (4 Tabs)
- **Tab 0**: Home (Rechner-Übersicht)
- **Tab 1**: Projects (Projektverwaltung)
- **Tab 2**: History (Berechnungshistorie, gruppiert nach Rechner-Typ)
- **Tab 3**: Settings (Einstellungen)

### Calculator Overlay via DataTemplates
- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties
- `MainView`: DataTemplates fuer automatische View-Zuordnung per VM-Typ (16 VMs)
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectHistoryTab/SelectSettingsTab` setzen `CurrentPage=null`

### History-Tab
- `HistoryViewModel`: Lädt alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach CalculatorId
- `HistoryView`: Gruppierte Liste mit Expander pro Rechner-Typ, SwipeToReveal zum Löschen
- Free: 5 Einträge pro Rechner, Extended (24h per Rewarded Ad): 30 Einträge
- Navigation: Tap auf Eintrag öffnet den entsprechenden Rechner (mit Premium-Check)
- 4 neue RESX-Keys in 6 Sprachen: TabHistory, HistoryEmpty, HistoryExtendedHint, WatchAdForHistory

### Projekt-Navigation
- ProjectsVM.NavigationRequested → MainVM.OnProjectNavigation (mit Premium-Check via `IsPremiumRoute`)
- HistoryVM.NavigationRequested → MainVM.OnHistoryNavigation (mit Premium-Check via `IsPremiumRoute`)
- WireCalculatorEvents: Per switch/case (kein gemeinsames Interface)
- SelectProjectsTab löst automatisch Reload der Projektliste aus

### Floor vs Premium VMs
- Beide erben direkt von `ObservableObject` (nicht von CalculatorViewModelBase)
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

## Changelog (Highlights)

- **01.03.2026**: **Immersiver Ladebildschirm**: Loading-Pipeline (`Loading/HandwerkerRechnerLoadingPipeline.cs`) mit `ShaderPreloader` (weight 30) + ViewModel-Erstellung (weight 15). `App.axaml.cs` nutzt `Panel(MainView + SkiaLoadingSplash)`-Pattern mit `RunLoadingAsync`. `DataContext` wird erst nach Pipeline-Abschluss gesetzt (nicht mehr synchron beim Start). Partikel-Effekte via `SplashScreenRenderer` aus `MeineApps.UI`.
- **28.02.2026**: Ladebildschirm mit echtem Preloading:
  - SplashOverlay (MeineApps.UI) erweitert um Task-basiertes Preloading mit Progress + Status-Text
  - MainView: Panel-Wrapper um Root-Grid, SplashOverlay als Overlay darüber
  - 4 Preload-Schritte: SkSL-Shader (12 Stück, ThreadPool), CalculationHistoryService warm machen, Projekte vorladen, History vorladen
  - Ladebalken zeigt echten Fortschritt (0-100%), Status-Text wechselt pro Schritt
  - Fade-Out (400ms) nach Abschluss, dann IsVisible=false + PreloadCompleted Event
- **28.02.2026**: Performance-Optimierung + Memory-Leak-Fixes (Tiefenanalyse):
  - HOCH: SKPaint pro Frame in 9 Visualisierungen → statische `_layerPaint` + `_rng` Felder (Garden, Insulation, Screed haben zusätzlich statische Random)
  - HOCH: Memory Leak in 16 View Code-Behinds → Named-Handler-Pattern mit explizitem Unsubscribe bei DataContext-Wechsel
  - HOCH: CleanupCurrentCalculator vervollständigt → alle 11 Premium-VMs haben `Cleanup()` Methode
  - MITTEL: UpdateHomeTexts → `OnPropertyChanged(string.Empty)` statt 46 einzelner Aufrufe
  - MITTEL: HistoryVM Groups → Batch-Update via neue ObservableCollection statt Clear+Add-Loop
  - MITTEL: ProjectService JsonSerializerOptions → statisch gecacht
  - MITTEL: Timer.Change() statt Dispose/New in ScheduleAutoCalculate (bereits implementiert)
  - FIX: CS0407 in 16 VMs → `Dispatcher.UIThread.Post(() => _ = Calculate())` statt Method-Group
- **28.02.2026**: Fugenmasse-Rechner (GroutCalculator) als 16. Rechner (11. Premium):
  - GroutViewModel + GroutView + GroutVisualization (3 neue Dateien)
  - CraftEngine.CalculateGrout(): Industriestandard-Formel (L+B)/(L*B) * Breite * Tiefe * Dichte, 10% Reserve, 5kg-Eimer
  - GroutResult Record: AreaSqm, TileLengthCm, TileWidthCm, GroutWidthMm, GroutDepthMm, TotalKg, TotalWithReserveKg, BucketsNeeded, ConsumptionPerSqm, TotalCost
  - CalculatorType.Grout im Enum + CalcTypeGrout RESX-Key
  - SkiaSharp-Visualisierung: Fliesengitter mit proportionalen Fugenlinien, Bemaßungen, Info-Box
  - Vollständig integriert: MainViewModel (8 Stellen), MainView (DataTemplate + Home-Card), App.axaml.cs (DI), HistoryVM, ProjectsVM
  - 14 neue RESX-Keys in 6 Sprachen (CalcGrout, CalcGroutDesc, GroutInputs, GroutArea, GroutTileLength, GroutTileWidth, GroutDepth, GroutPricePerKg, GroutConsumption, GroutTotal, GroutTotalWithReserve, GroutTileSize, UnitBuckets, CalcTypeGrout)
  - Live-Berechnung (300ms Debounce), CountUp-Animation, History, Projekte, Share, PDF-Export
  - Rose-Farbschema (#EC4899 → #DB2777), Material Icon: Texture
- **28.02.2026**: Leitungsquerschnitt-Rechner (CableSizingCalculator) als 15. Rechner (10. Premium):
  - CableSizingViewModel + CableSizingView + CableSizingVisualization (3 neue Dateien)
  - CraftEngine.CalculateCableSize(): 2 Materialtypen (Kupfer/Aluminium), DIN VDE Standardquerschnitte, Spannungsabfall-Formel
  - CableSizingResult Record: CurrentAmps, LengthM, VoltageV, MaterialType, MinCrossSection, RecommendedCrossSection, ActualDropV/Percent, IsVdeCompliant
  - CalculatorType.CableSizing im Enum + CalcTypeCableSizing RESX-Key
  - Spannung-Auswahl via ComboBox (230V/400V), Material-Auswahl (Kupfer/Aluminium)
  - SkiaSharp-Visualisierung: Kabelquerschnitt-Kreis (Kupfer/Alu-Farbe) mit Isolierung + Spannungsabfall-Balken (VDE-Grenzlinie)
  - Vollständig integriert: MainViewModel (8 Stellen), MainView (DataTemplate + Home-Card), App.axaml.cs (DI), HistoryVM, ProjectsVM
  - 17 neue RESX-Keys in 6 Sprachen (CalcCableSizing, CalcCableSizingDesc, CableCurrent/Length/Voltage/Material, 2x Spannung, 2x Material, MaxVoltageDrop, RecommendedCrossSection, MinCrossSection, VoltageDrop, VdeCompliant/NotCompliant, CalcTypeCableSizing)
  - Live-Berechnung (300ms Debounce), CountUp-Animation, History, Projekte, Share, PDF-Export
- **28.02.2026**: Dämmung-Rechner (InsulationCalculator) als 14. Rechner (9. Premium):
  - InsulationViewModel + InsulationView + InsulationVisualization (3 neue Dateien)
  - CraftEngine.CalculateInsulation(): 4 Dämmstofftypen (EPS/XPS/Mineralwolle/Holzfaser), Lambda-Werte, U-Wert-Formel
  - InsulationResult Record: Area, ThicknessCm, InsulationType, LambdaValue, PiecesNeeded, TotalCost
  - CalculatorType.Insulation im Enum + CalcTypeInsulation RESX-Key
  - Dämmstoff-Auswahl via ComboBox (4 Optionen, SelectedIndex-Binding)
  - SkiaSharp-Visualisierung: Wandquerschnitt mit Mauerwerk + Dämmschicht (4 Materialtypen mit spezifischen Texturen), Bemaßung
  - Vollständig integriert: MainViewModel (8 Stellen), MainView (DataTemplate + Home-Card), App.axaml.cs (DI), HistoryVM, ProjectsVM
  - 14 neue RESX-Keys in 6 Sprachen (CalcInsulation, CalcInsulationDesc, InsulationArea, CurrentUValue, TargetUValue, InsulationType, 4x Dämmstoffarten, InsulationThickness, InsulationLambda, CalcTypeInsulation, TargetUValueMustBeLess)
  - Live-Berechnung (300ms Debounce), CountUp-Animation, History, Projekte, Share, PDF-Export
- **28.02.2026**: Estrich-Rechner (ScreedCalculator) als 13. Rechner (8. Premium):
  - ScreedViewModel + ScreedView + ScreedVisualization (3 neue Dateien)
  - CraftEngine.CalculateScreed(): 3 Estricharten (Zement/Fließ/Anhydrit), Dichte-Tabelle, 40kg-Säcke, Trocknungszeit
  - ScreedResult Record: Area, ThicknessCm, ScreedType, VolumeM3, WeightKg, BagsNeeded, DryingDays
  - CalculatorType.Screed im Enum + CalcTypeScreed RESX-Key
  - Estrich-Typ-Auswahl via ComboBox (3 Optionen, SelectedIndex-Binding)
  - SkiaSharp-Visualisierung: Bodenquerschnitt mit Kies-Untergrund + Estrichschicht (proportional, animiert)
  - Vollständig integriert: MainViewModel (8 Stellen), MainView (DataTemplate + Home-Card), App.axaml.cs (DI), HistoryVM, ProjectsVM
  - 13 neue RESX-Keys in 6 Sprachen (CalcScreed, CalcScreedDesc, FloorArea, ScreedThickness, ScreedType, 3x Estricharten, ScreedVolume, ScreedWeight, DryingTime, UnitDays, CalcTypeScreed)
  - Live-Berechnung (300ms Debounce), CountUp-Animation, History, Projekte, Share, PDF-Export
- **28.02.2026**: Putz-Rechner (PlasterCalculator) als 12. Rechner (7. Premium):
  - PlasterViewModel + PlasterView + PlasterVisualization (3 neue Dateien)
  - CraftEngine.CalculatePlaster(): 4 Putzarten (Innen/Außen/Kalk/Gips), Dichte-Tabelle, 30kg-Säcke
  - PlasterResult Record: Area, ThicknessMm, PlasterType, PlasterKg, BagsNeeded
  - CalculatorType.Plaster im Enum + CalcTypePlaster RESX-Key
  - Putzart-Auswahl via ComboBox (4 Optionen, SelectedIndex-Binding)
  - SkiaSharp-Visualisierung: Wandquerschnitt mit Mauerwerk-Textur + Putzschicht (proportional, animiert)
  - Vollständig integriert: MainViewModel (8 Stellen), MainView (DataTemplate + Home-Card), App.axaml.cs (DI), HistoryVM, ProjectsVM
  - 11 neue RESX-Keys in 6 Sprachen (CalcPlaster, CalcPlasterDesc, PlasterThickness, PlasterType, 4x Putzarten, PlasterAmount, UnitBags, CalcTypePlaster)
  - Live-Berechnung (300ms Debounce), CountUp-Animation, History, Projekte, Share, PDF-Export
- **28.02.2026**: Fugenmasse-Output im Fliesen-Rechner:
  - Neues Input: GroutWidthMm (Slider 1-10mm, Default 3mm, 0.5er Schritte)
  - Neues Result: GroutMassDisplay (z.B. "2.3 kg")
  - Formel: Industriestandard (L+B)/(L*B) * Breite * Tiefe * Dichte (Fliesen in mm, Dichte 1.6 g/cm³, Fugentiefe 6mm)
  - Fugenmasse in History, Projekt-Save/Load, Share, PDF-Export integriert
  - Live-Berechnung via ScheduleAutoCalculate()
  - 2 neue RESX-Keys in 6 Sprachen (GroutWidth, GroutMass)
- **28.02.2026**: Wandflächen-Abzüge bei Farbe + Tapete:
  - Optionale Tür-/Fenster-Abzüge (ToggleSwitch ein/aus) in PaintCalculatorVM + WallpaperCalculatorVM
  - Neue Properties: ShowDeductions, DoorCount/Width/Height, WindowCount/Width/Height, DeductedAreaDisplay
  - CalculateDeductionArea(): Türen- + Fensterfläche, Math.Max(0,...) Guards
  - Paint: effectiveArea = Area - deduction; Wallpaper: proportionale WallLength-Kürzung
  - Deduction-Daten in History, Projekt-Save/Load, Share, PDF-Export integriert
  - 10 neue RESX-Keys in 6 Sprachen (DeductedArea, DeductionsOptional, Doors, DoorCount/Width/Height, Windows, WindowCount/Width/Height)
  - Deductions-Card in PaintCalculatorView + WallpaperCalculatorView (3-spaltig: Anzahl/Breite/Höhe)
- **28.02.2026**: History-Tab als 4. Tab hinzugefügt:
  - HistoryViewModel + HistoryView: Berechnungshistorie gruppiert nach Rechner-Typ
  - ICalculationHistoryService: Neue Methode `GetAllHistoryAsync(int maxItemsPerCalculator)`
  - MainViewModel: 4. Tab (Home=0, Projects=1, History=2, Settings=3)
  - SwipeToReveal zum Löschen einzelner Einträge
  - Extended-History-Hinweis (5 free, 30 per Rewarded Ad)
  - 4 neue RESX-Keys in 6 Sprachen (TabHistory, HistoryEmpty, HistoryExtendedHint, WatchAdForHistory)
- **28.02.2026**: Einschwing-Animationen auf allen 14 SkiaSharp-Visualisierungen:
  - AnimatedVisualizationBase (500ms, EaseOutCubic) in alle 13 Renderer integriert
  - Tile/Flooring: Reihen erscheinen nacheinander (rows * progress)
  - Wallpaper: Bahnen-Alpha Fade-In (alpha * progress)
  - Paint: Farbschichten füllen sich von unten (layerH * progress)
  - Stairs: Stufen bauen sich von unten auf (stepCount * progress)
  - CostBreakdown: Balken-Segmente wachsen (segWidth * progress)
  - MaterialStack: Icons erscheinen sequentiell (items * progress)
  - Concrete/Drywall/Electrical/Metal/Garden/RoofSolar: Global Alpha Fade-In (SaveLayer)
  - 12 Code-Behind: OnDataContextChanged + PropertyChanged("Result") startet Animation
  - Dispatcher.UIThread.Post-Loop für Animation-Frames (NeedsRedraw)
- **28.02.2026**: Live-Berechnung mit 300ms Debounce in allen 11 Calculator-VMs:
  - Automatische Berechnung bei jeder Eingabe-Änderung (kein manuelles "Berechnen" nötig)
  - 300ms Debounce via `System.Threading.Timer` + `Dispatcher.UIThread.Post`
  - `ScheduleAutoCalculate()` auf allen Input-Properties (Maße, Material, Preise, Optionen)
  - Bestehende `OnXxxChanged` Methoden (Preis-Properties) erweitert statt dupliziert
  - `Reset()` disposed Timer, alle 11 VMs implementieren `IDisposable`
  - Sub-Rechner-Wechsel (Concrete, Electrical, Metal, Garden, RoofSolar) triggert KEIN Auto-Calculate
- **28.02.2026**: CountUp-Animation auf allen 15 Hero-Value TextBlocks:
  - CountUpBehavior (MeineApps.UI) auf alle 11 Calculator Views angewendet
  - 5 Floor: TileCalculator (F0), Wallpaper (F0), Paint (F1 " L"), Flooring (F0), Concrete (F2 " m³")
  - 6 Premium: Drywall (F0), Electrical (3x: F2 " V", F2 " EUR", F2 " W"), Metal (2x: F2 " kg", F1 " mm"), Garden (3x: F0, F0, F2 " m²"), RoofSolar (3x: F1 "°", F0, F0 " kWh"), Stairs (F0)
  - 500ms CubicEaseOut Animation von 0 auf Zielwert bei Berechnung
- **16.02.2026**: Phase 12 SkiaSharp-Erweiterungen:
  - NEU: CostBreakdownVisualization (horizontale gestapelte Kostenbalken, Legende, Gesamtsumme, wiederverwendbar)
  - NEU: MaterialStackVisualization (10 Icon-Typen: Eimer, Sack, Rolle, Paket etc., Mengenangaben)
  - TileVisualization: Verschnitt-Info-Box mit Prozent+Fliesengröße, Einzelfliesen-Bemaßung
  - FlooringVisualization: Verschnitt-Zone rot schraffiert an Rändern, Gesamtflächenbedarf+Verschnitt als Formel, Dielen-Bemaßung
  - PaintVisualization: Farbkannen-Icons (2.5L/Kanne, max 10 Icons, ×N Overflow, Größen-Info)
  - ConcreteVisualization: Mischverhältnis-Leiste (Zement:Sand:Kies farbige Segmente mit Labels, konfigurierbar via Parameter)
  - GardenVisualization Erdschichten: 3-Schichten-Profil (Mutterboden+Sand+Kies), Grasnarbe mit Halmen, Wurzel-Andeutungen, Stein-Textur im Kies, Schicht-Labels rechts
- **13.02.2026**: Crash-Fix: Spinning-Animation (Export-Icon) nutzte `RenderTransform` in KeyFrame → "No animator registered" Crash beim App-Start. Fix: `RotateTransform.Angle` statt `RenderTransform` in KeyFrames + `RenderTransformOrigin="50%,50%"`. Avalonia KeyFrames unterstützen NUR double-Properties (Opacity, Angle, Width etc.), NICHT RenderTransform/TransformOperations.
- **13.02.2026**: UI/UX Überarbeitung (Game Juice):
  - MainView: Hero-Header Gradient, Premium-Card mit Shimmer direkt unter Hero, PRO-Badges (GoldGlow) auf 6 Premium-Cards
  - TapScaleBehavior + FadeInBehavior (Stagger 0-660ms) auf allen 11 Calculator-Cards + Premium/History-Cards
  - CSS-Animationen: GoldGlow (3s Loop), PremiumShimmer (2.5s), Spinning (Export-Icon)
  - Calculator-Farbpalette: 11 individuelle Farben pro Rechner (Amber, Violet, Grün, Blau, Grau, Rot, Orange, Stahl, Emerald, Cyan, Purple)
  - Premium-Views Konsistenz: Share+Export aus Bottom-Bar in Result-Cards verschoben (5 Views: Drywall, Electrical, Metal, Garden, RoofSolar)
  - ProjectsView: EmptyStateView (Shared Control) statt manuelles StackPanel, TapScale+FadeIn auf Projekt-Cards
  - Neue Shared Behaviors: TapScaleBehavior, FadeInBehavior, StaggerFadeInBehavior, CountUpBehavior (MeineApps.UI)
- **13.02.2026**: Double-Back-to-Exit: Android-Zurücktaste navigiert schrittweise zurück (Overlays→SaveDialog→Calculator→Home-Tab), App schließt erst bei 2x schnellem Drücken auf Home. HandleBackPressed() in MainViewModel (plattformunabhängig), OnBackPressed()-Override in MainActivity mit Toast-Hinweis. Overlay-Kette: PremiumAccess→ExtendedHistory→SaveDialog→Calculator→Tab→Home→Exit. Neuer RESX-Key "PressBackToExit" in 6 Sprachen.
- **13.02.2026**: Vierter Pass: Result-Daten + Code-Qualität:
  - Result-Daten in ConfirmSaveProject: Alle 11 VMs speichern jetzt Results im Projekt-Dictionary
  - ProjectsVM.ExportProject nutzt Result-Daten im PDF (war vorher nur Inputs)
  - BUG: MainVM Lambda-Events (4x) konnten nicht unsubscribed werden → benannte Handler + Dispose
  - BUG: HistoryItem DisplayDate zeigte UTC statt Lokalzeit → `CreatedAt.ToLocalTime()`
  - OPT: SettingsVM + ProjectsVM Transient → Singleton (waren unnötig Transient)
  - OPT: Calculators-Property in 3 Premium-VMs gecacht (`??=` statt neue Liste pro Zugriff)
  - OPT: ProjectService Read-Methoden mit Semaphore-Lock (Race-Condition bei parallelen Reads)
  - OPT: Tote `RestorePurchases`-Methode aus MainVM entfernt (nur in SettingsVM gebraucht)
  - OPT: `GC.SuppressFinalize` aus 3 Dispose-Methoden entfernt (kein Finalizer vorhanden)
  - OPT: `ResultTilesWithReserve` Lokalisierung in alle 6 Sprachen nachgerüstet
  - Duplicate `MoreCategoriesLabel` OnPropertyChanged entfernt
- **13.02.2026**: Dritter Pass: Lokalisierung, Views, UI-Konsistenz:
  - BUG: ConcreteVM PDF-Export nutzte nicht-existierenden Key `ResultCement` → `ResultCite`
  - BUG: FlooringView `TilesWithWaste` Label für Dielen → `BoardsWithWaste`
  - BUG: FlooringView Board-Sektion nutzte `RoomLength/RoomWidth` Labels → `BoardLength/BoardWidth`
  - BUG: WallpaperView Sektionstitel `WallLength` → `WallDimensions`
  - BUG: WallpaperView Strips-Zeile nutzte `RollsNeeded` wie Rollen → neuer Key `StripsNeeded` (6 Sprachen)
  - ShareResult-Button + IsExporting-Binding in alle 9 fehlenden Calculator Views nachgerüstet
  - Premium-Views UI-Konsistenz: Header (Background+Border), Button-Icons (Reset+Calculate), ScrollViewer-Padding
  - DrywallView: Save-Button `IsVisible="{Binding HasResult}"` + Icon hinzugefügt
- **13.02.2026**: Zweiter Bugfix + Optimierungs-Pass:
  - KRITISCH: ProjectsViewModel fehlte Routes für Beton+Treppen → Projekte konnten nicht geöffnet werden
  - BUG: CalculationHistoryService nutzte DateTime.Now statt DateTime.UtcNow
  - BUG: ProjectService Race Condition bei parallelen Saves (Semaphore umschließt jetzt gesamte Operation)
  - BUG: RoofSolar TileCostDisplay + PDF-Export rechnete ohne 5% Reserve (TilesWithReserve)
  - KONSISTENZ: ClipboardRequested-Deklaration in ConcreteVM + StairsVM nach oben verschoben
  - OPT: CalculationHistoryService Thread-Safety (SemaphoreSlim hinzugefügt)
  - OPT: CraftEngine Treppen-Konstanten als benannte Konstanten (DIN 18065)
- **12.02.2026**: Bugfixes + Konsistenz-Pass:
  - BUG: GardenVM JointWidth konnte negativ sein → Division/0 (Guard hinzugefügt)
  - BUG: DrywallVM Reset() setzte PricePerSqm nicht zurück
  - BUG: GardenVM PavingCostDisplay nutzte StonesNeeded statt StonesWithReserve (inkonsistent mit PDF)
  - BUG: 6 Premium-VMs SaveProjectName nicht mit DefaultProjectName vorbefüllt (nur bei neuen Projekten)
  - BUG: CraftEngine fehlende Division/0 Guards (Paint, Wallpaper, Soil, Paving)
  - ShareResult (Quick-Share via Clipboard) in alle 9 restlichen VMs nachgerüstet (vorher nur Beton+Treppen)
  - CraftEngine: ThreadDrill-Dictionary auf static readonly umgestellt (Performance)
  - MaterialExportService: Graphics null-safe Dispose
- **12.02.2026**: 2 neue Rechner hinzugefügt:
  - Beton-Rechner (Free): 3 Sub-Rechner (Platte, Streifenfundament, Säule), Volumen, Fertigbeton-Säcke, Selbstmischung (Zement/Sand/Kies/Wasser), Kosten
  - Treppen-Rechner (Premium): DIN 18065, Schrittmaßregel, Stufenhöhe/-tiefe, Lauflänge, Steigungswinkel, Komfort-Bewertung
  - CraftEngine: CalculateConcrete() + CalculateStairs() mit ConcreteResult/StairsResult Records
  - MainViewModel: Neue Routes (ConcretePage, StairsPage), Navigation, Wiring, IsPremiumRoute
  - 6 Sprachen: Alle neuen Keys in DE/EN/ES/FR/IT/PT
  - UX: IsExporting (Loading-State) in allen 11 VMs, ShareResult (Quick-Share) initial in Beton+Treppen (später auf alle 11 erweitert)
  - UI: TextBox :error Validation-Styles (rote Border), ClipboardRequested Event-Chain (VM→MainVM→View)
- **12.02.2026**: Umfangreicher Bugfix-Pass (14 Fixes):
  - KRITISCH: Premium-Bypass via Projekt-Laden behoben (IsPremiumRoute-Check in OnProjectNavigation)
  - CraftEngine: Defensive Guards (Division-durch-0, Sqrt-NaN, negative innerR bei Metallprofilen, Baseboard Math.Max)
  - Validierungen: WastePercentage >= 0 (Tile+Flooring), PatternRepeat >= 0 (Wallpaper), PanelEfficiency 0-100 + TiltDegrees 0-90 (Solar), HoursPerDay max 24 (Elektro), WallThickness < halber Durchmesser (Metal), Overlap >= 0 (Garten), OhmsLaw negative R/P abgelehnt
  - Projektliste: Automatischer Refresh bei Tab-Wechsel (SelectProjectsTab)
  - PDF-Export: Seitenumbruch-Logik bei vielen Einträgen (MaterialExportService)
  - Compiler: async→void bei DrywallVM + RoofSolarVM SaveProject (CS1998)
- **11.02.2026**: Optimierungen & Fixes:
  - CraftEngine: Fehlende P+R Kombination im Ohm'schen Gesetz ergaenzt
  - RoofSolarVM: Konfigurierbarer Strompreis (PricePerKwh) statt hardcoded 0.30
  - Project.GetValue(): JSON-Deserialisierungs-Caching (vermeidet wiederholtes Parsen)
  - SaveProject: async→void (9 VMs) - kein await, kein CS1998-Warning
  - Waehrungssymbol lokalisierbar: CurrencySymbol resx-Key statt hardcoded EUR in allen 9 VMs
  - UriLauncher: Process.Start ersetzt (Android PlatformNotSupportedException)
- **10.02.2026**: FileProvider-Infrastruktur hinzugefuegt (AndroidManifest, file_paths.xml, AndroidFileShareService.cs Link, FileShareServiceFactory), ACCESS_NETWORK_STATE Permission
- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay, Export-Buttons (9 Calculator + Projects), Extended-History
- **07.02.2026**: 4 Rewarded Ad Features, Design-Redesign, AppChecker Fixes
- **06.02.2026**: Vollstaendige Lokalisierung, Deep Code Review (36x Debug.WriteLine entfernt)
