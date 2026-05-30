# BomberBlast 3D — Master-Plan

> **Status:** Konzept-Phase (Stand 2026-05-30)
> **Arbeitstitel:** BomberBlast (3D-Neuauflage des produktiven BomberBlast)
> **Genre:** 3D-Top-Down Bomberman-Action mit Meta-Progression (Helden, Karten, Liga, Roguelike-Dungeon)
> **Leitprinzip:** **Genau dasselbe Spiel wie das produktive BomberBlast — nur in 3D und in jeder Hinsicht besser.**
> **Setting:** Identisch zum Original (10 thematische Welten + bestehende Welt-Story-Beats, Neon-Arcade-Stil)
> **Plattformen:** Android (primär, wie Original) + iOS + Steam (Windows/macOS/Linux) — NEU ggü. Original
> **Team:** Full Studio (5+ Personen) — siehe [ROADMAP.md](ROADMAP.md#team)
> **Launch-Strategie:** Soft-Launch DACH → EU → Global

Dieses Dokument ist die **Master-Übersicht**. Tiefe in:

| Bereich | Datei |
|---------|-------|
| Game-Design (Helden, Welten, Karten, Modi, Liga, Dungeon, Live-Service — 1:1 zum Original) | [DESIGN.md](DESIGN.md) |
| Tech-Architektur (Stack, Asmdefs, Determinismus, Save-Migration, Performance, optionaler Netcode) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Produktion (Roadmap, Team, Marketing, Compliance, Risiken) | [ROADMAP.md](ROADMAP.md) |
| Code-Conventions, bekannte Stolperfallen | [CLAUDE.md](CLAUDE.md) |
| KI-Asset-Pipeline (3D-Meshes + PBR-Texturen) | [ASSETS_AI.md](ASSETS_AI.md) |
| First-Time-Setup für Entwickler (folgt nach Projekt-Anlage) | SETUP.md |
| Cloud-Functions-Server-Doku (folgt) | Server/SERVEROPS.md |

> **Quelle der Wahrheit für alles "vom Original":** der produktive Code unter
> `F:/Meine_Apps_Ava/src/Apps/BomberBlast/BomberBlast.Shared/` und dessen granulare CLAUDE.md-Dateien.
> Jede Zahl/Mechanik in diesem Plan ist gegen den Code verifiziert (Stand 2026-05-30).

---

## Inhaltsverzeichnis

1. [Vision & Pitch](#1-vision--pitch)
2. [Zielgruppe & Personas](#2-zielgruppe--personas)
3. [Strategische Entscheidungen](#3-strategische-entscheidungen)
4. [Erfolgs-KPIs](#4-erfolgs-kpis)
5. [Was bleibt vom Original? (Nahezu alles — 1:1)](#5-was-bleibt-vom-original-nahezu-alles-11)
6. [Was ändert sich? (Nur Engine, Darstellung, Plattform, Plus-Features)](#6-was-ändert-sich-nur-engine-darstellung-plattform-plus-features)
7. [USPs (Unique Selling Points)](#7-usps-unique-selling-points)
8. [High-Level-Roadmap](#8-high-level-roadmap)
9. [Risiko-Summary](#9-risiko-summary)
10. [Nächste konkrete Schritte](#10-nächste-konkrete-schritte)

---

## 1. Vision & Pitch

### 1.1 Elevator-Pitch

> **BomberBlast** wird in Unity 6 als **vollwertiger 3D-Top-Down-Remake des bestehenden,
> produktiven BomberBlast** neu aufgelegt. Es ist **dasselbe Spiel** — dieselben 5 Helden,
> dieselben 12 Gegner-Typen, dieselben 5 Bosse, dasselbe 15×10-Grid, dieselben 100 Story-Level
> in 10 Welten, derselbe Roguelike-Dungeon, dieselbe Liga, derselbe Battle Pass, dieselbe Karten-
> und Wirtschafts-Logik — aber gerendert in echtem 3D mit URP, dynamischer Beleuchtung, VFX Graph
> und kuratiertem Audio. Das bewährte Game-Design (Combo-System, Skull-Curses, Master-Mode,
> Welt-Story-Beats) bleibt **unangetastet**; verbessert werden Engine, Optik, Audio, Plattform-
> Reichweite und — als echtes Plus — optionaler Multiplayer.

**In einem Satz:** *Das BomberBlast, das Spieler heute lieben — in 3D, schöner, auf mehr Plattformen,
mit optionalem Mehrspieler-Modus.*

### 1.2 Brand-Identität (unverändert zum Original)

Das Original hat eine etablierte **Neon-Arcade-Identität**. Diese bleibt erhalten — sie ist Teil
des Markenkerns, nicht verhandelbar.

| Aspekt | Wert (wie Original) |
|--------|---------------------|
| **Primärfarbe** | Neon-Orange **#FF6B35** (Markenfarbe, `AppPalette.axaml`) |
| **Akzentfarbe** | Cyan **#22D3EE** + Gold-Trail **#FFDD33** |
| **Tonalität** | Energetisch, Arcade, "Game Juice" — Nostalgie an SNES-Bomberman |
| **Visual-Sprache** | Neon-Arcade: oktagonale Formen, Glow, scharfe Kanten, zwei Visual-Styles (Classic HD + Neon/Cyberpunk) — **jetzt in 3D umgesetzt** |
| **Audio-Sprache** | Arcade-SFX (Kenney-CC0-Basis), 10 welt-thematische Loops, adaptive Layer — **jetzt kuratiert/aufgewertet** |
| **Setting** | Die 10 bestehenden Welt-Themes + die bestehenden Welt-Story-Beats (`IWorldStoryService`: 10 Intros, 9 Outros, Cliffhanger). Keine neue Mythologie. |

> **Bewusste Abkehr von der früheren Plan-Version (v0.2):** Die dort entworfene Sci-Fi-Neuerfindung
> (OmniCorp/Director Vex/Mech-Bomber/8 neue Helden/PvP-Arena-Fokus) wird **verworfen**. Sie war ein
> anderes Spiel. Diese Version ist ein treuer Remake des existierenden BomberBlast.

---

## 2. Zielgruppe & Personas

Die Kern-Zielgruppe bleibt die des bestehenden BomberBlast, erweitert um die neuen Plattformen.

**Persona A: "Der nostalgische Single-Player-Fan" (Kern, ~50 %)**
- Alter: 28-45. Spielte Bomberman in der Kindheit (SNES/NES/PS1).
- Will das BomberBlast-Erlebnis (Story-Level, Sterne sammeln, Dungeon-Runs, Liga) — jetzt schöner.
- Akzeptiert das bestehende Modell: kostenlos mit Rewarded-Ads, 1,99 EUR Remove-Ads, optionale Cosmetics.
- Spielt 10-30 Min pro Session, mehrmals pro Woche.

**Persona B: "Der Casual-Mobile-Gamer" (~30 %)**
- Alter: 18-40. Kurze Sessions (Daily-Challenge, Daily-Race, Quick-Play, Lucky-Spin).
- Free-to-Play, kauft selten. Liga + Battle Pass als Bindung.

**Persona C: "Der Completionist/Sammler" (~15 %)**
- Will alle 72 Achievements, alle Karten Lv-Max, alle Cosmetics, Master-Mode-Sterne, Collection-Album.

**Persona D (NEU durch Plattform-Erweiterung): "Der PC/Cross-Save-Spieler" (~5 %)**
- Spielt mobil unterwegs, am PC (Steam) zu Hause — ein Account, Cross-Save.
- Interessiert am optionalen Co-op/Versus-Multiplayer.

---

## 3. Strategische Entscheidungen

Diese Weichen bilden das Fundament des Remakes:

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Grundprinzip | **Treuer Remake:** dasselbe Spiel wie das produktive BomberBlast, nur 3D + besser. Keine Inhalts-Neuerfindung. |
| 2 | Engine | **Unity 6 + URP** (statt Avalonia 12 + SkiaSharp). 3D-Top-Down statt 2D-prozedural. |
| 3 | Content-Umfang zum Launch | **100 % Feature-Parität** mit dem Original (Helden, Gegner, Bosse, Karten, Modi, Liga, Dungeon, BattlePass, Achievements, Wirtschaft, Cosmetics). |
| 4 | Setting/Helden | **Identisch:** 10 bestehende Welt-Themes + Welt-Story-Beats; 5 bestehende Helden. |
| 5 | Plattformen | **Erweitert:** Android (wie heute) **+ iOS + Steam** mit Cross-Save. |
| 6 | Monetization | **Wie Original:** kostenlos + Rewarded-Ads, 1,99 EUR Remove-Ads, Gems, Battle Pass, Cosmetics, bestehende IAP-Palette. **Keine** Pay-to-Win-Stats, **keine** Lootboxen. |
| 7 | Multiplayer | **Optionales Plus:** echte Integration der vorhandenen MP-Foundation (Local-Coop/Versus, später Online via Photon). Single-Player bleibt das Herzstück. |
| 8 | Visueller Anspruch | **AAA-Mobile-Optik:** 3D-Modelle, dynamische Beleuchtung, VFX Graph, Shader — der "besser"-Teil. |
| 9 | Performance-Target | **60 FPS High-End, 30 FPS Low-End** mit Hardware-Tier-Skalierung (übernimmt das `HardwareTier`-Konzept des Originals). |
| 10 | Audio | **Aufgewertet:** kuratierte/eigene Loops + adaptive Layer; optional Voice (deferred, wie im Original abgewählt). |
| 11 | Launch-Region | **Soft-Launch DACH → EU → Global.** |

---

## 4. Erfolgs-KPIs

> Gemessen über drei Saisons (~6 Monate post-Launch). Erfolg = Retention + Monetization-Konsistenz,
> nicht Tag-1-DAU. Da es ein Remake einer bestehenden Live-App ist, dient deren Telemetrie als Baseline.

### 4.1 Akquisitions-KPIs (Launch-Window, erste 90 Tage)

| KPI | Soft-Launch DACH (W 1-8) | EU-Launch (W 9-16) | Global (W 17-26) |
|-----|--------------------------|---------------------|-------------------|
| Downloads | 50k | 500k | 3-5M |
| D1-Retention | ≥ 35 % | ≥ 30 % | ≥ 25 % |
| D7-Retention | ≥ 15 % | ≥ 12 % | ≥ 10 % |
| D30-Retention | ≥ 8 % | ≥ 6 % | ≥ 5 % |
| Crash-Free-Users | ≥ 99 % | ≥ 99.3 % | ≥ 99.5 % |
| App-Store-Rating | ≥ 4.3 ⭐ | ≥ 4.4 ⭐ | ≥ 4.5 ⭐ |

### 4.2 Engagement-KPIs

| KPI | Ziel |
|-----|------|
| Sessions pro DAU | ≥ 3.5 |
| Session-Länge median | 8-12 Min |
| Tutorial-Completion (T1-T3) | ≥ 85 % |
| Story-Welt-2-Erreichung (D7) | ≥ 60 % |
| Dungeon-Run-Teilnahme (Spieler ≥ L20) | ≥ 40 % |
| Liga-Teilnahme (MAU) | ≥ 30 % |
| Erste-Saison-BP-Tier-30-Reach | ≥ 30 % der Premium-Käufer |

### 4.3 Monetarisierungs-KPIs

| KPI | Ziel |
|-----|------|
| ARPDAU | 0.10-0.20 EUR (Casual-F2P, werbegestützt — wie Original-Modell) |
| Remove-Ads-Conversion (1,99 EUR) | ≥ 3 % |
| Battle-Pass-Premium-Conversion (pro Saison) | ≥ 8 % der MAU |
| Rewarded-Ad-Opt-In-Rate | ≥ 40 % der Sessions |
| ARPPU (pro Saison) | 8-15 EUR |

### 4.4 Technische KPIs

| KPI | Ziel |
|-----|------|
| Average Frame-Rate (High-End) | 60 FPS (± 2) |
| Average Frame-Rate (Low-End, z.B. Galaxy A50) | 30 FPS (± 3) |
| App-Größe (Android-AAB) | < 250 MB |
| App-Größe nach Install (mit Addressables) | < 800 MB |
| Cloud-Save-Sync-Success | ≥ 99.5 % |
| Save-Migration alt→neu Erfolgsrate | ≥ 99 % (siehe ARCHITECTURE: Legacy-Save-Import) |

---

## 5. Was bleibt vom Original? (Nahezu alles — 1:1)

> **Grundsatz:** Game-Design, Content und Balancing des produktiven BomberBlast werden **vollständig
> übernommen**. Die folgenden Inhalte/Systeme sind 1:1 nachzubauen — Details + exakte Werte in
> [DESIGN.md](DESIGN.md). Alle Zahlen hier sind gegen den Code verifiziert (Stand 2026-05-30).

### 5.1 Content (identisch)

- **Spielfeld:** 15×10-Grid (`GameGrid.cs`), `CellType` Empty/Indestructible/Destructible/Exit.
- **5 Helden:** Default, SpeedySam, BrickBoris, TwinTina, LuckyLola — mit ihren exakten Start-Stats,
  Multiplikatoren und `HeroTrait` (QuickPocket/DemolitionExpert/DoubleDetonation/LuckyDrops) und
  Unlock-Bedingungen (`HeroDefinition.cs`).
- **12 Gegner-Typen:** 8 klassische (Ballom, Onil, Doll, Minvo, Kondoria, Ovapi, Pass, Pontan) +
  Tanker, Ghost, Splitter, Mimic — plus Elite-Flag (1.2× Speed, 2× HP, 3× Punkte) (`EnemyType.cs`).
- **5 Bosse:** StoneGolem, IceDragon, FireDemon, ShadowMaster, FinalBoss — jedes 10. Level, Boss-Typ
  rotiert alle 2 Welten, Duo-Encounter Welt 9 (FinalBoss + ShadowMaster) und Welt 10 (2× FinalBoss).
  8 Boss-Modifier (`BossModifier.cs`).
- **12 PowerUps:** BombUp, Fire, Speed, Wallpass, Detonator, Bombpass, Flamepass, Mystery (35 s
  Unverwundbarkeit), Kick, LineBomb, PowerBomb, Skull — plus Cure (Curse-Heilung). Level-basierte
  Freischaltung via `GetUnlockLevel()` (`PowerUpType.cs`).
- **14 Bomben-Typen / 13 Karten:** 3 Shop-Bomben (Ice/Fire/Sticky) + 10 Karten (Smoke, Lightning,
  Gravity, Poison, TimeWarp, Mirror, Vortex, Phantom, Nova, BlackHole) + Standard-Bombe. Rarities
  exakt aus `CardCatalog.cs`. Verlangsamungs-Stacking multiplikativ.
- **10 Welten × 10 Level = 100 Story-Level** + **Master-Mode (Reborn)** nach L100. 12 Layout-Typen
  (`Level.cs`), 8-Layout-Pool pro Welt, 5 Mutatoren ab Welt 6 (`LevelLayoutGenerator.cs`).
- **Welt-Story-Beats:** 10 Welt-Intros + 9 Welt-Outros (Cliffhanger), one-shot, 6 Sprachen
  (`IWorldStoryService`). **Das Original hat bereits eine Story — sie wird übernommen und in 3D inszeniert.**
- **Roguelike-Dungeon:** 16 Buffs (5 Common/5 Rare/2 Epic/4 Legendary), 5 Synergien (Bombardier,
  Blitzkrieg, Festung, Midas, Elementar — je 2-Buff-Paare), 8 Floor-Modifier (ab Floor 3, 30 %),
  5 Raum-Typen (Normal/Elite/Treasure/Challenge/Rest), 10×3-Node-Map, 8 permanente Dungeon-Upgrades
  (DungeonCoins).
- **Cosmetics:** 32 Trails + 33 Frames + 33 Victories (= 98) + Spieler-Skins (`CustomizationService`).

### 5.2 Systeme & Logik (direkt portierbar — Pure-Domain-Code)

- **Combo-System:** Kills im 2-s-Fenster, ×2…×10+, Score-Boni, MEGA/ULTRA, Slow-Mo, Window-Extend (`ComboSystem.cs`).
- **Liga:** 5 Tiers (Bronze→Diamond) × 3 Sub-Tiers (I/II/III; Diamond single), Punktschwellen
  0/400/900/1600/2500, **perzentil-basierte** Promotion/Relegation (Top 30 % / Bottom 20 %) am
  Saisonende, 14-Tage-Saisons, NPC-Backfill bei < 20 Spielern, Daily-Race-Leaderboard (Tages-Seed),
  Profanity-Filter (Unicode-NFKD) (`LeagueService.cs`).
- **Battle Pass:** 30 Tiers, 30-Tage-Saison, XP-basiert, Free/Premium-Track, 10 Themes
  (deterministisch aus Saison-Nummer) (`BattlePassService.cs`).
- **Achievements:** **72 Achievements** in 5 Kategorien (Progress/Mastery/Combat/Skill/Challenge),
  JSON-Persistenz (`AchievementService.cs`).
- **Wirtschaft:** Coins + Gems + DungeonCoins. **9 permanente Shop-Upgrades** (`UpgradeType.cs`,
  Preise 700-17.000 Coins), Card-Crafting (Coin-Sink), Coin/Gem-**Overflow-Guard**
  (`(long)Balance + amount` Clamp). Dungeon-Trennung (Shop-Upgrades gelten nicht im Dungeon).
- **Live-Service:** Daily-Reward (7-Tage + Comeback), 3 Daily-Missions (14er-Pool), 5 Weekly-Missions
  (14er-Pool), 8 Wochen-Events (deterministisch via ISO-Wochen-Seed), saisonale Events
  (Halloween/Christmas/NewYear/Summer), Lucky-Spin (9 Segmente, Pity-Counter, Drop-Rate-Disclosure),
  Rotating-Deals, Starter-Pack, First-Purchase-×2.
- **Determinismus-Bausteine:** `DeterministicRandom` (xoshiro256+, integer-bit-stabil), `ReplayCapture`
  (1 Byte/Tick), `FixedTimestepRunner` (60 Hz), `IRngProvider` (DI). **Wichtig:** Im Original sind das
  **isolierte Bausteine, NICHT in den Game-Loop integriert** (Live-Pfad nutzt `System.Random`). Die
  Integration ist Neu-Arbeit, kein reiner Port (siehe §6 + ARCHITECTURE Determinismus).
- **Pathfinding:** A*-Pathfinding (Object-Pooled PriorityQueue), BFS-Safe-Cell-Finder, Danger-Zone-Map,
  Kettenreaktions-Erkennung, AStarBudgetPerFrame.
- **Anti-Cheat-Patterns:** Hybridtimer (TickCount64 + persistierte UTC) in CoinService/RetentionService/
  BattlePassService/RewardedAdCooldownTracker; Overflow-Guards; Firebase-Server-Rules mit ServerValue-Timestamp.
- **Persistenz/DSGVO:** Cloud-Save (Local-First, 35 Keys, `CloudSaveSchemaMigrator` V1→V3), Account-Delete
  (Art. 17), Data-Export (Art. 20), `PersistenceHealth`-Corruption-Schutz.
- **Clan-System:** `FirebaseClanService` ist im Original **voll integriert** (Create/Join/Chat mit
  Rate-Limit, ISO-Leaderboard, deployte RTDB-Rules) — wird übernommen.

### 5.3 Accessibility, Audio, Onboarding (übernommen)

- **Accessibility:** Colorblind (Off/Deuteranopia/Protanopia/Tritanopia via ColorMatrix), HighContrast,
  UiScale (0.75/1.0/1.25/1.5), Subtitles, Colorblind-Hint-Heuristik, Reduced-Effects (`IAccessibilityService`).
- **Audio:** 7-Kanal-AudioBus, Spatial-Pan, Anti-Repeat-Pool, Cinematic-Stinger, adaptive Music-Boost.
  Basis Kenney CC0 — in der Neuauflage **aufgewertet** (siehe §6).
- **Tutorial + Onboarding:** T1 Movement / T2 Bombs / T3 PowerUps (3 Phasen), Feature-Unlock-Choreographie
  (L10 DailyChallenge, L20 Dungeon, L30 LineBomb, L40 PowerBomb, L50 BossRush, L100 MasterMode),
  Discovery-System, What's-New, Re-Engagement-Push.

---

## 6. Was ändert sich? (Nur Engine, Darstellung, Plattform, Plus-Features)

> Alles unter §5 bleibt inhaltlich gleich. Was sich ändert, ist **wie** es läuft und aussieht — plus
> echte Erweiterungen, die klar als **NEU/besser** markiert sind.

| Achse | Alt (Avalonia/SkiaSharp) | Neu (Unity 6) | Art der Änderung |
|-------|--------------------------|---------------|------------------|
| **Engine** | Avalonia 12 + SkiaSharp CPU | Unity 6 + URP 17 + GPU | Technik |
| **Renderer** | 2D-Top-Down prozedural (Code-only) | **3D-Top-Down** mit URP, Lighting, Shadows, VFX Graph, Shader Graph | besser (Optik) |
| **Visual-Styles** | Classic HD + Neon (2D-SkiaSharp) | Dieselben zwei Style-Welten, jetzt als 3D-Material-Sets | besser (Optik) |
| **Audio** | Kenney CC0 + Android SoundPool | Kuratierte/aufgewertete Loops + adaptive Layer (FMOD optional); Voice weiterhin deferred | besser |
| **Plattform** | Android only | Android **+ iOS + Steam (Win/macOS/Linux)** | NEU (Reichweite) |
| **Persistenz** | sqlite-net-pcl (Highscores) + Firebase-RTDB Cloud-Save (`bomberblast-league`) | Firebase RTDB (`bomberblast-arena`) als Source-of-Truth + JSON-Fallback; **Legacy-Save-Import** alt→neu | Technik + Migration |
| **Determinismus** | Bausteine vorhanden, NICHT integriert; Live-Pfad nutzt `System.Random` | **Integriert:** alle Gameplay-Random über `IRngProvider`, Sim/Render-Trennung, 60-Hz-Fixed-Step | NEU (Voraussetzung für Replay/MP) |
| **Multiplayer** | Foundation-only (Enums/Snapshots), nie integriert; Clan async integriert | **Optionales Plus:** Local-Coop/Versus integriert; später Online (Photon Realtime Co-op, optional Fusion Versus) | NEU (Plus) |
| **Tooling** | dotnet build + AppChecker | Unity Cloud Build / GitHub Actions + Addressables | Technik |
| **Tests** | xUnit (~663 Tests) | Unity Test Framework EditMode + PlayMode + Determinismus-Replay-Suite | Technik |
| **Icon-System** | Eigenes Neon-Arcade-System (159 Icons) | Sprite-Atlas/3D-UI-Icons (Standard-Unity-Workflow) | Technik |

**Nicht geändert:** Setting, Welten, Story-Beats, Helden, Gegner, Bosse, Karten, PowerUps, Liga-Formel,
Dungeon-Inhalte, Battle-Pass-Struktur, Achievements, Wirtschaft, Monetization-Modell, Balancing.

---

## 7. USPs (Unique Selling Points)

### 7.1 USP 1: "Dein BomberBlast — jetzt in 3D"
Das vertraute Spiel, komplett in 3D neu gerendert: dynamische Beleuchtung, Schatten, Partikel,
3D-Bomben-Explosionen. Gleiche Mechanik, massiv aufgewertete Optik. Marketing-Hook: Before/After-Vergleich.

### 7.2 USP 2: "100 Level Story + Roguelike-Dungeon + Liga"
Tiefer Solo-Content (10 Welten, Master-Mode, Dungeon-Runs mit 16 Buffs/5 Synergien, 14-Tage-Liga) —
deutlich mehr als generische Bomberman-Klone. Vom Original bewährt, jetzt schöner präsentiert.

### 7.3 USP 3: "Cross-Save Mobile ↔ PC"
Erstmals auf iOS und Steam mit gemeinsamem Account + Cross-Save. Im Bus mobil, abends am PC weiter.
(NEU ggü. dem Android-only-Original.)

### 7.4 USP 4: "Werbe-fair & Pay-to-Win-frei"
Kostenlos spielbar, Rewarded-Ads opt-in, 1,99 EUR Remove-Ads, keine Lootboxen, keine Stat-Käufe —
das faire Modell des Originals bleibt.

### 7.5 USP 5 (NEU, optional): "Spiel mit Freunden"
Local-Coop/Versus zum Launch, Online-Co-op später — als Erweiterung des bewährten Solo-Kerns.

---

## 8. High-Level-Roadmap

> Detail-Sprints in [ROADMAP.md](ROADMAP.md). Hier nur die Phasen-Übersicht. Da das Game-Design
> bereits feststeht (= das Original), liegt der Fokus auf Engine-Aufbau, 3D-Art und faktentreuer Portierung.

| Phase | Zeitrahmen | Hauptziel |
|-------|-----------|-----------|
| **Phase 0** | Monat 1 | Setup: Unity-Skelett, CI, Firebase, Asmdefs. Pure-Domain-Code-Port (Combo/Dungeon/Liga/Determinismus-Bausteine). |
| **Phase 1** | Monat 2-4 | Single-Player-Core: 15×10-Grid, 5 Helden, 14 Bomben-Typen, 12 PowerUps, 12 Gegner, 5 Bosse, 100 Level in 10 Welten, Layouts/Mutatoren, HUD. |
| **Phase 2** | Monat 4-6 | Meta-Layer: Wirtschaft (Coins/Gems/DungeonCoins), 9 Shop-Upgrades, Karten/Deck/Crafting, Battle Pass (30 Tiers), Cloud-Save + Legacy-Import, 72 Achievements, Tutorial. |
| **Phase 3** | Monat 6-8 | Live-Service + Dungeon: Roguelike-Dungeon (16 Buffs/5 Synergien), Liga (5×3 + Daily-Race), Daily/Weekly/Events, Lucky-Spin, Clan, Master-Mode. |
| **Phase 4** | Monat 8-9 | Determinismus-Integration + Multiplayer-Foundation: Sim/Render-Trennung, `IRngProvider` überall, Local-Coop/Versus. |
| **Phase 5** | Monat 8-10 | Polish: 3D-Art aller Welten/Helden/Gegner/Bosse, VFX Graph, adaptive Music, Welt-Story-Cutscenes in 3D, Cosmetics. |
| **Phase 6** | Monat 10-11 | Closed Beta DACH, Save-Migration-Test mit Alt-Spielern, Performance-Pass (Low-End), LiveOps-Tooling. |
| **Phase 7** | Monat 12 | **Soft-Launch DACH** + Saison 1. |
| **Phase 8** | Monat 13-14 | **EU-Launch** + iOS-Release. |
| **Phase 9** | Monat 15-16 | **Global-Launch** + Steam-Demo. |
| **Phase 10** | Monat 17-18+ | Steam-Full-Launch; optional: Online-Co-op-Rollout, Versus-PvP-Erweiterung. |

**Realistischer Launch im 12. Monat, Global Q3 2027.** (Online-PvP ist ein optionales Post-Launch-Plus,
nicht launch-kritisch.)

---

## 9. Risiko-Summary

> Vollständiges Register in [ROADMAP.md](ROADMAP.md#risiken). Top-5:

| # | Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|--------|--------------------|--------|------------|
| 1 | **Save-Migration alt→neu** verliert Spielstände echter Spieler | Mittel | Hoch | Feld-für-Feld-Mapping der 35 Keys, UID-Bridging `bomberblast-league`→`bomberblast-arena`, ausgiebiger Test mit Alt-Accounts vor Launch (siehe ARCHITECTURE: Legacy-Save-Import) |
| 2 | **Float-Determinismus** für Replay/Online-MP nicht bit-stabil (IL2CPP/ARM64 ↔ Server) | Hoch | Mittel | Determinismus-Mandat: Fixed-Point/Quantisierung für hash-relevante Zustände; Online-MP ist optional/Post-Launch, daher kein Launch-Blocker; Lockstep statt Rollback als Fallback |
| 3 | **3D-Performance** auf Low-End (30 FPS, 4-Spieler) | Mittel | Hoch | Hardware-Tier-System (aus Original), LOD, VFX-Skalierung, dedizierte Low-End-Tests pro Sprint |
| 4 | **Feature-Parität** unvollständig (Original ist sehr umfangreich: ~117 Services) | Mittel | Hoch | Vollständige Parity-Matrix Original-System → Unity-Äquivalent als Pflicht-Checkliste (inkl. Live-Service-Glue: GameTracking/RotatingDeals/VIP/Push) |
| 5 | **Scope** durch optionalen Multiplayer aufgebläht | Mittel | Mittel | Single-Player + Feature-Parität zuerst; MP strikt als Post-Core-Plus, kein Launch-Gate |

---

## 10. Nächste konkrete Schritte

### Sofort (Woche 1-2)
1. **Stakeholder-Review** dieses Plans → Feedback einarbeiten.
2. **Parity-Matrix** erstellen: jedes Original-System (Services/Models/Core) → Unity-Äquivalent + Port-Status.
3. **Firebase-Projekt** `bomberblast-arena` anlegen (Auth + RTDB + Functions + Storage + Crashlytics) und **Legacy-Save-Import-Pfad** aus `bomberblast-league` spezifizieren.
4. **Unity-Projekt** `src/Apps/BomberBlast.Unity/Unity/` (Unity 6 + URP) anlegen + `.gitignore` + Git-LFS.
5. **Pure-Domain-Code-Port-Sprint** planen: `DeterministicRandom`, `ComboSystem`, `ReplayCapture`, `DungeonSynergyResolver`, `LevelLayoutGenerator`, Liga-Logik zuerst (keine Unity-API → 1:1 portierbar).

### Mittelfrist (Monat 1)
6. **CI/CD** (game-ci/unity-builder, EditMode-Tests + Determinismus-Suite pro PR).
7. **Concept-Art/3D-Sprint** für die 5 bestehenden Helden + Welt-1-Theme (im bestehenden Neon-Arcade-Stil, jetzt 3D).
8. **Boot-Scene** mit VContainer + Splash + Anonymous Auth.
9. **DESIGN.md / ARCHITECTURE.md** mit Feedback finalisieren.

### Langfrist (Monat 2-12)
Folge [ROADMAP.md](ROADMAP.md).

---

## Änderungslog

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-Version | Robert Schneider + Claude |
| 2026-05-26 | v0.2 | Restruktur in Sub-Files; Sci-Fi-Reinvention (OmniCorp/Mech/PvP-Arena) | Robert Schneider + Claude |
| 2026-05-30 | **v0.3** | **Grundsatz-Wechsel: treuer 3D-Remake des produktiven BomberBlast (genau dasselbe Spiel, nur 3D + besser). Sci-Fi-Reinvention verworfen. Alle Inhalte/Zahlen gegen den Code verifiziert.** | Robert Schneider + Claude |

> **Status:** Konzept-Phase v0.3 — treuer Remake. Bereit für Parity-Matrix + Setup-Sprint.
