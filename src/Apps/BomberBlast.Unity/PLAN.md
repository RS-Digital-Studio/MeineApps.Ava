# BomberBlast 3D — Master-Plan

> **Status:** Konzept-Phase v0.5 (Stand 2026-06-08)
> **Arbeitstitel:** BomberBlast: Reborn (modernes 3D-Bomberman)
> **Genre:** **Modernes 3D-Bomberman-Action** mit tiefer Meta-Progression (Sektoren/Level, Helden, Karten,
> Liga, Roguelike-Dives, Master-Mode/NG+). Klassisches Bomberman-Gameplay in modernem 3D — **immer aktiv
> selbst gespielt**.
> **Ausdrücklich:** **KEIN Idle-Game. KEIN AFK/Auto-Battle. KEIN Offline-Income.** Es spielt nie eine KI
> für dich, und es gibt keinen passiven Fortschritt — Fortschritt entsteht **nur durch aktives Spielen**.
> **Setting:** **Neue Story** (Neo-Grid / Overseer / Reborn-Core) im bestehenden Neon-Arcade-Look.
> **Plattformen:** Android (primär, wie das Original) + Desktop (Test). Kein iOS/Steam geplant.
> **Team:** Solo-Indie + KI-Assistenz (right-sized).
> **Monetarisierung:** **Lean / fair** wie das 2D-Original: kostenlos + Rewarded-Ads (kein Banner),
> 1,99 € Remove-Ads. Keine Lootboxen, kein Pay-to-Win.

Dieses Dokument ist die **Master-Übersicht**. Tiefe in:

| Bereich | Datei |
|---------|-------|
| Game-Design (Story, Gameplay, Helden, Bomben, Gegner, Bosse, Modi, Progression, Live-Service) | [DESIGN.md](DESIGN.md) |
| Content-Wiederverwendung (welches Original-System wird wie übernommen/umgewidmet) | [PARITY.md](PARITY.md) |
| Tech-Architektur (Unity-Stack, Asmdefs, Determinismus, Save, Performance) | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Produktion (Roadmap, Marketing, Compliance, Risiken) | [ROADMAP.md](ROADMAP.md) |
| Unity-Code-Conventions, bekannte Stolperfallen | [CLAUDE.md](CLAUDE.md) |
| KI-Asset-Pipeline (3D-Meshes + PBR-Texturen) | [ASSETS_AI.md](ASSETS_AI.md) |

> **Richtungs-Historie:** v0.2 = Sci-Fi-Reinvention (verworfen) · v0.3 = treuer 1:1-Remake (abgelöst) ·
> v0.4 = kurzzeitig Idle-Game-Experiment (**verworfen — BomberBlast ist kein Idle-Game**) ·
> **v0.5 (aktuell) = modernes 3D-Bomberman: klassisches, aktiv gespieltes Bomberman in 3D, mit neuer
> Story und der bewährten Bomberman-Meta-Progression.** Das Original-Gameplay + der Domain-Code bleiben
> Fundament; übernommen wird, was das Spiel trägt — kein strenges 1:1-Parität-Mandat (Inhalte dürfen
> bewusst modernisiert/angepasst werden).

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

> **BomberBlast: Reborn** ist ein **modernes 3D-Bomberman**, das man **immer selbst aktiv spielt** —
> klassisches Bomberman in echtem 3D (URP): Du steuerst deinen Bomber, legst Bomben, kettest Explosionen,
> sprengst Blöcke, sammelst PowerUps, baust Combos und legst Gegner und Sektor-Bosse. Drumherum liegt die
> **bewährte Bomberman-Meta-Progression**: 100 Story-Level in 10 Sektoren mit Sterne-Wertung, permanente
> Shop-Upgrades, eine Karten-/Bomben-Sammlung, Helden, ein Roguelike-Modus (Anomaly-Dives), Liga
> (Grid-Rankings), Battle-Pass — und **Master-Mode (Reborn)** als New-Game-Plus für endlosen Wiederspielwert.
> Neu sind **3D-Optik**, eine **neue Story** und moderne Präsentation. **Kein Idle, kein AFK, kein Offline-Farming.**

