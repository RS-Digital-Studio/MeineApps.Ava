# FinanzRechner — Vollwertiger Finanz-Manager

Expense-Tracking, Multi-Konto, Budgets, Sparziele, Schulden-Tracker, Finanz-Score, Prognosen,
Daueraufträge und 6 Finanz-Rechner. Alle Charts vollständig in SkiaSharp (kein LiveCharts).

| Aspekt | Wert |
|--------|------|
| Package-ID | com.meineapps.finanzrechner |
| Theme | Smaragd `#10B981` — Living Finance |
| Ads | Banner + Rewarded |
| Premium | 3,99 EUR `remove_ads` |

Generische Build-Befehle, Conventions, MVVM/DI/DateTime/Localization → [Haupt-CLAUDE.md](../../../CLAUDE.md).
Diese Datei beschreibt die **app-weite Sicht** (Features, Navigation, Monetarisierung). Ordner-Details
liegen bei den jeweiligen Unterordner-CLAUDE.md (siehe Doku-Karte).

---

## Doku-Karte — Detail liegt beim jeweiligen Bereich

| Bereich | Inhalt | Doku |
|---------|--------|------|
| Composition Root, DI, Loading-Start, Namespaces | `App.axaml.cs`, Service-/VM-Registrierung | [FinanzRechner.Shared](FinanzRechner.Shared/CLAUDE.md) |
| Android-Host | `AndroidApp`, `MainActivity`, AdMob, Factories, Immersive | [FinanzRechner.Android](FinanzRechner.Android/CLAUDE.md) |
| Desktop-Host | `Program.cs`, `MainWindow` | [FinanzRechner.Desktop](FinanzRechner.Desktop/CLAUDE.md) |
| ViewModels (Cache-Invalidierung, Calculator-Pattern, Back-Press, Insights-Race) | MainViewModel, Sub-VMs, Calculator-VMs | [Shared/ViewModels](FinanzRechner.Shared/ViewModels/CLAUDE.md) |
| Views (Ad-Banner-Layout, Overlay-Pattern, Behaviors, 60-fps-Timer, ItemsControl) | 18 AXAML-Views + Code-Behind | [Shared/Views](FinanzRechner.Shared/Views/CLAUDE.md) |
| Services (Persistenz, Export, Analyse, Transfer-Buchung, Backup v2) | Interface/Impl-Paare | [Shared/Services](FinanzRechner.Shared/Services/CLAUDE.md) |
| Models (Datenmodelle, FinanceEngine, Geldwerte-Konvention `decimal`/`double`) | Expense, Account, Budget, FinanceEngine … | [Shared/Models](FinanzRechner.Shared/Models/CLAUDE.md) |
| SkiaSharp-Renderer (App-Visualisierungen, View→Renderer-Zuordnung) | `Graphics/` | [Shared/Graphics](FinanzRechner.Shared/Graphics/CLAUDE.md) |
| Converter (IValueConverter) | Kategorie, TransactionType, Alert, Balance | [Shared/Converters](FinanzRechner.Shared/Converters/CLAUDE.md) |
| Helpers (CurrencyHelper-Overloads, CategoryLocalizationHelper) | Globale Formatierung + Kategorie-Icons/Farben | [Shared/Helpers](FinanzRechner.Shared/Helpers/CLAUDE.md) |
| Startup-Pipeline (Zwei-Stufen-Modell) | `FinanzRechnerLoadingPipeline` | [Shared/Loading](FinanzRechner.Shared/Loading/CLAUDE.md) |

Reine Asset-/Ressourcen-Ordner ohne eigene Doku: `Shared/Themes/` (`AppPalette.axaml`, Smaragd `#10B981`),
`Shared/Resources/Strings/` (`AppStrings.resx`, 6 Sprachen), `Shared/Assets/`.

---

## Features (app-weite Sicht)

### 4 Tabs

