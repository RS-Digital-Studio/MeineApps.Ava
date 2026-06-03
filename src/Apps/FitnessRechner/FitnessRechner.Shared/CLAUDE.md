# FitnessRechner.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Graphics, Models) und wird von `FitnessRechner.Android` und
`FitnessRechner.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

### Platform-Factories (gesetzt von Android vor `base.OnCreate`)

| Factory-Property | Typ | Android-Impl | Desktop-Fallback |
|------------------|-----|-------------|-----------------|
| `RewardedAdServiceFactory` | `Func<IServiceProvider, IRewardedAdService>` | `AndroidRewardedAdService` | Premium-Stub |
| `PurchaseServiceFactory` | `Func<IServiceProvider, IPurchaseService>` | `AndroidPurchaseService` | Billing-Stub |
| `FileShareServiceFactory` | `Func<IFileShareService>` | `AndroidFileShareService` | `DesktopFileShareService` |
| `BarcodeServiceFactory` | `Func<IBarcodeService>` | `AndroidBarcodeService` (CameraX + ML Kit) | `DesktopBarcodeService` (null → manuelle Eingabe) |
| `HapticServiceFactory` | `Func<IHapticService>` | `AndroidHapticService` | `NoOpHapticService` |
| `SoundServiceFactory` | `Func<IFitnessSoundService>` | `AndroidFitnessSoundService` | `NoOpFitnessSoundService` |
| `ReminderServiceFactory` | `Func<IServiceProvider, IReminderService>` | `AndroidReminderService` | `ReminderService` (No-Op) |

### `ConfigureServices` — Registrierungsreihenfolge

1. `IPreferencesService` → `PreferencesService("FitnessRechner")` (Singleton)
2. `AddMeineAppsPremium()` — Standard-Stubs für Ads/Billing
3. Platform-Override-Factories (wenn gesetzt): `IRewardedAdService`, `IPurchaseService`
4. `IScanLimitService` → `ScanLimitService`
5. `IFileShareService`, `IBarcodeService` — Platform-Factory oder Desktop-Fallback
6. `ILocalizationService` → `LocalizationService(AppStrings.ResourceManager, preferences)`
7. Domain-Services (alle Singleton): `IFitnessEngine`, `IStreakService`, `IAchievementService`,
   `ILevelService`, `IChallengeService`, `ITrackingService`, `IBarcodeLookupService`,
   `IFoodSearchService`, `IFastingService`, `IActivityService`
8. Plattform-Services: `IHapticService`, `IFitnessSoundService`, `IReminderService`
9. Haupt-ViewModels (Singleton): `MainViewModel`, `ProgressViewModel`, `FoodSearchViewModel`,
   `SettingsViewModel`, `RecipeViewModel`, `FastingViewModel`, `ActivityViewModel`
10. Calculator-ViewModels (Transient): `BmiViewModel`, `CaloriesViewModel`, `WaterViewModel`,
    `IdealWeightViewModel`, `BodyFatViewModel`, `BarcodeScannerViewModel`
11. `Func<T>`-Factories für alle Calculator-VMs (Constructor Injection, kein Service-Locator)

### `OnFrameworkInitializationCompleted()`

DI bauen → `locService.Initialize()` + `LocalizationManager.Initialize()` →
`SkiaThemeHelper.RefreshColors()` → `MainView` + Splash in Panel → `RunLoadingAsync`.

**Desktop:** `IClassicDesktopStyleApplicationLifetime.MainWindow`.
**Android:** `ISingleViewApplicationLifetime.MainView`.

### `RunLoadingAsync`

`FitnessRechnerLoadingPipeline` ausführen → Fortschritt auf Splash posten →
**mindestens 800 ms** Splash anzeigen → `MainViewModel` als `DataContext` setzen +
`splash.FadeOut()`. Fehler werden gefangen → FadeOut (kein Leerbildschirm).

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `FitnessRechner.ViewModels` |
| `ViewModels/Calculators/` | `FitnessRechner.ViewModels.Calculators` |
| `Views/` | `FitnessRechner.Views` |
| `Views/Calculators/` | `FitnessRechner.Views.Calculators` |
| `Services/` | `FitnessRechner.Services` |
| `Models/` | `FitnessRechner.Models` |
| `Graphics/` | `FitnessRechner.Graphics` |
| `Converters/` | `FitnessRechner.Converters` |
| `Loading/` | `FitnessRechner.Loading` |

---

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainVM + Dashboard-Partial, ProgressVM (4 Partials), Calculator-VMs | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views (Home, Progress, FoodSearch, Settings, Barcode, Fasting, Activity) + Calculator-Views | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Graphics/` | VitalOS Medical Design System (15 Renderer + `MedicalColors`) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Services/` | Domain-Services (Tracking, Food, Gamification, Fasting, Activity, Reminders) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | Datenmodelle, `FitnessEngine` (5 Berechnungen), Result-Records | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Converters/` | Tab-Farb-, Food-Kategorie- und Utility-Converter | [Converters/CLAUDE.md](Converters/CLAUDE.md) |
| `Loading/` | Startup-Pipeline | [Loading/CLAUDE.md](Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Cyan #06B6D4),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets).
