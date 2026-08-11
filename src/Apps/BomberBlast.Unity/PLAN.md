# BomberBlast 3D — Master-Plan

> **Status:** Konzept-Phase v0.6 (Stand 2026-06-14)
> **Arbeitstitel:** BomberBlast: Reborn (volumetrisches 3D-Demolition-Action)
> **Genre:** **Volumetrisches 3D-Action-Spiel mit Bomberman-DNA** — freie Bewegung in vertikalen,
> zerstörbaren Arenen, **physikalische Bomben**, echte **3D-Kettenexplosionen**, mit tiefer
> Meta-Progression (Sektoren/Level, Helden, Karten, Liga, Roguelike-Dives, Master-Mode/NG+).
> **Immer aktiv selbst gespielt.**
> **Ausdrücklich:** **KEIN Idle-Game. KEIN AFK/Auto-Battle. KEIN Offline-Income.** Es spielt nie eine KI
> für dich, und es gibt keinen passiven Fortschritt — Fortschritt entsteht **nur durch aktives Spielen**.
> **Mutige Neuerfindung:** Der Bomberman-Kern (Sprengen, Ketten, räumliches Risiko, PowerUps, Combos)
> bleibt die DNA — aber Bewegung, Raum und Explosionen werden **echt dreidimensional** neu gedacht
> (freie Bewegung statt Grid-Lock, volumetrische Blast-Formen, ebenenübergreifende Ketten,
> zerstörbare Architektur). **Kein 1:1-Remake.**
> **Setting:** **Neue Story** (Neo-Grid / Overseer / Reborn-Core) im bestehenden Neon-Arcade-Look,
> jetzt als volumetrisches 3D-Konstrukt.
> **Plattformen:** Android (primär, wie das Original) + Desktop (Test). Kein iOS/Steam geplant.
> **Team:** Solo-Indie + KI-Assistenz (right-sized).
> **Monetarisierung:** **Lean / fair** wie das 2D-Original: kostenlos + Rewarded-Ads (kein Banner),
> 1,99 € Remove-Ads, optional VIP-Abo + zwei Battle-Pass-Stufen (Premium 4,99 € / Plus 19,99 €).
> Keine Lootboxen, kein Pay-to-Win.

Dieses Dokument ist die **Master-Übersicht**. Tiefe in:

| Bereich | Datei |
|---------|-------|
| Game-Design (Story, Gameplay, Helden, Bomben, Gegner, Bosse, Modi, Progression, Live-Service) | [DESIGN.md](DESIGN.md) |
| Content-Wiederverwendung (welches Original-System wird wie übernommen/umgewidmet) | [PARITY.md](PARITY.md) |
| Tech-Architektur (Unity-Stack, Asmdefs, Determinismus, Physik/Voxel, Save, Performance) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Produktion (Roadmap, Marketing, Compliance, Risiken) | [ROADMAP.md](ROADMAP.md) |
| Unity-Code-Conventions, bekannte Stolperfallen | [CLAUDE.md](CLAUDE.md) |
| KI-Asset-Pipeline (3D-Meshes + PBR-Texturen + zerstörbare Umgebung) | [ASSETS_AI.md](ASSETS_AI.md) |

> **Richtungs-Historie:** v0.2 = Sci-Fi-Reinvention (verworfen) · v0.3 = treuer 1:1-Remake (abgelöst) ·
> v0.4 = kurzzeitig Idle-Game-Experiment (**verworfen — BomberBlast ist kein Idle-Game**) ·
> v0.5 = klassisches Grid-Bomberman in 3D (Top-Down, flaches Grid) ·
> **v0.6 (aktuell) = volumetrische 3D-Neuerfindung: freie Bewegung in vertikalen, zerstörbaren Arenen,
> physikalische Bomben, 3D-Kettenexplosionen — mit der bewährten Bomberman-Meta-Progression und neuer
> Story.** Der Bomberman-Kern (Sprengen/Ketten/Risiko/PowerUps/Combos) ist die DNA; das Original liefert
> **Meta-Progression & Live-Service-Code als Fundament** (wiederverwendet), während Bewegung/Raum/
> Combat **neu in 3D** gebaut werden. Kein 1:1-Mandat.

---

## Inhaltsverzeichnis

