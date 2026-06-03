# Services — App-spezifische Dienste

App-eigene Services in `HandwerkerRechner.Services`. Alle als Singleton in `App.axaml.cs`
registriert (Ausnahme: `IPhotoPickerService` per Factory). Generische Service-Patterns
(Thread-Safety, DI) → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `ICalculatorFactoryService.cs` / `CalculatorFactoryService.cs` | Route → `Func<ObservableObject>` Dictionary für alle 19 Calculator-VMs. Einzige Stelle wo Routen definiert sind. |
| `IProjectService.cs` / `ProjectService.cs` | JSON-Persistenz für `Project`-Objekte (inkl. Foto-Pfad-Liste). `SemaphoreSlim`-Lock + In-Memory-Cache. `SaveFailed`-Event bei I/O-Fehler. |
| `IProjectTemplateService.cs` / `ProjectTemplateService.cs` | Eingebaute Templates (hardcodiert) + benutzerdefinierte Templates (JSON-Persistenz). Property-Key-Konsistenz mit Calculator-VM `LoadFromProjectIdAsync`. |
| `IQuoteService.cs` / `QuoteService.cs` | Angebots-CRUD (JSON-Persistenz, `SemaphoreSlim`), `GenerateQuoteNumberAsync` (Format `"A-YYYY-NNN"`). `SaveFailed`-Event. Kein PDF-Export im Service — Export liegt in `IMaterialExportService`. |
| `IFavoritesService.cs` / `FavoritesService.cs` | Favorisierte Calculator-Routes; persistiert als kommagetrennte Liste in `IPreferencesService`. `FavoritesChanged`-Event, `IsFavorite(key)`, `Toggle(key)`. |
| `IMaterialExportService.cs` / `MaterialExportService.cs` | PDF (PdfSharpCore A4, mehrere Seiten mit `EnsureSpace`) + CSV (Semikolon, UTF-8-BOM). Injiziert `ILocalizationService` + `IFileShareService`. Formula-Injection-Schutz in `EscapeCsv()`. |
| `IMaterialPriceService.cs` / `MaterialPriceService.cs` | Regionale Durchschnittspreise + benutzerdefinierte Überschreibungen (Preferences). Filter nach Kategorie, Reset-Methoden. |
| `IPhotoPickerService.cs` (Interface) | Foto-Auswahl-Kontrakt. Desktop-Impl: `DesktopPhotoPickerService` (StorageProvider, kopiert in `LocalApplicationData/MeineApps/HandwerkerRechner/photos/` mit GUID-Dateinamen). Android-Override via `PhotoPickerServiceFactory`. |

## Kritische Patterns

### CalculatorFactoryService — Routen-Konvention

Routen sind EXAKT die Strings, die `MainViewModel.NavigateTo(route)` übergeben werden.
Konventionsbruch → `Create(route)` gibt `null` zurück → leeres Overlay ohne Fehlermeldung.
Aufteilung: 5 Floor (`TileCalculatorPage` … `ConcretePage`), 11 Premium (`DrywallPage` …
`GroutPage`), 3 Profi-Werkzeuge (`HourlyRatePage`, `MaterialComparePage`, `AreaMeasurePage`).

```csharp
// CalculatorFactoryService registriert alle Routen:
["TileCalculatorPage"] = () => Resolve<TileCalculatorViewModel>(serviceProvider)
// MainViewModel erstellt via:
ObservableObject? vm = _calculatorFactory.Create(route);
```

### ProjectService — Property-Key-Konsistenz

Built-in Templates in `ProjectTemplateService` müssen EXAKT die Property-Keys verwenden,
die Calculator-VMs in `LoadFromProjectIdAsync` via `project.GetValue<T>("Key")` erwarten.
`ProjectTemplatesViewModel.ParseTemplateValue` castet String-Defaults nach `bool`/`int`/`double`
damit der JSON-Roundtrip funktioniert — Template-Defaults werden als strings gespeichert.

### MaterialExportService — Formula-Injection

`EscapeCsv()` präfixt führende `=`/`+`/`-`/`@` mit Apostroph (Excel-Angriffs-Schutz).
CSV-Header sind lokalisiert — `ILocalizationService` im Konstruktor injiziert. Kein
Rewarded-Ad-Gate im Service selbst — das Gate (`material_pdf`, `project_export`) liegt
im aufrufenden ViewModel.

### DesktopPhotoPickerService — Path-Traversal-Schutz

`DeletePhotoAsync` ruft `IsPathInPhotoDirectory()` auf: `Path.GetFullPath(filePath)`
muss mit dem kanonischen `PhotoDirectory` (mit Trailing-Separator) beginnen — verhindert
`../`-Pfade in gespeicherten Projekt-JSON-Daten.
