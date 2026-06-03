# Services — RPG-Logik & Infrastruktur

18 Services, alle als **Singleton** in `App.axaml.cs` registriert.
Domänenlogik (Berechnungen, Persistenz, Geschäftsregeln) lebt hier — nicht in ViewModels oder Szenen.
Generische Conventions → [Haupt-CLAUDE.md](../../../../../CLAUDE.md).

## Dateien

| Datei | Service | Zweck |
|-------|---------|-------|
| `StoryEngine.cs` | `StoryEngine` | Kapitel-Navigation, Condition-Parser, StoryEffects (EXP/Gold/Karma/Flags). **`SetPlayer()` nach jedem Load/Neues-Spiel pflicht!** |
| `BattleEngine.cs` | `BattleEngine` | Schadensberechnung, Element-System (6 Elemente, 1,5×/0,5× Modifikatoren), Crits. |
| `SkillService.cs` | `SkillService` | Skills in 5 Tiers (1–5), Evolution via Mastery-Zähler (`UseSkill()`). `LoadSkills()` muss in `InitializeServicesAsync` aufgerufen werden. |
| `InventoryService.cs` | `InventoryService` | Items verwalten, Ausrüsten, Stack-Verwaltung. `LoadItems()` muss in `InitializeServicesAsync` aufgerufen werden. |
| `AffinityService.cs` | `AffinityService` | Bond-Punkte (0–100) → Bond-Level (1–5) für 5 NPCs (Aria, Luna, Kael, Aldric, Vex). `BondLevelUp`-Event bei Stufenaufstieg. |
| `FateTrackingService.cs` | `FateTrackingService` | Karma (−100 bis +100), Entscheidungs-Log, FateFlags. Karma-Änderungen via `RecordDecision()` (loggt + clamp). |
| `CodexService.cs` | `CodexService` | Enzyklopädie (Charaktere, Orte, Lore), nach Kategorien sortiert. |
| `ProgressionService.cs` | `ProgressionService` | EXP via `AwardExp()`, Gold via `AwardGold()`, `LevelUp`-Event (Player, Anzahl Level-Ups). Skill-Evolution via `RecordSkillUse()`. |
| `SaveGameService.cs` | `SaveGameService` | SQLite, 3 Slots, Auto-Save bei Knoten-Wechsel, `SemaphoreSlim(1,1)` gegen parallele Saves, `IDisposable` (SQLite `CloseAsync`). |
| `GoldService.cs` | `GoldService` | Gold addieren/abziehen (Clamp auf `[0, MaxGold=9_999_999]`), Rewarded-Video-Cooldown (3×/Tag, 500 G). |
| `ChapterUnlockService.cs` | `ChapterUnlockService` | K6–K10 per Gold freischalten, `SemaphoreSlim(1,1)` gegen Doppel-Unlock. |
| `TutorialService.cs` | `TutorialService` | Erstbesucher-Hints per `IPreferencesService`. `ShouldShow(key)` + `MarkSeen(key)`. |
| `DailyService.cs` | `DailyService` | Login-Bonus (Gold), Prophezeiung (RESX-Keys Prophecy_0–13), Login-Streak. |
| `EnemyLoader.cs` | `EnemyLoader` | **Statische Klasse** (kein DI-Singleton). Lazy-Load aus `enemies.json` beim ersten Zugriff, `GetById()` gibt geklonten Enemy zurück (defensive copy). |
| `SpriteCache.cs` | `SpriteCache` | LRU-Cache (max 30 Bilder, max 80 MB), thread-safe (`lock`), `IDisposable`. `PeekPixels()` in `ComputeContentBounds()` vermeidet JNI-Overhead. Wird von `CharacterRenderer` + `BackgroundCompositor` genutzt. |
| `AssetDeliveryService.cs` | `AssetDeliveryService` | Firebase Storage REST API, SHA256-Hash-Verifikation, Delta-Updates. Stream-basierter Download, Retry (3× exponentieller Backoff), temporäre Dateien. |
| `IAssetDeliveryService.cs` | Interface | Trennt `AssetDownloadScene` von der konkreten Implementierung. |
| `AudioService.cs` | `AudioService` | Desktop-Stub (kein Sound). Android-Override via `App.AudioServiceFactory` → `AndroidAudioService`. |

## Kritische Patterns

### StoryEngine — Condition-Parser

```
karma > 50          affinity:aria >= 10      class == 1
has_item:M001        !has_item:M001
has_flag:betrayed    !has_flag:betrayed
alliance_aria        (Fallback: Flags.Contains)
```

`AdvanceToNode()` überspringt Knoten iterativ (Limit 100) wenn Condition nicht erfüllt.

### StoryEngine — `SetPlayer()` Pflicht

`StoryEngine.SetPlayer(player)` MUSS nach `SaveGameService.LoadGameAsync()` und nach
"Neues Spiel" aufgerufen werden. Ohne diesen Aufruf liefert die Engine falschen State.

### Thread-Safety

| Service | Mechanismus |
|---------|-------------|
| `SaveGameService` | `SemaphoreSlim(1,1)` + `IDisposable` (SQLite CloseAsync) |
| `ChapterUnlockService` | `SemaphoreSlim(1,1)` gegen Doppel-Unlock |
| `SpriteCache` | `lock` auf internem Dictionary |
| `GoldService` | `Math.Min/Max` + `MaxGold=9_999_999` in Add/Remove |

### Asset-Delivery

`AssetManifest` beschreibt alle Packs (characters, backgrounds, enemies, items, scenes).
Firebase-Bucket: `gs://rebornsaga-671b6.firebasestorage.app/assets/` (317 Dateien, 69,2 MB).
Upload via `F:\AI\ComfyUI_workflows\upload_assets.py` (Uniform Bucket Access, kein `make_public()`).
