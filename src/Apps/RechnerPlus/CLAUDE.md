# RechnerPlus

> Build-Befehle, Conventions, Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

Scientific Calculator mit Unit Converter — werbefrei, kostenlos, kein IAP.

**Package:** `com.meineapps.rechnerplus`

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
RechnerPlus.Android ┐
                    ├─> RechnerPlus.Shared ──> MeineApps.CalcLib   (Engine/Parser/History)
RechnerPlus.Desktop ┘                       ├─> MeineApps.Core.Ava (Preferences, Localization, ViewLocator)
                                            └─> MeineApps.UI       (SkiaLoadingSplash, FloatingText, Helpers)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `RechnerPlus.Shared/App.axaml.cs`
(DI + Loading-Pipeline + Splash) → `MainViewModel` (3 Tabs) → `ViewLocator` löst die Views.
**Werbefrei** → keine `MeineApps.Core.Premium.Ava`-Referenz.

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces | `App.axaml.cs`, Service-/VM-Registrierung, Loading-Start | [RechnerPlus.Shared](RechnerPlus.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Haptic, Manifest, Immersive | [RechnerPlus.Android](RechnerPlus.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [RechnerPlus.Desktop](RechnerPlus.Desktop/CLAUDE.md) |
| Rechen-/Display-Logik, Converter, Gotchas | MainVM, CalculatorVM (+Partials), ConverterVM | [Shared/ViewModels](RechnerPlus.Shared/ViewModels/CLAUDE.md) |
| UI-Patterns (Button-Grid, Keyboard, Landscape) | MainView, CalculatorView, … | [Shared/Views](RechnerPlus.Shared/Views/CLAUDE.md) |
| SkiaSharp-Renderer (VFD, Burst, Graph) | `Graphics/` | [Shared/Graphics](RechnerPlus.Shared/Graphics/CLAUDE.md) |
| Custom Controls | `ExpressionHighlightControl` | [Shared/Controls](RechnerPlus.Shared/Controls/CLAUDE.md) |
| Startup-Pipeline | `RechnerPlusLoadingPipeline` | [Shared/Loading](RechnerPlus.Shared/Loading/CLAUDE.md) |
| Farbpalette | `AppPalette.axaml` (Indigo #7C7FF7) | [Shared/Themes](RechnerPlus.Shared/Themes/CLAUDE.md) |
| Lokalisierung (6 Sprachen) | `AppStrings.resx` | [Shared/Resources](RechnerPlus.Shared/Resources/CLAUDE.md) |

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Calculator-Engine, ExpressionParser, IHistoryService | [MeineApps.CalcLib](../../Libraries/MeineApps.CalcLib/CLAUDE.md) |
| Preferences, Localization, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| FloatingTextOverlay, SkiaLoadingSplash, SkiaThemeHelper | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
