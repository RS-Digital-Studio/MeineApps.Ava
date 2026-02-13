# FitnessRechner (Avalonia)

> Fuer Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

## App-Beschreibung

Fitness-App mit 5 Rechnern (BMI, Kalorien, Wasser, Idealgewicht, Koerperfett), Tracking mit Charts und Nahrungsmittel-Suche (114 Foods + Barcode-Scanner).

**Version:** 2.0.0 | **Package-ID:** com.meineapps.fitnessrechner | **Status:** Geschlossener Test

## Features

- **4 Tabs**: Home (Dashboard + Streak-Card + Tageszeit-Begrüßung), Progress (Tracking + 4 Sub-Tabs), Food Search (+ Quick-Add), Settings
- **5 Rechner**: BMI, Calories, Water, IdealWeight, BodyFat
- **Tracking**: Gewicht (+ Gewichtsziel mit ProgressBar), BMI, Koerperfett, Wasser, Kalorien (JSON-basiert, TrackingService)
- **Charts**: LiveCharts (LineSeries fuer Gewicht/BMI/BodyFat, ColumnSeries fuer Wochen-Kalorien), Chart-Zeitraum wählbar (7T/30T/90T)
- **Mahlzeiten**: Gruppiert nach Typ (Frühstück/Mittag/Abend/Snack mit Icons + Subtotals), "Gestern kopieren" Funktion
- **Food Search**: Fuzzy Matching, Favorites, Recipes (FoodDatabase mit 114 Items + Aliase)
- **Barcode Scanner**: Nativer CameraX + ML Kit Scanner (Android), manuelle Eingabe (Desktop), Open Food Facts API (BarcodeLookupService)

## App-spezifische Services

- **TrackingService**: JSON-Persistenz (TrackingEntry Model), IDisposable mit CancellationTokenSource Cleanup, `EntryAdded`-Event fuer Streak
- **FoodSearchService**: Fuzzy Matching, Favorites, Recipes (generisch fuer FoodItem/Recipe), `FoodLogAdded`-Event fuer Streak
- **StreakService**: Logging-Streak (aufeinanderfolgende Tage mit Aktivitaet), Preferences-basiert, Meilenstein-Confetti (3/7/14/21/30/50/75/100/150/200/365 Tage)
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

### Dashboard Fortschrittsbalken
- Kalorien + Wasser Cards haben ProgressBar (4px, farbig passend zur Card)
- `CalorieProgress` / `WaterProgress` (0-100) in MainViewModel berechnet
- ProgressBars bei Value=0 ausgeblendet (`HasWaterProgress`/`HasCalorieProgress`)

### Dashboard Quick-Add
- 3 Gradient-Buttons zwischen Dashboard-Card und Streak-Card: +kg (lila), +250ml (grün), +kcal (orange)
- **Gewicht**: Öffnet Quick-Add Panel (NumericUpDown, Min=20/Max=500, Increment=0.1), speichert via TrackingService
- **Wasser**: Sofort +250ml addieren, Wasser-Ziel Celebration prüfen
- **Kalorien**: Wechselt zu FoodSearch-Tab und öffnet Quick-Add Panel

## Changelog (Highlights)

