# RechnerPlus.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Grafik) und wird von `RechnerPlus.Android` und `RechnerPlus.Desktop` referenziert.
Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigureServices(IServiceCollection)`** — alles **Singleton**:
  - Core: `IPreferencesService` → `PreferencesService("RechnerPlus")`, `ILocalizationService` →
    `LocalizationService(AppStrings.ResourceManager, …)`.
  - CalcLib: `CalculatorEngine`, `ExpressionParser`, `IHistoryService` → `HistoryService`.
  - Plattform: `IHapticService` — Android setzt `HapticServiceFactory` in `MainActivity`,
    Desktop fällt auf `NoOpHapticService` zurück.
  - ViewModels: `CalculatorViewModel` (Ctor-Injection von Engine/Parser/Loc/History/Prefs/Haptic),
    `ConverterViewModel`, `SettingsViewModel`, `MainViewModel`.
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `SkiaThemeHelper.RefreshColors()` →
  `LocalizationService.Initialize()` + `LocalizationManager.Initialize()` → Splash + `MainView`
  in ein `Panel` (Desktop: `IClassicDesktopStyleApplicationLifetime.MainWindow`; Android:
  `ISingleViewApplicationLifetime.MainView`) → `RunLoadingAsync`.
- **`RunLoadingAsync(splash)`** — `RechnerPlusLoadingPipeline` ausführen, Fortschritt auf Splash
  posten, **mindestens 800 ms** Splash anzeigen, dann `MainViewModel` als `DataContext` setzen +
  `splash.FadeOut()`. Fehler werden gefangen → FadeOut (kein Leerbildschirm).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| Root (`App.axaml.cs`) | `RechnerPlus` |
| `ViewModels/` | `RechnerPlus.ViewModels` |
| `Views/` | `RechnerPlus.Views` |
| `Graphics/` | `RechnerPlus.Graphics` |
| `Controls/` | `RechnerPlus.Controls` |
| `Loading/` | `RechnerPlus.Loading` |

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel + Calculator/Converter/Settings-VMs, Rechen-/Display-Logik | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views + UI-Patterns (Button-Grid, Keyboard, Landscape) | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Graphics/` | SkiaSharp-Renderer (VFD-Display, Result-Burst, Function-Graph, Circuit-Board-Hintergrund, Splash) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |
| `Controls/` | App-eigene Custom Controls | [Controls/CLAUDE.md](Controls/CLAUDE.md) |
| `Loading/` | Startup-Pipeline | [Loading/CLAUDE.md](Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Indigo #7C7FF7),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/` (Bild-Assets).

## Build

```bash
dotnet build src/Apps/RechnerPlus/RechnerPlus.Shared
```
