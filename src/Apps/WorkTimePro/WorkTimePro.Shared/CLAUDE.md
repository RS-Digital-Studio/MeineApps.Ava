# WorkTimePro.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels, Views,
Services, Models, Graphics) und wird von `WorkTimePro.Android` und `WorkTimePro.Desktop`
referenziert. Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Überblick → [../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigureServices(IServiceCollection)`** — alles **Singleton**:
  - Core: `IPreferencesService` → `PreferencesService("WorkTimePro")`.
  - Premium: `services.AddMeineAppsPremium()` → `ITrialService`, `IAdService`, `IPurchaseService`-Stub,
    `IRewardedAdService`-Simulator. Android überschreibt per Factory.
  - Localization: `LocalizationService(AppStrings.ResourceManager, …)`.
  - Plattform: `IFileShareService` ← `FileShareServiceFactory?.Invoke()` ?? `DesktopFileShareService`.
  - `IRewardedAdService` ← `RewardedAdServiceFactory?.Invoke(sp)` (Android setzt Factory).
  - `IPurchaseService` ← `PurchaseServiceFactory?.Invoke(sp)` (Android setzt Factory).
  - `INotificationService` ← `NotificationServiceFactory?.Invoke()` ?? `DesktopNotificationService`.
  - `IHapticService` ← `HapticServiceFactory?.Invoke()` ?? `NoOpHapticService`.
  - App-Services: `IDatabaseService`, `ICalculationService`, `ITimeTrackingService`,
    `ICalendarExportService`, `IExportService`, `IVacationService`, `IHolidayService`,
    `IProjectService`, `IShiftService`, `IEmployerService`, `IBackupService`, `IReminderService`.
  - ViewModels: `WeekOverviewViewModel`, `CalendarViewModel`, `StatisticsViewModel`,
    `SettingsViewModel`, `DayDetailViewModel`, `MonthOverviewViewModel`, `YearOverviewViewModel`,
    `VacationViewModel`, `ShiftPlanViewModel`, `MainViewModel` — alles Singleton.
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `LocalizationService.Initialize()` +
  `LocalizationManager.Initialize()` → `SkiaThemeHelper.RefreshColors()` → `MainView` + Splash
  in ein `Panel` (Desktop: `IClassicDesktopStyleApplicationLifetime.MainWindow`; Android:
  `ISingleViewApplicationLifetime.MainView`) → `RunLoadingAsync`. DataContext wird erst nach
  Pipeline-Abschluss gesetzt (verhindert Race im `InitializeAsync`).
- **`RunLoadingAsync(splash)`** — `WorkTimeProLoadingPipeline` ausführen, Fortschritt auf Splash
  posten, **mindestens 800 ms** Splash anzeigen (Stechuhr-Animation), dann `MainViewModel` als
  `DataContext` setzen + `splash.FadeOut()`. Fehler → FadeOut ohne Absturz.
- **`App.Services`** — öffentliches Static-Property, darf nur von `MainActivity` gelesen werden
  (für Back-Button-VM-Lookup). KEIN Service-Locator in Views/ViewModels.

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `WorkTimePro.ViewModels` |
| `Views/` | `WorkTimePro.Views` |
| `Services/` | `WorkTimePro.Services` |
| `Models/` | `WorkTimePro.Models` |
| `Graphics/` | `WorkTimePro.Graphics` |
| `Converters/` | `WorkTimePro.Converters` |
| `Helpers/` | `WorkTimePro.Helpers` |
| `Loading/` | `WorkTimePro.Loading` |

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel (+Navigation-Partial), alle 9 Child-VMs, Tabs + Sub-Page-Routing | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 12 AXAML-Views + MainWindow, UI-Patterns (Overlay, Keyboard-Shortcuts) | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | 13 Interfaces + Implementierungen (DB, Zeiterfassung, Export, Backup, Reminder, ...) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | SQLite-Entitäten, Enums, berechnete Properties, AppColors | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 11 SkiaSharp-Visualisierungen + Splash + Background | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Converters/` | App-eigene `IValueConverter`-Implementierungen | [Converters/CLAUDE.md](Converters/CLAUDE.md) |
| `Helpers/` | `TimeFormatter`, `DurationMath` (DST-bewusste Dauer) | [Helpers/CLAUDE.md](Helpers/CLAUDE.md) |
| `Loading/` | `WorkTimeProLoadingPipeline` (Startup-Sequenz) | [Loading/CLAUDE.md](Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Blau #4F8BF9),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets).

## Build

```bash
dotnet build src/Apps/WorkTimePro/WorkTimePro.Shared
```