- **13.02.2026 (10)**: Phase 6 Polish: IHapticService (Tick/Click/HeavyClick, Android Vibrator), IFitnessSoundService (System-Sound bei Erfolgen), IReminderService (3 Typen: Wasser/Gewicht/Abend, AlarmManager + NotificationChannel), Abend-Zusammenfassung Dashboard-Card (nach 20 Uhr), Settings-Toggles (Haptic/Sound/3 Reminders). 10 neue RESX-Keys in 6 Sprachen.
- **13.02.2026 (9)**: Phase 5 Gamification: AchievementService (20 Badges in 5 Kategorien), LevelService (XP/Level-System, Max 50), ChallengeService (10 tägliche Challenges), AchievementsView Fullscreen-Overlay, Dashboard XP-Bar + Challenge-Card + Badge-Reihe + Wochenvergleich, LocalizeKeyConverter, 73 neue RESX-Keys in 6 Sprachen.
- **13.02.2026 (8)**: Phase 4 Charts: HeatmapCalendar Control (Aktivitäts-Heatmap wie GitHub), Trend-Pfeil (TrendingUp/Down/Flat mit Puls-Animation), Chart-Morphing (AnimationsSpeed 300ms), StreakService.GetActivityDates(). 8 neue RESX-Keys.
- **13.02.2026 (7)**: Phase 3 Food Search UX: SkeletonLoader (Shimmer-Animation), FoodCategory-Icons + Farben (7 Kategorien), SwipeToRevealBehavior (Slide-to-Delete), Recent Foods Chips. 3 neue RESX-Keys.
- **13.02.2026 (6)**: Phase 2 Dashboard Redesign: CircularProgress Ringe (Wasser grün, Kalorien orange), Tages-Score Ring (kombinierter Fortschritt), Motivationszitate (15, täglich wechselnd). 20 neue RESX-Keys.
- **13.02.2026 (5)**: Phase 1 Animationen: CountUpBehavior, StaggerFadeInBehavior, Page-Slide-Transition, ProgressBar smooth, Streak-Flamme Puls, TapScaleBehavior auf allen Cards.
- **13.02.2026 (4)**: Dashboard-Fixes: ProgressBar bei Value=0 ausgeblendet (HasWaterProgress/HasCalorieProgress), Streak FloatingText bei jedem täglichen Update (nicht nur Meilensteinen), Dashboard Quick-Add (3 Buttons: +kg Gewicht-Panel, +250ml Wasser direkt, +kcal wechselt zu FoodSearch). 2 neue RESX-Keys (StreakIncreased, QuickAddWeight) in 6 Sprachen.
- **13.02.2026 (3)**: Double-Back-to-Exit (Android): Zurück-Taste navigiert stufenweise zurück (Calculator schließen → Overlay schließen → Home-Tab), erst bei doppeltem Drücken innerhalb 2s wird App beendet (Toast-Hinweis). TryGoBack() in MainViewModel, OnBackPressed() in MainActivity, PressBackAgainToExit RESX-Key in 6 Sprachen.
- **13.02.2026 (2)**: UX-Verbesserungen (7 Features): Gewichtsziel-Tracking (Ziel setzen, ProgressBar, Confetti bei Erreichen, Fortschritts-Status), Chart-Zeitraum wählbar (7T/30T/90T Toggle in Weight+Body Tabs, Preferences-persistent), Mahlzeiten nach Typ gruppiert (Frühstück/Mittag/Abend/Snack mit Icons + Zwischensummen, wiederverwendbares DataTemplate), Wochen-Kalorien Balkendiagramm (ColumnSeries, 7-Tage Übersicht), Mahlzeiten von Gestern kopieren (CopyYesterdayMeals Command), Tageszeit-Begrüßung auf Dashboard (Morgen/Tag/Abend lokalisiert), Motivierende leere Zustände (Icon + Hinweistext für Weight/BMI/BodyFat/Kalorien). 22 neue RESX-Keys in 6 Sprachen.
- **13.02.2026**: Deep Code Review Runde 4 (12 Issues): FoodSearchService Read-Locks (GetFoodLogAsync, GetArchivableEntriesCountAsync, IsFavoriteAsync, GetFavoritesAsync, GetRecipesAsync alle mit Lock geschützt), Archive-Filter .Date-Konsistenz, SaveFoodLogToFileAsync transaktional (temp+move Pattern), FoodSearchService+TrackingService IDisposable (SemaphoreSlim-Cleanup), AndroidBarcodeService TCS-Guard (alter Scan wird bei doppeltem Aufruf cancelled), CaloriesVM UpdateLocalizedTexts (ActivityLevels bei Sprachwechsel), SettingsView DetachedFromVisualTree Event-Cleanup, ProgressView "Body Fat"+Watermark lokalisiert, FoodItem.Id unnötige GUID-Generierung entfernt (leerer String als Default)
- **12.02.2026 (7)**: Deep Code Review Runde 3b (6 weitere Issues): FoodItem deterministische IDs für statische DB-Einträge (stabil über App-Neustarts, Name-basiert statt zufällige GUIDs), BarcodeScannerActivity Paint-Leak behoben (cornerPaint in Konstruktor statt OnDraw-Alloc pro Frame), BarcodeAnalyzer Scanner.Close() in Dispose (ML Kit Ressourcen-Freigabe), MainView OnDetachedFromVisualTree Event-Cleanup (FloatingText+Celebration), HistoryVM+ProgressVM Undo-Flicker bei LoadData verhindert (Pending-Delete-Eintrag/Meal beim Laden gefiltert), TrackingService Backup-Throttling (max. alle 1 Minute statt bei jedem Save, reduziert Disk-IO bei Quick-Add)
- **12.02.2026 (6)**: Deep Code Review Runde 3 (8 Issues): StreakService ParseExact→TryParseExact mit Fallback (Crash bei korrupten Preferences), HistoryVM Tab-Wechsel committed pendenten Undo + SaveEntry Bereichsvalidierung (BMI/BodyFat≤100) + CTS Dispose vor Ersetzung, ScanLimitService Thread-Safety (lock-Objekt für _remainingScans), UndoService<T> CTS Dispose vor Ersetzung + in CancelUndo, HomeView async void try-catch (App-Crash-Prävention), FoodDatabase 8 Duplikate entfernt (Brötchen, Müsli, Bier, Butter, Kekse, Popcorn, Gummibärchen, Döner Kebab) + Pizza-Alias bereinigt + Aliases gemergt (Gummy Bears, Hafermüsli), ProgressVM Tab-Wechsel Undo-Commit + DeleteEntry/DeleteMeal Cross-Cleanup + RecalculateCalorieDataFromMeals für konsistente UI während Undo-Phase
- **12.02.2026 (5)**: Deep Code Review Runde 2 (10 Issues): [KRITISCH] FoodSearchVM.OnAppearing() nie aufgerufen beim Tab-Wechsel (Favorites/Log nie geladen), ProgressVM MacroGoals nie aus Preferences geladen (Fortschrittsbalken immer 0%), FoodSearchVM.DeleteLogEntry Summary-Inkonsistenz während Undo-Phase (RecalculateSummaryFromTodayLog statt DB-Read), Export ToDictionary Crash bei Duplikaten (GroupBy+Last). [HOCH] DateTime.Now→UtcNow in FavoriteFoodEntry+Recipe Models, WaterVM hardcodierter "daily_water_goal"→PreferenceKeys.WaterGoal, MainVM alle Lambda-Event-Handler→Named Methods + vollständige Abmeldung in Dispose (AdUnavailable, AdsStateChanged, FloatingText, Celebration, FoodSearchNavigation), TrackingService Read-Operationen mit _writeLock geschützt (ClearAllAsync ebenfalls), BarcodeScannerVM IDisposable implementiert (CTS-Lifecycle), ProgressVM.AddEntry Bereichsvalidierung (Weight≤500, BMI/BodyFat≤100, Water≤20000)
- **12.02.2026 (4)**: Deep Code Review Bugfixes (8 Issues): BarcodeLookupService Lock-Architektur komplett umstrukturiert (kein fragiles Release/Reacquire mehr, separate Lock-Blöcke für Cache-Read + Cache-Write), MainViewModel.Dispose() LanguageChanged Event-Leak behoben, FoodSearchService komplett mit Locking geschützt (SaveFoodLog, DeleteFoodLog, Archive, SaveFavorite, RemoveFavorite, IncrementFavorite, SaveRecipe, DeleteRecipe, IncrementRecipe, UpdateRecipe), FoodSearchViewModel Search-Query Race Condition (captured query statt Property-Read), PerformExtendedSearch Code-Duplikation eliminiert (delegiert an PerformSearch), ProgressViewModel Export 90→5 parallele Tasks (SemaphoreSlim Throttling)
- **12.02.2026 (3)**: 3 neue Features: Logging-Streak Service (StreakService, Preferences-basiert, Meilenstein-Confetti, Streak-Card auf Dashboard), Quick-Add Kalorien (Blitz-Button im FoodSearch-Header, direkte kcal-Eingabe ohne Food-Suche), Dashboard-Fortschrittsbalken (Kalorien + Wasser ProgressBars unter den Cards). 7 Bugfixes aus Code-Review. EntryAdded/FoodLogAdded Events fuer automatische Streak-Aktualisierung. 12 neue RESX-Keys in 6 Sprachen.
- **12.02.2026 (2)**: Code-Qualität: PreferenceKeys.cs zentral (eliminiert 4x Key-Duplikation), Undo-Timeout vereinheitlicht (8000→5000ms), Export parallelisiert (90x Task.WhenAll), Barcode-Kategorien 6-sprachig (EN/DE/ES/FR/IT/PT), Datumsformate lokalisiert (CultureInfo statt hardcoded dd.MM), TrackingService Date-Vergleich defensiv (.Date)
- **12.02.2026**: Bugfix: ProgressView "Lebensmittel hinzufügen" Button (Inline Food Search UI fehlte), Gradient-Header für alle Stat-Cards (Weight=Purple, Calories=Orange-Red, Water=Cyan-Blue, BMI=Blue, BodyFat=Orange), ScrollViewer-Padding optimiert (alle Views), 5 fehlende RESX-Keys ergänzt (Input, BrocaWeight, CreffWeight, Years, Minutes) in 6 Sprachen
- **11.02.2026 (3)**: Optimierungen: Input-Range-Validierung in allen 5 Rechnern (BMI/Kalorien/Wasser/Idealgewicht/Körperfett), BodyFat Log10-Guard in FitnessEngine (Navy-Methode crashte bei Waist≤Neck), HasDashboardData Bug (nie zurückgesetzt), 7-Tage-Report parallelisiert (Task.WhenAll statt sequentieller Schleife), BarcodeScannerActivity Hint-Text lokalisiert (6 Sprachen), IdealWeightViewModel MessageRequested Event ergänzt, 3 neue RESX-Keys (AlertInvalidInput, AlertBodyFatMeasurement, BarcodeScanHint)
- **11.02.2026**: Bugfixes: WaterViewModel speicherte Ziel in Liter statt Milliliter (Progress-Bar sofort 100% nach einem Glas), ProgressViewModel FoodLog DateTime.Now→DateTime.Today, BarcodeLookupService Cache-Timing DateTime.Now→DateTime.UtcNow
- **11.02.2026**: Nativer Barcode-Scanner (CameraX + ML Kit) mit BarcodeScannerView, manuelle Eingabe auf Desktop, FoodSearchVM → MainVM Navigation verdrahtet
- **08.02.2026**: FloatingTextOverlay + CelebrationOverlay (Game Juice)
- **07.02.2026**: 4 Rewarded Ad Features, HomeView Redesign, LanguageChanged Fix
- **06.02.2026**: Deep Code Review (Preferences Key-Mismatch, DateTime.Today, IDisposable, BarcodeLookupService Thread-Safety)