**In einem Satz:** *Das klassische Bomberman-Erlebnis — Skill, Combos, Bosse, Sterne sammeln — in modernem
3D, mit neuer Story und tiefer Meta-Progression. Voll aktiv gespielt.*

### 1.2 Brand-Identität (Look bleibt, Story neu)

Der **Neon-Arcade-Look bleibt** Markenkern — nur die **Story** ist neu (bewusste Entscheidung).

| Aspekt | Wert |
|--------|------|
| **Primärfarbe** | Neon-Orange **#FF6B35** |
| **Akzente** | Cyan **#22D3EE** + Gold-Trail **#FFDD33** |
| **Tonalität** | Energetisch, Arcade, "Game Juice"; leichter Cyber-Story-Rahmen (kein Grimdark) |
| **Visual-Sprache** | Neon-Arcade in 3D: oktagonale Formen, Glow, emissive Materialien, Bloom |
| **Anti-Style** | Realismus/Foto-Texturen, düstere Tristesse, **Idle-/AFK-Selbstläufer**, aggressive Whale-Monetarisierung |

---

## 2. Was es ist (und was nicht)

> Wegen der Richtungs-Historie hier explizit zur Vermeidung von Missverständnissen:

**Es IST:**
- Ein **modernes 3D-Bomberman-Action-Spiel**, klassisch und **aktiv** gespielt (Grid, Bomben, Ketten, PowerUps, Combos, Bosse).
- Inhaltsreich: 10 Sektoren × 10 Level, Bosse, Roguelike-Dives, Liga, Battle-Pass, Helden, Karten, Cosmetics.
- Mit **Master-Mode (Reborn)** = New-Game-Plus nach L100 für Wiederspielwert (Feature aus dem Original).
- Mit **neuer Story** und 3D-Aufwertung von Optik/Audio.

**Es ist NICHT:**
- **Kein Idle-/Incremental-Game.** Kein passives Einkommen, keine "Zahlen-gehen-hoch-während-du-wartest"-Mechanik.
- **Kein AFK / Auto-Battle / Auto-Run.** Es spielt **nie** eine KI Level für dich.
- **Kein Offline-Income.** Fortschritt entsteht ausschließlich durch aktives Spielen.
- **Kein striktes 1:1-Remake.** Inhalte dürfen modernisiert/angepasst werden (neue Story, neue Boss-Namen, 3D).

---

## 3. Story-Pitch (neu)

> Vollständige Story + Welt → [DESIGN.md §2](DESIGN.md). Hier der Pitch.

**Welt — NEO-GRID:** Unter einer Neon-Megacity liegt **das Grid**: 10 Wartungs-Sektoren, gekapert von
der außer Kontrolle geratenen Stadt-KI **OVERSEER**, die das Grid in einen tödlichen, sich selbst
wieder aufbauenden Parcours verwandelt hat.

**Held — der Bomber:** Du bist ein frisch aktivierter **Bomber** (augmentierter Abriss-Spezialist). In
Sektor 1 birgst du einen **Reborn-Core** — Overseer-Technik, die einen gefallenen Bomber aus seinen
**"Blast-Daten"** wieder zusammensetzt, jedes Mal **stärker**.

**Der Reborn (= Master-Mode / NG+):** Sprengst du dich durch alle 10 Sektoren bis zum **Core** des
Overseers und detonierst ihn, **kollabiert das Grid** — und baut sich **härter** neu auf. Du aber kehrst
**Reborn** zurück: stärker, für einen neuen, schwereren Durchlauf. (Das ist der bestehende Master-Mode des
Originals, narrativ verankert — **keine** Idle-Prestige-Schleife.)

**Neue Bosse (Sektor-Wardens des Overseers):** Granite Warden · Frostwyrm · Magma Revenant · Null
Phantom · **The Overseer** (Core-Avatar, Finale). Mechanisch bauen sie auf den 5 bewährten Boss-Archetypen
des Originals auf, neu benannt und inszeniert.

---

## 4. Zielgruppe & Personas

