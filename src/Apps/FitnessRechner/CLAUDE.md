# FitnessRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Fitness-App mit 5 Rechnern (BMI, Kalorien, Wasser, Idealgewicht, Koerperfett), Tracking mit Charts und Nahrungsmittel-Suche (114 Foods + Barcode-Scanner).

**Version:** 2.0.6 | **Package-ID:** com.meineapps.fitnessrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Streak-Card + Tageszeit-Begrüßung), Progress (Tracking + 4 Sub-Tabs), Food Search (+ Quick-Add), Settings
- **5 Rechner**: BMI, Calories, Water, IdealWeight, BodyFat
- **Tracking**: Gewicht (+ Gewichtsziel mit ProgressBar), BMI, Koerperfett, Wasser, Kalorien (JSON-basiert, TrackingService)
- **Charts**: SkiaSharp (HealthTrendVisualization für Gewicht/BMI/BodyFat, WeeklyCaloriesBarVisualization für Wochen-Kalorien), Chart-Zeitraum wählbar (7T/30T/90T)
- **Mahlzeiten**: Gruppiert nach Typ (Frühstück/Mittag/Abend/Snack mit Icons + Subtotals), "Gestern kopieren" Funktion
- **Food Search**: Fuzzy Matching, Favorites, Recipes (FoodDatabase mit 114 Items + Aliase)
- **Barcode Scanner**: Nativer CameraX + ML Kit Scanner (Android), manuelle Eingabe (Desktop), Open Food Facts API (BarcodeLookupService)
- **SkiaSharp-Visualisierungen**: BMI-Gauge (BmiGaugeRenderer, Medical Grid + Nadel-Glow + Scan-Line), Körperfett-Grafik (BodyFatRenderer, Cyan-Kontur + Scan-Linie + Prozent-Ring Glow), Kalorien-Ringe (CalorieRingRenderer, Medical Grid + pulsierender 72BPM Glow + Data-Stream Partikel), Wasserglas (inline in WaterView), HealthTrendVisualization (Catmull-Rom Spline mit Gradient-Fill, Target-Zones, Milestones), WeeklyCaloriesBarVisualization (Gradient-Balken mit Target-Linie), FitnessRechnerSplashRenderer (EKG-Herzschlag-Splash), VitalSignsHeroRenderer (kreisförmiger Monitor mit EKG-Ring, 4 Quadranten, Center-Score, Data-Stream Partikel), MedicalBackgroundRenderer (animierter Hintergrund), MedicalTabBarRenderer (holografische Tab-Bar), MedicalCardRenderer (glassmorphe Cards), CalculatorHeaderRenderer (Medical-Style Header für alle 5 Rechner mit Feature-Farb-Gradient, Medical Grid, Mini-EKG, holografischem Back-Button). HomeView nutzt VitalSignsHeroRenderer (ersetzt Hero-Header + Score-Card + Dashboard-Grid) + LinearProgressVisualization (XP-Bar, Challenge-Bar). Alle 3 Calculator-Renderer haben optionalen `time`-Parameter (Default 0f) für Animationen (Render-Loop wird in späterem Task aktiviert)
- **Medical-Styling (XAML-Cards)**: Alle verbleibenden Cards (Empty-State, Badges, Heatmap, Weekly Comparison, Disclaimer, Calculator-Buttons) nutzen Surface `#D90F1D32` + Cyan-Border `#1A06B6D4`. Weight Quick-Add + Evening Summary haben holografischen Cyan-Rand `#4D06B6D4`. Calculator-Buttons: `#D90F1D32` statt CardColor-Gradient, Feature-Farben auf Icon-Container beibehalten

## App-spezifische Services

- **TrackingService**: JSON-Persistenz (TrackingEntry Model), IDisposable mit CancellationTokenSource Cleanup, `EntryAdded`-Event fuer Streak
- **FoodSearchService**: Fuzzy Matching, Favorites, Recipes (generisch fuer FoodItem/Recipe), `FoodLogAdded`-Event fuer Streak, Batch-Methoden `GetFoodLogsInRangeAsync` + `GetDailySummariesInRangeAsync` (N+1 Query Fix)
- **IStreakService / StreakService**: Logging-Streak (aufeinanderfolgende Tage mit Aktivitaet), Preferences-basiert, Meilenstein-Confetti (3/7/14/21/30/50/75/100/150/200/365 Tage)
- **FoodDatabase**: 114 Nahrungsmittel mit lokalisierten Namen + Aliase (statische Liste)
- **BarcodeLookupService**: Open Food Facts API, _barcodeCache Dictionary mit SemaphoreSlim
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

