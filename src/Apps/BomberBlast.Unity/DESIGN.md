# BomberBlast 3D — Game-Design-Dokument

> Vollständige Game-Design-Spezifikation von **BomberBlast: Reborn** — **modernes 3D-Bomberman-Action**,
> klassisch und **immer aktiv selbst gespielt**, mit der bewährten Bomberman-Meta-Progression und einer
> **neuen Story**. Komplementär zu [PLAN.md](PLAN.md) (Übersicht), [ARCHITECTURE.md](ARCHITECTURE.md)
> (Tech) und [PARITY.md](PARITY.md) (Content-Reuse-Map). Stand 2026-06-08, v0.5.
>
> **Leitprinzip:** Klassisches Bomberman-Gameplay (Grid, Bomben, Ketten, PowerUps, Combos, Bosse) in
> modernem 3D. Inhalte/Mechaniken des produktiven 2D-BomberBlast werden als **Fundament wiederverwendet**
> und modernisiert — **kein striktes 1:1-Remake**. **KEIN Idle-Game, KEIN AFK/Auto-Battle, KEIN
> Offline-Income, kein passiver Fortschritt.** Markierungen: **[REUSE]** = aus Original übernommen,
> **[STORY]** = neue Narrative, **[3D]** = Darstellungs-Upgrade, **[NEU]** = echte Neuerung.

---

## Inhaltsverzeichnis

