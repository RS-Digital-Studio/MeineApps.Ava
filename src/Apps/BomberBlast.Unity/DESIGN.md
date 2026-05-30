# BomberBlast 3D — Game-Design-Dokument

> Vollständige Game-Design-Spezifikation des **treuen 3D-Remakes**. Komplementär zu
> [PLAN.md](PLAN.md) (Übersicht) und [ARCHITECTURE.md](ARCHITECTURE.md) (Tech). Stand 2026-05-30.
>
> **Leitprinzip:** Dies beschreibt **dasselbe Spiel wie das produktive BomberBlast** — alle Inhalte,
> Zahlen und Mechaniken sind 1:1 aus dem Code übernommen (`F:/Meine_Apps_Ava/src/Apps/BomberBlast/
> BomberBlast.Shared/`, verifiziert 2026-05-30). Was in 3D **anders/besser** wird, ist explizit als
> **[3D]** bzw. **[NEU]** markiert. Wo eine Mechanik im Original noch "Foundation" ist, steht **[Integration]**.

---

## Inhaltsverzeichnis

1. [Setting & Welt-Stil](#1-setting--welt-stil)
2. [Welt-Story-Beats (übernommen)](#2-welt-story-beats-übernommen)
3. [Die 10 Welten](#3-die-10-welten)
4. [Spielfeld & Grid](#4-spielfeld--grid)
5. [Helden (5)](#5-helden-5)
6. [Gegner (12 + Elite)](#6-gegner-12--elite)
7. [Bosse (5 + 8 Modifier)](#7-bosse-5--8-modifier)
8. [Bomben & Karten (14 Typen / 13 Karten)](#8-bomben--karten-14-typen--13-karten)
9. [PowerUps (12 + Cure)](#9-powerups-12--cure)
10. [Combo-System](#10-combo-system)
11. [Spielmodi (8)](#11-spielmodi-8)
12. [Roguelike-Dungeon](#12-roguelike-dungeon)
13. [Liga & Ranking](#13-liga--ranking)
14. [Battle Pass & Saisons](#14-battle-pass--saisons)
15. [Achievements (72)](#15-achievements-72)
16. [Wirtschaft & Monetization](#16-wirtschaft--monetization)
17. [Cosmetics & Player-Identity](#17-cosmetics--player-identity)
18. [Daily / Weekly / Live-Events](#18-daily--weekly--live-events)
19. [Onboarding & Tutorial](#19-onboarding--tutorial)
20. [Accessibility](#20-accessibility)
21. [Audio-Design](#21-audio-design)
22. [UI/UX-Konzept](#22-uiux-konzept)
23. [3D-Visuals & Game Juice (der "besser"-Teil)](#23-3d-visuals--game-juice-der-besser-teil)
24. [Multiplayer (optionales Plus)](#24-multiplayer-optionales-plus)

---

## 1. Setting & Welt-Stil

BomberBlast hat **kein narratives Sci-Fi-Konzern-Setting** — es ist ein klassisches Bomberman-Action-
Spiel mit **10 thematischen Welten** und einem leichtgewichtigen Story-Rahmen (Welt-Intros/Outros mit
Cliffhanger). Dieser Charakter bleibt im Remake erhalten.

### 1.1 Visueller Stil (Neon-Arcade, jetzt in 3D)

Das Original bietet **zwei Visual-Styles** (umschaltbar via `IGameStyleService`):
- **Classic HD** — sauberer, lesbarer Arcade-Look
- **Neon / Cyberpunk** — Glow, Neon-Akzente, dunkle Tiles

**[3D]** Beide Styles werden als 3D-Material-Sets neu umgesetzt (URP): Classic = mattere PBR-Materialien,
Neon = emissive Materialien + Bloom. Die Style-Umschaltung bleibt erhalten.

### 1.2 Brand-Style-Guide (unverändert)

| Element | Wert (Original `AppPalette.axaml`) |
|---------|-----------------------------------|
| **Primärfarbe** | Neon-Orange **#FF6B35** |
| **Akzent 1** | Cyan **#22D3EE** |
| **Akzent 2** | Gold-Trail **#FFDD33** |
| **Design-Sprache** | Oktagonale Formen, scharfe Kanten, Arcade-Glow |
| **HUD** | Side-Panel rechts (Landscape), Time/Score/Combo/Lives/Deck |
| **Anti-Style** | Realismus, Photo-Texturen, düstere Cyberpunk-Tristesse |

### 1.3 Welt-Themes (10, mit eigener Farbpalette `WorldPalette`)

Jede Welt hat ein eigenes Tile-Set, Farbpalette, Ambient-Partikel (`WeatherSystem`/
`AmbientParticleSystem`) und welt-thematische Bomben-FX (`BombFxTheme`). **[3D]** wird jede Welt als
3D-Umgebung mit eigener Beleuchtung, Skybox und Material-Set neu gebaut. Die thematische Identität der
10 Welten (siehe §3) bleibt.

---

## 2. Welt-Story-Beats (übernommen)

> Das Original hat eine Story-Schicht via `IWorldStoryService`: **10 Welt-Intros + 9 Welt-Outros**
> (Cliffhanger; Welt 10 ist das Ende). One-shot pro Lebenszeit (Pref-Flags `HasSeenIntro/HasSeenOutro`),
> voll lokalisiert in 6 Sprachen, mit `StingerKey` für Audio (`boss_reveal` ab Welt 2, `victory` für Outros).

**[3D]** Die bestehenden Story-Beats werden als **3D-Cutscenes** (Timeline + Cinematic-Kamera) inszeniert
statt als 2D-Text-Overlays. Inhalt/Reihenfolge bleiben identisch — kein neuer Story-Arc, keine neuen
Figuren. Die Cliffhanger-Struktur (Outro deutet auf nächste Welt) wird beibehalten.

**Migration der Texte:** Die bestehenden RESX-Story-Strings werden 1:1 in die Unity-Localization-Tables
übernommen (DE/EN/ES/FR/IT/PT).

---

## 3. Die 10 Welten

### 3.1 Struktur

- **10 Welten × 10 Level = 100 Story-Level.** Jedes 10. Level ist ein Boss-Level (L10, L20, …, L100).
- **Boss-Rotation:** Boss-Typ rotiert alle 2 Welten (5 Bosse → 10 Welten). Welt 9 (L90) und Welt 10
  (L100) sind **Duo-Boss-Encounter** (siehe §7).
- **Layout-Pool pro Welt:** 8 von 12 Layouts; Welt 1 einsteigerfreundlich (einfache Layouts), Welt 5+
  voller Pool (`LevelLayoutGenerator`).
- **Mutatoren ab Welt 6** auf den Leveln x3/x6/x9 jeder Welt (siehe §11.x / §4).
- **Master-Mode (Reborn)** nach L100-Abschluss (siehe §11).

### 3.2 Welt-Themes

Jede Welt hat ein eigenes visuelles Thema (Farbpalette + Tile-Set + Ambient-Partikel + Welt-Bomben-FX).
Die thematische Reihenfolge des Originals wird übernommen; **[3D]** jede Welt wird als eigene 3D-Umgebung
gebaut. (Die konkreten Welt-Namen/Themes werden aus `WorldPalette`/`ProceduralTextures` + RESX 1:1
übernommen — die 12 prozeduralen Textur-Funktionen liefern 10 welt-spezifische Tiles.)

### 3.3 Layout-Typen (12, aus `Level.cs`)

| # | Layout | Charakter |
|---|--------|-----------|
| 1 | **Classic** | Klassisches Bomberman-Raster |
| 2 | **Cross** | Kreuz-förmige Hauptachsen |
| 3 | **Arena** | Offene Arena, wenig Wände |
| 4 | **Maze** | Labyrinthartig, enge Pfade |
| 5 | **TwoRooms** | Zwei Räume mit Verbinder |
| 6 | **Spiral** | Spiralförmige Wandführung |
| 7 | **Diagonal** | Diagonale Wand-Pattern |
| 8 | **BossArena** | Offene Boss-Arena (Boss-Level) |
| 9 | **Labyrinth** | Dichtes Labyrinth |
| 10 | **Symmetry** | Spiegelsymmetrisch |
| 11 | **Islands** | Insel-Cluster mit Lücken |
| 12 | **Chaos** | Zufallsverteilte Wände |

### 3.4 Mutatoren (5, ab Welt 6, Level x3/x6/x9)

`AllPowerBombs`, `DoubleSpeed`, `InvisibleBlocks`, `NoTimer`, `MirrorControls`. Mutator-Level schenken
**3 garantierte Sterne** (Schwierigkeit = Belohnung, nicht Strafe). `GetMutatorDisplayName` für UI.

---

## 4. Spielfeld & Grid

- **15×10-Grid** (`GameGrid.cs`), Landscape-only.
- **`CellType`:** Empty, Indestructible, Destructible, Exit.
- **Destructible Blocks** droppen PowerUps/Karten (Drop-Chance hero-/upgrade-moduliert).
- **Pre-Turn-Buffering** (`Player.cs`): Richtung wird gepuffert, Turn bei 40 % Zellzentrum-Nähe.
- **[3D]** Das Grid wird als 3D-Bodenfläche mit erhöhten Block-Meshes gerendert; Top-Down-Kamera
  (leicht geneigt für Tiefe), dynamische Schatten der Blöcke, 3D-Explosions-Volumen.

---

## 5. Helden (5)

> 5 spielbare Charaktere (`HeroDefinition.cs`), freischaltbar via Achievement oder Gem-Kauf. Aktiver
> Hero in Preferences persistiert. **[3D]** Jeder Hero bekommt ein 3D-Charakter-Modell (im Neon-Arcade-
> Stil), behält aber exakt seine Stats/Traits. **[Integration]** Engine-Anwendung der Hero-Stats beim
> Spawn (`Player.ApplyHero`) ist im Original deferred — im Remake fester Bestandteil.

| Hero | MaxBombs | FireRange | SpeedLevel | Lives | Multiplikator / Trait | Unlock |
|------|----------|-----------|------------|-------|------------------------|--------|
| **Default** | 1 | 2 | 0 | 3 | — | von Anfang an |
| **SpeedySam** | 1 | 1 | 1 (Speed-Start) | 3 | Coin-Pickup ×1.05, **QuickPocket** (kein Speed-Penalty bei Curse) | `ach_speed_demon` |
| **BrickBoris** | 2 | 3 | 0 (langsam) | 2 (−1 Heart) | Block-Drop +10 %, **DemolitionExpert** | `ach_block_destroyer` |
| **TwinTina** | 2 | 1 | 0 | 3 | **DoubleDetonation** (Bomben zünden zweimal nacheinander) | `gems_500` (Direct-Buy) |
| **LuckyLola** | 1 | 2 | 0 | 3 | PowerUp-Drop ×1.20, **LuckyDrops** | `ach_jackpot` |

- **SpeedLevel 0-3:** `BASE_SPEED(80px/s) + Level × 20`.
- **HeroTrait-Enum:** None, DoubleDetonation, LuckyDrops, DemolitionExpert, QuickPocket.
- **Hero-Skins** als Cosmetics (siehe §17), Body-/Accent-Farbe pro Hero.

---

## 6. Gegner (12 + Elite)

> `EnemyType.cs` — 8 klassische Bomberman-Gegner + 4 erweiterte. **[3D]** 3D-Gegner-Modelle, behalten
> Verhalten/Stats. Pathfinding (A*, BFS) wird 1:1 portiert (Pure-Domain-Code).

| Gegner | Verhalten | Pathfinding | Besonderheit | ab Welt |
|--------|-----------|-------------|--------------|---------|
| **Ballom** | Langsam, dümmster Gegner | Random | Tutorial-Fodder | 1 |
| **Onil** | Normal, etwas zufällig | Random | — | 1+ |
| **Doll** | Normal, vorhersehbar | Low-Int | — | 2+ |
| **Minvo** | Schnell, normale Intelligenz | A* | gefährlich | 3+ |
| **Kondoria** | Sehr langsam, läuft durch Wände | Wallpass | hinterhältig | 4+ |
| **Ovapi** | Langsam, durch Wände | Wallpass | geisterhaft | 5+ |
| **Pass** | Schnell, hohe Intelligenz | A* (Chase) | jagt aktiv | 6+ |
| **Pontan** | Sehr schnell, durch Wände | A* + Wallpass | gefährlichster Standard | 7+ |
| **Tanker** | Langsam | A* | überlebt 1 Explosion (2 Hits nötig) | 5+ |
| **Ghost** | Schnell | — | periodisch unsichtbar (3 s sichtbar / 2 s unsichtbar) | 7+ |
| **Splitter** | — | Random | spaltet sich bei Tod in 2 Mini-Splitter | 7+ |
| **Mimic** | Stationär → Angriff | — | tarnt sich als PowerUp | 8+ |

**Elite-Variante** (`Enemy.IsElite`): 1.2× Speed, 2× HP, 3× Punkte, lila pulsierender Glow. Modifiziert
bestehende Gegner-Typen.

**[3D]** AI-Spawn-Jitter, AStarBudgetPerFrame=5, Danger-Zone-Map (1×/Frame), Kettenreaktions-Erkennung
(iterativ, max 5) — alles aus dem Original übernommen.

---

## 7. Bosse (5 + 8 Modifier)

> `BossEnemy.cs` — 5 Boss-Typen, jedes 10. Level, Multi-Cell-BoundingBox, HP 3-8, Enrage bei 50 % HP.
> **[3D]** Große 3D-Boss-Modelle mit Telegraph-Animationen, dynamischer Beleuchtung, Anticipation-Scale.

### 7.1 Boss-Roster

| Boss | Welt-Slot (Rotation alle 2 Welten) | Banner-Name |
|------|-----------------------------------|-------------|
| **StoneGolem** | W1/… | `STONE GOLEM` |
| **IceDragon** | W2/… | `ICE DRAGON` |
| **FireDemon** | W3/… | `FIRE DEMON` |
| **ShadowMaster** | W4/… (+ Welt-9-Duo) | `SHADOW MASTER` |
| **FinalBoss** | W5/… (+ W9/W10-Duo) | `FINAL BOSS` |

- **Duo-Boss-Encounter:** Welt 9 (L90) = FinalBoss **+** ShadowMaster; Welt 10 (L100) = 2× FinalBoss.
  Banner mit `&` verbunden bzw. Plural.

### 7.2 Boss-Mechanik

- **Angriffs-Zyklus:** Telegraph (2 s) → Attack (1.5 s) → Cooldown (12-18 s, kürzer bei Enrage).
- **5 Angriffe:** BlockRegen, Eisatem (Reihe), Lava-Welle, Teleport, rotierend (FinalBoss).
- **Kollision:** `OccupiesCell()` (Multi-Cell). Shield absorbiert Angriffe. Kein A* — direkter Richtungs-Check.
- **Enrage** bei 50 % HP: halbiert Decision-Timer, Phase-2-Patterns (`CurrentPhase` 1→2).
- **Anticipation:** Letzte 120 ms vor Big-Attack zieht sich Boss-Sprite auf 0.85× (`AnticipationScale`).

### 7.3 Boss-Modifier (8, `BossModifier.cs`)

`Shielded`, `Fast`, `Healing`, `Summoner`, `Frenzy`, `Berserk`, `Reflective`, `Burning`.
- **RollForWorld(world, rng):** deterministisch, 30 % Chance ab Welt 5, 60 % ab Welt 10. (8 Modifier × 5 Bosse = 40 Variationen.)
- Beispiele: Healing = 2.5 HP/s mit 50 %-HP-Cap; Shielded = absorbiert 1 Hit alle 15 s.
- **[Integration]** Modifier-Effekte sind im Original teils Foundation (Enum + Spawn-Roll) — im Remake
  voll implementiert.

---

## 8. Bomben & Karten (14 Typen / 13 Karten)

> **14 Bomben-Typen** (BombType-Enum inkl. `Normal`). Davon **13 Karten-Definitionen** im
> `CardCatalog` (Standard-Bombe ist keine Sammel-Karte). 3 davon werden im Shop freigeschaltet, 10 sind
> Sammel-/Drop-Karten. **[3D]** 3D-Bomben-Modelle + VFX-Graph-Explosionen pro Typ.

### 8.1 Karten-Liste (Rarities exakt aus `CardCatalog.cs`)

| Karte | Effekt | Rarity | Quelle |
|-------|--------|--------|--------|
| **Standard** | 3×3-Cross-Explosion | — (Default, keine Karte) | immer |
| **Ice** | Frost 3 s, 50 % Slow | Common | Shop |
| **Fire** | Lava-Feld 3 s, kontinuierlicher Schaden | Common | Shop |
| **Sticky** | Klebt 1.5 s + Kettenreaktion | **Common** | Shop |
| **Smoke** | 3×3-Nebel, AI 4 s zufällig | **Rare** | Karte |
| **Lightning** | Trifft bis 3 Gegner per Chain | Rare | Karte |
| **Gravity** | Zieht Gegner zur Bombe | Rare | Karte |
| **Poison** | Gift-Wolke, DoT | **Rare** | Karte |
| **TimeWarp** | 50 % Slow für 5 s | **Epic** | Karte |
| **Mirror** | Doppelte Reichweite (beide Achsen) | Epic | Karte |
| **Vortex** | Spiral-Explosion | Epic | Karte |
| **Phantom** | Durchdringt 1 Wand, dann Explosion | **Epic** | Karte |
| **Nova** | 360°-Explosion (alle Zellen) + PowerUp-Drop | **Legendary** | Karte |
| **BlackHole** | Sog, dann massive Explosion | Legendary | Karte |

> Hinweis: Diese Rarities/Effekte sind die echten Werte aus dem Code (die frühere Plan-Version v0.2 hatte
> sie falsch — z.B. Smoke ist Rare/3×3-Nebel, nicht Common/5×5-Sichtblocker; Nova ist Legendary/360°, nicht Epic/8-Wege).

### 8.2 Deck, Drops, Crafting

- **Deck:** 4 Basis-Slots + 1 freischaltbar (20 Gems). `ActiveCardSlot` per HUD-Tap wechselbar.
- **Drop-Gewichtung:** 60 % Common, 25 % Rare, 12 % Epic, 3 % Legendary.
- **Card-Crafting (Coin-Sink):** 5 Common + 2.000 C → 1 Rare; 5 Rare + 8.000 C → 1 Epic; 5 Epic + 25.000 C → 1 Legendary.
- **Verlangsamungs-Stacking** multiplikativ: Frost 0.5× · TimeWarp 0.5× · BlackHole 0.3×.
- `OwnedCard` (CardId + Level + Count), `ICardService` verwaltet Deck/Upgrade/Crafting.

---

## 9. PowerUps (12 + Cure)

> `PowerUpType.cs` — 12 PowerUp-Typen + Cure (Curse-Heilung). Level-basierte Freischaltung via
> `GetUnlockLevel()` (Story filtert gesperrte heraus). **[3D]** 3D-Pickup-Modelle mit Glow + Discovery-Overlay.

| PowerUp | Effekt | Persistenz | Unlock-Level |
|---------|--------|-----------|--------------|
| **BombUp** | +1 gleichzeitige Bombe (max 10) | permanent | 1 |
| **Fire** | +1 Explosions-Reichweite (max 10) | permanent | 1 |
| **Speed** | +1 SpeedLevel | bis Tod | 1 |
| **Kick** | Bombe gleitet in Blickrichtung (Slide 160 px), stoppt am Hindernis | bis Tod | 10 |
| **Wallpass** | Durch zerstörbare Blöcke laufen | bis Tod | 15 |
| **Mystery** | **35 s Unverwundbarkeit** | temporär | 15 |
| **Cure** | Heilt Curse-Status sofort (grünes Kreuz) | sofort | 15 |
| **Skull** | Curse: 4 Typen (Diarrhea/Slow/Constipation/ReverseControls), 10 s | temporär (Strafe) | 20 |
| **Detonator** | Manuelle Bomben-Zündung | bis Tod | 25 |
| **Bombpass** | Durch eigene Bomben laufen | bis Tod | 25 |
| **Flamepass** | Immun gegen Explosionen (nicht gegen Gegner) | bis Tod | 35 |
| **LineBomb** | Alle Bomben in Blickrichtung in einer Linie | bis Tod | (ab L30) |
| **PowerBomb** | Range = FireRange + MaxBombs − 1, verbraucht alle Slots | bis Tod | (ab L40) |

> Hinweis: Mystery = 35 s Unverwundbarkeit (nicht "Random-Power"); Cure ist ein eigenständiger 13. Eintrag
> (Skull und Cure sind getrennt). Unlock-Level exakt aus dem Code.

---

## 10. Combo-System

> `ComboSystem.cs` — Kills innerhalb 2-s-Fenster steigern den Combo-Zähler. **[3D]** Combo-Floating-Text
> mit Größen-Pop + Slow-Mo + Vignette-Flash.

| Combo | Score-Bonus | Besonderheit |
|-------|------------|--------------|
| ×2 | +200 | — |
| ×3 | +500 | — |
| ×4 | +1.000 | — |
| ×5 | +2.000 | **MEGA**, Slow-Mo 0.8 s |
| ×6 | +4.000 | Window +0.5 s |
| ×7 | +8.000 | — |
| ×8 | +15.000 | — |
| ×9 | +20.000 | — |
| ×10+ | +30.000 | **ULTRA**, Slow-Mo 1.2 s, Vignette-Flash |

- **Window** 2 s, +0.5 s ab ×6. **Slow-Motion-Multiplikator** 1.5× bei ULTRA. **Chain-Kill** 1.5× bei 3+ Kills.
- Crit-Indicator-Größen: ×2-3 = 18f / ×4-6 = 22f / ×7-9 = 26f / ×10+ = 32f (Hades-Pattern).

---

## 11. Spielmodi (8)

> 8 Modi (`IGameMode`-Implementierungen in `Core/Modes/`). Alle übernommen.

| Modus | Beschreibung | Belohnung |
|-------|--------------|-----------|
| **Story** | 100 Level in 10 Welten, Sterne-Rating | Coins, Sterne, Karten, PowerUp-Discovery |
| **Master-Mode (Reborn)** | Nach L100: Gegner ×1.5 Speed, Typ-Upgrade (Ballom→Minvo, Onil→Pass, Doll→Pontan), separater Persistenz-Pfad (`IMasterModeService`) | Master-Sterne (Normal-Sterne unberührt) |
| **Daily-Challenge** | Tägliches deterministisches Level (Tages-Seed `yyyy×10000+MM×100+dd`), Streak-Tracking | Coins + Daily-Token |
| **Quick-Play** | Zufalls-Level | Coins (kein Sterne-Update) |
| **Survival** | Endlos bis Tod (`SurvivalSpawner`) | Coins + Highscore |
| **Dungeon** | Roguelike (siehe §12) | DungeonCoins, Buffs, Karten |
| **Boss-Rush** | 5-Boss-Sequenz (`IBossRushService`), ISO-Year-Week-Reset | Boss-Coins + Karten |
| **Daily-Race** | 1 deterministisches Tages-Level weltweit, schnellster Run gewinnt (separate Liga) | Race-Coins + Daily-Race-Liga |

Zusätzlich: **Weekly-Challenge** (5 Missionen/Woche aus 14er-Pool, Montag-Reset) und **Daily-Missions**
(3/Tag aus 14er-Pool, Mitternacht-UTC) als Aufgaben-Layer über den Modi.

> **[Integration]** Im Original laufen die Mode-Klassen parallel zu Bool-Flags; die `UpdateLogic`-Hooks
> sind teils noch nicht aus der GameEngine migriert. Im Remake werden die Modi sauber als
> Mode-Plugins (`IGameMode`) implementiert (kein Bool-Flag-Routing).

---

## 12. Roguelike-Dungeon

> `IDungeonService` + `DungeonSynergyResolver` + `Models/Dungeon/`. **[3D]** Node-Map als 3D-Karte,
> Buff-Pick-Phase mit Karten-Reveal.

### 12.1 Run-Struktur

- **Floor 1-4** normal, **Floor 5** Mini-Boss, **Floor 6-9** härter, **Floor 10** End-Boss + Truhe,
  **ab Floor 11** +50 % Skalierung.
- **Node-Map:** 10×3 (Slay-the-Spire-inspiriert), Pfad-Auswahl zwischen Nodes.
- **5 Raum-Typen (Gewichtung):** Normal (W40), Elite (W20), Treasure (W15), Challenge (W15), Rest (W10).
  Challenge-Modi: SpeedRun (60 s) / NoPowerUps / DoubleEnemies.
- **8 Floor-Modifikatoren** ab Floor 3, 30 % Chance.
- **Buff-Pick** auf festen Floors `[2, 4, 5, 7, 9, 12, 14]`.
- **Eintritt:** 1×/Tag gratis, sonst 500 Coins / 3 Gems / Rewarded-Ad (1×/Tag). Datum-Tracking in
  `DungeonStats` (nicht im RunState) — Anti-Restart-Exploit.
- **Dungeon-Trennung:** Shop-Upgrades gelten **nicht** im Dungeon (nur Base-Stats + Dungeon-Buffs).
- **Run-Reset** bei Tod/Aufgabe: Buffs verfallen, Coins/Karten/Loot bleiben.

### 12.2 16 Buffs (`DungeonBuff.cs`)

- **5 Common:** ExtraBomb, ExtraFire, SpeedBoost, CoinBonus, BombTimer.
- **5 Rare:** u.a. BlastRadius, Crit-Chance, Bomb-Crit, Affix-/Shield-on-Hit, Reflect.
- **2 Epic:** u.a. ExtraLife, Combo-Multiplier / Heal-on-Crit.
- **4 Legendary:** Berserker, TimeFreeze, GoldRush, Phantom.

> Hinweis: Common-Pool ist ExtraBomb/ExtraFire/SpeedBoost/CoinBonus/BombTimer (ExtraLife ist Epic,
> BlastRadius ist Rare) — exakt aus `DungeonBuff.cs`.

### 12.3 5 Synergien (`DungeonSynergyResolver` — je 2-Buff-Paare)

| Synergie | Buff-Paar | Effekt |
|----------|-----------|--------|
| **Bombardier** | ExtraBomb + ExtraFire | Bomb-Bonus |
| **Blitzkrieg** | SpeedBoost + BombTimer | Mobilität/Tick-Bonus |
| **Festung** | Shield + ExtraLife | Defensiv-Bonus |
| **Midas** | CoinBonus + GoldRush | Coin-Bonus |
| **Elementar** | EnemySlow + FireImmunity | Element-Bonus |

> Hinweis: Alle 5 Synergien sind 2-Buff-Paare (nicht "3+ Buffs" / "5+ Karten" wie in v0.2 falsch beschrieben).

### 12.4 Dungeon-Meta-Progression

**8 permanente Dungeon-Upgrades** (`DungeonUpgrade.cs`, Währung **DungeonCoins** aus Floor-Wins). Diese
sind dauerhaft (überleben Run-Reset) und vom normalen Shop getrennt.

---

## 13. Liga & Ranking

> `ILeagueService` (Firebase RTDB). **Async-Score-Leaderboard** — kein Echtzeit-PvP, kein ELO/Glicko/MMR.
> (Ein etwaiges Skill-Rating für optionalen Online-Versus wäre **[NEU]** und kein Original-Port.)

### 13.1 Liga-Struktur

| Tier | Sub-Tiers | Punktschwelle (Tier-Einstieg) |
|------|-----------|-------------------------------|
| **Bronze** | I / II / III | 0 |
| **Silver** | I / II / III | 400 |
| **Gold** | I / II / III | 900 |
| **Platinum** | I / II / III | 1.600 |
| **Diamond** | (single, Endgame) | 2.500 |

- **Sub-Tiers** via Drittelung der Tier-Spanne (`GetSubTier(points)`); Diamond ohne Sub-Tier.
- **Promotion/Relegation perzentil-basiert am Saisonende:** Top 30 % steigen auf, Bottom 20 % ab
  (`LeagueService`). **Kein** "5 Wins → Sub-Tier"-Mechanismus.
- **14-Tage-Saisons.** Saison-Reset über `seasonReset`-Pfad.
- **NPC-Backfill** bei < 20 echten Spielern (Seeded Random).
- **Profanity-Filter** (Unicode-NFKD + Strip + Lowercase, deckt Leetspeak/Zero-Width).
- **Firebase:** Anonymous Auth, `league/s{saison}/{tier}/{uid}`, Write-Rate-Limit 60 s via
  Server-Timestamp, Report-Button (`reports/{reportedUid}/{reporterUid}`, 24 h Rate-Limit).

### 13.2 Daily-Race (separate Liga)

- 1 deterministisches Tages-Level weltweit (Seed `yyyy×10000+MM×100+dd`).
- Schema `league/s{saison}/daily_race/{date}/{tier}/{uid}`.
- Schnellster Komplett-Run gewinnt, Tier-Belohnungen.

---

## 14. Battle Pass & Saisons

> `IBattlePassService` — **30 Tiers**, **30-Tage-Saison**, XP-basiert, Free/Premium-Track.

### 14.1 Struktur

- **30 Tiers** (`BattlePassTier.cs`, MaxTier = 30). Free-Track umfasst alle 30 Tiers; Premium-Track
  zusätzliche/höherwertige Rewards.
- **Saison-Dauer 30 Tage.** XP aus Gameplay/Quests/Achievements. `XpBoostStartTicks`-Hybridtimer (24 h Boost).
- **10 Themes** (`BattlePassTheme.cs`), deterministisch aus Saison-Nummer: Classic, Cyberpunk, Halloween,
  Winter, Summer, Mech, Underwater, Sengoku, DiaDeLosMuertos, Steampunk. Saison 1 = Classic, dann Rotation.
- `BattlePassThemeExtensions` liefert Akzent-/Sekundärfarben, Icon-Hints, RESX-Keys.

> Hinweis: Liga-Saison (14 Tage) und Battle-Pass/Content-Saison (30 Tage) sind **zwei verschiedene Zyklen** —
> nicht koppeln (siehe ARCHITECTURE: getrennte Scheduled-Functions).

### 14.2 Premium-Monetization

Battle-Pass-Premium + Battle-Pass-Plus als IAP (siehe §16). **Keine** Zufalls-Belohnungen — klare Rewards pro Tier.

---

## 15. Achievements (72)

> `AchievementService.cs` — **72 Achievements** in **5 Kategorien** (`Achievement.cs`), JSON-Persistenz.
> Belohnungen: Coins/Gems/Cosmetics/Hero-Unlocks.

| Kategorie | ca. Anzahl | Beispiele |
|-----------|-----------|-----------|
| **Progress** | ~24 | Welt-Abschlüsse, Level-Meilensteine, Master-Mode |
| **Skill** | ~16 | 3-Sterne-Runs, 0-Death, Combo-Stufen |
| **Mastery** | ~14 | Lv-Max, Karten/PowerUp-Meisterung |
| **Combat** | ~14 | Kill-Zähler, Boss-Siege, Ultra-Combo |
| **Challenge** | ~4 | Spezial-Bedingungen, Daily/Weekly-Streaks |

- Verteilung gegen `AchievementService.cs:585-728` verifiziert (Summe = 72).
- **Hero-Unlock-Kopplung:** mehrere Helden werden über Achievement-IDs freigeschaltet
  (`ach_speed_demon`, `ach_block_destroyer`, `ach_jackpot`) — die Achievement-IDs müssen 1:1 erhalten bleiben.

> Hinweis: Es sind **72**, nicht 66; die Kategorien sind Progress/Mastery/Combat/Skill/Challenge,
> **nicht** Story/Collection/Multiplayer/Cooperative.

---

## 16. Wirtschaft & Monetization

### 16.1 Währungen

| Währung | Quelle | Verwendung |
|---------|--------|-----------|
| **Coins** | Level-Score / 3 (Welt 1: / 2), Win-Bonus | Shop-Upgrades, Card-Crafting, Skins |
| **Gems** | 3 bei erstmaligem 3-Sterne-Abschluss; IAP; Quests/BP | Deck-Slot, Hero-Direct-Buy, Premium-Karten, Dungeon-Eintritt |
| **DungeonCoins** | Dungeon-Floor-Wins | 8 permanente Dungeon-Upgrades |

> Premium-Multiplikator: 2× Coins bei LevelComplete, 3× bei GameOver-Trostcoins. Coin/Gem-**Overflow-Guard**
> (`(long)Balance + amount` Clamp auf int.MaxValue; Load clampt < 0 + Corruption-Flag).

### 16.2 Shop (permanente Upgrades)

**9 permanente Shop-Upgrades** (`UpgradeType.cs`, Preise 700-17.000 Coins):
StartBombs (max 3), StartFire (max 3), StartSpeed (max 1; Preiskurve [1.200/2.500/7.000]),
ExtraLives (max 2), ScoreMultiplier (max 3), TimeBonus (max 1), ShieldStart (max 1),
CoinBonus (max 2; L2 +60 %), PowerUpLuck (max 2). Zusätzlich Freischaltung der 3 Shop-Spezial-Bomben
(Ice/Fire/Sticky).

> **Wichtig:** Dieses permanente Stat-Upgrade-System ist der zentrale Coin-Sink und bleibt **erhalten**
> (die frühere Plan-Version v0.2 hatte es durch Hero-Talent-Bäume ersetzt — das wird verworfen).

### 16.3 IAP-Palette (aus `BomberBlastIapSkus.cs`)

| Produkt | Inhalt |
|---------|--------|
| **Remove-Ads** | Banner/Interstitial weg (Rewarded bleibt opt-in) — **1,99 EUR** (Original-Preis) |
| **Gem-Pakete** | 4 Größenstufen |
| **Battle-Pass-Premium / -Plus** | Saison-Premium-Track / Plus |
| **VIP-Subscription** | Abo-Vorteile (`VipSubscriptionService`) |
| **Starter-Pack** | Einmal-Angebot im ersten Start-Fenster (`StarterPackService`) |
| **First-Purchase-×2** | Verdoppelt den ersten IAP-Kauf (`FirstPurchaseService`, Anti-Reinstall) |

> Konkrete Preise/Inhalte 1:1 aus `BomberBlastIapSkus.cs` übernehmen. Re-Pricing nur als bewusste,
> dokumentierte Entscheidung — Default = Original-Werte.

### 16.4 Werbe-Modell (wie Original)

- **5 Rewarded-Placements:** `continue` (Coins verdoppeln), `level_skip`, `power_up` (ab L20),
  `score_double`, `revival`. Hybrid-Cooldown (TickCount64 + persistierte UTC, 60 s).
- Banner/Interstitial entfallen bei Remove-Ads/VIP.

### 16.5 Monetization-Ethik (unverändert)

- **Keine Lootboxen** (UK/China-Compliance). Lucky-Spin behält transparente Drop-Rates + Pity-Counter.
- **Keine Pay-to-Win-Stats** in kompetitiven Modi.
- Saison-Content auch über Gameplay erreichbar.

---

## 17. Cosmetics & Player-Identity

> 98 Cosmetic-Definitionen + Spieler-Skins. **[3D]** als 3D-Trail-VFX / 3D-Frame-Overlays / 3D-Victory-Animationen.

### 17.1 Cosmetic-Pools (exakt)

| Typ | Anzahl | Quelle |
|-----|--------|--------|
| **Trails** | 32 (`TrailDefinitions.All`) | Bewegungs-Spuren |
| **Frames** | 33 (`FrameDefinitions.All`) | Profilbild-Umrandung |
| **Victories** | 33 (`VictoryDefinitions.All`) | Sieg-Animation |
| **Spieler-Skins** | (CustomizationService) | Coin- + Gem-Skins, Hero-Body/Accent |

**Themen:** welt-thematisch (Pumpkin/Snowflake/CherryBlossom/Neon/Bone/Ocean/Samurai/Mech/Beach/Steampunk)
+ Karriere-Status (Champion/PrestigeAura/Diamond/Master/Ascension) + BattlePass-Saison-Exclusives.

> Hinweis: Das Original hat **keine** Emotes/Sprays/Match-Intros. Solche Typen wären **[NEU]** und nicht
> Teil der 98 — bei Bedarf separat als Erweiterung planen, nicht in die Portier-Zahl mischen.

### 17.2 Cosmetic-Quellen

Battle Pass (Free + Premium), Liga-Tier-Rewards, Achievement-Rewards, saisonale Event-Drops,
Cosmetic-Shop (Gems), Lucky-Spin.

---

## 18. Daily / Weekly / Live-Events

### 18.1 Wiederkehrende Aufgaben

- **Daily-Reward:** 7-Tage-Login-Bonus + Comeback-Bonus (> 3 Tage inaktiv) (`IDailyRewardService`).
- **Daily-Missions:** 3/Tag aus 14er-Pool, Mitternacht-UTC-Reset (`IDailyMissionService`).
- **Weekly-Missions:** 5/Woche aus 14er-Pool, Montag-Reset (`IWeeklyChallengeService`).

### 18.2 Events

- **Wochen-Events (8, deterministisch via ISO-Wochen-Seed `(year×7+week) % 8`):** DoubleXp, DoubleCoins,
  CardRain, BossWeek, DungeonRush, LeagueRumble, MissionMadness, LuckyWeek (`IEventCalendarService`,
  12-Wochen-Vorschau + Server-Override).
- **Saisonale Events:** Halloween, Christmas, NewYear, Summer (`IEventService`) mit Partikel-Overlays.
- **Wochen-Content** (`IWeeklyContentService`): 8 WeeklyModifier-Pool + 4 WeeklyReward-Pool + 3 wechselnde
  Boss-Modifier/Woche (Fisher-Yates), ISO-Wochen-deterministisch.

### 18.3 Lucky-Spin

- **9 gewichtete Segmente**, 1×/Tag gratis, **Pity-Counter** (garantierter Hit nach Pity-Schwelle),
  `GetDropRates()`-API für Compliance-Disclosure (`ILuckySpinService`).

### 18.4 Rotating-Deals

- 3 tägliche + 1 wöchentliches Angebot, 20-50 % Rabatt (`IRotatingDealsService`).

---

## 19. Onboarding & Tutorial

### 19.1 Tutorial-Phasen (3)

- **T1 Movement** — Joystick + Bombe legen + Block zerstören.
- **T2 Bombs** — mehrere Bomben, erster Gegner-Kill.
- **T3 PowerUps** — BombUp/Fire/Speed, Combo-Einführung.

`TutorialPhase`-Enum + `TutorialStep.IsFirstOfPhase`, Phasen-Banner via `PhaseChanged`-Event.

### 19.2 Feature-Unlock-Choreographie (`IFeatureUnlockChoreographer`)

| Level | Feature |
|-------|---------|
| L10 | Daily-Challenge |
| L20 | Dungeon |
| L30 | LineBomb |
| L40 | PowerBomb |
| L50 | Boss-Rush |
| L100 | Master-Mode |
| `ach_master_100` | Champion-Skin |

Queue-basiert, Pref-Flag pro Feature (einmal pro Lebenszeit), UI-Thread-Event `FeatureUnlocked`.

### 19.3 Weitere Onboarding-Systeme

- **Discovery-System:** Pausiert bei Erstentdeckung von PowerUps/Mechaniken (`DiscoveryOverlay`).
- **What's-New-Modal** (`IWhatsNewService`), **Re-Engagement-Push** (D1/D3/D7), **First-Win-Cinematic** (4-stufig).

---

## 20. Accessibility

> `IAccessibilityService` (Singleton) — vollständig übernommen.

| Mandat | Implementierung |
|--------|-----------------|
| **Colorblind** | Off / Deuteranopia / Protanopia / Tritanopia via ColorMatrix (Brettenmacher/Vienot) — **[3D]** als URP-PostProcessing |
| **HighContrast** | Outline-Pass + verstärkte Floating-Text-Outline |
| **UiScale** | 0.75 / 1.0 / 1.25 / 1.5 |
| **Subtitles** | Struct-Pool-Captions (max 4 aktiv), für Boss-Spawn/Time-Warning/Death/Level-Complete/Ultra-Combo/Victory |
| **Colorblind-Hint-Heuristik** | proaktiver Nudge bei schnellem L1-Fail (`RegisterL1Fail`/`ShouldOfferColorblindHint`) |
| **Reduced-Effects** | Photosensitivity: unterdrückt Flash/Screen-Shake-Spitzen |
| **Multi-Sprache** | DE/EN/ES/FR/IT/PT (Original-Parität) |

---

## 21. Audio-Design

> Basis: Kenney CC0 (Original-Mandat "kein Geld"). **[besser]** Im Remake aufgewertet (kuratierte/eigene
> Loops, adaptive Layer). Voice bleibt **deferred** (im Original bewusst abgewählt).

### 21.1 Übernommene Audio-Architektur

- **7-Kanal-AudioBus** (Master/Music/Ambient/Sfx/Ui/Voice/Cinematic) mit `AudioBusMixer` (Ducking, Boost, Recovery).
- **Spatial:** Stereo-Pan via `bomb.GridX / Grid.Width`, Distance-Falloff, Equal-Power-Crossfade, Reverb-Presets.
- **Anti-Repeat-Pool** (Brawl-Stars-Pattern): 27 Pool-Variants + 5 Cinematic-Stinger.
- **Stinger:** BOSS_REVEAL, COMBO_MEGA (×5), COMBO_ULTRA (×10), VICTORY, DEFEAT.
- **Adaptive Music-Boost:** Last-Enemy-Drama (+20 %/4 s), ULTRA-Combo (+25 %/5 s).
- **Pitch-Variation:** ±5 % Pitch + ±10 % Volume auf wiederholte SFX.
- **10 welt-thematische Music-Loops.**

### 21.2 Aufwertung im Remake (markiert)

- **[besser]** Kuratierte/eigene Loops statt reiner CC0; optional FMOD für adaptive Layer.
- **[besser]** 3D-Spatial-Audio (URP/Unity-Audio statt 2D-Pan).
- **[deferred]** Voice (DE/EN) — wie im Original als zukünftiges Budget-Item, kein Launch-Inhalt.

---

## 22. UI/UX-Konzept

### 22.1 Haupt-Bildschirme (Struktur wie Original)

Bottom-Tab-Navigation (Home / Play / Shop / Profile), `ActiveView`-gesteuert. Views u.a.: MainMenu,
PlayHub, LevelSelect, Game, GameOver, Victory, Shop, GemShop, Deck, Collection, Profile (mit Statistics/
Achievements/Collection-Sub-VMs), Settings, BattlePass, League, Dungeon, BossRush, DailyChallenge,
WeeklyChallenge, LuckySpin, Help, HighScores + Overlays (DailyReward, Onboarding, WhatsNew, SeasonBanner).

### 22.2 In-Game-HUD

- **Side-Panel rechts** (Landscape): Time / Score / Combo / Lives / Deck.
- **NeonJoystick:** Radius 75 dp, Bomb-Button 52 dp, Detonator 48 dp; Floating- oder Fixed-Modus
  (Default Fixed bei Neuinstallation). Separate Pointer-IDs, Pre-Turn-Buffering.
- **Card-Quickswap:** ActiveCardSlot per HUD-Tap.

### 22.3 Overlay-/Modal-System

- Zentrale Hit-Test-Aggregate (`IsAnyOverlayOpen` / `IsAnyDialogOpen`) gegen Android-ZIndex-Tap-Durchgriff.
- **[3D]** Modal-Übergänge (Slide/Fade) via DOTween; Iris-Wipe bei Level-Start/Complete.

---

## 23. 3D-Visuals & Game Juice (der "besser"-Teil)

> Das Original hat bereits umfangreiches "Game Juice". Im Remake wird es in 3D umgesetzt und erweitert.

### 23.1 Übernommene Juice-Patterns (in 3D)

Floating-Text (Score/Combo/PowerUp), Currency-Pulse, Iris-Wipe, Slow-Motion (Combo ×4+/letzter Kill),
Hit-Pause (Kill 50 ms / Death 100 ms), Squash & Stretch, prozedurale Walk-Animation, Boss-Banner,
Confetti, saisonale Event-Partikel, Trauma-basierter Screen-Shake (Squirrel-Eiserloh-Modell + PullBack),
Vignette-Flash (ULTRA-Combo + Damage), Player-i-Frame-Visualisierung, Anticipation-Frames, Outline-Pass,
First-Win-Cinematic, Boss-Reveal-Cinematic (1.5 s), Victory-Cinematic (2.5 s).

### 23.2 3D-Aufwertung (markiert)

- **[3D]** Dynamische Beleuchtung (URP Forward+), Schatten, emissive Neon-Materialien, Bloom (Ultra-Tier).
- **[3D]** VFX-Graph-Explosionen pro Bomben-Typ (GPU-Partikel statt SkiaSharp-CPU).
- **[3D]** Shader-Graph-Effekte: Glow, Dissolve, Hologramm, Outline, Liquid (Frost/Lava/Poison-Felder).
- **[3D]** Cinemachine-Kamera (Top-Down mit leichter Neigung, Damping, Impulse für Shake/Zoom).
- **[3D]** 3D-Welt-Story-Cutscenes (Timeline).

### 23.3 Performance-Adaption (übernommen)

`HardwareTier` (Low/Medium/High/Ultra) + adaptives Frame-Skipping. Partikel-Caps pro Tier (300/800/1200/1500),
Bloom nur Ultra, Reverb ab High. (Details ARCHITECTURE Performance-Targets.)

---

## 24. Multiplayer (optionales Plus)

> **[NEU]** Multiplayer ist im Original nur **Foundation** (Mode-Enum, PlayerInputSnapshot, InputBuffer,
> GameStateSnapshot mit FNV-1a-Hash) — nie ins Gameplay integriert. (Ausnahme: das **Clan-System**
> `FirebaseClanService` ist voll integriert und wird 1:1 übernommen.)

### 24.1 Ausbaustufen (Post-Core, nicht launch-kritisch)

| Stufe | Inhalt | Tech |
|-------|--------|------|
| **Local-Coop/Versus** | 2 Spieler an einem Gerät/Gamepads (`LocalCoop`/`LocalVersus` aus `MultiplayerMode`) | lokal, dual Input-Routing |
| **Online-Co-op** (später) | 2-4 Spieler kooperativ | Photon Realtime (Host-authoritative) |
| **Online-Versus** (optional) | kompetitiv | Photon Fusion — **erfordert** Determinismus-Integration (siehe ARCHITECTURE) |

### 24.2 Voraussetzung

Echtes Online-MP setzt die **Determinismus-Integration** voraus (alle Gameplay-Random über `IRngProvider`,
Sim/Render-Trennung, ggf. Fixed-Point für hash-stabile Sim). Bis dahin: Local-MP + async Clan/Liga.

> **Spawn:** P1 = (1,1), P2 = (13,8) (gegenüberliegende Ecken, `MultiplayerSpawnPositions`).

---

## Änderungslog (DESIGN)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-DESIGN mit Sci-Fi-Reinvention (8 Mech-Helden, OmniCorp, PvP-Arena) | Robert Schneider + Claude |
| 2026-05-30 | **v0.2** | **Komplett neu auf treuen 3D-Remake: echtes BomberBlast-Game-Design (5 Helden, 12 Gegner, 5 Bosse, 12 PowerUps, 13 Karten, 100 Level, 16 Dungeon-Buffs/5 Synergien, Liga 5×3 + Perzentil, 30 BP-Tiers, 72 Achievements, 9 Shop-Upgrades, 98 Cosmetics) — alle Werte code-verifiziert; 3D/Besser/NEU markiert** | Robert Schneider + Claude |

---

> **Status:** Treuer Remake v0.2. Quelle der Wahrheit = produktiver BomberBlast-Code.
> **Nächster Schritt:** Parity-Matrix (jedes Original-System → Unity-Äquivalent) als Port-Checkliste.