### Calculator-VM Factory Pattern
- Calculator-VMs (BmiViewModel, CaloriesViewModel, etc.) als Transient registriert
- MainViewModel erhaelt `Func<T>` Factories per Constructor Injection (kein Service-Locator)
- Jedes Oeffnen eines Rechners erzeugt eine frische VM-Instanz

### ProgressView Sub-Tabs
- 4 Sub-Tabs: Weight, BMI, BodyFat, Water/Calories
- `ProgressViewModel.OnAppearingAsync()` wird beim Tab-Wechsel aufgerufen
- **Partial-Class-Aufteilung** (4 Dateien):
  - `ProgressViewModel.cs` → Felder, Constructor, Properties, Events, Lifecycle, Navigation Commands
  - `ProgressViewModel.Tracking.cs` → Add/Delete/Undo, Load/Refresh, Ziele, Weekly Analysis, Export
  - `ProgressViewModel.Charts.cs` → Chart-Daten, Statistik, Meilensteine, Status-Updates, Meal Grouping
  - `ProgressViewModel.Food.cs` → Food-Search, Add Food, "Gestern kopieren"

### Extended Food DB (24h Zugang)
- **Ablauf-Key**: `extended_food_db_expiry` mit ISO 8601 UTC + `DateTimeStyles.RoundtripKind`
- **Hint-Card**: Zeigt "Mehr laden" bei <=5 lokalen Ergebnissen fuer Non-Premium

### Game Juice
- **FloatingText**: "+{amount} ml" (Wasser), "+{calories} kcal" (Food), "+{value} kg/BMI/%" (Tracking)
- **Celebration**: Confetti bei Wasser-Zielerreichung (einmal pro Session via `_wasWaterGoalReached`) + Streak-Meilensteine

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

### PreferenceKeys (zentral)
- `PreferenceKeys.cs` im Shared-Projekt: Alle Preference-Keys + Konstanten (UndoTimeoutMs=5000) zentral definiert
- Alle ViewModels + ScanLimitService + StreakService referenzieren PreferenceKeys statt lokaler Konstanten
- Streak-Keys: `streak_current`, `streak_best`, `streak_last_log_date`
- Gamification-Keys: `fitness_xp`, `fitness_level`, `achievements_unlocked`, `achievements_progress`, `challenge_completed_date`, `total_meals_logged`, `total_barcodes_scanned`, `distinct_foods_tracked`, `calculators_used_mask`

### Quick-Add Kalorien
- Blitz-Button im FoodSearch-Header → Quick-Add Panel (Orange Gradient)
- Kalorien direkt eingeben ohne Food-Suche, optionaler Name, Mahlzeit-Auswahl
- `FoodSearchViewModel.ConfirmQuickAdd()` → `FoodLogEntry` mit Grams=0

### Gamification (Phase 5)
- **AchievementService**: 20 Achievements in 5 Kategorien (Tracking/Ernährung/Wasser/Körper/Special), Preferences-basiert (JSON), `AchievementUnlocked`-Event
- **LevelService**: XP-System (Max Level 50), Formel `XpForLevel(n) = 100*n*(n+1)/2`, Preferences-basiert, `LevelUp`-Event
- **ChallengeService**: 10 tägliche Challenges (rotierend nach DayOfYear), `ChallengeCompleted`-Event
- **AchievementsView**: Fullscreen-Overlay (WrapPanel Grid), freigeschaltet=Gradient-Icon, gesperrt=grau+Fortschrittsbalken
- **LocalizeKeyConverter**: Konvertiert RESX-Keys in lokalisierte Texte (für Achievement-Titel/Beschreibungen in DataTemplates)
- **Dashboard-Elemente**: XP/Level-Bar, Daily Challenge Card (lila Gradient), Badge-Reihe (letzte 3), Wochenvergleich-Card (Kalorien/Wasser/Gewicht/Logging-Tage)
- **XP-Vergabe**: Gewicht +10, Mahlzeit +5, Wasser +3, Rechner +2, Achievement +50-200, Challenge +25-50
- **Calculator-Bitmask**: 5 Rechner als Bit-Flags (BMI=1, Calories=2, Water=4, IdealWeight=8, BodyFat=16) für "Alle benutzt"-Achievement

### Polish & Platform Features (Phase 6)
- **IHapticService**: Tick/Click/HeavyClick, IsEnabled Toggle in Settings, Android: Vibrator + HapticFeedback Fallback
- **IFitnessSoundService**: PlaySuccess (System-Notification-Sound), IsEnabled Toggle in Settings, Android: MediaPlayer
- **IReminderService / ReminderService**: 3 Erinnerungstypen (Wasser alle 2h, Gewicht täglich, Abend-Zusammenfassung), Preferences-basiert
- **AndroidReminderService**: AlarmManager + NotificationChannel + ReminderReceiver BroadcastReceiver
- **Haptic-Trigger**: Quick-Add=Tick, Speichern=Click, Achievement/Level-Up/Ziel-Erreichung=HeavyClick
- **Sound-Trigger**: Achievement, Level-Up, Challenge, Wasser-Ziel, Streak-Meilenstein
- **Abend-Zusammenfassung**: Dashboard-Card nach 20 Uhr (Kalorien|Wasser|Gewicht + Bewertung: Super/Gut/Morgen besser)
- **Settings-Toggles**: Haptic, Sound, 3 Reminder (Wasser/Gewicht/Abend) mit ToggleSwitch

