# Models — Datenmodelle & Berechnungs-Engine

Reine Datenstrukturen und die plattformunabhängige Berechnungs-Engine. Kein UI, kein Service,
keine Persistenz-Logik. Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Inhalt |
|-------|--------|
| `FitnessEngine.cs` + `IFitnessEngine.cs` | Berechnungs-Engine für alle 5 Rechner + alle Enums + Result-Records |
| `TrackingEntry.cs` | `TrackingEntry` (Id, Date, Type, Value, Note), `TrackingType` enum (Weight/Bmi/Water/BodyFat), `TrackingStats` record |
| `FoodItem.cs` | `FoodItem`, `FoodCategory` enum (13 Typen), `FoodSearchResult`, `FoodLogEntry`, `MealType` enum, `DailyNutritionSummary` record |
| `Recipe.cs` | `Recipe` mit `List<RecipeIngredient>`, Portionen, berechneten Nährwert-Properties (Total + PerServing) |
| `FavoriteFoodEntry.cs` | Favoriten-Eintrag (Id, Food, AddedAt, TimesUsed) — kein FoodItem-Snapshot, direkte Referenz |
| `MacroGoals.cs` | `MacroGoals` (ProteinGrams/CarbsGrams/FatGrams + TotalCalories + GetDistribution), `MacroDistribution` record, `MacroProgress` record, `DailyMacroSummary` record |
| `ActivityEntry.cs` | Sport-Eintrag: ActivityName, DurationMinutes, CaloriesBurned, MetValue |
| `FastingRecord.cs` | `FastingRecord` (StartTime, EndTime UTC, Plan, FastingHours, IsCompleted) + `FastingPlan` enum (Plan16_8, Plan18_6, Plan20_4, Custom) |
| `FitnessAchievement.cs` | `FitnessAchievement` (Id, TitleKey, DescriptionKey, Icon, AchievementCategory, TargetValue, CurrentValue, IsUnlocked, UnlockedAt, XpReward, Progress) + `AchievementCategory` enum |
| `DailyChallenge.cs` | `DailyChallenge` (Id, TitleKey, DescriptionKey, Icon, TargetValue, CurrentValue, IsCompleted, XpReward, Progress) — wechselt täglich per DayOfYear |
| `VersionedData.cs` | `VersionedData<T>` (Version, LastModified, Data), `DataSchemaVersions` (Konstanten: FoodLog/Favorites/Tracking/BarcodeCache), `IDataMigration<T>` |

---

## `FitnessEngine` — Berechnungsformeln

### BMI
```
BMI = Gewicht_kg / (Größe_m)²
Kategorien: SevereUnderweight (<16), ModerateUnderweight (<17), MildUnderweight (<18.5),
            Normal (<25), Overweight (<30), ObeseClass1 (<35), ObeseClass2 (<40), ObeseClass3 (≥40)
```

### Kalorien (Mifflin-St Jeor)
```
Männer: BMR = (10 × kg) + (6.25 × cm) - (5 × Alter) + 5
Frauen: BMR = (10 × kg) + (6.25 × cm) - (5 × Alter) - 161
TDEE = BMR × Aktivitätslevel (1.2–1.9)
WeightLossCalories = TDEE - 500, WeightGainCalories = TDEE + 500  (je 0.5 kg/Woche)
```

### Wasserbedarf
```
Basis = Gewicht_kg × 0.033 L
Sport = (Minuten / 30) × 0.35 L
Hitze = +0.5 L
Glasses = ceil(Total / 0.25)  (250ml Gläser)
```

### Idealgewicht
```
Broca:  kg = (Größe_cm - 100); Frauen × 0.85
Creff:  kg = (Größe_cm - 100 + Alter/10) × 0.9; Frauen × 0.9
BMI-Range: 18.5 × m² bis 24.9 × m²
AverageIdeal = (BrocaWeight + CreffWeight) / 2
```

### Körperfett (Navy-Methode)
```
Männer: 86.010 × log10(Taille - Hals) - 70.041 × log10(Größe) + 36.76
Frauen: 163.205 × log10(Taille + Hüfte - Hals) - 97.684 × log10(Größe) - 78.387
Begrenzt auf 0–60 %.
Kategorien (Männer): Essential (<6%), Athletes (<14%), Fitness (<18%), Average (<25%), Obese (≥25%)
Kategorien (Frauen): Essential (<14%), Athletes (<21%), Fitness (<25%), Average (<32%), Obese (≥32%)
```

**Gotcha Navy-Methode:** `diff = Taille - Hals` (Männer) bzw. `Taille + Hüfte - Hals` (Frauen)
muss positiv sein (sonst `log10` undefined). Engine gibt `BodyFatPercent = 0, Category = Essential`
zurück wenn `diff ≤ 0` — kein Exception-Werfen.

---

## `FoodItem` — Struktur

```csharp
FoodItem { Id, Name, Aliases[], Category, CaloriesPer100g, ProteinPer100g,
           CarbsPer100g, FatPer100g, FiberPer100g, DefaultPortion, DefaultPortionGrams }
```

`FoodLogEntry.Grams = 0` kennzeichnet Quick-Add (direkte Kalorieneingabe ohne Gramm-Angabe).

---

## Records (immutable)

- `DailyNutritionSummary(Date, TotalCalories, TotalProtein, TotalCarbs, TotalFat, EntryCount)`
- `TrackingStats(Type, CurrentValue, AverageValue, MinValue, MaxValue, TrendValue, TotalEntries)`
- `MacroDistribution(ProteinPercent, CarbsPercent, FatPercent)` — aus `MacroGoals.GetDistribution()`
- `MacroProgress(Name, Current, Goal, Remaining, PercentageComplete)` — enthält `IsOverGoal`
- `DailyMacroSummary(TotalProtein, TotalCarbs, TotalFat, TotalCalories, Goals)` — liefert ProteinProgress/CarbsProgress/FatProgress/CaloriesProgress

`TrendValue` in `TrackingStats` = Differenz zum Vortag (positiv = Zunahme, negativ = Abnahme).

---

## `FitnessAchievement.Progress` / `DailyChallenge.Progress`

```csharp
public double Progress => TargetValue > 0 ? Math.Min((double)CurrentValue / TargetValue, 1.0) : 0;
```

Immer auf `[0.0, 1.0]` begrenzt — Renderer können direkt `Progress` als Arc-Winkel verwenden.
Beide Klassen (`FitnessAchievement` und `DailyChallenge`) implementieren identisch.

---

## `VersionedData<T>` — Migrations-Infrastruktur

`DataSchemaVersions` hält Versionskonstanten (aktuell alle v1: FoodLog, Favorites, Tracking,
BarcodeCache). Bei brechendem Schema-Wechsel: Konstante erhöhen + `IDataMigration<T>`
implementieren. `VersionedData.LastModified` wird beim Anlegen auf `DateTime.UtcNow` gesetzt.
