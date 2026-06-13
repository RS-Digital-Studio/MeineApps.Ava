# ViewModels — Navigation, Calculator-Logik, Business-VMs

Alle ViewModels leben in `HandwerkerRechner.ViewModels` (Haupt-VMs),
`HandwerkerRechner.ViewModels.Floor` (5 Free) und `HandwerkerRechner.ViewModels.Premium` (14 Premium).
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Kern-Partial: Tab-Navigation (4 Tabs), Calculator-Overlay via `CurrentPage`/`CurrentCalculatorVm`, Back-Press, Rewarded-Counter, Premium-Status, Event-Wiring/Dispose. |
| `MainViewModel.Favorites.cs` | Favoriten-Partial: `FavoriteCalculators`-Collection, Toggle-/Open-Commands, 19 `IsFavXxx`-Properties, `GetCalculatorInfo` (`FavoriteItem`-Record bleibt in `MainViewModel.cs`). |
| `MainViewModel.Localization.cs` | Localization-Partial: `TabXxxText`-Properties, `UpdateNavTexts`/`UpdateHomeTexts`, `LocalizedPropertyNames`-Array, `OnLanguageChanged`, alle Label-Properties. |
| `ICalculatorViewModel.cs` | Interface für alle 19 Calculator-VMs: `NavigationRequested`, `MessageRequested`, `FloatingTextRequested`, `ClipboardRequested`, `CalculationPerformed`, `ShowSaveDialog`, `Cleanup()`, `LoadFromProjectIdAsync()`. |
| `SettingsViewModel.cs` | Sprache, Region, Einheiten, Feedback-Request. |
| `ProjectsViewModel.cs` | CRUD Projektliste, Foto-Verwaltung, Navigation zu Rechner mit Projekt-Daten. |
| `HistoryViewModel.cs` | Alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach CalculatorId. |
| `ProjectTemplatesViewModel.cs` | Built-in/Eigene Vorlagen, Anwenden-Dialog. |
| `QuoteViewModel.cs` | Angebots-CRUD, Positionen, MwSt/Marge, PDF-Export. |
| `Floor/TileCalculatorViewModel.cs` | Fliesenbedarf (Fläche, Verschnitt). |
| `Floor/WallpaperCalculatorViewModel.cs` | Tapetenbedarf (Rapport-Berechnung) — Kern: Ctor, Debounce, `Calculate`, Reset, Cleanup/Dispose. |
| `Floor/WallpaperCalculatorViewModel.Properties.cs` | Input-/Cost-/Result-Properties, Unit-Labels. |
| `Floor/WallpaperCalculatorViewModel.Persistence.cs` | Save-Dialog, Projekt speichern/laden, History-Save. |
| `Floor/WallpaperCalculatorViewModel.Export.cs` | Clipboard-Share, PDF-/CSV-Export. |
| `Floor/PaintCalculatorViewModel.cs` | Farbbedarf (Fläche, Ergiebigkeit, Anstriche). |
| `Floor/FlooringCalculatorViewModel.cs` | Dielenbedarf (Länge, Breite, Versatz). |
| `Floor/ConcreteCalculatorViewModel.cs` | Betonbedarf (Platte, Fundament, Rundsäule) — Kern: Ctor, Debounce, `Calculate`, Reset, Cleanup/Dispose. |
| `Floor/ConcreteCalculatorViewModel.Properties.cs` | Sub-Rechner-Auswahl, Input-/Cost-/Result-Properties, Unit-Labels. |
| `Floor/ConcreteCalculatorViewModel.Persistence.cs` | Save-Dialog, Projekt speichern/laden, History-Save. |
| `Floor/ConcreteCalculatorViewModel.Export.cs` | Clipboard-Share, PDF-/CSV-Export. |
| `Premium/DrywallViewModel.cs` | Trockenbau (CW/UW-Profile, Platten, Schrauben). |
| `Premium/ElectricalViewModel.cs` | Spannungsabfall, Stromkosten, Ohmsches Gesetz — Kern: Ctor, `Defaults`, Debounce, `Calculate`, Persistenz, Export. |
| `Premium/ElectricalViewModel.VoltageDrop.cs` | Sub-Rechner Spannungsabfall: Inputs, Kabelkosten, `VoltageDropResult`. |
| `Premium/ElectricalViewModel.PowerCost.cs` | Sub-Rechner Stromkosten: Inputs, `PowerCostResult`. |
| `Premium/ElectricalViewModel.OhmsLaw.cs` | Sub-Rechner Ohmsches Gesetz: String-Inputs, `OhmsLawResult`, `ParseDecimal`. |
| `Premium/MetalViewModel.cs` | Metallgewicht + Gewindebohrung — Kern: Ctor, Debounce, `Calculate`, Persistenz, Export. |
| `Premium/MetalViewModel.Weight.cs` | Sub-Rechner Metallgewicht: Inputs (6 Profile, 6 Materialien), kg-Preis, `MetalWeightResult`. |
| `Premium/MetalViewModel.Thread.cs` | Sub-Rechner Gewindebohrung: `ThreadSizes`, `ThreadDrillResult`. |
| `Premium/GardenViewModel.cs` | Pflastersteine, Erde/Mulch, Teichfolie — Kern: Ctor, Debounce, `Calculate`, Persistenz, Export. |
| `Premium/GardenViewModel.Paving.cs` | Sub-Rechner Pflastersteine: Inputs, Stein-Preis, `PavingResult`. |
| `Premium/GardenViewModel.Soil.cs` | Sub-Rechner Erde/Mulch: Inputs, Sack-Preis, `SoilResult`. |
| `Premium/GardenViewModel.Pond.cs` | Sub-Rechner Teichfolie: Inputs, m²-Preis, `PondLinerResult`. |
| `Premium/RoofSolarViewModel.cs` | Dachneigung, Dachziegel, Solar-Ertrag — Kern: Ctor, Debounce, `Calculate`, Persistenz, Export. |
| `Premium/RoofSolarViewModel.Pitch.cs` | Sub-Rechner Dachneigung: Inputs, `RoofPitchResult`. |
| `Premium/RoofSolarViewModel.Tiles.cs` | Sub-Rechner Dachziegel: Inputs, Ziegel-Preis, `RoofTilesResult`. |
| `Premium/RoofSolarViewModel.Solar.cs` | Sub-Rechner Solar-Ertrag: Inputs, `Orientations`, Anlagenkosten/Amortisation, `SolarYieldResult`. |
| `Premium/StairsViewModel.cs` | Treppenmaße nach DIN 18065. |
| `Premium/PlasterViewModel.cs` | Putzbedarf nach Putztyp (PlasterType-Enum). |
| `Premium/ScreedViewModel.cs` | Estrichbedarf nach Estrichtyp (ScreedType-Enum). |
| `Premium/InsulationViewModel.cs` | Dämmstoffdicke via U-Wert-Berechnung. |
| `Premium/CableSizingViewModel.cs` | Mindest-Leitungsquerschnitt nach DIN VDE. |
| `Premium/GroutViewModel.cs` | Fugenmasse-Bedarf (Industrie-Formel). |
| `Premium/HourlyRateViewModel.cs` | Stundenverrechnungssatz (statisch, kein Timer). |
| `Premium/MaterialCompareViewModel.cs` | Material-Vergleich nach Preis/Menge (statisch). |
| `Premium/AreaMeasureViewModel.cs` | Aufmaß-Rechner (statisch). |

