# GardenControl.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte Client-Logik (ViewModels,
Views, Services, Controls) und wird von `GardenControl.Android` und `GardenControl.Desktop`
referenziert. Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md).
App-Überblick → [../CLAUDE.md](../CLAUDE.md).

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

- **`Initialize()`** — XAML laden + Pi-Erkennung via `DetectRaspberryPi()` (`/proc/device-tree/model`,
  nur auf Linux/ARM64 — liefert `App.IsRunningOnPi`).
- **`OnFrameworkInitializationCompleted()`** — DI bauen, alle Services + VMs als **Singleton**:
  - Services: `IConnectionService` → `ConnectionService`, `IApiService` → `ApiService`.
  - ViewModels: `MainViewModel`, `DashboardViewModel`, `ZoneControlViewModel`,
    `ScheduleViewModel`, `CalibrationViewModel`, `HistoryViewModel`, `SettingsViewModel`.
  - Danach sofort `_mainVm.InitializeAsync()` (Verbindungsaufbau).

**Pi-Kiosk-Zweig** (Desktop auf Pi): `App.IsRunningOnPi == true` → `ServerUrl = "http://localhost:5000"`,
`Window.WindowState = FullScreen`, `WindowDecorations = None`. Auf dem Desktop-Entwicklungsrechner
öffnet sich ein normales 1200×800-Fenster.

**Android-Zweig** (`IActivityApplicationLifetime`): `MainViewFactory = () => new MainView { DataContext = _mainVm }`.
Avalonia 12 ruft die Factory pro Activity-Instanz neu auf — `_mainVm` bleibt Singleton im DI-Container.

**iOS-Fallback** (`ISingleViewApplicationLifetime`): `singleView.MainView = new MainView { DataContext = _mainVm }`.
Derselbe Singleton-`_mainVm` — keine separate Initialisierung nötig.

**Cleanup**: `desktop.ShutdownRequested` → `await _mainVm.DisposeAsync()` (SignalR-Verbindung sauber trennen).

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `GardenControl.Shared.ViewModels` |
| `Views/` | `GardenControl.Shared.Views` |
| `Services/` | `GardenControl.Shared.Services` |
| `Controls/` | `GardenControl.Shared.Controls` |

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `ViewModels/` | MainViewModel + 6 Tab-VMs, Verbindungsmanagement, Back-Press | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views + View-seitige Converter | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Services/` | SignalR-Client, REST-API-Client | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `Controls/` | SkiaSharp-Custom Controls (Gauge, Chart) | [Controls/CLAUDE.md](Controls/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Grün #2E7D32),
`Assets/` (Bild-Assets).

## Build

```bash
dotnet build src/Apps/GardenControl/GardenControl.Shared
```