**Persona A: "Der Bomberman-Nostalgiker" (Kern, ~45 %)**
- Spielte Bomberman (SNES/NES/PS1); will den aktiven Skill-Kern (Story-Level, Sterne, Bosse, Combos) in schönem 3D.
- Akzeptiert faires F2P: Rewarded-Ads opt-in, 1,99 € Remove-Ads.

**Persona B: "Der Casual-Mobile-Action-Gamer" (~30 %)**
- Kurze aktive Sessions: ein paar Level, Daily-Challenge, Daily-Race, Lucky-Spin. Liga + Battle-Pass als Bindung.

**Persona C: "Der Completionist/Skiller" (~20 %)**
- Will alle Achievements, alle Karten max, alle Cosmetics, 3-Sterne überall, Master-Mode-Sterne, Bestzeiten.

> (Reiner Single-Player, Android-fokussiert — keine PC-/Cross-Save-/Multiplayer-Persona.)

---

## 5. Strategische Entscheidungen

| # | Frage | Entscheidung |
|---|-------|--------------|
| 1 | Grundprinzip | **Modernes 3D-Bomberman, aktiv gespielt, mit tiefer Meta-Progression.** Kein Idle, kein 1:1-Remake. |
| 2 | Engine | **Unity 6 + URP** (3D-Top-Down mit leichter Neigung, Cinemachine). |
| 3 | Gameplay | **Klassisches Bomberman** (Grid, Bomben, Ketten, PowerUps, Combos, Bosse) — "typisch, modern". |
| 4 | Genre-Ausschluss | **Kein Idle/AFK/Auto-Battle, kein Offline-Income, kein passiver Fortschritt.** |
| 5 | Story/Setting | **Neue Story** (Neo-Grid/Overseer/Reborn). Neon-Arcade-**Look bleibt**. |
| 6 | Wiederspielwert | **Master-Mode (Reborn) = NG+** nach L100 (Feature aus Original, narrativ verankert). |
| 7 | Content-Quelle | Bomberman-Mechanik + Domain-Code des Originals als **Fundament** (wiederverwendet, modernisiert). |
| 8 | Bestehende Docs | Neue Richtung; alte Remake-/Idle-Docs werden **abgelöst/umgewidmet** (PARITY → Content-Reuse-Map). |
| 9 | Plattformen | **Android primär**, Desktop für Test. Kein iOS/Steam geplant. |
| 10 | Monetarisierung | **Lean (BomberBlast-Modell):** F2P + Rewarded (kein Banner), 1,99 € Remove-Ads. Keine Lootboxen, kein P2W. |
| 11 | Performance | **60 FPS High-End, 30 FPS Low-End** mit Hardware-Tier-Skalierung. |
| 12 | Team-Realität | **Solo-Indie + KI** — Scope getrimmt, **reiner Single-Player**. Kein Multiplayer/Online. |

---

## 6. Was bleibt vom Original / Was wird neu

### 6.1 Bleibt (wiederverwendet — Fundament)

- **Aktives Bomberman-Gameplay:** 15×10-Grid, Bomben-Lege-/Ketten-Logik, 12 PowerUps, 14 Bomben-Typen,
  12 Gegner-Typen, 5 Boss-**Archetypen** (neu benannt), Combo-System, Layout-/Mutator-Generator.
- **Modi:** Story (100 Level), Master-Mode (Reborn/NG+), Quick-Play, Survival, Roguelike-Dives, Boss-Rush,
  Daily-Challenge, Daily-Race.
- **Meta-Progression:** 12 permanente Shop-Upgrades, Karten-/Deck-Sammlung + Crafting, Helden, 72 Achievements,
  Wirtschaft (Coins/Gems), Cosmetics (98).
- **Pure-Domain-Code (1:1 portierbar):** `ComboSystem`, `LevelLayoutGenerator`, Pathfinding (A*/BFS),
  `DungeonSynergyResolver`, `DeterministicRandom`, Anti-Cheat-Hybridtimer, Overflow-Guards.
- **Live-Service:** Daily-Reward, Daily/Weekly-Missions, Wochen-/Saison-Events, Lucky-Spin, Battle-Pass,
  async-Liga (→ "Grid-Rankings"), Cloud-Save, DSGVO-Pfade.