## MainViewModel — Architektur

**Tab-Navigation:** `SelectedTab` (0=Home, 1=Projects, 2=History, 3=Settings). `SelectXxxTab`-Commands
setzen `CurrentPage = null` und triggern bei Projects/History automatisch einen Reload.

**Calculator-Overlay:** `CurrentPage` (Route-String) → `OnCurrentPageChanged` →
`CleanupCurrentCalculator()` (altes Transient-VM disposen) → `CreateCalculatorVm(route)` via
`ICalculatorFactoryService` → `WireCalculatorEvents(vm)` verdrahtet `NavigationRequested`,
`MessageRequested`, `FloatingTextRequested`, `ClipboardRequested`, `CalculationPerformed`.

**Route-Format:** `"TileCalculatorPage"` (einfach) oder `"TileCalculatorPage?projectId=abc123"`
(mit Projekt-Daten). Routing mit Query-String-Parsing im `CreateCalculatorVm`.

**Rewarded-Counter (Opt-in):** `_calculationCount` wird bei jedem `CalculationPerformed`-Event
erhöht. Ab 3 Berechnungen → Inline-Opt-in-Overlay (`ShowAdOfferDialog`, kein erzwungenes Video —
AdMob-Policy). `WatchAdCommand` → `ShowAdAsync("calculation_ad")`; bei Erfolg
`_adFreeCalculationsRemaining = 10` + `HideBanner()` — pro Berechnung dekrementiert, bei 0
`ShowBanner()` (session-only, keine Persistenz). `DeclineAdOfferCommand` → Counter zurück auf 0,
nächstes Angebot nach 3 weiteren Berechnungen. Premium-User: kein Counter, kein Angebot.

