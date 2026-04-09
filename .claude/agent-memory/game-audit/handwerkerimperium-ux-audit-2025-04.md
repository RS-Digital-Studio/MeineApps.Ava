---
name: HandwerkerImperium Game Audit v2.0.25 (Update 2)
description: Zweiter umfassender Game Audit (April 2026) -- 7 Findings (3 BAL, 2 ECON, 1 DESIGN, 1 UX), Gesamtbewertung stark positiv, Hauptpotenzial bei GS-Economy-Transparenz und Saison-Kommunikation
type: project
---

## HandwerkerImperium Game Audit v2.0.25 -- Update 2 (06.04.2026)

### Ergebnis
- 7 verifizierte Findings: 3 BAL, 2 ECON, 1 DESIGN, 1 UX
- Keine kritischen Balancing-Probleme
- Gesamtbewertung: Aussergewoehnlich tiefes Idle-Game mit durchdachter Economy

### Top-3 Prioritaeten
1. **ECON-2**: Saisonaler Winter-Malus (0.90x) unsichtbar fuer Spieler, frustriert ohne Verstaendnis
2. **BAL-1**: Lieferant-GS-Drop (1-3 GS, 20% Chance) bedeutungslos gegenueber 8 GS/Ad
3. **ECON-1**: Rebirth-Gesamtkosten 18.800 GS = ~710+ Tage F2P

### Staerken (verifiziert)
- 16 Meilenstein-Multiplikatoren, abgeflachter Exponent ab Lv500
- 4,99 EUR Premium Lifetime, kein P2W
- Dynamische Daily Rewards (skalieren mit Einkommen)
- Offline-Economy 80%/35%/15%/5% Staffelung, ~2.8h fuer 8h Nacht
- Worker-System mit 10 Tiers als eigenstaendiges Subsystem
- GoalService mit 10 Ziel-Prioritaeten
- Pity-System bei Events (2 negative → nur positive danach)

### Bekannte Balancing-Werte (verifiziert 06.04.2026)
- GS F2P-Rate: ~35 GS/Tag (Daily Challenges 12 + Login ~4.3 + Ad 16 + Gluecksrad ~0.6 + Lieferant ~2.9)
- Rebirth-Gesamtkosten: 18.800 GS (8 WS * 5 Sterne, (100+250+500+500+1000)*8)
- MaxRepeatableShopPurchases: 8 (GameBalanceConstants.cs)
- Income Soft-Cap: 8.0x / Order-Reward Soft-Cap: 10.0x
- Lieferant-GS: 1-3 GS (20% Chance, alle 2-5 Min)
- Gluecksrad Jackpot: 1% (Gewicht 1/100)
- Saisonale Multiplikatoren: Fruehling 1.15x, Sommer 1.20x, Herbst 1.10x, Winter 0.90x
- Bronze-Prestige: 30min 3x Speed-Boost nach Reset
- SoloMeister Challenge: +60% PP (dominiert die Meta)

### Design-Entscheidungen (beabsichtigt, kein Fix noetig)
- Rebirth als extremer Late-Game/Whale-Content ist beabsichtigt
- Bronze-Speed-Boost als Anti-Frustrations-Mechanik beim ersten Prestige
