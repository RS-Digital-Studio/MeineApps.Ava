# FitnessRechner (Avalonia)

> Für Build-Befehle, Conventions und Troubleshooting siehe [Haupt-CLAUDE.md](../../../CLAUDE.md)

Fitness-App mit 5 Rechnern (BMI, Kalorien, Wasser, Idealgewicht, Körperfett),
Tracking-Charts, Nahrungsmittel-Suche (114 lokal + Open Food Facts API), Intervallfasten,
Aktivitäts-Tracking, Rezept-Editor und Gamification.

| Aspekt | Wert |
|--------|------|
| Version | v2.0.7 (VersionCode 15) |
| Package-ID | `com.meineapps.fitnessrechner` |
| Premium | 3,99 EUR `remove_ads` (keine Ads, unbegrenzte Barcode-Scans, permanente Extended Food-DB) |
| Ad-Placements | `barcode_scan` (+5 Bonus-Scans), `detail_analysis` (7-Tage-Analyse), `tracking_export` (CSV), `extended_food_db` (24h-Zugang) |
| Theme | VitalOS Medical (Cyan + Teal + Electric Blue) |

---

## Build & Zielframework

| Projekt | Framework | Befehl |
|---------|-----------|--------|
| `FitnessRechner.Shared` | `net10.0` | `dotnet build src/Apps/FitnessRechner/FitnessRechner.Shared` |
| `FitnessRechner.Desktop` | `net10.0` | `dotnet run --project src/Apps/FitnessRechner/FitnessRechner.Desktop` |
| `FitnessRechner.Android` | `net10.0-android` | `dotnet build src/Apps/FitnessRechner/FitnessRechner.Android` |

Release-AAB: `dotnet publish src/Apps/FitnessRechner/FitnessRechner.Android -c Release`

## Namespace-Konvention

| Ordner | Namespace |
|--------|-----------|
| `FitnessRechner.Shared/ViewModels/` | `FitnessRechner.ViewModels` |
| `FitnessRechner.Shared/Views/` | `FitnessRechner.Views` |
| `FitnessRechner.Shared/Services/` | `FitnessRechner.Services` |
| `FitnessRechner.Shared/Models/` | `FitnessRechner.Models` |
| `FitnessRechner.Shared/Graphics/` | `FitnessRechner.Graphics` |
| `FitnessRechner.Shared/Loading/` | `FitnessRechner.Loading` |

---

## Architektur

### Tab-Navigation (4 Tabs)

| Tab | Inhalt |
|-----|--------|
| Home | Dashboard mit VitalSignsHero + Streak-Card + Fasten-Status + Tageszeit-Begrüßung + Quick-Add-Buttons |
| Progress | Tracking + 5 Sub-Tabs (Weight, BMI, BodyFat, Water/Calories, Aktivitäten) |
| Food Search | Lokale + API-Suche + Quick-Add + Rezepte |
| Settings | Benutzerprofil + Erinnerungen + Haptic/Sound-Toggles |

### Calculator-VM Factory-Pattern

Calculator-VMs (`BmiViewModel`, `CaloriesViewModel`, `WaterViewModel`, `IdealWeightViewModel`,
`BodyFatViewModel`) als **Transient** registriert. `MainViewModel` erhält `Func<T>`-Factories
per Constructor-Injection (kein Service-Locator). Jedes Öffnen erzeugt frische VM-Instanz.

### `ProgressViewModel` Partial-Class-Aufteilung

Wegen Größe in 4 Files gesplittet:

| Datei | Inhalt |
|-------|--------|
| `ProgressViewModel.cs` | Felder, Constructor, Properties, Events, Lifecycle, Navigation |
| `ProgressViewModel.Tracking.cs` | Add/Delete/Undo, Load/Refresh, Ziele, Weekly Analysis, Export |
| `ProgressViewModel.Charts.cs` | Chart-Daten, Statistik, Meilensteine, Status-Updates, Meal Grouping |
| `ProgressViewModel.Food.cs` | Food-Search, Add Food, "Gestern kopieren" |

`MainViewModel` ist ebenfalls in 2 Files aufgeteilt:

| Datei | Inhalt |
|-------|--------|
| `MainViewModel.cs` | Kern, DI, Tab-Navigation, Events, Lifecycle |
| `MainViewModel.Dashboard.cs` | Dashboard-Properties, lokalisierte Labels, Gamification, Quick-Add, Heatmap, Weekly Comparison, Evening Summary |

`OnAppearingAsync()` wird beim Tab-Wechsel aufgerufen.

