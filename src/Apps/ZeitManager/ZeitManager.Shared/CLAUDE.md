# ZeitManager.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (ViewModels,
Views, Services, Audio, Grafik) und wird von `ZeitManager.Android` und `ZeitManager.Desktop`
referenziert. Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick →
[../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden, `RequestedThemeVariant = Dark` (fest, kein Theme-Wechsel).
- **`ConfigurePlatformServices`** — statische `Action<IServiceCollection>?`-Property. Android setzt
  sie in `MainActivity.OnCreate` VOR `base.OnCreate` (also VOR dem DI-Build). Desktop lässt sie
  null — Fallback-Services greifen.
- **`ConfigureServices(IServiceCollection)`** — alles **Singleton**:
  - Core: `IPreferencesService` → `PreferencesService("ZeitManager")`,
    `ILocalizationService` → `LocalizationService(AppStrings.ResourceManager, …)`.
  - App: `IDatabaseService`, `ITimerService`, `IAudioService` → `AudioService`,
    `IAlarmSchedulerService`, `IShiftScheduleService`,
    `IShakeDetectionService` → `DesktopShakeDetectionService` (Desktop-Fallback),
    `IHapticService` → `NoOpHapticService` (Desktop-Fallback).
  - Platform: `INotificationService` ← `ConfigurePlatformServices?.Invoke(services)` (Android
    überschreibt `IAudioService`, `IShakeDetectionService` und `IHapticService` zusätzlich zu
    `INotificationService`) ?? `DesktopNotificationService`-Fallback.
  - ViewModels: `MainViewModel`, `TimerViewModel`, `StopwatchViewModel`, `PomodoroViewModel`,
    `AlarmViewModel`, `SettingsViewModel`, `AlarmOverlayViewModel`, `ShiftScheduleViewModel`.
- **`OnFrameworkInitializationCompleted()`** — DI bauen → `SkiaThemeHelper.RefreshColors()` →
  `LocalizationService.Initialize()` + `LocalizationManager.Initialize()` → Splash +
  `MainView` in ein `Panel` (Desktop: `MainWindow.Content`; Android:
  `ISingleViewApplicationLifetime.MainView`) → `RunLoadingAsync`.
- **`RunLoadingAsync(splash)`** — `ZeitManagerLoadingPipeline` ausführen, Fortschritt auf Splash
  posten, **mindestens 800 ms** Splash, dann `MainViewModel` per `GetRequiredService` auflösen
  und als `DataContext` auf das Panel setzen + `splash.FadeOut()`. Fehler werden gefangen →
  FadeOut (kein Leerbildschirm).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `ZeitManager.ViewModels` |
| `Views/` | `ZeitManager.Views` |
| `Services/` | `ZeitManager.Services` |
| `Audio/` | `ZeitManager.Audio` |
| `Graphics/` | `ZeitManager.Graphics` |
| `Loading/` | `ZeitManager.Loading` |
| `Models/` | `ZeitManager.Models` |

## Unterordner

Inhalt und Detail-Doku → [../CLAUDE.md](../CLAUDE.md) (Doku-Karte).

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Amber #F7A833),
`Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Assets/`.

## Build

```bash
dotnet build src/Apps/ZeitManager/ZeitManager.Shared
```
