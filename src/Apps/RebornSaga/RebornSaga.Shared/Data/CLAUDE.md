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
| `Chapters/chapter_{id}.json` | 11 Kapitel-Strukturen (chapter_p3, chapter_k1–chapter_k10): Nodes, Verbindungen, StoryEffects | `StoryEngine` |
| `Dialogue/{lang}/chapter_{id}.json` | Lokalisierte Texte pro Kapitel + Sprache (14 Dateien je Sprache: p1–p3, k1–k10) | `StoryEngine.LoadDialogueTextsAsync()` (privat) |
| `Maps/overworld_{id}.json` | 13 Overworld-Karten (overworld_p1–p3, overworld_k01–k10): Knoten-Positionen, Verbindungen | `OverworldScene` |
| `Skills/skills_swordmaster.json` | 15 Skills für Schwertmeister (5 Stufen je) | `SkillService.LoadSkills()` |
| `Skills/skills_arcanist.json` | 15 Skills für Arkanist | `SkillService.LoadSkills()` |
| `Skills/skills_shadowblade.json` | 15 Skills für Schattenklinke | `SkillService.LoadSkills()` |
| `Items/items.json` | Waffen, Rüstungen, Consumables, Key-Items | `InventoryService.LoadItems()` |
| `Enemies/enemies.json` | Alle Gegner + Bosse (Stats, Element, Drops) | `EnemyLoader` (lazy, bei Bedarf) |

## Hinweise zur Struktur

**Prolog-Kapitel in `Chapters/`:** `chapter_p1.json` und `chapter_p2.json` fehlen — P1- und
P2-Struktur ist direkt im Code von `DialogueScene` / `BattleScene` eingebettet (Prolog-Tutorial).
`chapter_p3.json` ist als Datei vorhanden.

**Dialogue-Fallback:** `StoryEngine.LoadDialogueTextsAsync()` versucht zunächst die
Systemsprache des Spielers (TwoLetterISOLanguageName), fällt bei fehlender Ressource direkt
auf Deutsch zurück. Kein Englisch-Zwischenschritt. Wenn auch Deutsch fehlt: leerer String (kein Crash).

**Map-Dateinamen:** Maps für K-Kapitel verwenden zweistellige Indizes: `overworld_k01.json`
bis `overworld_k10.json`. Prolog-Maps (`overworld_p1.json`–`overworld_p3.json`) sind ebenfalls
vorhanden, auch wenn `chapter_p1.json`/`chapter_p2.json` im Code eingebettet sind.

## Lade-Abhängigkeiten

`SkillService.LoadSkills()` und `InventoryService.LoadItems()` müssen abgeschlossen sein,
bevor `SaveGameService.LoadGameAsync()` aufgerufen wird — Speicherstände referenzieren Item-IDs
und Skill-Stufen. Details zur Lade-Reihenfolge → [RebornSaga.Shared/CLAUDE.md](../CLAUDE.md)
(`InitializeServicesAsync`).

Kapitel-Kosten und Freischalt-Logik → [RebornSaga/CLAUDE.md](../../CLAUDE.md) (Gold-Economy).
