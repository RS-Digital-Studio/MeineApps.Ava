# Data — Embedded JSON-Spielinhalte

Alle Spielinhalte als Embedded Resources in `RebornSaga.Shared`. Kein Code — nur Daten.
Die Daten werden von den zugehörigen Services geladen.

## Verzeichnisstruktur

```
Data/
├── Chapters/          → Kapitel-Strukturen (Nodes, Verbindungen, Bedingungen)
├── Dialogue/{lang}/   → Lokalisierte Dialog-Texte (DE, EN, ES, FR, IT, PT)
├── Maps/              → Overworld-Karten (Knoten + Pfade + Positionen)
├── Skills/            → Skill-Definitionen pro Klasse
├── Items/             → Item-Datenbank
└── Enemies/           → Gegner-Datenbank
```

## Dateien

| Pfad | Inhalt | Geladen von |
|------|--------|-------------|
| `Chapters/chapter_{id}.json` | 13 Kapitel (P1–P3, K1–K10): Nodes, Verbindungen, StoryEffects | `StoryEngine` |
| `Dialogue/{lang}/chapter_{id}.json` | Lokalisierte Texte pro Kapitel + Sprache | `StoryEngine.LoadDialogueTextsAsync()` |
| `Maps/overworld_{id}.json` | 13 Overworld-Karten (Knoten-Positionen, Verbindungen) | `OverworldScene` |
| `Skills/swordmaster.json` | 15 Skills für Schwertmeister (5 Stufen je) | `SkillService.LoadSkills()` |
| `Skills/arcanist.json` | 15 Skills für Arkanist | `SkillService.LoadSkills()` |
| `Skills/shadowblade.json` | 15 Skills für Schattenklinke | `SkillService.LoadSkills()` |
| `Items/items.json` | Waffen, Rüstungen, Consumables, Key-Items | `InventoryService.LoadItems()` |
| `Enemies/enemies.json` | Alle Gegner + Bosse (Stats, Element, Drops) | `EnemyLoader` (lazy, bei Bedarf) |

## Kapitel-Übersicht

| ID | Kapitel | Status | Kosten |
|----|---------|--------|--------|
| P1–P3 | Prolog | Gratis | — |
| K1–K5 | Arc 1 (frei) | Gratis | — |
| K6 | Arc 1 | Kostenpflichtig | 500 Gold |
| K7 | Arc 1 | Kostenpflichtig | 800 Gold |
| K8 | Arc 1 | Kostenpflichtig | 1.200 Gold |
| K9 | Arc 1 | Kostenpflichtig | 1.800 Gold |
| K10 | Arc 1 | Kostenpflichtig | 2.700 Gold |

**Hinweis:** `chapter_p1.json` und `chapter_p2.json` fehlen in `Chapters/` — P1 und P2
Struktur ist direkt im Code der `DialogueScene` / `BattleScene` eingebettet (Prolog-Tutorial).
`chapter_p3.json` ist vorhanden.

## Dialogue-Fallback

`StoryEngine.LoadDialogueTextsAsync()` versucht die Sprache des Spielers, fällt auf Englisch
zurück, dann auf Deutsch. Wenn kein Dialog gefunden: leerer String (kein Crash).

## Skill-/Item-Lade-Reihenfolge

`SkillService.LoadSkills()` + `InventoryService.LoadItems()` werden via `Task.WhenAll` parallel
geladen (in `App.InitializeServicesAsync()`). Beide müssen abgeschlossen sein bevor
`SaveGameService.LoadGameAsync()` aufgerufen wird — der gespeicherte Spieler enthält
Referenzen auf Item-IDs und Skill-Stufen.
