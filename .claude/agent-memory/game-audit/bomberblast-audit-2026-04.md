---
name: BomberBlast Game-Audit v2.0.30 (2026-04-18)
description: Audit v2.0.30 mit 5 Findings (1 BAL, 2 ECON, 1 UX, 1 JUICE), keine Blocker. Spiel in sehr gutem Zustand.
type: project
---

## Wichtigste Findings

1. **BAL-1 Welt-1 Effizienz-Bonus zu hart**: `GameEngine.Level.cs:924-929` — 5-Bomben-Schwelle für 4.000 Punkte zu ambitioniert für Onboarding. Anfänger braucht ~12 Bomben, bekommt nur 1.500. Empfehlung: `<=8: 4000, <=14: 2500, sonst 1500` in W1.

2. **ECON-1 StartSpeed überteuert**: `PlayerUpgrades.cs:38` — 1.800 Coins für MaxLevel 1, bringt nur was erste Speed-PU gratis liefert. Empfehlung: auf 1.000 senken ODER Max-Level 2.

3. **ECON-2 Gem→Shop-Umrechnung undurchsichtig**: `ShopService.cs:163` — 100 Coins ≈ 1 Gem, aber ohne "Spart-X-Grind"-Kommunikation kein emotionaler Kauf-Trigger.

4. **UX-1 Dungeon-Gem-Eintritt hoch**: 5G pro Extra-Run = 5-8% der L20-Gem-Reserve. Empfehlung: 3G oder 12h-Ad-Cooldown statt 1x/Tag.

5. **JUICE-1 Tutorial Warning-Step Auto-Advance**: `TutorialService.cs:19` — 3s Auto-Advance verpasst Lerngelegenheit "aktiv wegrennen". Empfehlung: Auto-Advance nur wenn Spieler außerhalb Radius.

6. **UX-2 Paid-Continue Wert unkommuniziert** (neu 2026-04-18 Re-Audit): `GameOverViewModel.cs:178, 292` — 199 Coins = Schnäppchen (ca. halber Welt-1-Clear), aber Button zeigt nur "Continue (199 Coins)" ohne Wertkommunikation. Empfehlung: Badge "Fair Price" + vergleichender Hinweis.

7. **JUICE-2 Effizienzbonus-Lernsignal fehlt** (neu): GameOver/LevelComplete Breakdown zeigt Bonus ohne Schwellen-Info. Empfehlung: Hint-Zeile bei <4000 Bonus "Unter 8 Bomben: 2500 Bonus".

## Verifizierte Balancing-Werte

- Shop-Gesamt-Grind ~132.000 Coins (v2.0.29+, -30%)
- Welt-Gating: 0/0/10/25/45/70/100/135/175/200/220 Sterne
- Coins pro Level-Clear: Welt 1 Score/2, sonst Score/3; GameOver Score/6
- Premium: 2x Coins bei Complete, 3x bei GameOver-Trost
- Gem-Quellen: 3G pro erster 3-Sterne-Clear (100 Level × 3G = 300G lifetime)
- StartSpeed: 1.800 Coins (MaxLevel 1)
- CoinBonus: 5.500/17.000 (MaxLevel 2, +25%/+50%)
- Effizienz-Bonus W1 bei ≤5 Bomben: 4.000; bei >12: 1.500

## Nicht gefunden / explizit ausgeschlossen

- Kein P2W (Premium nur 2x Coins + Ad-Removal, keine Exclusive-Content)
- Kein Onboarding-Overload (Tutorial aktiv, progressive Feature-Unlocks)
- Kein fehlender Retention-Hook (Daily/Weekly/BP/Liga/Dungeon alle vorhanden)
- Kein Juice-Mangel (AAA-Level: Slow-Mo, Iris-Wipe, Hit-Pause, Screen-Shake, 5 atmosphärische Subsysteme)
