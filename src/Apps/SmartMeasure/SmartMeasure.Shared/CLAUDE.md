# SmartMeasure.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Modelle, Grafik) und wird von `SmartMeasure.Android` und
`SmartMeasure.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

### Plattform-Factory-Properties (LAZY ausgewertet)

| Property | Setter | Fallback |
|----------|--------|---------|
| `App.AppPathsFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `AppPaths` (ApplicationData) |
| `App.ArCaptureServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `MockArCaptureService` |
| `App.VoiceAnnotationServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `NullVoiceAnnotationService` |

**KRITISCH (Avalonia 12 Android):** `OnFrameworkInitializationCompleted` (DI-Build) läuft in
`AvaloniaAndroidApplication.OnCreate` — also **VOR** `MainActivity.OnCreate`, das die Factories
setzt. Daher werden diese drei Services **lazy** registriert (Factory-Prüfung im Resolve-Lambda,
NICHT als Build-Zeit-`if`) und `MainViewModel` wird auf Android im
`IActivityApplicationLifetime.MainViewFactory`-Lambda aufgelöst — `AvaloniaActivity` ruft die
Factory aus `InitializeAvaloniaView` (in `MainActivity.OnCreate.base`) auf, also deterministisch
NACH der Factory-Setzung → echte Android-Services statt Mock (Bug-Historie:
`MockArCaptureService` → 10 Punkte ohne Kamera). Desktop löst sofort auf und lässt die Factories
null → Fallbacks/Mocks (gewollt). Generisches Pattern → [Core.Ava-CLAUDE.md](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md) „Threading & Lifecycle".

### `ConfigureServices` — Reihenfolge (Abhängigkeiten beachten!)

1. **`IAppPaths`** — MUSS als erstes registriert werden (`ProjectService`, `ExportService`, `SettingsViewModel` hängen davon ab).
2. **`IPreferencesService`** → `PreferencesService("SmartMeasure")`.
3. **`IArCaptureService`** — plattform-spezifisch oder Mock.
4. **Fachliche Services** (alle Singleton): `IMeasurementService`, `ICoordinateService`,
   `IGeoidService`, `ITerrainService`, `IGardenPlanService`, `IProjectService`,
   `IExportService`, `IBlenderExportService`, `IArTransferService`,
   `IDifferentialSnapshotService`, `IVolumeService`,
   `ITotalStationService`, `ILeastSquaresAdjustmentService`,
   `IVoiceAnnotationService`, `ISurveyReportService`, `ISceneReconstructionService`,
   `IMultiUserSessionService`.
5. **ViewModels** (alle Singleton): `MainViewModel`, `SurveyViewModel`,
   `TerrainViewModel`, `GardenPlanViewModel`, `MapViewModel`, `ProjectsViewModel`,
   `SettingsViewModel`.

### `OnFrameworkInitializationCompleted`

DI bauen → Lifetime-Branch:
- **Desktop** (`IClassicDesktopStyleApplicationLifetime`, 450×900): `MainViewModel` sofort holen,
  `MainView` als `DataContext`, `InitializeAsync()`. Factories null → Mocks (gewollt).
- **Android** (`IActivityApplicationLifetime` — VOR `ISingleViewApplicationLifetime` prüfen, das
  Android-Lifetime implementiert beide): `activity.MainViewFactory = () => { MainViewModel auflösen;
  InitializeAsync(); return new MainView { DataContext = vm }; }`. Die Factory wird von
  `AvaloniaActivity` in `MainActivity.OnCreate.base` aufgerufen → erster Resolve liegt NACH der
  Factory-Setzung → echte Android-Services statt Mock (siehe Factory-Hinweis oben).
- **iOS-Fallback** (`ISingleViewApplicationLifetime`): sofortiges Auflösen, `MainView` mit DataContext.

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
| `ViewModels/` | 7 ViewModels (Navigation, Messung, Gelände, Gartenplan, Karte, Projekte, Einstellungen) | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 7 Views mit `x:CompileBindings="True"`, Touch-Handler, Lazy-MapView | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | Interfaces + plattformneutrale Implementierungen (Mock, Geo, Terrain, Export, ...) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | Datenmodelle (SQLite-Entities, Immutable Structs, AR-Typen) | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 3 SkiaSharp-Renderer (Terrain, GardenPlan, Thumbnail) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Orange #FF6B00),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/`.

---

## Build

```bash
dotnet build src/Apps/SmartMeasure/SmartMeasure.Shared
dotnet run   --project src/Apps/SmartMeasure/SmartMeasure.Desktop
```
