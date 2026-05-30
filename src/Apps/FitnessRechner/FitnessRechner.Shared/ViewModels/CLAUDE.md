# ViewModels — UI-Logik & Tab-Navigation

Alle Haupt-ViewModels sind **Singleton**, Calculator-VMs **Transient** (in `App.axaml.cs`
registriert). Berechnungen delegieren immer an `IFitnessEngine` — kein direktes `Math.*` in VMs.
Generische MVVM-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Zweck |
|-------|-------|
| `MainViewModel.cs` | Kern: DI, Tab-Navigation (4 Tabs), Calculator-Navigation, Back-Navigation, Events, Dispose |
| `MainViewModel.Dashboard.cs` | Dashboard-Properties (`RawWeight`, `RawBmi`, ...), lokalisierte Labels, Gamification, Quick-Add, Heatmap, Weekly Comparison, Evening Summary |
| `ProgressViewModel.cs` | Felder, Constructor, Properties, Events, Lifecycle, Navigation, `OnAppearingAsync()` |
| `ProgressViewModel.Tracking.cs` | Add/Delete/Undo, Load/Refresh, Ziele, Weekly Analysis, Export |
| `ProgressViewModel.Charts.cs` | Chart-Daten, Statistik, Meilensteine, Status-Updates, Meal Grouping |
| `ProgressViewModel.Food.cs` | Food-Search, Add Food, "Gestern kopieren" |
| `FoodSearchViewModel.cs` | Nahrungsmittel-Suche (lokal + API), Quick-Add-Panel, Rezepte, Barcode-Flow |
| `SettingsViewModel.cs` | Benutzerprofil (Größe/Alter/Geschlecht/Aktivitätslevel), Erinnerungen, Haptic/Sound-Toggle, Profil-Vorausfüllung |
| `RecipeViewModel.cs` | Rezept-Editor (erstellen, bearbeiten, löschen, als Mahlzeit hinzufügen) |
| `FastingViewModel.cs` | Intervallfasten (16:8, 18:6, 20:4, Custom), Start/Stop, History (letzte 30 Perioden) |
| `ActivityViewModel.cs` | Sport-Tracking: Aktivität auswählen, Dauer eingeben, kcal berechnen, History |
| `BarcodeScannerViewModel.cs` | Barcode-Scan-Flow: `IBarcodeService.ScanBarcodeAsync` → API-Lookup → `FoodSelected`-Event |
| `Calculators/BmiViewModel.cs` | BMI-Rechner (Metric/Imperial), Profil-Vorausfüllung, `IFitnessEngine.CalculateBmi` |
| `Calculators/CaloriesViewModel.cs` | Kalorien-Rechner (Mifflin-St Jeor), Aktivitätslevel-Auswahl |
| `Calculators/WaterViewModel.cs` | Wasserbedarf-Rechner, Sport + Hitze-Zuschlag |
| `Calculators/IdealWeightViewModel.cs` | Idealgewicht (Broca + Creff + BMI-Range) |
| `Calculators/BodyFatViewModel.cs` | Körperfett Navy-Methode (Männer: Taille/Hals; Frauen: + Hüfte) |

---

## Tab-Navigation (`MainViewModel`)

4 Tabs: **Home** (`IsHomeTabActive`), **Progress** (`IsProgressTabActive`),
**Food Search** (`IsFoodTabActive`), **Settings** (`IsSettingsTabActive`).

`OnAppearingAsync()` wird bei jedem Tab-Wechsel auf dem Ziel-ViewModel aufgerufen — lädt
Dashboard-Daten, Streak, Level, Challenge neu.

### Calculator-VM Factory-Pattern

Calculator-VMs sind **Transient**: jedes Öffnen erzeugt eine frische Instanz (kein staler
Zustand). `MainViewModel` erhält `Func<T>`-Factories per Constructor-Injection:

```csharp
// Richtig: Factory aufrufen, kein App.Services.GetRequiredService<T>()
var bmiVm = _bmiVmFactory();
bmiVm.NavigationRequested += route => CurrentPage = route;
```