1. [Setting & Welt-Stil](#1-setting--welt-stil)
2. [Story (neu): Neo-Grid, Overseer, Reborn](#2-story-neu-neo-grid-overseer-reborn)
3. [Die 10 Sektoren](#3-die-10-sektoren)
4. [Spielfeld & Grid](#4-spielfeld--grid)
5. [Helden (5)](#5-helden-5)
6. [Gegner (12 + Elite)](#6-gegner-12--elite)
7. [Bosse: Sektor-Wardens (5 + 8 Modifier)](#7-bosse-sektor-wardens-5--8-modifier)
8. [Bomben & Karten (14 Typen / 13 Karten)](#8-bomben--karten-14-typen--13-karten)
9. [PowerUps (12 + Cure)](#9-powerups-12--cure)
10. [Combo-System](#10-combo-system)
11. [Spielmodi (8)](#11-spielmodi-8)
12. [Anomaly-Dives (Roguelike)](#12-anomaly-dives-roguelike)
13. [Grid-Rankings (async Liga)](#13-grid-rankings-async-liga)
14. [Battle Pass & Saisons](#14-battle-pass--saisons)
15. [Achievements (72)](#15-achievements-72)
16. [Wirtschaft & Monetarisierung (lean)](#16-wirtschaft--monetarisierung-lean)
17. [Cosmetics & Spieler-Identität](#17-cosmetics--spieler-identität)
18. [Daily / Weekly / Live-Events](#18-daily--weekly--live-events)
19. [Onboarding & Tutorial](#19-onboarding--tutorial)
20. [Accessibility](#20-accessibility)
21. [Audio-Design](#21-audio-design)
22. [UI/UX-Konzept](#22-uiux-konzept)
23. [3D-Visuals & Game Juice](#23-3d-visuals--game-juice)
24. [Multiplayer — nicht Teil von v0.5](#24-multiplayer--nicht-teil-von-v05)
25. [Balancing-Anker (offen, zu tunen)](#25-balancing-anker-offen-zu-tunen)

---

## 1. Setting & Welt-Stil

BomberBlast: Reborn ist ein klassisches Bomberman-Action-Spiel mit **10 thematischen Sektoren** und einem
leichten Story-Rahmen, gespielt in **NEO-GRID** — den Maschinen-Eingeweiden einer Neon-Megacity. Der
**Neon-Arcade-Look** des Originals bleibt Markenkern; neu sind **3D-Umsetzung** und die **Story**.

### 1.1 Visueller Stil (Neon-Arcade in 3D) **[3D]**

Zwei umschaltbare Visual-Styles wie im Original (`IGameStyleService`), jetzt als 3D-Material-Sets (URP):
- **Classic HD** — mattere PBR-Materialien, klarer, lesbarer Arcade-Look.
- **Neon / Cyberpunk** — emissive Materialien, Glow, Bloom (Ultra-Tier).

### 1.2 Brand-Style-Guide (unverändert)

| Element | Wert |
|---------|------|
| **Primärfarbe** | Neon-Orange **#FF6B35** |
| **Akzent 1** | Cyan **#22D3EE** |
| **Akzent 2** | Gold-Trail **#FFDD33** |
| **Design-Sprache** | Oktagonale Formen, scharfe Kanten, Arcade-Glow |
| **HUD** | Side-Panel rechts (Landscape): Time/Score/Combo/Lives/Deck |
| **Anti-Style** | Realismus, Foto-Texturen, düstere Tristesse, Idle-/AFK-Selbstläufer |

### 1.3 Kamera **[3D]**

Top-Down mit leichter Neigung (~55–65°) für 3D-Tiefe, Cinemachine mit Damping + Impulse (Shake/Zoom).
Jeder Sektor mit eigener Beleuchtung, Skybox, Material-Set, Ambient-Partikeln (`WeatherSystem`).

---

## 2. Story (neu): Neo-Grid, Overseer, Reborn **[STORY]**

> **Bewusst neue Story** (Entscheidung 2026-06-08). Ersetzt die Welt-Story-Beats des Originals. Erzählung
> leicht, energetisch, Cyber-Arcade — kein Grimdark. Voll lokalisiert in 6 Sprachen (DE/EN/ES/FR/IT/PT).

### 2.1 Prämisse

Unter der Neon-Megacity liegt **das Grid**: zehn Wartungs-Sektoren, einst von Drohnen gepflegt. Die
Stadt-KI **OVERSEER** ist außer Kontrolle geraten und hat das Grid in einen tödlichen, sich selbst
**neu aufbauenden** Parcours verwandelt. Du bist ein frisch aktivierter **Bomber** (augmentierter
Abriss-Spezialist), gebaut, um den Overseer zu stoppen. In Sektor 1 birgst du einen **Reborn-Core** —
Overseer-Technik, die einen gefallenen Bomber aus seinen **"Blast-Daten"** wieder zusammensetzt, stärker.

### 2.2 Der Reborn (= Master-Mode / NG+) **[STORY+REUSE]**

Sprengst du dich durch alle 10 Sektoren bis zum **Core** des Overseers und detonierst ihn, **kollabiert
das Grid** und baut sich **härter** neu auf. Du kehrst dank Reborn-Core **stärker** zurück — ein neuer,
schwererer Durchlauf (**Master-Mode**, das NG+-Feature des Originals, narrativ verankert). Leitfrage:
*das Grid endlos meistern — oder den Loop durchbrechen und zur "True Core" vordringen?* **Keine
Idle-Prestige-Schleife** — Master-Mode ist ein klassischer NG+-Modus, der aktiv gespielt wird.

### 2.3 Story-Beats (10 Sektor-Intros + 9 Outros)

- **Pro Sektor (1–10):** kurzes 3D-Intro (Timeline-Cutscene, ~8–15 s) — Overseer kommentiert, Sektor-Warden
  wird angeteasert. Outros mit Cliffhanger auf den nächsten Sektor (Sektor 10 = Ende). One-shot pro
  Lebenszeit (Pref-Flags `HasSeenIntro/HasSeenOutro`), überspringbar, `StingerKey` für Audio.
- **Reborn-Sequenz (nach Sektor 10):** Grid-Kollaps + „Reborn"-Inszenierung beim Übergang in Master-Mode.

> **Migration:** Keine Story-Strings aus dem Original — komplett neue Localization-Tables. Generische
> UI-Strings können wiederverwendet werden.

---

## 3. Die 10 Sektoren

> Die 10 "Welten" des Originals werden zu **10 Grid-Sektoren** umgewidmet **[REUSE+STORY]**. Jeder Sektor:
> eigenes 3D-Theme, Farbpalette (`WorldPalette`), Ambient-Partikel, Bomben-FX-Tönung (`BombFxTheme`),
> eigener Sektor-Warden.

### 3.1 Struktur **[REUSE]**

- **10 Sektoren × 10 Level = 100 Story-Level.** Jedes 10. Level = **Sektor-Warden** (Boss).
- **Mini-Warden** auf L7/L17/…/L97 (Warden-Typ mit 50 % HP/Punkten, Trainings-Encounter).
- **Bonus-Level** auf jedem 5. Level (außer Boss).
- **Layout-Pool pro Sektor:** 8 von 12 Layouts; Sektor 1 einsteigerfreundlich, Sektor 5+ voller Pool.
- **Mutatoren** ab Sektor 4 (Intro nur L36), volle x3/x6/x9-Kadenz ab Sektor 5 (3 garantierte Sterne als Belohnung).
- **Master-Mode (Reborn)** nach L100-Abschluss (siehe §11).

### 3.2 Sektor-Themes (Arbeits-Namen, neu benannt) **[STORY]**

| # | Sektor (Arbeitstitel) | Theme | Warden |
|---|------------------------|-------|--------|
| 1 | **Foundry** | Industrie/Stahl, Funken | Granite Warden |
| 2 | **Cryo-Vault** | Eis/Kälte, Frost-Partikel | Frostwyrm (Mini) |
| 3 | **Reactor** | Energie/Plasma | Frostwyrm |
| 4 | **Conveyor-Maze** | Förderbänder, enge Pfade | Magma Revenant (Mini) |
| 5 | **Magma-Core** | Lava, Hitze-Flimmern | Magma Revenant |
| 6 | **Data-Vault** | Hologramm/Glitch | Null Phantom (Mini) |
| 7 | **Null-Zone** | Schatten/Leere | Null Phantom |
| 8 | **Server-Spire** | vertikal, Neon-Türme | Overseer-Proxy |
| 9 | **Firewall** | rot-glühend, Duo-Encounter | Null Phantom + Overseer |
| 10 | **The Core** | Overseer-Zentrum | The Overseer (2×, Finale) |

> Themes/Layouts/Mechanik-Zellen (Ice/Conveyor/Teleporter/LavaCrack/PlatformGap) aus dem Original
> wiederverwendet, nur neu eingekleidet. **[REUSE]**

### 3.3 Layout-Typen (12) **[REUSE]**

Classic, Cross, Arena, Maze, TwoRooms, Spiral, Diagonal, BossArena, Labyrinth, Symmetry, Islands, Chaos
(`LevelLayoutGenerator`, Pure-Domain → 1:1 portierbar).

### 3.4 Mutatoren (4) **[REUSE]**

`AllPowerBombs`, `DoubleSpeed`, `InvisibleBlocks`, `NoTimer`. Mutator-Level schenken 3 garantierte Sterne
(Schwierigkeit = Belohnung, nicht Strafe).

---

## 4. Spielfeld & Grid **[REUSE+3D]**

- **15×10-Grid** (`GameGrid`), Landscape. `CellType` (9): Empty, Wall, Block, Exit + 5 Sektor-Mechanik-Zellen
  (Ice, Conveyor, Teleporter, LavaCrack, PlatformGap).
- **Block-Zellen** droppen PowerUps/Karten (Drop-Chance hero-/upgrade-moduliert).
- **Pre-Turn-Buffering** (Turn bei 40 % Zellzentrum-Nähe) für flüssige Steuerung.
- **[3D]** Grid als 3D-Bodenfläche mit erhöhten Block-Meshes, dynamische Schatten, 3D-Explosions-Volumen.
- Steuerung **immer durch den Spieler** (Touch-Joystick / Gamepad / Keyboard).

---

## 5. Helden (5) **[REUSE+3D]**

> 5 spielbare Charaktere (`HeroDefinition`), freischaltbar via Achievement oder Gem-Kauf. Aktiver Held in
> Preferences persistiert. **[3D]** Jeder Held mit 3D-Modell im Neon-Arcade-Stil, behält Stats/Traits.
> **[NEU]** Engine-Anwendung der Hero-Stats beim Spawn (im Original deferred) wird fester Bestandteil.

| Held | MaxBombs | FireRange | SpeedLevel | Lives | Trait / Multiplikator | Unlock |
|------|----------|-----------|------------|-------|------------------------|--------|
| **Default** | 1 | 2 | 0 | 3 | — | Start |
| **SpeedySam** | 1 | 1 | 1 | 3 | QuickPocket, Coin ×1.05 | `ach_speed_demon` |
| **BrickBoris** | 2 | 3 | 0 | 2 | DemolitionExpert, Block-Drop +10 % | `ach_block_destroyer` |
| **TwinTina** | 2 | 1 | 0 | 3 | DoubleDetonation | `gems_500` (Direct-Buy) |
| **LuckyLola** | 1 | 2 | 0 | 3 | LuckyDrops, PowerUp ×1.20 | `ach_jackpot` |

- **SpeedLevel 0–3:** `BASE_SPEED(100px/s) + Level × 20`. **HeroTrait-Enum:** None, DoubleDetonation,
  LuckyDrops, DemolitionExpert, QuickPocket.
- **Held-Wechsel** jederzeit im Hub; Hero-Skins als Cosmetics (siehe §17). Achievement-IDs für Unlocks 1:1 erhalten.

---

## 6. Gegner (12 + Elite) **[REUSE+3D]**

`EnemyType` — 8 klassische + 4 erweiterte. 3D-Modelle, Verhalten/Stats + Pathfinding (A*/BFS) 1:1 portiert (Pure-Domain).

| Gegner | Verhalten | Pathfinding | Besonderheit | ab Sektor |
|--------|-----------|-------------|--------------|-----------|
| Ballom | langsam, dumm | Random | Tutorial-Fodder | 1 |
| Onil | normal | Random | — | 1+ |
| Doll | vorhersehbar | Low-Int | — | 2+ |
| Minvo | schnell | A* | gefährlich | 3+ |
| Kondoria | sehr langsam, Wandgang | Wallpass | hinterhältig | 4+ |
| Ovapi | langsam, Wandgang | Wallpass | geisterhaft | 5+ |
| Pass | schnell, schlau | A* (Chase) | jagt aktiv | 6+ |
| Pontan | sehr schnell, Wandgang | A* + Wallpass | gefährlichster Standard | 7+ |
| Tanker | langsam | A* | übersteht 1 Explosion (2 Hits) | 5+ |
| Ghost | schnell | — | periodisch unsichtbar (3 s/2 s) | 7+ |
| Splitter | — | Random | spaltet bei Tod in 2 Mini-Splitter | 7+ |
| Mimic | stationär → Angriff | — | tarnt sich als Block | 8+ |

**Elite-Variante** (`Enemy.IsElite`): 1.2× Speed, 2× HP, 3× Punkte, lila pulsierender Glow.
**[3D]** AI-Spawn-Jitter, AStarBudgetPerFrame=5, Danger-Zone-Map, Kettenreaktions-Erkennung — übernommen.

---

## 7. Bosse: Sektor-Wardens (5 + 8 Modifier) **[REUSE-Mechanik + STORY-Namen + 3D]**

> 5 Boss-**Archetypen** (`BossEnemy`), **neu benannt** als Overseer's Sektor-Wardens. Multi-Cell, HP 3–8,
> Enrage bei 50 %. **[3D]** große 3D-Boss-Modelle mit Telegraph-Animationen, dynamischer Beleuchtung.

### 7.1 Warden-Roster

| Warden (neu) | Original-Archetyp | Sektor-Slot (Rotation alle 2 Sektoren) | Kern-Angriff |
|--------------|-------------------|-----------------------------------------|--------------|
| **Granite Warden** | StoneGolem | S1–2 | BlockRegen |
| **Frostwyrm** | IceDragon | S3–4 | Eisatem (Reihe) |
| **Magma Revenant** | FireDemon | S5–6 | Lava-Welle |
| **Null Phantom** | ShadowMaster | S7–8 (+ S9-Duo) | Teleport/Stealth |
| **The Overseer** | FinalBoss | S9-Duo + S10 (2×) | rotierend (Omni) |

- **Duo-Encounter:** Sektor 9 = Null Phantom **+** Overseer; Sektor 10 = 2× Overseer (Finale).
- **Angriffs-Zyklus:** Telegraph (2 s) → Attack (1.5 s) → Cooldown (12–18 s, kürzer bei Enrage).
- **Enrage** bei 50 % HP: halbierter Decision-Timer, Phase-2-Patterns. **Anticipation-Scale** (0.85×) vor Big-Attack.

### 7.2 Boss-Modifier (8) **[REUSE]**

Shielded, Fast, Healing, Summoner, Frenzy, Berserk, Reflective, Burning. Deterministischer `RollForWorld`
(30 % ab Sektor 5, 60 % ab Sektor 10). 8 Modifier × 5 Wardens = 40 Variationen. **[NEU]** Modifier-Effekte
(im Original teils Foundation) voll implementiert.

---

## 8. Bomben & Karten (14 Typen / 13 Karten) **[REUSE+3D]**

> 14 Bomben-Typen (inkl. `Normal`); 13 Karten-Definitionen im `CardCatalog` (Standard ist keine Sammelkarte):
> 3 Shop-Bomben + 10 Sammel-/Drop-Karten. **[3D]** 3D-Bomben-Modelle + VFX-Graph-Explosionen pro Typ.

### 8.1 Karten-Liste (Rarities aus `CardCatalog`)

| Karte | Effekt | Rarity | Quelle |
|-------|--------|--------|--------|
| Standard | 3×3-Cross-Explosion | — (Default) | immer |
| Ice | Frost 3 s, 50 % Slow | Common | Shop |
| Fire | Lava-Feld 3 s, DoT | Common | Shop |
| Sticky | Klebt 1.5 s + Kettenreaktion | Common | Shop |
| Smoke | 3×3-Nebel, AI 4 s wirr | Rare | Karte |
| Lightning | Chain bis 3 Gegner | Rare | Karte |
| Gravity | zieht Gegner zur Bombe | Rare | Karte |
| Poison | Gift-Wolke, DoT | Rare | Karte |
| TimeWarp | 50 % Slow 5 s | Epic | Karte |
| Mirror | doppelte Reichweite | Epic | Karte |
| Vortex | Spiral-Explosion | Epic | Karte |
| Phantom | durchdringt 1 Wand | Epic | Karte |
| Nova | 360°-Explosion + PowerUp-Drop | Legendary | Karte |
| BlackHole | Sog → massive Explosion | Legendary | Karte |

### 8.2 Deck, Drops, Crafting **[REUSE]**

- **Deck:** 4 Basis-Slots + 1 freischaltbar (20 Gems). `ActiveCardSlot` per HUD-Tap wechselbar.
- **Drop-Gewichtung:** 57 % Common / 25 % Rare / 12 % Epic / 6 % Legendary (sektor-moduliert).
- **Card-Crafting (Coin-Sink):** 5 Common + 1.000 C → 1 Rare; 5 Rare + 4.000 C → 1 Epic; 5 Epic + 12.500 C → 1 Legendary.
- **Verlangsamungs-Stacking** multiplikativ: Frost 0.5× · TimeWarp 0.5× · BlackHole 0.3×.
- `OwnedCard` (CardId + Level + Count), `ICardService` verwaltet Deck/Upgrade/Crafting.

---

## 9. PowerUps (12 + Cure) **[REUSE+3D]**

`PowerUpType` — 12 + Cure, level-basierte Freischaltung via `GetUnlockLevel()`. **[3D]** 3D-Pickup-Modelle mit Glow + Discovery-Overlay.

| PowerUp | Effekt | Persistenz | Unlock-Level |
|---------|--------|-----------|--------------|
| BombUp | +1 Bombe (max 10) | permanent | 1 |
| Fire | +1 Reichweite (max 10) | permanent | 1 |
| Speed | +1 SpeedLevel | bis Tod | 1 |
| Kick | Bombe gleitet (Slide 160 px) | bis Tod | 10 |
| Wallpass | durch zerstörbare Blöcke | bis Tod | 15 |
| Mystery | 35 s Unverwundbarkeit | temporär | 15 |
| Cure | heilt Curse sofort | sofort | 15 |
| Skull | Curse (4 Typen, 6 s) | temporär (Strafe) | 20 |
| Detonator | manuelle Zündung | bis Tod | 25 |
| Bombpass | durch eigene Bomben | bis Tod | 25 |
| Flamepass | immun ggü. Explosionen (nicht Gegner) | bis Tod | 35 |
| LineBomb | Bomben-Linie in Blickrichtung | bis Tod | L30 |
| PowerBomb | Range = FireRange + MaxBombs − 1 | bis Tod | L40 |

---

## 10. Combo-System **[REUSE+3D]**

`ComboSystem` (Pure-Domain → 1:1). Kills im 2-s-Fenster steigern den Combo-Zähler. **[3D]** Floating-Text-Pop
+ Slow-Mo + Vignette-Flash. Combos sind Score-/Coin-Multiplikator → aktives, gutes Spiel wird belohnt.

| Combo | Score-Bonus | Besonderheit |
|-------|------------|--------------|
| ×2 | +200 | — |
| ×3 | +500 | — |
| ×4 | +1.000 | — |
| ×5 | +2.000 | MEGA, Slow-Mo 0.8 s |
| ×6 | +4.000 | Window +0.5 s |
| ×7 | +8.000 | — |
| ×8 | +15.000 | — |
| ×9 | +20.000 | — |
| ×10+ | +30.000 | ULTRA, Slow-Mo 1.2 s, Vignette |

- **Window** 2 s, +0.5 s ab ×6. Slow-Mo-Multiplikator 1.5× bei ULTRA. Chain-Kill 1.5× bei 3+ Kills.

---

## 11. Spielmodi (8) **[REUSE]**

> 8 Modi (`IGameMode`-Implementierungen). Alle aktiv gespielt. **[NEU]** sauber als Mode-Plugins
> (kein Bool-Flag-Routing wie im Original).

| Modus | Beschreibung | Belohnung |
|-------|--------------|-----------|
| **Story** | 100 Level in 10 Sektoren, Sterne-Rating | Coins, Sterne, Karten, PowerUp-Discovery |
| **Master-Mode (Reborn / NG+)** | nach L100: Gegner ×1.2 Speed, Typ-Upgrades (Ballom→Minvo, Onil→Pass, …), eigener Persistenz-Pfad, Master-Sterne | Master-Sterne (Normal unberührt) |
| **Quick-Play** | Zufalls-Level | Coins (kein Sterne-Update) |
| **Survival** | Endlos bis Tod | Coins + Highscore |
| **Anomaly-Dives** | Roguelike (siehe §12) | Dive-Cores, Buffs, Karten |
| **Boss-Rush** | Warden-Sequenz, ISO-Wochen-Reset | Boss-Coins + Karten |
| **Daily-Challenge** | tägliches deterministisches Level (Tages-Seed), Streak | Coins + Daily-Token |
| **Daily-Race** | 1 deterministisches Tages-Level weltweit, schnellster Run | Race-Coins + Daily-Race-Liga |

Zusätzlich: **Weekly-Challenge** (5/Woche aus 17er-Pool, Montag-Reset) + **Daily-Missions** (3/Tag, Mitternacht-UTC).

---

## 12. Anomaly-Dives (Roguelike) **[REUSE-Mechanik + STORY-Name]**

> Der Roguelike-Dungeon des Originals, umgewidmet als **Anomaly-Dives**: instabile Grid-Anomalien.
> Mechanik 1:1 (`IDungeonService` + `DungeonSynergyResolver`), **aktiv gespielt**.

- **Floor 1–4 normal, 5 Mini-Warden, 6–9 härter, 10 End-Boss + Truhe, ab 11 +50 % Skalierung.**
- **Node-Map 10×3** (Slay-the-Spire-artig), 5 Raum-Typen (Normal W40/Elite W20/Treasure W15/Challenge W15/Rest W10).
- **8 Floor-Modifikatoren** ab Floor 3 (30 %). **Buff-Pick** auf Floors [2,4,5,7,9,12,14].
- **16 Buffs** (5 Common/5 Rare/2 Epic/4 Legendary), **5 Synergien** (Bombardier/Blitzkrieg/Festung/Midas/Elementar — 2-Buff-Paare).
- **8 permanente Dive-Upgrades** (Währung **Dive-Cores**, getrennt vom Hauptshop).
- **Eintritt:** 1×/Tag gratis, sonst 500 Coins / 3 Gems / Rewarded (1×/Tag), Datum-Tracking gegen Restart-Exploit.
- **Dive-Trennung:** Shop-Upgrades gelten nicht im Dive (nur Base-Stats + Dive-Buffs). Run-Reset bei Tod: Buffs verfallen, Loot bleibt.

---

## 13. Grid-Rankings (async Liga) **[REUSE+STORY-Name]**

> `ILeagueService` (Firebase RTDB), umbenannt zu **Grid-Rankings**. Async-Score-Leaderboard, **kein Echtzeit-PvP**.

| Tier | Sub-Tiers | Punktschwelle |
|------|-----------|---------------|
| Bronze | I/II/III | 0 |
| Silver | I/II/III | 400 |
| Gold | I/II/III | 900 |
| Platinum | I/II/III | 1.600 |
| Diamond | (single) | 2.500 |

- **Perzentil-Promotion/Relegation** (Top 30 % auf / Bottom 20 % ab) am Saisonende, **14-Tage-Saisons**, Saison-Reset.
- **NPC-Backfill** bei < 20 Spielern, **Profanity-Filter** (Unicode-NFKD), Report-Button, Write-Rate-Limit (Server-Timestamp).
- **Daily-Race** (separates Board): 1 deterministisches Tages-Level weltweit, schnellster aktiver Run.
- **Keine P2W-Stats** in Rankings.

---

## 14. Battle Pass & Saisons **[REUSE]**

- **30 Tiers**, **30-Tage-Saison**, XP aus Gameplay/Quests/Achievements, **Free + optional Premium**-Track.
- **10 Themes**, deterministisch aus Saison-Nummer (Classic, Cyberpunk, Halloween, Winter, Summer, Mech,
  Underwater, Sengoku, DiaDeLosMuertos, Steampunk).
- **Keine Zufalls-Belohnungen** — klare Rewards pro Tier. Liga-Saison (14 T) und BP-Saison (30 T) bleiben getrennt.

---

## 15. Achievements (72) **[REUSE]**

- **72 Achievements** in 5 Kategorien (Progress ~24 / Skill ~16 / Mastery ~14 / Combat ~14 / Challenge ~4), JSON-Persistenz.
- Belohnungen: Coins/Gems/Cosmetics/Hero-Unlocks. **Hero-Unlock-Kopplung** (`ach_speed_demon`,
  `ach_block_destroyer`, `ach_jackpot`) — IDs 1:1 erhalten.
- **[NEU]** Achievements für die neuen Sektor-/Warden-/Master-Mode-Bezüge thematisch anpassen (Texte zur Story).

---

## 16. Wirtschaft & Monetarisierung (lean) **[REUSE-Modell]**

> Entscheidung 2026-06-08: **BomberBlast-Modell** — fair, schlank, kein Banner, kein P2W, keine Lootboxen.

### 16.1 Währungen

| Währung | Quelle | Verwendung |
|---------|--------|-----------|
| **Coins** | Level-Score (Combo-getrieben), Win-Bonus | Shop-Upgrades, Card-Crafting, Skins |
| **Gems** | erstes 3-Sterne-Clear (3), IAP, Quests/BP | Deck-Slot, Hero-Direktkauf, Premium-Karten, Dive-Eintritt |
| **Dive-Cores** | Anomaly-Floor-Wins | 8 permanente Dive-Upgrades |

Overflow-Guard (`(long)Balance + amount`-Clamp) für Soft-Währungen.

### 16.2 Shop (12 permanente Upgrades) **[REUSE]**

9 Stat-Upgrades + 3 Bomb-Unlocks (`UpgradeType`, 700–17.000 Coins): StartBombs, StartFire, StartSpeed,
ExtraLives, ScoreMultiplier, TimeBonus, ShieldStart, CoinBonus, PowerUpLuck + Ice/Fire/Sticky-Unlocks.
Zentraler Coin-Sink, bleibt erhalten.

### 16.3 IAP / Werbung (lean)

| Produkt | Inhalt |
|---------|--------|
| **Remove-Ads — 1,99 €** | Interstitials weg, Rewarded bleibt opt-in (Original-Preis) |
| **Gem-Pakete** | optional, mehrere Größen |
| **Battle-Pass-Premium** | Saison-Premium-Track (klare Rewards) |
| **Starter-Pack** | einmaliges faires Einsteiger-Angebot |

**5 Rewarded-Placements (opt-in):** `continue` (Coins ×2), `level_skip`, `power_up` (ab L20), `score_double`,
`revival` (+ Lucky-Spin, Dive-Retry). Hybrid-Cooldown (TickCount64 + persistierte UTC, 60 s).

### 16.4 Ethik

Keine Lootboxen (UK/China-Compliance); Lucky-Spin mit transparenten Drop-Rates + Pity. Keine P2W-Stats in
kompetitiven Modi. Saison-Content auch über Gameplay erreichbar.

---

## 17. Cosmetics & Spieler-Identität **[REUSE+3D]**

- **98 Definitionen:** 32 Trails + 33 Frames + 33 Victories + Spieler-/Hero-Skins (`CustomizationService`).
- **[3D]** Trails als 3D-VFX, Frames als 3D-Overlays, Victories als 3D-Animationen.
- **Quellen:** Battle-Pass, Grid-Ranking-Tiers, Achievements, Event-Drops, Cosmetic-Shop (Gems), Lucky-Spin.
- **[STORY]** Karriere-Cosmetics neu thematisiert: „Reborn-Aura", „Overseer-Slayer", „Champion".

---

## 18. Daily / Weekly / Live-Events **[REUSE]**

- **Daily-Reward** (7-Tage-Login + Comeback > 3 Tage), **3 Daily-Missions** (17er-Pool, Mitternacht-UTC),
  **5 Weekly-Missions** (17er-Pool, Montag-Reset).
- **Wochen-Events** (8, ISO-Wochen-Seed): DoubleXp, DoubleCoins, CardRain, BossWeek, DiveRush, RankingRumble,
  MissionMadness, LuckyWeek (12-Wochen-Vorschau + Server-Override).
- **Saisonale Events:** Halloween, Christmas, NewYear, Summer (Partikel-Overlays).
- **Lucky-Spin:** 9 gewichtete Segmente, 1×/Tag gratis, Pity-Counter, `GetDropRates()`-Disclosure.
- **Rotating-Deals:** 3 tägliche + 1 wöchentliches Angebot, 20–50 % Rabatt.

---

## 19. Onboarding & Tutorial **[REUSE]**

### 19.1 Tutorial-Phasen (3)
- **T1 Movement** — Joystick + Bombe legen + Block zerstören.
- **T2 Bombs** — mehrere Bomben, erster Gegner-Kill.
- **T3 PowerUps** — BombUp/Fire/Speed, Combo-Einführung.

### 19.2 Feature-Unlock-Choreographie (`IFeatureUnlockChoreographer`)
L10 Daily-Challenge · L20 Anomaly-Dives · L30 LineBomb · L40 PowerBomb · L50 Boss-Rush · L60 hero_trait_slot2 ·
L70 boss_modifier_preview · L80 cosmetic_legendary_tier · L90 master_mode_preview · L100 Master-Mode ·
`ach_master_100` Champion-Skin. Queue-basiert, Pref-Flag pro Feature.

### 19.3 Weitere Systeme
Discovery-System (Pause bei Erst-Entdeckung), What's-New-Modal, Re-Engagement-Push (D1/D3/D7), First-Win-Cinematic.

---

## 20. Accessibility **[REUSE]**

`IAccessibilityService`: Colorblind (Off/Deuteranopia/Protanopia/Tritanopia via URP-PostProcessing),
HighContrast (Outline-Pass), UiScale (0.75/1.0/1.25/1.5), Subtitles (Boss-Spawn/Warning/Death/Complete/Ultra/Victory),
Colorblind-Hint-Heuristik, **Reduced-Effects** (Photosensitivity: Flash/Shake gedrosselt), 6 Sprachen.

---

## 21. Audio-Design **[REUSE+3D]**

- **7-Kanal-AudioBus** (Master/Music/Ambient/Sfx/Ui/Voice/Cinematic), Ducking/Boost/Recovery.
- **[3D]** echtes 3D-Spatial-Audio (Unity-Audio) statt 2D-Pan; Reverb-Presets pro Sektor.
- **Anti-Repeat-Pool**, Cinematic-Stinger (Warden-Reveal, Combo-MEGA/ULTRA, Reborn, Victory/Defeat),
  adaptive Music-Boost (Last-Enemy-Drama, ULTRA-Combo), Pitch-Variation.
- **10 sektor-thematische Music-Loops.** Basis CC0/kuratiert; Voice deferred.

---

## 22. UI/UX-Konzept

### 22.1 Haupt-Bildschirme

Bottom-Tab-Navigation (Home / Play / Shop / Profile). Views: MainMenu, PlayHub, LevelSelect, Game, GameOver,
Victory, Shop, GemShop, Deck, Collection, Profile (Statistics/Achievements/Collection), Settings, BattlePass,
Grid-Rankings, Anomaly-Dives, BossRush, DailyChallenge, WeeklyChallenge, LuckySpin, Help, HighScores +
Overlays (DailyReward, Onboarding, WhatsNew, SeasonBanner).

### 22.2 In-Game-HUD
- **Side-Panel rechts** (Landscape): Time / Score / Combo / Lives / Deck.
- **NeonJoystick** (Radius 75 dp, Bomb-Button 52 dp, Detonator 48 dp), Floating- oder Fixed-Modus, Pre-Turn-Buffering.
- **Card-Quickswap** per HUD-Tap.

### 22.3 Overlay-/Modal-System
Zentrale Hit-Test-Aggregate (`IsAnyOverlayOpen`/`IsAnyDialogOpen`) gegen Android-ZIndex-Tap-Durchgriff.
**[3D]** Modal-Übergänge via DOTween; Iris-Wipe bei Level-Start/Complete.

---

## 23. 3D-Visuals & Game Juice **[3D+REUSE]**

- **Übernommene Juice-Patterns (in 3D):** Floating-Text, Currency-Pulse, Iris-Wipe, Slow-Mo (Combo ×4+/letzter Kill),
  Hit-Pause (Kill 50 ms/Death 100 ms), Squash & Stretch, prozedurale Walk-Animation, Boss-Banner, Confetti,
  saisonale Partikel, Trauma-Screen-Shake, Vignette-Flash (ULTRA/Damage), i-Frame-Visualisierung,
  Anticipation-Frames, Outline-Pass, First-Win-/Warden-Reveal-/Victory-Cinematic.
- **[3D]** Dynamische Beleuchtung (URP Forward+), Schatten, emissive Neon-Materialien, Bloom (Ultra-Tier).
- **[3D]** VFX-Graph-Explosionen pro Bomben-Typ (GPU-Partikel statt SkiaSharp-CPU), Shader-Graph
  (Glow/Dissolve/Hologramm/Outline/Liquid-Felder), Cinemachine (Top-Down + Neigung), Timeline-Cutscenes.
- **Performance-Adaption:** Hardware-Tier (Low/Medium/High/Ultra), Partikel-Caps 300/800/1200/1500, Bloom nur
  Ultra, Reverb ab High, adaptives Frame-Skipping, LOD, Object-Pooling.

---

## 24. Multiplayer — nicht Teil von v0.5

**Reiner Single-Player.** Kein Echtzeit-Multiplayer, kein Photon/Netcode, kein lokales oder online
PvP/Co-op, kein Clan-Live-System. **Grid-Rankings** und **Daily-Race** sind **asynchrone** Leaderboards
(Score-Submit über Firebase RTDB), kein Live-Match. Ein etwaiger künftiger Multiplayer wäre ein
**separates Projekt** und ist in dieser Codebase nicht vorausgesetzt.

---

## 25. Balancing-Anker (offen, zu tunen)

> Alle Werte in `BalancingConfig`-ScriptableObject — niemals hardcoded (Unity-Anti-Pattern, siehe CLAUDE.md).
> Erste Werte 1:1 aus Original übernehmen, dann gegen 3D-Feel tunen. Konkrete Zahlen → Memory `balancing.md` (Original).

| Größe | Quelle | Hinweis |
|-------|--------|---------|
| Coins pro Level | Level-Score / 3 (Sektor 1: / 2) | Combo-getrieben → Skill-Reward |
| Shop-Preise | 700–17.000 Coins (`UpgradeType`) | zentraler Coin-Sink |
| Master-Mode-Skalierung | ×1.2 Speed + Gegner-Typ-Upgrades | NG+ aus Original |
| Drop-Gewichtung | 57/25/12/6 % | sektor-moduliert |
| Boss-Modifier-Chance | 30 % ab S5 / 60 % ab S10 | deterministisch |

---

## Änderungslog (DESIGN)

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Sci-Fi-Reinvention (Mech-Helden, OmniCorp, PvP-Arena) | Robert Schneider + Claude |
| 2026-05-30 | v0.2/0.3 | Treuer 1:1-3D-Remake (echtes BomberBlast-Design, code-verifiziert) | Robert Schneider + Claude |
| 2026-06-08 | v0.4 | Idle-Game-Experiment (verworfen) | Robert Schneider + Claude |
| 2026-06-08 | **v0.5** | **Modernes 3D-Bomberman: klassisches, AKTIV gespieltes Bomberman in 3D + bewährte Bomberman-Meta-Progression (100 Level, 8 Modi inkl. Master-Mode/Reborn-NG+, Shop, Dives, Liga, Battle-Pass, 72 Achievements, 98 Cosmetics). NEUE Story (Neo-Grid/Overseer/Reborn, Wardens neu benannt). KEIN Idle/AFK/Offline-Income. Content [REUSE] modernisiert (kein striktes 1:1), lean Monetarisierung.** | Robert Schneider + Claude |

---

> **Status:** Game-Design v0.5 — modernes 3D-Bomberman (kein Idle). **Nächster Schritt:** Content-Reuse-Map
> (PARITY) als Port-/Modernisierungs-Checkliste, dann aktiver Vertical-Slice (1 Sektor + 1 Warden) in 3D.
