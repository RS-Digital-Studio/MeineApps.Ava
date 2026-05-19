# HandwerkerRechner (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

Handwerker-App mit 19 Rechnern (5 Free Floor + 14 Premium-Tools), Projektverwaltung,
Angebots-Generator, Vorlagen und Einheiten-Umrechnung. Premium-Modell = nur "remove_ads".

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.handwerkerrechner` |
| Version | v2.0.7 (VersionCode 22) |
| Theme | Blueprint Professional (`#3B82F6` Blau) |
| Premium | 3,99 EUR `remove_ads` (alle 19 Rechner sind frei zugänglich) |
| Ad-Placements | `material_pdf`, `project_export` (Rewarded) |

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `HandwerkerRechner.Shared` | `net10.0` | `dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Shared` |
| `HandwerkerRechner.Desktop` | `net10.0` | `dotnet run --project src/Apps/HandwerkerRechner/HandwerkerRechner.Desktop` |
| `HandwerkerRechner.Android` | `net10.0-android` | `dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Android` |

Release-AAB: `dotnet publish src/Apps/HandwerkerRechner/HandwerkerRechner.Android -c Release`

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `HandwerkerRechner.Shared/ViewModels/` | `HandwerkerRechner.ViewModels` |
| `HandwerkerRechner.Shared/Views/` | `HandwerkerRechner.Views` |
| `HandwerkerRechner.Shared/Services/` | `HandwerkerRechner.Services` |
| `HandwerkerRechner.Shared/Models/` | `HandwerkerRechner.Models` |
| `HandwerkerRechner.Shared/Graphics/` | `HandwerkerRechner.Graphics` |
| `HandwerkerRechner.Shared/Loading/` | `HandwerkerRechner.Loading` |

---

## Architektur

### Calculator-Kategorien

**Floor (Free, 5):** Tile, Wallpaper, Paint, Flooring, Concrete
**Premium (14):** Drywall, Electrical, Metal, Garden, RoofSolar, Stairs, Plaster, Screed,
Insulation, CableSizing, Grout, HourlyRate, MaterialCompare, AreaMeasure

### Tab-Navigation (4 Tabs)

| Tab | Inhalt |
|-----|--------|
| Home | Rechner-Übersicht (Kategorien Floor/Premium) |
| Projects | Projektverwaltung (CRUD, Foto-Dokumentation) |
| History | Berechnungshistorie gruppiert nach Rechner-Typ |
| Settings | Einstellungen |

### Calculator-VM Factory-Pattern

- 19 Calculator-VMs als **Transient** in DI registriert.
- `MainViewModel` erhält `Func<T>` Factories per Constructor-Injection (kein Service-Locator).
- Jedes Öffnen eines Rechners erzeugt frische VM-Instanz via `_xxxVmFactory()`.
- `Func<T>` Factories als **Singleton** in `App.axaml.cs` registriert.

### Calculator-Overlay via ViewLocator

- `MainViewModel`: `CurrentPage` + `CurrentCalculatorVm` Properties.
- `MainView` hat KEINE lokalen DataTemplates — globaler `ViewLocator` (App.axaml) löst alle
  19 Calculator-VMs per Konvention auf:
  `ViewModels.Floor.TileCalculatorViewModel` → `Views.Floor.TileCalculatorView`.
- Tab-Wechsel: `SelectHomeTab/SelectProjectsTab/SelectHistoryTab/SelectSettingsTab` setzen
  `CurrentPage = null`.
- Render-Performance: BlueprintBackground-Timer pausiert wenn `CurrentPage != null`
  (Hintergrund verdeckt → keine GPU-Arbeit).

### `ICalculatorViewModel` Interface

Alle 19 Calculator-VMs implementieren `ICalculatorViewModel`:

```csharp
NavigationRequested, MessageRequested, FloatingTextRequested, ClipboardRequested,
ShowSaveDialog, Cleanup(), LoadFromProjectIdAsync()
```

`MainViewModel` nutzt Interface-Polymorphie statt 19-facher switch/case-Blöcke für
`WireCalculatorEvents`, `CleanupCurrentCalculator`, `HandleBackPressed`. Factory-Dictionary
`_calculatorFactories` (Route → `Func<ObservableObject>`) ersetzt 19 einzelne Func<T>-Fields.

### Floor vs Premium VM-Hierarchie

