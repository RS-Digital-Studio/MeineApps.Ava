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

## Bedarfs-Rendering (`NeedsContinuousRender`)

Statische Szenen überschreiben `NeedsContinuousRender => false` und sparen so den Paint, solange
keine sichtbare Änderung anliegt (Akku). Sie MÜSSEN bei jeder Zustandsänderung `RequestRedraw()`
rufen. Mechanik → [Engine/CLAUDE.md](../Engine/CLAUDE.md).

| Szene | Wert | Grund |
|-------|------|-------|
| `TitleScene` | `true` | Ambient-Partikel (`EmitContinuous`), Fade-In, pulsierender Titel-Glow (`Sin(_time)`). |
| `AssetDownloadScene` | `true` | Kontinuierlich aufsteigende Ambient-Partikel, Fortschrittsbalken. |
| `SaveSlotScene` | `true` | Animierter Hintergrund (`RenderBack/Front(_time)` mit ScanLines). |
| `ClassSelectScene` | `true` | Pulsierender Kartenrand (`Sin(_time)`), Partikel, `systemVoid`-Hintergrund, `DrawFullBody(_time)`. |
| `DialogueScene` | `true` | Typewriter, Glitch (ARIA), Kamera-Zoom/Shake, animierter Hintergrund, Sprite-Breathing/Blinzeln. |
| `BattleScene` | `true` | Floating-Damage-Numbers, Flash/Shake, Phasen-Animationen, Sprite-Effekte. |
| `OverworldScene` | `true` | Ambient-Partikel-Emission, Zoom-Interpolation, animierte Node-Map (`_animTime`). |
| `InventoryScene` | **`false`** | Reine Grid-/Detail-Liste; kein `_time`, keine Partikel/Pulse. Redraw bei Tab/Selektion/Equip/Use/Gold. |
| `CodexScene` | **`false`** | Listen-/Detail-Ansicht; `_time` wird im Render nicht genutzt. Redraw bei Kategorie/Eintrag/Scroll/Hover. |
| `StatusScene` | **`false`** | Status-/Skill-/Equipment-Tabs; kein `_time`. Redraw bei Tab-Wechsel + Stat-Änderung (`UpdateCachedStrings`). |
| `ShopScene` | **`false`** | Kauf-/Verkauf-Liste; kein `_time`. Redraw bei Tab/Selektion/Kauf/Verkauf/Gold (`RefreshDisplay`). |
| `SettingsScene` | **`false`** | Formular; `Update` leer. Redraw bei Toggle/Speed/Slider-Drag (Drag braucht Redraw pro Move). |

**Regel:** Im Zweifel `true` lassen. Eine `false`-Szene, die eine sichtbare Änderung ohne
`RequestRedraw()` macht, friert ein. Overlays öffnen nur continuous Szenen — die `false`-Szenen
liegen nie unter einem Overlay.

## Performance-Gotchas

| Szene | Pattern |
|-------|---------|
| `BattleScene` | `_cachedEnemySpriteKey` verhindert Asset-Reload pro Frame; `BackgroundCompositor.SetScene()` nur in `OnEnter()`. |
| `SaveSlotScene` | `_isLoading`-Guard verhindert async Race Condition bei Doppel-Tap. |
| `OverworldScene` | Vorgänger-Index (Dictionary) wird einmalig beim Map-Load aufgebaut. |
