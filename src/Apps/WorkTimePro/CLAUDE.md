# WorkTimePro — Arbeitszeiterfassung & Export

Vollständige Arbeitszeiterfassung mit Check-in/out, Pausen, Urlaub, Schichtplanung,
Feiertagen (DE/AT/CH), Statistiken (11 eigene + 3 geteilte SkiaSharp-Visualisierungen)
und Export (PDF/Excel/CSV/ICS).

> Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.worktimepro` |
| Plattformen | Android + Desktop |
| Primärfarbe | #4F8BF9 Blau ("Professional Workspace") |

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
WorkTimePro.Android ┐
                    ├─> WorkTimePro.Shared ──> MeineApps.Core.Ava          (Preferences, Localization, ViewLocator)
WorkTimePro.Desktop ┘                       ├─> MeineApps.Core.Premium.Ava (Ads, IAP, Trial)
                                            └─> MeineApps.UI               (SkiaLoadingSplash, FloatingText, Helpers)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `WorkTimePro.Shared/App.axaml.cs`
(DI + Loading-Pipeline + Splash) → `MainViewModel` (5 Tabs + 5 Sub-Pages) → `ViewLocator` löst Views.

**Premium:** 3,99 EUR/Monat oder 19,99 EUR Lifetime. 7 Tage Trial. Rewarded Ads als Gate
für Export und erweiterte Statistiken.

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces | `App.axaml.cs`, alle Service-/VM-Registrierungen, Loading-Flow | [WorkTimePro.Shared](WorkTimePro.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, AdMob-Init, Factories, Immersive, Back-Button | [WorkTimePro.Android](WorkTimePro.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [WorkTimePro.Desktop](WorkTimePro.Desktop/CLAUDE.md) |
| ViewModels (5 Tabs + 5 Sub-Pages) | MainViewModel, Tab-Nav, Sub-Page-Routing, Undo, Earnings-Ticker | [Shared/ViewModels](WorkTimePro.Shared/ViewModels/CLAUDE.md) |
| Views (AXAML, Overlays) | 12 Views, Overlay-Pattern, Keyboard-Shortcuts, Compiled Bindings | [Shared/Views](WorkTimePro.Shared/Views/CLAUDE.md) |
| Services (13 Interfaces) | DB, Zeiterfassung, Export, Backup, Reminder, Notification, ... | [Shared/Services](WorkTimePro.Shared/Services/CLAUDE.md) |
| Models & Enums | SQLite-Entitäten, Enums, AppColors, DateTime-Konvention | [Shared/Models](WorkTimePro.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (11 Visualisierungen) | Timeline, Bars, Gauges, Splash, Background | [Shared/Graphics](WorkTimePro.Shared/Graphics/CLAUDE.md) |
| Custom Controls | `CircularProgressControl` (IsPulsing!) | [Shared/Controls](WorkTimePro.Shared/Controls/CLAUDE.md) |
| Converter | `InvertBool`, `IntToBool`, `RoundingDisplay`, ... | [Shared/Converters](WorkTimePro.Shared/Converters/CLAUDE.md) |
| Helpers | `TimeFormatter`, `Icons` (MDI-Codepoints) | [Shared/Helpers](WorkTimePro.Shared/Helpers/CLAUDE.md) |
| Startup-Pipeline | `WorkTimeProLoadingPipeline` (2 Schritte, Parallel-Init) | [Shared/Loading](WorkTimePro.Shared/Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (AppPalette, Blau #4F8BF9),
`Shared/Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Shared/Assets/`.

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, Localization, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| AdMob, Google Play Billing, Trial, Linked-Files | [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| CircularProgress, SkiaLoadingSplash, FloatingText, Helpers | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