### `PreferenceKeys` (zentral)

`PreferenceKeys.cs` im Shared-Projekt — alle Preference-Keys + Konstanten
(`UndoTimeoutMs = 5000`) zentral. Alle ViewModels + Services referenzieren `PreferenceKeys`
statt lokaler Konstanten.

Key-Gruppen:
- Streak: `streak_current`, `streak_best`, `streak_last_log_date`
- Gamification: `fitness_xp`, `fitness_level`, `achievements_unlocked`,
  `achievements_progress`, `challenge_completed_date`, `total_meals_logged`,
  `total_barcodes_scanned`, `distinct_foods_tracked`, `calculators_used_mask`
- Extended Food DB: `ExtendedFoodDbExpiry` (ISO 8601 UTC mit `DateTimeStyles.RoundtripKind`)

---

## Services

| Service | Zweck |
|---------|-------|
| `TrackingService` | JSON-Persistenz `TrackingEntry`, `IDisposable` mit CTS-Cleanup, `EntryAdded`-Event für Streak |
| `FoodSearchService` | Fuzzy Matching, Favorites, Recipes (generisch FoodItem/Recipe), `FoodLogAdded`-Event, Batch-Methoden `GetFoodLogsInRangeAsync` + `GetDailySummariesInRangeAsync` (N+1 Query Fix) |
| `IStreakService` / `StreakService` | Logging-Streak (aufeinanderfolgende Tage mit Aktivität), Preferences-basiert, Meilenstein-Confetti (3/7/14/21/30/50/75/100/150/200/365) |
| `FoodDatabase` | 114 Nahrungsmittel mit lokalisierten Namen + Aliasen (statische Liste) |
| `BarcodeLookupService` | Open Food Facts API, `_barcodeCache` Dictionary mit `SemaphoreSlim` |
| `IScanLimitService` / `ScanLimitService` | Tages-Limit (3 Scans/Tag), Bonus-Scans via Rewarded Ad |
| `IBarcodeService` | Plattform-Interface (Android: CameraX + ML Kit, Desktop: null → manuelle Eingabe) |
| `IFastingService` / `FastingService` | Intervallfasten (16:8, 18:6, 20:4, Custom), Start/Stop, History (letzte 30 Perioden) |
| `IActivityService` / `ActivityService` | Sport-Tracking mit MET-Werten, JSON-Persistenz `activity_log.json`, Thread-Safe (SemaphoreSlim), `ActivityAdded`-Event |
| `ActivityDatabase` | 30 Aktivitäten in 4 Kategorien (Cardio/Kraft/Sport/Alltag) mit MET-Werten. Formel: `kcal = MET × Gewicht_kg × Dauer_h` |
| `AchievementService` | 20 Achievements in 5 Kategorien (Tracking/Ernährung/Wasser/Körper/Special), Preferences-basiert (JSON), `AchievementUnlocked`-Event |
| `LevelService` | XP-System (Max Level 50), Formel `XpForLevel(n) = 100 × n × (n+1) / 2`, `LevelUp`-Event |
| `ChallengeService` | 10 tägliche Challenges (rotierend nach DayOfYear), `ChallengeCompleted`-Event |
| `IHapticService` | Tick (Ziffern/Tab), Click (Speichern), HeavyClick (Achievement/Level-Up/Ziel) — Settings-Toggle |
| `IFitnessSoundService` | `PlaySuccess` (System-Notification-Sound), Settings-Toggle, Android: MediaPlayer |
| `IReminderService` / `AndroidReminderService` | 3 Erinnerungstypen (Wasser alle 2h, Gewicht täglich, Abend-Zusammenfassung). Android: AlarmManager + NotificationChannel + ReminderReceiver |

---

## Feature-Patterns

### Profil-Vorausfüllung

Benutzerprofil (Größe, Alter, Geschlecht, Aktivitätslevel) in Settings → automatische
Vorausfüllung aller 5 Rechner.

### Mahlzeiten-Gruppierung

Logs nach Typ gruppiert (Frühstück/Mittag/Abend/Snack mit Icons + Subtotals).
"Gestern kopieren"-Funktion in `ProgressViewModel.Food.cs`.

### Quick-Add Kalorien

Blitz-Button im FoodSearch-Header → Quick-Add Panel (Orange Gradient). Kalorien direkt
eingeben ohne Food-Suche, optionaler Name, Mahlzeit-Auswahl. `FoodLogEntry` mit `Grams = 0`.

### Wasser Quick-Add

