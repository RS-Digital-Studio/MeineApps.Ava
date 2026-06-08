# ORIGINAL_WERTE — Verbindliche Referenz (Single Source of Truth)

> ⚠️ **STATUS (8.6.2026): Formel-Referenz, nicht mehr global verbindlich.** Die unten formulierte
> „1:1 dasselbe Spiel"-Doktrin ist **abgelöst** — die Unity-Version wird voll neu als 3D-Idle-Tycoon konzipiert
> (**[3D_IDLE_GAME_PLAN.md](3D_IDLE_GAME_PLAN.md)**). Diese Werte bleiben die **verbindliche Quelle für die
> wiederverwendeten Formeln** (Income-Soft-Cap, Offline-Staffelung, Auto-Produktion u. a. — GDD §12); für die
> **nicht** übernommenen Sim-Systeme sind sie **Reaktivierungs-Referenz** (GDD §15), nicht mehr das Soll.

> **Zweck:** Diese Datei spiegelt die echten Mechaniken, Formeln und Balancing-Werte des produktiven **Avalonia-HandwerkerImperium** — direkt aus dem Quellcode extrahiert. Sie ist die **alleinige Wahrheit**, gegen die der Unity-Plan (PLAN/DESIGN/ARCHITECTURE/...) ausgerichtet wird.
>
> **Grundsatz:** HandwerkerImperium-Unity ist 1:1 dasselbe Spiel wie das Original — gleiche Mechanik, gleiche Formeln, gleiche Werte — nur besser und in 3D. Jede Abweichung eines Plan-Dokuments von dieser Referenz ist ein Fehler und auf diese Werte zu korrigieren. "Besser/3D" betrifft ausschliesslich Praesentation (Grafik, Hub, Cinematics, UX), niemals die hier dokumentierte Logik.
>
> Werte ohne Code-Fund sind im Text als "(nicht im Code gefunden)" markiert und vor Verwendung im Code zu verifizieren.

## Inhalt

- 01_workshops-workers
- 02_core-economy
- 03_orders-crafting
- 04_prestige-meta
- 05_guild
- 06_liveops
- 07_engagement
- 08_infra-ui

---

# 01 — Werkstaetten, Arbeiter, Manager, Gebaeude, Spezialisierung

> Verbindliche Wahrheit aus dem produktiven Avalonia-Code. Jeder Wert ist exakt aus den
> angegebenen Quelldateien extrahiert. Berechnete Werte sind als Formel angegeben.
> Code-Root: `F:/Meine_Apps_Ava/src/Apps/HandwerkerImperium/HandwerkerImperium.Shared`

---

## 1. WorkshopType (10 Typen)

Quelle: `Models/Enums/WorkshopType.cs` (`WorkshopType`-Enum + `WorkshopTypeExtensions`)

Enum-Reihenfolge / numerische Werte:

| Enum | Wert | Beschreibung (Doc-Kommentar) |
|------|------|------------------------------|
| Carpenter | 0 | Woodworking — Sawing, Planing, Assembly |
| Plumber | 1 | Plumbing — Pipe puzzles, Fittings |
| Electrician | 2 | Electrical — Wiring, Circuits |
| Painter | 3 | Painting — Brush strokes, Color mixing |
| Roofer | 4 | Roofing — Tile laying, Measurements |
| Contractor | 5 | General Contractor — Large projects, Management |
| Architect | 6 | Architecture — Design, Planning (Prestige 1 exclusive) |
| GeneralContractor | 7 | General Contractor Plus — Full-service (Prestige 3 exclusive) |
| MasterSmith | 8 | Meisterschmiede — Schmiedekunst (Prestige 4 exclusive) |
| InnovationLab | 9 | Innovationslabor — Erfindungen, Prototypen (Prestige 5 exclusive) |

### 1.1 Pro-Typ-Werte (alle Extension-Methoden)

| Typ | UnlockLevel (`GetUnlockLevel`) | RequiredPrestige (`GetRequiredPrestige`) | UnlockCost EUR (`GetUnlockCost`) | BaseIncomeMultiplier (`GetBaseIncomeMultiplier`) | ColorHex (`GetColorHex`) |
|-----|------|------|------|------|------|
| Carpenter | 1 | 0 | 0 | 1.0 | #A0522D (Sienna) |
| Plumber | 5 | 0 | 5.000 | 1.5 | #0E7490 (Teal) |
| Electrician | 15 | 0 | 250.000 | 2.0 | #F97316 (Orange) |
| Painter | 22 | 0 | 2.500.000 | 2.5 | #EC4899 (Pink) |
| Roofer | 40 | 0 | 10.000.000 | 3.0 | #DC2626 (Rot) |
| Contractor | 80 | 0 | 100.000.000 | 4.0 | #EA580C (Craft-Orange) |
| Architect | 1 | 1 | 2.500.000.000 | 5.0 | #78716C (Stone-Grau) |
| GeneralContractor | 1 | 3 | 25.000.000.000 | 7.0 | #FFD700 (Gold) |
| MasterSmith | 500 | 4 | 30.000.000.000 | 3.0 | #D4A373 (Kupfer-Orange) |
| InnovationLab | 750 | 5 | 50.000.000.000 | 5.0 | #6A5ACD (Violett) |

Hinweise:
- `IsPrestigeExclusive` = `GetRequiredPrestige() > 0` (Architect, GeneralContractor, MasterSmith, InnovationLab).
- `Workshop.Create(type)` setzt `IsUnlocked = true` nur fuer Carpenter, alle anderen `false`.
- Carpenter ist Default-Workshop; Worker werden bei `CreateNew` auf `Carpenter` zugewiesen (Gotcha, App-CLAUDE.md).

### 1.2 Material-Affinitaet pro Workshop

Quelle: WorkshopType selbst hat **keine** eigene `MaterialAffinity`-Methode. Die im Auftrag erwaehnte
"MaterialAffinity je WorkshopType" existiert im Code nur als **Worker-Eigenschaft** (siehe Abschnitt 3.6)
bzw. als **Produkt-zu-Affinitaet-Mapping** in `MaterialAffinityExtensions.GetMaterialAffinity(productId)`
(Quelle: `Models/Enums/MaterialAffinity.cs`). Die Zuordnung erfolgt also ueber das gecraftete Produkt,
NICHT ueber den Workshop-Typ. Produkt→Affinitaet-Mapping:

| Affinity | Produkt-IDs |
|----------|-------------|
| Wood (=1) | planks, furniture, luxury_furniture, concrete_foundation |
| Metal (=2) | pipes, plumbing_system, bathroom_installation, fittings, master_fittings, masterpiece_fittings |
| Stone (=3) | concrete, skyscraper_frame, roof_tiles, roofing_system, roof_structure |
| Art (=4) | paint_mix, wall_design, artwork, blueprint, framework, master_blueprint, contract, contract_complex, general_contract |
| Tech (=5) | cables, circuit, smart_home, prototype, innovation, patent |
| None (=0) | alle uebrigen (T4-Items: gemischt, kein einzelner Match) |

### 1.3 Workshop-Spezialeffekte (Auto-Produktion)

Quelle: `Models/GameBalanceConstants.cs`
- Standard-Workshops: `AutoProductionIntervalSeconds = 180`
- InnovationLab: `AutoProductionInnovationLabInterval = 120`
- MasterSmith: `AutoProductionMasterSmithInterval = 60`
- Auto-Produktion freigeschaltet ab Workshop-Level `AutoProductionUnlockLevel = 50`.

---

## 2. Workshop-Mechanik (Level, Kosten, Einkommen, Slots, Milestones, Rebirth)

Quelle: `Models/Workshop.cs`, `Models/WorkshopFormulas.cs`, `Models/GameBalanceConstants.cs`

### 2.1 Level & MaxLevel

- `Workshop.Level` Start = 1.
- `Workshop.MaxLevel = GameBalanceConstants.WorkshopMaxLevel = 1000`.
- `CanUpgrade` = `Level < MaxLevel`.

### 2.2 Basis-Einkommen pro Worker pro Sekunde

Quelle: `WorkshopFormulas.CalculateBaseIncomePerWorker(level, type)`

```
BaseIncomePerWorker = IncomeBaseMultiplier^(Level-1) * TypeMultiplier * MilestoneMultiplier(Level)
```
- `IncomeBaseMultiplier = 1.02` (`GameBalanceConstants.IncomeBaseMultiplier`)
- `TypeMultiplier` = `WorkshopType.GetBaseIncomeMultiplier()` (siehe 1.1)
- `MilestoneMultiplier` = kumulativ (siehe 2.6)

### 2.3 Brutto-Einkommen pro Sekunde (GrossIncome)

Quelle: `WorkshopFormulas.CalculateGrossIncome(...)` + `Workshop.GrossIncomePerSecond`

```
WENN workers.Count == 0  →  GrossIncome = 0    (KEIN Einkommen ohne Worker)

totalIncome = Σ über alle Worker:
    BaseIncomePerWorker
    * worker.EffectiveEfficiency
    * LevelFitFactor(Level, worker.Tier.LevelResistance, levelResistanceBonus)

// Aura-Bonus (additiv summiert über alle Worker, dann gedeckelt):
auraBonus = Σ worker.Tier.GetAuraBonus()
WENN auraBonus > 0:  totalIncome *= (1 + min(auraBonus, MaxAuraBonus=0.50))

// Rebirth-Einkommens-Bonus:
WENN rebirthIncomeBonus > 0:  totalIncome *= (1 + rebirthIncomeBonus)
```

Zusaetzlich in `Workshop.GrossIncomePerSecond` (NACH CalculateGrossIncome):
- Quality-Spez: wenn `EfficiencyModifier != 0` → `gross *= (1 + 0.20)`
- Efficiency-Spez (+0.30) / Economy-Spez (-0.05): wenn `IncomeModifier != 0` → `gross *= (1 + IncomeModifier)`

`MaxAuraBonus = 0.50` (50% Cap) — Quelle: `GameBalanceConstants.MaxAuraBonus`.

### 2.4 Level-Fit-Faktor (Tier-Anforderungsmalus)

Quelle: `WorkshopFormulas.CalculateLevelFitFactor(workshopLevel, tierResistance, levelResistanceBonus)`

```
WENN workshopLevel <= 30:  return 1.0
steps          = workshopLevel / LevelPenaltyStep(=30)     (Integer-Division)
basePenalty    = steps * LevelPenaltyPerStep(=0.02)
totalResistance= min(1.0, tierResistance + levelResistanceBonus)
adjustedPenalty= basePenalty * (1 - totalResistance)
return max(MinLevelFitFactor(=0.20), 1 - adjustedPenalty)
```
Konstanten (`GameBalanceConstants`): `LevelPenaltyStep = 30`, `LevelPenaltyPerStep = 0.02`, `MinLevelFitFactor = 0.20`.

### 2.5 Upgrade-Kosten-Formel

Quelle: `WorkshopFormulas.CalculateRawLevelCost(level)` + `CalculateUpgradeCost(...)`
Konstanten (`GameBalanceConstants`): `UpgradeCostLevel1 = 100`, `UpgradeCostBase = 200`,
`UpgradeCostExponent = 1.07`, `UpgradeCostReducedExponent = 1.06`, `PrestigeDiscountCap = 0.50`.

```
RawLevelCost(level):
    WENN level == 1:        return 100
    WENN level > 500:
        rawAt500 = 200 * 1.07^499
        rawCost  = rawAt500 * 1.06^(level-500)
        (Overflow → decimal.MaxValue)
    SONST (2 <= level <= 500):
        raw = 200 * 1.07^(level-1)
        (Overflow → decimal.MaxValue)
```
WICHTIG: Der Exponent-Knick ist bei Level 500/501. Bis Lv500 Exponent 1.07, ab Lv501 wird die
Kette mit 1.06 fortgesetzt (ausgehend vom 1.07^499-Wert bei Lv500).

UpgradeCost mit Rabatten (`CalculateUpgradeCost`):
```
WENN level >= MaxLevel(1000):  return 0
baseCost = RawLevelCost(level)
WENN rebirthDiscount  > 0:  baseCost *= (1 - rebirthDiscount)
WENN prestigeDiscount > 0:  baseCost *= (1 - min(prestigeDiscount, 0.50))
WENN vipReduction     > 0:  baseCost *= (1 - vipReduction)
```
Rabatt-Quellen: `RebirthUpgradeDiscount` (Rebirth-Sterne), `UpgradeDiscount` (Prestige-Shop, gecappt 0.50),
`VipCostReduction` (extern, 0.0–0.10 laut Doc).

Bulk-Upgrade (`CalculateBulkUpgradeCost`): Summe der RawLevelCosts × kombinierter Discount-Faktor,
gecappt auf `MaxLevel - currentLevel` Schritte.
Max-Affordable (`CalculateMaxAffordableUpgrades`): inkrementelle Kette (1 Math.Pow initial, danach
`*expNormal` bzw. `*expReduced`, Rekalibrierung via Math.Pow bei Level==501).

### 2.6 Milestone-Multiplikatoren (16 Stufen)

Quelle: `GameBalanceConstants.MilestoneMultipliers` (array von (Level, Multiplier))
`CalculateMilestoneMultiplier(level)` = Produkt aller Multiplikatoren mit `milestoneLevel <= level` (kumulativ).

| # | Level | Multiplier |
|---|-------|-----------|
| 1 | 25 | 1.15 |
| 2 | 50 | 1.30 |
| 3 | 75 | 1.30 |
| 4 | 100 | 1.45 |
| 5 | 150 | 1.60 |
| 6 | 200 | 1.45 |
| 7 | 225 | 1.30 |
| 8 | 250 | 1.60 |
| 9 | 350 | 1.60 |
| 10 | 400 | 1.60 |
| 11 | 500 | 2.00 |
| 12 | 600 | 1.70 |
| 13 | 650 | 1.65 |
| 14 | 750 | 1.60 |
| 15 | 900 | 1.60 |
| 16 | 1000 | 3.00 |

`IsMilestoneLevel(level)` = level ist exakt einer dieser Levels.
`GetMilestoneMultiplierForLevel(level)` = Einzel-Multiplikator des exakten Levels (sonst 1.0).
Kumulativer Multiplikator bei Level 1000 = Produkt aller 16 Milestones = **~1500x**
(1.15·1.30·1.30·1.45·1.60·1.45·1.30·1.60·1.60·1.60·2.00·1.70·1.65·1.60·1.60·3.00 = 1499.86).
Der Code-Doc-Kommentar (`WorkshopFormulas.cs:71`) nennt veraltet "~921x" — bei der 1:1-Portierung
gilt der reale Produktwert ~1500x, nicht der Kommentar.

### 2.7 MaxWorkers-Formel

Quelle: `Models/Workshop.cs`

```
BaseMaxWorkers = min(20, 1 + (Level-1)/50)     (Integer-Division; +1 alle 50 Level, Cap 20)

MaxWorkers = max(1, BaseMaxWorkers
                    + ExtraWorkerSlots                         (Buildings/Research, extern)
                    + AdBonusWorkerSlots                       (Rewarded Ads, persistent, max 3)
                    + RebirthExtraWorkers                      (0..3, siehe 2.9)
                    + (WorkshopSpecialization?.WorkerCapacityModifier ?? 0))   (Efficiency = -1)
```
Konstanten (`GameBalanceConstants`): `WorkerSlotInterval = 50`, `WorkerSlotMax = 20`,
`MaxAdBonusWorkerSlots = 3` (auch `Workshop.MaxAdBonusWorkerSlots = 3`).
`CanHireWorker` = `Workers.Count < MaxWorkers`.

### 2.8 Laufende Kosten

Quelle: `Models/Workshop.cs` + `WorkshopFormulas`
- Miete/h (`CalculateRentPerHour`): `Level <= 100` → `RentBaseLinear(10) * Level`;
  `Level > 100` → `RentBaseExponential(1000) * RentExponent(1.005)^(Level-100)`.
- Material/h (`CalculateMaterialCostPerHour`): `Level <= 100` → `MaterialCostBaseLinear(5) * Level * TypeMult`;
  `Level > 100` → `MaterialCostBaseExponential(500) * MaterialCostExponent(1.005)^(Level-100) * TypeMult`.
- Loehne/h (`TotalWagesPerHour`): Σ `WagePerHour` ueber alle Worker mit `!IsResting`.
- `TotalCostsPerHour` = (Rent + Material + Wages); Spez-Modifier: Quality `*1.15`, Economy `*0.75` (`*(1+CostModifier)`); `max(0, …)`.
- `NetIncomePerSecond` (nur Display) = `GrossIncomePerSecond - TotalCostsPerHour/3600`.

### 2.9 Hire-Worker-Kosten (pro naechstem Worker)

Quelle: `WorkshopFormulas.CalculateHireWorkerCost(currentWorkerCount)`
```
HireWorkerCost = HireWorkerCostBase(50) * HireWorkerCostExponent(1.5)^currentWorkerCount
```

### 2.10 Workshop-Rebirth (0–5 Sterne)

Quelle: `Models/Workshop.cs` (RebirthStars 0-5, extern aus `GameState.WorkshopStars`) +
`GameBalanceConstants.RebirthIncomeBonuses / RebirthUpgradeDiscounts / RebirthExtraWorkers`

- `RebirthIncomeBonuses` (Index = Sterne-1): `[0.15, 0.35, 0.60, 1.00, 1.50]`
- `RebirthUpgradeDiscounts` (Index = Sterne-1): `[0.05, 0.10, 0.15, 0.20, 0.25]`
- `RebirthExtraWorkers` (Index = Sterne, 6 Elemente): `[0, 1, 1, 2, 2, 3]`

| Sterne | IncomeBonus | UpgradeDiscount | ExtraWorker |
|--------|-------------|-----------------|-------------|
| 0 | 0% | 0% | 0 |
| 1 | +15% | -5% | +1 |
| 2 | +35% | -10% | +1 |
| 3 | +60% | -15% | +2 |
| 4 | +100% | -20% | +2 |
| 5 | +150% | -25% | +3 |

**Rebirth-Kosten pro nächstem Stern** (Quelle: `Services/RebirthService.cs` `RebirthCosts` — Tupel
`(Goldschrauben, Geld-Prozent des aktuellen Geldes)`):

| Stern | Goldschrauben | Geld-Anteil |
|-------|---------------|-------------|
| 1 | 50 | 10% |
| 2 | 125 | 15% |
| 3 | 250 | 20% |
| 4 | 200 | 25% |
| 5 | 400 | 30% |

`moneyCost = state.Money * moneyPercent` wird in `DoRebirth` ZUSÄTZLICH zu den Goldschrauben fällig.
Die früher hier zitierten 100/250/500/500/1000 GS (ohne Geld-Anteil) waren veraltet.

---

## 3. Worker-System

Quelle: `Models/Worker.cs`, `Models/Enums/WorkerTier.cs`, `Models/Enums/WorkerPersonality.cs`,
`Models/Enums/TrainingType.cs`, `Models/Enums/MaterialAffinity.cs`, `Services/WorkerService.cs`,
`Models/GameBalanceConstants.cs`

### 3.1 WorkerTier (10 Tiers)

Quelle: `Models/Enums/WorkerTier.cs` (`WorkerTier`-Enum + `WorkerTierExtensions`)
Enum-Werte: F=0, E=1, D=2, C=3, B=4, A=5, S=6, SS=7, SSS=8, Legendary=9

| Tier | MinEff (`GetMinEfficiency`) | MaxEff (`GetMaxEfficiency`) | Wage/h (`GetWagePerHour`) | BaseHiringCost EUR (`GetBaseHiringCost`) | UnlockLevel (`GetUnlockLevel`) | LevelResistance (`GetLevelResistance`) | AuraBonus (`GetAuraBonus`) | HiringScrewCost (`GetHiringScrewCost`) | ColorKey |
|------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| F | 0.30 | 0.50 | 5 | 50 | 1 | 0.00 | 0% | 0 | #9E9E9E |
| E | 0.50 | 0.80 | 9 | 200 | 1 | 0.10 | 0% | 0 | #4CAF50 |
| D | 0.75 | 1.25 | 16 | 1.000 | 8 | 0.20 | 0% | 0 | #2196F3 |
| C | 1.10 | 1.90 | 28 | 5.000 | 15 | 0.30 | 0% | 0 | #9C27B0 |
| B | 1.70 | 2.80 | 50 | 25.000 | 25 | 0.40 | 0% | 0 | #FFC107 |
| A | 2.50 | 4.20 | 90 | 100.000 | 35 | 0.55 | 0% | 20 | #F44336 |
| S | 3.80 | 6.00 | 160 | 500.000 | 45 | 0.70 | +5% | 60 | #FF9800 |
| SS | 5.50 | 9.00 | 280 | 2.000.000 | 100 | 0.80 | +8% | 120 | #E040FB |
| SSS | 8.50 | 14.00 | 500 | 10.000.000 | 250 | 0.90 | +12% | 300 | #7C4DFF |
| Legendary | 13.00 | 22.00 | 900 | 50.000.000 | 500 | 1.00 | +20% | 750 | #FFD700 |

- S/SS/SSS/Legendary brauchen ZUSAETZLICH Research-Unlock `mgmt_10` (UnlocksSTierWorkers) —
  Quelle: `Worker.GetAvailableTiers(...)` filtert `tier >= S && !hasSTierResearch`.
- `GetHiringCost(playerLevel)` = `round(BaseHiringCost * (1 + max(0, playerLevel-1) * 0.02))`
  (also +2% pro Spielerlevel ueber 1; Lv50 = 1.98x ≈ 2.0x, Lv100 = 2.98x ≈ 3.0x).
- `GetLocalizationKey` = `"Tier{tier}"`.

### 3.2 Talent-Range je Tier (beim Hiring gerollt)

Quelle: `Worker.CreateForTier(tier)` — `random.Next(min, max)` (max exklusiv):

| Tier | Talent-Range |
|------|--------------|
| F | 1–2 (`Next(1,3)`) |
| E | 1–3 (`Next(1,4)`) |
| D | 2–3 (`Next(2,4)`) |
| C | 2–4 (`Next(2,5)`) |
| B | 3–4 (`Next(3,5)`) |
| A | 3–5 (`Next(3,6)`) |
| S | 4–5 (`Next(4,6)`) |
| SS | 4–5 (`Next(4,6)`) |
| SSS | 5 (fix) |
| Legendary | 5 (fix) |

`Worker.Talent` Default = 3 (Plain-Model). Talent beeinflusst MaxEff und EffectiveEfficiency (siehe 3.4).

### 3.3 Spezialisierungs-Chance je Tier (beim Hiring)

Quelle: `Worker.CreateForTier(tier)` — `specChance`, dann zufaelliger WorkshopType:

| Tier | Spez-Chance |
|------|-------------|
| F, E | 0.40 (40%) |
| D, C | 0.50 (50%) |
| B, A | 0.65 (65%) |
| S, SS, SSS, Legendary | 0.85 (85%) |

Bei Treffer: zufaelliger `WorkshopType` aus allen 10 (gleichverteilt). `Worker.Specialization` = bevorzugter
Workshop; +15% Effizienz wenn dort eingesetzt (siehe 3.4).

### 3.4 EffectiveEfficiency-Formel

Quelle: `Worker.EffectiveEfficiency` (gecacht via Input-Hash)

```
WENN IsResting ODER IsTraining:  return 0

baseEff         = Efficiency                              (Basis innerhalb Tier-Range, beim Hiring gerollt)
xpBonus         = 1 + ExperienceLevel * 0.03             (+3% pro Erfahrungslevel)
moodFactor      = GetMoodFactor()                        (siehe unten)
fatigueFactor   = GetFatigueFactor()                     (siehe unten)
specBonus       = GetSpecializationBonus()               (0, oder 0.15 + Personality-Spez-Bonus)
personalityMult = Personality.GetEfficiencyMultiplier()  (siehe 3.5)
equipBonus      = EquippedItem?.EfficiencyBonus ?? 0
talentBonus     = 1 + (Talent-1) * 0.05                  (1★=1.00x, 3★=1.10x, 5★=1.20x)

result = max(0, baseEff * xpBonus * moodFactor * fatigueFactor
                * (1 + specBonus + equipBonus) * personalityMult * talentBonus)
```

`MaxEfficiency` (anzeige) = `Tier.GetMaxEfficiency() * (1 + ExpLvl*0.03) * (1 + (Talent-1)*0.05)`.

Base-Efficiency beim Hiring: `minEff + (maxEff-minEff) * random.NextDouble()`, gerundet auf 3 Nachkommastellen.

GetMoodFactor (Quelle `Worker.GetMoodFactor`):
```
Mood >= 80:  1.0 + (Mood-80)/200       (Mood 100 = 1.1x, Mood 80 = 1.0x)
Mood >= 50:  0.8 + (Mood-50)/150       (Mood 50 = 0.8x)
sonst:       0.5 + Mood/100            (Mood 0 = 0.5x)
```
GetFatigueFactor (Quelle `Worker.GetFatigueFactor`):
```
Fatigue <= 0:    1.0
Fatigue >= 100:  0.5
sonst:           1.0 - Fatigue/200     (Fatigue 50 = 0.85x)
```
GetSpecializationBonus (Quelle `Worker.GetSpecializationBonus`):
```
WENN Specialization == null ODER AssignedWorkshop == null:  0
WENN Specialization != AssignedWorkshop:                    0
SONST:  0.15 + Personality.GetSpecializationBonus()         (Specialist: +0.15 → total 0.30)
```

### 3.5 WorkerPersonality (6 Persoenlichkeiten — alle 5 Effekt-Achsen + Preis-Mult)

Quelle: `Models/Enums/WorkerPersonality.cs` (`WorkerPersonalityExtensions`) + `Worker.CalculateMarketPrice`
Enum-Werte: Steady=0, Perfectionist=1, Cheerful=2, Ambitious=3, Relaxed=4, Specialist=5
Beim Hiring gleichverteilt gerollt (`random.Next(0,6)`).

| Personality | EfficiencyMult (`GetEfficiencyMultiplier`) | MoodDecayMult (`GetMoodDecayMultiplier`) | FatigueMult (`GetFatigueMultiplier`) | XpMult (`GetXpMultiplier`) | SpecBonus (`GetSpecializationBonus`) | Preis-Mult (`CalculateMarketPrice`) |
|-------------|------|------|------|------|------|------|
| Steady | 1.00 | 1.0 | 1.0 | 1.0 | 0.0 | 1.00 |
| Perfectionist | 1.20 | 1.5 | 1.0 | 1.0 | 0.0 | 1.20 |
| Cheerful | 0.90 | 0.5 | 1.0 | 1.0 | 0.0 | 1.05 |
| Ambitious | 1.00 | 1.0 | 1.25 | 1.25 | 0.0 | 1.10 |
| Relaxed | 0.85 | 1.0 | 0.70 | 1.0 | 0.0 | 0.90 |
| Specialist | 1.00 | 1.0 | 1.0 | 1.0 | 0.15 | 1.15 |

- `GetLocalizationKey` = `"Person{personality}"`. Icon-Keys: Steady=ShieldHalfFull, Perfectionist=StarFourPoints,
  Cheerful=EmoticonHappy, Ambitious=RocketLaunch, Relaxed=WeatherSunset, Specialist=Wrench.
- Spezial: Specialist erleidet KEINEN Mood-Hit (-5) beim Transfer (Quelle `WorkerService.TransferWorker`).

### 3.6 MaterialAffinity (Worker)

Quelle: `Models/Enums/MaterialAffinity.cs`, `Worker.CreateForTier`
- Enum: None=0, Wood=1, Metal=2, Stone=3, Art=4, Tech=5.
- Beim Hiring gleichverteilt gerollt aus 1..5 (`(MaterialAffinity)random.Next(1,6)` — None wird NICHT gerollt).
- Match mit Output-Produkt-Affinitaet gibt +20% Crafting-Speed (anteilig pro Worker; via CraftingService,
  nicht in den gelesenen Dateien). Produkt→Affinitaet-Mapping siehe 1.2.

### 3.7 Experience-Level & XP

Quelle: `Models/Worker.cs` + `GameBalanceConstants`
- `ExperienceLevel` 1–10 (Start 1, Cap 10 — Effizienz-Training stoppt bei Lvl 10).
- `XpForNextLevel = ExperienceLevel * 200` (`XpPerLevelMultiplier = 200`).
- `TrainingXpPerHour = 50`.
- Passiver Arbeits-XP: 25% der Trainingsrate = 12.5 XP/h × `Personality.GetXpMultiplier()`
  (Quelle `WorkerService.UpdateWorkingLockHeld`). Akkumulator `WorkingXpAccumulator` (fraktional).
- XP-Akkumulatoren persistiert: `WorkingXpAccumulator`, `TrainingXpAccumulator`.
- Level-Up-Effekt (sowohl Arbeit als Training): `Efficiency = min(tierMax, Efficiency + (tierMax-tierMin) * 0.05)`
  (+5% des Tier-Range-Spans pro Level-Up).
- `EfficiencyBonusPerLevel = 0.03` (XP-Bonus), `EfficiencyBonusPerTalent = 0.05` (Talent-Bonus).

### 3.8 Mood & Fatigue (Decay-Raten + Kurven)

Quelle: `Models/Worker.cs`, `Models/GameBalanceConstants.cs`, `Services/WorkerService.cs`
- Start-Mood = 80 (`WorkerInitialMood`). Mood-Bereich 0–100.
- `MoodDecayPerHour` (Basis 3) = `3 * Personality.GetMoodDecayMultiplier() * (1 - MoraleBonus)`.
- `FatiguePerHour` (Basis 12.5) = `12.5 * Personality.GetFatigueMultiplier() * (1 - EnduranceBonus)`.
- `RestHoursNeeded = 4`.
- Schwellen (`GameBalanceConstants`): Happy 80, Neutral 50, Critical 20; FatigueExhausted 100.
- Status-Flags: `IsTired` = Fatigue>=100, `IsUnhappy` = Mood<50, `WillQuit` = Mood<20,
  `IsWorking` = !IsResting && !IsTraining && AssignedWorkshop != null.

Arbeiten (`UpdateWorkingLockHeld`):
- moodDecay = MoodDecayPerHour, reduziert durch (in dieser Reihenfolge, multiplikativ):
  Prestige-Shop MoodDecayReduction; Manager-MoodBoost `*(1 - min(bonus, 0.50))`; Gilden-FatigueReduction;
  Equipment-MoodBonus.
- passiveMoodRecovery = Canteen `MoodRecoveryPerHour`. netMoodChange = moodDecay - passiveMoodRecovery
  (kann Mood erhoehen wenn negativ).
- fatigueRate = FatiguePerHour, reduziert durch Gilden-FatigueReduction + Equipment-FatigueReduction.
- Auto-Rest bei Fatigue >= 100.
- Passiver XP-Gewinn (siehe 3.7).

Ruhen (`UpdateResting`):
- restMultiplier = `1 + Canteen.RestTimeReduction`.
- fatigueRecovery = `(100 / RestHoursNeeded) * deltaHours * restMultiplier`, +Equipment-FatigueReduction.
- moodRecovery = `(1 + Canteen.MoodRecoveryPerHour) * (1 + Equipment.MoodBonus) * deltaHours`.
- Auto-Ende bei Fatigue <= 0 → Auto-Resume des gemerkten `ResumeTrainingType` (wenn nicht abgeschlossen).

### 3.9 Quit-Mechanik

Quelle: `WorkerService.UpdateWorkerStates`
```
WENN WillQuit (Mood < 20):
    WENN QuitDeadline == null:  QuitDeadline = UtcNow + 24h; WorkerMoodWarning-Event
    SONST WENN UtcNow >= QuitDeadline:  Worker entfernen (TotalWorkersFired++, WorkerQuit-Event + Analytics)
SONST:  QuitDeadline = null     (Erholung > 20 reset)
```
GiveBonus (`WorkerService.GiveBonus`): kostet 8h Lohn (`WagePerHour * 8`), Mood `+30` (max 100),
`QuitDeadline = null`.

### 3.10 Training (3 Typen, Mechanik + Caps)

Quelle: `Services/WorkerService.cs`, `Models/Enums/TrainingType.cs`, `Models/Worker.cs`
- Enum: Efficiency=0, Endurance=1, Morale=2.
- `TrainingCostPerHour = 2 * WagePerHour` (`TrainingCostMultiplier = 2`). Pro Tick abgezogen; bei Nicht-Leistbar
  Training gestoppt.
- Training-Speed-Multiplikator = `TrainingCenter.TrainingSpeedMultiplier * (1 + GildenTrainingSpeedBonus + ManagerTrainingBonus)`.
- Training erhoeht Fatigue mit halber Arbeitsrate (`FatiguePerHour * 0.5`, minus Equipment-FatigueReduction).
- Auto-Rest bei Fatigue >= 100 → `ResumeTrainingType` gemerkt.

| Training | Effekt pro Stunde | Cap / Stopp-Bedingung |
|----------|-------------------|------------------------|
| Efficiency | XP `+= 50 * Personality.XpMult * speedMult`; bei Level-Up Efficiency `+= (tierMax-tierMin)*0.05` | nur bis `ExperienceLevel < 10` (StartTraining blockt bei >=10) |
| Endurance | `EnduranceBonus += 0.05 * deltaHours * speedMult` | Cap 0.5 (=50% Fatigue-Reduktion); Auto-Stopp bei 0.5 |
| Morale | `MoraleBonus += 0.05 * deltaHours * speedMult` | Cap 0.5 (=50% MoodDecay-Reduktion); Auto-Stopp bei 0.5 |

StartTraining blockt wenn: bereits trainiert/ruht; Efficiency & ExpLvl>=10; Endurance & EnduranceBonus>=0.5;
Morale & MoraleBonus>=0.5.

### 3.11 Praktikanten (Intern-System, F→E-Promotion)

Quelle: `Services/WorkerService.cs` (HireIntern, PromoteIntern, DeclineInternPromotion, UpdateWorkerStates)
- `HireIntern(workshop)`: F-Tier, `WagePerHour = 0`, `IsIntern = true`, Mood 80, kostenlos.
  Blockt wenn `InternCount >= MaxInterns` (MaxInterns = Interface-Konstante, App-CLAUDE.md nennt 2 —
  nicht in den gelesenen Dateien direkt sichtbar) oder Workshop voll.
- Tick-Akkumulation: `InternProgressTicks += max(1, deltaSeconds)` nur wenn `IsIntern && !InternAwaitingPromotion && !IsResting`.
- Promotions-Schwelle: `86400` Ticks (= 24h aktive Spielzeit, NICHT Echtzeit) → `InternAwaitingPromotion = true`,
  `InternReadyForPromotion`-Event.
- `PromoteIntern`: nur wenn `IsIntern && InternAwaitingPromotion` → `IsIntern=false`, `Tier=E`,
  `WagePerHour = WorkerTier.E.GetWagePerHour()` (=9), `InternProgressTicks=0`.
- `DeclineInternPromotion`: entfernt den Worker aus dem Workshop.

### 3.12 Marktpreis-Berechnung (CalculateMarketPrice)

Quelle: `Worker.CalculateMarketPrice(playerLevel)`
```
baseCost        = Tier.GetHiringCost(playerLevel)             (= BaseHiringCost * (1 + (lvl-1)*0.02), gerundet)
talentMult      = 0.70 + (Talent-1) * 0.15                   (1★=0.70, 2★=0.85, 3★=1.00, 4★=1.15, 5★=1.30)
personalityMult = siehe 3.5 (Preis-Mult-Spalte)
specMult        = Specialization.HasValue ? 1.15 : 1.0
effPosition     = clamp((Efficiency - minEff)/(maxEff-minEff), 0, 1)  (0.5 wenn Range 0)
effMult         = 0.85 + effPosition * 0.30                  (Min=0.85x, Max=1.15x)

MarketPrice = round(baseCost * talentMult * personalityMult * specMult * effMult)
```
HiringScrewCost (Tier A/S/SS/SSS/Legendary, siehe 3.1) wird zusaetzlich zur EUR-Kosten beim Hiren faellig.

### 3.13 Worker-Namen & Geschlecht

Quelle: `Worker.GenerateRandomName`, `Worker.CreateForTier`
- 40 maennliche + 40 weibliche Vornamen (Index 0–39 maennlich, 40–79 weiblich), 50 Nachnamen.
- `IsFemale = (id.GetHashCode() % 2 == 0)`.

---

## 4. Worker-Markt (WorkerMarketPool)

Quelle: `Models/WorkerMarketPool.cs`, `Services/WorkerService.cs`

- Rotation: alle 4 Stunden (`NextRotation = UtcNow.AddHours(4)`), `NeedsRotation` = UtcNow >= NextRotation.
- Pool-Groesse: `hasHeadhunter ? 8 : 5` (Headhunter via Research `mgmt_04`).
- `FreeRefreshUsedThisRotation`: Gratis-Refresh pro Rotation, zurueckgesetzt bei `GeneratePool`.
  Bei manuellem `RefreshMarket` wird das Flag bewahrt (nur Rotation resettet es).
- `GetWorkerMarket()` generiert Pool bei null oder NeedsRotation; `RefreshMarket()` regeneriert sofort.

### 4.1 Tier-Gewichtung (GetWeightedTier)

Quelle: `WorkerMarketPool.GetWeightedTier`. Nur Tiers in `GetAvailableTiers(playerLevel, prestige, hasSTier)`.

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

- Verfuegbare Tiers: `Worker.GetAvailableTiers` — Tier muss `playerLevel >= UnlockLevel` erfuellen; S+
  zusaetzlich `hasSTierResearch`.
- Legendary-Cooldown: 7 Tage nach letzter Legendary-Sichtung (`LastLegendarySpawn`) wird Legendary aus dem
  Pool gefiltert (verhindert Legendary-Farming).

---

## 5. Worker-Auktion

Quelle: `Services/WorkerAuctionService.cs`

- Auktions-Dauer: 30s active-Phase (`AuctionDuration = TimeSpan.FromSeconds(30)`).
- Firebase-Pfad: `guilds/{guildId}/auctions/{auctionId}`.
- Mindest-Erhoehung (PlaceBid + NPC): `HighestBid > 0 ? ceil(HighestBid * 1.1) : 100` EUR
  (= +10% des Hoechstgebots, mindestens 100 EUR).
- 1s-Cooldown gegen Spam-Bidding (Client) + serverseitige bidTimestamps-Rule.
- Geld-Locking: Nur Delta zum bisherigen Eigen-Bid wird abgezogen; bei Ablehnung Refund.
- HMAC-Signierung ueber AuctionId + WorkerTier + WorkerName + Status + HighestBidderId + HighestBid + sortierte AllBids.
- Multi-Path-PATCH (kein PUT) fuer Bids; highestBid-Monotonie-Rule lehnt Verlierer-Bids ab.

### 5.1 Tier-Verteilung beim Spawn (SpawnAuctionIfMaster)

Quelle: `WorkerAuctionService.SpawnAuctionIfMasterAsync` (`roll = rng.Next(0,100)`)

| Tier | Chance |
|------|--------|
| S | 70% (`roll < 70`) |
| SS | 25% (`70 <= roll < 95`) |
| SSS | 5% (`roll >= 95`) |

- Master-Client-Pattern: Spieler mit lexikografisch kleinster (aktiver) PlayerId spawnt; Solo = immer Master.
- Aktiv = LastActiveAt < 30 Tage (Founder/Leader immer aktiv).

### 5.2 NPC-Bot-Bidding (RunNpcBotTick)

Quelle: `WorkerAuctionService.RunNpcBotTickAsync` (Tick alle 5s, nur Master)
- 35% Chance pro Tick (`rng.Next(0,100) >= 35` → kein Bid).
- Bot-Anzahl pro Auktion: `1 + (abs(AuctionId.GetHashCode()) % 3)` (1–3 Bots).
- Bid = `currentMin + round(currentMin * (0.05 + NextDouble()*0.20), 2)` (5–25% ueber Min).
- Bot-Maximum (kein Bid wenn ueberschritten): S = 50.000, SS = 250.000, SSS = 1.000.000, sonst 25.000.
- Abgelaufene Auktion (UtcNow > EndsAt) wird im Tick gesettled.

### 5.3 Settlement / Refund (ApplyRefunds)

Quelle: `WorkerAuctionService.SettleAsync / ApplyRefunds`
- Idempotenz via `GameState.ClaimedAuctionIds`.
- Gewinner (HighestBidderId == self): Worker via `Worker.CreateForTier(WorkerTier)` generiert, in ersten
  freigeschalteten Workshop mit freiem Slot eingestellt; bei keinem freien Slot → Gebot erstattet.
- Verlierer: Eigenes Gebot vollstaendig zurueckerstattet.

---

## 6. Manager (14 Definitionen)

Quelle: `Models/Manager.cs`, `Services/ManagerService.cs`

### 6.1 ManagerAbility-Enum

`AutoCollectOrders, EfficiencyBoost, FatigueReduction, MoodBoost, IncomeBoost, TrainingSpeedUp`

### 6.2 Manager-Level & Kosten

- Start-Level 1, Max-Level 5 (`IsMaxLevel = Level >= 5`).
- `UpgradeCost (GS)` = `Level * 10` (Lv1→2: 10 GS, Lv2→3: 20, Lv3→4: 30, Lv4→5: 40 GS).
- Upgrade in Goldschrauben (`ManagerService.UpgradeManager` → `TrySpendGoldenScrews`).

### 6.3 Alle 14 Definitionen

Quelle: `Manager._allDefinitions` — `(Id, NameKey, Workshop, Ability, RequiredLevel, RequiredPrestige, RequiredPerfectRatings)`

| # | Id | NameKey | DefaultName | Workshop | Ability | ReqLevel | ReqPrestige | ReqPerfectRatings |
|---|----|---------|-------------|----------|---------|----------|-------------|-------------------|
| 1 | mgr_hans | ManagerHans | Hans | Carpenter | EfficiencyBoost | 10 | 0 | 0 |
| 2 | mgr_fritz | ManagerFritz | Fritz | Plumber | FatigueReduction | 20 | 0 | 0 |
| 3 | mgr_kurt | ManagerKurt | Kurt | Electrician | IncomeBoost | 30 | 0 | 0 |
| 4 | mgr_lisa | ManagerLisa | Lisa | Painter | MoodBoost | 40 | 0 | 0 |
| 5 | mgr_karl | ManagerKarl | Karl | Roofer | EfficiencyBoost | 60 | 0 | 0 |
| 6 | mgr_otto | ManagerOtto | Otto | Contractor | IncomeBoost | 80 | 0 | 0 |
| 7 | mgr_anna | ManagerAnna | Anna | Architect | FatigueReduction | 0 | 0 | 25 |
| 8 | mgr_max | ManagerMax | Max | GeneralContractor | IncomeBoost | 100 | 0 | 0 |
| 9 | mgr_schmied | ManagerSchmied | Schmied | MasterSmith | EfficiencyBoost | 120 | 0 | 0 |
| 10 | mgr_erfinder | ManagerErfinder | Erfinder | InnovationLab | IncomeBoost | 140 | 0 | 0 |
| 11 | mgr_schmidt | ManagerSchmidt | Schmidt | (global/null) | TrainingSpeedUp | 0 | 1 | 0 |
| 12 | mgr_weber | ManagerWeber | Weber | (global/null) | AutoCollectOrders | 0 | 2 | 0 |
| 13 | mgr_mueller | ManagerMueller | Müller | (global/null) | EfficiencyBoost | 0 | 3 | 0 |
| 14 | mgr_kaiser | ManagerKaiser | Kaiser | (global/null) | IncomeBoost | 0 | 4 | 0 |

Unlock-Bedingung (`ManagerService.IsEligible`, alle gesetzten Bedingungen muessen erfuellt sein):
- `RequiredLevel > 0` → `PlayerLevel >= RequiredLevel`
- `RequiredPrestige > 0` → `Prestige.TotalPrestigeCount >= RequiredPrestige`
- `RequiredPerfectRatings > 0` → `Statistics.PerfectRatings >= RequiredPerfectRatings`

### 6.4 Per-Level-Boni (Manager.GetBonus)

Quelle: `Manager.GetBonus(ability)` — gilt nur wenn `IsUnlocked` und `def.Ability == ability`.

| Ability | Bonus pro Level | Beispiel Lv5 |
|---------|-----------------|--------------|
| EfficiencyBoost | +0.05 * Level (+5%/Lvl) | +25% |
| FatigueReduction | +0.03 * Level (-3%/Lvl) | -15% |
| MoodBoost | +0.04 * Level (+4%/Lvl) | +20% |
| IncomeBoost | +0.05 * Level (+5%/Lvl) | +25% |
| TrainingSpeedUp | +0.10 * Level (+10%/Lvl) | +50% |
| AutoCollectOrders | = Level (Anzahl pro Check) | 5 |

- Workshop-gebundene Manager (Workshop != null) → `GetManagerBonusForWorkshop(type, ability)`.
- Globale Manager (Workshop == null) → `GetGlobalManagerBonus(ability)`.
- MoodBoost wird im WorkerService auf 50% gedeckelt (`min(managerMoodBonus, 0.50)`).

---

## 7. Buildings (7 Gebaeude)

Quelle: `Models/Building.cs`, `Models/Enums/BuildingType.cs`, `Services/BuildingService.cs`, `GameBalanceConstants`

### 7.1 BuildingType-Enum + Basiswerte

Enum: Canteen=0, Storage=1, Office=2, Showroom=3, TrainingCenter=4, VehicleFleet=5, WorkshopExtension=6

| Typ | BaseCost EUR (`GetBaseCost`) | UnlockLevel (`GetUnlockLevel`) | MaxLevel (`GetMaxLevel`) | Icon |
|-----|------|------|------|------|
| Canteen | 10.000 | 5 | 5 | SilverwareForkKnife |
| Storage | 15.000 | 8 | 5 | Warehouse |
| Office | 20.000 | 10 | 5 | OfficeBuildingOutline |
| Showroom | 25.000 | 15 | 5 | StorefrontOutline |
| TrainingCenter | 50.000 | 20 | 5 | GraduationCap |
| VehicleFleet | 75.000 | 25 | 5 | Truck |
| WorkshopExtension | 100.000 | 30 | 5 | HammerWrench |

- MaxLevel ist fuer ALLE Buildings 5.
- Upgrade-Kosten (`Building.NextLevelCost`): wenn nicht gebaut → BaseCost (Level 1);
  wenn gebaut und Level < MaxLevel → `BaseCost * 2^Level`.
  HINWEIS: Doc-Kommentar in BuildingType.cs nennt `3^(Level-1)`, der tatsaechliche Code (Building.cs +
  `BuildingCostExponent = 2`) verwendet `2^Level`. **Verbindlich = `BaseCost * 2^Level`.**
- Build-Vorgang (`BuildingService.TryBuildBuilding`): zahlt `BaseCost`, setzt `IsBuilt=true`, `Level=1`.
- `CanUpgrade` = `IsBuilt && Level < 5`.

### 7.2 Per-Level-Effekte (alle Stufen 1–5)

Quelle: `Models/Building.cs` (Effekt-Properties) + `GameBalanceConstants` (Spiegel-Arrays)

**Canteen**
- MoodRecoveryPerHour = `Level * 1.0` → Lv1-5: 1%, 2%, 3%, 4%, 5% (`CanteenMoodRecoveryPerLevel = 1.0`)
- RestTimeReduction (Lv1-5): 0.50, 0.55, 0.60, 0.70, 0.80 (`CanteenRestReduction = [0, 0.50, 0.55, 0.60, 0.70, 0.80]`, Index = Level)

**Storage**
- MaterialCostReduction (Lv1-5): 0.15, 0.25, 0.35, 0.45, 0.50 (`StorageMaterialReduction = [0, 0.15, 0.25, 0.35, 0.45, 0.50]`)

**Office**
- ExtraOrderSlots = `Level + 1` → Lv1-5: 2, 3, 4, 5, 6

**Showroom**
- DailyReputationGain = `Level * 0.5` → Lv1-5: 0.5, 1.0, 1.5, 2.0, 2.5 (`ShowroomReputationPerLevel = 0.5`)

**TrainingCenter**
- TrainingSpeedMultiplier = `1.0 + Level * TrainingCenterSpeedPerLevel(1.0) + 0.5`
  → Lv1-5: 2.5x, 3.5x, 4.5x, 5.5x, 6.5x

**VehicleFleet**
- OrderRewardBonus (Lv1-5): 0.20, 0.30, 0.40, 0.50, 0.60 (`VehicleFleetRewardBonus = [0, 0.20, 0.30, 0.40, 0.50, 0.60]`)

**WorkshopExtension**
- ExtraWorkerSlots = `Level + 1` → Lv1-5: 2, 3, 4, 5, 6 (addiert sich zu Workshop.ExtraWorkerSlots)

---

## 8. Workshop-Spezialisierung (3 Typen)

Quelle: `Models/WorkshopSpecialization.cs`, `GameBalanceConstants`

- Freischaltung ab Workshop-Level `SpecializationUnlockLevel = 50` (Doc-Bug "Level 100" — korrekt 50).
- Erste Wahl gratis; Re-Spec kostet `SpecializationRespecCostGoldenScrews = 20` GS;
  Re-Spec gratis unterhalb Workshop-Level `SpecializationFreeRespecBelowLevel = 75`; Entfernen gratis.
- Enum: Efficiency, Quality, Economy.

### 8.1 Effekt-Tabelle (alle Modifier)

Quelle: `WorkshopSpecialization` Property-Switches

| Typ | IncomeModifier | CostModifier | EfficiencyModifier | WorkerCapacityModifier | AuraBonusMultiplier | OrderRewardBonus | Color |
|-----|----------------|--------------|--------------------|------------------------|---------------------|------------------|-------|
| Efficiency | +0.30 | 0 | 0 | -1 | 1.0 | 0 | #FF9800 |
| Quality | 0 | +0.15 | +0.20 | 0 | 2.0 | 0 | #2196F3 |
| Economy | -0.05 | -0.25 | 0 | 0 | 1.0 | +0.15 | #4CAF50 |

Wirkung (siehe auch 2.3 / 2.8):
- Efficiency: +30% Einkommen (`gross *= 1.30`), -1 Worker-Slot.
- Quality: +20% Worker-Effizienz (`gross *= 1.20`), +15% Kosten (`costs *= 1.15`), Aura-Bonus verdoppelt (×2 ueber Standard-Cap).
- Economy: -5% Einkommen (`gross *= 0.95`), -25% Kosten (`costs *= 0.75`), +15% Auftragsbelohnung.
- NameKey = `"Specialization_{Type}"`, DescriptionKey = `"Specialization_{Type}_Desc"`.

---

## 9. Weitere relevante Konstanten (GameBalanceConstants, kontextrelevant)

Quelle: `Models/GameBalanceConstants.cs`
- `MaxParallelOrders = 3`.
- `AutoCraftTier2UnlockLevel = 150`, `AutoCraftTier3UnlockLevel = 320`.
- `CraftingSellPriceLogDivisor = 15.0`.
- `MaterialOfferUnlockLevel = 30`, `MaterialOrderCrossWorkshopLevel = 100`.
- `MaterialOrderRewardMultiplier = 1.8`, `MaterialOrderXpMultiplier = 1.5`, `MaterialOrdersPerDay = 5`, `MaterialOrderDeadlineHours = 4`.
- Eternal Mastery: `EternalMasteryBonusPerPrestige = 0.005`, `...Per5Prestiges = 0.025`, `...Per10Prestiges = 0.05`, SoftCapThreshold = 50.
- `BonusPpPerAscensionLevel = 0.5`, `DiminishingReturnsPerTierPrestige = 0.2`.
- `OrderRewardMultiplierSoftCap = 10.0`, `CraftingSellMultiplierSoftCap = 8.0`, `CraftingSellMultiplierHardCap = 12.0`.
- Heirloom: `MaxHeirloomsPerRun = 3`, `...Premium = 4`, `HeirloomBonusPerItem = 0.02`, `PermanentHeirloomBonusPerItem = 0.005`, `MaxPermanentHeirlooms = 50`.
- `MoneyAnimationInterpolationFactor = 0.15`, `SplashMinimumDisplayMs = 800`.

---

## Anmerkungen zu nicht in den gelesenen Quelldateien gefundenen Werten

- **Rebirth-Kosten pro Stern**: verifiziert in `Services/RebirthService.cs` (`RebirthCosts`):
  Goldschrauben 50/125/250/200/400 PLUS Geld-Anteil 10%/15%/20%/25%/30% des aktuellen Geldes
  (siehe Abschnitt 2.10).
- **`MaxInterns`**: Interface-Konstante `IWorkerService.MaxInterns` (Interface nicht gelesen). App-CLAUDE.md
  nennt 2 — nicht aus den gelesenen Dateien verifiziert.
- **Research-Unlocks** (`mgmt_04` Headhunter Pool 5→8, `mgmt_10` S-Tier): via `IResearchService.GetTotalEffects()`
  referenziert; die konkreten Research-Node-Definitionen liegen in `Models/Research.cs` (nicht im Leseauftrag).
- **WorkshopType besitzt KEINE eigene MaterialAffinity-Methode** — die im Auftrag erwaehnte
  "MaterialAffinity je WorkshopType" existiert im Code nur als Worker-Property + Produkt-Mapping (siehe 1.2 / 3.6).

---

# 02 — Core-Loop, Income, Soft-Cap, Offline, Tick-Offsets, Costs, GameState-Mutationen

> Verbindliche Wahrheit, extrahiert direkt aus dem Avalonia-Code von HandwerkerImperium.
> Jeder Wert stammt aus dem Code; Formeln sind exakt uebernommen. Wo der Code etwas nicht
> enthaelt, steht "(nicht im Code gefunden)".
> Code-Root: `HandwerkerImperium.Shared`.

---

## 1. Game-Loop-Grundtakt

Quelle: `Services/GameLoopService.cs`

- Timer: `DispatcherTimer` mit `Interval = TimeSpan.FromSeconds(1)` → **1 Tick = 1 Sekunde**.
- `Start()`: setzt `_sessionStart = DateTime.UtcNow`, `_isPaused = false`, `_tickCount = 0`, startet Timer.
- `Stop()`: stoppt Timer, akkumuliert `Statistics.TotalPlayTimeSeconds += (long)(UtcNow - _sessionStart).TotalSeconds`, setzt `State.LastPlayedAt = UtcNow`, dann `SaveAsync()` (Fire-and-Forget).
- `PauseAsync()`: `_isPaused = true`, Timer stop, akkumuliert PlayTime, setzt `LastPlayedAt = UtcNow`, `_sessionStart = UtcNow`, **synchroner** `SaveAsync().ConfigureAwait(false)` (kein Fire-and-Forget — Android-Kill-Schutz fuer Offline-Earnings).
- `Resume()`: `_isPaused = false`, `_sessionStart = UtcNow` (Pause-Zeit zaehlt nicht), Timer start.
- `OnTimerTick`: Early-Return wenn `_isPaused || !_gameStateService.IsInitialized`.

### OnTimerTick-Reihenfolge (pro Tick, exakt)

Quelle: `GameLoopService.cs` `OnTimerTick`

1. `state = _gameStateService.State`, `now = DateTime.UtcNow`.
2. `RefreshWorkshopCacheIfNeeded(state)` + `RefreshPrestigeEffectsIfNeeded(state)`.
3. `researchEffects = _researchService.GetTotalEffects()`, `UpdateExtraWorkerSlots(state, researchEffects)`.
4. Falls `_cachedMasterToolBonus < 0`: `_cachedMasterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools)`.
5. `eventEffects = _eventService?.GetCurrentEffects()`.
6. `grossIncome = _incomeCalculator.CalculateGrossIncome(state, _cachedPrestigeIncomeBonus, _cachedMasterToolBonus, researchEffects, eventEffects)`.
7. `grossIncome = _incomeCalculator.ApplySoftCap(state, grossIncome)`.
8. `costs = _incomeCalculator.CalculateCosts(state, researchEffects, eventEffects)`.
9. Event-Einmaleffekte + SpecialEffects (siehe Abschnitt 9).
10. `netEarnings = grossIncome - costs`.
11. SpeedBoost (×2) + Rush-Boost (siehe Abschnitt 6).
12. Netto anwenden (`AddMoney` bei positiv / `TrySpendMoney` bei negativ mit Floor; siehe Abschnitt 7).
13. Per-Workshop-Tracking (`ws.TotalEarned += grossInc` roh, `worker.TotalEarned += ws.BaseIncomePerWorker * worker.EffectiveEfficiency * ws.GetWorkerLevelFitFactor(worker)`).
14. `_workerService.UpdateWorkerStates(1.0)`.
15. `_researchService.UpdateTimer(1.0)`.
16. `_tickCount++`, `ProcessPeriodicChecks(state, now)`.
17. `state.LastPlayedAt = now`; bei `_tickCount % 30 == 0` → `SaveAsync()` (Fire-and-Forget).
18. `OnTick?.Invoke(this, new GameTickEventArgs(netEarnings, state.Money, SessionDuration))`.

`SessionDuration => DateTime.UtcNow - _sessionStart`.

---

## 2. Tick-Offsets / Intervalle (vollstaendig)

Konstanten-Quelle: `GameLoopService.cs`. Auswertungs-Quelle: `GameLoopService.PeriodicChecks.cs`.
`AutoProduction`/`AutoCraft`-Intervalle aus `GameBalanceConstants.cs`.

Auswertung als `_tickCount % Intervall == Offset` (Sekunden, da 1 Tick = 1 s).

| Aktion | Intervall (s) | Offset | Bedingung / Methode | Quelle |
|--------|---------------|--------|---------------------|--------|
| AutoSave | 30 | 0 (`% 30 == 0`) | `SaveAsync().FireAndForget()` | `GameLoopService.cs` (`AutoSaveIntervalTicks=30`) |
| Crafting-Timer + InnovationLab-Bonus | jeder Tick | — | `_craftingService?.UpdateTimers()` + `ApplyInnovationLabBonus(state)` | PeriodicChecks |
| Automation (AutoCollect/AutoAccept) | 5 | **3** | `ProcessAutomation(state)` (`AutomationCheckIntervalTicks=5`) | PeriodicChecks |
| Lieferant-Check | 10 | 0 | `CheckAndGenerateDelivery(state, now)` (`DeliveryCheckIntervalTicks=10`) | PeriodicChecks |
| Live-Order-Expire | 3 | 0 | `_orderGeneratorService.ExpireOldLiveOrders()` (`OrderLiveExpireCheckTicks=3`) | PeriodicChecks |
| Live-Order-Spawn | 25 | **17** | bei `Random.Shared.NextDouble() < 0.5` → `GenerateLiveOrder()` (`OrderLiveSpawnCheckTicks=25`, `OrderLiveSpawnProbability=0.5`) | PeriodicChecks |
| QuickJob-Rotation + DailyChallenge-Reset + Order-Expiry (Block) | 60 | 0 | `RotateIfNeeded()` + `CheckAndResetIfNewDay()` + Order-Expiry-Lock-Block (`QuickJobCheckIntervalTicks=60`) | PeriodicChecks |
| WeeklyMission-Reset | 60 | **15** | `_weeklyMissionService?.CheckAndResetIfNewWeek()` (`WeeklyMissionCheckIntervalTicks=60`) | PeriodicChecks |
| AutoAssign | 60 | **30** | `ProcessAutoAssign(state)` (`AutoAssignIntervalTicks=60`) | PeriodicChecks |
| MasterSmith-Material-Produktion | 60 | **45** | `ProduceMasterSmithMaterials(state)` (`% 60 == 45`, literal) | PeriodicChecks |
| Auto-Produktion Tier-1 | 180 | **90** | `_autoProductionService.ProduceForAllWorkshops(state)` (`AutoProductionIntervalSeconds=180`) | PeriodicChecks |
| Auto-Craft hoehere Tiers | 360 | **270** | `_autoProductionService.AutoCraftHigherTiers(state)` (`% 360 == 270`, literal) | PeriodicChecks |
| Manager-Unlock-Check | 120 | **60** | `_managerService?.CheckAndUnlockManagers()` (`ManagerCheckIntervalTicks=120`) | PeriodicChecks |
| MasterTool-Check | 120 | 0 | `CheckMasterTools(state)` (`MasterToolCheckIntervalTicks=120`) | PeriodicChecks |
| Event-Check | 300 | 0 | `_eventService?.CheckForNewEvent()` (`EventCheckIntervalTicks=300`) | PeriodicChecks |
| Seasonal-Event-Check | 300 | **150** | `_seasonalEventService?.CheckSeasonalEvent()` (`SeasonalEventCheckIntervalTicks=300`) | PeriodicChecks |
| BattlePass-Season-Check | 300 | **200** | `_battlePassService?.CheckNewSeason()` (`BattlePassSeasonCheckIntervalTicks=300`) | PeriodicChecks |
| Guild-Tick | jeder Tick | — | `_guildTickService?.ProcessTick(state, _tickCount)` (interne Sub-Offsets, siehe unten) | PeriodicChecks |
| Reputation-Decay (Showroom + Decay) | zeitbasiert 24h | — | wenn `(now - state.LastReputationDecay).TotalHours >= 24` | PeriodicChecks |

Hinweis: `QuickJobCheckIntervalTicks` und `AutoAssignIntervalTicks` sind beide 60; der Code definiert
zusaetzlich `QuickJobCheckIntervalTicks=60` als eigene Konstante (gleiche Periode, anderer Offset).
Die konstanten `MasterSmith` (`% 60 == 45`) und `Auto-Craft` (`% 360 == 270`) sind als Literale gesetzt, nicht ueber benannte Konstanten.

### Guild-Tick Sub-Offsets

Quelle: GameLoop-/Services-CLAUDE.md (Sub-Service-Dokumentation; `GuildTickService.ProcessTick(state, _tickCount)` delegiert).
Im hier gelesenen Code nicht im Detail enthalten — Werte aus der Service-Hierarchie-Doku:

| Sub-Service | Intervall (s) | Offset |
|-------------|---------------|--------|
| GuildBossService | 60 | 20 |
| GuildHallService | 60 | 40 |
| GuildAchievementService | 300 | 250 |
| GuildWarSeasonService | 300 | 260 |
| WorkerAuction-Spawn | 300 | 90 |
| WorkerAuction NPC-Bot-Tick | 5 | 1 |

(Die genauen Modulo-Werte liegen in `GuildTickService.cs` / den Sub-Services — nicht Teil der hier gelesenen Dateien.)

### GameTickCoordinator (UI-Tick, separat vom GameLoop)

Quelle: `Services/GameTickCoordinator.cs` — subscribed auf `IGameLoopService.OnTick` (1 Hz). Eigene Modulo-Zaehler:

- `_floatingTextCounter` (inkrementiert jeden Tick):
  - `% 3 == 0`: `UpdateDeliveryDisplay()`, und (separater Zaehler-Pfad) WorkerProfile-Refresh wenn aktiv.
  - `% 5 == 0`: `UpdateEventDisplay()`, `LiveEventBannerVm.Refresh()`, Dashboard/Missionen → `RefreshChallenges()`, Buildings/Dashboard → `RefreshReputation()` + `RefreshPrestigeBanner()`.
  - `% 10 == 0`: Lucky-Spin/Welcome-Back-Timer, Worker-Warnung, Soft-Cap-Indikator (nur Dashboard).
- `_tickForGoal`: alle 60 Ticks (`>= 60`) → `GoalBannerVm.Refresh()`; sonst falls aktives Event → `UpdateEventTimer()`.
- Header-Income-Update nur wenn `state.NetIncomePerSecond != _headerVm.IncomePerSecond`.
- WorkerMarket-Timer: jede Sekunde wenn `WorkerMarket`-Tab aktiv.

### FrameClockService (Render-Tick, NICHT GameLoop)

Quelle: `Services/FrameClockService.cs`

- Master-Tick: `DispatcherTimer` mit `Interval = FpsProfile.DashboardActive()` (~33 ms, ~30 Hz).
- Pro Subscriber eigenes Intervall; Handler feuert nur wenn `elapsedSinceLast >= intervalSeconds`.
- Erster Tick eines Subscribers feuert sofort (`LastTickSeconds = -1f`).
- Auto-Stop bei 0 Subscribern; `Pause()`/`Resume()` fuer App-Lifecycle. `Stopwatch`-basierte DeltaSeconds.
- Idempotentes `Subscribe` (doppeltes Subscribe wird ignoriert).

---

## 3. Income-Berechnung (Brutto) — Multiplikator-Reihenfolge

Quelle: `Services/IncomeCalculatorService.cs` `CalculateGrossIncome(...)`

Start: `grossIncome = state.TotalIncomePerSecond`.

Dann **in dieser exakten Reihenfolge** (jeweils nur wenn Bedingung > 0):

| # | Faktor | Formel | Bedingung |
|---|--------|--------|-----------|
| 1 | Prestige-Shop Income | `*= (1 + prestigeIncomeBonus)` | `prestigeIncomeBonus > 0` |
| 2 | Research-Effizienz | `*= (1 + Min(researchEffects.EfficiencyBonus, 0.50))` | `EfficiencyBonus > 0` — **Cap +50%** |
| 3 | Event-Multiplikator | `*= eventEffects.IncomeMultiplier` | `eventEffects != null` |
| 4 | TaxAudit-Steuer | `*= 0.90` | `eventEffects.SpecialEffect == "tax_10_percent"` |
| 5 | Meisterwerkzeuge | `*= (1 + mtBonus)` | `mtBonus > 0` (siehe Abschnitt 11) |
| 6 | Gilde Income-Bonus | `*= (1 + GuildMembership.IncomeBonus)` | `GuildMembership != null && IncomeBonus > 0` |
| 7 | Gilde Research-Income | `*= (1 + gm.ResearchIncomeBonus)` | `ResearchIncomeBonus > 0` |
| 8 | Gilde Research-Efficiency | `*= (1 + gm.ResearchEfficiencyBonus)` | `ResearchEfficiencyBonus > 0` |
| 9 | Gilde Hall-Income | `*= (1 + gm.HallIncomeBonus)` | `HallIncomeBonus > 0` |
| 10 | Gilde Hall-Everything | `*= (1 + gm.HallEverythingBonus)` | `HallEverythingBonus > 0` |
| 11 | VIP Income | `*= (1 + _vipService.IncomeBonus)` | `IncomeBonus > 0` |
| 12 | Manager Income (Summe) | `*= (1 + totalManagerIncome)` | siehe unten |
| 13 | Manager Efficiency (Summe, SEPARAT) | `*= (1 + totalManagerEfficiency)` | siehe unten |
| 14 | Premium | `*= 1.5` | `state.IsPremium` |
| 15 | Eternal Mastery | `*= (1 + _eternalMastery.IncomeBonus)` | `_eternalMastery.IsActive` |
| 16 | Heirloom (Erbstuecke) | `*= (1 + heirloomBonus)` | `heirloomBonus > 0` |

Die beiden Gildenhallen-Faktoren (#9/#10) liegen NACH Gilde-Research-Efficiency und VOR VIP
(`IncomeCalculatorService.cs:84-87`). Sie werden von Gildenhallen-Gebäuden gesetzt
(`Guild.cs:101/113/148/152`) — eine 1:1-Portierung ohne sie untergewichtet das Gildenhallen-Einkommen.

Wichtig: **Manager Income und Manager Efficiency werden als ZWEI separate Multiplikatoren angewandt** (nicht summiert).
`totalManagerIncome` = Summe von `GetManagerBonusForWorkshop(wsType, IncomeBoost)` ueber alle Workshops + `GetGlobalManagerBonus(IncomeBoost)`.
`totalManagerEfficiency` = analog mit `EfficiencyBoost`.

Heirloom-Bonus (`GetTotalHeirloomBonus(state)`, public static):
```
active    = state.HeirloomItems.Count           * 0.02   (HeirloomBonusPerItem)
permanent = state.Ascension.PermanentHeirlooms  * 0.005  (PermanentHeirloomBonusPerItem)
heirloomBonus = active + permanent
```

`mtBonus` = `masterToolBonus >= 0 ? masterToolBonus : MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools)`.

### Basis `TotalIncomePerSecond` + PermanentMultiplier-Cap (20x)

Quelle: `Models/GameState.cs` `RecalculateIncomeCache()`

```
totalIncome = Σ Workshops[i].GrossIncomePerSecond
totalCosts  = Σ Workshops[i].TotalCostsPerHour
multiplier  = Min(Prestige.PermanentMultiplier, 20.0)        // PermanentMultiplier-Cap = 20x
_cachedIncome = totalIncome * multiplier
_cachedCosts  = totalCosts / 3600                            // Kosten pro Stunde → pro Sekunde
```
- `TotalIncomePerSecond` und `TotalCostsPerSecond` lesen diesen Cache (Dirty-Flag `_incomeCacheDirty`).
- `NetIncomePerSecond => TotalIncomePerSecond - TotalCostsPerSecond` (nur fuer Display, OHNE die Modifikatoren aus Abschnitt 3).
- `MaxPermanentMultiplier = 20.0m` ist auch in `PrestigeService.cs` definiert (beim Schreiben in `DoPrestige` gekappt: `Math.Min(Math.Round(prestige.PermanentMultiplier, 3), 20.0)`).

### Crafting-Sell-Multiplikator (separater Pfad)

Quelle: `IncomeCalculatorService.CalculateCraftingSellMultiplier(...)`

Start `mult = 1.0`. Reihenfolge: Prestige-Income → Research-Eff (Cap 50%) → Event → TaxAudit (×0.90) →
MasterTools → Gilde (Income + ResearchIncome + ResearchEfficiency + **HallIncome + HallEverything**) →
VIP → Rebirth-Income → Premium (×1.5). (Hall-Boni: `IncomeCalculatorService.cs:320-323`.)
Dann Soft-Cap:
```
SoftCap = GameBalanceConstants.CraftingSellMultiplierSoftCap = 8.0
HardCap = GameBalanceConstants.CraftingSellMultiplierHardCap = 12.0
wenn mult > 8.0:  mult = 8.0 + log2(1 + (mult - 8.0))
return Min(mult, 12.0)
```

---

## 4. Income-Soft-Cap (tier-skalierend)

Quelle: `IncomeCalculatorService.ApplySoftCap(state, grossIncome)`. Es gibt **KEINE** benannte Konstante
`SoftCapThreshold` — die Schwelle ist tier-abhängig per Inline-`switch` (siehe Tier-Schwellen). `8.0` ist
NUR der `_ => 8.0m`-Default-Zweig des switch (zugleich der Silver-Wert), kein eigener Schwellen-const.

Early-Return: wenn `state.TotalIncomePerSecond <= 0` → `grossIncome` unveraendert zurueck.

### Tier-Schwellen

```
tier = state.Prestige?.CurrentTier ?? PrestigeTier.None
```
| PrestigeTier | tierThreshold (x) |
|--------------|-------------------|
| None | 4.0 |
| Bronze | 6.0 |
| Silver | 8.0 |
| Gold | 10.0 |
| Platin | 12.0 |
| Diamant | 14.0 |
| Meister | 16.0 |
| Legende | 20.0 |
| (default) | 8.0 |

### Ascension-Floor

```
ascensionThreshold = (Ascension.AscensionLevel > 0)
    ? Min(18.0 + Ascension.AscensionLevel * 2.0, 30.0)
    : 0.0
threshold = Max(tierThreshold, ascensionThreshold)
```
Hard-Cap des Ascension-Floors: **30.0x** (`Min(18 + AscLvl*2, 30)`).
- AscLvl 1 → 20, AscLvl 2 → 22, …, AscLvl 6 → 30, AscLvl ≥ 6 → 30 (gedeckelt).

### log2-Daempfung des Ueberschusses

```
effectiveMultiplier = grossIncome / state.TotalIncomePerSecond
wenn effectiveMultiplier > threshold:
    excess   = effectiveMultiplier - threshold
    softened = threshold + log2(1.0 + excess)                 // (decimal)Math.Log(1+excess, 2)
    grossIncome = state.TotalIncomePerSecond * softened
    state.IsSoftCapActive = true
    state.SoftCapReductionPercent = (int)Round((1 - softened/effectiveMultiplier) * 100)
sonst:
    state.IsSoftCapActive = false
    state.SoftCapReductionPercent = 0
```

---

## 5. Kosten-Berechnung

Quelle: `IncomeCalculatorService.CalculateCosts(state, researchEffects, eventEffects)`

Start: `costs = state.TotalCostsPerSecond`.

```
totalCostReduction = 0
+= _prestigeService.GetCostReduction()                        // Prestige-Shop CostReduction
+= researchEffects.CostReduction + researchEffects.WageReduction
storage = state.GetBuilding(BuildingType.Storage)
+= storage.MaterialCostReduction * 0.5                          // Storage: nur HALBER Effekt auf laufende Kosten
wenn totalCostReduction > 0:
    costs *= (1 - Min(totalCostReduction, 0.50))               // Cap 50%

// Event-Kosten
wenn eventEffects != null:
    costs *= eventEffects.CostMultiplier

// Gilden-Research Kosten-Reduktion
wenn GuildMembership.ResearchCostReduction > 0:
    costs *= (1 - Min(GuildMembership.ResearchCostReduction, 0.50))   // eigener Cap 50%
```

Hinweis: Der **CostReduction-Cap 50%** gilt fuer (Prestige + Research.CostReduction + Research.WageReduction + Storage*0.5)
zusammen. Der Gilden-`ResearchCostReduction` hat einen **eigenen** zusaetzlichen Cap 50% und wird danach multiplikativ angewandt.

---

## 6. Boosts: SpeedBoost + Feierabend-Rush

Quelle netto-Anwendung: `GameLoopService.cs` `OnTimerTick`. Boost-Definitionen: `Models/BoostData.cs`.

### Online (pro Tick, NACH `netEarnings = grossIncome - costs`)

```
// SpeedBoost: verdoppelt Netto (nur wenn positiv)
wenn state.IsSpeedBoostActive && netEarnings > 0:
    netEarnings *= 2

// Feierabend-Rush: ×2, gestackt mit PrestigeShop-Rush-Bonus
wenn state.IsRushBoostActive && netEarnings > 0:
    rushMultiplier = 2
    wenn _cachedPrestigeRushBonus > 0: rushMultiplier += _cachedPrestigeRushBonus
    netEarnings *= rushMultiplier
```
- Stacking ist multiplikativ: Speed (×2) und Rush (×2 + PrestigeBonus) → bis **×4** netto (bzw. mehr mit Prestige-Rush-Bonus).
- BoostData: `IsSpeedBoostActive => SpeedBoostEndTime > UtcNow`; `IsRushBoostActive => RushBoostEndTime > UtcNow`; `IsXpBoostActive => XpBoostEndTime > UtcNow`.
- `IsFreeRushAvailable => LastFreeRushUsed.Date < UtcNow.Date` (zeitmanipulations-sicher: Zukunfts-Datum blockiert).
- Default-EndTimes: `DateTime.MinValue`.

### Offline (pro-rata gewichtet)

Quelle: `OfflineProgressService.ApplyBoostsProRata(...)`

```
speedBoostSeconds = (SpeedBoostEndTime > lastPlayed) ? Min((SpeedBoostEndTime - lastPlayed).TotalSeconds, totalSeconds) : 0
rushMultiplier = 2
wenn RushBoostEndTime > lastPlayed:
    rushBoostSeconds = Min((RushBoostEndTime - lastPlayed).TotalSeconds, totalSeconds)
    rushMultiplier = Min(2 + GetPrestigeRushBonus(state), 4)        // Rush-Multi HART bei 4x gecappt
```
Gewichtete Fenster:
```
bothSeconds      = Max(0, Min(speedBoostSeconds, rushBoostSeconds))
onlySpeedSeconds = Max(0, speedBoostSeconds - bothSeconds)
onlyRushSeconds  = Max(0, rushBoostSeconds  - bothSeconds)
unboostedSeconds = totalSeconds - bothSeconds - onlySpeedSeconds - onlyRushSeconds

weighted = bothSeconds*2*rushMultiplier + onlySpeedSeconds*2 + onlyRushSeconds*rushMultiplier + unboostedSeconds*1
averageMultiplier = weighted / totalSeconds
earnings *= averageMultiplier
```
Maximal-Stack offline: Speed (×2) × Rush (max ×4) = **×8** (kontrollierbar, Kommentar im Code).

---

## 7. Netto-Anwendung + negatives Netto-Handling

Quelle: `GameLoopService.cs` `OnTimerTick`

```
wenn netEarnings != 0:
    wenn netEarnings > 0:
        _gameStateService.AddMoney(netEarnings)
    sonst:  // Kosten > Einkommen
        // Geld NICHT unter 0 durch Kosten allein fallen lassen
        wenn state.Money + netEarnings > 0:
            _gameStateService.TrySpendMoney(-netEarnings)
```
- Bei negativem Netto wird abgezogen NUR wenn `Money + netEarnings > 0` (sonst gar kein Abzug → Floor bei 0).
- Boosts (SpeedBoost/Rush) wirken ausschliesslich bei `netEarnings > 0`.

---

## 8. Zeitmanipulations-Schutz + Offline-Dauer

Quelle: `OfflineProgressService.cs`

```
GetOfflineDuration():
    lastPlayed = State.LastPlayedAt; now = UtcNow
    wenn lastPlayed > now: return TimeSpan.Zero       // Systemuhr zurueckgedreht → keine Offline-Earnings
    return now - lastPlayed
```
- `CalculateOfflineProgress()`: wenn `IsOfflineIncomeBlocked()` (Sprint-Challenge) → return 0.
- Mindest-Offline-Dauer: `< 60s` → return 0.
- `wasCapped = offlineDuration > maxDuration`; `effectiveDuration = wasCapped ? maxDuration : offlineDuration`.
- Lucky-Spin/Free-Rush etc. nutzen `.Date`-Vergleiche mit gleichem Zukunfts-Schutz.

### MaxOfflineHours

Quelle: `Models/GameState.cs` `MaxOfflineHours` (gecacht, `InvalidateMaxOfflineHoursCache()`)

```
baseHours = IsPremium ? 16 : (OfflineVideoExtended ? 8 : 4)
// PrestigeShop OfflineHoursBonus (pp_offline_hours: +4h) addieren:
foreach item in PrestigeShop.GetAllItems():
    wenn !item.IsRepeatable && purchased.Contains(item.Id) && item.Effect.OfflineHoursBonus > 0:
        baseHours += item.Effect.OfflineHoursBonus
```
- `BaseOfflineHours => 4` (immer 4).
- Basis: **4h** (Standard) / **8h** (Video-erweitert, Session-Flag `OfflineVideoExtended`) / **16h** (Premium).
- + Prestige-Shop `OfflineHoursBonus` (typisch +4h aus `pp_offline_hours`).
- `OfflineVideoExtended` und `OfflineVideoDoubled` sind `[JsonIgnore]` Session-Flags (nicht persistiert).
- `GetMaxOfflineDuration() = TimeSpan.FromHours(State.MaxOfflineHours)`.

### Offline-Earnings-Berechnung (Kern)

Quelle: `OfflineProgressService.CalculateOfflineProgress()`

```
researchEffects laden, levelResistance = Min(LevelResistanceBonus, 0.50) auf alle Workshops setzen
prestigeBonus = IncomeCalculatorService.GetPrestigeIncomeBonus(state)
grossIncome   = CalculateGrossIncome(state, prestigeBonus)          // OHNE masterToolBonus-Override (=-1 default)
grossIncome   = ApplySoftCap(state, grossIncome)
costs         = CalculateCosts(state)
netPerSecond  = Max(0, grossIncome - costs)                          // offline NIE Geld verlieren
```

---

## 9. Offline-Staffelung (4-stufig)

Quelle: `OfflineProgressService.CalculateOfflineProgress()` (Earnings) und
`AutoProductionService.CalculateEffectiveOfflineSeconds()` (Items) — identische Grenzen.

```
totalSeconds = effectiveDuration.TotalSeconds
first2h   = Min(totalSeconds, 7200)                                  // 0-2h    → 80%
next2h    = Min(Max(totalSeconds - 7200, 0), 7200)                   // 2-4h    → 35%
next4h    = Min(Max(totalSeconds - 14400, 0), 14400)                 // 4-8h    → 15%
remaining = Max(totalSeconds - 28800, 0)                             // 8h+     → 5%
earnings  = netPerSecond * (first2h*0.80 + next2h*0.35 + next4h*0.15 + remaining*0.05)
```
| Stufe | Zeitfenster | Grenze (s) | Rate |
|-------|-------------|------------|------|
| 1 | 0–2h | bis 7200 | 80% |
| 2 | 2–4h | 7200–14400 | 35% |
| 3 | 4–8h | 14400–28800 | 15% |
| 4 | 8h+ | ab 28800 | 5% |

Danach: `earnings = ApplyBoostsProRata(state, earnings, effectiveDuration)` (Abschnitt 6),
dann `SimulateWorkerStatesOffline(...)` + Auto-Produktion.

---

## 10. Player-XP-Kurve + AddXp / AddMoney / TrySpend / AddGoldenScrews

### XP-Kurve

Quelle: `Models/GameState.cs`

```
CalculateXpForLevel(level):
    wenn level <= 1: return 0
    return (int)(100 * Math.Pow(level - 1, 1.2))

XpForNextLevel = CalculateXpForLevel(PlayerLevel + 1)
LevelProgress  = clamp( (CurrentXp - CalculateXpForLevel(PlayerLevel)) / (XpForNextLevel - CalculateXpForLevel(PlayerLevel)), 0, 1 )
```
- Formel: **`XpForLevel(L) = 100 * (L-1)^1.2`** (kumulativer XP-Schwellwert fuer Level L).
- Hinweis: `GameBalanceConstants.XpPerLevelMultiplier = 200` und `XpForLevelMultiplier`-aehnliche Worker-Konstanten betreffen **Worker**-XP, nicht Player-XP. Player-XP nutzt ausschliesslich die obige `Math.Pow`-Kurve.

### AddXp

Quelle: `Services/GameStateService.Xp.cs`

```
wenn amount <= 0: return
unter Lock:
    wenn PlayerLevel >= LevelThresholds.MaxPlayerLevel (1500): return     // Max-Level-Cap, kein Overflow
    oldLevel = PlayerLevel
    wenn IsXpBoostActive: amount *= 2                                     // DailyReward XP-Boost ×2
    xpBonus = GetPrestigeXpBonus()                                        // Prestige-Shop XpMultiplier (Summe)
    wenn xpBonus > 0: amount = (int)(amount * (1 + xpBonus))
    CurrentXp += amount; TotalXp += amount
    while CurrentXp >= XpForNextLevel && PlayerLevel < 1500: PlayerLevel++; levelUps++
XpGained-Event feuern; bei levelUps>0 → neu freigeschaltete WorkshopTypes ermitteln + LevelUp-Event
```
- Max-Level-Cap: **1500** (`LevelThresholds.MaxPlayerLevel`). Min-Level: 1 (`MinPlayerLevel`).
- Reihenfolge der XP-Boni: XP-Boost (×2) ZUERST, dann Prestige-Shop XpMultiplier.

### AddMoney

Quelle: `GameStateService.Money.cs`

```
wenn amount <= 0: return
unter Lock: Money += amount; TotalMoneyEarned += amount; CurrentRunMoney += amount
MoneyChanged-Event (oldAmount, newAmount)
```
- `CurrentRunMoney` wird mit-erhoeht (Basis der Prestige-PP-Formel).

### TrySpendMoney

```
unter Lock:
    wenn amount <= 0 || Money < amount: return false
    Money -= amount; TotalMoneySpent += amount
MoneyChanged-Event; return true
```
- `CanAfford(amount) => Money >= amount`.

### AddGoldenScrews (Premium ×2)

Quelle: `GameStateService.Money.cs`

```
AddGoldenScrews(amount, fromPurchase = false):
    wenn amount <= 0: return
    wenn !fromPurchase:                                  // nur Gameplay-Quellen
        bonus = GetGoldenScrewBonus()                    // Prestige-Shop GoldenScrewBonus (Summe)
        bonus += ExternalGoldenScrewBonusProvider?.Invoke() ?? 0    // Ascension Golden-Era (additiv)
        wenn bonus > 0: amount = (int)Ceiling(amount * (1 + bonus))
        wenn IsPremium: amount *= 2                       // Premium +100% GS
    unter Lock: GoldenScrews += amount; TotalGoldenScrewsEarned += amount
    GoldenScrewsChanged-Event
```
- IAP-Kaeufe (`fromPurchase: true`) bekommen WEDER Prestige/Ascension-Bonus NOCH Premium-Verdopplung.
- `TrySpendGoldenScrews(amount)`: `amount <= 0 || GoldenScrews < amount` → false; sonst abziehen + `TotalGoldenScrewsSpent`.
- `CanAffordGoldenScrews(amount) => GoldenScrews >= amount`.

---

## 11. Meisterwerkzeuge-Bonus (im Income)

Quelle: `IncomeCalculatorService` + `GameLoopService`

- `_cachedMasterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools)` (im GameLoop einmalig gecacht, `-1` = dirty).
- Cache wird bei neuer MasterTool-Freischaltung in `CheckMasterTools` auf `-1` zurueckgesetzt.
- Anwendung im Brutto-Income: `grossIncome *= (1 + mtBonus)` (Reihenfolge-Position 5, siehe Abschnitt 3).
- Konkrete Bonus-Werte pro Tool (gesamt +74%) liegen in `Models/MasterTool.cs` (nicht Teil dieser Datei).

---

## 12. CalculateCosts-Inputs: Prestige-Shop-Effekt-Cache

Quelle: `GameLoopService.PrestigeCache.cs` + `PrestigeService.cs`

GameLoop-interner Prestige-Cache (`RefreshPrestigeEffectsIfNeeded`, dirty bei Kauf/Reset):
```
_cachedPrestigeIncomeBonus   = Σ (item.Effect.IncomeMultiplier [* count bei repeatable])
_cachedPrestigeRushBonus     = Σ (item.Effect.RushMultiplierBonus, nur nicht-repeatable)
_cachedPrestigeDeliveryBonus = Σ (item.Effect.DeliverySpeedBonus [* count])
_cachedPrestigeUpgradeDiscount = Σ (item.Effect.UpgradeDiscount, nur nicht-repeatable)
_cachedPrestigeIncomeBonus = Min(_cachedPrestigeIncomeBonus, 3.0)        // PrestigeIncome HART bei +300% gecappt
```
- Setzt zusaetzlich pro Workshop: `UpgradeDiscount = _cachedPrestigeUpgradeDiscount`, `VipCostReduction = _vipService.CostReduction`.
- `PrestigeService.GetCostReduction()` / `GetMoodDecayReduction()` / `GetXpMultiplier()` lesen einen eigenen Effekt-Cache (`RefreshEffectCacheIfNeeded`, dirty bei Kauf/Load), summiert aus `PrestigeShop`-Items.

GameStateService-interner Prestige-Bonus-Cache (`RefreshPrestigeBonusCacheIfNeeded`, dirty via `InvalidatePrestigeBonusCache` → feuert `PrestigeShopPurchased`):
```
_cachedGoldenScrewBonus = Σ (item.Effect.GoldenScrewBonus, nur nicht-repeatable)
_cachedXpBonus          = Σ (item.Effect.XpMultiplier, nur nicht-repeatable)
_cachedOrderRewardBonus = Σ (item.Effect.OrderRewardBonus [* count bei repeatable])
```

---

## 13. OrderRewardMultiplier (Soft-Cap 10x via Sqrt)

Quelle: `GameStateService.Orders.cs` `CalculateOrderRewardMultiplierUnlocked(order)`

Start `multiplier = 1`. Multiplikativ in dieser Reihenfolge:
```
1) Research RewardMultiplier:  researchRewardBonus = Σ (Researched.Effect.RewardMultiplier); wenn >0: *= (1 + bonus)
2) VehicleFleet-Gebaeude:      wenn OrderRewardBonus > 0: *= (1 + OrderRewardBonus)
3) Reputation:                 *= Reputation.ReputationMultiplier                 (0.7x–1.5x)
4) Event RewardMultiplier:     wenn aktiv && Effect.RewardMultiplier != 1.0 && (AffectedWorkshop == null || == order.WorkshopType): *= Effect.RewardMultiplier
5) Stammkunde:                 wenn IsRegularCustomerOrder && customer gefunden: *= customer.BonusMultiplier
6) Prestige-Shop OrderReward:  shopOrderBonus = Min(_cachedOrderRewardBonus, 1.0); wenn >0: *= (1 + shopOrderBonus)   // Cap +100%
7) Gilde Hall-OrderReward:     wenn GuildMembership.HallOrderRewardBonus > 0: *= (1 + HallOrderRewardBonus)
8) Gilde Hall-Everything:      wenn GuildMembership.HallEverythingBonus > 0: *= (1 + HallEverythingBonus)
```
Die beiden Gildenhallen-Faktoren (7/8) liegen NACH Prestige-Shop-OrderReward und VOR dem Sqrt-Soft-Cap
(`GameStateService.Orders.cs:373-376`) — sind also mit-gedeckelt.
Soft-Cap (Sqrt auf Ueberschuss):
```
cap = GameBalanceConstants.OrderRewardMultiplierSoftCap = 10.0
wenn multiplier > 10.0:
    multiplier = 10.0 + (decimal)Math.Sqrt((double)(multiplier - 10.0))
```
Beispiel (Code-Kommentar): Raw 15x → 10 + sqrt(5) ≈ 12.24x.
- `GetPrestigeShopOrderRewardBonus() = Min(_cachedOrderRewardBonus, 1.0)` → eigener +100%-Cap VOR dem Soft-Cap.

### CompleteActiveOrder — Reward-Anwendung

Quelle: `GameStateService.Orders.cs` `CompleteActiveOrder()`

```
moneyReward = order.FinalReward * CalculateOrderRewardMultiplierUnlocked(order)
xpReward    = order.FinalXp
// Material-Offer (VOR Combo/Doppel):
wenn MaterialOfferAccepted && MaterialOfferBonusMultiplier > 0:
    bonusFactor = 1 + MaterialOfferBonusMultiplier
    moneyReward *= bonusFactor; xpReward = (int)(xpReward * bonusFactor)
    ConsumeOrderMaterialReservation(order)
// Combo (PaintingGame):
wenn ComboMultiplier > 1: moneyReward *= ComboMultiplier; xpReward = (int)(xpReward * ComboMultiplier)
// Rewarded-Ad-Verdopplung (Premium ×3, sonst ×2):
wenn IsScoreDoubled: factor = IsPremium ? 3 : 2; moneyReward *= factor; xpReward = (int)(xpReward * factor)
```
Reputation-Rating beim Abschluss (avgRating → Sterne): Perfect→5, Good→4, Ok→3, sonst→2. `Reputation.AddRating(stars, reputationBonus)`.
Stammkunde-BonusMultiplier-Wachstum: ab >5 Perfects: `Min(1.5, 1.1 + (PerfectOrderCount-5)*0.02)`. Max 20 Stammkunden (aelteste raus).

### CompleteMaterialOrder

Quelle: `GameStateService.Orders.cs`

```
nur OrderType.MaterialOrder mit RequiredMaterials != null
prueft Verfuegbarkeit aller RequiredMaterials, zieht sie ab
reward  = order.EstimatedReward * CalculateOrderRewardMultiplierUnlocked(order)
xpReward = (int)(order.BaseXp * order.Difficulty.GetXpMultiplier() * OrderType.MaterialOrder.GetXpMultiplier())
OrderCompleted-Event mit MiniGameRating.Good
```

---

## 14. Auto-Complete-Schwellen (MiniGame)

Quelle: `GameStateService.Orders.cs` `CanAutoComplete(type, isPremium)`

```
baseThreshold = (type in {PipePuzzle, Blueprint, InventGame, DesignPuzzle, Inspection}) ? 20 : 30
threshold = isPremium ? baseThreshold / 2 : baseThreshold
return PerfectRatingCounts[(int)type] >= threshold
```
| MiniGame-Kategorie | Free-Schwelle | Premium-Schwelle |
|--------------------|---------------|------------------|
| Puzzle/Memory (PipePuzzle, Blueprint, InventGame, DesignPuzzle, Inspection) | 20 Perfects | 10 Perfects |
| Timing/Sonstige (Sawing, Wiring, Painting, RoofTiling, ForgeGame) | 30 Perfects | 15 Perfects |

`PerfectRatingCounts` wird bei Ascension zurueckgesetzt; `LifetimePerfectRatingCounts` (fuer Mastery) NICHT.
`RecordPerfectRating(type)` inkrementiert beide und feuert `PerfectRatingIncremented` (mit Lifetime-Count).
`RecordMiniGameResult(rating)`: Statistiken IMMER (auch QuickJobs), Task-Ergebnis nur bei `ActiveOrder != null`.
Score-Mapping (DailyChallenge): Perfect=100%, Good=75%, Ok=50%, Miss=0% (siehe App-CLAUDE.md; nicht in dieser Datei).

---

## 15. Auto-Produktion (Items)

Quelle: `Services/AutoProductionService.cs` + `GameBalanceConstants.cs`

### Intervalle + Unlock

```
GetProductionInterval(type):
    MasterSmith   → 60   (AutoProductionMasterSmithInterval)
    InnovationLab → 120  (AutoProductionInnovationLabInterval)
    sonst         → 180  (AutoProductionIntervalSeconds)
IsAutoProductionUnlocked(ws): ws.Level >= 50 (AutoProductionUnlockLevel)
```
Tier-1-Produkt-Mapping (`Tier1Products`):
| WorkshopType | ProductId |
|--------------|-----------|
| Carpenter | planks |
| Plumber | pipes |
| Electrician | cables |
| Painter | paint_mix |
| Roofer | roof_tiles |
| Contractor | concrete |
| Architect | blueprint |
| GeneralContractor | contract |
| MasterSmith | fittings |
| InnovationLab | prototype |

### ProduceForAllWorkshops (online, Tick-Offset 90 in 180er-Block)

- Pro freigeschaltetem Workshop: `workingWorkers` zaehlen; bei `>0` → `_warehouse.AddToInventory(productId, workingWorkers, type)` (oder direktes Inventar-Increment ohne DI).
- `Statistics.TotalItemsAutoProduced += workingWorkers`.

### AutoCraftHigherTiers (online, Tick-Offset 270 in 360er-Block)

- Tier-3 zuerst (ab WS-Level `AutoCraftTier3UnlockLevel = 320`), dann Tier-2 (ab `AutoCraftTier2UnlockLevel = 150`).
- Max **1 Rezept pro Workshop pro Tick**.
- Cross-Workshop-Inputs ab Spielerlevel `MaterialOrderCrossWorkshopLevel = 100` (`CraftingRecipe.GetEffectiveInputs(recipe, playerLevel)`).
- Output-Stack-Limit via `_warehouse.CanAddToInventory(...)` geprueft (sonst kein Craft, kein Material-Burn).

### CalculateOfflineProduction

```
effectiveSeconds = CalculateEffectiveOfflineSeconds(offlineSeconds)    // gleiche 80/35/15/5-Staffelung
pro Workshop (Level>=50, workingWorkers>0):
    interval = GetProductionInterval(type)
    itemsProduced = (int)(effectiveSeconds / interval * workingWorkers)
    cap = state.WarehouseStackLimit - current; wenn cap<=0: skip; wenn itemsProduced>cap: itemsProduced=cap
```

### MasterSmith-Passiv (Tick-Offset 45 in 60er-Block)

Quelle: `GameLoopService.PeriodicChecks.cs` `ProduceMasterSmithMaterials`

- Tier-1-Pool: `["planks","pipes","cables","paint_mix","roof_tiles"]` (zufaellig pro arbeitendem Worker, 1 Item/Worker).
- Respektiert `_warehouseService.CurrentStackLimit` + `FreeSlotCount`. `Statistics.TotalItemsCrafted += produced`.

### InnovationLab-Bonus (jeder Tick)

Quelle: `GameLoopService.PeriodicChecks.cs` `ApplyInnovationLabBonus`

- Wenn aktive Forschung + InnovationLab besetzt: `activeResearch.BonusSeconds += workingWorkers * 0.5` (0.5s Extra-Fortschritt pro arbeitendem Worker pro Tick).

---

## 16. Automation (AutoCollect / AutoAccept / AutoAssign)

Quelle: `GameLoopService.Automation.cs`. Unlock-Gates: `GameStateService.cs` + `LevelThresholds.cs`.

### Unlock-Gates (permanent freigeschaltet nach 1. Prestige)

```
HasEverPrestiged => Prestige.TotalPrestigeCount > 0
IsAutoCollectUnlocked => HasEverPrestiged || PlayerLevel >= 15   (LevelThresholds.AutoCollect)
IsAutoAcceptUnlocked  => HasEverPrestiged || PlayerLevel >= 25   (LevelThresholds.AutoAccept)
IsAutoAssignUnlocked  => HasEverPrestiged || PlayerLevel >= 20   (LevelThresholds.AutoAssign)
```

### ProcessAutomation (alle 5 Ticks, Offset 3)

- **AutoCollect**: wenn `Automation.AutoCollectDelivery && IsAutoCollectUnlocked && PendingDelivery != null && !IsExpired` → einsammeln. Effekte: Money→`AddMoney`, GoldenScrews→`AddGoldenScrews((int)Round(Amount))`, Experience→`AddXp((int)Amount)`, MoodBoost→alle Worker `Mood = Min(100, Mood + Amount)`, SpeedBoost→`SpeedBoostEndTime = UtcNow.AddMinutes(Amount)` + `InvalidateIncomeCache()`. `Statistics.TotalDeliveriesClaimed++`.
- **AutoAccept**: wenn `Automation.AutoAcceptOrder && IsAutoAcceptUnlocked && ActiveOrder == null && AvailableOrders.Count > 0` → besten Auftrag nach `BaseReward` waehlen. Bei `AutoAcceptOnlyStandard` werden Live-/Premium-Auftraege uebersprungen. Setzt `ActiveOrder`, spiegelt in `ParallelOrdersByWorkshop`, entfernt aus `AvailableOrders`.
- Mutationen unter `ExecuteWithLock`; Events (`AutoCollectedDelivery`, `AutoAcceptedOrder`) NACH Lock-Release.

### ProcessAutoAssign (alle 60 Ticks, Offset 30)

- Wenn `Automation.AutoAssignWorkers && IsAutoAssignUnlocked`: ruhende Worker mit `Fatigue <= 20` → `IsResting = false`.

---

## 17. Lieferant-System (CheckAndGenerateDelivery)

Quelle: `GameLoopService.PeriodicChecks.cs` (alle 10 Ticks)

- Wenn `IsDeliveryBlocked()` (KeinNetz-Challenge) → return.
- Wenn `PendingDelivery != null`: abgelaufene entfernen, return.
- Wenn `now < NextDeliveryTime` → return.
- Sonst: `delivery = SupplierDelivery.GenerateRandom(state)`, `PendingDelivery = delivery`.
- Naechstes Intervall: `baseIntervalSec = Random.Shared.Next(120, 300)` (2–5 Min). Mit Prestige-Delivery-Bonus: `baseIntervalSec *= (1 - Min(_cachedPrestigeDeliveryBonus, 0.50))`. `NextDeliveryTime = now.AddSeconds(baseIntervalSec)`.
- `DeliveryArrived`-Event.

---

## 18. Reputation-Decay (24h-Block)

Quelle: `GameLoopService.PeriodicChecks.cs` (zeitbasiert, jeder Tick geprueft)

```
wenn (now - state.LastReputationDecay).TotalHours >= 24:
    LastReputationDecay = now
    tierBefore = Reputation.CurrentTier
    showroom = GetBuilding(Showroom)
    wenn showroom != null && DailyReputationGain > 0:
        Reputation.ReputationScore = Min(100, Score + (int)Ceiling(DailyReputationGain))
    Reputation.DecayReputation()
    RaiseReputationTierChangedIfNeeded(tierBefore)
```

---

## 19. MasterTool-Check + ExtraWorkerSlots

Quelle: `GameLoopService.PeriodicChecks.cs`

- `CheckMasterTools` (alle 120 Ticks): Early-Exit wenn alle gesammelt. Pro Definition: `MasterTool.CheckEligibility(def.Id, state)` → `CollectedMasterTools.Add(def.Id)` + Cache invalidieren (`_cachedMasterToolBonus = -1`). `MasterToolUnlocked`-Event nach Lock.
- `UpdateExtraWorkerSlots` (jeder Tick): `totalExtra = researchSlots + buildingSlots(WorkshopExtension) + guildSlots(ResearchWorkerSlotBonus)`. `levelResistance = Min(LevelResistanceBonus, 0.50)`. Nur zuweisen bei Aenderung; setzt `ws.ExtraWorkerSlots` + `ws.LevelResistanceBonus`.

---

## 20. Order-Expiry (60er-Block, Offset 0)

Quelle: `GameLoopService.PeriodicChecks.cs` (unter `ExecuteWithLock`)

- `AvailableOrders.RemoveAll(o => !o.IsLive && o.IsExpired)` (Live-Orders separat ueber `ExpireOldLiveOrders` alle 3 Ticks).
- `ActiveOrder`-Expiry NUR wenn `IsExpired && CurrentTaskIndex == 0 && TaskResults.Count == 0` (laufende MiniGame-Session geschuetzt). Reservierung freigeben (`ReleaseExpiredOrderReservation`), `ActiveOrder = null`, aus `ParallelOrdersByWorkshop` entfernen, `OrderExpired`-Event.
- Parallele Orders: abgelaufene (≠ ActiveOrder) Reservierung freigeben + entfernen, je ein `OrderExpired`-Event.

Live-Order-Pausierung: `PauseAllLiveOrders` setzt `PausedAt`; `ResumeAllLiveOrders` akkumuliert `AccumulatedPauseDuration` mit **5-Minuten-Cap** (`TimeSpan.FromMinutes(5)`), negative Dauer → 0.

---

## 21. Workshop-Upgrade / Hire / Purchase (GameState-Mutationen)

Quelle: `GameStateService.Workshop.cs`

- `TryUpgradeWorkshop(type)`: prueft `CanUpgrade`; `cost = workshop.UpgradeCost`; Inflations-Challenge: `cost = Round(cost * GetUpgradeCostMultiplier(), 0)` wenn >1.0; bei Geld → `Money -= cost`, `TotalMoneySpent += cost`, `Level++`, `InvalidateIncomeCache()`. XP-Reward: **`5 + newLevel / 10`** (`AddXp`).
- `TryUpgradeWorkshopBulk(type, count)`: `maxUpgrades = count==0 ? Workshop.MaxLevel - Level : count`; pro Upgrade `totalXp += 5 + Level/10`.
- `TryHireWorker(type)`: `maxWorkers = GetMaxWorkers(workshop.MaxWorkers)` (Spartaner-Challenge cappt auf 3); `cost = HireWorkerCost`; `Worker.CreateRandom(type)`.
- `TryPurchaseWorkshop(type, costOverride = -1)`: `cost = costOverride>=0 ? costOverride : type.GetUnlockCost()`; nach Kauf `AddXp(50)`.
- `CanPurchaseWorkshop`: `!UnlockedWorkshopTypes.Contains(type)` && `PlayerLevel >= type.GetUnlockLevel()` && `type.GetRequiredPrestige() <= Prestige.TotalPrestigeCount` && nicht durch SoloMeister-Challenge blockiert.

Upgrade-/Kosten-Kurven-Konstanten (`GameBalanceConstants.cs`, fuer Vollstaendigkeit):
- `IncomeBaseMultiplier = 1.02`, `UpgradeCostExponent = 1.07`, `UpgradeCostReducedExponent = 1.06` (ab Level 500), `UpgradeCostBase = 200`, `UpgradeCostLevel1 = 100`, `PrestigeDiscountCap = 0.50`.
- `HireWorkerCostBase = 50`, `HireWorkerCostExponent = 1.5`.
- Detail-Formeln in `Models/WorkshopFormulas.cs` / `Workshop.cs` (nicht Teil dieser Datei).

---

## 22. Relevante GameBalanceConstants (Zahlenwerte, Auszug Core-Economy)

Quelle: `Models/GameBalanceConstants.cs`

| Konstante | Wert |
|-----------|------|
| `IncomeBaseMultiplier` | 1.02 |
| `UpgradeCostExponent` | 1.07 |
| `UpgradeCostReducedExponent` (ab Lv500) | 1.06 |
| `UpgradeCostBase` | 200 |
| `UpgradeCostLevel1` | 100 |
| `PrestigeDiscountCap` | 0.50 |
| `WorkshopMaxLevel` | 1000 |
| `WorkerSlotInterval` | 50 |
| `WorkerSlotMax` | 20 |
| `MaxAdBonusWorkerSlots` | 3 |
| `SpecializationUnlockLevel` | 50 |
| `SpecializationRespecCostGoldenScrews` | 20 |
| `SpecializationFreeRespecBelowLevel` | 75 |
| `MaxParallelOrders` | 3 |
| `MaxAuraBonus` | 0.50 |
| `DiminishingReturnsPerTierPrestige` | 0.2 |
| `BonusPpPerAscensionLevel` | 0.5 |
| `EternalMasteryBonusPerPrestige` | 0.005 (+0.5%) |
| `EternalMasterySoftCapThreshold` | 50 |
| `EternalMasteryBonusPer5Prestiges` | 0.025 (+2.5%) |
| `EternalMasteryBonusPer10Prestiges` | 0.05 (+5%) |
| `MaxRepeatableShopPurchases` | 8 |
| `AutoProductionIntervalSeconds` | 180 |
| `AutoProductionInnovationLabInterval` | 120 |
| `AutoProductionMasterSmithInterval` | 60 |
| `AutoProductionUnlockLevel` | 50 |
| `AutoCraftTier2UnlockLevel` | 150 |
| `AutoCraftTier3UnlockLevel` | 320 |
| `CraftingSellPriceLogDivisor` | 15.0 |
| `MaterialOrderRewardMultiplier` | 1.8 |
| `MaterialOrderXpMultiplier` | 1.5 |
| `MaterialOrdersPerDay` | 5 |
| `MaterialOrderDeadlineHours` | 4 |
| `MaterialOrderCrossWorkshopLevel` | 100 |
| `MaterialOfferUnlockLevel` | 30 |
| `MaterialOfferChance` | 0.35 |
| `MaterialOfferBonusQuick` | 0.25 |
| `MaterialOfferBonusStandard` | 0.30 |
| `MaterialOfferBonusLarge` | 0.40 |
| `MaterialOfferBonusCooperation` | 0.50 |
| `MaterialOfferBonusWeekly` | 0.60 |
| `MaxHeirloomsPerRun` | 3 |
| `MaxHeirloomsPerRunPremium` | 4 |
| `HeirloomBonusPerItem` | 0.02 (+2%) |
| `PermanentHeirloomBonusPerItem` | 0.005 (+0.5%) |
| `MaxPermanentHeirlooms` | 50 |
| `OrderRewardMultiplierSoftCap` | 10.0 |
| `CraftingSellMultiplierSoftCap` | 8.0 |
| `CraftingSellMultiplierHardCap` | 12.0 |
| `SplashMinimumDisplayMs` | 800 |
| `MoneyAnimationInterpolationFactor` | 0.15 |
| `XpPerLevelMultiplier` (Worker, nicht Player) | 200 |
| `TrainingXpPerHour` (Worker) | 50 |
| `MoodDecayPerHour` (Worker) | 3.0 |
| `FatigueIncreasePerHour` (Worker) | 12.5 |
| `RestHoursNeeded` (Worker) | 4 |

PermanentMultiplier-Cap (20x): `RecalculateIncomeCache` (`GameState.cs`) und `MaxPermanentMultiplier = 20.0m` (`PrestigeService.cs`).

---

## 23. LevelThresholds (Core-Gates)

Quelle: `Models/LevelThresholds.cs`

| Gate | Level |
|------|-------|
| `BannerStrip` | 3 |
| `QuickJobs` / `HintQuickJobs` | 2 |
| `CraftingResearch` / `HintCrafting` | 8 |
| `ManagerSection` / `HintManagerUnlock` | 10 |
| `MasterToolsSection` / `HintMasterTools` | 20 |
| `AutoCollect` | 15 |
| `AutoAccept` | 25 |
| `AutoAssign` | 20 |
| `HintWorkerUnlock` | 3 |
| `HintAutomation` | 15 |
| `HintPrestige` | 50 |
| `TournamentSection` | 35 |
| `SeasonalEventSection` | 45 |
| `BattlePassSection` | 55 |
| `PrestigeShopUnlock` | 50 |
| `TutorialHintMaxLevel` | 3 |
| `WorkshopCeremonyThreshold` | 50 |
| `ReputationWarningThreshold` | 50 |
| `ReputationHighlightThreshold` | 80 |
| `MinPlayerLevel` | 1 |
| `MaxPlayerLevel` | 1500 |
| `TabUnlockLevels` (Werkstatt/Imperium/Missionen/Gilde/Shop) | [1, 5, 8, 15, 3] |

---

## 24. Event-Einmaleffekte (Tick-Verarbeitung)

Quelle: `GameLoopService.cs` `OnTimerTick` (Schritt 9)

- Bei neuem `state.ActiveEvent?.Id != _lastAppliedSpecialEffectId`:
  - `mood_drop_all_20` → alle Worker `Mood = Max(0, Mood - 20)` (einmalig).
  - `eventEffects.ReputationChange != 0` → `Reputation.ReputationScore = Clamp(Score + (int)ReputationChange, 0, 100)` + `RaiseReputationTierChangedIfNeeded`.
- Bei `currentEventId == null`: `_lastAppliedSpecialEffectId = null`.
- `tax_10_percent` wirkt dauerhaft im Income (`grossIncome *= 0.90`, Abschnitt 3), nicht als Einmaleffekt.

---

## 25. Startup-Sequenz (GameStartupCoordinator)

Quelle: `Services/GameStartupCoordinator.cs` `RunAsync()`

1. Spielstand laden (`LoadAsync`); falls nicht initialisiert → `Initialize()` (neuer State).
2. `CheckCloudSaveAsync()` — Cloud-Save-Abgleich (Toleranz 5s gegen Clock-Skew; bei `StateVersion > CurrentStateVersion(7)` → Alert statt Download; bei lokal-korrupt immer Cloud bevorzugen).
3. `FpsProfile.SetCurrent(Settings.GraphicsQuality)`.
4. Sprache: gespeicherte Sprache setzen oder Geraetesprache uebernehmen.
5. `SettingsVm.ReloadSettings()`.
6. Recover: `if (ActiveOrder != null) CancelActiveOrder()` (MiniGame-State nicht persistiert).
7. `RefreshFromState()`; bei `AvailableOrders.Count < 3` → `RefreshOrders()`.
8. QuickJobs / DailyChallenges / WeeklyMissions / LuckySpin initialisieren.
9. `IsLoading = false`; Welcome-Flow (`RunStartupDialogSequence`); FTUE `StartIfNeeded()`.
10. `_gameLoopService.Start()`.
11. Verzoegert: WhatsNew (`Task.Delay(2500)` + bis 4s warten), Analytics-Consent.

---

## 26. State-Initialisierung + Reset

Quelle: `Services/GameStateService.cs`

- `Initialize(loadedState=null)`: `_state = loadedState ?? GameState.CreateNew()`, `IsInitialized = true`, `_prestigeBonusCacheDirty = true`, `StateLoaded`-Event.
- `Reset()`: `_state = GameState.CreateNew()`, Cache dirty, `StateLoaded`-Event.
- `ExecuteWithLock(Action)` / `ExecuteWithLock<T>(Func<T>)`: `lock (_stateLock)`.
- `CurrentStateVersion = 7` (`GameState.cs`).
- Starting-Money / `CreateNew()`-Initialwerte: Detail in `GameState.CreateNew()` (nicht im hier gelesenen Ausschnitt; nur Worker-`AssignedWorkshop = Carpenter` + KeptWorkers-Restore wurde gesehen).

---

## Offene / nicht in diesen Dateien gefundene Punkte

- Exakte Guild-Tick-Modulo-Offsets liegen in `GuildTickService.cs` + den Gilde-Sub-Services (hier nur aus der Service-Doku uebernommen).
- `Workshop.GrossIncomePerSecond` / `BaseIncomePerWorker` / `TotalCostsPerHour` / `GetWorkerLevelFitFactor` — Detail-Formeln in `Models/Workshop.cs` + `WorkshopFormulas.cs` (nicht Teil dieser Datei).
- MasterTool-Einzelbeträge (gesamt +74%) in `Models/MasterTool.cs`.
- `GameState.CreateNew()` Start-Geld/Start-Workshops nicht vollstaendig im gelesenen Ausschnitt (nur Worker-Restore-Logik).
- `Worker`-Tier-Effizienzen/Loehne in `Models/Worker.cs` (separat).

---

# 03 — Auftraege, QuickJob, Crafting, Tools, Equipment, Markt, Lager, Materialien

Verbindliche Werte- und Mechanik-Spezifikation, extrahiert direkt aus dem Avalonia-Code von
HandwerkerImperium. Jeder Wert stammt exakt aus der angegebenen Quelldatei. Wo ein Wert nur
berechnet/abgeleitet wird, ist die Formel angegeben. "(nicht im Code gefunden)" markiert Luecken.

Quelldateien-Wurzel: `HandwerkerImperium.Shared/`

---

## 1. Auftraege (Orders)

### 1.1 Order-Modell — persistierte/berechnete Felder

Quelle: `Models/Order.cs`

| Feld | Typ | Default | Bemerkung |
|------|-----|---------|-----------|
| Id | string | `Guid.NewGuid().ToString()` | |
| TitleKey / TitleFallback | string | "" | |
| WorkshopType | WorkshopType | — | |
| Difficulty | OrderDifficulty | `Medium` | |
| OrderType | OrderType | `Standard` | |
| Strategy | OrderStrategy | `Standard` | Spieler waehlt vor MiniGame-Start |
| HasHardFailed | bool | false | true → FinalReward=0, FinalXp=0 |
| IsLive | bool | false | Live-Stream-Auftrag |
| IsPremium | bool | false | VIP-Auftrag |
| Tasks | List<OrderTask> | [] | |
| BaseReward | decimal | 0 | eingefroren bei Generierung, recompute via RecalculateAvailableOrderRewards |
| BaseXp | int | 0 | |
| RequiredLevel | int | 1 | bei Generierung `Max(1, workshopLevel - 1)` |
| CreatedAt | DateTime | `UtcNow` | |
| ExpiresAt | DateTime? | null | nur Live-Auftraege |
| Deadline | DateTime? | null | Weekly + MaterialOrder |
| PausedAt | DateTime? | null | App-Hintergrund-Pause |
| AccumulatedPauseDuration | TimeSpan | 0 | Cap 5 Minuten |
| CustomerId | string? | null | gesetzt bei Stammkunde |
| CustomerName | string | "" | |
| CustomerAvatarSeed | string | `Guid…[..8]` | bei Generierung `nameSeed.ToString("X8")` |
| RequiredWorkshops | List<WorkshopType>? | null | nur Cooperation |
| RequiredMaterials | Dictionary<string,int>? | null | nur MaterialOrder |
| MaterialOffer | Dictionary<string,int>? | null | optionales Angebot |
| MaterialOfferBonusMultiplier | double | 0.0 | 0 = kein Offer |
| MaterialOfferAccepted | bool | false | |
| ReputationBonus | decimal | 0 | |
| IsScoreDoubled | bool | false | Rewarded-Ad-Verdopplung |
| ComboMultiplier | decimal | 1m | aus PaintingGame (1.0 = kein Combo) |
| CurrentTaskIndex | int | 0 | |
| TaskResults | List<MiniGameRating> | [] | |

OrderTask: `GameType` (MiniGameType), `DescriptionKey`, `DescriptionFallback`.

Berechnete Felder:
- `IsCompleted` = `CurrentTaskIndex >= Tasks.Count`
- `CurrentTask` = `Tasks[CurrentTaskIndex]` solange Index < Count
- `HasMaterialOffer` = `MaterialOffer is { Count: > 0 }`
- `IsRegularCustomerOrder` = `CustomerId != null`
- `IsExpired` = `(Deadline != null && UtcNow > Deadline) || (ExpiresAt != null && GetEffectiveNow() > ExpiresAt)`
- `RecordTaskResult(rating)` haengt Rating an TaskResults an und erhoeht `CurrentTaskIndex++`.

`GetEffectiveNow()` (Live-Order-Pause-Logik): `now - pauseTotal`, wobei `pauseTotal = AccumulatedPauseDuration` plus laufende Pause (`now - PausedAt`, min 0) gecappt auf 5 Minuten (`TimeSpan.FromMinutes(5)`).

`LiveCountdownSeconds`: null wenn nicht Live oder ExpiresAt null, sonst `(ExpiresAt - GetEffectiveNow()).TotalSeconds`, min 0.
`LiveCountdownText`: `"{m}m {ss:00}s"` ab >=60s, sonst `"{(int)sec}s"`.

### 1.2 Final-Reward-Formel (Order-Abschluss)

Quelle: `Models/Order.cs` (FinalReward / FinalXp)

```
FinalReward = HasHardFailed ? 0
            : TaskResults.Count == 0 ? 0
            : BaseReward
              * avg(TaskResults.GetRewardPercentage())
              * Difficulty.GetRewardMultiplier()
              * OrderType.GetRewardMultiplier()
              * Strategy.GetRewardMultiplier()

FinalXp = HasHardFailed ? 0
        : TaskResults.Count == 0 ? 0
        : (int)( BaseXp
                 * avg(TaskResults.GetXpPercentage())
                 * Difficulty.GetXpMultiplier()
                 * OrderType.GetXpMultiplier()
                 * Strategy.GetXpMultiplier() )
```

`EstimatedReward` / `CalculateEstimatedReward()` (Dashboard-Anzeige, geht von Good=100% aus):
`BaseReward * Difficulty.GetRewardMultiplier() * OrderType.GetRewardMultiplier() * Strategy.GetRewardMultiplier()` (KEIN avg-Faktor — implizit 1.0).
`EstimatedXp` analog mit XP-Multiplikatoren.

Hinweis: ComboMultiplier, IsScoreDoubled, ReputationBonus sind NICHT Teil von FinalReward/FinalXp — sie werden ausserhalb (in der Completion-Logik des MainViewModel/EconomyVM) angewendet. (Anwendungslogik nicht in den hier gelesenen Dateien.)

### 1.3 MiniGameRating — Reward-/XP-Prozentwerte

Quelle: `Models/Enums/MiniGameResult.cs` (enum MiniGameRating, MiniGameRatingExtensions)

| Rating | Wert | GetRewardPercentage | GetXpPercentage |
|--------|------|---------------------|-----------------|
| Miss | 0 | 0.20 (20%) | 0.20 (20%) |
| Ok | 1 | 0.50 (50%) | 0.50 (50%) |
| Good | 2 | 1.00 (100%) | 1.00 (100%) |
| Perfect | 3 | 1.50 (150%) | 1.50 (150%) |

Spread Miss→Perfect = 7.5x (identisch Reward + XP).

### 1.4 ComputeBaseRewardAndXp — zentrale Basis-Formel

Quelle: `Services/OrderGeneratorService.cs` (ComputeBaseRewardAndXp), Multiplikator aus `Models/Enums/WorkshopType.cs` (GetBaseIncomeMultiplier)

```
netIncomePerSecond = Max(0, state.NetIncomePerSecond)
perTaskReward      = Max(100 + playerLevel * 100, netIncomePerSecond * 300)
taskMultiplier     = taskCount * (1.0 + (taskCount - 1) * 0.15)
baseReward         = perTaskReward * taskMultiplier * WorkshopType.GetBaseIncomeMultiplier()
   wenn GuildMembership.ResearchRewardBonus > 0:  baseReward *= (1 + ResearchRewardBonus)
baseXp             = 25 * workshopLevel * taskCount
   wenn GuildMembership.ResearchXpBonus > 0:      baseXp = (int)(baseXp * (1 + ResearchXpBonus))
```

`BaseReward` der Order wird mit `Math.Round(baseReward)` gespeichert.

WorkshopType.GetBaseIncomeMultiplier():

| Workshop | Multiplikator |
|----------|---------------|
| Carpenter | 1.0 |
| Plumber | 1.5 |
| Electrician | 2.0 |
| Painter | 2.5 |
| Roofer | 3.0 |
| Contractor | 4.0 |
| Architect | 5.0 |
| GeneralContractor | 7.0 |
| MasterSmith | 3.0 |
| InnovationLab | 5.0 |

taskMultiplier-Beispiele: 1 Task = 1.0; 2 = 2.30; 3 = 3.90; 4 = 5.80; 5 = 8.00; 6 = 10.50; 10 = 23.50.

### 1.5 RecalculateAvailableOrderRewards (Neu-Berechnung wartender Orders)

Quelle: `Services/OrderGeneratorService.cs` (RecalculateAvailableOrderRewards)

Unter State-Lock; ueberspringt Orders mit `TaskResults.Count > 0` und `OrderType == MaterialOrder`.
Holt aktuelles Workshop-Level, ruft ComputeBaseRewardAndXp. Bei `order.IsPremium`: `newBaseReward *= 3`, `newBaseXp = (int)(newBaseXp * 2.5)`. Schreibt `BaseReward = Math.Round(...)`, `BaseXp`.

### 1.6 OrderType — 6 Typen, Multiplikatoren, Task-Counts, Unlocks

Quelle: `Models/Enums/OrderType.cs` (OrderType + OrderTypeExtensions), MaterialOrder-Konstanten aus `Models/GameBalanceConstants.cs`

| OrderType | Wert | TaskCount (min,max) | RewardMult | XpMult | UnlockLevel | HasDeadline | Deadline | Mehrere Workshops |
|-----------|------|---------------------|-----------|--------|-------------|-------------|----------|-------------------|
| Quick | 0 | (1,1) | 0.6 | 0.5 | 1 | nein | — | nein |
| Standard | 1 | (2,3) | 1.0 | 1.0 | 1 | nein | — | nein |
| Large | 2 | (4,6) | 1.8 | 2.0 | 10 | nein | — | nein |
| Weekly | 3 | (10,10) | 3.0 | 3.0 | 20 | ja | 7 Tage | nein |
| Cooperation | 4 | (3,3) | 2.5 | 3.0 | 15 | nein | — | ja |
| MaterialOrder | 5 | (0,0) | 1.8 (MaterialOrderRewardMultiplier) | 1.5 (MaterialOrderXpMultiplier) | 50 (AutoProductionUnlockLevel) | ja | 4 Std. (MaterialOrderDeadlineHours) | nein |

Icon-Mapping (GameIconKind): Quick=Flash, Standard=ClipboardList, Large=ClipboardTextMultiple, Weekly=CalendarCheck, Cooperation=Handshake, MaterialOrder=PackageVariantClosed.
LocalizationKey = `"OrderType" + typ` (z.B. OrderTypeQuick).

### 1.7 OrderDifficulty — 4 Stufen, Multiplikatoren, MiniGame-Parameter

Quelle: `Models/Enums/OrderDifficulty.cs` (OrderDifficulty + OrderDifficultyExtensions)

| Difficulty | Wert | RewardMult | XpMult | PerfectZoneSize | SpeedMult | ReqReputation |
|------------|------|-----------|--------|-----------------|-----------|---------------|
| Easy | 1 | 1.0 | 1.0 | 0.20 | 0.9 | 0 |
| Medium | 2 | 1.5 | 1.75 | 0.12 | 1.2 | 0 |
| Hard | 3 | 3.5 | 3.0 | 0.09 | 1.6 | 0 |
| Expert | 4 | 5.0 | 5.5 | 0.06 | 2.2 | 80 |

GetStars: Easy=1 Stern, Medium=2, Hard=3, Expert=4 (Unicode U+2B50 wiederholt).

### 1.8 OrderStrategy — 3 Strategien

Quelle: `Models/Enums/OrderStrategy.cs` (OrderStrategy + OrderStrategyExtensions)

| Strategy | Wert | RewardMult | XpMult | ToleranceMult (Zonen) | SpeedMult | TimeMult | HasHardFail | ReputationPenaltyOnMiss |
|----------|------|-----------|--------|------------------------|-----------|----------|-------------|--------------------------|
| Safe | 0 | 0.75 | 0.75 | 1.5 (+50% breiter) | 0.7 | 1.3 (+30% Zeit) | nein | 0 |
| Standard | 1 | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | nein | 0 |
| Risk | 2 | 2.0 | 1.75 | 0.5 (-50% schmaler) | 1.3 | 0.7 (-30% Zeit) | ja | -10 |

NameKeys: OrderStrategySafe/Standard/Risk. DescKeys: …SafeDesc/…StandardDesc/…RiskDesc.

### 1.9 DetermineOrderType — level-abhaengige Wuerfeltabelle

Quelle: `Services/OrderGeneratorService.cs` (DetermineOrderType)

`roll = Random.Shared.Next(100)` (0..99). Reputation/Gilde/Research senken den effektiven Roll:
```
adjustedRoll = Clamp( roll - (OrderQualityBonus + GuildOrderQualityBonus + ResearchPremiumOrderChance) * 100, 0, 100 )
```
- `OrderQualityBonus` = `state.Reputation.OrderQualityBonus` (Score<30: -0.10, <60: 0, <80: 0.10, sonst 0.20 — Quelle: `Models/CustomerReputation.cs`)
- `GuildOrderQualityBonus` = `state.GuildMembership.ResearchOrderQualityBonus` (Default 0)
- `ResearchPremiumOrderChance` = `_researchService.GetTotalEffects().PremiumOrderChance` (Default 0)

`unlockedWorkshops` = Anzahl freigeschalteter Workshops.

Entscheidungstabelle (playerLevel):

| playerLevel | Bedingung | Schwellen (adjustedRoll) |
|-------------|-----------|--------------------------|
| < 10 | — | immer Standard |
| < 15 | — | <70 Standard, sonst Large |
| < 20 | unlockedWorkshops >= 2 | <55 Standard, <80 Large, sonst Cooperation |
| < 20 | unlockedWorkshops < 2 | <70 Standard, sonst Large |
| >= 20 | unlockedWorkshops >= 2 | <45 Standard, <70 Large, <85 Cooperation, sonst Weekly |
| >= 20 | unlockedWorkshops < 2 | <55 Standard, <80 Large, sonst Weekly |

DetermineOrderType produziert nie Quick oder MaterialOrder (Quick/MaterialOrder kommen aus eigenen Pfaden).

### 1.10 GetDifficulty — Matrix (WS-Level x Prestige, + Reputation-Gate)

Quelle: `Services/OrderGeneratorService.cs` (GetDifficulty)

`roll = Random.Shared.Next(100)` (0..99). prestigeCount = `state.Prestige.TotalPrestigeCount`. Match auf `(workshopLevel, prestigeCount)`:

WS-Level 1–25:
| Prestige | Verteilung |
|----------|-----------|
| 0 | <80 Easy, sonst Medium |
| 1 | <65 Easy, <90 Medium, <95 Hard, sonst Expert |
| 2 | <50 Easy, <80 Medium, <95 Hard, sonst Expert |
| >=3 | <40 Easy, <70 Medium, <90 Hard, sonst Expert |

WS-Level 26–100:
| Prestige | Verteilung |
|----------|-----------|
| 0 | <45 Easy, <90 Medium, sonst Hard |
| 1 | <25 Easy, <65 Medium, <90 Hard, sonst Expert |
| 2 | <15 Easy, <45 Medium, <80 Hard, sonst Expert |
| >=3 | <5 Easy, <30 Medium, <65 Hard, sonst Expert |

WS-Level 101–300:
| Prestige | Verteilung |
|----------|-----------|
| 0 | <15 Easy, <60 Medium, sonst Hard |
| 1 | <5 Easy, <30 Medium, <75 Hard, sonst Expert |
| 2 | <15 Medium, <60 Hard, sonst Expert |
| >=3 | <10 Medium, <50 Hard, sonst Expert |

WS-Level 301–700:
| Prestige | Verteilung |
|----------|-----------|
| 0 | <5 Easy, <35 Medium, sonst Hard |
| 1 | <10 Medium, <60 Hard, sonst Expert |
| 2 | <5 Medium, <45 Hard, sonst Expert |
| >=3 | <30 Hard, sonst Expert |

WS-Level 701+:
| Prestige | Verteilung |
|----------|-----------|
| 0 | <20 Medium, sonst Hard |
| 1 | <5 Medium, <45 Hard, sonst Expert |
| 2 | <30 Hard, sonst Expert |
| >=3 | <20 Hard, sonst Expert |

Expert-Gate: Wenn Ergebnis Expert UND `reputation < 80` (`OrderDifficulty.Expert.GetRequiredReputation()`), faellt es auf Hard zurueck. reputation = `state.Reputation.ReputationScore`.

### 1.11 Template-Auswahl + Task-Generierung

Quelle: `Services/OrderGeneratorService.cs` (GenerateOrder, _templates)

- `maxTemplateIndex = Min(templates.Count - 1, (workshopLevel - 1) / 2)` — hoehere WS-Level schalten schwerere Templates frei.
- Template per `Random.Shared.Next(0, maxTemplateIndex + 1)`.
- `targetTaskCount = Random.Shared.Next(minTasks, maxTasks + 1)` aus OrderType.GetTaskCount().
- Standard/Large/Weekly: GameType = `template.GameTypes[i % template.GameTypes.Length]`, DescriptionKey = `"task_" + gametype.ToLower()`, Fallback = `gameType.GetLocalizationKey()`.
- Cooperation: zweiter freigeschalteter Workshop (zufaellig, != primaer) liefert secondTemplate; Tasks wechseln ab (gerade Index = template, ungerade = secondTemplate), `idx = i / 2 % src.GameTypes.Length`. `RequiredWorkshops = [workshopType, secondType]` wenn secondType != primaer.

Order-Templates pro WorkshopType (TitleKey, TitleFallback, MiniGame-Sequenz):

Carpenter:
| TitleKey | Fallback | GameTypes |
|----------|----------|-----------|
| order_shelf | Build a Shelf | Sawing |
| order_cabinet | Build a Cabinet | Sawing, Planing |
| order_table | Build a Table | Sawing, Planing, Sawing |
| order_deck | Build a Deck | Measuring, Sawing, Sawing |
| order_shed | Build a Garden Shed | Sawing, Sawing, Sawing |

Plumber:
| order_faucet | Replace Faucet | PipePuzzle |
| order_toilet | Install Toilet | PipePuzzle, PipePuzzle |
| order_shower | Install Shower | PipePuzzle, PipePuzzle |
| order_bathroom | Renovate Bathroom | PipePuzzle, PipePuzzle, PipePuzzle |

Electrician:
| order_outlet | Install Outlet | WiringGame |
| order_light | Install Light Fixture | WiringGame |
| order_panel | Upgrade Electrical Panel | WiringGame, WiringGame |
| order_smart_home | Smart Home Setup | WiringGame, WiringGame, WiringGame |

Painter:
| order_room | Paint a Room | PaintingGame |
| order_exterior | Paint Exterior | PaintingGame, PaintingGame |
| order_house | Paint Entire House | PaintingGame, PaintingGame, PaintingGame |

Roofer:
| order_repair_roof | Repair Roof Section | RoofTiling |
| order_new_roof | Install New Roof | RoofTiling, RoofTiling |
| order_roof_complete | Complete Roof Replacement | RoofTiling, TileLaying, RoofTiling |

Contractor:
| order_renovation | Home Renovation | Blueprint, Sawing |
| order_addition | Build Addition | Blueprint, Sawing, WiringGame |
| order_multi_unit | Multi-Unit Project | Blueprint, Blueprint, PipePuzzle, WiringGame |

Architect:
| order_blueprint | Design Blueprint | DesignPuzzle |
| order_floor_plan | Create Floor Plan | DesignPuzzle, DesignPuzzle |
| order_full_design | Complete Building Design | DesignPuzzle, Blueprint, DesignPuzzle |

GeneralContractor:
| order_house_build | Build House | Inspection, Sawing, PipePuzzle |
| order_commercial | Commercial Build | Inspection, Blueprint, WiringGame |
| order_luxury_villa | Luxury Villa Project | Inspection, Inspection, RoofTiling, DesignPuzzle |

MasterSmith:
| order_forge_blade | Forge Blade | ForgeGame |
| order_master_tools | Forge Master Tools | ForgeGame, ForgeGame |
| order_forge_artifact | Forge Artifact | ForgeGame, ForgeGame, ForgeGame |

InnovationLab:
| order_prototype | Build Prototype | InventGame |
| order_invention | Create Invention | InventGame, InventGame |
| order_breakthrough | Revolutionary Breakthrough | InventGame, InventGame, InventGame |

### 1.12 Kundenname + Stammkunden

Quelle: `Services/OrderGeneratorService.cs` (GenerateCustomerName, GenerateOrder)

- `nameSeed = (int)(DateTime.UtcNow.Ticks % int.MaxValue) ^ Random.Shared.Next()`. Name deterministisch aus `new Random(seed)` ueber `_firstNames` (30 Namen) + `_lastNames` (30 Namen). CustomerAvatarSeed = `nameSeed.ToString("X8")`.
- _firstNames (30): Hans, Klaus, Werner, Petra, Sabine, Ingrid, Thomas, Michael, Monika, Helga, Stefan, Andreas, Brigitte, Ursula, Frank, Jürgen, Renate, Dieter, Gabriele, Gerhard, Manfred, Erika, Wolfgang, Heike, Ralf, Ulrike, Heinz, Karin, Bernd, Martina.
- _lastNames (30): Müller, Schmidt, Schneider, Fischer, Weber, Meyer, Wagner, Becker, Schulz, Hoffmann, Schäfer, Koch, Bauer, Richter, Klein, Wolf, Schröder, Neumann, Schwarz, Zimmermann, Braun, Krüger, Hartmann, Lange, Schmitt, Werner, Krause, Meier, Lehmann, Schmid.

Stammkunden-Chance:
```
forcedRegular = state.RepShopRegularCustomerCharges > 0  (dann --, „Stammkunden-Garantie")
regularCustomerChance = 0.20 + CurrentTier.GetRegularCustomerBonus()
if (forcedRegular || NextDouble() < regularCustomerChance) → zufaelligen IsRegular-Kunden zuweisen
```
GetRegularCustomerBonus (Quelle: `Models/Enums/CustomerReputationTier.cs`): Beginner 0, CityKnown +0.10, RegionStar +0.20, IndustryLegend +0.35.
(BonusMultiplier 1.1–1.5x pro Stammkunde laut App-CLAUDE.md — die exakte Vergabe liegt in CustomerReputation/Completion-Logik, nicht in den hier gelesenen Dateien.)

Risk-Strategy-Sticky: `order.Strategy = workshop.DefaultRiskStrategy` (pro Workshop voreingestellt).

### 1.13 Verfuegbare Orders generieren + Refresh

Quelle: `Services/OrderGeneratorService.cs` (GenerateAvailableOrders, RefreshOrders)

`GenerateAvailableOrders(count=3)`:
```
totalCount = count
           + Office-Gebaeude ExtraOrderSlots
           + Research ExtraOrderSlots
           + Reputation ExtraOrderSlots
           + GuildMembership.ResearchOrderSlotBonus
```
Wenn keine Workshops unlocked: 1 Carpenter-Order (Level 1). Sonst totalCount Orders fuer zufaellig gewaehlte freigeschaltete Workshops.

`RefreshOrders()`: behaelt bestehende nicht-abgelaufene MaterialOrders, generiert 3 neue normale Orders (GenerateAvailableOrders(3)), plus 1 MaterialOrder wenn keine bestehende existiert. Swap unter Lock.

### 1.14 Live-Auftrags-Stream

Quelle: `Services/OrderGeneratorService.cs` (Konstanten, GenerateLiveOrder, ExpireOldLiveOrders)

| Konstante | Wert |
|-----------|------|
| MaxLiveOrdersCap | 5 |
| PremiumSpawnChance | 0.05 (5%) |
| LiveExpiryMinSeconds | 45 |
| LiveExpiryMaxSeconds | 180 |
| PremiumExpiryMinSeconds | 45 |
| PremiumExpiryMaxSeconds | 90 |

`GenerateLiveOrder()`:
- Cap-Check: max 5 gleichzeitige Live-Orders.
- Zufaelliger freigeschalteter Workshop → GenerateOrder → `IsLive = true`.
- Premium-Chance: `tierLiveBonus = CurrentTier.GetLiveOrderSpawnChance()` (RegionStar 0.05, IndustryLegend 0.10, sonst 0 — Quelle: `Models/Enums/CustomerReputationTier.cs`). `effectivePremiumChance = tierLiveBonus > 0 ? tierLiveBonus : 0.05`. `isPremium = NextDouble() < effectivePremiumChance`.
- Premium: `IsPremium=true`, `BaseReward *= 3`, `BaseXp = (int)(BaseXp * 2.5)`, ExpiresAt = `UtcNow + Next(45, 91)` Sek.
- Nicht-Premium: ExpiresAt = `UtcNow + Next(45, 181)` Sek.
- Feuert `OrderSpawned`-Event ausserhalb Lock.

`ExpireOldLiveOrders()`: entfernt Live-Orders mit `ExpiresAt <= now`. (GameLoop: alle 3 Ticks, Early-Exit wenn LiveOrderCount==0; Spawn alle 25 Ticks 50% Chance laut App-CLAUDE.md.)

### 1.15 MaterialOrder (Lieferauftrag) — eigene Generierung

Quelle: `Services/OrderGeneratorService.cs` (GenerateMaterialOrder, GetMaterialOrderDifficulty), Konstanten aus `Models/GameBalanceConstants.cs`

- Tages-Reset bei neuem UTC-Tag: `Statistics.MaterialOrdersCompletedToday = 0`.
- Tageslimit: `MaterialOrdersPerDay = 5` → return null wenn erreicht.
- Nur Workshops mit freigeschalteter Auto-Produktion qualifizieren.
- Haupt-Workshop zufaellig; Hauptprodukt = Tier-1-Produkt des Workshops.
- Hauptmenge: `mainCount = 5 + Min(playerLevel / 50, 10)` (5..15).
- Cross-Workshop ab Level 100 (`MaterialOrderCrossWorkshopLevel`) wenn >=2 qualifizierte Workshops: zweites Produkt, Menge `3 + Min(playerLevel / 100, 5)` (3..8).
- Reward: `perItemReward = Max(100 + playerLevel*100, netIncome*300)`; `baseReward = perItemReward * (1.0 + totalItems * 0.1) * Workshop.GetBaseIncomeMultiplier()`; Gilden-RewardBonus multiplikativ; `Math.Round`.
- XP: `baseXp = 25 * mainWorkshop.Level * Max(1, totalItems / 3)`.
- Deadline = `UtcNow + 4 Std` (MaterialOrderDeadlineHours).
- Tasks = [] (kein MiniGame).
- Difficulty (GetMaterialOrderDifficulty): WS-Level <=75 Easy, <=200 Medium, sonst Hard (kein Expert).

### 1.16 Material-Offer-Lifecycle (optionales Angebot)

Quelle: `Services/OrderGeneratorService.cs` (TryRollMaterialOffer, SampleMaterialOffer), Konstanten aus `Models/GameBalanceConstants.cs`, Lifecycle aus App-CLAUDE.md

Spawn-Gate (TryRollMaterialOffer):
- `state.PlayerLevel >= MaterialOfferUnlockLevel (= 30)`.
- `OrderType != MaterialOrder`.
- `NextDouble() <= MaterialOfferChance (= 0.35)` → sonst kein Offer (Bedingung im Code: return wenn `> 0.35`).

Pool je OrderType (t1/t2/t3 Stueck + BonusMultiplier):

| OrderType | T1 | T2 | T3 | CrossWorkshopT2 | Bonus (Konstante) |
|-----------|----|----|----|-----------------|-------------------|
| Quick | 1 | 0 | 0 | nein | 0.25 (MaterialOfferBonusQuick) |
| Standard | 2 | 0 | 0 | nein | 0.30 (MaterialOfferBonusStandard) |
| Large | 3 | 1 | 0 | nein | 0.40 (MaterialOfferBonusLarge) |
| Cooperation | 0 | 2 | 0 | ja | 0.50 (MaterialOfferBonusCooperation) |
| Weekly | 0 | 2 | 1 | nein | 0.60 (MaterialOfferBonusWeekly) |

SampleMaterialOffer: zieht Tier-N-Produkte des Auftrags-Workshops via `FindProductByTier(allRecipes, ws, tier)` (erstes Rezept mit `WorkshopType==ws && Tier==tier`, gibt OutputProductId). Wenn ein gefordertes Tier nicht existiert → ganzes Offer null (kein Spawn).
Cooperation Cross-T2: wenn `crossWorkshopT2 && t2>=2 && Workshops.Count>1`: 1x primaerer T2 + 1x T2 aus zufaelligem anderem Workshop (`result[primaryT2]=1`, `result[secondaryT2]=1`).
Gesetzt: `order.MaterialOffer = sample`, `order.MaterialOfferBonusMultiplier = bonus` nur wenn sample.Count > 0.
Discoverability-Hint (`ContextualHints.MaterialOffer`) beim allerersten gerollten Offer.

Lifecycle (App-CLAUDE.md, Logik in GameStateService/EconomyVM/WarehouseService — nicht in den hier gelesenen Dateien):
1. Generator setzt MaterialOffer + Multiplier.
2. UI: Button "Mit Material" nur bei HasMaterialOffer.
3. `TryAcceptMaterialOffer(order)` reserviert Material atomar in ReservedInventory, setzt `MaterialOfferAccepted=true`. Bei zu wenig Material → Alert, Auftrag nicht gestartet.
4. Bei Complete: wenn `MaterialOfferAccepted` → Bonus `× (1 + Multiplier)` auf Money/XP, Material consumed (ConsumeReserved). Bei HardFail (Risk-Miss) Bonus 0, Material trotzdem consumed.
5. Cancel: ReleaseReserved (kein Verbrauch).
6. Order-Expiry: ReleaseReserved.
7. SaveGame-Sanitize: Orphan-Reservierungen entfernt.

### 1.17 Combo / ScoreDoubled

Quelle: `Models/Order.cs`

- `ComboMultiplier` (decimal, Default 1m) — aus PaintingGame (Combo-Badge ab Combo>=3 laut App-CLAUDE.md). NICHT in FinalReward enthalten (wird extern angewendet — Logik nicht in gelesenen Dateien).
- `IsScoreDoubled` (bool, JsonIgnore) — Rewarded-Ad-Verdopplung. Ebenfalls extern angewendet.
- VIP/Premium-Multiplikator (3x Reward, 2.5x XP) wird direkt auf BaseReward/BaseXp gesetzt (siehe 1.14).

---

## 2. QuickJob (Schnellauftraege)

### 2.1 QuickJob-Modell

Quelle: `Models/QuickJob.cs`

| Feld | Typ | Default |
|------|-----|---------|
| Id | string | Guid |
| WorkshopType | WorkshopType | — |
| Difficulty | OrderDifficulty | Easy |
| MiniGameType | MiniGameType | — |
| Reward | decimal | 0 |
| XpReward | int | 0 |
| TitleKey | string | "" |
| CreatedAt | DateTime | UtcNow |
| IsCompleted | bool | false |
| IsScoreDoubled | bool (JsonIgnore) | false |

### 2.2 Rotation + Tageslimit (prestige-skaliert)

Quelle: `Services/QuickJobService.cs` (GetRotationInterval, GetMaxQuickJobsPerDay)

Rotations-Intervall nach `Prestige.TotalPrestigeCount`:
| Prestige | Intervall |
|----------|-----------|
| 0 | 15 Min |
| 1 | 12 Min |
| 2 | 10 Min |
| >=3 | 8 Min |

Tageslimit (baseLimit) nach Prestige:
| Prestige | baseLimit |
|----------|-----------|
| 0 | 20 |
| 1 | 25 |
| 2 | 30 |
| >=3 | 40 |
Plus Prestige-Shop `ExtraQuickJobLimit` (z.B. pp_quickjob_limit: +10) fuer nicht-repeatable gekaufte Items.

Anzahl Jobs gleichzeitig: 5 (GenerateJobs default count=5; RotateIfNeeded fuellt bis 5 auf). Erledigte werden bei Rotation entfernt. `LastQuickJobRotation = UtcNow` nach Generierung. `NeedsRotation()` = `UtcNow - LastQuickJobRotation > Intervall`.

Tages-Reset (ResetDailyCounterIfNewDay): bei neuem UTC-Tag `QuickJobsCompletedToday=0`; Zeitmanipulations-Schutz wenn LastReset in Zukunft. `NotifyJobCompleted` erhoeht `QuickJobsCompletedToday++`.

### 2.3 Reward-Formel

Quelle: `Services/QuickJobService.cs` (CalculateQuickJobRewards), Difficulty-Multiplikatoren aus OrderDifficulty

```
fiveMinIncome = Max(0, NetIncomePerSecond) * 300
baseReward    = Max(20 + level * 50, fiveMinIncome)
typeMult      = TitleRewardMultipliers[titleKey]  (Default 1.0)
diffMult      = difficulty.GetRewardMultiplier()
prestigeMult  = Min(1.0 + prestigeCount * 0.10, 3.0)
reward        = Round( baseReward * typeMult * diffMult * prestigeMult, 0 )

xpReward      = (int)( (5 + level * 3) * difficulty.GetXpMultiplier() )
   wenn typeMult > 1.0:  xpReward = (int)(xpReward * typeMult)
```
Belohnungen werden bei jedem GetAvailableJobs() neu berechnet (RecalculateRewards) fuer nicht-abgeschlossene Jobs.

### 2.4 Titel-Multiplikatoren

Quelle: `Services/QuickJobService.cs` (TitleKeys, TitleRewardMultipliers)

TitleKeys (8): QuickRepair, QuickFix, ExpressService, SmallOrder, QuickMeasure, QuickInstall, QuickPaint, QuickCheck.

| TitleKey | RewardMultiplier |
|----------|------------------|
| QuickRepair | 0.90 |
| QuickFix | 0.85 |
| ExpressService | 1.40 |
| SmallOrder | 0.80 |
| QuickMeasure | 0.75 |
| QuickInstall | 1.10 |
| QuickPaint | 0.95 |
| QuickCheck | 1.30 |

### 2.5 QuickJob-Difficulty

Quelle: `Services/QuickJobService.cs` (GetQuickJobDifficulty)

`roll = Random.Shared.Next(100)`. Kein Expert (QuickJobs bleiben locker):
| WS-Level | Verteilung |
|----------|-----------|
| <=50 | immer Easy |
| <=200 | <50 Easy, sonst Medium |
| <=500 | <20 Easy, <75 Medium, sonst Hard |
| >500 | <5 Easy, <50 Medium, sonst Hard |

### 2.6 MiniGame-Zuordnung pro Workshop

Quelle: `Services/QuickJobService.cs` (WorkshopMiniGameMap, GetMiniGameForWorkshop)

| Workshop | moegliche MiniGames |
|----------|---------------------|
| Carpenter | Sawing |
| Plumber | PipePuzzle |
| Electrician | WiringGame |
| Painter | PaintingGame |
| Roofer | RoofTiling |
| Contractor | Blueprint, Sawing |
| Architect | DesignPuzzle, Blueprint |
| GeneralContractor | Inspection, Sawing, PipePuzzle, RoofTiling, DesignPuzzle |
| MasterSmith | ForgeGame |
| InnovationLab | InventGame |
Auswahl per `Random.Shared.Next(games.Length)`. Fallback: Sawing.

---

## 3. Crafting

### 3.1 Crafting-Rezepte — ALLE 30 (T1–T3) + 3 (T4)

Quelle: `Models/CraftingRecipe.cs` (AllRecipes)

OutputCount = 1 fuer alle Rezepte (Default, nirgends ueberschrieben). Tier-Freischaltung: T1=Level 50, T2=Level 150, T3=Level 300, T4=Level 500.
Cross-Workshop-Inputs (markiert) werden erst ab Spielerlevel 100 gefordert (siehe 3.3).

| Id | Workshop | Tier | ReqWSLevel | Inputs | Output | Dauer (s) |
|----|----------|------|------------|--------|--------|-----------|
| r_planks | Carpenter | 1 | 50 | — | planks | 30 |
| r_furniture | Carpenter | 2 | 150 | planks×3, paint_mix×1 (Painter) | furniture | 120 |
| r_luxury_furniture | Carpenter | 3 | 300 | furniture×2, fittings×1 (MasterSmith) | luxury_furniture | 300 |
| r_pipes | Plumber | 1 | 50 | — | pipes | 30 |
| r_plumbing | Plumber | 2 | 150 | pipes×3, fittings×1 (MasterSmith) | plumbing_system | 120 |
| r_bathroom | Plumber | 3 | 300 | plumbing_system×2, cables×1 (Electrician) | bathroom_installation | 300 |
| r_cables | Electrician | 1 | 50 | — | cables | 30 |
| r_circuit | Electrician | 2 | 150 | cables×3, prototype×1 (InnovationLab) | circuit | 120 |
| r_smarthome | Electrician | 3 | 300 | circuit×2, concrete×1 (Contractor) | smart_home | 300 |
| r_paint | Painter | 1 | 50 | — | paint_mix | 30 |
| r_walldesign | Painter | 2 | 150 | paint_mix×3, blueprint×1 (Architect) | wall_design | 120 |
| r_artwork | Painter | 3 | 300 | wall_design×2, planks×1 (Carpenter) | artwork | 300 |
| r_tiles | Roofer | 1 | 50 | — | roof_tiles | 30 |
| r_roofing | Roofer | 2 | 150 | roof_tiles×3, concrete×1 (Contractor) | roofing_system | 120 |
| r_roof_structure | Roofer | 3 | 300 | roofing_system×2, blueprint×1 (Architect) | roof_structure | 300 |
| r_concrete | Contractor | 1 | 50 | — | concrete | 30 |
| r_foundation | Contractor | 2 | 150 | concrete×3, pipes×1 (Plumber) | concrete_foundation | 150 |
| r_skyscraper_frame | Contractor | 3 | 300 | concrete_foundation×2, contract×1 (GeneralContractor) | skyscraper_frame | 360 |
| r_blueprint | Architect | 1 | 50 | — | blueprint | 30 |
| r_framework | Architect | 2 | 150 | blueprint×3, planks×1 (Carpenter), concrete×1 (Contractor) | framework | 150 |
| r_master_blueprint | Architect | 3 | 300 | framework×2, contract×1 (GeneralContractor) | master_blueprint | 360 |
| r_contract | GeneralContractor | 1 | 50 | — | contract | 30 |
| r_contract_complex | GeneralContractor | 2 | 150 | contract×3, blueprint×1 (Architect) | contract_complex | 150 |
| r_general_contract | GeneralContractor | 3 | 300 | contract_complex×2, blueprint×1 (Architect) | general_contract | 420 |
| r_fittings | MasterSmith | 1 | 50 | — | fittings | 30 |
| r_master_fittings | MasterSmith | 2 | 150 | fittings×3, cables×1 (Electrician) | master_fittings | 150 |
| r_masterpiece_fittings | MasterSmith | 3 | 300 | master_fittings×2, prototype×1 (InnovationLab) | masterpiece_fittings | 360 |
| r_prototype | InnovationLab | 1 | 50 | — | prototype | 30 |
| r_innovation | InnovationLab | 2 | 150 | prototype×3, cables×1 (Electrician) | innovation | 150 |
| r_patent | InnovationLab | 3 | 300 | innovation×2, master_fittings×1 (MasterSmith T2) | patent | 420 |

Tier 4 (3 Rezepte, alle GeneralContractor, ReqWSLevel 500; logi_09-Research laut App-CLAUDE.md):
| Id | Output | Inputs | Dauer (s) |
|----|--------|--------|-----------|
| r_villa | villa | luxury_furniture×5, smart_home×3, roof_structure×2, artwork×1 | 1800 (30 min) |
| r_skyscraper | skyscraper | skyscraper_frame×5, bathroom_installation×3, smart_home×3, artwork×2 | 2400 (40 min) |
| r_imperium_hq | imperium_hq | luxury_furniture×2, bathroom_installation×2, smart_home×2, artwork×2, roof_structure×2, skyscraper_frame×2, master_blueprint×2, general_contract×1, masterpiece_fittings×2, patent×2 | 3600 (60 min) |

### 3.2 CraftingProduct-Katalog (BaseValue, Tier, Heirloom)

Quelle: `Models/CraftingRecipe.cs` (CraftingProduct.AllProducts)

Tier 1 (10):
| productId | BaseValue |
|-----------|-----------|
| planks | 500 |
| pipes | 500 |
| cables | 500 |
| paint_mix | 400 |
| roof_tiles | 600 |
| concrete | 800 |
| blueprint | 1000 |
| contract | 1500 |
| fittings | 1200 |
| prototype | 2000 |

Tier 2 (10):
| furniture | 2500 |
| plumbing_system | 2500 |
| circuit | 2500 |
| wall_design | 2000 |
| roofing_system | 3000 |
| concrete_foundation | 4000 |
| framework | 5000 |
| contract_complex | 6000 |
| master_fittings | 5000 |
| innovation | 7000 |

Tier 3 (10):
| luxury_furniture | 50000 |
| bathroom_installation | 50000 |
| smart_home | 50000 |
| artwork | 40000 |
| roof_structure | 60000 |
| skyscraper_frame | 60000 |
| master_blueprint | 70000 |
| general_contract | 80000 |
| masterpiece_fittings | 60000 |
| patent | 75000 |

Tier 4 (3, alle IsHeirloomEligible=true):
| villa | 2.500.000 |
| skyscraper | 4.000.000 |
| imperium_hq | 5.000.000 |

### 3.3 Cross-Workshop-Input-Gate + OwnInputs

Quelle: `Models/CraftingRecipe.cs` (GetEffectiveInputs, GetOwnInputs, GetCrossInputs), Konstante MaterialOrderCrossWorkshopLevel=100

```
GetEffectiveInputs(recipe, playerLevel):
  if playerLevel >= 100: return recipe.InputProducts (alle)
  else:                  return GetOwnInputs(recipe) (nur Inputs deren OutputProductId zum gleichen WorkshopType gehoert)
```
OwnWorkshopInputs / CrossWorkshopInputs sind berechnete Properties (Subsets von InputProducts, klassifiziert per `GetByOutputProduct(productId).WorkshopType`).

### 3.4 StartCrafting — Ablauf + Tier-1-Goldkosten + Stack-Schutz

Quelle: `Services/CraftingService.cs` (StartCrafting)

Reihenfolge (unter `_craftingLock`):
1. Rezept laden, sonst false.
2. `effectiveInputs = GetEffectiveInputs(recipe, playerLevel)`.
3. Tier-1-Goldkosten: wenn `recipe.Tier == 1 && effectiveInputs.Count == 0`: `materialCost = product.BaseValue * 0.20`. `CanAfford` pruefen, `TrySpendMoney(materialCost)`. (= 20% des BaseValue; Beispiel planks 500 → 100 Gold.)
4. Output-Stack-Limit-Check: wenn Output-Bestand 0 → freier Slot noetig (`usedSlots < WarehouseSlotCount`, sonst false). Sonst wenn `currentOutput + OutputCount > WarehouseStackLimit` → false.
5. Input-Verfuegbarkeit: fuer jeden Input `available = CraftingInventory[id] - ReservedInventory[id]`, wenn `< required` → false.
6. Inputs abziehen (entfernt Key bei <=0).
7. Crafting-Speed-Bonus: siehe 3.5.
8. Job mit `StartedAt=UtcNow`, `DurationSeconds=effectiveDuration` in ActiveCraftingJobs.

### 3.5 Crafting-Speed-Bonus (Dauer-Reduktion)

Quelle: `Services/CraftingService.cs` (StartCrafting, GetPrestigeCraftingSpeedBonus, GetMaterialAffinityBonus)

```
craftingSpeedBonus = PrestigeShopCraftingSpeedBonus
                   + Research.CraftingSpeedBonus
                   + MaterialAffinityBonus
                   + GuildMembership.MegaProjectCraftingSpeedBonus
                   + GuildMembership.HallCraftingSpeedBonus
if craftingSpeedBonus > 0:
   effectiveDuration = Max(1, (int)(DurationSeconds * (1 - Min(craftingSpeedBonus, 0.50))))
```
Cap der gesamten Beschleunigung: 50%.
MaterialAffinityBonus: `0.20 * matchingWorkingWorkers / totalWorkingWorkers` (linear; alle matchend = +20%; abhaengig von `MaterialAffinityExtensions.GetMaterialAffinity(OutputProductId)`; 0 wenn None oder kein arbeitender Worker).
PrestigeShopCraftingSpeedBonus: Summe der `CraftingSpeedBonus`-Effekte aller nicht-repeatable gekauften Prestige-Shop-Items (gecacht via Dirty-Flag).

### 3.6 CollectProduct (Einsammeln)

Quelle: `Services/CraftingService.cs` (CollectProduct)

- Job per JobId finden (muss IsComplete sein), sonst false.
- Stack-Limit-Check wie StartCrafting (currentStock==0 → freier Slot noetig; sonst `currentStock + OutputCount > WarehouseStackLimit` → false; Job bleibt completed bei vollem Lager — kein Material-Burn).
- Output zu CraftingInventory addieren, Job entfernen.
- Telemetrie `material_crafted` (product_id, tier, workshop, count).

### 3.7 Verkauf aus dem Lager (SellProducts / GetSellPrice)

Quelle: `Services/CraftingService.cs` (SellProducts, GetSellPrice), Konstante CraftingSellPriceLogDivisor=15.0

`SellProduct(productId)` = `SellProducts(productId, 1) > 0`.
`SellProducts(productId, count)`:
- `sellable = Max(0, CraftingInventory[id] - ReservedInventory[id])` (reservierte ausgeschlossen).
- `sellCount = Min(count, sellable)`.
- `pricePerUnit = GetSellPrice(productId)`, `totalRevenue = pricePerUnit * sellCount`.
- Inventar reduzieren, `AddMoney(totalRevenue)`, Telemetrie `material_sold` (source="warehouse").

`GetSellPrice(productId)` (Lager-Verkaufspreis, mit Level- + Income-Multiplikatoren):
```
levelMult = 1.0 + Log2(1.0 + workshopLevel / 15.0)
prestigeIncomeBonus = IncomeCalculatorService.GetPrestigeIncomeBonus(state)
rebirthBonus        = Workshop.RebirthIncomeBonus (fuer den produzierenden Workshop)
boostMult           = IncomeCalculator.CalculateCraftingSellMultiplier(state, prestigeIncomeBonus, rebirthBonus)
GetSellPrice        = Round(product.BaseValue * levelMult * boostMult)
```
(Dies ist NICHT der Markt-Verkaufspreis — siehe Abschnitt 6 fuer den Material-Markt.)

### 3.8 CraftingJob-Modell

Quelle: `Models/CraftingRecipe.cs` (CraftingJob)

| Feld | Typ | Default |
|------|-----|---------|
| JobId | string | `Guid.ToString("N")` |
| RecipeId | string | "" |
| StartedAt | DateTime | — |
| DurationSeconds | int | — |
IsComplete = `(UtcNow - StartedAt).TotalSeconds >= DurationSeconds`. Progress = `Clamp(elapsed/Duration, 0, 1)`. TimeRemaining berechnet.

---

## 4. Tools (8 Werkzeuge, Goldschrauben-Upgrade)

Quelle: `Models/Tool.cs`

8 ToolTypes: Saw, PipeWrench, Screwdriver, Paintbrush, Hammer, SpiritLevel, Magnifier, Compass.
MaxLevel = 5. IsUnlocked = `Level > 0`. CanUpgrade = `Level < 5`. CreateDefaults() erstellt alle 8 mit Level 0.

Upgrade-Kosten (Goldschrauben, UpgradeCostScrews je aktuellem Level → naechstes):
| von Level | UpgradeCostScrews (GS) | UpgradeCost (Legacy €, ungenutzt) |
|-----------|------------------------|-----------------------------------|
| 0 → 1 | 5 | 50 |
| 1 → 2 | 15 | 150 |
| 2 → 3 | 35 | 400 |
| 3 → 4 | 70 | 1000 |
| 4 → 5 | 120 | 2500 |
| 5 (max) | 0 | 0 |

Effekt-Werte pro Level (ZoneBonus = Faktor fuer Saege/Zonen-Spiele, TimeBonus = Extra-Sekunden):
| Level | ZoneBonus | TimeBonus (s) |
|-------|-----------|---------------|
| 0 | 0.0 | 0 |
| 1 | 0.05 | 5 |
| 2 | 0.10 | 8 |
| 3 | 0.15 | 10 |
| 4 | 0.20 | 12 |
| 5 | 0.25 | 15 |

Zuordnung ToolType → NameKey / RelatedMiniGame:
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

(Welcher Effekt — ZoneBonus vs. TimeBonus — pro Tool greift, wird in den MiniGame-VMs entschieden; nicht in Tool.cs. Doku-Kommentar: Saege=ZoneBonus, Rohrzange/Schraubendreher/Pinsel=TimeBonus.)

---

## 5. MasterTools (12 Artefakte)

Quelle: `Models/MasterTool.cs` (statische Klasse, _allDefinitions)

GameState speichert nur die IDs (CollectedMasterTools). Gesamt-Income-Bonus = Summe der IncomeBonus aller gesammelten (GetTotalIncomeBonus). Summe aller 12 = +74% (0.02×2 + 0.03×2 + 0.05×3 + 0.07×2 + 0.10×2 + 0.15 = 0.74).

| Id | Rarity | IncomeBonus | Icon | NameKey | Freischalt-Bedingung (CheckEligibility) |
|----|--------|-------------|------|---------|------------------------------------------|
| mt_golden_hammer | Common | 0.02 (+2%) | Hammer | MasterToolGoldenHammer | Workshop Level >= 75 |
| mt_diamond_saw | Common | 0.02 (+2%) | Saw | MasterToolDiamondSaw | Workshop Level >= 150 |
| mt_titanium_pliers | Common | 0.03 (+3%) | Wrench | MasterToolTitaniumPliers | TotalOrdersCompleted >= 150 |
| mt_brass_level | Common | 0.03 (+3%) | RulerSquare | MasterToolBrassLevel | TotalMiniGamesPlayed >= 300 |
| mt_silver_wrench | Uncommon | 0.05 (+5%) | Screwdriver | MasterToolSilverWrench | Workshop Level >= 300 |
| mt_jade_brush | Uncommon | 0.05 (+5%) | Brush | MasterToolJadeBrush | PerfectRatings >= 75 |
| mt_crystal_chisel | Uncommon | 0.05 (+5%) | Pickaxe | MasterToolCrystalChisel | Prestige.BronzeCount >= 1 |
| mt_obsidian_drill | Rare | 0.07 (+7%) | Drill | MasterToolObsidianDrill | Workshop Level >= 750 |
| mt_ruby_blade | Rare | 0.07 (+7%) | DiamondStone | MasterToolRubyBlade | Prestige.SilverCount >= 1 |
| mt_emerald_toolbox | Epic | 0.10 (+10%) | Toolbox | MasterToolEmeraldToolbox | Workshop Level >= 500 (BAL-38: von 1500 gesenkt) |
| mt_dragon_anvil | Epic | 0.10 (+10%) | Anvil | MasterToolDragonAnvil | Prestige.GoldCount >= 1 |
| mt_master_crown | Legendary | 0.15 (+15%) | Crown | MasterToolMasterCrown | alle 11 anderen Tools gesammelt (CollectedMasterTools.Count >= 11) |

DescriptionKey jeweils NameKey + "Desc". MasterToolRarity-Enum: Common/Uncommon/Rare/Epic/Legendary.

Hinweis: Die App-CLAUDE.md-Tabelle nennt fuer mt_emerald_toolbox "Workshop Lv.1500"; der tatsaechliche Code-Wert ist 500 (BAL-38). Verbindlich = 500.

---

## 6. Equipment (Arbeiter-Ausruestung)

Quelle: `Models/Equipment.cs` (EquipmentType, EquipmentRarity, Equipment), `Services/EquipmentService.cs`

4 EquipmentTypes: Helmet, Gloves, Boots, Belt.
4 EquipmentRarities: Common, Uncommon, Rare, Epic.
NameKey-Schema: `$"Equipment_{Type}_{Rarity}"`.

### 6.1 Stat-Roll-Bereiche pro Rarity

Quelle: `Models/Equipment.cs` (GenerateRandom). `rng.Next(a, b)` ist exklusiv b, geteilt durch 100 → decimal-Faktor.

| Rarity | EfficiencyBonus | FatigueReduction | MoodBonus |
|--------|-----------------|------------------|-----------|
| Common | 5–7% (`Next(5,8)/100`) | 3–5% (`Next(3,6)/100`) | 3–5% (`Next(3,6)/100`) |
| Uncommon | 8–10% (`Next(8,11)/100`) | 6–8% (`Next(6,9)/100`) | 5–7% (`Next(5,8)/100`) |
| Rare | 11–13% (`Next(11,14)/100`) | 9–11% (`Next(9,12)/100`) | 7–9% (`Next(7,10)/100`) |
| Epic | 13–15% (`Next(13,16)/100`) | 11–14% (`Next(11,15)/100`) | 8–10% (`Next(8,11)/100`) |

Gesamt-Spannweiten ueber alle Rarities: EfficiencyBonus 5–15%, FatigueReduction 3–14%, MoodBonus 3–10%.
(Plan-Vorgabe nennt EfficiencyBonus 5–16 / FatigueReduction 3–15 / MoodBonus 3–11 — der tatsaechliche Code-Maximalwert ist je 1 niedriger, da `Next(a,b)` b ausschliesst. Verbindlich = Code-Werte oben.)

### 6.2 Rarity-Gewichtung beim Drop (GenerateRandom)

Quelle: `Models/Equipment.cs` (GenerateRandom)

`type = (EquipmentType)rng.Next(4)` (gleichverteilt 25% je Typ). `roll = rng.Next(100)`:
```
if difficultyLevel >= 3 && roll < 5  → Epic
else if difficultyLevel >= 2 && roll < 20 → Rare
else if roll < 45 → Uncommon
else → Common
```
Effektive Wahrscheinlichkeiten haengen vom difficultyLevel ab:
- difficulty 0–1: Epic 0%, Rare 0%, Uncommon 45% (roll<45), Common 55%.
- difficulty 2: Epic 0%, Rare 20% (roll<20), Uncommon 25% (20–44), Common 55%.
- difficulty >=3: Epic 5% (roll<5), Rare 15% (5–19), Uncommon 25% (20–44), Common 55%.

### 6.3 Shop-Preis (Goldschrauben)

Quelle: `Models/Equipment.cs` (ShopPrice — ECON-1 Rebalancing)

| Rarity | ShopPrice (GS) |
|--------|----------------|
| Common | 3 |
| Uncommon | 8 |
| Rare | 18 |
| Epic | 40 |

(Alte Preise 5/15/30/60 ersetzt.)

### 6.4 Rarity-Farben / -Icons

Quelle: `Models/Equipment.cs`

| Rarity | RarityColor | RarityIcon |
|--------|-------------|------------|
| Common | #9E9E9E | Circle |
| Uncommon | #4CAF50 | DiamondOutline |
| Rare | #2196F3 | Diamond |
| Epic | #9C27B0 | Star |

### 6.5 Drop nach MiniGame + Shop-Rotation

Quelle: `Services/EquipmentService.cs`

| Konstante | Wert |
|-----------|------|
| BaseDropChance | 0.05 (5%) |
| MinShopItems | 3 |
| MaxShopItems | 4 |

`TryGenerateDrop(difficulty, isPerfect=false)`:
```
dropChance = 0.05 + difficulty * 0.05 + (isPerfect ? 0.05 : 0.0)
if NextDouble() >= dropChance → kein Drop (null)
```
Effektive Drop-Chancen: Easy(diff 1)=10%, Medium(2)=15%, Hard(3)=20%, Expert(4)=25%; +5% bei Perfect.
(App-CLAUDE.md-Kommentar: Easy=5/Medium=10/Hard=15/Expert=20 + Perfect +5 — bezieht sich auf den schwierigkeitsabhaengigen Anteil OHNE die Basis-5%; verbindlich = Formel oben.)

`GetShopItems()`: `Next(3,5)` Items (3–4), jedes mit `shopDifficulty = Next(1,4)` (1–3).
`BuyEquipment`: kostet `equipment.ShopPrice` GS via TrySpendGoldenScrews.
`EquipItem`/`UnequipItem`: verschiebt zwischen Worker.EquippedItem und EquipmentInventory; InvalidateIncomeCache.

---

## 7. Material-Markt

Quelle: `Services/MarketService.cs`

| Konstante | Wert |
|-----------|------|
| SpreadFactor | 0.05 (5% Maklergebuehr) |
| DailyAmplitude | 0.50 (±50% Sinus-Welle) |
| MarketUnlockResearchId | "logi_05" |

### 7.1 Verfuegbarkeit

`IsMarketAvailable`: true wenn `state.IsPremium`; sonst true wenn kein ResearchService; sonst nur wenn Research "logi_05" `IsResearched`.

### 7.2 Preis-Engine (deterministisch pro Spieler/Tag/Material)

`ComputeDailyFactor(productId, utc)`:
```
playerKey = state.PlayerGuid ?? "anonymous"
dayIndex  = (int)(utc - 2026-01-01 UTC).TotalDays
seed      = StableHash.Compute(playerKey) ^ dayIndex ^ StableHash.Compute(productId)
rng       = new Random(seed)
phaseOffset = rng.NextDouble() * 2π
hourFraction = utc.TimeOfDay.TotalHours / 24.0
phase     = hourFraction * 2π + phaseOffset
factor    = 1.0 + sin(phase) * 0.50    (Bereich 0.5 .. 1.5)
```
(StableHash statt string.GetHashCode() — sonst pro-Prozess-randomisiert. App-CLAUDE.md-Hinweis "PlayerGuid.GetHashCode()" ist veraltet; verbindlich = StableHash.)

### 7.3 Kauf-/Verkaufspreis

```
GetBuyPrice(productId) = Max(1, Round( BaseValue * ComputeDailyFactor ) )  [× Event-Modulator]
GetSellPrice(productId) = Round( GetBuyPrice(productId) * (1 - 0.05) )    = Round( Kaufpreis × 0.95 )
```
Event-Modulator (nur wenn `ActiveEvent.Effect.AffectedWorkshop == recipe.WorkshopType`):
- `GameEventType.MaterialShortage` → `price *= 3`
- `GameEventType.HighDemand` → `price *= 2`

`GetPriceTrend(productId)`: `Clamp((factorNextHour - factorNow) * 2.0, -1, 1)`.
`Get24hPriceSeries(productId)`: 24 Werte `Max(1, Round(BaseValue * factor(startOfDay + h)))`.

### 7.4 TryBuy / TrySell

Quelle: `Services/MarketService.cs`

`TryBuy(productId, count)`:
- `count<=0` oder `!IsMarketAvailable` → false.
- `totalCost = GetBuyPrice × count`. `WarehouseService.CanAddToInventory` Stack/Slot-Check. `TrySpendMoney`.
- `AddToInventory`; bei Teil-Einlagerung Differenz zurueckerstatten (`AddMoney(pricePer * shortfall)`).
- Telemetrie `material_market_trade` (side="buy").

`TrySell(productId, count)`:
- `count<=0` oder `!IsMarketAvailable` → 0.
- `sellable = Max(0, CraftingInventory[id] - ReservedInventory[id])`; `sellCount = Min(count, sellable)`.
- `revenue = GetSellPrice × sellCount`; Inventar reduzieren; `AddMoney(revenue)`.
- Telemetrie `material_market_trade` (side="sell"). Gibt revenue zurueck.

---

## 8. Lager (Warehouse V7)

Quelle: `Services/WarehouseService.cs`

| Konstante / Wert | Wert | Quelle |
|------------------|------|--------|
| Start-Slots | 20 | GameState V7 (WarehouseSlotCount Default 20) |
| Start-Stack-Limit | 50 | GameState V7 (WarehouseStackLimit Default 50) |
| SlotsPerUpgrade | 5 | WarehouseService.SlotsPerUpgrade |
| MaxSlots | 200 | WarehouseService.MaxSlots |
| SlotUpgradeBaseCost | 50.000 € | WarehouseService (private) |
| SlotUpgradeExponent | 1.5 | WarehouseService (private) |
| Stack-Limit Hard-Cap | 9999 | CurrentStackLimit |

### 8.1 Slot-/Stack-Berechnung

```
EffectiveSlotCount = Min(200, WarehouseSlotCount + BonusSlotsFromResearch + BonusSlotsFromGuildMegaProject)
UsedSlotCount      = Anzahl CraftingInventory-Eintraege mit Value > 0
FreeSlotCount      = Max(0, EffectiveSlotCount - UsedSlotCount)
IsWarehouseFull    = FreeSlotCount == 0
CurrentStackLimit  = Min(9999, Max(WarehouseStackLimit, Round(WarehouseStackLimit * StackLimitMultiplierFromResearch)))
StackLimitMultiplierFromResearch = max(1.0, Research.StackLimitMultiplier)
BonusSlotsFromResearch = Research.BonusWarehouseSlots
BonusSlotsFromGuildMegaProject = GuildMembership.MegaProjectBonusWarehouseSlots  (Kathedrale +3, HQ +5, beide +8)
```

### 8.2 Slot-Upgrade-Kosten

```
upgradesDone = Max(0, (WarehouseSlotCount - 20) / 5)
GetNextSlotUpgradeCost = 50_000 * 1.5^upgradesDone   (0 wenn SlotCount >= 200)
```
Beispiele: 1. Upgrade (20→25) = 50.000 €; 2. (25→30) = 75.000; 3. = 112.500; 4. = 168.750.
`TryUpgradeSlots`: TrySpendMoney(cost), `WarehouseSlotCount = Min(200, +5)`.

### 8.3 Inventar-/Reservierungs-Operationen

Quelle: `Services/WarehouseService.cs`

`CanAddToInventory(productId, count)`: bei vorhandenem Bestand `current + count <= CurrentStackLimit`; bei neuem Slot `FreeSlotCount > 0 && count <= CurrentStackLimit`.
`AddToInventory(productId, count, sourceWorkshop?)`: fuegt bis Stack-Limit hinzu, Rest = overflow. Bei overflow:
- AutoSellRule.Enabled → Auto-Verkauf zum Marktpreis `GetSellPrice × overflow`, mit Gilden-Bonus `× (1 + MegaProjectAutoSellPriceBonus)`; AddMoney; Event `OverflowAutoSold`.
- sonst wenn sourceWorkshop gesetzt → Event `WorkshopPaused` + Telemetrie `warehouse_full_pause`.
- sonst stilles Verwerfen (z.B. Offline-Earnings).
Gibt actuallyAdded zurueck.
`TryReserve`: nur wenn `current - reserved >= count`. `ConsumeReserved`: reduziert ReservedInventory UND CraftingInventory atomar. `ReleaseReserved`: gibt Reservierung frei (kein Verbrauch). `GetAvailable` = `Max(0, current - reserved)`.
`GetTotalWarehouseValue` = Summe `GetSellPrice × count` ueber alle Bestaende.

### 8.4 AutoSellRule

Quelle: `Models/AutoSellRule.cs`

| Feld | Typ | Default | Bemerkung |
|------|-----|---------|-----------|
| Enabled | bool | false | V7 nutzt nur dieses |
| MinPrice | decimal | 0 | reserviert (0 = immer verkaufen) |
| KeepUntil | int | 0 | reserviert (0 = alles bis Stack-Limit) |
`GetAutoSellRule(productId)`: legt Default-Regel (Enabled=false) an wenn fehlt.

---

## 9. Lieferant-Material-Variante (SupplierDelivery)

Quelle: `Models/SupplierDelivery.cs`, DeliveryType aus `Models/Enums/DeliveryType.cs`

6 DeliveryTypes: Money, GoldenScrews, Experience, MoodBoost, SpeedBoost, Material.
Erscheint alle 2–5 Min (App-CLAUDE.md), Abholzeit 2 Min (`ExpiresAt = UtcNow + 2 Min`).

`GenerateRandom(state)`:
- Material-Eligibility: `PlayerLevel >= AutoProductionUnlockLevel (= 50)`.
- Wenn eligible UND `NextDouble() < 0.25` → DeliveryType.Material. Sonst gewichtete Geld-Auswahl:

| roll (Next 100) | Type |
|-----------------|------|
| <35 | Money |
| <55 | GoldenScrews |
| <75 | Experience |
| <90 | MoodBoost |
| sonst | SpeedBoost |

(Ohne Material: Money 35%, GoldenScrews 20%, Experience 20%, MoodBoost 15%, SpeedBoost 10% — konsistent mit App-CLAUDE.md.)

Amount pro Type:
| Type | Amount |
|------|--------|
| Money | `Max(50, Round(NetIncomePerSecond * Next(60,180)))` (1–3 Min Einkommen, min 50) |
| GoldenScrews | `Next(2,6)` (2–5 GS) |
| Experience | `20 + PlayerLevel*2 + Next(0,40)` |
| MoodBoost | 10 (Mood +10 fuer alle Worker) |
| SpeedBoost | 30 (30 Min Boost) |
| Material | `Next(1,11)` (1–10 Stueck) |

Material: zufaelliges Tier-1-Produkt eines freigeschalteten Workshops (Fallback "planks"). MaterialProductId gesetzt; Menge in Amount.
Research `logi_08` (SupplierMaterialBonus) erhoeht Material-Menge proportional (App-CLAUDE.md).

Icons: Money=Cash, GoldenScrews=Screwdriver, Experience=Star, MoodBoost=EmoticonHappy, SpeedBoost=LightningBolt, Material=PackageVariant.
DescriptionKeys: DeliveryMoney/DeliveryGoldenScrews/DeliveryExperience/DeliveryMoodBoost/DeliverySpeedBoost/DeliveryMaterial.

---

## 10. MiniGameType (Referenz)

Quelle: `Models/Enums/MiniGameType.cs`

13 Werte: Sawing(0), Planing(1), PipePuzzle(2), WiringGame(3), PaintingGame(4), TileLaying(5), Measuring(6), RoofTiling(7), Blueprint(8), DesignPuzzle(9), Inspection(10), ForgeGame(11), InventGame(12).

Routen: Sawing/Planing/TileLaying/Measuring → "minigame/sawing"; PipePuzzle → "minigame/pipes"; WiringGame → "minigame/wiring"; PaintingGame → "minigame/painting"; RoofTiling → "minigame/rooftiling"; Blueprint → "minigame/blueprint"; DesignPuzzle → "minigame/designpuzzle"; Inspection → "minigame/inspection"; ForgeGame → "minigame/forge"; InventGame → "minigame/invent".
(10 eigenstaendige MiniGame-VMs; Planing/TileLaying/Measuring teilen die Sawing-Timing-Mechanik.)

---

## 11. Relevante GameBalanceConstants (extrahiert)

Quelle: `Models/GameBalanceConstants.cs`

| Konstante | Wert |
|-----------|------|
| AutoProductionUnlockLevel | 50 |
| CraftingSellPriceLogDivisor | 15.0 |
| MaterialOrderRewardMultiplier | 1.8 |
| MaterialOrderXpMultiplier | 1.5 |
| MaterialOrdersPerDay | 5 |
| MaterialOrderDeadlineHours | 4 |
| MaterialOrderCrossWorkshopLevel | 100 |
| MaterialOfferUnlockLevel | 30 |
| MaterialOfferChance | 0.35 |
| MaterialOfferBonusQuick | 0.25 |
| MaterialOfferBonusStandard | 0.30 |
| MaterialOfferBonusLarge | 0.40 |
| MaterialOfferBonusCooperation | 0.50 |
| MaterialOfferBonusWeekly | 0.60 |
| MaxAuraBonus | 0.50 |

---

## 12. Offene/Externe Punkte (nicht in den gelesenen Dateien)

- Anwendung von ComboMultiplier, IsScoreDoubled und ReputationBonus auf den ausgezahlten Reward (liegt in Order-Completion-Logik des MainViewModel/EconomyFeatureViewModel/GameStateService).
- `TryAcceptMaterialOffer`, `CompleteActiveOrder`, `CancelActiveOrder` Reservierungs-Mechanik (IGameStateService — nicht gelesen).
- Stammkunden-BonusMultiplier 1.1–1.5x exakte Vergabe (CustomerReputation/Completion).
- Welcher Tool-Effekt (ZoneBonus/TimeBonus) konkret pro MiniGame greift (MiniGame-VMs/Renderer).
- Auto-Produktions-Mengen/Intervalle (AutoProductionService — separater Bereich).

---

# 04 — Prestige, Prestige-Shop, Ascension, Rebirth, EternalMastery, Reputation, Reputation-Shop, Heirlooms, Speedrun

Verbindliche Werte- und Mechanik-Spezifikation, extrahiert 1:1 aus dem Avalonia-Code von
HandwerkerImperium. Jeder Wert stammt direkt aus dem Code; Formeln sind exakt wiedergegeben.
Quell-Dateien sind pro Block angegeben (relativ zu
`HandwerkerImperium.Shared/`).

---

## 1. Prestige-System (7 Tiers)

### 1.1 Tier-Enum & Index

Quelle: `Models/Enums/PrestigeTier.cs`

| Tier | Enum-Wert (int) |
|------|-----------------|
| None | 0 |
| Bronze | 1 |
| Silver | 2 |
| Gold | 3 |
| Platin | 4 |
| Diamant | 5 |
| Meister | 6 |
| Legende | 7 |

`tierIndex = (int)tier` wird für den Bonus-PP-Faktor `sqrt(TierIndex+1)` verwendet (siehe 1.7).

### 1.2 Freischalt-Voraussetzungen (Level + Vortier-Count)

Quelle: `Models/Enums/PrestigeTier.cs` (`GetRequiredLevel`, `GetRequiredPreviousTierCount`),
`Models/PrestigeData.cs` (`CanPrestige`)

| Tier | Min. Spielerlevel | Benötigte Vortier-Abschlüsse |
|------|-------------------|------------------------------|
| Bronze | 30 | 0 (immer, wenn Level ≥ 30) |
| Silver | 100 | 1× Bronze (BronzeCount ≥ 1) |
| Gold | 250 | 1× Silver (SilverCount ≥ 1) |
| Platin | 500 | 2× Gold (GoldCount ≥ 2) |
| Diamant | 750 | 2× Platin (PlatinCount ≥ 2) |
| Meister | 1000 | 2× Diamant (DiamantCount ≥ 2) |
| Legende | 1200 | 3× Meister (MeisterCount ≥ 3) |
| None | int.MaxValue | — (nicht prestigebar) |

`CanPrestige(tier, playerLevel)`: zuerst `playerLevel >= GetRequiredLevel(tier)`, dann der
Vortier-Count-Check. None ist nie prestigebar.

### 1.3 Basis-PP-Formel (CalculatePrestigePoints)

Quelle: `Models/PrestigeData.cs` (`CalculatePrestigePoints`), `Services/PrestigeService.cs`
(`GetPrestigePoints`)

```
basePoints = floor( sqrt( CurrentRunMoney / 100_000 ) )
```

- Eingabe ist **`GameState.CurrentRunMoney`** (im aktuellen Durchlauf verdientes Geld, NICHT kumulativ,
  NICHT `TotalMoneyEarned`).
- Wenn `CurrentRunMoney <= 0` → 0 PP.
- Berechnung in `double`: `(int)Math.Floor(Math.Sqrt((double)(currentRunMoney / 100_000m)))`.

### 1.4 Tier-PP-Multiplikator (GetPointMultiplier) — Cap 64×

Quelle: `Models/Enums/PrestigeTier.cs` (`GetPointMultiplier`)

| Tier | PP-Multiplikator |
|------|------------------|
| Bronze | 1.0× |
| Silver | 2.0× |
| Gold | 4.0× |
| Platin | 8.0× |
| Diamant | 16.0× |
| Meister | 32.0× |
| Legende | 64.0× (Cap) |
| None | 0× |

`tierPoints = round( basePoints × GetPointMultiplier(tier) )`.

### 1.5 Bronze-Mindest-PP

Quelle: `Services/PrestigeService.cs` (`CalculateTotalPrestigePoints`)

Beim Tier Bronze: wenn `tierPoints < 15` → `tierPoints = 15` (garantiert mind. 15 PP, damit beim
ersten Prestige 3–4 Shop-Items kaufbar sind). Greift NUR für Bronze.

### 1.6 Vollständige PP-Berechnung (CalculateTotalPrestigePoints) — Reihenfolge

Quelle: `Services/PrestigeService.cs` (`CalculateTotalPrestigePoints`). Diese Methode ist die
Single Source of Truth für Auszahlung UND Dialog-Vorschau. Exakte Reihenfolge:

1. `basePoints = floor(sqrt(CurrentRunMoney / 100_000))` (siehe 1.3)
2. `tierPoints = round(basePoints × GetPointMultiplier(tier))` (siehe 1.4)
3. **Bronze-Floor:** wenn Bronze und `tierPoints < 15` → `tierPoints = 15`
4. **Challenge-Multiplikator** (nur wenn `ActiveChallenges.Count > 0`):
   `tierPoints = round( tierPoints × GetTotalPpMultiplier(ActiveChallenges) )` (siehe 1.8)
5. **Prestige-Pass** (wenn `state.IsPrestigePassActive`): `tierPoints = round( tierPoints × 1.5 )` (+50 %)
6. **Gilden-Forschungs-PP-Bonus** (wenn `GuildMembership.ResearchPrestigePointBonus > 0`):
   `tierPoints = round( tierPoints × (1 + ResearchPrestigePointBonus) )`
7. **Bonus-PP** (flat, NACH Tier-Multiplikator addiert):
   `tierPoints += CalculateBonusPrestigePoints(tier)` (siehe 1.7)
8. Rückgabe `tierPoints`.

Wichtig: Die multiplikativen Schritte (4–6) wirken NUR auf den Tier-Multiplikator-Anteil, nicht auf
die flachen Bonus-PP (Schritt 7 addiert danach).

### 1.7 Bonus-PP (flat, NACH Tier-Multiplikator) + sqrt-Tier-Faktor

Quelle: `Services/PrestigeService.Challenges.cs` (`CalculateBonusPrestigePoints`),
`Models/GameBalanceConstants.cs`

Reihenfolge der Akkumulation (`bonusPp` startet 0):

| Bedingung | Bonus-PP | Cap | Konstante |
|-----------|----------|-----|-----------|
| Perfect Ratings: `+1 PP` je 10 Perfects (`Statistics.PerfectRatings / 10`) | `perfectBlocks × 1` | max +5 | `BonusPpPerPerfectBlock = 1`, `BonusPpPerfectRatingsCap = 5` |
| Voll erforschte Research-Branch: `+2 PP` pro komplettem Branch (alle Nodes der Branch `IsResearched`, mind. 1 Node in Branch) | `+2` pro Branch | (kein expliziter Cap; faktisch max +8 bei **4** Branches) | `BonusPpFullBranch = 2` |
| Alle Gebäude auf Level 5 (`Buildings.Count >= 7` UND alle `Level >= 5`) | `+1` | — | `BonusPpAllBuildingsMax = 1` |
| Level-Überschuss: `+0.05 PP` pro Level über `tier.GetRequiredLevel()` | `floor( extraLevels × 0.05 )` | max +5 | `BonusPpPerExtraLevel = 0.05`, `BonusPpExtraLevelCap = 5` |

Danach Tier-Skalierungs-Faktor angewendet:

```
tierFactor = sqrt( (int)tier + 1 )
bonusPp = round( bonusPp × tierFactor )
```

`tierFactor`-Werte (laut Code-Kommentar): None=×1.0, Bronze=×1.41, Silver=×1.73, Gold=×2.0,
Platin=×2.24, Diamant=×2.45, Meister=×2.65, Legende=×2.83.

### 1.8 Prestige-Herausforderungen (Challenges)

Quelle: `Models/Enums/PrestigeChallengeType.cs` (`PrestigeChallengeExtensions`),
`Models/PrestigeData.cs` (`ActiveChallenges`)

- Max gleichzeitig aktiv: `MaxActiveChallenges = 3`.
- PP-Boni stacken **additiv**; `GetTotalPpMultiplier = 1.0 + Summe(GetPpBonus)`.
- Bei leerer Liste → Multiplikator 1.0.

| Challenge | Enum-Wert | PP-Bonus (GetPpBonus) | Effekt-Beschreibung |
|-----------|-----------|------------------------|---------------------|
| Spartaner | 0 | +0.45 (45 %) | Max 3 Worker pro Workshop |
| OhneForschung | 1 | +0.30 (30 %) | Keine Forschung möglich |
| Inflationszeit | 2 | +0.25 (25 %) | Doppelte Upgrade-Kosten |
| SoloMeister | 3 | +0.50 (50 %) | Nur 1 Workshop erlaubt |
| Sprint | 4 | +0.35 (35 %) | Kein Offline-Einkommen |
| KeinNetz | 5 | +0.20 (20 %) | Keine Lieferanten |

Hinweis: Die CLAUDE.md-Tabelle nennt Spartaner +45 %, SoloMeister +50 % — entspricht dem Code
(frühere Werte +40 %/+60 % sind im Kommentar überholt vermerkt).

Aktive Challenges bleiben nach dem Reset für den neuen Run erhalten (Constraints werden ab dann
enforced). `AbandonChallengeRun()` (Quelle: `PrestigeService.Challenges.cs`): vergibt
`max(1, basePoints / 2)` PP (50 % der Basis-PP ohne Challenge-Bonus, ohne Tier-Multiplikator),
leert `ActiveChallenges`, invalidiert Effekt-Cache.

### 1.9 Permanenter Multiplikator + Diminishing Returns + Cap 20×

Quelle: `Services/PrestigeService.cs` (`ApplyPrestige`),
`Models/Enums/PrestigeTier.cs` (`GetPermanentMultiplierBonus`), `Models/GameBalanceConstants.cs`

Basis-Bonus pro Prestige (GetPermanentMultiplierBonus):

| Tier | Basis-Bonus |
|------|-------------|
| Bronze | +0.20 (20 %) |
| Silver | +0.35 (35 %) |
| Gold | +0.50 (50 %) |
| Platin | +1.00 (100 %) |
| Diamant | +2.00 (200 %) |
| Meister | +4.00 (400 %) |
| Legende | +8.00 (800 %) |

Diminishing Returns (pro wiederholtem Prestige desselben Tiers):

```
tierCount   = (Tier-spezifischer Count NACH Inkrement) − 1   (auf >= 0 geclamped)
diminishedBonus = baseBonus × ( 1 / (1 + DiminishingReturnsPerTierPrestige × tierCount) )
PermanentMultiplier += diminishedBonus
PermanentMultiplier = min( round(PermanentMultiplier, 3), MaxPermanentMultiplier )
```

- `DiminishingReturnsPerTierPrestige = 0.2` (Konstante; nach 5 Same-Tier-Prestiges nur noch 50 % Bonus).
- `MaxPermanentMultiplier = 20.0m` (Cap, privates const in `PrestigeService.cs`).
- `PermanentMultiplier` startet bei `1.0m` (Default in `PrestigeData.cs`).
- Hinweis: Der Code-Kommentar in der CLAUDE.md nennt Faktor 0.1, der tatsächliche Konstantenwert
  (`GameBalanceConstants.DiminishingReturnsPerTierPrestige`) ist **0.2**.

`GetPermanentMultiplier()` gibt nur diesen Tier-Multiplikator zurück; Shop-Income-Boni werden
separat im GameLoop/Offline angewendet.

### 1.10 Prestige-Pass

Quelle: `Services/PrestigeService.cs` (`ActivatePrestigePass`, `CalculateTotalPrestigePoints`)

- `IsPrestigePassActive` = einmaliger IAP-Kauf, permanent (wird NICHT pro Prestige zurückgesetzt;
  siehe Reset-Preservierung 1.12).
- Effekt: +50 % auf die berechneten Tier-PP (`× 1.5`, Schritt 5 in 1.6).
- `ActivatePrestigePass()` setzt nur das Flag `state.IsPrestigePassActive = true`.

### 1.11 Tier-Zähler, History, Sonstiges bei ApplyPrestige

Quelle: `Services/PrestigeService.cs` (`ApplyPrestige`), `Models/PrestigeData.cs`

- `PrestigePoints += tierPoints`; `TotalPrestigePoints += tierPoints`.
- Tier-Zähler inkrementieren (BronzeCount … LegendeCount).
- `CurrentTier` wird auf `tier` gesetzt, wenn `tier > CurrentTier`.
- `History` (List<PrestigeHistoryEntry>): neuester Eintrag an Index 0 eingefügt, auf max 20 Einträge
  begrenzt (`History.RemoveRange(20, …)`).
- Legacy-Felder synchron: `state.PrestigeLevel = TotalPrestigeCount`,
  `state.PrestigeMultiplier = PermanentMultiplier`.
- `TotalPrestigeCount` (computed) = `BronzeCount + SilverCount + GoldCount + PlatinCount +
  DiamantCount + MeisterCount + LegendeCount`.
- Nach Bronze-Prestige: `SpeedBoostEndTime = UtcNow + 15 Minuten` (15 min 3× Speed-Boost,
  nur bei Bronze).
- `PrestigesSinceLastWeeklyReward++` (für Wochen-Meilenstein, siehe 1.13).
- `RunStartTime = UtcNow` (neuer Speedrun-Run startet) — gesetzt am Ende von `ResetProgress`.
- Doppel-Tap-Guard: `Interlocked.CompareExchange` auf `_prestigeInProgress` (nur erster Aufruf gewinnt).

### 1.12 Reset-Preservierung pro Tier (ResetProgress)

Quelle: `Services/PrestigeService.cs` (`ResetProgress`), `Models/Enums/PrestigeTier.cs`
(`Keeps*`-Methoden)

Schwellen (kumulativ — höhere Tiers behalten alles von niedrigeren plus eigenes):

| Behalten | Ab Tier | Methode |
|----------|---------|---------|
| Research | Gold (≥ 3) | `KeepsResearch` |
| Prestige-Shop-Items | Platin (≥ 4) | `KeepsShopItems` |
| MasterTools (Meisterwerkzeuge) | Diamant (≥ 5) | `KeepsMasterTools` |
| Gebäude (Level → 1) | Meister (≥ 6) | `KeepsBuildings` |
| Equipment-Inventar | Meister (≥ 6) | `KeepsEquipment` |
| Manager (Level → 1) | Legende (≥ 7) | `KeepsManagers` |
| Beste Worker (Top 3/Workshop) | Legende (≥ 7) | `KeepsBestWorkers` |

**Immer erhalten (nicht angefasst):** `Prestige` (PrestigeData), `UnlockedAchievements`,
`IsPremium`, `Tutorial.SeenHints`, `TotalMoneyEarned`, `Statistics.TotalPlayTimeSeconds`,
`Statistics.BestPerfectStreak`, Settings (Sound/Music/Haptics/Language), `CreatedAt`, `BattlePass`,
`CurrentSeasonalEvent`, `ClaimedLevelOffers`, `HasPurchasedStarterPack`, `VipLevel`,
`TotalPurchaseAmount`, `Friends`, `Statistics.TotalTournamentsPlayed`, `StreakRescueUsed`,
`IsPrestigePassActive`, GuildMembership.

**Immer zurückgesetzt:** PlayerLevel→1, CurrentXp/TotalXp→0, CurrentRunMoney→0,
TotalMoneySpent→0, Workshops (→ nur Carpenter Lvl 1 + 1 Worker), UnlockedWorkshopTypes (→ Carpenter),
WorkerMarket, Orders/ParallelOrders/AvailableOrders, Reputation (neu, mit Start-Reputation),
Events/EventHistory, LuckySpin, WeeklyMissionState, WelcomeBackOffer, Tournament, Crafting (außer
Erbstücke), DailyChallengeState, QuickJobs, DailyRewards, Boosts (SpeedBoost/XpBoost/RushBoost),
PendingDelivery, DailyShopOffer, Workshop-Spezialisierung (→ null), ReservedInventory,
ActiveCraftingJobs, PerfectRatings/PerfectStreak/MiniGamesPlayed (Statistics).
PerfectRatingCounts wird beim Prestige NICHT mehr resettet (nur bei Ascension).

**Startgeld nach Prestige:**
```
startMoney = tier.GetTierStartMoney()                          (siehe Tabelle unten)
           + Summe ExtraStartMoney aller gekauften Shop-Items  (pp_start_money 5.000, pp_start_money_big 50.000)
falls Ascension-StartCapitalMultiplier > 1.0:
   startMoney = round( startMoney × ascCapitalMultiplier, 0 )
```

GetTierStartMoney (Quelle: `PrestigeTier.cs`):

| Tier | Tier-Startgeld |
|------|----------------|
| Bronze | 10.000 |
| Silver | 100.000 |
| Gold | 1.000.000 |
| Platin | 25.000.000 |
| Diamant | 250.000.000 |
| Meister | 2.500.000.000 |
| Legende | 25.000.000.000 |
| None | 100 |

**Start-Worker-Tier:** Default `WorkerTier.E`. Wird durch gekaufte Shop-Items mit
`StartingWorkerTier` erhöht (pp_better_start_worker → D, pp_start_worker_b → B); höchster gilt.

**Legende — beste Worker sichern (`KeepsBestWorkers`):** vor Reset werden pro Workshop die Top-3
(nach `Efficiency`) gesichert; Worker zurückgesetzt (Mood=80, Fatigue=0, IsResting/IsTraining=false).
Keys: `"{Type}"` (Index 0, backward-compatible) und `"{Type}_1"`, `"{Type}_2"`. Beim Workshop-Unlock
via `RestoreKeptWorkers` wiederhergestellt (nur wenn `worker.Tier >= startWorkerTier`); mind. 1 Worker
garantiert via `Worker.CreateForTier(startWorkerTier, wsType)`.

**Ascension-Perk Quick-Start:** schaltet `GetQuickStartWorkshops()` Workshops in fester Reihenfolge frei:
Plumber, Electrician, Painter, Roofer, Contractor, Architect, GeneralContractor, MasterSmith.

### 1.13 Prestige-Meilensteine (GS-Belohnungen, permanent über Ascension)

Quelle: `Models/GameBalanceConstants.cs` (`PrestigeMilestones`),
`Services/PrestigeService.Challenges.cs` (`CheckAndAwardMilestones`)

Kumulativ nach `TotalPrestigeCount`; ID in `ClaimedMilestones` (HashSet) → einmalig:

| Benötigte Prestiges | ID | GS-Belohnung |
|---------------------|-----|--------------|
| 1 | pm_first | 10 |
| 5 | pm_5 | 20 |
| 10 | pm_10 | 35 |
| 25 | pm_25 | 50 |
| 50 | pm_50 | 75 |
| 100 | pm_100 | 100 |

**Wiederholbarer Wochen-Meilenstein:** wenn `PrestigesSinceLastWeeklyReward >= 7` →
`weeklyReward = 5 GS`, Counter `-= 7`, ID `pm_weekly`. Counter wird in `ApplyPrestige` hochgezählt.

GS-Gutschrift via `AddGoldenScrews(reward, fromPurchase: false)` (Gameplay-Quelle, kann durch
Premium +100 % verdoppelt werden). `ClaimedMilestones` überlebt Ascension.

### 1.14 Speedrun-Belohnungen

Quelle: `Models/SpeedrunRewards.cs` (`CalculateReward`), `Services/PrestigeService.cs` (`ApplyPrestige`),
`Models/PrestigeData.cs` (`BestRunTimes`)

- Run-Dauer = `UtcNow − RunStartTime` (gemessen vor dem Reset). Nur wenn `RunStartTime > MinValue`.
- Bestzeit pro Tier in `BestRunTimes` (Dictionary<string, long>, Key = `tier.ToString()`, Value = Ticks).
  Neue Bestzeit, wenn kein Eintrag existiert ODER `runDuration.Ticks < bestTicks`.
- Bei neuer persönlicher Bestzeit: Bestzeit speichern, Belohnung über `CalculateReward(tier, runDuration)`
  via `AddGoldenScrews` gutschreiben, `SpeedrunRecordSet`-Event feuern.
- `BestRunTimes` überlebt Ascension.

**GS-Belohnungstabelle (Tier × Time-Bracket).** Logik: höchste passende Belohnung (kleinster
TimeLimit, das `runDuration.TotalHours <= limit` erfüllt) gewinnt; durchläuft Brackets in der
gelisteten Reihenfolge und überschreibt fortlaufend, daher zählt das schnellste erfüllte Bracket:

| Tier | Bracket 1 (langsamster) | Bracket 2 | Bracket 3 (schnellster) |
|------|--------------------------|-----------|--------------------------|
| Bronze | ≤ 2.0 h → 5 GS | ≤ 1.0 h → 10 GS | ≤ 0.5 h → 15 GS |
| Silver | ≤ 3.0 h → 10 GS | ≤ 2.0 h → 20 GS | ≤ 1.0 h → 35 GS |
| Gold | ≤ 5.0 h → 15 GS | ≤ 3.0 h → 30 GS | ≤ 1.5 h → 60 GS |
| Platin | ≤ 8.0 h → 25 GS | ≤ 5.0 h → 50 GS | ≤ 2.5 h → 100 GS |
| Diamant | ≤ 12.0 h → 40 GS | ≤ 8.0 h → 80 GS | ≤ 4.0 h → 150 GS |
| Meister | ≤ 20.0 h → 60 GS | ≤ 12.0 h → 120 GS | ≤ 6.0 h → 250 GS |
| Legende | ≤ 30.0 h → 100 GS | ≤ 20.0 h → 200 GS | ≤ 10.0 h → 400 GS |

- `runDuration <= TimeSpan.Zero` → 0 GS.
- None/sonst → keine Brackets → 0 GS.
- `GetGoldBracketHours(tier)` (UI-Anzeige bestes Bracket): Bronze 0.5, Silver 1.0, Gold 1.5,
  Platin 2.5, Diamant 4.0, Meister 6.0, Legende 10.0.

### 1.15 Tier-Darstellung (Farben/Icons)

Quelle: `Models/Enums/PrestigeTier.cs`

| Tier | Farbe (Hex) | Icon (Material) | Loc-Key |
|------|-------------|-----------------|---------|
| None | #9E9E9E | (leer) | PrestigeNone |
| Bronze | #CD7F32 | MedalOutline | PrestigeBronze |
| Silver | #C0C0C0 | Medal | PrestigeSilver |
| Gold | #FFD700 | TrophyAward | PrestigeGold |
| Platin | #E5E4E2 | DiamondStone | PrestigePlatin |
| Diamant | #B9F2FF | StarFourPoints | PrestigeDiamant |
| Meister | #FF4500 | Fire | PrestigeMeister |
| Legende | #FF69B4 | Crown | PrestigeLegende |

`GetLocalizationKey() = $"Prestige{tier}"`. `GetNextTier()`: None→Bronze→Silver→Gold→Platin→
Diamant→Meister→Legende→None.

---

## 2. Prestige-Shop (24 Items)

Quelle: `Models/PrestigeShop.cs`, `Models/PrestigeShopItem.cs`, `Services/PrestigeService.cs`

### 2.1 Item-Liste (vollständig)

Kosten in PP. Effekt-Feld zeigt die im `PrestigeEffect` gesetzten Werte. "Wiederholbar" = `IsRepeatable`.
Tier-Lock = `RequiredTier` (None = immer sichtbar). Alle Items ohne RequiredTier-Angabe haben `None`.

| # | Id | Kosten (PP) | Kategorie | Effekt | Wiederholbar | Tier-Lock |
|---|-----|-------------|-----------|--------|--------------|-----------|
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
| 15 | pp_offline_hours | 35 | SpeedAndAutomation | OfflineHoursBonus +4 (h max. Offline) | nein | None |
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

Hinweis: Es sind **25** Items im Code (24 + 1 Tier-locked). Die Aufgabe nannte "~23–24" — die exakte
Anzahl ist 25 Einträge in `_allItems`.

Icons (Material): pp_income_10/25 = Cash, pp_income_50 = DiamondStone, pp_income_100 = CashMultiple,
pp_cost_15/30 = TrendingDown, pp_upgrade_discount = ArrowDown, pp_better_start_worker = HardHat,
pp_start_worker_b = AccountStar, pp_mood_slow = EmoticonHappy, pp_mood_immunity = ShieldHalfFull,
pp_rush_boost = LightningBolt, pp_delivery_speed = TruckDelivery, pp_crafting_speed = Hammer,
pp_offline_hours = ClockPlus, pp_quickjob_limit = LightningBoltCircle, pp_start_money(_big) = Bank,
pp_xp_15/30 = Star, pp_golden_screw_25 = Screwdriver, pp_income_repeatable = Refresh,
pp_order_reward_rep = ClipboardCheck, pp_delivery_interval_rep = TruckFast,
pp_research_speed_tier = FlaskRoundBottom.

### 2.2 Wiederholbare Items — Kosten & Cap

Quelle: `Services/PrestigeService.cs` (`GetRepeatableItemCost`, `BuyShopItem`),
`Models/GameBalanceConstants.cs`

```
Kosten(purchaseCount) = item.Cost × 2^( min(purchaseCount, 15) )
```

- D. h. erster Kauf = Basis, dann verdoppeln: z. B. pp_income_repeatable 15/30/60/120/240/480/960/1920.
- Max-Käufe pro wiederholbarem Item: `MaxRepeatableShopPurchases = 8`. Ab 8 Käufen ist das Item als
  "gekauft" (gesperrt) markiert.
- Kauf-Anzahl in `PrestigeData.RepeatableItemCounts` (Dict<Id, int>).

### 2.3 Tier-Lock-Sichtbarkeit

Quelle: `Services/PrestigeService.cs` (`GetShopItems`)

Tier-locked Items (RequiredTier != None) sind nur sichtbar, wenn `CurrentTier >= RequiredTier`
ODER das Item bereits gekauft wurde. Aktuell nur pp_research_speed_tier (Diamant).

### 2.4 Effekt-Caps (aggregiert)

Quelle: `Services/PrestigeService.cs` (`RefreshEffectCacheIfNeeded`)

Beim Aggregieren über gekaufte Items werden folgende Caps angewendet:

| Effekt | Cap |
|--------|-----|
| CostReduction (Summe) | max 0.50 (−50 %) |
| MoodDecayReduction (Summe) | max 0.50 (−50 %) |
| XpMultiplier (Summe) | kein expliziter Cap |
| OrderRewardBonus (Summe, inkl. repeatable × count) | max 1.0 (+100 %) |
| ResearchSpeedBonus (Summe) | max 0.50 (−50 %) |

Wiederholbare Items: Effekt × Kaufanzahl. IncomeMultiplier-Aggregation erfolgt separat in
`IncomeCalculatorService.GetPrestigeIncomeBonus(state)` (nicht in RefreshEffectCacheIfNeeded —
dort werden CostReduction, MoodDecay, XpMultiplier, OrderRewardBonus, ResearchSpeedBonus gecacht).
DeliverySpeedBonus wird im GameLoopService separat gecacht.

### 2.5 Kauf-Logik

Quelle: `Services/PrestigeService.cs` (`BuyShopItem`)

- Einmalig: ablehnen, wenn schon in `PurchasedShopItems` oder `PrestigePoints < Cost`. Sonst PP abziehen,
  ID zu `PurchasedShopItems` hinzufügen.
- Wiederholbar: ablehnen, wenn `count >= MaxRepeatableShopPurchases (8)` oder `PrestigePoints < cost`.
  Sonst PP abziehen (`GetRepeatableItemCost`), `RepeatableItemCounts[id]++`.
- Käufe unter State-Lock (atomar gegen Doppel-Tap).

---

## 3. Ascension-System (Meta-Prestige)

### 3.1 Freischaltung

Quelle: `Services/AscensionService.cs` (`CanAscend`)

```
CanAscend = Prestige.LegendeCount >= 3
```

### 3.2 AP-Berechnung (CalculateAscensionPoints)

Quelle: `Services/AscensionService.cs` (`CalculateAscensionPoints`)

```
apFromPP       = TotalPrestigePoints / 500            (1 AP pro 500 PP, ganzzahlig)
apFromLegende  = LegendeCount / 2                     (1 AP pro 2 Legende-Prestiges)
apFromMaxLevel = (Workshops mit Level >= Workshop.MaxLevel) >= 8 ? 2 : 0
apFromTools    = CollectedMasterTools.Count >= 12 ? 1 : 0
premiumBonus   = IsPremium ? 1 : 0
apFromScaling  = (int)sqrt( Ascension.AscensionLevel ) × 2

result = max( 5, apFromPP + apFromLegende + apFromMaxLevel + apFromTools + premiumBonus + apFromScaling )
```

- Minimum 5 AP (damit Ascension sich immer lohnt).
- `apFromScaling`: Wurzel-Skalierung (AL=50 → +14 AP, AL=100 → +20 AP, laut Kommentar).
- `Workshop.MaxLevel` = 1000 (siehe Rebirth, der CanRebirth ebenfalls darauf prüft).

### 3.3 Die 6 Ascension-Perks

Quelle: `Models/AscensionPerk.cs` (`GetAll`), `Services/AscensionService.cs`,
`Models/AscensionData.cs`

Alle Perks: MaxLevel 3. Kosten und Werte als Arrays (Index 0 = Level 1). Gesamt-AP für alle Perks
auf Max: Summe aller CostsPerLevel.

| Perk-Id | Loc-Key | Icon | MaxLevel | Kosten/Level (AP) | Werte/Level | Wirkung |
|---------|---------|------|----------|-------------------|-------------|---------|
| asc_start_capital | AscStartCapital | Bank | 3 | [1, 3, 5] | [1.00, 5.00, 10.00] | Start-Kapital-Multiplikator = 1 + Wert → ×2.0 / ×6.0 / ×11.0 (+100 %/+500 %/+1000 % Startgeld) |
| asc_eternal_tools | AscEternalTools | Wrench | 3 | [2, 4, 5] | [2, 4, 5] | MasterTools bewahren: Lvl1 erste 2, Lvl2 erste 4, Lvl3 alle |
| asc_quick_start | AscQuickStart | RocketLaunch | 3 | [1, 3, 5] | [2, 4, 8] | Start mit 2 / 4 / 8 Workshops freigeschaltet |
| asc_timeless_research | AscTimelessResearch | FlaskOutline | 3 | [1, 2, 5] | [0.15, 0.30, 0.50] | Research-Dauer −15 % / −30 % / −50 % |
| asc_golden_era | AscGoldenEra | Screwdriver | 3 | [1, 3, 5] | [0.20, 0.50, 1.00] | Goldschrauben-Verdienst +20 % / +50 % / +100 % |
| asc_legendary_reputation | AscLegendaryReputation | StarCircle | 3 | [1, 2, 5] | [65, 80, 100] | Start-Reputation 65 / 80 / 100 (Default 50) |

Kosten-Summe pro Perk auf Max: start_capital 9, eternal_tools 11, quick_start 9, timeless_research 8,
golden_era 9, legendary_reputation 8 → **gesamt 54 AP** für alle 6 Perks auf Max.

**Perk-Abfragen (AscensionService):**
- `GetStartCapitalMultiplier()`: `bonus == 0 ? 1.0 : 1.0 + bonus` (Lvl0 → 1.0).
- `GetEternalToolsLevel()`: liefert Perk-Level direkt (0–3).
- `GetQuickStartWorkshops()`: Lvl0 → 0, sonst `(int)GetValue(level)` (2/4/8).
- `GetStartReputation()`: Lvl0 → 50, sonst `(int)GetValue(level)` (65/80/100).
- `GetGoldenScrewBonus()`: `GetPerkValue("asc_golden_era")` (0/0.20/0.50/1.00).
- `GetResearchSpeedBonus()`: `GetPerkValue("asc_timeless_research")` (0/0.15/0.30/0.50).

**Upgrade-Logik (`UpgradePerk`):** ablehnen, wenn auf MaxLevel oder `AscensionPoints < GetCost(level+1)`.
Sonst AP abziehen, `Perks[perkId] = currentLevel + 1`.

`AscensionPerk.GetValue(level)` clamped Level auf MaxLevel (Save-Kompatibilität für alte Saves > 3).

### 3.4 DoAscension — Reset & Preservierung

Quelle: `Services/AscensionService.cs` (`DoAscension`)

- AP gutschreiben: `AscensionLevel++`, `AscensionPoints += calculatedAP`,
  `TotalAscensionPoints += calculatedAP`, `LastAscensionDate = UtcNow`.
- Startgeld nach Ascension: `Money = 1000m × GetStartCapitalMultiplier()`.
- **Härtester Reset im Spiel** — resettet ALLES inkl. Prestige-Daten:
  Player (Level 1, XP 0), Workshops (nur Carpenter Lvl 1 + 1 E-Worker), WorkerMarket, Orders,
  Reputation (`new CustomerReputation()` → Score 50, KEIN Ascension-Reputation-Perk-Apply hier),
  Research (`ResearchTree.CreateAll()`), Events, Boosts, DailyRewards, Lieferant, QuickJobs,
  DailyChallenges, LuckySpin, WeeklyMissions, WelcomeBack, Tournament, Crafting-Inventar, DailyShopOffer,
  Workshop-Spezialisierung. `PerfectRatingCounts?.Clear()` (anders als Prestige).
  Manager, Equipment, Gebäude komplett gelöscht.

**Prestige-Daten Reset mit Preservierung** (`state.Prestige = new PrestigeData { … }`):
bewahrt nur:
- `ClaimedMilestones` (GS-Belohnungen, permanent)
- `BestRunTimes` (Speedrun-Bestzeiten)
- `PurchasedShopItems` (PP-Investitionen, permanent)
- `RepeatableItemCounts` (wiederholbare Shop-Käufe)

Alle anderen Prestige-Felder (PrestigePoints, TotalPrestigePoints, Tier-Counts, PermanentMultiplier,
History, ActiveChallenges, …) werden zurückgesetzt. Legacy: `PrestigeLevel = 0`,
`PrestigeMultiplier = 1.0m`.

**EternalTools-Perk beim Ascension:** `keptTools` analog — Lvl≥3 alle, Lvl1 erste 2, Lvl2 erste 4,
Lvl0 keine. Danach `CollectedMasterTools` geleert und `keptTools` zurückgeschrieben.

**Bewahrt (nicht angefasst):** `Ascension` (inkl. Perks/PermanentHeirlooms), `WorkshopStars`
(Rebirth-Sterne, permanent), `UnlockedAchievements`, `IsPremium`, Tutorial,
`TotalMoneyEarned`, `Statistics.TotalPlayTimeSeconds`/`BestPerfectStreak`, Settings, `CreatedAt`,
`BattlePass`, `CurrentSeasonalEvent`, `ClaimedLevelOffers`, `HasPurchasedStarterPack`, VIP,
`Friends`, `GuildMembership`, `PlayerGuid`/`PlayerName`, Cosmetics.

### 3.5 Soft-Cap-Floor (apFromScaling)

Quelle: `Services/AscensionService.cs`. Es gibt keinen Soft-Cap-"Floor" im klassischen Sinn — der
"Floor" ist das Minimum von 5 AP (`Math.Max(5, …)`). Die Wurzel-Skalierung `apFromScaling` ersetzt
frühere lineare Skalierung, um Whale-Inflation zu begrenzen. (Den eigentlichen Income-Soft-Cap-Floor
betreibt EternalMastery, siehe Abschnitt 5 / SoftCapThreshold im IncomeCalculator.)

---

## 4. Workshop-Rebirth-System (0–5 Sterne)

### 4.1 Trigger & Voraussetzung

Quelle: `Services/RebirthService.cs` (`CanRebirth`), `Models/Workshop.cs` (`MaxLevel`)

```
CanRebirth(type) = Workshop.Level >= Workshop.MaxLevel (= 1000) AND GetStars(type) < 5
```

Sterne überleben Prestige UND Ascension (permanentester Fortschritt). Persistiert in
`GameState.WorkshopStars` (Dict<WorkshopType.ToString(), int>).

### 4.2 Rebirth-Kosten (RebirthCosts)

Quelle: `Services/RebirthService.cs` (`RebirthCosts`-Array, `GetRebirthCost`)

Pro nächstem Stern (`nextStar = GetStars + 1`, Index `nextStar - 1`): (Goldschrauben, Geld-Prozent
des aktuellen `state.Money`).

| Nächster Stern | Goldschrauben | Geld-Prozent (von state.Money) |
|----------------|---------------|--------------------------------|
| 1 | 50 | 0.10 (10 %) |
| 2 | 125 | 0.15 (15 %) |
| 3 | 250 | 0.20 (20 %) |
| 4 | 200 | 0.25 (25 %) |
| 5 | 400 | 0.30 (30 %) |

- `moneyCost = state.Money × moneyPercent` (zum Zeitpunkt des Rebirth).
- Fallback (sollte nie greifen): `(int.MaxValue, 1.0m)`.
- Hinweis: Die CLAUDE.md-Tabelle (100/250/500/500/1000) ist **veraltet**. Code-Wahrheit ist
  50/125/250/200/400 GS.

### 4.3 Rebirth-Durchführung

Quelle: `Services/RebirthService.cs` (`DoRebirth`)

1. GS abziehen (`TrySpendGoldenScrews`). Bei Fehlschlag → abbrechen.
2. Geld abziehen (`TrySpendMoney(moneyCost)`). Bei Fehlschlag → GS zurückgeben
   (`AddGoldenScrews(cost.goldenScrews, fromPurchase: true)` — kein Bonus-Apply auf Rollback) → abbrechen.
3. Stern erhöhen: `WorkshopStars[type] = GetStars + 1`.
4. Workshop `Level = 1` (TotalEarned und Worker bleiben erhalten, bewusst kein Reset).
5. `ApplyStarsToWorkshops()` + Income-Cache invalidieren + LevelUp-Sound + `RebirthCompleted`-Event.

`ApplyStarsToWorkshops()` setzt `workshop.RebirthStars` aus `WorkshopStars`. Wird auf StateLoaded,
PrestigeCompleted und AscensionCompleted erneut angewendet (neue Workshops erben Sterne).

### 4.4 Stern-Boni (Einkommen / Upgrade-Rabatt / Extra-Worker)

Hinweis: Die Boni-Werte (IncomeBonus, UpgradeDiscount, ExtraWorkers) sind im RebirthService selbst
NICHT definiert — der Service vergibt nur Sterne und setzt `workshop.RebirthStars`. Die Wirkung der
Sterne (Einkommens-Bonus, Upgrade-Rabatt, Extra-Worker-Slots) ist im `Workshop`-Modell bzw.
`WorkshopFormulas` verankert und in **dieser** Datei nicht aus den gelesenen Dateien belegbar.

**Laut CLAUDE.md (NICHT aus den hier gelesenen Code-Dateien verifiziert, daher als Plan-Referenz):**

| Sterne | Einkommens-Bonus | Upgrade-Rabatt | Extra-Worker | GS-Kosten (CLAUDE.md, veraltet) |
|--------|------------------|----------------|--------------|----------------------------------|
| 1 | +15 % | −5 % | +1 | (Code: 50) |
| 2 | +35 % | −10 % | +1 | (Code: 125) |
| 3 | +60 % | −15 % | +2 | (Code: 250) |
| 4 | +100 % | −20 % | +2 | (Code: 200) |
| 5 | +150 % | −25 % | +3 | (Code: 400) |

**Code-verifiziert (`GameBalanceConstants.cs` Z.197/200/206, `RebirthService.cs` Z.32-38):**
- `RebirthIncomeBonuses = [0.15, 0.35, 0.60, 1.00, 1.50]` (Index = Sterne − 1) → +15/35/60/100/150 % Einkommen.
- `RebirthUpgradeDiscounts = [0.05, 0.10, 0.15, 0.20, 0.25]` (Index = Sterne − 1) → −5/10/15/20/25 % Upgrade-Kosten.
- `RebirthExtraWorkers = [0, 1, 1, 2, 2, 3]` (Index = Sterne, 0-5) → kumulativ +0/+1/+1/+2/+2/+3 Worker-Slots.
- `RebirthCosts` (Goldschrauben, Money-Prozent): Stern 1 (50, 10 %), Stern 2 (125, 15 %), Stern 3 (250, 20 %), Stern 4 (200, 25 %), Stern 5 (400, 30 %).
- Trigger: `workshop.Level >= Workshop.MaxLevel` (1000) **und** `GetStars(type) < 5` (`RebirthService.CanRebirth`).
- Sterne sind permanent (`state.WorkshopStars`), ueberleben Prestige **und** Ascension; `DoRebirth` setzt `workshop.Level = 1`.
- **GS-Kosten: Code-Wahrheit ist die obige Tabelle, nicht die aelteren CLAUDE.md-Werte (100/250/500/500/1000).**

---

## 5. EternalMastery (permanenter Einkommens-Bonus)

Quelle: `Services/EternalMasteryService.cs`, `Models/GameBalanceConstants.cs`,
`Services/IncomeCalculatorService.cs`

### 5.1 Aktivierung & Basis

```
CompletedPrestiges = Prestige.TotalPrestigeCount
IsActive = CompletedPrestiges > 0
IncomeBonus = CalculateBonus(CompletedPrestiges)
```

Kein Reset bei Ascension (basiert auf `TotalPrestigeCount`, der bei Ascension allerdings über das
neue PrestigeData zurückgesetzt würde — siehe Hinweis unten).

> Hinweis zur Wechselwirkung: `CalculateBonus` liest `Prestige.TotalPrestigeCount`. Bei Ascension wird
> `state.Prestige` neu erzeugt (Tier-Counts = 0), wodurch `TotalPrestigeCount` faktisch resettet.
> Der Code-Kommentar formuliert "kein Reset bei Ascension" als Design-Ziel — die Persistenz erfolgt
> implizit darüber, dass nur die Tier-Counts resettet werden. (Aus dem Code wörtlich: CalculateBonus
> hängt allein an TotalPrestigeCount.)

### 5.2 Soft-Cap-Dämpfung (CalculateBonus)

```
if completedPrestiges <= 0: return 0

effectivePrestiges = completedPrestiges
if completedPrestiges > EternalMasterySoftCapThreshold (= 50):
    excess = completedPrestiges - 50
    effectivePrestiges = 50 + (int)( log10(excess + 1) × 10 )

linear     = effectivePrestiges × EternalMasteryBonusPerPrestige      (0.005 = +0.5 % je Prestige)
tier5Bonus = (effectivePrestiges / 5)  × EternalMasteryBonusPer5Prestiges   (0.025 = +2.5 % je 5)
tier10Bonus= (effectivePrestiges / 10) × EternalMasteryBonusPer10Prestiges  (0.05  = +5 %  je 10)

IncomeBonus = linear + tier5Bonus + tier10Bonus
```

Konstanten: `EternalMasteryBonusPerPrestige = 0.005`, `EternalMasteryBonusPer5Prestiges = 0.025`,
`EternalMasteryBonusPer10Prestiges = 0.05`, `EternalMasterySoftCapThreshold = 50`.

Beispiel (Kommentar): 100 Prestiges = +150 % Income (50 % linear + 50 % 5er + 50 % 10er) — gilt vor
Soft-Cap, da hier `effectivePrestiges` bei 100 > 50 gedämpft wird; die "100 Prestiges = +150 %"
gilt rechnerisch bei ungedämpftem Wert. Mit Soft-Cap bei 100 Prestiges:
`effectivePrestiges = 50 + (int)(log10(51)×10) = 50 + 17 = 67`.

### 5.3 Hilfs-Properties

- `PrestigesUntilNextTier`: bis nächste 5er-Stufe (`0 → 5`, sonst `((CP/5)+1)×5 − CP`).
- `PrestigesUntilNextMegaTier`: bis nächste 10er-Stufe (`0 → 10`, sonst `((CP/10)+1)×10 − CP`).
- `DisplayText`: `"+{IncomeBonus×100:F1}%"` (InvariantCulture).

### 5.4 Income-Integration

Quelle: `Services/IncomeCalculatorService.cs` (`CalculateGrossIncome`)

In `CalculateGrossIncome` wird nach allen anderen Boni (Prestige-Shop, Research, MasterTools, Gilde,
VIP, Manager, Premium ×1.5) multipliziert:
```
if (_eternalMastery != null && _eternalMastery.IsActive)
    grossIncome *= (1m + _eternalMastery.IncomeBonus);
```
Danach Heirloom-Bonus (siehe Abschnitt 8). Der Income-Soft-Cap im IncomeCalculator ist tier-abhängig
(4.0x–20.0x je Prestige-Tier + Ascension-Floor, siehe Abschnitt 4) — es gibt KEINE benannte Konstante
`SoftCapThreshold`; `8.0` ist nur der switch-Default-/Silver-Zweig.

---

## 6. Reputation-System

Quelle: `Models/CustomerReputation.cs`, `Models/Enums/CustomerReputationTier.cs`,
`Services/ReputationTierEffects.cs`, `Services/GameStateService.Orders.cs`

### 6.1 Score & Multiplikatoren

- `ReputationScore` (0–100), Start **50** (Default; bei Prestige aus Ascension-Perk `GetStartReputation()`).
- `ReputationMultiplier` (Income/Reward-Multiplikator):

| Score | Multiplikator |
|-------|---------------|
| < 30 | 0.7× |
| < 60 | 1.0× |
| < 80 | 1.2× |
| ≥ 80 | 1.5× |

Dieser `ReputationMultiplier` ist die "Region-Star Reward-Mult 1.2×": Score 61–79 (RegionStar-Tier)
→ 1.2× auf Auftragsbelohnungen (`GameStateService.Orders.cs`:
`multiplier *= _state.Reputation.ReputationMultiplier` in `CalculateOrderRewardMultiplierUnlocked`).

- `ReputationLevelKey` (Loc): <30 ReputationPoor, <60 ReputationAverage, <80 ReputationGood,
  <90 ReputationExcellent, ≥90 ReputationLegendary.

### 6.2 Extra-Order-Slots & Order-Qualitäts-Bonus

`ExtraOrderSlots`:

| Score | Extra-Slots |
|-------|-------------|
| ≥ 90 | 2 |
| ≥ 70 | 1 |
| sonst | 0 |

`OrderQualityBonus` (senkt Standard-Order-Wahrscheinlichkeit):

| Score | Bonus |
|-------|-------|
| < 30 | −0.10 |
| < 60 | 0 |
| < 80 | +0.10 |
| ≥ 80 | +0.20 |

### 6.3 AddRating — Delta-Logik

Quelle: `Models/CustomerReputation.cs` (`AddRating`)

```
stars = clamp(stars, 1, 5)
RecentRatings.Add(stars); (max 50, ältester entfernt)

delta = stars: 5→+3, 4→+1, 3→0, 2→−2, sonst(1)→−5

if delta > 0 and researchReputationBonus > 0:
    delta = ceil( delta × (1 + researchReputationBonus) )   (z. B. +45 % bei voller Forschung)

ReputationScore = clamp(ReputationScore + delta, 0, 100)
```

### 6.4 Decay

Quelle: `Models/CustomerReputation.cs` (`DecayReputation`)

```
if ReputationScore > 50:
    ReputationScore = max(50, ReputationScore − 1)
```
1× pro Tag aufgerufen. Fällt nie unter 50 durch Decay.

### 6.5 Showroom-Gewinn

Quelle: `Services/BuildingService.cs` (`GetDailyReputationGain`)

`GetDailyReputationGain() = GetBuilding(BuildingType.Showroom)?.DailyReputationGain ?? 0m`. Der konkrete
Tageswert (CLAUDE.md nennt 0.5–2.5/Tag, level-abhängig) ist in `Building.cs`/`BuildingFormulas`
definiert — **nicht in den hier gelesenen Dateien** belegbar. Quelle/Wert: (Building-Modell, nicht
Teil dieser Extraktion).

### 6.6 Reputation-Tier-System (CustomerReputationTier)

Quelle: `Models/Enums/CustomerReputationTier.cs`

| Tier | Enum | Score-Range | Badge-Farbe | Stammkunden-Spawn-Bonus | Live-Order-Spawn-Chance |
|------|------|-------------|-------------|--------------------------|--------------------------|
| Beginner | 0 | 0–30 | #8B6F47 (Holz, unsichtbar) | 0 | 0 (Default) |
| CityKnown | 1 | 31–60 | #CD7F32 (Bronze) | +0.10 (+10 %) | 0 |
| RegionStar | 2 | 61–80 | #C0C0C0 (Silber) | +0.20 (+20 %) | 0.05 (5 %, Override) |
| IndustryLegend | 3 | 81–100 | #FFD700 (Gold) | +0.35 (+35 %) | 0.10 (10 %, Override) |

`GetLocalizationKey`: RepTierBeginner / RepTierCityKnown / RepTierRegionStar / RepTierIndustryLegend.

**FromScore (stateless):** ≥81 IndustryLegend, ≥61 RegionStar, ≥31 CityKnown, sonst Beginner.

**Hysterese (FromScoreWithHysteresis):** Tier-Up bei harten Schwellen (81/61/31), Tier-Down 3 Punkte
darunter (78/58/28):
- Up: ≥81 → IndustryLegend; ≥61 → RegionStar; ≥31 → CityKnown.
- Down: <78 (von IndustryLegend) → RegionStar; <58 (von RegionStar) → CityKnown;
  <28 (von CityKnown) → Beginner.

`CustomerReputation.CurrentTier` wird persistiert (`RecomputeTier(out oldTier)` liefert true bei Wechsel).

### 6.7 Tier-Up-Effekte (ReputationTierEffects)

Quelle: `Services/ReputationTierEffects.cs`

- Nur bei Tier-Aufstieg (`e.IsUp`): FloatingText (`{TierName} reached!` via `RepTierUpFormat`),
  Celebration, LevelUp-Sound.
- Achievement-Dialog mit Tier-Effekten nur für Tier > Beginner und nur für CityKnown/RegionStar/
  IndustryLegend (Effekt-Texte: RepTierCityKnownEffects / RepTierRegionStarEffects /
  RepTierIndustryLegendEffects).

---

## 7. Reputation-Shop (5 Items)

Quelle: `Services/ReputationShopService.cs`, `Models/ReputationShopItem.cs`,
`Services/Interfaces/IReputationShopService.cs`

### 7.1 Unlock & Währung

- Sichtbar/freigeschaltet ab `ReputationScore >= MinReputationToUnlock`. **MinReputationToUnlock = 60**
  (Default-Interface-Property in `IReputationShopService`).
- Bezahlt mit `ReputationScore`-Punkten (3. Währung, keine GS, kein Geld). Score wird beim Kauf
  abgezogen (`state.Reputation.ReputationScore -= item.ReputationCost`).

### 7.2 Item-Liste (vollständig)

| Id | Effekt (Enum) | Kosten (Reputation) | Icon | Name (Fallback DE) | Wirkung |
|-----|---------------|---------------------|------|--------------------|---------|
| rep_regular_customer_guarantee | RegularCustomerGuarantee | 30 | AccountStar | Stammkunden-Garantie | Nächste 5 Aufträge werden Stammkunden (bis 1.5× Reward) → setzt `RepShopRegularCustomerCharges = 5` |
| rep_faster_delivery | FasterDelivery | 20 | Truck | Schnelle Lieferung | Nächster Lieferant +50 % Speed für 1 h → `RepShopFasterDeliveryUntil = UtcNow + 1h` |
| rep_worker_mood_boost | WorkerMoodBoost | 25 | AccountGroup | Team-Stimmungs-Boost | Alle Worker +30 Mood (sofort, `min(100, Mood+30)`) |
| rep_workshop_skin_wood_premium | WorkshopSkinWoodPremium | 100 | Palette | Workshop-Skin „Holz-Premium" | Permanenter kosmetischer Skin → `RepShopWoodPremiumSkinUnlocked = true` |
| rep_insurance | ReputationInsurance | 40 | ShieldCheck | Reputation-Insurance | Nächster Risk-Miss kostet keine Reputation → `RepShopInsuranceCharges = 1` |

### 7.3 Kauf-Logik

Quelle: `Services/ReputationShopService.cs` (`TryBuy`, `ApplyEffect`)

- Ablehnen, wenn Item nicht gefunden oder `ReputationScore < ReputationCost`.
- Sonst Score abziehen, `ApplyEffect` (unter State-Lock), `ItemPurchased`-Event feuern.
- Items sind statisch/global (gleicher Inhalt für alle Spieler), nicht "ausverkauft" (mehrfach kaufbar,
  solange Score reicht).

---

## 8. Heirlooms (Erbstücke)

Quelle: `Services/PrestigeService.cs` (`ResetProgress`), `Services/AscensionService.cs` (`DoAscension`),
`Models/GameBalanceConstants.cs`, `Models/AscensionData.cs`, `Services/IncomeCalculatorService.cs`,
`Models/CraftingRecipe.cs` (CraftingProduct)

### 8.1 Heirloom-Pool (nur Tier-4-Crafting-Items)

Nur Crafting-Produkte mit `IsHeirloomEligible == true` qualifizieren — laut Plan/Code nur Tier-4-Items.
Tier-4-Rezepte (Quelle `Models/CraftingRecipe.cs`, alle GeneralContractor, RequiredWorkshopLevel 500,
Tier 4):

| OutputProductId | Rezept-Id | Inputs | Dauer |
|------------------|-----------|--------|-------|
| villa | r_villa | 5× luxury_furniture, 3× smart_home, 2× roof_structure, 1× artwork | 1800 s (30 min) |
| skyscraper | r_skyscraper | 5× skyscraper_frame, 3× bathroom_installation, 3× smart_home, 2× artwork | 2400 s (40 min) |
| imperium_hq | r_imperium_hq | je 2× aller 10 T3-Produkte (general_contract nur 1×; inkl. master_blueprint 2×, patent 2×, …) | 3600 s (60 min) |

`CraftingProduct.IsHeirloomEligible` markiert genau diese Tier-4-Items (BaseValue: villa 2.5 Mio.,
skyscraper 4.0 Mio., imperium_hq 5.0 Mio. laut CLAUDE.md — die konkreten BaseValue-Felder stehen in der
CraftingProduct-Definition; die genaue Zahl pro Produkt war in den gelesenen Zeilen nicht vollständig
sichtbar, BaseValue-Property existiert in `CraftingProduct`).

### 8.2 Heirloom beim Prestige (Run-Erbstücke)

Quelle: `Services/PrestigeService.cs` (`ResetProgress`)

- Slot-Cap: `GetEffectiveHeirloomSlots(IsPremium)` = `IsPremium ? 4 : 3`
  (`MaxHeirloomsPerRun = 3`, `MaxHeirloomsPerRunPremium = 4`).
- Auto-Füllung, wenn `HeirloomItems` leer: alle eligible Items aus `CraftingInventory` als Kandidaten
  (jedes Stück einzeln, mehrfach möglich), nach `BaseValue` absteigend sortiert, bis Cap.
- Wenn `HeirloomItems` > Cap: auf Cap beschneiden.
- Erbstücke überleben den Reset: nur die in `HeirloomItems` gelisteten eligible Items bleiben im neuen
  `CraftingInventory` (alles andere verworfen). `ReservedInventory.Clear()` beim Prestige.
- Telemetrie `heirloom_chosen` pro übertragenes Item.
- **Einkommens-Bonus pro aktivem Run-Erbstück: +2 %** (`HeirloomBonusPerItem = 0.02m`).

### 8.3 Heirloom beim Ascension (permanente Erbstücke)

Quelle: `Services/AscensionService.cs` (`DoAscension`), `Models/AscensionData.cs`

- Beim Ascension wandern eligible Items aus `CraftingInventory` in
  `Ascension.PermanentHeirlooms` (nach BaseValue absteigend, jedes Stück einzeln).
- Hard-Cap: `MaxPermanentHeirlooms = 50`. Es werden nur die wertvollsten bis zum Cap übernommen.
- **Einkommens-Bonus pro permanentem Erbstück: +0.5 % forever** (`PermanentHeirloomBonusPerItem = 0.005m`).
- Max permanenter Heirloom-Income: 50 × 0.5 % = +25 %.

### 8.4 Income-Integration (GetTotalHeirloomBonus)

Quelle: `Services/IncomeCalculatorService.cs`

```
active    = HeirloomItems.Count × 0.02                       (Run-Erbstücke, +2 % je Stück)
permanent = Ascension.PermanentHeirlooms.Count × 0.005       (permanent, +0.5 % je Stück)
GetTotalHeirloomBonus = active + permanent

In CalculateGrossIncome (nach EternalMastery):
   if heirloomBonus > 0: grossIncome *= (1 + heirloomBonus)
```

---

## 9. Zusammenfassung der Schlüssel-Konstanten

Quelle: `Models/GameBalanceConstants.cs` (sofern nicht anders vermerkt)

| Konstante | Wert | Bedeutung |
|-----------|------|-----------|
| PP-Formel-Divisor | 100_000 | `floor(sqrt(CurrentRunMoney / 100_000))` |
| Bronze-Min-PP | 15 | (in PrestigeService) |
| Prestige-Pass-Bonus | ×1.5 (+50 %) | (in PrestigeService) |
| MaxPermanentMultiplier | 20.0× | (privates const in PrestigeService) |
| DiminishingReturnsPerTierPrestige | 0.2 | |
| BonusPpPerPerfectBlock / Cap | 1 / 5 | |
| BonusPpFullBranch | 2 | |
| BonusPpAllBuildingsMax | 1 | |
| BonusPpPerExtraLevel / Cap | 0.05 / 5 | |
| MaxRepeatableShopPurchases | 8 | |
| MaxActiveChallenges | 3 | (PrestigeChallengeExtensions) |
| CanAscend-Schwelle | LegendeCount ≥ 3 | (AscensionService) |
| AP min | 5 | (AscensionService) |
| AP-Skalierung | (int)sqrt(AscensionLevel) × 2 | (AscensionService) |
| Ascension-Perks gesamt-AP | 54 | (Summe CostsPerLevel) |
| EternalMasteryBonusPerPrestige | 0.005 | +0.5 % je Prestige |
| EternalMasteryBonusPer5Prestiges | 0.025 | +2.5 % je 5 |
| EternalMasteryBonusPer10Prestiges | 0.05 | +5 % je 10 |
| EternalMasterySoftCapThreshold | 50 | log10-Dämpfung darüber |
| HeirloomBonusPerItem | 0.02 | +2 % je Run-Erbstück |
| PermanentHeirloomBonusPerItem | 0.005 | +0.5 % je permanentem Erbstück |
| MaxHeirloomsPerRun / Premium | 3 / 4 | |
| MaxPermanentHeirlooms | 50 | |
| MinReputationToUnlock (Rep-Shop) | 60 | (IReputationShopService) |
| Reputation Start | 50 | (CustomerReputation, oder Ascension-Perk 65/80/100) |
| Workshop.MaxLevel (Rebirth-Trigger) | 1000 | (Workshop.cs) |

---

## 10. Offene/nicht aus den gelesenen Dateien belegbare Punkte

- **Rebirth-Stern-Boni** (IncomeBonus/UpgradeDiscount/ExtraWorkers pro Stern): liegen in
  `Workshop.cs`/`WorkshopFormulas.cs`, nicht in den zu lesenden Dateien → siehe Abschnitt 4.4
  (CLAUDE.md-Werte als Plan-Referenz, nicht code-verifiziert in dieser Extraktion). GS-Kosten dagegen
  sind code-verifiziert (Abschnitt 4.2: 50/125/250/200/400).
- **Showroom DailyReputationGain konkreter Wert** (0.5–2.5/Tag): in `Building.cs`/Building-Formeln,
  nicht in gelesenen Dateien (Abschnitt 6.5).
- **CraftingProduct.BaseValue konkrete Zahlen** der Tier-4-Items: BaseValue-Property existiert in
  `CraftingProduct`; die genauen Werte (villa/skyscraper/imperium_hq) standen nicht vollständig in den
  gelesenen Zeilen — CLAUDE.md nennt 2.5/4.0/5.0 Mio. (Abschnitt 8.1).
- **GuildMembership.ResearchPrestigePointBonus konkreter Prozentsatz**: Gilden-Forschung
  `guild_mastery_*` gibt einen PP-Bonus; der konkrete Wert liegt in `GuildResearch.cs`, nicht hier.

---

# 05 — Gilden-System komplett

Verbindliche Werte- und Mechanik-Spezifikation, extrahiert direkt aus dem Avalonia-Produktivcode
von HandwerkerImperium. Jeder Wert stammt 1:1 aus dem Quellcode. Wo etwas berechnet wird, ist die
Formel angegeben. Wo etwas im Code nicht gefunden wurde, steht "(nicht im Code gefunden)".

Code-Root: `HandwerkerImperium.Shared`. Die echten Gilden-Daten liegen in der Firebase Realtime
Database; lokal wird nur ein minimaler Cache (`GuildMembership`) im `GameState` persistiert.

---

## 1. Rollen, Mitglieder-Limits, Identität

Quelle: `Models/GuildEnums.cs`, `Services/GuildService.cs`, `Models/Firebase/FirebaseGuildData.cs`,
`Models/Firebase/FirebaseGuildMember.cs`

### Rollen (`GuildRole`-Enum)

Genau **drei** Rollen, KEIN Co-Leader:

| Enum-Wert | Firebase-String | Rechte (aus GuildService) |
|-----------|-----------------|---------------------------|
| `Member` | `"member"` | keine Sonderrechte |
| `Officer` | `"officer"` | darf einladen + Members kicken (nicht Officer/Leader) |
| `Leader` | `"leader"` | volle Admin-Rechte: befördern, degradieren, alle kicken, Führung übertragen |

- Im Code taucht zusätzlich der String `"founder"` nur im Stale-Filter auf (`IsStaleMember`:
  `Role is "founder" or "leader"` → wird nie als stale gefiltert). Es gibt aber keinen
  `Founder`-Enum-Wert und kein Code-Pfad setzt `"founder"`. Nur Legacy-Schutz.

### Mitglieder-Limits

Quelle: `GuildService.cs` Konstanten + `GetMaxMembers()`

- `BaseMaxGuildMembers = 20` (const).
- `GetMaxMembers()` = `20 + ResearchMaxMembersBonus + HallMaxMembersBonus` (gecachte Werte aus `GuildMembership`).
- `FirebaseGuildData.MaxMembers` default = 20; wird bei `CreateGuildAsync` auf `BaseMaxGuildMembers` (20) gesetzt.
  Forschungs-/Hall-Boni erweitern das Limit clientseitig über `GetMaxMembers()`, NICHT das Firebase-`MaxMembers`-Feld.
- Max-Member-Boni-Quellen:
  - Forschung Infrastruktur (`guild_expand_1/2/3`): +5 / +5 / +10 → max +20.
  - Halle `AssemblyHall`: +2 pro Level, MaxLevel 3 → max +6.
  - Theoretisches Maximum: 20 + 20 + 6 = **46**.

### Verlassen (KEIN Cooldown)

Quelle: `LeaveGuildAsync()`. Es gibt **keinen Verlassen-Cooldown** im Code. Verlassen ist jederzeit
möglich. Beim Verlassen:
1. Wenn eigene Rolle = Leader → automatischer Leader-Transfer (`TransferLeadershipOnLeaveAsync`):
   ältester Officer (nach `JoinedAt`), sonst ältestes Mitglied bekommt `"leader"`.
2. Eigenen Member-Eintrag löschen, `guild_boss_damage/{guildId}/{uid}` löschen.
3. `CountAndSyncMemberCountAsync`; wenn `newCount == 0` → komplette Gilde aufräumen (`CleanupDeletedGuildAsync`).
4. `player_guilds/{uid}` löschen, lokalen Cache leeren, `GuildResearchService.InvalidateCache()`.

### Spielername

Quelle: `GuildService.SetPlayerName`, `CreateGuildAsync`
- Längen-Cap 30 Zeichen, Mindestlänge 2 (bei Gilden-Namen). Spielername-Cap ebenfalls 30.
- Unicode-Control/Format-Zeichen entfernt + `ProfanityFilter.Clean` (6 Sprachen).
- Preferences-Key: `"guild_player_name"`.

---

## 2. Gilden-Einkommens-Bonus (Income, Cache, Offline)

Quelle: `Models/Guild.cs` (`GuildMembership`), `GuildService.GetIncomeBonus()`

- Formel Gilden-Level-Bonus: `IncomeBonus = Math.Min(0.20m, GuildLevel * 0.01m)`
  → **+1% pro Gilden-Level, gedeckelt bei +20%** (= ab Level 20).
- `GuildMembership.IncomeBonus` ist `[JsonIgnore]` (berechnet), wird also nicht persistiert, sondern
  aus dem persistierten `GuildLevel` abgeleitet. Das macht den Bonus offline-tauglich (GameLoop/OfflineProgress).
- `GuildDetailData.IncomeBonus` nutzt dieselbe Formel (`Math.Min(0.20m, Level * 0.01m)`).
- `GuildService.GetIncomeBonus()` gibt `membership?.IncomeBonus ?? 0m` zurück.

Gecachte Zusatz-Boni in `GuildMembership` (persistiert, von den jeweiligen Services aktualisiert):
Research-Effekte (14 Felder), Hall-Effekte (9 Felder), Mega-Projekt-Boni (3 Felder + Liste). Siehe Abschnitte 5, 6, 8.

Default-Werte `GuildMembership`: `GuildLevel = 1`, `GuildIcon = "ShieldHome"`, `GuildColor = "#D97706"`,
`GuildHallLevel = 1`, `LeagueId = "bronze"`.

---

## 3. Wochenziel (Weekly Goal, Level-Up, sqrt-Skalierung, Belohnung)

Quelle: `GuildService.cs` (`DefaultWeeklyGoal`, `CheckWeeklyResetAsync`, `ContributeAsync`)

- `DefaultWeeklyGoal = 500_000` (const, long). Auch `FirebaseGuildData.WeeklyGoal` default = 500_000.
- Wochenstart = Montag (UTC). `GetCurrentMonday()` / `GetCurrentMondayUtc()`: Sonntag zählt als
  letzter Tag der Vorwoche (Sonntag → Montag -6 Tage; sonst -(DayOfWeek-1)).

### Wochenreset-Logik (`CheckWeeklyResetAsync`)

Wird bei `RefreshGuildDetailsAsync` ausgelöst. Wenn `weekStartParsed < currentMonday`:
- Setze `weekStartUtc = currentMonday`, `weeklyProgress = 0`, eigener `contribution = 0`.
- Wenn `WeeklyProgress >= WeeklyGoal` (Ziel erreicht):
  - `level = Level + 1`
  - `totalWeeksCompleted += 1`
  - **Neues Wochenziel (sqrt-Skalierung):**
    `weeklyGoal = (long)(DefaultWeeklyGoal * (1.0 + Math.Sqrt(newLevel) * 0.2))`
    → also `500_000 * (1 + 0.2*sqrt(neuesLevel))`.
  - **GS-Belohnung:** `screwReward = Math.Min(50, 5 + guildData.Level * 2)` (verwendet das alte
    `Level` vor Inkrement). Duplikat-Schutz per Preferences-Key `guild_weekly_reward_{Montag:yyyy-MM-dd}`.

### Beitrag (`ContributeAsync`)

- Wöchentliches Spenden-Cap pro Spieler: **30% des Wochenziels** (`maxDonation = (long)(weeklyGoal * 0.30)`).
- Tracking-Key: `guild_weekly_donation_{Montag:yyyy-MM-dd}_{uid}` (Preferences).
- Betrag wird auf verbleibendes Cap begrenzt (`cappedAmount = Math.Min(amount, remaining)`).
- Geld via `TrySpendMoney`; Firebase-Update `weeklyProgress += contribution`; bei Fehler Rollback (`AddMoney`).
- Spieler-`contribution` + `playerLevel` werden im Member-Eintrag aktualisiert.
- Integritäts-Check `VerifyIntegrityForFirebase` (HMAC-Signatur) vor jedem Firebase-Write.

### Beispielwerte neue Wochenziele (berechnet, nicht hardcoded)

| Neues Level | Formel `500000*(1+0.2*sqrt(L))` | Wert (gerundet abwärts) |
|-------------|---------------------------------|--------------------------|
| 2 | 500000*(1+0.2*1.4142) | 641 421 |
| 5 | 500000*(1+0.2*2.2360) | 723 607 |
| 10 | 500000*(1+0.2*3.1623) | 816 228 |
| 20 | 500000*(1+0.2*4.4721) | 947 214 |

### Gilden-Erstellung

Quelle: `CreateGuildAsync`. GuildId-Format: `g_{utcNow:yyyyMMddHHmmss}_{uid[..min(6,len)]}`.
Initialwerte: `Level=1, MemberCount=1, WeeklyGoal=500000, WeeklyProgress=0,
WeekStartUtc=GetCurrentMonday("O"), MaxMembers=20, LeagueId="bronze", LeaguePoints=0, HallLevel=1`.
Ersteller wird als `role="leader"` eingetragen.

### Browse

`BrowseGuildsAsync`: max 50 Gilden (`orderBy="level"&limitToLast=50`), Gilden mit
`MemberCount >= MaxMembers` werden ausgeblendet, Sortierung Level DESC, dann MemberCount DESC.

---

## 4. Gilden-Bosse (6 Typen)

Quelle: `Models/GuildBoss.cs` (`GuildBossDefinition.GetAll()`, `GuildBossType`), `Services/GuildBossService.cs`

### Boss-Definitionen (alle 6, vollständig)

`HpPerLevel` = HP pro Boss-Level. `CalculateHp(level) = HpPerLevel * Math.Max(1, level)`.
Default-Multiplikatoren (wenn nicht angegeben) = 1.0. Default `DurationHours` = 48.

| BossType | NameKey / DescKey | Icon | HpPerLevel | DurationHours | Crafting× | Order× | MiniGame× | MoneyDonation× | Color |
|----------|-------------------|------|-----------:|--------------:|----------:|-------:|----------:|---------------:|-------|
| StoneGolem | GuildBoss_StoneGolem | Wall | 5 000 | 48 | 1.0 | 1.0 | 1.0 | 1.0 | #78716C |
| IronTitan | GuildBoss_IronTitan | ShieldSword | 7 500 | 48 | **2.0** | 1.0 | 1.0 | 1.0 | #475569 |
| MasterArchitect | GuildBoss_MasterArchitect | HardHat | 6 000 | 48 | 1.0 | **2.0** | 1.0 | 1.0 | #D97706 |
| RustDragon | GuildBoss_RustDragon | Fire | 8 000 | 48 | 1.0 | 1.0 | **2.0** | 1.0 | #DC2626 |
| ShadowTrader | GuildBoss_ShadowTrader | Ninja | 5 500 | 48 | 1.0 | 1.0 | 1.0 | **3.0** | #6D28D9 |
| ClockworkColossus | GuildBoss_ClockworkColossus | CogSync | 10 000 | **24** | **1.5** | **1.5** | **1.5** | **1.5** | #0E7490 |

### Schaden zufügen (`DealDamageAsync(damage, source)`)

Multiplikator nach `source.ToLowerInvariant()`:
- `"crafting"` → `CraftingDamageMultiplier`
- `"order"` / `"orders"` → `OrderDamageMultiplier`
- `"minigame"` / `"minigames"` → `MiniGameDamageMultiplier`
- `"donation"` / `"donations"` → `MoneyDonationDamageMultiplier`
- sonst → 1.0
- `effectiveDamage = (long)(damage * multiplier)`.
- Race-Condition-frei: jeder Spieler schreibt nur seinen Eintrag `guild_boss_damage/{guildId}/{uid}`
  (`TotalDamage += effectiveDamage`, `Hits++`, `LastHitAt`).
- Hall-Boni werden in DealDamage **nicht** angewendet (Kommentar nennt sie, Code wendet nur Boss-Typ-Multiplikator an).

### Boss-Spawn (`SpawnBossIfNeededAsync`)

- Boss-Typ rotiert wöchentlich: `weekNumber = ISO-Kalenderwoche (FirstFourDayWeek, Monday)`,
  `bossIndex = weekNumber % 6`.
- HP-Berechnung:
  - `baseBossHp = definition.CalculateHp(guildLevel)` mit `guildLevel = Math.Max(1, GuildLevel)`.
  - Mitgliederzahl aus Firebase `guilds/{guildId}.memberCount` (min 1).
  - `bossHp = (long)(baseBossHp * Math.Max(0.5, memberCount / 5.0))`
    → 1 Mitglied = 0.5×, 3 = 0.6×, 5 = 1.0×, 10 = 2.0×, 20 = 4.0× (lineare Skalierung ab 5/5).
    (Kommentar im Code nennt abweichende Beispielwerte "1=0.5x, 3=0.8x, 5=1.0x, 10=1.5x, 20=2.5x";
    die tatsächliche Formel ist `max(0.5, memberCount/5.0)` — die Formel ist verbindlich, nicht der Kommentar.)
- `ExpiresAt = now + DurationHours`. Status `"active"`.
- Read-after-Write-Verifikation gegen Race; erst bei Bestätigung alte Damage-Einträge löschen.
- `BossId` = `definition.BossType.ToString()` (z.B. `"StoneGolem"`).

### Boss-Status (`CheckBossStatusAsync`)

- Throttle: 30s pro guildId (`_lastBossCheck` + `_lastBossCheckGuildId`).
- HP-Aggregation client-seitig: `currentHp = Math.Max(0, BossHp - SUM(allDamage.TotalDamage))`.
- Bei `now >= ExpiresAt` → Status `"expired"`.
- Bei `totalDamage >= BossHp` → Status `"defeated"`, `currentHp=0`, Belohnungen verteilen.

### Boss-Belohnungen (`DistributeBossRewardsAsync`) — Rang-basiert

Konstanten: `MvpRewardGs = 30`, `Top3RewardGs = 20`, `ParticipantRewardGs = 10`.
- `ownRank = (Anzahl Spieler mit mehr Schaden) + 1`.
- Reward: Rang 1 → **30 GS** (MVP), Rang 2–3 → **20 GS**, sonst → **10 GS**.
- Nur Spieler mit `Damage > 0` bekommen etwas.
- MVP-Kommentar nennt "+ Cosmetic", im Code wird kein Cosmetic vergeben (nur GS).
- Duplikat-Schutz: Preferences-Key `guild_boss_reward_week_{guildId}`, Wert = Boss-`StartedAt`.

### Speedkill / MVP / Defeated (für Achievements)

- Speedkill < 24h und MVP/Defeated-Zähler werden **im GuildBossService nicht** inkrementiert. Die
  zugehörigen Achievements (`guild_ach_boss_*`, `guild_ach_mvp_*`, `guild_ach_speedkill_*`) haben im
  Code KEINE automatische Fortschritts-Aktualisierung (siehe Abschnitt 7). Sie werden laut Kommentar
  "direkt bei den entsprechenden Aktionen aktualisiert", aber der konkrete `UpdateProgressAsync`-Aufruf
  für Boss/MVP/Speedkill ist (nicht im Code gefunden).

### Firebase-Modell `FirebaseGuildBoss` (Pfad: `guild_bosses/{guildId}`)

`bossId`, `bossHp`, `currentHp`, `bossLevel` (default 1), `startedAt`, `expiresAt`, `status` (default `"active"`).
Schaden pro Spieler `GuildBossDamage` (Pfad `guild_bosses/{guildId}/{bossId}/damage/{playerId}` lt.
Kommentar; der Service nutzt jedoch `guild_boss_damage/{guildId}/{uid}` mit Feldern `totalDamage`, `hits`, `lastHitAt`).

---

## 5. Gilden-Hauptquartier (10 Gebäude)

Quelle: `Models/GuildHall.cs` (`GuildBuildingDefinition.GetAll()`, `GuildHallEffects`, `GuildBuildingId`),
`Services/GuildHallService.cs`. Firebase-Pfad: `guild_hall/{guildId}/buildings/{buildingId}`.

### Upgrade-Kosten-Formel (`GetUpgradeCost(targetLevel)`)

- `screws = (int)(10 * Math.Pow(2.0, targetLevel - 1))`
- `money = (long)(500_000 * Math.Pow(2.5, targetLevel - 1))` (Gildengeld aus der Gildenkasse)
- Beispiel-Kosten: Lv1 = 10 GS / 500 000; Lv2 = 20 GS / 1 250 000; Lv3 = 40 GS / 3 125 000;
  Lv4 = 80 GS / 7 812 500; Lv5 = 160 GS / 19 531 250.

### Upgrade-Timer-Dauern (`UpgradeDurations`, Stunden, Tier = `Min(aktuelles Level + 1, 5)` — auf 5 gedeckelt)

| Tier | Stunden |
|------|--------:|
| 1 | 1.0 |
| 2 | 2.0 |
| 3 | 4.0 |
| 4 | 8.0 |
| 5 | 12.0 |

### Alle 10 Gebäude (vollständig)

`EffectPerLevel` = Effekt pro Level. `UnlockHallLevel` = ab welchem Hallen-Level freigeschaltet.
Effekt-Typ-Zuordnung aus `GuildHallEffects.Calculate`.

| BuildingId | NameKey | Icon | EffectPerLevel | MaxLevel | UnlockHallLevel | Color | Effekt (Gesamt-Max) |
|------------|---------|------|---------------:|---------:|----------------:|-------|---------------------|
| Workshop | GuildBuilding_Workshop | Hammer | 0.02 (+2%) | 5 | 1 | #D97706 | CraftingSpeedBonus, max +10% |
| ResearchLab | GuildBuilding_ResearchLab | FlaskOutline | 0.05 (-5%) | 5 | 2 | #2196F3 | ResearchTimeReduction, max +25% |
| TradingPost | GuildBuilding_TradingPost | StorefrontOutline | 0.03 (+3%) | 5 | 3 | #4CAF50 | IncomeBonus, max +15% |
| Smithy | GuildBuilding_Smithy | Anvil | 0.02 (+2%) | 5 | 4 | #EA580C | OrderRewardBonus, max +10% |
| Watchtower | GuildBuilding_Watchtower | TowerFire | 0.05 (+5%) | 5 | 5 | #DC2626 | WarPointsBonus, max +25% |
| AssemblyHall | GuildBuilding_AssemblyHall | AccountGroup | 2 (+2 Mitgl.) | 3 | 6 | #0E7490 | MaxMembersBonus, max +6 |
| Treasury | GuildBuilding_Treasury | TreasureChest | 0.05 (+5%) | 3 | 7 | #FFD700 | WeeklyRewardBonus, max +15% |
| Fortress | GuildBuilding_Fortress | ShieldLock | 0.05 (+5%) | 3 | 8 | #475569 | DefenseBonus, max +15% |
| TrophyHall | GuildBuilding_TrophyHall | Trophy | 0 | 1 | 9 | #9C27B0 | kein numerischer Effekt (zeigt Achievements) |
| MasterThrone | GuildBuilding_MasterThrone | Crown | 0.05 (+5%) | 1 | 10 | #B91C1C | EverythingBonus, max +5% |

### Effekt-Berechnung (`GuildHallEffects.Calculate`)

Pro Gebäude: `clampedLevel = min(level, MaxLevel)`, `totalEffect = EffectPerLevel * clampedLevel`.
Zuordnung der 9 Effekt-Felder: Workshop→CraftingSpeedBonus, ResearchLab→ResearchTimeReduction,
TradingPost→IncomeBonus, Smithy→OrderRewardBonus, Watchtower→WarPointsBonus,
AssemblyHall→MaxMembersBonus (int), Treasury→WeeklyRewardBonus, Fortress→DefenseBonus,
MasterThrone→EverythingBonus, TrophyHall→ohne Effekt.

### Hall-Service-Verhalten

- Upgrade-Voraussetzungen: `_hallLevel >= UnlockHallLevel`, nicht Max-Level, kein laufendes Upgrade,
  genug GS (`CanAffordGoldenScrews`) + genug Geld (`CanAfford`).
- Kosten werden mit `TrySpendGoldenScrews` + `TrySpendMoney` abgezogen; Rollback bei Fehler nutzt
  `AddGoldenScrews(gsSpent, fromPurchase: true)` (verhindert Premium/Prestige-GS-Boni auf Refund).
- Hallen-Level kommt aus `guilds/{guildId}/hallLevel` (separat von Gebäude-States). Wie das Hallen-Level
  selbst erhöht wird, ist im GuildHallService **nicht** implementiert (kein Code, der `hallLevel` schreibt) → (nicht im Code gefunden).
- Effekte werden auf `GuildMembership` gecacht (`ApplyHallEffects`), für Offline-Nutzung.

---

## 6. Gilden-Forschung (18 Nodes, 6 Kategorien)

Quelle: `Models/GuildResearch.cs` (`GuildResearchDefinition.GetAll()`, `GuildResearchEffects`,
`GuildResearchCategory`, `GuildResearchEffectType`), `Services/GuildResearchService.cs`.
Firebase-Pfad: `guild_research/{guildId}/{researchId}`.

### Alle 18 Forschungs-Nodes (vollständig)

`Cost` in EUR. `EffectValue` je nach Typ Prozent (decimal) oder absolute Zahl (int-Cast).

| Id | Kategorie | Order | Cost | EffectType | EffectValue | Icon |
|----|-----------|------:|-----:|------------|-------------|------|
| guild_expand_1 | Infrastructure | 1 | 50 000 000 | MaxMembers | 5 | AccountMultiplePlus |
| guild_expand_2 | Infrastructure | 2 | 500 000 000 | MaxMembers | 5 | AccountMultiplePlus |
| guild_expand_3 | Infrastructure | 3 | 5 000 000 000 | MaxMembers | 10 | AccountMultiplePlus |
| guild_income_1 | Economy | 1 | 10 000 000 | IncomeBonus | 0.05 (+5%) | Handshake |
| guild_income_2 | Economy | 2 | 100 000 000 | CostReduction | 0.10 (-10%) | CartArrowDown |
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
| guild_workforce_3 | Workforce | 3 | 5 000 000 000 | FatigueReduction | 0.20 (-20%) | ShieldAccount |
| guild_mastery_1 | Mastery | 1 | 500 000 000 | ResearchSpeedBonus | 0.20 (+20%) | FlashOutline |
| guild_mastery_2 | Mastery | 2 | 7 500 000 000 | PrestigePointBonus | 0.10 (+10%) | Crown |

### Kategorie-Farben (`GetCategoryColor`)

Infrastructure #D97706, Economy #4CAF50, Knowledge #2196F3, Logistics #9C27B0,
Workforce #0E7490, Mastery #FFD700, Default #888888.

### Aggregierte Effekte (`GuildResearchEffects`, alle abgeschlossen)

- IncomeBonus +20% (income_1 5% + income_4 15%)
- CostReduction -10% (income_2)
- RewardBonus +30% (income_3 10% + logistics_3 20%)
- XpBonus +10% (knowledge_1)
- EfficiencyBonus +5% (knowledge_2)
- MiniGameBonus +15% (knowledge_3)
- MaxMembersBonus +20 (expand_1/2/3: 5+5+10)
- OrderSlotBonus +1 (logistics_1)
- OrderQualityBonus +15% (logistics_2)
- WorkerSlotBonus +1 pro Workshop (workforce_1)
- TrainingSpeedBonus +25% (workforce_2)
- FatigueReduction -20% (workforce_3)
- ResearchSpeedBonus +20% (mastery_1)
- PrestigePointBonus +10% (mastery_2)

### Forschungsdauer (`GetResearchDurationHours(cost)`)

- `cost < 100 000 000` → 1.0 h
- `100 000 000 <= cost <= 2 000 000 000` → 4.0 h
- `cost > 2 000 000 000` → 12.0 h
- ResearchSpeedBonus (mastery_1) verkürzt die Dauer: `durH *= (1 - ResearchSpeedBonus)`.

### Beitrags-Mechanik (`ContributeToResearchAsync`)

- Mitglieder-basierter Kosten-Rabatt (`GetMemberCountCostMultiplierAsync`):
  `memberCount < 3` → ×0.50 (-50%); `< 5` → ×0.75 (-25%); sonst ×1.0.
- `scaledCost = (long)(definition.Cost * costMultiplier)`.
- Beitrag begrenzt auf `remaining = scaledCost - Progress`.
- Geld via `TrySpendMoney`, bei Firebase-Fehler Rollback `AddMoney`.
- Bei `Progress >= scaledCost` → `ResearchStartedAt = now` (Timer startet), `Completed` erst wenn Timer abläuft.
- „Erste nicht-abgeschlossene Forschung pro Kategorie = aktiv" (sequenzielle Freischaltung pro Kategorie).
- Effekt-Cache auf `GuildMembership` gecacht (`ApplyResearchEffects`).
- `GuildResearchState` (Firebase): `progress`, `completed`, `completedAt`, `researchStartedAt`.

---

## 7. Gilden-Achievements (30 = 10 Typen × 3 Tiers)

Quelle: `Models/GuildAchievement.cs` (`GuildAchievementDefinition.GetAll()`, `GuildAchievementCategory`,
`AchievementTier`), `Services/GuildAchievementService.cs`. Firebase-Pfad: `guild_achievements/{guildId}/{achievementId}`.

### Tiers (`AchievementTier`)

Bronze (#CD7F32), Silver (#C0C0C0), Gold (#FFD700). GS-Belohnung im Code IMMER: Bronze 5, Silver 25, Gold 50.
(Enum-Kommentar nennt "Bronze 5 / Silver 15+Banner / Gold 30+Emblem" — der Code vergibt jedoch 5/25/50
und KEINE Kosmetik. Die konkreten `GoldenScrewReward`-Werte und `CosmeticReward = ""` aus den Definitionen sind verbindlich.)

### Kategorie-Farben (`GetCategoryColor`)

StrongerTogether #4CAF50, WarHeroes #DC2626, DragonSlayers #D97706, Builders #2196F3, Default #888888.

### Alle 30 Achievements (vollständig)

Alle haben `CosmeticReward = ""` (leer). GS = 5 (Bronze) / 25 (Silver) / 50 (Gold).

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

Hinweis: Die Tabelle listet bewusst alle 33 Zeilen-Einträge der `_allDefinitions` (10 Typen × 3 Tiers
ergibt 30; die Liste enthält jedoch faktisch 11 Typen × 3 = 33 Einträge: money, research, members,
wars, seasons, league, boss, mvp, speedkill, maxbuilding, hall). **Der Code/Kommentar spricht von "30
Achievements (10 Typen × 3 Tiers)", die tatsächliche `_allDefinitions`-Liste enthält aber 33 Einträge
(11 Typen).** Verbindlich ist die obige vollständige Liste aus dem Code.

### Auto-Tracking (`CheckAllAchievementsAsync`, periodisch alle 300s/Offset 250)

Nur diese Achievements werden automatisch aktualisiert (3 parallele Firebase-Reads):
- money_* ← `guilds/{id}.weeklyProgress` (totalContrib)
- research_* ← Anzahl `completed`-Forschungen
- members_* ← `guilds/{id}.memberCount`
- maxbuilding_* ← Anzahl Gebäude mit `Level >= MaxLevel`
- hall_* ← `guilds/{id}.hallLevel`
- **wars/seasons/league/boss/mvp/speedkill werden hier NICHT aktualisiert** (Kommentar: "direkt bei
  den entsprechenden Aktionen" — der konkrete Aufruf ist nicht im Code gefunden).

### Update-Logik (`UpdateProgressCoreAsync`)

- Fortschritt nur erhöhen (`if progress <= state.Progress return`).
- Bei `Progress >= Target` → `Completed=true`, `CompletedAt`, GS-Belohnung NUR nach erfolgreichem Firebase-Write.
- Event `AchievementCompleted(GuildAchievementDisplay)` für UI-Celebration.

---

## 8. Mega-Projekte (2 Templates)

Quelle: `Models/Firebase/GuildMegaProject.cs` (`GuildMegaProjectTemplates`, `GuildMegaProjectType`,
`GuildMegaProjectReward`), `Services/GuildMegaProjectService.cs`. Firebase-Pfad: `guilds/{guildId}/megaProjects/active`.

### Templates und Material-Anforderungen

| Type (Enum) | Material-Anforderungen (`GetRequirements`) | Belohnung (`GetReward`) | NameKey |
|-------------|--------------------------------------------|--------------------------|---------|
| Cathedral (0) | luxury_furniture 50, roof_structure 40, artwork 30, smart_home 20, villa 1 | CraftingSpeed +0.05, AutoSellPrice +0.10, +3 Lager-Slots | GuildMegaProjectCathedral |
| Headquarters (1) | skyscraper_frame 80, smart_home 60, bathroom_installation 50, master_blueprint 30, masterpiece_fittings 30, villa 2, skyscraper 1 | CraftingSpeed +0.10, AutoSellPrice +0.20, +5 Lager-Slots | GuildMegaProjectHeadquarters |

Hinweis: Im Modell heißt das HQ-Material `bathroom_installation` (App-CLAUDE.md verkürzt auf `bathroom`).
Verbindlich ist der Code-Wert `bathroom_installation`.

### Konstanten & Lifecycle

- `AbandonmentSunsetDays = 30`: Projekte älter als 30 Tage werden für Spenden geblockt (`ageDays > 30` → return).
- Start (`StartProjectAsync`): nur wenn kein aktives/unfertiges Projekt; `ProjectId = Guid("N")`,
  `CreatedAt = UtcNow`, HMAC über `ProjectId|(int)Type|CreatedAt:O` mit Salt `"guild-mega-project-v1"`.
- Spende (`DonateAsync` → `DonateCoreAsync`, SemaphoreSlim-serialisiert):
  - Verfügbarkeit via `WarehouseService.GetAvailable` (abzgl. Reservierungen).
  - `actualCount = min(count, required - alreadyDonated)`.
  - Inventar atomar reduzieren; `donationValue = CraftingService.GetSellPrice(productId) * actualCount`.
  - Atomarer PATCH (`UpdateAsync`) nur Subpfade `contributions/{productId}`, `donations/{playerId}/...`.
  - Bei Komplettierung (`IsAllRequirementsMet`) → `completedAt` setzen + `ClaimRewardInternal`.
  - Rollback (Material wieder einlagern) bei PATCH-Fehler.
- Belohnung (`ClaimRewardInternal`/`TryClaimRewardAsync`): permanenter Bonus pro Spieler einmalig,
  Idempotenz via `state.ClaimedGuildProjectIds`. Addiert auf `GuildMembership`:
  `MegaProjectCraftingSpeedBonus`, `MegaProjectAutoSellPriceBonus`, `MegaProjectBonusWarehouseSlots`;
  `CompletedMegaProjectTypes` (List<int>) ergänzt.
- Top-Spender-Leaderboard: `GuildMegaProjectDonation` mit `playerName`, `totalValue` (decimal), `itemCount`, `lastDonatedAt`.
- Telemetrie-Event `guild_mega_project_donation` (project_id, project_type, item_id, count, donation_value).

### Boni-Integration (laut App-CLAUDE.md)

- CraftingSpeedBonus → `CraftingService.StartCrafting`.
- AutoSellPriceBonus → Marktpreis im Overflow-Auto-Sell.
- BonusWarehouseSlots → `WarehouseService.EffectiveSlotCount`.

---

## 9. Kriegssaison (Liga, Phasen, Scoring, Belohnungen)

Quelle: `Services/GuildWarSeasonService.cs`, `Models/GuildWarSeason.cs`, `Models/Firebase/GuildWar.cs`,
`Models/GuildEnums.cs` (`GuildLeague`, `WarPhase`), `Models/GuildWarDisplayData.cs`.

### Ligen (`GuildLeague`)

Bronze, Silver, Gold, Diamond. Firebase-String = `ToString().ToLowerInvariant()`.
Achievement-Target-Mapping: Silver=1, Gold=2, Diamond=3.

### Saison-Struktur

- **Saison-Dauer: 4 Wochen** (28 Tage). `EndDate = seasonStart + 28 Tage`.
- Saison-ID: `s_{ISO-Jahr}_{seasonNumber:D2}`, `seasonNumber = (isoWeek - 1) / 4 + 1` (4-Wochen-Block).
- Aktuelle Woche in Saison: `((isoWeek - 1) % 4) + 1` (1–4).
- `GuildWarSeasonData`: `startDate`, `endDate`, `status` (`"active"`/`"completed"`/`"upcoming"`, default `"active"`), `week` (default 1).

### Phasen (`WarPhase`, wochentag-basiert UTC)

`GetCurrentPhase()`:
- Mo/Di/Mi → **Attack**
- Do/Fr → **Defense**
- Sa/So → **Evaluation**
- (Completed = Krieg abgeschlossen, wird über Status gesetzt)

`GetPhaseEndTime()` (Tage bis Phasenende, jeweils 00:00 UTC):
Mo→+3 (Do), Di→+2, Mi→+1; Do→+2 (Sa), Fr→+1; Sa→+2 (Mo nächste Woche), So→+1.

### Matchmaking (`FindOrCreateWarAsync`)

- Level-Matching: erst Toleranz ±3 (`LevelMatchTolerance`), dann ±5 (`LevelMatchToleranceExtended`).
- Sucht offene Wars (`orderBy="status"&equalTo="active"&limitToFirst=20`) mit `guildBId == "waiting"`.
- Optimistic Locking: PATCH guildB-Felder, 200ms Delay, Verify-after-Write. Verloren → eigenen War erstellen.
- Eigener War: `guildBId="waiting"`, Phase aus `GetCurrentPhase()`, WarId = `{seasonId}_w{week}_{ownGuildId}`.
- Kein Match → Bye-Week (`IsByeWeek` wenn `WarId` leer).

### Scoring (`ContributeScoreAsync(points, source)`)

Reihenfolge der Multiplikatoren:
1. Keine Punkte in Phase Evaluation/Completed.
2. **Defense-Phase: Punkte halbiert** (`points * 0.5`).
3. **Aufhol-Multiplikator ×1.5** wenn `freshOpponentScore > freshOwnScore`.
4. **Hall-WarPoints-Bonus**: `effectivePoints * (1 + HallWarPointsBonus)`.
5. Score zugewiesen: Attack-Phase → `AttackScore +=`, sonst → `DefenseScore +=`.
- Race-frei: nur eigener Score-Pfad `guild_war_scores/{warId}/{guildId}/{uid}`.
- Gilden-Gesamtscore = Summe aller Member-`TotalScore` (`AttackScore + DefenseScore`).
- War-Log-Eintrag per `PushAsync` (Type `"score"`).

### Bonus-Missionen (`GetBonusMissionsAsync`, lokal/Preferences, wöchentlich)

3 Missionen, wöchentlicher Reset (`gws_bonusmission_{Montag}_{type}`):

| Id | NameKey | Target | BonusPoints |
|----|---------|-------:|------------:|
| orders_5 | GuildWarBonusOrders | 5 | 200 |
| minigames_3 | GuildWarBonusMiniGames | 3 | 150 |
| deposit_50k | GuildWarBonusDeposit | 50 000 | 100 |

Bei Abschluss → `ContributeScoreAsync(bonusPoints, "bonus_{type}")`, claimed-Flag `..._claimed`.

### Belohnungen (`DistributeWarRewardsAsync`) — pro Krieg

GS-Konstanten: `WinRewardGs=20`, `DrawRewardGs=10`, `LossRewardGs=5`, `MvpBonusGs=5`, `AllBonusMissionGs=3`.
Liga-Punkte: `WinLeaguePoints=3`, `DrawLeaguePoints=1` (Loss=0).

| Ergebnis | GS | Liga-Punkte |
|----------|---:|------------:|
| Sieg (ownScore > opponentScore) | 20 | 3 |
| Unentschieden (==) | 10 | 1 |
| Niederlage (<) | 5 | 0 |

- **MVP-Bonus +5 GS** wenn eigener `TotalScore >= mvpScore` (und > 0).
- **+3 GS** wenn alle 3 Bonus-Missionen abgeschlossen.
- Liga-Eintrag (`GuildLeagueEntry`): `Points += leaguePoints`, `Wins++`/`Losses++`.
- War → `status="completed"`, `phase="completed"`. Duplikat-Schutz `gws_war_reward_{warId}`.

### Saison-End-Belohnungen (`DistributeSeasonRewards`) — Liga-abhängig

| Liga | GS |
|------|---:|
| Diamond | 100 |
| Gold | 50 |
| Silver | 25 |
| Bronze | 10 |

Duplikat-Schutz `gws_season_reward_{seasonId}`.

### Liga-Auf-/Abstieg (`ProcessLeaguePromotionAsync`, Perzentil-basiert)

Sortierung Points DESC, Wins DESC, GuildId ASC. `ownPercentile = (ownIndex+1)/totalGuilds`.

| Liga | Aufstieg | Abstieg |
|------|----------|---------|
| Bronze | Top 30% (`<=0.30`) → Silver | — |
| Silver | Top 25% (`<=0.25`) → Gold | Bottom 25% (`>0.75`) → Bronze |
| Gold | Top 20% (`<=0.20`) → Diamond | Bottom 30% (`>0.70`) → Silver |
| Diamond | — | Bottom 30% (`>0.70`) → Gold |

Neue Liga → Firebase `guilds/{guildId}/leagueId` + lokaler `GuildMembership.LeagueId`-Cache.

### Saison-Ende (`CheckSeasonEndAsync`)

Auslöser: `EndDate` überschritten ODER (Woche >= 4 UND Phase = Evaluation). Dann: letzte War-Belohnungen,
Liga-Auf/Abstieg, Saison-`status="completed"`, Saison-Belohnungen.

### Firebase-Modelle (Krieg)

- `GuildWar` (Pfad lt. Service `guild_wars/{warId}`): guildAId, guildBId, guildAName, guildBName,
  scoreA, scoreB, startDate, endDate, status (default `"active"`), guildALevel, guildBLevel,
  phase (default `"attack"`), phaseEndsAt.
- `GuildWarPlayerScore` (`guild_war_scores/{warId}/{guildId}/{playerId}`): attackScore, defenseScore,
  updatedAt; `TotalScore = AttackScore + DefenseScore` ([JsonIgnore]).
- `GuildWarScore`: score, updatedAt (alternatives Modell).
- `GuildLeagueEntry` (`guild_war_seasons/{seasonId}/leagues/{leagueId}/{guildId}`): points, wins, losses, rank.
- `GuildWarLogEntry` (`guild_war_log/{warId}` via Push; Kommentar nennt `guild_wars/{warId}/log`):
  type (`"score"`/`"phase_change"`/`"bonus"`/`"result"`), guildId, playerName, points, message, timestamp.
- `WarBonusMission`: id, nameKey, descKey, target, progress, bonusPoints; `IsCompleted = Progress >= Target`.

---

## 10. Co-op-Auftrag

Quelle: `Services/GuildCoopOrderService.cs`, `Models/Firebase/CoopOrderState.cs`.
Firebase-Pfad: `guilds/{guildId}/coopOrders/{orderId}`.

WICHTIG: Der reale Code implementiert Co-op als **1:1-Einladungs-Auftrag zwischen zwei Mitgliedern**
(Ersteller + eingeladener Spieler), KEIN gildenweiter "1 pro Gilde, 5-15 Tasks, Beitrag-basiert"-Auftrag.
Die in der Aufgabenstellung genannte Beschreibung "1 pro Gilde, 5-15 Tasks, Beitrag-basiert" findet sich
**nicht** im Code — verbindlich ist die folgende 2-Spieler-Mechanik.

### Lebenszyklus & Werte

- `CreateInviteAsync(invitedPlayerId, miniGameType)`:
  - `OrderId = Guid("N")`, Status `Pending`, `ExpiresAt = UtcNow + 5 min` (Annahme-Frist).
  - `BaseReward = 100_000m` EUR, `RewardSplit = 0.5` (50/50).
  - MiniGame-Typ kommt vom Aufrufer (Bugfix: früher fest Sawing).
  - HMAC über `OrderId|CreatedBy|InvitedPlayer|BaseReward|MiniGameType` mit Salt `"coop-order-v1"`.
- `AcceptAsync`: nur eingeladener Spieler, nur Pending, vor Ablauf → Status `Active`, `ExpiresAt = UtcNow + 3 min` (MiniGame-Fenster).
- `DeclineAsync`: Status `Expired`.
- `SubmitScoreAsync(orderId, score, isPlayer1)`:
  - `score = Math.Clamp(score, 0, 100)`.
  - PATCH eigenes Feld (`player1Score`/`player2Score`).
  - Wenn beide Scores gesetzt und Status Active → PATCH Status `Completed` (idempotent).
- Reward (`TryClaimCompletedRewardAsync`, idempotent):
  - Lokaler Check `GameState.ClaimedCoopOrderIds` + Server-Write-once-Marker `claimedBy/{playerId}`.
  - **Bonus +25%** wenn beide Scores `>= 95` (`bothPerfect`): `multiplier = 1.25`, sonst 1.0.
  - `myShare = BaseReward * RewardSplit * multiplier` → bei Perfect 100 000 × 0.5 × 1.25 = **62 500** pro Spieler, sonst 50 000.
  - HMAC-Tampering → Status `Expired`.

### `CoopOrderStatus`-Enum

Pending, Active, Completed, Expired.

### `CoopOrderState`-Felder (Firebase)

orderId, createdBy, invitedPlayer, status, expiresAt (DateTime), miniGameType, player1Score (int?),
player2Score (int?), rewardSplit (default 0.5), baseReward (decimal), hmac (string?).

---

## 11. Gilden-Tipps (GuildTips)

Quelle: `Services/GuildTipService.cs`, `ViewModels/GuildViewModel.cs`, RESX `AppStrings.*.resx`.

- Rein lokal (Preferences). Key-Prefix `"guild_tip_seen_"`. RESX-Key-Muster `GuildTip_{context}`.
- API: `GetTipForContext(context)` (null wenn schon gesehen), `MarkTipSeen(context)`, `HasUnseenTip(context)`.
- **Klassen-Kommentar nennt 8 Kontexte:** `joined, research, war, boss, hall, officer, season_end, chat`.
- **Tatsächlich im Code verwendet wird NUR ein Kontext:** `"guild_hub"`
  (`GuildViewModel.cs`: `GetTipForContext("guild_hub")` + `MarkTipSeen("guild_hub")`).
- **Tatsächlich in den RESX vorhanden ist NUR EIN Key:** `GuildTip_guild_hub`
  (DE-Wert: „Willkommen in deiner Gilde! Hier siehst du das Wochenziel und die Mitglieder."), in allen 6 Sprachen.
- Die 8 im Kommentar genannten Kontext-Keys (`GuildTip_joined`, `GuildTip_research`, `GuildTip_war`,
  `GuildTip_boss`, `GuildTip_hall`, `GuildTip_officer`, `GuildTip_season_end`, `GuildTip_chat`) sind
  **nicht** als RESX-Einträge und **nicht** als Aufruf-Stellen im Code gefunden. Für die 1:1-Neuentwicklung
  verbindlich: nur `guild_hub` ist real implementiert; die 8 Kontexte sind vorgesehene, aber unfertige Stubs.

---

## 12. Chat-Limits

Quelle: `Services/GuildChatService.cs`, `Models/Firebase/ChatMessage.cs`. Firebase-Pfad: `guild_chat/{guildId}/messages`.

- `MaxMessageLength = 200` Zeichen (überlange Texte werden auf 200 gekürzt).
- `MaxMessages = 50` (letzte 50 Nachrichten, `orderBy="timestamp"&limitToLast=50`).
- `MessageCooldown = 5 Sekunden` (Spam-Schutz, `CanSendMessage`).
- Control-Zeichen entfernt (außer `\n`), `ProfanityFilter.Clean` (6 Sprachen).
- `ChatMessage`: uid, name, text, timestamp. Nachrichten via `PushAsync`.

---

## 13. Einladungs-System (Invite-Codes / Browser / Inbox)

Quelle: `Services/GuildInviteService.cs`. Beitritte delegieren an `GuildService.JoinGuildAsync`.

- **Invite-Codes: 6-stellig**, Zeichensatz `ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789` (36 Zeichen),
  kryptografisch via `RandomNumberGenerator.GetInt32`. Max 5 Kollisions-Versuche, sonst Fehler.
  Bidirektionales Mapping: `guild_invite_codes/{guildId}` ↔ `invite_code_to_guild/{code}`.
- `JoinByInviteCodeAsync`: Code muss exakt 6 Zeichen sein; Lookup über `code.ToUpperInvariant()`.
- Verfügbare Spieler (`available_players/{uid}`): `AvailablePlayerInfo` (Name, Level, LastActive),
  Browser max 50 (nach LastActive sortiert).
- Einladungs-Inbox (`player_invites/{uid}/{guildId}` → `GuildInvitation`): **max 10 Einladungen** pro
  Spieler (älteste wird gelöscht). Felder: guildName, guildIcon, guildColor, guildLevel, memberCount,
  invitedBy, invitedAt.
- Stale-Member-Schwelle: **30 Tage** Inaktivität (`IsStaleMember`, gefiltert nur aus Anzeige; eigener Spieler nie gefiltert).

---

## 14. Firebase-Pfad-Schema (alle verwendeten Pfade)

| Pfad | Inhalt | Quelle |
|------|--------|--------|
| `player_guilds/{playerId}` | GuildId-Schnell-Lookup (string) | GuildService |
| `guilds/{guildId}` | `FirebaseGuildData` | GuildService |
| `guilds/{guildId}/hallLevel` | int (separat geladen) | GuildHallService |
| `guilds/{guildId}/leagueId` | string | GuildWarSeasonService |
| `guild_members/{guildId}/{uid}` | `FirebaseGuildMember` | GuildService |
| `guild_members/{guildId}/{uid}/role` | string | GuildService.GetMemberRoleAsync |
| `available_players/{uid}` | `AvailablePlayerInfo` | GuildInviteService / GuildService |
| `player_invites/{uid}/{guildId}` | `GuildInvitation` | GuildInviteService |
| `guild_invite_codes/{guildId}` | string (Code) | GuildInviteService |
| `invite_code_to_guild/{code}` | string (GuildId) | GuildInviteService |
| `guild_research/{guildId}/{researchId}` | `GuildResearchState` | GuildResearchService |
| `guild_hall/{guildId}/buildings/{buildingId}` | `GuildBuildingState` | GuildHallService |
| `guild_bosses/{guildId}` | `FirebaseGuildBoss` | GuildBossService |
| `guild_boss_damage/{guildId}/{uid}` | `GuildBossDamage` | GuildBossService |
| `guild_achievements/{guildId}/{achievementId}` | `GuildAchievementState` | GuildAchievementService |
| `guild_chat/{guildId}/messages/{messageId}` | `ChatMessage` (Push) | GuildChatService |
| `guild_war_seasons/{seasonId}` | `GuildWarSeasonData` | GuildWarSeasonService |
| `guild_war_seasons/{seasonId}/leagues/{leagueId}/{guildId}` | `GuildLeagueEntry` | GuildWarSeasonService |
| `guild_wars/{warId}` | `GuildWar` | GuildWarSeasonService |
| `guild_war_scores/{warId}/{guildId}/{uid}` | `GuildWarPlayerScore` | GuildWarSeasonService |
| `guild_war_log/{warId}/{entryId}` | `GuildWarLogEntry` (Push) | GuildWarSeasonService |
| `guilds/{guildId}/coopOrders/{orderId}` | `CoopOrderState` | GuildCoopOrderService |
| `guilds/{guildId}/coopOrders/{orderId}/claimedBy/{playerId}` | bool (Write-once-Claim) | GuildCoopOrderService |
| `guilds/{guildId}/megaProjects/active` | `GuildMegaProject` | GuildMegaProjectService |
| `auth_to_player/{uid}` | PlayerId (Mapping, lt. App-CLAUDE.md) | FirebaseService |

Identität: **PlayerId** (GUID) ist stabile Spieler-Identität (überlebt Account-/Geräte-Wechsel).
Alle Pfade verwenden PlayerId, nicht Firebase-`Uid`. Migration alt→neu via `MigrateFromUidToPlayerIdAsync`.

---

## 15. HMAC-signierte Felder

Quelle: `GuildCoopOrderService.ComputeHmac`, `GuildMegaProjectService.ComputeHmac`,
`GuildService.VerifyIntegrityForFirebase`. HMAC via `IGameIntegrityService.ComputeStringHmac`.

| Kontext | Salt | Signierte (stabile) Felder | NICHT signiert |
|---------|------|----------------------------|----------------|
| Co-op-Auftrag | `coop-order-v1` | OrderId, CreatedBy, InvitedPlayer, BaseReward, MiniGameType | Score, Status, ExpiresAt (inkrementell via PATCH) |
| Mega-Projekt | `guild-mega-project-v1` | ProjectId, (int)Type, CreatedAt (`:O`) | Contributions, Donations, CompletedAt |
| GameState (vor Gilden-Writes) | (GameIntegrityService) | gesamter GameState-Signatur-Check (`VerifySignature`) | — |

Begründung im Code: nur stabile Identitäts-Felder im HMAC, weil veränderliche Felder per atomarem
PATCH aktualisiert werden (Race-Condition-Fix). Score-Wertebereiche werden zusätzlich über Firebase-Rules (`validate`) begrenzt.

---

## 16. GuildTick-Offsets (periodische Checks)

Quelle: `Services/GuildTickService.cs`. `ProcessTick(state, tickCount)` (1 Tick = 1 Sekunde im GameLoop).
Nur aktiv wenn `state.GuildMembership?.GuildId != null`.

| Check | Intervall (s) | Offset (tickCount % Intervall ==) | Aktion |
|-------|--------------:|----------------------------------:|--------|
| Boss-Status + Spawn (sequenziell) | 60 | 20 | `CheckBossStatusAsync` → `SpawnBossIfNeededAsync` |
| Hall Upgrade-Completion | 60 | 40 | `CheckUpgradeCompletionAsync` |
| Achievements prüfen | 300 | 250 | `CheckAllAchievementsAsync` |
| War-Saison (Phase + Saisonende, sequenziell) | 300 | 260 | `CheckPhaseTransitionAsync` → `CheckSeasonEndAsync` |
| Auktion Refresh + Spawn (Master-Client) | 300 | 90 | `RefreshAuctionAsync` → `SpawnAuctionIfMasterAsync` |
| NPC-Bot-Tick (Auktion) | 5 | 1 | `RunNpcBotTickAsync` |

Alle Aufrufe per `FireAndForget`. Boss-internes Throttle zusätzlich 30s pro guildId.

---

## 17. Facade & Service-Architektur

Quelle: `Services/GuildFacade.cs`.

`IGuildFacade` bündelt **9** Subsystem-Services (Klassen-Kommentar sagt "7", konstruiert aber 9):
Guild, Invite, Research, Chat, WarSeason, Boss, Hall, Tip, Achievement. Reiner Pass-Through-Container,
kein State. `GuildCoopOrderService` und `GuildMegaProjectService` sind NICHT Teil des Facades (separat injiziert).

Thread-Safety: jeder Service mit Firebase-Mutationen nutzt `SemaphoreSlim` (Timeout 15s, Chat 10s).
Alle Services sind Singletons; die mit Lock implementieren `IDisposable`.

---

## 18. Wichtige Code-vs-Plan-Abweichungen (für die Plan-Korrektur)

- Co-op ist **2-Spieler-Einladung** (BaseReward 100 000, 50/50, +25% bei beidseitig Score≥95), NICHT
  "1 pro Gilde, 5-15 Tasks, beitrag-basiert".
- GuildTips: real nur **1** Kontext (`guild_hub`) implementiert, nicht 8.
- Achievement-Definitionen-Liste enthält **33** Einträge (11 Typen × 3 Tiers), Doku/Kommentar sagt "30 (10×3)".
- Achievement-GS = **5/25/50** (Code), nicht 5/15/30; KEINE Kosmetik-Belohnung im Code.
- Boss-MVP gibt nur GS, kein Cosmetic.
- Boss-HP-Member-Skalierung = `max(0.5, memberCount/5.0)` (linear), nicht die im Kommentar genannten Stützpunkte.
- Saison-Dauer im Code = **4 Wochen / 28 Tage** (BattlePass-Saison ist separat 30 Tage — nicht verwechseln).
- Kein Co-Leader, kein Verlassen-Cooldown.

---

# 06 — Live-Ops & Monetarisierung

Verbindliche Werte- und Mechanik-Spezifikation, exakt aus dem Avalonia-Original-Code extrahiert.
Jeder Wert stammt aus dem unten genannten Quell-File. Abgeleitete Werte sind als Formel angegeben.
"(nicht im Code gefunden)" markiert Werte, die in den gelesenen Dateien fehlen.

Gelesene Quell-Dateien:
- `Services/BattlePassService.cs`, `Services/LiveEventService.cs`, `Services/LiveEventScoreTracker.cs`,
  `Services/SeasonalEventService.cs`, `Services/DailyRewardService.cs`, `Services/DailyChallengeService.cs`,
  `Services/DailyBundleService.cs`, `Services/WeeklyMissionService.cs`, `Services/TournamentService.cs`,
  `Services/VipService.cs`, `Services/LuckySpinService.cs`, `Services/ReferralService.cs`,
  `Services/CrossPromoService.cs`, `Services/EventService.cs`
- `Models/BattlePass.cs`, `Models/LiveEvent.cs`, `Models/SeasonalEvent.cs`, `Models/DailyReward.cs`,
  `Models/DailyChallenge.cs`, `Models/DailyBundleOffer.cs`, `Models/WeeklyMission.cs`, `Models/Tournament.cs`,
  `Models/VipTier.cs`, `Models/LuckySpin.cs`, `Models/ShopOffer.cs`, `Models/BoostData.cs`,
  `Models/ReferralProgress.cs`, `Models/CrossPromoApp.cs`, `Models/GameEvent.cs`,
  `Models/GameEventEffect.cs`, `Models/Enums/GameEventType.cs`

---

## 1. Battle Pass

Quelle: `Models/BattlePass.cs` (Daten/Formeln), `Services/BattlePassService.cs` (Logik/XP-Quellen/IAP).

### 1.1 Grundparameter

| Parameter | Wert | Quelle |
|-----------|------|--------|
| Max-Tier (`MaxTier`) | 50 (const) | BattlePass.cs |
| Tiers in Tracks generiert | 50 Tiers (Index 0–49) | GenerateFreeRewards/GeneratePremiumRewards (`for i = 0; i < MaxTier`) |
| Saison-Dauer (`SeasonDurationDays`) | 30 Tage (const) | BattlePass.cs |
| Start-SeasonNumber | 1 | BattlePass.cs (Default) |
| `DaysRemaining` | `max(0, 30 - floor((UtcNow - SeasonStartDate).TotalDays))` | BattlePass.cs |
| `IsSeasonExpired` | `(UtcNow - SeasonStartDate).TotalDays > 30` | BattlePass.cs |
| Premium-IAP-SKU | `"battle_pass_season"` (Consumable) | BattlePassService.UpgradeToPremiumAsync |
| Premium-Preis | (nicht im Code gefunden — Preis kommt aus Store/IAP, nicht aus Code) | — |

### 1.2 XP-Schwelle pro Tier (`XpForNextTier`)

Quelle: BattlePass.cs.

- `baseXp = 250 * (CurrentTier + 1)`
- Wenn `CurrentTier >= 40`: `XpForNextTier = baseXp * 2` (Endgame, Tiers 41–50 doppelte Anforderung)
- Sonst: `XpForNextTier = baseXp`

Beispiele: Tier 0→1 = 250, Tier 1→2 = 500, Tier 9→10 = 2500, Tier 39→40 = 10000,
Tier 40→41 = `250*41*2` = 20500, Tier 49 (Max): XP wird auf 0 gecapt.

`AddXp(amount)`: addiert XP, steigt Tier solange `CurrentTier < 50 && CurrentXp >= XpForNextTier`
(Überschuss-XP wird übertragen). Bei `CurrentTier >= MaxTier` wird `CurrentXp = 0`. Gibt Anzahl Tier-Ups zurück.

### 1.3 XP-Quellen (automatische Vergabe)

Quelle: BattlePassService.cs (Event-Handler). XP wird nur vergeben, wenn `!IsSeasonExpired`.

| Aktion | BP-XP | Event/Handler |
|--------|-------|---------------|
| Auftrag abgeschlossen | +100 | OnOrderCompleted (`IGameStateService.OrderCompleted`) |
| MiniGame-Ergebnis | +50 | OnMiniGameResultRecorded |
| Workshop-Upgrade | +25 | OnWorkshopUpgraded |
| Arbeiter eingestellt | +30 | OnWorkerHired |
| Worker-Level-Up (Training) | +20 | OnWorkerLevelUp (`IWorkerService.WorkerLevelUp`) |
| Crafting-Produkt eingesammelt | +40 | OnCraftingProductCollected (`ICraftingService.CraftingProductCollected`) |

### 1.4 Free-Track-Belohnungen (50 Tiers, Index 0–49)

Quelle: BattlePass.GenerateFreeRewards(baseIncome). `baseMoney = max(500, baseIncome * 60)`.
`baseIncome` = `BattlePass.BaseIncomeAtSeasonStart` (fixiert beim Saisonstart, sonst `TotalIncomePerSecond`).

Drei Bereiche:

**Tiers 0–29 (Basis):**
- MoneyReward = `baseMoney * (1 + i * 0.5)`
- XpReward (Spiel-XP) = `50 + i * 25`
- GoldenScrewReward = `3`, wenn `(i+1) % 5 == 0` (also Tier-Index 4, 9, 14, 19, 24, 29), sonst `0`
- DescriptionKey = `BPFree_{i}`

**Tiers 30–48 (verbessert):**
- moneyMult = `0.75` bei ungeradem i, sonst `0.6`
- MoneyReward = `baseMoney * (1 + i * moneyMult)`
- XpReward = `100 + i * 30`
- GoldenScrewReward:
  - i == 34 (Tier 35): 15
  - i == 39 (Tier 40): 20
  - i == 44 (Tier 45): 25
  - sonst: 3 bei geradem i, sonst 0
- DescriptionKey = `BPFree_{i}`

**Tier 49 (Free-Capstone):**
- MoneyReward = `baseMoney * (1 + 49 * 0.75)`
- XpReward = `200 + 49 * 30` = 1670
- GoldenScrewReward = 50
- DescriptionKey = `BPFreeCapstone`

### 1.5 Premium-Track-Belohnungen (50 Tiers, Index 0–49)

Quelle: BattlePass.GeneratePremiumRewards(baseIncome, seasonNumber). `baseMoney = max(1500, baseIncome * 180)`.
Premium-Spread bewusst ~3x Free.

**Tiers 0–29 (Basis):**
- MoneyReward = `baseMoney * (1 + i * 0.75)`
- XpReward = `100 + i * 50`
- GoldenScrewReward = `12`, wenn `(i+1) % 3 == 0`, sonst `3`
- DescriptionKey = `BPPremium_{i}`

**Tiers 30–48 (Sonderfälle haben Vorrang vor dem regulären Zweig):**

| Tier-Index | RewardType | MoneyReward | XpReward | GS | SpeedBoostMin | DescriptionKey |
|------------|-----------|-------------|----------|----|----|----------------|
| 34 (Tier 35) | SpeedBoost | `baseMoney*(1+34*0.85)` | `150+34*55`=2020 | 5 | 120 | BPPremiumSpeedBoost2h |
| 39 (Tier 40) | Standard | `baseMoney*(1+39*0.85)` | `150+39*55`=2295 | 50 | — | BPPremiumMilestone40 |
| 44 (Tier 45) | SpeedBoost | `baseMoney*(1+44*0.85)` | `150+44*55`=2570 | 10 | 240 | BPPremiumSpeedBoost4h |
| regulär (30–48 ohne 34/39/44) | Standard | `baseMoney*(1+i*0.85)` | `150+i*55` | `12` wenn `(i+1)%3==0` sonst `3` | — | BPPremium_{i} |

**Tier 49 (Premium-Capstone):**
- MoneyReward = `baseMoney * (1 + 49 * 1.0)`
- XpReward = `200 + 49 * 60` = 3140
- GoldenScrewReward = 150
- DescriptionKey = `BPCapstone{season}` (season aus `seasonNumber % 4`, s. 1.7)

### 1.6 Reward-Anwendung (ApplyReward)

Quelle: BattlePassService.ApplyReward.
- MoneyReward > 0 → `AddMoney`
- XpReward > 0 → `AddXp` (Spiel-XP)
- GoldenScrewReward > 0 → `AddGoldenScrews`
- RewardType == SpeedBoost && SpeedBoostMinutes > 0 → SpeedBoostEndTime stackt:
  `currentEnd = max(SpeedBoostEndTime, now); SpeedBoostEndTime = currentEnd + SpeedBoostMinutes`

Claim-Regeln (ClaimReward):
- Tier muss erreicht sein (`tier <= CurrentTier`).
- Premium-Claim nur wenn `IsPremium == true`; je Tier nur einmal (ClaimedPremiumTiers / ClaimedFreeTiers).

### 1.7 Saison-Theme (zyklisch über SeasonNumber % 4)

Quelle: BattlePass.SeasonTheme / SeasonThemeColor / SeasonThemeIcon / CapstoneRewardKey.

| SeasonNumber % 4 | Theme | ThemeColor | ThemeIcon | Capstone-Key |
|------------------|-------|-----------|-----------|--------------|
| 0 | Spring | #4CAF50 | Flower | BPCapstoneSpring |
| 1 | Summer | #FF9800 | WhiteBalanceSunny | BPCapstoneSummer |
| 2 | Autumn | #795548 | Forest | BPCapstoneAutumn |
| 3 | Winter | #2196F3 | Snowflake | BPCapstoneWinter |

### 1.8 Premium-Kauf-Lock-in & Saison-Reset

Quelle: BattlePassService.
- Premium-Kauf blockiert, wenn `DaysRemaining <= 3` (Lock-in-Schutz ab Tag 27). UI-Flag `IsPremiumLockedDueToSeasonEnd`.
- Kauf erfordert erfolgreichen `PurchaseConsumableAsync("battle_pass_season")`.
- `CheckNewSeason()` (bei `IsSeasonExpired`): `SeasonNumber++`, `CurrentTier=0`, `CurrentXp=0`,
  `ClaimedFreeTiers.Clear()`, `ClaimedPremiumTiers.Clear()`, `IsPremium=false` (Premium pro Saison neu kaufen),
  `SeasonStartDate=UtcNow`, `BaseIncomeAtSeasonStart=TotalIncomePerSecond`.
- BP-Check-Tick: GameLoop alle 300 Ticks / Offset 200 (siehe App-CLAUDE.md GameLoop, nicht in diesen Files).

---

## 2. Live Events (Limited-Time)

Quelle: `Services/LiveEventService.cs`, `Services/LiveEventScoreTracker.cs`, `Models/LiveEvent.cs`.

### 2.1 Allgemein

- RemoteConfig-getrieben. Schlüssel:
  - `live_event.id` (stabile Event-ID; leer → kein Event)
  - `live_event.template` (Default `"DoubleReward"`)
  - `live_event.starts_at` (ISO-8601)
  - `live_event.ends_at` (ISO-8601)
- Event-Dauer im Modell-Kommentar als 7 Tage beschrieben, faktisch durch `starts_at`/`ends_at` definiert.
- `IsActive`: parse `EndsAtIso` (InvariantCulture, RoundtripKind) → `UtcNow < endsAt`.
- Persistenz: `GameState.LiveEvent` (Score nur übernommen, wenn `persisted.Id == id`).
- `Tick()` (vom GameLoop): bei abgelaufenem Event → EventEnded, `CurrentEvent=null`, `GameState.LiveEvent=null`.
- `AddScore(points)`: nur wenn Event aktiv und `points > 0`; `PlayerScore += points`.

### 2.2 Die 4 Templates (`LiveEventTemplate` Enum)

Quelle: LiveEvent.cs, LiveEventScoreTracker.cs (Score-Verdrahtung).

| Template | Beschreibung | Score-Verdrahtung |
|----------|--------------|-------------------|
| DoubleReward | Doppelte Auftrags-Belohnungen | +1 Punkt pro abgeschlossenem Auftrag (jeder Typ), via OrderCompleted |
| BossRush | Boss-Rush mit Spawn-Boost | Aktuell NICHT verdrahtet (benötigt GuildBoss-Damage-Event-Hook) |
| CoopMarathon | Doppelte Co-op-Auftrags-Belohnungen | +1 Punkt pro Cooperation-Auftrag (`OrderType.Cooperation`), via OrderCompleted |
| MiniGameMastery | Perfekte Ratings geben Bonus-GS | +1 Punkt pro Perfect-Rating, via PerfectRatingIncremented |

Hinweis: Die genannten Reward-Effekte (z.B. "doppelte Belohnung") sind im Code als Template-Namen/Doku
beschrieben; die tatsächliche Belohnung ist die GS-Reward-Tier-Ausschüttung (siehe 2.3), nicht ein
laufender Multiplikator (kein Multiplikator-Effekt in diesen Files implementiert).

### 2.3 Reward-Tiers

Quelle: LiveEventService.RewardTierThresholds + TryClaimNextReward.

| Tier-Index | Score-Schwelle | GS-Belohnung |
|------------|----------------|--------------|
| 0 | 100 | 25 |
| 1 | 500 | 75 |
| 2 | 2000 | 200 |

- `TryClaimNextReward()` prüft Tiers aufsteigend, zahlt den ersten erfüllten, noch nicht geclaimten Tier
  (`ClaimedRewardTiers`).
- Analytics-Events: `live_event_started`, `live_event_tier_claimed`, `live_event_ended`.

---

## 3. Seasonal Events (4 pro Jahr)

Quelle: `Services/SeasonalEventService.cs`, `Models/SeasonalEvent.cs`.

### 3.1 Zeitfenster & Lifecycle

Quelle: SeasonalEvent.CheckSeason + SeasonalEventService.CheckSeasonalEvent.

| Saison | Zeitfenster (UTC) |
|--------|-------------------|
| Spring | 1.–14. März |
| Summer | 1.–14. Juni |
| Autumn | 1.–14. September |
| Winter | 1.–14. Dezember |

- Event-Start: `StartDate = {Jahr}-{Monat}-01 00:00:00 UTC`, `EndDate = {Jahr}-{Monat}-14 23:59:59 UTC`.
- Start nur, wenn im Fenster und kein aktives Event. Außerhalb des Fensters → Event auf null.
- Zeitmanipulations-Schutz: Event-`StartDate` in Zukunft → Event verworfen.
- `IsActive` (Modell): `UtcNow >= StartDate && UtcNow <= EndDate`.

### 3.2 SP-Verdienst (Seasonal Points / Currency)

Quelle: SeasonalEventService (private consts). Nur wenn Event aktiv. `AddSeasonalCurrency` erhöht
`Currency` und `TotalPoints`.

| Quelle | SP | Bedingung |
|--------|----|-----------|
| Auftrag Basis (BaseSpPerOrder) | 5 | jeder Auftrag |
| Auftrag-Bonus Good (GoodBonusSp) | +3 | AverageRating == Good |
| Auftrag-Bonus Perfect (PerfectBonusSp) | +5 | AverageRating == Perfect |
| MiniGame (SpPerMiniGame) | 2 | pro MiniGame-Ergebnis (nicht Perfect) |
| MiniGame Perfect (SpPerPerfectMiniGame) | 4 | Rating == Perfect |
| Worker-Level-Up (SpPerWorkerLevelUp) | 1 | pro Training-Level-Up |
| Crafting eingesammelt (SpPerCraftingCollected) | 3 | pro Crafting-Produkt |

### 3.3 Event-Shop — Basis-Items (4 in JEDER Saison)

Quelle: SeasonalEventService.GetShopItems. Icon je Saison: Spring=Flower, Summer=WhiteBalanceSunny,
Autumn=Leaf, Winter=Snowflake. Item-IDs als `{prefix}_...` (prefix = Saison lowercase, z.B. `spring_income_boost`).
Kauf: einmalig pro Item (PurchasedItems), Kosten in SP.

| Item-Suffix | NameKey | Kosten (SP) | Effekt |
|-------------|---------|-------------|--------|
| income_boost | Seasonal{Season}IncomeBoost | 50 | IncomeBonus = 0.10 (+10% passiv, GameLoop) |
| xp_pack | Seasonal{Season}XpPack | 30 | XpBonus = 500 (sofort) |
| screw_bundle | Seasonal{Season}ScrewBundle | 75 | GoldenScrews = 15 (sofort) |
| speed_boost | Seasonal{Season}SpeedBoost | 100 | SpeedBoostMinutes = 120 (sofort, stackt) |

### 3.4 Event-Shop — Saison-einzigartige Items (2 pro Saison)

Quelle: SeasonalEventService.GetUniqueSeasonItems.

| Saison | Item-ID | NameKey | Kosten (SP) | Effekt |
|--------|---------|---------|-------------|--------|
| Spring | spring_extra_worker | SeasonalSpringExtraWorker | 150 | ExtraWorkerDays=1, EffectDurationDays=14 (+1 Max-Worker 14 Tage) |
| Spring | spring_research_speed | SeasonalSpringResearchSpeed | 80 | ResearchSpeedBonusPercent=30, EffectDurationDays=14 |
| Summer | summer_double_prestige | SeasonalSummerDoublePrestige | 200 | DoubleNextPrestige=true (2x PP nächster Prestige) |
| Summer | summer_offline_boost | SeasonalSummerOfflineBoost | 120 | OfflineEarningsBonusPercent=50, EffectDurationDays=14 |
| Autumn | autumn_instant_screws | SeasonalAutumnInstantScrews | 250 | InstantGoldenScrews=500 (sofort) |
| Autumn | autumn_mood_reset | SeasonalAutumnMoodReset | 60 | WorkerMoodResetTo=100 (alle Worker Mood=100, sofort) |
| Winter | winter_speed_4h | SeasonalWinterSpeed4h | 100 | SpeedBoostHours=4 (sofort, stackt) |
| Winter | winter_double_daily | SeasonalWinterDoubleDaily | 150 | DoubleDailyReward=true (nächster Daily-Reward ×100%) |

### 3.5 Effekt-Anwendung (ApplySeasonalItemEffect)

Quelle: SeasonalEventService.ApplySeasonalItemEffect (Sofort-Effekte):
- GoldenScrews > 0 → AddGoldenScrews
- XpBonus > 0 → AddXp
- SpeedBoostMinutes > 0 → SpeedBoostEndTime = max(bestehend, now + Minuten)
- InstantGoldenScrews > 0 → AddGoldenScrews
- SpeedBoostHours > 0 → SpeedBoostEndTime = max(bestehend, now + Stunden)
- WorkerMoodResetTo > 0 → alle Worker aller Workshops Mood = Wert

Passive/temporäre Effekte (von anderen Services über `PurchasedItems` ausgewertet, nicht hier):
IncomeBonus, ExtraWorkerDays, ResearchSpeedBonusPercent, OfflineEarningsBonusPercent,
DoubleNextPrestige, DoubleDailyReward.

Cosmetics: (keine eigenen Cosmetic-Items in diesen Files — Seasonal-Shop hat keine Skins; SeasonColor/SeasonIcon
nur für UI-Theming, siehe SeasonalEvent.cs.)

| Saison | SeasonColor | SeasonIcon |
|--------|-------------|------------|
| Spring | #4CAF50 | Flower |
| Summer | #FF9800 | WhiteBalanceSunny |
| Autumn | #795548 | Leaf |
| Winter | #2196F3 | Snowflake |

---

## 4. Daily Reward (30-Tage-Zyklus)

Quelle: `Services/DailyRewardService.cs`, `Models/DailyReward.cs`.

### 4.1 Mechanik

- 30-Tage-Zyklus mit Streak. `CurrentDay = ((streak - 1) % 30) + 1`, bei streak 0 → 1.
- `IsRewardAvailable`: true, wenn nie geclaimt ODER `UtcNow.Date > LastDailyRewardClaim.Date`
  (Zeitmanipulation: LastClaim in Zukunft → false).
- Streak gebrochen, wenn `daysSinceLastClaim > 1` (Negativ → ebenfalls Bruch). Bei Bruch:
  `StreakBeforeBreak = aktueller Streak`, `StreakRescueUsed=false`, `DailyRewardStreak=1`; sonst `Streak++`.
- Claim atomar unter State-Lock (Doppel-Tap-Schutz). `LastDailyRewardClaim = UtcNow`.
- Bonus-Anwendung beim Claim: SpeedBoost → `SpeedBoostEndTime = now + 1h`; XpBoost → `XpBoostEndTime = now + 1h`.
- KEINE Premium-Verdopplung des Daily-Reward im Code dieser Files (DailyRewardService ruft AddMoney/AddXp/
  AddGoldenScrews ohne `fromPurchase` / ohne ×2-Multiplikator auf). Premium-GS-Verdopplung passiert allgemein in
  `AddGoldenScrews` (Gameplay-Quelle), nicht als eigene Daily-Verdopplung. Die "DoubleDailyReward"-Verdopplung
  ist ein separater Seasonal-Effekt (Winter-Item), nicht Premium.
- VIP Auto-Claim: VipTier.HasAutoClaimDailyRewards (Bronze+) — Flag im VipTier-Modell, Auto-Claim-Verdrahtung
  außerhalb dieser Files.

### 4.2 Skalierte Geld-Belohnung (GetScaledMoney)

Quelle: DailyReward.GetScaledMoney(netIncomePerSecond). Wenn netIncome <= 0 → `Money` (Festbetrag).
Sonst: `minutesWorth = sqrt(Day) * netIncomePerSecond * 60`, gecapt bei `netIncomePerSecond * 900` (15 Min),
Ergebnis = `max(Money, minutesWorth)`.

### 4.3 Tabelle aller 30 Tage

Quelle: DailyReward.s_cachedSchedule (Festbeträge; Money wird per GetScaledMoney ggf. hochskaliert).

| Tag | Money (€) | XP | GS | Bonus |
|-----|-----------|----|----|-------|
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

`DailyBonusType`: None, SpeedBoost (2x Income 1h), XpBoost (+50% XP 1h), FreeWorker (im Schedule nicht verwendet).

---

## 5. Daily Challenges

Quelle: `Services/DailyChallengeService.cs`, `Models/DailyChallenge.cs`.

### 5.1 Mechanik

- 3 Challenges/Tag, +1 bei VIP Silver+ (`VipService.ExtraDailyChallenges`). `challengeCount = 3 + ExtraDailyChallenges`.
- Reset bei neuem UTC-Tag (CheckAndResetIfNewDay, Zeitmanipulations-Schutz).
- Typen werden zufällig aus dem nach Tier verfügbaren Pool ohne Wiederholung gezogen.
- Score-Mapping MiniGame: Perfect=100%, Good=75%, Ok=50%, Miss=0%.
- Retry per Ad: `RetryChallenge` setzt CurrentValue=0, IsCompleted=false, HasRetriedWithAd=true
  (nur wenn nicht abgeschlossen, noch nicht genutzt, CurrentValue>0).
- "Alle fertig"-Bonus: 500 € (`AllCompletedBonusAmount`) + GS nach Tier (siehe 5.2). Beim Claim werden
  vorher alle offenen Einzelbelohnungen mit eingesammelt.
- Event: `ChallengeCompleted` feuert pro Challenge bei false→true (speist Weekly-Mission CompleteDailyChallenges).

### 5.2 Tier-System (Level-basiert, identisch zu Weekly)

Quelle: DailyChallengeService.GetTier(level).

| Tier | Level-Range |
|------|-------------|
| 0 | ≤ 5 |
| 1 | 6–15 |
| 2 | 16–30 |
| 3 | 31–50 |
| 4 | 51–100 |
| 5 | 101–300 |
| 6 | 301–500 |
| 7 | 501–750 |
| 8 | ≥ 751 |

"Alle fertig"-Bonus-GS (AllCompletedBonusScrews): Tier ≤4 → 6, Tier 5 → 8, 6 → 10, 7 → 12, 8 → 15.

### 5.3 Verfügbarkeit der Typen nach Tier (GetAvailableTypesForTier)

- Basis (immer): CompleteOrders, EarnMoney, UpgradeWorkshop, HireWorker, CompleteQuickJob, PlayMiniGames, AchieveMinigameScore
- Tier ≥5: + TrainWorker, CompleteCrafting, ProduceItems, SellItems
- Tier ≥6: + AchievePerfectStreak, CompleteMaterialOrder, CollectEquipment
- Tier ≥7: + ReachWorkshopLevel

### 5.4 Belohnungs-Basis (CreateChallenge)

`netPerSecond = max(0, NetIncomePerSecond)`; `levelFloor = max(level*30, level*level/2)`;
`incomeBase = max(levelFloor, netPerSecond * 600)`. MoneyReward = `round(incomeBase * Faktor)`.
GS-Reward nach Tier: Tier ≤4 → `min(1+tier, 2)`; 5→3, 6→4, 7→5, 8→6.

### 5.5 Alle 15 Challenge-Typen (TargetValue je Tier, Reward-Formeln)

Quelle: DailyChallengeService.CreateChallenge + DailyChallenge.cs (Enum).
TargetValue-Spalten: Tier0..Tier8 (leere Zelle = Typ in diesem Tier nicht verfügbar).

| Typ (Enum) | T0 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | MoneyReward | XpReward |
|------------|----|----|----|----|----|----|----|----|----|-------------|----------|
| CompleteOrders (0) | 2 | 3 | 4 | 5 | 5 | 6 | 7 | 8 | 10 | incomeBase*0.8 | 20+level*2 |
| EarnMoney (1) | TargetValue = `max(200, incomeBase*0.5)` (tier-unabhängig) ||||||||| incomeBase*0.6 | 15+level*2 |
| UpgradeWorkshop (2) | 1 | 2 | 2 | 3 | 3 | 4 | 5 | 6 | 8 | incomeBase*1.0 | 25+level*2 |
| HireWorker (3) | 1 | 1 | 1 | 1 | 1 | 1 | 2 | 2 | 2 | incomeBase*0.7 | 20+level*2 |
| CompleteQuickJob (4) | 1 | 2 | 3 | 4 | 4 | 5 | 6 | 7 | 8 | incomeBase*0.5 | 15+level*2 |
| PlayMiniGames (5) | 3 | 4 | 5 | 7 | 7 | 8 | 10 | 12 | 15 | incomeBase*0.7 | 20+level*2 |
| AchieveMinigameScore (6) | 70 | 75 | 80 | 90 | 90 | 90 | 90 | 90 | 90 | incomeBase*1.0 | 25+level*2 |
| TrainWorker (7) | — | — | — | — | — | 2 | 3 | 4 | 5 | incomeBase*0.9 | 25+level*2 |
| CompleteCrafting (8) | — | — | — | — | — | 1 | 2 | 3 | 4 | incomeBase*1.0 | 30+level*2 |
| AchievePerfectStreak (9) | — | — | — | — | — | — | 3 | 5 | 7 | incomeBase*1.2 | 35+level*2 |
| ReachWorkshopLevel (10) | — | — | — | — | — | — | — | +10 | +50 | incomeBase*1.5 | 40+level*2 |
| ProduceItems (11) | — | — | — | — | — | 20 | 50 | 100 | 200 | incomeBase*0.8 | 25+level*2 |
| SellItems (12) | — | — | — | — | — | 10 | 25 | 50 | 100 | incomeBase*0.9 | 25+level*2 |
| CompleteMaterialOrder (13) | — | — | — | — | — | — | 1 | 2 | 3 | incomeBase*1.3 | 35+level*2 |
| CollectEquipment (14) | — | — | — | — | — | — | 1 | 2 | 3 | incomeBase*1.0 | 30+level*2 |

Hinweise:
- HireWorker: `TargetValue = tier >= 6 ? 2 : 1`.
- ReachWorkshopLevel: `TargetValue = höchstesWorkshopLevel + increment`; increment = `tier>=8 ? 50 : 10` (Tier 7 = +10).
- Tracking: CompleteOrders/EarnMoney/UpgradeWorkshop/HireWorker via GameState-Events; PlayMiniGames +
  AchieveMinigameScore (SetMax) via MiniGameResult; QuickJob/Crafting/Train/ProduceItems/SellItems/
  MaterialOrder/Equipment via externe Service-Aufrufe; AchievePerfectStreak = `Statistics.PerfectStreak` (SetMax).

---

## 6. Daily Bundle (7-Slot)

Quelle: `Services/DailyBundleService.cs`, `Models/DailyBundleOffer.cs`.

- 7 Slots (Mo=0 … So=6), RemoteConfig-getrieben — JSON aus `RemoteConfigKeys.DailyBundleSkus`,
  Enable-Flag `RemoteConfigKeys.DailyBundleEnabled` (Default false → Bundle aus, wenn kein JSON: deaktiviert).
- JSON-Format pro Slot: `{"sku","title_key","desc_key","bonus_screws","bonus_money","speed_hours"}`.
  Wrapper `{"slots":[...]}` oder direktes Array erlaubt. Max 7 Slots gelesen.
- Aktiver Slot = `((int)today.DayOfWeek + 6) % 7` (ISO Mo=0..So=6). Rotation um 00:00 UTC → Event `BundleRotated`
  (im Lock, Doppel-Event-Schutz). `ExpiresAtUtc = nächstes 00:00 UTC`.
- Kauf (PurchaseCurrentBundleAsync): `PurchaseConsumableAsync(sku)`; bei Erfolg:
  BonusGoldenScrews > 0 → `AddGoldenScrews(.., fromPurchase: true)` (NICHT verdoppelt);
  BonusMoney > 0 → AddMoney; SpeedBoostHours > 0 → SpeedBoostEndTime stackt (`max(bestehend, now) + Stunden`).
- Analytics: IapPurchaseStarted / IapPurchaseFailed / IapPurchaseSuccess.
- Konkrete Slot-Werte: (nicht im Code gefunden — kommen ausschließlich aus RemoteConfig-JSON, kein Hardcoded-Default in diesen Files.)

`DailyBundleOffer`-Felder: DayOfWeekIndex, Sku, TitleKey, DescriptionKey, BonusGoldenScrews,
BonusMoney (decimal), SpeedBoostHours, DisplayPrice (Store-Fetch), ExpiresAtUtc, SecondsUntilExpiry.

---

## 7. Weekly Missions (5 + VIP)

Quelle: `Services/WeeklyMissionService.cs`, `Models/WeeklyMission.cs`.

### 7.1 Mechanik

- 5 Missionen/Woche, +1 bei VIP Gold+ (`VipService.ExtraWeeklyMissions`). `missionCount = 5 + ExtraWeeklyMissions`.
- Reset: nächster Montag 00:00 UTC nach `LastWeeklyReset` (GetNextMonday; Zeitmanipulations-Schutz).
- Tier-System identisch zu Daily (siehe 5.2).
- "Alle fertig"-Bonus-GS (AllCompletedBonusScrews): Tier ≤4 → 50, 5 → 60, 6 → 75, 7 → 90, 8 → 120.
  Erfordert alle Missionen abgeschlossen; sammelt zuvor alle offenen Einzelbelohnungen ein.
- `IsCompleted` (Modell): `CurrentValue >= TargetValue`.

### 7.2 Verfügbarkeit der Typen nach Tier (GetAvailableTypesForTier)

- Basis (immer): CompleteOrders, EarnMoney, UpgradeWorkshops, HireWorkers, PlayMiniGames, CompleteDailyChallenges, AchievePerfectRatings
- Tier ≥5: + TrainWorkers, CompleteCraftings, ProduceItems, SellItems
- Tier ≥6: + AchievePerfectStreak, CompleteMaterialOrders, CollectEquipment
- Tier ≥7: + ReachWorkshopLevels

### 7.3 Belohnungs-Basis (CreateMission)

`netPerSecond = max(0, NetIncomePerSecond)`; `incomeBase = max(level*150, netPerSecond * 3000)` (~50 Min, 5x Daily).
MoneyReward = `round(incomeBase * Faktor)`. XpReward = `Basis + level*5`.
GS-Reward nach Tier: T0→5, T1→7, T2→9, T3→11, T4→15, T5→18, T6→22, T7→28, T8→35.

### 7.4 Alle 15 Missions-Typen (TargetValue je Tier, Reward-Formeln)

Quelle: WeeklyMissionService.CreateMission + WeeklyMission.cs (Enum). TargetValue Tier0..Tier8.

| Typ (Enum) | T0 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | MoneyReward | XpReward |
|------------|----|----|----|----|----|----|----|----|----|-------------|----------|
| CompleteOrders (0) | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 50 | 60 | incomeBase*0.8 | 100+level*5 |
| EarnMoney (1) | TargetValue = `max(1000, incomeBase*2.5)` (tier-unabhängig) ||||||||| incomeBase*0.6 | 75+level*5 |
| UpgradeWorkshops (2) | 5 | 8 | 12 | 15 | 20 | 25 | 30 | 40 | 50 | incomeBase*1.0 | 125+level*5 |
| HireWorkers (3) | 2 | 3 | 4 | 5 | 7 | 8 | 10 | 12 | 15 | incomeBase*0.7 | 100+level*5 |
| PlayMiniGames (4) | 15 | 20 | 25 | 30 | 40 | 45 | 50 | 60 | 75 | incomeBase*0.7 | 100+level*5 |
| CompleteDailyChallenges (5) | 5 | 7 | 10 | 12 | 15 | 18 | 20 | 18 | 20 | incomeBase*0.9 | 110+level*5 |
| AchievePerfectRatings (6) | 5 | 8 | 12 | 15 | 20 | 25 | 30 | 35 | 40 | incomeBase*1.0 | 125+level*5 |
| TrainWorkers (7) | — | — | — | — | — | 8 | 12 | 16 | 20 | incomeBase*0.9 | 125+level*5 |
| CompleteCraftings (8) | — | — | — | — | — | 4 | 7 | 10 | 15 | incomeBase*1.0 | 150+level*5 |
| AchievePerfectStreak (9) | — | — | — | — | — | — | 10 | 15 | 20 | incomeBase*1.2 | 175+level*5 |
| ReachWorkshopLevels (10) | — | — | — | — | — | — | — | +50 | +150 | incomeBase*1.5 | 200+level*5 |
| ProduceItems (11) | — | — | — | — | — | 100 | 250 | 500 | 1000 | incomeBase*0.8 | 125+level*5 |
| SellItems (12) | — | — | — | — | — | 50 | 125 | 250 | 500 | incomeBase*0.9 | 125+level*5 |
| CompleteMaterialOrders (13) | — | — | — | — | — | — | 5 | 10 | 15 | incomeBase*1.3 | 175+level*5 |
| CollectEquipment (14) | — | — | — | — | — | — | 5 | 10 | 15 | incomeBase*1.0 | 150+level*5 |

Hinweise:
- CompleteDailyChallenges Tier 7 bewusst 18 (statt 21), Tier 8 = 20 (Toleranz 1–2 Tage).
- ReachWorkshopLevels: increment = `tier>=8 ? 150 : 50` (Tier 7 = +50).

---

## 8. Tournament (wöchentlich, MiniGame)

Quelle: `Services/TournamentService.cs`, `Models/Tournament.cs`.

### 8.1 Mechanik

- Wöchentlich, startet Montag 00:00 UTC (`GetCurrentMonday`). Bei Wochenwechsel neues Turnier mit
  zufälligem MiniGame-Typ. `IsExpired`: `UtcNow > WeekStart + 7 Tage`.
- MiniGame-Pool (alle 10): Sawing, PipePuzzle, WiringGame, PaintingGame, RoofTiling, Blueprint,
  DesignPuzzle, Inspection, ForgeGame, InventGame.
- Teilnahme: 3 Gratis-Teilnahmen/Tag (`FreeEntriesRemaining` = `max(0, 3 - EntriesUsedToday)`, reset bei neuem Tag),
  danach 5 Goldschrauben (`EntryCost`).
- Scoring (`AddScore`): hält Top-3 (`BestScores`, absteigend), `TotalScore = Summe der Top-3`.
  EntriesUsedToday/LastEntryDate werden mitgeführt. `RecordScore` ignoriert score <= 0; zieht ggf. 5 GS ab.
- Statistik: `TotalTournamentsPlayed++` pro RecordScore; `TotalTournamentsWon++` nur bei Gold-Claim.
- Play-Games-Leaderboards: WeeklyScore=`CgkloeDjOZMKEAIQFA`, TournamentWins=`CgkloeDjOZMKEAIQFQ`,
  HighScore=`CgkloeDjOZMKEAIQFg`. Fallback: simulierte Gegner.

### 8.2 Simulierte Gegner (GenerateSimulatedOpponents)

Quelle: Tournament.GenerateSimulatedOpponents(playerLevel). 9 Gegner.
Namen: HandwerkerMax, BaumeisterPro, WerkstattKing, MeisterFritz, HammerHans, SchrauberLisa,
ProfiAnna, BaustelleKurt, WerkzeugOtto. `baseScore = max(100, playerLevel*15)`,
score = `baseScore * (0.4 + rnd*1.2)`.

### 8.3 Reward-Tiers (rang-basiert)

Quelle: Tournament.GetRewardTier (Rang des Spieler-Eintrags im Leaderboard).

| Tier | Rang | GS-Belohnung | Geld-Belohnung |
|------|------|--------------|----------------|
| Gold | 1–3 | `30 + AscensionLevel*5` | `max(100.000, netPerSecond*600)` (10 Min) |
| Silver | 4–6 | `15 + AscensionLevel*3` | `max(50.000, netPerSecond*300)` (5 Min) |
| Bronze | 7–9 | `5 + AscensionLevel*1` | `max(20.000, netPerSecond*120)` (2 Min) |
| None | 10 / kein Eintrag | 0 | 0 |

Quelle GS/Geld-Werte: TournamentService.ClaimRewards. `netPerSecond = max(0, NetIncomePerSecond)`,
`AscensionLevel = Ascension.AscensionLevel`. Claim nur einmal (`RewardsClaimed`), nur wenn `TotalScore > 0`.

---

## 9. VIP (ausgaben-basiert)

Quelle: `Services/VipService.cs`, `Models/VipTier.cs`.

- Tier-Bestimmung aus `GameState.TotalPurchaseAmount` (EUR). Neu berechnet bei StateLoaded und RecordPurchase.
- VIP ist bewusst KEIN Pay-to-Win: Income-Bonus gedeckelt, CostReduction = 0.

### 9.1 Tiers, Schwellen & Boni

Quelle: VipTierExtensions.

| Tier | MinSpend (EUR) | IncomeBonus | XpBonus | CostReduction | Farbe |
|------|----------------|-------------|---------|---------------|-------|
| None | (∞ / kein Kauf) | 0% | 0% | 0% | #808080 |
| Bronze | 4,99 | +2% | 0% | 0% | #CD7F32 |
| Silver | 9,99 | +3% | +2% | 0% | #C0C0C0 |
| Gold | 19,99 | +4% | +3% | 0% | #FFD700 |
| Platinum | 49,99 | +5% | +5% | 0% | #E5E4E2 |

### 9.2 Perks (Flags & Mengen)

| Perk | Ab Tier | Quelle |
|------|---------|--------|
| ExtraDailyChallenges = 1 | Silver+ | VipService.ExtraDailyChallenges |
| ExtraWeeklyMissions = 1 | Gold+ | VipService.ExtraWeeklyMissions |
| Auto-Claim Daily Rewards | Bronze+ | VipTier.HasAutoClaimDailyRewards |
| Lieferanten-Timer sichtbar (QoL) | Silver+ | VipTier.HasDeliveryTimer |
| Exklusiver Avatar-Rahmen (kosmetisch) | Gold+ | VipTier.HasExclusiveFrame |

`RecordPurchase(amountEur)`: `TotalPurchaseAmount += amountEur`, dann RefreshVipLevel.

---

## 10. Lucky Spin (Glücksrad, 8 Slots)

Quelle: `Services/LuckySpinService.cs`, `Models/LuckySpin.cs`.

### 10.1 Mechanik

- Spin-Stufen (Priorität): 1) regulärer Gratis-Spin (1/Tag), 2) Premium-Bonus-Gratis-Spin (zweiter Gratis-Spin/Tag,
  nur Premium / Imperium-Pass), 3) bezahlt 5 GS (`FlatSpinCost`, `SpinCost`). `PaidSpinsToday++` bei Bezahl-Spin.
- Ad-Spin: `SpinForAd()` (kein Kosten), danach `MarkAdSpinUsed()`. `HasAdSpin`: Gratis-Spin verbraucht UND
  Ad-Spin heute noch nicht genutzt → 1 Ad-Spin/Tag.
- Tages-Reset über UTC-Datum (LastFreeSpinDate / LastBonusFreeSpinDate / LastAdSpinDate / LastPaidSpinDate);
  Zeitmanipulation in Zukunft → selbstbestrafend false.
- `TotalSpins++` pro Spin.

### 10.2 Die 8 Slots: Gewichte & Werte

Quelle: LuckySpinService.PrizeWeights + LuckySpinPrize.CalculateReward.
`baseMoney = max(1000, incomePerSecond * 300)`; `incomePerSecond = max(1, NetIncomePerSecond)`.
Gesamtgewicht = 30+20+10+15+12+8+4+2 = 101.

| Slot (Enum) | Gewicht | Wahrscheinlichkeit | Belohnung |
|-------------|---------|--------------------|-----------|
| MoneySmall | 30 | 30/101 ≈ 29,7% | Geld = baseMoney * 0.5 |
| MoneyMedium | 20 | 20/101 ≈ 19,8% | Geld = baseMoney |
| MoneyLarge | 10 | 10/101 ≈ 9,9% | Geld = baseMoney * 2 |
| XpBoost | 15 | 15/101 ≈ 14,9% | XP = 500 |
| GoldenScrews5 | 12 | 12/101 ≈ 11,9% | 5 GS |
| SpeedBoost | 8 | 8/101 ≈ 7,9% | 2x Geschwindigkeit 30 Min (SpeedBoostEndTime stackt: max(bestehend, now)+30min) |
| ToolUpgrade | 4 | 4/101 ≈ 4,0% | (kein Money/GS/XP/Boost in CalculateReward — Effekt 0; ToolUpgrade-Logik nicht in diesen Files) |
| Jackpot50 | 2 | 2/101 ≈ 2,0% | 50 GS |

Auswahl: gewichteter Roll `Random.Shared.Next(TotalWeight)` mit kumulativer Summe. ApplyPrize zahlt
Money/GS/XP via AddMoney/AddGoldenScrews/AddXp, SpeedBoost gesondert.

---

## 11. Referral (Friend-Invite)

Quelle: `Services/ReferralService.cs`, `Models/ReferralProgress.cs`.

- Eigener 6-stelliger Code (alphanumerisch uppercase, ohne O/0/I/1: Zeichensatz `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`),
  einmalig generiert (`EnsureOwnCode`).
- `SubmitReferralCode(code)`: nur 6-stellig, nur einmal (UsedReferralCode), kein Self-Referral.
- `OnReferralSucceeded()`: `SuccessfulReferrals++` (nach 24h Aktivität des Eingeladenen, server-getrieben).
- Tier-Belohnungen (TryClaimNextTier), je Tier einmalig (ClaimedTiers):

| Tier (erfolgreiche Empfehlungen) | GS-Belohnung | Zusatz |
|----------------------------------|--------------|--------|
| 1 | 50 | (+ Achievement laut Doku) |
| 5 | 200 | (+ exklusiver Workshop-Skin laut Doku) |
| 10 | 500 | + permanenter Income-Boost |

- `PermanentIncomeBonus`: +5% (0.05), wenn ClaimedTiers 10 enthält, sonst 0 (ReferralProgress.cs).
- Analytics: referral_code_used, referral_succeeded, referral_tier_claimed.
- Anti-Cheat (Geräte-Fingerprint, IP-Limit) + Server-Endpoint `POST /referrals/{ownerCode}/claim`:
  laut Doku-Kommentar Folge-Sprint (nicht in diesem Service implementiert).

---

## 12. Random Events (8 Random-Typen + saisonal)

Quelle: `Services/EventService.cs`, `Models/GameEvent.cs`, `Models/GameEventEffect.cs`,
`Models/Enums/GameEventType.cs`.

### 12.1 Intervall-/Chance-Skalierung nach Prestige

Quelle: EventService.CheckForNewEvent (prestigeCount = `Prestige.TotalPrestigeCount`).

| PrestigeCount | minHours | Chance |
|---------------|----------|--------|
| 0 (kein Prestige) | 8,0 | 30% |
| 1 (Bronze) | 6,0 | 35% |
| 2 (Silver) | 4,0 | 40% |
| ≥3 (Gold+) | 3,0 | 50% |

Ablauf: nur wenn kein aktives Event; `hoursSinceLastCheck >= minHours`; dann `LastEventCheck = UtcNow`;
`Random.NextDouble() > chance` → kein Event. EventHistory hält max 20 Einträge.

### 12.2 Pity-Timer

Quelle: EventService.CheckForNewEvent. Bei `ConsecutiveNegativeEvents >= 2` werden nur positive Typen
zur Auswahl zugelassen. Nach negativem Event `ConsecutiveNegativeEvents++`, nach positivem → 0.

### 12.3 Die 8 Random-Event-Typen (Pool für zufällige Auswahl)

Quelle: EventService (allRandomTypes) + GameEvent.GetDefaultEffect + GameEventType-Extensions.
`IsPositive`: MaterialSale, HighDemand, InnovationFair, CelebrityEndorsement = positiv;
MaterialShortage, EconomicDownturn, TaxAudit, WorkerStrike = negativ.

| Typ | Positiv? | Dauer | Effekt (Default) | Icon |
|-----|----------|-------|------------------|------|
| MaterialSale (0) | ja | 6h | CostMultiplier 0.7 (-30% Kosten) | Star |
| MaterialShortage (1) | nein | 4h | CostMultiplier 1.5 (+50% Kosten), AffectedWorkshop = zufällig | AlertCircle |
| HighDemand (2) | ja | 8h | RewardMultiplier 1.5 (+50% Belohnung), AffectedWorkshop = zufällig | CashMultiple |
| EconomicDownturn (3) | nein | 6h | RewardMultiplier 0.7, ReputationChange +2 | TrendingDown |
| TaxAudit (4) | nein | 1h | SpecialEffect "tax_10_percent" (10% Steuer auf Brutto) | Finance |
| WorkerStrike (5) | nein | 2h | SpecialEffect "mood_drop_all_20", MarketRestriction = Tier C | AccountOff |
| InnovationFair (6) | ja | 4h | IncomeMultiplier 1.3 (+30%) | LightbulbOn |
| CelebrityEndorsement (7) | ja | 8h | IncomeMultiplier 1.2, ReputationChange +5 | StarCircle |

Hinweis: HighDemand/MaterialShortage rollen `AffectedWorkshop` aus allen WorkshopTypes. WorkerStrike setzt
zusätzlich MarketRestriction = WorkerTier.C.

### 12.4 Saisonale Event-Typen (zeitbasiert, NICHT im Random-Pool)

Quelle: GameEventType (≥10) + GetDefaultEffect + GetDefaultDuration (24h für Saison-Typen).

| Typ | Effekt | Icon |
|-----|--------|------|
| SpringSeason (10) | IncomeMultiplier 1.15 | Flower |
| SummerBoom (11) | RewardMultiplier 1.2 | WhiteBalanceSunny |
| AutumnSurge (12) | IncomeMultiplier 1.1, RewardMultiplier 1.1 | Forest |
| WinterSlowdown (13) | IncomeMultiplier 0.9 | Snowflake |

### 12.5 Saisonaler Monats-Multiplikator (immer aktiv, multipliziert Income)

Quelle: EventService.GetSeasonalMultiplier(month). Wird in GetCurrentEffects mit dem Event-IncomeMultiplier
multipliziert; Cache invalidiert bei Monatswechsel/StateLoaded.

| Monate | Multiplikator |
|--------|---------------|
| 3,4,5 (Frühling) | 1.15 (+15%) |
| 6,7,8 (Sommer) | 1.20 (+20%) |
| 9,10,11 (Herbst) | 1.10 (+10%) |
| 12,1,2 (Winter) | 0.90 (-10%) |

`GameEventEffect`-Felder (Defaults 1.0 bei Multiplikatoren): IncomeMultiplier, CostMultiplier,
RewardMultiplier, ReputationChange (0), MarketRestriction (WorkerTier?), AffectedWorkshop (WorkshopType?),
SpecialEffect (string?).

---

## 13. Cross-Promotion (House-Ads)

Quelle: `Services/CrossPromoService.cs`, `Models/CrossPromoApp.cs`.

- Statischer Katalog mit 10 Einträgen (im Code; eigene App `handwerkerimperium` wird bei `GetAvailable`
  ausgefiltert → 9 promotbar). Kein Cost, deterministische Tagesrotation.
- Tagesrotation: `startIdx = UtcNow.DayOfYear % available.Count`; `GetCurrentRotation(count)` liefert
  ab startIdx zyklisch. Self-Package `com.meineapps.handwerkerimperium` gefiltert.
- Deep-Link: `https://play.google.com/store/apps/details?id={PackageId}`. Analytics: `cross_promo_click`.

| Id | NameKey | HookKey | IconKind | AccentColor | PackageId |
|----|---------|---------|----------|-------------|-----------|
| rechnerplus | CrossPromo_RechnerPlus_Name | CrossPromo_RechnerPlus_Hook | Cash | #7C7FF7 | com.meineapps.rechnerplus |
| zeitmanager | CrossPromo_ZeitManager_Name | CrossPromo_ZeitManager_Hook | TimerOutline | #F7A833 | com.meineapps.zeitmanager |
| finanzrechner | CrossPromo_FinanzRechner_Name | CrossPromo_FinanzRechner_Hook | CashMultiple | #10B981 | com.meineapps.finanzrechner |
| fitnessrechner | CrossPromo_FitnessRechner_Name | CrossPromo_FitnessRechner_Hook | Dumbbell | #06B6D4 | com.meineapps.fitnessrechner |
| handwerkerrechner | CrossPromo_HandwerkerRechner_Name | CrossPromo_HandwerkerRechner_Hook | HammerWrench | #3B82F6 | com.meineapps.handwerkerrechner |
| worktimepro | CrossPromo_WorkTimePro_Name | CrossPromo_WorkTimePro_Hook | Cog | #4F8BF9 | com.meineapps.worktimepro |
| handwerkerimperium | CrossPromo_HandwerkerImperium_Name | CrossPromo_HandwerkerImperium_Hook | Hammer | #D97706 | com.meineapps.handwerkerimperium (self, gefiltert) |
| bomberblast | CrossPromo_BomberBlast_Name | CrossPromo_BomberBlast_Hook | RocketLaunch | #FF6B35 | com.meineapps.bomberblast |
| rebornsaga | CrossPromo_RebornSaga_Name | CrossPromo_RebornSaga_Hook | Sword | #4A90D9 | com.meineapps.rebornsaga |
| bingxbot | CrossPromo_BingXBot_Name | CrossPromo_BingXBot_Hook | FlaskOutline | #3B82F6 | com.meineapps.bingxbot |
| gardencontrol | CrossPromo_GardenControl_Name | CrossPromo_GardenControl_Hook | ShieldHalfFull | #2E7D32 | com.meineapps.gardencontrol |

Hinweis: Der Service-Klassenkommentar spricht von "11 Apps", das Array `s_catalog` enthält aber genau
10 Einträge (inkl. self). SmartMeasure ist NICHT im Katalog.

---

## 14. Shop Daily Offer (ShopOffer)

Quelle: `Models/ShopOffer.cs`. (Generierungs-Logik im Modell; Service-Wiring außerhalb der gelesenen Files.)

`GenerateDaily(incomePerSecond)` wählt zufällig 1 von 4 Angeboten; `DiscountedPrice = OriginalPrice / 2`,
`Discount = 50%`, `ExpiresAt = nächstes 00:00 UTC` (Mitternacht):

| ItemId | NameKey | OriginalPrice (GS) | DiscountedPrice (GS) | GS-Reward | Money-Reward |
|--------|---------|--------------------|----------------------|-----------|--------------|
| daily_screws_10 | DailyOfferScrews10 | 20 | 10 | 10 | 0 |
| daily_screws_25 | DailyOfferScrews25 | 50 | 25 | 25 | 0 |
| daily_money_boost | DailyOfferMoneyBoost | 15 | 7 | 0 | `max(5000, incomePerSecond*600)` |
| daily_speed_boost | DailyOfferSpeedBoost | 10 | 5 | 0 | 0 |

---

## 15. Boost-Daten (BoostData)

Quelle: `Models/BoostData.cs`. Relevant für Speed/XP/Rush-Boosts, die Live-Ops-Rewards setzen.

| Feld | Bedeutung |
|------|-----------|
| SpeedBoostEndTime | 2x-Income-Boost-Ende (von BattlePass, Daily, Seasonal, LuckySpin, DailyBundle gesetzt) |
| XpBoostEndTime | +50%-XP-Boost-Ende (von Daily Reward XpBoost gesetzt, 1h) |
| RushBoostEndTime | Feierabend-Rush-Ende |
| LastFreeRushUsed | letztes Datum des Gratis-Rush |
| IsSoftCapActive / SoftCapReductionPercent | Soft-Cap-Status (vom GameLoop gesetzt) |
| IsFreeRushAvailable | `LastFreeRushUsed.Date < UtcNow.Date` |

Konkrete Rush-Dauer/-Kosten (2h, 1x/Tag gratis, danach 10 GS) sind laut App-CLAUDE.md beschrieben, aber
nicht in `BoostData.cs` als Konstanten enthalten — (nicht in diesen Files gefunden, liegen in GameLoop/Constants).

---

## 16. Goldschrauben-Quellen-Tabelle (GS) — aus den Live-Ops-Quellen

Konsolidiert aus den oben gelesenen Files (nur GS-Ausschüttungen aus dem Live-Ops-/Monetarisierungs-Bereich):

| Quelle | GS-Wert | Quell-File |
|--------|---------|-----------|
| Battle Pass Free, alle 5 Tiers (0–29) | 3 | BattlePass.GenerateFreeRewards |
| Battle Pass Free, Tier 35/40/45 | 15 / 20 / 25 | BattlePass.GenerateFreeRewards |
| Battle Pass Free, gerade Tiers 30–48 | 3 | BattlePass.GenerateFreeRewards |
| Battle Pass Free Capstone (Tier 49) | 50 | BattlePass.GenerateFreeRewards |
| Battle Pass Premium (0–29) | 12 (alle 3 Tiers) / sonst 3 | BattlePass.GeneratePremiumRewards |
| Battle Pass Premium Tier 35 (SpeedBoost) | 5 | BattlePass.GeneratePremiumRewards |
| Battle Pass Premium Tier 40 (Milestone) | 50 | BattlePass.GeneratePremiumRewards |
| Battle Pass Premium Tier 45 (SpeedBoost) | 10 | BattlePass.GeneratePremiumRewards |
| Battle Pass Premium reguläre 30–48 | 12 (alle 3 Tiers) / sonst 3 | BattlePass.GeneratePremiumRewards |
| Battle Pass Premium Capstone (Tier 49) | 150 | BattlePass.GeneratePremiumRewards |
| Live Event Tier 0/1/2 | 25 / 75 / 200 | LiveEventService.TryClaimNextReward |
| Seasonal Shop screw_bundle | 15 | SeasonalEventService.GetShopItems |
| Seasonal Shop autumn_instant_screws | 500 | GetUniqueSeasonItems (Autumn) |
| Daily Reward (Tage mit GS) | 1–25 (siehe Tabelle 4.3, Tag 30 = 25) | DailyReward.s_cachedSchedule |
| Daily Challenge GS-Reward | Tier ≤4: 1–2; 5:3; 6:4; 7:5; 8:6 | DailyChallengeService.CreateChallenge |
| Daily Challenge "Alle fertig"-Bonus | Tier ≤4:6; 5:8; 6:10; 7:12; 8:15 | DailyChallengeService.AllCompletedBonusScrews |
| Weekly Mission GS-Reward | T0:5 … T8:35 (siehe 7.3) | WeeklyMissionService.CreateMission |
| Weekly Mission "Alle fertig"-Bonus | Tier ≤4:50; 5:60; 6:75; 7:90; 8:120 | WeeklyMissionService.AllCompletedBonusScrews |
| Tournament Gold/Silver/Bronze | 30+Asc*5 / 15+Asc*3 / 5+Asc | TournamentService.ClaimRewards |
| Lucky Spin GoldenScrews5 | 5 | LuckySpinPrize.CalculateReward |
| Lucky Spin Jackpot50 | 50 | LuckySpinPrize.CalculateReward |
| Lucky Spin Kosten (Bezahl-Spin) | -5 (Ausgabe) | LuckySpinService.FlatSpinCost |
| Tournament Entry (nach 3 Gratis) | -5 (Ausgabe) | TournamentService.EntryCost |
| Referral Tier 1/5/10 | 50 / 200 / 500 | ReferralService.TryClaimNextTier |
| Daily Bundle Bonus-GS | aus RemoteConfig (fromPurchase, nicht verdoppelt) | DailyBundleService |
| Shop Daily Offer daily_screws_10/25 | 10 / 25 | ShopOffer.GenerateDaily |

Hinweis Premium-GS-Verdopplung: Gameplay-GS-Quellen werden über `AddGoldenScrews(amount)` ausgeschüttet und
bei Premium (+100% GS) allgemein verdoppelt; IAP-/Kauf-Quellen rufen `AddGoldenScrews(.., fromPurchase: true)`
(Daily Bundle) und werden NICHT verdoppelt. Die Verdopplungs-Logik selbst liegt in `GameStateService.AddGoldenScrews`
(außerhalb der gelesenen Files).

---

# 07 — Engagement & Narrative: Research-Tree, Achievements, Goals, Story, FTUE, Hints, MiniGames+Mastery

Diese Datei ist die verbindliche Wahrheit aus dem Original-Code von HandwerkerImperium (Avalonia).
Alle Werte stammen 1:1 aus dem Code. Berechnete Werte sind als Formel gekennzeichnet.
Code-Root: `HandwerkerImperium.Shared/`

---

## 1. Research-Tree

Quelle: `Models/ResearchTree.cs`, `Models/Research.cs`, `Models/ResearchEffect.cs`, `Models/Enums/ResearchBranch.cs`, `Services/ResearchService.cs`

### 1.1 Branch-Übersicht

Quelle: `Models/Enums/ResearchBranch.cs`

| Branch | Enum-Wert | Icon (Original-Glyph als Unicode-Codepoint) | Farbe | Nodes |
|--------|-----------|---------------------------------------------|-------|-------|
| Tools | 0 | Wrench (U+1F527) | #FF9800 (Orange) | 20 |
| Management | 1 | Briefcase (U+1F4BC) | #2196F3 (Blau) | 20 |
| Marketing | 2 | Megaphone (U+1F4E3) | #4CAF50 (Grün) | 20 |
| Logistics | 3 | Package (U+1F4E6) | #D97706 (Amber) | 12 |

> Hinweis: Das Avalonia-Original verwendet diese Emoji-Glyphen direkt als Branch-Icons. Fuer die Unity-Version werden sie gemaess Projekt-Regel (keine Emojis als UI-Text) durch Material-Icons/3D-Icons ersetzt — das ist eine reine Praesentations-Verbesserung ("besser/3D"), keine Mechanik-Aenderung.

**Node-Anzahl gesamt: 72** (Tools 20 + Management 20 + Marketing 20 + Logistics 12).

WICHTIG: Der ResearchService-Code spricht im Cache von "45 Eintraegen" (`BranchCount = 3`, Kommentar "ueber 45 Eintraege"), und Achievements (`research_all`) zielt auf 45 ("Complete all 45 researches"). Der ResearchTree-Klassen-Kommentar nennt "~57 Knoten". Der tatsaechliche Code von `ResearchTree.CreateAll()` erzeugt jedoch **72 Nodes** (4 Branches je 20/20/20/12). Diese Diskrepanz ist im Original vorhanden — die Kommentare/Achievement-Werte sind veraltet. Maßgeblich für die Neuentwicklung ist die tatsächliche Node-Liste unten (72 Nodes).

Branch-Lokalisierungs-Keys: `Branch{branch}` (z.B. `BranchTools`), Beschreibung `Branch{branch}Desc`.

ACHTUNG `BranchCount`-Cache-Bug-Potenzial: `ResearchService.BranchCount = 3` und `_cachedBranches` ist nur 3 Slots groß. `GetBranch(ResearchBranch.Logistics)` (Index 3) würde `_cachedBranches[3]` außerhalb des Arrays adressieren. `RebuildCaches()` prüft `branchIdx >= 0 && branchIdx < BranchCount`, sodass Logistics-Nodes NICHT in den Branch-Cache aufgenommen werden, aber `GetBranch(Logistics)` würde dennoch `_cachedBranches[3]` mit IndexOutOfRange werfen. Für die Neuentwicklung: BranchCount = 4 setzen.

### 1.2 ResearchEffect-Felder (alle möglichen Effekt-Typen)

Quelle: `Models/ResearchEffect.cs`. Alle decimal-Werte sind additive Boni; bool-Werte sind Unlocks. Kombination via `ResearchEffect.Combine(a, b)`:
- additiv: EfficiencyBonus, CostReduction, MiniGameZoneBonus, WageReduction, ExtraWorkerSlots, ExtraOrderSlots, TrainingSpeedMultiplier, RewardMultiplier, LevelResistanceBonus, AscensionPointBonus, WorkshopSynergyBonus, ReputationBonus, PremiumOrderChance, BonusWarehouseSlots, CraftingSpeedBonus, SupplierMaterialBonus
- bool OR: UnlocksAutoMaterial, UnlocksHeadhunter, UnlocksSTierWorkers, UnlocksAutoAssign, UnlocksAutoTraining, UnlocksMassHiring, UnlocksMarket, UnlocksAutoSellRules, UnlocksTier4, UnlocksHeirloomSurvival
- `StackLimitMultiplier`: **Max** (nicht additiv) → `Math.Max(a.StackLimitMultiplier, b.StackLimitMultiplier)`

### 1.3 Tools-Branch (20 Nodes)

Quelle: `ResearchTree.CreateToolsBranch()`. Spalten: Id | Level | NameKey | Kosten (€) | Dauer | Effekt | Prereqs

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| tools_01 | 1 | ResearchBetterSaws | 500 | 10 min | EfficiencyBonus +0.05 | — |
| tools_02 | 2 | ResearchPrecisionTools | 2.000 | 30 min | MiniGameZoneBonus +0.02 | tools_01 |
| tools_03 | 3 | ResearchPowerTools | 8.000 | 1 h | EfficiencyBonus +0.05 | tools_01 |
| tools_04 | 4 | ResearchAutoMaterial | 25.000 | 2 h | UnlocksAutoMaterial = true | tools_02, tools_03 |
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
| tools_20 | 20 | ResearchEternalForge | 100.000.000.000 | 168 h | EfficiencyBonus +0.30, AscensionPointBonus +0.25, MiniGameZoneBonus +0.08, WorkshopSynergyBonus +0.03 | tools_19 |

### 1.4 Management-Branch (20 Nodes)

Quelle: `ResearchTree.CreateManagementBranch()`

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| mgmt_01 | 1 | ResearchHrBasics | 500 | 10 min | WageReduction +0.05 | — |
| mgmt_02 | 2 | ResearchTeamBuilding | 2.000 | 30 min | ExtraWorkerSlots +1 | mgmt_01 |
| mgmt_03 | 3 | ResearchMotivation | 8.000 | 1 h | WageReduction +0.05 | mgmt_01 |
| mgmt_04 | 4 | ResearchHeadhunter | 25.000 | 2 h | UnlocksHeadhunter = true | mgmt_02, mgmt_03 |
| mgmt_05 | 5 | ResearchTrainingProgram | 80.000 | 4 h | TrainingSpeedMultiplier +0.5, LevelResistanceBonus +0.05 | mgmt_04 |
| mgmt_06 | 6 | ResearchWorkLifeBalance | 200.000 | 6 h | WageReduction +0.08 | mgmt_04 |
| mgmt_07 | 7 | ResearchAutoAssign | 500.000 | 8 h | UnlocksAutoAssign = true | mgmt_05, mgmt_06 |
| mgmt_08 | 8 | ResearchTalentScout | 1.000.000 | 12 h | ExtraWorkerSlots +1 | mgmt_07 |
| mgmt_09 | 9 | ResearchLeadership | 3.000.000 | 16 h | WageReduction +0.10, LevelResistanceBonus +0.08 | mgmt_07 |
| mgmt_10 | 10 | ResearchEliteRecruitment | 8.000.000 | 24 h | UnlocksSTierWorkers = true | mgmt_08, mgmt_09 |
| mgmt_11 | 11 | ResearchMentorship | 20.000.000 | 32 h | TrainingSpeedMultiplier +0.5, LevelResistanceBonus +0.07 | mgmt_10 |
| mgmt_12 | 12 | ResearchCorporateCulture | 50.000.000 | 40 h | WageReduction +0.10 | mgmt_10 |
| mgmt_13 | 13 | ResearchGlobalTalent | 100.000.000 | 48 h | ExtraWorkerSlots +2 | mgmt_11, mgmt_12 |
| mgmt_14 | 14 | ResearchAiManagement | 300.000.000 | 60 h | WageReduction +0.12, LevelResistanceBonus +0.10 | mgmt_13 |
| mgmt_15 | 15 | ResearchMasterManager | 1.000.000.000 | 72 h | ExtraWorkerSlots +2, WageReduction +0.15, LevelResistanceBonus +0.10 | mgmt_13 |
| mgmt_16 | 16 | ResearchTalentAcademy | 5.000.000.000 | 96 h | WageReduction +0.15, ExtraWorkerSlots +2 | mgmt_14, mgmt_15 |
| mgmt_17 | 17 | ResearchAutoTraining | 10.000.000.000 | 108 h | UnlocksAutoTraining = true, TrainingSpeedMultiplier +0.5 | mgmt_16 |
| mgmt_18 | 18 | ResearchMasterMentor | 20.000.000.000 | 120 h | LevelResistanceBonus +0.15, TrainingSpeedMultiplier +1.0 | mgmt_16 |
| mgmt_19 | 19 | ResearchMassHiring | 50.000.000.000 | 144 h | UnlocksMassHiring = true, ExtraWorkerSlots +3 | mgmt_17, mgmt_18 |
| mgmt_20 | 20 | ResearchLegendaryLeader | 100.000.000.000 | 168 h | ExtraWorkerSlots +3, WageReduction +0.20, TrainingSpeedMultiplier +1.0, LevelResistanceBonus +0.15 | mgmt_19 |

### 1.5 Marketing-Branch (20 Nodes)

Quelle: `ResearchTree.CreateMarketingBranch()`

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
| mkt_20 | 20 | ResearchEternalLegacy | 100.000.000.000 | 168 h | RewardMultiplier +0.30, ExtraOrderSlots +3, ReputationBonus +0.20, PremiumOrderChance +0.15 | mkt_19 |

### 1.6 Logistics-Branch (12 Nodes)

Quelle: `ResearchTree.CreateLogisticsBranch()`. WICHTIG: Die Node-Ids sind NICHT in numerischer Reihenfolge — die Prereq-Kette ordnet sie. Reihenfolge der Definition / Levels:

| Id | Lv | NameKey | Kosten | Dauer | Effekt | Prereqs |
|----|----|---------|--------|-------|--------|---------|
| logi_01 | 1 | ResearchLogiSlots1 | 50.000 | 30 min | BonusWarehouseSlots +5 | — |
| logi_02 | 2 | ResearchLogiStack2x | 200.000 | 1 h | StackLimitMultiplier 2.0 | logi_01 |
| logi_05 | 3 | ResearchLogiMarket | 500.000 | 2 h | UnlocksMarket = true | logi_02 |
| logi_04 | 4 | ResearchLogiSlots2 | 1.500.000 | 3 h | BonusWarehouseSlots +10 | logi_05 |
| logi_08 | 5 | ResearchLogiSupplier | 4.000.000 | 6 h | SupplierMaterialBonus +0.50 | logi_04 |
| logi_07 | 6 | ResearchLogiAutoSell | 10.000.000 | 8 h | UnlocksAutoSellRules = true | logi_08 |
| logi_10 | 7 | ResearchLogiCraftSpeed | 25.000.000 | 12 h | CraftingSpeedBonus +0.20 | logi_07 |
| logi_11 | 8 | ResearchLogiStack5x | 60.000.000 | 16 h | StackLimitMultiplier 5.0 | logi_10 |
| logi_09 | 9 | ResearchLogiTier4 | 150.000.000 | 24 h | UnlocksTier4 = true | logi_11 |
| logi_03 | 10 | ResearchLogiSlots3 | 400.000.000 | 24 h | BonusWarehouseSlots +25 | logi_09 |
| logi_12 | 11 | ResearchLogiHeirloom | 1.000.000.000 | 24 h | UnlocksHeirloomSurvival = true | logi_03 |
| logi_06 | 12 | ResearchLogiMaster | 5.000.000.000 | 24 h | CraftingSpeedBonus +0.30, BonusWarehouseSlots +25 | logi_12 |

Hinweis Logistics: Die obersten 3 Nodes (logi_03/logi_12/logi_06) wurden auf max 24 h gekappt (Kommentar: vorher 32/48/72 h).

### 1.7 Research-Node Datenmodell

Quelle: `Models/Research.cs`. DescriptionKey wird automatisch erzeugt als `NameKey + "Desc"`. Persistierte Felder: id, branch, level, nameKey, descriptionKey, cost, durationTicks, isResearched, isActive, startedAt, completedAt, bonusSeconds, effect, prerequisites.

Transient (JsonIgnore): Duration (= TimeSpan.FromTicks(DurationTicks)), EffectiveDuration (vom Service gesetzt), RemainingTime, Progress.

**RemainingTime-Formel**: `elapsed = UtcNow - StartedAt + TimeSpan.FromSeconds(BonusSeconds)`; `duration = EffectiveDuration ?? Duration`; `remaining = duration - elapsed` (geclampt auf ≥ 0).

**Progress-Formel** (0–100): IsResearched → 100; sonst `Clamp(elapsed / duration * 100, 0, 100)`.

### 1.8 InstantFinish-GS-Kosten pro Level (ab Level 8)

Quelle: `Research.InstantFinishScrewCost`. `CanInstantFinish` = IsActive && Kosten > 0 (also nur ab Level 8).

| Level | GS-Kosten |
|-------|-----------|
| < 8 | 0 (nicht möglich) |
| 8 | 15 |
| 9 | 25 |
| 10 | 40 |
| 11 | 60 |
| 12 | 90 |
| 13 | 120 |
| 14 | 150 |
| 15 | 180 |
| 16 | 220 |
| 17 | 260 |
| 18 | 320 |
| 19 | 400 |
| 20 | 500 |

### 1.9 Research-Mechaniken (Service)

Quelle: `Services/ResearchService.cs`

**StartResearch(id)** — Reihenfolge der Checks:
1. Challenge OhneForschung blockiert → false (`IChallengeConstraintService.IsResearchBlocked()`).
2. Es darf keine aktive Forschung laufen (`ActiveResearchId != null` → false).
3. Node existiert, ist nicht IsResearched, nicht IsActive.
4. Alle Prerequisites müssen IsResearched sein.
5. `CanAfford(Cost)` (Geld) — sonst false.
6. Geld abziehen (`TrySpendMoney`), IsActive = true, StartedAt = UtcNow, BonusSeconds = 0, EffectiveDuration = CalculateEffectiveDuration, ActiveResearchId = id.

**CancelResearch()**: nur wenn aktiv. IsActive = false, StartedAt = null, **50% der Kosten erstattet** (`AddMoney(Cost * 0.5m)`), ActiveResearchId = null.

**InstantFinishResearch()** (GS-Festpreis nach Level, s. 1.8): nur wenn `CanInstantFinish`. Kosten = `InstantFinishScrewCost`. Prüft `CanAffordGoldenScrews`. Setzt IsResearched, IsActive=false, CompletedAt=UtcNow, ActiveResearchId=null. Feuert `ResearchCompleted`.

**InstantCompleteResearch()** (zeitbasierte GS-Kosten — alternative Methode): nur wenn IsActive. Berechnet effektive Restzeit (inkl. Boni). `remaining = EffectiveDuration - elapsed`, elapsed = `UtcNow - StartedAt + FromSeconds(BonusSeconds)`. Wenn ≤ 0 → false. Kosten via `GetInstantCompleteGSCost(remaining)`. Prüft CanAffordGoldenScrews. Setzt fertig + feuert ResearchCompleted.

**GetInstantCompleteGSCost(remaining)**: 0 wenn remaining ≤ 0; sonst `hours = Ceiling(remaining.TotalHours)`, `cost = hours * 5`, `Clamp(cost, 5, 50)`. → **5 GS pro angefangener Reststunde, min 5, max 50.**

**ReduceResearchTime(percentage)** (z.B. Rewarded-Ad −50%): nur wenn aktiv. `percentage = Clamp(percentage, 0.0, 1.0)`. effektive Restzeit = EffectiveDuration − elapsed. Wenn ≤ 0 → false. `reductionSeconds = effectiveRemaining.TotalSeconds * percentage`; **BonusSeconds += reductionSeconds** (StartedAt wird NICHT manipuliert).

**UpdateTimer(deltaSeconds)**: aktualisiert EffectiveDuration jeden Tick (Boni können sich ändern). Abschluss wenn `UtcNow >= StartedAt + effectiveDuration − FromSeconds(BonusSeconds)`. Setzt fertig + feuert ResearchCompleted.

**CalculateEffectiveDuration(research)** — Reihenfolge der Multiplikatoren auf `research.Duration`:
1. **Gilden-Forschungs-Bonus** (additiv auf Geschwindigkeit): `duration = duration.TotalSeconds / (1 + GuildMembership.ResearchSpeedBonus)` (nur wenn > 0).
2. **Ascension Timeless-Research-Perk** (reduziert Dauer): `duration *= (1 - ascensionBonus)` (`_ascensionService.GetResearchSpeedBonus()`, nur wenn > 0).
3. **Prestige-Shop Forschungs-Turbo**: `duration *= (1 - shopResearchBonus)` (`_prestigeService.GetResearchSpeedBonus()`, nur wenn > 0).
4. **Minimaldauer-Cap**: `Math.Max(duration.TotalSeconds, 60)` → mindestens 60 Sekunden (verhindert Sofort-Abschluss durch gestackte Boni).

HINWEIS zum "+50%-Cap" aus der Aufgabe: Im ResearchService selbst gibt es KEINEN expliziten "+50%-Cap" auf die Geschwindigkeit/Dauer. Es gibt nur den **60-Sekunden-Minimal-Dauer-Cap**. Ein "+50%"-Cap ist im Research-Code **(nicht im Code gefunden)** — der einzige feste Reduktions-Prozentwert (ReduceResearchTime) hat keinen festen Wert, sondern wird vom Aufrufer übergeben (Rewarded-Ad: −50% laut App-CLAUDE.md `research_speedup`, nur ab 30min Restzeit). Der Wert 0.50 (SupplierMaterialBonus) ist ein anderer Effekt.

**GetTotalEffects()**: summiert alle `IsResearched`-Nodes via `ResearchEffect.Combine`. Gecacht (Dirty-Flag).

**Caches**: `_cachedEffects`, `_activeResearchCache`, `_cachedBranches[3]` (s. BranchCount-Hinweis 1.1), `_researchedIds` (HashSet). Invalidierung bei jeder Statusänderung + bei `StateLoaded`.

**Event**: `ResearchCompleted` (EventHandler<Research>).

---

## 2. Achievements

Quelle: `Models/Achievement.cs` (`Achievements.GetAll()`), `Services/AchievementService.cs`, `Models/Enums/AchievementCategory.cs`

### 2.1 Übersicht

**Echte Anzahl: 109 Achievements** (gezählt aus `Achievements.GetAll()` — `new Achievement(` × 109).

`AchievementService.TotalCount` = `_achievements.Count` = 109. `UnlockedCount` zählt freigeschaltete.

**17 Kategorien** (Enum `AchievementCategory`): Orders, Workshops, MiniGames, Money, Time, Special, Workers, Buildings, Research, Reputation, Prestige, Guilds, Crafting, Tournaments, Collection, Ascension, Rebirth.

ACHTUNG: Der Service-`UpdateProgress()`-Switch deckt NICHT alle 109 Ids ab. Mehrere Achievements werden im Switch nie aktualisiert (kein `case`), d.h. ihr CurrentValue bleibt 0 → sie können über den automatischen Pfad nie freigeschaltet werden (nur theoretisch über `BoostAchievement`-Ad oder manuelles Setzen). Nicht im Switch behandelt (CurrentValue bleibt default): `all_workshops_8`, `events_survived_10`, `worker_a_tier`, `workers_max_level`, `worker_loyal`, `worker_specialist`, `workers_total_50`, `worker_s_tier`, `research_first`, `research_branch`, `research_all`, `research_tools5`, `research_mgmt5`, `reputation_70`, `reputation_90`, `reputation_100`, `regular_10`. Für die Neuentwicklung: diese müssen mit echtem Tracking verdrahtet werden (siehe RESX-Fallbacks unten als Intention).

### 2.2 Achievement-Datenmodell

Quelle: `Models/Achievement.cs`. Felder: Id, TitleKey, TitleFallback, DescriptionKey, DescriptionFallback, Category, Icon (Emoji/Material-Icon-Name), TargetValue (long, default 1), CurrentValue, MoneyReward (decimal), XpReward (int), GoldenScrewReward (int), IsUnlocked, UnlockedAt, HasUsedAdBoost.
- `Progress` = `Min(100, CurrentValue/TargetValue*100)`.
- `IsCloseToUnlock` = nicht unlocked && Progress ≥ 75.

### 2.3 Vollständige Achievement-Liste (alle 109)

Spalten: Id | Category | TitleFallback | TargetValue | Money | XP | GS | Bedingung (Switch-Mapping in AchievementService.UpdateProgress)

| Id | Category | Titel | Target | Money | XP | GS | Bedingung (Quelle State) |
|----|----------|-------|--------|-------|----|----|--------------------------|
| first_order | Orders | First Steps | 1 | 100 | 10 | 0 | Statistics.TotalOrdersCompleted |
| orders_10 | Orders | Getting Started | 10 | 500 | 25 | 0 | TotalOrdersCompleted |
| orders_50 | Orders | Reliable Worker | 50 | 2.500 | 150 | 10 | TotalOrdersCompleted |
| orders_100 | Orders | Master Craftsman | 100 | 5.000 | 300 | 25 | TotalOrdersCompleted |
| orders_500 | Orders | Industry Legend | 500 | 25.000 | 750 | 0 | TotalOrdersCompleted |
| perfect_first | MiniGames | Perfection! | 1 | 200 | 15 | 0 | Statistics.PerfectRatings |
| perfect_10 | MiniGames | Skilled Hands | 10 | 1.000 | 75 | 0 | PerfectRatings |
| perfect_50 | MiniGames | Precision Master | 50 | 5.000 | 250 | 0 | PerfectRatings |
| streak_5 | MiniGames | On Fire! | 5 | 500 | 30 | 0 | Statistics.BestPerfectStreak |
| streak_10 | MiniGames | Unstoppable | 10 | 2.000 | 150 | 0 | BestPerfectStreak |
| games_100 | MiniGames | Mini-Game Veteran | 100 | 2.500 | 150 | 0 | Statistics.TotalMiniGamesPlayed |
| workshop_level10 | Workshops | Upgraded | 10 | 1.000 | 50 | 0 | maxWsLevel (höchstes Workshop-Level) |
| workshop_level25 | Workshops | Expert Facility | 25 | 5.000 | 150 | 0 | maxWsLevel |
| all_workshops | Workshops | Full House | 6 | 2.500 | 200 | 0 | UnlockedWorkshopTypes.Count |
| worker_first | Workshops | Team Builder | 1 | 100 | 10 | 0 | totalWorkers > 0 ? 1 : 0 |
| workers_10 | Workshops | Growing Team | 10 | 1.000 | 75 | 0 | totalWorkers |
| workers_25 | Workshops | Big Business | 25 | 5.000 | 250 | 15 | totalWorkers |
| workshop_level50 | Workshops | Maximum Power | 50 | 50.000 | 500 | 0 | maxWsLevel |
| workshop_level100 | Workshops | Century Workshop | 100 | 200.000 | 1.000 | 30 | maxWsLevel |
| workshop_level250 | Workshops | Elite Facility | 250 | 1.000.000 | 2.500 | 50 | maxWsLevel |
| workshop_level500 | Workshops | Legendary Workshop | 500 | 5.000.000 | 5.000 | 100 | maxWsLevel |
| workshop_level1000 | Workshops | Transcendent | 1000 | 50.000.000 | 12.500 | 250 | maxWsLevel |
| all_workshops_8 | Workshops | Complete Empire | 8 | 100.000 | 1.000 | 20 | (NICHT im Switch — kein Tracking) |
| events_survived_10 | Workshops | Weathered | 10 | 5.000 | 150 | 0 | (NICHT im Switch) |
| money_1k | Money | First Thousand | 1.000 | 100 | 10 | 0 | TotalMoneyEarned |
| money_10k | Money | Making Money | 10.000 | 500 | 25 | 0 | TotalMoneyEarned |
| money_100k | Money | Wealthy | 100.000 | 2.500 | 100 | 0 | TotalMoneyEarned |
| money_1m | Money | Millionaire | 1.000.000 | 10.000 | 250 | 15 | TotalMoneyEarned |
| money_10m | Money | Multi-Millionaire | 10.000.000 | 25.000 | 400 | 0 | TotalMoneyEarned |
| money_100m | Money | Mega Rich | 100.000.000 | 50.000 | 600 | 30 | TotalMoneyEarned |
| money_1b | Money | Billionaire | 1.000.000.000 | 100.000 | 1.000 | 0 | TotalMoneyEarned |
| money_10b | Money | Deca-Billionaire | 10.000.000.000 | 1.000.000 | 2.500 | 100 | TotalMoneyEarned |
| play_1h | Time | Dedicated | 3600 | 250 | 20 | 0 | Statistics.TotalPlayTimeSeconds |
| play_10h | Time | Committed | 36000 | 2.500 | 150 | 0 | TotalPlayTimeSeconds |
| daily_7 | Time | Week Warrior | 7 | 1.000 | 50 | 0 | DailyRewardStreak |
| level_10 | Special | Rising Star | 10 | 2.000 | 0 | 0 | PlayerLevel |
| level_25 | Special | Experienced | 25 | 10.000 | 0 | 5 | PlayerLevel |
| level_50 | Special | Veteran | 50 | 25.000 | 0 | 10 | PlayerLevel |
| level_100 | Special | Centurion | 100 | 100.000 | 0 | 30 | PlayerLevel |
| level_250 | Special | Elite Player | 250 | 500.000 | 0 | 50 | PlayerLevel |
| level_500 | Special | Grandmaster | 500 | 5.000.000 | 0 | 100 | PlayerLevel |
| level_1000 | Special | Immortal | 1000 | 50.000.000 | 0 | 250 | PlayerLevel |
| prestige_1 | Prestige | New Beginning | 1 | 5.000 | 250 | 0 | Prestige.TotalPrestigeCount |
| worker_a_tier | Workers | Elite Recruitment | 1 | 10.000 | 200 | 0 | (NICHT im Switch) |
| workers_max_level | Workers | Master Workers | 10 | 25.000 | 400 | 0 | (NICHT im Switch) |
| worker_loyal | Workers | Loyal Employee | 100 | 15.000 | 300 | 0 | (NICHT im Switch) |
| worker_specialist | Workers | Perfect Match | 1 | 500 | 15 | 0 | (NICHT im Switch) |
| workers_total_50 | Workers | HR Manager | 50 | 20.000 | 350 | 0 | (NICHT im Switch) |
| worker_s_tier | Workers | Legend Found | 1 | 50.000 | 500 | 0 | (NICHT im Switch) |
| worker_ss_tier | Workers | SS-Tier Recruit | 1 | 200.000 | 1.000 | 30 | hasSS ? 1 : 0 (Worker-Tier ≥ SS) |
| worker_sss_tier | Workers | SSS-Tier Recruit | 1 | 1.000.000 | 2.500 | 50 | hasSSS ? 1 : 0 |
| worker_legendary | Workers | Legendary Recruit | 1 | 10.000.000 | 5.000 | 100 | hasLegendary ? 1 : 0 |
| building_first | Buildings | Developer | 1 | 2.000 | 25 | 0 | builtCount > 0 ? 1 : 0 |
| building_all | Buildings | Real Estate Mogul | 7 | 50.000 | 500 | 0 | builtCount |
| building_max | Buildings | Fully Upgraded | 5 | 20.000 | 250 | 0 | maxBldLevel |
| canteen_built | Buildings | Happy Workers | 1 | 5.000 | 50 | 0 | hasCanteen ? 1 : 0 |
| training_center | Buildings | Academy | 1 | 5.000 | 50 | 0 | hasTraining ? 1 : 0 |
| research_first | Research | Scientist | 1 | 2.000 | 25 | 0 | (NICHT im Switch) |
| research_branch | Research | Expert | 15 | 100.000 | 1.000 | 0 | (NICHT im Switch) |
| research_all | Research | Genius | 45 | 500.000 | 3.000 | 0 | (NICHT im Switch; Desc "all 45 researches" — veraltet, real 72 Nodes) |
| research_tools5 | Research | Tool Master | 5 | 10.000 | 100 | 0 | (NICHT im Switch) |
| research_mgmt5 | Research | Manager | 5 | 10.000 | 100 | 0 | (NICHT im Switch) |
| reputation_70 | Reputation | Well Known | 70 | 10.000 | 150 | 0 | (NICHT im Switch) |
| reputation_90 | Reputation | Famous | 90 | 25.000 | 300 | 0 | (NICHT im Switch) |
| reputation_100 | Reputation | Legendary | 100 | 100.000 | 750 | 0 | (NICHT im Switch) |
| regular_10 | Reputation | Popular Choice | 10 | 15.000 | 250 | 0 | (NICHT im Switch) |
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
| perfect_100 | MiniGames | Perfection Master | 100 | 50.000 | 1.000 | 30 | Statistics.PerfectRatings |
| games_500 | MiniGames | Mini-Game Legend | 500 | 100.000 | 2.000 | 50 | TotalMiniGamesPlayed |
| all_minigames_perfect | MiniGames | Universal Talent | 8 | 200.000 | 3.000 | 75 | PerfectMiniGameTypes.Count |
| all_ws_level100 | Workshops | Full Power | 8 | 5.000.000 | 5.000 | 100 | wsLevel100Count (Workshops ≥ Lv100) |
| guild_founder | Guilds | Guild Founder | 1 | 10.000 | 250 | 20 | GuildMembership != null ? 1 : 0 |
| guild_member | Guilds | Team Player | 1 | 5.000 | 100 | 0 | GuildMembership != null ? 1 : 0 |
| guild_weekly_goal | Guilds | Team Effort | 1 | 25.000 | 500 | 25 | GuildMembership.GuildLevel > 1 ? 1 : 0 |
| guild_level_10 | Guilds | Legendary Guild | 10 | 100.000 | 1.500 | 50 | GuildMembership.GuildLevel |
| workers_trained_50 | Workers | Training Master | 50 | 50.000 | 750 | 25 | Statistics.TotalWorkersTrained |
| crafting_100 | Crafting | Mass Producer | 100 | 100.000 | 1.500 | 30 | Statistics.TotalItemsCrafted |
| all_recipes | Crafting | Recipe Master | 13 | 200.000 | 2.500 | 50 | CompletedRecipeIds.Count (Desc "all 13 recipes" — real 30 Rezepte) |
| tournament_gold | Tournaments | Tournament Champion | 1 | 25.000 | 500 | 20 | TotalTournamentsWon > 0 ? 1 : 0 |
| tournaments_10 | Tournaments | Serial Winner | 10 | 100.000 | 2.000 | 50 | TotalTournamentsWon |
| all_mastertools | Collection | Tool Collector | 12 | 500.000 | 3.000 | 75 | CollectedMasterTools.Count |
| equipment_all_rarities | Collection | Rarity Collector | 4 | 100.000 | 1.500 | 30 | distinctRarities (Bitflags über EquipmentInventory.Rarity) |
| asc_first | Ascension | First Ascension | 1 | 500.000 | 5.000 | 100 | Ascension.AscensionLevel |
| asc_5 | Ascension | Master Ascender | 5 | 5.000.000 | 12.500 | 250 | AscensionLevel |
| asc_10 | Ascension | Transcendence | 10 | 50.000.000 | 25.000 | 500 | AscensionLevel |
| asc_perk_first | Ascension | Perk Enthusiast | 1 | 100.000 | 2.500 | 50 | Ascension.Perks.Count > 0 ? 1 : 0 |
| asc_perks_max | Ascension | Fully Upgraded | 6 | 100.000.000 | 50.000 | 1.000 | CountMaxedPerks (Perks auf MaxLevel) |
| rebirth_first | Rebirth | First Star | 1 | 1.000.000 | 5.000 | 100 | totalRebirthStars > 0 ? 1 : 0 |
| rebirth_stars_10 | Rebirth | Star Collector | 10 | 10.000.000 | 15.000 | 300 | totalRebirthStars |
| rebirth_ws_5stars | Rebirth | Perfection | 5 | 25.000.000 | 20.000 | 500 | maxRebirthStars (höchste WS-Sterne) |
| rebirth_all_ws | Rebirth | Galaxy | 8 | 50.000.000 | 25.000 | 750 | wsWithAtLeast1Star |
| all_ws_level1000 | Workshops | On the Summit | 8 | 100.000.000 | 50.000 | 1.000 | wsLevel1000Count (Workshops ≥ Lv1000) |
| auto_craft_first | Crafting | First Prototype | 1 | 1.000 | 500 | 5 | Statistics.TotalItemsAutoProduced |
| auto_craft_100 | Crafting | Serial Production | 100 | 25.000 | 2.500 | 25 | TotalItemsAutoProduced |
| auto_craft_1000 | Crafting | Mass Production | 1000 | 250.000 | 10.000 | 100 | TotalItemsAutoProduced |
| material_order_10 | Orders | Material Master | 10 | 50.000 | 5.000 | 50 | Statistics.TotalMaterialOrdersCompleted |
| craft_tier3_first | Crafting | Masterpiece | 1 | 100.000 | 5.000 | 50 | CountTier3Items (Inventar/abgeschl. T3-Rezept) |

(Hinweis: Zeilenanzahl = 109 Achievements, lückenlos.)

### 2.4 Achievement-Mechaniken (Service)

Quelle: `Services/AchievementService.cs`

- **GetAll()** wird im Ctor geladen, IsUnlocked aus `State.UnlockedAchievements` rehydriert.
- **CheckAchievements()**: ruft UpdateProgress, dann für jedes nicht-unlocked Achievement `CurrentValue >= TargetValue` → UnlockAchievement.
- **UnlockAchievement()**: IsUnlocked=true, UnlockedAt=UtcNow, CurrentValue=TargetValue, in `State.UnlockedAchievements` schreiben. Belohnungen: `AddMoney(MoneyReward)`, `AddXp(XpReward)`, `AddGoldenScrews(GoldenScrewReward)`. Feuert Event `AchievementUnlocked`. Analytics-Event `AchievementUnlocked` (id, category, xp_reward, screw_reward).
- **BoostAchievement(id, boostPercent)** (Rewarded-Ad): nur wenn nicht unlocked && nicht HasUsedAdBoost. `boost = (long)(TargetValue * boostPercent)` (min 1). CurrentValue += boost, HasUsedAdBoost=true. Wenn dadurch ≥ Target → Unlock. (App-CLAUDE.md: Placement `achievement_boost` = +20%, nur bei TargetValue > 5.)
- **Auto-Tracking Event-Subscriptions**: OrderCompleted, LevelUp, WorkerHired, WorkshopUpgraded → CheckAchievements. MoneyChanged → debounced (nur alle 30 s bei Zuwachs, `MoneyCheckIntervalSeconds = 30`). PrestigeCompleted/AscensionCompleted/RebirthCompleted → CheckAchievements.
- **CountTier3Items**: summiert CraftingInventory aller T3-Produkte; falls 0 aber CompletedRecipeIds enthält ein T3-Rezept → 1.
- **CountMaxedPerks**: zählt Ascension-Perks mit Level ≥ MaxLevel.
- **GetAllAchievements()**: sortiert nach IsUnlocked desc, Progress desc, Category asc.
- **GetUnlockedAchievements()**: nach UnlockedAt desc.
- Reset bei StateLoaded.

---

## 3. GoalService (dynamisches Nächstes-Ziel)

Quelle: `Services/GoalService.cs`, `Models/GameGoal.cs`

### 3.1 GameGoal-Modell

Felder (alle init): Description, RewardHint, Progress (0.0–1.0), NavigationRoute, IconKind (Material-Icon-Name, default "TrendingUp"), Priority (niedrigere Zahl = höhere Prio).

`GetCurrentGoal()` cacht das Ergebnis (Dirty-Flag), invalidiert bei StateLoaded. Logik: `CalculateBestGoal()` — gibt das erste passende Ziel der höchsten Prio zurück.

### 3.2 Prioritätskette (0 = höchste)

Quelle: `CalculateBestGoal()`. Verarbeitung in Reihenfolge; bei Treffer mit `Priority <= n` wird sofort zurückgegeben.

| Prio | Methode/Block | Bedingung | Beschreibung-Format | Route | IconKind |
|------|---------------|-----------|---------------------|-------|----------|
| 0 | FindBeginnerGoal | PlayerLevel < 10 && TotalPrestigeCount == 0 | siehe 3.3 | dashboard | (variiert) |
| 1 | Workshop-Meilenstein nahe | unlocked WS, Meilenstein 1–5 Level entfernt | `{wsName} → Lv.{milestone}` | dashboard | TrendingUp |
| 2 | Prestige verfügbar | GetHighestAvailableTier != None | "Prestige available!" / `+{tierPoints} PP` | prestige | StarFourPoints |
| 3 | Nächster Workshop freischaltbar | günstigster Locked, `remaining > 0 && remaining < Money*5` | `{wsName} freischalten` | dashboard | LockOpenVariant |
| 4 | Gebäude-Upgrade | PlayerLevel ≥ 5, Gebäude IsBuilt, Level < 5, `Money ≥ NextLevelCost*0.5` | `{bName} → Lv.{Level+1}` | imperium | HomeCity |
| 5 | Nächster Worker-Tier | nextTier > höchster WorkerTier, PlayerLevel ≥ Tier-Unlock, `Money ≥ hiringCost*0.3 && Money < hiringCost*3` | `{tierName}-Worker` | workers | AccountArrowUp |
| 6 | FindWorkshopRebirthGoal | WS Level ≥ 995 && Sterne < 5 | `{wsName} ready for Rebirth!` / `★{stars+1} (+{bonus})` | dashboard | StarShooting |
| 7 | FindAscensionGoal | `_ascensionService.CanAscend` | "Ascension available!" / `+{ap} AP` | prestige | ArrowUpBoldCircle |
| 8 | FindAllWorkshopsMaxGoal | atMax ≥ 6 && atMax < totalUnlocked | `All workshops at Lv.1000 ({atMax}/8)` | dashboard | ChartBar |
| 9 | FindNextRebirthStarGoal | WS Level ≥ 900 && Sterne < 5 (höchster) | `{wsName}: {remaining} more levels until next star` | dashboard | Star |
| 10 | FindStretchGoal (Variante A) | 0 < totalStars < maxPossibleStars | `Collect Rebirth stars ({totalStars}/{maxPossibleStars})` | dashboard | StarCircle |
| 10 | FindStretchGoal (Variante B) | niedrigster WS Level < MaxLevel | `{wsName} auf Level {nextHundred} bringen` | dashboard | TrendingUp |

**Workshop-Meilensteine (Prio 1)**: Liste `[25, 50, 75, 100, 150, 200, 225, 250, 350, 500, 1000]`. Trigger wenn `diff = milestone - Level` in `(0, 5]`. RewardHint: `x{GetMilestoneMultiplierForLevel(milestone)} Income Boost!`.

**Rebirth-Bonus-Text** (`GetRebirthBonusText`): 1★ +15%, 2★ +35%, 3★ +60%, 4★ +100%, 5★ +150%.

### 3.3 Anfänger-Ziele (Prio 0, `FindBeginnerGoal`)

Nur aktiv wenn PlayerLevel < 10 && TotalPrestigeCount == 0. Erstes zutreffendes Ziel:

| Reihenfolge | Bedingung | Description | RewardHint | IconKind |
|-------------|-----------|-------------|------------|----------|
| 1 | firstWorkshop.Level ≤ 1 && TotalMoneySpent == 0 | `{wsName} upgraden` | "+Einkommen!" | ArrowUpBold |
| 2 | TotalOrdersCompleted == 0 | "Accept first order" | "Earn money!" | ClipboardText |
| 3 | firstWorkshop.Level < 10 | `{Workshop} → Lv.10` | "New features!" | TrendingUp |
| 4 (Fallback) | sonst | `Reach Level {targetLevel}` (5 falls Level<5, sonst 10) | "New features!" | StarFourPoints |

Ziel-4-Progress: `xpForTarget = CalculateXpForLevel(targetLevel)`; `progress = Min(1, TotalXp / xpForTarget)`.

### 3.4 Hilfs-Methoden-Details

- **FindAllWorkshopsMaxGoal**: nur wenn `atMax >= 6 && atMax < totalUnlocked && totalUnlocked != 0`. Progress = `atMax/totalUnlocked`.
- **FindNextRebirthStarGoal**: höchster Level-Kandidat ≥ 900 mit Sternen < 5; `remaining = Workshop.MaxLevel - Level`.
- **FindStretchGoal**: zählt totalStars + `maxPossibleStars (= 5 pro unlocked WS)`. Variante A wenn Sterne sammelbar. Variante B: `nextHundred = ((Level/100)+1)*100`, geclampt auf MaxLevel.

---

## 4. Story-Chapters (Meister Hans)

Quelle: `Services/StoryService.cs`, `Models/StoryChapter.cs`, `Services/SeasonStorylineCatalog.cs`, `Models/SeasonStoryline.cs`

### 4.1 Übersicht

- **Master-Liste: 40 Kapitel** in `StoryService.CreateChapters()` (ChapterNumber 1–40).
- **+20 Saison-Kapitel** aus `SeasonStorylineCatalog.GetAllSeasonChapters()` (ChapterNumber 100–119, 4 Saisons × 5).
- **Gesamt: 60 Kapitel** (`_chapters.Count`). Der Klassen-Kommentar von StoryService spricht von "37 Kapitel" — das ist veraltet (real 40 Hauptkapitel + 20 Saison = 60).

### 4.2 StoryChapter-Modell

Felder (init): Id, ChapterNumber, TitleKey, TextKey, TitleFallback, TextFallback, Mood (happy/proud/concerned/excited), IsTutorial.
Belohnungen: MoneyReward (decimal), GoldenScrewReward (int), XpReward (int).
Bedingungen (alle gesetzten müssen erfüllt sein): RequiredPlayerLevel, RequiredWorkshopCount, RequiredTotalOrders, RequiredPrestige, RequiredQuickJobsCompleted, RequiredBattlePassTier, RequiredSeasonTheme (Season?), RequiredPrestigeTier, RequiredAscensionLevel.

### 4.3 Freischalt-Logik (`IsChapterUnlocked`)

1. **FTUE-vs-tutorial_welcome-Konflikt-Fix**: Tutorial-Kapitel (`IsTutorial == true`) werden erst freigeschaltet, wenn `state.Tutorial.Ftue.IsCompleted == true`. Begründung im Code: Vorher konkurrierten ftue_welcome (Meister Hans) und tutorial_welcome (Meister Hans) am ersten Start parallel — beide mit identischer Persona "Weiter", Onboarding wirkte redundant. Nicht-Tutorial-Kapitel laufen unabhängig.
2. Danach: alle gesetzten Required-Felder prüfen (PlayerLevel, WorkshopCount via `UnlockedWorkshopTypes.Count`, TotalOrders, Prestige via `TotalPrestigeCount`, PrestigeTier via `(int)CurrentTier`, AscensionLevel, QuickJobsCompleted via `TotalQuickJobsCompleted`, BattlePassTier via `BattlePass.CurrentTier`, SeasonTheme via `BattlePass.SeasonTheme`).

`CheckForNewChapter()`: durchläuft alle Kapitel, überspringt bereits gesehene (`ViewedStoryIds`). Setzt `PendingStoryId` auf das erste ungesehene + freigeschaltete Kapitel. Nichtlineare Progression möglich (Kapitel überspringbar).

### 4.4 Belohnungs-Auszahlung (`MarkChapterViewed`)

Nur beim ERSTEN Mal (Race-Condition-Schutz über `ViewedStoryIds`):
- **MoneyReward**: `scaledReward = Max(chapter.MoneyReward, NetIncomePerSecond * 600)` (mind. ~10 Min Einkommen).
- GoldenScrewReward → `AddGoldenScrews`.
- XpReward → `AddXp`.
- PendingStoryId zurücksetzen.

### 4.5 Haupt-Kapitel 1–40

Spalten: Nr | Id | Mood | IsTutorial | Bedingungen | Money | GS | XP

| Nr | Id | Mood | Tut | Bedingungen | Money | GS | XP |
|----|----|------|-----|-------------|-------|----|----|
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

TitleKeys/TextKeys folgen `Story_Ch{NN}_Title`/`_Text` (Kapitel 1–12 mit Ch01–Ch12, 13/14 = Ch12a/Ch12b, 15–37 = Ch13–Ch35; Kapitel 38–40 = `Story_ResWarehouse_*` / `Story_ResSupplyChain_*` / `Story_ResLogistics_*`). TitleFallback/TextFallback siehe `StoryService.cs` (vollständige deutsche Texte hinterlegt).

WICHTIG Kapitel 38–40 sind `IsTutorial = true` → werden erst nach FTUE-Abschluss freigeschaltet (gleiche Gate-Regel wie Kapitel 1–5).

### 4.6 Saison-Kapitel (ChapterNumber 100–119)

Quelle: `SeasonStorylineCatalog`. Pro Saison 5 Kapitel an BP-Tier-Trigger [1, 10, 25, 40, 50]. Alle `IsTutorial = false`, `RequiredSeasonTheme` gesetzt. TitleKey/TextKey-Schema: `SeasonStory_{Theme}_Ch{n}_Title/_Text`.

**SeasonStoryline-Definitionen** (`Storylines`-Dictionary, ThemeKey + ChapterIds + TierTriggers):

| Season | ThemeKey | ChapterIds | TierTriggers |
|--------|----------|------------|--------------|
| Spring | SeasonStorySpringTheme | season_spring_ch1..ch5 | 1, 10, 25, 40, 50 |
| Summer | SeasonStorySummerTheme | season_summer_ch1..ch5 | 1, 10, 25, 40, 50 |
| Autumn | SeasonStoryAutumnTheme | season_autumn_ch1..ch5 | 1, 10, 25, 40, 50 |
| Winter | SeasonStoryWinterTheme | season_winter_ch1..ch5 | 1, 10, 25, 40, 50 |

**Spring — "Der Aufschwung der Stadt"**

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|------|------|-------|----|----|
| 100 | season_spring_ch1 | 1 | excited | 100.000 | 5 | 200 |
| 101 | season_spring_ch2 | 10 | proud | 1.000.000 | 15 | 750 |
| 102 | season_spring_ch3 | 25 | concerned | 5.000.000 | 25 | 1.500 |
| 103 | season_spring_ch4 | 40 | excited | 25.000.000 | 50 | 3.000 |
| 104 | season_spring_ch5 | 50 | proud | 100.000.000 | 100 | 7.500 |

**Summer — "Der Insel-Auftrag"**

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|------|------|-------|----|----|
| 105 | season_summer_ch1 | 1 | excited | 500.000 | 8 | 300 |
| 106 | season_summer_ch2 | 10 | proud | 2.500.000 | 18 | 1.000 |
| 107 | season_summer_ch3 | 25 | concerned | 7.500.000 | 30 | 2.000 |
| 108 | season_summer_ch4 | 40 | excited | 35.000.000 | 60 | 3.500 |
| 109 | season_summer_ch5 | 50 | proud | 150.000.000 | 120 | 8.500 |

**Autumn — "Wettbewerb der Innungen"**

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|------|------|-------|----|----|
| 110 | season_autumn_ch1 | 1 | excited | 700.000 | 10 | 350 |
| 111 | season_autumn_ch2 | 10 | proud | 3.500.000 | 22 | 1.200 |
| 112 | season_autumn_ch3 | 25 | proud | 12.000.000 | 38 | 2.400 |
| 113 | season_autumn_ch4 | 40 | concerned | 50.000.000 | 80 | 4.500 |
| 114 | season_autumn_ch5 | 50 | excited | 200.000.000 | 150 | 10.000 |

**Winter — "Der Sturm-Notdienst"**

| Nr | Id | Tier | Mood | Money | GS | XP |
|----|----|------|------|-------|----|----|
| 115 | season_winter_ch1 | 1 | concerned | 600.000 | 9 | 320 |
| 116 | season_winter_ch2 | 10 | concerned | 3.000.000 | 20 | 1.100 |
| 117 | season_winter_ch3 | 25 | concerned | 10.000.000 | 35 | 2.200 |
| 118 | season_winter_ch4 | 40 | proud | 40.000.000 | 70 | 4.000 |
| 119 | season_winter_ch5 | 50 | excited | 180.000.000 | 130 | 9.000 |

Vollständige Fallback-Texte (DE) je Kapitel: siehe `SeasonStorylineCatalog.cs`.

---

## 5. FTUE (First-Time User Experience)

Quelle: `Services/FtueService.cs`, `Services/FtueProgressTracker.cs`, `Models/FtueStep.cs`, `Models/TutorialState.cs`

### 5.1 Schritt-Anzahl + Sequenz

**10 Schritte** (`s_defaultSteps`), Reihenfolge stabil nach `Order` 0–9.

| Order | Id | TitleKey | TextKey | ExpectedAction | SpotlightAutomationId | CanSkip |
|-------|----|----------|---------|----------------|----------------------|---------|
| 0 | ftue_welcome | FtueWelcomeTitle | FtueWelcomeText | TapContinue | (keine) | **false** |
| 1 | ftue_first_upgrade | FtueFirstUpgradeTitle | FtueFirstUpgradeText | BuyFirstUpgrade | Workshop_Btn_Upgrade | **false** |
| 2 | ftue_first_order | FtueFirstOrderTitle | FtueFirstOrderText | AcceptFirstOrder | Dashboard_Items_Orders | true |
| 3 | ftue_first_minigame | FtueFirstMiniGameTitle | FtueFirstMiniGameText | CompleteFirstMiniGame | (keine) | true |
| 4 | ftue_money_explained | FtueMoneyExplainedTitle | FtueMoneyExplainedText | TapContinue | Dashboard_Txt_Money | true |
| 5 | ftue_first_worker | FtueFirstWorkerTitle | FtueFirstWorkerText | HireFirstWorker | Workshop_Btn_HireWorker | true |
| 6 | ftue_xp_explained | FtueXpExplainedTitle | FtueXpExplainedText | ReachLevel2 | Dashboard_Txt_PlayerLevel | true |
| 7 | ftue_screws_explained | FtueScrewsExplainedTitle | FtueScrewsExplainedText | TapContinue | Dashboard_Btn_GoldenScrews | true |
| 8 | ftue_imperium_intro | FtueImperiumIntroTitle | FtueImperiumIntroText | TapContinue | Main_Canvas_TabBar | true |
| 9 | ftue_complete | FtueCompleteTitle | FtueCompleteText | TapContinue | (keine) | true |

**Skip-Block**: Schritt 0 + 1 sind NICHT skippbar (CanSkip = false) — erst nachdem der Core-Loop sichtbar wird, darf der Spieler eine informierte Skip-Entscheidung treffen.

### 5.2 FtueExpectedAction (Enum)

Quelle: `Models/FtueStep.cs`: TapAnywhere, TapSpotlight, BuyFirstUpgrade, AcceptFirstOrder, HireFirstWorker, CompleteFirstMiniGame, ReachLevel2, TapContinue.

### 5.3 FtueState (persistiert in GameState.Tutorial.Ftue)

Felder: CurrentStepIndex (-1 = nicht gestartet), IsCompleted, WasSkipped, CompletedStepIds (HashSet), StartedAtIso, CompletedAtIso. Default-Init reicht (keine SaveGame-Migration nötig).

### 5.4 State-Machine (`FtueService`)

- **IsActive**: `CurrentStepIndex >= 0 && !IsCompleted && !WasSkipped`.
- **CurrentStep**: s_defaultSteps[CurrentStepIndex] wenn IsActive.
- **Start()**: idempotent. No-op wenn IsCompleted/WasSkipped/bereits gestartet. Setzt CurrentStepIndex=0, StartedAtIso=UtcNow("O"). Feuert Analytics `ftue_started` + CurrentStepChanged.
- **CompleteCurrentStep()**: fügt step.Id zu CompletedStepIds, Analytics `ftue_step_completed`, dann AdvanceToNextUncompletedStep.
- **OnPlayerAction(action)** (Catch-Up-Pass): markiert ALLE Steps mit dieser ExpectedAction als completed (auch nachgelagerte, falls Bedingung schon erfüllt). Wenn dadurch der aktuelle Step abgehakt wird → AdvanceToNextUncompletedStep.
- **AdvanceToNextUncompletedStep()**: springt zum nächsten Step der NOCH NICHT in CompletedStepIds steht (überspringt per Catch-Up bereits abgehakte). Wenn keiner mehr → IsCompleted=true, CompletedAtIso=UtcNow, CurrentStepIndex=-1, CurrentStepChanged(null), FtueFinished, Analytics `ftue_completed` (steps_completed).
- **SkipAll()**: WasSkipped=true, CompletedAtIso=UtcNow, CurrentStepIndex=-1. Analytics `ftue_skipped` (last_step_index, steps_completed). Feuert CurrentStepChanged(null) + FtueFinished.
- **Events**: CurrentStepChanged (EventHandler<FtueStep?>), FtueFinished.
- **Telemetrie-Property-Keys**: step_id, step_order.

### 5.5 FtueProgressTracker (Event-Verdrahtung)

Quelle: `Services/FtueProgressTracker.cs`. Singleton, IDisposable. Ohne diesen Tracker schreitet die FTUE nie voran (OnPlayerAction wird sonst nirgendwo gerufen).

Event-Mapping (GameStateService-Events → OnPlayerAction):
- WorkshopUpgraded → BuyFirstUpgrade
- WorkerHired → HireFirstWorker
- OrderStarted → AcceptFirstOrder
- OrderCompleted → CompleteFirstMiniGame **nur wenn** `e.Order.Tasks.Count > 0` (MaterialOrders ohne MiniGame triggern nicht)
- LevelUp → ReachLevel2 **nur wenn** `e.NewLevel >= 2`

**StartIfNeeded()**: vom `IGameStartupCoordinator` nach Spielstand-Laden aufgerufen. No-op wenn IsCompleted/WasSkipped, sonst `_ftueService.Start()` (selbst idempotent → Re-Start nach App-Restart mitten in FTUE möglich).

### 5.6 Spotlight-Overlay-Status

Im Code-Kommentar: das Daten-Modell + State-Machine + Analytics-Hooks sind vorhanden; das visuelle Spotlight-Overlay-Rendering ist als "Folge-Sprint" markiert. Die SpotlightAutomationIds zeigen auf real in den Views vorhandene Elemente (siehe Tabelle 5.1).

---

## 6. ContextualHints (alle 32)

Quelle: `Models/ContextualHint.cs` (`ContextualHints`-Klasse), `Services/ContextualHintService.cs`

### 6.1 Service-Mechanik

Quelle: `ContextualHintService`. Tracking via `GameState.Tutorial.SeenHints` (HashSet, JSON-persistiert).
- **TryShowHint(hint)**: false wenn bereits gesehen (`HasSeenHint`) ODER ein anderer Hint aktiv ist. Sonst ActiveHint=hint, feuert `HintChanged`, true.
- **DismissHint()**: markiert ActiveHint.Id als gesehen (SeenHints.Add), setzt ActiveHint=null, feuert HintChanged(null).
- **HasSeenHint(id)**: SeenHints.Contains.
- **ResetAllHints()**: SeenHints.Clear + SeenMiniGameTutorials.Clear + HasSeenTutorialHint=false.
- **Event**: HintChanged (EventHandler<ContextualHint?>).

### 6.2 ContextualHint-Modell

Felder (init): Id, TitleKey, TextKey, Position (HintPosition: Above/Below, default Below), IsDialog (bool — true = zentrierter Dialog statt Tooltip-Bubble).

### 6.3 Alle 32 Hints

Trigger-Kontexte aus Code-Kommentaren. Position/IsDialog wie im Code definiert.

| # | Feld (statisch) | Id | TitleKey | TextKey | Position | IsDialog | Trigger (aus Kommentar) |
|---|-----------------|----|----------|---------|----------|----------|-------------------------|
| 1 | Welcome | welcome | HintWelcomeTitle | HintWelcomeText | Below | **ja** | Zentrierter Dialog, allererster Start |
| 2 | FirstWorkshop | first_workshop | HintFirstWorkshopTitle | HintFirstWorkshopText | Below | nein | Erste Werkstatt erkunden |
| 3 | WorkshopDetail | workshop_detail | HintWorkshopDetailTitle | HintWorkshopDetailText | Above | nein | Werkstatt-Detail: Upgrade |
| 4 | FirstOrder | first_order | HintFirstOrderTitle | HintFirstOrderText | Above | nein | Erster Auftrag annehmen |
| 5 | OrderCompleted | order_completed | HintOrderCompletedTitle | HintOrderCompletedText | Below | nein | Erster Auftrag abgeschlossen |
| 6 | WorkerUnlock | worker_unlock | HintWorkerUnlockTitle | HintWorkerUnlockText | Below | nein | Mitarbeiter freigeschaltet (Level 3) |
| 7 | ShopHint | shop_hint | HintShopTitle | HintShopText | Above | nein | Shop-Tab (erster Besuch) |
| 8 | ResearchHint | research_hint | HintResearchTitle | HintResearchText | Below | nein | Forschung (erster Research-Tab-Besuch) |
| 9 | BuildingHint | building_hint | HintBuildingTitle | HintBuildingText | Below | nein | Gebäude (erster Buildings-Tab-Besuch) |
| 10 | DailyChallenge | daily_challenge | HintDailyChallengeTitle | HintDailyChallengeText | Below | nein | Tägliche Herausforderungen (erster Missionen-Tab-Besuch) |
| 11 | QuickJobs | quick_jobs | HintQuickJobsTitle | HintQuickJobsText | Below | nein | Quick Jobs (Level 2 / erster QuickJobs-Tab-Besuch, ab v2.0.36) |
| 12 | PrestigeHint | prestige_hint | HintPrestigeTitle | HintPrestigeText | Below | nein | Prestige verfügbar (Level 50) |
| 13 | GuildHint | guild_hint | HintGuildTitle | HintGuildText | Below | nein | Gilden (erster Guild-Tab-Besuch) |
| 14 | CraftingHint | crafting_hint | HintCraftingTitle | HintCraftingText | Below | nein | Crafting freigeschaltet |
| 15 | BattlePass | battle_pass | HintBattlePassTitle | HintBattlePassText | Below | nein | Battle Pass verfügbar |
| 16 | LuckySpin | lucky_spin | HintLuckySpinTitle | HintLuckySpinText | Below | nein | Glücksrad (Tag 2) |
| 17 | Automation | automation | HintAutomationTitle | HintAutomationText | Below | nein | Automatisierung freigeschaltet (Level 15) |
| 18 | ManagerUnlock | manager_unlock | HintManagerUnlockTitle | HintManagerUnlockText | Below | nein | Vorarbeiter freigeschaltet (Level 10) |
| 19 | MasterToolsUnlock | master_tools_unlock | HintMasterToolsUnlockTitle | HintMasterToolsUnlockText | Below | nein | Meisterwerkzeuge freigeschaltet (Level 20) |
| 20 | AscensionAvailable | ascension_available | HintAscensionAvailableTitle | HintAscensionAvailableText | Below | **ja** | Ascension verfügbar (erstmals CanAscend, nach 3x Legende) |
| 21 | AscensionPath | ascension_path | HintAscensionPathTitle | HintAscensionPathText | Below | **ja** | Foreshadowing nach 1. Prestige (erklärt Ascension-Konzept) |
| 22 | RebirthReady | rebirth_ready | HintRebirthReadyTitle | HintRebirthReadyText | Below | **ja** | Rebirth bereit (erster Workshop Level 1000) |
| 23 | FirstStar | first_star | HintFirstStarTitle | HintFirstStarText | Below | **ja** | Erster Stern (nach erstem Rebirth) |
| 24 | GoldenScrews | golden_screws | HintGoldenScrewsTitle | HintGoldenScrewsText | Below | **ja** | Goldschrauben erklärt (erster Erhalt Premium-Währung) |
| 25 | AcceptOrder | accept_order | HintAcceptOrderTitle | HintAcceptOrderText | Below | nein | Aufträge erklärt (nach FirstWorkshop-Hint) |
| 26 | OrderTypes | order_types | HintOrderTypesTitle | HintOrderTypesText | Below | **ja** | ONB-1: Auftragstypen |
| 27 | ReputationHint | reputation_hint | HintReputationTitle | HintReputationText | Below | **ja** | ONB-2: Reputation |
| 28 | LongPressBulk | long_press_bulk | HintLongPressBulkTitle | HintLongPressBulkText | Below | nein | Nach 2. erfolgreichem Workshop-Upgrade (Long-Press Bulk x10/x100) |
| 29 | MaterialOffer | material_offer | HintMaterialOfferTitle | HintMaterialOfferText | Below | **ja** | Erstes gerolltes Material-Angebot (ab Level ≥ MaterialOfferUnlockLevel = 30) |
| 30 | MultiTaskOrder | multi_task_order | HintMultiTaskOrderTitle | HintMultiTaskOrderText | Below | **ja** | Erster Multi-Task-Auftrag (Rating-DURCHSCHNITT zählt) |
| 31 | CrossWorkshopComing | cross_workshop_coming | HintCrossWorkshopComingTitle | HintCrossWorkshopComingText | Below | **ja** | Bei Lv 99 (ab Lv 100 Cross-Workshop-Lieferketten) |
| 32 | Tier4Coming | tier4_coming | HintTier4ComingTitle | HintTier4ComingText | Below | **ja** | WS-Lv 450 oder logi_08-Abschluss (T4 naht) |

**IsDialog = true bei 12 Hints**: welcome, ascension_available, ascension_path, rebirth_ready, first_star, golden_screws, order_types, reputation_hint, material_offer, multi_task_order, cross_workshop_coming, tier4_coming.
**Position Above bei 2 Hints**: workshop_detail, first_order. Alle anderen Below.

---

## 7. Mini-Games (13 Typen) + Mastery

Quelle: `Models/Enums/MiniGameType.cs`, `Models/Enums/MiniGameMasteryTier.cs`, `Services/MiniGameMasteryService.cs`, `Services/MiniGameNavigator.cs`

### 7.1 13 MiniGame-Typen (Enum-Werte + Route + Workshop)

Quelle: `MiniGameType` (Enum), `MiniGameTypeExtensions`.

| Enum-Wert | Name | LocalizationKey | Route (GetRoute) | Workshop-Typen | Beschreibung |
|-----------|------|-----------------|------------------|----------------|--------------|
| 0 | Sawing | Sawing | minigame/sawing | Carpenter | Timing: Marker in grüner Zone stoppen |
| 1 | Planing | Planing | minigame/sawing (geteilt) | Carpenter | Timing: gleichmäßige Hobelbewegung |
| 2 | PipePuzzle | PipePuzzle | minigame/pipes | Plumber | Puzzle: Rohre verbinden |
| 3 | WiringGame | WiringGame | minigame/wiring | Electrician | Drag&Drop: Kabelfarben zuordnen |
| 4 | PaintingGame | PaintingGame | minigame/painting | Painter | Swipe: malen ohne über Kanten |
| 5 | TileLaying | TileLaying | minigame/sawing (geteilt) | Roofer | Timing: Fliesen platzieren |
| 6 | Measuring | Measuring | minigame/sawing (geteilt) | Contractor, Carpenter | Timing: exakt messen/schneiden |
| 7 | RoofTiling | RoofTiling | minigame/rooftiling | Roofer | Pattern: Dachziegel im Muster |
| 8 | Blueprint | Blueprint | minigame/blueprint | Contractor | Memory: Bauschritte in Reihenfolge |
| 9 | DesignPuzzle | DesignPuzzle | minigame/designpuzzle | Architect | Puzzle: Räume im Grundriss |
| 10 | Inspection | Inspection | minigame/inspection | GeneralContractor | Suchbild: Fehler finden |
| 11 | ForgeGame | ForgeGame | minigame/forge | MasterSmith | Timing: Metall bei richtiger Temperatur hämmern |
| 12 | InventGame | InventGame | minigame/invent | InnovationLab | Puzzle: Bauteile zusammensetzen |

HINWEIS: Es gibt **13 MiniGame-Typen** im Enum, aber nur **10 distinkte Routen/Renderer**. Planing(1), TileLaying(5), Measuring(6) teilen sich die Sawing-Route/-Mechanik (`minigame/sawing`). Die App-CLAUDE.md beschreibt 10 MiniGame-Views/Renderer.

ACHTUNG MiniGame-Achievement-Diskrepanz: `all_minigames_perfect` (Achievement) hat TargetValue 8 und prüft `PerfectMiniGameTypes.Count` — Desc "all 8 mini-game types". Das passt zu den 8 visuell distinkten Spielen (Sawing-Familie zählt als 1). Maßgeblich: 13 Enum-Typen, 10 Renderer, 8 "perfekt-zählbare" Typen laut Achievement.

### 7.2 Mastery-Tiers (Bronze/Silver/Gold)

Quelle: `Models/Enums/MiniGameMasteryTier.cs` (`MiniGameMasteryThresholds`). Permanent (kein Reset bei Prestige/Ascension). Basiert auf `GameState.LifetimePerfectRatingCounts` PRO MiniGame-Typ.

| Tier | Enum | Lifetime-Perfect-Schwelle | GS-Belohnung |
|------|------|---------------------------|--------------|
| None | 0 | < 50 | 0 |
| Bronze | 1 | 50 (BronzeThreshold) | 5 |
| Silver | 2 | 200 (SilverThreshold) | 15 |
| Gold | 3 | 1000 (GoldThreshold) | 50 |

`GoldenScrewRewards[]` = `[0, 5, 15, 50]` (Index = Tier-Int).

`GetTierForCount(count)`: ≥1000→Gold, ≥200→Silver, ≥50→Bronze, sonst None.

### 7.3 MiniGameMasteryService

Quelle: `Services/MiniGameMasteryService.cs`. Singleton, IDisposable. Wird **eager** in `App.axaml.cs` aufgelöst (subscribed im Ctor auf `IGameStateService.PerfectRatingIncremented`).

- **GetCurrentTier(type)**: `GetTierForCount(GetLifetimePerfectCount(type))`.
- **GetLifetimePerfectCount(type)**: `LifetimePerfectRatingCounts[(int)type]` (0 falls fehlt).
- **GetNextTierThreshold(type)**: None→50, Bronze→200, Silver→1000, Gold→null.
- **OnPerfectRatingRecorded(type)**: Lifetime-Counter wird zuvor in `GameStateService.RecordPerfectRating` atomar inkrementiert. Ermittelt currentTier; vergleicht mit `ClaimedMiniGameMasteryTiers[key]`. Wenn neues Tier > geclaimtes: schüttet **alle übersprungenen Tiers** in Reihenfolge aus (z.B. Sprung 0→Silver gibt Bronze + Silver), `AddGoldenScrews(reward)` je Tier, aktualisiert ClaimedTier, feuert `MasteryTierUnlocked` (MiniGameType, Tier, GoldenScrewReward).
- **Event**: MasteryTierUnlocked (EventHandler<MasteryTierUnlockedEventArgs>).
- Persistenz: `ClaimedMiniGameMasteryTiers` (Dictionary<int,int>, defensive null-Init).

Verwandte Auto-Complete-/Rating-Werte (App-CLAUDE.md, nicht in diesen Services): Rating Perfect=100% / Good=75% / Ok=50% / Miss=0%. Auto-Complete: Timing-Spiele ab 30 Perfects (Premium 15), Puzzle/Memory ab 20 Perfects (Premium 10). Diese Werte stammen aus der App-Doku, nicht aus den hier gelesenen Service-Dateien.

### 7.4 MiniGameNavigator (Route-Map + Abbruch)

Quelle: `Services/MiniGameNavigator.cs`. Sealed, kein State außer Host-Ref.

**Statische Route-Map** `s_miniGameRoutes` (Dictionary<string, ActivePage>) — **10 Einträge**:

| Route-String | ActivePage |
|--------------|------------|
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

- **AttachHost(host)**: setzt INavigationHost.
- **TryResolveRoute(routePart, out page)**: Lookup in Route-Map.
- **NavigateToMiniGame(routePart, orderId)**: setzt `_host.ActivePage = page` und `ActiveMiniGameViewModel?.SetOrderId(orderId)`.
- **IsAnyMiniGamePlaying()**: `vm.IsPlaying || vm.IsCountdownActive`.
- **ConfirmMiniGameAbortAsync()**: Confirm-Dialog (Keys: MiniGameAbortTitle / MiniGameAbortMessage / MiniGameAbortConfirm / Cancel). Bei Bestätigung: StopCurrent() + SelectDashboardTab(). Fallback-Texte: "Abort mini-game?" / "Your progress will be lost. Do you really want to abort?" / "Abort" / "Back".
- **StopCurrent()**: `_host.ActiveMiniGameViewModel?.StopGame()`.

---

## 8. WelcomeBack-Service

Quelle: `Services/WelcomeBackService.cs`, abhängig von `WelcomeBackOffer` / `WelcomeBackOfferType`.

### 8.1 Angebots-Typen + Schwellen

`CheckAndGenerateOffer()` — prüft `absence = UtcNow - LastPlayedAt`. Reihenfolge (erst StarterPack, dann Premium, dann Standard; else-if-Kette, also exklusiv):

| Typ | Bedingung | GS | Money-Reward | XP | Ablauf |
|-----|-----------|----|--------------|----|--------|
| StarterPack | PlayerLevel ≥ 5 && !ClaimedStarterPack (einmalig) | 10 | 50.000 (fix) | 0 | +24 h |
| Premium | absence ≥ 72 h | 8 | `Max(5000, Min(round(NetIncomePerSecond * 3600), 1.000.000.000))` = 1 h Einkommen, hart bei 1 Mrd. € gecappt | 0 | +24 h |
| Standard | absence ≥ 24 h | 5 | `Max(2000, Min(round(NetIncomePerSecond * 1800), 500.000.000))` = 30 min Einkommen, hart bei 500 Mio. gecappt | 0 | +24 h |

- `netPerSecond = Max(1m, NetIncomePerSecond)`.
- Wenn bereits ein aktives, nicht-abgelaufenes Angebot existiert → kein neues. Abgelaufenes wird verworfen.
- Bei generiertem Angebot: `ActiveWelcomeBackOffer = offer`, feuert `OfferGenerated`.

### 8.2 Claim / Dismiss

- **ClaimOffer()**: nur wenn nicht abgelaufen. Zahlt MoneyReward/GoldenScrewReward/XpReward aus. StarterPack → `ClaimedStarterPack = true`. Setzt ActiveWelcomeBackOffer = null.
- **DismissOffer()**: setzt ActiveWelcomeBackOffer = null (keine Belohnung).
- **Event**: OfferGenerated (Action).

---

## 9. WhatsNew-Service

Quelle: `Services/WhatsNewService.cs`

### 9.1 Release-Einträge

`s_releases` (Tuple-Array `(Version, FeatureKeys[])`, aufsteigend sortiert) — **4 Einträge**:

| Version | FeatureKeys (RESX) |
|---------|--------------------|
| 2.0.36 | WhatsNewBell, WhatsNewStrategyEV, WhatsNewReputation |
| 2.0.37 | WhatsNewReputationShop, WhatsNewImperiumTabs, WhatsNewWhatsNewItself |
| 2.1.2 | WhatsNewMinigameFlow, WhatsNewBalancingPolish, WhatsNewCraftingStability, WhatsNewGuildHallBonuses, WhatsNewEconomySaveFixes |
| 2.1.3 | WhatsNewGuildMultiplayerLive, WhatsNewNoBannerAds, WhatsNewPrestigeShopReset, WhatsNewFairCostsAndMaterials, WhatsNewSaveStabilityV213, WhatsNewSmoothnessAndTouch |

### 9.2 Mechanik (`ShowWhatsNewIfNeededAsync`)

- Vergleicht `Settings.LastWhatsNewVersion` (Default "0.0.0") gegen aktuelle App-Version (`GetCurrentAppVersion`: erste 3 Felder der Assembly-Version, Fallback "2.0.0").
- No-op wenn `lastSeen >= current`.
- **Brandneue Spieler** (`State.LastSavedAt == default` ODER `Statistics.TotalOrdersCompleted == 0`): kein Dialog, aber `LastWhatsNewVersion = currentVersion` merken.
- Sammelt kumulativ alle FeatureKeys aus Releases mit `releaseVer > lastSeen`.
- Baut Bullet-Liste (Bullet-Char `•`, kein Emoji) aus lokalisierten Texten. Leere/fehlende Keys werden übersprungen.
- Zeigt `ShowAlertDialog(WhatsNewTitle, body, Confirm)` (Fallback-Title "What's new", Confirm "OK").
- **Crash-Sicherheit**: `LastWhatsNewVersion = currentVersion` wird sofort persistiert, sobald der Dialog triggert (verhindert Doppel-Anzeige bei Crash).

---

## 10. Zusammenfassung der Schlüssel-Zahlen (Cross-Check)

| Element | Echte Anzahl (Code) | Veraltete Doku-/Achievement-Angabe |
|---------|---------------------|-------------------------------------|
| Research-Nodes gesamt | **72** (20+20+20+12) | "45" (ResearchService-Cache + research_all-Achievement) / "~57" (ResearchTree-Kommentar) |
| Research-Branches | **4** (Tools/Management/Marketing/Logistics) | "3" (BranchCount-Konstante) |
| Achievements | **109** | — |
| Achievement-Kategorien | **17** | — |
| Story-Kapitel (Haupt) | **40** (Nr 1–40) | "37"/"38" (StoryService-Kommentar) |
| Story-Kapitel (Saison) | **20** (Nr 100–119) | — |
| Story-Kapitel gesamt | **60** | — |
| FTUE-Schritte | **10** | — |
| ContextualHints | **32** | — |
| MiniGame-Typen (Enum) | **13** | 10 Routen/Renderer, 8 perfekt-zählbar |
| Mastery-Tiers | 3 (Bronze 50/Silver 200/Gold 1000 → 5/15/50 GS) | — |
| MiniGame-Routen | **10** | — |
| WhatsNew-Releases | **4** (2.0.36, 2.0.37, 2.1.2, 2.1.3) | — |
| WelcomeBack-Typen | **3** (StarterPack/Premium/Standard) | — |

Werte, die NICHT in den gelesenen Dateien standen und ggf. anderswo zu suchen sind:
- "+50%-Cap" auf Research-Geschwindigkeit: **(nicht im Code gefunden)** — nur 60-Sekunden-Minimaldauer-Cap existiert.
- `Workshop.MaxLevel`, `Workshop.GetMilestoneMultiplierForLevel`, `GameState.CalculateXpForLevel`, `MaterialOfferUnlockLevel`, Reward-/Tier-Multiplikatoren der Prestige-Tiers: in `Models/Workshop.cs` / `GameBalanceConstants.cs` / `GameState.cs` (nicht Teil dieses Auftrags — siehe andere SoT-Teile).

---

# 08 — Infrastruktur, Save-Schema, Cloud-Save, Notifications, Audio, UI-Mechaniken, Dialoge

Verbindliche Werte- und Mechanik-Spezifikation, ausschliesslich aus dem produktiven Avalonia-Code
extrahiert. Jeder Wert mit Quell-Datei. "(nicht im Code gefunden)" markiert fehlende Belege.

---

## 1. Save-Schema (GameState)

**Quelle: Models/GameState.cs**

- Klasse `GameState` (kein `sealed`, regulaere `class`), Persistenz-Root.
- `public const int CurrentStateVersion = 7;` (Zeile 18). Wird sowohl als Default fuer neue States
  als auch fuer Cloud-Save-Version-Checks genutzt.
- `[JsonPropertyName("version")] public int Version { get; set; } = CurrentStateVersion;`
- JSON-Serialisierung: `JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`
  (in SaveGameService + CloudSaveService). SaveGameService ergaenzt zusaetzlich
  `Converters = { new Helpers.UtcDateTimeJsonConverter() }` (erzwingt UTC fuer alle DateTime-Felder —
  sonst deserialisiert STJ "Z"-Strings als Local und verschiebt `.Date`-basierte Resets um den UTC-Offset).

### 1.1 GameState-Felder (vollstaendig, mit JSON-Key + Default)

Quelle: Models/GameState.cs. `[JsonIgnore]` = nicht persistiert (berechnet/Session). Legacy-Weiterleitungen
sind get/set-Aliase auf Sub-Objekte (V4-Backward-Kompatibilitaet — JSON wird in die Sub-Objekte deserialisiert).

| JSON-Key | C#-Property | Typ | Default | Hinweis |
|----------|-------------|-----|---------|---------|
| version | Version | int | 7 (CurrentStateVersion) | |
| createdAt | CreatedAt | DateTime | DateTime.UtcNow | |
| lastSavedAt | LastSavedAt | DateTime | DateTime.UtcNow | |
| lastPlayedAt | LastPlayedAt | DateTime | DateTime.UtcNow | |
| playerGuid | PlayerGuid | string? | null | Stabile Spieler-ID, Firebase-Pfade |
| playerLevel | PlayerLevel | int | 1 | |
| currentXp | CurrentXp | int | 0 | |
| totalXp | TotalXp | int | 0 | |
| money | Money | decimal | 1000m | |
| totalMoneyEarned | TotalMoneyEarned | decimal | 0 | |
| currentRunMoney | CurrentRunMoney | decimal | 0 | Basis fuer PP-Formel, Reset bei Prestige |
| totalMoneySpent | TotalMoneySpent | decimal | 0 | |
| goldenScrews | GoldenScrews | int | 0 | |
| totalGoldenScrewsEarned | TotalGoldenScrewsEarned | long | 0 | |
| totalGoldenScrewsSpent | TotalGoldenScrewsSpent | long | 0 | |
| premiumAdRewardsUsedToday | PremiumAdRewardsUsedToday | int | 0 | max 3/Tag ohne Video |
| lastPremiumAdRewardReset | LastPremiumAdRewardReset | DateTime | DateTime.MinValue | |
| lastShopAdRewardTime | LastShopAdRewardTime | DateTime | DateTime.MinValue | 3h Cooldown Free |
| lastGoldenScrewsAdTime | LastGoldenScrewsAdTime | DateTime | DateTime.MinValue | eigener 4h-Cooldown |
| workshops | Workshops | List&lt;Workshop&gt; | [] | |
| unlockedWorkshopTypes | UnlockedWorkshopTypes | List&lt;WorkshopType&gt; | [Carpenter] | |
| workerMarket | WorkerMarket | WorkerMarketPool? | null | |
| availableOrders | AvailableOrders | List&lt;Order&gt; | [] | |
| activeOrder | ActiveOrder | Order? | null | Vordergrund-Slot |
| parallelOrdersByWorkshop | ParallelOrdersByWorkshop | Dictionary&lt;WorkshopType, Order&gt; | new() | max MaxParallelOrders=3 |
| (JsonIgnore) | ActiveQuickJob | QuickJob? | null | nicht persistiert |
| lastOrderCooldownStart | LastOrderCooldownStart | DateTime | DateTime.MinValue | |
| weeklyOrderReset | WeeklyOrderReset | DateTime | DateTime.UtcNow | |
| (JsonIgnore) | OrderCooldownThreshold | int | 10 (konstant) | |
| (JsonIgnore) | WeeklyOrderLimit | int | 100 (konstant) | |
| reputation | Reputation | CustomerReputation | new() | |
| lastReputationDecay | LastReputationDecay | DateTime | DateTime.UtcNow | |
| buildings | Buildings | List&lt;Building&gt; | [] | |
| researches | Researches | List&lt;Research&gt; | [] | |
| activeResearchId | ActiveResearchId | string? | null | |
| activeEvent | ActiveEvent | GameEvent? | null | |
| lastEventCheck | LastEventCheck | DateTime | DateTime.UtcNow | |
| eventHistory | EventHistory | List&lt;string&gt; | [] | |
| consecutiveNegativeEvents | ConsecutiveNegativeEvents | int | 0 | Pity-Counter: nach 2 neg. in Folge ausgeschlossen |
| statistics | Statistics | StatisticsData | new() | |
| prestige | Prestige | PrestigeData | new() | |
| ascension | Ascension | AscensionData | new() | |
| prestigeLevel | PrestigeLevel | int | 0 | Legacy V1 |
| prestigeMultiplier | PrestigeMultiplier | decimal | 1.0m | Legacy V1 |
| workshopStars | WorkshopStars | Dictionary&lt;string, int&gt; | new() | 0-5 pro Workshop, ueberlebt Prestige+Ascension |
| settings | Settings | SettingsData | new() | |
| isPremium | IsPremium | bool | false | beim Load aus IPurchaseService gesetzt |
| dailyProgress | DailyProgress | DailyProgressData | new() | |
| lastDailyRewardClaim | (→DailyProgress) | DateTime | — | Legacy-Forward |
| dailyRewardStreak | (→DailyProgress) | int | — | Legacy-Forward |
| boosts | Boosts | BoostData | new() | |
| speedBoostEndTime / xpBoostEndTime / rushBoostEndTime / lastFreeRushUsed | (→Boosts) | DateTime | — | Legacy-Forward |
| unlockedAchievements | UnlockedAchievements | List&lt;string&gt; | [] | |
| quickJobs / lastQuickJobRotation / totalQuickJobsCompleted / quickJobsCompletedToday / lastQuickJobDailyReset | (→DailyProgress) | — | — | Legacy-Forward |
| dailyChallengeState | DailyChallengeState | DailyChallengeState | new() | |
| notificationInbox | NotificationInbox | List&lt;NotificationItem&gt; | [] | Bell-Center (Cap 100) |
| repShopRegularCustomerCharges | RepShopRegularCustomerCharges | int | 0 | Reputation-Shop |
| repShopFasterDeliveryUntil | RepShopFasterDeliveryUntil | DateTime | DateTime.MinValue | |
| repShopWoodPremiumSkinUnlocked | RepShopWoodPremiumSkinUnlocked | bool | false | |
| repShopInsuranceCharges | RepShopInsuranceCharges | int | 0 | |
| claimedCoopOrderIds | ClaimedCoopOrderIds | List&lt;string&gt; | [] | Idempotenz Co-op |
| claimedAuctionIds | ClaimedAuctionIds | List&lt;string&gt; | [] | Idempotenz Auktionen |
| claimedGuildProjectIds | ClaimedGuildProjectIds | List&lt;string&gt; | [] | Idempotenz Mega-Projekte |
| tools | Tools | List&lt;Tool&gt; | [] | |
| collectedMasterTools | CollectedMasterTools | List&lt;string&gt; | [] | |
| nextDeliveryTime | NextDeliveryTime | DateTime | DateTime.MinValue | |
| pendingDelivery | PendingDelivery | SupplierDelivery? | null | |
| tutorial | Tutorial | TutorialState | new() | enthaelt FtueState |
| viewedStoryIds | ViewedStoryIds | List&lt;string&gt; | [] | |
| pendingStoryId | PendingStoryId | string? | null | |
| automation | Automation | AutomationSettings | new() | |
| weeklyMissionState | (→DailyProgress) | WeeklyMissionState | — | Legacy-Forward |
| activeWelcomeBackOffer | (→DailyProgress) | WelcomeBackOffer? | — | Legacy-Forward |
| claimedStarterPack / streakBeforeBreak / streakRescueUsed | (→DailyProgress) | — | — | Legacy-Forward |
| luckySpin | LuckySpin | LuckySpinState | new() | |
| equipmentInventory | EquipmentInventory | List&lt;Equipment&gt; | [] | |
| currentTournament | CurrentTournament | Tournament? | null | |
| lastMaterialOrderReset | LastMaterialOrderReset | string | "" | |
| completedRecipeIds | CompletedRecipeIds | List&lt;string&gt; | [] | |
| perfectMiniGameTypes | PerfectMiniGameTypes | List&lt;string&gt; | [] | |
| perfectRatingCounts | PerfectRatingCounts | Dictionary&lt;int, int&gt; | new() | reset bei Ascension |
| lifetimePerfectRatingCounts | LifetimePerfectRatingCounts | Dictionary&lt;int, int&gt; | new() | KEIN reset (Mastery) |
| claimedMiniGameMasteryTiers | ClaimedMiniGameMasteryTiers | Dictionary&lt;int, int&gt; | new() | 0=None..3=Gold |
| managers | Managers | List&lt;Manager&gt; | [] | |
| currentSeasonalEvent | CurrentSeasonalEvent | SeasonalEvent? | null | |
| battlePass | BattlePass | BattlePass | new() | |
| isPrestigePassActive | IsPrestigePassActive | bool | false | reset bei Prestige |
| claimedLevelOffers | ClaimedLevelOffers | List&lt;int&gt; | [] | |
| starterOfferShown | StarterOfferShown | bool | false | |
| starterOfferTimestamp | StarterOfferTimestamp | DateTime? | null | 24h-Countdown |
| guildMembership | GuildMembership | GuildMembership? | null | |
| integritySignature | IntegritySignature | string? | null | HMAC-SHA256 |
| craftingInventory | CraftingInventory | Dictionary&lt;string, int&gt; | new() | |
| activeCraftingJobs | ActiveCraftingJobs | List&lt;CraftingJob&gt; | [] | |
| warehouseSlotCount | WarehouseSlotCount | int | 20 | V7, Cap 200 |
| warehouseStackLimit | WarehouseStackLimit | int | 50 | V7, Cap 9999 |
| reservedInventory | ReservedInventory | Dictionary&lt;string, int&gt; | new() | V7 |
| autoSellRules | AutoSellRules | Dictionary&lt;string, AutoSellRule&gt; | new() | V7 |
| heirloomItems | HeirloomItems | List&lt;string&gt; | [] | V7, max 3 (4 Premium) |
| totalPurchaseAmount | TotalPurchaseAmount | decimal | 0 | VIP-Tracking |
| vipLevel | VipLevel | VipTier | VipTier.None | |
| hasPurchasedStarterPack | HasPurchasedStarterPack | bool | false | |
| lastBossOrderDate | LastBossOrderDate | DateTime | DateTime.MinValue | |
| bossOrdersCompleted | BossOrdersCompleted | int | 0 | |
| friends | Friends | List&lt;Friend&gt; | [] | |
| referral | Referral | ReferralProgress | new() | |
| liveEvent | LiveEvent | LiveEvent? | null | |
| lastGiftSentDate | LastGiftSentDate | DateTime | DateTime.MinValue | |
| playerName | PlayerName | string? | null | |
| dailyShopOffer | DailyShopOffer | ShopOffer? | null | |
| cosmetics | Cosmetics | CosmeticData | new() | |
| unlockedCosmetics / activeCityThemeId / activeWorkshopSkins | (→Cosmetics) | — | — | Legacy-Forward |
| maxOfflineEarnings | MaxOfflineEarnings | decimal | 0 | |

**Berechnete/Session-Properties (nicht persistiert):**
- `BaseOfflineHours => 4` (konstant).
- `MaxOfflineHours`: `IsPremium ? 16 : OfflineVideoExtended ? 8 : 4`, plus Prestige-Shop-`OfflineHoursBonus`
  (pp_offline_hours: +4h). Gecacht (`_cachedMaxOfflineHours`), Invalidierung via `InvalidateMaxOfflineHoursCache()`.
- `OfflineVideoExtended`, `OfflineVideoDoubled` (Session-Flags, JsonIgnore).
- `XpForNextLevel => CalculateXpForLevel(PlayerLevel + 1)`.
- `CalculateXpForLevel(level)`: `level <= 1 → 0`, sonst `(int)(100 * Math.Pow(level - 1, 1.2))`.
- `LevelProgress`: `(CurrentXp - XpFuerLevel) / (XpFuerNext - XpFuerLevel)`, geclampt 0..1.
- `TotalIncomePerSecond`: `Sum(Workshops.GrossIncomePerSecond) * min(Prestige.PermanentMultiplier, 20.0m)`,
  gecacht (`InvalidateIncomeCache()`).
- `TotalCostsPerSecond`: `Sum(Workshops.TotalCostsPerHour) / 3600m`.
- `NetIncomePerSecond => TotalIncomePerSecond - TotalCostsPerSecond`.

**CreateNew()** (Quelle: GameState.cs, Z. 1010): Start-Workshop Carpenter (IsUnlocked=true) + 2x
`Worker.CreateRandom(Carpenter)`, `ResearchTree.CreateAll()`, `Tool.CreateDefaults()`. Money-Default 1000m
(aus Property-Initializer).

### 1.2 SaveGame-Migration (V1→V7)

**Quelle: SaveGameService.MigrateState() (internal static)**

| Von→Zu | Aktion |
|--------|--------|
| <2 | `MigrateFromV1`: Worker→Tier.E/Talent3/Steady/Mood80/Fatigue0, ExperienceLevel=min(10,SkillLevel), WagePerHour=E.GetWagePerHour(), AssignedWorkshop=ws.Type, ws.IsUnlocked=true. Prestige aus PrestigeLevel/PrestigeMultiplier. Reputation/Buildings/EventHistory init. ResearchTree.CreateAll() oder Prerequisites-Sync. Version=2 |
| <3 | `WorkshopStars ??= new()`, Version=3 |
| <5 | `Boosts/DailyProgress/Cosmetics ??= new()` (Legacy-Forwards uebernehmen Daten automatisch), Version=5 |
| <6 | `ParallelOrdersByWorkshop ??= new()`; vorhandenen `ActiveOrder` ins Dictionary migrieren (Key=WorkshopType), Version=6 |
| <7 | `WarehouseSlotCount ??= 20`, `WarehouseStackLimit ??= 50`, `ReservedInventory/AutoSellRules ??= new()`, `HeirloomItems ??= []`. **Stack-Truncation:** ueberlaufende CraftingInventory-Stacks auf StackLimit kuerzen, Differenz `× product.BaseValue` (KEIN Sell-Multiplier) als Geld gutschreiben. Version=7 |

Hinweis: V3→V4 und V4→V5 sind im sichtbaren Code zusammengefasst (kein eigener MigrateFromV4 mehr — die
Legacy-Weiterleitungs-Properties in GameState deserialisieren flache V4-Felder direkt in die Sub-Objekte).

### 1.3 SaveGame-Sanitize (Reparatur statt Ablehnung)

**Quelle: SaveGameService.SanitizeState() — laeuft nach Migration bei jedem Load/Import.**

- Sub-Objekt-Null-Safety: Boosts, DailyProgress, Cosmetics, Tutorial, Statistics, Settings, BattlePass je `??= new()`.
- `state.IsPremium = _purchaseService?.IsPremium ?? false` (aus kaufgesichertem Preference-Cache, VOR Heirloom-Cap).
- PlayerLevel: clamp `[MinPlayerLevel=1, MaxPlayerLevel=1500]`.
- Money: `< 0 → 0`; Cap `moneyCap = Math.Max(1_000_000_000_000_000m, TotalMoneyEarned)` (1e15-Floor).
- CurrentXp/GoldenScrews/TotalMoneyEarned/CurrentRunMoney/TotalMoneySpent: `< 0 → 0`.
- GoldenScrews: Cap **100_000**.
- Workshops: mind. 1 Carpenter (IsUnlocked, in UnlockedWorkshopTypes). Workshop.Level clamp `[1, Workshop.MaxLevel]`.
- Prestige: alle Counts `< 0 → 0`; PermanentMultiplier clamp `[1.0m, 20.0m]`; PurchasedShopItems gegen `PrestigeShop.GetValidIds()` filtern.
- DailyRewardStreak `< 0 → 0`.
- Worker: AdBonusWorkerSlots ≤ `Workshop.MaxAdBonusWorkerSlots`; Mood/Fatigue clamp `[0,100]`; ExperienceLevel ≥ 1; ExperienceXp ≥ 0; AssignedWorkshop = ws.Type; WagePerHour = Tier.GetWagePerHour(); Efficiency clamp `[Tier.GetMinEfficiency(), Tier.GetMaxEfficiency()]`. **Material-Affinity-Migration:** wenn `None && Id != ""` → `(MaterialAffinity)((|Id.GetHashCode()| % 5) + 1)`.
- Reputation: ReputationScore clamp `[0,100]`.
- ParallelOrdersByWorkshop: verwaiste Eintraege entfernen (Key≠WorkshopType, IsExpired, oder nicht-freigeschaltet); hartes Cap auf `MaxParallelOrders=3`.
- ResearchTree-Sync aus Template (`ResearchTree.CreateAll()`): DurationTicks/Effect/Cost/Prerequisites/Branch/Level/NameKey/DescriptionKey aktualisieren, fehlende Nodes ergaenzen, ActiveResearchId-Konsistenz pruefen.
- Building.Level clamp `[0, Type.GetMaxLevel()]`.
- CollectedMasterTools gegen `MasterTool.GetValidIds()` filtern; Tool-Defaults ergaenzen.
- CraftingInventory: count ≤ 0 entfernen; count > WarehouseStackLimit auf Limit kappen.
- ReservedInventory: nie > CraftingInventory; Orphan-Reservierungen (Reserved > Summe akzeptierter MaterialOffers in ActiveOrder+ParallelOrders) freigeben.
- WarehouseSlotCount clamp `[20, WarehouseService.MaxSlots=200]`; WarehouseStackLimit clamp `[50, 9999]`.
- WorkshopStars clamp `[0,5]`.
- Ascension: Perks/PermanentHeirlooms ??= ; AscensionLevel/AscensionPoints `< 0 → 0`.
- HeirloomItems + Ascension.PermanentHeirlooms: nur `IsHeirloomEligible`-Produkte (gegen `CraftingProduct.GetAllProducts()`); HeirloomItems Cap = `GameBalanceConstants.GetEffectiveHeirloomSlots(IsPremium)`.
- Statistics-Counter `< 0 → 0`.
- `BattlePass.IsPremium = false` und `IsPrestigePassActive = false` (Exploit-Schutz; eigene Restore-Pfade).
- Abgelaufene PendingDelivery entfernen.

### 1.4 Save-IO-Pipeline (Atomare Writes)

**Quelle: SaveGameService.cs**

- Dateien (`Environment.SpecialFolder.LocalApplicationData/HandwerkerImperium/`):
  Save = `handwerker_imperium_save.json`, Backup = `handwerker_imperium_save.bak`, Temp = `*.json.tmp`.
- `_ioLock` = `SemaphoreSlim(1,1)`, Timeout `IoLockTimeoutSeconds = 30`.
- Save: unter Lock nur `LastSavedAt=UtcNow` + `ComputeSignature` + `SerializeToUtf8Bytes` (Snapshot). Off-Lock:
  `WriteAllBytes(temp)` → `File.Move(save→backup, overwrite)` → `File.Move(temp→save, overwrite)` (atomares Rename).
- Serialize auf Background-Thread via `Task.Run(() => ExecuteWithLock(...))` (verhindert UI-Freeze).
- Load: Haupt-Datei zuerst, sonst Backup. Wenn beide existieren aber beschaedigt → `LastLoadFailedCorrupt = true`
  (Signal fuer Cloud-Recovery), keine Datei → `null` (legitimer Neu-Spieler).
- DeleteSave loescht save+backup+tmp.
- Export/Import: ExportSaveAsync = `JsonSerializer.Serialize` (kein IO-Lock). ImportSaveAsync laeuft atomar
  unter `_ioLock` (Deserialize → MigrateState → SanitizeState → Initialize → SaveInternalAsync).
- `ErrorOccurred`-Event (`Action<string,string>`): "Error"/"SaveErrorMessage" etc.

### 1.5 Stuck-Order-Recovery

**Quelle: GameStartupCoordinator.RunAsync()** — beim Startup:
`if (State.ActiveOrder != null) → CancelActiveOrder()` (MiniGame-State wird nicht gespeichert, kann nicht fortgesetzt werden).
Zusaetzlich `NavigateBack` (Navigation.cs): bei Back aus MiniGame/OrderDetail mit aktivem Auftrag → `PauseActiveOrder()` +
zurueck zum Dashboard (bleibt im ParallelOrdersByWorkshop als Fortsetzen-Banner).

---

## 2. Cloud-Save (Local-First, Firebase)

**Quellen: CloudSaveService.cs, GameStartupCoordinator.CheckCloudSaveAsync(), Models/CloudSaveMetadata.cs, SaveGameService.cs**

### 2.1 Speicherort / Struktur

- BasePath `cloud_saves`. Pfade: `cloud_saves/{playerId}/metadata` (kleine Preview) und
  `cloud_saves/{playerId}/data` (kompakter State-JSON, als String-Wert gespeichert).
- `IsAvailable => _firebase.IsOnline && !string.IsNullOrEmpty(_firebase.PlayerId)`.
- Beim Download wird der Cloud-State fuer das lokale Geraet neu HMAC-signiert (`_integrity.ComputeSignature(state)`),
  weil der Integrity-Key geraetegebunden ist. Bewusst: Cloud-Save schuetzt gegen Geraeteverlust, nicht gegen Save-Editing.
- Upload signiert vor dem Schreiben: `state.LastSavedAt = UtcNow; ComputeSignature(state)`.
- Metadata + Data via `SetAsync` (PUT). Upload nur wenn beide ok (`metaOk && dataOk`).

### 2.2 CloudSaveMetadata (Models/CloudSaveMetadata.cs)

| JSON-Key | Property | Typ |
|----------|----------|-----|
| level | PlayerLevel | int |
| money | Money | decimal |
| goldenScrews | GoldenScrews | int |
| prestigePoints | PrestigePoints | decimal |
| ascensionLevel | AscensionLevel | int |
| savedAt | SavedAtIso | string ("O") |
| version | StateVersion | int |
| appVersion | AppVersion | string (Assembly Version, ToString(3)) |

- `SavedAtUtc` (JsonIgnore): parst SavedAtIso mit `CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind`,
  Fallback `DateTime.MinValue`.

### 2.3 Konflikt-Logik (NICHT Server-wins — Local-First mit User-Confirmation)

**Quelle: GameStartupCoordinator.CheckCloudSaveAsync()**

1. Vorbedingung: `_cloudSaveService.IsAvailable == true && Settings.CloudSaveEnabled`.
2. Metadata laden; null → return.
3. **Version-Outdated-Schutz:** `if (metadata.StateVersion > GameState.CurrentStateVersion)` → Alert
   ("CloudSaveTooNewTitle"/"CloudSaveTooNewBody", "App update required"), **kein Download** (Migration auf
   bereits-aktuelle Daten wuerde State korrumpieren).
4. **SavedAt-Vergleich mit 5s-Toleranz:** `localSavedAt = State.LastSavedAt`, `cloudSavedAt = metadata.SavedAtUtc`.
   `bool localWasCorrupt = _saveGameService.LastLoadFailedCorrupt`. Wenn **nicht** corrupt **und**
   `cloudSavedAt <= localSavedAt.AddSeconds(5)` → return (lokal aktuell genug). Bei corruptem Local wird die
   Heuristik uebersprungen — Cloud ist immer besser als frischer Leer-State.
5. **User-Confirmation-Dialog** ("CloudSaveNewer" Titel; Message zeigt lokal `Level {0} ({Money})` + Cloud
   `Level {0} ({Money})` via `MoneyFormatter.FormatCompact`; Buttons "Use Cloud" / "Keep Local").
   Bei Ablehnung → return.
6. Bei Zustimmung: `DownloadAsync()` → `JsonSerializer.Serialize(cloudState)` → `ImportSaveAsync(cloudJson)`
   (laeuft Sanitize + Save) → `RefreshFromState()`. Analytics-Event `cloud_save_downloaded` (level, money).
7. Fehler still ignoriert (lokaler Save funktioniert weiter).

### 2.4 Cloud-Upload aus dem Save-Loop (Rate-Limit)

**Quelle: SaveGameService.SaveInternalAsync()**

- Nur wenn `_cloudSaveService?.IsAvailable == true && cloudSaveEnabled`.
- Rate-Limit: `CloudUploadMinIntervalTicks = TimeSpan.FromMinutes(2).Ticks`. Lock-frei via
  `Interlocked.Read` + `CompareExchange` auf `_lastCloudUploadTicks` (nur ein Thread gewinnt den Upload-Slot pro Fenster).
- Upload via `UploadJsonAsync(json, metadata)` (race-frei: JSON + Metadata frozen). Bei Erfolg
  `state.Settings.LastCloudSaveTime = UtcNow`.

Hinweis: Die App-CLAUDE.md erwaehnt "Push-Debounce 5s" — im Code ist das relevante Intervall **2 Minuten**
(SaveGameService) bzw. die **5s-Toleranz** beim Konflikt-Vergleich (GameStartupCoordinator). Ein separates
5s-Push-Debounce ist (nicht im Code gefunden).

---

## 3. Firebase-Service (REST, Anonymous Auth + Realtime DB)

**Quelle: FirebaseService.cs**

- Projekt: `handwerkerimperium-487917`.
- ApiKey `AIzaSyCyfSD0g7TZR1CNgjPlc9L3SyfNwbEst9k`.
- DatabaseUrl `https://handwerkerimperium-487917-default-rtdb.europe-west1.firebasedatabase.app`.
- AuthUrl `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=`.
- TokenRefreshUrl `https://securetoken.googleapis.com/v1/token?key=`.
- Preference-Keys: `firebase_uid`, `firebase_refresh_token`, `player_id`.
- Konstanten: `RequestTimeout = 15s`, `TokenLifetime = 55 min` (5min vor Ablauf refreshen), `RetryDelayMs = 500`.
- `_authLock = SemaphoreSlim(1,1)`. Auth-Cooldown nach dauerhaft fehlgeschlagenem Refresh: `_authCooldownUntil = UtcNow.AddMinutes(5)` (kein Infinite-401-Loop).
- PlayerId-Initialisierung (Prioritaet): 1. Preferences `player_id`, 2. GameState-Backup (PlayerGuid),
  3. neue `Guid.NewGuid().ToString("N")`.
- Auth-Flow: gespeicherte Credentials → 2x Refresh-Versuch (mit 500ms Delay) → Fallback `SignUpAnonymouslyAsync`.
- `auth_to_player/{uid}` → PlayerId-Mapping (nach jedem Refresh fire-and-forget geschrieben; Security-Rules nutzen es).
- DB-Operationen: GET/SET(PUT)/UPDATE(PATCH)/PUSH(POST)/DELETE/QUERY. Bei 401 → `ForceRefreshAndRetryAuth` + 1 Retry.
  `IsOnline` spiegelt `response.IsSuccessStatusCode`.
- `IDisposable`: HttpClient + _authLock.

### PlayGamesService (Desktop-Stub)
**Quelle: PlayGamesService.cs** — `IsSignedIn=false`, `SupportsCloudSave=false`, alle Methoden No-Op/false/leer.
Android-Impl: AndroidPlayGamesService (Leaderboards/Achievements/Cloud Save, nicht im Shared-Stub).

---

## 4. GameIntegrityService (HMAC-Signierung)

**Quelle: GameIntegrityService.cs**

- Preference-Key `game_integrity_install_id`. PackageSalt = `com.meineapps.handwerkerimperium`.
- **ZWEI Schluessel für zwei Vertrauensgrenzen** (`GameIntegrityService.cs:36-55`):
  1. **Lokaler Save-Key** `_hmacKey = SHA256(UTF8(PackageSalt + installId))` — Installations-GUID
     (`Guid.NewGuid().ToString("N")`, persistiert), GERÄTE-EINZIGARTIG. Nur für die lokale
     `GameState.IntegritySignature` (`ComputeSignature`/`VerifySignature`).
  2. **Geteilter Multiplayer-Key** `_sharedHmacKey = SHA256(UTF8(PackageSalt + "|shared-guild-hmac-v1"))`
     — OHNE installId, daher auf ALLEN Geräten identisch. Wird von `ComputeStringHmac` benutzt (s.u.).
  Je 32 Byte (256 Bit), kein hardcodierter Schluessel.
- **Signierte Felder (Payload-Format):** `"{PlayerLevel}|{Prestige.TotalPrestigeCount}|{Money:F2}|{GoldenScrews}|{Statistics.TotalOrdersCompleted}"`
  (kulturunabhaengig, `string.Create(null, stackalloc char[128], ...)`).
- HMAC-SHA256 ueber UTF8(payload), als Hex-String (lower) in `GameState.IntegritySignature`.
- `VerifySignature`: `Convert.FromHexString` der gespeicherten Signatur, timing-sicherer
  `CryptographicOperations.FixedTimeEquals` gegen frisch berechnete Bytes. Ungueltiges Hex → false.
- `ComputeStringHmac(payload)`: HMAC-Helper (lower-Hex) mit dem **geteilten** `_sharedHmacKey` (NICHT dem
  geräte-lokalen `_hmacKey`!) — für Gilden-Co-op/Auktionen/Mega-Projekte. Diese Objekte werden von einem
  Spieler signiert und von ANDEREN validiert; ein geräte-lokaler Key würde die Cross-Client-Validierung
  IMMER brechen. Server-seitige Manipulations-Abwehr leisten zusätzlich die Firebase-Rules.
  Beleg: `GameIntegrityService.cs:90-98`.

---

## 5. Push-Notifications (8 Trigger)

**Quelle: HandwerkerImperium.Android/AndroidNotificationService.cs**

- Channel: Id `handwerker_game`, Name `HandwerkerImperium`, Importance `Default` (API 26+). Prefs `notification_schedule`.
- Mechanik: `AlarmManager.SetAndAllowWhileIdle(RtcWakeup, ...)` + `NotificationReceiver` (BroadcastReceiver).
  Persistiert in SharedPreferences fuer Boot-Recovery (`BootReceiver` → `RescheduleFromPreferences`).
- Vorbedingung: `if (!state.Settings.NotificationsEnabled) return;` — vor dem Planen `CancelAllNotifications()`.

| # | ID | RESX-Key (message_key) | Delay/Trigger | Bedingung |
|---|-----|------------------------|---------------|-----------|
| 1 | 1001 ResearchComplete | ResearchDoneNotif | `endTime - UtcNow` (StartedAt + Duration) | ActiveResearchId gesetzt, endTime > jetzt |
| 2 | 1002 DeliveryReminder | DeliveryWaitingNotif | 3 min (3*60*1000 ms) | immer (wenn Notifications an) |
| 3 | 1004 DailyReward | DailyRewardNotif | naechster Tag 10:00 UTC | immer |
| 4 | 1003 RushAvailable | RushAvailableNotif | naechste 18:00 UTC | immer |
| 5 | 1005 WorkerMoodCritical | WorkerMoodCriticalNotif | 30 min (30*60*1000 ms) | irgendein Worker `Mood < 25` |
| 6 | 1006 OfflineEarningsCapped | OfflineEarningsCappedNotif | 4 h (4*60*60*1000 ms) | immer |
| 7 | 1007 BattlePassExpiring | BattlePassExpiringNotif | `max(0, DaysRemaining - 3)` Tage | BattlePass `DaysRemaining` in (0, 5] |
| 8 | 1008 LiveOrderAvailable | LiveOrderAvailableNotif | 1 h (60*60*1000 ms) | irgendein Workshop `Level >= 25` |

- Texte mit "Meister Hans"-Persona-Praefix (laut App-CLAUDE.md; konkreter Praefix-Text liegt in den RESX-Werten — nicht in dieser Service-Datei).
- **Desktop-Stub** (Services/NotificationService.cs): `ScheduleGameNotifications`/`CancelAllNotifications` No-Op.

---

## 6. NotificationCenter (In-App-Bell/Inbox)

**Quelle: NotificationCenterService.cs, Models/NotificationItem.cs**

- Abgrenzung: Bell-Inbox ist in-App (persistiert in `GameState.NotificationInbox`), getrennt von den 8 Android-Push-Triggern (Abschnitt 5).
- `MaxInboxSize = 100` (FIFO-Eviction der aeltesten ueber CreatedAt bei Ueberlauf).
- Alle Mutationen (Add/Dismiss/Clear/MarkAllSeen) laufen unter `IGameStateService.ExecuteWithLock(...)` (gleicher Lock wie SaveGame → kein "Collection was modified").
- `Items` (IReadOnlyList): sortierte Snapshot-Liste, neueste zuerst (`OrderByDescending(CreatedAt)`), gecacht (`_isCacheDirty`) — vermeidet O(n log n)/Allokation pro Frame bei offener Bell.
- `UnseenCount`: `Count(i => !i.Seen)`.
- `Add`: dedupliziert ueber `Id` (gleiche Id → Update statt Add; Seen-Flag bleibt erhalten).
- `Changed`-Event (`Action`) bei jeder Mutation.

### NotificationKind-Enum (Models/NotificationItem.cs)
| Wert | Anzeige |
|------|---------|
| OfflineEarnings | IMMER Modal, NICHT in Bell |
| DailyReward | in Bell sammelbar |
| WelcomeBackOffer | in Bell sammelbar (Premium-Bundle) |
| AchievementUnlocked | in Bell sammelbar |
| StreakSaved | in Bell sammelbar |
| NewStoryChapter | in Bell sammelbar, mit Pulse-Akzent |
| LiveOrderAvailable | Live-/Premium-Auftrag (durch AutoAcceptOnlyStandard nicht angenommen) |

### NotificationItem-Felder
`id` (string), `kind` (NotificationKind), `titleKey` (string), `titleArg` (string?), `bodyKey` (string),
`bodyArg` (string?), `createdAt` (DateTime, UtcNow), `seen` (bool), `iconKind` (string?).

---

## 7. RemoteConfig

**Quellen: RemoteConfigService.cs, Models/RemoteConfigKeys.cs, HandwerkerImperiumLoadingPipeline.cs**

- Firebase-Pfad `remote_config`. Preference-Cache: `remote_config_cache_json` + `remote_config_last_fetched`.
- Cache-Strategie: letzter Erfolg im PreferencesService (JSON). Offline-Start → letzte bekannte Werte;
  kalter Erststart → Defaults.
- Verschachteltes JSON wird auf flache Dot-Keys gewalkt (`{"balancing":{"foo":1}}` → `balancing.foo`).
  Arrays werden als JSON-String abgelegt (nicht weiter geparst).
- Typisierte Getter: `GetInt/GetDecimal/GetBool/GetString(key, default)` mit InvariantCulture-Parsing-Fallbacks.
- Startup (LoadingPipeline Schritt 3, Weight 5): `InitializeAsync()` mit **5s-Timeout** (Task.WhenAny gegen
  `Task.Delay(5s)`). Bei Timeout laeuft Fetch im Hintergrund weiter; DailyBundle wird per ContinueWith deferred initialisiert.

### RemoteConfig-Keys (typisiert, mit Default) — Models/RemoteConfigKeys.cs

| Konstante | Key | Default (laut Doc-Kommentar) |
|-----------|-----|------------------------------|
| StarterOfferMinLevel | balancing.starter_offer_min_level | 10 |
| OfflineEarningsMaxHours | balancing.offline_earnings_max_hours | 8 |
| DeliveryIntervalMinSec | balancing.delivery_min_sec | 120 |
| DeliveryIntervalMaxSec | balancing.delivery_max_sec | 300 |
| AutoCompletePerfectThreshold | balancing.auto_complete_threshold | 30 |
| AutoCompletePerfectThresholdPremium | balancing.auto_complete_threshold_premium | 15 |
| SeasonalEffectsEnabled | features.seasonal_effects_enabled | true |
| CloudSaveDefaultEnabled | features.cloud_save_default | true |
| StarterOfferEnabled | features.starter_offer_enabled | true |
| PromoBannerActive | promo.banner_active | false |
| PromoBannerTextKey | promo.banner_text_key | "" (leer) |
| OrderDifficultyMultiplier | balancing.order_difficulty_multiplier | 1.0 |
| LiveOrderSpawnChance | balancing.live_order_spawn_chance | 0.5 |
| LiveOrderPremiumChance | balancing.live_order_premium_chance | 0.05 |
| WorkerMarketWeights | balancing.worker_market_weights | CSV "F=20,E=22,D=22,C=14,B=10,A=6,S=3,SS=1.5,SSS=0.5,Legendary=0.1" |
| PremiumPriceFallback | monetization.premium_price_fallback | "4.99 EUR" |
| GoldenScrewAdReward | monetization.golden_screw_ad_reward | 8 |
| GoldenScrewAdCooldownHours | monetization.golden_screw_cooldown_hours | 4 |
| ShopAdCooldownHours | monetization.shop_reward_cooldown_hours | 3 |
| DailyBundleEnabled | monetization.daily_bundle_enabled | false |
| DailyBundleSkus | monetization.daily_bundle_skus | "" (leer, JSON-Array 7 SKUs) |
| SeasonalThemeOverride | events.seasonal_theme_override | "" (Bug-Out-Switch) |
| LuckySpinSegmentWeights | events.lucky_spin_segment_weights | "" (Code-Default) |
| **CoopOrdersEnabled** (Kill-Switch) | features.coop_orders_enabled | true |
| **AuctionsEnabled** (Kill-Switch) | features.auctions_enabled | true |
| CrossPromoBannerActive | marketing.cross_promo_active | false |
| CrossPromoTarget | marketing.cross_promo_target | "" |
| OnboardingDialogCount | ux.onboarding_dialog_count | 1 (0 = aggressives Skip) |
| OnboardingStorySkipEnabled | ux.onboarding_story_skip | true |

Kill-Switches: `CoopOrdersEnabled`, `AuctionsEnabled` (Bug-Notabschaltung der Big-Bet-Features).

---

## 8. Analytics

**Quellen: AnalyticsService.cs, Models/AnalyticsEvents.cs**

### 8.1 Mechanik / Batching / Consent
- REST via FirebaseService nach `analytics_events/{YYYY-MM-DD}` (ein PATCH/UpdateAsync, pushId pro Event).
- Konstanten: `QueueCap = 500` (FIFO-Drop bei Ueberlauf), `FlushIntervalSeconds = 30`, `MaxBatchSize = 50`.
- `_queue` = ConcurrentQueue, `_flushLock = SemaphoreSlim(1,1)`, `_userPropsLock` = object.
- **Consent (DSGVO):** `IsEnabled` setzt `Settings.AnalyticsEnabled`. Bei false → Timer stoppen + Queue verwerfen
  (keine Daten nach Opt-Out). `TrackEvent` ist No-Op wenn `!_isEnabled || _disposed`.
- Flush: nur wenn `IsEnabled && !disposed && !queue.IsEmpty && Firebase.IsOnline && PlayerId != ""`. Bei
  Fehler werden Events zurueck in die Queue gelegt (Queue-Cap re-check). `_flushTimer` = System.Timers.Timer (AutoReset).
- Event-Payload: `{ eventName, timestamp (UtcNow "O"), sessionId, playerId, params, user (Snapshot der User-Props) }`.
- SessionId = `Guid.NewGuid().ToString("N")[..12]`.
- Dispose: best-effort Flush mit `Wait(2s)`.

### 8.2 User-Properties (AnalyticsUserProperties)
`language`, `premium`, `prestige_tier`, `ascension_level`, `player_level`, `graphics_quality`,
`days_since_install` (aus CreatedAt), `app_version`, `test_cohort` (A/B: stabiler PlayerId-Hash `*31+c`, `%2`→"a"/"b"),
`install_cohort_week` (ISO "YYYY-Www", persistiert `analytics_install_cohort_week`), `player_id`.
RetentionDay-Event 1x/Tag (`analytics_last_retention_day`).

### 8.3 Event-Katalog (AnalyticsEvents.cs — snake_case)
Session/Retention: `session_start`, `session_end`, `retention_day`, `app_open`, `app_pause`, `app_resume`.
Tutorial: `tutorial_step`, `tutorial_complete`, `welcome_seen`.
Unlocks: `workshop_unlocked`, `first_order_accepted`, `first_minigame_played`, `feature_unlocked`, `level_up`.
Progression: `prestige_done`, `ascension_done`, `rebirth_done`, `research_completed`, `building_upgraded`, `achievement_unlocked`.
MiniGames: `minigame_played`, `minigame_perfect`, `auto_complete_used`.
IAP: `iap_shop_viewed`, `iap_item_viewed`, `iap_purchase_started`, `iap_purchase_success`, `iap_purchase_failed`, `iap_purchase_cancelled`.
Ads: `ad_requested`, `ad_shown`, `ad_rewarded`, `ad_failed`, `ad_dismissed`.
Gilden: `guild_joined`, `guild_created`, `guild_left`, `guild_boss_hit`, `guild_war_joined`.
Economy: `order_completed`, `worker_hired`, `offline_earnings_claimed`, `daily_reward_claimed`, `lucky_spin_played`.
Cloud-Save: `cloud_save_uploaded`, `cloud_save_downloaded`, `cloud_save_conflict`.
Worker-Lifecycle: `worker_promoted`, `worker_aura_unlocked`, `worker_quit`.
Co-op: `coop_order_invited`, `coop_order_accepted`, `coop_order_declined`, `coop_order_completed`, `coop_order_score_submitted`.
Auktionen: `auction_bid_placed`, `auction_won`, `auction_lost`.
Reputation-Shop: `reputation_shop_purchased`.
Equipment: `equipment_dropped`, `equipment_equipped`.
Live/Premium-Order: `live_order_expired_unstarted`, `live_order_premium_accepted`, `parallel_order_started`.
Prestige-Cinematic: `prestige_cinematic_skipped`, `prestige_cinematic_completed`.
Manager/Inbox: `manager_unlocked`, `notification_inbox_opened`.
Onboarding-Funnel: `onboarding_story_skipped`, `onboarding_first_workshop_shown`, `onboarding_first_order_hinted`.
Fehler: `error_occurred`.
Funnel-Helper: `TrackFunnelStep(funnel, step, name)` → `funnel_{funnelName}` mit step/step_name.

---

## 9. Review (Milestones + 14d-Cooldown)

**Quelle: ReviewService.cs**

- Preference-Key `ReviewPromptedDate` (UtcNow "O"). `CooldownDays = 14`.
- `ShouldPromptReview()` → `_shouldPrompt`. `MarkReviewPrompted()` setzt `_shouldPrompt=false` + speichert Datum.
- **Trigger-Meilensteine (`OnMilestone(type, value)`):**
  - `"level"` → value ist **20, 50 oder 100**.
  - `"prestige"` → value >= 1.
  - `"orders"` → value >= 50.
- Bei Trigger: wenn letzter Prompt < 14 Tage her → return (Cooldown aktiv), sonst `_shouldPrompt = true`.
- Ausloesung: ProgressionFeedbackCoordinator ruft `OnMilestone("level"/"prestige")` + `CheckReviewPrompt()`
  → bei `ShouldPromptReview()==true` → `MarkReviewPrompted()` + `App.ReviewPromptRequested?.Invoke()` (Android In-App-Review).

---

## 10. Audio

**Quelle: Services/AudioService.cs (Desktop-Stub), App-CLAUDE.md (Android-Impl), CinematicCoordinator.cs**

- `IAudioService` (Shared). Desktop-Stub `AudioService`: SoundEnabled/MusicEnabled/SfxVolume/MusicVolume lesen/schreiben
  `GameStateService.Settings`, alle Play/Stop/Vibrate sind No-Op. SfxVolume/MusicVolume clamp `[0f, 1f]`.
- **MusicTrack-Enum (Code):** `IdleWorkshop`, `BossOrTournament`, `Celebration`. (Hinweis: App-CLAUDE.md erwaehnt
  `MusicTrack.IdleWorkshop`, `BossOrTournament`, `Celebration`.)
- **Crossfade default 800 ms** (laut App-CLAUDE.md; `PlayMusicAsync(MusicTrack track, bool crossfade = true)`).
- Cinematic-Audio (CinematicCoordinator): bei CinematicReady `PlayMusicAsync(Celebration, crossfade:true)`;
  nach Dismiss `PlayMusicAsync(IdleWorkshop, crossfade:true)`.
- **Android-Impl** (`AndroidAudioService`, nicht im Shared-Stub): SoundPool fuer SFX, MediaPlayer fuer Musik +
  Crossfade, **AudioFocus-Listener** fuer Telefonanrufe. Assets: 82 SFX (`Assets/Sounds/*.ogg`) + 4 Music-Loops
  (`Assets/Music/*.ogg`).
- **Desktop-Impl** (`DesktopAudioService`, in Desktop-Projekt): Windows NAudio + NAudio.Vorbis; Linux/macOS ffplay-Fallback.
- Plattform-Wiring via `App.AudioServiceFactory` (in MainActivity/Program.cs gesetzt).
- VibrationType (Haptik): per `Vibrate(VibrationType)`, Android-only (Desktop No-Op).

---

## 11. DI-Composition-Root (Reihenfolge, Dispose)

**Quelle: App.axaml.cs**

### 11.1 Initialize()
`AvaloniaXamlLoader.Load`; `RequestedThemeVariant = Dark` (fest); `FpsProfile.Current = Android ? Medium : High`.

### 11.2 OnFrameworkInitializationCompleted()
1. `ConfigureServices(services)` → `BuildServiceProvider()` → `App.Services`.
2. `AsyncExtensions.Logger = Services.GetService<ILogService>()`.
3. Ascension-GS-Bonus + ChallengeConstraints an GameStateService anbinden (vermeidet zirkulaere DI):
   `gss.ExternalGoldenScrewBonusProvider = ascension.GetGoldenScrewBonus; gss.ChallengeConstraints = ...`.
4. `_ = Services.GetService<IMiniGameMasteryService>()` (eager — subscribed im Ctor auf PerfectRatingIncremented).
5. `locService.Initialize()` + `LocalizationManager.Initialize(loc)`.
6. `SkiaThemeHelper.RefreshColors()`.
7. Statische Renderer init (mit IGameAssetService): MeisterHansRenderer, WorkerAvatarRenderer,
   WorkshopGameCardRenderer, Icons.GameIcon; `GameAssetService.Current = assetService`.
8. Lifetime-Branch: Desktop (MainWindow + Splash-Panel; ShutdownRequested → DisposeServices) /
   ISingleViewApplicationLifetime (Android: MainView + Splash-Panel).
9. `RunLoadingAsync(splash)`.

### 11.3 RunLoadingAsync()
`HandwerkerImperiumLoadingPipeline.ExecuteAsync()` (Progress → Splash) → min. `SplashMinimumDisplayMs = 800ms`
anzeigen → `MainViewModel` via `Dispatcher.UIThread.Post` als DataContext → `splash.FadeOut()`. Fehler → still FadeOut.

### 11.4 DisposeServices() — KRITISCHE Reihenfolge (idempotent via `_servicesDisposed`)
1. `IGameLoopService` (zuerst stoppen — tickt sonst gegen gecleante Services).
2. `GameJuiceEngine` (GPU-Ressourcen SKPaint/SKFont/SKPath deterministisch).
3. `Services as IDisposable` → disposed ALLE registrierten IDisposable-Singletons (Reverse-Resolution-Order).
4. Statisch: `Icons.GameIcon.ClearCache()` + `Icons.GameIconRenderer.Cleanup()`.

### 11.5 Plattform-Factories (statische Felder, in MainActivity/Program.cs gesetzt)
`RewardedAdServiceFactory`, `PurchaseServiceFactory`, `AudioServiceFactory`, `NotificationServiceFactory`,
`PlayGamesServiceFactory` (alle `Func<IServiceProvider, IXxx>`), `ReviewPromptRequested` (Action),
`PlatformKeepScreenOn` (Action&lt;bool&gt;). Plus `GameAssetService.PlatformAssetLoader`, `UriLauncher.PlatformShareText`.

### 11.6 DI — alle Singleton
Core: `IPreferencesService("HandwerkerImperium")`, `IGameAssetService`, `AddMeineAppsPremium()`, Android-Override-Factories,
`ILocalizationService` (AppStrings.ResourceManager), `IFrameClock` (FrameClockService, 30Hz), `IUiEffectBus` (UiEffectBus),
`IEternalMasteryService`, `IMissionsFacade`. Game: `IGameStateService`, `IGameIntegrityService`, `ISaveGameService`,
`IGameLoopService`, `IAchievementService`, `IMiniGameMasteryService`, `IAudioService`, `INotificationService`,
`IPlayGamesService`, `IDailyRewardService`, `IIncomeCalculatorService`, `IOfflineProgressService`, `IOrderGeneratorService`,
`IPrestigeService`, `IChallengeConstraintService`, `IContextualHintService`, `INotificationCenterService`, `IWhatsNewService`,
`IReputationShopService`, `IGuildCoopOrderService`, `IWorkerAuctionService`, `IGuildMegaProjectService`, plus ~70 weitere.
Telemetrie: `IAnalyticsService`, `IRemoteConfigService`, `ICloudSaveService`. Coordinator: `ICinematicCoordinator`,
`IReputationTierEffects`, `IGameStartupCoordinator`, `IProgressionFeedbackCoordinator`, `IGameTickCoordinator`.
Navigation: `INavigationService`, `IDialogOrchestrator`, `IMiniGameNavigator`. VMs (Singleton): MainViewModel,
DialogViewModel (auch als `IDialogService`), MissionsFeatureViewModel, HeaderViewModel, PrestigeBannerViewModel,
WelcomeFlowViewModel, GoalBannerViewModel, alle MiniGame-VMs, alle Guild-Sub-VMs, etc.
Ausnahme: `EconomyFeatureViewModel` per `new` in MainViewModel.Economy.cs (kein DI); Thin-Wrapper-Guild-Sub-VMs
im GuildViewModel-Ctor manuell erstellt.

### 11.7 Loading-Pipeline (HandwerkerImperiumLoadingPipeline.cs)
- Schritt 1 "Shader+ViewModel+Icons" (Weight 40, DisplayName `SplashStep_Graphics`): parallel ShaderPreloader.PreloadAll,
  MainViewModel-Resolve, `GameIcon.PreloadAllAsync` (224 Icons), 20 Worker-Portraits.
- Schritt 2 "GameInit" (Weight 35, `SplashStep_Workshops`): `mainVm.InitializeAsync()` (→ GameStartupCoordinator),
  danach `IPurchaseService.InitializeAsync()` (Restore nach SanitizeState-Premium=false).
- Schritt 3 "RemoteConfig" (Weight 5, `SplashStep_Config`): `RemoteConfig.InitializeAsync()` mit 5s-Timeout,
  DailyBundle sync oder deferred.
- Pipeline darf KEINE ViewModels ausser dem MainViewModel-Resolve auswerten; Splash-Mindestanzeige 800ms (SplashMinimumDisplayMs).
- **Loading-Tipps:** Es gibt rotierende DisplayName-Texte pro Schritt (Graphics/Workshops/Config), aber **keine
  separate rotierende "Tipps"-Liste** in dieser Pipeline-Datei gefunden (Tipp-Rotation: nicht im Code gefunden).

---

## 12. UI-Mechaniken

### 12.1 ActivePage (Single Source of Truth + Lazy-Loading mit 1 ContentControl)

**Quelle: MainViewModel.Tabs.cs, App-CLAUDE.md**

- `[ObservableProperty] ActivePage _activePage = ActivePage.Dashboard`. Alle `IsXxxActive` sind berechnete
  Properties (`ActivePage == ActivePage.Xxx`).
- `OnActivePageChanging`: `PageTransitionStarting?.Invoke()` (View setzt Opacity=0 → kein Flimmern).
- `OnActivePageChanged`: Stack-Management (`PageNavigationHelper.ManageStack`), GuildChat-Polling stoppen,
  Gilden-Tab-Besuch stempeln (`membership.LastTabVisitIso`), Notifies fuer GuildBadgeCount/ShopBadgeCount/IsXxxActive
  (nur 2 geaenderte)/IsTabBarVisible/BreadcrumbText/ActiveMiniGameViewModel/IsAnyMiniGameActive/ActivePageContent/HasActivePageContent.
- **ActivePageContent** (1 ContentControl statt 25+): Direct-Bound (`null` → IsVisible-Bindings) fuer
  Dashboard/Buildings/Missionen/Prestige; sonst Mapping ActivePage → VM (Shop→ShopVM, Statistics→StatisticsVM, …,
  Guild-Sub-Pages → GuildViewModel.XxxVM, MiniGames → ActiveMiniGameViewModel). Cold-Start vermeidet ~25 Sub-View-Materialisierungen.
- `HasActivePageContent => ActivePageContent != null`.
- **ActiveMiniGameViewModel** (1 ContentControl fuer 10 MiniGames): switch ActivePage → MiniGames.Sawing/PipePuzzle/…/Invent, sonst null.

### 12.2 IsXxxActive-Liste (vollstaendig, MainViewModel.Tabs.cs)
Dashboard, Shop, Statistics, Achievements, Settings, WorkshopDetail, OrderDetail, SawingGame, PipePuzzle, WiringGame,
PaintingGame, RoofTilingGame, BlueprintGame, DesignPuzzleGame, InspectionGame, WorkerMarket, Buildings, Research,
Manager, Tournament, SeasonalEvent, BattlePass, Guild, Missionen, GuildResearch, GuildMembers, GuildInvite,
GuildWarSeason, GuildBoss, GuildHall, GuildAchievements, GuildChat, GuildWar, Crafting, ForgeGame, InventGame,
Ascension, Prestige, Market, GuildBuildSite.

### 12.3 Imperium-Sub-Tabs (ImperiumSubTab-Enum)
Workshops, Warehouse, Workers, Research, Equipment, Ascension. Default `Workshops`.
- `IsImperiumWarehouseUnlocked => HeaderVM.PlayerLevel >= AutoProductionUnlockLevel (50)`.
- `IsImperiumAscensionUnlocked => Prestige.LegendeCount >= 3`.
- Locked-Tab-Tap → FloatingText ("WarehouseLockedHint" / "AscensionLockedHint", Kategorie "info") statt No-Op.

### 12.4 13-stufige Back-Press-Kaskade (DialogOrchestrator.TryDismissTopmost)

**Quelle: DialogOrchestrator.cs.** Strikte Reihenfolge (jede Stufe konsumiert den Back und gibt true zurueck):

| # | Bedingung | Aktion |
|---|-----------|--------|
| 1 | dlg.IsHintVisible | DismissHintCommand |
| 2 | host.IsLuckySpinVisible | HideLuckySpinOverlay() |
| 3 | host.IsCombinedWelcomeDialogVisible | DismissCombinedDialog() |
| 4 | missions.IsWelcomeOfferVisible | DismissWelcomeOfferCommand |
| 5 | dlg.IsConfirmDialogVisible | ConfirmDialogCancelCommand |
| 6 | dlg.IsPrestigeSummaryVisible | DismissPrestigeSummaryCommand |
| 7 | dlg.IsAlertDialogVisible | DismissAlertDialogCommand |
| 8 | dlg.IsAchievementDialogVisible | DismissAchievementDialogCommand |
| 9 | dlg.IsLevelUpDialogVisible | DismissLevelUpDialogCommand |
| 10 | host.IsOfflineEarningsDialogVisible | CollectOfflineEarningsNormal() |
| 11 | host.IsDailyRewardDialogVisible | IsDailyRewardDialogVisible=false + CheckDeferredDialogs() |
| 12 | dlg.IsStoryDialogVisible | DismissStoryDialogCommand |
| 13 | host.IsWorkerProfileActive | IsWorkerProfileActive=false (Overlay, ActivePage bleibt) |

**Gesamt-Back-Flow** (MainViewModel.HandleBackPressed): 1. DialogOrchestrator.TryDismissTopmost → 2. wenn
ActivePage==Dashboard: `BackPressHelper.HandleDoubleBack("PressBackAgainToExit")` (Double-Back-to-Exit) →
3. sonst stack-basiert `NavigateBack()`. NavigateBack: bei aktivem Order/MiniGame-Screen erst MiniGame-Timer
stoppen + `PauseActiveOrder()` + zum Dashboard; sonst Stack-Pop (Fallback Dashboard). Stack-Cap = 10
(`CappedNavigationStack`).

### 12.5 Tab-Badge-Formeln (MainViewModel.Properties.cs)
- **GuildBadgeCount**: 0 wenn keine Gilde / kein GuildId / IsGuildActive. Wenn `LastTabVisitIso` leer → 1
  (One-Shot beim Erst-Beitritt). Wenn letzter Besuch ≥ 24h her → 1, sonst 0.
- **ShopBadgeCount**: 0 wenn IsShopActive; sonst `WelcomeFlowVM.IsStarterOfferVisible ? 1 : 0`.
- (Boss-Active/MegaProject/Chat-Unread + Shop-Daily-Deal: laut Kommentar Folge-Sprint, aktuell nicht im Badge.)

### 12.6 CeremonyType (5 Typen) + Reward-Ceremony

**Quelle: Graphics/RewardCeremonyRenderer.cs**

| CeremonyType | Akzentfarbe (SKColor) |
|--------------|------------------------|
| LevelMilestone | #F59E0B Amber (10/25/50/100/250/500/1000) |
| WorkshopMilestone | #D97706 Craft-Orange (50/100/250/500/1000) |
| Prestige | #FFD700 Gold |
| Achievement | #22C55E Gruen |
| MasterTool | #A855F7 Lila |

Reward-Ceremony (RewardCeremonyRenderer, `IDisposable`):
- `TotalDuration = 4.0s`, `FadeInDuration = 0.4s`, `ScaleInDuration = 0.6s`, `TextDelay = 0.5s`,
  `FadeOutStart = 3.2s`, `MaxConfetti = 120`. Full-Screen Scale-In + Confetti + Feuerwerk (FireworksRenderer),
  Tap-to-Dismiss. `Start(type, title, subtitle)`, gecachte Paints/Fonts (Title 24f Embolden, Subtitle 16f).

Auslosung via `IUiEffectBus.RaiseCeremony(CeremonyType, title, subtitle)` aus ProgressionFeedbackCoordinator
(z.B. LevelMilestone bei Level-Meilenstein, WorkshopMilestone bei Workshop-Stufe, Prestige nach DoPrestige, MasterTool bei Unlock).

### 12.7 IUiEffectBus / FloatingText / Celebration / Ceremony

**Quelle: UiEffectBus.cs** — reiner Event-Multiplexer (kein State):
- `event Action<string,string> FloatingTextRequested` → `RaiseFloatingText(text, category)`.
- `event Action CelebrationRequested` → `RaiseCelebration()`.
- `event Action<CeremonyType,string,string> CeremonyRequested` → `RaiseCeremony(type, title, subtitle)`.

**FloatingText-Kategorien** (zweiter String-Parameter, frei-form, gefunden in ProgressionFeedbackCoordinator/Tabs):
`"warning"`, `"goldscrews"`, `"golden_screws"`, `"level"`, `"milestone"`, `"currency"`, `"MasterTool"`, `"info"`.
(Es gibt **kein** FloatingType-Enum — die Kategorie ist ein roher String, vom Renderer auf Farben gemappt.
Ein FloatingType-/CelebrationType-Enum wurde nicht gefunden; CelebrationType ist im Code als
parameterloses `RaiseCelebration()` realisiert.)

### 12.8 IsBusy-Guard
**Quelle: App-/ViewModels-CLAUDE.md** — `private bool _isBusy` + try/finally in GuildViewModel, SettingsViewModel,
ShopViewModel, WorkerMarketViewModel fuer alle async-Methoden (Doppel-Tap-Race).

### 12.9 Loading-Screen 800ms + Tipps
- Splash-Mindestanzeige `SplashMinimumDisplayMs = 800` (GameBalanceConstants). Schritt-DisplayNames
  `SplashStep_Graphics` / `SplashStep_Workshops` / `SplashStep_Config`. Separate rotierende Tipp-Texte: nicht im Code gefunden.

### 12.10 Z-Order / Overlay-Stack
- `IsTabBarVisible => PageNavigationHelper.MainTabs.Contains(ActivePage) && !IsWorkerProfileActive`.
- Overlay-States ueberlagern ActivePage (ActivePage bleibt): `IsWorkerProfileActive`, `IsLuckySpinVisible`.
- Welcome-Flow-Dialoge (Offline/DailyReward/CombinedWelcome/StarterOffer), DialogVM-Dialoge (Hint/Confirm/
  PrestigeSummary/Alert/Achievement/LevelUp/Story) — Dismiss-Prioritaet siehe Back-Press-Kaskade (12.4).
- Z-Order der MainView-Layer (Background/Tabs/ContentPanel/Overlays/Splash): konkrete numerische ZIndex-Werte
  (nicht im gelesenen Code gefunden — liegen in MainView.axaml, hier nicht ausgewertet).

### 12.11 Dialog-Typen (DialogViewModel)
**Quelle: DialogViewModel.cs + Partial-Files.** 11 Dialog-Typ-Slots:
1. **Alert** (ShowAlertDialog: Title/Message/ButtonText, IsAlertDialogVisible).
2. **Confirm** (ShowConfirmDialog → `Task<bool>`, IsConfirmDialogVisible, Accept/CancelText, Ad-Banner-Hide).
3. **Story** (DialogViewModel.Story.cs, IsStoryDialogVisible).
4. **Hint** (DialogViewModel.Hint.cs, IsHintVisible — kontextuelle Hints via IContextualHintService).
5. **Achievement** (DialogViewModel.Achievement.cs, IsAchievementDialogVisible).
6. **LevelUp** (DialogViewModel.LevelUp.cs, IsLevelUpDialogVisible, IsLevelUpPulsing).
7. **PrestigeSummary** (DialogViewModel.PrestigeSummary.cs, IsPrestigeSummaryVisible — Tier/Icon/Color/Points/Multiplier/Count).
8. **Prestige-Tier-Auswahl** (eigenstaendige `PrestigeConfirmationViewModel`, IsTierSelectionVisible, Heirloom-Selektion).
9. **ReputationInfo** (ShowReputationInfo → als Alert).
10. **LockedTabHint** (ShowLockedTabHint → FloatingText "TabLockedHint", Kategorie "info").
11. **Welcome-Flow-Dialoge** (in WelcomeFlowViewModel: OfflineEarnings, DailyReward, CombinedWelcome, StarterOffer, WelcomeOffer)
    — als eigene Overlay-Familie, von DialogOrchestrator mit-dismissed.

`IsAnyDialogVisible` (DialogVM) = Story || Hint || LevelUp || Achievement || Alert || Confirm || PrestigeSummary
(prueft NICHT OfflineEarnings/DailyReward — die liegen im Welcome-Flow/Host).

ConfirmDialogAccept/Cancel: setzen IsConfirmDialogVisible=false + PrestigeConfirmation.IsTierSelectionVisible=false
+ `_confirmDialogTcs.TrySetResult(true/false)`.

### 12.12 Sprachen
**Quelle: Resources/Strings/*.resx.** **10 RESX-Sprach-Files**: Basis `AppStrings.resx` (EN-Fallback) + de, en, es,
fr, it, ja, ko, pt, zh-CN. (Hinweis: Workspace-Doku nennt "6 Sprachen DE/EN/ES/FR/IT/PT" als Standard — diese App
hat tatsaechlich zusaetzlich ja/ko/zh-CN, also **10 lokalisierte Sprachen**.)

### 12.13 Progressive Tab-Freischaltung + Level-Schwellen

**Quelle: Models/LevelThresholds.cs, MainViewModel.Tabs.cs**

- **TabUnlockLevels** = `[1, 5, 8, 15, 3]` (Werkstatt 1, Imperium 5, Missionen 8, Gilde 15, Shop 3).
- `IsTabLocked(i)`: nach erstem Prestige (`HasEverPrestiged`) alle frei; sonst `PlayerLevel < TabUnlockLevels[i]`.
- `GetLockedTabs()`: bool[5], gecacht pro Level.
- Weitere Schwellen: BannerStrip 3, QuickJobs 2, CraftingResearch 8, ManagerSection 10, MasterToolsSection 20,
  AutoCollect 15, AutoAccept 25, AutoAssign 20, TournamentSection 35, SeasonalEventSection 45, BattlePassSection 55,
  PrestigeShopUnlock 50, TutorialHintMaxLevel 3, WorkshopCeremonyThreshold 50, ReputationWarningThreshold 50,
  ReputationHighlightThreshold 80, MinPlayerLevel 1, MaxPlayerLevel 1500.
- Hint-Trigger-Level: HintWorkerUnlock 3, HintQuickJobs 2, HintCrafting 8, HintManagerUnlock 10, HintAutomation 15,
  HintMasterTools 20, HintPrestige 50.
- Mid-Game-Vanity-Level-Ups (ProgressionFeedbackCoordinator): 110/120/130/140 (FloatingText + Celebration, kein GS).

### 12.14 Progression-Feedback-Meilensteine (ProgressionFeedbackCoordinator)
- **Spieler-Level-Milestones (Level, GS-Reward):** (10,3), (25,5), (50,10), (100,20), (250,50), (500,100), (1000,200).
- **Workshop-Level-Milestones (Level, GS):** (50,2), (100,5), (250,10), (500,25), (1000,50).
- Eternal-Mastery-FloatingText nach Prestige: `(0.005*n + 0.025*(n/5) + 0.05*(n/10)) * 100` % (n = TotalPrestigeCount).
- Level-Up-Pulse: DispatcherTimer 500ms (`IsLevelUpPulsing`).
- Ascension-Hint-Kaskade: LegendeCount>=3 → AscensionAvailable; sonst nach erstem Prestige → AscensionPath.

---

## 13. Relevante GameBalanceConstants (Infra-/UI-bezogen)

**Quelle: Models/GameBalanceConstants.cs**

| Konstante | Wert |
|-----------|------|
| SplashMinimumDisplayMs | 800 |
| MaxParallelOrders | 3 |
| MaxAuraBonus | 0.50m (50%) |
| AutoProductionUnlockLevel | 50 |
| MaterialOrderCrossWorkshopLevel | 100 |
| MaterialOfferUnlockLevel | 30 |
| EternalMasteryBonusPerPrestige | 0.005m (+0.5%) |
| EternalMasteryBonusPer5Prestiges | 0.025m (+2.5%) |
| EternalMasteryBonusPer10Prestiges | 0.05m (+5%) |
| GetEffectiveHeirloomSlots(isPremium) | isPremium ? MaxHeirloomsPerRunPremium : MaxHeirloomsPerRun (3 bzw. 4 lt. App-CLAUDE.md) |

---

## 14. SettingsData / StatisticsData (Schema)

**Quelle: Models/SettingsData.cs**

| JSON-Key | Property | Typ | Default |
|----------|----------|-----|---------|
| soundEnabled | SoundEnabled | bool | true |
| musicEnabled | MusicEnabled | bool | true |
| hapticsEnabled | HapticsEnabled | bool | true |
| notificationsEnabled | NotificationsEnabled | bool | true |
| graphicsQuality | GraphicsQuality | GraphicsQuality | High |
| reduceMotion | ReduceMotion | bool | false |
| sfxVolume | SfxVolume | float | 1.0f |
| musicVolume | MusicVolume | float | 1.0f |
| keepScreenOn | KeepScreenOn | bool | false |
| cloudSaveEnabled | CloudSaveEnabled | bool | true |
| lastCloudSaveTime | LastCloudSaveTime | DateTime | default |
| language | Language | string | "" |
| analyticsEnabled | AnalyticsEnabled | bool | false (Opt-In) |
| analyticsConsentShown | AnalyticsConsentShown | bool | false |
| lastWhatsNewVersion | LastWhatsNewVersion | string | "0.0.0" |

**Quelle: Models/StatisticsData.cs** — StatisticsData-Felder:
totalMiniGamesPlayed, perfectRatings, perfectStreak, bestPerfectStreak, totalPlayTimeSeconds (long),
totalOrdersCompleted, ordersCompletedToday, ordersCompletedThisWeek, totalWorkersHired, totalWorkersFired,
totalWorkersTrained, totalItemsCrafted, totalItemsAutoProduced (long), totalMaterialOrdersCompleted,
materialOrdersCompletedToday, totalTournamentsPlayed, totalTournamentsWon, totalDeliveriesClaimed,
miniGamePerformance (Dictionary&lt;MiniGameType, MiniGameStats&gt;).
**MiniGameStats:** totalPlays, perfectRatings, misses, rollingResults (List&lt;bool&gt;, "Erfolg" = ≥4 Sterne),
lastPlayedAt (DateTime), `RollingWindowSize = 20` (const). Score-Mapping (App-CLAUDE.md): Perfect=100%, Good=75%, Ok=50%, Miss=0%.

---

## 15. Analytics-Consent-/WhatsNew-Startup-Dialoge (GameStartupCoordinator)

- **Analytics-Consent:** wenn `AnalyticsConsentShown && AnalyticsEnabled` → `analytics.InitializeAsync()`. Wenn
  `!AnalyticsConsentShown` → `ShowAnalyticsConsentIfNeededAsync` (fire-and-forget): 1.5s Delay (+2.5s falls
  Offline/Welcome/DailyReward-Dialog offen), Confirm-Dialog ("AnalyticsConsentTitle"/Message/Accept/Decline),
  setzt `AnalyticsConsentShown=true` + `analytics.IsEnabled=consent`, bei consent `InitializeAsync()`, dann `SaveAsync()`.
- **WhatsNew:** `ShowWhatsNewDeferredAsync` fire-and-forget: 2.5s Delay + bis zu 8×500ms warten falls Dialoge offen,
  dann `whatsNew.ShowWhatsNewIfNeededAsync()`. `Settings.LastWhatsNewVersion` ("0.0.0" Default → Bestandsspieler sehen kumulativ).

---

