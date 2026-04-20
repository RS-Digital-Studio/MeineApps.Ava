---
name: HandwerkerImperium Audit v2.0.31 (April 2026)
description: Post-Review-Audit nach 18.04.2026-Fixes. 4 neue Findings (1 HOCH BAL-1 Ascension-Perks, 2 MITTEL ECON, 1 NIEDRIG UX). Day-1-Flow und Rebirth-Economy gelten als solide.
type: project
---

## HandwerkerImperium Audit v2.0.31 (20.04.2026)

Durchgeführt nach den umfangreichen 18.04.2026-Review-Fixes (Plumber Lv5, MasterSmith 30Mrd, Silver +35%, Rebirth-Kosten halbiert auf 11.750 GS gesamt).

### Bestätigte Stärken (nach Review)
- Day-1-Flow: Plumber Lv5/5k, Carpenter Start 2 Worker, Story Ch.1 statt Welcome
- Rebirth-Economy saniert: 1175 GS/Workshop (50/125/250/250/500), ~107 Tage F2P
- Silver-Prestige durchbricht Weber-Gesetz-Schwelle (+35%)
- MaterialOrders 5/Tag und TrainingCenterSpeed 1.0 adressieren Late-Game-Frust

### Neue Findings
- **BAL-1 (HOCH)**: Ascension Level-3-Perks zu teuer vs. erste Ascension-AP (5-8 AP Erstausgabe vs 6-8 AP/Perk-Max-Level). `asc_eternal_tools` 8 AP + `asc_quick_start` 7 AP sind unerreichbar beim ersten Reset-Highlight. Vorschlag: Level-3-Kosten [3,5,5] (`Models/AscensionPerk.cs:51-106`, `Services/AscensionService.cs:45-76`)
- **ECON-1 (MITTEL)**: Equipment-Shop-Preise (5/15/30/60 GS) + 35 GS/Tag F2P-Rate = Epic-Teil 2 Tage GS-Stillstand. Tote Konversion (`Models/Equipment.cs:137-144`)
- **ECON-2 (MITTEL)**: Premium-Value-Kommunikation nur für Einkommen, nicht für GS-Verdienst sichtbar. Zweite Compare-Zeile "GS heute X -> Premium 2X" empfohlen (CLAUDE.md PremiumIncomeComparison)
- **UX-1 (NIEDRIG)**: Starter-Offer Level 15 passt nicht mehr zu neuem Plumber-Unlock Lv5, Kommentar veraltet. Vorschlag: Level 10 (`MainViewModel.Init.cs:522`)

### Bestätigte Balancing-Werte (verifiziert 20.04.2026)
- Ascension-Perks: MaxLevel 3, Gesamt 61 AP, Level-3-Kosten 5-8 AP
- `CalculateAscensionPoints()` Minimum 5 AP, +2 AP pro bereits durchgeführte Ascension
- Equipment Shop: 5/15/30/60 GS (Common/Uncommon/Rare/Epic)
- Prestige-Shop wiederholbare Items: pp_income_repeatable 15 GS, pp_order_reward_rep 20 GS, pp_delivery_interval_rep 25 GS
- Starter-Offer: PlayerLevel >= 15, 24h Countdown
- Rebirth Total: 1175 GS/Workshop (10 WS = 11.750 GS)

### Keine Findings in
- Onboarding (Day-1-Dialog-Flut durch 18.04-Fixes bereits entschärft)
- Gilden-UX (Mitglieder-Liste-Fix, UpdateLastActiveAsync-Keep-Alive)
- Prestige-Dead-Zones (Silver +35%-Fix wirkt)
- Monetarisierungs-Fairness der 4,99 EUR (kein P2W, Live-Compare existiert)
