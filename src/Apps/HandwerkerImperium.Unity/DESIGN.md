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
7. [Mini-Games (13 Typen, 10 Renderer)](#7-mini-games-13-typen-10-renderer)
8. [Forschung (72 Nodes, 4 Branches)](#8-forschung-72-nodes-4-branches)
9. [Prestige (7 Tiers + Ascension)](#9-prestige-7-tiers--ascension)
10. [Crafting (33 Rezepte, 4 Tiers)](#10-crafting-33-rezepte-4-tiers)
11. [Lager (Warehouse V7)](#11-lager-warehouse-v7)
12. [Markt & Material-Affinität](#12-markt--material-affinität)
13. [Reputation & Tier-System](#13-reputation--tier-system)
14. [Master-Tools (12 Artefakte)](#14-master-tools-12-artefakte)
15. [Gebäude (7 Stück)](#15-gebäude-7-stück)
16. [Equipment-System](#16-equipment-system)
17. [Gilden & Multiplayer](#17-gilden--multiplayer)
18. [Achievements (109 Spieler-Achievements, 17 Kategorien)](#18-achievements-109-spieler-achievements-17-kategorien)
19. [Daily-Reward (30-Tage-Zyklus)](#19-daily-reward-30-tage-zyklus)
20. [Lucky-Spin (8 Slots)](#20-lucky-spin-8-slots)
21. [BattlePass (50 Tier, 30-Tage-Saison)](#21-battlepass-50-tier-30-tage-saison)
22. [Live-Events & Random Events](#22-live-events--random-events)
23. [Saisonale Events (4/Jahr) & Event-Shop](#23-saisonale-events-4jahr--event-shop)
24. [Heirlooms (Erbstücke)](#24-heirlooms-erbstücke)
25. [Eternal-Mastery (permanenter Einkommens-Bonus)](#25-eternal-mastery-permanenter-einkommens-bonus)
26. [Story-Chapters (60 = 40 Haupt + 20 Saison, Meister Hans)](#26-story-chapters-60--40-haupt--20-saison-meister-hans)
27. [Tutorial / FTUE (10 Schritte) + ContextualHints (32)](#27-tutorial--ftue-10-schritte--contextualhints-32)
28. [Notifications (8 Push-Trigger + In-App-Bell)](#28-notifications-8-push-trigger--in-app-bell)
29. [Premium / IAP / Ads](#29-premium--iap--ads)
30. [Economy-Formeln](#30-economy-formeln)
31. [Anti-Cheat](#31-anti-cheat)
32. [Telemetrie-Events](#32-telemetrie-events)
33. [Handwerker-Stadt-Hub-Design](#33-handwerker-stadt-hub-design)
34. [Mega-Projekte (Cathedral, HQ)](#34-mega-projekte-cathedral-hq)
35. [Future / Phase 2](#35-future--phase-2)
36. [Konsistenz mit ASSETS_AI.md](#36-konsistenz-mit-assets_aimd)
37. [Links](#37-links)

---

## 1. Spielprinzip & Loop

### 1.1 Core-Concept

**HandwerkerImperium** ist ein **Idle-Incremental-Management-Game** mit **aktiven Mini-Games** für Bonus-Rewards. Der Spieler erbt Meister Hans' alte Werkstatt und baut ein Imperium aus 10 Handwerks-Werkstätten in einer wachsenden 3D-Stadt auf, hired Arbeiter, nimmt Aufträge an, forscht, prestiged (7 Tiers) und ascended (nach 3× Legende).

> **Verbindlicher Grundsatz:** Die Unity-Version ist mechanisch GENAU DASSELBE Spiel wie das produktive Avalonia-Original — identische Formeln, identische Balancing-Werte, identische Gates. "Besser/3D" betrifft ausschließlich die Präsentation (3D-Hub, Cinematics, GPU-Particles, Game-Juice, UX). Alle Zahlen in §1, §3 und §30 sind 1:1 aus dem Avalonia-Code extrahiert.

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
1. Werkstätten leveln (Lv 1 → 1000, WorkshopMaxLevel = 1000)
2. Worker rekrutieren (10 Tiers F → Legendary)
3. Forschung (Nodes in 4 Branches)
4. Goldscrews sammeln & Boosts kaufen (SpeedBoost ×2, Feierabend-Rush ×2)
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
3. Eternal-Mastery sammeln (+0.5% Income pro Prestige, Soft-Cap ab 50 Prestiges)
4. Heirlooms maximieren (50 Permanent-Slots, +0.5% Income je permanentes Heirloom)
5. Mega-Projekt-Boni stacken
```

### 1.5.1 Game-Loop-Grundtakt (1 Tick = 1 Sekunde)

Der `GameLoopService` tickt über einen 1-Hz-Timer (1 Tick = 1 Sekunde). Pro Tick wird das Netto-Einkommen berechnet und gutgeschrieben:

```
1. Brutto-Income berechnen (GrossIncome — §30.1)
2. Soft-Cap anwenden (Multiplikator-Cap — §30.2)
3. Kosten berechnen (§30.7)
4. netEarnings = GrossIncome − Kosten
5. SpeedBoost (×2) + Feierabend-Rush (×2, gestackt) — nur bei netEarnings > 0 (§30.8)
6. Netto anwenden: positiv → AddMoney, negativ → Abzug NUR wenn Money + netEarnings > 0 (Floor bei 0)
7. Worker-States + Forschungs-Timer um 1.0s fortschreiben
8. LastPlayedAt = jetzt (jeden Tick — Basis für Offline-Earnings)
9. AutoSave alle 30 Ticks (_tickCount % 30 == 0)
```

- **GrossIncome = 0 ohne arbeitende Worker** (Income entsteht nur pro Worker, nie durch leere Werkstätten).
- Boosts (SpeedBoost/Rush) wirken ausschließlich bei positivem Netto.
- Bei negativem Netto (Kosten > Income) wird abgezogen, aber Geld fällt nie unter 0.
- Periodische Sub-Systeme laufen über Modulo-Offsets (Automation alle 5 Ticks, Lieferant alle 10 Ticks, Auto-Produktion alle 180 Ticks usw.) — Details in ARCHITECTURE.md.

Separater Render-Tick (`FrameClockService`, ~30 Hz) treibt nur die Präsentation (Partikel, Floating-Text, Money-Animation) — niemals Spiellogik.

### 1.6 Erwartetes Spieler-Profil (Avalonia-Telemetrie)

| Spielzeit | Werkstätten | Worker | Player-Level | Geld/s | Status |
|-----------|-------------|--------|--------------|--------|--------|
| 30 min | 1-2 | 2-3 | 5 | ~10 € | FTUE-Ende, Plumber-Unlock |
| 2h | 3-5 | 10-15 | 25 | ~500 € | Electrician verfügbar |
| 8h | 5-7 | 25-30 | 60 | ~25.000 € | Painter+Roofer leveling |
| 24h | 8-10 | 40-60 | 100 | ~500.000 € | Bronze-Prestige bereit (Lv 30) |
| 1 Woche | 10 | 80+ | 250 | ~5 Mio. € | Silver/Gold-Prestige-Runs |
| 1 Monat | 10 (Lv 500+) | 150+ | 750 | ~100 Mio. € | Diamant-Prestige + Logistics-Branch |
| 6 Monate | 10 (Lv 1000 = Max) | 200+ | 1200 | ~10 Mrd. € | 3× Legende → Ascension-Unlock |

> Werte sind Telemetrie-Projektionen aus der Avalonia-Production (Spieler-Profilierung), keine code-extrahierten Konstanten. Hard-Caps: WorkshopMaxLevel = 1000, MaxPlayerLevel = 1500.

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
- Schneller Setup (kein Aufnahme-Equipment, keine Sprecher-Freigabe-PDF)
- Konsistente Qualität in allen 6 Sprachen (ElevenLabs Multilingual v2-Modell)
- Keine rechtlichen Risiken (Voice ist von ElevenLabs lizenziert)
- Re-Generation bei Bedarf jederzeit möglich (z.B. neue Story-Chapters)

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
| **Reputation** | `0-100` (decimal/int Score) | Auftrag-Erfolg/Misserfolg | 4 Tiers (Beginner → Industry Legend), Reward-Mult. 0.7-1.5x |
| **Eternal-Mastery (EM)** | `int` | Pro Prestige +1 | +0.5% Income je Prestige (`EternalMasteryBonusPerPrestige = 0.005`), Soft-Cap ab 50 Prestiges (`EternalMasterySoftCapThreshold = 50`) |
| **Saison-Punkte (SP)** | `int` | Saison-Aktivitäten | Saison-Event-Shop |

**Caps & harte Grenzen (aus dem Avalonia-Code):**
- **Player-Level:** Min 1 (`MinPlayerLevel`), Max **1500** (`MaxPlayerLevel`). `AddXp` über Level 1500 verfällt (kein Overflow).
- **Prestige-PermanentMultiplier:** hart bei **20×** gedeckelt (`Math.Min(PermanentMultiplier, 20.0)` in der Income-Berechnung; `MaxPermanentMultiplier = 20.0`). Tier-Tabellen-Werte über 20× (z.B. 64× in §9.1) sind theoretische Vor-Cap-Werte und werden im Income nie wirksam.
- **Prestige-Income-Bonus (Prestige-Shop):** hart bei **+300%** gedeckelt.
- **Goldschrauben aus IAP:** bekommen WEDER Prestige-/Ascension-Bonus NOCH Premium-Verdopplung (nur Gameplay-Quellen verdoppeln mit Premium).

### 3.2 Crafting-Materialien (5 Affinity-Typen × 4 Tiers)

| Affinität | T1 Beispiele | T2 Beispiele | T3 Beispiele | T4 Beispiele |
|-----------|-------------|-------------|-------------|-------------|
| **Wood** | Wood, Plank | Wooden Beam, Furniture-Component | Luxury Furniture | Villa-Wood-Bauteil |
| **Metal** | Iron, Copper, Steel | Steel Ingot, Circuit Board | Smart Home Module | Skyscraper-Frame |
| **Stone** | Concrete, Clay | Concrete Block, Roof Tile | Roof Structure | Imperium-HQ-Foundation |
| **Art** | Paint, Paper | Paint Can, Blueprint | Artwork | Premium-Artwork |
| **Tech** | Wire, Silicon | Wire Spool, Circuit Board | Smart Home | High-Tech-Module |

### 3.3 Premium-Items

- **Imperium-Pass** (Premium, 4,99 € Lifetime) → Income ×1.5 (+50%), Goldschrauben ×2 auf Gameplay-Quellen (+100%), Offline-Cap **16h** (statt 4h), Mini-Game-Auto-Complete-Schwellen halbiert, Rewarded-Ad-Reward-Verdopplung ×3 statt ×2, keine Ads.
- **Speed-Boost** (×2 Netto-Einkommen über IAP-Bundles, verschiedene Laufzeiten)
- **Feierabend-Rush** (×2 Netto, 1×/Tag gratis, danach Goldschrauben; stackt multiplikativ mit Speed-Boost → online bis ×4)
- **Crafting-Booster** (BattlePass-Reward)
- **XP-Booster** (DailyReward/BattlePass — verdoppelt zugewiesenes XP, `IsXpBoostActive`)

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

Quelle: `WorkshopType.cs` (`WorkshopTypeExtensions`), `GameBalanceConstants`. Carpenter ist die Start-Werkstatt
und als einzige initial freigeschaltet. Prestige-exklusiv (RequiredPrestige > 0): Architect (1×), GeneralContractor
(3×), MasterSmith (4×), InnovationLab (5×). Auto-Produktion von Items wird ab Workshop-Level 50
(`AutoProductionUnlockLevel`) aktiv: Standard-Workshops alle **180s**, InnovationLab alle **120s**,
MasterSmith alle **60s**.

### 4.2 Workshop-Sub-Module (3D-Aufbau gemäß ASSETS_AI.md § 7.2)

Jede Werkstatt besteht aus folgenden 3D-Sub-Modulen (siehe ASSETS_AI.md):
- **Building** (Hauptgebäude, sichtbar ab Lv1)
- **Sign** (Werkstatt-Schild über der Tür)
- **Workbench** (Werkbank/Werkzeug-Setup)
- **StorageAddon** (Lager-Anbau, sichtbar ab Lv2)
- **Decoration_Lv{1-5}** (Deko-Layer, ausgetauscht bei Upgrade)

### 4.3 Income-Formel (vollständig)

Quelle: `WorkshopFormulas.CalculateGrossIncome`, `Workshop.GrossIncomePerSecond`. Das Brutto-Einkommen
ist eine **Summe über alle eingesetzten Worker** — **ohne Worker gibt es kein Einkommen** (return 0 bei
`workers.Count == 0`). KEIN globaler `BaseValue × IncomeMultiplier`-Term.

**Schritt 1 — Basis pro Worker (`CalculateBaseIncomePerWorker`):**
```
BaseIncomePerWorker = 1.02^(Level-1)                  // IncomeBaseMultiplier = 1.02
                    × TypeMultiplier                  // 1.0 .. 7.0 je WorkshopType (siehe 4.1)
                    × MilestoneMultiplier(Level)       // kumulativ (siehe 4.4)
```

**Schritt 2 — Brutto pro Workshop (`CalculateGrossIncome`):**
```
WENN workers.Count == 0 → GrossIncome = 0

totalIncome = Σ über alle Worker:
    BaseIncomePerWorker
    × worker.EffectiveEfficiency                      // siehe § 5.2 (Mood/Fatigue/XP/Talent/Spez/Equip/Personality)
    × LevelFitFactor(Level, Tier.LevelResistance, levelResistanceBonus)   // siehe 4.1-Fußnote / § 5.1

// Aura-Bonus (additiv über alle Worker summiert, dann gedeckelt auf 50%):
auraBonus = Σ Tier.AuraBonus                          // S +5%, SS +8%, SSS +12%, Legendary +20%
WENN auraBonus > 0:  totalIncome ×= (1 + min(auraBonus, MaxAuraBonus = 0.50))

WENN RebirthIncomeBonus > 0:  totalIncome ×= (1 + RebirthIncomeBonus)   // gestaffelt je Stern, siehe 4.8
```

**Schritt 3 — Spezialisierungs-Aufschläge (`Workshop.GrossIncomePerSecond`, nach Schritt 2):**
```
WENN Quality-Spez (EfficiencyModifier != 0):   gross ×= 1.20   // +20%
WENN Efficiency-Spez (IncomeModifier +0.30):   gross ×= 1.30
WENN Economy-Spez (IncomeModifier -0.05):      gross ×= 0.95
```

**Schritt 4 — globale Multiplikatoren (auf die Summe aller Workshops, `IncomeCalculatorService`):**
```
GrossTotal = (Σ aller Workshop-GrossIncomePerSecond × PrestigeMultiplier[HARD-Cap 20×, in der Basis])
           × (1 + PrestigeShopIncomeBonus)    // Prestige-Shop-Income
           × (1 + ResearchBonus)              // Research-Effizienz, Cap +50%
           × EventMultiplier                  // inkl. TaxAudit ×0.90
           × (1 + MasterToolBonus)            // 0% bis +74% (Summe aller 12 gesammelten Tools, siehe § 14)
           × (1 + GuildIncome) × (1 + GuildResearchIncome) × (1 + GuildResearchEfficiency)
           × (1 + HallIncomeBonus) × (1 + HallEverythingBonus)   // Gildenhallen-Boni — NICHT weglassen
           × VipMultiplier
           × (1 + ManagerIncomeBoost) × (1 + ManagerEfficiencyBoost)   // ZWEI separate Manager-Faktoren
           × PremiumMultiplier                // ×1.5 wenn Premium
           × EternalMasteryMultiplier         // siehe § 25
           × (1 + HeirloomBonus)              // aktive + permanente Erbstücke
           ; danach SoftCapFactor             // tier-skalierende log2-Dämpfung auf den MULTIPLIKATOR (siehe § 30)
```
Die **exakte** multiplikative Reihenfolge (inkl. PrestigeMultiplier in der Income-Basis) ist in § 30.1
definiert und dort maßgeblich; diese Übersicht zeigt alle beteiligten Faktoren. Soft-Cap-Mechanik exakt
wie im Original `IncomeCalculatorService.ApplySoftCap` (Schwelle = Multiplikator-Einheit, nicht €/s).

### 4.4 Milestone-Multiplikatoren (Level-Boni, 16 Stufen)

Quelle: `GameBalanceConstants.MilestoneMultipliers`. `CalculateMilestoneMultiplier(level)` = Produkt
**aller** Multiplikatoren mit `milestoneLevel <= level` (kumulativ). Kumulativ **~1500x** bei Level 1000
(Produkt aller 16 Milestones = 1499.86). Der veraltete Code-Doc-Kommentar nennt "~921x" — maßgeblich
ist der reale Produktwert ~1500x.

| # | Level | Multiplikator | Hinweis |
|---|-------|---------------|---------|
| 1 | 25 | 1.15x | — |
| 2 | 50 | 1.30x | Spezialisierung freischaltbar |
| 3 | 75 | 1.30x | — |
| 4 | 100 | 1.45x | — |
| 5 | 150 | 1.60x | Cross-Workshop-Crafting freigeschaltet |
| 6 | 200 | 1.45x | — |
| 7 | 225 | 1.30x | — |
| 8 | 250 | 1.60x | — |
| 9 | 350 | 1.60x | — |
| 10 | 400 | 1.60x | — |
| 11 | 500 | 2.00x | MasterSmith-Unlock (mit Prestige) |
| 12 | 600 | 1.70x | — |
| 13 | 650 | 1.65x | — |
| 14 | 750 | 1.60x | InnovationLab-Unlock |
| 15 | 900 | 1.60x | — |
| 16 | 1000 | 3.00x | Hard-Cap Level (= Rebirth-Trigger) |

### 4.5 Upgrade-Kosten-Formel

Quelle: `WorkshopFormulas.CalculateRawLevelCost` + `CalculateUpgradeCost`. Der Exponent-Knick liegt
bei Level 500/501: bis Lv500 mit 1.07, ab Lv501 wird die Kette mit 1.06 **vom 1.07^499-Wert bei Lv500
ausgehend** fortgesetzt (kein Reset des Exponenten).

```
RawCost(Lv 1)        = 100 €
RawCost(Lv 2-500)    = 200 € × 1.07^(Lv-1)                       // UpgradeCostBase=200, Exponent=1.07
RawCost(Lv 501+)     = (200 € × 1.07^499) × 1.06^(Lv-500)        // Fortsetzung mit 1.06
                       // Overflow → decimal.MaxValue

UpgradeCost(Lv)      = RawCost(Lv)
                     × (1 - RebirthUpgradeDiscount)   // Rebirth-Sterne, -5..-25% (siehe 4.8)
                     × (1 - min(PrestigeDiscount, 0.50))   // Prestige-Shop, max -50% (PrestigeDiscountCap)
                     × (1 - VipCostReduction)         // 0.0 .. 0.10
WENN Lv >= MaxLevel(1000): UpgradeCost = 0
```

### 4.6 Workshop-Slots (Worker pro Werkstatt)

Quelle: `Workshop.MaxWorkers`, `GameBalanceConstants` (WorkerSlotInterval=50, WorkerSlotMax=20, MaxAdBonusWorkerSlots=3).

```
BaseMaxWorkers = min(20, 1 + (Level-1)/50)     // Integer-Division, +1 Slot alle 50 Level, Cap 20

MaxWorkers = max(1, BaseMaxWorkers
                    + ExtraWorkerSlots                 // Gebäude (WorkshopExtension Lv+1, siehe § 15) + Research
                    + AdBonusWorkerSlots               // Rewarded Ads, PERSISTENT (nicht temporär), max +3
                    + RebirthExtraWorkers              // 0/1/1/2/2/3 je Stern (siehe 4.8)
                    + SpecWorkerCapacityModifier)      // Efficiency-Spez = -1, sonst 0 (siehe 4.7)
```
`CanHireWorker` = `Workers.Count < MaxWorkers`.

### 4.7 Spezialisierung (ab Level 50)

Quelle: `WorkshopSpecialization.cs`, `GameBalanceConstants`. Freischaltung ab Workshop-Level
`SpecializationUnlockLevel = 50`. Erste Wahl gratis; Re-Spec kostet `SpecializationRespecCostGoldenScrews = 20`
GS, aber **gratis unterhalb Workshop-Level 75** (`SpecializationFreeRespecBelowLevel`); Entfernen gratis.
3 Typen (Efficiency/Quality/Economy):

| Spezialisierung | IncomeMod | CostMod | EfficiencyMod | WorkerSlot | AuraMult | OrderReward | Farbe |
|-----------------|-----------|---------|---------------|------------|----------|-------------|-------|
| **Efficiency** | +30% Einkommen | 0 | 0 | −1 Slot | ×1.0 | 0 | `#FF9800` |
| **Quality** | 0 | +15% Kosten | +20% Worker-Effizienz | 0 | **×2.0** (Aura-Bonus verdoppelt) | 0 | `#2196F3` |
| **Economy** | −5% Einkommen | −25% Kosten | 0 | 0 | ×1.0 | **+15% Auftragsbelohnung** | `#4CAF50` |

- **Efficiency:** `gross ×= 1.30`, kostet −1 Worker-Slot.
- **Quality:** `gross ×= 1.20`, `costs ×= 1.15`, Aura-Bonus der Worker wird verdoppelt (×2 über den Standard-50%-Cap hinaus).
- **Economy:** `gross ×= 0.95`, `costs ×= 0.75`, +15% Auftragsbelohnung.

### 4.8 Rebirth-System (0–5 Sterne)

Quelle: `RebirthService.cs`, `Workshop.cs` (RebirthStars), `GameBalanceConstants`
(RebirthIncomeBonuses / RebirthUpgradeDiscounts / RebirthExtraWorkers).

- **Trigger:** Rebirth ist erst möglich, wenn der Workshop **Level 1000 (MaxLevel)** erreicht hat
  (`CanRebirth` = `Level >= 1000 && Stars < 5`). KEIN automatischer Stern pro 100 Level.
- **Durchführung (`DoRebirth`):** setzt Workshop-Level auf 1 zurück, vergibt +1 Stern. Sterne liegen in
  `GameState.WorkshopStars` und überleben **Prestige UND Ascension** (permanent).
- **Kosten pro Stern:** Goldschrauben + prozentualer Geld-Anteil (`RebirthCosts`):

| Stern | GS-Kosten | Geld-Kosten (% des akt. Vermögens) | IncomeBonus | UpgradeDiscount | ExtraWorker |
|-------|-----------|-------------------------------------|-------------|-----------------|-------------|
| 0 | — | — | 0% | 0% | 0 |
| 1 | 50 GS | 10% | +15% | −5% | +1 |
| 2 | 125 GS | 15% | +35% | −10% | +1 |
| 3 | 250 GS | 20% | +60% | −15% | +2 |
| 4 | 200 GS | 25% | +100% | −20% | +2 |
| 5 | 400 GS | 30% | +150% | −25% | +3 |

- `RebirthIncomeBonuses` = `[0.15, 0.35, 0.60, 1.00, 1.50]` (gestaffelt, NICHT pauschal +25%).
- `RebirthUpgradeDiscounts` = `[0.05, 0.10, 0.15, 0.20, 0.25]` (Rabatt auf Upgrade-Kosten, siehe 4.5).
- `RebirthExtraWorkers` (Index = Sterne) = `[0, 1, 1, 2, 2, 3]` (zusätzliche Worker-Slots, siehe 4.6).
- KEIN Mini-Game-Score-Bonus (war erfunden).
- **Visualisierung:** Sterne erscheinen physisch über dem 3D-Gebäude, kosmetisches Glow je Stern-Anzahl.

### 4.9 Manager (14 feste Definitionen)

Quelle: `Manager.cs` (`_allDefinitions`), `Services/ManagerService.cs`. KEINE freien Slots pro Workshop —
es gibt **14 fest definierte Manager**, jeder mit einer festen Fähigkeit (`ManagerAbility`) und entweder an
einen Workshop gebunden oder global wirkend.

- **Level:** Start 1, **Max-Level 5** (`IsMaxLevel = Level >= 5`).
- **Upgrade-Kosten (Goldschrauben):** `Level × 10` GS → Lv1→2: 10 GS, Lv2→3: 20, Lv3→4: 30, Lv4→5: 40 GS.
- **Auto-Unlock:** Ein Manager wird freigeschaltet, sobald **alle** seiner gesetzten Bedingungen erfüllt sind
  (`PlayerLevel >= RequiredLevel`, `TotalPrestigeCount >= RequiredPrestige`, `PerfectRatings >= RequiredPerfectRatings`).
- **6 ManagerAbilities:** AutoCollectOrders, EfficiencyBoost, FatigueReduction, MoodBoost, IncomeBoost, TrainingSpeedUp.

**Per-Level-Boni (`Manager.GetBonus`):**

| Ability | Bonus pro Level | Beispiel Lv5 |
|---------|-----------------|--------------|
| EfficiencyBoost | +5%/Lvl | +25% |
| FatigueReduction | −3%/Lvl | −15% |
| MoodBoost | +4%/Lvl (im WorkerService auf max 50% gedeckelt) | +20% |
| IncomeBoost | +5%/Lvl | +25% |
| TrainingSpeedUp | +10%/Lvl | +50% |
| AutoCollectOrders | = Level (Anzahl pro Check) | 5 |

Workshop-gebundene Manager wirken via `GetManagerBonusForWorkshop(type, ability)`, globale via
`GetGlobalManagerBonus(ability)` — beide Bonus-Quellen sind additiv.

**Die 14 Manager** (Id, Default-Name, Ziel, Ability, Unlock-Bedingung):

| # | Id | Name | Workshop / Global | Ability | Unlock |
|---|----|------|-------------------|---------|--------|
| 1 | mgr_hans | Hans | Carpenter | EfficiencyBoost | Spieler-Lv 10 |
| 2 | mgr_fritz | Fritz | Plumber | FatigueReduction | Spieler-Lv 20 |
| 3 | mgr_kurt | Kurt | Electrician | IncomeBoost | Spieler-Lv 30 |
| 4 | mgr_lisa | Lisa | Painter | MoodBoost | Spieler-Lv 40 |
| 5 | mgr_karl | Karl | Roofer | EfficiencyBoost | Spieler-Lv 60 |
| 6 | mgr_otto | Otto | Contractor | IncomeBoost | Spieler-Lv 80 |
| 7 | mgr_anna | Anna | Architect | FatigueReduction | 25 Perfect Ratings |
| 8 | mgr_max | Max | GeneralContractor | IncomeBoost | Spieler-Lv 100 |
| 9 | mgr_schmied | Schmied | MasterSmith | EfficiencyBoost | Spieler-Lv 120 |
| 10 | mgr_erfinder | Erfinder | InnovationLab | IncomeBoost | Spieler-Lv 140 |
| 11 | mgr_schmidt | Schmidt | global | TrainingSpeedUp | 1× Prestige |
| 12 | mgr_weber | Weber | global | AutoCollectOrders | 2× Prestige |
| 13 | mgr_mueller | Müller | global | EfficiencyBoost | 3× Prestige |
| 14 | mgr_kaiser | Kaiser | global | IncomeBoost | 4× Prestige |

- **Prestige-Reset:** Manager-Level wird bei Legende-Prestige (Tier 7) auf 1 zurückgesetzt.

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

*\*Alle Tiers ab S brauchen die **eine** S-Tier-Research (`mgmt_10`, UnlocksSTierWorkers) — sie schaltet
S, SS, SSS UND Legendary frei. Zusätzlich greift das Spielerlevel-Gate (S 45 / SS 100 / SSS 250 / Legendary 500).
Es gibt kein separates „mgmt_20". Headhunter (`mgmt_04`) erweitert nur die Markt-Pool-Größe (5→8).*

**Level-Fit-Faktor** (`CalculateLevelFitFactor` — Malus bei zu hohem Workshop-Level für niedrigen Tier):
```
WENN Workshop-Level <= 30:  Faktor = 1.0
steps           = Workshop-Level / 30                       // Integer-Division (LevelPenaltyStep=30)
basePenalty     = steps × 0.02                              // LevelPenaltyPerStep
totalResistance = min(1.0, Tier.LevelResistance + levelResistanceBonus)
adjustedPenalty = basePenalty × (1 - totalResistance)
Faktor          = max(0.20, 1 - adjustedPenalty)            // MinLevelFitFactor = 0.20
```
- Höhere `LevelResistance` (siehe Tabelle) **mildert** den Malus — Legendary (1.00) ist komplett immun.
- **Aura-Bonus** (S/SS/SSS/Legendary) ist KEINE Penalty-Reduktion, sondern ein additiver Workshop-Income-Multiplikator
  (Σ aller Auren, gedeckelt auf 50%, siehe § 4.3). Quality-Spezialisierung verdoppelt diesen Aura-Beitrag.

### 5.1b Worker-Roll beim Anheuern (je Tier)

Quelle: `Worker.CreateForTier`. Beim Anheuern werden Effizienz, Talent, Spezialisierung und Material-Affinität
zufällig gerollt — abhängig vom Tier:

| Tier | Eff-Roll (Min..Max) | Talent-Stars | Spez-Chance |
|------|---------------------|--------------|-------------|
| F | 0.30–0.50 | 1–2 | 40% |
| E | 0.50–0.80 | 1–3 | 40% |
| D | 0.75–1.25 | 2–3 | 50% |
| C | 1.10–1.90 | 2–4 | 50% |
| B | 1.70–2.80 | 3–4 | 65% |
| A | 2.50–4.20 | 3–5 | 65% |
| S | 3.80–6.00 | 4–5 | 85% |
| SS | 5.50–9.00 | 4–5 | 85% |
| SSS | 8.50–14.00 | 5 (fix) | 85% |
| Legendary | 13.00–22.00 | 5 (fix) | 85% |

- Base-Efficiency: `minEff + (maxEff−minEff) × random`, gerundet auf 3 Nachkommastellen.
- Bei Spez-Treffer: zufälliger der 10 WorkshopTypes als bevorzugte Werkstatt (+15% Effizienz dort, siehe § 5.2).
- Material-Affinität: gleichverteilt 1 von 5 (Wood/Metal/Stone/Art/Tech, je 20% — None wird NICHT gerollt).
- `GetHiringCost(playerLevel)` = `round(BaseHiringCost × (1 + max(0, playerLevel−1) × 0.02))` (+2% pro Spielerlevel
  über 1; Lv50 ≈ 2.0×, Lv100 ≈ 3.0×). A/S/SS/SSS/Legendary kosten zusätzlich Goldschrauben (Hire-GS-Spalte oben).

### 5.2 Worker-Stats & EffectiveEfficiency

Quelle: `Worker.cs` (`EffectiveEfficiency`, gecacht). **Wichtig:** Der Worker-`ExperienceLevel` läuft von
**1 bis 10** (NICHT 1–1000 — das ist der Workshop-Level). Bei Level 10 stoppt das Effizienz-Training.

| Stat | Range | Wirkung |
|------|-------|---------|
| **ExperienceLevel** | 1–10 | +3% Effizienz pro Level (max +27% bei Lv10) |
| **XP** | 0–∞ | XP-Bedarf für nächstes Level = `ExperienceLevel × 200`; Level-Up: `Efficiency += (tierMax−tierMin) × 0.05` |
| **Mood** | 0–100 | nicht-linearer Faktor (siehe § 5.3) |
| **Fatigue** | 0–100 | nicht-linearer Faktor (siehe § 5.4) |
| **Personality** | 1 von 6 | Steady/Perfectionist/Cheerful/Ambitious/Relaxed/Specialist (siehe § 5.5b) |
| **Affinity (Material)** | 1 von 5 | Wood/Metal/Stone/Art/Tech — Crafting-Speed, kein Effizienz-Einfluss (siehe § 12 / § 5.7) |
| **Specialization** | 1 von 10 WorkshopTypes (optional) | +15% Effizienz, wenn im bevorzugten Workshop eingesetzt |
| **Talent-Stars** | 1–5 | +5% Effizienz pro Star über 1 (1★=1.00x, 3★=1.10x, 5★=1.20x) |

**EffectiveEfficiency-Formel** (0 wenn ruhend oder im Training):
```
result = baseEff                              // beim Hiring gerollt (Tier-Range)
       × (1 + ExperienceLevel × 0.03)         // XP-Bonus
       × MoodFactor                           // siehe § 5.3
       × FatigueFactor                        // siehe § 5.4
       × (1 + SpecBonus + EquipBonus)         // SpecBonus = 0.15 (+ Specialist-Personality +0.15) wenn im bevorzugten Workshop
       × Personality.EfficiencyMultiplier     // siehe § 5.5b
       × (1 + (Talent − 1) × 0.05)            // Talent-Bonus
```
`MaxEfficiency` (Anzeige) = `Tier.MaxEff × (1 + ExpLvl × 0.03) × (1 + (Talent−1) × 0.05)`.

### 5.3 Worker-Mood-System

Quelle: `Worker.cs`, `WorkerService.cs`, `GameBalanceConstants`.

- **Initial Mood:** 80/100. Schwellen: Happy 80+, Neutral 50+, Critical 20−.
- **MoodFactor** (nicht-linear, `GetMoodFactor`):
  ```
  Mood >= 80:  1.0 + (Mood−80)/200       // Mood 100 = 1.1x, Mood 80 = 1.0x
  Mood >= 50:  0.8 + (Mood−50)/150       // Mood 50 = 0.8x
  sonst:       0.5 + Mood/100            // Mood 0 = 0.5x
  ```
- **Mood-Decay pro Stunde** (Basis 3): `3 × Personality.MoodDecayMultiplier × (1 − MoraleBonus)`,
  zusätzlich reduziert (multiplikativ) durch: Prestige-Shop-MoodDecayReduction, Manager-MoodBoost
  `×(1 − min(bonus, 0.50))`, Gilden-Bonus, Equipment-MoodBonus.
- **Passive Erholung beim Arbeiten:** Kantine `MoodRecoveryPerHour` (siehe § 15) wird vom Decay abgezogen
  (kann Mood netto steigen lassen).
- **Erholung in Ruhephase:** `(1 + Kantine.MoodRecoveryPerHour) × (1 + Equipment.MoodBonus)` pro Stunde (Basis +1/h).

**Quit-Mechanik** (`WorkerService.UpdateWorkerStates`):
- `WillQuit` = Mood < 20. Bei erstmaligem Unterschreiten: `QuitDeadline = UtcNow + 24h` + Warn-Event.
- Erreicht die Deadline (auch offline) ohne Erholung → Worker kündigt (entfernt, Analytics-Event).
- Steigt Mood wieder über 20 → `QuitDeadline = null` (Reset).
- `GiveBonus`: kostet 8h Lohn (`WagePerHour × 8`), gibt +30 Mood (max 100), setzt `QuitDeadline = null`.

**Visualisierung (3D):** 4 Gesichtstexturen (Happy/Neutral/Sad/Frustrated) auf separatem UV-Set, Material-Slot-Swap synchronisiert mit Animator-State.

### 5.4 Worker-Fatigue-System

Quelle: `Worker.cs`, `WorkerService.cs`.

- **FatigueFactor** (nicht-linear, `GetFatigueFactor`):
  ```
  Fatigue <= 0:    1.0
  Fatigue >= 100:  0.5
  sonst:           1.0 − Fatigue/200     // Fatigue 50 = 0.85x
  ```
- **Fatigue-Anstieg pro Stunde** (Basis 12.5): `12.5 × Personality.FatigueMultiplier × (1 − EnduranceBonus)`,
  zusätzlich reduziert durch Gilden-FatigueReduction + Equipment-FatigueReduction.
- **Fatigue-Exhausted:** 100 (`IsTired`). Bei Fatigue >= 100 → **Auto-Rest** (gemerkter `ResumeTrainingType`).
- **Rest Hours Needed:** 4h (ohne Kantine-Bonus). Fatigue-Erholung in Ruhe: `(100 / 4) × (1 + Kantine.RestTimeReduction)`
  pro Stunde + Equipment-FatigueReduction. Auto-Ende bei Fatigue <= 0 → Auto-Resume des gemerkten Trainings.
- **Kantine** (siehe § 15): RestTimeReduction 50%/55%/60%/70%/80% je Level 1–5.

### 5.5 Praktikanten (Intern-System, F→E-Promotion)

Quelle: `WorkerService.cs` (HireIntern/PromoteIntern/DeclineInternPromotion), `Worker.InternAwaitingPromotion`.

- **Anheuern:** F-Tier, `WagePerHour = 0` (kostenlos), `IsIntern = true`, Mood 80. Max **2** Praktikanten gleichzeitig.
- **Fortschritt:** `InternProgressTicks += max(1, deltaSeconds)` nur wenn aktiv arbeitend (nicht ruhend). Tick =
  1s **aktive Spielzeit** (NICHT Echtzeit).
- **Reife nach 86.400 Ticks** (= 24h aktive Spielzeit) → `InternAwaitingPromotion = true` + Event. Jetzt
  entscheidet der **Spieler**:
  - **Promotion:** Praktikant wird zu **E-Tier**-Worker (`IsIntern = false`, Tier=E, `WagePerHour = 9` — ab jetzt lohnpflichtig).
  - **Ablehnen:** Praktikant verlässt die Werkstatt (entfernt).

### 5.6 Worker-Training

Quelle: `WorkerService.cs`, `TrainingType.cs`. **3 Training-Typen** (keine 5 Stufen, sondern kontinuierliche
Boni bzw. ein XP-Level-Cap):

| Training | Effekt pro Stunde | Cap / Stopp-Bedingung |
|----------|-------------------|------------------------|
| **Efficiency** | XP `+= 50 × Personality.XpMult × SpeedMult`; bei Level-Up `Efficiency += (tierMax−tierMin) × 0.05` | nur bis `ExperienceLevel < 10` (blockt ab Lv10) |
| **Endurance** | `EnduranceBonus += 0.05 × deltaHours × SpeedMult` | Cap **0.5** (= 50% Fatigue-Reduktion), Auto-Stopp |
| **Morale** | `MoraleBonus += 0.05 × deltaHours × SpeedMult` | Cap **0.5** (= 50% MoodDecay-Reduktion), Auto-Stopp |

- **Training-Kosten:** `2 × WagePerHour` pro Stunde (`TrainingCostMultiplier = 2`); bei Nicht-Leistbarkeit wird das Training gestoppt.
- **Training-XP-Basis (Efficiency):** 50 XP/h (`TrainingXpPerHour`), moduliert durch Personality-XP-Mult (Ambitious ×1.25).
- **Speed-Multiplikator** = `TrainingCenter.TrainingSpeedMultiplier × (1 + GildenTrainingSpeedBonus + ManagerTrainingBonus)`.
  Trainings-Zentrum (Gebäude, siehe § 15) liefert 2.5x bis 6.5x.
- Training erhöht Fatigue mit halber Arbeitsrate (`FatiguePerHour × 0.5` − Equipment-FatigueReduction); bei
  Fatigue >= 100 → Auto-Rest, danach Auto-Resume des gemerkten Trainings.
- **Passiver Arbeits-XP** (ohne Training): 25% der Trainingsrate = 12.5 XP/h × Personality.XpMult.

### 5.5b Worker-Personalities (6 Typen)

Quelle: `WorkerPersonality.cs`, `Worker.CalculateMarketPrice`. Beim Anheuern gleichverteilt gerollt:

| Personality | EffizienzMult | MoodDecayMult | FatigueMult | XpMult | SpecBonus | Preis-Mult |
|-------------|---------------|---------------|-------------|--------|-----------|------------|
| Steady | 1.00 | 1.0 | 1.0 | 1.0 | 0.0 | 1.00 |
| Perfectionist | 1.20 | 1.5 | 1.0 | 1.0 | 0.0 | 1.20 |
| Cheerful | 0.90 | 0.5 | 1.0 | 1.0 | 0.0 | 1.05 |
| Ambitious | 1.00 | 1.0 | 1.25 | 1.25 | 0.0 | 1.10 |
| Relaxed | 0.85 | 1.0 | 0.70 | 1.0 | 0.0 | 0.90 |
| Specialist | 1.00 | 1.0 | 1.0 | 1.0 | +0.15 | 1.15 |

- **Specialist:** +0.15 zusätzlicher Spez-Bonus (im bevorzugten Workshop → total +0.30 Effizienz); erleidet
  zudem KEINEN Mood-Hit (−5) beim Transfer.

### 5.7 Material-Affinity-Matching (V7)

- 20% Drop-Chance pro Affinity beim Hiring (gleichverteilt 5×20%)
- Alte Worker werden bei Migration deterministisch via WorkerId-Hash zugewiesen
- **Crafting-Speed-Bonus:** bis +20% wenn alle Worker einer Werkstatt die Output-Material-Affinität matchen
- Anteilig: 3 von 5 Workern Match = +12% Speed

### 5.8 Worker-Markt

Quelle: `WorkerMarketPool.cs`, `WorkerService.cs`.

- **Rotation-Zyklus:** alle **4 Stunden** (`NextRotation = UtcNow.AddHours(4)`).
- **Pool-Größe:** 5 Worker (8 mit Headhunter-Research `mgmt_04`).
- **Gratis-Refresh:** 1× pro Rotation kostenlos (manueller `RefreshMarket` bewahrt das Flag; nur Rotation resettet es).
- **Tier-Gewichtung** (`GetWeightedTier`, nur verfügbare Tiers je Spielerlevel/Research):

| Tier | Gewicht |
|------|---------|
| F | 20.0 |
| E | 22.0 |
| D | 22.0 |
| C | 14.0 |
| B | 10.0 |
| A | 6.0 |
| S | 3.0 |
| SS | 1.5 |
| SSS | 0.5 |
| Legendary | 0.1 |

- Verfügbar ist ein Tier, wenn `playerLevel >= UnlockLevel`; S+ zusätzlich nur mit S-Tier-Research (`mgmt_10`).
- **Legendary-Cooldown:** 7 Tage nach letzter Legendary-Sichtung wird Legendary aus dem Pool gefiltert (verhindert Farming).

**Marktpreis-Formel** (`CalculateMarketPrice`):
```
baseCost        = Tier.GetHiringCost(playerLevel)            // BaseHiringCost × (1 + (lvl−1)×0.02), gerundet
talentMult      = 0.70 + (Talent−1) × 0.15                   // 1★=0.70 .. 5★=1.30
personalityMult = Personality-Preis-Mult (siehe § 5.5b)
specMult        = Specialization vorhanden ? 1.15 : 1.0
effPosition     = clamp((Eff−minEff)/(maxEff−minEff), 0, 1)
effMult         = 0.85 + effPosition × 0.30                  // 0.85x .. 1.15x

MarketPrice = round(baseCost × talentMult × personalityMult × specMult × effMult)
```
- **Ad-Refresh:** 1× pro Rotation kostenlos via Rewarded Ad (kein Premium-Drop-Rate-Bonus im Original).

### 5.9 Worker-Auktionen (Gilden-Feature)

Quelle: `WorkerAuctionService.cs`. Firebase-Pfad `guilds/{guildId}/auctions/{auctionId}`.

- **Dauer:** 30s aktive Phase (`AuctionDuration = 30s`). **Kein** Tageslimit.
- **Mindestgebot-Steigerung:** `HighestBid > 0 ? ceil(HighestBid × 1.1) : 100` € (= +10% des Höchstgebots, Mindest-100 €).
- **1s-Cooldown** gegen Spam-Bidding (Client) + serverseitige bidTimestamps-Rule.
- **Tier-Verteilung beim Spawn** (`roll = rng.Next(0,100)`): S 70% (`roll<70`), SS 25% (70–94), SSS 5% (>=95).
- **Master-Client-Pattern:** Spieler mit lexikografisch kleinster aktiver PlayerId spawnt Auktionen + treibt NPC-Bots (Solo = immer Master). Aktiv = LastActiveAt < 30 Tage.
- **NPC-Bots** (`RunNpcBotTick`, alle 5s, nur Master): 35% Bid-Chance pro Tick; 1–3 Bots pro Auktion;
  Bid = `currentMin + round(currentMin × (0.05 + rnd×0.20))` (5–25% über Min); Bot-Maximum S=50.000 / SS=250.000 / SSS=1.000.000 €.
- **Geld-Locking:** nur das Delta zum bisherigen Eigen-Gebot wird abgezogen; bei Ablehnung Refund.
- **HMAC-signiert** (über AuctionId + WorkerTier + Name + Status + HighestBidder + HighestBid + sortierte AllBids); Multi-Path-PATCH (kein PUT), highestBid-Monotonie-Rule lehnt Verlierer-Bids ab.
- **Settlement (`SettleAsync`/`ApplyRefunds`):** idempotent via `ClaimedAuctionIds`. Gewinner erhält den Worker
  (`CreateForTier`) im ersten freigeschalteten Workshop mit freiem Slot — **kein freier Slot → Gebot wird erstattet**.
  Verlierer: vollständiger Refund.

### 5.10 3D-Visualisierung (gemäß ASSETS_AI.md § 5+9)

- 10 Basis-Modelle (5 m/w-Paare) + ~120 Skin-Varianten via Material-Color-Swap
- **Mecanim-Animator-States:** Idle (4 Mood-Variants), Walking, Hammering, Sawing, Painting, Frustrated-Outburst, Happy-Cheer
- **Affinity-Props an Hand-Bone:** Hammer (Wood), Schraubenschlüssel (Metal), Maurer-Kelle (Stone), Pinsel (Art), Tablet (Tech)
- Worker laufen physisch durch die 3D-Stadt (NavMesh)

---

## 6. Aufträge (6 Types + 3 Strategien)

> Verbindliche Werte 1:1 aus dem Avalonia-Original (`OrderType.cs`, `OrderDifficulty.cs`, `OrderStrategy.cs`, `OrderGeneratorService.cs`, `Order.cs`, `GameBalanceConstants.cs`). Gleiche Mechanik, gleiche Formeln, gleiche Zahlen — die Unity-Version unterscheidet sich ausschliesslich in der 3D-Präsentation (siehe §6.9).

### 6.1 Order-Types

`TaskCount (min,max)` aus `OrderType.GetTaskCount()`. Deadline nur bei Weekly und MaterialOrder — Standard/Large/Cooperation/Quick haben KEINE Deadline (`HasDeadline = false`).

| Type | Index | TaskCount (min,max) | Reward-Mult. | XP-Mult. | Unlock-Lv | Deadline | Mehrere Werkstätten |
|------|-------|---------------------|--------------|----------|-----------|----------|---------------------|
| **Quick** | 0 | (1, 1) | 0.6x | 0.5x | 1 | — | nein |
| **Standard** | 1 | (2, 3) | 1.0x | 1.0x | 1 | — | nein |
| **Large** | 2 | (4, 6) | 1.8x | 2.0x | 10 | — | nein |
| **Weekly** | 3 | (10, 10) | 3.0x | 3.0x | 20 | 7 Tage | nein |
| **Cooperation** | 4 | (3, 3) | 2.5x | 3.0x | 15, ≥2 Werkstätten | — | ja (Cross-Workshop) |
| **MaterialOrder** | 5 | (0, 0) — keine MiniGames | 1.8x | 1.5x | 50 (AutoProduktion) | 4 Std. | nein |

**Parallel-Limit:** Max 3 aktive Aufträge gleichzeitig.

**Icons (GameIconKind):** Quick=Flash, Standard=ClipboardList, Large=ClipboardTextMultiple, Weekly=CalendarCheck, Cooperation=Handshake, MaterialOrder=PackageVariantClosed.

### 6.1a Reward-/XP-Formel (verbindlich, 1:1 Original)

**Basis-Werte bei Generierung** (`OrderGeneratorService.ComputeBaseRewardAndXp`):

```
netIncomePerSecond = Max(0, state.NetIncomePerSecond)
perTaskReward      = Max(100 + playerLevel × 100, netIncomePerSecond × 300)
taskMultiplier     = taskCount × (1.0 + (taskCount - 1) × 0.15)
baseReward         = perTaskReward × taskMultiplier × WorkshopType.GetBaseIncomeMultiplier()
   wenn GuildMembership.ResearchRewardBonus > 0:  baseReward ×= (1 + ResearchRewardBonus)
baseXp             = 25 × workshopLevel × taskCount
   wenn GuildMembership.ResearchXpBonus > 0:      baseXp = (int)(baseXp × (1 + ResearchXpBonus))
```

`BaseReward` wird als `Math.Round(baseReward)` gespeichert. `taskMultiplier`-Beispiele: 1 Task=1.0, 2=2.30, 3=3.90, 4=5.80, 5=8.00, 6=10.50, 10=23.50.

**WorkshopType.GetBaseIncomeMultiplier():** Carpenter 1.0, Plumber 1.5, Electrician 2.0, Painter 2.5, Roofer 3.0, Contractor 4.0, Architect 5.0, GeneralContractor 7.0, MasterSmith 3.0, InnovationLab 5.0.

**Final-Reward bei Abschluss** (`Order.cs`):

```
FinalReward = HasHardFailed || TaskResults.Count == 0 ? 0
            : BaseReward × avg(TaskResults.RewardPercentage)
                         × Difficulty.RewardMult × OrderType.RewardMult × Strategy.RewardMult

FinalXp     = HasHardFailed || TaskResults.Count == 0 ? 0
            : (int)( BaseXp × avg(TaskResults.XpPercentage)
                            × Difficulty.XpMult × OrderType.XpMult × Strategy.XpMult )
```

`EstimatedReward` (Dashboard-Anzeige) lässt den avg-Faktor weg (geht von Good=100% aus): `BaseReward × Difficulty.RewardMult × OrderType.RewardMult × Strategy.RewardMult`.

**MiniGame-Rating → Prozentwerte** (`MiniGameRating`): Miss=20%, Ok=50%, Good=100%, Perfect=150% (Reward UND XP identisch; Spread Miss→Perfect = 7.5x).

Hinweis: `ComboMultiplier`, `IsScoreDoubled` (Rewarded-Ad-Verdopplung) und `ReputationBonus` sind NICHT Teil von FinalReward/FinalXp — sie werden extern in der Completion-Logik angewendet.

**RecalculateAvailableOrderRewards:** wartende Orders werden bei Income-/Level-Änderung neu berechnet (überspringt Orders mit `TaskResults.Count > 0` und MaterialOrders). Bei `IsPremium`: `newBaseReward ×= 3`, `newBaseXp = (int)(newBaseXp × 2.5)`.

### 6.1b DetermineOrderType — level-abhängige Würfeltabelle (1:1 Original)

`roll = Random.Shared.Next(100)` (0..99). Reputation/Gilde/Research senken den effektiven Roll:

```
adjustedRoll = Clamp( roll - (OrderQualityBonus + GuildOrderQualityBonus + ResearchPremiumOrderChance) × 100, 0, 100 )
```

- `OrderQualityBonus` (aus `CustomerReputation`): Score<30 → −0.10, <60 → 0, <80 → +0.10, sonst +0.20.
- `GuildOrderQualityBonus` = `GuildMembership.ResearchOrderQualityBonus` (Default 0).
- `ResearchPremiumOrderChance` = `Research.GetTotalEffects().PremiumOrderChance` (Default 0).

`unlockedWorkshops` = Anzahl freigeschalteter Werkstätten.

| playerLevel | Bedingung | Schwellen (adjustedRoll) |
|-------------|-----------|--------------------------|
| < 10 | — | immer Standard |
| < 15 | — | <70 Standard, sonst Large |
| < 20 | unlockedWorkshops ≥ 2 | <55 Standard, <80 Large, sonst Cooperation |
| < 20 | unlockedWorkshops < 2 | <70 Standard, sonst Large |
| ≥ 20 | unlockedWorkshops ≥ 2 | <45 Standard, <70 Large, <85 Cooperation, sonst Weekly |
| ≥ 20 | unlockedWorkshops < 2 | <55 Standard, <80 Large, sonst Weekly |

DetermineOrderType erzeugt NIE Quick oder MaterialOrder — diese kommen aus eigenen Pfaden (QuickJob-Service bzw. MaterialOrder-Generierung).

### 6.1c GetDifficulty — Matrix (WS-Level × Prestige, + Reputation-Gate) (1:1 Original)

`roll = Random.Shared.Next(100)` (0..99). `prestigeCount = Prestige.TotalPrestigeCount`. Match auf `(workshopLevel, prestigeCount)`:

**WS-Level 1–25:**
| Prestige | Verteilung |
|----------|-----------|
| 0 | <80 Easy, sonst Medium |
| 1 | <65 Easy, <90 Medium, <95 Hard, sonst Expert |
| 2 | <50 Easy, <80 Medium, <95 Hard, sonst Expert |
| ≥3 | <40 Easy, <70 Medium, <90 Hard, sonst Expert |

**WS-Level 26–100:**
| Prestige | Verteilung |
|----------|-----------|
| 0 | <45 Easy, <90 Medium, sonst Hard |
| 1 | <25 Easy, <65 Medium, <90 Hard, sonst Expert |
| 2 | <15 Easy, <45 Medium, <80 Hard, sonst Expert |
| ≥3 | <5 Easy, <30 Medium, <65 Hard, sonst Expert |

**WS-Level 101–300:**
| Prestige | Verteilung |
|----------|-----------|
| 0 | <15 Easy, <60 Medium, sonst Hard |
| 1 | <5 Easy, <30 Medium, <75 Hard, sonst Expert |
| 2 | <15 Medium, <60 Hard, sonst Expert |
| ≥3 | <10 Medium, <50 Hard, sonst Expert |

**WS-Level 301–700:**
| Prestige | Verteilung |
|----------|-----------|
| 0 | <5 Easy, <35 Medium, sonst Hard |
| 1 | <10 Medium, <60 Hard, sonst Expert |
| 2 | <5 Medium, <45 Hard, sonst Expert |
| ≥3 | <30 Hard, sonst Expert |

**WS-Level 701+:**
| Prestige | Verteilung |
|----------|-----------|
| 0 | <20 Medium, sonst Hard |
| 1 | <5 Medium, <45 Hard, sonst Expert |
| 2 | <30 Hard, sonst Expert |
| ≥3 | <20 Hard, sonst Expert |

**Expert-Gate:** Ergibt das Würfeln Expert, aber `Reputation.ReputationScore < 80` (`Expert.GetRequiredReputation()`), fällt das Ergebnis auf Hard zurück.

### 6.1d OrderDifficulty — 4 Stufen (1:1 Original)

| Difficulty | Index | Reward-Mult | XP-Mult | PerfectZoneSize | Speed-Mult | Req. Reputation | Sterne |
|------------|-------|-------------|---------|-----------------|------------|-----------------|--------|
| **Easy** | 1 | 1.0x | 1.0x | 0.20 | 0.9x | 0 | 1 |
| **Medium** | 2 | 1.5x | 1.75x | 0.12 | 1.2x | 0 | 2 |
| **Hard** | 3 | 3.5x | 3.0x | 0.09 | 1.6x | 0 | 3 |
| **Expert** | 4 | 5.0x | 5.5x | 0.06 | 2.2x | 80 | 4 |

### 6.1e Template-Auswahl + Task-Generierung

- `maxTemplateIndex = Min(templates.Count - 1, (workshopLevel - 1) / 2)` — höhere WS-Level schalten schwerere Templates frei.
- Template per `Random.Shared.Next(0, maxTemplateIndex + 1)`; `targetTaskCount = Random.Shared.Next(minTasks, maxTasks + 1)` aus `OrderType.GetTaskCount()`.
- Standard/Large/Weekly: `GameType = template.GameTypes[i % template.GameTypes.Length]`.
- Cooperation: ein zweiter freigeschalteter Workshop liefert `secondTemplate`; Tasks wechseln ab (gerade Index = primär, ungerade = sekundär). `RequiredWorkshops = [primär, sekundär]`.

Order-Templates pro Workshop (TitleKey → MiniGame-Sequenz):
- **Carpenter:** Shelf (Sawing) · Cabinet (Sawing, Planing) · Table (Sawing, Planing, Sawing) · Deck (Measuring, Sawing, Sawing) · Garden Shed (Sawing, Sawing, Sawing)
- **Plumber:** Faucet (PipePuzzle) · Toilet (PipePuzzle×2) · Shower (PipePuzzle×2) · Bathroom (PipePuzzle×3)
- **Electrician:** Outlet (WiringGame) · Light Fixture (WiringGame) · Panel (WiringGame×2) · Smart Home (WiringGame×3)
- **Painter:** Room (PaintingGame) · Exterior (PaintingGame×2) · Entire House (PaintingGame×3)
- **Roofer:** Repair Roof (RoofTiling) · New Roof (RoofTiling×2) · Complete Roof (RoofTiling, TileLaying, RoofTiling)
- **Contractor:** Renovation (Blueprint, Sawing) · Addition (Blueprint, Sawing, WiringGame) · Multi-Unit (Blueprint, Blueprint, PipePuzzle, WiringGame)
- **Architect:** Blueprint (DesignPuzzle) · Floor Plan (DesignPuzzle×2) · Full Design (DesignPuzzle, Blueprint, DesignPuzzle)
- **GeneralContractor:** House (Inspection, Sawing, PipePuzzle) · Commercial (Inspection, Blueprint, WiringGame) · Luxury Villa (Inspection, Inspection, RoofTiling, DesignPuzzle)
- **MasterSmith:** Forge Blade (ForgeGame) · Master Tools (ForgeGame×2) · Forge Artifact (ForgeGame×3)
- **InnovationLab:** Prototype (InventGame) · Invention (InventGame×2) · Breakthrough (InventGame×3)

### 6.1f Kundenname + Stammkunden

- Kundenname deterministisch aus `nameSeed` über 30 Vornamen + 30 Nachnamen; `CustomerAvatarSeed = nameSeed.ToString("X8")`.
- Stammkunden-Chance: `regularCustomerChance = 0.20 + CurrentTier.GetRegularCustomerBonus()` (Beginner 0, CityKnown +0.10, RegionStar +0.20, IndustryLegend +0.35). RepShop-Garantie `RepShopRegularCustomerCharges > 0` erzwingt einen Stammkunden (verbraucht eine Ladung).
- **Order-Sticky-Strategy:** Bei Generierung wird `order.Strategy = workshop.DefaultRiskStrategy` gesetzt (pro Workshop voreingestellt).

### 6.1g Verfügbare Orders generieren + Refresh

`GenerateAvailableOrders(count=3)`: `totalCount = count + Office.ExtraOrderSlots + Research.ExtraOrderSlots + Reputation.ExtraOrderSlots + GuildMembership.ResearchOrderSlotBonus`. Ohne freigeschaltete Workshops: 1 Carpenter-Order (Level 1). `RequiredLevel` bei Generierung = `Max(1, workshopLevel - 1)`.

`RefreshOrders()`: behält nicht-abgelaufene MaterialOrders, generiert 3 neue normale Orders + 1 MaterialOrder (wenn keine existiert).

### 6.2 Order-Strategien (1:1 Original)

`ToleranceMult` skaliert die Sweet-Spot-/Zonen-Breite, `Zeit-Mult` die verfügbare MiniGame-Zeit. Der Spieler wählt die Strategie vor MiniGame-Start (Default = `workshop.DefaultRiskStrategy`).

| Strategy | Index | Reward-Mult | XP-Mult | Toleranz-Mult (Zonen) | Speed-Mult | Zeit-Mult | Hard-Fail | Rep-Penalty bei Miss |
|----------|-------|-------------|---------|------------------------|------------|-----------|-----------|----------------------|
| **Safe** | 0 | 0.75x | 0.75x | 1.5 (+50% breiter) | 0.7x | 1.3 (+30% Zeit) | Nein | 0 |
| **Standard** | 1 | 1.0x | 1.0x | 1.0 | 1.0x | 1.0 | Nein | 0 |
| **Risk** | 2 | 2.0x | 1.75x | 0.5 (−50% schmaler) | 1.3x | 0.7 (−30% Zeit) | Ja → FinalReward 0 € + FinalXp 0 | −10 |

### 6.3 Live-Orders (Live-Stream / VIP) (1:1 Original)

Konstanten (`OrderGeneratorService`):

| Konstante | Wert |
|-----------|------|
| MaxLiveOrdersCap | 5 (gleichzeitig) |
| PremiumSpawnChance | 0.05 (5%) |
| LiveExpiryMinSeconds / Max | 45 / 180 |
| PremiumExpiryMinSeconds / Max | 45 / 90 |

- **Spawn-Loop (GameLoop):** Expiry-Check alle 3 Ticks (Early-Exit wenn keine Live-Order); Spawn alle 25 Ticks mit 50% Chance.
- **GenerateLiveOrder:** zufälliger freigeschalteter Workshop → `IsLive = true`. Premium-Chance `effectivePremiumChance = tierLiveBonus > 0 ? tierLiveBonus : 0.05` (RegionStar 0.05, IndustryLegend 0.10, sonst 0). `isPremium = NextDouble() < effectivePremiumChance`.
- **Premium (VIP):** `IsPremium=true`, `BaseReward ×= 3`, `BaseXp = (int)(BaseXp × 2.5)`, `ExpiresAt = UtcNow + Next(45, 91)` Sek.
- **Nicht-Premium:** `ExpiresAt = UtcNow + Next(45, 181)` Sek.
- **Pause-Logik (`GetEffectiveNow`):** `now − pauseTotal`, gecappt auf 5 Minuten kumulativ (`AccumulatedPauseDuration` + laufende Pause). `LiveCountdownText`: `"{m}m {ss:00}s"` ab ≥60s, sonst `"{sec}s"`.

### 6.4 Material-Order / Lieferauftrag (V7) (1:1 Original)

Liefert Crafting-Inventar statt MiniGames zu spielen (keine Tasks). Eigene Generierung (`GenerateMaterialOrder`):

| Konstante | Wert |
|-----------|------|
| Unlock-Level | 50 (AutoProductionUnlockLevel) |
| Reward-Multiplier | 1.8x (MaterialOrderRewardMultiplier) |
| XP-Multiplier | 1.5x (MaterialOrderXpMultiplier) |
| Max pro Tag | 5 (MaterialOrdersPerDay) |
| Deadline | 4 Std. (MaterialOrderDeadlineHours) |
| Cross-Workshop-Level | 100 (MaterialOrderCrossWorkshopLevel) |

- Tages-Reset bei neuem UTC-Tag (`Statistics.MaterialOrdersCompletedToday = 0`); bei erreichtem Tageslimit → keine neue MaterialOrder.
- Nur Workshops mit freigeschalteter Auto-Produktion qualifizieren. Hauptprodukt = Tier-1-Produkt des Workshops; `mainCount = 5 + Min(playerLevel / 50, 10)` (5..15).
- Cross-Workshop ab Level 100 (bei ≥2 qualifizierten Workshops): zweites Produkt, Menge `3 + Min(playerLevel / 100, 5)` (3..8).
- **Reward:** `perItemReward = Max(100 + playerLevel × 100, netIncome × 300)`; `baseReward = perItemReward × (1.0 + totalItems × 0.1) × Workshop.GetBaseIncomeMultiplier()` (Gilden-RewardBonus multiplikativ), `Math.Round`.
- **XP:** `baseXp = 25 × mainWorkshop.Level × Max(1, totalItems / 3)`.
- **Difficulty (`GetMaterialOrderDifficulty`):** WS-Level ≤75 Easy, ≤200 Medium, sonst Hard (KEIN Expert).

### 6.5 Material-Offer in Orders (optionales Angebot, V7) (1:1 Original)

Ab Level 30 (`MaterialOfferUnlockLevel`) können normale Orders (kein MaterialOrder) mit Material-Offer spawnen. Spawn-Gate (`TryRollMaterialOffer`): `NextDouble() ≤ MaterialOfferChance (0.35)`.

Pool je OrderType (T1/T2/T3-Stück + Bonus-Multiplikator):

| Order-Type | T1 | T2 | T3 | Cross-Workshop-T2 | Bonus-Reward |
|-----------|----|----|----|-------------------|--------------|
| Quick | 1 | 0 | 0 | nein | +25% |
| Standard | 2 | 0 | 0 | nein | +30% |
| Large | 3 | 1 | 0 | nein | +40% |
| Cooperation | 0 | 2 | 0 | ja | +50% |
| Weekly | 0 | 2 | 1 | nein | +60% |

`SampleMaterialOffer` zieht die Tier-N-Produkte des Auftrags-Workshops; existiert ein gefordertes Tier nicht → kein Offer (null). Cooperation Cross-T2: 1× primärer T2 + 1× T2 eines zufälligen anderen Workshops. Gesetzt werden `MaterialOffer` + `MaterialOfferBonusMultiplier` nur wenn das Sample nicht leer ist.

**Lifecycle (verbindlich):**
1. Generator setzt `MaterialOffer` + `MaterialOfferBonusMultiplier`.
2. UI: Button „Mit Material" nur bei `HasMaterialOffer`.
3. `TryAcceptMaterialOffer(order)` reserviert Material atomar in `ReservedInventory`, setzt `MaterialOfferAccepted = true`. Zu wenig Material → Alert, Auftrag startet nicht.
4. Bei Complete mit `MaterialOfferAccepted`: Bonus `× (1 + Multiplier)` auf Money/XP, Material via `ConsumeReserved` verbraucht. Bei Hard-Fail (Risk-Miss): Bonus 0, Material trotzdem verbraucht.
5. Cancel: `ReleaseReserved` (kein Verbrauch). Order-Expiry: `ReleaseReserved`.
6. SaveGame-Sanitize entfernt Orphan-Reservierungen.

### 6.6 Reputation-Einfluss auf Aufträge

Die Reputation wirkt NICHT als direkter Faktor in der FinalReward-Formel (§6.1a). Stattdessen beeinflusst sie Auftrags-Verfügbarkeit und -Qualität:
- **Auftrags-Qualität:** `OrderQualityBonus` senkt den `DetermineOrderType`-Roll (§6.1b) → bessere Order-Typen.
- **Expert-Gate:** Expert-Aufträge erst ab `ReputationScore ≥ 80` (§6.1c).
- **Stammkunden-Bonus:** höhere Stammkunden-Chance je Tier (§6.1f).
- **Live-/VIP-Spawn:** Tier-abhängige Premium-Spawn-Chance (§6.3).
- **ReputationBonus** wird extern in der Completion-Logik auf die Auszahlung angewendet (nicht Teil von FinalReward).

Reputation-Tiers, -Score und Tier-Boni → §13 (Reputation & Tier-System).

### 6.7 Schnellaufträge (QuickJob) (1:1 Original)

Eigenes leichtgewichtiges System (`QuickJobService`) — 5 gleichzeitige Jobs, rotierend, mit Tageslimit. Quick-Order (OrderType 0) und QuickJob sind zwei getrennte Pfade.

**Rotation + Tageslimit** (prestige-skaliert nach `Prestige.TotalPrestigeCount`):

| Prestige | Rotations-Intervall | Tageslimit (baseLimit) |
|----------|---------------------|------------------------|
| 0 | 15 Min | 20 |
| 1 | 12 Min | 25 |
| 2 | 10 Min | 30 |
| ≥3 | 8 Min | 40 |

Plus Prestige-Shop `ExtraQuickJobLimit` (z.B. +10). 5 Jobs gleichzeitig (`RotateIfNeeded` füllt auf, entfernt Erledigte). Tages-Reset bei neuem UTC-Tag mit Zeitmanipulations-Schutz (`LastReset` in Zukunft → kein Reset).

**Reward-Formel** (`CalculateQuickJobRewards`):
```
fiveMinIncome = Max(0, NetIncomePerSecond) × 300
baseReward    = Max(20 + level × 50, fiveMinIncome)
typeMult      = TitleRewardMultipliers[titleKey]   (Default 1.0)
diffMult      = difficulty.GetRewardMultiplier()
prestigeMult  = Min(1.0 + prestigeCount × 0.10, 3.0)
reward        = Round( baseReward × typeMult × diffMult × prestigeMult, 0 )

xpReward      = (int)( (5 + level × 3) × difficulty.GetXpMultiplier() )
   wenn typeMult > 1.0:  xpReward = (int)(xpReward × typeMult)
```
Belohnungen werden bei jedem `GetAvailableJobs()` für nicht-abgeschlossene Jobs neu berechnet.

**Titel-Multiplikatoren** (8 TitleKeys): QuickRepair 0.90 · QuickFix 0.85 · ExpressService 1.40 · SmallOrder 0.80 · QuickMeasure 0.75 · QuickInstall 1.10 · QuickPaint 0.95 · QuickCheck 1.30.

**QuickJob-Difficulty** (`roll = Next(100)`, KEIN Expert): WS-Level ≤50 immer Easy · ≤200: <50 Easy, sonst Medium · ≤500: <20 Easy, <75 Medium, sonst Hard · >500: <5 Easy, <50 Medium, sonst Hard.

**MiniGame-Zuordnung pro Workshop:** Carpenter→Sawing · Plumber→PipePuzzle · Electrician→WiringGame · Painter→PaintingGame · Roofer→RoofTiling · Contractor→{Blueprint, Sawing} · Architect→{DesignPuzzle, Blueprint} · GeneralContractor→{Inspection, Sawing, PipePuzzle, RoofTiling, DesignPuzzle} · MasterSmith→ForgeGame · InnovationLab→InventGame (Auswahl per Random, Fallback Sawing).

### 6.8 Lieferant-Material-Variante (SupplierDelivery)

Lieferanten erscheinen alle 2–5 Min (Abholzeit 2 Min, `ExpiresAt = UtcNow + 2 Min`), 6 DeliveryTypes. Ab Spielerlevel 50 (`AutoProductionUnlockLevel`) kann mit 25% Chance ein **Material**-Drop erscheinen (zufälliges Tier-1-Produkt eines freigeschalteten Workshops, Fallback `planks`, Menge `Next(1,11)` = 1–10). Research `logi_08` erhöht die Material-Menge.

Ohne Material gewichtete Geld-Auswahl (`roll = Next(100)`): Money 35% (`Max(50, Round(NetIncomePerSecond × Next(60,180)))`), GoldenScrews 20% (`Next(2,6)` = 2–5), Experience 20% (`20 + PlayerLevel×2 + Next(0,40)`), MoodBoost 15% (Mood +10 alle Worker), SpeedBoost 10% (30 Min Boost).

### 6.9 3D-Visualisierung

- **Live-Orders als animierte 3D-Kunden-NPCs** die durch die Stadt zur Werkstatt laufen
- VIP-Kunden mit goldenem Glow + Krone-Icon
- Strategy-Wahl via 3D-Risiko-Meter (Tachometer-UI)
- Reputation-Tier-Up: 3D-Trophäen-Cinematic + Stadt-Atmosphäre wird „schicker"

---

## 7. Mini-Games (13 Typen, 10 Renderer)

Quelle: `Models/Enums/MiniGameType.cs`, `MiniGameTypeExtensions`, `Services/MiniGameNavigator.cs`,
`Services/MiniGameMasteryService.cs`, `Models/Enums/MiniGameMasteryTier.cs`, `Models/Enums/MiniGameResult.cs`.

**13 MiniGame-Enum-Typen, aber nur 10 distinkte Routen/Renderer:** Planing(1), TileLaying(5) und
Measuring(6) teilen sich die Sawing-Route/-Mechanik (`minigame/sawing`). Das Achievement
`all_minigames_perfect` zählt 8 "perfekt-zählbare" Typen (die Sawing-Familie zählt dort als 1).
Maßgeblich: **13 Enum-Typen, 10 Renderer, 8 perfekt-zählbar.**

### 7.1 Vollständige Tabelle (alle 13 Enum-Typen)

| Typ | Index | LocKey | Route (`GetRoute`) | Werkstatt-Typen | Mechanik |
|-----|-------|--------|--------------------|-----------------|----------|
| **Sawing** | 0 | Sawing | `minigame/sawing` | Carpenter | Timing: Marker in grüner Zone stoppen |
| **Planing** | 1 | Planing | `minigame/sawing` (geteilt) | Carpenter | Timing: gleichmäßige Hobelbewegung |
| **PipePuzzle** | 2 | PipePuzzle | `minigame/pipes` | Plumber | Puzzle: Rohre verbinden |
| **WiringGame** | 3 | WiringGame | `minigame/wiring` | Electrician | Drag&Drop: Kabelfarben zuordnen |
| **PaintingGame** | 4 | PaintingGame | `minigame/painting` | Painter | Swipe: malen ohne über Kanten |
| **TileLaying** | 5 | TileLaying | `minigame/sawing` (geteilt) | Roofer | Timing: Fliesen platzieren |
| **Measuring** | 6 | Measuring | `minigame/sawing` (geteilt) | Contractor, Carpenter | Timing: exakt messen/schneiden |
| **RoofTiling** | 7 | RoofTiling | `minigame/rooftiling` | Roofer | Pattern: Dachziegel im Muster |
| **Blueprint** | 8 | Blueprint | `minigame/blueprint` | Contractor | Memory: Bauschritte in Reihenfolge |
| **DesignPuzzle** | 9 | DesignPuzzle | `minigame/designpuzzle` | Architect | Puzzle: Räume im Grundriss |
| **Inspection** | 10 | Inspection | `minigame/inspection` | GeneralContractor | Suchbild: Fehler finden |
| **ForgeGame** | 11 | ForgeGame | `minigame/forge` | MasterSmith | Timing: Metall bei richtiger Temperatur hämmern |
| **InventGame** | 12 | InventGame | `minigame/invent` | InnovationLab | Puzzle: Bauteile zusammensetzen |

### 7.2 Rating-System (`MiniGameRating`)

Quelle: `Models/Enums/MiniGameResult.cs` (`GetRewardPercentage` / `GetXpPercentage`). Reward UND XP
nutzen dieselbe Skala mit **Perfect = 150%** (Bonus). Spread Miss→Perfect = 7.5x. Konsistent mit §6.1a.

| Rating | Reward/XP-Anteil |
|--------|------------------|
| **Perfect** | 150% |
| **Good** | 100% |
| **Ok** | 50% |
| **Miss** | 20% |

> Nicht verwechseln: Die separate Skala Perfect=100% / Good=75% / Ok=50% / Miss=0% ist das
> **DailyChallenge-Score-Mapping** (`DailyChallengeService.cs:572-578`), NICHT die Reward-/XP-Auszahlung.

(Bei Risk-Strategie wirken Miss-Ergebnisse zusätzlich auf die Reputation — siehe Order-/Reputation-Abschnitt.)

### 7.3 Auto-Complete-Ticket

- **Timing-Spiele** (Sawing-Familie, RoofTiling, ForgeGame): Auto-Complete ab **30 Perfects** (Premium: 15).
- **Puzzle-/Memory-Spiele** (PipePuzzle, WiringGame, PaintingGame, Blueprint, DesignPuzzle, Inspection,
  InventGame): Auto-Complete ab **20 Perfects** (Premium: 10).
- Mit Ticket: Spiel überspringen, Ergebnis = Good (75%). `RecordMiniGameResult(rating)` aktualisiert die
  Statistiken IMMER (auch bei QuickJobs); das Task-Ergebnis wird nur bei aktivem Auftrag verbucht.

### 7.4 Mini-Game-Mastery (Bronze / Silver / Gold)

Quelle: `Services/MiniGameMasteryService.cs`, `MiniGameMasteryThresholds`. **Permanent** — kein Reset bei
Prestige/Ascension. Basiert auf `GameState.LifetimePerfectRatingCounts` PRO MiniGame-Typ.

| Tier | Enum | Lifetime-Perfect-Schwelle | GS-Belohnung |
|------|------|---------------------------|--------------|
| None | 0 | < 50 | 0 |
| Bronze | 1 | ≥ 50 (BronzeThreshold) | 5 |
| Silver | 2 | ≥ 200 (SilverThreshold) | 15 |
| Gold | 3 | ≥ 1000 (GoldThreshold) | 50 |

`GoldenScrewRewards[] = [0, 5, 15, 50]` (Index = Tier-Int). `GetTierForCount(count)`: ≥1000→Gold,
≥200→Silver, ≥50→Bronze, sonst None.

**Service-Mechanik:** Singleton, IDisposable, **eager** im Composition Root aufgelöst (subscribed im
Ctor auf `PerfectRatingIncremented`). `GetNextTierThreshold(type)`: None→50, Bronze→200, Silver→1000,
Gold→null. `OnPerfectRatingRecorded(type)`: Lifetime-Counter wird zuvor in
`GameStateService.RecordPerfectRating` atomar inkrementiert; ermittelt currentTier; vergleicht mit
`ClaimedMiniGameMasteryTiers[key]`. Bei neuem Tier > geclaimtem werden **alle übersprungenen Tiers in
Reihenfolge ausgeschüttet** (z.B. Sprung 0→Silver gibt Bronze + Silver), je Tier `AddGoldenScrews(reward)`,
ClaimedTier aktualisiert, Event `MasteryTierUnlocked` (MiniGameType, Tier, GoldenScrewReward) gefeuert.
Persistenz: `ClaimedMiniGameMasteryTiers` (Dictionary<int,int>, defensive Null-Init).

### 7.5 MiniGameNavigator (Route-Map + Abbruch)

Quelle: `Services/MiniGameNavigator.cs`. Statische Route-Map mit **10 Einträgen** (eine pro Renderer):

| Route-String | Aktive Page |
|--------------|-------------|
| minigame/sawing | SawingGame |
| minigame/pipes | PipePuzzle |
| minigame/wiring | WiringGame |
| minigame/painting | PaintingGame |
| minigame/rooftiling | RoofTilingGame |
| minigame/blueprint | BlueprintGame |
| minigame/designpuzzle | DesignPuzzleGame |
| minigame/inspection | InspectionGame |
| minigame/forge | ForgeGame |
| minigame/invent | InventGame |

- **NavigateToMiniGame(routePart, orderId)**: setzt aktive Page + reicht orderId ans aktive MiniGame-VM.
- **IsAnyMiniGamePlaying()**: `vm.IsPlaying || vm.IsCountdownActive`.
- **ConfirmMiniGameAbortAsync()**: Confirm-Dialog (Keys `MiniGameAbortTitle` / `MiniGameAbortMessage` /
  `MiniGameAbortConfirm` / `Cancel`; Fallbacks "Abort mini-game?" / "Your progress will be lost. Do you
  really want to abort?" / "Abort" / "Back"). Bei Bestätigung: `StopCurrent()` + Rückkehr zum Dashboard.

### 7.6 Master-Tool-Kopplung

Die mit MiniGames gekoppelten Master-Tools (Saw/Tile-Slot Hammer, PipeWrench, Screwdriver, Paintbrush,
SpiritLevel, Compass, Magnifier) liefern Zone-/Zeit-Boni — vollständige Master-Tools-Tabelle siehe den
Crafting-/Master-Tools-Abschnitt.

### 7.7 Mini-Game-3D-Konzepte (gemäß ASSETS_AI.md)

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

## 8. Forschung (72 Nodes, 4 Branches)

Quelle: `Models/ResearchTree.cs`, `Models/Research.cs`, `Models/ResearchEffect.cs`,
`Models/Enums/ResearchBranch.cs`, `Services/ResearchService.cs`.

> **KRITISCH — echte Node-Anzahl: 72.** Der Code von `ResearchTree.CreateAll()` erzeugt **72 Nodes**
> (Tools 20 + Management 20 + Marketing 20 + Logistics 12). Im Original gibt es veraltete Doku-Werte
> ("45" im ResearchService-Cache und im `research_all`-Achievement, "~57" im ResearchTree-Klassen-Kommentar) —
> diese sind falsch. Maßgeblich ist die Node-Liste unten (72). Branch-Anzahl = **4** (im Original steht
> `BranchCount = 3` mit einem Array-Bug für Logistics; für die Neuentwicklung **BranchCount = 4** setzen).

### 8.1 Branch-Übersicht

Quelle: `Models/Enums/ResearchBranch.cs`. Branch-Loc-Keys: `Branch{branch}` / `Branch{branch}Desc`.

| Branch | Enum | Farbe | Nodes | Fokus |
|--------|------|-------|-------|-------|
| **Tools** | 0 | #FF9800 Orange | 20 | Effizienz, MiniGame-Zone, Bau-Kosten, Auto-Material, Ascension |
| **Management** | 1 | #2196F3 Blau | 20 | Lohn-Reduktion, Worker-Slots, Worker-Tiers-Unlock, Training, Auto-Assign |
| **Marketing** | 2 | #4CAF50 Grün | 20 | Reward-Multiplikator, Order-Slots, Reputation, Premium-Order-Chance |
| **Logistics** | 3 | #D97706 Amber | 12 | Lager-Slots, Stack-Limit, Markt, Auto-Sell, Crafting-Speed, Tier-4, Erbstücke |

### 8.2 ResearchEffect-Felder (alle Effekt-Typen)

Quelle: `Models/ResearchEffect.cs`. Kombination via `ResearchEffect.Combine(a, b)`:
- **Additiv** (decimal): EfficiencyBonus, CostReduction, MiniGameZoneBonus, WageReduction, ExtraWorkerSlots,
  ExtraOrderSlots, TrainingSpeedMultiplier, RewardMultiplier, LevelResistanceBonus, AscensionPointBonus,
  WorkshopSynergyBonus, ReputationBonus, PremiumOrderChance, BonusWarehouseSlots, CraftingSpeedBonus,
  SupplierMaterialBonus.
- **bool OR** (Unlocks): UnlocksAutoMaterial, UnlocksHeadhunter, UnlocksSTierWorkers, UnlocksAutoAssign,
  UnlocksAutoTraining, UnlocksMassHiring, UnlocksMarket, UnlocksAutoSellRules, UnlocksTier4,
  UnlocksHeirloomSurvival.
- **StackLimitMultiplier**: `Max` (nicht additiv) → `Math.Max(a, b)`.

### 8.3 Tools-Branch (20 Nodes)

Quelle: `ResearchTree.CreateToolsBranch()`.

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| tools_01 | 1 | ResearchBetterSaws | 500 | 10 min | EfficiencyBonus +0.05 | — |
| tools_02 | 2 | ResearchPrecisionTools | 2.000 | 30 min | MiniGameZoneBonus +0.02 | tools_01 |
| tools_03 | 3 | ResearchPowerTools | 8.000 | 1 h | EfficiencyBonus +0.05 | tools_01 |
| tools_04 | 4 | ResearchAutoMaterial | 25.000 | 2 h | UnlocksAutoMaterial | tools_02, tools_03 |
| tools_05 | 5 | ResearchAdvancedMachinery | 80.000 | 4 h | EfficiencyBonus +0.08 | tools_04 |
| tools_06 | 6 | ResearchQualityControl | 200.000 | 6 h | MiniGameZoneBonus +0.03 | tools_04 |
| tools_07 | 7 | ResearchCncMachines | 500.000 | 8 h | EfficiencyBonus +0.10 | tools_05, tools_06 |
| tools_08 | 8 | ResearchLaserCutting | 1.000.000 | 12 h | MiniGameZoneBonus +0.03 | tools_07 |
| tools_09 | 9 | ResearchRobotics | 3.000.000 | 16 h | EfficiencyBonus +0.10 | tools_07 |
| tools_10 | 10 | Research3dPrinting | 8.000.000 | 24 h | CostReduction +0.10 | tools_08, tools_09 |
| tools_11 | 11 | ResearchSmartFactory | 20.000.000 | 32 h | EfficiencyBonus +0.12 | tools_10 |
| tools_12 | 12 | ResearchNanotech | 50.000.000 | 40 h | MiniGameZoneBonus +0.04 | tools_10 |
| tools_13 | 13 | ResearchQuantumMeasure | 100.000.000 | 48 h | EfficiencyBonus +0.15 | tools_11, tools_12 |
| tools_14 | 14 | ResearchAiAssisted | 300.000.000 | 60 h | CostReduction +0.15 | tools_13 |
| tools_15 | 15 | ResearchMasterCraftsman | 1.000.000.000 | 72 h | EfficiencyBonus +0.20, MiniGameZoneBonus +0.05 | tools_13 |
| tools_16 | 16 | ResearchDimensionalForge | 5.000.000.000 | 96 h | EfficiencyBonus +0.20, CostReduction +0.10 | tools_14, tools_15 |
| tools_17 | 17 | ResearchAscensionCatalyst | 10.000.000.000 | 108 h | AscensionPointBonus +0.15 | tools_16 |
| tools_18 | 18 | ResearchMolecularAssembly | 20.000.000.000 | 120 h | EfficiencyBonus +0.25, MiniGameZoneBonus +0.06 | tools_16 |
| tools_19 | 19 | ResearchWorkshopSynergy | 50.000.000.000 | 144 h | WorkshopSynergyBonus +0.02 | tools_17, tools_18 |
| tools_20 | 20 | ResearchEternalForge (Capstone) | 100.000.000.000 | 168 h | EfficiencyBonus +0.30, AscensionPointBonus +0.25, MiniGameZoneBonus +0.08, WorkshopSynergyBonus +0.03 | tools_19 |

### 8.4 Management-Branch (20 Nodes)

Quelle: `ResearchTree.CreateManagementBranch()`.

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| mgmt_01 | 1 | ResearchHrBasics | 500 | 10 min | WageReduction +0.05 | — |
| mgmt_02 | 2 | ResearchTeamBuilding | 2.000 | 30 min | ExtraWorkerSlots +1 | mgmt_01 |
| mgmt_03 | 3 | ResearchMotivation | 8.000 | 1 h | WageReduction +0.05 | mgmt_01 |
| mgmt_04 | 4 | ResearchHeadhunter | 25.000 | 2 h | UnlocksHeadhunter (Pool 5→8) | mgmt_02, mgmt_03 |
| mgmt_05 | 5 | ResearchTrainingProgram | 80.000 | 4 h | TrainingSpeedMultiplier +0.5, LevelResistanceBonus +0.05 | mgmt_04 |
| mgmt_06 | 6 | ResearchWorkLifeBalance | 200.000 | 6 h | WageReduction +0.08 | mgmt_04 |
| mgmt_07 | 7 | ResearchAutoAssign | 500.000 | 8 h | UnlocksAutoAssign | mgmt_05, mgmt_06 |
| mgmt_08 | 8 | ResearchTalentScout | 1.000.000 | 12 h | ExtraWorkerSlots +1 | mgmt_07 |
| mgmt_09 | 9 | ResearchLeadership | 3.000.000 | 16 h | WageReduction +0.10, LevelResistanceBonus +0.08 | mgmt_07 |
| mgmt_10 | 10 | ResearchEliteRecruitment | 8.000.000 | 24 h | **UnlocksSTierWorkers** (schaltet ALLE Tiers ab S frei: S/SS/SSS/Legendary) | mgmt_08, mgmt_09 |
| mgmt_11 | 11 | ResearchMentorship | 20.000.000 | 32 h | TrainingSpeedMultiplier +0.5, LevelResistanceBonus +0.07 | mgmt_10 |
| mgmt_12 | 12 | ResearchCorporateCulture | 50.000.000 | 40 h | WageReduction +0.10 | mgmt_10 |
| mgmt_13 | 13 | ResearchGlobalTalent | 100.000.000 | 48 h | ExtraWorkerSlots +2 | mgmt_11, mgmt_12 |
| mgmt_14 | 14 | ResearchAiManagement | 300.000.000 | 60 h | WageReduction +0.12, LevelResistanceBonus +0.10 | mgmt_13 |
| mgmt_15 | 15 | ResearchMasterManager | 1.000.000.000 | 72 h | ExtraWorkerSlots +2, WageReduction +0.15, LevelResistanceBonus +0.10 | mgmt_13 |
| mgmt_16 | 16 | ResearchTalentAcademy | 5.000.000.000 | 96 h | WageReduction +0.15, ExtraWorkerSlots +2 | mgmt_14, mgmt_15 |
| mgmt_17 | 17 | ResearchAutoTraining | 10.000.000.000 | 108 h | UnlocksAutoTraining, TrainingSpeedMultiplier +0.5 | mgmt_16 |
| mgmt_18 | 18 | ResearchMasterMentor | 20.000.000.000 | 120 h | LevelResistanceBonus +0.15, TrainingSpeedMultiplier +1.0 | mgmt_16 |
| mgmt_19 | 19 | ResearchMassHiring | 50.000.000.000 | 144 h | UnlocksMassHiring, ExtraWorkerSlots +3 | mgmt_17, mgmt_18 |
| mgmt_20 | 20 | ResearchLegendaryLeader (Capstone) | 100.000.000.000 | 168 h | ExtraWorkerSlots +3, WageReduction +0.20, TrainingSpeedMultiplier +1.0, LevelResistanceBonus +0.15 | mgmt_19 |

> **Worker-Tier-Unlock:** Es gibt **kein** mgmt_20-Tier-Unlock — die einzige `UnlocksSTierWorkers`-Research
> ist **mgmt_10**, und sie schaltet ALLE Tiers ab S frei (S, SS, SSS, Legendary), zusätzlich nur über das
> Spielerlevel gegated (S45/SS100/SSS250/Leg500). mgmt_04 (Headhunter) erweitert nur die Rekrutierungs-Pool-Größe (5→8).

### 8.5 Marketing-Branch (20 Nodes)

Quelle: `ResearchTree.CreateMarketingBranch()`.

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| mkt_01 | 1 | ResearchLocalAds | 500 | 10 min | RewardMultiplier +0.05 | — |
| mkt_02 | 2 | ResearchOnlinePresence | 2.000 | 30 min | ExtraOrderSlots +1 | mkt_01 |
| mkt_03 | 3 | ResearchBranding | 8.000 | 1 h | RewardMultiplier +0.05 | mkt_01 |
| mkt_04 | 4 | ResearchReferralProgram | 25.000 | 2 h | RewardMultiplier +0.08 | mkt_02, mkt_03 |
| mkt_05 | 5 | ResearchPremiumBrand | 80.000 | 4 h | ExtraOrderSlots +1 | mkt_04 |
| mkt_06 | 6 | ResearchSocialMedia | 200.000 | 6 h | RewardMultiplier +0.08 | mkt_04 |
| mkt_07 | 7 | ResearchPublicRelations | 500.000 | 8 h | RewardMultiplier +0.10 | mkt_05, mkt_06 |
| mkt_08 | 8 | ResearchTvCampaign | 1.000.000 | 12 h | ExtraOrderSlots +1 | mkt_07 |
| mkt_09 | 9 | ResearchInternational | 3.000.000 | 16 h | RewardMultiplier +0.10 | mkt_07 |
| mkt_10 | 10 | ResearchLuxuryBrand | 8.000.000 | 24 h | RewardMultiplier +0.12 | mkt_08, mkt_09 |
| mkt_11 | 11 | ResearchFranchise | 20.000.000 | 32 h | ExtraOrderSlots +2 | mkt_10 |
| mkt_12 | 12 | ResearchGlobalBrand | 50.000.000 | 40 h | RewardMultiplier +0.12 | mkt_10 |
| mkt_13 | 13 | ResearchCelebEndorsement | 100.000.000 | 48 h | RewardMultiplier +0.15 | mkt_11, mkt_12 |
| mkt_14 | 14 | ResearchMonopoly | 300.000.000 | 60 h | ExtraOrderSlots +2 | mkt_13 |
| mkt_15 | 15 | ResearchMarketDomination | 1.000.000.000 | 72 h | RewardMultiplier +0.20, ExtraOrderSlots +2 | mkt_13 |
| mkt_16 | 16 | ResearchImperialBrand | 5.000.000.000 | 96 h | RewardMultiplier +0.20, ExtraOrderSlots +2 | mkt_14, mkt_15 |
| mkt_17 | 17 | ResearchReputationEngine | 10.000.000.000 | 108 h | ReputationBonus +0.10, RewardMultiplier +0.10 | mkt_16 |
| mkt_18 | 18 | ResearchPremiumContracts | 20.000.000.000 | 120 h | PremiumOrderChance +0.15, RewardMultiplier +0.15 | mkt_16 |
| mkt_19 | 19 | ResearchWorldRenown | 50.000.000.000 | 144 h | ReputationBonus +0.15, PremiumOrderChance +0.10, ExtraOrderSlots +3 | mkt_17, mkt_18 |
| mkt_20 | 20 | ResearchEternalLegacy (Capstone) | 100.000.000.000 | 168 h | RewardMultiplier +0.30, ExtraOrderSlots +3, ReputationBonus +0.20, PremiumOrderChance +0.15 | mkt_19 |

### 8.6 Logistics-Branch (12 Nodes)

Quelle: `ResearchTree.CreateLogisticsBranch()`. WICHTIG: Die Node-Ids sind NICHT numerisch geordnet — die
Prereq-Kette bestimmt die Reihenfolge (Levels 1–12). Die obersten 3 Nodes (logi_03/logi_12/logi_06)
wurden auf max 24 h gekappt (vorher 32/48/72 h).

| Lv | Id | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| 1 | logi_01 | ResearchLogiSlots1 | 50.000 | 30 min | BonusWarehouseSlots +5 | — |
| 2 | logi_02 | ResearchLogiStack2x | 200.000 | 1 h | StackLimitMultiplier 2.0 | logi_01 |
| 3 | logi_05 | ResearchLogiMarket | 500.000 | 2 h | **UnlocksMarket** | logi_02 |
| 4 | logi_04 | ResearchLogiSlots2 | 1.500.000 | 3 h | BonusWarehouseSlots +10 | logi_05 |
| 5 | logi_08 | ResearchLogiSupplier | 4.000.000 | 6 h | SupplierMaterialBonus +0.50 | logi_04 |
| 6 | logi_07 | ResearchLogiAutoSell | 10.000.000 | 8 h | **UnlocksAutoSellRules** | logi_08 |
| 7 | logi_10 | ResearchLogiCraftSpeed | 25.000.000 | 12 h | CraftingSpeedBonus +0.20 | logi_07 |
| 8 | logi_11 | ResearchLogiStack5x | 60.000.000 | 16 h | StackLimitMultiplier 5.0 | logi_10 |
| 9 | logi_09 | ResearchLogiTier4 | 150.000.000 | 24 h | **UnlocksTier4** | logi_11 |
| 10 | logi_03 | ResearchLogiSlots3 | 400.000.000 | 24 h | BonusWarehouseSlots +25 | logi_09 |
| 11 | logi_12 | ResearchLogiHeirloom | 1.000.000.000 | 24 h | **UnlocksHeirloomSurvival** | logi_03 |
| 12 | logi_06 | ResearchLogiMaster | 5.000.000.000 | 24 h | CraftingSpeedBonus +0.30, BonusWarehouseSlots +25 | logi_12 |

### 8.7 Research-Datenmodell + Effekt-Aggregation

Quelle: `Models/Research.cs`, `Services/ResearchService.cs`. DescriptionKey = `NameKey + "Desc"`.
Persistierte Felder: id, branch, level, nameKey, descriptionKey, cost, durationTicks, isResearched,
isActive, startedAt, completedAt, bonusSeconds, effect, prerequisites. Transient (JsonIgnore): Duration,
EffectiveDuration, RemainingTime, Progress.

- **RemainingTime**: `elapsed = UtcNow - StartedAt + FromSeconds(BonusSeconds)`;
  `duration = EffectiveDuration ?? Duration`; `remaining = duration - elapsed` (≥ 0).
- **Progress** (0–100): IsResearched → 100; sonst `Clamp(elapsed / duration * 100, 0, 100)`.
- **GetTotalEffects()**: summiert alle `IsResearched`-Nodes via `ResearchEffect.Combine` (gecacht, Dirty-Flag).

Cache (`_cachedEffects`, `_activeResearchCache`, `_cachedBranches`, `_researchedIds`) wird invalidiert bei
jeder Statusänderung UND bei `StateLoaded`. Event: `ResearchCompleted` (EventHandler<Research>).

### 8.8 Research-Mechaniken (Service)

Quelle: `Services/ResearchService.cs`.

- **StartResearch(id)** — Check-Reihenfolge: (1) Challenge "OhneForschung" darf nicht blockieren,
  (2) keine aktive Forschung (`ActiveResearchId == null`), (3) Node existiert, nicht IsResearched/IsActive,
  (4) alle Prereqs IsResearched, (5) `CanAfford(Cost)`. Dann Geld abziehen, IsActive=true, StartedAt=UtcNow,
  BonusSeconds=0, EffectiveDuration berechnen, ActiveResearchId=id. **Genau eine** Forschung gleichzeitig.
- **CancelResearch()**: nur wenn aktiv. **50% der Kosten erstattet** (`AddMoney(Cost * 0.5)`).
- **InstantFinishResearch()** (GS-Festpreis nach Level, nur ab Level 8): Kosten = `InstantFinishScrewCost`,
  prüft `CanAffordGoldenScrews`, setzt fertig, feuert `ResearchCompleted`.
- **InstantCompleteResearch()** (zeitbasiert): Restzeit (inkl. Boni); Kosten via `GetInstantCompleteGSCost`.
- **GetInstantCompleteGSCost(remaining)**: `hours = Ceiling(remaining.TotalHours)`, `cost = hours * 5`,
  `Clamp(cost, 5, 50)` → **5 GS/angefangener Reststunde, min 5, max 50.**
- **ReduceResearchTime(percentage)** (z.B. Rewarded-Ad −50%, nur ab 30 min Restzeit): `BonusSeconds +=
  effectiveRemaining.TotalSeconds * Clamp(percentage, 0, 1)` (StartedAt wird NICHT manipuliert).
- **CalculateEffectiveDuration(research)** — Multiplikatoren auf `research.Duration` in Reihenfolge:
  (1) Gilden-Forschungs-Speed `/ (1 + GuildMembership.ResearchSpeedBonus)`,
  (2) Ascension Timeless-Research `*= (1 - ascensionBonus)`,
  (3) Prestige-Shop Forschungs-Turbo `*= (1 - shopResearchBonus)`,
  (4) **Minimaldauer-Cap** `Max(seconds, 60)` → mindestens 60 Sekunden.
  Es gibt **keinen** "+50%-Cap" auf die Research-Geschwindigkeit (nur den 60-Sekunden-Minimal-Cap).

### 8.9 InstantFinish-GS-Kosten pro Level (ab Level 8)

Quelle: `Research.InstantFinishScrewCost`. `CanInstantFinish` = IsActive && Kosten > 0 (nur ab Level 8).

| Level | <8 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 |
|-------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| GS | 0 (n/a) | 15 | 25 | 40 | 60 | 90 | 120 | 150 | 180 | 220 | 260 | 320 | 400 | 500 |

### 8.10 3D-Visualisierung

- Forschungsbaum als **3D-Skill-Tree** in eigener Sub-Szene
- Aktive Forschung: Particle-Strom zwischen Nodes
- Abgeschlossen: Goldene Aura
- Cinemachine-Kamera zoomt zwischen Branches

---

## 9. Prestige (7 Tiers + Ascension)

### 9.1 Prestige-Tier-Tabelle (vollständig)

Quelle: `PrestigeTier.cs` (`GetRequiredLevel`, `GetRequiredPreviousTierCount`, `GetPointMultiplier`,
`GetPermanentMultiplierBonus`, `GetTierStartMoney`), `PrestigeData.cs`.

| Tier | Index | Player-Level | Voraussetzung | PP-Multiplikator (Cap 64×) | Permanent-Mult-Bonus | Tier-Startgeld | Preservation (kumulativ) |
|------|-------|--------------|---------------|----------------------------|----------------------|----------------|--------------------------|
| **None** | 0 | int.MaxValue | nicht prestigebar | 0× | — | 100 | — |
| **Bronze** | 1 | 30 | — (immer ab Lv 30) | 1.0× | +20% | 10.000 | (Basis: Prestige-Daten, Achievements, Premium, Settings, Tutorial — siehe §9.9) |
| **Silver** | 2 | 100 | 1× Bronze | 2.0× | +35% | 100.000 | + dito |
| **Gold** | 3 | 250 | 1× Silver | 4.0× | +50% | 1.000.000 | + Research |
| **Platin** | 4 | 500 | 2× Gold | 8.0× | +100% | 25.000.000 | + Prestige-Shop-Items |
| **Diamant** | 5 | 750 | 2× Platin | 16.0× | +200% | 250.000.000 | + Master-Tools |
| **Meister** | 6 | 1000 | 2× Diamant | 32.0× | +400% | 2.500.000.000 | + Gebäude (Level→1), Equipment |
| **Legende** | 7 | 1200 | 3× Meister | 64.0× | +800% | 25.000.000.000 | + Manager (Level→1), Top-3 Worker/WS, Best-Worker-Restore |

**`CanPrestige(tier, playerLevel)`:** erst `playerLevel >= GetRequiredLevel(tier)`, dann Vortier-Count
(BronzeCount ≥ 1 für Silver, SilverCount ≥ 1 für Gold, GoldCount ≥ 2 für Platin, PlatinCount ≥ 2 für
Diamant, DiamantCount ≥ 2 für Meister, MeisterCount ≥ 3 für Legende). None ist nie prestigebar.

> **KRITISCH — PP-Multiplikator (64×) ≠ Income-Multiplikator-Cap (20×):** Die `PP-Multiplikator`-Spalte
> (Bronze 1.0× … Legende 64.0×, `GetPointMultiplier`) skaliert NUR die berechneten **Prestige-Punkte**
> bei der Auszahlung. Der **permanente Einkommens-Multiplikator** (`PermanentMultiplier`, akkumuliert über
> die `Permanent-Mult-Bonus`-Spalte mit Diminishing Returns, §9.3) ist davon getrennt und hart bei
> **20×** gedeckelt (`MaxPermanentMultiplier = 20.0`, privates const in `PrestigeService.cs`;
> `GameState.RecalculateIncomeCache` clampt `min(PermanentMultiplier, 20.0)`). Die 64× sind ausschließlich
> ein PP-Faktor, niemals ein Income-Faktor.

**Bonus-PP-Faktor `sqrt(TierIndex+1)`:** Die flachen Bonus-PP (§9.5) werden NACH dem Tier-Multiplikator
mit `tierFactor = sqrt((int)tier + 1)` skaliert: None ×1.0, Bronze ×1.41, Silver ×1.73, Gold ×2.0,
Platin ×2.24, Diamant ×2.45, Meister ×2.65, Legende ×2.83.

**Permanenter Einkommens-Multiplikator (`GetPermanentMultiplierBonus`):** Basis-Bonus pro Prestige laut
Spalte oben (Bronze +0.20 … Legende +8.00), Akkumulation mit Diminishing Returns (§9.3), Start 1.0,
gedeckelt bei 20.0×, auf 3 Nachkommastellen gerundet.

**Tier-Darstellung (`PrestigeTier.cs`):** Bronze #CD7F32/MedalOutline, Silver #C0C0C0/Medal,
Gold #FFD700/TrophyAward, Platin #E5E4E2/DiamondStone, Diamant #B9F2FF/StarFourPoints,
Meister #FF4500/Fire, Legende #FF69B4/Crown, None #9E9E9E. Loc-Key = `Prestige{tier}`.

### 9.2 PP-Berechnung (`CalculateTotalPrestigePoints` — Single Source of Truth)

Quelle: `PrestigeData.cs` (`CalculatePrestigePoints`), `PrestigeService.cs`. Diese Methode liefert
sowohl die Auszahlung als auch die Dialog-Vorschau. Exakte Reihenfolge:

```
1. basePoints = floor(sqrt(CurrentRunMoney / 100_000))   # CurrentRunMoney, NICHT TotalMoneyEarned; <=0 → 0 PP
2. tierPoints = round(basePoints × GetPointMultiplier(tier))   # Tier-Mult 1.0 .. 64.0
3. Bronze-Floor: wenn Bronze UND tierPoints < 15 → tierPoints = 15   # garantiert 3-4 Shop-Items beim 1. Prestige
4. Challenge-Mult (nur wenn ActiveChallenges > 0): tierPoints = round(tierPoints × (1.0 + Σ GetPpBonus))
5. Prestige-Pass (wenn IsPrestigePassActive): tierPoints = round(tierPoints × 1.5)   # +50 %
6. Gilden-Forschungs-PP-Bonus (wenn ResearchPrestigePointBonus > 0): tierPoints = round(tierPoints × (1 + Bonus))
7. Bonus-PP (flat, NACH allen Multiplikatoren addiert): tierPoints += CalculateBonusPrestigePoints(tier)   # siehe §9.5
8. return tierPoints
```

Wichtig: Schritte 4–6 wirken NUR auf den Tier-Multiplikator-Anteil; die flachen Bonus-PP (Schritt 7)
werden erst danach addiert. `GuildMembership.ResearchPrestigePointBonus` konkreter Prozentsatz: liegt in
`GuildResearch.cs` (nicht in dieser Extraktion belegbar).

**Beim `ApplyPrestige`:** `PrestigePoints += tierPoints`; `TotalPrestigePoints += tierPoints`;
Tier-Zähler inkrementieren; `CurrentTier = max(CurrentTier, tier)`; History (max 20 Einträge, neuester
an Index 0); Legacy `PrestigeLevel = TotalPrestigeCount`, `PrestigeMultiplier = PermanentMultiplier`.
Nach Bronze: `SpeedBoostEndTime = UtcNow + 15 min` (15 min 3× Speed-Boost, nur Bronze).
`PrestigesSinceLastWeeklyReward++`. `RunStartTime = UtcNow` (neuer Speedrun-Run). Doppel-Tap-Guard via
`Interlocked.CompareExchange` auf `_prestigeInProgress`.

### 9.3 Permanenter Multiplikator + Diminishing Returns + Cap 20×

Quelle: `PrestigeService.cs` (`ApplyPrestige`), `PrestigeTier.cs` (`GetPermanentMultiplierBonus`).

```
baseBonus       = GetPermanentMultiplierBonus(tier)                 # +0.20 .. +8.00 (siehe §9.1)
tierCount       = max(0, (Tier-Count NACH Inkrement) − 1)
diminishedBonus = baseBonus × (1 / (1 + 0.2 × tierCount))           # DiminishingReturnsPerTierPrestige = 0.2
PermanentMultiplier += diminishedBonus
PermanentMultiplier  = min(round(PermanentMultiplier, 3), 20.0)     # MaxPermanentMultiplier = 20.0× (Hard-Cap)
```

`PermanentMultiplier` startet bei 1.0. Nach 5× Same-Tier-Prestiges nur noch 50% des Basis-Bonus.
`GetPermanentMultiplier()` liefert nur diesen Tier-Multiplikator; Shop-Income-Boni werden separat im
GameLoop/Offline angewendet.

### 9.3a Prestige-Pass (IAP, permanent)

Quelle: `PrestigeService.cs` (`ActivatePrestigePass`). Einmaliger IAP-Kauf, `IsPrestigePassActive`
überlebt jedes Prestige (wird NICHT zurückgesetzt). Effekt: +50 % auf die berechneten Tier-PP (×1.5,
Schritt 5 in §9.2).

### 9.4 Meilensteine (GS-Belohnungen, permanent über Ascension)

Quelle: `GameBalanceConstants.PrestigeMilestones`, `PrestigeService.Challenges.cs`
(`CheckAndAwardMilestones`). Kumulativ nach `TotalPrestigeCount`; ID in `ClaimedMilestones` (HashSet) →
einmalig. GS via `AddGoldenScrews(reward, fromPurchase: false)` (Gameplay-Quelle, durch Premium +100 %
verdoppelbar). `ClaimedMilestones` überlebt Ascension.

| Benötigte Prestiges | ID | GS-Reward |
|---------------------|-----|-----------|
| 1 | pm_first | +10 GS |
| 5 | pm_5 | +20 GS |
| 10 | pm_10 | +35 GS |
| 25 | pm_25 | +50 GS |
| 50 | pm_50 | +75 GS |
| 100 | pm_100 | +100 GS |

**Wiederholbarer Wochen-Meilenstein (ID pm_weekly):** wenn `PrestigesSinceLastWeeklyReward >= 7` →
+5 GS, Counter `-= 7`. Der Counter wird in `ApplyPrestige` je Prestige hochgezählt.

### 9.5 Prestige-Bonus-PP (flat, NACH Tier-Multiplikator addiert)

Quelle: `PrestigeService.Challenges.cs` (`CalculateBonusPrestigePoints`), `GameBalanceConstants.cs`.
`bonusPp` startet 0, Akkumulation in dieser Reihenfolge:

| Bonus | Wert | Cap | Konstante |
|-------|------|-----|-----------|
| Per 10 Perfect Ratings (`Statistics.PerfectRatings / 10`) | +1 PP je Block | max +5 | BonusPpPerPerfectBlock=1, BonusPpPerfectRatingsCap=5 |
| Voll erforschte Research-Branch (alle Nodes der Branch `IsResearched`, mind. 1 Node) | +2 PP je Branch | faktisch max +8 (**4** Branches: Tools/Management/Marketing/Logistics) | BonusPpFullBranch=2 |
| Alle Gebäude auf Level 5 (`Buildings.Count >= 7` UND alle `Level >= 5`) | +1 PP | — | BonusPpAllBuildingsMax=1 |
| Per Level über `tier.GetRequiredLevel()` | floor(extraLevels × 0.05) | max +5 | BonusPpPerExtraLevel=0.05, BonusPpExtraLevelCap=5 |

Danach Tier-Skalierung: `bonusPp = round(bonusPp × sqrt((int)tier + 1))` (Faktoren siehe §9.1).

### 9.6 Prestige-Challenges (max 3 parallel, additiv)

Quelle: `PrestigeChallengeType.cs` (`PrestigeChallengeExtensions`), `PrestigeData.ActiveChallenges`.
`MaxActiveChallenges = 3`. PP-Boni stacken **additiv**: `GetTotalPpMultiplier = 1.0 + Σ GetPpBonus`
(leere Liste → 1.0). Aktive Challenges bleiben nach dem Reset für den neuen Run erhalten (Constraints
werden ab dann enforced).

| Challenge | Enum | Effekt | PP-Bonus (GetPpBonus) |
|-----------|------|--------|------------------------|
| **Spartaner** | 0 | Max 3 Worker pro Workshop | +0.45 (45%) |
| **OhneForschung** | 1 | Keine Forschung möglich | +0.30 (30%) |
| **Inflationszeit** | 2 | Doppelte Upgrade-Kosten | +0.25 (25%) |
| **SoloMeister** | 3 | Nur 1 Workshop erlaubt | +0.50 (50%) |
| **Sprint** | 4 | Kein Offline-Einkommen | +0.35 (35%) |
| **KeinNetz** | 5 | Keine Lieferanten | +0.20 (20%) |

**`AbandonChallengeRun()`** (`PrestigeService.Challenges.cs`): vergibt `max(1, basePoints / 2)` PP
(50 % der Basis-PP, ohne Challenge-Bonus, ohne Tier-Multiplikator), leert `ActiveChallenges`,
invalidiert den Effekt-Cache.

### 9.7 Prestige-Shop (25 Items)

Quelle: `PrestigeShop.cs`, `PrestigeShopItem.cs`, `PrestigeService.cs`. Kosten in PP. Tier-Lock =
`RequiredTier` (None = immer sichtbar). Wiederholbar = `IsRepeatable`. Items gehören als
ScriptableObjects nach `ScriptableObjects/Prestige/`.

| # | Id | PP-Kosten | Kategorie | Effekt | Wiederholbar | Tier-Lock |
|---|-----|-----------|-----------|--------|--------------|-----------|
| 1 | pp_income_10 | 5 | IncomeAndCosts | IncomeMultiplier +0.10 (+10 % Einkommen) | nein | None |
| 2 | pp_income_25 | 15 | IncomeAndCosts | IncomeMultiplier +0.25 (+25 %) | nein | None |
| 3 | pp_income_50 | 40 | IncomeAndCosts | IncomeMultiplier +0.50 (+50 %) | nein | None |
| 4 | pp_income_100 | 80 | IncomeAndCosts | IncomeMultiplier +1.00 (+100 %) | nein | None |
| 5 | pp_cost_15 | 12 | IncomeAndCosts | CostReduction 0.15 (−15 % Kosten) | nein | None |
| 6 | pp_cost_30 | 30 | IncomeAndCosts | CostReduction 0.30 (−30 %) | nein | None |
| 7 | pp_upgrade_discount | 50 | IncomeAndCosts | UpgradeDiscount 0.15 (−15 % Upgrade-Kosten) | nein | None |
| 8 | pp_better_start_worker | 10 | WorkerAndMood | StartingWorkerTier = "D" | nein | None |
| 9 | pp_start_worker_b | 30 | WorkerAndMood | StartingWorkerTier = "B" | nein | None |
| 10 | pp_mood_slow | 10 | WorkerAndMood | MoodDecayReduction 0.25 (−25 % Mood-Decay) | nein | None |
| 11 | pp_mood_immunity | 25 | WorkerAndMood | MoodDecayReduction 0.50 (−50 %) | nein | None |
| 12 | pp_rush_boost | 15 | SpeedAndAutomation | RushMultiplierBonus +0.50 (Rush 3× statt 2×) | nein | None |
| 13 | pp_delivery_speed | 12 | SpeedAndAutomation | DeliverySpeedBonus 0.30 (Lieferant 30 % schneller) | nein | None |
| 14 | pp_crafting_speed | 18 | SpeedAndAutomation | CraftingSpeedBonus 0.25 (+25 % Crafting) | nein | None |
| 15 | pp_offline_hours | 35 | SpeedAndAutomation | OfflineHoursBonus +4 (h max. Offline, additiv) | nein | None |
| 16 | pp_quickjob_limit | 22 | SpeedAndAutomation | ExtraQuickJobLimit +10 (QuickJobs/Tag) | nein | None |
| 17 | pp_start_money | 6 | CurrencyAndStart | ExtraStartMoney +5.000 € | nein | None |
| 18 | pp_start_money_big | 18 | CurrencyAndStart | ExtraStartMoney +50.000 € | nein | None |
| 19 | pp_xp_15 | 8 | CurrencyAndStart | XpMultiplier +0.15 (+15 % XP) | nein | None |
| 20 | pp_xp_30 | 20 | CurrencyAndStart | XpMultiplier +0.30 (+30 %) | nein | None |
| 21 | pp_golden_screw_25 | 25 | CurrencyAndStart | GoldenScrewBonus +0.25 (+25 % GS-Quellen) | nein | None |
| 22 | pp_income_repeatable | 15 (Basis) | IncomeAndCosts | IncomeMultiplier +0.05 pro Kauf | **ja** | None |
| 23 | pp_order_reward_rep | 20 (Basis) | IncomeAndCosts | OrderRewardBonus +0.05 pro Kauf (+5 % Auftragsbel.) | **ja** | None |
| 24 | pp_delivery_interval_rep | 25 (Basis) | SpeedAndAutomation | DeliverySpeedBonus +0.10 pro Kauf | **ja** | None |
| 25 | pp_research_speed_tier | 45 | SpeedAndAutomation | ResearchSpeedBonus 0.25 (−25 % Forschungszeit) | nein | **Diamant** |

**Wiederholbare Items — Kosten & Cap** (`GetRepeatableItemCost`, `GameBalanceConstants.cs`):
`Kosten(purchaseCount) = item.Cost × 2^(min(purchaseCount, 15))` (erster Kauf = Basis, dann verdoppeln,
z. B. pp_income_repeatable 15/30/60/120/240/480/960/1920). Max-Käufe pro wiederholbarem Item:
`MaxRepeatableShopPurchases = 8` (ab 8 Käufen gesperrt). Kauf-Anzahl in `RepeatableItemCounts`.

**Tier-Lock-Sichtbarkeit** (`GetShopItems`): Tier-locked Items (RequiredTier != None) nur sichtbar, wenn
`CurrentTier >= RequiredTier` ODER bereits gekauft. Aktuell nur pp_research_speed_tier (Diamant).

**Effekt-Caps (aggregiert, `RefreshEffectCacheIfNeeded`):** CostReduction Σ max 0.50 (−50 %),
MoodDecayReduction Σ max 0.50, OrderRewardBonus Σ (inkl. repeatable × count) max 1.0 (+100 %),
ResearchSpeedBonus Σ max 0.50, XpMultiplier kein expliziter Cap. IncomeMultiplier-Aggregation separat
in `IncomeCalculatorService.GetPrestigeIncomeBonus`, DeliverySpeedBonus separat im GameLoopService.

**Kauf-Logik (`BuyShopItem`, atomar unter State-Lock):** Einmalig → ablehnen, wenn schon in
`PurchasedShopItems` oder `PrestigePoints < Cost`; sonst PP abziehen, ID hinzufügen. Wiederholbar →
ablehnen, wenn `count >= 8` oder PP < Kosten; sonst PP abziehen (`GetRepeatableItemCost`),
`RepeatableItemCounts[id]++`.

### 9.8 Ascension-System (Meta-Prestige, nach 3× Legende)

**Freischaltung** (`AscensionService.CanAscend`): `Prestige.LegendeCount >= 3`.

**AP-Berechnung** (`CalculateAscensionPoints`):

```
apFromPP       = TotalPrestigePoints / 500              # 1 AP je 500 PP, ganzzahlig
apFromLegende  = LegendeCount / 2                       # 1 AP je 2 Legende-Prestiges
apFromMaxLevel = (Workshops mit Level >= 1000) >= 8 ? 2 : 0
apFromTools    = CollectedMasterTools.Count >= 12 ? 1 : 0
premiumBonus   = IsPremium ? 1 : 0
apFromScaling  = (int)sqrt(Ascension.AscensionLevel) × 2     # Wurzel-Skalierung gegen Whale-Inflation
result = max(5, apFromPP + apFromLegende + apFromMaxLevel + apFromTools + premiumBonus + apFromScaling)
```

Minimum 5 AP (`Math.Max(5, …)` — der einzige „Floor"; keine lineare Skalierung).

**Die 6 Ascension-Perks** (alle MaxLevel 3, Kosten/Werte als Arrays Index 0 = Level 1; gesamt **54 AP**
für alle 6 auf Max). Quelle `AscensionPerk.cs` (`GetAll`), `AscensionService.cs`:

| Perk-Id | Loc-Key | Icon | Kosten/Level (AP) | Werte/Level | Wirkung |
|---------|---------|------|-------------------|-------------|---------|
| asc_start_capital | AscStartCapital | Bank | [1, 3, 5] | [1.00, 5.00, 10.00] | Start-Kapital-Mult = 1 + Wert → ×2.0 / ×6.0 / ×11.0 (+100/500/1000 % Startgeld) |
| asc_eternal_tools | AscEternalTools | Wrench | [2, 4, 5] | [2, 4, 5] | MasterTools bewahren: Lvl1 erste 2, Lvl2 erste 4, Lvl3 alle |
| asc_quick_start | AscQuickStart | RocketLaunch | [1, 3, 5] | [2, 4, 8] | Start mit 2 / 4 / 8 Workshops freigeschaltet |
| asc_timeless_research | AscTimelessResearch | FlaskOutline | [1, 2, 5] | [0.15, 0.30, 0.50] | Research-Dauer −15 / −30 / −50 % |
| asc_golden_era | AscGoldenEra | Screwdriver | [1, 3, 5] | [0.20, 0.50, 1.00] | Goldschrauben-Verdienst +20 / +50 / +100 % |
| asc_legendary_reputation | AscLegendaryReputation | StarCircle | [1, 2, 5] | [65, 80, 100] | Start-Reputation 65 / 80 / 100 (Default 50) |

Kosten-Summe pro Perk auf Max: start_capital 9, eternal_tools 11, quick_start 9, timeless_research 8,
golden_era 9, legendary_reputation 8 → **54 AP gesamt**.

**Perk-Abfragen:** `GetStartCapitalMultiplier()` = `bonus==0 ? 1.0 : 1.0+bonus` (Lvl0 → 1.0);
`GetEternalToolsLevel()` = Perk-Level direkt (0–3); `GetQuickStartWorkshops()` = Lvl0 → 0, sonst 2/4/8;
`GetStartReputation()` = Lvl0 → 50, sonst 65/80/100; `GetGoldenScrewBonus()` = 0/0.20/0.50/1.00;
`GetResearchSpeedBonus()` = 0/0.15/0.30/0.50. `UpgradePerk`: ablehnen bei MaxLevel oder
`AscensionPoints < GetCost(level+1)`; sonst AP abziehen, `Perks[perkId]++`.
`Quick-Start`-Workshop-Reihenfolge: Plumber, Electrician, Painter, Roofer, Contractor, Architect,
GeneralContractor, MasterSmith.

**DoAscension — Reset & Preservierung** (härtester Reset im Spiel, resettet inkl. Prestige-Daten):
- AP gutschreiben: `AscensionLevel++`, `AscensionPoints += AP`, `TotalAscensionPoints += AP`,
  `LastAscensionDate = UtcNow`. Startgeld: `Money = 1000 × GetStartCapitalMultiplier()`.
- Reset: Player (Lv 1, XP 0), Workshops (nur Carpenter Lvl 1 + 1 E-Worker), WorkerMarket, Orders,
  Reputation (`new CustomerReputation()` → Score 50, **kein** Reputation-Perk-Apply hier),
  Research (`CreateAll()`), Events, Boosts, DailyRewards, Lieferant, QuickJobs, DailyChallenges,
  LuckySpin, WeeklyMissions, WelcomeBack, Tournament, Crafting-Inventar (außer Heirlooms),
  DailyShopOffer, Workshop-Spezialisierung; Manager/Equipment/Gebäude komplett gelöscht;
  `PerfectRatingCounts?.Clear()` (anders als Prestige).
- **Prestige-Daten neu** (`state.Prestige = new PrestigeData {…}`): bewahrt NUR `ClaimedMilestones`,
  `BestRunTimes`, `PurchasedShopItems`, `RepeatableItemCounts`. Alles andere (PrestigePoints,
  TotalPrestigePoints, Tier-Counts, PermanentMultiplier, History, ActiveChallenges) wird resettet.
- **EternalTools-Perk:** `keptTools` analog (Lvl3 alle, Lvl1 erste 2, Lvl2 erste 4, Lvl0 keine);
  danach `CollectedMasterTools` geleert und `keptTools` zurückgeschrieben.
- **Bewahrt:** `Ascension` (Perks/PermanentHeirlooms), `WorkshopStars` (Rebirth, permanent),
  Achievements, IsPremium, Tutorial, TotalMoneyEarned, Statistics (TotalPlayTime/BestPerfectStreak),
  Settings, CreatedAt, BattlePass, SeasonalEvent, ClaimedLevelOffers, StarterPack, VIP, Friends,
  GuildMembership, PlayerGuid/PlayerName, Cosmetics.
- Run-Heirlooms gehen beim Ascension in Permanent-Heirlooms über (siehe §24).

### 9.9 Reset-Preservierung pro Prestige-Tier (`ResetProgress`)

Quelle: `PrestigeService.cs` (`ResetProgress`), `PrestigeTier.cs` (`Keeps*`-Methoden). Schwellen
kumulativ — höhere Tiers behalten alles von niedrigeren plus eigenes:

| Behalten | Ab Tier | Methode |
|----------|---------|---------|
| Research | Gold (≥ 3) | KeepsResearch |
| Prestige-Shop-Items | Platin (≥ 4) | KeepsShopItems |
| MasterTools (Meisterwerkzeuge) | Diamant (≥ 5) | KeepsMasterTools |
| Gebäude (Level → 1) | Meister (≥ 6) | KeepsBuildings |
| Equipment-Inventar | Meister (≥ 6) | KeepsEquipment |
| Manager (Level → 1) | Legende (≥ 7) | KeepsManagers |
| Beste Worker (Top 3/Workshop) | Legende (≥ 7) | KeepsBestWorkers |

**Immer erhalten:** Prestige (PrestigeData), UnlockedAchievements, IsPremium, Tutorial.SeenHints,
TotalMoneyEarned, Statistics (TotalPlayTime/BestPerfectStreak), Settings, CreatedAt, BattlePass,
CurrentSeasonalEvent, ClaimedLevelOffers, HasPurchasedStarterPack, VipLevel, TotalPurchaseAmount,
Friends, TotalTournamentsPlayed, StreakRescueUsed, IsPrestigePassActive, GuildMembership.

**Immer zurückgesetzt:** PlayerLevel→1, XP→0, CurrentRunMoney→0, TotalMoneySpent→0, Workshops
(→ nur Carpenter Lvl 1 + 1 Worker), UnlockedWorkshopTypes (→ Carpenter), WorkerMarket, Orders,
Reputation (neu, mit Start-Reputation), Events, LuckySpin, WeeklyMissionState, WelcomeBackOffer,
Tournament, Crafting (außer Erbstücke), DailyChallengeState, QuickJobs, DailyRewards, Boosts,
PendingDelivery, DailyShopOffer, Workshop-Spezialisierung, ReservedInventory, ActiveCraftingJobs,
PerfectRatings/PerfectStreak/MiniGamesPlayed. `PerfectRatingCounts` wird beim Prestige NICHT resettet
(nur bei Ascension).

**Startgeld nach Prestige:** `startMoney = tier.GetTierStartMoney()` (Bronze 10.000 … Legende
25.000.000.000, siehe §9.1) `+ Σ ExtraStartMoney aller Shop-Items` (pp_start_money +5.000,
pp_start_money_big +50.000); falls Ascension-StartCapitalMultiplier > 1.0:
`startMoney = round(startMoney × ascCapitalMultiplier, 0)`.

**Start-Worker-Tier:** Default `WorkerTier.E`, erhöht durch Shop-Items (pp_better_start_worker → D,
pp_start_worker_b → B; höchster gilt). **Legende — beste Worker:** vor Reset Top-3/Workshop (nach
`Efficiency`) gesichert (Mood=80, Fatigue=0); Keys `"{Type}"`, `"{Type}_1"`, `"{Type}_2"`; beim
Workshop-Unlock via `RestoreKeptWorkers` wiederhergestellt (nur wenn `worker.Tier >= startWorkerTier`),
mind. 1 Worker garantiert via `Worker.CreateForTier`.

### 9.10 Speedrun-Belohnungen (Bestzeit pro Tier × Time-Bracket)

Quelle: `SpeedrunRewards.cs` (`CalculateReward`), `PrestigeService.cs` (`ApplyPrestige`),
`PrestigeData.BestRunTimes`.

- Run-Dauer = `UtcNow − RunStartTime` (gemessen vor dem Reset; nur wenn `RunStartTime > MinValue`).
- Bestzeit pro Tier in `BestRunTimes` (Dict, Key = `tier.ToString()`, Value = Ticks). Neue Bestzeit,
  wenn kein Eintrag existiert ODER `runDuration.Ticks < bestTicks`.
- Bei neuer persönlicher Bestzeit: Bestzeit speichern, GS via `CalculateReward(tier, runDuration)`
  gutschreiben (`AddGoldenScrews`), `SpeedrunRecordSet`-Event. `BestRunTimes` überlebt Ascension.

Logik: schnellstes erfülltes Bracket (`runDuration.TotalHours <= limit`) gewinnt. `runDuration <= 0`
→ 0 GS. None/sonst → 0 GS.

| Tier | Bracket 1 (langsamster) | Bracket 2 | Bracket 3 (schnellster) |
|------|--------------------------|-----------|--------------------------|
| Bronze | ≤ 2.0 h → 5 GS | ≤ 1.0 h → 10 GS | ≤ 0.5 h → 15 GS |
| Silver | ≤ 3.0 h → 10 GS | ≤ 2.0 h → 20 GS | ≤ 1.0 h → 35 GS |
| Gold | ≤ 5.0 h → 15 GS | ≤ 3.0 h → 30 GS | ≤ 1.5 h → 60 GS |
| Platin | ≤ 8.0 h → 25 GS | ≤ 5.0 h → 50 GS | ≤ 2.5 h → 100 GS |
| Diamant | ≤ 12.0 h → 40 GS | ≤ 8.0 h → 80 GS | ≤ 4.0 h → 150 GS |
| Meister | ≤ 20.0 h → 60 GS | ≤ 12.0 h → 120 GS | ≤ 6.0 h → 250 GS |
| Legende | ≤ 30.0 h → 100 GS | ≤ 20.0 h → 200 GS | ≤ 10.0 h → 400 GS |

`GetGoldBracketHours` (UI „bestes Bracket"): Bronze 0.5, Silver 1.0, Gold 1.5, Platin 2.5, Diamant 4.0,
Meister 6.0, Legende 10.0.

### 9.11 Prestige-Cinematic (Unity-Neuerung gemäß ASSETS_AI.md)

**Timeline-Sequenz (~12s):**
- Phase 1: Geld zerspringt in Sterne (GPU-Particles), Music duckt
- Phase 2: Badge schwebt nach oben mit Bloom-Effekt
- Phase 3: Multiplikator-Zahl zoomt mit Distortion-Shader
- Phase 4: Belohnungs-Karte fliegt zum Spieler
- Auto-Dismiss nach 12s, Skip-Button verfügbar

**5 Hero-Cinematic-Assets** (siehe ASSETS_AI.md § 12) werden via **Rodin Gen-2.5** Cloud-Polish erstellt.

---

## 10. Crafting (33 Rezepte, 4 Tiers)

> Verbindliche Werte 1:1 aus `CraftingRecipe.cs` (AllRecipes, CraftingProduct.AllProducts) und `CraftingService.cs`. ALLE Rezepte/Produkte/Werte exakt — keine erfundenen Materialien.
>
> **Tier-Freischaltung (Spielerlevel):** T1 = Lv 50, T2 = Lv 150, T3 = Lv 300, T4 = Lv 500 (+ Research `logi_09`).
> **Cross-Workshop-Inputs** (markiert mit Quell-Workshop) werden erst ab Spielerlevel 100 gefordert (darunter zählen nur eigene Inputs — §10.7).
> `OutputCount = 1` für alle Rezepte.

### 10.1 Tier-1-Rezepte (10 Stück, je 1 pro Workshop, ReqWSLevel 50)

Roh-Rezepte ohne Inputs. Bei leeren Inputs fallen Tier-1-Goldkosten = `BaseValue × 0.20` an (§10.4a).

| Id | Workshop | Output | BaseValue | Dauer |
|----|----------|--------|-----------|-------|
| r_planks | Carpenter | planks | 500 | 30s |
| r_pipes | Plumber | pipes | 500 | 30s |
| r_cables | Electrician | cables | 500 | 30s |
| r_paint | Painter | paint_mix | 400 | 30s |
| r_tiles | Roofer | roof_tiles | 600 | 30s |
| r_concrete | Contractor | concrete | 800 | 30s |
| r_blueprint | Architect | blueprint | 1.000 | 30s |
| r_contract | GeneralContractor | contract | 1.500 | 30s |
| r_fittings | MasterSmith | fittings | 1.200 | 30s |
| r_prototype | InnovationLab | prototype | 2.000 | 30s |

### 10.2 Tier-2-Rezepte (10 Stück, ReqWSLevel 150)

Cross-Inputs in Klammern mit Quell-Workshop (erst ab Spielerlevel 100 gefordert).

| Id | Workshop | Output | BaseValue | Inputs | Dauer |
|----|----------|--------|-----------|--------|-------|
| r_furniture | Carpenter | furniture | 2.500 | planks×3, paint_mix×1 (Painter) | 120s |
| r_plumbing | Plumber | plumbing_system | 2.500 | pipes×3, fittings×1 (MasterSmith) | 120s |
| r_circuit | Electrician | circuit | 2.500 | cables×3, prototype×1 (InnovationLab) | 120s |
| r_walldesign | Painter | wall_design | 2.000 | paint_mix×3, blueprint×1 (Architect) | 120s |
| r_roofing | Roofer | roofing_system | 3.000 | roof_tiles×3, concrete×1 (Contractor) | 120s |
| r_foundation | Contractor | concrete_foundation | 4.000 | concrete×3, pipes×1 (Plumber) | 150s |
| r_framework | Architect | framework | 5.000 | blueprint×3, planks×1 (Carpenter), concrete×1 (Contractor) | 150s |
| r_contract_complex | GeneralContractor | contract_complex | 6.000 | contract×3, blueprint×1 (Architect) | 150s |
| r_master_fittings | MasterSmith | master_fittings | 5.000 | fittings×3, cables×1 (Electrician) | 150s |
| r_innovation | InnovationLab | innovation | 7.000 | prototype×3, cables×1 (Electrician) | 150s |

### 10.3 Tier-3-Rezepte (10 Stück, ReqWSLevel 300)

| Id | Workshop | Output | BaseValue | Inputs | Dauer |
|----|----------|--------|-----------|--------|-------|
| r_luxury_furniture | Carpenter | luxury_furniture | 50.000 | furniture×2, fittings×1 (MasterSmith) | 300s |
| r_bathroom | Plumber | bathroom_installation | 50.000 | plumbing_system×2, cables×1 (Electrician) | 300s |
| r_smarthome | Electrician | smart_home | 50.000 | circuit×2, concrete×1 (Contractor) | 300s |
| r_artwork | Painter | artwork | 40.000 | wall_design×2, planks×1 (Carpenter) | 300s |
| r_roof_structure | Roofer | roof_structure | 60.000 | roofing_system×2, blueprint×1 (Architect) | 300s |
| r_skyscraper_frame | Contractor | skyscraper_frame | 60.000 | concrete_foundation×2, contract×1 (GeneralContractor) | 360s |
| r_master_blueprint | Architect | master_blueprint | 70.000 | framework×2, contract×1 (GeneralContractor) | 360s |
| r_general_contract | GeneralContractor | general_contract | 80.000 | contract_complex×2, blueprint×1 (Architect) | 420s |
| r_masterpiece_fittings | MasterSmith | masterpiece_fittings | 60.000 | master_fittings×2, prototype×1 (InnovationLab) | 360s |
| r_patent | InnovationLab | patent | 75.000 | innovation×2, master_fittings×1 (MasterSmith T2) | 420s |

### 10.4 Tier-4-Rezepte (3 Stück, ReqWSLevel 500 + logi_09, NUR GeneralContractor)

Alle drei sind heirloom-fähig (`IsHeirloomEligible = true`).

| Id | Output | BaseValue | Inputs | Dauer |
|----|--------|-----------|--------|-------|
| r_villa | villa | 2.500.000 | luxury_furniture×5, smart_home×3, roof_structure×2, artwork×1 | 1800s (30 min) |
| r_skyscraper | skyscraper | 4.000.000 | skyscraper_frame×5, bathroom_installation×3, smart_home×3, artwork×2 | 2400s (40 min) |
| r_imperium_hq | imperium_hq | 5.000.000 | luxury_furniture×2, bathroom_installation×2, smart_home×2, artwork×2, roof_structure×2, skyscraper_frame×2, master_blueprint×2, general_contract×1, masterpiece_fittings×2, patent×2 | 3600s (60 min) |

### 10.4a StartCrafting — Ablauf, Tier-1-Goldkosten, Stack-Schutz

Reihenfolge unter `_craftingLock` (`CraftingService.StartCrafting`):
1. Rezept laden; `effectiveInputs = GetEffectiveInputs(recipe, playerLevel)` (§10.7).
2. **Tier-1-Goldkosten:** wenn `recipe.Tier == 1 && effectiveInputs.Count == 0`: `materialCost = product.BaseValue × 0.20` (= 20% des BaseValue; z.B. planks 500 → 100 Gold). `CanAfford` + `TrySpendMoney`. → Geld-Senke.
3. **Output-Stack-Limit-Check:** bei Bestand 0 → freier Slot nötig (`usedSlots >= WarehouseSlotCount` → Abbruch; Code prueft gegen die **Basis**-Slots `WarehouseSlotCount`, NICHT `EffectiveSlotCount` mit Research-/Gilden-Bonus); sonst `currentOutput + OutputCount > CurrentStackLimit` → Abbruch.
4. **Input-Verfügbarkeit:** für jeden Input `available = CraftingInventory[id] − ReservedInventory[id]`; bei `< required` → Abbruch. Sonst Inputs abziehen.
5. Crafting-Speed-Bonus berechnen (§10.5), Job mit `StartedAt=UtcNow`, `DurationSeconds=effectiveDuration` in `ActiveCraftingJobs`.

**CollectProduct:** Job muss `IsComplete` sein. Erneuter Stack-Limit-Check wie oben — bei vollem Lager bleibt der Job completed liegen (KEIN Material-Burn). Output zu `CraftingInventory` addieren, Telemetrie `material_crafted`.

### 10.5 Crafting-Speed-Bonus (Dauer-Reduktion)

```
craftingSpeedBonus = PrestigeShopCraftingSpeedBonus
                   + Research.CraftingSpeedBonus
                   + MaterialAffinityBonus
                   + GuildMembership.MegaProjectCraftingSpeedBonus
                   + GuildMembership.HallCraftingSpeedBonus
if craftingSpeedBonus > 0:
   effectiveDuration = Max(1, (int)(DurationSeconds × (1 − Min(craftingSpeedBonus, 0.50))))
```
**Gesamt-Cap der Beschleunigung: 50%.** `MaterialAffinityBonus = 0.20 × matchingWorkingWorkers / totalWorkingWorkers` (linear, alle matchend = +20%, 0 bei `MaterialAffinity.None` oder ohne arbeitenden Worker — siehe §12.5). Material-Affinity ist ein **Crafting-Speed-Bonus**, KEIN Worker-Effizienz-Multiplikator.

### 10.6 Auto-Produktion

Zwei getrennte Mechanismen (1:1 Original, `AutoProductionService`):

**Tier-1-Auto-Produktion** (`ProduceForAllWorkshops`, ab Spielerlevel 50 / `AutoProductionUnlockLevel`) — erzeugt das Tier-1-Produkt des Workshops. Intervall pro Workshop-Typ:

| Workshop-Typ | Intervall |
|--------------|-----------|
| Standard (alle ausser MasterSmith/InnovationLab) | 180s (`AutoProductionIntervalSeconds`) |
| MasterSmith | 60s (`AutoProductionMasterSmithInterval`) |
| InnovationLab | 120s (`AutoProductionInnovationLabInterval`) |

Menge je Zyklus: **1 Item pro arbeitendem Worker** (`workingWorkers`), via
`WarehouseService.AddToInventory(productId, workingWorkers, type)`. Workshops ohne arbeitenden Worker
produzieren nichts. (Die früher hier zitierte Formel `5 + Min(playerLevel/50, 10)` gehört zur
**MaterialOrder-Generierung** in `OrderGeneratorService`, NICHT zur Auto-Produktion — Beleg:
`AutoProductionService.cs:68-78`.)

**Higher-Tier-Auto-Craft** (`AutoCraftHigherTiers`) — wird pauschal alle 360 GameLoop-Ticks (~6 min) aufgerufen, KEINE workshop-/tier-spezifischen Intervalle. Pro Aufruf max. 1 Rezept je Workshop. T3 ab `AutoCraftTier3UnlockLevel`, sonst T2 ab `AutoCraftTier2UnlockLevel` (Workshop-Level-Gates).

> Hinweis: Die fruehere Tabelle ("T1 vs. T2-T4"-Intervalle, InnovationLab 60s, "Premium -50%") war falsch. Die "Offset"-Werte (90/270) sind reine GameLoop-Tick-Scheduling-Offsets zur Lastverteilung (180-/360-Tick-Slot), KEINE Produktions-Intervalle. Eine Premium-Halbierung der Auto-Produktions-Intervalle existiert im Original NICHT.

### 10.6a Verkauf aus dem Lager (Crafting-Sell-Preis)

`SellProducts(productId, count)`: `sellable = Max(0, CraftingInventory[id] − ReservedInventory[id])` (reservierte ausgeschlossen), `sellCount = Min(count, sellable)`, `AddMoney(GetSellPrice × sellCount)`, Telemetrie `material_sold` (source="warehouse").

**Crafting-Sell-Preis** (NICHT der Markt-Preis — §12):
```
levelMult           = 1.0 + Log2(1.0 + workshopLevel / 15.0)     // Log-Divisor 15.0
prestigeIncomeBonus = IncomeCalculatorService.GetPrestigeIncomeBonus(state)
rebirthBonus        = Workshop.RebirthIncomeBonus (produzierender Workshop)
boostMult           = IncomeCalculator.CalculateCraftingSellMultiplier(state, prestigeIncomeBonus, rebirthBonus)
GetSellPrice        = Round( product.BaseValue × levelMult × boostMult )
```

### 10.7 Cross-Workshop-Inputs (V7)

```
GetEffectiveInputs(recipe, playerLevel):
  if playerLevel >= 100: return recipe.InputProducts (alle, inkl. Cross-Workshop)
  else:                  return GetOwnInputs(recipe)  (nur Inputs deren Output zum gleichen WorkshopType gehört)
```
Unter Level 100 werden also nur die eigenen (gleicher Workshop) Inputs gefordert — Cross-Workshop-Inputs sind erst ab Level 100 nötig.

### 10.8 Tools (8 aufrüstbare Werkzeuge, Goldschrauben) (1:1 Original)

8 ToolTypes, MaxLevel 5, Start bei Level 0 (`IsUnlocked = Level > 0`). Upgrade kostet Goldschrauben (GS):

| von Level | Upgrade-Kosten (GS) | ZoneBonus (Ziel-Level) | TimeBonus (s) |
|-----------|---------------------|------------------------|---------------|
| 0 → 1 | 5 | 0.05 | 5 |
| 1 → 2 | 15 | 0.10 | 8 |
| 2 → 3 | 35 | 0.15 | 10 |
| 3 → 4 | 70 | 0.20 | 12 |
| 4 → 5 | 120 | 0.25 | 15 |

ZoneBonus = Faktor für Sägen-/Zonen-Spiele; TimeBonus = Extra-Sekunden. Welcher Effekt greift, entscheidet das jeweilige MiniGame (Doku: Säge=ZoneBonus; Rohrzange/Schraubendreher/Pinsel=TimeBonus).

Zuordnung ToolType → MiniGame:

| ToolType | NameKey | RelatedMiniGame |
|----------|---------|-----------------|
| Saw | ToolSaw | Sawing |
| PipeWrench | ToolPipeWrench | PipePuzzle |
| Screwdriver | ToolScrewdriver | WiringGame |
| Paintbrush | ToolPaintbrush | PaintingGame |
| Hammer | ToolHammer | RoofTiling |
| SpiritLevel | ToolSpiritLevel | Blueprint |
| Magnifier | ToolMagnifier | Inspection |
| Compass | ToolCompass | DesignPuzzle |

### 10.9 3D-Visualisierung

- Crafting-Tisch in jeder Werkstatt sichtbar (animiert während Produktion)
- T4-Crafting: Villa/Skyscraper/Imperium-HQ als **Mega-Projekte** mit 5 Bauphasen (siehe ASSETS_AI.md § 12, „Mega-Projekte: 2 + 5 Bauphasen")

---

## 11. Lager (Warehouse V7)

> Verbindliche Werte 1:1 aus `WarehouseService.cs` und `AutoSellRule.cs`.

### 11.1 Lager-Stats

| Konstante / Wert | Wert |
|------------------|------|
| Start-Slots (WarehouseSlotCount) | 20 |
| Start-Stack-Limit (WarehouseStackLimit) | 50 |
| SlotsPerUpgrade | 5 |
| MaxSlots | 200 |
| SlotUpgradeBaseCost | 50.000 € |
| SlotUpgradeExponent | 1.5 |
| Stack-Limit Hard-Cap | 9999 |

**Slot-/Stack-Berechnung:**
```
EffectiveSlotCount = Min(200, WarehouseSlotCount + Research.BonusWarehouseSlots + GuildMegaProject.BonusWarehouseSlots)
UsedSlotCount      = Anzahl CraftingInventory-Einträge mit Value > 0
FreeSlotCount      = Max(0, EffectiveSlotCount − UsedSlotCount)
IsWarehouseFull    = FreeSlotCount == 0
CurrentStackLimit  = Min(9999, Max(WarehouseStackLimit, Round(WarehouseStackLimit × StackLimitMultiplierFromResearch)))
StackLimitMultiplierFromResearch = Max(1.0, Research.StackLimitMultiplier)
```
Gilden-MegaProject-Slots: Kathedrale +3, HQ +5, beide +8. Stack-Limit mit ×10 Research = 500 (gecappt bei 9999).

### 11.2 Slot-Upgrade-Kosten

```
upgradesDone = Max(0, (WarehouseSlotCount − 20) / 5)
GetNextSlotUpgradeCost = 50_000 × 1.5^upgradesDone     (0 wenn SlotCount >= 200)
```
Beispiele: 1. Upgrade (20→25) = 50.000 € · 2. (25→30) = 75.000 · 3. = 112.500 · 4. = 168.750. `TryUpgradeSlots`: `TrySpendMoney(cost)`, `WarehouseSlotCount = Min(200, +5)`.

### 11.3 Logistics-Research-Boni

| Node | Effekt |
|------|--------|
| logi_01 | +5 Slots |
| logi_02 | Stack-Limit ×2 |
| logi_04 | +10 Slots |
| logi_03 | +25 Slots (Premium) |
| logi_11 | Stack-Limit ×5 (zusammen mit logi_02 → ×10) |
| logi_06 | +25 Slots + Crafting-Speed |
| logi_07 | Auto-Sell-Regeln freischalten |

### 11.4 Inventar-/Reservierungs-Operationen

- `CanAddToInventory(productId, count)`: bei Bestand `current + count ≤ CurrentStackLimit`; bei neuem Slot `FreeSlotCount > 0 && count ≤ CurrentStackLimit`.
- `AddToInventory(...)`: füllt bis Stack-Limit, Rest = overflow → siehe §11.5. Gibt `actuallyAdded` zurück.
- `TryReserve`: nur wenn `current − reserved ≥ count`. `ConsumeReserved`: reduziert `ReservedInventory` UND `CraftingInventory` atomar. `ReleaseReserved`: gibt Reservierung frei (kein Verbrauch). `GetAvailable = Max(0, current − reserved)`.
- `GetTotalWarehouseValue` = Σ `GetSellPrice × count` über alle Bestände.

### 11.5 Auto-Sell-Regeln (Unlock via logi_07)

`AutoSellRule` pro Material: `Enabled` (Default false, V7 nutzt nur dieses), `MinPrice` (reserviert, 0), `KeepUntil` (reserviert, 0). `GetAutoSellRule` legt Default-Regel an, wenn fehlend.

Bei Stack-Overflow (`AddToInventory`):
- `AutoSellRule.Enabled` → Auto-Verkauf zum Marktpreis `GetSellPrice × overflow`, mit Gilden-Bonus `× (1 + MegaProjectAutoSellPriceBonus)`; `AddMoney`; Event `OverflowAutoSold`.
- sonst, wenn `sourceWorkshop` gesetzt → Event `WorkshopPaused` + Telemetrie `warehouse_full_pause`.
- sonst stilles Verwerfen (z.B. Offline-Earnings).

### 11.6 3D-Visualisierung

- Lager als **3D-Regalsystem** in jeder Werkstatt
- Animiert auffüllend (Particle-Drop in Slots)
- Material-Icons als TextMeshPro-SpriteAsset

---

## 12. Markt & Material-Affinität

> Verbindliche Werte 1:1 aus `MarketService.cs`. Markt-Konstanten: SpreadFactor 0.05 (5% Maklergebühr), DailyAmplitude 0.50 (±50%), MarketUnlockResearchId `logi_05`.

### 12.1 Markt-System (Unlock via logi_05)

**Verfügbarkeit (`IsMarketAvailable`):** true wenn `state.IsPremium`; sonst true wenn kein ResearchService vorhanden; sonst nur wenn `logi_05` erforscht ist.

**Deterministische Tagespreis-Engine** (pro Spieler/Tag/Material, `ComputeDailyFactor`):
```
playerKey    = state.PlayerGuid ?? "anonymous"
dayIndex     = (int)(utc − 2026-01-01 UTC).TotalDays
seed         = StableHash.Compute(playerKey) ^ dayIndex ^ StableHash.Compute(productId)
rng          = new Random(seed)
phaseOffset  = rng.NextDouble() × 2π
hourFraction = utc.TimeOfDay.TotalHours / 24.0
phase        = hourFraction × 2π + phaseOffset
factor       = 1.0 + sin(phase) × 0.50          (Bereich 0.5 .. 1.5)
```
Wichtig: `StableHash` (nicht `string.GetHashCode()`) — sonst pro-Prozess-randomisiert. Jedes Material hat über `phaseOffset` einen eigenen, aber pro Spieler/Tag reproduzierbaren Phasenversatz.

`GetPriceTrend(productId) = Clamp((factorNextHour − factorNow) × 2.0, −1, 1)`. `Get24hPriceSeries`: 24 Werte `Max(1, Round(BaseValue × factor(startOfDay + h)))`.

**Premium-Feature:** **Markt-Insider-Heatmap** zeigt Preistrendlinien (Premium-only — reine Präsentation, ändert keine Preise).

### 12.2 Event-Modulation

Greift nur wenn `ActiveEvent.Effect.AffectedWorkshop == recipe.WorkshopType` (also material-/workshop-spezifisch, nicht global):
- `GameEventType.MaterialShortage` → `price × 3`
- `GameEventType.HighDemand` → `price × 2`

### 12.3 Buy & Sell

```
GetBuyPrice(productId)  = Max(1, Round( BaseValue × ComputeDailyFactor ))   [× Event-Modulator]
GetSellPrice(productId) = Round( GetBuyPrice(productId) × (1 − 0.05) )       = Round( Kaufpreis × 0.95 )
```
**Verkaufs-Spread immer 5%** (`SpreadFactor 0.05`) — gilt auch für Premium-Spieler (kein 0%-Premium-Bonus).

- `TryBuy(productId, count)`: Stack/Slot-Check via `WarehouseService.CanAddToInventory`, `TrySpendMoney`; bei Teil-Einlagerung Differenz zurückerstatten; Telemetrie `material_market_trade` (side="buy").
- `TrySell(productId, count)`: `sellable = Max(0, CraftingInventory[id] − ReservedInventory[id])`, `sellCount = Min(count, sellable)`, `AddMoney(GetSellPrice × sellCount)`; Telemetrie (side="sell"); gibt revenue zurück.

### 12.4 Material-Affinität-Enum

```csharp
public enum MaterialAffinity
{
    None,    // kein Affinitäts-Bonus
    Wood,    // Holz
    Metal,   // Metall
    Stone,   // Stein
    Art,     // Kunst (Paint, Paper, Artwork)
    Tech     // Technologie (Wire, Silicon, Circuits)
}
```
`MaterialAffinityExtensions.GetMaterialAffinity(OutputProductId)` ordnet jedem Crafting-Produkt eine Affinität zu (`None` = kein Bonus).

### 12.5 Worker-Affinity-Matching (= Crafting-Speed-Bonus)

Jeder Worker bekommt beim Hire eine zufällige Affinität (5×20% gleichverteilt über Wood/Metal/Stone/Art/Tech). Material-Affinity wirkt **ausschließlich als Crafting-Speed-Bonus** (NICHT als Worker-Effizienz-Multiplikator):

```
MaterialAffinityBonus = 0.20 × matchingWorkingWorkers / totalWorkingWorkers
```
Linear, anteilig an den arbeitenden Workern, deren Affinität zur Affinität des produzierten Produkts (`GetMaterialAffinity(OutputProductId)`) passt. 0 bei `None` oder ohne arbeitenden Worker. Geht als Summand in den Crafting-Speed-Bonus ein (Gesamt-Cap 50%, §10.5):

| Anteil matchender Worker | Crafting-Speed-Bonus |
|--------------------------|----------------------|
| 5/5 (alle) | +20% |
| 4/5 | +16% |
| 3/5 | +12% |
| 2/5 | +8% |
| 1/5 | +4% |
| 0/5 | 0% |

### 12.6 3D-Visualisierung

- Markt als **animiertes Liniendiagramm** im UI Toolkit
- Preise springen mit Tween-Animation
- Markttafel-Glow bei „günstigen" Preisen

---

## 13. Reputation & Tier-System

### 13.1 Score & Reward-Multiplikator

Quelle: `CustomerReputation.cs`. `ReputationScore` ist 0–100, Start **50** (Default; bei Prestige aus
Ascension-Perk `GetStartReputation()` → 50/65/80/100). Der score-basierte `ReputationMultiplier`
wirkt auf Income/Auftragsbelohnung (`multiplier *= Reputation.ReputationMultiplier` in
`CalculateOrderRewardMultiplierUnlocked`):

| Score | Reward-Mult (ReputationMultiplier) |
|-------|------------------------------------|
| < 30 | 0.7× |
| < 60 | 1.0× |
| < 80 | 1.2× (= „Region-Star Reward-Mult 1.2×") |
| ≥ 80 | 1.5× |

`ReputationLevelKey` (Loc): <30 ReputationPoor, <60 ReputationAverage, <80 ReputationGood,
<90 ReputationExcellent, ≥90 ReputationLegendary.

### 13.2 Reputation-Tier-System (CustomerReputationTier)

Quelle: `Enums/CustomerReputationTier.cs`. Diese vier Tiers (mit Hysterese) bestimmen Badge,
Stammkunden-Spawn-Bonus und Live-Order-Spawn-Chance — getrennt vom score-basierten Reward-Mult oben.

| Tier | Enum | Range | Badge-Farbe | Stammkunden-Spawn | Live-Order-Spawn | Up | Down |
|------|------|-------|-------------|--------------------|-------------------|----|------|
| **Beginner** | 0 | 0–30 | #8B6F47 (Holz, unsichtbar) | 0 | 0 (Default) | — | — |
| **CityKnown** | 1 | 31–60 | #CD7F32 (Bronze) | +0.10 (+10 %) | 0 | 31 | 28 |
| **RegionStar** | 2 | 61–80 | #C0C0C0 (Silber) | +0.20 (+20 %) | 0.05 (5 %, Override) | 61 | 58 |
| **IndustryLegend** | 3 | 81–100 | #FFD700 (Gold) | +0.35 (+35 %) | 0.10 (10 %, Override) | 81 | 78 |

`GetLocalizationKey`: RepTierBeginner / RepTierCityKnown / RepTierRegionStar / RepTierIndustryLegend.
**FromScore (stateless):** ≥81 IndustryLegend, ≥61 RegionStar, ≥31 CityKnown, sonst Beginner.
**Hysterese (FromScoreWithHysteresis):** Tier-Up bei 81/61/31, Tier-Down erst 3 Punkte darunter
(78/58/28) — verhindert Flickern. `CurrentTier` wird persistiert (`RecomputeTier(out oldTier)` liefert
true bei Wechsel).

### 13.3 Extra-Order-Slots & Order-Qualitäts-Bonus

`ExtraOrderSlots`: Score ≥ 90 → 2, ≥ 70 → 1, sonst 0.
`OrderQualityBonus` (senkt Standard-Order-Wahrscheinlichkeit): Score < 30 → −0.10, < 60 → 0,
< 80 → +0.10, ≥ 80 → +0.20.

### 13.4 AddRating — Delta-Logik

Quelle: `CustomerReputation.cs` (`AddRating`). Pro Auftrags-Sternebewertung (1–5):

```
stars = clamp(stars, 1, 5)
RecentRatings.Add(stars)        # max 50, ältester entfernt
delta = stars: 5 → +3, 4 → +1, 3 → 0, 2 → −2, sonst(1) → −5
if delta > 0 and researchReputationBonus > 0:
    delta = ceil(delta × (1 + researchReputationBonus))    # z. B. +45 % bei voller Forschung
ReputationScore = clamp(ReputationScore + delta, 0, 100)
```

### 13.5 Decay

Quelle: `CustomerReputation.cs` (`DecayReputation`), 1× pro Tag:

```
if ReputationScore > 50:  ReputationScore = max(50, ReputationScore − 1)
```

Fällt nie unter 50 durch Decay.

### 13.6 Showroom-Gewinn

`BuildingService.GetDailyReputationGain() = GetBuilding(Showroom)?.DailyReputationGain ?? 0`.
Der konkrete level-abhängige Tageswert liegt in `Building.cs`/`BuildingFormulas` (nicht in dieser
Extraktion belegbar). Bei einem Risk-Hard-Fail: Reputation-Verlust über `AddRating` mit niedriger
Sterne-Bewertung (negatives Delta, siehe §13.4).

### 13.7 Tier-Up-Effekte (ReputationTierEffects)

Quelle: `Services/ReputationTierEffects.cs`. Nur bei Tier-Aufstieg (`e.IsUp`): FloatingText
(`{TierName} reached!` via `RepTierUpFormat`), Celebration, LevelUp-Sound. Achievement-Dialog mit
Tier-Effekt-Texten nur für CityKnown/RegionStar/IndustryLegend (RepTierCityKnownEffects /
RepTierRegionStarEffects / RepTierIndustryLegendEffects).

### 13.8 Reputation-Shop (5 Items, Unlock ab Score 60)

Quelle: `ReputationShopService.cs`, `ReputationShopItem.cs`. Sichtbar/freigeschaltet ab
`ReputationScore >= MinReputationToUnlock` (**= 60**). Bezahlt mit `ReputationScore`-Punkten (3.
Währung, keine GS, kein Geld; Score wird beim Kauf abgezogen). Items statisch/global, nicht
„ausverkauft" (mehrfach kaufbar, solange Score reicht). `ReputationShopService` in DI registrieren.

| Id | Effekt (Enum) | Kosten (Rep) | Icon | Name (DE) | Wirkung |
|-----|---------------|--------------|------|-----------|---------|
| rep_regular_customer_guarantee | RegularCustomerGuarantee | 30 | AccountStar | Stammkunden-Garantie | Nächste 5 Aufträge werden Stammkunden (bis 1.5× Reward) → `RepShopRegularCustomerCharges = 5` |
| rep_faster_delivery | FasterDelivery | 20 | Truck | Schnelle Lieferung | Nächster Lieferant +50 % Speed für 1 h → `RepShopFasterDeliveryUntil = UtcNow + 1h` |
| rep_worker_mood_boost | WorkerMoodBoost | 25 | AccountGroup | Team-Stimmungs-Boost | Alle Worker +30 Mood (sofort, `min(100, Mood+30)`) |
| rep_workshop_skin_wood_premium | WorkshopSkinWoodPremium | 100 | Palette | Workshop-Skin „Holz-Premium" | Permanenter kosmetischer Skin → `RepShopWoodPremiumSkinUnlocked = true` |
| rep_insurance | ReputationInsurance | 40 | ShieldCheck | Reputation-Insurance | Nächster Risk-Miss kostet keine Reputation → `RepShopInsuranceCharges = 1` |

**Kauf-Logik (`TryBuy`, `ApplyEffect`):** ablehnen, wenn Item nicht gefunden oder
`ReputationScore < ReputationCost`; sonst Score abziehen, `ApplyEffect` (unter State-Lock),
`ItemPurchased`-Event. State-Felder: `RepShopRegularCustomerCharges`, `RepShopFasterDeliveryUntil`,
`RepShopInsuranceCharges`, `RepShopWoodPremiumSkinUnlocked`.

### 13.9 3D-Visualisierung

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
| mt_emerald_toolbox | Emerald Toolbox | Epic | +10% Income | Workshop Lv 500 |
| mt_dragon_anvil | Dragon Anvil | Epic | +10% Income | Gold-Prestige (GoldCount >= 1) |
| mt_master_crown | Master Crown | Legendary | +15% Income | Alle 11 anderen gesammelt |

**Gesamtbonus alle 12:** +74% Income (0.02×2 + 0.03×2 + 0.05×3 + 0.07×2 + 0.10×2 + 0.15).

Quelle: `MasterTool.cs` (statische `_allDefinitions`). GameState speichert nur die gesammelten IDs
(`CollectedMasterTools`); Gesamt-Income-Bonus = Summe der IncomeBonus aller gesammelten (`GetTotalIncomeBonus`).
MasterToolRarity: Common/Uncommon/Rare/Epic/Legendary. Prestige-gebundene Unlocks: Crystal Chisel = Bronze
(BronzeCount >= 1), Ruby Blade = Silber (SilverCount >= 1), Dragon Anvil = Gold (GoldCount >= 1).

### 14.2 3D-Visualisierung

- Jedes Master-Tool als 3D-Modell mit **Emissive-Shader** (Glow gemäß Seltenheit)
- Master-Crown-Unlock: Cinematic-Sequenz, 3D-Krone rotiert mit Bloom
- Anzeige im Hub als Trophäen-Vitrine

---

## 15. Gebäude (7 Stück)

### 15.1 Tabelle

Quelle: `BuildingType.cs` (`GetBaseCost`/`GetUnlockLevel`), `Building.cs`, `BuildingService.cs`. Alle Gebäude
haben **Max-Level 5**. Upgrade-Kosten: `NextLevelCost = BaseCost × 2^Level` (Build = BaseCost für Level 1;
danach Lv→Lv+1 = `BaseCost × 2^Level`). Es gibt **kein „Tech-Hub"** — das 3. Gebäude ist das **Büro** (Office,
zusätzliche Auftrags-Slots).

| Gebäude (Type) | Effekt | Unlock-Lv | BaseCost | Per-Level (Lv1–5) |
|----------------|--------|-----------|----------|-------------------|
| **Kantine** (Canteen) | Stimmungs-Erholung/h + Ruhezeit-Reduktion | 5 | 10.000 € | MoodRecovery/h: 1/2/3/4/5 %; RestTimeReduction: 50/55/60/70/80 % |
| **Lager** (Storage) | −Material-Kosten | 8 | 15.000 € | 15/25/35/45/50 % |
| **Büro** (Office) | +Auftrags-Slots | 10 | 20.000 € | ExtraOrderSlots = Level+1 → 2/3/4/5/6 |
| **Ausstellungsraum** (Showroom) | +Ruf/Tag | 15 | 25.000 € | DailyReputationGain = Level×0.5 → 0.5/1.0/1.5/2.0/2.5 |
| **Trainings-Zentrum** (TrainingCenter) | +Training-Speed | 20 | 50.000 € | Multiplikator: 2.5x/3.5x/4.5x/5.5x/6.5x |
| **Fuhrpark** (VehicleFleet) | +Auftrags-Reward | 25 | 75.000 € | 20/30/40/50/60 % |
| **Werkstatt-Erweiterung** (WorkshopExtension) | +Worker-Slots | 30 | 100.000 € | ExtraWorkerSlots = Level+1 → 2/3/4/5/6 (addiert sich pro Workshop) |

### 15.2 3D-Visualisierung

Gebäude erscheinen als **physische 3D-Strukturen in der Handwerker-Stadt** (siehe § 33).

---

## 16. Equipment-System

Quelle: `Equipment.cs` (EquipmentType/EquipmentRarity/Equipment), `EquipmentService.cs`.
NameKey-Schema: `Equipment_{Type}_{Rarity}`.

### 16.1 Rarity-Tiers (4 — KEIN Legendary)

| Rarity | Farbe | Icon |
|--------|-------|------|
| Common | `#9E9E9E` Grau | Circle |
| Uncommon | `#4CAF50` Grün | DiamondOutline |
| Rare | `#2196F3` Blau | Diamond |
| Epic | `#9C27B0` Lila | Star |

### 16.2 Typen & Slot (pro Worker)

**4 Equipment-Typen:** Helmet (Helm), Gloves (Handschuhe), Boots (Stiefel), Belt (Gürtel).
Jeder Worker hat **1 Equipment-Slot** (`EquippedItem`) — kein Multi-Slot. Nicht getragene Stücke liegen im
`EquipmentInventory`. `EquipItem`/`UnequipItem` verschiebt zwischen beiden und invalidiert den Income-Cache.

### 16.3 Stats (3 echte Stats, je Rarity gerollt)

Quelle: `Equipment.GenerateRandom` (`rng.Next(a, b)/100`, b exklusiv). Keine „Mini-Game-Score"- oder
„Lohn-Reduktions"-Stats:

| Rarity | EfficiencyBonus | FatigueReduction | MoodBonus |
|--------|-----------------|------------------|-----------|
| Common | 5–7 % | 3–5 % | 3–5 % |
| Uncommon | 8–10 % | 6–8 % | 5–7 % |
| Rare | 11–13 % | 9–11 % | 7–9 % |
| Epic | 13–15 % | 11–14 % | 8–10 % |

Gesamt-Spannweiten über alle Rarities: EfficiencyBonus 5–15 %, FatigueReduction 3–14 %, MoodBonus 3–10 %.

### 16.4 Erwerb

- **Mini-Game-Drop** (`TryGenerateDrop`): `dropChance = 0.05 + difficulty × 0.05 + (Perfect ? 0.05 : 0)`.
  Effektiv: Easy 10 % / Medium 15 % / Hard 20 % / Expert 25 %, je +5 % bei Perfect.
  Rarity-Gewichtung difficulty-abhängig (`roll = rng.Next(100)`): `diff>=3 & roll<5` → Epic; `diff>=2 & roll<20`
  → Rare; `roll<45` → Uncommon; sonst Common. Typ gleichverteilt (25 % je Typ).
- **Shop** (`GetShopItems`): 3–4 Items pro Rotation (`Next(3,5)`), jedes mit `shopDifficulty = Next(1,4)`.
  Kauf (`BuyEquipment`) in Goldschrauben (`ShopPrice`):

| Rarity | ShopPrice (GS) |
|--------|----------------|
| Common | 3 |
| Uncommon | 8 |
| Rare | 18 |
| Epic | 40 |

### 16.5 Equipment-Preservation (Prestige)

- **Meister-Prestige (Tier 6):** Equipment bleibt erhalten; Gebäude werden auf Level 1 zurückgesetzt (statt entfernt).
- **Legende-Prestige (Tier 7):** zusätzlich Manager (Level → 1) und **Top-3-Worker pro Workshop** (nach Effizienz) bewahrt.

---

## 17. Gilden & Multiplayer

### 17.1 Gilden-Struktur

Die Gilden-Daten liegen in der **Firebase Realtime Database**, identifiziert über die stabile **PlayerId** (GUID, nicht Firebase-Uid). Lokal wird nur ein minimaler Cache (`GuildMembership`) im Save persistiert. Firebase-Pfad-Schema siehe § 17.12 — 1:1 wie Avalonia.

| Feature | Beschreibung |
|---------|-------------|
| **CRUD** | Erstellen, Beitreten, Verlassen, Auflösen |
| **Max Mitglieder (Basis)** | **20** (`BaseMaxGuildMembers = 20`). `GetMaxMembers() = 20 + ResearchMaxMembersBonus + HallMaxMembersBonus` (gecachte Werte) |
| **Max-Member-Boni** | Research Infrastruktur +5/+5/+10 (max +20) · Halle `AssemblyHall` +2/Level bis MaxLevel 3 (max +6) → theoretisches Maximum **46** |
| **Gilden-Name** | Mindestlänge 2, Cap 30 Zeichen, Unicode-Control/Format-Zeichen entfernt + `ProfanityFilter` (6 Sprachen) |
| **Spielername** | Cap 30 Zeichen, gleiche Bereinigung. Preferences-Key `guild_player_name` |
| **GuildId-Format** | `g_{utcNow:yyyyMMddHHmmss}_{uid[..min(6,len)]}` |
| **Invite-Codes** | 6-stellig, Zeichensatz `A-Z0-9` (36 Zeichen), kryptografisch via `RandomNumberGenerator`; max 5 Kollisions-Versuche. Lookup über `code.ToUpperInvariant()` |
| **Rollen** | **Member / Officer / Leader** (genau 3 Rollen — KEIN Co-Leader). `"founder"` existiert nur als Legacy-Stale-Filter-String, ohne Enum-Wert und ohne setzenden Code-Pfad |
| **Wochenziel** | **1 kollektives** Wochenziel pro Woche (siehe § 17.1.2), KEINE 3 separaten Ziele |
| **Verlassen** | **KEIN Cooldown** — jederzeit möglich (siehe § 17.1.3) |

#### 17.1.1 Rollen-Rechte

| Rolle | Firebase-String | Rechte |
|-------|-----------------|--------|
| **Member** | `"member"` | keine Sonderrechte |
| **Officer** | `"officer"` | darf einladen + Members kicken (nicht Officer/Leader) |
| **Leader** | `"leader"` | volle Admin-Rechte: befördern, degradieren, alle kicken, Führung übertragen |

`GuildMembership`-Defaults: `GuildLevel = 1`, `GuildIcon = "ShieldHome"`, `GuildColor = "#D97706"`, `GuildHallLevel = 1`, `LeagueId = "bronze"`.

#### 17.1.2 Wochenziel (Weekly Goal)

- `DefaultWeeklyGoal = 500_000` (long). Wochenstart = **Montag (UTC)**; Sonntag zählt als letzter Tag der Vorwoche.
- **Wochenreset** (`CheckWeeklyResetAsync`, ausgelöst bei `RefreshGuildDetailsAsync`): wenn `weekStart < currentMonday` → `weekStart = currentMonday`, `weeklyProgress = 0`, eigene `contribution = 0`.
- Bei erreichtem Ziel (`WeeklyProgress >= WeeklyGoal`):
  - `level += 1`, `totalWeeksCompleted += 1`.
  - **Neues Wochenziel (sqrt-Skalierung):** `weeklyGoal = (long)(500_000 * (1 + Math.Sqrt(newLevel) * 0.2))`.
  - **GS-Belohnung:** `screwReward = Math.Min(50, 5 + Level * 2)` (altes Level vor Inkrement). Duplikat-Schutz Preferences-Key `guild_weekly_reward_{Montag:yyyy-MM-dd}`.
- **Beitrag** (`ContributeAsync`): Wöchentliches Spenden-Cap pro Spieler = **30% des Wochenziels** (`maxDonation = (long)(weeklyGoal * 0.30)`), Tracking-Key `guild_weekly_donation_{Montag:yyyy-MM-dd}_{uid}`. Betrag auf verbleibendes Cap begrenzt. Geld via `TrySpendMoney`, Firebase `weeklyProgress += contribution`, Rollback (`AddMoney`) bei Fehler. Integritäts-Check `VerifyIntegrityForFirebase` (HMAC) vor jedem Write.

**Beispiel-Wochenziele** (berechnet, nicht hardcoded):

| Neues Level | Formel `500000*(1+0.2*sqrt(L))` | Wert (abgerundet) |
|-------------|---------------------------------|-------------------|
| 2 | 500000*(1+0.2*1.4142) | 641 421 |
| 5 | 500000*(1+0.2*2.2360) | 723 607 |
| 10 | 500000*(1+0.2*3.1623) | 816 228 |
| 20 | 500000*(1+0.2*4.4721) | 947 214 |

#### 17.1.3 Verlassen (KEIN Cooldown)

Verlassen ist jederzeit möglich (`LeaveGuildAsync`):
1. Wenn eigene Rolle = Leader → automatischer Leader-Transfer (`TransferLeadershipOnLeaveAsync`): ältester Officer nach `JoinedAt`, sonst ältestes Mitglied bekommt `"leader"`.
2. Eigenen Member-Eintrag + `guild_boss_damage/{guildId}/{uid}` löschen.
3. `CountAndSyncMemberCountAsync`; bei `newCount == 0` → komplette Gilde aufräumen (`CleanupDeletedGuildAsync`).
4. `player_guilds/{uid}` löschen, lokalen Cache leeren, `GuildResearchService.InvalidateCache()`.

#### 17.1.4 Gilden-Einkommens-Bonus

- Formel: `IncomeBonus = Math.Min(0.20m, GuildLevel * 0.01m)` → **+1% pro Gilden-Level, gedeckelt bei +20%** (ab Level 20).
- `GuildMembership.IncomeBonus` ist `[JsonIgnore]` (berechnet aus persistiertem `GuildLevel`) → offline-tauglich.
- Gecachte Zusatz-Boni in `GuildMembership` (persistiert, von den jeweiligen Services aktualisiert): Research-Effekte (14 Felder), Hall-Effekte (9 Felder), Mega-Projekt-Boni (3 Felder + Liste).

#### 17.1.5 Einladungs-System & Browse

- **Browse** (`BrowseGuildsAsync`): max 50 Gilden (`orderBy="level"&limitToLast=50`); volle Gilden (`MemberCount >= MaxMembers`) ausgeblendet; Sortierung Level DESC, dann MemberCount DESC.
- **Verfügbare Spieler** (`available_players/{uid}`): `AvailablePlayerInfo` (Name, Level, LastActive), Browser max 50 (nach LastActive sortiert).
- **Einladungs-Inbox** (`player_invites/{uid}/{guildId}` → `GuildInvitation`): **max 10 Einladungen** pro Spieler (älteste wird gelöscht). Felder: guildName, guildIcon, guildColor, guildLevel, memberCount, invitedBy, invitedAt.
- **Invite-Code-Mapping**: bidirektional `guild_invite_codes/{guildId}` ↔ `invite_code_to_guild/{code}`.
- **Stale-Member-Schwelle: 30 Tage** Inaktivität (`IsStaleMember`, nur aus Anzeige gefiltert; eigener Spieler nie gefiltert; Rolle `founder`/`leader` nie stale).

### 17.2 Guild-Research (18 Nodes, 6 Kategorien)

Firebase-Pfad: `guild_research/{guildId}/{researchId}`. Kosten in EUR. `EffectValue` je nach Typ Prozent (decimal) oder absolute Zahl (int-Cast). Alle 18 Nodes vollständig:

| Id | Kategorie | Order | Cost | EffectType | EffectValue | Icon |
|----|-----------|------:|-----:|------------|-------------|------|
| guild_expand_1 | Infrastructure | 1 | 50 000 000 | MaxMembers | 5 | AccountMultiplePlus |
| guild_expand_2 | Infrastructure | 2 | 500 000 000 | MaxMembers | 5 | AccountMultiplePlus |
| guild_expand_3 | Infrastructure | 3 | 5 000 000 000 | MaxMembers | 10 | AccountMultiplePlus |
| guild_income_1 | Economy | 1 | 10 000 000 | IncomeBonus | 0.05 (+5%) | Handshake |
| guild_income_2 | Economy | 2 | 100 000 000 | CostReduction | 0.10 (−10%) | CartArrowDown |
| guild_income_3 | Economy | 3 | 1 000 000 000 | RewardBonus | 0.10 (+10%) | TruckDelivery |
| guild_income_4 | Economy | 4 | 10 000 000 000 | IncomeBonus | 0.15 (+15%) | CurrencyEur |
| guild_knowledge_1 | Knowledge | 1 | 25 000 000 | XpBonus | 0.10 (+10%) | BookOpenVariant |
| guild_knowledge_2 | Knowledge | 2 | 250 000 000 | EfficiencyBonus | 0.05 (+5%) | Cog |
| guild_knowledge_3 | Knowledge | 3 | 2 500 000 000 | MiniGameBonus | 0.15 (+15%) | SchoolOutline |
| guild_logistics_1 | Logistics | 1 | 75 000 000 | OrderSlotBonus | 1 | ClipboardTextMultiple |
| guild_logistics_2 | Logistics | 2 | 750 000 000 | OrderQualityBonus | 0.15 (+15%) | AccountTie |
| guild_logistics_3 | Logistics | 3 | 3 000 000 000 | RewardBonus | 0.20 (+20%) | RocketLaunch |
| guild_workforce_1 | Workforce | 1 | 150 000 000 | WorkerSlotBonus | 1 | DomainPlus |
| guild_workforce_2 | Workforce | 2 | 1 000 000 000 | TrainingSpeedBonus | 0.25 (+25%) | HumanMaleBoard |
| guild_workforce_3 | Workforce | 3 | 5 000 000 000 | FatigueReduction | 0.20 (−20%) | ShieldAccount |
| guild_mastery_1 | Mastery | 1 | 500 000 000 | ResearchSpeedBonus | 0.20 (+20%) | FlashOutline |
| guild_mastery_2 | Mastery | 2 | 7 500 000 000 | PrestigePointBonus | 0.10 (+10%) | Crown |

**Kategorie-Farben:** Infrastructure #D97706, Economy #4CAF50, Knowledge #2196F3, Logistics #9C27B0, Workforce #0E7490, Mastery #FFD700, Default #888888.

**Aggregierte Effekte (alle abgeschlossen):** IncomeBonus +20% · CostReduction −10% · RewardBonus +30% · XpBonus +10% · EfficiencyBonus +5% · MiniGameBonus +15% · MaxMembersBonus +20 · OrderSlotBonus +1 · OrderQualityBonus +15% · WorkerSlotBonus +1 pro Workshop · TrainingSpeedBonus +25% · FatigueReduction −20% · ResearchSpeedBonus +20% · PrestigePointBonus +10%.

**Forschungsdauer** (`GetResearchDurationHours(cost)`): `cost < 100M` → 1.0h · `100M ≤ cost ≤ 2B` → 4.0h · `cost > 2B` → 12.0h. `ResearchSpeedBonus` (mastery_1) verkürzt: `durH *= (1 − ResearchSpeedBonus)`.

**Beitrags-Mechanik** (`ContributeToResearchAsync`):
- Mitglieder-basierter Kosten-Rabatt: `memberCount < 3` → ×0.50 (−50%); `< 5` → ×0.75 (−25%); sonst ×1.0.
- `scaledCost = (long)(Cost * costMultiplier)`; Beitrag begrenzt auf `remaining = scaledCost − Progress`.
- Geld via `TrySpendMoney`, Rollback `AddMoney` bei Firebase-Fehler.
- Bei `Progress >= scaledCost` → `ResearchStartedAt = now` (Timer startet), `Completed` erst nach Timer-Ablauf.
- „Erste nicht-abgeschlossene Forschung pro Kategorie = aktiv" (sequenzielle Freischaltung pro Kategorie).
- Effekt-Cache auf `GuildMembership` (`ApplyResearchEffects`). `GuildResearchState` (Firebase): `progress`, `completed`, `completedAt`, `researchStartedAt`.

### 17.3 Co-op-Auftrag (2-Spieler-Einladung)

Co-op ist ein **1:1-Einladungs-Auftrag zwischen zwei Mitgliedern** (Ersteller + eingeladener Spieler) — KEIN gildenweiter Multi-Task-Auftrag. Firebase-Pfad: `guilds/{guildId}/coopOrders/{orderId}`.

**Lebenszyklus & Werte:**
- `CreateInviteAsync(invitedPlayerId, miniGameType)`: `OrderId = Guid("N")`, Status `Pending`, `ExpiresAt = UtcNow + 5 min` (Annahme-Frist). `BaseReward = 100_000m` EUR, `RewardSplit = 0.5` (50/50). MiniGame-Typ kommt vom Aufrufer. HMAC über `OrderId|CreatedBy|InvitedPlayer|BaseReward|MiniGameType` (Salt `coop-order-v1`).
- `AcceptAsync`: nur eingeladener Spieler, nur Pending, vor Ablauf → Status `Active`, `ExpiresAt = UtcNow + 3 min` (MiniGame-Fenster).
- `DeclineAsync`: Status `Expired`.
- `SubmitScoreAsync(orderId, score, isPlayer1)`: `score = Math.Clamp(score, 0, 100)`; PATCH eigenes Feld (`player1Score`/`player2Score`); wenn beide Scores gesetzt + Status Active → PATCH Status `Completed` (idempotent).
- **Reward** (`TryClaimCompletedRewardAsync`, idempotent): Lokaler Check `ClaimedCoopOrderIds` + Server-Write-once-Marker `claimedBy/{playerId}`. **Bonus +25%** wenn beide Scores `>= 95` (`bothPerfect` → ×1.25, sonst 1.0). `myShare = BaseReward × RewardSplit × multiplier` → bei Perfect 100 000 × 0.5 × 1.25 = **62 500** pro Spieler, sonst 50 000. HMAC-Tampering → Status `Expired`.

**`CoopOrderStatus`-Enum:** Pending, Active, Completed, Expired.

**`CoopOrderState`-Felder (Firebase):** orderId, createdBy, invitedPlayer, status, expiresAt (DateTime), miniGameType, player1Score (int?), player2Score (int?), rewardSplit (default 0.5), baseReward (decimal), hmac (string?).

### 17.4 Guild-Bosse (6 Typen)

Firebase-Pfad: `guild_bosses/{guildId}`, Schaden pro Spieler `guild_boss_damage/{guildId}/{uid}`. `HpPerLevel` = HP pro Boss-Level; `CalculateHp(level) = HpPerLevel × Math.Max(1, level)`. Default-Multiplikatoren (wenn nicht angegeben) = 1.0; Default `DurationHours` = 48.

| BossType | NameKey | Icon | HpPerLevel | DurationHours | Crafting× | Order× | MiniGame× | MoneyDonation× | Color |
|----------|---------|------|-----------:|--------------:|----------:|-------:|----------:|---------------:|-------|
| StoneGolem | GuildBoss_StoneGolem | Wall | 5 000 | 48 | 1.0 | 1.0 | 1.0 | 1.0 | #78716C |
| IronTitan | GuildBoss_IronTitan | ShieldSword | 7 500 | 48 | **2.0** | 1.0 | 1.0 | 1.0 | #475569 |
| MasterArchitect | GuildBoss_MasterArchitect | HardHat | 6 000 | 48 | 1.0 | **2.0** | 1.0 | 1.0 | #D97706 |
| RustDragon | GuildBoss_RustDragon | Fire | 8 000 | 48 | 1.0 | 1.0 | **2.0** | 1.0 | #DC2626 |
| ShadowTrader | GuildBoss_ShadowTrader | Ninja | 5 500 | 48 | 1.0 | 1.0 | 1.0 | **3.0** | #6D28D9 |
| ClockworkColossus | GuildBoss_ClockworkColossus | CogSync | 10 000 | **24** | **1.5** | **1.5** | **1.5** | **1.5** | #0E7490 |

**Schaden zufügen** (`DealDamageAsync(damage, source)`): Multiplikator nach `source.ToLowerInvariant()` — `"crafting"` → CraftingDamageMultiplier, `"order"`/`"orders"` → OrderDamageMultiplier, `"minigame"`/`"minigames"` → MiniGameDamageMultiplier, `"donation"`/`"donations"` → MoneyDonationDamageMultiplier, sonst 1.0. `effectiveDamage = (long)(damage × multiplier)`. Race-frei: jeder Spieler schreibt nur seinen Eintrag (`TotalDamage += effectiveDamage`, `Hits++`, `LastHitAt`). Hall-Boni werden in DealDamage NICHT angewendet (nur Boss-Typ-Multiplikator).

**Boss-Spawn** (`SpawnBossIfNeededAsync`): Typ rotiert wöchentlich — `weekNumber = ISO-Kalenderwoche`, `bossIndex = weekNumber % 6`. HP: `baseBossHp = CalculateHp(Math.Max(1, GuildLevel))`; `bossHp = (long)(baseBossHp × Math.Max(0.5, memberCount / 5.0))` → 1 Mitglied = 0.5×, 5 = 1.0×, 10 = 2.0×, 20 = 4.0× (lineare Skalierung ab 5/5). `ExpiresAt = now + DurationHours`, Status `"active"`, `BossId = BossType.ToString()`. Read-after-Write-Verifikation gegen Race; alte Damage-Einträge erst nach Bestätigung gelöscht.

**Boss-Status** (`CheckBossStatusAsync`): Throttle 30s pro guildId. HP-Aggregation client-seitig: `currentHp = Math.Max(0, BossHp − SUM(allDamage.TotalDamage))`. Bei `now >= ExpiresAt` → `"expired"`; bei `totalDamage >= BossHp` → `"defeated"`, Belohnungen verteilen.

**Boss-Belohnungen** (`DistributeBossRewardsAsync`, rang-basiert): `ownRank = (Anzahl Spieler mit mehr Schaden) + 1`. Rang 1 → **30 GS** (MVP), Rang 2–3 → **20 GS**, sonst → **10 GS**. Nur Spieler mit `Damage > 0`. KEINE Kosmetik-Belohnung (nur GS). Duplikat-Schutz Preferences-Key `guild_boss_reward_week_{guildId}`.

Boss-Tick alle 60s (Offset 20s), zusätzliches internes 30s-Throttle pro guildId. Schaden via Crafting/Aufträge/Mini-Games/Geld-Spenden. **HMAC-/Integritäts-geschützt.**

> Hinweis: Speedkill-(<24h)-, MVP- und Defeated-Zähler werden im GuildBossService NICHT inkrementiert; die zugehörigen Achievements (`guild_ach_boss_*`, `guild_ach_mvp_*`, `guild_ach_speedkill_*`) haben im Original-Code KEINE automatische Fortschritts-Aktualisierung (nicht im Code gefunden).

### 17.5 Guild-Hall (10 Gebäude)

Firebase-Pfad: `guild_hall/{guildId}/buildings/{buildingId}`. `EffectPerLevel` = Effekt pro Level; `UnlockHallLevel` = ab welchem Hallen-Level freigeschaltet. Effekt: `clampedLevel = min(level, MaxLevel)`, `totalEffect = EffectPerLevel × clampedLevel`.

| BuildingId | NameKey | Icon | EffectPerLevel | MaxLevel | UnlockHallLevel | Color | Effekt (Gesamt-Max) |
|------------|---------|------|---------------:|---------:|----------------:|-------|---------------------|
| Workshop | GuildBuilding_Workshop | Hammer | 0.02 (+2%) | 5 | 1 | #D97706 | CraftingSpeedBonus, max +10% |
| ResearchLab | GuildBuilding_ResearchLab | FlaskOutline | 0.05 (−5%) | 5 | 2 | #2196F3 | ResearchTimeReduction, max −25% |
| TradingPost | GuildBuilding_TradingPost | StorefrontOutline | 0.03 (+3%) | 5 | 3 | #4CAF50 | IncomeBonus, max +15% |
| Smithy | GuildBuilding_Smithy | Anvil | 0.02 (+2%) | 5 | 4 | #EA580C | OrderRewardBonus, max +10% |
| Watchtower | GuildBuilding_Watchtower | TowerFire | 0.05 (+5%) | 5 | 5 | #DC2626 | WarPointsBonus, max +25% |
| AssemblyHall | GuildBuilding_AssemblyHall | AccountGroup | 2 (+2 Mitgl.) | 3 | 6 | #0E7490 | MaxMembersBonus, max +6 |
| Treasury | GuildBuilding_Treasury | TreasureChest | 0.05 (+5%) | 3 | 7 | #FFD700 | WeeklyRewardBonus, max +15% |
| Fortress | GuildBuilding_Fortress | ShieldLock | 0.05 (+5%) | 3 | 8 | #475569 | DefenseBonus, max +15% |
| TrophyHall | GuildBuilding_TrophyHall | Trophy | 0 | 1 | 9 | #9C27B0 | kein numerischer Effekt (zeigt Achievements) |
| MasterThrone | GuildBuilding_MasterThrone | Crown | 0.05 (+5%) | 1 | 10 | #B91C1C | EverythingBonus, max +5% |

**Upgrade-Kosten** (`GetUpgradeCost(targetLevel)`): `screws = (int)(10 × 2^(targetLevel−1))`, `money = (long)(500_000 × 2.5^(targetLevel−1))` (aus der Gildenkasse). Beispiel: Lv1 = 10 GS / 500 000; Lv2 = 20 GS / 1 250 000; Lv3 = 40 GS / 3 125 000; Lv4 = 80 GS / 7 812 500; Lv5 = 160 GS / 19 531 250.

**Upgrade-Timer** (`UpgradeDurations`, Stunden, Tier = Ziel-Level, min 5):

| Tier | 1 | 2 | 3 | 4 | 5 |
|------|--:|--:|--:|--:|--:|
| Stunden | 1.0 | 2.0 | 4.0 | 8.0 | 12.0 |

**Service-Verhalten:** Upgrade-Voraussetzungen: `_hallLevel >= UnlockHallLevel`, nicht Max-Level, kein laufendes Upgrade, genug GS + genug Geld. Kosten via `TrySpendGoldenScrews` + `TrySpendMoney`; Rollback nutzt `AddGoldenScrews(..., fromPurchase: true)` (verhindert Premium/Prestige-GS-Boni auf Refund). Hallen-Level kommt aus `guilds/{guildId}/hallLevel` (separat von Gebäude-States); wie das Hallen-Level erhöht wird, ist im GuildHallService NICHT implementiert (nicht im Code gefunden). Effekte werden auf `GuildMembership` gecacht (`ApplyHallEffects`) für Offline-Nutzung.

Hall-Tick alle 60s (Offset 40s) → `CheckUpgradeCompletionAsync`.

### 17.6 Mega-Projekte (2 Templates)

- Alle Mitglieder spenden gecraftete Materialien (kein Zeit-Tick — Fortschritt rein spendenbasiert).
- Atomarer Firebase-PATCH pro Spende; **HMAC-signiert** (Salt `guild-mega-project-v1`).
- **Abandonment-Sunset: 30 Tage** — Projekte älter als 30 Tage werden für Spenden geblockt.
- Pro Spieler **einmaliger permanenter Bonus** bei Abschluss (Idempotenz via `ClaimedGuildProjectIds`).
- Firebase-Pfad: `guilds/{guildId}/megaProjects/active`.

**Die 2 Templates** (vollständige Material-Anforderungen + Boni siehe § 34):
- **Cathedral** → CraftingSpeed +5%, AutoSellPrice +10%, +3 Lager-Slots.
- **Headquarters (HQ)** → CraftingSpeed +10%, AutoSellPrice +20%, +5 Lager-Slots.

### 17.7 Kriegssaison

**Ligen** (`GuildLeague`): Bronze, Silver, Gold, Diamond (Firebase-String = lowercase). Achievement-Target-Mapping Silver=1, Gold=2, Diamond=3.

**Saison-Struktur:** Dauer **4 Wochen (28 Tage)**, `EndDate = seasonStart + 28 Tage`. Saison-ID `s_{ISO-Jahr}_{seasonNumber:D2}` mit `seasonNumber = (isoWeek − 1) / 4 + 1`; aktuelle Woche in Saison `((isoWeek − 1) % 4) + 1` (1–4). `status` ∈ `"active"`/`"completed"`/`"upcoming"` (default `"active"`).

**Phasen** (`WarPhase`, wochentag-basiert UTC, `GetCurrentPhase()`):
- Mo/Di/Mi → **Attack**
- Do/Fr → **Defense**
- Sa/So → **Evaluation**
- (Completed = über Status gesetzt)

`GetPhaseEndTime()` (Tage bis Phasenende, 00:00 UTC): Mo→+3 (Do), Di→+2, Mi→+1; Do→+2 (Sa), Fr→+1; Sa→+2 (Mo nächste Woche), So→+1.

**Matchmaking** (`FindOrCreateWarAsync`): Level-Matching erst ±3 (`LevelMatchTolerance`), dann ±5 (`LevelMatchToleranceExtended`). Sucht offene Wars (`orderBy="status"&equalTo="active"&limitToFirst=20`) mit `guildBId == "waiting"`. Optimistic Locking: PATCH guildB-Felder, 200ms Delay, Verify-after-Write; verloren → eigenen War erstellen (`guildBId="waiting"`, WarId `{seasonId}_w{week}_{ownGuildId}`). Kein Match → Bye-Week.

**Scoring** (`ContributeScoreAsync(points, source)`, Reihenfolge):
1. Keine Punkte in Phase Evaluation/Completed.
2. **Defense-Phase: Punkte halbiert** (`× 0.5`).
3. **Aufhol-Multiplikator ×1.5** wenn `freshOpponentScore > freshOwnScore`.
4. **Hall-WarPoints-Bonus**: `effectivePoints × (1 + HallWarPointsBonus)`.
5. Zuweisung: Attack-Phase → `AttackScore +=`, sonst → `DefenseScore +=`.

Race-frei: nur eigener Pfad `guild_war_scores/{warId}/{guildId}/{uid}`. Gilden-Gesamtscore = Summe aller Member-`TotalScore`. War-Log-Eintrag per Push.

**Bonus-Missionen** (`GetBonusMissionsAsync`, lokal/Preferences, wöchentlicher Reset `gws_bonusmission_{Montag}_{type}`):

| Id | NameKey | Target | BonusPoints |
|----|---------|-------:|------------:|
| orders_5 | GuildWarBonusOrders | 5 | 200 |
| minigames_3 | GuildWarBonusMiniGames | 3 | 150 |
| deposit_50k | GuildWarBonusDeposit | 50 000 | 100 |

Bei Abschluss → `ContributeScoreAsync(bonusPoints, "bonus_{type}")`.

**Belohnungen pro Krieg** (`DistributeWarRewardsAsync`): `MvpBonusGs=5`, `AllBonusMissionGs=3`.

| Ergebnis | GS | Liga-Punkte |
|----------|---:|------------:|
| Sieg (ownScore > opponentScore) | 20 | 3 |
| Unentschieden (==) | 10 | 1 |
| Niederlage (<) | 5 | 0 |

- **MVP-Bonus +5 GS** wenn eigener `TotalScore >= mvpScore` (und > 0).
- **+3 GS** wenn alle 3 Bonus-Missionen abgeschlossen.
- Liga-Eintrag: `Points += leaguePoints`, `Wins++`/`Losses++`. War → `status="completed"`, `phase="completed"`. Duplikat-Schutz `gws_war_reward_{warId}`.

**Saison-End-Belohnungen** (`DistributeSeasonRewards`, liga-abhängig): Diamond 100 GS, Gold 50, Silver 25, Bronze 10. Duplikat-Schutz `gws_season_reward_{seasonId}`.

**Liga-Auf-/Abstieg** (`ProcessLeaguePromotionAsync`, perzentil-basiert; Sortierung Points DESC, Wins DESC, GuildId ASC; `ownPercentile = (ownIndex+1)/totalGuilds`):

| Liga | Aufstieg | Abstieg |
|------|----------|---------|
| Bronze | Top 30% (≤0.30) → Silver | — |
| Silver | Top 25% (≤0.25) → Gold | Bottom 25% (>0.75) → Bronze |
| Gold | Top 20% (≤0.20) → Diamond | Bottom 30% (>0.70) → Silver |
| Diamond | — | Bottom 30% (>0.70) → Gold |

**Saison-Ende** (`CheckSeasonEndAsync`): Auslöser `EndDate` überschritten ODER (Woche ≥ 4 UND Phase = Evaluation). Dann: letzte War-Belohnungen, Liga-Auf/Abstieg, Saison-`status="completed"`, Saison-Belohnungen.

War-Saison-Tick alle 300s (Offset 260s) → `CheckPhaseTransitionAsync` → `CheckSeasonEndAsync`.

### 17.8 Chat

Firebase-Pfad: `guild_chat/{guildId}/messages` (Push). `ChatMessage`-Felder: uid, name, text, timestamp.

- Realtime Firebase
- `MaxMessageLength = 200` Zeichen (überlange Texte gekürzt)
- `MaxMessages = 50` (`orderBy="timestamp"&limitToLast=50`)
- `MessageCooldown = 5 Sekunden` (Spam-Schutz, `CanSendMessage`)
- Control-Zeichen entfernt (außer `\n`), **Profanity-Filter (DE/EN/ES/FR/IT/PT)**
- Emoji-Support (TextMeshPro)
- Image-Sharing (Phase 2 — im Original-Code nicht implementiert)

### 17.9 Worker-Auktionen

(Siehe § 5.9)

### 17.10 Achievement-System (Gilde, 33 Einträge)

Firebase-Pfad: `guild_achievements/{guildId}/{achievementId}`. Tiers Bronze (#CD7F32) / Silver (#C0C0C0) / Gold (#FFD700). **GS-Belohnung immer 5 / 25 / 50** (Bronze/Silver/Gold) — KEINE Kosmetik (`CosmeticReward = ""`). Kategorie-Farben: StrongerTogether #4CAF50, WarHeroes #DC2626, DragonSlayers #D97706, Builders #2196F3, Default #888888.

> Doku/Kommentar spricht von „30 Achievements (10 Typen × 3 Tiers)", die tatsächliche `_allDefinitions`-Liste enthält jedoch **33 Einträge (11 Typen × 3 Tiers)**: money, research, members, wars, seasons, league, boss, mvp, speedkill, maxbuilding, hall. Verbindlich ist die folgende vollständige Liste.

| Id | Kategorie | Tier | Icon | Target | GS |
|----|-----------|------|------|-------:|---:|
| guild_ach_money_bronze | StrongerTogether | Bronze | CurrencyEur | 100 000 | 5 |
| guild_ach_money_silver | StrongerTogether | Silver | CurrencyEur | 1 000 000 | 25 |
| guild_ach_money_gold | StrongerTogether | Gold | CurrencyEur | 10 000 000 | 50 |
| guild_ach_research_bronze | StrongerTogether | Bronze | FlaskOutline | 3 | 5 |
| guild_ach_research_silver | StrongerTogether | Silver | FlaskOutline | 9 | 25 |
| guild_ach_research_gold | StrongerTogether | Gold | FlaskOutline | 18 | 50 |
| guild_ach_members_bronze | StrongerTogether | Bronze | AccountGroup | 5 | 5 |
| guild_ach_members_silver | StrongerTogether | Silver | AccountGroup | 10 | 25 |
| guild_ach_members_gold | StrongerTogether | Gold | AccountGroup | 20 | 50 |
| guild_ach_wars_bronze | WarHeroes | Bronze | SwordCross | 3 | 5 |
| guild_ach_wars_silver | WarHeroes | Silver | SwordCross | 10 | 25 |
| guild_ach_wars_gold | WarHeroes | Gold | SwordCross | 50 | 50 |
| guild_ach_seasons_bronze | WarHeroes | Bronze | CalendarStar | 1 | 5 |
| guild_ach_seasons_silver | WarHeroes | Silver | CalendarStar | 4 | 25 |
| guild_ach_seasons_gold | WarHeroes | Gold | CalendarStar | 12 | 50 |
| guild_ach_league_bronze | WarHeroes | Bronze | MedalOutline | 1 (Silver-Liga) | 5 |
| guild_ach_league_silver | WarHeroes | Silver | MedalOutline | 2 (Gold-Liga) | 25 |
| guild_ach_league_gold | WarHeroes | Gold | MedalOutline | 3 (Diamond-Liga) | 50 |
| guild_ach_boss_bronze | DragonSlayers | Bronze | Skull | 3 | 5 |
| guild_ach_boss_silver | DragonSlayers | Silver | Skull | 10 | 25 |
| guild_ach_boss_gold | DragonSlayers | Gold | Skull | 50 | 50 |
| guild_ach_mvp_bronze | DragonSlayers | Bronze | StarShooting | 1 | 5 |
| guild_ach_mvp_silver | DragonSlayers | Silver | StarShooting | 5 | 25 |
| guild_ach_mvp_gold | DragonSlayers | Gold | StarShooting | 20 | 50 |
| guild_ach_speedkill_bronze | DragonSlayers | Bronze | TimerOutline | 1 | 5 |
| guild_ach_speedkill_silver | DragonSlayers | Silver | TimerOutline | 3 | 25 |
| guild_ach_speedkill_gold | DragonSlayers | Gold | TimerOutline | 10 | 50 |
| guild_ach_maxbuilding_bronze | Builders | Bronze | OfficeBuilding | 1 | 5 |
| guild_ach_maxbuilding_silver | Builders | Silver | OfficeBuilding | 5 | 25 |
| guild_ach_maxbuilding_gold | Builders | Gold | OfficeBuilding | 10 | 50 |
| guild_ach_hall_bronze | Builders | Bronze | HomeCity | 3 | 5 |
| guild_ach_hall_silver | Builders | Silver | HomeCity | 6 | 25 |
| guild_ach_hall_gold | Builders | Gold | HomeCity | 10 | 50 |

**Auto-Tracking** (`CheckAllAchievementsAsync`, alle 300s/Offset 250): Nur diese werden automatisch aktualisiert — money_* ← `weeklyProgress`, research_* ← Anzahl `completed`-Forschungen, members_* ← `memberCount`, maxbuilding_* ← Gebäude mit `Level >= MaxLevel`, hall_* ← `hallLevel`. **wars/seasons/league/boss/mvp/speedkill werden hier NICHT aktualisiert** (laut Kommentar „direkt bei den Aktionen" — konkreter Aufruf nicht im Code gefunden).

**Update-Logik:** Fortschritt nur erhöhen; bei `Progress >= Target` → `Completed=true`, GS-Belohnung NUR nach erfolgreichem Firebase-Write; Event `AchievementCompleted` für UI-Celebration.

### 17.11 Gilden-Tipps (GuildTips)

Rein lokal (Preferences), Key-Prefix `guild_tip_seen_`, RESX-Key-Muster `GuildTip_{context}`. API: `GetTipForContext(context)` (null wenn schon gesehen), `MarkTipSeen(context)`, `HasUnseenTip(context)`.

- Klassen-Kommentar nennt **8 Kontexte** als vorgesehene Stubs: `joined, research, war, boss, hall, officer, season_end, chat`.
- **Tatsächlich im Code real implementiert ist nur ein Kontext:** `guild_hub` (einziger RESX-Key `GuildTip_guild_hub`, in allen 6 Sprachen; DE: „Willkommen in deiner Gilde! Hier siehst du das Wochenziel und die Mitglieder.").
- Die 8 genannten Kontext-Keys sind weder als RESX-Einträge noch als Aufruf-Stellen vorhanden (unfertige Stubs). Verbindlich für die Neuentwicklung: nur `guild_hub` ist real.

### 17.12 Firebase-Pfad-Schema (1:1 wie Avalonia)

Identität: **PlayerId** (GUID) ist die stabile Spieler-Identität (überlebt Account-/Geräte-Wechsel). Alle Pfade verwenden PlayerId, nicht Firebase-`Uid`. Migration alt→neu via `MigrateFromUidToPlayerIdAsync`.

| Pfad | Inhalt |
|------|--------|
| `player_guilds/{playerId}` | GuildId-Schnell-Lookup (string) |
| `guilds/{guildId}` | `FirebaseGuildData` |
| `guilds/{guildId}/hallLevel` | int |
| `guilds/{guildId}/leagueId` | string |
| `guild_members/{guildId}/{uid}` | `FirebaseGuildMember` |
| `guild_members/{guildId}/{uid}/role` | string |
| `available_players/{uid}` | `AvailablePlayerInfo` |
| `player_invites/{uid}/{guildId}` | `GuildInvitation` |
| `guild_invite_codes/{guildId}` | string (Code) |
| `invite_code_to_guild/{code}` | string (GuildId) |
| `guild_research/{guildId}/{researchId}` | `GuildResearchState` |
| `guild_hall/{guildId}/buildings/{buildingId}` | `GuildBuildingState` |
| `guild_bosses/{guildId}` | `FirebaseGuildBoss` |
| `guild_boss_damage/{guildId}/{uid}` | `GuildBossDamage` (totalDamage, hits, lastHitAt) |
| `guild_achievements/{guildId}/{achievementId}` | `GuildAchievementState` |
| `guild_chat/{guildId}/messages/{messageId}` | `ChatMessage` (Push) |
| `guild_war_seasons/{seasonId}` | `GuildWarSeasonData` |
| `guild_war_seasons/{seasonId}/leagues/{leagueId}/{guildId}` | `GuildLeagueEntry` |
| `guild_wars/{warId}` | `GuildWar` |
| `guild_war_scores/{warId}/{guildId}/{uid}` | `GuildWarPlayerScore` |
| `guild_war_log/{warId}/{entryId}` | `GuildWarLogEntry` (Push) |
| `guilds/{guildId}/coopOrders/{orderId}` | `CoopOrderState` |
| `guilds/{guildId}/coopOrders/{orderId}/claimedBy/{playerId}` | bool (Write-once-Claim) |
| `guilds/{guildId}/megaProjects/active` | `GuildMegaProject` |
| `auth_to_player/{uid}` | PlayerId (Mapping) |

### 17.13 Facade, Tick-Offsets & HMAC

**Facade** (`IGuildFacade`): bündelt **9** Subsystem-Services (Pass-Through, kein State): Guild, Invite, Research, Chat, WarSeason, Boss, Hall, Tip, Achievement. `GuildCoopOrderService` und `GuildMegaProjectService` sind NICHT Teil des Facades (separat injiziert). Alle Services Singletons; mutierende Services nutzen `SemaphoreSlim` (Timeout 15s, Chat 10s) und implementieren `IDisposable`.

**GuildTick-Offsets** (`GuildTickService.ProcessTick`, 1 Tick = 1 Sekunde, nur aktiv wenn `GuildMembership?.GuildId != null`):

| Check | Intervall (s) | Offset | Aktion |
|-------|--------------:|-------:|--------|
| Boss-Status + Spawn | 60 | 20 | `CheckBossStatusAsync` → `SpawnBossIfNeededAsync` |
| Hall Upgrade-Completion | 60 | 40 | `CheckUpgradeCompletionAsync` |
| Achievements prüfen | 300 | 250 | `CheckAllAchievementsAsync` |
| War-Saison (Phase + Saisonende) | 300 | 260 | `CheckPhaseTransitionAsync` → `CheckSeasonEndAsync` |
| Auktion Refresh + Spawn (Master) | 300 | 90 | `RefreshAuctionAsync` → `SpawnAuctionIfMasterAsync` |
| NPC-Bot-Tick (Auktion) | 5 | 1 | `RunNpcBotTickAsync` |

Alle Aufrufe per `FireAndForget`. Boss-internes Throttle zusätzlich 30s pro guildId.

**HMAC-signierte Felder** (`IGameIntegrityService.ComputeStringHmac`):

| Kontext | Salt | Signierte Felder | NICHT signiert (inkrementell via PATCH) |
|---------|------|------------------|------------------------------------------|
| Co-op-Auftrag | `coop-order-v1` | OrderId, CreatedBy, InvitedPlayer, BaseReward, MiniGameType | Score, Status, ExpiresAt |
| Mega-Projekt | `guild-mega-project-v1` | ProjectId, (int)Type, CreatedAt (`:O`) | Contributions, Donations, CompletedAt |
| GameState (vor Gilden-Writes) | (GameIntegrityService) | gesamter GameState-Signatur-Check | — |

Nur stabile Identitäts-Felder im HMAC; veränderliche Felder per atomarem PATCH (Race-Condition-Fix). Score-Wertebereiche zusätzlich über Firebase-Rules (`validate`) begrenzt.

### 17.14 3D-Visualisierung (Guild-Hub)

- **Eigene 3D-Szene** (Guild.unity)
- 10 Hall-Gebäude als physisch sichtbare Strukturen (Level = Größe + Glow)
- Bosse als animierte 3D-Modelle in eigener Boss-Arena
- Members als Avatare auf Hub-Karte (mit Online-Indicator)
- **Mega-Projekt-Bauphasen** sichtbar als wachsende Cathedral/Skyscraper-Struktur

---

## 18. Achievements (109 Spieler-Achievements, 17 Kategorien)

Quelle: `Models/Achievement.cs` (`Achievements.GetAll()`), `Services/AchievementService.cs`,
`Models/Enums/AchievementCategory.cs`. (Die 33 Gilden-Achievements sind ein separates System — siehe §17.10.)

> **Echte Anzahl: 109 Spieler-Achievements** (`AchievementService.TotalCount = _achievements.Count = 109`, gezaehlt aus `Achievements.GetAll()`).
> Es gibt **kein** eigenes Bronze/Silver/Gold-Tier-System bei den Spieler-Achievements — Belohnungen sind
> pro Achievement individuell (Money + XP + GS, jeweils ≥ 0).

### 18.1 Datenmodell

Felder: Id, TitleKey, TitleFallback, DescriptionKey, DescriptionFallback, Category, Icon, TargetValue
(long, default 1), CurrentValue, MoneyReward (decimal), XpReward (int), GoldenScrewReward (int),
IsUnlocked, UnlockedAt, HasUsedAdBoost.
- `Progress = Min(100, CurrentValue/TargetValue*100)`.
- `IsCloseToUnlock` = nicht unlocked && Progress ≥ 75.

### 18.2 Kategorien (17, Enum `AchievementCategory`)

Orders, Workshops, MiniGames, Money, Time, Special, Workers, Buildings, Research, Reputation, Prestige,
Guilds, Crafting, Tournaments, Collection, Ascension, Rebirth.

### 18.3 Vollständige Liste (alle 109)

Spalten: Id | Kategorie | Titel (Fallback) | Target | Money | XP | GS | Tracking-Quelle (State).
"(kein Tracking)" = im Original-`UpdateProgress`-Switch nicht behandelt → **muss in der Neuentwicklung mit
echtem Tracking verdrahtet werden** (im Original bleibt CurrentValue 0 und das Achievement ist nur über den
Ad-Boost erreichbar).

| Id | Kat. | Titel | Target | Money | XP | GS | Tracking |
|----|------|-------|-------:|------:|---:|---:|----------|
| first_order | Orders | First Steps | 1 | 100 | 10 | 0 | TotalOrdersCompleted |
| orders_10 | Orders | Getting Started | 10 | 500 | 25 | 0 | TotalOrdersCompleted |
| orders_50 | Orders | Reliable Worker | 50 | 2.500 | 150 | 10 | TotalOrdersCompleted |
| orders_100 | Orders | Master Craftsman | 100 | 5.000 | 300 | 25 | TotalOrdersCompleted |
| orders_500 | Orders | Industry Legend | 500 | 25.000 | 750 | 0 | TotalOrdersCompleted |
| perfect_first | MiniGames | Perfection! | 1 | 200 | 15 | 0 | PerfectRatings |
| perfect_10 | MiniGames | Skilled Hands | 10 | 1.000 | 75 | 0 | PerfectRatings |
| perfect_50 | MiniGames | Precision Master | 50 | 5.000 | 250 | 0 | PerfectRatings |
| streak_5 | MiniGames | On Fire! | 5 | 500 | 30 | 0 | BestPerfectStreak |
| streak_10 | MiniGames | Unstoppable | 10 | 2.000 | 150 | 0 | BestPerfectStreak |
| games_100 | MiniGames | Mini-Game Veteran | 100 | 2.500 | 150 | 0 | TotalMiniGamesPlayed |
| workshop_level10 | Workshops | Upgraded | 10 | 1.000 | 50 | 0 | maxWsLevel |
| workshop_level25 | Workshops | Expert Facility | 25 | 5.000 | 150 | 0 | maxWsLevel |
| all_workshops | Workshops | Full House | 6 | 2.500 | 200 | 0 | UnlockedWorkshopTypes.Count |
| worker_first | Workshops | Team Builder | 1 | 100 | 10 | 0 | totalWorkers > 0 |
| workers_10 | Workshops | Growing Team | 10 | 1.000 | 75 | 0 | totalWorkers |
| workers_25 | Workshops | Big Business | 25 | 5.000 | 250 | 15 | totalWorkers |
| workshop_level50 | Workshops | Maximum Power | 50 | 50.000 | 500 | 0 | maxWsLevel |
| workshop_level100 | Workshops | Century Workshop | 100 | 200.000 | 1.000 | 30 | maxWsLevel |
| workshop_level250 | Workshops | Elite Facility | 250 | 1.000.000 | 2.500 | 50 | maxWsLevel |
| workshop_level500 | Workshops | Legendary Workshop | 500 | 5.000.000 | 5.000 | 100 | maxWsLevel |
| workshop_level1000 | Workshops | Transcendent | 1000 | 50.000.000 | 12.500 | 250 | maxWsLevel |
| all_workshops_8 | Workshops | Complete Empire | 8 | 100.000 | 1.000 | 20 | (kein Tracking) |
| events_survived_10 | Workshops | Weathered | 10 | 5.000 | 150 | 0 | (kein Tracking) |
| money_1k | Money | First Thousand | 1.000 | 100 | 10 | 0 | TotalMoneyEarned |
| money_10k | Money | Making Money | 10.000 | 500 | 25 | 0 | TotalMoneyEarned |
| money_100k | Money | Wealthy | 100.000 | 2.500 | 100 | 0 | TotalMoneyEarned |
| money_1m | Money | Millionaire | 1.000.000 | 10.000 | 250 | 15 | TotalMoneyEarned |
| money_10m | Money | Multi-Millionaire | 10.000.000 | 25.000 | 400 | 0 | TotalMoneyEarned |
| money_100m | Money | Mega Rich | 100.000.000 | 50.000 | 600 | 30 | TotalMoneyEarned |
| money_1b | Money | Billionaire | 1.000.000.000 | 100.000 | 1.000 | 0 | TotalMoneyEarned |
| money_10b | Money | Deca-Billionaire | 10.000.000.000 | 1.000.000 | 2.500 | 100 | TotalMoneyEarned |
| play_1h | Time | Dedicated | 3600 | 250 | 20 | 0 | TotalPlayTimeSeconds |
| play_10h | Time | Committed | 36000 | 2.500 | 150 | 0 | TotalPlayTimeSeconds |
| daily_7 | Time | Week Warrior | 7 | 1.000 | 50 | 0 | DailyRewardStreak |
| level_10 | Special | Rising Star | 10 | 2.000 | 0 | 0 | PlayerLevel |
| level_25 | Special | Experienced | 25 | 10.000 | 0 | 5 | PlayerLevel |
| level_50 | Special | Veteran | 50 | 25.000 | 0 | 10 | PlayerLevel |
| level_100 | Special | Centurion | 100 | 100.000 | 0 | 30 | PlayerLevel |
| level_250 | Special | Elite Player | 250 | 500.000 | 0 | 50 | PlayerLevel |
| level_500 | Special | Grandmaster | 500 | 5.000.000 | 0 | 100 | PlayerLevel |
| level_1000 | Special | Immortal | 1000 | 50.000.000 | 0 | 250 | PlayerLevel |
| prestige_1 | Prestige | New Beginning | 1 | 5.000 | 250 | 0 | TotalPrestigeCount |
| worker_a_tier | Workers | Elite Recruitment | 1 | 10.000 | 200 | 0 | (kein Tracking) |
| workers_max_level | Workers | Master Workers | 10 | 25.000 | 400 | 0 | (kein Tracking) |
| worker_loyal | Workers | Loyal Employee | 100 | 15.000 | 300 | 0 | (kein Tracking) |
| worker_specialist | Workers | Perfect Match | 1 | 500 | 15 | 0 | (kein Tracking) |
| workers_total_50 | Workers | HR Manager | 50 | 20.000 | 350 | 0 | (kein Tracking) |
| worker_s_tier | Workers | Legend Found | 1 | 50.000 | 500 | 0 | (kein Tracking) |
| worker_ss_tier | Workers | SS-Tier Recruit | 1 | 200.000 | 1.000 | 30 | hasSS (Worker-Tier ≥ SS) |
| worker_sss_tier | Workers | SSS-Tier Recruit | 1 | 1.000.000 | 2.500 | 50 | hasSSS |
| worker_legendary | Workers | Legendary Recruit | 1 | 10.000.000 | 5.000 | 100 | hasLegendary |
| building_first | Buildings | Developer | 1 | 2.000 | 25 | 0 | builtCount > 0 |
| building_all | Buildings | Real Estate Mogul | 7 | 50.000 | 500 | 0 | builtCount |
| building_max | Buildings | Fully Upgraded | 5 | 20.000 | 250 | 0 | maxBldLevel |
| canteen_built | Buildings | Happy Workers | 1 | 5.000 | 50 | 0 | hasCanteen |
| training_center | Buildings | Academy | 1 | 5.000 | 50 | 0 | hasTraining |
| research_first | Research | Scientist | 1 | 2.000 | 25 | 0 | (kein Tracking) |
| research_branch | Research | Expert | 15 | 100.000 | 1.000 | 0 | (kein Tracking) |
| research_all | Research | Genius | 45 | 500.000 | 3.000 | 0 | (kein Tracking; Desc "all 45" veraltet, real 72 Nodes) |
| research_tools5 | Research | Tool Master | 5 | 10.000 | 100 | 0 | (kein Tracking) |
| research_mgmt5 | Research | Manager | 5 | 10.000 | 100 | 0 | (kein Tracking) |
| reputation_70 | Reputation | Well Known | 70 | 10.000 | 150 | 0 | (kein Tracking) |
| reputation_90 | Reputation | Famous | 90 | 25.000 | 300 | 0 | (kein Tracking) |
| reputation_100 | Reputation | Legendary | 100 | 100.000 | 750 | 0 | (kein Tracking) |
| regular_10 | Reputation | Popular Choice | 10 | 15.000 | 250 | 0 | (kein Tracking) |
| prestige_bronze | Prestige | New Beginning | 1 | 10.000 | 250 | 20 | Prestige.BronzeCount |
| prestige_silver | Prestige | Experienced Master | 1 | 50.000 | 750 | 50 | Prestige.SilverCount |
| prestige_gold | Prestige | Golden Legend | 1 | 200.000 | 1.500 | 100 | Prestige.GoldCount |
| prestige_points_100 | Prestige | Point Collector | 100 | 50.000 | 500 | 0 | TotalPrestigePoints − PrestigePoints |
| prestige_total_5 | Prestige | Veteran Prestige | 5 | 25.000 | 500 | 25 | TotalPrestigeCount |
| prestige_total_10 | Prestige | Prestige Addict | 10 | 100.000 | 1.500 | 50 | TotalPrestigeCount |
| prestige_total_25 | Prestige | Prestige Master | 25 | 500.000 | 3.000 | 100 | TotalPrestigeCount |
| prestige_total_50 | Prestige | Prestige Legend | 50 | 2.000.000 | 7.500 | 250 | TotalPrestigeCount |
| prestige_points_1000 | Prestige | Point Hoarder | 1000 | 1.000.000 | 5.000 | 150 | TotalPrestigePoints − PrestigePoints |
| prestige_platin | Prestige | Platinum Craftsman | 1 | 500.000 | 3.000 | 150 | Prestige.PlatinCount |
| prestige_diamant | Prestige | Diamond Dynasty | 1 | 2.000.000 | 5.000 | 250 | Prestige.DiamantCount |
| prestige_meister | Prestige | Master of Masters | 1 | 10.000.000 | 10.000 | 500 | Prestige.MeisterCount |
| prestige_legende | Prestige | Living Legend | 1 | 50.000.000 | 25.000 | 1.000 | Prestige.LegendeCount |
| perfect_100 | MiniGames | Perfection Master | 100 | 50.000 | 1.000 | 30 | PerfectRatings |
| games_500 | MiniGames | Mini-Game Legend | 500 | 100.000 | 2.000 | 50 | TotalMiniGamesPlayed |
| all_minigames_perfect | MiniGames | Universal Talent | 8 | 200.000 | 3.000 | 75 | PerfectMiniGameTypes.Count |
| all_ws_level100 | Workshops | Full Power | 8 | 5.000.000 | 5.000 | 100 | wsLevel100Count |
| guild_founder | Guilds | Guild Founder | 1 | 10.000 | 250 | 20 | GuildMembership != null |
| guild_member | Guilds | Team Player | 1 | 5.000 | 100 | 0 | GuildMembership != null |
| guild_weekly_goal | Guilds | Team Effort | 1 | 25.000 | 500 | 25 | GuildMembership.GuildLevel > 1 |
| guild_level_10 | Guilds | Legendary Guild | 10 | 100.000 | 1.500 | 50 | GuildMembership.GuildLevel |
| workers_trained_50 | Workers | Training Master | 50 | 50.000 | 750 | 25 | TotalWorkersTrained |
| crafting_100 | Crafting | Mass Producer | 100 | 100.000 | 1.500 | 30 | TotalItemsCrafted |
| all_recipes | Crafting | Recipe Master | 13 | 200.000 | 2.500 | 50 | CompletedRecipeIds.Count (Desc "all 13" — real 30 Rezepte) |
| tournament_gold | Tournaments | Tournament Champion | 1 | 25.000 | 500 | 20 | TotalTournamentsWon > 0 |
| tournaments_10 | Tournaments | Serial Winner | 10 | 100.000 | 2.000 | 50 | TotalTournamentsWon |
| all_mastertools | Collection | Tool Collector | 12 | 500.000 | 3.000 | 75 | CollectedMasterTools.Count |
| equipment_all_rarities | Collection | Rarity Collector | 4 | 100.000 | 1.500 | 30 | distinctRarities (Bitflags Equipment.Rarity) |
| asc_first | Ascension | First Ascension | 1 | 500.000 | 5.000 | 100 | Ascension.AscensionLevel |
| asc_5 | Ascension | Master Ascender | 5 | 5.000.000 | 12.500 | 250 | AscensionLevel |
| asc_10 | Ascension | Transcendence | 10 | 50.000.000 | 25.000 | 500 | AscensionLevel |
| asc_perk_first | Ascension | Perk Enthusiast | 1 | 100.000 | 2.500 | 50 | Ascension.Perks.Count > 0 |
| asc_perks_max | Ascension | Fully Upgraded | 6 | 100.000.000 | 50.000 | 1.000 | CountMaxedPerks |
| rebirth_first | Rebirth | First Star | 1 | 1.000.000 | 5.000 | 100 | totalRebirthStars > 0 |
| rebirth_stars_10 | Rebirth | Star Collector | 10 | 10.000.000 | 15.000 | 300 | totalRebirthStars |
| rebirth_ws_5stars | Rebirth | Perfection | 5 | 25.000.000 | 20.000 | 500 | maxRebirthStars (höchste WS-Sterne) |
| rebirth_all_ws | Rebirth | Galaxy | 8 | 50.000.000 | 25.000 | 750 | wsWithAtLeast1Star |
| all_ws_level1000 | Workshops | On the Summit | 8 | 100.000.000 | 50.000 | 1.000 | wsLevel1000Count |
| auto_craft_first | Crafting | First Prototype | 1 | 1.000 | 500 | 5 | TotalItemsAutoProduced |
| auto_craft_100 | Crafting | Serial Production | 100 | 25.000 | 2.500 | 25 | TotalItemsAutoProduced |
| auto_craft_1000 | Crafting | Mass Production | 1000 | 250.000 | 10.000 | 100 | TotalItemsAutoProduced |
| material_order_10 | Orders | Material Master | 10 | 50.000 | 5.000 | 50 | TotalMaterialOrdersCompleted |
| craft_tier3_first | Crafting | Masterpiece | 1 | 100.000 | 5.000 | 50 | CountTier3Items (Inventar/abgeschl. T3-Rezept) |

(Summe = 109 Achievements, lückenlos.)

### 18.4 Achievement-Mechaniken (Service)

Quelle: `Services/AchievementService.cs`.

- **GetAll()** im Ctor geladen, IsUnlocked aus `State.UnlockedAchievements` rehydriert.
- **CheckAchievements()**: ruft UpdateProgress, dann für jedes nicht-unlocked `CurrentValue >= TargetValue`
  → UnlockAchievement.
- **UnlockAchievement()**: IsUnlocked=true, UnlockedAt=UtcNow, CurrentValue=TargetValue, in
  `State.UnlockedAchievements` schreiben. Belohnungen: `AddMoney(MoneyReward)`, `AddXp(XpReward)`,
  `AddGoldenScrews(GoldenScrewReward)`. Feuert `AchievementUnlocked` + Analytics-Event (id, category,
  xp_reward, screw_reward).
- **BoostAchievement(id, boostPercent)** (Rewarded-Ad, Placement `achievement_boost` = +20%, nur bei
  TargetValue > 5): nur wenn nicht unlocked && nicht HasUsedAdBoost. `boost = (long)(TargetValue *
  boostPercent)` (min 1). CurrentValue += boost, HasUsedAdBoost=true; bei ≥ Target → Unlock.
- **Auto-Tracking Event-Subscriptions (8 Game-Events)**: OrderCompleted, LevelUp, WorkerHired,
  WorkshopUpgraded → CheckAchievements. MoneyChanged → debounced (nur alle 30 s bei Zuwachs,
  `MoneyCheckIntervalSeconds = 30`). PrestigeCompleted / AscensionCompleted / RebirthCompleted →
  CheckAchievements.
- **CountTier3Items**: summiert CraftingInventory aller T3-Produkte; falls 0 aber CompletedRecipeIds
  enthält ein T3-Rezept → 1. **CountMaxedPerks**: Ascension-Perks mit Level ≥ MaxLevel.
- **GetAllAchievements()**: sortiert IsUnlocked desc, Progress desc, Category asc.
  **GetUnlockedAchievements()**: UnlockedAt desc. Reset bei StateLoaded.

### 18.5 3D-Trophäen-Cinematic

Statt 2D-Dialog:
- 3D-Trophäe spawnt am Bildschirmrand
- Rotiert 3× mit Glow
- Floating-Text mit Achievement-Name + Reward
- Sound + Haptic-Feedback
- Meister-Hans-Voice-Stinger (random aus Pool)
- 5s sichtbar, Smooth-Fade

---

## 19. Daily-Reward (30-Tage-Zyklus)

Quelle: `DailyRewardService` / `DailyReward` (Avalonia-Original). **30-Tage-Zyklus** mit Streak,
**KEINE** Premium-Verdopplung des Daily-Rewards. Werte 1:1 aus `DailyReward.s_cachedSchedule`.

### 19.1 Mechanik (Streak + Skalierung)

- **30-Tage-Zyklus** mit Streak: `CurrentDay = ((Streak - 1) % 30) + 1` (bei Streak 0 → Tag 1).
- **Verfügbarkeit (`IsRewardAvailable`):** true, wenn nie geclaimt ODER `UtcNow.Date > LastDailyRewardClaim.Date`.
  Zeitmanipulations-Schutz: LastClaim in der Zukunft → false.
- **Streak gebrochen**, wenn `daysSinceLastClaim > 1` (auch bei negativ): `StreakBeforeBreak = aktueller Streak`,
  `StreakRescueUsed = false`, `Streak = 1`. Sonst `Streak++`.
- **Claim atomar** unter State-Lock (Doppel-Tap-Schutz), danach `LastDailyRewardClaim = UtcNow`.
- **Skalierte Geld-Belohnung (`GetScaledMoney`):** Festbeträge der Tabelle werden bei laufendem Einkommen
  hochskaliert. Wenn `netIncomePerSecond <= 0` → Festbetrag. Sonst:
  `minutesWorth = sqrt(Day) × netIncomePerSecond × 60`, gecapt bei `netIncomePerSecond × 900` (15 Min Wert);
  Ergebnis = `max(Festbetrag, minutesWorth)`.
- **KEINE Premium-Verdopplung** im Daily-Reward selbst (DailyRewardService ruft AddMoney/AddXp/AddGoldenScrews
  ohne ×2-Multiplikator auf). Die allgemeine Premium-GS-Verdopplung (+100%) greift in `AddGoldenScrews`
  (Gameplay-Quelle). Die separate **"DoubleDailyReward"-Verdopplung** ist ein Seasonal-Effekt (Winter-Shop-Item),
  NICHT Premium.

### 19.2 Belohnungs-Tabelle (alle 30 Tage)

Quelle: `DailyReward.s_cachedSchedule` (Festbeträge; Money wird per `GetScaledMoney` ggf. hochskaliert).

| Tag | Geld (€) | XP | GS | Bonus |
|-----|----------|----|----|-------|
| 1 | 500 | 0 | 0 | — |
| 2 | 750 | 0 | 1 | — |
| 3 | 1.000 | 25 | 0 | — |
| 4 | 1.500 | 0 | 2 | — |
| 5 | 2.000 | 50 | 0 | — |
| 6 | 2.500 | 0 | 3 | — |
| 7 | 5.000 | 100 | 5 | SpeedBoost |
| 8 | 3.000 | 50 | 0 | — |
| 9 | 3.500 | 0 | 3 | — |
| 10 | 4.000 | 75 | 0 | — |
| 11 | 5.000 | 0 | 4 | — |
| 12 | 6.000 | 100 | 0 | — |
| 13 | 7.000 | 0 | 5 | — |
| 14 | 10.000 | 200 | 8 | XpBoost |
| 15 | 8.000 | 100 | 0 | — |
| 16 | 9.000 | 0 | 5 | — |
| 17 | 10.000 | 150 | 0 | — |
| 18 | 12.000 | 0 | 6 | — |
| 19 | 15.000 | 200 | 0 | — |
| 20 | 18.000 | 0 | 8 | — |
| 21 | 25.000 | 300 | 10 | SpeedBoost |
| 22 | 15.000 | 150 | 0 | — |
| 23 | 18.000 | 0 | 8 | — |
| 24 | 20.000 | 200 | 0 | — |
| 25 | 25.000 | 0 | 10 | — |
| 26 | 30.000 | 300 | 0 | — |
| 27 | 35.000 | 0 | 12 | — |
| 28 | 40.000 | 400 | 15 | XpBoost |
| 29 | 50.000 | 500 | 15 | — |
| 30 | 100.000 | 1.000 | 25 | SpeedBoost |

### 19.3 Bonus-Typen (`DailyBonusType`)

Beim Claim werden zeitlich begrenzte Boosts gesetzt (stacken nicht, feste 1h-Dauer):

| Bonus | Effekt | Anwendung beim Claim |
|-------|--------|----------------------|
| SpeedBoost | 2× Income für 1h | `SpeedBoostEndTime = now + 1h` |
| XpBoost | +50% XP für 1h | `XpBoostEndTime = now + 1h` |
| FreeWorker | (im Schedule nicht verwendet) | — |
| None | kein Bonus | — |

### 19.4 VIP Auto-Claim

`VipTier.HasAutoClaimDailyRewards` (Bronze+) erlaubt automatischen Daily-Claim 1×/Tag (Flag im VipTier-Modell).
Auto-Claim ist NICHT an den Imperium-Pass/Premium gebunden, sondern an die ausgaben-basierte VIP-Stufe (§9 in
ORIGINAL_WERTE; HWI-Unity-VIP siehe Monetarisierungs-Abschnitt).

### 19.5 3D-/UX-Präsentation (Unity-spezifisch)

- **Daily-Reward-Kalender als 3D-Karten-Wand:** 30 physische Belohnungs-Karten in einer 5×6-Anordnung, der
  aktuelle Tag glüht (Rim-Light-Shader), bereits geclaimte Karten liegen umgedreht.
- **Claim-Cinematic:** Karte dreht sich, springt heraus, platzt in Münz-/Goldschrauben-Partikel (GPU-Particles),
  Kamera-Punch + Impulse-Shake bei Jackpot-Tagen (7/14/21/28/30 mit Boost).
- **Streak-Flammen-Effekt:** sichtbare Streak-Zahl mit aufsteigender Flammen-Intensität, die bei Bruch erlischt
  (Shader-Auflösen statt Hard-Cut). StreakRescue als optionaler Ad-Prompt visualisiert.

---

## 20. Lucky-Spin (8 Slots)

Quelle: `LuckySpinService` / `LuckySpin` (Avalonia-Original). Acht feste Slot-Typen mit gewichteter
Auswahl; keine Niete. Geld-Belohnungen skalieren mit dem Einkommen, GS/XP/Boost sind fix.

### 20.1 Die 8 Slots: Gewichte & Belohnungen

`baseMoney = max(1000, incomePerSecond × 300)`, `incomePerSecond = max(1, NetIncomePerSecond)`.
Gesamtgewicht = 30+20+10+15+12+8+4+2 = **101**. Auswahl: gewichteter Roll `Random.Next(TotalWeight)`
über kumulative Summe.

| Slot (Enum) | Gewicht | Wahrscheinlichkeit | Belohnung |
|-------------|---------|--------------------|-----------|
| MoneySmall | 30 | ≈ 29,7% | Geld = `baseMoney × 0.5` |
| MoneyMedium | 20 | ≈ 19,8% | Geld = `baseMoney` |
| MoneyLarge | 10 | ≈ 9,9% | Geld = `baseMoney × 2` |
| XpBoost | 15 | ≈ 14,9% | 500 XP |
| GoldenScrews5 | 12 | ≈ 11,9% | 5 GS |
| SpeedBoost | 8 | ≈ 7,9% | 2× Geschwindigkeit 30 Min (`SpeedBoostEndTime` stackt: `max(bestehend, now) + 30min`) |
| ToolUpgrade | 4 | ≈ 4,0% | (kein Money/GS/XP/Boost in `CalculateReward` — Effekt 0; ToolUpgrade-Logik außerhalb der Spin-Files) |
| Jackpot50 | 2 | ≈ 2,0% | 50 GS |

> **RemoteConfig-Hinweis:** Die konkreten Slot-Werte/Gewichte werden im Original aus der Berechnung in
> `LuckySpinService.PrizeWeights` + `LuckySpinPrize.CalculateReward` gespeist (kein freier RemoteConfig-JSON).
> In Unity bleiben Gewichte + Reward-Formeln über `BalancingConfig` / Firebase Remote Config tunebar, aber die
> **Default-Werte sind exakt obige Tabelle** — keine erfundenen Beträge.

### 20.2 Spin-Verfügbarkeit (Priorität)

1. **Regulärer Gratis-Spin:** 1× pro Tag.
2. **Premium-Bonus-Gratis-Spin:** zweiter Gratis-Spin/Tag, nur mit Imperium-Pass/Premium.
3. **Ad-Spin:** `SpinForAd()` (kostenlos), danach `MarkAdSpinUsed()`. Verfügbar (`HasAdSpin`), wenn der
   reguläre Gratis-Spin verbraucht ist UND heute noch kein Ad-Spin genutzt wurde → max. 1 Ad-Spin/Tag.
4. **Bezahl-Spin:** 5 GS (`FlatSpinCost` / `SpinCost`), `PaidSpinsToday++`.

Tages-Reset über UTC-Datum je Kanal (`LastFreeSpinDate`, `LastBonusFreeSpinDate`, `LastAdSpinDate`,
`LastPaidSpinDate`); Zeitmanipulation in die Zukunft → selbstbestrafend false. `TotalSpins++` pro Spin.

### 20.3 3D-/UX-Präsentation (Unity-spezifisch)

- **Physisches 3D-Glücksrad** (8 Segmente), per Cinemachine-Orbit zentriert, mit Speed-Lines, Motion-Blur und
  ratterndem Zeiger (Haptik-Tick pro Segment-Übergang).
- **Slow-Motion-Stopp** kurz vor dem Halt für Spannung, anschließend Reward-Burst (GPU-Particles in
  Slot-Farbe), Jackpot50 mit Gold-Konfetti + Kamera-Shake.
- **Spin-Kanal-UI:** Drei klar getrennte Buttons (Gratis / Ad / 5 GS) mit Cooldown-Ring; Premium-Bonus-Spin
  als zweite Gratis-Münze sichtbar.

---

## 21. BattlePass (50 Tier, 30-Tage-Saison)

Quelle: `BattlePass` (Daten/Formeln) + `BattlePassService` (Logik/XP-Quellen/IAP). Alle Formeln, Schwellen
und Reward-Werte 1:1 aus dem Avalonia-Original.

### 21.1 Saison-Eigenschaften

- **Max-Tier (`MaxTier`):** 50 (const); beide Tracks generieren 50 Tiers (Index 0–49).
- **Saison-Dauer (`SeasonDurationDays`):** 30 Tage (const). Start-`SeasonNumber` = 1.
- **`DaysRemaining`** = `max(0, 30 - floor((UtcNow - SeasonStartDate).TotalDays))`.
- **`IsSeasonExpired`** = `(UtcNow - SeasonStartDate).TotalDays > 30`.
- **Premium-IAP-SKU:** `"battle_pass_season"` (Consumable). Premium-Preis kommt aus dem Store/IAP, nicht aus
  dem Code (nicht hartkodiert).
- **Saison-Theme zyklisch über `SeasonNumber % 4`** (s. 21.7).

### 21.2 XP-Schwelle pro Tier (`XpForNextTier`)

- `baseXp = 250 × (CurrentTier + 1)`.
- Wenn `CurrentTier >= 40`: `XpForNextTier = baseXp × 2` (Endgame, Tiers 41–50 doppelte Anforderung).
- Sonst: `XpForNextTier = baseXp`.

Beispiele: Tier 0→1 = 250, Tier 1→2 = 500, Tier 9→10 = 2500, Tier 39→40 = 10000,
Tier 40→41 = `250×41×2` = 20500. Bei `CurrentTier >= MaxTier` wird `CurrentXp = 0` gecapt.
`AddXp(amount)`: addiert XP, steigt Tier solange `CurrentTier < 50 && CurrentXp >= XpForNextTier`
(Überschuss-XP überträgt), gibt Anzahl Tier-Ups zurück.

### 21.3 XP-Quellen (automatische Vergabe)

XP wird nur vergeben, wenn `!IsSeasonExpired`.

| Aktion | BP-XP | Event/Handler |
|--------|-------|---------------|
| Auftrag abgeschlossen | +100 | OrderCompleted |
| MiniGame-Ergebnis | +50 | MiniGameResultRecorded |
| Workshop-Upgrade | +25 | WorkshopUpgraded |
| Arbeiter eingestellt | +30 | WorkerHired |
| Worker-Level-Up (Training) | +20 | WorkerLevelUp |
| Crafting-Produkt eingesammelt | +40 | CraftingProductCollected |

### 21.4 Free-Track-Belohnungen (50 Tiers, Index 0–49)

`baseMoney = max(500, baseIncome × 60)`, wobei `baseIncome = BaseIncomeAtSeasonStart` (beim Saisonstart
fixiert).

**Tiers 0–29 (Basis):**
- MoneyReward = `baseMoney × (1 + i × 0.5)`
- XpReward (Spiel-XP) = `50 + i × 25`
- GoldenScrewReward = `3`, wenn `(i+1) % 5 == 0` (Index 4, 9, 14, 19, 24, 29), sonst `0`
- DescriptionKey = `BPFree_{i}`

**Tiers 30–48 (verbessert):**
- `moneyMult = 0.75` bei ungeradem i, sonst `0.6`
- MoneyReward = `baseMoney × (1 + i × moneyMult)`
- XpReward = `100 + i × 30`
- GoldenScrewReward: i==34 (Tier 35) → 15, i==39 (Tier 40) → 20, i==44 (Tier 45) → 25, sonst 3 bei geradem i, sonst 0
- DescriptionKey = `BPFree_{i}`

**Tier 49 (Free-Capstone):**
- MoneyReward = `baseMoney × (1 + 49 × 0.75)`
- XpReward = `200 + 49 × 30` = 1670
- GoldenScrewReward = **50**
- DescriptionKey = `BPFreeCapstone`

### 21.5 Premium-Track-Belohnungen (50 Tiers, Index 0–49)

`baseMoney = max(1500, baseIncome × 180)`. Premium-Spread bewusst ~3× Free.

**Tiers 0–29 (Basis):**
- MoneyReward = `baseMoney × (1 + i × 0.75)`
- XpReward = `100 + i × 50`
- GoldenScrewReward = `12`, wenn `(i+1) % 3 == 0`, sonst `3`
- DescriptionKey = `BPPremium_{i}`

**Tiers 30–48 (Sonderfälle haben Vorrang vor dem regulären Zweig):**

| Tier-Index | RewardType | MoneyReward | XpReward | GS | SpeedBoostMin | DescriptionKey |
|------------|-----------|-------------|----------|----|----|----------------|
| 34 (Tier 35) | SpeedBoost | `baseMoney×(1+34×0.85)` | `150+34×55`=2020 | 5 | 120 | BPPremiumSpeedBoost2h |
| 39 (Tier 40) | Standard | `baseMoney×(1+39×0.85)` | `150+39×55`=2295 | 50 | — | BPPremiumMilestone40 |
| 44 (Tier 45) | SpeedBoost | `baseMoney×(1+44×0.85)` | `150+44×55`=2570 | 10 | 240 | BPPremiumSpeedBoost4h |
| regulär (30–48 ohne 34/39/44) | Standard | `baseMoney×(1+i×0.85)` | `150+i×55` | `12` wenn `(i+1)%3==0` sonst `3` | — | BPPremium_{i} |

**Tier 49 (Premium-Capstone):**
- MoneyReward = `baseMoney × (1 + 49 × 1.0)`
- XpReward = `200 + 49 × 60` = 3140
- GoldenScrewReward = **150**
- DescriptionKey = `BPCapstone{season}` (season aus `SeasonNumber % 4`, s. 21.7)

### 21.6 Reward-Anwendung & Claim-Regeln

- MoneyReward > 0 → `AddMoney`; XpReward > 0 → `AddXp` (Spiel-XP); GoldenScrewReward > 0 → `AddGoldenScrews`.
- RewardType == SpeedBoost && SpeedBoostMinutes > 0 → `SpeedBoostEndTime` stackt:
  `currentEnd = max(SpeedBoostEndTime, now); SpeedBoostEndTime = currentEnd + SpeedBoostMinutes`.
- **Claim-Regeln:** Tier muss erreicht sein (`tier <= CurrentTier`). Premium-Claim nur wenn `IsPremium == true`;
  je Tier nur einmal (`ClaimedPremiumTiers` / `ClaimedFreeTiers`).

### 21.7 Saison-Theme (zyklisch über `SeasonNumber % 4`)

| SeasonNumber % 4 | Theme | ThemeColor | ThemeIcon | Capstone-Key |
|------------------|-------|-----------|-----------|--------------|
| 0 | Spring | #4CAF50 | Flower | BPCapstoneSpring |
| 1 | Summer | #FF9800 | WhiteBalanceSunny | BPCapstoneSummer |
| 2 | Autumn | #795548 | Forest | BPCapstoneAutumn |
| 3 | Winter | #2196F3 | Snowflake | BPCapstoneWinter |

### 21.8 Premium-Kauf-Lock-in & Saison-Reset

- Premium-Kauf **blockiert**, wenn `DaysRemaining <= 3` (Lock-in-Schutz ab Tag 27). UI-Flag
  `IsPremiumLockedDueToSeasonEnd`.
- Kauf erfordert erfolgreichen `PurchaseConsumableAsync("battle_pass_season")`.
- `CheckNewSeason()` (bei `IsSeasonExpired`): `SeasonNumber++`, `CurrentTier=0`, `CurrentXp=0`,
  `ClaimedFreeTiers.Clear()`, `ClaimedPremiumTiers.Clear()`, `IsPremium=false` (Premium pro Saison neu kaufen),
  `SeasonStartDate=UtcNow`, `BaseIncomeAtSeasonStart=TotalIncomePerSecond`.
- BP-Check-Tick: vom GameLoop getrieben (Tick-Offset, siehe Core-Loop-Abschnitt).

> **Hinweis zur DESIGN-Vorgabe:** Der frühere Eintrag "Premium 9,99 €" war eine Erfindung — der Preis kommt
> aus dem Store-IAP (`battle_pass_season`), nicht aus dem Code. Im Mega-Bundle ist der BattlePass NICHT
> enthalten (das Mega-Bundle enthält ausschließlich GS/Boost/Geld/Premium, s. §29).

### 21.9 3D-/UX-Präsentation (Unity-spezifisch)

- **Vertikaler 3D-Tier-Pfad** durch die Werkstatt-Stadt: 50 Reward-Podeste, der erreichte Tier glüht, Free-
  und Premium-Spur laufen parallel (Premium-Spur initial vergittert/gesperrt visualisiert).
- **Tier-Up-Cinematic:** Kamera fährt zum nächsten Podest, Reward materialisiert mit Partikel-Burst;
  Milestone-Tiers (35/40/45) und Capstone (49) mit eigener, stärkerer Sequenz.
- **Saison-Theming:** Stadt-Beleuchtung, Skybox und Partikel wechseln je `SeasonNumber % 4`
  (Spring/Summer/Autumn/Winter) passend zu ThemeColor/ThemeIcon.

---

## 22. Live-Events & Random Events

Zwei getrennte Systeme aus dem Avalonia-Original: (A) **Live Events** (RemoteConfig-getriebene Limited-Time-
Wettbewerbe mit Score-Tracker, genau **4 Templates**) und (B) **Random Events** (8 zufällige Wirtschafts-
Ereignisse + saisonale Multiplikatoren). Beide hier 1:1 dokumentiert.

### 22.A Live Events (Limited-Time, 4 Templates)

Quelle: `LiveEventService`, `LiveEventScoreTracker`, `LiveEvent`.

#### 22.A.1 Allgemein (RemoteConfig-getrieben)

- RemoteConfig-Schlüssel: `live_event.id` (stabile ID; leer → kein Event), `live_event.template`
  (Default `"DoubleReward"`), `live_event.starts_at` (ISO-8601), `live_event.ends_at` (ISO-8601).
- Event-Dauer im Modell-Kommentar als 7 Tage beschrieben, faktisch durch `starts_at`/`ends_at` definiert.
- `IsActive`: parse `EndsAtIso` (InvariantCulture, RoundtripKind) → `UtcNow < endsAt`.
- Persistenz in `GameState.LiveEvent` (Score nur übernommen, wenn `persisted.Id == id`).
- `Tick()` (vom GameLoop): bei abgelaufenem Event → EventEnded, `CurrentEvent=null`, `GameState.LiveEvent=null`.
- `AddScore(points)`: nur wenn Event aktiv und `points > 0`; `PlayerScore += points`.

#### 22.A.2 Die 4 Templates (`LiveEventTemplate` Enum)

| Template | Beschreibung | Score-Verdrahtung |
|----------|--------------|-------------------|
| DoubleReward | Doppelte Auftrags-Belohnungen | +1 Punkt pro abgeschlossenem Auftrag (jeder Typ), via OrderCompleted |
| BossRush | Boss-Rush mit Spawn-Boost | aktuell NICHT verdrahtet (benötigt GuildBoss-Damage-Event-Hook) |
| CoopMarathon | Doppelte Co-op-Auftrags-Belohnungen | +1 Punkt pro Cooperation-Auftrag (`OrderType.Cooperation`), via OrderCompleted |
| MiniGameMastery | Perfekte Ratings geben Bonus-GS | +1 Punkt pro Perfect-Rating, via PerfectRatingIncremented |

Hinweis: Die Reward-Effekte (z.B. "doppelte Belohnung") sind als Template-Namen/Doku beschrieben; die
tatsächliche Belohnung ist die GS-Reward-Tier-Ausschüttung (22.A.3), kein laufender Multiplikator.

#### 22.A.3 Reward-Tiers

| Tier-Index | Score-Schwelle | GS-Belohnung |
|------------|----------------|--------------|
| 0 | 100 | 25 |
| 1 | 500 | 75 |
| 2 | 2000 | 200 |

`TryClaimNextReward()` prüft Tiers aufsteigend, zahlt den ersten erfüllten, noch nicht geclaimten Tier
(`ClaimedRewardTiers`). Analytics: `live_event_started`, `live_event_tier_claimed`, `live_event_ended`.

### 22.B Random Events (8 Random-Typen + saisonal)

Quelle: `EventService`, `GameEvent`, `GameEventEffect`, `GameEventType`.

#### 22.B.1 Intervall-/Chance-Skalierung nach Prestige

`prestigeCount = Prestige.TotalPrestigeCount`. Nur wenn kein aktives Event und
`hoursSinceLastCheck >= minHours`; dann `LastEventCheck = UtcNow`; `Random.NextDouble() > chance` → kein Event.
EventHistory hält max 20 Einträge.

| PrestigeCount | minHours | Chance |
|---------------|----------|--------|
| 0 (kein Prestige) | 8,0 | 30% |
| 1 (Bronze) | 6,0 | 35% |
| 2 (Silver) | 4,0 | 40% |
| ≥3 (Gold+) | 3,0 | 50% |

#### 22.B.2 Pity-Timer

Bei `ConsecutiveNegativeEvents >= 2` werden nur positive Typen zur Auswahl zugelassen. Nach negativem Event
`ConsecutiveNegativeEvents++`, nach positivem → 0.

#### 22.B.3 Die 8 Random-Event-Typen

`IsPositive`: MaterialSale, HighDemand, InnovationFair, CelebrityEndorsement = positiv;
MaterialShortage, EconomicDownturn, TaxAudit, WorkerStrike = negativ.

| Typ | Positiv? | Dauer | Effekt (Default) | Icon |
|-----|----------|-------|------------------|------|
| MaterialSale (0) | ja | 6h | CostMultiplier 0.7 (−30% Kosten) | Star |
| MaterialShortage (1) | nein | 4h | CostMultiplier 1.5 (+50% Kosten), AffectedWorkshop = zufällig | AlertCircle |
| HighDemand (2) | ja | 8h | RewardMultiplier 1.5 (+50% Belohnung), AffectedWorkshop = zufällig | CashMultiple |
| EconomicDownturn (3) | nein | 6h | RewardMultiplier 0.7, ReputationChange +2 | TrendingDown |
| TaxAudit (4) | nein | 1h | SpecialEffect "tax_10_percent" (10% Steuer auf Brutto) | Finance |
| WorkerStrike (5) | nein | 2h | SpecialEffect "mood_drop_all_20", MarketRestriction = Tier C | AccountOff |
| InnovationFair (6) | ja | 4h | IncomeMultiplier 1.3 (+30%) | LightbulbOn |
| CelebrityEndorsement (7) | ja | 8h | IncomeMultiplier 1.2, ReputationChange +5 | StarCircle |

HighDemand/MaterialShortage rollen `AffectedWorkshop` aus allen WorkshopTypes. WorkerStrike setzt zusätzlich
`MarketRestriction = WorkerTier.C`.

#### 22.B.4 Saisonale Event-Typen (zeitbasiert, NICHT im Random-Pool, 24h)

| Typ | Effekt | Icon |
|-----|--------|------|
| SpringSeason (10) | IncomeMultiplier 1.15 | Flower |
| SummerBoom (11) | RewardMultiplier 1.2 | WhiteBalanceSunny |
| AutumnSurge (12) | IncomeMultiplier 1.1, RewardMultiplier 1.1 | Forest |
| WinterSlowdown (13) | IncomeMultiplier 0.9 | Snowflake |

#### 22.B.5 Saisonaler Monats-Multiplikator (immer aktiv, multipliziert Income)

`EventService.GetSeasonalMultiplier(month)` wird in `GetCurrentEffects` mit dem Event-IncomeMultiplier
multipliziert; Cache invalidiert bei Monatswechsel/StateLoaded.

| Monate | Multiplikator |
|--------|---------------|
| 3,4,5 (Frühling) | 1.15 (+15%) |
| 6,7,8 (Sommer) | 1.20 (+20%) |
| 9,10,11 (Herbst) | 1.10 (+10%) |
| 12,1,2 (Winter) | 0.90 (−10%) |

`GameEventEffect`-Felder (Defaults 1.0 bei Multiplikatoren): IncomeMultiplier, CostMultiplier,
RewardMultiplier, ReputationChange (0), MarketRestriction (WorkerTier?), AffectedWorkshop (WorkshopType?),
SpecialEffect (string?).

### 22.C 3D-/UX-Präsentation (Unity-spezifisch)

- **Live-Event-Banner als animiertes 3D-Schaufenster** über der Stadt mit Countdown-Hologramm und einem
  Score-Fortschrittsbalken zu den drei Reward-Tiers (100 / 500 / 2000).
- **Random-Events als Stadt-Ereignisse:** positive Events lösen warme Beleuchtung + aufsteigende Goldpartikel
  aus, negative Events legen einen Grau-Filter (Post-Processing) über die betroffene Werkstatt; TaxAudit/
  WorkerStrike mit eigenen Icon-Pop-ups und Haptik-Feedback.
- **Saison-Multiplikator** dauerhaft als dezentes Skybox-/Lichtthema sichtbar (kein UI-Overlay nötig).

---

## 23. Saisonale Events (4/Jahr) & Event-Shop

Quelle: `SeasonalEventService`, `SeasonalEvent` (Avalonia-Original). 4 zeitlich begrenzte Saison-Events pro
Jahr, jedes mit Seasonal-Points-Währung (SP) und einem Event-Shop aus **4 Basis-Items + 2 Saison-einzigartigen
Items** (= 6 Items pro Saison). **Es gibt KEINE Cosmetic-/Skin-Items** im Seasonal-Shop —
`SeasonColor`/`SeasonIcon` dienen ausschließlich dem UI-Theming.

### 23.1 Zeitfenster & Lifecycle

| Saison | Zeitfenster (UTC) | SeasonColor | SeasonIcon |
|--------|-------------------|-------------|------------|
| Spring | 1.–14. März | #4CAF50 | Flower |
| Summer | 1.–14. Juni | #FF9800 | WhiteBalanceSunny |
| Autumn | 1.–14. September | #795548 | Leaf |
| Winter | 1.–14. Dezember | #2196F3 | Snowflake |

- Event-Start: `StartDate = {Jahr}-{Monat}-01 00:00:00 UTC`, `EndDate = {Jahr}-{Monat}-14 23:59:59 UTC`.
- Start nur, wenn im Fenster und kein aktives Event. Außerhalb des Fensters → Event auf null.
- Zeitmanipulations-Schutz: `StartDate` in der Zukunft → Event verworfen.
- `IsActive` (Modell): `UtcNow >= StartDate && UtcNow <= EndDate`.

### 23.2 SP-Verdienst (Seasonal Points)

Nur wenn Event aktiv. `AddSeasonalCurrency` erhöht `Currency` und `TotalPoints`.

| Quelle | SP | Bedingung |
|--------|----|-----------|
| Auftrag Basis (`BaseSpPerOrder`) | 5 | jeder Auftrag |
| Auftrag-Bonus Good (`GoodBonusSp`) | +3 | AverageRating == Good |
| Auftrag-Bonus Perfect (`PerfectBonusSp`) | +5 | AverageRating == Perfect |
| MiniGame (`SpPerMiniGame`) | 2 | pro MiniGame-Ergebnis (nicht Perfect) |
| MiniGame Perfect (`SpPerPerfectMiniGame`) | 4 | Rating == Perfect |
| Worker-Level-Up (`SpPerWorkerLevelUp`) | 1 | pro Training-Level-Up |
| Crafting eingesammelt (`SpPerCraftingCollected`) | 3 | pro Crafting-Produkt |

### 23.3 Event-Shop — Basis-Items (4 in JEDER Saison)

Icon je Saison: Spring=Flower, Summer=WhiteBalanceSunny, Autumn=Leaf, Winter=Snowflake. Item-IDs als
`{prefix}_...` (prefix = Saison lowercase, z.B. `spring_income_boost`). Kauf einmalig pro Item
(`PurchasedItems`), Kosten in SP.

| Item-Suffix | NameKey | Kosten (SP) | Effekt |
|-------------|---------|-------------|--------|
| income_boost | Seasonal{Season}IncomeBoost | 50 | IncomeBonus = 0.10 (+10% passiv, GameLoop) |
| xp_pack | Seasonal{Season}XpPack | 30 | XpBonus = 500 (sofort) |
| screw_bundle | Seasonal{Season}ScrewBundle | 75 | GoldenScrews = 15 (sofort) |
| speed_boost | Seasonal{Season}SpeedBoost | 100 | SpeedBoostMinutes = 120 (sofort, stackt) |

### 23.4 Event-Shop — Saison-einzigartige Items (2 pro Saison)

| Saison | Item-ID | NameKey | Kosten (SP) | Effekt |
|--------|---------|---------|-------------|--------|
| Spring | spring_extra_worker | SeasonalSpringExtraWorker | 150 | ExtraWorkerDays=1, EffectDurationDays=14 (+1 Max-Worker 14 Tage) |
| Spring | spring_research_speed | SeasonalSpringResearchSpeed | 80 | ResearchSpeedBonusPercent=30, EffectDurationDays=14 |
| Summer | summer_double_prestige | SeasonalSummerDoublePrestige | 200 | DoubleNextPrestige=true (2× PP nächster Prestige) |
| Summer | summer_offline_boost | SeasonalSummerOfflineBoost | 120 | OfflineEarningsBonusPercent=50, EffectDurationDays=14 |
| Autumn | autumn_instant_screws | SeasonalAutumnInstantScrews | 250 | InstantGoldenScrews=500 (sofort) |
| Autumn | autumn_mood_reset | SeasonalAutumnMoodReset | 60 | WorkerMoodResetTo=100 (alle Worker Mood=100, sofort) |
| Winter | winter_speed_4h | SeasonalWinterSpeed4h | 100 | SpeedBoostHours=4 (sofort, stackt) |
| Winter | winter_double_daily | SeasonalWinterDoubleDaily | 150 | DoubleDailyReward=true (nächster Daily-Reward ×100%) |

### 23.5 Effekt-Anwendung (`ApplySeasonalItemEffect`)

Sofort-Effekte: GoldenScrews > 0 → AddGoldenScrews · XpBonus > 0 → AddXp · SpeedBoostMinutes > 0 →
`SpeedBoostEndTime = max(bestehend, now + Minuten)` · InstantGoldenScrews > 0 → AddGoldenScrews ·
SpeedBoostHours > 0 → `SpeedBoostEndTime = max(bestehend, now + Stunden)` · WorkerMoodResetTo > 0 → alle
Worker aller Workshops Mood = Wert.

Passive/temporäre Effekte (von anderen Services über `PurchasedItems` ausgewertet): IncomeBonus,
ExtraWorkerDays, ResearchSpeedBonusPercent, OfflineEarningsBonusPercent, DoubleNextPrestige, DoubleDailyReward.

### 23.6 3D-/Visual-Präsentation (Unity-spezifisch)

- **Saisonale Stadt-Visuals:** Schnee (Winter), fallende Blätter (Autumn), Kirschblüten (Spring), Sonnenflimmern
  (Summer) als GPU-Particle-Schichten über der 3D-Stadt; 2× Partikel-Dichte gegenüber Default.
- **Saison-Theming** über `SeasonColor`/`SeasonIcon` auf Skybox, Licht und UI-Akzent — KEINE freischaltbaren
  Skins (der Shop verkauft nur die 6 funktionalen Items oben).
- **Event-Shop als animierter 3D-Marktstand** mit SP-Währungs-HUD; gekaufte Items klappen sichtbar als
  "verkauft" um (einmaliger Kauf).
- Optional (Phase 2): Live-Wetter via API zur Verstärkung der Saison-Stimmung; Mid-Season Story-Chapter-Drops
  und Limited-Time-Achievements als reine Präsentations-/Content-Layer (keine Mechanik-Abweichung).

---

## 24. Heirlooms (Erbstücke)

> Quelle: `PrestigeService.cs` (`ResetProgress`), `AscensionService.cs` (`DoAscension`),
> `GameBalanceConstants.cs`, `AscensionData.cs`, `IncomeCalculatorService.cs`, `CraftingRecipe.cs`.

### 24.1 Heirloom-Pool — NUR Tier-4-Crafting-Items

**KRITISCH:** Heirloom-fähig sind ausschließlich Crafting-Produkte mit `IsHeirloomEligible == true` —
laut Code/Plan **nur die drei Tier-4-Items**. Worker, Workshops, Equipment und Master-Tools sind
**KEINE** Heirloom-Kandidaten (Master-Tools werden über die Prestige-Tier-Preservierung bzw. den
Ascension-Perk `asc_eternal_tools` bewahrt, siehe §9.8/§9.9 — nicht über das Heirloom-System).

Tier-4-Rezepte (alle GeneralContractor, RequiredWorkshopLevel 500, Tier 4):

| OutputProductId | Rezept-Id | BaseValue (CLAUDE.md) |
|------------------|-----------|------------------------|
| villa | r_villa | 2,5 Mio. € |
| skyscraper | r_skyscraper | 4,0 Mio. € |
| imperium_hq | r_imperium_hq | 5,0 Mio. € |

(Die konkreten BaseValue-Felder stehen in der `CraftingProduct`-Definition; CLAUDE.md nennt
2.5/4.0/5.0 Mio. — die exakten Zahlen waren in den gelesenen Code-Zeilen nicht vollständig sichtbar.)

### 24.2 Run-Heirlooms (beim Prestige)

Quelle: `PrestigeService.cs` (`ResetProgress`).

- **Slot-Cap:** `GetEffectiveHeirloomSlots(IsPremium)` = `IsPremium ? 4 : 3`
  (`MaxHeirloomsPerRun = 3`, `MaxHeirloomsPerRunPremium = 4`).
- **Auto-Füllung** (wenn `HeirloomItems` leer): alle eligible Items aus `CraftingInventory` als
  Kandidaten (jedes Stück einzeln, mehrfach möglich), nach `BaseValue` absteigend sortiert, bis Cap.
- Wenn `HeirloomItems` > Cap: auf Cap beschneiden.
- Erbstücke überleben den Reset: nur die in `HeirloomItems` gelisteten eligible Items bleiben im neuen
  `CraftingInventory` (alles andere verworfen). `ReservedInventory.Clear()` beim Prestige.
- Telemetrie `heirloom_chosen` pro übertragenes Item.
- **Einkommens-Bonus pro aktivem Run-Erbstück: +2 %** (`HeirloomBonusPerItem = 0.02`).

### 24.3 Permanent-Heirlooms (beim Ascension)

Quelle: `AscensionService.cs` (`DoAscension`), `AscensionData.cs`.

- Beim Ascension wandern eligible Items aus `CraftingInventory` in `Ascension.PermanentHeirlooms`
  (nach `BaseValue` absteigend, jedes Stück einzeln).
- **Hard-Cap:** `MaxPermanentHeirlooms = 50` (nur die wertvollsten bis zum Cap).
- **Einkommens-Bonus pro permanentem Erbstück: +0.5 % forever** (`PermanentHeirloomBonusPerItem = 0.005`).
- Max permanenter Heirloom-Income: 50 × 0.5 % = **+25 %**.

> Hinweis: Eine Research-Unlock-Bedingung (z. B. `logi_12`) für Permanent-Heirlooms ist in den
> gelesenen Code-Dateien NICHT belegt — die Übertragung erfolgt unbedingt beim Ascension.

### 24.4 Income-Integration (GetTotalHeirloomBonus)

Quelle: `IncomeCalculatorService.cs`. Wird in `CalculateGrossIncome` NACH EternalMastery angewendet:

```
active    = HeirloomItems.Count × 0.02                  # Run-Erbstücke, +2 % je Stück
permanent = Ascension.PermanentHeirlooms.Count × 0.005  # permanent, +0.5 % je Stück
GetTotalHeirloomBonus = active + permanent
if heirloomBonus > 0:  grossIncome *= (1 + heirloomBonus)
```

---

## 25. Eternal-Mastery (permanenter Einkommens-Bonus)

> Quelle: `Services/EternalMasteryService.cs`, `GameBalanceConstants.cs`, `IncomeCalculatorService.cs`.

### 25.1 Aktivierung & Basis

```
CompletedPrestiges = Prestige.TotalPrestigeCount
IsActive           = CompletedPrestiges > 0
IncomeBonus        = CalculateBonus(CompletedPrestiges)
```

Der Bonus hängt allein an `TotalPrestigeCount` (Summe aller Tier-Counts), NICHT an einer separaten
„EM-Währung". Bei Ascension wird `state.Prestige` neu erzeugt (Tier-Counts = 0), wodurch
`TotalPrestigeCount` faktisch resettet — der Code-Kommentar formuliert „kein Reset bei Ascension" als
Design-Ziel, die Persistenz ergibt sich allein aus den nicht-resetteten Tier-Counts.

### 25.2 Soft-Cap-Dämpfung (CalculateBonus)

```
if completedPrestiges <= 0:  return 0

effectivePrestiges = completedPrestiges
if completedPrestiges > 50:                              # EternalMasterySoftCapThreshold = 50
    excess = completedPrestiges - 50
    effectivePrestiges = 50 + (int)(log10(excess + 1) × 10)

linear     = effectivePrestiges × 0.005                  # EternalMasteryBonusPerPrestige   = +0.5 % je Prestige
tier5Bonus = (effectivePrestiges / 5)  × 0.025           # EternalMasteryBonusPer5Prestiges = +2.5 % je 5
tier10Bonus= (effectivePrestiges / 10) × 0.05            # EternalMasteryBonusPer10Prestiges= +5 %  je 10

IncomeBonus = linear + tier5Bonus + tier10Bonus
```

Beispiel: 100 Prestiges = +150 % (ungedämpft: 50 % linear + 50 % 5er + 50 % 10er). Mit Soft-Cap bei 100:
`effectivePrestiges = 50 + (int)(log10(51) × 10) = 50 + 17 = 67`.

### 25.3 Hilfs-Properties

- `PrestigesUntilNextTier`: bis nächste 5er-Stufe (`0 → 5`, sonst `((CP/5)+1)×5 − CP`).
- `PrestigesUntilNextMegaTier`: bis nächste 10er-Stufe (`0 → 10`, sonst `((CP/10)+1)×10 − CP`).
- `DisplayText`: `"+{IncomeBonus×100:F1}%"` (InvariantCulture).

### 25.4 Income-Integration

Quelle: `IncomeCalculatorService.cs` (`CalculateGrossIncome`). Nach allen anderen Boni (Prestige-Shop,
Research, MasterTools, Gilde, VIP, Manager, Premium ×1.5) multipliziert, vor dem Heirloom-Bonus (§24.4):

```
if (_eternalMastery != null && _eternalMastery.IsActive)
    grossIncome *= (1 + _eternalMastery.IncomeBonus);
```

Der Income-Soft-Cap im IncomeCalculator ist tier-abhängig (4.0x–20.0x je Prestige-Tier + Ascension-Floor,
siehe §30.2) und betrifft den Gesamt-**Multiplikator** (grossIncome/baseIncome), nicht EternalMastery
direkt. Es gibt KEINE benannte Konstante `SoftCapThreshold` — der Wert 8.0 ist nur der switch-Default-/
Silver-Zweig.

---

## 26. Story-Chapters (60 = 40 Haupt + 20 Saison, Meister Hans)

Quelle: `Services/StoryService.cs`, `Models/StoryChapter.cs`, `Services/SeasonStorylineCatalog.cs`,
`Models/SeasonStoryline.cs`.

> **Echte Anzahl: 60 Kapitel.** 40 Haupt-Kapitel (ChapterNumber 1–40, `StoryService.CreateChapters()`) +
> 20 Saison-Kapitel (ChapterNumber 100–119, 4 Saisons × 5, `SeasonStorylineCatalog`). Der
> StoryService-Klassen-Kommentar nennt "37 Kapitel" — das ist veraltet.

### 26.1 StoryChapter-Modell

Felder (init): Id, ChapterNumber, TitleKey, TextKey, TitleFallback, TextFallback, Mood
(happy/proud/concerned/excited), IsTutorial. Belohnungen: MoneyReward (decimal), GoldenScrewReward (int),
XpReward (int). Bedingungen (alle gesetzten müssen erfüllt sein): RequiredPlayerLevel,
RequiredWorkshopCount, RequiredTotalOrders, RequiredPrestige, RequiredQuickJobsCompleted,
RequiredBattlePassTier, RequiredSeasonTheme, RequiredPrestigeTier, RequiredAscensionLevel.

### 26.2 Freischalt-Logik (`IsChapterUnlocked`)

1. **FTUE-Gate für Tutorial-Kapitel:** Kapitel mit `IsTutorial == true` werden erst freigeschaltet, wenn
   `state.Tutorial.Ftue.IsCompleted == true` (verhindert die Parallel-Konkurrenz von ftue_welcome und
   tutorial_welcome am ersten Start, beide Persona "Meister Hans"). Nicht-Tutorial-Kapitel laufen unabhängig.
2. Danach alle gesetzten Required-Felder prüfen (PlayerLevel, WorkshopCount via UnlockedWorkshopTypes.Count,
   TotalOrders, Prestige via TotalPrestigeCount, PrestigeTier via (int)CurrentTier, AscensionLevel,
   QuickJobs via TotalQuickJobsCompleted, BattlePassTier, SeasonTheme via BattlePass.SeasonTheme).

`CheckForNewChapter()` durchläuft alle Kapitel, überspringt bereits gesehene (`ViewedStoryIds`), setzt
`PendingStoryId` auf das erste ungesehene + freigeschaltete Kapitel (nichtlineare Progression möglich).

### 26.3 Belohnungs-Auszahlung (`MarkChapterViewed`, nur beim ERSTEN Mal)

Race-Schutz über `ViewedStoryIds`:
- **MoneyReward**: `scaledReward = Max(chapter.MoneyReward, NetIncomePerSecond * 600)` (mind. ~10 Min Einkommen).
- GoldenScrewReward → `AddGoldenScrews`. XpReward → `AddXp`. PendingStoryId zurücksetzen.

### 26.4 Haupt-Kapitel 1–40

Spalten: Nr | Id | Mood | Tut | Bedingungen | Money | GS | XP.

| Nr | Id | Mood | Tut | Bedingungen | Money | GS | XP |
|----|----|------|-----|-------------|------:|---:|---:|
| 1 | tutorial_welcome | happy | ja | Level 1 | 100 | 0 | 10 |
| 2 | tutorial_orders | excited | ja | QuickJobs ≥ 1 | 250 | 0 | 20 |
| 3 | tutorial_workers | proud | ja | Level 6 | 500 | 1 | 30 |
| 4 | tutorial_golden_screws | happy | ja | Level 9 | 750 | 2 | 40 |
| 5 | tutorial_buildings | excited | ja | Level 13 | 1.000 | 2 | 50 |
| 6 | early_plumber | excited | nein | Level 16, WS-Count 1 | 5.000 | 3 | 75 |
| 7 | early_first_worker | proud | nein | Level 18, Orders 5 | 10.000 | 3 | 100 |
| 8 | early_electrician | happy | nein | Level 22, WS-Count 2 | 25.000 | 5 | 150 |
| 9 | early_daily | excited | nein | Level 30, Orders 10 | 50.000 | 5 | 200 |
| 10 | early_painter | happy | nein | Level 35, WS-Count 3 | 100.000 | 8 | 250 |
| 11 | early_mastertools | proud | nein | Level 50, Orders 20 | 500.000 | 10 | 400 |
| 12 | early_roofer | excited | nein | Level 55, WS-Count 4 | 2.000.000 | 15 | 600 |
| 13 | mid_arena | excited | nein | Level 70, Orders 30 | 1.500.000 | 10 | 400 |
| 14 | mid_seasons | happy | nein | Level 80, Orders 40 | 2.000.000 | 15 | 500 |
| 15 | mid_contractor | proud | nein | Level 100, WS-Count 5, Orders 50 | 25.000.000 | 25 | 1.000 |
| 16 | mid_empire | happy | nein | Level 125, WS-Count 6 | 50.000.000 | 30 | 1.500 |
| 17 | mid_reputation | excited | nein | Level 150, Orders 75 | 100.000.000 | 40 | 2.000 |
| 18 | mid_prestige_hint | concerned | nein | Level 200, WS-Count 6 | 250.000.000 | 50 | 3.000 |
| 19 | mid_mastery | proud | nein | Level 250, Orders 100 | 500.000.000 | 75 | 5.000 |
| 20 | prestige_first | proud | nein | Prestige 1 | 1.000.000 | 100 | 7.500 |
| 21 | prestige_architect | excited | nein | Prestige 1, Level 50 | 5.000.000 | 50 | 5.000 |
| 22 | prestige_silver | proud | nein | PrestigeTier 2 (Silber) | 50.000.000 | 200 | 15.000 |
| 23 | prestige_general | excited | nein | Prestige 4, Level 75 | 100.000.000 | 100 | 10.000 |
| 24 | endgame_gold | excited | nein | PrestigeTier 3 (Gold) | 500.000.000 | 500 | 25.000 |
| 25 | endgame_legend | proud | nein | Level 500, WS-Count 7 | 1.000.000.000 | 300 | 50.000 |
| 26 | endgame_allmax | happy | nein | Level 750, WS-Count 8, Orders 500 | 5.000.000.000 | 500 | 75.000 |
| 27 | endgame_grandmaster | excited | nein | Level 1000 | 10.000.000.000 | 1.000 | 100.000 |
| 28 | endgame_platin | proud | nein | PrestigeTier 4 (Platin) | 50.000.000 | 50 | 25.000 |
| 29 | endgame_diamant_path | concerned | nein | PrestigeTier 4, Prestige 7 | 100.000.000 | 75 | 30.000 |
| 30 | endgame_diamant | excited | nein | PrestigeTier 5 (Diamant) | 500.000.000 | 100 | 40.000 |
| 31 | endgame_meister_weg | proud | nein | PrestigeTier 5, Prestige 12 | 1.000.000.000 | 125 | 50.000 |
| 32 | endgame_meister | excited | nein | PrestigeTier 6 (Meister) | 5.000.000.000 | 150 | 60.000 |
| 33 | endgame_legende_weg | concerned | nein | PrestigeTier 6, Prestige 18 | 10.000.000.000 | 200 | 75.000 |
| 34 | endgame_legende | excited | nein | PrestigeTier 7 (Legende) | 50.000.000.000 | 300 | 80.000 |
| 35 | endgame_ascension_ready | concerned | nein | PrestigeTier 7, Prestige 21 | 100.000.000.000 | 400 | 90.000 |
| 36 | endgame_first_ascension | proud | nein | AscensionLevel 1 | 500.000.000.000 | 500 | 100.000 |
| 37 | endgame_transcendence | excited | nein | AscensionLevel 3 | 1.000.000.000.000 | 750 | 100.000 |
| 38 | resources_warehouse | proud | **ja** | Level 50 | 100.000 | 5 | 250 |
| 39 | resources_supply_chain | excited | **ja** | Level 100 | 500.000 | 8 | 500 |
| 40 | resources_logistics_research | happy | **ja** | Level 150 | 2.000.000 | 10 | 1.000 |

TitleKeys/TextKeys: `Story_Ch{NN}_Title`/`_Text` (Kapitel 1–12 → Ch01–Ch12, 13/14 → Ch12a/Ch12b,
15–37 → Ch13–Ch35; Kapitel 38–40 → `Story_ResWarehouse_*` / `Story_ResSupplyChain_*` /
`Story_ResLogistics_*`). Vollständige DE-Fallback-Texte in `StoryService.cs`. **Kapitel 38–40 sind
`IsTutorial = true`** → erst nach FTUE-Abschluss freigeschaltet (gleiche Gate-Regel wie Kapitel 1–5).

### 26.5 Saison-Kapitel (ChapterNumber 100–119, 4 Saisons × 5)

Quelle: `SeasonStorylineCatalog`. Pro Saison 5 Kapitel an BP-Tier-Trigger **[1, 10, 25, 40, 50]**, alle
`IsTutorial = false`, `RequiredSeasonTheme` gesetzt. TitleKey/TextKey: `SeasonStory_{Theme}_Ch{n}_Title/_Text`.

**Spring — "Der Aufschwung der Stadt"** (ThemeKey `SeasonStorySpringTheme`)

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|-----:|------|------:|---:|---:|
| 100 | season_spring_ch1 | 1 | excited | 100.000 | 5 | 200 |
| 101 | season_spring_ch2 | 10 | proud | 1.000.000 | 15 | 750 |
| 102 | season_spring_ch3 | 25 | concerned | 5.000.000 | 25 | 1.500 |
| 103 | season_spring_ch4 | 40 | excited | 25.000.000 | 50 | 3.000 |
| 104 | season_spring_ch5 | 50 | proud | 100.000.000 | 100 | 7.500 |

**Summer — "Der Insel-Auftrag"** (ThemeKey `SeasonStorySummerTheme`)

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|-----:|------|------:|---:|---:|
| 105 | season_summer_ch1 | 1 | excited | 500.000 | 8 | 300 |
| 106 | season_summer_ch2 | 10 | proud | 2.500.000 | 18 | 1.000 |
| 107 | season_summer_ch3 | 25 | concerned | 7.500.000 | 30 | 2.000 |
| 108 | season_summer_ch4 | 40 | excited | 35.000.000 | 60 | 3.500 |
| 109 | season_summer_ch5 | 50 | proud | 150.000.000 | 120 | 8.500 |

**Autumn — "Wettbewerb der Innungen"** (ThemeKey `SeasonStoryAutumnTheme`)

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|-----:|------|------:|---:|---:|
| 110 | season_autumn_ch1 | 1 | excited | 700.000 | 10 | 350 |
| 111 | season_autumn_ch2 | 10 | proud | 3.500.000 | 22 | 1.200 |
| 112 | season_autumn_ch3 | 25 | proud | 12.000.000 | 38 | 2.400 |
| 113 | season_autumn_ch4 | 40 | concerned | 50.000.000 | 80 | 4.500 |
| 114 | season_autumn_ch5 | 50 | excited | 200.000.000 | 150 | 10.000 |

**Winter — "Der Sturm-Notdienst"** (ThemeKey `SeasonStoryWinterTheme`)

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|-----:|------|------:|---:|---:|
| 115 | season_winter_ch1 | 1 | concerned | 600.000 | 9 | 320 |
| 116 | season_winter_ch2 | 10 | concerned | 3.000.000 | 20 | 1.100 |
| 117 | season_winter_ch3 | 25 | concerned | 10.000.000 | 35 | 2.200 |
| 118 | season_winter_ch4 | 40 | proud | 40.000.000 | 70 | 4.000 |
| 119 | season_winter_ch5 | 50 | excited | 180.000.000 | 130 | 9.000 |

Vollständige DE-Fallback-Texte je Kapitel in `SeasonStorylineCatalog.cs`.

### 26.6 Meister-Hans-Voice

Pro Chapter 5 Lines × 6 Sprachen = 30 Voice-Files pro Chapter.
**Gesamt:** 60 Chapters × 5 Lines × 6 Sprachen = ~1800 Voice-Files (Teil der Voice-Assets in ASSETS_AI.md).

**Voice-Generation:** Via ElevenLabs API mit vorgefertigter Standard-Voice (siehe § 2.1). Batchable über REST-API.

### 26.7 3D-Story-Visualisierung

- Vollbild-Dialog mit **3D-Meister-Hans-Avatar** (links, animiert)
- Parallax-Background
- Voice-Over mit Untertiteln
- Skip-Button verfügbar
- Story-Re-Read im Settings-Menü

---

## 27. Tutorial / FTUE (10 Schritte) + ContextualHints (32)

Quelle: `Services/FtueService.cs`, `Services/FtueProgressTracker.cs`, `Models/FtueStep.cs`,
`Models/TutorialState.cs`, `Models/ContextualHint.cs`, `Services/ContextualHintService.cs`.

### 27.1 FTUE — 10 Schritte (`s_defaultSteps`, Order 0–9)

> **Echte Schrittzahl: 10** (nicht 8). Stabile Reihenfolge nach `Order`. SpotlightAutomationId zeigt auf
> reale UI-Elemente; das visuelle Spotlight-Overlay ist Folge-Sprint, Daten-Modell/State-Machine/Analytics
> sind vorhanden.

| Order | Id | TitleKey | ExpectedAction | SpotlightAutomationId | CanSkip |
|-------|----|----------|----------------|----------------------|---------|
| 0 | ftue_welcome | FtueWelcomeTitle | TapContinue | (keine) | **false** |
| 1 | ftue_first_upgrade | FtueFirstUpgradeTitle | BuyFirstUpgrade | Workshop_Btn_Upgrade | **false** |
| 2 | ftue_first_order | FtueFirstOrderTitle | AcceptFirstOrder | Dashboard_Items_Orders | true |
| 3 | ftue_first_minigame | FtueFirstMiniGameTitle | CompleteFirstMiniGame | (keine) | true |
| 4 | ftue_money_explained | FtueMoneyExplainedTitle | TapContinue | Dashboard_Txt_Money | true |
| 5 | ftue_first_worker | FtueFirstWorkerTitle | HireFirstWorker | Workshop_Btn_HireWorker | true |
| 6 | ftue_xp_explained | FtueXpExplainedTitle | ReachLevel2 | Dashboard_Txt_PlayerLevel | true |
| 7 | ftue_screws_explained | FtueScrewsExplainedTitle | TapContinue | Dashboard_Btn_GoldenScrews | true |
| 8 | ftue_imperium_intro | FtueImperiumIntroTitle | TapContinue | Main_Canvas_TabBar | true |
| 9 | ftue_complete | FtueCompleteTitle | TapContinue | (keine) | true |

**Skip-Sperre:** Schritt 0 + 1 sind NICHT skippbar (CanSkip = false) — erst nachdem der Core-Loop sichtbar
wird, darf der Spieler eine informierte Skip-Entscheidung treffen. TextKey je Step = `Ftue{Name}Text`.

**FtueExpectedAction-Enum**: TapAnywhere, TapSpotlight, BuyFirstUpgrade, AcceptFirstOrder, HireFirstWorker,
CompleteFirstMiniGame, ReachLevel2, TapContinue.

**FtueState** (persistiert in `GameState.Tutorial.Ftue`): CurrentStepIndex (-1 = nicht gestartet),
IsCompleted, WasSkipped, CompletedStepIds (HashSet), StartedAtIso, CompletedAtIso. Default-Init reicht
(keine SaveGame-Migration nötig).

### 27.2 FTUE-State-Machine (`FtueService`)

- **IsActive**: `CurrentStepIndex >= 0 && !IsCompleted && !WasSkipped`.
- **Start()**: idempotent — No-op wenn IsCompleted/WasSkipped/bereits gestartet. Setzt CurrentStepIndex=0,
  StartedAtIso=UtcNow. Analytics `ftue_started` + CurrentStepChanged.
- **CompleteCurrentStep()**: fügt step.Id zu CompletedStepIds, Analytics `ftue_step_completed`, dann
  AdvanceToNextUncompletedStep.
- **OnPlayerAction(action) (Catch-Up-Pass)**: markiert ALLE Steps mit dieser ExpectedAction als completed
  (auch nachgelagerte, falls Bedingung schon erfüllt). Wird der aktuelle Step abgehakt →
  AdvanceToNextUncompletedStep.
- **AdvanceToNextUncompletedStep()**: springt zum nächsten Step, der noch nicht in CompletedStepIds steht
  (überspringt per Catch-Up bereits abgehakte). Keiner mehr → IsCompleted=true, CompletedAtIso=UtcNow,
  CurrentStepIndex=-1, CurrentStepChanged(null), FtueFinished, Analytics `ftue_completed`.
- **SkipAll()**: WasSkipped=true, CompletedAtIso=UtcNow, CurrentStepIndex=-1, Analytics `ftue_skipped`
  (last_step_index, steps_completed), CurrentStepChanged(null) + FtueFinished.
- **Events**: CurrentStepChanged (EventHandler<FtueStep?>), FtueFinished. Telemetrie-Keys: step_id, step_order.

### 27.3 FtueProgressTracker (Event-Verdrahtung — Pflicht)

Quelle: `Services/FtueProgressTracker.cs`. Singleton, IDisposable. **Ohne diesen Tracker schreitet die FTUE
nie voran** (OnPlayerAction wird sonst nirgendwo gerufen). Event-Mapping (GameState-Events → OnPlayerAction):

- WorkshopUpgraded → BuyFirstUpgrade
- WorkerHired → HireFirstWorker
- OrderStarted → AcceptFirstOrder
- OrderCompleted → CompleteFirstMiniGame **nur wenn** `e.Order.Tasks.Count > 0` (MaterialOrders ohne
  MiniGame triggern nicht)
- LevelUp → ReachLevel2 **nur wenn** `e.NewLevel >= 2`

**StartIfNeeded()**: vom GameStartupCoordinator nach Spielstand-Laden aufgerufen. No-op wenn
IsCompleted/WasSkipped, sonst `_ftueService.Start()` (idempotent → Re-Start nach App-Restart mitten in FTUE).

### 27.4 ContextualHints (alle 32)

Quelle: `Models/ContextualHint.cs`, `Services/ContextualHintService.cs`. Tracking via
`GameState.Tutorial.SeenHints` (HashSet, JSON-persistiert).

**Service-Mechanik:**
- **TryShowHint(hint)**: false wenn bereits gesehen ODER ein anderer Hint aktiv ist (Ein-Hint-Lock). Sonst
  ActiveHint=hint, `HintChanged`, true.
- **DismissHint()**: markiert ActiveHint.Id als gesehen, ActiveHint=null, HintChanged(null).
- **HasSeenHint(id)** / **ResetAllHints()** (SeenHints + SeenMiniGameTutorials + HasSeenTutorialHint).
- Modell-Felder: Id, TitleKey, TextKey, Position (Above/Below, default Below), IsDialog (true = zentrierter
  Dialog statt Tooltip-Bubble). Event: HintChanged (EventHandler<ContextualHint?>).

| # | Id | TitleKey | Position | Dialog | Trigger |
|---|----|----------|----------|--------|---------|
| 1 | welcome | HintWelcomeTitle | Below | **ja** | Allererster Start (zentrierter Dialog) |
| 2 | first_workshop | HintFirstWorkshopTitle | Below | nein | Erste Werkstatt erkunden |
| 3 | workshop_detail | HintWorkshopDetailTitle | **Above** | nein | Werkstatt-Detail: Upgrade |
| 4 | first_order | HintFirstOrderTitle | **Above** | nein | Erster Auftrag annehmen |
| 5 | order_completed | HintOrderCompletedTitle | Below | nein | Erster Auftrag abgeschlossen |
| 6 | worker_unlock | HintWorkerUnlockTitle | Below | nein | Mitarbeiter freigeschaltet (Level 3) |
| 7 | shop_hint | HintShopTitle | Above | nein | Shop-Tab (erster Besuch) |
| 8 | research_hint | HintResearchTitle | Below | nein | Forschung (erster Research-Tab-Besuch) |
| 9 | building_hint | HintBuildingTitle | Below | nein | Gebäude (erster Buildings-Tab-Besuch) |
| 10 | daily_challenge | HintDailyChallengeTitle | Below | nein | Tägliche Herausforderungen (erster Missionen-Tab) |
| 11 | quick_jobs | HintQuickJobsTitle | Below | nein | Quick Jobs (Level 2 / erster QuickJobs-Tab) |
| 12 | prestige_hint | HintPrestigeTitle | Below | nein | Prestige verfügbar (Level 50) |
| 13 | guild_hint | HintGuildTitle | Below | nein | Gilden (erster Guild-Tab-Besuch) |
| 14 | crafting_hint | HintCraftingTitle | Below | nein | Crafting freigeschaltet |
| 15 | battle_pass | HintBattlePassTitle | Below | nein | Battle Pass verfügbar |
| 16 | lucky_spin | HintLuckySpinTitle | Below | nein | Glücksrad (Tag 2) |
| 17 | automation | HintAutomationTitle | Below | nein | Automatisierung freigeschaltet (Level 15) |
| 18 | manager_unlock | HintManagerUnlockTitle | Below | nein | Vorarbeiter freigeschaltet (Level 10) |
| 19 | master_tools_unlock | HintMasterToolsUnlockTitle | Below | nein | Meisterwerkzeuge freigeschaltet (Level 20) |
| 20 | ascension_available | HintAscensionAvailableTitle | Below | **ja** | Ascension verfügbar (erstmals CanAscend, nach 3× Legende) |
| 21 | ascension_path | HintAscensionPathTitle | Below | **ja** | Foreshadowing nach 1. Prestige |
| 22 | rebirth_ready | HintRebirthReadyTitle | Below | **ja** | Rebirth bereit (erster Workshop Level 1000) |
| 23 | first_star | HintFirstStarTitle | Below | **ja** | Erster Stern (nach erstem Rebirth) |
| 24 | golden_screws | HintGoldenScrewsTitle | Below | **ja** | Goldschrauben erklärt (erster Erhalt) |
| 25 | accept_order | HintAcceptOrderTitle | Below | nein | Aufträge erklärt (nach FirstWorkshop-Hint) |
| 26 | order_types | HintOrderTypesTitle | Below | **ja** | ONB-1: Auftragstypen |
| 27 | reputation_hint | HintReputationTitle | Below | **ja** | ONB-2: Reputation |
| 28 | long_press_bulk | HintLongPressBulkTitle | Below | nein | Nach 2. erfolgreichem Workshop-Upgrade (Bulk x10/x100) |
| 29 | material_offer | HintMaterialOfferTitle | Below | **ja** | Erstes gerolltes Material-Angebot (ab Level ≥ 30) |
| 30 | multi_task_order | HintMultiTaskOrderTitle | Below | **ja** | Erster Multi-Task-Auftrag (Rating-Durchschnitt zählt) |
| 31 | cross_workshop_coming | HintCrossWorkshopComingTitle | Below | **ja** | Bei Lv 99 (ab Lv 100 Cross-Workshop-Lieferketten) |
| 32 | tier4_coming | HintTier4ComingTitle | Below | **ja** | WS-Lv 450 oder logi_08-Abschluss (T4 naht) |

**12 Dialog-Hints** (IsDialog = true): welcome, ascension_available, ascension_path, rebirth_ready,
first_star, golden_screws, order_types, reputation_hint, material_offer, multi_task_order,
cross_workshop_coming, tier4_coming. **2 Above-Hints**: workshop_detail, first_order. Alle anderen Below.

### 27.5 3D-Visualisierung

- 3D-Kamera-Pan durch die Handwerker-Stadt
- "Tippe hier" mit animiertem 3D-Finger-Indikator
- Meister-Hans-3D-Avatar erscheint, gibt Tipps
- Stage-Lighting fokussiert wichtigen Bereich

---

## 28. Notifications (8 Push-Trigger + In-App-Bell)

Quelle: `HandwerkerImperium.Android/AndroidNotificationService.cs`, `Services/NotificationService.cs`
(Desktop-Stub), `NotificationCenterService.cs`, `Models/NotificationItem.cs`.

### 28.1 Push-Notifications (8 Trigger)

- **Channel**: Id `handwerker_game`, Name `HandwerkerImperium`, Importance `Default` (API 26+),
  Prefs-Key `notification_schedule`.
- **Mechanik (Android)**: `AlarmManager.SetAndAllowWhileIdle(RtcWakeup, …)` + `NotificationReceiver`
  (BroadcastReceiver). In SharedPreferences persistiert für Boot-Recovery (`BootReceiver` →
  `RescheduleFromPreferences`). Unity-Äquivalent: Unity Mobile Notifications mit gleichen IDs/Delays.
- **Vorbedingung**: `if (!Settings.NotificationsEnabled) return;` — vor dem Planen `CancelAllNotifications()`.
- **Texte** mit "Meister Hans"-Persona-Präfix (konkreter Text in den RESX-Werten), KEINE Emojis.
- **Desktop-Stub**: `ScheduleGameNotifications`/`CancelAllNotifications` sind No-Op.

| # | ID | RESX-Key | Delay / Trigger | Bedingung |
|---|-----|----------|-----------------|-----------|
| 1 | 1001 ResearchComplete | ResearchDoneNotif | `endTime − UtcNow` (StartedAt + Duration) | ActiveResearchId gesetzt, endTime > jetzt |
| 2 | 1002 DeliveryReminder | DeliveryWaitingNotif | 3 min (180.000 ms) | immer (wenn Notifications an) |
| 3 | 1004 DailyReward | DailyRewardNotif | nächster Tag 10:00 UTC | immer |
| 4 | 1003 RushAvailable | RushAvailableNotif | nächste 18:00 UTC | immer |
| 5 | 1005 WorkerMoodCritical | WorkerMoodCriticalNotif | 30 min (1.800.000 ms) | irgendein Worker `Mood < 25` |
| 6 | 1006 OfflineEarningsCapped | OfflineEarningsCappedNotif | 4 h (14.400.000 ms) | immer |
| 7 | 1007 BattlePassExpiring | BattlePassExpiringNotif | `max(0, DaysRemaining − 3)` Tage | BattlePass `DaysRemaining` in (0, 5] |
| 8 | 1008 LiveOrderAvailable | LiveOrderAvailableNotif | 1 h (3.600.000 ms) | irgendein Workshop `Level ≥ 25` |

### 28.2 In-App-Bell / NotificationCenter

Quelle: `NotificationCenterService.cs`, `Models/NotificationItem.cs`. **Getrennt** von den 8 Android-Push-
Triggern: die Bell-Inbox ist in-App, persistiert in `GameState.NotificationInbox`.

- `MaxInboxSize = 100` (FIFO-Eviction der ältesten über CreatedAt bei Überlauf).
- Alle Mutationen (Add/Dismiss/Clear/MarkAllSeen) laufen unter `ExecuteWithLock(…)` (gleicher Lock wie
  SaveGame → kein "Collection was modified").
- `Items`: gecachte Snapshot-Liste, neueste zuerst (`OrderByDescending(CreatedAt)`).
  `UnseenCount = Count(i => !i.Seen)`. `Add` dedupliziert über Id (Seen-Flag bleibt erhalten).
- `Changed`-Event (Action) bei jeder Mutation.

**NotificationKind-Enum** (Anzeige-Ort):

| Wert | Anzeige |
|------|---------|
| OfflineEarnings | IMMER Modal, NICHT in Bell |
| DailyReward | in Bell sammelbar |
| WelcomeBackOffer | in Bell sammelbar (Premium-Bundle) |
| AchievementUnlocked | in Bell sammelbar |
| StreakSaved | in Bell sammelbar |
| NewStoryChapter | in Bell sammelbar, mit Pulse-Akzent |
| LiveOrderAvailable | Live-/Premium-Auftrag (durch AutoAcceptOnlyStandard nicht angenommen) |

NotificationItem-Felder: id, kind, titleKey, titleArg (string?), bodyKey, bodyArg (string?), createdAt
(UtcNow), seen (bool), iconKind (string?).

### 28.3 Lokalisierung

Alle Texte in 6 Sprachen (DE/EN/ES/FR/IT/PT). Englisch ist Basis-Sprache für Fallbacks.

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
| Markt-Verkaufs-Spread | **5% konstant** (KEIN Premium-Rabatt — Spread ist Anti-Arbitrage, unabhängig vom Premium-Status) |
| Markt-Insider-Heatmap | aktiv (Markt-Sichtbarkeit) |
| Auto-Verkaufs-Regeln | aktiv (vor Research-Unlock) |
| Max-Offline-Stunden | 16h (statt Basis 4h; Rewarded-Video temporär 8h; Prestige-Shop +4h additiv) |

> **Korrektur ggü. früherer DESIGN-Fassung:** Premium hebt den Markt-Spread NICHT auf. Im Original
> (`MarketService`) ist `GetSellPrice = GetBuyPrice × (1 − 0.05)` mit konstantem `SpreadFactor = 0.05` —
> Premium beeinflusst nur Markt-Sichtbarkeit/Heatmap, nicht den Spread. Premium-Offline-Cap ist ein
> Dauer-Effekt (16h), KEIN Earnings-Multiplikator.

### 29.2 IAP-Bundles

| Bundle | Preis | Inhalt |
|--------|-------|--------|
| **Mid** | 9,99 € | 1500 GS + 8h Speed-Boost |
| **Big** | 19,99 € | 4000 GS + 48h Speed-Boost + 25 Mio. € |
| **Mega** | 49,99 € | 12.000 GS + 7 Tage Boost + 200 Mio. € + Premium (Imperium-Pass) |

> **Hinweis:** Das Mega-Bundle enthält NICHT den BattlePass-Season-Pass — der ist ein eigener Consumable-SKU
> `battle_pass_season` pro Saison (§21.8). Daily-Bundle (§29.5) ist ein separates, RemoteConfig-getriebenes
> Tages-IAP, kein Teil dieser drei Whale-Bundles.

### 29.3 Ad-Placements (13)

1. **golden_screws** — 12 GS, 4h Cooldown
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
| Mini-Game-Mastery (Bronze/Silver/Gold) | 5 / 15 / 50 |
| Daily-Challenge (pro Challenge / Alle-fertig-Bonus) | Tier ≤4: 1–2 · 5:3 · 6:4 · 7:5 · 8:6 / Alle-fertig 6–15 |
| Weekly-Mission (pro Mission / Alle-fertig-Bonus) | T0:5 … T8:35 / Alle-fertig 50–120 |
| Achievements | 5-50 |
| Daily-Login | 1-25 (Tag 30 = 25) |
| Rewarded Ad (`golden_screws`) | 12 |
| Live-Event Tier 0/1/2 | 25 / 75 / 200 |
| Tournament Gold/Silver/Bronze | 30+Asc×5 / 15+Asc×3 / 5+Asc |
| Workshop-/Prestige-Meilensteine | 2-50 / 5-100 |
| Lucky-Spin (regulär / Jackpot) | **5 / 50** |
| Referral Tier 1/5/10 | 50 / 200 / 500 |
| Saison-Capstone (BattlePass Tier 49 Free/Premium) | 50 / 150 |

> **Korrektur ggü. früherer DESIGN-Fassung:** Lucky-Spin gibt **5 GS** (GoldenScrews5) bzw. **50 GS**
> (Jackpot50) — nie 20 (siehe §20.1).

**Premium-GS-Verdopplung (`GameStateService.AddGoldenScrews`):** Gameplay-GS-Quellen werden zuerst um den
Prestige-Shop-GoldenScrewBonus + Ascension-Golden-Era-Bonus (additiv) erhöht, DANN bei Premium ×2 (+100%).
**IAP-/Kauf-Quellen** rufen `AddGoldenScrews(.., fromPurchase: true)` (z.B. Daily-Bundle) und werden
**NICHT verdoppelt**.

### 29.5 Daily-Bundle (7-Slot-Tages-IAP, RemoteConfig)

Quelle: `DailyBundleService` / `DailyBundleOffer`. Separates Tages-IAP-Bundle, RemoteConfig-getrieben:

- **7 Slots (Mo=0 … So=6)**, JSON aus `RemoteConfigKeys.DailyBundleSkus`, Enable-Flag
  `RemoteConfigKeys.DailyBundleEnabled` (Default false → Bundle aus, wenn kein JSON). Max 7 Slots.
- JSON pro Slot: `{"sku","title_key","desc_key","bonus_screws","bonus_money","speed_hours"}` (Wrapper
  `{"slots":[...]}` oder direktes Array).
- Aktiver Slot = `((int)today.DayOfWeek + 6) % 7` (ISO Mo=0..So=6). Rotation um 00:00 UTC → Event
  `BundleRotated` (im Lock, Doppel-Event-Schutz). `ExpiresAtUtc = nächstes 00:00 UTC`.
- Kauf (`PurchaseCurrentBundleAsync`): `PurchaseConsumableAsync(sku)`; bei Erfolg BonusGoldenScrews > 0 →
  `AddGoldenScrews(.., fromPurchase: true)` (NICHT verdoppelt), BonusMoney > 0 → AddMoney, SpeedBoostHours > 0
  → `SpeedBoostEndTime` stackt (`max(bestehend, now) + Stunden`).
- Analytics: IapPurchaseStarted / IapPurchaseFailed / IapPurchaseSuccess.
- **Konkrete Slot-Werte:** kommen ausschließlich aus dem RemoteConfig-JSON, KEIN Hardcoded-Default im Code
  (nicht erfinden).

### 29.6 Shop Daily Offer (`ShopOffer`)

`GenerateDaily(incomePerSecond)` wählt zufällig 1 von 4 Angeboten; `DiscountedPrice = OriginalPrice / 2`
(Discount 50%), `ExpiresAt = nächstes 00:00 UTC`:

| ItemId | NameKey | Original (GS) | Rabattiert (GS) | GS-Reward | Money-Reward |
|--------|---------|---------------|-----------------|-----------|--------------|
| daily_screws_10 | DailyOfferScrews10 | 20 | 10 | 10 | 0 |
| daily_screws_25 | DailyOfferScrews25 | 50 | 25 | 25 | 0 |
| daily_money_boost | DailyOfferMoneyBoost | 15 | 7 | 0 | `max(5000, incomePerSecond×600)` |
| daily_speed_boost | DailyOfferSpeedBoost | 10 | 5 | 0 | 0 |

### 29.7 Cross-Promotion (House-Ads)

Quelle: `CrossPromoService` / `CrossPromoApp`. Statischer Katalog, eigene App ausgefiltert, deterministische
Tagesrotation (`startIdx = UtcNow.DayOfYear % available.Count`). Deep-Link
`https://play.google.com/store/apps/details?id={PackageId}`, Analytics `cross_promo_click`.

| Id | IconKind | AccentColor | PackageId |
|----|----------|-------------|-----------|
| rechnerplus | Cash | #7C7FF7 | com.meineapps.rechnerplus |
| zeitmanager | TimerOutline | #F7A833 | com.meineapps.zeitmanager |
| finanzrechner | CashMultiple | #10B981 | com.meineapps.finanzrechner |
| fitnessrechner | Dumbbell | #06B6D4 | com.meineapps.fitnessrechner |
| handwerkerrechner | HammerWrench | #3B82F6 | com.meineapps.handwerkerrechner |
| worktimepro | Cog | #4F8BF9 | com.meineapps.worktimepro |
| handwerkerimperium | Hammer | #D97706 | com.meineapps.handwerkerimperium (self, gefiltert) |
| bomberblast | RocketLaunch | #FF6B35 | com.meineapps.bomberblast |
| rebornsaga | Sword | #4A90D9 | com.meineapps.rebornsaga |
| bingxbot | FlaskOutline | #3B82F6 | com.meineapps.bingxbot |
| gardencontrol | ShieldHalfFull | #2E7D32 | com.meineapps.gardencontrol |

NameKey/HookKey je Eintrag = `CrossPromo_{App}_Name` / `CrossPromo_{App}_Hook`. SmartMeasure ist NICHT im
Katalog. Self-Eintrag (`com.meineapps.handwerkerimperium`) wird bei `GetAvailable` ausgefiltert.

### 29.8 VIP (ausgaben-basiert)

Quelle: `VipService` / `VipTier`. Tier-Bestimmung aus `GameState.TotalPurchaseAmount` (EUR). **Bewusst KEIN
Pay-to-Win:** Income-Bonus gedeckelt, CostReduction = 0. NICHT zu verwechseln mit Live-Order-VIP-Kunden
(3× Reward) — das ist ein anderes System.

| Tier | MinSpend (EUR) | IncomeBonus | XpBonus | CostReduction | Farbe |
|------|----------------|-------------|---------|---------------|-------|
| None | (kein Kauf) | 0% | 0% | 0% | #808080 |
| Bronze | 4,99 | +2% | 0% | 0% | #CD7F32 |
| Silver | 9,99 | +3% | +2% | 0% | #C0C0C0 |
| Gold | 19,99 | +4% | +3% | 0% | #FFD700 |
| Platinum | 49,99 | +5% | +5% | 0% | #E5E4E2 |

Perks: Auto-Claim Daily Rewards (Bronze+), Lieferanten-Timer sichtbar (Silver+), `ExtraDailyChallenges = 1`
(Silver+ → 3+1 Challenges), `ExtraWeeklyMissions = 1` (Gold+ → 5+1 Missionen), exklusiver Avatar-Rahmen
(Gold+, kosmetisch). `RecordPurchase(amountEur)`: `TotalPurchaseAmount += amountEur`, dann RefreshVipLevel.

### 29.9 Referral (Friend-Invite)

Quelle: `ReferralService` / `ReferralProgress`. Eigener 6-stelliger Code (Alphabet
`ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, ohne O/0/I/1), einmalig generiert. `SubmitReferralCode`: nur 6-stellig,
nur einmal, kein Self-Referral. `OnReferralSucceeded` nach 24h Aktivität des Eingeladenen (server-getrieben).

| Tier (erfolgreiche Empfehlungen) | GS-Belohnung | Zusatz |
|----------------------------------|--------------|--------|
| 1 | 50 | — |
| 5 | 200 | — |
| 10 | 500 | + permanenter Income-Boost +5% (`PermanentIncomeBonus = 0.05`) |

Analytics: referral_code_used, referral_succeeded, referral_tier_claimed. Anti-Cheat (Geräte-Fingerprint,
IP-Limit) + Server-Endpoint `POST /referrals/{ownerCode}/claim` (laut Original-Doku Folge-Sprint).

### 29.10 3D-/UX-Präsentation (Unity-spezifisch)

- **Premium-Shop als hochwertiger 3D-Store** mit rotierenden Bundle-Podesten, Gold-Shimmer-Shader auf den
  Whale-Bundles und einem dezenten "Imperium-Pass"-Hero-Banner (keine aggressiven Pop-ups).
- **Rewarded-Ad-Trigger** kontextuell platziert (z.B. Lucky-Spin-Rad, Daily-Challenge-Retry, Offline-Double-
  Dialog), mit klarem Cooldown-Ring und ehrlicher Reward-Vorschau.
- **VIP-Tier-Aufstieg** als kurze Cinematic (Wappen-Reveal in Tier-Farbe); House-Ads/Cross-Promo als dezente
  Settings-Karte mit Tagesrotation, kein interstitielles Overlay.

---

## 30. Economy-Formeln

> Alle Formeln in diesem Abschnitt sind 1:1 aus dem Avalonia-Original extrahiert (`IncomeCalculatorService`, `OfflineProgressService`, `GameStateService`, `GameState`). Reihenfolge und Caps sind verbindlich.

### 30.1 Income-Berechnung (Brutto, vollständige Multiplikator-Kette)

Basis siehe § 4.3 (Pro-Worker-Summe). Wichtig: **GrossIncome = 0, wenn keine arbeitenden Worker** — Income entsteht ausschließlich pro Worker, nie durch leere Werkstätten.

```
Basis:  grossIncome = TotalIncomePerSecond
        = (Σ Workshops[i].GrossIncomePerSecond) × min(Prestige.PermanentMultiplier, 20.0)
        // PermanentMultiplier HART bei 20× gedeckelt
```

Dann **in dieser exakten Reihenfolge** (jeder Faktor nur, wenn seine Bedingung erfüllt ist):

| # | Faktor | Operation | Bedingung / Cap |
|---|--------|-----------|-----------------|
| 1 | Prestige-Shop Income | `× (1 + prestigeIncomeBonus)` | `prestigeIncomeBonus` summiert; der **+300%**-Deckel (`Math.Min(…, 3.0)`) wird im **GameLoop-Cache (online)** gesetzt — offline (`OfflineProgressService`) wird der Bonus ungedeckelt berechnet |
| 2 | Research-Effizienz | `× (1 + min(EfficiencyBonus, 0.50))` | Cap **+50%** |
| 3 | Event-Multiplikator | `× eventEffects.IncomeMultiplier` | aktives Event |
| 4 | TaxAudit-Steuer | `× 0.90` | Event-SpecialEffect `tax_10_percent` |
| 5 | Meisterwerkzeuge | `× (1 + mtBonus)` | 12 Tools, gesamt **+74%** |
| 6 | Gilde Income-Bonus | `× (1 + GuildMembership.IncomeBonus)` | Mitglied |
| 7 | Gilde Research-Income | `× (1 + ResearchIncomeBonus)` | > 0 |
| 8 | Gilde Research-Efficiency | `× (1 + ResearchEfficiencyBonus)` | > 0 |
| 9 | Gilde Hall-Income | `× (1 + HallIncomeBonus)` | > 0 (Gildenhalle TradingPost) |
| 10 | Gilde Hall-Everything | `× (1 + HallEverythingBonus)` | > 0 (universeller Hall-Bonus) |
| 11 | VIP Income | `× (1 + VIP.IncomeBonus)` | > 0 |
| 12 | Manager Income (Summe) | `× (1 + totalManagerIncome)` | workshop-spezifisch + global |
| 13 | Manager Efficiency (Summe, **separat**) | `× (1 + totalManagerEfficiency)` | workshop-spezifisch + global |
| 14 | Premium | `× 1.5` | `IsPremium` |
| 15 | Eternal Mastery | `× (1 + EternalMastery.IncomeBonus)` | aktiv |
| 16 | Heirloom (Erbstücke) | `× (1 + heirloomBonus)` | siehe unten |

Danach: **Soft-Cap (§30.2)**.

**Wichtig:** Manager Income und Manager Efficiency sind **zwei getrennte Multiplikatoren** (nicht summiert).

```
heirloomBonus = HeirloomItems.Count          × 0.02   (+2% je aktivem Heirloom, HeirloomBonusPerItem)
              + Ascension.PermanentHeirlooms  × 0.005  (+0.5% je permanentem, PermanentHeirloomBonusPerItem)
```

### 30.2 Income-Soft-Cap (tier-skalierender Multiplikator-Cap — KEIN €/s-Cap)

Der Soft-Cap operiert auf dem **effektiven Multiplikator** `grossIncome / TotalIncomePerSecond`, NICHT auf einem absoluten €/s-Betrag. Er dämpft alles oberhalb einer tier- und ascension-abhängigen Schwelle logarithmisch.

```
Early-Return: wenn TotalIncomePerSecond <= 0 → grossIncome unverändert.

tierThreshold (nach Prestige-Tier):
   None 4.0 | Bronze 6.0 | Silver 8.0 | Gold 10.0 | Platin 12.0 | Diamant 14.0 | Meister 16.0 | Legende 20.0 | (default 8.0)

ascensionThreshold = (AscensionLevel > 0) ? min(18.0 + AscensionLevel × 2.0, 30.0) : 0.0
                     // Ascension-Floor, hart bei 30× gedeckelt (Lv1→20, Lv6+→30)

threshold = max(tierThreshold, ascensionThreshold)

effectiveMultiplier = grossIncome / TotalIncomePerSecond
wenn effectiveMultiplier > threshold:
    excess   = effectiveMultiplier − threshold
    softened = threshold + log₂(1 + excess)
    grossIncome = TotalIncomePerSecond × softened
    IsSoftCapActive = true
    SoftCapReductionPercent = round((1 − softened/effectiveMultiplier) × 100)   // für UI-Indikator
sonst:
    IsSoftCapActive = false; SoftCapReductionPercent = 0
```

(Es gibt KEINE benannte Konstante `SoftCapThreshold` — `8.0` ist nur der `default`-/Silver-Zweig des Tier-`switch`.)

### 30.3 Offline-Income (4-stufige Staffelung + Schutz)

**Offline-Dauer & Zeitmanipulations-Schutz:**

```
lastPlayed = LastPlayedAt; now = UtcNow
wenn lastPlayed > now → 0 Earnings     // Systemuhr zurückgedreht (Anti-Cheat)
offlineDuration = now − lastPlayed
wenn offlineDuration < 60s → 0 Earnings
wenn Offline-Income blockiert (Sprint-Challenge) → 0 Earnings

wasCapped = offlineDuration > MaxOfflineDuration
effectiveDuration = min(offlineDuration, MaxOfflineDuration)   // wasCapped → "Neuer Rekord"-UI
```

**MaxOfflineHours** (gecacht, invalidiert bei Premium-Wechsel / Prestige-Shop-Kauf):

```
baseHours = IsPremium ? 16 : (OfflineVideoExtended ? 8 : 4)    // Basis 4h, Video-erweitert 8h, Premium 16h
+ PrestigeShop OfflineHoursBonus (pp_offline_hours: +4h, additiv)
```

**Earnings-Berechnung (offline):**

```
netPerSecond = max(0, GrossIncome − Kosten)    // offline NIE Geld verlieren (auf 0 geklemmt)
totalSeconds = effectiveDuration

first2h   = min(totalSeconds, 7200)                   →  80%
next2h    = min(max(totalSeconds − 7200, 0), 7200)    →  35%
next4h    = min(max(totalSeconds − 14400, 0), 14400)  →  15%
remaining = max(totalSeconds − 28800, 0)              →   5%

earnings = netPerSecond × (first2h×0.80 + next2h×0.35 + next4h×0.15 + remaining×0.05)
```

| Stufe | Zeitfenster | Grenze (s) | Rate |
|-------|-------------|------------|------|
| 1 | 0–2h | bis 7200 | 80% |
| 2 | 2–4h | 7200–14400 | 35% |
| 3 | 4–8h | 14400–28800 | 15% |
| 4 | 8h+ | ab 28800 | 5% |

Dieselbe 80/35/15/5-Staffelung gilt für die **Offline-Auto-Produktion** (`CalculateEffectiveOfflineSeconds`). Danach werden Boosts pro-rata angewandt (§30.8), Worker-States simuliert und Items produziert.

### 30.4 Worker-Effizienz

Detail-Faktoren siehe § 5 (Arbeiter). Wichtig: Die **Material-Affinität ist KEIN Faktor der Worker-Effizienz** — sie wirkt ausschließlich als anteiliger **Crafting-Geschwindigkeits-Bonus (+20%)** im Crafting-System (§10/§12).

```
EffectiveEfficiency = BaseEff × (1 + Level × 0.03)
                    × MoodFactor             // 0-1 linear
                    × FatigueFactor          // 1-0.5 linear
                    × (1 + SpecBonus + EquipBonus)
                    × PersonalityMult        // 0.85 - 1.20x (6 Persönlichkeiten)
                    × TalentBonus            // 1.0 - 1.25x (Talent-Stars)
                    × LevelFitFactor         // Penalty bei niedrigem Tier auf hohem WS-Level
```

Workshop-Brutto (vor globalen Multiplikatoren) zusätzlich: `× (1 + min(Σ AuraBonus, 0.50))` (Aura-Cap **+50%**, `MaxAuraBonus = 0.50`) `× (1 + RebirthIncomeBonus)`.

### 30.5 Upgrade-Kosten

Siehe § 4.5. Kern-Konstanten: `UpgradeCostBase = 200`, `UpgradeCostLevel1 = 100`, `UpgradeCostExponent = 1.07` (ab Lv 500: `1.06`), Prestige-Discount-Cap `0.50`. Worker-Hire: `HireWorkerCostBase = 50`, `HireWorkerCostExponent = 1.5`. Upgrade-XP-Reward: `5 + newLevel / 10`.

### 30.6 Order-Reward-Multiplikator (Soft-Cap 10× via Sqrt)

Der Auftrags-Reward-Multiplikator (`CalculateOrderRewardMultiplierUnlocked`) wird **multiplikativ in dieser Reihenfolge** aufgebaut, mit einem Sqrt-Soft-Cap bei 10×:

```
multiplier = 1
1) Research RewardMultiplier:  × (1 + Σ Researched.RewardMultiplier)        [wenn > 0]
2) VehicleFleet-Gebäude:       × (1 + OrderRewardBonus)                     [wenn > 0]
3) Reputation:                 × Reputation.ReputationMultiplier            (0.7×–1.5×)
4) Event RewardMultiplier:     × Event.RewardMultiplier                     [wenn aktiv & Workshop passt]
5) Stammkunde:                 × customer.BonusMultiplier                   (1.1×–1.5×, ab >5 Perfects)
6) Prestige-Shop OrderReward:  × (1 + min(orderRewardBonus, 1.0))           // eigener Cap +100%
7) Gilde Hall-OrderReward:     × (1 + HallOrderRewardBonus)                 [wenn > 0]
8) Gilde Hall-Everything:      × (1 + HallEverythingBonus)                  [wenn > 0]

Soft-Cap (Sqrt auf Überschuss, OrderRewardMultiplierSoftCap = 10.0; die Hall-Boni 7/8 liegen VOR dem Cap):
wenn multiplier > 10.0:  multiplier = 10.0 + √(multiplier − 10.0)
   // Beispiel: roh 15× → 10 + √5 ≈ 12.24×
```

**Reward-Anwendung beim Auftragsabschluss** (`CompleteActiveOrder`):

```
moneyReward = order.FinalReward × OrderRewardMultiplier
xpReward    = order.FinalXp
// Material-Offer (zuerst):  × (1 + MaterialOfferBonusMultiplier)   (0.25/0.30/0.40/0.50/0.60 je Order-Typ)
// Combo (PaintingGame):     × ComboMultiplier
// Rewarded-Ad-Verdopplung:  × (IsPremium ? 3 : 2)                  (IsScoreDoubled)
```

`order.FinalReward` = `BaseReward × TypeMultiplier × StrategyMultiplier × MiniGameRatingMultiplier × Difficulty` (Detail in §6: Type 0.6/1.0/1.8/2.5/3.0/1.8 · Strategy 0.75/1.0/2.0 · Rating 0.20/0.50/1.00/1.50 (Miss/Ok/Good/Perfect)). Material-Order: `EstimatedReward × OrderRewardMultiplier`, XP über `MaterialOrderXpMultiplier = 1.5`.

### 30.7 Kosten-Berechnung (Netto = Brutto − Kosten)

Ohne Kostenseite ist das Netto-Einkommen nicht berechenbar. Kosten (`CalculateCosts`):

```
costs = TotalCostsPerSecond                          // = (Σ Workshops[i].TotalCostsPerHour) / 3600

totalCostReduction = Prestige.CostReduction
                   + Research.CostReduction + Research.WageReduction
                   + Storage.MaterialCostReduction × 0.5          // Storage nur HALBER Effekt auf laufende Kosten
wenn totalCostReduction > 0:
    costs ×= (1 − min(totalCostReduction, 0.50))     // gemeinsamer Cap 50%

wenn Event aktiv:        costs ×= Event.CostMultiplier
wenn Gilde-Research:     costs ×= (1 − min(GuildResearchCostReduction, 0.50))   // eigener Cap 50%
```

### 30.8 Boosts: SpeedBoost + Feierabend-Rush

**Online** (pro Tick, NACH `netEarnings = GrossIncome − Kosten`, nur bei `netEarnings > 0`):

```
wenn SpeedBoost aktiv:  netEarnings ×= 2
wenn Rush aktiv:        rushMultiplier = 2 + PrestigeShop-Rush-Bonus;  netEarnings ×= rushMultiplier
```

Stacking ist multiplikativ: Speed (×2) × Rush (×2 + Prestige-Bonus) → online bis **×4** (mehr mit Prestige-Rush-Bonus). Feierabend-Rush: 1×/Tag gratis (`IsFreeRushAvailable` via `.Date`-Vergleich, zukunftssicher), danach Goldschrauben.

**Offline** (pro-rata gewichteter Durchschnitt, `ApplyBoostsProRata`):

```
rushMultiplier = min(2 + PrestigeShop-Rush-Bonus, 4)          // Rush-Multi HART bei 4× gedeckelt
gewichtete Fenster über die Offline-Dauer:
   bothSeconds × 2 × rushMultiplier + onlySpeedSeconds × 2 + onlyRushSeconds × rushMultiplier + unboosted × 1
averageMultiplier = weighted / totalSeconds;  earnings ×= averageMultiplier
```

Maximal-Stack offline: Speed (×2) × Rush (max ×4) = **×8**.

### 30.9 Player-XP-Kurve

```
XpForLevel(L) = (L <= 1) ? 0 : (int)(100 × (L − 1)^1.2)        // kumulativer XP-Schwellwert für Level L
```

**AddXp**-Reihenfolge: XP-Boost (×2, `IsXpBoostActive`) ZUERST, dann Prestige-Shop XpMultiplier (`× (1 + xpBonus)`). Über Level 1500 (`MaxPlayerLevel`) verfällt XP (kein Overflow).

### 30.10 Crafting-Sell-Multiplikator (separater Pfad)

```
mult = 1.0 × (Prestige-Income) × (1 + min(Research-Eff, 0.50)) × Event × TaxAudit(0.90)
       × MasterTools × Gilde[Income + ResearchIncome + ResearchEfficiency + HallIncome + HallEverything] × VIP × Rebirth-Income × Premium(1.5)
Soft-Cap:  wenn mult > 8.0:  mult = 8.0 + log₂(1 + (mult − 8.0));   return min(mult, 12.0)
           // CraftingSellMultiplierSoftCap = 8.0, HardCap = 12.0
```

### 30.11 Auto-Complete-Schwellen (Mini-Game)

```
baseThreshold = (PipePuzzle|Blueprint|InventGame|DesignPuzzle|Inspection) ? 20 : 30   (Timing/Sonstige: 30)
threshold = IsPremium ? baseThreshold / 2 : baseThreshold       // Premium halbiert (10 bzw. 15)
auto-complete wenn PerfectRatingCounts[type] >= threshold
```

`PerfectRatingCounts` wird bei Ascension zurückgesetzt, `LifetimePerfectRatingCounts` (Mastery) nicht.

---

## 31. Anti-Cheat

### 31.1 HMAC-Signierung (GameIntegrityService 1:1)

**Schlüssel-Ableitung (gerätegebunden, NIE hardcodiert):** Beim ersten Start wird eine Installations-GUID
(`Guid.NewGuid().ToString("N")`) erzeugt und persistiert (Preference-Key `game_integrity_install_id`).
Daraus `_hmacKey = SHA256(UTF8(PackageSalt + installId))` → 32 Byte / 256 Bit. PackageSalt =
`com.meineapps.handwerkerimperium`. Auf Mobile kann der Install-Seed zusätzlich in Android-Keystore/iOS-Keychain
gehärtet werden. Die Beta-App-ID lautet `com.meineapps.handwerkerimperium2.beta` — der PackageSalt bleibt aber
identisch zur Avalonia-Version, damit Cloud-Saves geräteübergreifend re-signierbar sind.

**GameState-Snapshot-Signatur** (`GameState.IntegritySignature`, HMAC-SHA256 als Lower-Hex):
Payload kultur-invariant `"{PlayerLevel}|{Prestige.TotalPrestigeCount}|{Money:F2}|{GoldenScrews}|{Statistics.TotalOrdersCompleted}"`.
Genau diese **5 Felder** werden signiert (nicht der ganze State). Verifikation via
`CryptographicOperations.FixedTimeEquals` über `Convert.FromHexString` der gespeicherten Signatur gegen frisch
berechnete Bytes (timing-sicher; ungültiges Hex → false).

**Einzelwert-HMAC** (`ComputeStringHmac(payload)`, generischer Lower-Hex-Helper) für serverseitig gespiegelte
Werte der Big-Bet-Features. Folgende Werte sind einzeln HMAC-signiert:
- Money (decimal, `F2`)
- GoldenScrews (int)
- BossDamage (int)
- AuctionBid (decimal)
- CoopOrderScore (long)
- MegaProjectContribution (long)

Mega-Projekt-Salt: `guild-mega-project-v1` (signiert `ProjectId|(int)Type|CreatedAt:O` — siehe § 34.3).

**Cloud-Re-Signierung:** Der HMAC-Key ist gerätegebunden, daher wird ein heruntergeladener Cloud-State für das
lokale Gerät neu signiert (`ComputeSignature(state)`). Bewusste Design-Entscheidung: Cloud-Save schützt gegen
Geräteverlust, nicht gegen lokales Save-Editing (dafür greift der Save-Sanitizer, § 31.3).

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

### 31.3 Save-Sanitizer (SanitizeState — Reparatur statt Ablehnung)

Läuft nach Migration bei **jedem** Load/Import (`SaveGameService.SanitizeState`). Repariert statt abzulehnen —
ein beschädigter Save wird nie verworfen. Vollständige Repair-Schritte:

- **Sub-Objekt-Null-Safety:** Boosts, DailyProgress, Cosmetics, Tutorial, Statistics, Settings, BattlePass je `??= new()`.
- **Premium:** `IsPremium = _purchaseService?.IsPremium ?? false` (aus kaufgesichertem Preference-Cache, VOR Heirloom-Cap).
- **PlayerLevel:** clamp `[1, 1500]`.
- **Money:** `< 0 → 0`; Cap `moneyCap = Math.Max(1_000_000_000_000_000m, TotalMoneyEarned)` (1e15-Floor, NICHT 1e21).
- **CurrentXp / GoldenScrews / TotalMoneyEarned / CurrentRunMoney / TotalMoneySpent:** `< 0 → 0`.
- **GoldenScrews-Cap: 100.000.**
- **Workshops:** mind. 1 Carpenter (IsUnlocked, in UnlockedWorkshopTypes); Workshop.Level clamp `[1, MaxLevel=1000]`.
- **Prestige:** alle Counts `< 0 → 0`; PermanentMultiplier clamp `[1.0, 20.0]`; PurchasedShopItems gegen `PrestigeShop.GetValidIds()` filtern.
- **Worker:** AdBonusWorkerSlots ≤ `MaxAdBonusWorkerSlots`; Mood/Fatigue clamp `[0,100]`; ExperienceLevel ≥ 1; AssignedWorkshop = ws.Type; WagePerHour = Tier.GetWagePerHour(); Efficiency clamp `[Tier.GetMinEfficiency(), Tier.GetMaxEfficiency()]`. Material-Affinity-Migration (None && Id≠"" → `(|Id.GetHashCode()|%5)+1`).
- **Reputation:** ReputationScore clamp `[0,100]`.
- **ParallelOrdersByWorkshop:** verwaiste Einträge entfernen; hartes Cap `MaxParallelOrders=3`.
- **ResearchTree-Sync** aus Template (`ResearchTree.CreateAll()`): fehlende Nodes ergänzen, ActiveResearchId-Konsistenz.
- **Building.Level** clamp `[0, Type.GetMaxLevel()]`. **CollectedMasterTools** gegen `MasterTool.GetValidIds()` filtern.
- **CraftingInventory:** count ≤ 0 entfernen; count > WarehouseStackLimit kappen.
- **ReservedInventory:** nie > CraftingInventory; Orphan-Reservierungen freigeben.
- **WarehouseSlotCount** clamp `[20, 200]`; **WarehouseStackLimit** clamp `[50, 9999]`; **WorkshopStars** clamp `[0,5]`.
- **Ascension:** Perks/PermanentHeirlooms `??=`; AscensionLevel/AscensionPoints `< 0 → 0`.
- **HeirloomItems + Ascension.PermanentHeirlooms:** nur `IsHeirloomEligible`-Produkte; HeirloomItems-Cap = `GetEffectiveHeirloomSlots(IsPremium)` (3, Premium 4).
- **Statistics-Counter** `< 0 → 0`.
- **Exploit-Schutz:** `BattlePass.IsPremium = false` und `IsPrestigePassActive = false` (eigene Restore-Pfade).
- Abgelaufene PendingDelivery entfernen.
- Firebase-PlayerGuid mit lokaler PlayerId synchronisieren.

### 31.4 Offline-Zeit-Schutz (OfflineProgressService)

Anti-Cheat gegen Systemuhr-Manipulation (`GetOfflineDuration`):
- `LastPlayedAt > now` (Uhr zurückgestellt) → **0 Offline-Earnings**.
- Offline-Dauer `< 60s` → **0 Offline-Earnings**.
- `wasCapped`-Flag für UI ("Neuer Rekord" bei Übertreffen von `MaxOfflineEarnings`, sonst Cap-Hinweis).

### 31.5 Rate-Limits

In `database.rules.json`:
- Max Schreibvorgänge pro Sekunde
- Max Read-Volumen pro Minute
- Cloud-Function-Cooldowns
- Cloud-Save-Upload aus dem Save-Loop ist client-seitig auf **min. 2 min** rate-limitiert
  (`Interlocked.CompareExchange` auf `_lastCloudUploadTicks` — race-frei, ein Thread pro Fenster).

---

## 32. Telemetrie-Events

### 32.1 Mechanik (Batching, Queue, Consent)

**Quelle: AnalyticsService.cs / AnalyticsEvents.cs.** REST via FirebaseService nach
`analytics_events/{YYYY-MM-DD}` (ein PATCH/UpdateAsync, PushId pro Event — KEIN Firebase-Analytics-SDK).

- **Queue:** `ConcurrentQueue`, `QueueCap = 500` (FIFO-Drop bei Überlauf), `MaxBatchSize = 50`, `FlushIntervalSeconds = 30`.
- **Consent (DSGVO) = Opt-IN, Default false** (`Settings.AnalyticsEnabled`), getrennt vom UMP-Ad-Consent.
  Bei Opt-Out → Timer stoppen + Queue verwerfen (keine Daten nach Opt-Out). `TrackEvent` ist No-Op wenn
  `!IsEnabled || disposed`.
- **Flush:** nur wenn `IsEnabled && !disposed && !queue.IsEmpty && Firebase.IsOnline && PlayerId != ""`. Bei
  Fehler werden Events zurück in die Queue gelegt (Cap-Re-Check). `_flushTimer` = System.Timers.Timer (AutoReset).
- **Event-Payload:** `{ eventName, timestamp (UtcNow "O"), sessionId, playerId, params, user (Snapshot User-Props) }`.
  SessionId = `Guid.NewGuid().ToString("N")[..12]`. Dispose: best-effort Flush mit `Wait(2s)`.

**Unity-Hinweis:** Implementierung über die portierte REST-Pipeline (`FirebaseAnalyticsService` als VContainer-Singleton),
nicht das native Firebase-SDK — hält die Event-Struktur 1:1 zur Avalonia-Version.

### 32.2 User-Properties (AnalyticsUserProperties, 11 Stück)

`language`, `premium`, `prestige_tier`, `ascension_level`, `player_level`, `graphics_quality`,
`days_since_install` (aus CreatedAt), `app_version`, `test_cohort` (A/B: stabiler PlayerId-Hash `*31+c`, `%2` → "a"/"b"),
`install_cohort_week` (ISO "YYYY-Www", persistiert), `player_id`.
RetentionDay-Event wird 1×/Tag gesendet (`analytics_last_retention_day`).

### 32.3 Event-Katalog (AnalyticsEvents.cs 1:1, snake_case)

| Gruppe | Events |
|--------|--------|
| Session/Retention | `session_start`, `session_end`, `retention_day`, `app_open`, `app_pause`, `app_resume` |
| Tutorial | `tutorial_step`, `tutorial_complete`, `welcome_seen` |
| Unlocks | `workshop_unlocked`, `first_order_accepted`, `first_minigame_played`, `feature_unlocked`, `level_up` |
| Progression | `prestige_done`, `ascension_done`, `rebirth_done`, `research_completed`, `building_upgraded`, `achievement_unlocked` |
| Mini-Games | `minigame_played`, `minigame_perfect`, `auto_complete_used` |
| IAP | `iap_shop_viewed`, `iap_item_viewed`, `iap_purchase_started`, `iap_purchase_success`, `iap_purchase_failed`, `iap_purchase_cancelled` |
| Ads | `ad_requested`, `ad_shown`, `ad_rewarded`, `ad_failed`, `ad_dismissed` |
| Gilden | `guild_joined`, `guild_created`, `guild_left`, `guild_boss_hit`, `guild_war_joined` |
| Economy | `order_completed`, `worker_hired`, `offline_earnings_claimed`, `daily_reward_claimed`, `lucky_spin_played` |
| Cloud-Save | `cloud_save_uploaded`, `cloud_save_downloaded`, `cloud_save_conflict` |
| Worker-Lifecycle | `worker_promoted`, `worker_aura_unlocked`, `worker_quit` |
| Co-op | `coop_order_invited`, `coop_order_accepted`, `coop_order_declined`, `coop_order_completed`, `coop_order_score_submitted` |
| Auktionen | `auction_bid_placed`, `auction_won`, `auction_lost` |
| Reputation-Shop | `reputation_shop_purchased` |
| Equipment | `equipment_dropped`, `equipment_equipped` |
| Live/Premium-Order | `live_order_expired_unstarted`, `live_order_premium_accepted`, `parallel_order_started` |
| Prestige-Cinematic | `prestige_cinematic_skipped`, `prestige_cinematic_completed` |
| Manager/Inbox | `manager_unlocked`, `notification_inbox_opened` |
| Onboarding-Funnel | `onboarding_story_skipped`, `onboarding_first_workshop_shown`, `onboarding_first_order_hinted` |
| Fehler | `error_occurred` |

**Funnel-Helper:** `TrackFunnelStep(funnel, step, name)` → Event `funnel_{funnelName}` mit `step` / `step_name`.

### 32.4 Privacy

- Analytics-Consent ist **Opt-IN (Default false)**, getrennt vom UMP-Ad-Consent (DSGVO).
- Opt-Out via Settings → sofortiges Verwerfen der Queue (keine Daten nach Opt-Out).
- UMP-Consent (DSGVO) steuert separat die Ad-Personalisierung.
- EU AI Act Transparenz-Hinweis (KI-Assets gekennzeichnet — siehe ASSETS_AI.md § 14).

---

## 33. Handwerker-Stadt-Hub-Design

### 33.1 Stadt-Layout

Die **Handwerker-Stadt** ist die zentrale 3D-Welt im Hub.unity. Sie zeigt alle 10 Werkstätten als physische Gebäude in einer **stylisierten Toon-Stadt** (siehe ASSETS_AI.md § 13).

**Anordnung (von Anfang sichtbar, gestaffeltes Unlock — Gebäude-Plätze + Bauschilder für noch
gesperrte Werkstätten):** Alle 10 Werkstätten und ihre Freischalt-Gates entsprechen exakt § 4
(gleiche Namen, gleiche Required-Level, gleiche Prestige-Gates). Prestige-exklusive Werkstätten
(Architect, GeneralContractor, MasterSmith, InnovationLab) zeigen vor dem Unlock ein Prestige-Bauschild.

```
                  [InnovationLab  Lv 750 + Diamant-Prestige]
                          |
                  [MasterSmith    Lv 500 + Platin-Prestige]
                          |
[Architect       ]   [GeneralContractor    ]
[Lv 1 + Bronze-P.]   [Lv 1 + Gold-Prestige ]
    |                |
[Contractor Lv 80]   [Roofer Lv 40]
    |                |
[Painter Lv 22  ]   [Electrician Lv 15]
    |                |
[Plumber Lv 5   ]   [Carpenter ← Start (Lv 1, frei)]
    |                |
    └─── Stadtplatz ──┘ (Meister-Hans-Statue zentral)
```

> Konsistenz-Anker: Die 10 Werkstatt-Namen, Required-Level und Prestige-Gates sind die kanonische
> Liste aus § 4. Diese Hub-Sektion legt nur die **3D-Präsentation** darüber — keine eigenen Werte.

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

2 Templates (`GuildMegaProjectTemplates`). Firebase-Pfad: `guilds/{guildId}/megaProjects/active`. Fortschritt ist **rein spendenbasiert** (kein Zeit-Tick / keine feste Wochen-Dauer) — ein Projekt ist abgeschlossen, sobald alle Material-Anforderungen erfüllt sind. **Abandonment-Sunset = 30 Tage**: Projekte älter als 30 Tage werden für Spenden geblockt.

### 34.1 Cathedral (Type 0)

**Material-Anforderungen** (`GetRequirements`):

| Material | Menge |
|----------|------:|
| luxury_furniture | 50 |
| roof_structure | 40 |
| artwork | 30 |
| smart_home | 20 |
| villa | 1 |

**Bonus bei Abschluss** (`GetReward`, permanent, einmalig pro Spieler): CraftingSpeed **+0.05 (+5%)**, AutoSellPrice **+0.10 (+10%)**, **+3 Lager-Slots**.

**3D-Visualisierung:** Cathedral wächst sichtbar in der Stadt (5 Bauphasen-Modelle gemäß ASSETS_AI.md § 12). Die 5 Bauphasen sind eine reine Präsentations-Staffelung des Spenden-Fortschritts (kein eigener Mechanik-Schritt).

### 34.2 Headquarters (HQ, Type 1)

**Material-Anforderungen** (`GetRequirements`):

| Material | Menge |
|----------|------:|
| skyscraper_frame | 80 |
| smart_home | 60 |
| bathroom_installation | 50 |
| master_blueprint | 30 |
| masterpiece_fittings | 30 |
| villa | 2 |
| skyscraper | 1 |

**Bonus bei Abschluss** (permanent, einmalig pro Spieler): CraftingSpeed **+0.10 (+10%)**, AutoSellPrice **+0.20 (+20%)**, **+5 Lager-Slots**.

**3D-Visualisierung:** Skyscraper-Wachstum, Hero-Asset mit Cloud-Polish (Rodin Gen-2.5). 5 visuelle Bauphasen über den Spenden-Fortschritt.

### 34.3 Lifecycle, Donations & Boni-Integration

**Start** (`StartProjectAsync`): nur wenn kein aktives/unfertiges Projekt; `ProjectId = Guid("N")`, `CreatedAt = UtcNow`, HMAC über `ProjectId|(int)Type|CreatedAt:O` (Salt `guild-mega-project-v1`).

**Spende** (`DonateAsync` → `DonateCoreAsync`, SemaphoreSlim-serialisiert):
- Verfügbarkeit via `WarehouseService.GetAvailable` (abzgl. Reservierungen).
- `actualCount = min(count, required − alreadyDonated)`; Inventar atomar reduzieren.
- `donationValue = CraftingService.GetSellPrice(productId) × actualCount`.
- Atomarer PATCH (`UpdateAsync`) nur auf Subpfade `contributions/{productId}` und `donations/{playerId}/...`.
- Bei Komplettierung (`IsAllRequirementsMet`) → `completedAt` setzen + `ClaimRewardInternal`.
- Rollback (Material wieder einlagern) bei PATCH-Fehler.

**Belohnung** (`ClaimRewardInternal`/`TryClaimRewardAsync`): permanenter Bonus pro Spieler einmalig, Idempotenz via `state.ClaimedGuildProjectIds`. Addiert auf `GuildMembership`: `MegaProjectCraftingSpeedBonus`, `MegaProjectAutoSellPriceBonus`, `MegaProjectBonusWarehouseSlots`; `CompletedMegaProjectTypes` (List<int>) ergänzt.

**Boni-Integration:** CraftingSpeedBonus → `CraftingService.StartCrafting`; AutoSellPriceBonus → Marktpreis im Overflow-Auto-Sell; BonusWarehouseSlots → `WarehouseService.EffectiveSlotCount`.

**Top-Spender-Leaderboard** (`GuildMegaProjectDonation`): playerName, totalValue (decimal), itemCount, lastDonatedAt.

**Mega-Projekt-Donations:**
- Pro Spieler trackbar (Telemetrie-Event `guild_mega_project_donation`: project_id, project_type, item_id, count, donation_value)
- HMAC-signiert (Salt `guild-mega-project-v1`; signiert: ProjectId, (int)Type, CreatedAt — Contributions/Donations/CompletedAt via PATCH)
- Score-Visualisierung in Guild.unity
- Top-Contributors mit Avatar-Frame-Cosmetic

---

## 35. Future / Phase 2

> **Grundsatz:** Die Unity-Beta bildet das produktive Avalonia-Original mechanisch 1:1 ab. Die folgenden
> Punkte sind **Post-MVP** und unterteilen sich in (A) reine Präsentations-/Plattform-Erweiterungen, die das
> Gameplay nicht verändern, und (B) inhaltliche Erweiterungen, die **über das Original hinausgehen** —
> diese werden erst nach der Cutover-Entscheidung erwogen und ändern Mechanik/Balancing, dürfen also NICHT
> Teil des faithful Ports sein.

**(A) Präsentation & Plattform (kein Mechanik-Eingriff):**

- **iOS-Launch** (Architektur via Unity Cross-Platform vorbereitet — Entscheidung nach Beta-Erfolg)
- **Day/Night-Cycle** (rein visuell, siehe § 33.2)
- **Live-Wetter** (API-gestützte Wetter-Visuals; die saisonalen Income-Multiplikatoren bleiben unverändert
  zeitbasiert wie im Original, siehe § 22.B.5)
- **Worker-Voice-Lines** (in ASSETS_AI.md § 8 / Phase 2 geplant — kann ins MVP, wenn Audio-Budget reicht)
- **Replay-Highlights** (Prestige-Cinematic-Clips zum Teilen — Präsentation, kein Balancing)
- **Cosmetic-DLC** (rein kosmetisch, keine Gameplay-Boni)
- **Cross-Save Avalonia ↔ Unity** (nur falls Hard-Cutover entschieden wird — Save-Schema-Brücke)

**(B) Inhaltliche Erweiterungen (gehen über das Original hinaus — NICHT im faithful Port):**

- **Live-PvP via Photon Fusion** (Echtzeit-Klan-Matches) — das Original kennt nur **asynchrone** Gilden-Inhalte
  (Co-op-Aufträge, Auktionen, Mega-Projekte). Live-PvP ist neue Mechanik, daher strikt Phase 2.
- **Kriegssaison-Ligen erweitern** (über die Original-Kriegssaison hinaus)
- **Mehr Saisonale Live-Events** — das Original liefert **4 Saison-Events pro Jahr** (§ 23) + 8 Live-Event-Templates
  (§ 22). Zusätzliche Events sind neuer Content.
- **Ascension-Perks erweitern** — das Original hat exakt **6 Perks × 3 Level (54 AP)** (§ 1 / Ascension-Sektion).
  Eine Erweiterung auf 10+ ist neues Balancing.
- **Master-Tools erweitern** — das Original hat exakt **12 Artefakte** (§ 14). Eine Erweiterung auf 18+ ist neuer Content.

---

## 36. Konsistenz mit ASSETS_AI.md

### 36.1 Persona-Anker

- **Meister Hans** — vorgefertigte **ElevenLabs-Standard-Voice (KEIN Voice-Cloning, kein eigener Sprecher)**,
  ElevenLabs Multilingual v2 mit EINER Voice-ID über alle 6 Sprachen (konsistente Persona). ~250 Voice-Lines
  × 6 Sprachen = ~1500 Voice-Files (siehe ASSETS_AI.md § 11.3). Kommerzielle Rechte via Pro-Sub, keine
  Echtperson abgebildet → keine Deepfake-/EU-AI-Act-Relevanz.
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
