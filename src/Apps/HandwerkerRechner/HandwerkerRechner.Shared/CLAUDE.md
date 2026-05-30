# HandwerkerRechner.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Grafik) und wird von `HandwerkerRechner.Android` und `HandwerkerRechner.Desktop`
referenziert. Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Überblick → [../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigureServices(IServiceCollection)`**:
  - Core: `IPreferencesService` → `PreferencesService("HandwerkerRechner")`, `IUnitConverterService`,
    `ICalculationHistoryService`.
  - Premium: `services.AddMeineAppsPremium()` → dann Android-Override via Factories wenn gesetzt:
    `RewardedAdServiceFactory`, `PurchaseServiceFactory`.
  - Localization: `LocalizationService(AppStrings.ResourceManager, IPreferencesService)`.
  - App-Services: `IProjectService`, `IFavoritesService`, `IProjectTemplateService`,
    `IMaterialPriceService`, `IQuoteService` — alle Singleton.
  - Export: `IFileShareService` ← Factory (Android) oder `DesktopFileShareService`. `IMaterialExportService`.
  - Foto-Picker: `IPhotoPickerService` ← Factory (Android) oder `DesktopPhotoPickerService`.
  - Engine: `CraftEngine` (Singleton).
  - ViewModels Singleton: `MainViewModel`, `SettingsViewModel`, `ProjectsViewModel`, `HistoryViewModel`,
    `ProjectTemplatesViewModel`, `QuoteViewModel`.
  - ViewModels Transient (19): alle Calculator-VMs (Floor + Premium). Frisch pro Öffnen.
  - Factory-Service: `ICalculatorFactoryService` → `CalculatorFactoryService` (Singleton,
    Route → `Func<ObservableObject>`-Dictionary).
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `SkiaThemeHelper.RefreshColors()` →
  `LocalizationService.Initialize()` + `LocalizationManager.Initialize()` → Splash + `MainView`
  in ein `Panel` (Desktop: `IClassicDesktopStyleApplicationLifetime.MainWindow`; Android:
  `ISingleViewApplicationLifetime.MainView`) → `RunLoadingAsync`.
- **`RunLoadingAsync(splash)`** — `HandwerkerRechnerLoadingPipeline` ausführen, Fortschritt auf Splash
  posten, **mindestens 800 ms** Splash anzeigen, dann `MainViewModel` als `DataContext` setzen +
  `splash.FadeOut()`. Fehler werden gefangen → FadeOut (kein Leerbildschirm).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `HandwerkerRechner.ViewModels` |
| `ViewModels/Floor/` | `HandwerkerRechner.ViewModels.Floor` |
| `ViewModels/Premium/` | `HandwerkerRechner.ViewModels.Premium` |
| `Views/` | `HandwerkerRechner.Views` |
| `Views/Floor/` | `HandwerkerRechner.Views.Floor` |
| `Views/Premium/` | `HandwerkerRechner.Views.Premium` |
| `Services/` | `HandwerkerRechner.Services` |
| `Models/` | `HandwerkerRechner.Models` |
| `Graphics/` | `HandwerkerRechner.Graphics` |
| `Loading/` | `HandwerkerRechner.Loading` |
| `Converters/` | `HandwerkerRechner.Converters` |

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel, ICalculatorViewModel, Tab-Navigation, Back-Press, alle 19+4 VMs | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views, CalculatorViewBase, Floor/Premium-Unterordner | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | 7 App-Services (Project, Quote, Export, FavoritesService …) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | CraftEngine (Domänen-Berechnungen), Project, Quote, CalculatorCategory | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 20 SkiaSharp-Renderer + Splash-Renderer + Background-Renderer | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Loading/` | Startup-Pipeline | [Loading/CLAUDE.md](Loading/CLAUDE.md) |
| `Converters/` | XAML-Converter | [Converters/CLAUDE.md](Converters/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Blau #3B82F6),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets).

## Build

```bash
dotnet build src/Apps/HandwerkerRechner/HandwerkerRechner.Shared
```
