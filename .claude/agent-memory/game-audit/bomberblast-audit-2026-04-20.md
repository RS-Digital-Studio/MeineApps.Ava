---
name: BomberBlast Vollaudit v2.0.31 (2026-04-20)
description: 8 Findings (2 High, 4 Medium, 2 Low). Top-3: Starter-Pack Mismatch Doku-vs-Code, Gem-Pack Value-Kurve unfair fuer Soft-Whales, StartSpeed Dead-End-Upgrade.
type: project
---

## Top-Findings (Priorisiert)

1. **ECON-1 High**: `StarterPackService.cs:16-18` liefert 2500C/10G/2 Rare — CLAUDE.md 196 verspricht 5000C/20G/3. Mismatch.
2. **ECON-2 High**: `GemShopViewModel.cs:111-154` — Gem/EUR Ratio 101/125/188/334 → Mega zerstoert Soft-Whale-Segment.
3. **BAL-1 Medium**: `PlayerUpgrades.cs:19` — StartSpeed MaxLevel=1 (Dead-End). Nur Preis gesenkt (1800→1200), Struktur nicht.
4. **ECON-3 Medium**: Dungeon Paid-Run 500C/3G fair, aber Reward-Preview fehlt im Entry-Dialog.
5. **ECON-4 Medium**: DailyReward Ceiling 15.500C/Woche = 12% des 130k Shop-Grinds. Zu schwach fuer Langzeit-Retention.
6. **UX-1 Low**: Welt-Gating in CLAUDE.md Zeile 141 veraltet (175/200/220), Code ist 155/180/200.
7. **ECON-5 Low**: LuckySpin nur 9,5% Gem-Segmente. Segment 4 (100C/W20) durch 3G/W15 ersetzen.
8. **UX-2 Low**: Feature-Unlock-Luecke L15-L20 (Dungeon), Spieler-Drop-Risk.

## Verifizierte Werte (v2.0.31)

- **Welt-Gating**: [0,0,10,25,45,70,100,135,155,180,200] (ProgressService.cs:30) — W9/10 weiter gesenkt
- **Shop-Preise**: StartBombs/Fire [700,2500,7000], StartSpeed [1200] ML1, ExtraLives [5000,14000], ScoreMulti [2800,7000,14000], TimeBonus [4000] ML1, ShieldStart [5500] ML1, CoinBonus [5500,17000], PowerUpLuck [3500,10000], Spezial-Bomben [4000/5500/7000]
- **IAP-Pakete**: gem_pack_small 100G/0,99€, medium 500G/3,99€, large 1500G/7,99€, mega 5000G/14,99€
- **BattlePass Premium**: 2,99€ (battle_pass_premium)
- **Dungeon Paid-Run**: 500C oder 3G (DungeonService.cs:18-22) — seit letztem Audit von 5G auf 3G gesenkt
- **Daily Reward**: [500,1000,1500,2000,2500,3000,5000] + Tag 7 Bonus 10G
- **LuckySpin**: 9,5% Gem-Chance (Segment 7: 5G W8, Segment 8 Jackpot: 3000C+10G W5)
- **Starter-Pack**: IST 2500C/10G/2 Rares (CODE); SOLL 5000C/20G/3 Rares (CLAUDE.md)
- **Coin-Payout**: Welt 1 Score/2, sonst Score/3; GameOver Score/6; Premium 2x

## Nicht gefunden / explizit ausgeschlossen

- Kein P2W, kein Pay-Wall, kein Exclusive-Content hinter IAP
- Kein Dominant-Mode (Story/Dungeon/Survival/Daily alle mit eigenen Reward-Pfaden)
- Kein Monetarisierungs-Aggression (5 Rewarded-Placements mit 60s Cooldown, keine Spam-Interstitials)
- Juice/UX keine neuen Findings gegenueber Audit 2026-04-18 (alle alten Punkte gefixt)
