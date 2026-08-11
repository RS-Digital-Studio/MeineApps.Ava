# BomberBlast 3D — Reinvention-GDD (v0.6, verbindlich)

> **Dies ist die verbindliche Spiel-Design-Quelle (GDD) für BomberBlast.Unity.** Sie löst die v0.5-Docs als
> *Soll* ab: [PLAN.md](PLAN.md), [DESIGN.md](DESIGN.md), [PARITY.md](PARITY.md), [VERTICAL_SLICE.md](VERTICAL_SLICE.md)
> bleiben **Referenz/Detail** (auf v0.6 nachgezogen), aber wo etwas abweicht, **gilt dieses Dokument**.
> Tech-Stack/Conventions → [CLAUDE.md](CLAUDE.md) + [ARCHITECTURE.md](ARCHITECTURE.md). KI-Assets → [ASSETS_AI.md](ASSETS_AI.md).
>
> **Stand:** v0.6 · 2026-06-14 · Robert Schneider + Claude
> **Genre:** **Voll-3D-Arena-Demolition-Roguelite** mit Bomberman-DNA — frei begehbar, **physisch
> zerstörbare** Arenen, Vertikalität, **Physik-Bomben**, volumetrische Explosionen. Immer **aktiv** gespielt.
> **Kein** Idle/AFK/Offline-Income, **kein** flaches 15×10-Grid als Gameplay, **kein** Multiplayer.

---

## Inhaltsverzeichnis