4 Mengenoptionen: 150ml Tasse, 250ml Glas, 500ml Flasche, 750ml große Flasche.
Wasser-Ziel-Celebration einmal pro Session via `_wasWaterGoalReached`.

### Automatische Makro-Berechnung

Wenn Kalorienziel gesetzt wird → Default-Makros (30% Protein, 40% Carbs, 30% Fett).

### Extended Food DB (24h Premium-Zugang)

Hint-Card erscheint bei ≤ 5 lokalen Ergebnissen für Non-Premium. Rewarded Ad → 24h
permanente erweiterte Suchergebnisse (`maxResults = 200`).

### Calculator-Bitmask für "Alle benutzt"-Achievement

5 Rechner als Bit-Flags: BMI=1, Calories=2, Water=4, IdealWeight=8, BodyFat=16.

### XP-Vergabe

| Aktion | XP |
|--------|-----|
| Gewicht-Eintrag | +10 |
| Mahlzeit-Eintrag | +5 |
| Wasser-Eintrag | +3 |
| Rechner-Nutzung | +2 |
| Achievement | +25–500 |
| Challenge | +20–40 |

### Dashboard Quick-Add (3 SkiaSharp-Buttons)

`QuickActionButtonRenderer`: +kg (lila), +250ml (grün), +kcal (orange). Holografischer
Rand mit 3s Puls-Animation, Press-Effekt (Scale 0.95). Gewicht öffnet Quick-Add Panel
(`NumericUpDown`, Min=20/Max=500, Increment=0.1). Wasser addiert sofort +250ml.
Kalorien wechselt zu FoodSearch-Tab und öffnet Quick-Add Panel.

### Game Juice

`FloatingText` für `+{amount} ml` (Wasser), `+{calories} kcal` (Food), `+{value}` für
Tracking. Confetti bei Wasser-Ziel + Streak-Meilensteinen.

---

## Barcode-Scanner Architektur

| Plattform | Implementation |
|-----------|----------------|
| Android | `BarcodeScannerActivity` (AppCompatActivity) mit CameraX Preview + ML Kit ImageAnalysis. Erkennt EAN-13/EAN-8/UPC-A/UPC-E. Semi-transparentes Overlay mit Scan-Bereich + Ecken-Akzente |
| Desktop | `DesktopBarcodeService` gibt null zurück → `BarcodeScannerView` zeigt manuelle Texteingabe |

**Flow**:
`FoodSearchVM.OpenBarcodeScanner` → `IBarcodeService.ScanBarcodeAsync` →
`NavigationRequested("BarcodeScannerPage?barcode=...")` → `MainVM.CreateCalculatorVm` →
`BarcodeScannerVM.OnBarcodeDetected` → API-Lookup → UseFood → `FoodSelected`-Event →
zurück zu FoodSearch.

**DI-Wiring**: `App.BarcodeServiceFactory` (analog zu `RewardedAdServiceFactory`).
**Packages**: CameraX Camera2/Lifecycle/View 1.5.2.1 + ML Kit BarcodeScanning 117.3.0.5.

**Android-Activity-Result**: `MainActivity.OnActivityResult` + `OnRequestPermissionsResult`
leiten an `AndroidBarcodeService` weiter (`StartActivityForResult` + `TaskCompletionSource`).

---

## VitalOS Design-System

Konzept: High-End Medical Dashboard ("Apple Watch Health trifft Sci-Fi Medical Console").
Full SkiaSharp Immersion für alle visuellen Elemente, XAML nur für native Form-Controls.

### Farbpalette (`MedicalColors.cs`)

| Farbe | Hex | Zweck |
|-------|-----|-------|
| Primär | `#06B6D4` Cyan | Akzent, EKG, Glow |
| Sekundär | `#14B8A6` Teal, `#3B82F6` Electric Blue | Hintergrund-Verläufe |
| Hintergrund | `#142832` → `#0A1824` | Teal Deep / Teal Dark |
| Surface | `#1E3844` | Cards |
| Card-Surface | `#D90F1D32` | Universelle Card-Hintergrund |
| Card-Border | `#1A06B6D4` (Standard), `#4D06B6D4` (holografisch) | Cyan-Glow |
| Weight | `#8B5CF6` Lila | Feature-Farbe |
| BMI | `#3B82F6` Blau | Feature-Farbe |
| Wasser | `#22C55E` Grün | Feature-Farbe |
| Kalorien | `#F59E0B` Amber | Feature-Farbe |

### EKG-Konfiguration

24-Punkt Array (P-Welle + QRS-Komplex + T-Welle + Baseline). Herzschlag: 72 BPM
(1.2 Beats/Sekunde).

