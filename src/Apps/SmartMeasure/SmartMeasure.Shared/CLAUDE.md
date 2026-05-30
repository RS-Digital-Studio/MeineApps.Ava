# SmartMeasure.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Modelle, Grafik) und wird von `SmartMeasure.Android` und
`SmartMeasure.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

### Plattform-Factory-Properties (vor DI-Build setzen)

| Property | Setter | Fallback |
|----------|--------|---------|
| `App.AppPathsFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `AppPaths` (ApplicationData) |
| `App.BleServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `MockBleService` |
| `App.ArCaptureServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `MockArCaptureService` |
| `App.VoiceAnnotationServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `NullVoiceAnnotationService` |

Alle vier Factories werden ausgewertet bevor `ConfigureServices` den DI-Container baut. Android setzt
sie in `OnCreate` VOR `base.OnCreate`. Desktop lässt sie null → Fallbacks greifen.

### `ConfigureServices` — Reihenfolge (Abhängigkeiten beachten!)

1. **`IAppPaths`** — MUSS als erstes registriert werden (`ProjectService`, `ExportService`, `SettingsViewModel` hängen davon ab).
2. **`IPreferencesService`** → `PreferencesService("SmartMeasure")`.
3. **`IBleService`** / **`IArCaptureService`** — plattform-spezifisch oder Mock.
4. **Fachliche Services** (alle Singleton): `IMeasurementService`, `ICoordinateService`,
   `IGeoidService`, `ITerrainService`, `IGardenPlanService`, `IProjectService`,
   `IExportService`, `IBlenderExportService`, `IArTransferService`,
   `IDifferentialSnapshotService`, `IGnssConditionService`, `IVolumeService`,
   `ITotalStationService`, `ILeastSquaresAdjustmentService`,
   `IVoiceAnnotationService`, `ISurveyReportService`, `ISceneReconstructionService`,
   `IMultiUserSessionService`.
5. **ViewModels** (alle Singleton): `MainViewModel`, `ConnectViewModel`, `SurveyViewModel`,
   `TerrainViewModel`, `GardenPlanViewModel`, `MapViewModel`, `ProjectsViewModel`,
   `StakeoutViewModel`, `SettingsViewModel`.

### `OnFrameworkInitializationCompleted`

DI bauen → `MainViewModel` holen → `MainView` als `DataContext` setzen →
`_mainVm.InitializeAsync()` (fire-and-forget).
Desktop: `IClassicDesktopStyleApplicationLifetime.MainWindow` (450×900).
Android: `ISingleViewApplicationLifetime.MainView`.
Kein Splash-Schirm — `InitializeAsync()` ist aktuell leer (kein langer Startup-Load nötig).
Fehler werden nicht geschluckt sondern weitergeworfen (Logcat-Sichtbarkeit auf Android).

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `SmartMeasure.ViewModels` |
| `Views/` | `SmartMeasure.Views` |
| `Services/` | `SmartMeasure.Services` |
| `Models/` | `SmartMeasure.Models` |
| `Graphics/` | `SmartMeasure.Graphics` |

---

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | 9 ViewModels (Navigation, Live-Messung, Gelände, Gartenplan, Karte, Projekte, Stakeout, Verbindung, Einstellungen) | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 9 Views mit `x:CompileBindings="True"`, Touch-Handler, Lazy-MapView | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | Interfaces + plattformneutrale Implementierungen (Mock, Geo, Terrain, Export, ...) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | Datenmodelle (SQLite-Entities, Immutable Structs, AR-Typen) | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 5 SkiaSharp-Renderer (Terrain, GardenPlan, SurveyLive, Stakeout, Thumbnail) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Orange #FF6B00),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/`.

---

## Build

```bash
dotnet build src/Apps/SmartMeasure/SmartMeasure.Shared
dotnet run   --project src/Apps/SmartMeasure/SmartMeasure.Desktop
```
