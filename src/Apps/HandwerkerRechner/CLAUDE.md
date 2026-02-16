# HandwerkerRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Handwerker-App mit 11 Rechnern (5 Free Floor + 6 Premium), Projektverwaltung und Einheiten-Umrechnung.

**Version:** 2.0.0 | **Package-ID:** com.meineapps.handwerkerrechner | **Status:** Geschlossener Test

## Features

### 11 Rechner in 2 Kategorien

**Free Floor Calculators (5):**
1. TileCalculator - Fliesenbedarf (Raum, Verschnitt, Fugenmasse)
2. WallpaperCalculator - Tapetenrollen (Wandhoehe, Muster-Rapport)
3. PaintCalculator - Farbbedarf (Anstriche, Deckfaehigkeit)
4. FlooringCalculator - Laminat/Parkett (Raumform, Verschnitt)
5. ConcreteCalculator - Beton (Platte/Fundament/Säule, Volumen, Säcke, Mischverhältnis)

**Premium Calculators (6):**
6. DrywallCalculator - Trockenbau (Platten, Profile, Schrauben)
7. ElectricalCalculator - Elektrik (Kabel, Kosten, Ohm'sches Gesetz)
8. MetalCalculator - Metall (Gewicht, Gewindegroesse, Bohrung)
9. GardenCalculator - Garten (Erde, Mulch, Pflaster, Rasen)
10. RoofSolarCalculator - Dach+Solar (Dachflaeche, Solarpanel, Amortisation)
11. StairsCalculator - Treppen (DIN 18065, Schrittmaß, Stufenhöhe, Komfort)

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
1. **premium_access**: 30 Minuten Zugang zu 6 Premium-Rechnern (HomeView)
2. **extended_history**: 24h-Zugang zu 30 statt 10 History-Eintraegen (HomeView)
3. **material_pdf**: Material-Liste PDF Export (alle 11 Calculator Views)
4. **project_export**: Projekt-Export als PDF (ProjectsView)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads, permanenter Premium-Rechner-Zugang, unbegrenzte History, direkter PDF-Export

## SkiaSharp Visualisierungen (Graphics/)

11 statische Renderer-Klassen in `HandwerkerRechner.Shared/Graphics/`, alle nutzen `SkiaBlueprintCanvas` + `SkiaThemeHelper` aus MeineApps.UI.

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
| `TileVisualization.cs` | Floor | 2D-Grundriss mit Fliesengitter, Verschnitt-Fliesen rot schraffiert, Maßlinien |
| `FlooringVisualization.cs` | Floor | Dielen-Verlegung mit 50%-Versatz, 3 Holzfarben abwechselnd, Maßlinien |
| `WallpaperVisualization.cs` | Floor | Wand-Abwicklung mit vertikalen Bahnen, Rapport-Versatz gestrichelt |
| `PaintVisualization.cs` | Floor | Wand mit semi-transparenten Farbschichten pro Anstrich |
| `ConcreteVisualization.cs` | Floor | 3 Sub-Typen: Platte (Isometrie), Fundament (Schnitt), Säule (Zylinder) |
| `StairsVisualization.cs` | Premium | Seitenansicht Treppenprofil, Winkel-Arc, DIN-Farbcode (Grün/Gelb/Rot) |
| `RoofSolarVisualization.cs` | Premium | 3 Sub-Typen: Dachdreieck+Winkel, Ziegelraster, Solar-Panel-Layout mit Kompass |
| `DrywallVisualization.cs` | Premium | Wandschnitt mit CW/UW-Ständerwerk, Plattenaufteilung |
| `ElectricalVisualization.cs` | Premium | 3 Sub-Typen: Spannungsabfall-Kurve, Kosten-Balken, Ohmsches Dreieck |
| `MetalVisualization.cs` | Premium | 2 Sub-Typen: 6 Profil-Querschnitte, Gewindebohrung-Kreis |
| `GardenVisualization.cs` | Premium | 3 Sub-Typen: Pflastermuster, Erdschichten-Profil, Teichfolie-Draufsicht |

**Pattern**: Alle `public static void Render(SKCanvas, SKRect, ...)` mit gecachten `SKPaint` (static readonly). Views haben `OnPaintVisualization` Code-Behind Handler. Visualisierung in `<Border Classes="Card" Height="220" ClipToBounds="True">` mit `IsVisible="{Binding HasResult}"`.

## Besondere Architektur

### Calculator Overlay via DataTemplates
- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties
- `MainView`: DataTemplates fuer automatische View-Zuordnung per VM-Typ (11 VMs)
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectSettingsTab` setzen `CurrentPage=null`

### Projekt-Navigation
- ProjectsVM.NavigationRequested → MainVM.OnProjectNavigation (mit Premium-Check via `IsPremiumRoute`)
- WireCalculatorEvents: Per switch/case (kein gemeinsames Interface)
- SelectProjectsTab löst automatisch Reload der Projektliste aus

### Floor vs Premium VMs
- Beide erben direkt von `ObservableObject` (nicht von CalculatorViewModelBase)
- CalculatorViewModelBase existiert als Abstract Base Class, wird aber NICHT verwendet

### Game Juice
- **FloatingText**: "Projekt wurde gespeichert!" nach ConfirmSaveProject
- **Celebration**: Confetti bei erfolgreichem Save

## Changelog (Highlights)

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
