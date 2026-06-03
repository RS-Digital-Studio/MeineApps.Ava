# FitnessRechner

> Build-Befehle, Conventions und Architektur → [Haupt-CLAUDE.md](../../../CLAUDE.md)

Fitness-App mit 5 Rechnern (BMI, Kalorien, Wasser, Idealgewicht, Körperfett),
Tracking-Charts, Nahrungsmittel-Suche (99 lokal + Open Food Facts API), Intervallfasten,
Aktivitäts-Tracking, Rezept-Editor und Gamification.

| Aspekt | Wert |
|--------|------|
| Package-ID | `com.meineapps.fitnessrechner` |
| Premium | 3,99 EUR `remove_ads` (keine Ads, unbegrenzte Barcode-Scans, permanente Extended Food-DB) |
| Ad-Placements | `barcode_scan` (+5 Bonus-Scans), `detail_analysis` (7-Tage-Analyse), `tracking_export` (CSV), `extended_food_db` (24h-Zugang) |
| Theme | VitalOS Medical (Cyan #06B6D4 + Teal #14B8A6 + Electric Blue #3B82F6) |

---

## Architektur-Überblick

```
FitnessRechner.Android ─┐
                        ├─> FitnessRechner.Shared ──> MeineApps.Core.Ava        (Preferences, Localization, ViewLocator)
FitnessRechner.Desktop ─┘                          ├─> MeineApps.Core.Premium.Ava (Ads, IAP, AdConfig, Linked-Files)
                                                   └─> MeineApps.UI              (SkiaLoadingSplash, FloatingText, Helpers)
```

Composition-Flow: Host (`AndroidApp` / `Program.cs`) → `FitnessRechner.Shared/App.axaml.cs`
(DI + Loading-Pipeline + Splash) → `MainViewModel` (4 Tabs + Calculator-Navigation) →
`ViewLocator` löst die Views.

**Werbe-App** → referenziert `MeineApps.Core.Premium.Ava` (Banner-Ad `56dp`, Rewarded Ads).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Namespaces | `App.axaml.cs`, Service-/VM-Registrierung, Loading-Pipeline | [FitnessRechner.Shared](FitnessRechner.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, Barcode, Reminder, AdMob, Manifest | [FitnessRechner.Android](FitnessRechner.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs` | [FitnessRechner.Desktop](FitnessRechner.Desktop/CLAUDE.md) |
| ViewModels | MainVM (+ Dashboard-Partial), ProgressVM (4 Partials), Calculator-VMs | [Shared/ViewModels](FitnessRechner.Shared/ViewModels/CLAUDE.md) |
| Views | AXAML-Views, Barcode-Flow, Calculator-Views | [Shared/Views](FitnessRechner.Shared/Views/CLAUDE.md) |
| SkiaSharp-Renderer | VitalOS Medical Design System (15 Renderer + MedicalColors) | [Shared/Graphics](FitnessRechner.Shared/Graphics/CLAUDE.md) |
| Services | Domain-Services (Tracking, Food, Gamification, Fasting, Activity, Reminders) | [Shared/Services](FitnessRechner.Shared/Services/CLAUDE.md) |
| Models | Datenmodelle, FitnessEngine (5 Berechnungen), Result-Records | [Shared/Models](FitnessRechner.Shared/Models/CLAUDE.md) |
| Converters | Tab-Farb-, Food-Kategorie- und Utility-Converter | [Shared/Converters](FitnessRechner.Shared/Converters/CLAUDE.md) |
| Loading | Startup-Pipeline | [Shared/Loading](FitnessRechner.Shared/Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (AppPalette, Cyan #06B6D4),
`Shared/Resources/Strings/` (AppStrings.resx, 6 Sprachen), `Shared/Assets/`.

---

## Verweise

| Was | Wo |
|-----|----|
| Build, Conventions, Architektur | [Haupt-CLAUDE.md](../../../CLAUDE.md) |
| Preferences, Localization, BackPressHelper, ViewLocator | [MeineApps.Core.Ava](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) |
| AdMob, IAP, AdConfig, Linked-Files | [MeineApps.Core.Premium.Ava](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md) |
| FloatingTextOverlay, SkiaLoadingSplash, SkiaThemeHelper | [MeineApps.UI](../../UI/MeineApps.UI/CLAUDE.md) |
| Release-Notes | `Releases/FitnessRechner/CHANGELOG_*.md` |