Wenn der Rechner geschlossen wird, wird das VM verworfen → kein Event-Leak nötig (Events
automatisch GC'd, weil VM kurzlebig ist).

---

## `MainViewModel.Dashboard.cs` — Rohwert-Properties

Direkter Zugriff für SkiaSharp-Renderer ohne Converter:

| Property | Typ | Bedeutung |
|----------|-----|-----------|
| `RawWeight` | `double` | Aktuelles Gewicht in kg |
| `RawBmi` | `double` | Aktueller BMI-Wert |
| `RawWaterMl` | `int` | Heutige Wasser-Einnahme in ml |
| `RawWaterGoalMl` | `int` | Tagesziel Wasser in ml |
| `RawCalories` | `int` | Heutige Kalorien |
| `RawCalorieGoal` | `int` | Tagesziel Kalorien |
| `WeightTrend` | `double` | Differenz zum Vortag (+ Zunahme, - Abnahme) |
| `BmiCategoryText` | `string` | Lokalisierte BMI-Kategorie |

---

## `ProgressViewModel` Partial-Class-Aufteilung

Wegen Größe in 4 Files gesplittet. Partials teilen dieselbe Klasse — alle Fields sind in
`ProgressViewModel.cs` definiert, Partial-Files erweitern nur Methoden.

`ProgressViewModel.Food.cs` enthält "Gestern kopieren": kopiert alle `FoodLogEntry` des Vortags
ins aktuelle Datum — Meal-Typen bleiben erhalten, Datums-Timestamp wird auf Today gesetzt.

---

## Back-Navigation (`MainViewModel.HandleBackPressed`)

1. Offener Calculator → Calculator schließen, zurück zu Tab-Übersicht.
2. Barcode-Scanner aktiv → abbrechen.
3. Nicht auf Home-Tab → `IsHomeTabActive = true`.
4. Home-Tab → Double-Back-to-Exit via `BackPressHelper`.

---

## `PreferenceKeys` (zentral)

`PreferenceKeys.cs` im Shared-Root — alle Preference-Keys + Konstanten
(`UndoTimeoutMs = 5000`) zentral. Alle ViewModels + Services referenzieren `PreferenceKeys`
statt lokaler Konstanten.

Key-Gruppen:
- **Streak:** `streak_current`, `streak_best`, `streak_last_log_date`
- **Gamification:** `fitness_xp`, `fitness_level`, `achievements_unlocked`,
  `achievements_progress`, `challenge_completed_date`, `total_meals_logged`,
  `total_barcodes_scanned`, `distinct_foods_tracked`, `calculators_used_mask`
- **Extended Food DB:** `ExtendedFoodDbExpiry` (ISO 8601 UTC, `DateTimeStyles.RoundtripKind`)

### Calculator-Bitmask

5 Rechner als Bit-Flags für "Alle benutzt"-Achievement:
`BMI=1, Calories=2, Water=4, IdealWeight=8, BodyFat=16`. Wird in `SettingsViewModel` oder den
Calculator-VMs per OR-Verknüpfung in Preferences gesetzt.

---

## Gamification — Ereignis-Kette

```
TrackingService.EntryAdded → StreakService.UpdateStreak → StreakService.StreakMilestoneReached
                                                        → MainViewModel.CelebrationRequested (Confetti)

AchievementService.AchievementUnlocked → LevelService.AddXp → LevelService.LevelUp
                                       → MainViewModel.FloatingTextRequested ("+XP")

ChallengeService.ChallengeCompleted → LevelService.AddXp
```

---

## Gotchas

- **`Func<T>` Factory statt Service-Locator:** Calculator-VMs werden über `Func<BmiViewModel>` etc.
  erzeugt — kein `App.Services.GetRequiredService<T>()`. Die `Func<T>`-Registrierung in
  `App.axaml.cs` schließt den Kreis sauber.
- **`ProgressViewModel.OnAppearingAsync()`** muss immer `await`-bar sein — wird vom MainVM
  bei Tab-Wechsel via `_ = vm.OnAppearingAsync()` aufgerufen (fire-and-forget auf UI-Thread).
- **`SettingsViewModel` Profile-Vorausfüllung:** Alle 5 Calculator-VMs lesen Profil aus
  `IPreferencesService` beim Öffnen — Änderungen in Settings sind sofort beim nächsten
  Rechner-Öffnen sichtbar (Transient-Pattern).