**Favoriten:** `IFavoritesService` → `FavoriteCalculators`-Collection (ObservableCollection<FavoriteItem>).
20 `IsFavXxx`-Properties für Compiled-Binding-kompatibles Stern-Toggle.
`NotifyFavoriteProperties()` aktualisiert alle 20 Properties nach `FavoritesChanged`.

**Localization:** Gezielte Invalidierung via `LocalizedPropertyNames`-Array (nicht
`OnPropertyChanged(string.Empty)` — das würde alle Bindings im Visual-Tree auslösen → 50-150ms Stutter).

**Back-Navigation:** 1. App-Overlays schließen (Message-Dialog, Ad-Angebot) → 2. Projekt-Dialoge
schließen (Lösch-Bestätigung, Notizen-Editor) → 3. SaveDialog schließen → 4. Calculator schließen →
5. Templates: Apply-Dialog schließen statt Seite → 6. Quotes: `GoBackCommand` (behandelt IsEditing) →
7. Nicht-Calculator-VM schließen → 8. Nicht-Home-Tab → Home → 9. Double-Back-to-Exit via `BackPressHelper`.

## ICalculatorViewModel — Vertrag

Alle 19 Calculator-VMs implementieren dieses Interface. Erlaubt Polymorphie statt switch/case:

```csharp
// MainViewModel nutzt Interface überall:
if (vm is ICalculatorViewModel calc)
{
    calc.NavigationRequested += OnCalculatorGoBack;
    calc.CalculationPerformed += OnCalculationPerformed;
    // ...
}
```

## Live-Berechnung (Debounce-Pattern)

Alle 19 Calculator-VMs berechnen automatisch 300 ms nach letzter Eingabe-Änderung:

```csharp
partial void OnXxxChanged() => ScheduleAutoCalculate();
// ScheduleAutoCalculate: Timer.Change(300ms) — wiederverwendet statt Dispose/New
// Timer-Callback: Dispatcher.UIThread.Post(() => _ = Calculate())
// History-Save: _historyService.ScheduleDebouncedSave(...) — Service übernimmt Debounce
```

`_isCalculating` als Reentrancy-Schutz in `Calculate()`. `Reset()` disposed Timer.
Alle VMs: `IDisposable` (Timer + Event-Subscriptions). `Cleanup()` ist API-Konsistenz über Interface.

## Domänen-Gotchas (VM-Ebene)

- **Drehstrom-Erkennung (Electrical/CableSizing):** `Voltage >= 380V` → Phasen-Faktor `√3`
  statt `2` (DIN VDE). VMs prüfen `IsThreePhase`-Flag, nicht die Spannungsgrenze direkt —
  beide müssen konsistent sein.
- **PlasterType/ScreedType Enum-Routing:** Tipp-sichere Enums statt String-Vergleich.
  `CalculateXxx(area, thickness, PlasterType.Gypsum)` — nie String-"Gipsputz" übergeben.
- **AreaMeasure Index→Enum-Mapping:** Die UI bindet `SelectedShapeIndex` (0–5), die Engine
  nimmt das `AreaShape`-Enum. Das Mapping lebt im `Calculate()`-switch des VM — Formeln
  (inkl. Negativ-Guards) liegen in `CraftEngine.CalculateShapeArea`.
- **Tür-/Fenster-Abzüge (Paint/Wallpaper):** gemeinsame Engine-Methode
  `CalculateOpeningsDeduction` — der `ShowDeductions`-Toggle bleibt UI-Logik im VM.
- **`GetString(key)` gibt NIEMALS null zurück:** `LocalizationService` gibt den Key-Namen zurück
  wenn der Key fehlt. `?? "fallback"` ist toter Code — fehlende RESX-Keys werden mit dem Key-Namen sichtbar.
- **ProjectsViewModel Lade-Guard:** `LoadProjectsAsync` prüft `IsLoading` gegen Doppel-Aufruf.
  Kein `_initTask`-Pattern — der Service ist zustandslos; Race-Schutz sitzt im `IProjectService`.