- Floor-VMs (5): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Floor`
- Premium-VMs (14): erben von `ViewModelBase`, Namespace `HandwerkerRechner.ViewModels.Premium`
- Alle 19 VMs implementieren `ICalculatorViewModel` + `IDisposable` (räumen Debounce-Timer
  + Event-Subscriptions auf).
- Alle 19 VMs haben `[ObservableProperty] private bool _isCalculating;` als
  Reentrancy-Schutz in `Calculate()`.

---

## Domänen-Patterns

### Typsicheres Enum-Routing in Plaster/Screed

`PlasterType` / `ScreedType` Enums statt deutscher Strings — typsicher gegen Refactor-Fallen.

### Drehstrom-Erkennung in CableSizing/VoltageDrop

`Voltage >= 380V` → Faktor `√3` statt `2` (DIN-konform für 400V-Drehstrom).

### Project-Templates (`ProjectTemplateService`)

Built-in Templates verwenden EXAKT die Property-Keys, die die Calculator-VMs in
`LoadProjectAsync` via `project.GetValue<T>("Key")` erwarten.
`ProjectTemplatesViewModel.ParseTemplateValue` castet String-Defaults nach `bool`/`int`/`double`
damit der JSON-Roundtrip funktioniert.

### Renderer-Lokalisierung

4 Visualisierungen (Tile/Grout/Garden/Concrete) holen Labels via
`LocalizationManager.Service?.GetString("VizXxx")`. **Hardcoded deutsche Strings sind verboten**
(englische User würden „Verschnitt" statt „Waste" sehen).

### `CraftEngine.Clamp(value, max)`

Plausibilitäts-Bounds gegen `Infinity`/`NaN` aus User-Inputs. Pattern in `CalculateTiles`
etabliert; bei Bedarf erweitern.

### Pre-Computed Random Arrays in Visualisierungen

`InsulationVisualization` nutzt `_epsRandoms`/`_woodFiberRandoms` als pre-computed static
Arrays — `Random.Shared.NextDouble()` pro Frame würde sichtbar wackeln.

### Live-Berechnung (Debounce)

- Alle 19 Calculator-VMs berechnen automatisch 300ms nach letzter Eingabe-Änderung.
- Pattern: `partial void OnXxxChanged() => ScheduleAutoCalculate()` +
  `Timer.Change()` (wiederverwendet statt Dispose/New).
- Timer-Callback: `Dispatcher.UIThread.Post(() => _ = Calculate())` (async Task → fire-and-forget Lambda).
- History-Save NICHT pro Live-Calculate-Iteration (10-30ms Stutter pro Tastendruck) →
  `ScheduleDebouncedSave` mit 2s-Debounce.
- `Reset()` disposed Timer, alle VMs implementieren `IDisposable`.
- `Cleanup()` ist API-konsistent über alle 19 VMs.

### UI-Feedback (Seriöse App, kein Confetti)

`FloatingText` für dezentes Feedback bei Aktionen (Save, Export, Clipboard).

---

## Services

### App-spezifisch (`HandwerkerRechner.Shared/Services/`)

| Service | Zweck |
|---------|-------|
| `ICalculatorFactoryService` / `CalculatorFactoryService` | Factory-Dictionary `Route → Func<ObservableObject>` für alle 19 Calculator-VMs (ersetzt switch/case in MainViewModel) |
| `IProjectService` / `ProjectService` | JSON-Persistenz Project-Model, `DateTime.UtcNow`, SemaphoreSlim-Locks |
| `IProjectTemplateService` / `ProjectTemplateService` | Built-in + Eigene Templates, Property-Key-Konsistenz mit Calculator-VM `LoadProjectAsync` |
| `IQuoteService` / `QuoteService` | Angebots-Generator (Kundendaten, Positionen, Marge + MwSt, PDF-Export) |
| `IFavoritesService` / `FavoritesService` | Favorisierte Calculator (Reihenfolge merken) |
| `IMaterialExportService` / `MaterialExportService` | PDF (PdfSharpCore A4) + CSV (Semikolon, UTF-8-BOM, Excel-kompatibel). Lokalisierte Header. Formula-Injection-Schutz in `EscapeCsv()` (führendes `=`/`+`/`-`/`@` mit Apostroph präfixt) |
| `IMaterialPriceService` / `MaterialPriceService` | Material-Preise pro Region/Land (für Kostenschätzungen) |
| `IPhotoPickerService` / `DesktopPhotoPickerService` | Foto-Auswahl via StorageProvider, kopiert nach AppData/photos/ mit GUID-Name. Path-Traversal-Schutz in `DeletePhotoAsync` (`Path.GetFullPath`-Vergleich mit erwartetem PhotoDirectory). Android-Override via Factory in `App.axaml.cs` |

### Aus `MeineApps.Core.Ava` (Shared)

| Service | Zweck |
|---------|-------|
| `CalculationHistoryService` | History pro Rechner (max 30 Items), 2s-Debounce-Save, parallel-load mit `Task.WhenAll`, static `JsonSerializerOptions` |
| `IUnitConverterService` / `UnitConverterService` | Länge, Fläche, Volumen, Gewicht (Metrisch/Imperial) |

---

## Features

### Projektverwaltung

CRUD mit JSON-Persistenz, Foto-Dokumentation (max 5 Fotos pro Projekt im Notizen-Editor,
Desktop: StorageProvider, Android: Factory).

### History-Tab

`HistoryViewModel` lädt alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach
CalculatorId. `HistoryView` mit Expander pro Rechner-Typ + SwipeToReveal zum Löschen.
- **Free**: 5 Einträge pro Rechner
- **Extended (24h per Rewarded Ad)**: 30 Einträge
- Tap auf Eintrag öffnet entsprechenden Rechner (mit Premium-Check via `IsPremiumRoute`)

### Material-Liste Export

PDF (A4 mit Header/Inputs/Results/Footer) + CSV (Semikolon, UTF-8-BOM). Alle 19 Calculator-VMs
haben `ExportCsvCommand` (gleiche Inputs/Results wie PDF, gleiches Ad-Gate).

### Angebots-Generator (`QuoteView`)

Kundendaten, Positions-Tabelle, Marge + MwSt, PDF-Export.

### Projekt-Vorlagen (`ProjectTemplatesView`)

2 Sektionen (Eingebaut/Eigene), Anwenden-Dialog.

### Projekt-/History-Navigation

`ProjectsVM.NavigationRequested` → `MainVM.OnProjectNavigation` (mit Premium-Check).
`HistoryVM.NavigationRequested` → `MainVM.OnHistoryNavigation` (mit Premium-Check).
`SelectProjectsTab` löst automatisch Reload der Projektliste aus.

---

## SkiaSharp Visualisierungen (`Graphics/`)

18 statische Renderer-Klassen + 1 Splash-Renderer + 1 Background-Renderer in
`HandwerkerRechner.Shared/Graphics/`, alle nutzen `SkiaBlueprintCanvas` + `SkiaThemeHelper`
aus `MeineApps.UI`.

### Renderer-Übersicht

| Datei | Typ | Beschreibung |
|-------|-----|--------------|
| `BlueprintBackgroundRenderer` | Background | Animiert (5 Layer: Gradient, Blueprint-Grid mit Drift, Maßband-Markierungen, 8 Tool-Silhouetten, Vignette). Instance-basiert, IDisposable, 0 GC/Frame, ~5 FPS DispatcherTimer |
| `TileVisualization` | Floor | 2D-Grundriss mit Fliesengitter, Verschnitt-Fliesen rot schraffiert |
| `FlooringVisualization` | Floor | Dielen-Verlegung mit 50%-Versatz, 3 Holzfarben |
| `WallpaperVisualization` | Floor | Wand-Abwicklung mit vertikalen Bahnen, Rapport-Versatz gestrichelt |
| `PaintVisualization` | Floor | Wand mit Farbschichten + Kannen-Icons (max 10, ×N bei mehr) |
| `ConcreteVisualization` | Floor | 3 Sub-Typen + Mischverhältnis-Leiste (Zement:Sand:Kies farbig) |
| `StairsVisualization` | Premium | Seitenansicht Treppenprofil, Winkel-Arc, DIN-Farbcode |
| `RoofSolarVisualization` | Premium | 3 Sub-Typen: Dachdreieck+Winkel, Ziegelraster, Solar-Panel-Layout mit Kompass |
| `DrywallVisualization` | Premium | Wandschnitt mit CW/UW-Ständerwerk, Plattenaufteilung |
| `ElectricalVisualization` | Premium | 3 Sub-Typen: Spannungsabfall-Kurve, Kosten-Balken, Ohmsches Dreieck |
| `MetalVisualization` | Premium | 2 Sub-Typen: 6 Profil-Querschnitte, Gewindebohrung-Kreis |
| `GardenVisualization` | Premium | 3 Sub-Typen: Pflastermuster, Erdschichten-Profil (3 Schichten + Grasnarbe + Wurzeln + Stein-Textur), Teichfolie-Draufsicht |
| `PlasterVisualization` | Premium | Wandquerschnitt mit Mauerwerk-Textur + proportionaler Putzschicht, Bemaßung, Sack-Info |
| `ScreedVisualization` | Premium | Bodenquerschnitt mit Untergrund (Kies-Textur) + Estrichschicht (proportional, farblich nach Typ) |
| `InsulationVisualization` | Premium | Wandquerschnitt mit Mauerwerk + Dämmschicht (4 Materialtypen: EPS-Kreise, XPS-Linien, Mineralwolle-Wellen, Holzfaser-Striche) |
| `CableSizingVisualization` | Premium | Kabelquerschnitt (Kupfer/Alu mit Isolierung), Spannungsabfall-Balken (VDE-Grenzlinie) |
| `GroutVisualization` | Premium | Fliesengitter mit proportionalen Fugenlinien, Bemaßungen, Info-Box |
| `CostBreakdownVisualization` | Shared | Horizontale gestapelte Kostenbalken mit Segmenten, Prozent-Labels, Legende. Wiederverwendbar |
| `MaterialStackVisualization` | Shared | Material-Icon-Reihe (10 Typen: Eimer, Sack, Rolle, Paket, Box, Platte, Kabel, Stange) mit Mengen |
| `HandwerkerRechnerSplashRenderer` | Splash | "Das Maßband": Holz-Hintergrund + gelbes Maßband als Fortschrittsbalken (cm-Markierungen, Bleistift), 12 Sägespäne-Partikel. Erbt von `SplashRendererBase` |

### Renderer-Pattern

Alle `public static void Render(SKCanvas, SKRect, ...)` mit gecachten `SKPaint`
(`static readonly`), inklusive `_layerPaint` für Alpha-Fade-In (`SaveLayer`).

Views haben `OnPaintVisualization`-Code-Behind-Handler mit Named-Handler-Pattern
(explizites Unsubscribe bei DataContext-Wechsel). Visualisierung in
`<Border Classes="Card" Height="220" ClipToBounds="True">` mit `IsVisible="{Binding HasResult}"`.

### `CalculatorViewBase`

Abstrakte Basisklasse für alle 19 Calculator-Views. Kapselt PropertyChanged-Subscription-Pattern
(`_currentVm`/`_resultHandler`, An-/Abmeldung bei DataContext-Wechsel). Abgeleitete Klassen
überschreiben:

- `ShouldInvalidateOnPropertyChanged(propertyName)`: Filter-Logik (Standard: `Contains("Result")`)
- `OnResultPropertyChanged()`: Reaktion (Animation starten oder Canvas invalidieren)
- `RequestAnimationFrame(sender)`: Statische Hilfsmethode für Animation-Loop

### Background-Render-Loop

`MainView`: DispatcherTimer 200ms (~5 FPS), `_backgroundRenderer.Update(0.2f)` +
`BackgroundCanvas.InvalidateSurface()`. SKCanvasView mit `Grid.RowSpan="3"` +
`IsHitTestVisible="False"` hinter Content. UserControl `Background=Transparent`
(Gradient kommt vom Renderer). Start in `OnDataContextChanged`, Stop+Dispose in
`OnDetachedFromVisualTree`.

### Animation-Pattern

17 Renderer erben von `AnimatedVisualizationBase` (`StartAnimation`/`NeedsRedraw`).
3 Renderer (HourlyRate, MaterialCompare, AreaMeasure) sind statisch ohne Animation —
Code-Behind ruft direkt `InvalidateSurface()` bei PropertyChanged.

---

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Build-Befehle, Conventions, Troubleshooting
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — `SkiaBlueprintCanvas`, `SkiaThemeHelper`
- [MeineApps.Core.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) — `CalculationHistoryService`
- [MeineApps.CalcLib/CLAUDE.md](../../Libraries/MeineApps.CalcLib/CLAUDE.md) — `CraftEngine`
- `Releases/HandwerkerRechner/CHANGELOG_*.md` — Release-Notes
