# FinanzRechner.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Grafik, Modelle) und wird von `FinanzRechner.Android` und
`FinanzRechner.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigureServices(IServiceCollection)`** — alles **Singleton**:
  - Core: `IPreferencesService` → `PreferencesService("FinanzRechner")`.
  - Premium: `services.AddMeineAppsPremium()` + Android-Factory-Overrides
    (`RewardedAdServiceFactory`, `PurchaseServiceFactory`, `FileShareServiceFactory`).
  - Lokalisierung: `ILocalizationService` → `LocalizationService(AppStrings.ResourceManager, …)`.
  - App-Services: `IFileDialogService`, `IFileShareService` (Platform-Factory oder
    `DesktopFileShareService`), `INotificationService`, `IExpenseService`, `IExportService`,
    `IAccountService`, `ISavingsGoalService`, `IDebtService`, `ICustomCategoryService`,
    `IFinancialAnalysisService`.
  - Berechnungs-Engine: `FinanceEngine` (kein Interface — rein mathematisch, kein Mocking nötig).
  - ViewModels: `ExpenseTrackerViewModel`, `StatisticsViewModel`, `SettingsViewModel`,
    `BudgetsViewModel`, `RecurringTransactionsViewModel`, `AccountsViewModel`,
    `SavingsGoalsViewModel`, `DebtTrackerViewModel`, `CustomCategoriesViewModel`,
    `LoanViewModel`, `CompoundInterestViewModel`, `SavingsPlanViewModel`,
    `AmortizationViewModel`, `YieldViewModel`, `InflationViewModel`, `MainViewModel`.
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `SkiaThemeHelper.RefreshColors()` →
  `LocalizationService.Initialize()` + `LocalizationManager.Initialize()` →
  Splash + `MainView` in ein `Panel` (Desktop: `IClassicDesktopStyleApplicationLifetime.MainWindow`;
  Android: `ISingleViewApplicationLifetime.MainView`) → `RunLoadingAsync`.
- **`RunLoadingAsync(splash)`** — `FinanzRechnerLoadingPipeline` ausführen, Fortschritt auf Splash
  posten, **mindestens 800 ms** Splash anzeigen, dann `MainViewModel` als `DataContext` setzen +
  `splash.FadeOut()`. Fehler gefangen → FadeOut (kein Leerbildschirm).

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `FinanzRechner.ViewModels` |
| `ViewModels/Calculators/` | `FinanzRechner.ViewModels.Calculators` |
| `Views/` | `FinanzRechner.Views` |
| `Views/Calculators/` | `FinanzRechner.Views.Calculators` |
| `Services/` | `FinanzRechner.Services` |
| `Models/` | `FinanzRechner.Models` |
| `Graphics/` | `FinanzRechner.Graphics` |
| `Converters/` | `FinanzRechner.Converters` |
| `Helpers/` | `FinanzRechner.Helpers` |
| `Loading/` | `FinanzRechner.Loading` |

---

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel + alle Sub-VMs, Partials, Calculator-VMs | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | 18 AXAML-Views, Behaviors, Overlay-Patterns | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | 8 Interface/Impl-Paare (Persistenz, Export, Analyse) | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Models/` | Datenmodelle, Enums, FinanceEngine + Result-Records | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 12 SkiaSharp-Renderer + ChartHelper | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Converters/` | 18 IValueConverter-Implementierungen | [Converters/CLAUDE.md](Converters/CLAUDE.md) |
| `Helpers/` | CurrencyHelper, CategoryLocalizationHelper | [Helpers/CLAUDE.md](Helpers/CLAUDE.md) |
| `Loading/` | FinanzRechnerLoadingPipeline | [Loading/CLAUDE.md](Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Smaragd `#10B981`),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets).

---

## Build

```bash
dotnet build src/Apps/FinanzRechner/FinanzRechner.Shared
```
