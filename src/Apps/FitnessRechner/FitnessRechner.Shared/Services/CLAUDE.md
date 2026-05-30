# Services — Domain-Logik & Plattform-Interfaces

Alle Services sind **Singleton** (in `App.axaml.cs` registriert). Services mit Events
implementieren `IDisposable` und melden sich in `Dispose()` ab.
Generische Service-Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

---

## Dateien

| Datei | Interface | Zweck |
|-------|-----------|-------|
| `TrackingService.cs` | `ITrackingService` | JSON-Persistenz `TrackingEntry` (tracking.json). Thread-Safe (2x `SemaphoreSlim`: Load + Write). 1-min Backup-Intervall. `EntryAdded`-Event für Streak-Trigger |
| `FoodSearchService.cs` | `IFoodSearchService` | Fuzzy Matching auf 114 lokalen Foods + Open Food Facts. Favorites + Recipes. `FoodLogAdded`-Event. Batch-Methoden `GetFoodLogsInRangeAsync` + `GetDailySummariesInRangeAsync` (N+1-Query-Fix). Lowercase-Cache im static-Ctor |
| `FoodDatabase.cs` | — | 114 Nahrungsmittel mit lokalisierten Namen + Aliase (statische Liste, kein File-I/O) |
| `BarcodeLookupService.cs` | `IBarcodeLookupService` | Open Food Facts API. `_barcodeCache` Dictionary mit `SemaphoreSlim` |
| `ScanLimitService.cs` | `IScanLimitService` | Tages-Limit (3 Scans/Tag), Bonus-Scans via Rewarded Ad (+5). Preferences-basiert |
| `DesktopBarcodeService.cs` | `IBarcodeService` | Desktop-Fallback: gibt `null` zurück → View zeigt manuelle Eingabe |
| `FastingService.cs` | `IFastingService` | Intervallfasten (16:8, 18:6, 20:4, Custom), Start/Stop, History (letzte 30 Perioden). JSON-Persistenz |
| `ActivityService.cs` | `IActivityService` | Sport-Tracking mit MET-Werten. JSON-Persistenz `activity_log.json`. Thread-Safe (SemaphoreSlim). `ActivityAdded`-Event |
| `ActivityDatabase.cs` | — | 30 Aktivitäten in 4 Kategorien (Cardio/Kraft/Sport/Alltag) mit MET-Werten |
| `StreakService.cs` | `IStreakService` | Logging-Streak (aufeinanderfolgende Tage). Meilenstein-Confetti bei 3/7/14/21/30/50/75/100/150/200/365 Tagen. Preferences-basiert |
| `AchievementService.cs` | `IAchievementService` | 20 Achievements in 5 Kategorien. Preferences-basiert (freigeschaltete IDs + Fortschritt als JSON). `AchievementUnlocked`-Event (ID + XP) |
| `LevelService.cs` | `ILevelService` | XP-System (Max Level 50). `XpForLevel(n) = 100 × n × (n+1) / 2`. `LevelUp`-Event. Preferences-basiert |
| `ChallengeService.cs` | `IChallengeService` | 10 tägliche Challenges (rotierend nach `DayOfYear`). `ChallengeCompleted`-Event (+20-40 XP) |
| `IReminderService.cs` / `ReminderService.cs` | `IReminderService` | Desktop-No-Op. 3 Erinnerungstypen. Basis-Klasse für `AndroidReminderService` |

---

## MET-Formel (`ActivityDatabase`)

```
kcal = MET × Gewicht_kg × Dauer_h
```

Kategorien: Cardio (Laufen 8.0, Radfahren 6.0, Schwimmen 7.0, ...), Kraft (Gewichtheben 5.0,
Sit-Ups 3.0, ...), Sport (Fußball 7.0, Tennis 6.0, ...), Alltag (Spazierengehen 3.5, ...).

---

## `FoodSearchService` — Architektur-Details

**Lowercase-Cache:** Im `static`-Konstruktor einmalig berechnet — verhindert
`ToLowerInvariant()` bei jeder Suche über alle 114 Foods.

**Batch-Methoden:** `GetFoodLogsInRangeAsync(start, end)` und `GetDailySummariesInRangeAsync`
laden einmal alle Logs in den gewünschten Zeitraum statt N einzelne Queries (N+1-Query-Fix für
Progress-Charts über längere Zeiträume).

**Fuzzy-Match-Score:** Min-Score `0.3` — darunter werden Ergebnisse unterdrückt. Trifft auf
exakten Match, Alias-Match und Prefix-Match priorisiert.

**File-Struktur:**
- `food_log.json` — aktuelle Logs (laufendes Jahr)
- `food_log_archive.json` — archivierte Logs (ältere Einträge)
- `food_favorites.json` — Favoriten
- `recipes.json` — Rezepte

---

## Gamification — Ereignis-Ketten

```
TrackingService.EntryAdded
  → StreakService.UpdateStreak
      → StreakMilestoneReached (Confetti via MainViewModel.CelebrationRequested)

AchievementService.AchievementUnlocked(id, xp)
  → LevelService.AddXp(xp)
      → LevelUp (FloatingText "+Level Up!")

ChallengeService.ChallengeCompleted
  → LevelService.AddXp(xpReward)
```

---

## XP-Vergabe (Referenz)

| Aktion | XP |
|--------|-----|
| Gewicht-Eintrag | +10 |
| Mahlzeit-Eintrag | +5 |
| Wasser-Eintrag | +3 |
| Rechner-Nutzung | +2 |
| Achievement | +25–500 |
| Challenge | +20–40 |

---

## Gotchas

- **`TrackingService` doppelter Lock:** `_loadLock` verhindert parallele Initialisierungen
  (`EnsureLoadedAsync`), `_writeLock` schützt den Write-Pfad. Beide sind nötig — ohne
  separaten Load-Lock können parallele `AddEntryAsync`-Calls die Init doppelt auslösen.
- **`ScanLimitService` Tages-Reset:** Prüft `DateTime.Today` gegen gespeichertes Datum.
  IMMER `DateTime.Today` (lokale Zeit) statt `DateTime.UtcNow.Date` — Scans sind tagesbasiert
  aus Nutzer-Perspektive, nicht UTC-basiert.
- **`AchievementService` Fortschritt JSON:** Fortschritts-Dictionary wird als JSON-String in
  Preferences gespeichert (Key `achievements_progress`). Bei Deserialisierungs-Fehler wird
  ein leeres Dictionary verwendet — kein Crash, aber Fortschritt geht verloren.
- **`ChallengeService` DayOfYear-Rotation:** Challenges rotieren nach `DateTime.Now.DayOfYear % 10`.
  An Schaltjahren (366 Tage) bleibt die Rotation korrekt — keine Sonder-Behandlung nötig.