1. [Vision & Pitch](#1-vision--pitch)
2. [Was es ist (und was nicht)](#2-was-es-ist-und-was-nicht)
3. [Story-Pitch (neu)](#3-story-pitch-neu)
4. [Zielgruppe & Personas](#4-zielgruppe--personas)
5. [Strategische Entscheidungen](#5-strategische-entscheidungen)
6. [Was bleibt vom Original / Was wird neu](#6-was-bleibt-vom-original--was-wird-neu)
7. [Core-Loop & Modi](#7-core-loop--modi)
8. [Monetarisierung (lean)](#8-monetarisierung-lean)
9. [USPs](#9-usps)
10. [Erfolgs-KPIs](#10-erfolgs-kpis)
11. [High-Level-Roadmap](#11-high-level-roadmap)
12. [Risiko-Summary](#12-risiko-summary)
13. [Nächste konkrete Schritte](#13-nächste-konkrete-schritte)
14. [Offene Design-Fragen](#14-offene-design-fragen)

---

## 1. Vision & Pitch

### 1.1 Elevator-Pitch

> **BomberBlast: Reborn** ist ein **volumetrisches 3D-Action-Spiel mit Bomberman-DNA**, das man **immer
> selbst aktiv spielt**. Du bewegst dich **frei** durch vertikale, mehrstöckige Arenen — laufen, dashen,
> springen, an Kanten ziehen — **legst, wirfst und rollst physikalische Bomben**, die abprallen, Rampen
> hinunterrollen und **zwischen Ebenen fallen**. Explosionen sind **echte 3D-Volumen** (Kugel, Säule,
> gerichteter Kegel, Cluster, Schockwelle), zünden in 3D **Kettenreaktionen** und lassen **zerstörbare
> Architektur kollabieren**. Drumherum liegt die **bewährte Bomberman-Meta-Progression**: 100 Story-Level
> in 10 Sektoren mit Sterne-Wertung, permanente Shop-Upgrades, Karten-/Bomben-Sammlung, Helden, ein
> Roguelike-Modus (Anomaly-Dives), Liga (Grid-Rankings), Battle-Pass — und **Master-Mode (Reborn)** als
> New-Game-Plus. **Kein Idle, kein AFK, kein Offline-Farming.**

**In einem Satz:** *Bomberman-DNA — Sprengen, Ketten, räumliches Risiko, Combos — als volumetrisches,
frei bewegtes 3D-Action-Spiel mit neuer Story und tiefer Meta-Progression. Voll aktiv gespielt.*

### 1.2 Brand-Identität (Look bleibt, Raum + Story neu)

Der **Neon-Arcade-Look bleibt** Markenkern — neu sind **echte Dreidimensionalität** (freie Bewegung,
vertikale Arenen, volumetrische Blasts) und die **Story** (bewusste Entscheidung).

| Aspekt | Wert |
|--------|------|
| **Primärfarbe** | Neon-Orange **#FF6B35** |
| **Akzente** | Cyan **#22D3EE** + Gold-Trail **#FFDD33** |
| **Tonalität** | Energetisch, Arcade, "Game Juice"; leichter Cyber-Story-Rahmen (kein Grimdark) |
| **Visual-Sprache** | Neon-Arcade in echtem 3D: oktagonale Formen, Glow, emissive Materialien, Bloom, volumetrische Explosionen, kollabierende Architektur |
| **Anti-Style** | Realismus/Foto-Texturen, düstere Tristesse, **Idle-/AFK-Selbstläufer**, aggressive Whale-Monetarisierung |

---

## 2. Was es ist (und was nicht)

> Wegen der Richtungs-Historie hier explizit zur Vermeidung von Missverständnissen:

**Es IST:**
- Ein **volumetrisches 3D-Action-Spiel mit Bomberman-DNA**, klassisch **aktiv** gespielt: freie Bewegung,
  physikalische Bomben, 3D-Ketten, PowerUps, Combos, Bosse.
- **Mutig neu gedacht in 3D:** freie Bewegung statt Grid-Lock, Vertikalität (Ebenen/Rampen/Lifts/Fall),
  volumetrische Blast-Formen, ebenenübergreifende Kettenreaktionen, zerstörbare Architektur.
- Inhaltsreich: 10 Sektoren × 10 Level, Bosse, Roguelike-Dives, Liga, Battle-Pass, Helden, Karten, Cosmetics.
- Mit **Master-Mode (Reborn)** = New-Game-Plus nach L100 für Wiederspielwert (Feature aus dem Original).
- Mit **neuer Story** und voller 3D-Aufwertung von Raum, Optik und Audio.

**Es ist NICHT:**
- **Kein Idle-/Incremental-Game.** Kein passives Einkommen, keine "Zahlen-gehen-hoch-während-du-wartest"-Mechanik.
- **Kein AFK / Auto-Battle / Auto-Run.** Es spielt **nie** eine KI Level für dich.
- **Kein Offline-Income.** Fortschritt entsteht ausschließlich durch aktives Spielen.
- **Kein striktes 1:1-Remake** und **kein flaches Grid-Bomberman** mehr — der Raum wird echt 3D.
- **Kein Multiplayer.** Reiner Single-Player (Grid-Rankings/Daily-Race sind asynchrone Leaderboards).

---

## 3. Story-Pitch (neu)

> Vollständige Story + Welt → [DESIGN.md §2](DESIGN.md). Hier der Pitch.

**Welt — NEO-GRID:** Unter einer Neon-Megacity liegt **das Grid**: 10 Wartungs-Sektoren als **echtes
volumetrisches 3D-Konstrukt** — mehrstöckige Maschinen-Architektur, gekapert von der außer Kontrolle
geratenen Stadt-KI **OVERSEER**, die das Grid in einen tödlichen, sich selbst wieder aufbauenden,
**vertikalen** Parcours verwandelt hat.

**Held — der Bomber:** Du bist ein frisch aktivierter **Bomber** (augmentierter Abriss-Spezialist).
In Sektor 1 birgst du einen **Reborn-Core** — Overseer-Technik, die einen gefallenen Bomber aus seinen
**"Blast-Daten"** wieder zusammensetzt, jedes Mal **stärker** — und die deine 3D-Mobilität speist
(Dash, Blast-Jump, Reborn-Fähigkeiten).

**Der Reborn (= Master-Mode / NG+):** Sprengst du dich durch alle 10 Sektoren bis zum **Core** des
Overseers und detonierst ihn, **kollabiert das Grid** — und baut sich **härter und anders verschachtelt**
neu auf. Du aber kehrst **Reborn** zurück: stärker, für einen neuen, schwereren Durchlauf. (Bestehender
Master-Mode des Originals, narrativ verankert — **keine** Idle-Prestige-Schleife.)

**Neue Bosse (Sektor-Wardens des Overseers):** Granite Warden · Frostwyrm · Magma Revenant · Null
Phantom · **The Overseer** (Core-Avatar, Finale). Mechanisch bauen sie auf den 5 bewährten Boss-Archetypen
des Originals auf, **neu inszeniert als große 3D-Encounter mit Vertikalität und Arena-Zerstörung**.

---

## 4. Zielgruppe & Personas

**Persona A: "Der Bomberman-Nostalgiker" (Kern, ~45 %)**
- Spielte Bomberman (SNES/NES/PS1); will den aktiven Skill-Kern (Sprengen, Ketten, Sterne, Bosse, Combos)
  — jetzt befreit in echtem 3D mit mehr Bewegungsfreiheit und Wucht.
- Akzeptiert faires F2P: Rewarded-Ads opt-in, 1,99 € Remove-Ads.

**Persona B: "Der Casual-Mobile-Action-Gamer" (~30 %)**
- Kurze aktive Sessions: ein paar Level, Daily-Challenge, Daily-Race, Lucky-Spin. Liga + Battle-Pass als Bindung.

**Persona C: "Der Completionist/Skiller" (~20 %)**
- Will alle Achievements, alle Karten max, alle Cosmetics, 3-Sterne überall, Master-Mode-Sterne,
  Bestzeiten und hohe Style-Ränge.

> (Reiner Single-Player, Android-fokussiert — keine PC-/Cross-Save-/Multiplayer-Persona.)

---

## 5. Strategische Entscheidungen

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Grundprinzip | **Volumetrisches 3D-Action-Spiel mit Bomberman-DNA, aktiv gespielt, mit tiefer Meta-Progression.** Kein Idle, kein 1:1-Remake, kein flaches Grid. |
| 2 | Engine | **Unity 6 + URP** (echtes 3D, freie Bewegung, dynamische Action-Kamera, Cinemachine, Physik). |
| 3 | Bewegung & Raum | **Freie 3D-Bewegung** (Dash/Sprung/Ledge), **vertikale, mehrstöckige Arenen** (Rampen/Lifts/Fall/Void). |
| 4 | Bomben & Blasts | **Physik-Bomben** (legen/werfen/rollen/kicken, fallen zwischen Ebenen) + **volumetrische Blast-Formen** + **3D-Kettenreaktion** + **zerstörbare Architektur**. |
| 5 | Genre-Ausschluss | **Kein Idle/AFK/Auto-Battle, kein Offline-Income, kein passiver Fortschritt, kein Multiplayer.** |
| 6 | Story/Setting | **Neue Story** (Neo-Grid/Overseer/Reborn) als volumetrisches 3D-Konstrukt. Neon-Arcade-**Look bleibt**. |
| 7 | Wiederspielwert | **Master-Mode (Reborn) = NG+** nach L100 (Feature aus Original, narrativ verankert). |
| 8 | Content-Quelle | **Meta-Progression + Live-Service-Code des Originals** als Fundament (wiederverwendet); **Combat/Bewegung/Raum neu in 3D** (kein Grid-Port). |
| 9 | Determinismus | **2-Schichten-Modell:** autoritative deterministische Gameplay-Sim (Occupancy, Fixed-Step, `IRngProvider`, **Replay-Hash bleibt**) **+** kosmetische, nicht-autoritative PhysX-Debris-Schicht. Daily-Race verifiziert über die deterministische Schicht. Begründung → [GDD §12.3](3D_REINVENTION_PLAN.md) / [ARCHITECTURE §13](ARCHITECTURE.md). |
| 10 | Plattformen | **Android primär**, Desktop für Test. Kein iOS/Steam geplant. |
| 11 | Monetarisierung | **Lean (BomberBlast-Modell):** F2P + Rewarded (kein Banner), 1,99 € Remove-Ads, Premium-BP 4,99 € + Plus-BP 19,99 €. Keine Lootboxen, kein P2W. |
| 12 | Performance | **60 FPS High-End, 30 FPS Low-End** mit Hardware-Tier-Skalierung (Physik-/VFX-/Destruktions-Caps pro Tier). |
| 13 | Team-Realität | **Solo-Indie + KI** — Scope getrimmt, **reiner Single-Player**, **Feel-Prototyp vor Content-Breite**. |

---

## 6. Was bleibt vom Original / Was wird neu

### 6.1 Bleibt (wiederverwendet — Fundament)

- **Meta-Progression:** 12 permanente Shop-Upgrades, Karten-/Deck-Sammlung + Crafting, Helden, 72 Achievements,
  Wirtschaft (Coins/Gems), Cosmetics (98).
- **Modi-Gerüst:** Story (100 Level), Master-Mode (Reborn/NG+), Quick-Play, Survival, Roguelike-Dives,
  Boss-Rush, Daily-Challenge, Daily-Race.
- **Live-Service:** Daily-Reward, Daily/Weekly-Missions, Wochen-/Saison-Events, Lucky-Spin, Battle-Pass,
  async-Liga (→ "Grid-Rankings"), Cloud-Save, DSGVO-Pfade.
- **Pure-Domain-Code (1:1 portierbar):** `ComboSystem` (Basis), `DungeonSynergyResolver`, `DeterministicRandom`
  + `IRngProvider` + `FixedTimestepRunner` + `GameStateSnapshot` (Fundament der **autoritativen Schicht A** —
  Replay-Hash bleibt, [GDD §12.3](3D_REINVENTION_PLAN.md)), Anti-Cheat-Hybridtimer, Overflow-Guards,
  Liga-/BattlePass-/Mission-Formeln, Profanity-Filter.
- **Bomben-/Gegner-/Boss-/PowerUp-Konzepte:** 14 Bomben-Typen, 12 PowerUps, 12 Gegner-Typen, 5 Boss-Archetypen
  — als **Design-Vorlage** in 3D neu umgesetzt (Werte/Verhalten als Anker).
- **Accessibility & Audio-Architektur:** Colorblind/HighContrast/UiScale/Subtitles; 7-Kanal-AudioBus, adaptive Music.

### 6.2 Neu

- **Volumetrische 3D-Spielwelt:** freie Bewegung, vertikale/mehrstöckige Arenen (Rampen/Lifts/Fall/Void),
  zerstörbare Architektur (Voxel-/Chunk-Destruktion mit Trümmer-Physik).
- **Physik-Bomben & volumetrische Explosionen:** legen/werfen/rollen/kicken, ebenenübergreifender Fall,
  Blast-Formen (Kugel/Säule/Kegel/Cluster/Schockwelle/Implosion), echte 3D-Kettenreaktion.
- **3D-Bewegungs-Skill:** Dash/Roll (i-Frames), Doppelsprung/Blast-Jump, Ledge-Grab; **Air-Combos,
  Drop-Kills, Environmental-Kills** und ein **Style-Rang-System** (erweitertes Combo).
- **Neue Story** (Neo-Grid/Overseer/Reborn) + neu benannte Bosse, in 3D-Cutscenes (Timeline).
- **3D-Engine/Optik/Audio:** Unity 6 + URP, dynamische Beleuchtung, Schatten, VFX-Graph-Explosionen,
  Shader-Graph, 3D-Spatial-Audio, dynamische Action-Kamera (Cinemachine).

### 6.3 Bewusst gestrichen / nicht enthalten

- **Idle/Incremental-Mechanik, AFK/Auto-Battle/Auto-Run, Offline-Income, passiver Fortschritt** — alles raus.
- **Flaches 15×10-Grid als Sim-Kern** — ersetzt durch **freie 3D-Bewegung** + eine **deterministische
  3D-Occupancy-Repräsentation** (Schicht A, bleibt deterministisch). Physik-Zerstörung läuft in einer
  **kosmetischen, nicht-autoritativen PhysX-Schicht** (Schicht B) — Replay-Hash gilt nur für Schicht A
  ([GDD §12.3](3D_REINVENTION_PLAN.md)).
- **Idle-Meta-Prestige (Singularity/Eternal-Drive aus dem v0.4-Experiment)** — gestrichen; nur Master-Mode/Reborn bleibt.
- **"100 % Feature-Parität" als striktes Mandat** — ersetzt durch "Meta wiederverwenden, Combat/Raum neu in 3D".
- **Online-PvP/Multiplayer, Photon/Netcode, Esports, Cross-Platform-AAA-Anspruch** — komplett gestrichen (Solo-Indie, Single-Player).
- **Whale-Monetarisierung / aggressive IAP** — bleibt lean.

---

## 7. Core-Loop & Modi

### 7.1 Session-Loop (rein aktiv)

```
Öffnen → Sektor/Level wählen → AKTIV spielen (frei bewegen, Bomben legen/werfen/rollen, 3D-Ketten,
       Architektur sprengen, PowerUps, Air-/Environmental-Combos, ggf. Boss)
       → Sterne + Style-Rang + Coins/Karten verdienen → Shop-Upgrades / Deck verbessern
       → nächstes Level / nächster Sektor → … → L100 → Master-Mode (Reborn, NG+)
       → nebenbei: Daily/Weekly, Dives, Liga, Battle-Pass
```

### 7.2 Spielmodi

| Modus | Inhalt |
|-------|--------|
| **Story** | 100 Level in 10 Sektoren, Sterne-Rating, Bosse, Story-Cutscenes |
| **Master-Mode (Reborn / NG+)** | nach L100: härter, Gegner-Upgrades, eigene Master-Sterne |
| **Quick-Play / Survival** | schnelle Action / Endlos bis Tod |
| **Anomaly-Dives (Roguelike)** | Floor-basierte Runs mit Buffs/Synergien, eigene Meta-Upgrades |
| **Boss-Rush** | Boss-Sequenz, wöchentlicher Reset |
| **Daily-Challenge / Daily-Race** | tägliche Seed-Arena (weltweit gleich) / Bestzeit-Wettlauf (Grid-Rankings) |

---

## 8. Monetarisierung (lean)

> Entscheidung: **BomberBlast-Modell** — fair, schlank, kein Banner, kein P2W, keine Lootboxen.
> Details → [DESIGN.md §16](DESIGN.md).

- **Remove-Ads (1,99 €, non-consumable):** wirkt wie im Original als **Premium-Flag** — Rewarded-Belohnungen
  gibt es **ohne Video-Zwang** (IsPremium-Bypass in den Rewarded-Flows) + exklusive Premium-Skins.
  Werbung existiert ausschließlich als opt-in Rewarded (wie im Original — keine Unterbrecher-Werbung).
- **Rewarded-Ads (opt-in):** Continue (Coins verdoppeln), Level-Skip, PowerUp (ab L20), Score-Double,
  Revival, Lucky-Spin-Extra, Dive-Retry. Hybrid-Cooldown.
- **VIP-Abo (`vip_subscription_monthly`, 9,99 €/Monat):** im Original produktiv — wird übernommen,
  Vorteile sinngemäß aus dem Original.
- **Battle-Pass — zwei getrennte Produkte (Entscheidung 2026-06-14):**
  - **Premium-Track (`battle_pass_premium_season`, 4,99 €/Saison):** schaltet den Premium-Belohnungs-Track
    der laufenden Saison frei — klare Rewards pro Tier, **kein Zufall**.
  - **Plus-Paket (`battle_pass_plus_season`, 19,99 €/Saison):** Premium-Track **plus** Komfort obendrauf —
    +25 Tier-Skip beim Kauf, +50 % XP-Multiplier, Bonus-Gems pro Tier-Up (enthält den Premium-Track).
- **Gems** (erspielbar + optionaler IAP): Deck-Slot, Helden-Direktkauf, Premium-Karten, Anomaly-Eintritt.
- **Battle-Pass (Saison):** Free-Track + Premium-Track (via Premium- oder Plus-Kauf) — klare Rewards pro
  Tier, **keine** Zufalls-Boxen.
- **Cosmetics:** Trails/Frames/Victories/Skins über Gems, Battle-Pass, Events.
- **Ethik:** keine Pay-to-Win-Stats in kompetitiven Modi, Saison-Content auch über Gameplay erreichbar,
  transparente Drop-Rates (Lucky-Spin mit Pity).

---

## 9. USPs

1. **"Bomberman, endlich in echtem 3D"** — freie Bewegung, vertikale Arenen, volumetrische Explosionen:
   das vertraute Sprengen, befreit aus dem flachen Gitter.
2. **Physik-Bomben & kollabierende Architektur** — werfen, rollen, ebenenübergreifende Ketten, Strukturen
   einstürzen lassen: taktische Zerstörung als Kern-Spaß.
3. **Tiefer aktiver Content** — 100 Level + Master-Mode + Roguelike-Dives + Liga: viel mehr als generische Klone.
4. **Skill & Style statt Selbstläufer** — Air-Combos, Drop-/Environmental-Kills, Style-Rang belohnen Können.
   **Kein Idle, kein AFK.**
5. **Werbe-fair & P2W-frei** — kostenlos, Rewarded opt-in, 1,99 € Remove-Ads, keine Lootboxen.

---

## 10. Erfolgs-KPIs

### 10.1 Engagement

| KPI | Ziel |
|-----|------|
| Sessions pro DAU | ≥ 3,5 |
| Session-Länge median | 8–12 Min |
| Tutorial-Completion | ≥ 85 % |
| Sektor-2-Erreichung (Anteil der an D7 aktiven Nutzer) | ≥ 60 % |
| Dives-Teilnahme (ab L20) | ≥ 40 % |
| Liga-Teilnahme (MAU) | ≥ 30 % |

### 10.2 Retention

| KPI | Soft-Launch | Skaliert |
|-----|-------------|----------|
| D1 | ≥ 35 % | ≥ 28 % |
| D7 | ≥ 14 % | ≥ 10 % |
| D30 | ≥ 8 % | ≥ 5 % |
| Crash-Free-Users | ≥ 99 % | ≥ 99,5 % |

### 10.3 Monetarisierung (lean)

| KPI | Ziel |
|-----|------|
| ARPDAU | 0,08–0,15 € (werbegestützt, fair) |
| Remove-Ads-Conversion (1,99 €) | ≥ 3 % |
| Rewarded-Opt-In-Rate (Sessions) | ≥ 40 % |
| Battle-Pass (Premium + Plus, pro Saison) | ≥ 6 % MAU |

### 10.4 Technik

| KPI | Ziel |
|-----|------|
| FPS High-End / Low-End (z.B. Galaxy A50) | 60 / 30 |
| App-Größe (AAB, mit Play-Asset-Delivery) | < 250 MB |
| Cloud-Save-Sync-Success | ≥ 99,5 % |

---

## 11. High-Level-Roadmap

> Detail-Sprints → [ROADMAP.md](ROADMAP.md). Right-sized für Solo-Indie + KI. **Feel-Prototyp vor
> Content-Breite** — das 3D-Spielgefühl muss zuerst sitzen.

| Phase | Zeitrahmen | Hauptziel |
|-------|-----------|-----------|
| **Phase 0** | Monat 1 | Setup: Unity-Skelett, URP, VContainer, CI, Asmdefs. Pure-Domain-Port (Combo/RNG/Formeln). |
| **Phase 1 — Feel-Prototyp (Gate)** | Monat 2–4 | **Das volumetrische 3D-Spielgefühl beweisen:** freie Bewegung (Dash/Sprung/Ledge), Physik-Bombe (legen/werfen/rollen/Fall), **eine volumetrische Blast-Form + 3D-Kette**, zerstörbare Arena-Elemente, **ein vertikaler Sektor + Granite Warden**, 3D-Lesbarkeit auf Min-Spec. **Gate: macht Spaß & ist lesbar → erst dann weiter.** |
| **Phase 2** | Monat 4–7 | **Combat-Breite + Meta-Progression:** alle Blast-Formen/Bomben, 12 PowerUps, 12 Gegner, 5 Bosse, 100 Level in 10 Sektoren; Wirtschaft, 12 Shop-Upgrades, Karten/Deck/Crafting, Helden, 72 Achievements, Cloud-Save, Tutorial. |
| **Phase 3** | Monat 7–9 | **Modi & Live-Service:** Master-Mode (Reborn/NG+), Anomaly-Dives, Boss-Rush, Liga (Grid-Rankings), Daily/Weekly/Events, Lucky-Spin, Battle-Pass. |
| **Phase 4** | Monat 9–11 | **3D-Art & Polish:** alle Sektoren/Helden/Gegner/Bosse, Destruktions-Assets, VFX-Graph, Shader, adaptive Music, Story-Cutscenes, Cosmetics, Style-System-Politur. |
| **Phase 5** | Monat 11–13 | **Closed Beta DACH:** Balancing, Low-End-Performance (Physik/Destruktion), Touch-3D-Controls, Tutorial-Funnel, LiveOps-Tooling. |
| **Phase 6** | Monat 14–15 | **Soft-Launch DACH** + Saison 1. |
| **Phase 7** | Monat 16+ | Skalierung (EU/Global), weitere Saisons & Content-Updates. Kein Multiplayer, kein iOS/Steam. |

**Realistischer Soft-Launch ~Monat 14–15** (die volumetrische Neuerfindung + Physik/Destruktion ist
ambitionierter als ein Grid-Port — bewusst mehr Puffer). Reiner Single-Player.

---

## 12. Risiko-Summary

> Vollständiges Register → [ROADMAP.md §8](ROADMAP.md#8-risiko-register). Top-7:

| # | Risiko | Wkt. | Impact | Mitigation |
|---|--------|------|--------|------------|
| 1 | **Scope** (volumetrische Neuerfindung + Physik + Destruktion + voller Content) für Solo zu groß | Hoch | Hoch | **Feel-Prototyp-Gate**, Content-Reuse der Meta maximieren, Single-Player, Polish nach hinten |
| 2 | **3D-Spielgefühl/Controls auf Touch** (freie Bewegung + Zielen + Werfen mit Daumen) | Hoch | Hoch | Prototyp zuerst, Auto-Aim/Soft-Lock, Bewegungs-Assist, frühe Min-Spec-Tests |
| 3 | **Performance Low-End** (Physik + volumetrische VFX + Destruktion + viele Gegner) | Hoch | Hoch | Hardware-Tier, LOD, VFX-/Physik-/Debris-Caps, Object-Pooling, Min-Spec-Test pro Sprint |
| 4 | **3D-Lesbarkeit** (welche Volumina trifft der Blast? Höhe/Ebene?) | Hoch | Hoch | Blast-Preview-Volumen, Through-Wall-Outlines, Höhen-/Schatten-Indikatoren, empirischer A50-Test |
| 5 | **Determinismus-Relaxation → Daily-Race-Anti-Cheat** | Mittel | Mittel | Server-Plausibilität (Score/Time-Sanity, Rate-Limit), Anzeige-Ghosts statt Resim |
| 6 | **Balancing in 3D** (Werte aus 2D übertragen sich nicht 1:1) | Mittel | Mittel | `BalancingConfig`-ScriptableObject, Beta-Telemetrie, Tuning-Loop |
| 7 | **Story/Brand-Kohärenz** (neue Story vs. Neon-Brand) | Niedrig | Mittel | Look bleibt #FF6B35-Neon-Arcade; Story als leichter Cyber-Rahmen |

---

## 13. Nächste konkrete Schritte

### Erledigt
1. **Plan + DESIGN.md reviewt** — Richtung v0.6 (volumetrische Neuerfindung) bestätigt.
2. **Unity-Projekt-Skelett** existiert unter `src/Apps/BomberBlast.Unity/Unity/` (Unity 6 + URP) —
   Setup-Doku in [SETUP.md](SETUP.md), Slice-Plan in [VERTICAL_SLICE.md](VERTICAL_SLICE.md).

### Sofort
3. **Editor-Open-Verifikation** — Projekt in der installierten Unity-6-Version öffnen, sauber kompilieren,
   Paket-Versionen + R3-NuGet-Setup prüfen ([SETUP.md](SETUP.md)).
4. **Feel-Prototyp (Phase 1):** freie 3D-Bewegung + eine Physik-Bombe + eine volumetrische Blast-Form +
   3D-Kette + ein zerstörbares Arena-Element auf dem Gerät — **Spielgefühl zuerst**
   ([VERTICAL_SLICE.md](VERTICAL_SLICE.md)).

### Mittelfrist (Monat 1–4)
5. **CI/CD** (game-ci, EditMode-Tests + Seed-Reproduzierbarkeit-Tests; **kein** bit-exakter Replay-Gate mehr).
6. **Concept-Art/3D-Sprint** für Bomber + Sektor 1 (Neon-Arcade-Stil, 3D) inkl. zerstörbarer Module.

---

## 14. Offene Design-Fragen

1. **Vertikalitäts-Tiefe:** Wie viele Ebenen pro Arena im Schnitt (2–3?) und wie aggressiv Fall-/Lift-Mechaniken?
   — *Vorschlag: 2–3 Ebenen, Fall als Feature, im Prototyp tunen.*
2. **Zerstörungs-Granularität:** echte Voxel-Destruktion vs. vordefinierte zerbrechende Module/Chunks?
   — *Vorschlag: Chunk-/Modul-basiert (Solo-machbar, performant), Voxel-Optik nur kosmetisch.*
3. **Style-Rang-Tiefe:** eigenständiges DMC-artiges Rang-System oder nur erweitertes Combo?
   — *Vorschlag: erweitertes Combo mit Rang-Anzeige, kein eigenes Meta.*
4. **Sektor-Umfang:** exakt 10 × 10 (=100)? — *Vorschlag: 100 wie Original.*
5. **Titel:** "BomberBlast: Reborn"? Alternativen willkommen.

---

## Änderungslog

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-Version | Robert Schneider + Claude |
| 2026-05-26 | v0.2 | Sci-Fi-Reinvention (OmniCorp/Mech/PvP-Arena) | Robert Schneider + Claude |
| 2026-05-30 | v0.3 | Treuer 1:1-3D-Remake (Sci-Fi verworfen) | Robert Schneider + Claude |
| 2026-06-08 | v0.4 | Idle-Game-Experiment (verworfen) | Robert Schneider + Claude |
| 2026-06-08 | v0.5 | Klassisches Grid-Bomberman in 3D (Top-Down, flaches Grid), neue Story, bewährte Meta-Progression | Robert Schneider + Claude |
| 2026-06-14 | **v0.6** | **Voll-3D-Arena-Demolition-Roguelite (kanonische GDD [3D_REINVENTION_PLAN.md](3D_REINVENTION_PLAN.md)): freie Bewegung, vertikale/zerstörbare Arenen, Physik-Bomben, volumetrische Blast-Formen, ebenenübergreifende 3D-Ketten, Air-/Drop-/Environmental-Combos + Style-Rang. Meta-Progression & Live-Service bleiben Fundament. Determinismus = 2-Schichten-Modell (autoritative deterministische Occupancy-Sim + kosmetische PhysX-Schicht; Replay-Hash bleibt über Schicht A). Battle-Pass als zwei Produkte (Premium 4,99 € / Plus 19,99 €). Feel-Prototyp-Gate vor Content-Breite; Timeline realistisch ~14–15 Mo.** | Robert Schneider + Claude |

> **Status:** Konzept-Phase v0.6 — volumetrisches 3D-Bomberman-Action (kein Idle, kein flaches Grid).
> Bereit für Feel-Prototyp + aktualisierte Content-Reuse-Map.
