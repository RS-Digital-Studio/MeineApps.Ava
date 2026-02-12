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
- **Barcode Scanner**: Nativer CameraX + ML Kit Scanner (Android), manuelle Eingabe (Desktop), Open Food Facts API (BarcodeLookupService)

## App-spezifische Services

- **TrackingService**: JSON-Persistenz (TrackingEntry Model), IDisposable mit CancellationTokenSource Cleanup
- **FoodSearchService**: Fuzzy Matching, Favorites, Recipes (generisch fuer FoodItem/Recipe)
- **FoodDatabase**: 114 Nahrungsmittel mit lokalisierten Namen + Aliase (statische Liste)
- **BarcodeLookupService**: Open Food Facts API, _barcodeCache Dictionary mit SemaphoreSlim
- **UndoService<T>**: Generischer Undo mit Timeout (VMs erstellen eigene Instanzen, nicht per DI)
- **IScanLimitService / ScanLimitService**: Tages-Limit (3 Scans/Tag), Bonus-Scans via Rewarded Ad
- **IBarcodeService**: Plattform-Interface fuer nativen Barcode-Scan (Android: CameraX + ML Kit, Desktop: null → manuelle Eingabe)

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

### Barcode-Scanner Architektur (11.02.2026)
- **Android**: `BarcodeScannerActivity` (AppCompatActivity) mit CameraX Preview + ML Kit ImageAnalysis
  - Erkennt EAN-13, EAN-8, UPC-A, UPC-E
  - Semi-transparentes Overlay mit Scan-Bereich + Ecken-Akzente
  - `AndroidBarcodeService` → `StartActivityForResult` + `TaskCompletionSource`
  - `MainActivity.OnActivityResult` + `OnRequestPermissionsResult` leiten an Service weiter
- **Desktop**: `DesktopBarcodeService` gibt null zurueck → `BarcodeScannerView` zeigt manuelle Texteingabe
- **Flow**: FoodSearchVM.OpenBarcodeScanner → IBarcodeService.ScanBarcodeAsync → NavigationRequested("BarcodeScannerPage?barcode=...") → MainVM.CreateCalculatorVm → BarcodeScannerVM.OnBarcodeDetected → API-Lookup → UseFood → FoodSelected Event → zurueck zu FoodSearch
- **DI**: `App.BarcodeServiceFactory` (analog zu RewardedAdServiceFactory)
- **Packages**: CameraX Camera2/Lifecycle/View 1.5.2.1 + ML Kit BarcodeScanning 117.3.0.5

## Changelog (Highlights)

- **11.02.2026 (3)**: Optimierungen: Input-Range-Validierung in allen 5 Rechnern (BMI/Kalorien/Wasser/Idealgewicht/Körperfett), BodyFat Log10-Guard in FitnessEngine (Navy-Methode crashte bei Waist≤Neck), HasDashboardData Bug (nie zurückgesetzt), 7-Tage-Report parallelisiert (Task.WhenAll statt sequentieller Schleife), BarcodeScannerActivity Hint-Text lokalisiert (6 Sprachen), IdealWeightViewModel MessageRequested Event ergänzt, 3 neue RESX-Keys (AlertInvalidInput, AlertBodyFatMeasurement, BarcodeScanHint)
- **11.02.2026**: Bugfixes: WaterViewModel speicherte Ziel in Liter statt Milliliter (Progress-Bar sofort 100% nach einem Glas), ProgressViewModel FoodLog DateTime.Now→DateTime.Today, BarcodeLookupService Cache-Timing DateTime.Now→DateTime.UtcNow
- **11.02.2026**: Nativer Barcode-Scanner (CameraX + ML Kit) mit BarcodeScannerView, manuelle Eingabe auf Desktop, FoodSearchVM → MainVM Navigation verdrahtet
- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, HomeView Redesign, LanguageChanged Fix
- **06.02.2026**: Deep Code Review (Preferences Key-Mismatch, DateTime.Today, IDisposable, BarcodeLookupService Thread-Safety)
