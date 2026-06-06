# SunSeeker.Shared — Composition Root & App-Logik

Plattformneutrales Shared-Projekt (`net10.0`). Enthält die gesamte App-Logik (Engine, ViewModels,
Views, Grafik, Modelle, Lokalisierung) und wird von `SunSeeker.Android` und `SunSeeker.Desktop`
referenziert. Generische Conventions → [Haupt-CLAUDE.md](../../../../CLAUDE.md). App-Überblick → [../CLAUDE.md](../CLAUDE.md).

---

## Composition Root (`App.axaml.cs`)

Einziger Ort, an dem Services + ViewModels verdrahtet werden (kein Service-Locator anderswo).

### Plattform-Factory-Properties (LAZY ausgewertet)

| Property | Setter | Fallback |
|----------|--------|---------|
| `App.LocationServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `MockLocationService` |
| `App.HeadingServiceFactory` | `MainActivity.OnCreate` (vor `base.OnCreate`) | `MockHeadingService` |
| `App.AnkerMonitorServiceFactory` | (kein Android-Override nötig — plattformneutral) | `AnkerMonitorService` (echt; Demo-Fallback ohne Zugangsdaten) |

**KRITISCH (Avalonia 12 Android):** `OnFrameworkInitializationCompleted` (DI-Build) läuft VOR
`MainActivity.OnCreate`. Daher die Plattform-Services **lazy** im Resolve-Lambda registrieren und
`MainViewModel` auf Android im `IActivityApplicationLifetime.MainViewFactory`-Lambda auflösen.
Generisches Pattern → [Core.Ava-CLAUDE.md](../../../Libraries/MeineApps.Core.Ava/CLAUDE.md) „Threading & Lifecycle".

### `ConfigureServices` — Reihenfolge

1. `IPreferencesService` (`PreferencesService("SunSeeker")`) + `ILocalizationService`
   (`LocalizationService(AppStrings.ResourceManager, prefs)`).
2. Plattform-Services LAZY: `ILocationService`, `IHeadingService`; `MockAnkerMonitorService` (Demo-Quelle)
   + `IAnkerMonitorService` → `AnkerMonitorService` (echte Anker-Cloud/MQTT, Demo-Fallback).
3. Engine (Singleton): `ISolarPositionService`, `IAlignmentService`, `IBifacialService`.
4. ViewModels (Singleton): `DashboardViewModel`, `AlignViewModel`, `LivePowerViewModel`, `MainViewModel`.

### `OnFrameworkInitializationCompleted`

DI bauen → `ILocalizationService.Initialize()` + `LocalizationManager.Initialize()` (Gerätesprache,
VOR der ersten View) → Lifetime-Branch (Desktop sofort / Android `MainViewFactory` / iOS-Fallback).

---

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `ViewModels/` | `SunSeeker.Shared.ViewModels` |
| `Views/` | `SunSeeker.Shared.Views` |
| `Services/` | `SunSeeker.Shared.Services` |
| `Models/` | `SunSeeker.Shared.Models` |
| `Graphics/` | `SunSeeker.Shared.Graphics` |
| `Resources/Strings/` | `SunSeeker.Shared.Resources.Strings` |

---

## Unterordner

| Ordner | Inhalt | Doku |
|--------|--------|------|
| `Services/` | Engine (Solar/Alignment/Bifacial) + Plattform-Service-Interfaces + Mocks | [Services/CLAUDE.md](Services/CLAUDE.md) |
| `ViewModels/` | Navigator + Dashboard/Align/LivePower (Tab-Lifecycle, Lokalisierung) | [ViewModels/CLAUDE.md](ViewModels/CLAUDE.md) |
| `Views/` | AXAML-Views, SKCanvasView-Pattern, `{loc:Translate}` | [Views/CLAUDE.md](Views/CLAUDE.md) |
| `Models/` | Immutable Records (Solar/Alignment/Bifacial/Power) | [Models/CLAUDE.md](Models/CLAUDE.md) |
| `Graphics/` | 3 SkiaSharp-Renderer (Kompass, Sonnenbahn, Power-Chart) | [Graphics/CLAUDE.md](Graphics/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner (keine eigene Doku): `Themes/` (`AppPalette.axaml`, Sonnen-Amber #FFB300),
`Resources/Strings/` (`AppStrings.*.resx`, 6 Sprachen — DE/neutral-EN/ES/FR/IT/PT, plus `AppStrings.cs`
mit dem ResourceManager).

---

## Build

```bash
dotnet build src/Apps/SunSeeker/SunSeeker.Shared
dotnet run   --project src/Apps/SunSeeker/SunSeeker.Desktop
```