- **Accessibility & Audio-Architektur:** Colorblind/HighContrast/UiScale/Subtitles; 7-Kanal-AudioBus, adaptive Music.

### 6.2 Neu

- **Neue Story** (Neo-Grid/Overseer/Reborn) + neu benannte Bosse, in 3D-Cutscenes (Timeline).
- **3D-Engine/Optik/Audio:** Unity 6 + URP, dynamische Beleuchtung, Schatten, VFX-Graph-Explosionen,
  Shader-Graph, 3D-Spatial-Audio, kuratiertes/aufgewertetes Audio.
- **Modernisierungs-Freiheit:** Inhalte dürfen angepasst werden (kein 1:1-Zwang) — neue Boss-/Sektor-Namen,
  ggf. neue Karten/Cosmetics-Themen passend zur Story.

### 6.3 Bewusst gestrichen / nicht enthalten

- **Idle/Incremental-Mechanik, AFK/Auto-Battle/Auto-Run, Offline-Income, passiver Fortschritt** — alles raus.
- **Idle-Meta-Prestige (Singularity/Eternal-Drive aus dem v0.4-Experiment)** — gestrichen; nur Master-Mode/Reborn bleibt.
- **"100 % Feature-Parität"** als striktes Mandat — ersetzt durch "übernehmen + modernisieren, was trägt".
- **Online-PvP/Multiplayer, Photon/Netcode, Esports, Cross-Platform-AAA-Anspruch, Full-Studio-Team & -Budget** — **komplett gestrichen** (Solo-Indie, reiner Single-Player).
- **Whale-Monetarisierung / aggressive IAP** — bleibt lean.

---

## 7. Core-Loop & Modi

### 7.1 Session-Loop (rein aktiv)

