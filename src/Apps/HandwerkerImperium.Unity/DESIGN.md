# DESIGN.md — HandwerkerImperium-Unity (Game Design Document)

> **Vollständige Spielmechanik-Spezifikation mit konkreten Werten aus Avalonia v2.1.1.**
> Alle Balancing-Werte 1:1 portiert (siehe `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/Models/GameBalanceConstants.cs` als Single-Source-of-Truth).
> Visualisierungs-Direktiven entsprechen [ASSETS_AI.md](ASSETS_AI.md) (Low-Poly Stylized Toon, Handwerker-Stadt-Hub, 3D-Worker mit Mecanim).
> **Persona-Anker:** Meister Hans (1500 Voice-Lines in 6 Sprachen, siehe ASSETS_AI.md § 11.3).

---

## Inhaltsverzeichnis

1. [Spielprinzip & Loop](#1-spielprinzip--loop)
2. [Setting & Persona "Meister Hans"](#2-setting--persona-meister-hans)
3. [Ressourcen & Währungen](#3-ressourcen--währungen)
4. [Werkstätten (10 Typen)](#4-werkstätten-10-typen)
5. [Arbeiter (10 Tiers)](#5-arbeiter-10-tiers)
6. [Aufträge (6 Types + 3 Strategien)](#6-aufträge-6-types--3-strategien)
7. [Mini-Games (13 Typen)](#7-mini-games-13-typen)
8. [Forschung (57 Nodes, 4 Branches)](#8-forschung-57-nodes-4-branches)
9. [Prestige (7 Tiers + Ascension)](#9-prestige-7-tiers--ascension)
10. [Crafting (30 Rezepte, 4 Tiers)](#10-crafting-30-rezepte-4-tiers)
11. [Lager (Warehouse V7)](#11-lager-warehouse-v7)
12. [Markt & Material-Affinität](#12-markt--material-affinität)
13. [Reputation & Tier-System](#13-reputation--tier-system)
14. [Master-Tools (12 Artefakte)](#14-master-tools-12-artefakte)
15. [Gebäude (7 Stück, Reputation-Shop)](#15-gebäude-7-stück-reputation-shop)
16. [Equipment-System](#16-equipment-system)
17. [Gilden & Multiplayer](#17-gilden--multiplayer)
18. [Achievements (60+, 3 Tiers)](#18-achievements-60-3-tiers)
19. [Daily-Reward (7-Tage-Karte)](#19-daily-reward-7-tage-karte)
20. [Lucky-Spin (8 Slots)](#20-lucky-spin-8-slots)
21. [BattlePass (50 Tier, 30-Tage-Saison)](#21-battlepass-50-tier-30-tage-saison)
22. [Live-Events (8 Templates)](#22-live-events-8-templates)
23. [Saisonale Events & Cosmetics](#23-saisonale-events--cosmetics)
24. [Heirlooms (V7)](#24-heirlooms-v7)
25. [Eternal-Mastery](#25-eternal-mastery)
26. [Story-Chapters (38-40)](#26-story-chapters-38-40)
27. [Tutorial / FTUE](#27-tutorial--ftue)
28. [Notifications (8 Trigger)](#28-notifications-8-trigger)
29. [Premium / IAP / Ads](#29-premium--iap--ads)
30. [Economy-Formeln](#30-economy-formeln)
31. [Anti-Cheat](#31-anti-cheat)
32. [Telemetrie-Events](#32-telemetrie-events)
33. [Handwerker-Stadt-Hub-Design](#33-handwerker-stadt-hub-design)
34. [Mega-Projekte (Cathedral, HQ)](#34-mega-projekte-cathedral-hq)
35. [Future / Phase 2](#35-future--phase-2)

---

## 1. Spielprinzip & Loop

### 1.1 Core-Concept

**HandwerkerImperium** ist ein **Idle-Incremental-Management-Game** mit **aktiven Mini-Games** für Bonus-Rewards. Der Spieler erbt Meister Hans' alte Werkstatt und baut ein Imperium aus 10 Handwerks-Werkstätten in einer wachsenden 3D-Stadt auf, hired Arbeiter, nimmt Aufträge an, forscht (57 Nodes), prestiged (7 Tiers) und ascended (nach 3× Legende).

### 1.2 5-Minuten-Core-Loop

```
1. Beobachten → Werkstätten verdienen passiv Geld (visueller Particle-Cash-Flow)
2. Auftrag annehmen → 3 Strategien wählen (Safe/Standard/Risk)
3. Mini-Game spielen → Score bestimmt Reward-Multiplikator
4. Auftrag abschließen → Geld + XP + Reputation
5. Investieren → Workshop-Upgrade, Worker hiren, Forschung starten
6. (Loop)
```

### 1.3 Stunden-Loop

```
1. Werkstätten leveln (Lv 1 → 1500+)
2. Worker rekrutieren (10 Tiers F → Legendary)
3. Forschung (57 Nodes in 4 Branches)
4. Goldscrews sammeln & Boosts kaufen
5. Reputation-Tier-Aufstieg (Beginner → Industry Legend)
6. Daily-Challenges & Live-Orders maxen
```

### 1.4 Wochen/Monate-Loop

```
1. Prestige-Reset (Bronze → Silver → ... → Legende)
2. Master-Tools sammeln (12 Stück, +74% Income)
3. Gilde beitreten, Co-op-Orders, Boss-Kämpfe, Hall-Aufbau
4. BattlePass-Saison komplett (50 Tier)
5. Saison-Themes erleben (Spring/Summer/Autumn/Winter)
6. Mega-Projekt mit Gilde bauen (Cathedral, HQ)
```

### 1.5 Endgame (Monate)

```
1. Nach 3× Legende-Prestige: Ascension freischalten
2. Ascension-Perks ausbauen (6 Perks × 3 Levels = 54 AP)
3. Eternal-Mastery sammeln (+0.5% Income pro Prestige, max 50)
4. Heirlooms maximieren (50 Permanent-Slots)
5. Mega-Projekt-Boni stacken
```

### 1.6 Erwartetes Spieler-Profil (Avalonia-Telemetrie)

| Spielzeit | Werkstätten | Worker | Player-Level | Geld/s | Status |
|-----------|-------------|--------|--------------|--------|--------|
| 30 min | 1-2 | 2-3 | 5 | ~10 € | FTUE-Ende, Plumber-Unlock |
| 2h | 3-5 | 10-15 | 25 | ~500 € | Electrician verfügbar |
| 8h | 5-7 | 25-30 | 60 | ~25.000 € | Painter+Roofer leveling |
| 24h | 8-10 | 40-60 | 100 | ~500.000 € | Bronze-Prestige bereit (Lv 30) |
| 1 Woche | 10 | 80+ | 250 | ~5 Mio. € | Silver/Gold-Prestige-Runs |
| 1 Monat | 10 (Lv 500+) | 150+ | 750 | ~100 Mio. € | Diamant-Prestige + Logistics-Branch |
| 6 Monate | 10 (Lv 1500+) | 200+ | 1200 | ~10 Mrd. € | 3× Legende → Ascension-Unlock |

---

## 2. Setting & Persona "Meister Hans"

### 2.1 Narrative

Der Spieler erbt die alte **Werkstatt seines Großonkels Meister Hans** in einer kleinen Stadt. Meister Hans ist über die Jahre zur lokalen Legende geworden und führt den Spieler als **Mentor-Stimme** (Voice-Cloned, eigene Aufnahmen, EU-konforme Lizenzkette) durch das Spiel.

**Meister Hans erscheint:**
- Im Splash-Screen (Voice-Intro)
- Bei Tutorial-Schritten (10 Lines pro Sprache)
- In Story-Chapters (100 Lines pro Sprache × 6 = 600 Lines)
- Bei Achievement-Unlocks (kurze Voice-Stinger)
- Bei Idle-Tipps (20 Lines, zufällig)
- Bei Live-Events (gesondert pro Event)

**Insgesamt:** ~250 Voice-Lines × 6 Sprachen = **1500 Voice-Files**

**Voice-Strategie (geändert Mai 2026):**
Wir nutzen eine **vorgefertigte ElevenLabs-Standard-Voice** (eine warme, freundliche, leicht karikierte Stimme aus der ElevenLabs-Library) statt Voice-Cloning mit eigener Aufnahme. Vorteile:
- ✅ Schneller Setup (kein Aufnahme-Equipment, keine Sprecher-Freigabe-PDF)
- ✅ Konsistente Qualität in allen 6 Sprachen (ElevenLabs Multilingual v2-Modell)
- ✅ Keine rechtlichen Risiken (Voice ist von ElevenLabs lizenziert)
- ✅ Re-Generation bei Bedarf jederzeit möglich (z.B. neue Story-Chapters)

**Voice-Auswahl-Kriterium:** ElevenLabs-Voice-Library nach "warm, friendly, slightly older male, German native" filtern → 3-5 Kandidaten anhören → eine wählen, die für **alle 6 Sprachen** glaubwürdig klingt (Multi-Lingual-Support).

### 2.2 Stadt-Setting

**Genre:** Stylized Toon-Cartoon (Township/Hay-Day-Stil, siehe [ASSETS_AI.md § 13](ASSETS_AI.md))
**Hauptfarbe:** Amber `#D97706` (Warmth + Handwerk)
**Atmosphäre:** Freundlich, leicht karikiert, gemütlich-meisterhaft
**Stadt-Theme:** Wechselt mit Saisonen (Spring/Summer/Autumn/Winter)

### 2.3 Visualisierungs-Direktive

- **Hub:** Handwerker-Stadt mit allen 10 Werkstätten als 3D-Gebäude
- **Worker:** 3D-Charaktere mit Mecanim-Animationen (Walk/Idle/Work/Mood-States)
- **Mini-Games:** 3D-Erlebnisse pro Werkstatt
- **Audio:** BGM + SFX + Meister-Hans-Voice in 6 Sprachen

---

## 3. Ressourcen & Währungen

### 3.1 Haupt-Währungen

| Ressource | Typ | Quelle | Verwendung |
|-----------|-----|--------|------------|
| **Geld (€)** | `decimal` | Werkstätten passiv, Aufträge, Mini-Games | Upgrades, Worker-Löhne, Materialien, Markt |
| **XP / Player-Level** | `long` / `int` (1-1500) | Aufträge, Mini-Games | Feature-Unlocks, Workshop-Unlocks |
| **Goldschrauben (GS)** | `int` | Achievements, Mini-Games, Ads, IAPs | Premium-Boosts, Slot-Refresh, Heirloom-Slots |
| **Prestige-Punkte (PP)** | `long` | Prestige-Reset | Permanente Boni (Diminishing Returns) |
| **Ascension-Punkte (AP)** | `long` | Ascension (nach 3× Legende) | 6 Perks × 3 Levels = 54 AP |
| **Reputation** | `0-100` (decimal) | Auftrag-Erfolg/Misserfolg | 4 Tiers (Beginner → Industry Legend), Reward-Mult. 0.7-1.5x |
| **Eternal-Mastery (EM)** | `int` | Pro Prestige +1 | +0.5% Income (Cap nach 50 Prestiges) |
| **Saison-Punkte (SP)** | `int` | Saison-Aktivitäten | Saison-Event-Shop |

### 3.2 Crafting-Materialien (5 Affinity-Typen × 4 Tiers)

| Affinität | T1 Beispiele | T2 Beispiele | T3 Beispiele | T4 Beispiele |
|-----------|-------------|-------------|-------------|-------------|
| **Wood** | Wood, Plank | Wooden Beam, Furniture-Component | Luxury Furniture | Villa-Wood-Bauteil |
| **Metal** | Iron, Copper, Steel | Steel Ingot, Circuit Board | Smart Home Module | Skyscraper-Frame |
| **Stone** | Concrete, Clay | Concrete Block, Roof Tile | Roof Structure | Imperium-HQ-Foundation |
| **Art** | Paint, Paper | Paint Can, Blueprint | Artwork | Premium-Artwork |
| **Tech** | Wire, Silicon | Wire Spool, Circuit Board | Smart Home | High-Tech-Module |

### 3.3 Premium-Items

- **Imperium-Pass** (Premium, 4,99 € Lifetime) → +50% Income, +100% GS (Gameplay), keine Ads, etc.
- **Speed-Boost** (1h, 8h, 48h, 7 Tage über IAP-Bundles)
- **Crafting-Booster** (BattlePass-Reward)
- **XP-Booster** (BattlePass-Reward)

---

## 4. Werkstätten (10 Typen)

### 4.1 Vollständige Tabelle (aus Avalonia GameBalanceConstants.cs)

| # | Werkstatt | Unlock-Level | Unlock-Kosten | Income-Mult. | Mini-Game | Farbe (Hex) | Spezial |
|---|-----------|--------------|---------------|--------------|-----------|-------------|---------|
| 1 | **Carpenter** (Schreiner) | 1 | 0 € | 1.0x | Sawing, Planing | `#A0522D` Sienna | Start-Werkstatt |
| 2 | **Plumber** (Klempner) | 5 | 5.000 € | 1.5x | PipePuzzle | `#0E7490` Teal | — |
| 3 | **Electrician** (Elektriker) | 15 | 250.000 € | 2.0x | WiringGame | `#F97316` Orange | — |
| 4 | **Painter** (Maler) | 22 | 2.500.000 € | 2.5x | PaintingGame | `#EC4899` Pink | — |
| 5 | **Roofer** (Dachdecker) | 40 | 10.000.000 € | 3.0x | TileLaying, RoofTiling | `#DC2626` Rot | — |
| 6 | **Contractor** (Baumeister) | 80 | 100.000.000 € | 4.0x | Measuring, Blueprint | `#EA580C` Craft-Orange | — |
| 7 | **Architect** (Architekt) | 1 (mit Bronze-Prestige) | 2.500.000.000 € | 5.0x | DesignPuzzle | `#78716C` Stone-Grau | Prestige-Unlock |
| 8 | **GeneralContractor** (Generalunternehmer) | 1 (mit Gold-Prestige) | 25.000.000.000 € | 7.0x | Inspection | `#FFD700` Gold | Gold-Prestige-Unlock |
| 9 | **MasterSmith** (Meisterschmied) | 500 (mit Platin-Prestige) | 30.000.000.000 € | 3.0x | ForgeGame | `#D4A373` Kupfer | Auto-Produktion **60s** (statt 180s), passive Crafting-Materialien |
| 10 | **InnovationLab** (Innovationslabor) | 750 (mit Diamant-Prestige) | 50.000.000.000 € | 5.0x | InventGame | `#6A5ACD` Violett | Auto-Produktion **120s**, verdoppelt Research-Geschwindigkeit |

### 4.2 Workshop-Sub-Module (3D-Aufbau gemäß ASSETS_AI.md § 7.2)

Jede Werkstatt besteht aus folgenden 3D-Sub-Modulen (siehe ASSETS_AI.md):
- **Building** (Hauptgebäude, sichtbar ab Lv1)
- **Sign** (Werkstatt-Schild über der Tür)
- **Workbench** (Werkbank/Werkzeug-Setup)
- **StorageAddon** (Lager-Anbau, sichtbar ab Lv2)
- **Decoration_Lv{1-5}** (Deko-Layer, ausgetauscht bei Upgrade)

### 4.3 Income-Formel (vollständig)

```
IncomePerSecond = BaseValue × IncomeMultiplier × 1.02^Level
                × (1 + ResearchBonus)             // max +X% via Research-Branches
                × (1 + MasterToolBonus)           // 0% bis +74% (alle 12 Tools)
                × (1 + GuildBonus)                // +1% pro Guild-Research-Level
                × (1 + ManagerBonus)              // pro Workshop
                × (1 + SpecializationBonus)       // 0-30% je nach Spez.
                × (1 + WorkerEfficiencyAggregate)
                × PrestigeMultiplier              // 1.0 bis 64.0× je Tier
                × MilestoneMultiplier             // Level-Boni (Lv 25/50/100/...)
                × EventMultiplier                 // 0.9x bis 2.0x
                × VipMultiplier                   // Premium: 1.5x
                × EternalMasteryMultiplier        // 1.0 + EM × 0.005, max 50
                × SoftCapFactor                   // log-Dämpfung bei extrem hohem Income
```

### 4.4 Milestone-Multiplikatoren (Level-Boni)

| Level | Multiplikator | Hinweis |
|-------|---------------|---------|
| 25 | 1.15x | — |
| 50 | 1.30x | Spezialisierung freischaltbar |
| 100 | 1.45x | — |
| 150 | 1.60x | Cross-Workshop-Crafting freigeschaltet |
| 250 | 1.60x | — |
| 350 | 1.60x | — |
| 400 | 1.40x | **Neu** (entschärft Lv 350→500-Lücke) |
| 500 | 2.00x | MasterSmith-Unlock (mit Prestige) |
| 600 | 1.50x | **Neu** |
| 650 | 1.50x | **Neu** (entschärft tote Zone) |
| 750 | 1.60x | InnovationLab-Unlock |
| 900 | 1.40x | **Neu** |
| 1000 | 3.00x | Hard-Cap Level |

### 4.5 Upgrade-Kosten-Formel

```
Cost(Lv 1)      = 100 €
Cost(Lv 2-500)  = 200 € × 1.07^(Lv-2)
Cost(Lv 501+)   = 200 € × 1.06^(Lv-501)
                × (1 - PrestigeDiscount)    // max -50%
```

### 4.6 Workshop-Slots (Worker pro Werkstatt)

- **Base:** `1 + (WorkshopLevel - 1) / 50`, max 20
- **Extra:** +1 alle 50 Level
- **Max Ad-Bonus:** +3 Slots temporär (über Rewarded Ad)

### 4.7 Spezialisierung (ab Level 50)

Erste Wahl kostenlos, Respec kostet 20 GS (kostenlos bis Level 75):

| Spezialisierung | Bonus | Trade-Off |
|-----------------|-------|-----------|
| **Efficiency** | +30% Einkommen | −1 Worker-Slot |
| **Quality** | +20% Worker-Effizienz | +15% Kosten |
| **Economy** | −25% Kosten | −5% Einkommen |

### 4.8 Rebirth-System

- **Pro 100 Levels: +1 Rebirth-Stern** (max 5 Sterne)
- Stern-Bonus: **+25% Income, +10% Mini-Game-Score**, kosmetisches Glow
- Rebirth setzt Workshop-Level auf 1 zurück, behält Sterne
- **Visualisierung:** Sterne erscheinen physisch über dem 3D-Gebäude

### 4.9 Manager (1 Slot pro Workshop)

- Manager hat eigenes Level (1-100), eigene Affinity, eigene Stats
- **Bonus:** bis +30% Income (Lv 100)
- **Kosten:** ~5x Worker-Lohn (proportional)
- **Prestige-Reset:** Manager-Level auf 1 (ab Tier 7 Legende)

---

## 5. Arbeiter (10 Tiers)

### 5.1 Vollständige Tier-Tabelle

| Tier | Index | Rarity | Name | Min-Eff | Max-Eff | Hourly-Wage | Hire-Cost | Hire-GS | Unlock-Lv | Aura-Bonus | Lv-Resistance | Farbe |
|------|-------|--------|------|---------|---------|-------------|-----------|---------|-----------|------------|---------------|-------|
| **F** | 0 | Common | Auszubildender | 0.30x | 0.50x | 5 € | 50 € | 0 | 1 | — | 0.00x | `#9E9E9E` Grey |
| **E** | 1 | Common | Geselle | 0.50x | 0.80x | 9 € | 200 € | 0 | 1 | — | 0.10x | `#4CAF50` Green |
| **D** | 2 | Uncommon | Facharbeiter | 0.75x | 1.25x | 16 € | 1.000 € | 0 | 8 | — | 0.20x | `#2196F3` Blue |
| **C** | 3 | Uncommon | Vorarbeiter | 1.10x | 1.90x | 28 € | 5.000 € | 0 | 15 | — | 0.30x | `#9C27B0` Purple |
| **B** | 4 | Rare | Meister | 1.70x | 2.80x | 50 € | 25.000 € | 0 | 25 | — | 0.40x | `#FFC107` Gold |
| **A** | 5 | Rare | Großmeister | 2.50x | 4.20x | 90 € | 100.000 € | 20 | 35 | — | 0.55x | `#F44336` Red |
| **S** | 6 | Epic | Star-Handwerker | 3.80x | 6.00x | 160 € | 500.000 € | 60 | 45* | +5% | 0.70x | `#FF9800` Orange |
| **SS** | 7 | Epic | Industrie-Veteran | 5.50x | 9.00x | 280 € | 2.000.000 € | 120 | 100* | +8% | 0.80x | `#E040FB` Pink |
| **SSS** | 8 | Legendary | Halbgott der Werkbank | 8.50x | 14.00x | 500 € | 10.000.000 € | 300 | 250* | +12% | 0.90x | `#7C4DFF` DeepPurple |
| **Legendary** | 9 | Mythic | Hephaestus | 13.00x | 22.00x | 900 € | 50.000.000 € | 750 | 500* | +20% | 1.00x | `#FFD700` Gold |

*\*Braucht zusätzlich Research-Unlock (mgmt_10 für S-Tier, mgmt_20 für Legendary)*

**Level-Fit-Faktor** (Penalty bei zu hohem Workshop-Level für niedrigen Tier):
- Penalty Step: alle 30 Workshop-Level
- Penalty pro Step: −2%
- Minimum-Fit-Faktor: 0.20x
- **Aura-Bonus** der S/SS/SSS/Legendary-Worker reduziert Penalty entsprechend

### 5.2 Worker-Stats

| Stat | Range | Wirkung |
|------|-------|---------|
| **Level** | 1-1000 | +3% Effizienz pro Level (gilt nur für eingesetzte Werkstatt) |
| **XP** | 0-∞ | XP-Bedarf pro Level = Level × 200 |
| **Mood** | 0-100 | Linear 0-1 Faktor auf Effizienz |
| **Fatigue** | 0-100 | Linear 1-0.5 Faktor auf Effizienz |
| **Personality** | Random | Modifiziert Mood-Volatilität (Calm/Volatile/Stable) |
| **Affinity (Material)** | 1 von 5 Tiers | Wood/Metal/Stone/Art/Tech (siehe § 12) |
| **Specialization** | 1 von 3 | Stärke/Geschick/Geschwindigkeit |
| **Talent-Stars** | 1-5 | +5% Effizienz pro Star (max +25%) |

### 5.3 Worker-Mood-System

- **Initial Mood:** 80/100
- **Happy Threshold:** 80+
- **Neutral Threshold:** 50+
- **Critical Threshold:** 20−
- **Mood Decay pro Stunde:** −3.0
- **Erholung in Ruhephase:** +1 Mood/Min

**Visualisierung (3D):** 4 Gesichtstexturen (Happy/Neutral/Sad/Frustrated) auf separatem UV-Set, Material-Slot-Swap synchronisiert mit Animator-State.

### 5.4 Worker-Fatigue-System

- **Fatigue Increase pro Stunde:** +12.5
- **Fatigue-Exhausted:** 100
- **Rest Hours Needed:** 4h (ohne Kantine-Bonus)
- **Kantine** (siehe § 15): Reduktion 50%/55%/60%/70%/80% je Level 1-5

### 5.5 Praktikanten

- **Kosten:** Kostenlos
- **Max gleichzeitig:** 2
- **Promotion:** Nach 86.400 Ticks (~24h Spielzeit) zu F-Tier-Worker

### 5.6 Worker-Training

3 Training-Types, jeweils 5 Stufen (Stärke / Geschick / Geschwindigkeit):

| Stufe | Bonus pro Type |
|-------|----------------|
| 1 | +5% |
| 2 | +10% |
| 3 | +15% |
| 4 | +20% |
| 5 | +25% |

- **Training-Kosten:** 2× Hourly-Wage pro Stunde
- **Training-XP pro Stunde:** 50 XP
- **Trainings-Zentrum** (Gebäude) gibt Multiplikator 1.0x bis 6.5x

### 5.7 Material-Affinity-Matching (V7)

- 20% Drop-Chance pro Affinity beim Hiring (gleichverteilt 5×20%)
- Alte Worker werden bei Migration deterministisch via WorkerId-Hash zugewiesen
- **Crafting-Speed-Bonus:** bis +20% wenn alle Worker einer Werkstatt die Output-Material-Affinität matchen
- Anteilig: 3 von 5 Workern Match = +12% Speed

### 5.8 Worker-Markt

- **Refresh-Zyklus:** 15 Minuten, generiert 5 neue Worker
- **Rarity-Verteilung (ohne Research-Boosts):**
  - 60% F-E
  - 25% D-C
  - 10% B-A
  - 4% S-SS
  - 1% SSS-Legendary
- **Premium-Bonus:** +30% bessere Drop-Raten
- **Ad-Refresh:** 1× pro 4h kostenlos via Rewarded Ad

### 5.9 Worker-Auktionen (Gilden-Feature)

- **3 Auktionen pro Gilde pro Tag**
- **Startgebot:** 50% Worker-Hire-Kosten
- **Mindestgebot-Steigerung:** +10%
- **Dauer:** 6h
- **NPC-Bots:** 35% Chance pro Tick, kompetitives Bieten
- **HMAC-signiert** (Anti-Cheat)
- **Refund** bei Überbieten (idempotent)

### 5.10 3D-Visualisierung (gemäß ASSETS_AI.md § 5+9)

- 10 Basis-Modelle (5 m/w-Paare) + ~120 Skin-Varianten via Material-Color-Swap
- **Mecanim-Animator-States:** Idle (4 Mood-Variants), Walking, Hammering, Sawing, Painting, Frustrated-Outburst, Happy-Cheer
- **Affinity-Props an Hand-Bone:** Hammer (Wood), Schraubenschlüssel (Metal), Maurer-Kelle (Stone), Pinsel (Art), Tablet (Tech)
- Worker laufen physisch durch die 3D-Stadt (NavMesh)

---

## 6. Aufträge (6 Types + 3 Strategien)

### 6.1 Order-Types

| Type | Index | Tasks | Reward-Mult. | XP-Mult. | Unlock-Lv | Deadline | Besonderheit |
|------|-------|-------|--------------|----------|-----------|----------|-------------|
| **Quick** | 0 | 1 | 0.6x | 0.5x | 1 | — (sofortig) | Schnell, niedriges Risiko |
| **Standard** | 1 | 2-3 | 1.0x | 1.0x | 1 | 30min | Basis-Auftrag |
| **Large** | 2 | 4-6 | 1.8x | 2.0x | 10 | 2h | Mehr Worker erforderlich |
| **Cooperation** | 4 | 3 (Cross-Workshop) | 2.5x | 3.0x | 15, ≥2 Werkstätten | 4-12h | Multi-Workshop, Gilden-Co-op |
| **Weekly** | 3 | 10 | 3.0x | 3.0x | 20 | 7 Tage | Sehr hoch belohnt |
| **MaterialOrder** | 5 | 0 (keine MiniGames) | 1.8x | 1.5x | 50 | 4h | Verbraucht Crafting-Inventar |

**Parallel-Limit:** Max 3 Aufträge gleichzeitig.

### 6.2 Order-Strategien

| Strategy | Reward-Mult | XP-Mult | Zone-Mult | Speed-Mult | Zeit-Mult | Hard-Fail | Rep-Penalty |
|----------|-------------|---------|-----------|------------|-----------|-----------|-------------|
| **Safe** | 0.75x | 0.75x | +50% (größere Sweet-Spots) | 0.7x | +30% | Nein | 0 |
| **Standard** | 1.0x | 1.0x | 1.0x | 1.0x | 1.0x | Nein | 0 |
| **Risk** | 2.0x | 1.75x | -50% (kleinere Sweet-Spots) | 1.3x | -30% | Ja: 0 € + Rep -10 | -10 |

### 6.3 Live-Orders (VIP)

- **Spawn:** 25-Tick-Intervall, 50% Chance, max 5 gleichzeitig
- **Deadline:** 45-180 Sekunden
- **VIP (5% Spawn-Chance):**
  - 3x Reward
  - 2.5x XP
  - kürzere Deadline
- **Pause-Limit:** 5 Minuten kumulativ pro Spieler

### 6.4 Material-Order (V7)

- **Reward-Multiplier:** 1.8x
- **XP-Multiplier:** 1.5x
- **Max pro Tag:** 5
- **Deadline:** 4h
- **Cross-Workshop-Unlock:** Level 100

### 6.5 Material-Offer in Orders (V7)

Ab Level 30 spawnen Orders mit Material-Offer (35% Chance):

| Order-Type | Bonus-Reward |
|-----------|--------------|
| Quick | +25% |
| Standard | +30% |
| Large | +40% |
| Cooperation | +50% |
| Weekly | +60% |

### 6.6 Reputation-Tier-Multiplikator (Reward)

| Reputation-Tier | Reward-Mult |
|-----------------|-------------|
| Beginner (0-30) | 0.7x |
| City Known (31-60) | 1.0x |
| Region Star (61-80) | 1.25x |
| Industry Legend (81-100) | 1.5x |

### 6.7 Soft-Caps

- **Order-Reward-Multiplier:** max 10.0x (kumuliert alle Bonusse)
- **Crafting-Sell-Multiplier:** Soft-Cap 8.0x, Hard-Cap 12.0x

### 6.8 3D-Visualisierung

- **Live-Orders als animierte 3D-Kunden-NPCs** die durch die Stadt zur Werkstatt laufen
- VIP-Kunden mit goldenem Glow + Krone-Icon
- Strategy-Wahl via 3D-Risiko-Meter (Tachometer-UI)
- Reputation-Tier-Up: 3D-Trophäen-Cinematic + Stadt-Atmosphäre wird "schicker"

---

## 7. Mini-Games (13 Typen)

### 7.1 Vollständige Tabelle

| Typ | Index | Werkstatt | Mechanik | Master-Tool | Perfects-Unlock |
|-----|-------|-----------|----------|-------------|-----------------|
| **Sawing** | 0 | Carpenter | Timing: Sägemehl-Kontrolle | Golden Hammer (Saw-Slot) | 30 (Premium: 15) |
| **Planing** | 1 | Carpenter | Timing: Hobelmechanik | — | — |
| **PipePuzzle** | 2 | Plumber | Puzzle: Rohrverlauf (BFS) | PipeWrench | 30 (Premium: 15) |
| **WiringGame** | 3 | Electrician | Drag-Drop: Kabel verbinden | Screwdriver | 30 (Premium: 15) |
| **PaintingGame** | 4 | Painter | Swipe: Malen ohne Übermalerei | Paintbrush | 30 (Premium: 15) |
| **TileLaying** | 5 | Roofer | Timing: Dachziegel-Platzierung | — | — |
| **Measuring** | 6 | Contractor / Carpenter | Timing: Längen messen | — | — |
| **RoofTiling** | 7 | Roofer | Pattern: Musterkorrektheit | Hammer (Tile-Slot) | 30 (Premium: 15) |
| **Blueprint** | 8 | Contractor | Memory: Bauschritte merken | SpiritLevel | 30 (Premium: 15) |
| **DesignPuzzle** | 9 | Architect | Puzzle: Raumeinteilung | Compass | 30 (Premium: 15) |
| **Inspection** | 10 | GeneralContractor | Suchbild: 8 gut / 8 defekt | Magnifier | 30 (Premium: 15) |
| **ForgeGame** | 11 | MasterSmith | Timing: Temperaturzone | — | — |
| **InventGame** | 12 | InnovationLab | Sequenz: Bauteile zusammensetzen | — | — |

### 7.2 Rating-System

| Rating | Score | Reward-Multiplikator |
|--------|-------|----------------------|
| **Perfect** | 95-100% | 1.5x |
| **Good** | 75-94% | 1.0x |
| **OK** | 50-74% | 0.75x |
| **Miss** | <50% | 0.5x (bei Risk: 0% + Rep-Penalty) |

### 7.3 Auto-Complete-Ticket

- Nach **30 Perfects** (Premium: 15) in einem Mini-Game → Auto-Complete-Ticket
- Mit Ticket: Spiel überspringen, Reward = 1.0x (Good)

### 7.4 Mini-Game-3D-Konzepte (gemäß ASSETS_AI.md)

| Mini-Game | 3D-Konzept |
|-----------|------------|
| **Sawing** | 3D-Holzbrett mit Procedural-Maserung, Säge folgt Touch, GPU-Splitter-Particles, Sound reagiert auf Druck |
| **Planing** | Hobel über 3D-Holz-Werkstück, Späne-Particles |
| **PipePuzzle** | 3D-Rohrleitungs-Anlage, Touch-Drehen, Wasser-Particle-System |
| **WiringGame** | 3D-Schaltkreis-Board, Funken-FX bei korrektem Timing |
| **PaintingGame** | 3D-Wand, Pinsel-Spuren via Render-Texture, Farb-Tropfen mit Physics |
| **TileLaying** | 3D-Bodenfliesen mit Klebstoff-Particles |
| **Measuring** | 3D-Werkstück, Laser-Maßband, Präzisions-Score |
| **RoofTiling** | Echtes 3D-Dach, Ziegel-Drag-and-Drop mit Physik |
| **Blueprint** | 3D-Bauplan-Tisch, Pläne werden physisch umgedreht |
| **DesignPuzzle** | 3D-Raum-Layout-Editor, Möbel-Drag-and-Drop mit Echtzeit-Vorschau |
| **Inspection** | 3D-Gebäude-Innenansicht, animierte Lupe scannt Wände, Mängel als rote Glyphen |
| **ForgeGame** | 3D-Schmiede mit Procedural-Feuer-Shader, Hammer-Schlag mit Funken-Particles |
| **InventGame** | 3D-Labor mit verbindbaren Modulen, Particle-Strom zwischen Verbindungen |

---

## 8. Forschung (57 Nodes, 4 Branches)

### 8.1 Branch-Übersicht

| Branch | Nodes | Fokus |
|--------|-------|-------|
| **Tools** | 20 | Werkstatt-Speed, Effizienz, Bau-Kosten |
| **Management** | 20 | Worker-Lohn, Worker-Tiers-Unlock, Manager-Boni |
| **Marketing** | 5 | Reputation, Event-Boni, Kunden-Loyalität |
| **Logistics** | 12 | Lager, Crafting, Markt, Auto-Sell |

### 8.2 Tools-Branch (20 Nodes)

| ID | Name | Kosten | Dauer | Effekt |
|----|------|--------|-------|--------|
| tools_01 | BetterSaws | 500 € | 10 min | +5% Effizienz Carpenter |
| tools_02 | ... | ... | ... | ... |
| tools_20 | **EternalForge** (Capstone) | 100 Mrd € | 168h | +30% Effizienz alle Werkstätten, +25% Ascension-Bonus |

(Vollständige Liste in `ScriptableObjects/Research/Tools/`)

### 8.3 Management-Branch (20 Nodes)

| ID | Name | Kosten | Dauer | Effekt |
|----|------|--------|-------|--------|
| mgmt_01 | HrBasics | 500 € | 10 min | −5% Lohnkosten |
| mgmt_10 | EliteRecruitment | 8 Mio € | 24h | **S-Tier Worker freischalten** |
| mgmt_20 | **LegendaryLeader** (Capstone) | 100 Mrd € | 168h | +3 Worker-Slots, −20% Löhne, +Level-Resistance |

### 8.4 Marketing-Branch (5 Nodes)

Fokus: Reputation-Gain, Event-Reward-Boost, Customer-Loyalty.

### 8.5 Logistics-Branch (12 Nodes — V7)

| # | ID | Kosten | Dauer | Effekt |
|---|-----|--------|-------|--------|
| 1 | logi_01 | 50 K € | 30 min | +5 Warehouse-Slots |
| 2 | logi_02 | 200 K € | 1h | Stack-Limit ×2 |
| 3 | logi_05 | 500 K € | 2h | **Markt freischalten** |
| 4 | logi_04 | 1.5 Mio € | 3h | +10 Warehouse-Slots |
| 5 | logi_08 | 4 Mio € | 6h | +50% Lieferanten-Material-Bonus |
| 6 | logi_07 | 10 Mio € | 8h | **Auto-Sell-Regeln freischalten** |
| 7 | logi_10 | 25 Mio € | 12h | +20% Crafting-Speed |
| 8 | logi_11 | 60 Mio € | 16h | Stack-Limit ×5 (kombiniert ×10) |
| 9 | logi_09 | 150 Mio € | 24h | **Tier-4-Rezepte freischalten** |
| 10 | logi_03 | 400 Mio € | 24h | +25 Warehouse-Slots (Premium) |
| 11 | logi_12 | 1 Mrd € | 24h | **Erbstücke überleben Prestige** |
| 12 | logi_06 | 5 Mrd € | 24h | +30% Crafting-Speed + 25 Slots |

### 8.6 Research-Effekt-Cache

Alle Effekte werden in `ResearchEffectCache` aggregiert:
- `TotalIncomeMultiplier`
- `TotalCostReduction`
- `TotalWorkerXpBonus`
- `TotalMiniGameScoreBonus`
- `AutoCollectEnabled`
- `AutoAcceptEnabled`
- `CraftingSpeedBonus`
- `MarketUnlocked`
- `AutoSellUnlocked`
- `Tier4RecipesUnlocked`
- `HeirloomsSurvivePrestige`

Cache wird invalidiert bei `ResearchCompletedEvent` und `StateLoadedEvent`.

### 8.7 3D-Visualisierung

- Forschungsbaum als **3D-Skill-Tree** in eigener Sub-Szene
- Aktive Forschung: Particle-Strom zwischen Nodes
- Abgeschlossen: Goldene Aura
- Cinemachine-Kamera zoomt zwischen Branches

---

## 9. Prestige (7 Tiers + Ascension)

### 9.1 Prestige-Tier-Tabelle (vollständig)

| Tier | Index | Player-Level-Anforderung | Voraussetzung | PP-Multiplikator | Income-Bonus | Level-Reset | Preservation |
|------|-------|--------------------------|---------------|------------------|--------------|-------------|--------------|
| **Bronze** | 1 | 30 | — | 1.0x | +20% | Ja | Achievements, Premium, Settings, Tutorial |
| **Silver** | 2 | 100 | 1× Bronze | 2.0x | +35% | Ja | + dito |
| **Gold** | 3 | 250 | 1× Silver | 4.0x | +50% | Ja | + Research |
| **Platin** | 4 | 500 | 2× Gold | 8.0x | +100% | Ja | + Prestige-Shop-Items |
| **Diamant** | 5 | 750 | 2× Platin | 16.0x | +200% | Ja | + Master-Tools |
| **Meister** | 6 | 1000 | 2× Diamant | 32.0x | +400% | Ja | + Gebäude (Level→1), Equipment |
| **Legende** | 7 | 1200 | 3× Meister | 64.0x | +800% | Ja | + Manager (Level→1), Top-3 Worker/WS |

### 9.2 PP-Berechnung

```
PP = floor(sqrt(CurrentRunMoney / 100_000))
```

### 9.3 Diminishing Returns

```
Effektive_Bonus = base_bonus / (1 + 0.2 × same_rebirth_count)
```

Nach 5× Same-Tier-Prestiges nur noch 50% des Basis-Bonus.

### 9.4 Meilensteine (permanent, kumulativ)

| Prestige-Count | GS-Reward |
|----------------|-----------|
| 1 | +10 GS |
| 5 | +20 GS |
| 10 | +35 GS |
| 25 | +50 GS |
| 50 | +75 GS |
| 100 | +100 GS |
| Alle 7 danach | +5 GS (wiederholbar) |

### 9.5 Prestige-Bonus-PP (zusätzlich zur Tier-Mult)

| Bonus | Wert |
|-------|------|
| Per 10 Perfect Ratings | +1 PP (max 5) |
| Full Research-Branch | +2 PP (max 6) |
| All Buildings auf Max | +1 PP |
| Per Level über Tier-Min | +0.05 PP (max 5) |

### 9.6 Challenges (max 3 parallel, additiv)

| Challenge | Effekt | PP-Bonus |
|-----------|--------|----------|
| **Spartaner** | Max 3 Worker | +45% |
| **OhneForschung** | Keine Research erlaubt | +30% |
| **Inflationszeit** | 2× Upgrade-Kosten | +25% |
| **SoloMeister** | Nur 1 Workshop aktiv | +50% |
| **Sprint** | Kein Offline-Income | +35% |
| **KeinNetz** | Keine Lieferanten | +20% |

### 9.7 Ascension-System (nach 3× Legende)

**Voraussetzung:** 3× Legende-Prestige erreicht

**Effekte:**
- 6 permanente Perks (je MaxLevel 3, 54 AP gesamt)
- +2 AP pro Ascension-Stufe
- Vollständiger Reset (Geld, Workshop-Level, Worker, Research, Prestige-Daten)
- **Alle Ascension-Perks bleiben erhalten**
- Alle Run-Heirlooms gehen in Permanent-Heirlooms über

**Die 6 Ascension-Perks** (jeweils 3 Level):
- *(Vollständige Liste in ScriptableObjects/Ascension/, zu erstellen während Implementation)*

### 9.8 Prestige-Cinematic (Unity-Neuerung gemäß ASSETS_AI.md)

**Timeline-Sequenz (~12s):**
- Phase 1: Geld zerspringt in Sterne (GPU-Particles), Music duckt
- Phase 2: Badge schwebt nach oben mit Bloom-Effekt
- Phase 3: Multiplikator-Zahl zoomt mit Distortion-Shader
- Phase 4: Belohnungs-Karte fliegt zum Spieler
- Auto-Dismiss nach 12s, Skip-Button verfügbar

**5 Hero-Cinematic-Assets** (siehe ASSETS_AI.md § 12) werden via **Rodin Gen-2.5** Cloud-Polish erstellt.

---

## 10. Crafting (30 Rezepte, 4 Tiers)

### 10.1 Tier-1-Rezepte (10 Workshops × 1 Recipe = 10)

| Workshop | Recipe-ID | Inputs | Dauer | Output-Value |
|----------|-----------|--------|-------|--------------|
| Carpenter | wood_beam | 5× Wood | 5 min | 50 € |
| Plumber | copper_pipe | 3× Copper | 5 min | 60 € |
| Electrician | wire_spool | 2× Wire | 5 min | 70 € |
| Painter | paint_can | 4× Paint | 5 min | 80 € |
| Roofer | roof_tile | 6× Clay | 5 min | 90 € |
| Contractor | concrete_block | 8× Concrete | 5 min | 100 € |
| Architect | design_blueprint | 3× Paper | 5 min | 120 € |
| GeneralContractor | office_furniture | 5× Wood + 2× Metal | 10 min | 150 € |
| MasterSmith | steel_ingot | 5× Iron | 10 min | 200 € |
| InnovationLab | circuit_board | 3× Copper + 2× Silicon | 10 min | 250 € |

### 10.2 Tier-2 (Unlock Lv 150)

10 weitere Rezepte (z.B. Wooden Beam → Furniture-Component, etc.)

### 10.3 Tier-3 (Unlock Lv 320)

10 weitere Rezepte (Luxury Furniture, Smart Home, Premium-Artwork)

### 10.4 Tier-4 (Unlock Lv 320 + logi_09, NUR auf GeneralContractor)

**3 Hero-Rezepte:**

| Recipe | Inputs | Dauer | Output-Value |
|--------|--------|-------|--------------|
| **Villa** | 5× luxury_furniture + 3× smart_home + 2× roof_structure + 1× artwork | 30 min | 2.5 Mio € |
| **Skyscraper** | 5× skyscraper_frame + 3× bathroom + 3× smart_home + 2× artwork | 40 min | 4.0 Mio € |
| **Imperium-HQ** | je 2× aller 10 T3-Produkte (außer 1× general_contract) | 60 min | 5.0 Mio € |

### 10.5 Auto-Produktion

| Tier | Interval (Default) | MasterSmith | InnovationLab |
|------|---------------------|-------------|---------------|
| T1 | 180s (Offset 90s) | 60s | 60s |
| T2-T4 | 360s (Offset 270s) | — | 120s |

**Premium:** -50% Intervalle für T1.

### 10.6 Crafting-Sell-Price-Formel

```
Price = BaseValue × (1 + log₂(1 + Level / 25))
      × CraftingSellMultiplier     // Premium +50%
      × GuildMegaProjectBonus      // bis +20%
      × MaterialAffinityBonus      // bis +20% bei Match aller Worker
```

**Log-Divisor:** 15.0 (für die `log₂`-Formel)

### 10.7 Cross-Workshop-Inputs (V7)

Ab **Level 100** freigeschaltet: Rezepte können Materialien aus mehreren Werkstätten verwenden.

### 10.8 3D-Visualisierung

- Crafting-Tisch in jeder Werkstatt sichtbar (animiert während Produktion)
- T4-Crafting: Cathedral/Skyscraper/Imperium-HQ als **Mega-Projekte** mit 5 Bauphasen (siehe ASSETS_AI.md § 12, "Mega-Projekte: 2 + 5 Bauphasen")

---

## 11. Lager (Warehouse V7)

### 11.1 Lager-Stats

| Stat | Start | Max (mit Research) |
|------|-------|---------------------|
| **Slots** | 20 | 200 |
| **Stack-Limit** | 50 | ~5000 (×10 via Research) |

### 11.2 Upgrade-Kosten

- **Base:** 50.000 €
- **Exponent:** 1.5x pro Slot-Level

### 11.3 Logistics-Research-Boni

| Node | Effekt |
|------|--------|
| logi_01 | +5 Slots |
| logi_02 | Stack-Limit ×2 |
| logi_04 | +10 Slots |
| logi_03 | +25 Slots (Premium) |
| logi_11 | Stack-Limit ×5 (zusammen ×10) |
| logi_06 | +25 Slots + Crafting-Speed |

### 11.4 Auto-Sell-Regeln (Unlock via logi_07)

Bei Stack-Overflow:
- Automatischer Verkauf zum aktuellen Marktpreis
- Konfigurierbar pro Material (verkaufen/behalten/Schwelle)
- Telemetrie-Event `warehouse_full_pause` bei Overflow ohne Auto-Sell

### 11.5 3D-Visualisierung

- Lager als **3D-Regalsystem** in jeder Werkstatt
- Animiert auffüllend (Particle-Drop in Slots)
- Material-Icons als TextMeshPro-SpriteAsset

---

## 12. Markt & Material-Affinität

### 12.1 Markt-System (Unlock via logi_05)

**Tagespreis-Sinus-Welle:**

```
Price(material, t) = BaseValue × (1 + 0.5 × sin(2π × t / 24h + offset))
```

→ Preise schwanken ±50% pro 24h-Zyklus, jeder Material-Typ hat eigenen Offset.

**Premium-Feature:** **Markt-Insider-Heatmap** zeigt Preistrendlinien (Premium-only).

### 12.2 Event-Modulation

- Events können Preise verändern (+/− 100%)
- "Marktcrash" Event: alle Preise −30%
- "Booming Economy" Event: alle Preise +50%

### 12.3 Buy & Sell

- **Buy:** Sofort, aktueller Marktpreis
- **Sell:** Sofort, Strafzoll 5% (Premium: 0%)

### 12.4 Material-Affinität-Enum

```csharp
public enum MaterialAffinity
{
    Wood,    // Holz
    Metal,   // Metall
    Stone,   // Stein
    Art,     // Kunst (Paint, Paper, Artwork)
    Tech     // Technologie (Wire, Silicon, Circuits)
}
```

### 12.5 Worker-Affinity-Matching

- Worker bekommt zufällige Affinity beim Hire (5×20% gleichverteilt)
- Match-Bonus: Crafting-Speed-Bonus
  - 5/5 Match: +20%
  - 4/5 Match: +16%
  - 3/5 Match: +12%
  - 2/5 Match: +8%
  - 1/5 Match: +4%
  - 0/5 Match: 0%

### 12.6 3D-Visualisierung

- Markt als **animiertes Liniendiagramm** im UI Toolkit
- Preise springen mit Tween-Animation
- Markttafel-Glow bei "günstigen" Preisen

---

## 13. Reputation & Tier-System

### 13.1 Tier-Tabelle

| Tier | Range | Reward-Mult | Hysterese-Up | Hysterese-Down |
|------|-------|-------------|--------------|----------------|
| **Beginner** | 0-30 | 0.7x | — | — |
| **City Known** | 31-60 | 1.0x | 31 | 28 |
| **Region Star** | 61-80 | 1.25x | 61 | 58 |
| **Industry Legend** | 81-100 | 1.5x | 81 | 78 |

**Hysterese-Logik:** Aufstieg bei Erreichen des Up-Werts, Abstieg erst bei Down-Wert (verhindert Flickern).

### 13.2 Reputation-Quellen

- **Order erfolgreich:** +0.1 bis +1.0 Rep (je nach Type & Strategy)
- **Order failed (Risk Hard-Fail):** -10 Rep
- **Ausstellungsraum-Gebäude:** +0.5 Rep/Tag pro Level

### 13.3 3D-Visualisierung

- **Tier-Up-Celebration:** 3D-Trophäen-Cinematic (siehe ASSETS_AI.md § 9)
- Stadt-Atmosphäre wechselt (mehr NPCs, hellere Beleuchtung, lebendiger)

---

## 14. Master-Tools (12 Artefakte)

### 14.1 Vollständige Liste

| ID | Name | Seltenheit | Bonus | Unlock-Bedingung |
|----|------|-----------|-------|------------------|
| mt_golden_hammer | Golden Hammer | Common | +2% Income | Workshop Lv 75 |
| mt_diamond_saw | Diamond Saw | Common | +2% Income | Workshop Lv 150 |
| mt_titanium_pliers | Titanium Pliers | Common | +3% Income | 150 Aufträge |
| mt_brass_level | Brass Level | Common | +3% Income | 300 Mini-Games |
| mt_silver_wrench | Silver Wrench | Uncommon | +5% Income | Workshop Lv 300 |
| mt_jade_brush | Jade Brush | Uncommon | +5% Income | 75 Perfect Ratings |
| mt_crystal_chisel | Crystal Chisel | Uncommon | +5% Income | Bronze-Prestige |
| mt_obsidian_drill | Obsidian Drill | Rare | +7% Income | Workshop Lv 750 |
| mt_ruby_blade | Ruby Blade | Rare | +7% Income | Silver-Prestige |
| mt_emerald_toolbox | Emerald Toolbox | Epic | +10% Income | Workshop Lv 1500 |
| mt_dragon_anvil | Dragon Anvil | Epic | +10% Income | Gold-Prestige |
| mt_master_crown | Master Crown | Legendary | +15% Income | Alle 11 anderen |

**Gesamtbonus alle 12:** +74% Income

### 14.2 3D-Visualisierung

- Jedes Master-Tool als 3D-Modell mit **Emissive-Shader** (Glow gemäß Seltenheit)
- Master-Crown-Unlock: Cinematic-Sequenz, 3D-Krone rotiert mit Bloom
- Anzeige im Hub als Trophäen-Vitrine

---

## 15. Gebäude (7 Stück, Reputation-Shop)

### 15.1 Tabelle

| Gebäude | Effekt pro Level | Max-Lv | Kosten-Basis | Exponent | Besonderheit |
|---------|------------------|--------|--------------|----------|--------------|
| **Kantine** | +1.0 Stimmung/h | 5 | 50.000 € | 2 | Ruhezeit-Reduktion: 0/50%/55%/60%/70%/80% |
| **Lager** | −Material-Kosten | 5 | 75.000 € | 2 | Reduktion: 0/15%/25%/35%/45%/50% |
| **Fuhrpark** | +Auftrags-Reward | 5 | 100.000 € | 2 | Bonus: 0/20%/30%/40%/50%/60% |
| **Ausstellungsraum** | +Ruf/Tag | 5 | 60.000 € | 2 | +0.5 Reputation pro Level |
| **Trainings-Zentrum** | +Training-Speed | 5 | 150.000 € | 2 | Multiplikator: 1.0x/2.0x/3.0x/4.0x/6.5x |
| **Werkstatt-Erweiterung** | +Worker-Slot | 5 | 200.000 € | 2 | +1 bis +5 Worker-Slots |
| **Tech-Hub** | +Research-Speed | 5 | 100.000 € | 2 | Speed-Multiplikator pro Level |

### 15.2 3D-Visualisierung

Gebäude erscheinen als **physische 3D-Strukturen in der Handwerker-Stadt** (siehe § 33).

---

## 16. Equipment-System

### 16.1 Rarity-Tiers

- Common (grau)
- Uncommon (grün)
- Rare (blau)
- Epic (lila)
- Legendary (gold)

### 16.2 Slots (pro Worker)

- Helm
- Werkzeug
- Stiefel

### 16.3 Stats

Pro Equipment:
- +X% Effizienz (5-25%)
- +X% Mini-Game-Score (5-15%)
- +X% Lohn-Reduktion (0-20%)

### 16.4 Erwerb

- BattlePass-Reward (Tier 15+)
- Boss-Drop
- Lucky-Spin (selten)
- IAP-Bundle (selten)

### 16.5 Equipment-Preservation

- Bei **Meister-Prestige (Tier 6):** Equipment bleibt erhalten

---

## 17. Gilden & Multiplayer

### 17.1 Gilden-Struktur

| Feature | Beschreibung |
|---------|-------------|
| **CRUD** | Erstellen, Beitreten, Verlassen, Auflösen |
| **Max Mitglieder** | 30 (V1), +5/+5/+10 via Research |
| **Tag** | 6-stellig, eindeutig (Cloud-Function-Validierung) |
| **Invite-Codes** | 6-stellig alphanumerisch |
| **Rollen** | Leader, Co-Leader, Member |
| **Wochenziele** | 3 pro Woche, kollektiv |
| **Verlassen-Cooldown** | 24h |

### 17.2 Guild-Research (18 Nodes, 6 Kategorien)

| Kategorie | Nodes | Kosten-Range | Beispiel-Effekt |
|-----------|-------|--------------|-----------------|
| **Infrastruktur** | 3 | 50M–5B € | Max Mitglieder +5/+5/+10 |
| **Wirtschaft** | 4 | 10M–10B € | +Einkommen, −Kosten, +Auftragsbelohnungen |
| **Wissen** | 3 | 25M–2.5B € | +XP, +Worker-Effizienz, +MiniGame-Belohnungen |
| **Logistik** | 3 | 75M–3B € | +Auftragsslot, +Order-Qualität, +Belohnungen |
| **Arbeitsmarkt** | 3 | 150M–5B € | +Worker-Slot, +Training-Speed, −Ermüdung |
| **Meisterschaft** | 2 | 500M–7.5B € | +Research-Speed, +Prestige-Punkte |

### 17.3 Co-op-Aufträge

- 1 aktiver Co-op-Auftrag pro Gilde
- 5-15 Tasks, alle Mitglieder können beitragen
- Score-Tracking pro Mitglied
- Reward-Verteilung gemäß Beitrag
- **HMAC-signiert**, atomar via Firebase PATCH
- Reward-Idempotenz via `ClaimedCoopOrderIds`

### 17.4 Guild-Bosse (6 Stück)

| Boss | HP | Belohnung-Pool |
|------|-----|---------------|
| **Dragon** | 10.000 | Premium-Materialien |
| **Phoenix** | 8.000 | Equipment |
| **Leviathan** | 12.000 | Goldscrews |
| **Titan** | 15.000 | Heirloom-Material |
| **Kraken** | 7.000 | XP-Booster |
| **Chimera** | 9.000 | Saison-Punkte |

Boss-Tick alle 60s (Offset 20s). Schaden via Aufträge + Mini-Game-Erfolge. **HMAC-signiert.**

### 17.5 Guild-Hall (10 Gebäude, Level 1-5)

| Gebäude | Effekt |
|---------|--------|
| **Barracks** | +Einkommen pro Level |
| **Library** | +Research-Speed |
| **Treasury** | +Gilden-Geld-Storage |
| **Workshop** | +Workshop-Effizienz |
| **Arena** | +Boss-Damage |
| **Academy** | +XP-Gewinn |
| **Smithy** | +Equipment-Qualität |
| **Vault** | +Lager-Slots |
| **Market** | +Handels-Einnahmen |
| **Cathedral** | +Moral-Bonus |

Hall-Tick alle 60s (Offset 40s).

### 17.6 Mega-Projekte (V7)

- 2-4 Wochen Dauer
- Alle Mitglieder spenden Materialien
- Pro Tick (4h): Score-Update via Cloud-Function
- **HMAC-signiert**
- **Permanenter Bonus** für alle Mitglieder (+5% Income, +5% Crafting-Speed) bei Abschluss

**Aktuelle Mega-Projekte:**
- **Cathedral** (5 Bauphasen, 2 Wochen)
- **Headquarters (HQ)** (5 Bauphasen, 4 Wochen)

### 17.7 Kriegssaison

- Wöchentliche Liga (Bronze/Silver/Gold/Diamond)
- Matchmaking gegen andere Gilden
- 4 Tage Vorbereitung + 3 Tage Krieg
- Scoring: Aufträge, Mini-Game-Perfects, Boss-Schaden
- Belohnungen: Liga-Tier-bezogen

### 17.8 Chat

- Realtime Firebase
- **Profanity-Filter (DE/EN/ES/FR/IT/PT)**
- Emoji-Support (TextMeshPro)
- Image-Sharing (Phase 2)

### 17.9 Worker-Auktionen

(Siehe § 5.9)

### 17.10 Achievement-System (Gilde, 30 Stück)

10 Typen × 3 Tiers = 30 Achievements

### 17.11 3D-Visualisierung (Guild-Hub)

- **Eigene 3D-Szene** (Guild.unity)
- 10 Hall-Gebäude als physisch sichtbare Strukturen (Level = Größe + Glow)
- Bosse als animierte 3D-Modelle in eigener Boss-Arena
- Members als Avatare auf Hub-Karte (mit Online-Indicator)
- **Mega-Projekt-Bauphasen** sichtbar als wachsende Cathedral/Skyscraper-Struktur

---

## 18. Achievements (60+, 3 Tiers)

### 18.1 Tier-System

- **Bronze:** 5-15 GS
- **Silver:** 15-30 GS
- **Gold:** 30-50 GS

### 18.2 Kategorien

- Workshops (Carpenter Lv 10/50/100, etc.)
- Workers (10/50/100 hired)
- Orders (100/500/1000 completed)
- Mini-Games (Perfect Ratings 10/50/100)
- Research (5/25/45 nodes)
- Prestige (1× Bronze/Silver/Gold/.../Legende)
- Reputation (Tier-Aufstiege)
- Gold (1M/100M/1B €)
- Events (Live-Event-Teilnahme)
- Guild (Beitreten, Co-op, Boss-Beteiligung)
- Story (Chapter-Completion)

### 18.3 Vollständige Liste

*(60+ Achievements werden als ScriptableObjects in `Assets/_Project/ScriptableObjects/Achievements/` definiert. Bei Implementierung: 1:1 aus `Avalonia/Models/Achievements/*` portieren.)*

### 18.4 3D-Trophäen-Cinematic

Statt 2D-Dialog:
- 3D-Trophäe spawnt am Bildschirmrand
- Rotiert 3× mit Glow
- Floating-Text mit Achievement-Name + Reward
- Sound + Haptic-Feedback
- Meister-Hans-Voice-Stinger (random aus Pool)
- 5s sichtbar, Smooth-Fade

---

## 19. Daily-Reward (7-Tage-Karte)

### 19.1 Belohnungs-Tabelle

| Tag | Geld | XP | GS |
|-----|------|-----|-----|
| 1 | 100 € | 10 | 0 |
| 2 | 500 € | 25 | 0 |
| 3 | 1.000 € | 50 | 0 |
| 4 | 5.000 € | 100 | 0 |
| 5 | 10.000 € | 150 | 0 |
| 6 | 50.000 € | 200 | 0 |
| **7 (Jackpot)** | **500.000 €** | **500** | **50 GS** |

### 19.2 Premium-Bonus

Premium verdoppelt Tage 5-7.

### 19.3 Auto-Claim

Premium-Spieler: 1× pro Tag automatisch claimed.

---

## 20. Lucky-Spin (8 Slots)

### 20.1 Slot-Konfiguration

| Slot | Reward | Wahrscheinlichkeit (gewichtet) |
|------|--------|---------------------------------|
| 1 | 10.000 € | hoch |
| 2 | 50.000 € | mittel |
| 3 | 200 XP | hoch |
| 4 | 20 GS | mittel |
| 5 | 1h Speed-Boost | mittel |
| 6 | Niete (Nichts) | hoch |
| 7 | 100.000 € | niedrig |
| 8 | Niete (Nichts) | hoch |

### 20.2 Spin-Verfügbarkeit

- **Frei:** 1× pro Tag
- **Premium:** 2× pro Tag
- **Rewarded Ad:** Weitere Spins möglich

---

## 21. BattlePass (50 Tier, 30-Tage-Saison)

### 21.1 Saison-Eigenschaften

- **Dauer:** 30 Tage (reduziert von 42, 12 Saisons/Jahr statt 8.7)
- **Max Tier:** 50
- **XP pro Tier:** 250 × (CurrentTier + 1), Tiers 40+: ×2
- **Themes (zyklisch):** Spring/Summer/Autumn/Winter

### 21.2 Free-Track (Tiers 0-49)

- Tiers 0-29: Standard Rewards (Geld, XP)
- Tiers 30-49: Improved Rewards (GS, Equipment)
- **Tier 49 (Capstone):** 50 GS + großer Geldbetrag

### 21.3 Premium-Track

- 3× höhere Free-Rewards
- Zusätzliche Rewards auf Tier 35/40/45 (Milestones)
- Tier 49 Capstone: 150 GS + Cosmetic-Frame

### 21.4 Verdienst-Formeln

```
BaseMoneyReward = max(500, BaseIncome × 60)
BaseMoney(Tier) = BaseMoneyReward × (1 + Tier × 0.05)
BaseXp(Tier) = 100 + (Tier × 10)
GoldenScrews(Tier) = 1 + (Tier / 10)
```

### 21.5 BattlePass-Premium-Preis

9,99 € pro Saison (oder im Mega-Bundle 49,99 € enthalten)

---

## 22. Live-Events (8 Templates)

### 22.1 Event-Liste

| Event | Auswirkung | Trigger |
|-------|-----------|---------|
| **TaxAudit** | +10% Steuer auf Brutto (Income −10%) | Zufällig |
| **WorkerStrike** | Alle Stimmungen −20 | Zufällig |
| **HighDemand** | +Reward für Workshop-Typ | Zufällig (1 Workshop) |
| **MaterialShortage** | +Kosten für Workshop-Typ | Zufällig (1 Workshop) |
| **MarketRestriction** | Nur Tier C und niedriger verfügbar | Zufällig |
| **GoldenAge** | +Einkommen all-over | Saisonal |
| **PriceBoom** | Markt-Preise +50% | Zufällig |
| **OrderFlood** | Live-Order-Spawn-Rate ×2 | Zufällig |

### 22.2 Event-Check-Intervall

Alle 300s (5min).

### 22.3 Event-Tier-Skalierung

Höhere Prestige-Tiers haben häufigere und stärkere Events.

---

## 23. Saisonale Events & Cosmetics

### 23.1 4 Saisons pro Jahr

| Saison | Theme | Cosmetics-Pool |
|--------|-------|----------------|
| Spring | Kirschblüten, Pastel-Töne | Spring-Set (5-10 Items) |
| Summer | Sonne, helle Farben | Summer-Set |
| Autumn | Herbstlaub, warme Töne | Autumn-Set |
| Winter | Schnee, kalte Töne | Winter-Set |

### 23.2 Saison-Punkte (SP)

- 5 SP + Bonus pro Auftrag (während Saison)
- Eintauschbar im **Saison-Event-Shop** (6 Items)

### 23.3 Wetter-System

- 2× Partikel-Dichte
- Saisonale Visuals (Schnee, Blätter, Blüten in der 3D-Stadt)
- Optional: Live-Wetter via API (Phase 2)

### 23.4 Mid-Season-Events

- Story-Chapter-Releases
- Saison-spezifische Live-Events
- Limited-Time-Achievements

---

## 24. Heirlooms (V7)

### 24.1 Run-Heirlooms

- **Max pro Run:** 3 (Free) / 4 (Premium)
- **Bonus pro Item:** +2% Income (kumulativ)
- Wählbar bei jedem Prestige

### 24.2 Permanent-Heirlooms

- **Max gesamt:** 50
- **Bonus pro Item:** +0.5% Income permanent
- Werden bei Ascension aus Run-Heirlooms übertragen
- **Unlock-Bedingung:** logi_12 (Research)

### 24.3 Heirloom-Pool

Bei jedem Prestige werden Top-Items als Heirloom angeboten:
- Top-3 Worker (höchstes Level)
- Top-3 Workshops (höchstes Level)
- Erfolgreichste Equipment-Items
- Master-Tools werden grundsätzlich erhalten

---

## 25. Eternal-Mastery

- **+1 EM pro Prestige**
- **+0.5% Income pro EM** (permanent)
- **Soft-Cap-Threshold:** 50 Prestiges
- **Bonus per 5 Prestiges (kumulativ):** +2.5%
- **Bonus per 10 Prestiges:** +5%

---

## 26. Story-Chapters (38-40)

### 26.1 Chapter-Struktur

- **Chapter 1-10:** Intro & Day 1 (Erbe der Werkstatt, erste Mitarbeiter, Plumber-Unlock)
- **Chapter 11-20:** Early Game (bis Electrician, erste Konkurrenz)
- **Chapter 21-30:** Mid Game (bis Research-Branches abgeschlossen)
- **Chapter 31-38:** Late Game (bis Legende-Prestige)
- **Chapter 39-40:** Endgame (Ascension/Meta)

### 26.2 Trigger-Bedingungen

Jeder Chapter triggert bei:
- Player-Level (z.B. Lv 5, 10, 25, ...)
- Prestige-Stufe (Bronze, Silver, ...)
- Master-Tool-Unlock
- Spezial-Event (z.B. erste Goldscrew, erstes Premium)

### 26.3 Meister-Hans-Voice

Pro Chapter 5 Lines × 6 Sprachen = 30 Voice-Files pro Chapter.
**Gesamt:** ~40 Chapters × 5 Lines × 6 Sprachen = ~1200 Voice-Files (Teil der 1500 in ASSETS_AI.md).

**Voice-Generation:** Via ElevenLabs API mit vorgefertigter Standard-Voice (siehe § 2.1). Batchable über REST-API.

### 26.4 3D-Story-Visualisierung

- Vollbild-Dialog mit **3D-Meister-Hans-Avatar** (links, animiert)
- Parallax-Background
- Voice-Over mit Untertiteln
- Skip-Button verfügbar
- Story-Re-Read im Settings-Menü

---

## 27. Tutorial / FTUE

### 27.1 8 Tutorial-Schritte

1. **Willkommen-Voice** (Meister Hans intro)
2. **Erster Auftrag** annehmen (Quick-Order)
3. **Sawing-Mini-Game** (5 vereinfachte Schnitte)
4. **Auftrag abschließen** (Reward + Floating-Text)
5. **Workshop-Upgrade** (Carpenter Lv 1→2)
6. **Erster Worker hiren** (F-Tier)
7. **Plumber-Unlock** (Level 5)
8. **Tutorial-Ende** + Premium-Hinweis (optional)

### 27.2 Contextual Hints

- Nach 5min Idle: "Probiere Forschung aus!"
- Nach 10. Auftrag: "Du kannst Strategien wählen"
- Bei Worker-Mood < 30: "Achte auf Worker-Stimmung"
- Bei 1. Goldscrew: "Premium-Boost im Shop"

### 27.3 3D-Visualisierung

- 3D-Kamera-Pan durch die Handwerker-Stadt
- "Tippe hier" mit animiertem 3D-Finger-Indikator
- Meister-Hans-3D-Avatar erscheint, gibt Tipps
- Stage-Lighting fokussiert wichtigen Bereich

---

## 28. Notifications (8 Trigger)

### 28.1 Trigger-Tabelle

| Trigger | Zeitpunkt | Text-Beispiel |
|---------|-----------|---------------|
| **ResearchComplete** | Nach Forschungs-Timer | "🔬 [Research-Name] ist fertig!" |
| **DeliveryReminder** | 30 min vor Order-Ablauf | "📦 Auftrag läuft in 30min ab!" |
| **RushAvailable** | Spieler ist online | "⚡ Speed-Boost verfügbar!" |
| **DailyReward** | Täglich 09:00 Uhr | "🎁 Deine tägliche Belohnung wartet!" |
| **WorkerMoodCritical** | 30 min nach Spieler-Close | "😰 [Worker] ist unzufrieden!" |
| **OfflineEarningsCapped** | Nach 4h Offline | "💰 Offline-Einnahmen sind voll!" |
| **BattlePassExpiring** | 3 Tage vor Saison-Ende | "⏰ BattlePass endet in 3 Tagen!" |
| **LiveOrderAvailable** | Ab Level 25 | "👑 VIP-Kunde wartet!" |

### 28.2 Lokalisierung

Alle Texte in 6 Sprachen (DE/EN/ES/FR/IT/PT).

---

## 29. Premium / IAP / Ads

### 29.1 Imperium-Pass (Premium, 4,99 € Lifetime)

| Feature | Effekt |
|---------|--------|
| Income-Multiplier | +50% |
| Goldscrew-Multiplier (Gameplay) | +100% |
| Goldscrew-Display | +100% |
| Keine Ads | ja |
| Auto-Claim Daily | 1×/Tag |
| Heirloom-Slots | +1 (3→4) |
| Lucky-Spin/Tag | 2× (statt 1×) |
| Auto-Complete-Tickets | 15 Perfects statt 30 |
| Mini-Game-Score | +10% |
| Markt-Strafzoll | 0% (statt 5%) |
| T1-Auto-Produktion | −50% Intervall |
| T2-T4-Auto-Produktion | −50% Intervall |
| Markt-Insider-Heatmap | aktiv |
| Auto-Verkaufs-Regeln | aktiv (vor Research-Unlock) |

### 29.2 IAP-Bundles

| Bundle | Preis | Inhalt |
|--------|-------|--------|
| **Mid** | 9,99 € | 1500 GS + 8h Speed-Boost |
| **Big** | 19,99 € | 4000 GS + 48h Speed-Boost + 25 Mio. € |
| **Mega** | 49,99 € | 12.000 GS + 7 Tage Boost + 200 Mio. € + Premium |

### 29.3 Ad-Placements (13)

1. **golden_screws** — 10 GS, 4h Cooldown
2. **shop_reward** — Cash/Boost, 3h Cooldown
3. **score_double** — Mini-Game-Score verdoppeln
4. **market_refresh** — Worker-Markt neu würfeln
5. **workshop_speedup** — 2h Income
6. **workshop_unlock** — 30% Rabatt
7. **worker_hire_bonus** — +1 Slot temporär (max 3/Workshop)
8. **research_speedup** — −50% Restzeit (nur ab 30min)
9. **daily_challenge_retry** — Challenge neu starten
10. **achievement_boost** — Progress +20% (nur bei TargetValue>5)
11. **offline_double** — 2× Offline-Income
12. **rush_boost** — 1h Rush
13. **lucky_spin** — 1× pro Tag zusätzlich

**Cooldowns:** Separate Tracking pro Placement.

### 29.4 GS-Quellen (Gameplay-verdient)

| Quelle | GS-Menge |
|--------|----------|
| Mini-Games | 3-10 |
| Daily-Challenges | ~12 |
| Achievements | 5-50 |
| Daily-Login | 1-25 |
| Rewarded Ad | 10 |
| Workshop-Meilensteine | 2-50 |
| Prestige-Meilensteine | 5-100 |
| Lucky-Spin | 20 |
| Saison-Capstone | 50-150 |

(Premium verdoppelt nur Gameplay-Quellen, NICHT IAP-Käufe.)

---

## 30. Economy-Formeln

### 30.1 Income-Berechnung (vollständig)

Siehe § 4.3 (Income-Formel).

### 30.2 Soft-Cap (Income)

```
SoftCapFactor = if (GrossIncome <= 8.0M €/s) 1.0
              else log₂(8.0M + (GrossIncome - 8.0M)) / log₂(GrossIncome)
```

### 30.3 Offline-Income

```
OfflineEarnings = GrossIncome × min(TimeSinceLastPlay, OfflineCap)
                × OfflineSoftCap   // -70% bei >2h, Premium +50%
```

### 30.4 Worker-Effizienz

```
EffectiveEfficiency = BaseEff × (1 + Level × 0.03)
                    × MoodFactor             // 0-1 linear
                    × FatigueFactor          // 1-0.5 linear
                    × (1 + SpecBonus + EquipBonus)
                    × PersonalityMult        // 0.8 - 1.3x
                    × TalentBonus            // 1.0 - 1.25x (Talent-Stars)
                    × (MaterialAffinity-Match ? 1.2 : 1.0)
                    × LevelFitFactor         // Penalty bei niedrigem Tier auf hohem Level
```

### 30.5 Upgrade-Kosten

Siehe § 4.5.

### 30.6 Reward-Multiplikatoren

```
OrderReward = BaseReward
            × TypeMultiplier            // 0.6, 1.0, 1.8, 2.5, 3.0, 1.8
            × StrategyMultiplier        // 0.75, 1.0, 2.0
            × MiniGameRatingMultiplier  // 0.5, 0.75, 1.0, 1.5
            × ReputationFactor          // 0.7 - 1.5
            × IsVipFactor               // 3.0 wenn VIP
            × StammkundeFactor          // 1.1 - 1.5
            × PremiumIncomeMult         // 1.5 wenn Premium
            × GuildBoostFactor
            × EventMultiplier
            × MaterialOfferBonus        // 1.25 - 1.60 wenn Material-Offer
```

---

## 31. Anti-Cheat

### 31.1 HMAC-Signierung

Folgende Werte sind HMAC-SHA256-signiert:
- Money (decimal)
- GoldenScrews (int)
- BossDamage (int)
- AuctionBid (decimal)
- CoopOrderScore (long)
- MegaProjectContribution (long)

### 31.2 Server-Side-Validation

Über Firebase Cloud Functions:
- `validateMiniGameScore` — Score-Cap pro Mini-Game
- `validateIapReceipt` — Google Play Receipt
- `settleBattlePassRewards` — Saison-Reward-Verteilung
- `createGuild` — Tag-Eindeutigkeit (Transaction)
- `onPlayerWriteValidate` — Schema + Cap-Check
- `onReportReceived` — Auto-Mute nach N Reports
- `onWarSeasonCompleted` — Belohnungs-Verteilung
- `liveEventRefresh` — Live-Event-Score-Tabelle (alle 4h)

### 31.3 Save-Sanitizer

- Heirloom-Items gegen Catalog validieren
- Money-Cap (z.B. max 1e21 €)
- Worker-Level-Cap
- Orphan-Reservierungen entfernen
- Firebase-PlayerGuid mit lokaler ID synchronisieren

### 31.4 Rate-Limits

In `database.rules.json`:
- Max Schreibvorgänge pro Sekunde
- Max Read-Volumen pro Minute
- Cloud-Function-Cooldowns

---

## 32. Telemetrie-Events

### 32.1 Event-Liste (FirebaseAnalytics)

| Event | Properties |
|-------|-----------|
| `level_up` | `new_level`, `time_since_last_level` |
| `workshop_bought` | `workshop_id`, `level`, `cost` |
| `workshop_upgraded` | `workshop_id`, `new_level`, `cost` |
| `worker_hired` | `worker_tier`, `cost`, `source` (`market`/`auction`/`research`) |
| `order_completed` | `order_type`, `strategy`, `reward`, `rating` |
| `minigame_perfect` | `type`, `score` |
| `prestige_triggered` | `tier`, `count`, `pp_gained` |
| `iap_purchase` | `product_id`, `amount_usd` |
| `ad_rewarded` | `placement`, `reward` |
| `material_crafted` | `recipe_id`, `output_amount` |
| `material_sold` | `material_id`, `amount`, `price` |
| `warehouse_full_pause` | `slot_count` |
| `material_market_trade` | `material_id`, `direction`, `amount` |
| `guild_joined` | `guild_id`, `member_count` |
| `guild_mega_project_donation` | `project_id`, `material_id`, `amount` |
| `heirloom_chosen` | `item_id` |
| `order_accepted_with_material` | `material_id`, `amount` |
| `tutorial_step_completed` | `step_id`, `time_taken` |
| `chapter_unlocked` | `chapter_id` |
| `notification_tapped` | `trigger_id` |

### 32.2 Privacy

- UMP-Consent (DSGVO)
- Opt-Out via Settings
- EU AI Act Transparenz-Hinweis (KI-Assets gekennzeichnet)

---

## 33. Handwerker-Stadt-Hub-Design

### 33.1 Stadt-Layout

Die **Handwerker-Stadt** ist die zentrale 3D-Welt im Hub.unity. Sie zeigt alle 10 Werkstätten als physische Gebäude in einer **stylisierten Toon-Stadt** (siehe ASSETS_AI.md § 13).

**Anordnung (von Anfang sichtbar, gestaffeltes Unlock):**

```
                  [InnovationLab Lv 750+]
                          |
                  [MasterSmith Lv 500+]
                          |
[Architect]   [GeneralContractor]
    |                |
[Contractor]   [Roofer]
    |                |
[Painter]      [Electrician]
    |                |
[Plumber]      [Carpenter ← Start]
    |                |
    └─── Stadtplatz ──┘ (Meister-Hans-Statue zentral)
```

### 33.2 Stadt-Wachstum

- **Stadtplatz**: Zentraler Treffpunkt, Meister-Hans-Statue, NPC-Anziehungspunkt
- **Werkstätten**: Wachsen visuell mit Level (Modul-Decals + Sub-Bauten siehe ASSETS_AI.md § 7.2)
- **Straßen**: Werden hochwertiger ab Player-Level 50 (Schotter → Asphalt → gepflastert)
- **NPCs**: Anzahl skaliert mit Reputation-Tier
- **Wetter**: Saisonale Themes (Schnee/Regen/Sonne/Herbstlaub)
- **Day/Night-Cycle**: Optional Phase 2

### 33.3 City-Tiles (80 Stück)

10 Welt-Themes × 8 Tiles = 80 Tiles (siehe ASSETS_AI.md § 12).

Welt-Themes wechseln mit Saison oder Player-Progression:
1. **Sunny Day Plaza** (Default)
2. **Spring Blossom**
3. **Summer Heat**
4. **Autumn Fall**
5. **Winter Snow**
6. **Industrial Park** (höhere Levels)
7. **Modern City** (Endgame)
8. **Premium District** (mit Premium-Boost)
9. **Skyline Vista** (mit Mega-Projekt-HQ)
10. **Cathedral District** (mit Mega-Projekt-Cathedral)

### 33.4 Camera-System (Cinemachine)

- **HubOverviewCamera:** Top-Down auf gesamte Stadt
- **WorkshopOrbitCamera:** Orbit um aktive Werkstatt
- **WorkerFollowCamera:** Folgt Worker beim Mini-Game-Start
- **MeisterHansCamera:** Bei Story-Dialogen
- **PrestigeCamera:** Cinematic-Schwenk

### 33.5 Navigation

- Tap auf Werkstatt → Camera-Pan zur Detail-Ansicht
- Swipe links/rechts → durch Werkstätten
- Pinch-Zoom → Stadt-Übersicht
- Tab-Menü unten → Sub-Screens (Imperium/Missionen/Gilde/Shop)

### 33.6 Worker-Bewegung

Worker laufen sichtbar zwischen:
- Werkstatt (Idle, Arbeit)
- Kantine (Mood-Erholung)
- Trainings-Zentrum (Training)
- Werkstatt-Erweiterung (extra Worker-Slot)

NavMesh-Pathfinding für realistisches Verhalten.

---

## 34. Mega-Projekte (Cathedral, HQ)

### 34.1 Cathedral

- **Dauer:** 2 Wochen (336h)
- **Bauphasen:** 5
- **Materialien:** Hohe Mengen aller T1-T4
- **Bonus bei Abschluss:** +5% Income, +5% Crafting-Speed (permanent für Gilde)

**3D-Visualisierung:** Cathedral wächst sichtbar in der Stadt (5 Bauphasen-Modelle gemäß ASSETS_AI.md § 12).

### 34.2 Headquarters (HQ)

- **Dauer:** 4 Wochen (672h)
- **Bauphasen:** 5
- **Materialien:** Sehr hohe Mengen (T3-T4 Schwerpunkt)
- **Bonus bei Abschluss:** +10% Income, +10% Crafting-Speed (permanent für Gilde)

**3D-Visualisierung:** Skyscraper-Wachstum, Hero-Asset mit Cloud-Polish (Rodin Gen-2.5).

### 34.3 Mega-Projekt-Donations

- Pro Spieler trackbar (Telemetrie: `guild_mega_project_donation`)
- HMAC-signiert
- Score-Visualisierung in Guild.unity
- Top-Contributors mit Avatar-Frame-Cosmetic

---

## 35. Future / Phase 2

Nicht im MVP:

- **iOS-Launch** (Architektur ist vorbereitet via Unity Cross-Platform — Entscheidung nach Beta-Erfolg)
- **Live-PvP via Photon Fusion** (Echtzeit-Klan-Matches, 5v5) — siehe Phase 2 Roadmap unten
- **Day/Night-Cycle**
- **Live-Wetter** (API)
- **Worker-Voice-Lines** (bereits in ASSETS_AI.md geplant — kann ins MVP wenn Audio-Budget reicht)
- **Replay-Highlights** (Prestige-Cinematic-Clips zum Teilen)
- **Cosmetic-DLC**
- **Mehr Saisonale Live-Events** (initial nur 1 pro Quartal)
- **Kriegssaison-Ligen erweitern**
- **Photon Live-PvP** (asynchrone Auktionen funktionieren ohne)
- **Cross-Save Avalonia ↔ Unity** (falls Hard-Cutover entschieden wird)
- **Ascension-Perks erweitern** (initial 6, später 10+)
- **Master-Tools erweitern** (initial 12, später 18+)

---

## 36. Konsistenz mit ASSETS_AI.md

### 36.1 Persona-Anker

- **Meister Hans** (Voice-Cloned, 1500 Voice-Lines, 6 Sprachen)
- **Persona-Marke:** Amber `#D97706` als Hauptfarbe
- **Tonalität:** Freundlich, leicht karikiert, meisterhaft

### 36.2 Asset-Plan-Übereinstimmung

| DESIGN-Bereich | ASSETS_AI-Referenz |
|----------------|---------------------|
| 10 Werkstätten + Modul-Setup | § 7.2 |
| Worker-Mood-States (4) | § 8.3 |
| Material-Affinity (5 Props) | § 8.4 |
| Master-Tools-Glow | § 8.5 |
| Worker-Animationen | § 9.2 |
| Workshop-Idle-Particle-FX | § 9.4 |
| Audio-Plan (BGM + SFX + Voice) | § 11 |
| Mega-Projekte (Cathedral, HQ, 5 Bauphasen) | § 12 |
| Prestige-Cinematic-Hero | § 12 |

### 36.3 EU-Compliance

Alle Assets EU-konform (kein Hunyuan, kein Suno/Udio) — siehe ASSETS_AI.md § 14.

---

## 37. Links

- [PLAN.md](PLAN.md) — Strategischer Plan
- [CLAUDE.md](CLAUDE.md) — Conventions
- [ARCHITECTURE.md](ARCHITECTURE.md) — Tech-Details
- [ROADMAP.md](ROADMAP.md) — Wochenplan
- [ASSETS_AI.md](ASSETS_AI.md) — KI-Asset-Pipeline (3D + Audio + Voice)
- [Avalonia-DESIGN-Referenz](../HandwerkerImperium/CLAUDE.md)
- [GameBalanceConstants.cs (Avalonia)](../HandwerkerImperium/HandwerkerImperium.Shared/Models/GameBalanceConstants.cs)
