# Services — App-spezifische Dienste

App-eigene Services in `HandwerkerRechner.Services`. Alle als Singleton in `App.axaml.cs`
registriert. Generische Service-Patterns (Thread-Safety, DI) → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Zweck |
|-------|-------|
| `ICalculatorFactoryService.cs` / `CalculatorFactoryService.cs` | Route → `Func<ObservableObject>` Dictionary für alle 19 Calculator-VMs. Einzige Stelle wo Routen definiert sind. |
| `IProjectService.cs` / `ProjectService.cs` | JSON-Persistenz für `Project`-Objekte. `SemaphoreSlim`-Lock. CRUD + Foto-Management. |
| `IProjectTemplateService.cs` / `ProjectTemplateService.cs` | Built-in Templates + benutzerdefinierte Templates. Property-Key-Konsistenz mit Calculator-VM `LoadFromProjectIdAsync`. |
| `IQuoteService.cs` / `QuoteService.cs` | Angebots-CRUD (JSON-Persistenz), Positions-Verwaltung, PDF-Export. |
| `IFavoritesService.cs` / `FavoritesService.cs` | Favorisierte Calculator-Routes (Reihenfolge), `FavoritesChanged`-Event, `IsFavorite(key)`, `Toggle(key)`. |
| `IMaterialExportService.cs` / `MaterialExportService.cs` | PDF (PdfSharpCore A4) + CSV (Semikolon, UTF-8-BOM). Rewarded-Ad-Gate (`material_pdf`, `project_export`). Formula-Injection-Schutz in `EscapeCsv()`. |
| `IMaterialPriceService.cs` / `MaterialPriceService.cs` | Material-Preise pro Region/Land für Kostenschätzungen. |
| `IPhotoPickerService.cs` (Interface) | Foto-Auswahl-Kontrakt. Desktop-Impl: `DesktopPhotoPickerService` (StorageProvider, kopiert in AppData/photos/ mit GUID). Android-Override via `PhotoPickerServiceFactory`. |

## Aus `MeineApps.Core.Ava` (geteilt)

| Service | Zweck |
|---------|-------|
| `CalculationHistoryService` | History pro Rechner (max 30 Items), 2s-Debounce-Save, parallel-load mit `Task.WhenAll`, static `JsonSerializerOptions`. |
| `IUnitConverterService` / `UnitConverterService` | Länge, Fläche, Volumen, Gewicht (Metrisch/Imperial). |

## Kritische Patterns

### CalculatorFactoryService

Ersetzt 19 einzelne `Func<T>`-Fields in MainViewModel. Routen sind EXAKT die Strings, die
`MainViewModel.NavigateTo(route)` übergeben werden — Konventionsbruch führt zu `null`-Return
und leerem Overlay.

```csharp
// CalculatorFactoryService registriert alle Routen:
_factories["TileCalculatorPage"] = sp => sp.GetRequiredService<TileCalculatorViewModel>();
// MainViewModel erstellt via:
ObservableObject? vm = _calculatorFactory.Create(route);
```

### ProjectService — Property-Key-Konsistenz

Built-in Templates in `ProjectTemplateService` müssen EXAKT die Property-Keys verwenden,
die Calculator-VMs in `LoadFromProjectIdAsync` via `project.GetValue<T>("Key")` erwarten.
`ProjectTemplatesViewModel.ParseTemplateValue` castet String-Defaults nach `bool`/`int`/`double`
damit der JSON-Roundtrip funktioniert — das ist notwendig weil Template-Defaults als strings
gespeichert werden.

### MaterialExportService — Formula-Injection

`EscapeCsv()` präfixt führende `=`/`+`/`-`/`@` mit Apostroph (Excel-Angriffs-Schutz).
CSV-Header sind lokalisiert — `ILocalizationService` im Konstruktor injiziert.

### DesktopPhotoPickerService — Path-Traversal-Schutz

`DeletePhotoAsync` vergleicht `Path.GetFullPath(filePath)` mit dem erwarteten `PhotoDirectory` —
verhindert Dateizugriff außerhalb des erlaubten Ordners via `../` in gespeicherten Pfaden.