### Renderer (`Graphics/`)

| Renderer | Typ | Zweck |
|----------|-----|-------|
| `MedicalColors` | Static | Farben, EKG-Daten, Timing-Konstanten |
| `MedicalBackgroundRenderer` | Instance | 5-Layer Hintergrund (Gradient, Grid, EKG, Partikel, Vignette) |
| `MedicalTabBarRenderer` | Instance | Holografische Tab-Bar (64dp, 4 Tabs, Cyan-Glow) |
| `MedicalCardRenderer` | Static | Universeller Card-Hintergrund (Surface + HUD-Brackets + Akzent) |
| `VitalSignsHeroRenderer` | Instance | Dashboard Vital Signs Monitor (300dp, 4 Quadranten, EKG-Ring, Center-Score) |
| `QuickActionButtonRenderer` | Static | Holografische Quick-Action Buttons |
| `StreakCardRenderer` | Static | Medical Streak-Anzeige (pulsierendes Herz, Mini-EKG) |
| `ChallengeCardRenderer` | Static | Medical Challenge-Card (Indigo-Gradient, Scan-Line Progress) |
| `LevelProgressRenderer` | Static | Medical XP/Level-Bar (Cyan-Badge + Gradient + Scan-Line) |
| `CalculatorHeaderRenderer` | Static | Header für alle 5 Rechner (Feature-Gradient + Grid + EKG, holografischer Back-Button) |
| `BmiGaugeRenderer` | Static | BMI-Gauge (Medical Grid + Nadel-Glow + Scan-Line) |
| `BodyFatRenderer` | Static | Körperfett-Grafik (Cyan-Kontur + Scan-Linie + Prozent-Ring Glow) |
| `CalorieRingRenderer` | Static | Kalorien-Ringe (Medical Grid + 72BPM Glow + Data-Stream Partikel) |
| `HealthTrendVisualization` | Static | Catmull-Rom Spline mit Gradient-Fill, Target-Zones, Milestones |
| `WeeklyCaloriesBarVisualization` | Static | Gradient-Balken mit Target-Linie |
| `FitnessRechnerSplashRenderer` | Splash | EKG-Herzschlag-Splash |

### Render-Loop

| Pfad | Loop |
|------|------|
| `MainView` | DispatcherTimer 33ms (~30 FPS) → `_backgroundRenderer` + `_tabBarRenderer` |
| `HomeView` | `OnRenderTick(float)` von `MainView` aufgerufen → invalidiert VitalSigns, QuickButtons, Level, Challenge, Streak |
| Calculator-Views | Kein Render-Loop (`time = 0f` → statischer Snapshot, Animationen vorbereitet) |

### Touch-HitTest-Pattern

`e.GetPosition(canvas)` → DPI-Skalierung (`lastBounds.Width / canvas.Bounds.Width`) →
SkiaSharp-Koordinaten → HitTest. Spezialfälle:
- `VitalSignsHero`: Winkelberechnung (`atan2`) für Quadrant-Erkennung
- `TabBar`: Positions-Berechnung (`bounds.Width / 4`) für Tab-Erkennung
- `CalculatorHeader`: `IsBackButtonHit()` mit Radius-Toleranz

### Rohwert-Properties im `MainViewModel`

`RawWeight`, `RawBmi`, `RawWaterMl`, `RawWaterGoalMl`, `RawCalories`, `RawCalorieGoal`,
`WeightTrend`, `BmiCategoryText` — direkter Zugriff für Renderer ohne Converter.

### Medical-Styling (XAML-Cards)

Empty-State, Badges, Heatmap, Weekly Comparison, Disclaimer, Calculator-Buttons nutzen
Surface `#D90F1D32` + Cyan-Border `#1A06B6D4`. Weight Quick-Add + Evening Summary haben
holografischen Cyan-Rand `#4D06B6D4`. Calculator-Buttons: `#D90F1D32` statt
CardColor-Gradient, Feature-Farben auf Icon-Container beibehalten.

---

## Verweise

- [Haupt-CLAUDE.md](../../../CLAUDE.md) — Build-Befehle, Conventions, Troubleshooting
- [MeineApps.UI/CLAUDE.md](../../UI/MeineApps.UI/CLAUDE.md) — Shared UI Components
- [MeineApps.Core.Ava/CLAUDE.md](../../Libraries/MeineApps.Core.Ava/CLAUDE.md) — `IHapticService`, `BackPressHelper`, Converters
- `Releases/FitnessRechner/CHANGELOG_*.md` — Release-Notes
