# HandwerkerRechner (Avalonia)

> Build-Befehle, Conventions und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

Handwerker-App mit 19 Rechnern (alle frei zugänglich), Projektverwaltung,
Angebots-Generator, Vorlagen und Einheiten-Umrechnung. Premium-Modell = nur "remove_ads".

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.handwerkerrechner` |
| Theme | Blueprint Professional (`#3B82F6` Blau) |
| Premium | 3,99 EUR `remove_ads` (alle 19 Rechner sind frei zugänglich) |
| Ad-Placements | `material_pdf`, `project_export` (Rewarded); Banner (Tab-Bar-Höhe 56dp) |

---

## Architektur-Überblick

Drei Projekte, ViewModel-First, kein Service-Locator:

```
HandwerkerRechner.Android ┐
                          ├─> HandwerkerRechner.Shared ──> MeineApps.Core.Ava     (Preferences, Localization, ViewLocator)
HandwerkerRechner.Desktop ┘                            ├─> MeineApps.Core.Premium.Ava (Ads, IAP)
                                                       ├─> MeineApps.UI            (SkiaLoadingSplash, FloatingText, SkiaBlueprintCanvas)
                                                       └─> CraftEngine             (Berechnungslogik in Models/CraftEngine.cs)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `HandwerkerRechner.Shared/App.axaml.cs`
(DI + Loading-Pipeline + Splash) → `MainViewModel` (4 Tabs + Calculator-Overlay) → `ViewLocator`
löst die 19 Calculator-Views auf.

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces | `App.axaml.cs`, alle Service-/VM-Registrierungen, Loading-Start | [HandwerkerRechner.Shared](HandwerkerRechner.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, AdMob-Setup, Linked-Files, Immersive | [HandwerkerRechner.Android](HandwerkerRechner.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [HandwerkerRechner.Desktop](HandwerkerRechner.Desktop/CLAUDE.md) |
| ViewModels (25) | MainViewModel, ICalculatorViewModel, 19 Calculator-VMs + 6 Business-VMs, Debounce-Pattern, Back-Press | [Shared/ViewModels](HandwerkerRechner.Shared/ViewModels/CLAUDE.md) |
| Views (26 .axaml + CalculatorViewBase) | CalculatorViewBase, Floor/Premium-Views, ViewLocator-Routing, Hintergrund-Loop | [Shared/Views](HandwerkerRechner.Shared/Views/CLAUDE.md) |
| Services (7 App-Services) | ProjectService, QuoteService, CalculatorFactoryService, Export, Favoriten | [Shared/Services](HandwerkerRechner.Shared/Services/CLAUDE.md) |
| Domänen-Modelle & CraftEngine | 19 Berechnungsalgorithmen, Result-Records, Plausibilitäts-Bounds | [Shared/Models](HandwerkerRechner.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (23) | 21 Visualisierungen + Splash-Renderer + BlueprintBackground | [Shared/Graphics](HandwerkerRechner.Shared/Graphics/CLAUDE.md) |
| Startup-Pipeline | `HandwerkerRechnerLoadingPipeline`, Shader-Preload, VM-Instanziierung | [Shared/Loading](HandwerkerRechner.Shared/Loading/CLAUDE.md) |
| XAML-Converter | `IsNotNullConverter`, `IntEqualsConverter` | [Shared/Converters](HandwerkerRechner.Shared/Converters/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (AppPalette, Blau #3B82F6),
`Shared/Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Shared/Assets/`.

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| `SkiaBlueprintCanvas`, `SkiaThemeHelper`, `AnimatedVisualizationBase` | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
| `CalculationHistoryService`, `IUnitConverterService` | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| AdMob/Billing, Linked-Files, Rewarded-Pattern | [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| `Releases/HandwerkerRechner/CHANGELOG_*.md` | Release-Notes |