| Tab | Inhalt |
|-----|--------|
| **Home** | Dashboard: Saldo, Einnahmen/Ausgaben, Budget-Status, Prognose, Finanz-Score, Recent Transactions, Calculator-Grid, Quick-Add FAB |
| **Tracker** | Expense-CRUD mit Filter/Sort, Monatsnavigation, Undo-Delete, Swipe-to-Delete |
| **Statistics** | Donut-Charts, TrendLine 6 Monate, Monatsvergleich, CSV/PDF-Export |
| **Settings** | Währung, Backup v2, Restore Merge/Replace, Sprache, Premium, Feedback |

### Sub-Pages (alle via GoBack `".."`)

| Sub-Page | Zweck |
|----------|-------|
| AccountsView | Konten verwalten + Überweisungen zwischen Konten |
| SavingsGoalsView | Sparziele + Einzahlungen/Entnahmen + Celebration bei 100 % |
| DebtTrackerView | Schulden verwalten + Zahlungen buchen + Tilgungsberechnung |
| CustomCategoriesView | Benutzerdefinierte Kategorien mit Icon und Farbe |
| BudgetsView | Budget-Limits pro Kategorie + Alert-Levels + ContextFlyout |
| RecurringTransactionsView | Daueraufträge + Toggle aktiv/inaktiv |

### 6 Finanz-Rechner (im Calculator-Grid auf der Home-View)

| Rechner | ViewModel | Chart |
|---------|-----------|-------|
| Zinseszins | CompoundInterestViewModel | StackedArea |
| Sparplan | SavingsPlanViewModel | StackedArea |
| Kredit | LoanViewModel | Donut |
| Tilgungsplan | AmortizationViewModel | Stacked-Bar |
| Rendite | YieldViewModel | Donut |
| Inflation | InflationViewModel | StackedArea |

Gemeinsames Calculator-Pattern (`XxxValue`/`XxxDisplay`, Gold-Flash, kein HasResult-Guard) →
[Shared/ViewModels](FinanzRechner.Shared/ViewModels/CLAUDE.md).

---

## Monetarisierung

### Ad-Placements (Rewarded)

| Placement-ID | Auslöser | View |
|-------------|----------|------|
| `export_pdf` | PDF-Export | StatisticsView |
| `export_csv` | CSV-Export | ExpenseTrackerView + StatisticsView |
| `budget_analysis` | Monatsreport mit Kategorie-Breakdown + Spartipps | HomeView |
| `extended_stats` | 24h-Zugang zu Quartal/Halbjahr/Jahr-Statistiken | StatisticsView |

Premium-Nutzer (`remove_ads`, 3,99 EUR): keine Ads, direkter Export, unbegrenzter Budget-Report,
permanente erweiterte Statistiken. AdMob/IAP-Mechanik → [Core.Premium](../../Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md).

---

## Build / Test / Deploy

```bash
dotnet build   src/Apps/FinanzRechner/FinanzRechner.Shared
dotnet run     --project src/Apps/FinanzRechner/FinanzRechner.Desktop
dotnet publish src/Apps/FinanzRechner/FinanzRechner.Android -c Release   # AAB
dotnet run     --project tools/AppChecker FinanzRechner
```

---

## Verweise

| Datei | Zweck |
|-------|-------|
| `F:\Meine_Apps_Ava\CLAUDE.md` | Build, Conventions, DI-Patterns, Architektur |
| `src/Libraries/MeineApps.Core.Ava/CLAUDE.md` | Themes, BackPressHelper, UriLauncher, CurrencyHelper-Basis |
| `src/Libraries/MeineApps.Core.Premium.Ava/CLAUDE.md` | AdMob, IAP, RewardedAdHelper |
| `src/UI/MeineApps.UI/CLAUDE.md` | Shared Behaviors, DonutChart, SkiaGradientRing, LinearProgress |
| `Releases/FinanzRechner/CHANGELOG_*.md` | Release-Notes |