### VitalOS Design-System (Phase 7, 04.03.2026)

**Konzept:** High-End Medical Dashboard ("Apple Watch Health trifft Sci-Fi Medical Console"). Full SkiaSharp Immersion für alle visuellen Elemente, XAML nur für native Form-Controls.

**Farbpalette:** `MedicalColors.cs` - zentrale Farbkonstanten
- Primär: Cyan `#06B6D4`, Teal `#14B8A6`, Electric Blue `#3B82F6`
- Hintergrund: Teal Deep `#142832` → Teal Dark `#0A1824`, Surface `#1E3844`
- Feature: Weight=Lila `#8B5CF6`, BMI=Blau `#3B82F6`, Wasser=Grün `#22C55E`, Kalorien=Amber `#F59E0B`
- EKG-Wellenform: 24-Punkt Array (P-Welle + QRS-Komplex + T-Welle + Baseline)
- Herzschlag: 72 BPM (1.2 Beats/Sekunde)

**Renderer (10 Dateien in `Graphics/`):**

| Renderer | Typ | Zweck |
|----------|-----|-------|
| `MedicalColors.cs` | Static | Farben, EKG-Daten, Timing-Konstanten |
| `MedicalBackgroundRenderer.cs` | Instance | 5-Layer Hintergrund (Gradient, Grid, EKG, Partikel, Vignette) |
| `MedicalTabBarRenderer.cs` | Instance | Holografische Tab-Bar (64dp, 4 Tabs, Cyan-Glow) |
| `MedicalCardRenderer.cs` | Static | Universeller Card-Hintergrund (Surface + HUD-Brackets + Akzent) |
| `VitalSignsHeroRenderer.cs` | Instance | Dashboard Vital Signs Monitor (300dp, 4 Quadranten, EKG-Ring) |
| `QuickActionButtonRenderer.cs` | Static | Holografische Quick-Action Buttons (+kg, +250ml, +kcal) |
| `StreakCardRenderer.cs` | Static | Medical Streak-Anzeige (pulsierendes Herz, Mini-EKG) |
| `ChallengeCardRenderer.cs` | Static | Medical Challenge-Card (Indigo-Gradient, Scan-Line Progress) |
| `LevelProgressRenderer.cs` | Static | Medical XP/Level-Bar (Cyan-Badge + Gradient-Bar + Scan-Line) |
| `CalculatorHeaderRenderer.cs` | Static | Medical Header für alle 5 Rechner (Feature-Gradient + Grid + EKG) |

**Render-Loop:**
- MainView: DispatcherTimer 33ms (30fps), `_backgroundRenderer` + `_tabBarRenderer`
- HomeView: `OnRenderTick(float)` von MainView aufgerufen → invalidiert VitalSigns, QuickButtons, Level, Challenge, Streak
- Calculator-Views: Kein Render-Loop (time=0f → statischer Snapshot, Animationen vorbereitet)

**Touch-HitTest Pattern:**
- `e.GetPosition(canvas)` → DPI-Skalierung (`lastBounds.Width / canvas.Bounds.Width`) → SkiaSharp-Koordinaten → HitTest
- VitalSignsHero: Winkelberechnung (atan2) für Quadrant-Erkennung
- TabBar: Positions-Berechnung (bounds.Width / 4) für Tab-Erkennung
- CalculatorHeader: IsBackButtonHit() mit Radius-Toleranz

**Rohwert-Properties im MainViewModel:**
`RawWeight`, `RawBmi`, `RawWaterMl`, `RawWaterGoalMl`, `RawCalories`, `RawCalorieGoal`, `WeightTrend`, `BmiCategoryText`

### Dashboard Quick-Add
- 3 SkiaSharp-Buttons (QuickActionButtonRenderer): +kg (lila), +250ml (grün), +kcal (orange)
- Holografischer Rand mit 3s Puls-Animation, Press-Effekt (Scale 0.95)
- **Gewicht**: Öffnet Quick-Add Panel (NumericUpDown, Min=20/Max=500, Increment=0.1), speichert via TrackingService
- **Wasser**: Sofort +250ml addieren, Wasser-Ziel Celebration prüfen
- **Kalorien**: Wechselt zu FoodSearch-Tab und öffnet Quick-Add Panel
