# Scenes — Konkrete Spielszenen

12 Szenen erben von `RebornSaga.Engine.Scene`. Jede Szene ist ein vollständiger
Spielzustand mit eigenem Update/Render/Input-Lifecycle.
Engine-Grundlagen → [Engine/CLAUDE.md](../Engine/CLAUDE.md).

## Dateien

| Datei | Szene | Beschreibung |
|-------|-------|-------------|
| `AssetDownloadScene.cs` | `AssetDownloadScene` | Erster Start: Firebase-Asset-Download mit Fortschrittsbalken, Partikel, Retry. Wechselt automatisch zu `TitleScene`. |
| `TitleScene.cs` | `TitleScene` | Animierter Titelbildschirm, "Neues Spiel" vs. "Fortsetzen" (SaveGame-Erkennung). |
| `SaveSlotScene.cs` | `SaveSlotScene` | 3 Speicherplätze, Long-Press zum Löschen, `_isLoading`-Guard gegen async Race Condition. |
| `ClassSelectScene.cs` | `ClassSelectScene` | Auswahl aus 3 Klassen (Schwertmeister / Arkanist / Schattenklinke). |
| `DialogueScene.cs` | `DialogueScene` | Hintergrund + Portrait + Typewriter + Choices + MangaPanel + GlitchEffect (ARIA) + Kamera-Zoom/Shake. |
| `BattleScene.cs` | `BattleScene` | Aktionsbasierter Kampf + Element-System + geführtes 5-Phasen-Tutorial (Prolog P1). |
| `OverworldScene.cs` | `OverworldScene` | Node-Map (Slay the Spire-inspiriert) mit Kamera-Pan, AI-Regions-Hintergründe. Vorgänger-Index beim Map-Load aufgebaut (`MarkPredecessorsCompleted` O(V+E)). |
| `InventoryScene.cs` | `InventoryScene` | Grid-Ansicht, 6 Kategorien, AI-Item-Icons mit Qualitäts-Glow. |
| `ShopScene.cs` | `ShopScene` | Kaufen/Verkaufen, Gold-Counter. |
| `StatusScene.cs` | `StatusScene` | 3 Tabs (Status / Skills / Equipment). |
| `CodexScene.cs` | `CodexScene` | Bestiary, Lore und Charakter-Profile nach Kategorien sortiert. |
| `SettingsScene.cs` | `SettingsScene` | Audio + Text-Geschwindigkeit, persistiert via `IPreferencesService`. |

## BattleScene — Tutorial (Prolog P1)

- Aktivierung: `enemy.Id == "B001" && TutorialService.ShouldShow("FirstBattle")`
- 5 Phasen (`_tutorialStep` 0–5): Intro → Angriff → Skill → Item → Ausweichen → Frei
- `IsTutorialActionEnabled()` erlaubt pro Phase nur die zu lernende Aktion
- Schaden-Override: Vor Phase 3 max 10% HP, Phase 4 erzwingt erfolgreichen Dodge
- Abschluss: `TutorialService.MarkSeen("FirstBattle")`

**BattlePhase-Enum:**
```
Intro → PlayerTurn → SkillSelect | ItemSelect
      → PlayerAttack | PlayerSkillAttack | PlayerDodge
      → EnemyTurn → EnemyAttack
      → Victory | Defeat | BossPhaseChange | Done
```
`BossPhaseChange` — Boss wechselt Phase (Mini-Cutscene, volle HP).

## Performance-Gotchas

| Szene | Pattern |
|-------|---------|
| `BattleScene` | `_cachedEnemySpriteKey` verhindert Asset-Reload pro Frame; `BackgroundCompositor.SetScene()` nur in `OnEnter()`. |
| `SaveSlotScene` | `_isLoading`-Guard verhindert async Race Condition bei Doppel-Tap. |
| `OverworldScene` | Vorgänger-Index (Dictionary) wird einmalig beim Map-Load aufgebaut. |
