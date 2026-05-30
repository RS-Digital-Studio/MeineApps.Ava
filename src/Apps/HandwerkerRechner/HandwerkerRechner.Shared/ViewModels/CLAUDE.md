# ViewModels — Navigation, Calculator-Logik, Business-VMs

Alle ViewModels leben in `HandwerkerRechner.ViewModels` (Haupt-VMs),
`HandwerkerRechner.ViewModels.Floor` (5 Free) und `HandwerkerRechner.ViewModels.Premium` (14 Premium).
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Tab-Navigation (4 Tabs), Calculator-Overlay via `CurrentPage`/`CurrentCalculatorVm`, Favoriten, Back-Press, Rewarded-Counter, Premium-Status, Localization-Relay. |
| `ICalculatorViewModel.cs` | Interface für alle 19 Calculator-VMs: `NavigationRequested`, `MessageRequested`, `FloatingTextRequested`, `ClipboardRequested`, `CalculationPerformed`, `ShowSaveDialog`, `Cleanup()`, `LoadFromProjectIdAsync()`. |
| `SettingsViewModel.cs` | Sprache, Region, Einheiten, Feedback-Request. |
| `ProjectsViewModel.cs` | CRUD Projektliste, Foto-Verwaltung, Navigation zu Rechner mit Projekt-Daten. |
| `HistoryViewModel.cs` | Alle History-Einträge via `GetAllHistoryAsync()`, gruppiert nach CalculatorId. |
| `ProjectTemplatesViewModel.cs` | Built-in/Eigene Vorlagen, Anwenden-Dialog. |
| `QuoteViewModel.cs` | Angebots-CRUD, Positionen, MwSt/Marge, PDF-Export. |
| `Floor/TileCalculatorViewModel.cs` | Fliesenbedarf (Fläche, Verschnitt). |
| `Floor/WallpaperCalculatorViewModel.cs` | Tapetenbedarf (Rapport-Berechnung). |
| `Floor/PaintCalculatorViewModel.cs` | Farbbedarf (Fläche, Ergiebigkeit, Anstriche). |
| `Floor/FlooringCalculatorViewModel.cs` | Dielenbedarf (Länge, Breite, Versatz). |
| `Floor/ConcreteCalculatorViewModel.cs` | Betonbedarf (Platte, Fundament, Rundsäule). |
| `Premium/DrywallViewModel.cs` | Trockenbau (CW/UW-Profile, Platten, Schrauben). |
| `Premium/ElectricalViewModel.cs` | Spannungsabfall, Stromkosten, Ohmsches Gesetz. |
| `Premium/MetalViewModel.cs` | Metallgewicht (6 Profile, 6 Materialien) + Gewindebohrung. |
| `Premium/GardenViewModel.cs` | Pflastersteine, Erde/Mulch, Teichfolie. |
| `Premium/RoofSolarViewModel.cs` | Dachneigung, Dachziegel, Solar-Ertrag. |
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

**Rewarded-Counter:** `_calculationCount` wird bei jedem `CalculationPerformed`-Event erhöht.
Ab 3 Berechnungen → `_rewardedAdService.ShowAdAsync("calculation_ad")` (außer bei Premium).

**Favoriten:** `IFavoritesService` → `FavoriteCalculators`-Collection (ObservableCollection).
19 `IsFavXxx`-Properties für Compiled-Binding-kompatibles Stern-Toggle.
`NotifyFavoriteProperties()` aktualisiert alle 19 Properties nach `FavoritesChanged`.

**Localization:** Gezielte Invalidierung via `LocalizedPropertyNames`-Array (nicht
`OnPropertyChanged(string.Empty)` — das würde alle Bindings im Visual-Tree auslösen → 50-150ms Stutter).

**Back-Navigation:** 1. SaveDialog schließen → 2. Calculator schließen → 3. Nicht-Calculator-VM
schließen → 4. Nicht-Home-Tab → Home → 5. Double-Back-to-Exit via `BackPressHelper`.

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
// History-Save: separater 2s-Debounce via ScheduleDebouncedSave
```

`_isCalculating` als Reentrancy-Schutz in `Calculate()`. `Reset()` disposed Timer.
Alle VMs: `IDisposable` (Timer + Event-Subscriptions). `Cleanup()` ist API-Konsistenz über Interface.

## Domänen-Gotchas (VM-Ebene)

- **Drehstrom-Erkennung (Electrical/CableSizing):** `Voltage >= 380V` → Phasen-Faktor `√3`
  statt `2` (DIN VDE). VMs prüfen `IsThreePhase`-Flag, nicht die Spannungsgrenze direkt —
  beide müssen konsistent sein.
- **PlasterType/ScreedType Enum-Routing:** Tipp-sichere Enums statt String-Vergleich.
  `CalculateXxx(area, thickness, PlasterType.Gypsum)` — nie String-"Gipsputz" übergeben.
- **`GetString(key)` gibt NIEMALS null zurück:** `LocalizationService` gibt den Key-Namen zurück
  wenn der Key fehlt. `?? "fallback"` ist toter Code — Fehlende RESX-Keys mit dem Key-Namen sichtbar.
- **History-Save-Race (ProjectsVM):** `_initTask = InitializeAsync()` speichern;
  in allen öffentlichen Commands `await _initTask` abwarten — sonst `_list.Clear()` raced mit User.