```
Öffnen → Sektor/Level wählen → AKTIV spielen (Bomben, Combos, PowerUps, ggf. Boss)
       → Sterne + Coins/Karten verdienen → Shop-Upgrades / Deck verbessern
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
| **Daily-Challenge / Daily-Race** | tägliches deterministisches Level / Bestzeit-Wettlauf (Grid-Rankings) |

---

## 8. Monetarisierung (lean)

> Entscheidung: **BomberBlast-Modell** — fair, schlank, kein Banner, kein P2W, keine Lootboxen.
> Details → [DESIGN.md §16](DESIGN.md).

- **Remove-Ads (1,99 €):** entfernt Interstitials; Rewarded bleibt opt-in. Kern-IAP wie im Original.
- **Rewarded-Ads (opt-in):** Continue (Coins verdoppeln), Level-Skip, PowerUp (ab L20), Score-Double,
  Revival, Lucky-Spin, Anomaly-Retry. Hybrid-Cooldown.
- **Gems** (erspielbar + optionaler IAP): Deck-Slot, Helden-Direktkauf, Premium-Karten, Anomaly-Eintritt.
- **Battle-Pass (Saison):** Free + optional Premium-Track — klare Rewards pro Tier, **keine** Zufalls-Boxen.
- **Cosmetics:** Trails/Frames/Victories/Skins über Gems, Battle-Pass, Events.
- **Ethik:** keine Pay-to-Win-Stats in kompetitiven Modi, Saison-Content auch über Gameplay erreichbar,
  transparente Drop-Rates (Lucky-Spin mit Pity).

---

## 9. USPs

1. **"Dein Bomberman — jetzt in 3D"** — das vertraute Action-Erlebnis, komplett 3D neu gerendert.
2. **Tiefer aktiver Content** — 100 Level + Master-Mode + Roguelike-Dives + Liga: viel mehr als generische Klone.
3. **Skill statt Selbstläufer** — voll aktiv gespielt, Combos & 3-Sterne belohnen Können. **Kein Idle, kein AFK.**
4. **Werbe-fair & P2W-frei** — kostenlos, Rewarded opt-in, 1,99 € Remove-Ads, keine Lootboxen.
5. **AAA-Mobile-Optik im Neon-Arcade-Stil** — dynamische Beleuchtung, VFX-Graph-Explosionen, neue Story.

---

## 10. Erfolgs-KPIs

### 10.1 Engagement

| KPI | Ziel |
|-----|------|
| Sessions pro DAU | ≥ 3.5 |
| Session-Länge median | 8–12 Min |
| Tutorial-Completion | ≥ 85 % |
| Sektor-2-Erreichung (D7) | ≥ 60 % |
| Dives-Teilnahme (ab L20) | ≥ 40 % |
| Liga-Teilnahme (MAU) | ≥ 30 % |

### 10.2 Retention

| KPI | Soft-Launch | Skaliert |
|-----|-------------|----------|
| D1 | ≥ 35 % | ≥ 28 % |
| D7 | ≥ 14 % | ≥ 10 % |
| D30 | ≥ 8 % | ≥ 5 % |
| Crash-Free-Users | ≥ 99 % | ≥ 99.5 % |

### 10.3 Monetarisierung (lean)

| KPI | Ziel |
|-----|------|
| ARPDAU | 0.08–0.15 € (werbegestützt, fair) |
| Remove-Ads-Conversion (1,99 €) | ≥ 3 % |
| Rewarded-Opt-In-Rate (Sessions) | ≥ 40 % |
| Battle-Pass-Premium (pro Saison) | ≥ 6 % MAU |

### 10.4 Technik

| KPI | Ziel |
|-----|------|
| FPS High-End / Low-End (z.B. Galaxy A50) | 60 / 30 |
| App-Größe (AAB, mit Play-Asset-Delivery) | < 250 MB |
| Cloud-Save-Sync-Success | ≥ 99.5 % |

---

## 11. High-Level-Roadmap

> Detail-Sprints → [ROADMAP.md](ROADMAP.md). Right-sized für Solo-Indie + KI. Single-Player-Core zuerst.

| Phase | Zeitrahmen | Hauptziel |
|-------|-----------|-----------|
| **Phase 0** | Monat 1 | Setup: Unity-Skelett, URP, VContainer, CI, Asmdefs. Pure-Domain-Code-Port (Combo/Layout/Pathfinding/RNG). |
| **Phase 1** | Monat 2–4 | **Aktiver Bomberman-Core in 3D:** Grid, Bomben/Ketten, 12 PowerUps, 12 Gegner, 5 Bosse, 100 Level in 10 Sektoren, Layouts/Mutatoren, HUD, Combo. Fixed-Step-Sim + `IRngProvider`. |
| **Phase 2** | Monat 4–6 | **Meta-Progression:** Wirtschaft (Coins/Gems), 12 Shop-Upgrades, Karten/Deck/Crafting, Helden, 72 Achievements, Cloud-Save, Tutorial. |
| **Phase 3** | Monat 6–8 | **Modi & Live-Service:** Master-Mode (Reborn/NG+), Anomaly-Dives, Boss-Rush, Liga (Grid-Rankings), Daily/Weekly/Events, Lucky-Spin, Battle-Pass. |
| **Phase 4** | Monat 8–9 | **3D-Art & Polish:** alle Sektoren/Helden/Gegner/Bosse, VFX-Graph, Shader, adaptive Music, Story-Cutscenes, Cosmetics. |
| **Phase 5** | Monat 9–10 | **Closed Beta DACH:** Balancing, Low-End-Performance, Tutorial-Funnel, LiveOps-Tooling. |
| **Phase 6** | Monat 11–12 | **Soft-Launch DACH** + Saison 1. |
| **Phase 7** | Monat 13+ | Skalierung (EU/Global), weitere Saisons & Content-Updates. Kein Multiplayer, kein iOS/Steam. |

**Realistischer Soft-Launch ~Monat 12.** Reiner Single-Player — kein Multiplayer/Online.

---

## 12. Risiko-Summary

> Vollständiges Register → [ROADMAP.md](ROADMAP.md#risiken). Top-6:

| # | Risiko | Wkt. | Impact | Mitigation |
|---|--------|------|--------|------------|
| 1 | **Scope** (voller Bomberman-Content + 3D + neue Story) für Solo zu groß | Hoch | Hoch | Strikte Phasen-Gates, Content-Reuse maximieren, Single-Player-Fokus (kein MP), Polish nach hinten |
| 2 | **3D-Performance Low-End** (VFX-Explosionen + viele Gegner) | Mittel | Hoch | Hardware-Tier-System, LOD, VFX-Caps, Object-Pooling, Low-End-Tests pro Sprint |
| 3 | **Balancing/Schwierigkeit** (klassisch knackig vs. modern zugänglich) | Mittel | Mittel | `BalancingConfig`-ScriptableObject, Beta-Telemetrie, Sterne-/Difficulty-Tuning |
| 4 | **Story/Brand-Kohärenz** (neue Story vs. Neon-Brand) | Niedrig | Mittel | Look bleibt #FF6B35-Neon-Arcade; Story als leichter Cyber-Rahmen, kein Tonbruch |
| 5 | **Content-Umfang** (Original ist sehr groß) realistisch portieren | Mittel | Hoch | Content-Reuse-Map (PARITY) als Checkliste; modernisieren statt 1:1 |
| 6 | **Genre-Erwartung extern** (Marketing als "Idle" falsch gelabelt) | Niedrig | Mittel | Klar als **Action-Bomberman** positionieren — nicht als Idle |

---

## 13. Nächste konkrete Schritte

### Sofort (Woche 1–2)
1. **Diesen Plan + DESIGN.md reviewen** → Story & Modernisierungs-Umfang bestätigen (siehe §14).
2. **Unity-Projekt** `src/Apps/BomberBlast.Unity/Unity/` (Unity 6 + URP) anlegen + Git-LFS + `.gitignore`.
3. **Pure-Domain-Code-Port** planen (Combo/Layout/Pathfinding/RNG — keine Unity-API → 1:1).

### Mittelfrist (Monat 1–4)
4. **CI/CD** (game-ci, EditMode-Tests + Determinismus-Replay).
5. **Aktiver Bomberman-Vertical-Slice** (1 Sektor, 1 Boss) in 3D — Spielgefühl zuerst.
6. **Concept-Art/3D-Sprint** für Bomber + Sektor 1 (Neon-Arcade-Stil, 3D).

---

## 14. Offene Design-Fragen

1. **Modernisierungs-Umfang:** Wie weit weg vom Original-Content (neue Karten/Cosmetics-Themen passend zur
   Story?) oder Content weitgehend übernehmen und nur neu einkleiden? — *Vorschlag: Content übernehmen, Namen/Themen modernisieren.*
2. **Sektor-Umfang:** exakt 10 × 10 (=100), oder mehr/weniger? — *Vorschlag: 100 wie Original.*
3. **Master-Mode-Tiefe:** nur 1 NG+-Stufe (wie Original) oder mehrere? — *Vorschlag: wie Original (1), später erweiterbar.*
4. **Titel:** "BomberBlast: Reborn"? Alternativen willkommen.

---

## Änderungslog

| Datum | Version | Änderung | Autor |
|-------|---------|----------|-------|
| 2026-05-26 | v0.1 | Initial-Version | Robert Schneider + Claude |
| 2026-05-26 | v0.2 | Sci-Fi-Reinvention (OmniCorp/Mech/PvP-Arena) | Robert Schneider + Claude |
| 2026-05-30 | v0.3 | Treuer 1:1-3D-Remake (Sci-Fi verworfen) | Robert Schneider + Claude |
| 2026-06-08 | v0.4 | Idle-Game-Experiment (verworfen) | Robert Schneider + Claude |
| 2026-06-08 | **v0.5** | **Modernes 3D-Bomberman: klassisches, AKTIV gespieltes Bomberman in 3D + bewährte Bomberman-Meta-Progression (100 Level, Modi, Shop, Liga, Battle-Pass, Master-Mode/Reborn-NG+). NEUE Story (Neo-Grid/Overseer/Reborn). KEIN Idle, KEIN AFK, KEIN Offline-Income. Lean Monetarisierung, Solo-Indie-Scope. Content modernisiert, kein striktes 1:1.** | Robert Schneider + Claude |

> **Status:** Konzept-Phase v0.5 — modernes 3D-Bomberman (kein Idle). Bereit für Vertical-Slice + Content-Reuse-Map.