1. [Vision & Pitch](#1-vision--pitch)
2. [Was es ist (und was nicht)](#2-was-es-ist-und-was-nicht)
3. [Story (Neo-Grid / Overseer / Reborn)](#3-story-neo-grid--overseer--reborn)
4. [Core-Loop & Struktur](#4-core-loop--struktur)
5. [Bewegung & Raum (frei, vertikal)](#5-bewegung--raum-frei-vertikal)
6. [Bomben & volumetrische Blasts (Physik)](#6-bomben--volumetrische-blasts-physik)
7. [Zerstörbare Arenen](#7-zerstörbare-arenen)
8. [Combat: Gegner, Wardens, Combo & Style](#8-combat-gegner-wardens-combo--style)
9. [Spielmodi](#9-spielmodi)
10. [Meta-Progression & Wirtschaft](#10-meta-progression--wirtschaft)
11. [Monetarisierung (lean)](#11-monetarisierung-lean)
12. [Determinismus & Sim-Architektur](#12-determinismus--sim-architektur)
13. [3D-Lesbarkeit (Pflicht)](#13-3d-lesbarkeit-pflicht)
14. [Performance & Plattform](#14-performance--plattform)
15. [Roadmap (Feel-Prototyp-Gate)](#15-roadmap-feel-prototyp-gate)
16. [Risiken](#16-risiken)
17. [Offene Design-Fragen](#17-offene-design-fragen)
18. [Verhältnis zu den anderen Docs & Änderungslog](#18-verhältnis-zu-den-anderen-docs--änderungslog)

---

## 1. Vision & Pitch

> **BomberBlast: Reborn** nimmt die Bomberman-DNA — Sprengen, Ketten, räumliches Risiko, PowerUps, Combos —
> und baut daraus ein **voll-3D-Arena-Demolition-Roguelite**: Du bewegst dich **frei** durch **vertikale,
> physisch zerstörbare Arenen**, **legst/wirfst/rollst Physik-Bomben**, löst **volumetrische Explosionen**
> und **ebenenübergreifende Kettenreaktionen** aus, **bringst Architektur zum Einsturz** und nutzt die
> Zerstörung taktisch gegen Gegner und Sektor-Wardens. Drumherum die bewährte Meta-Progression: 10 Sektoren,
> Helden, Karten, Shop, Liga (Grid-Rankings), Roguelite-Dives (Anomaly-Dives), Battle-Pass — und
> **Master-Mode (Reborn)** als NG+. **Immer aktiv gespielt. Kein Idle, kein AFK, kein flaches Grid.**

**In einem Satz:** *Bomberman, befreit aus dem Gitter — ein volumetrisches 3D-Demolition-Action-Roguelite,
in dem Zerstörung, Vertikalität und physikalische Bomben das Spielgefühl tragen.*

**Brand:** Neon-Arcade-Look bleibt (Primär **#FF6B35**, Akzente Cyan **#22D3EE** / Gold **#FFDD33**),
energetisch, „Game Juice", leichter Cyber-Story-Rahmen. Anti-Style: Realismus, Tristesse, Idle/AFK,
Whale-Monetarisierung.

---

## 2. Was es ist (und was nicht)

**Es IST:** ein **aktiv gespieltes Voll-3D-Arena-Demolition-Roguelite** — freie Bewegung, Vertikalität,
Physik-Bomben, volumetrische Blasts, zerstörbare Arenen, tiefe Meta-Progression, neue Story, Master-Mode/NG+.

**Es ist NICHT:** kein Idle/Incremental, kein AFK/Auto-Battle/Auto-Run, kein Offline-Income, **kein flaches
15×10-Grid-Bomberman**, kein striktes 1:1-Remake, kein Multiplayer (Grid-Rankings/Daily-Race sind
**asynchrone** Leaderboards).

**Mechanik-Freiheit:** v0.6 darf **bewusst vom Original & von v0.5 abweichen**. Die Mechanik des produktiven
2D-BomberBlast und die v0.5-Grid-Pläne sind **Inspiration/Werte-Anker**, kein Soll. Was übernommen wird, ist
die **Meta-Progression + Live-Service-Code** (raum-agnostisch) — siehe [PARITY.md](PARITY.md).

---

## 3. Story (Neo-Grid / Overseer / Reborn)

Unter einer Neon-Megacity liegt **das Grid**: 10 Wartungs-Sektoren als **echtes volumetrisches 3D-Konstrukt**
(mehrstöckige Maschinen-Architektur), gekapert von der außer Kontrolle geratenen Stadt-KI **OVERSEER**, die
das Grid in einen tödlichen, sich selbst wieder aufbauenden, **vertikalen** Parcours verwandelt hat.

Du bist ein frisch aktivierter **Bomber** (augmentierter Abriss-Spezialist). In Sektor 1 birgst du einen
**Reborn-Core** — Overseer-Technik, die einen gefallenen Bomber aus seinen **„Blast-Daten"** wieder
zusammensetzt (stärker) und deine **3D-Mobilität** speist (Dash, Blast-Jump, Reborn-Fähigkeiten).

**Reborn (= Master-Mode/NG+):** Detonierst du den **Core** des Overseers, kollabiert das Grid und baut sich
**härter/anders verschachtelt** neu auf; du kehrst stärker zurück. Klassischer NG+, **keine** Idle-Prestige-
Schleife. Offener Geheimnis-Hook: **„True Core"** (Master-Mode-Ende, später).

**Wardens (neu benannt, Archetyp-Anker):** Granite Warden (StoneGolem) · Frostwyrm (IceDragon) · Magma
Revenant (FireDemon) · Null Phantom (ShadowMaster) · **The Overseer** (FinalBoss). Inszeniert als große
**vertikale 3D-Encounter mit Arena-Zerstörung**. Story voll lokalisiert (DE/EN/ES/FR/IT/PT).

---

## 4. Core-Loop & Struktur

**Session-Loop (rein aktiv):**
```
Öffnen → Sektor/Level (oder Dive) wählen → AKTIV spielen:
  frei bewegen (laufen/dashen/springen) · Physik-Bomben legen/werfen/rollen · 3D-Ketten ·
  Architektur sprengen · Gegner via Blast/Environmental-Kill · ggf. Warden
→ Sterne + Style-Rang + Coins/Karten → Shop/Deck verbessern → nächstes Level/Sektor
→ … → L100 → Master-Mode (Reborn) · nebenbei: Daily/Weekly, Anomaly-Dives, Liga, Battle-Pass
```

**Doppelstruktur (Roguelite-Betonung):**
- **Kampagne:** 100 Story-Level in 10 Sektoren (je 10), L10/20/… = Warden, Sterne-Wertung, Cutscenes.
- **Anomaly-Dives (Roguelite-Herz):** Floor-basierte Runs mit Buffs/Synergien/Modifikatoren, eigene
  Meta-Währung (Dive-Cores) — die **demolition-lastige Endlos-/Variabilitäts-Schicht**.

---

## 5. Bewegung & Raum (frei, vertikal)

- **Freie 3D-Bewegung** (NavMesh-/Collider-/CharacterController-basiert), **kein Grid-Lock**.
- **Vertikale, mehrstöckige Arenen** (Arbeitsannahme 2–3 Ebenen): Rampen, Plattformen, **Lifts**,
  Sprungfelder, **zerstörbare Böden** (Durchbruch → Fall), **Abgründe/Void** (Umgebungs-Kills).
- **Bewegungs-Skill:** Laufen · **Dash/Roll** (i-Frames, Cooldown) · **Sprung + Doppelsprung / Blast-Jump**
  (Bombe als Sprung-Boost, Risiko/Reward) · **Ledge-Grab/Vault**.
- **Reborn-Core-Fähigkeiten** schalten Mobilität schrittweise frei (narrativ verankert). Fall-Schaden = nein
  (Arcade); Void/Hazard = Tod/Schaden (telegraphiert).
- **Kamera:** dynamische 3D-Action-Kamera (Cinemachine 3, Smart-Framing, optional Soft-Lock auf Boss) +
  **Top-Down-Tilt-Fallback** (~55–65°) als Accessibility/Lesbarkeits-Option.

---

## 6. Bomben & volumetrische Blasts (Physik)

**Physik-Bomben** als echte 3D-Objekte (visuelle/physische Schicht): **legen / werfen (lobben) / rollen /
kicken**; prallen ab, rollen Rampen hinunter, **fallen zwischen Ebenen** → **ebenenübergreifende Ketten**.
**Charge-Bombe:** Halten → größerer/geformter Blast.

**Volumetrische Blast-Formen** (statt flachem Kreuz; treibt Bomben-/Karten-Identität):

| Form | Charakter | Beispiel |
|------|-----------|----------|
| **Sphere/Dome** | radial | Standard, Nova |
| **Pillar** | vertikale Säule (Ebenen darüber/darunter) | Lightning, PowerBomb |
| **Cone / Shaped Charge** | gerichtet, zielbar | Mirror, Vortex |
| **Cluster** | Streu-Submunition | Cluster |
| **Shockwave** | Knockback-Ring (in Void/Hazard) | Gravity/Sticky |
| **Implosion** | Sog → Blast | BlackHole |

**Authoritative Wirkung** (Schaden/Ketten/Belegung) wird auf der **deterministischen Occupancy-Sim**
aufgelöst (§12), **nicht** auf der PhysX-Debris-Schicht. 14 Bomben-Typen / 13 Sammel-Karten als Anker
([DESIGN §8](DESIGN.md)); **Collection trackt 13 Karten** (Standard = Default, nicht sammelbar).

---

## 7. Zerstörbare Arenen

- **Chunk-/Modul-basierte Destruktion** (Solo-machbar, performant; echte Voxel nur kosmetisch): Wände,
  Plattformen, Stützen brechen.
- **Strukturelles Spiel:** Stützpfeiler sprengen → **Sektion kollabiert** (taktische Zerstörung,
  Environmental-Kills). **Trümmer-Physik** rein **kosmetisch** (PhysX), mit Anzahl-/Lebensdauer-Caps pro
  Hardware-Tier — **nicht** Teil des autoritativen Zustands (§12.3).
- **Indestructible-Hülle** (Arena-Grenzen/tragende Struktur) bleibt — verhindert Soft-Locks, sichert Lesbarkeit.
- **Authoritative Belegung** (was blockiert/ist zerstört) lebt in der Occupancy-Sim; der sichtbare Einsturz
  ist die visuelle Spiegelung davon.

---

## 8. Combat: Gegner, Wardens, Combo & Style

- **Gegner (12-Typen-Anker):** Ballom/Onil/Doll/Minvo/Kondoria/Ovapi/Pass/Pontan + Tanker/Ghost/Splitter/
  Mimic — Verhaltens-Intent aus dem Original, **neu auf NavMesh + Vertikalität** (Off-Mesh-Links für
  Rampe/Lift/Fall), Bomben-Gefahr meiden. Elite-Variante (1.2× Speed / 2× HP / 3× Punkte).
- **Wardens (5 + 8 Modifier):** Telegraph→Attack→Cooldown, Enrage 50 %, **vertikale Arena-Phasen +
  Arena-Zerstörung** (kollabierende Deckung, Modul-Regen). Granite=BlockRegen→Modul-Regen, Frostwyrm,
  Magma Revenant, Null Phantom (Teleport/Stealth, Duo S9), The Overseer (Finale 2×).
- **Combo & Style:** Original-Combo-Fenster als Basis (×2…×10+), **erweitert um Air-Kills, Drop-Kills,
  Environmental-Kills, Cross-Level-Ketten**; daraus ein **Style-Rang** (D→SS, „DMC-light") → Score-/Coin-
  Faktor + HUD-Feedback. Bewusst leichtgewichtig, **kein** eigenes Meta, **kein** P2W. Werte → `BalancingConfig`.

---

## 9. Spielmodi

| Modus | Inhalt |
|-------|--------|
| **Story** | 100 Level / 10 Sektoren, Sterne, Wardens, Cutscenes |
| **Master-Mode (Reborn/NG+)** | nach L100: härter, Gegner-Upgrades, Master-Sterne |
| **Anomaly-Dives (Roguelite)** | Floor-Runs, Buffs/Synergien/Modifikatoren, Dive-Cores, demolition-lastig |
| **Quick-Play / Survival** | schnelle Action / Endlos |
| **Boss-Rush** | Warden-Sequenz, Wochen-Reset |
| **Daily-Challenge / Daily-Race** | tägliche **Seed-Arena** (weltweit gleich) / Bestzeit-Wettlauf |

---

## 10. Meta-Progression & Wirtschaft

- **Währungen:** Coins (Score/Combo/Style), Gems (3-Sterne/IAP/Quests), Dive-Cores (Anomaly).
- **Shop:** 12 permanente Upgrades (9 Stat + 3 Bomb-Unlocks, 700–17.000 Coins) — zentraler Coin-Sink.
- **Karten/Deck:** 4+1 Slots, Drops (57/25/12/6 %), Crafting (Coin-Sink). **13 Sammel-Karten.**
- **Helden (5):** Default/SpeedySam/BrickBoris/TwinTina/LuckyLola — Stats/Traits **beim 3D-Spawn angewandt** (NEU).
- **Achievements (72), Cosmetics (98 + Skins), Collection (Enemies/Bosses/PowerUps/13 BombCards/Cosmetics).**
- **Live-Service:** Daily-Reward, Daily/Weekly-Missions, Wochen-/Saison-Events, Lucky-Spin (Pity), Liga
  (Grid-Rankings, 14-Tage-Saison), Battle-Pass (30 Tier, 30-Tage-Saison), Cloud-Save, DSGVO-Pfade.
- **Legacy-Save-Import:** Bestandsspieler der Avalonia-Version verlustfrei übernehmen ([ARCHITECTURE §6.5](ARCHITECTURE.md)).

---

## 11. Monetarisierung (lean)

> **BomberBlast-Modell** — fair, kein Banner, kein P2W, keine Lootboxen. Werbung nur opt-in Rewarded.

- **Remove-Ads (1,99 €, non-consumable):** Premium-Flag → Rewarded ohne Video-Zwang + Premium-Skins.
- **Rewarded-Ads (opt-in):** Continue/Skip/PowerUp(ab L20)/Score-Double/Revival/Lucky-Spin-Extra/Dive-Retry.
- **VIP-Abo (`vip_subscription_monthly`, 9,99 €/Mo).**
- **Battle-Pass — zwei getrennte Produkte (Entscheidung 2026-06-14):**
  - **Premium-Track** `battle_pass_premium_season` **4,99 €/Saison** — schaltet den Premium-Track frei (klare Rewards).
  - **Plus-Paket** `battle_pass_plus_season` **19,99 €/Saison** — Premium-Track **+** 25 Tier-Skip, +50 % XP,
    Bonus-Gems/Tier-Up (enthält Premium). Plus impliziert Premium.
- **Gems** (erspielbar + IAP), **Starter-Pack**, **Cosmetics** (Gems/BP/Events). **Ethik:** transparente
  Drop-Rates (Lucky-Spin-Pity), Saison-Content auch erspielbar, keine P2W-Stats in kompetitiven Modi.

---

## 12. Determinismus & Sim-Architektur

> **Kern-Entscheidung v0.6 (verbindlich):** Voll-3D-Physik-Zerstörung ist **nicht** geräte-deterministisch
> (PhysX float, Mono vs. IL2CPP/ARM64). Lösung = **2-Schichten-Modell** — Gameplay bleibt deterministisch,
> nur die Optik ist es nicht.

### 12.1 Schicht A — Autoritative, deterministische Gameplay-Sim
- Läuft **fixed-step** (60 Hz, `FixedTimestepRunner`, **nie** `Time.deltaTime` in der Sim) auf einer
  **abstrahierten Occupancy-Repräsentation** (3D-Belegungs-/Voxel-Raster, Integer-/Fixed-Point) — **nicht**
  auf float-PhysX.
- Enthält: **Bomben-Timing, Blast-Auflösung (Volumen/Ketten/Belegung), Schaden, Gegner-Entscheidungen,
  Spieler-Treffer/i-Frames, Score/Combo/Style, Drop-Rolls, Win/Lose.**
- Alle gameplay-relevanten Random-Calls über `IRngProvider` (`DeterministicRandom`, xoshiro256+).
- Produziert einen **State-Hash** (FNV-1a, `GameStateSnapshot`) → reproduzierbar.

### 12.2 Schicht B — Nicht-autoritative visuelle Physik
- **PhysX-Debris/Geröll, Trümmer, Partikel, Kamera-Tremor** — **rein kosmetisch**, beeinflusst Schicht A nie.
- Darf **non-deterministisch** sein (float, Visual-RNG `[Key("visual")]`), wird über Hardware-Tier gecapt.
- **Nicht** Teil des State-Hash.

### 12.3 Verifikation, Daily-Race & Anti-Cheat
- **Replay/Daily-Race verifizieren über den deterministischen Zustand (Schicht A)**, **nie** über Debris.
  Gleicher Seed + gleiche Input-Sequenz ⇒ identischer State-Hash (Schicht A). `ReplayCapture` zeichnet Inputs
  auf; die visuelle Debris-Spur ist nur Anzeige (Ghost), nicht Verifikations-Orakel.
- **Seed-deterministische Generierung** (Arena/Spawns/Drops/Dive-Map/Daily-Inhalte) über `IRngProvider`:
  gleiche Seed = gleiche Welt weltweit.
- **CI-Gate (Pflicht):** (1) **Determinismus-Replay-Suite** über Schicht A (Replay-Corpus → identischer
  State-Hash) + (2) **Seed-Reproduzierbarkeit** der Generierung. Failure blockt Merge.
- **Anti-Cheat:** lokale Integrität (Overflow-Guards, Hybrid-Timer, PersistenceHealth) + async
  Server-Plausibilität (RTDB-Rules, Server-Timestamp, Rate-Limit) für Grid-Rankings/Daily-Race. Kein
  Echtzeit-Server-Resim (Single-Player).

> **Konsequenz für den Port:** `DeterministicRandom`/`IRngProvider`/`FixedTimestepRunner`/`ReplayCapture`/
> `GameStateSnapshot` bleiben **PORT-1:1, bit-identisch** (für Schicht A). Das frühere flache 15×10-Grid wird
> durch die **3D-Occupancy-Repräsentation** ersetzt (REBUILD/NEW), bleibt aber deterministisch. Siehe
> [ARCHITECTURE §13](ARCHITECTURE.md) + [PARITY §7](PARITY.md).

---

## 13. 3D-Lesbarkeit (Pflicht)

Größtes Spielbarkeits-Risiko. Verbindlich:
- **Blast-Preview-Volumen** beim Zielen/Legen (Form + betroffene Flächen + prognostizierte Ketten/Fall),
  Standard AN, abschaltbar.
- **Through-Wall-Silhouetten (Outline)** für Spieler, scharfe Bomben, gefährdete Gegner.
- **Höhen-/Ebenen-Indikatoren** (Boden-Schattenpunkt unter Spieler & fallenden Bomben, Ebenen-Tönung).
- **Occlusion-Handling** (Dither/Fade verdeckender Geometrie), **Danger-Telegraphs in 3D**.
- **Empirischer Min-Spec-Test (Galaxy A50)** im Feel-Prototyp — fällt er durch: konservativere Kamera/
  reduzierte Vertikalität.

---

## 14. Performance & Plattform

- **Ziel:** 60 FPS High-End, 30 FPS Low-End (Galaxy A50). **Android primär** (API 24+), Desktop (Test).
  Kein iOS/Steam.
- **Hardware-Tier** (Low/Mid/High/Ultra) skaliert **Partikel-, Physik-Debris- und Destruktions-Caps**, LOD,
  Bloom, Reverb, adaptives Frame-Skipping. Object-Pooling Pflicht (Bomben/Gegner/Trümmer/VFX).
- **AAB < 250 MB** (Play Asset Delivery). Determinismus-Schicht A ist günstig; die Kosten liegen in Schicht B
  (Physik/VFX) → strikt gecapt.

---

## 15. Roadmap (Feel-Prototyp-Gate)

| Phase | Monat | Ziel |
|-------|-------|------|
| 0 Setup | 1 | Unity/URP-Skelett, Asmdefs, CI (Replay-Hash über Schicht A + Seed-Repro) |
| **1 Feel-Prototyp (GATE)** | 2–4 | freie Bewegung, Physik-Bombe, **eine** volumetrische Blast-Form + 3D-Kette, zerstörbare Module, **ein vertikaler Sektor + Granite Warden**, Lesbarkeit auf A50 → **Spaß & lesbar? erst dann weiter** |
| 2 Combat-Breite + Meta | 4–7 | alle Blast-Formen/Bomben, 12 Gegner (NavMesh), 5 Wardens, 100 Level/10 Sektoren; Wirtschaft/Shop/Karten/Helden/Achievements/Cloud-Save/Tutorial |
| 3 Modi & LiveOps | 7–9 | Master-Mode, Anomaly-Dives, Boss-Rush, Grid-Rankings, Daily/Weekly/Events, Lucky-Spin, Battle-Pass (Premium+Plus) |
| 4 3D-Art & Polish | 9–11 | Sektoren/Helden/Gegner/Wardens, Destruktions-/Debris-Assets, VFX, Shader, Music, Cutscenes, Cosmetics, Style |
| 5 Closed Beta DACH | 11–13 | Balancing (3D), Low-End-Perf (Physik/Destruktion), Touch-3D-Controls, Tutorial-Funnel |
| 6 Soft-Launch DACH | 14–15 | Saison 1 |
| 7 Skalierung | 16+ | EU → Global, weitere Saisons. Kein MP, kein iOS/Steam |

**Realistischer Soft-Launch ~Monat 14–15.** Detail → [ROADMAP.md](ROADMAP.md), Slice → [VERTICAL_SLICE.md](VERTICAL_SLICE.md).

---

## 16. Risiken

| # | Risiko | Wkt. | Impact | Mitigation |
|---|--------|------|--------|------------|
| 1 | Scope (Voll-3D-Demolition + Physik + voller Content) Solo | Hoch | Hoch | **Feel-Prototyp-Gate**, Meta-Reuse, Single-Player |
| 2 | 3D-Controls auf Touch (Bewegung+Zielen+Werfen) | Hoch | Hoch | Prototyp zuerst, Auto-Aim/Soft-Lock, Assist |
| 3 | Low-End-Performance (Physik+Debris+VFX) | Hoch | Hoch | Tier-Caps, Pooling, A50-Test/Sprint |
| 4 | 3D-Lesbarkeit | Hoch | Hoch | Preview-Volumen, Outlines, Höhen-Indikatoren, A50-Test |
| 5 | **2-Schichten-Sauberkeit** (Debris darf Schicht A nie beeinflussen) | Mittel | Hoch | strikte Trennung, Determinismus-Replay-Gate (§12.3) |
| 6 | Balancing in 3D | Mittel | Mittel | `BalancingConfig`, Beta-Telemetrie |
| 7 | Save-Integrität / Legacy-Import | Mittel | Hoch | Schema-Migrator, ChooseBest, ≥99 % Import |

---

## 17. Offene Design-Fragen

1. **Occupancy-Auflösung:** Wie fein das deterministische 3D-Belegungsraster (Balance Determinismus/Feel/Perf)? — *Prototyp.*
2. **Vertikalitäts-Tiefe:** 2–3 Ebenen, wie aggressiv Fall/Lift? — *2–3, im Prototyp tunen.*
3. **Roguelite-Gewichtung:** Wie stark Anomaly-Dives ggü. Kampagne ins Zentrum rücken? — *Dives als Endgame-Herz, Kampagne als Onboarding/Story.*
4. **Style-Rang-Tiefe:** erweitertes Combo vs. eigenes System? — *erweitertes Combo + Rang-Anzeige.*
5. **Titel:** „BomberBlast: Reborn"? Alternativen willkommen.

---

## 18. Verhältnis zu den anderen Docs & Änderungslog

**Doc-Hierarchie (v0.6):** Diese GDD = **Soll** (Spiel-Design). [CLAUDE.md](CLAUDE.md) = Conventions/Tech-Regeln
(verbindlich). [ARCHITECTURE.md](ARCHITECTURE.md) = Tech-Tiefe. [PARITY.md](PARITY.md) = Reuse/Neubau-Map.
[ROADMAP.md](ROADMAP.md) = Produktion. [VERTICAL_SLICE.md](VERTICAL_SLICE.md) = Phase-1-Durchstich.
[PLAN.md](PLAN.md)/[DESIGN.md](DESIGN.md) = **detaillierte Referenz** (auf v0.6 nachgezogen), dieser GDD geht vor.

| Datum | Version | Änderung |
|-------|---------|----------|
| 2026-06-14 | **v0.6** | **Initiale Reinvention-GDD: Voll-3D-Arena-Demolition-Roguelite (freie Bewegung, vertikale/zerstörbare Arenen, Physik-Bomben, volumetrische Blasts, ebenenübergreifende Ketten, Air-/Drop-/Environmental-Combos + Style-Rang). 2-Schichten-Determinismus (§12.3: autoritative deterministische Occupancy-Sim + kosmetische PhysX-Schicht; Replay-Hash über Schicht A bleibt). Battle-Pass-Split (Premium 4,99 € / Plus 19,99 €). Collection 13 Karten. Feel-Prototyp-Gate, Soft-Launch ~Mo 14–15.** |

> **Status:** GDD v0.6 verbindlich. **Nächster Schritt:** Editor-Open-Verifikation → **Feel-Prototyp**
> (Sektor 1 + Granite Warden in 3D, [VERTICAL_SLICE.md](VERTICAL_SLICE.md)).
