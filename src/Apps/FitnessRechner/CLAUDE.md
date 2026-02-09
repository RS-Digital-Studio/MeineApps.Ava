# FitnessRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Fitness-App mit 5 Rechnern (BMI, Kalorien, Wasser, Idealgewicht, Koerperfett), Tracking mit Charts und Nahrungsmittel-Suche (114 Foods + Barcode-Scanner).

**Version:** 2.0.0 | **Package-ID:** com.meineapps.fitnessrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard), Progress (Tracking + 4 Sub-Tabs), Food Search, Settings
- **5 Rechner**: BMI, Calories, Water, IdealWeight, BodyFat
- **Tracking**: Gewicht, BMI, Koerperfett, Wasser, Kalorien (JSON-basiert, TrackingService)
- **Charts**: LiveCharts (LineSeries fuer Gewicht/BMI/BodyFat/Water/Calories)
- **Food Search**: Fuzzy Matching, Favorites, Recipes (FoodDatabase mit 114 Items + Aliase)
- **Barcode Scanner**: Open Food Facts API (BarcodeLookupService mit SemaphoreSlim)

## App-spezifische Services

- **TrackingService**: JSON-Persistenz (TrackingEntry Model), IDisposable mit CancellationTokenSource Cleanup
- **FoodSearchService**: Fuzzy Matching, Favorites, Recipes (generisch fuer FoodItem/Recipe)
- **FoodDatabase**: 114 Nahrungsmittel mit lokalisierten Namen + Aliase (statische Liste)
- **BarcodeLookupService**: Open Food Facts API, _barcodeCache Dictionary mit SemaphoreSlim
- **UndoService<T>**: Generischer Undo mit Timeout (VMs erstellen eigene Instanzen, nicht per DI)
- **IScanLimitService / ScanLimitService**: Tages-Limit (3 Scans/Tag), Bonus-Scans via Rewarded Ad

## Premium & Ads

### Ad-Placements (Rewarded)
1. **barcode_scan**: +5 Bonus-Scans (FoodSearchView)
2. **detail_analysis**: 7-Tage-Analyse (ProgressView)
3. **tracking_export**: CSV-Export (ProgressView)
4. **extended_food_db**: 24h-Zugang zu erweiterten Suchergebnissen (maxResults=200)

### Premium-Modell
- **Preis**: 3,99 EUR (`remove_ads`)
- **Vorteile**: Keine Ads, unbegrenzte Barcode-Scans, permanente erweiterte Food-DB, direkter Export/Analyse

## Besondere Architektur

### IDisposable Pattern
- **TrackingViewModel**: IDisposable fuer CancellationTokenSource Cleanup (Dispose-Pattern mit `_disposed` Flag)
- **LiveCharts Fix**: IDisposable statt Finalizer verhindert SIGSEGV (MAUI-Bug geloest)

### ProgressView Sub-Tabs
- 4 Sub-Tabs: Weight, BMI, BodyFat, Water/Calories
- `ProgressViewModel.OnAppearingAsync()` wird beim Tab-Wechsel aufgerufen

### Extended Food DB (24h Zugang)
- **Ablauf-Key**: `extended_food_db_expiry` mit ISO 8601 UTC + `DateTimeStyles.RoundtripKind`
- **Hint-Card**: Zeigt "Mehr laden" bei <=5 lokalen Ergebnissen fuer Non-Premium

### Game Juice
- **FloatingText**: "+{amount} ml" (Wasser), "+{calories} kcal" (Food), "+{value} kg/BMI/%" (Tracking)
- **Celebration**: Confetti bei Wasser-Zielerreichung (einmal pro Session via `_wasWaterGoalReached`)

## Changelog (Highlights)

- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, HomeView Redesign, LanguageChanged Fix
- **06.02.2026**: Deep Code Review (Preferences Key-Mismatch, DateTime.Today, IDisposable, BarcodeLookupService Thread-Safety)
