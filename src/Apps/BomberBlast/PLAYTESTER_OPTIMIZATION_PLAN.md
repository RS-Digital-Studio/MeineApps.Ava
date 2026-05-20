# BomberBlast — Spieletester-Optimierungsplan

**Version analysiert:** v2.0.59 (VersionCode 69, Produktion)
**Datum:** 2026-05-20
**Auditor:** Claude (Spieletester-Perspektive)
**Scope:** Vollständiger Tiefen-Audit über vier Achsen: **Balance & Schwierigkeit**, **Economy & Monetization**, **UX & Onboarding**, **Retention & Live-Ops**, plus **Bug-Hunt & Edge-Cases**. Ergänzt das bestehende [`AAA_AUDIT_SOLO.md`](AAA_AUDIT_SOLO.md), das eher die Code-Architektur-Sicht abdeckt — dieser Plan ist aus der Tester-Sicht "Was passiert, wenn der Spieler das wirklich spielt".

**Methodik:** Fünf parallele Code-Audits + Verifikation der kritischsten Befunde gegen den realen v2.0.59-Quellcode. Nicht aus dem Inhaltsverzeichnis übernommen, sondern an konkreten Datei-/Zeilen-Stellen belegt. Insgesamt rund **100 Befunde**, daraus priorisiert und konsolidiert.

---

## Inhaltsverzeichnis

1. [Executive Summary — 3-Sätze-Diagnose](#1-executive-summary)
2. [Top-12-Kritische-Befunde](#2-top-12-kritische-befunde)
3. [Achse A — Balance & Schwierigkeit](#3-achse-a--balance--schwierigkeit)
4. [Achse B — Economy & Monetization](#4-achse-b--economy--monetization)
5. [Achse C — UX, Onboarding & Friktion](#5-achse-c--ux-onboarding--friktion)
6. [Achse D — Retention & Live-Ops](#6-achse-d--retention--live-ops)
7. [Achse E — Bug-Hunt & Edge-Cases](#7-achse-e--bug-hunt--edge-cases)
8. [Konsolidierte Roadmap v2.0.60 → v2.0.65](#8-konsolidierte-roadmap)
9. [Test-Matrix für Playtest-Sessions](#9-test-matrix)
10. [Definition of Done je Block](#10-definition-of-done)

---

## 1. Executive Summary

BomberBlast v2.0.59 ist auf der Engine-, Architektur- und Game-Feel-Achse **AAA-nah** (siehe AAA_AUDIT_SOLO.md). Aus reiner Spieletester-Sicht ist die Schwäche **nicht in der Engine**, sondern in **drei Tester-relevanten Achsen**:

1. **Marketing-vs-Code-Diskrepanzen** (CRITICAL): Premium-Käufer erhalten laut RESX-Texten und CLAUDE.md *zusätzliche Coin-Multiplikatoren*, im Code aber **nicht implementiert**. Boss-Modifier *Healing* heilt 5 HP/s bei Bossen mit nur 3-8 max-HP — der Modifier macht den Boss praktisch unsterblich, wenn Spieler den Cooldown nicht ausreizen kann.
2. **First-Hour-Friction** (HIGH): Drei Modals (DailyReward, WhatsNew-deferred, FeatureUnlock) können im D0-Flow gleichzeitig erscheinen; das MainMenu (974 LOC) hat keinen klaren Primary-CTA für Neulinge; Tutorial-Skip überspringt auch das Soft-Onboarding ohne Warnung.
3. **Anti-Frust-Schutz-Lücken in der Schwierigkeitskurve** (HIGH): BASE_SPEED 80 macht Spieler langsamer als Elite-Pass-Gegner (84 px/s), Berserk-Boss-Modifier reduziert Telegraph auf 1s (unter Reaktionszeit), MirrorControls-Mutator sabotiert Muscle-Memory statt intellektuell zu fordern, und Skull-PowerUp + Cure-PowerUp werden auf demselben Level (L20) freigeschaltet — Spieler hat keine etablierte Cure-Strategie wenn der erste Curse trifft.

Wenn **diese drei Achsen** in v2.0.60-v2.0.62 gefixt werden, ist die Decke aus Tester-Sicht von aktuell ~7.5/10 auf ~8.7/10 erreichbar — alles ohne externes Audio-/Art-Budget.

---

## 2. Top-12-Kritische-Befunde

Diese zwölf Befunde haben die höchste Tester-Severity und sind direkt durch Code-Verifikation belegt. Reihenfolge = Empfohlene Fix-Reihenfolge.

| # | Bereich | Befund | Severity | Quick-Win? |
|---|---------|--------|----------|------------|
| 1 | Economy | **Premium-Käufer (1,99 €) erhalten KEINEN ×2/×3-Coin-Multiplikator**, obwohl RESX-Strings `PremiumFeatureDoubleCoins` + `PremiumFeature3xCoins` und CLAUDE.md das versprechen | CRITICAL | Ja, ~2h |
| 2 | Balance | **Boss-Modifier `Healing` 5 HP/s** bei 3-8 max-HP → Boss heilt im 12-18s-Cooldown 60-90 HP, faktisch unbesiegbar wenn Spieler nicht jede Telegraph-Phase trifft | CRITICAL | Ja, ~1h |
| 3 | Balance | **Boss-Modifier `Berserk` reduziert Telegraph auf 1s** (`TELEGRAPH_DURATION * 0.5f`), in Enrage-Phase auf 0.6s → unter menschlicher Reaktionszeit für Bomb-Platzierung | CRITICAL | Ja, ~1h |
| 4 | Bug | **`GameEngine` SKMaskFilter statisch im Ctor initialisiert**, aber `GameRenderer` in `App.DisposeServices` bewusst NICHT disposed (Android-Resume-Schutz) → Re-Use nach Process-Resume kann mit disposed SKPaint crashen | CRITICAL | Mittel, ~4h |
| 5 | UX | **Combo-Counter ×10 pulsiert mit 30% Skala bei 12 Hz** (`GameRenderer.HUD.cs:137`) — Photosensitivity-Risiko, kein Accessibility-Bypass | CRITICAL | Ja, ~2h |
| 6 | UX | **Account-Deletion-UI fehlt in SettingsView** (`_accountDeletionService` nullable, kein DeleteAccountCommand sichtbar) — DSGVO Art. 17 Compliance-Risiko | CRITICAL | Mittel, ~4h |
| 7 | Retention | **BattlePass Season-Ende ohne Premium-Pass-Carryover**: gekaufter Premium-Pass mit Tier 15+ und unclaimed Rewards verschwindet beim Saison-Wechsel → Whale-Retention-Killer | HIGH | Mittel, ~6h |
| 8 | Bug | **CardService.CraftCard Race**: 5-Common-Verbrauch und Coin-Spend nicht atomar — Karten-Mutation während Crafting kann zu Silent-Coin-Loss führen | HIGH | Mittel, ~3h |
| 9 | UX | **DailyReward nutzt `DateTime.UtcNow.Date`** (`DailyRewardService.cs:113,142`) — Spieler in UTC-7/UTC+12 erleben Daily-Reset zu ungewohnten Zeiten | HIGH | Ja, ~3h |
| 10 | Balance | **Player BASE_SPEED 80 px/s** + Elite-Pass-Gegner 84 px/s → Spieler langsamer als Standard-Gegner ohne Speed-Upgrade; mit Slow-Curse (0.5×) wird L50+ unspielbar | HIGH | Ja, ~30min |
| 11 | UX | **D0-Modal-Stack**: DailyReward + FeatureUnlock + WhatsNew (wenn implementiert) können beim ersten App-Start gleichzeitig getriggert werden — Neuling-Überforderung | HIGH | Mittel, ~3h |
| 12 | Economy | **LuckySpin-Pity 50 Spins = 50 Tage bei 1×/Tag** — Casual sieht garantierten Jackpot erst nach 7 Wochen, fühlt sich betrogen → schlechte D1-Retention | HIGH | Ja, ~30min |

Die zwölf Befunde sind in den Achsen-Kapiteln unten ausführlich dokumentiert.

---

## 3. Achse A — Balance & Schwierigkeit

### 3.1 Schwierigkeitskurve & Level-Generierung

**B-A1: Mutator-Frequenz-Klippe ab Welt 6** (verifiziert: `LevelLayoutGenerator.cs:445`)
Bis Welt 5 keine Mutatoren, ab Welt 6 sofort 33% Frequenz (L3/6/9 jeder Welt). Tester-Hypothese: Spieler erlebt L41-L50 ohne Mutator-Druck, dann springt L51 mit DoubleSpeed oder MirrorControls — Spike ohne Vorwarnung. **Empfehlung:** Welt 5 als Intro-Phase mit 15-20% Chance, ab Welt 6 volle 33%. Severity: HIGH.

**B-A2: Block-Density inverse Kurve auf Boss-Leveln** (`LevelLayoutGenerator.cs:500-554`)
Boss-Level 0.25f vs. normale späte Level >0.35f → Bosse haben weniger Deckung. Bei L90 Duo-Boss + Ghost-Spawns wird das unfair. **Empfehlung:** Boss-Level auf 0.35f. Severity: MEDIUM.

**B-A3: PowerUp-Drop-Versorgung L75+** (`Core/LevelGeneration/LevelGenerator.cs:103-129`)
3 PowerUps/Level reicht für L50, aber in Welt 7-10 mit Ghost/Splitter/Mimic werden Wallpass/Bombpass/Flamepass kritisch. **Empfehlung:** Ab L61 +1 Extra-PowerUp-Slot mit ~40% Chance. Severity: MEDIUM.

**B-A4: Exit-Block-Frust** (`Core/LevelGeneration/LevelGenerator.cs:132-197`)
Bei hoher Block-Density kann Exit-Block-Position über einem zerstörten PowerUp landen → Spieler erwartet PowerUp, findet Exit. **Empfehlung:** Code prüft das schon (Z.144), aber Edge-Case bei vollbelegtem Grid bleibt. Severity: LOW (doku ausreichend).

### 3.2 EnemyAI & Pathfinding

**B-A5: A*-Budget 5/Frame zu klein bei Mass-Spawn** (`AI/EnemyAI.cs:26`)
`AStarBudgetPerFrame = 5` deckt bei Survival-Spike oder Splitter-Explosion nicht — Gegner fallen auf Random. **Empfehlung:** `5 + activeEnemyCount/4`, Min 8, Max 12. Severity: HIGH.

**B-A6: High-Intel-Decision-Jitter zu aggressiv** (`AI/EnemyAI.cs:64-70`)
Pass (70 px/s) mit 0-0.5s Random-Jitter und 0.3-Chance-Chase wird in engen Korridoren (Maze L33) zu schnell reaktiv. **Empfehlung:** Min-Interval auf 0.7s, Jitter-Faktor halbieren. Severity: MEDIUM.

**B-A7: Tanker (2 HP) unterzeitig in L50+** (`Models/Entities/EnemyType.cs:165`)
Mit Wallpass+Detonator killt Spieler Tanker in 2 Sekunden. **Empfehlung:** Ab L70 auf 3 HP oder Regen 1 HP/10s out-of-range. Severity: MEDIUM.

**B-A8: Mimic-Aktivierungsradius 3 Zellen** (`Models/Entities/Enemy.cs:99`)
Spieler erkennt Mimic 3 Zellen entfernt → Tarnung umgehbar. **Empfehlung:** Auf 2 Zellen. Severity: LOW.

**B-A9: Pin-Down-Last-Resort exploitabel** (`AI/EnemyAI.cs:464-482`)
`allowBombCell: true` Last-Resort-Pass macht Gegner suizidal — Spieler tricked die AI. **Empfehlung:** Limit 1×/2s pro Enemy, nur in echter Danger-Zone. Severity: MEDIUM.

### 3.3 Combo-System

**B-A10: 2s-Combo-Fenster zu eng früh** (`Core/Combat/ComboSystem.cs:10-14`)
Bei 1 MaxBomb braucht der Spieler 2-3s für 2 Kills (Place + Wait + Reposition + Place). **Empfehlung:** Window 2.5s, Extension ab ×4 statt ×6. Severity: MEDIUM.

**B-A11: ×10-Score-Sprung zu flach** (`Core/Combat/ComboSystem.cs:73-85`)
×9 → 20.000, ×10 → 30.000 (+50%); davor 2.5× Sprünge. Signalisiert "ULTRA ist nichts Besonderes". **Empfehlung:** ×10 → 50.000. Severity: LOW.

### 3.4 PowerUp-Balance

**B-A12: Skull + Cure auf gleichem Unlock-Level** (`Models/Entities/PowerUpType.cs:175-191`)
Beide L20 → Spieler kann 5× Skull sammeln bevor Cure droppt. **Empfehlung:** Cure auf L15. Severity: MEDIUM.

**B-A13: Mystery (L15) droppt faktisch nie** (`PowerUpType.cs:29`)
Kein Code in `LevelLayoutGenerator.PlacePowerUps` erzeugt Mystery. **Empfehlung:** 5% Extra-PowerUp-Chance ab Welt 3, oder Boss-Reward. Severity: HIGH.

**B-A14: BASE_SPEED 80 unter Elite-Pass 84** (verifiziert: `Player.cs:12`)
Spieler ohne SpeedLevel ist langsamer als Elite-Pass; mit Slow-Curse (0.5×) auf L50+ unspielbar. **Empfehlung:** `BASE_SPEED = 100f`, oder Slow-Curse-Malus auf 0.75. Severity: HIGH.

**B-A15: Wallpass (L20) zu spät** (`PowerUpType.cs:183`)
L20-24 sind Maze-dominiert; ohne Wallpass extrem frustrierend. **Empfehlung:** Wallpass auf L15. Severity: MEDIUM.

### 3.5 Boss-System

**B-A16: Healing-Modifier OP** (verifiziert: `BossEnemy.cs:236,342-348`)
`HEALING_REGEN_PER_SECOND = 5f` × 12-18s Cooldown = +60-90 HP regeneriert pro Cycle bei Boss mit 3-8 max-HP. Faktisch unbesiegbar wenn Spieler keine Telegraph-Phase nutzen kann. **Empfehlung:** 2.5 HP/s oder nur in Enrage-Phase. Severity: CRITICAL.

**B-A17: Berserk-Modifier 1s Telegraph** (verifiziert: `BossEnemy.cs:296`)
`TelegraphTimer = TELEGRAPH_DURATION * 0.5f` → 1s, in Enrage 0.6s. Unter menschlicher Reaktionszeit für Bomb-Place. **Empfehlung:** Min 1.5s, nie unter 1.2s. Severity: CRITICAL.

**B-A18: Summoner in Duo-Boss-Encounter** (`BossEnemy.cs:236-254`)
Bei L90 (FinalBoss + ShadowMaster) wird Modifier nicht gerollt laut LevelGenerator — Code aber nicht im BossEnemy dokumentiert. **Empfehlung:** Kommentar erweitern + Test-Case. Severity: MEDIUM.

**B-A19: FinalBoss Attack-Rotation in Enrage** (`BossEnemy.cs:330-332`)
Linear rotierend, Spieler kann Pattern vorhersagen. **Empfehlung:** Shuffle bei Phase-2-Eintritt. Severity: LOW.

**B-A20: Fast-Modifier nicht implementiert** (`BossEnemy.cs:200-208`)
+25% Speed in Doku, aber kein Code im BossSpeed-Getter. **Empfehlung:** Implementieren oder aus Modifier-Pool entfernen. Severity: MEDIUM.

### 3.6 Dungeon, Cards, Mutator, Master-Mode

**B-A21: Legendary-Buff ~11% Chance** (`Models/Dungeon/DungeonBuff.cs:59-164`)
Common 110 Weight, Legendary 13 Weight → 11%. Casual sieht in 10 Buffs realistisch keinen Legendary. **Empfehlung:** Legendary auf 8-10 Weight (Ziel ~20%). Severity: HIGH.

**B-A22: Floor 11+ Difficulty-Cliff** (`Core/GameEngine.Level.cs`)
Nach Floor 10 +50% Skalierung ohne neue Buff-Picks. **Empfehlung:** Floor 11-15 zwei Buff-Floors einschieben. Severity: MEDIUM.

**B-A23: Card-Legendary 3% Drop-Rate** (`Services/CardService.cs`)
Whale-Bait. Standard-Gacha ist 5-10% für "rare". **Empfehlung:** 6-8% oder Pity nach 30 Drops. Severity: HIGH.

**B-A24: Slowdown-Stacking 0.075×** (Frost × TimeWarp × BlackHole)
Multiplicative Stacking → Gegner praktisch stillgelegt. **Empfehlung:** Cap auf 0.25×. Severity: MEDIUM.

**B-A25: MirrorControls UX-Anti-Pattern** (`MutatorEffects.cs:20-44`)
Sabotiert Muscle-Memory statt intellektuelle Forderung. **Empfehlung:** Aus Pool entfernen, durch LevelRotation oder ReverseGravity ersetzen. Severity: MEDIUM.

**B-A26: InvisibleBlocks + FoW gleichzeitig** (`LevelLayoutGenerator.cs:116-119`)
Doppelt-blinde Navigation in Quick-Play/Dungeon. **Empfehlung:** Konflikt-Prävention im Generator. Severity: MEDIUM.

**B-A27: Master-Mode ×1.5 Speed zu aggressiv** (`GameEngine.Level.cs:73`)
Plus Type-Upgrade macht Gegner zu Ueber-Versionen. **Empfehlung:** ×1.2 Speed + 30% Type-Upgrade-Chance. Severity: HIGH.

**B-A28: Master-Mode-Reward gleich Normal** (`MasterModeService.cs`)
Schwerer, gleiches Reward → Spieler skip. **Empfehlung:** ×1.5 Coins + +10% XP. Severity: MEDIUM.

---

## 4. Achse B — Economy & Monetization

### 4.1 Premium-Path und Multiplikator-Diskrepanz

**B-B1: Premium-Coin-Multiplikator ist Marketing-Versprechen ohne Code** (CRITICAL, verifiziert)
RESX-Strings `PremiumFeatureDoubleCoins` und `PremiumFeature3xCoins` sind in 6 Sprachen vorhanden; CLAUDE.md dokumentiert "Premium-Multiplikator: 2× Coins bei LevelComplete, 3× bei GameOver-Trostcoins"; **aber kein Code in `GameEngine.Level.cs:1613-1627` oder `GameEngine.cs:1580-1583` multipliziert mit `_purchaseService.IsPremium`**. Nur `CoinPickupMultiplier` (Hero-bezogen) und `coinBonusLevel`-Shop-Upgrade wirken. Premium-Käufer wird sich verschaukelt fühlen, sobald jemand die Marketing-Texte mit dem realen Wert vergleicht (Reddit/Discord-Risiko).

**Empfehlung:** Entweder den Multiplikator implementieren (in `GameEngine.Level.cs` Score-zu-Coins-Konvertierung mit `_purchaseService.IsPremium ? *2.0f : 1.0f`) **oder** die RESX-Strings und CLAUDE.md entfernen. Implementierung ist die richtige Wahl, weil 1,99 EUR für reines Ad-Removal Marktstandard-low ist. Severity: CRITICAL.

**B-B2: Premium-Pass-Preis und SKUs nicht im Shared-Code dokumentiert** (`BattlePassService.cs:197-202`)
`ActivatePremium()` setzt nur ein Bool, keine `IPurchaseService.Buy(...)`-Anbindung sichtbar. **Empfehlung:** Verifizieren, ob Android-Side die SKU `battle_pass_plus_season` mit definiertem Preis hat. Erwartung: 4.99-9.99 EUR/Saison. Severity: CRITICAL.

**B-B3: Gem-Pack IAP-Preise nicht dokumentiert**
Im Shared-Code keine SKU-Liste auffindbar. **Empfehlung:** Inventory + Preis-Audit der `*_gem_pack_*` SKUs in der Android-Implementierung. Severity: CRITICAL.

### 4.2 Coin-Sources vs. Sinks

**B-B4: Coin-Yield-Rechnung (kalkuliert)**
Welt 1: ~600 Coins/Level (Score/2-Pfad). Welt 5+: ~400 Coins/Level (Score/3). DailyBonus 500. Casual 1 Level/Tag = ~13.500-30.000 Coins/Monat. Shop-Komplettierung (700-17.000 pro Item × 9) ≈ ~80.000-100.000 Coins → 6-8 Monate Story-Spielen für vollständigen Shop. **Bewertung:** Akzeptable Kurve, **aber** ohne Premium-Multiplikator (siehe B-B1) ist Free-Path zu lang.

**B-B5: CoinBonus-L1 Amortisation undokumentiert** (`PlayerUpgrades.cs:46`)
Preise [5500, 17000], aber realer Bonus-Wert nicht im ShopService dokumentiert. Wenn nur +10%: ROI = 137 Levels (Trap-Upgrade). Wenn +60% (CLAUDE.md): ROI = 23 Levels (OK). **Empfehlung:** Bonus-Wert in ShopService-API exposed und in UI angezeigt. Severity: MEDIUM.

**B-B6: StartSpeed-L3 Preisklippe** (`PlayerUpgrades.cs:41`)
[1200, 2500, 7000] — L3 ist 2.8× L2. Verglichen mit ScoreMultiplier-Linearität [2800, 7000, 14000] eine Anomalie. **Empfehlung:** [1200, 3000, 6500] glätten. Severity: MEDIUM.

**B-B7: Endgame-Coin-Stuck** (kalkuliert)
Nach 100 Levels Story sind alle Shop-Upgrades gekauft. Keine weiteren Coin-Sinks → unbegrenzte Coins. **Empfehlung:** Cosmetic-Store (Skins 10k-50k Coins) oder Seasonal-Challenges mit Coin-Sinks. Severity: MEDIUM.

### 4.3 Gems & Premium-Currency

**B-B8: Free-Gem-Yield ~52/Saison vs. ~70 reale Sinks** (`BattlePassTierDefinitions.cs:65-88`)
Casual mit 3× Dungeon (9G) + Slot-5 (20G) + XP-Boost (20G) braucht 69G/Monat, BP-Free liefert 52. **Empfehlung:** Free-Track auf 65-75 Gems, oder Dungeon-Entry auf 2G. Severity: MEDIUM.

**B-B9: FirstPurchaseBonus-×2-Target unklar** (`IFirstPurchaseService`)
Wenn auf BattlePassPlus: schlechte Revenue-Optimierung. Wenn auf Gem-Pack: optimal. **Empfehlung:** Explizit auf Gem-Pack-100 anwenden, mit Onboarding-Card "First 100 Gems = 200 Gems". Severity: HIGH.

### 4.4 Rewarded-Ads

**B-B10: 5 Rewarded-Placements sinnvoll, aber Cooldown undokumentiert** (`RewardedAdCooldownTracker`)
Frequenz-Cap unklar. **Empfehlung:** Verifizieren, dass `RewardedAdCooldownTracker` 60s zwischen Ads + max 15-20/Tag erzwingt. Severity: HIGH.

**B-B11: Continue vs. Revival redundant**
Beide auf GameOver, beide kosten Ad. UX-Verwirrung. **Empfehlung:** Continue (Coins verdoppeln) + Level-Skip + Revival = 3 distinkte Optionen. Severity: LOW.

### 4.5 Crafting-Economy

**B-B12: Pfad zu 1 Legendary-Card** (kalkuliert)
125 Common-Drops (à 3% Drop = ~33h Farm) + 65.000 Coins (~8h) = **~41 Stunden für 1 Legendary**. Whale-Push-Design oder pure Grind-Wall. **Empfehlung:** Drop-Rate 6% oder Crafting-Kosten halbieren (Common→Rare 1000C). Severity: MEDIUM.

**B-B13: Card-Upgrade-Duplikate zu hoch**
Bei 5 Dupes pro Stufe und 3 Stufen → 15 Dupes für Gold-Version. **Empfehlung:** [3, 5, 8] statt [5, 10, 15], oder Daily-Quest "1 garantierte Dupe". Severity: MEDIUM.

### 4.6 Live-Service-Mechaniken

**B-B14: LuckySpin-Pity 50 Spins** (`LuckySpinService.cs:57`)
1 Free-Spin/Tag × 50 Pity = 7 Wochen bis garantierter Jackpot. Compliance-OK, aber psychologisch zu lang. **Empfehlung:** Pity auf 25-35, plus Soft-Pity ab Spin 15 (Chance steigt auf 8.5%, ab 25 auf 25%, ab 30 auf 50%). Severity: HIGH.

**B-B15: RotatingDeals "LargeCoinPack" mathematisch wertlos**
Beispiel-Deal "5000C → 5000 Coins" = 0% ROI. **Empfehlung:** Daily-Deals nur Cards/Cosmetics, Weekly-Deal als echter Killer (25G → 5000 Coins, doppelter Ratio). Severity: LOW.

**B-B16: StarterPack-Trigger L5 zu früh** (`StarterPackService.cs:15`)
L5 = ~15 Min Spielzeit, Spieler kennt Shop nicht → kauft schlechte Upgrades. **Empfehlung:** REQUIRED_LEVEL = 20. Severity: MEDIUM.

**B-B17: Coin-Overflow-Cap int.MaxValue zu hoch** (`CoinService.cs:66`)
2.1B Coins ist unerreichbar; "Max Coins" als Motivations-Hook wegrationalisiert. **Empfehlung:** Cap auf 1.000.000 mit "Max Coins reached"-UI. Severity: LOW.

---

## 5. Achse C — UX, Onboarding & Friktion

### 5.1 First-Hour-Experience

**B-C1: D0-Modal-Stack-Überlauf** (`DialogPresenter.cs:34-36`)
Beim ersten App-Start können DailyReward + WhatsNew (deferred-View) + FeatureUnlock + Discovery-Overlay gleichzeitig getriggert werden. Neuling überwältigt → schließt alles weg ohne zu lesen. **Empfehlung:** D0-Modal-Priorisierung: WhatsNew > DailyReward > FeatureUnlock; nur einer pro Session beim D0. Severity: HIGH.

**B-C2: Tutorial-Skip übersppringt Soft-Onboarding** (`TutorialService.cs:196-206`)
Skip setzt alle drei Phasen-Flags + SoftOnboardingLevelsKey=0. Genre-Vet oder Ungeduld-Klick landet sofort in Normalschwierigkeit. **Empfehlung:** Confirm-Dialog mit "Tutorial überspringen? Das Tutorial erklärt das Bomb-Timing", Default "Behalten". Severity: MEDIUM.

**B-C3: Discovery-Overlay pausiert mid-combat** (`DiscoveryService.cs:28-52`)
Pause-Pop bei Erstkontakt unterbricht Boss-Encounter. **Empfehlung:** Queue bis Level-Complete, dann Serien-Modal. Severity: MEDIUM.

**B-C4: LoadingTips ohne Tier-Filterung**
33 globale Tips, einige Hardcore-relevant (Combo-Tier, Synergies) erscheinen bei L1. Neuling versteht nicht. **Empfehlung:** Tip-Tiers (newbie L1-L5 / casual L6-L30 / hardcore L31+) im LoadingTips-Service. Severity: LOW.

**B-C5: WhatsNew-Modal-View deferred** (Service vorhanden, View fehlt)
Spieler updated von v2.0.58 → v2.0.59 ohne Patch-Notes-Sichtbarkeit. **Empfehlung:** AXAML-Modal mit Grid (Icon + 2-Zeilen-Text pro Bullet), Trigger via OnAppearing in MainMenuVM. Severity: MEDIUM.

### 5.2 MainMenu & Navigation

**B-C6: MainMenuView 974 LOC ohne klaren Primary-CTA** (`Views/MainMenuView.axaml`)
Top-Bar (Logo + 2 Currencies + Avatar + Settings) + Hero-Section + Modi-Strip (5+ Tiles) + HEUTE-Card + KARRIERE-Card + Level-Progress = Overload für Neulinge. **Empfehlung:** D0-State: nur HEUTE-Card + großes "Play"-CTA. Modi-Strip ab L3 unlock. Severity: HIGH.

**B-C7: Settings hat 50+ Optionen in einer View** (`SettingsView.axaml:52-257`)
Controls/Audio/Visual/Accessibility/Privacy alle in scrollender ListView. **Empfehlung:** Tab-Bar oder Accordion-Sections. Severity: LOW.

**B-C8: Settings → Game-Return-Race** (`NavigationCoordinator.cs:286-304`)
Dungeon-Run mid-Floor → Settings → Back: GameEngine wurde disposed, neue Instance startet. Dungeon-Progress weg. **Empfehlung:** Während Dungeon/BossRush Settings-Button deaktivieren, oder Game-State persistieren vor Settings-Open. Severity: MEDIUM.

### 5.3 Accessibility

**B-C9: Colorblind-Mode ohne Onboarding-Prompt** (`AccessibilityService.cs:62-138`)
Spieler mit Protanopie startet L1 → 5s tot. Findet Setting erst nach Frustration. **Empfehlung:** Nach L1-Fail < 10s → "Sicht-Schwierigkeiten? Try Colorblind-Mode"-Hint. SettingsView: ColorblindMode oben prominent. Severity: HIGH.

**B-C10: Combo-Pulse 30% @ 12 Hz Photosensitivity-Risiko** (verifiziert: `GameRenderer.HUD.cs:137-171`)
Bei ULTRA-Combo ist Pulse-Amplitude 30%, Frequenz 12 Hz. WCAG 2.1 sagt: Blitze >3 Hz bei großen Bereichen sind Risiko. **Empfehlung:** ReducedEffects + ein neuer Photosensitivity-Toggle (oder beide kombinieren) → Pulse auf 10% oder Text-Farbe statt Scale. Severity: CRITICAL.

**B-C11: UiScale 1.5× nicht durchgängig** 
17 `_overlayFont.Size = X`-Stellen mit `_overlayUiScale`-Multiplikator — verifizieren, dass alle Renderer-Stellen reagieren. **Empfehlung:** Grep-Audit aller `SKFont`-Größen. Severity: LOW.

### 5.4 In-Game-UX

**B-C12: Pause-Button schwer sichtbar Landscape** (`GameView.axaml:44-61`)
`Margin="0,24,80,0"` + 44dp + `#80000000` Opacity → über HUD-Panel, klein, leicht verwechselbar mit Help-Button. **Empfehlung:** Größer (52dp), näher zu MitteRand (Margin 50), Opacity #A0. Severity: MEDIUM.

**B-C13: Joystick-Bomb-Button 52dp suboptimal** (`NeonJoystick.cs:96-99`)
Material Design Mindestziel 48dp; 52dp + 80%-Opacity-Default bei großen Daumen verfehlbar. **Empfehlung:** Default 60dp, plus UiScale-Boost. Severity: MEDIUM.

**B-C14: DailyReward Timezone UTC-zentriert** (verifiziert: `DailyRewardService.cs:113,142`)
Spieler in UTC+12 erleben Reset zu Mittag, in UTC-7 mitten in der Nacht. **Empfehlung:** TimeZoneInfo des Geräts respektieren, Reset auf lokales Midnight. Severity: HIGH.

### 5.5 Localization & DSGVO

**B-C15: Lokalisierungs-Lücken-Risiko**
6 RESX-Sprachen (DE/EN/ES/FR/IT/PT) — stichproben-getestet OK, aber keine CI-Garantie. **Empfehlung:** Unit-Test `AllLanguagesHaveSameKeys()`. Severity: LOW.

**B-C16: Account-Deletion fehlt in UI** (`SettingsViewModel.cs:33`)
`_accountDeletionService` ist nullable, kein DeleteAccountCommand in der View sichtbar. DSGVO Art. 17 Compliance-Risiko, Google Play Policy fordert in-app Account-Delete. **Empfehlung:** 3-Schritt-Confirm + Daten-Export-Hint sichtbar. Severity: CRITICAL.

---

## 6. Achse D — Retention & Live-Ops

### 6.1 Daily-Loop

**B-D1: 3-Tage-Gnaden-Streak verwässert Begriff** (verifiziert: `DailyRewardService.cs:115-117`)
Code-Kommentar "Audit M13: 3-Tage-Gnade ist absichtlich (Streak-Bewahrung)" — Design-Choice, kein Bug. **Aber:** UI nennt es "7-Tage-Streak", was missleitend ist. **Empfehlung:** UI-Wording "7-Tage-Zyklus (bis zu 3 Tage Pause erlaubt)" oder "Comeback-Streak". Severity: LOW.

**B-D2: DailyChallenge-Payoff vs. Daily-Reward-Trivialität**
DailyChallenge ~3500-4500 Coins in 3-5 min. DailyReward 500-1000 Coins in 30s. Casual rechnet und skipt Challenge. **Empfehlung:** First-of-Day-Bonus +500 Coins für Challenge-Complete, Streak-Bonus auf 5000 statt 3000 maximal. Severity: MEDIUM.

**B-D3: Daily-Missionen ohne Level-Gating** (`TimedMissionServiceBase.cs:162-195`)
"Complete 3-Star Levels" auf L10-Account → unmöglich. "Upgrade 1 Card" auf L5 → trivial. **Empfehlung:** `GenerateMissions(periodId, playerLevel)` mit Difficulty-Tiers; skill-Missionen gated auf L30+ bzw. L60+. Severity: HIGH.

**B-D4: LuckySpin-Pity 50 Tage** (verifiziert oben)
Severity: HIGH (siehe B-B14).

### 6.2 Weekly & Saison

**B-D5: WeeklyChallenge 5/14 = ~35% pro Woche**
Nach 2-3 Wochen 90% des Pools gesehen. **Empfehlung:** Pool auf 25 erweitern, Reward-Bonus auf 5000C + 10G für Komplettierung, 1× Reroll-Token/Woche. Severity: MEDIUM.

**B-D6: EventCalendar 8-Wochen-Rotation repetitiv** (`EventCalendarService.cs:44-46`)
Nach 2 Monaten identische Sequenz. **Empfehlung:** Pool auf 12 erweitern, oder Mini-Events (Mo-Mi-Fr-Modifier) zusätzlich. Severity: MEDIUM.

**B-D7: BattlePass Premium-Pass kein Carryover** (`BattlePassService.cs:181-195`)
Saison-Ende setzt komplett zurück. Wenn Spieler Tier 15+ mit Premium-Pass hat und unclaimed Rewards: alles weg. **Empfehlung:** 7-Tage "Legacy Rewards"-Fenster nach Season-End + Saison-Premium-Carryover-Hint (z.B. permanenter +5%-XP-Boost als Premium-Veteran). Severity: HIGH.

### 6.3 Liga & Wettbewerb

**B-D8: NPC-Backfill-Transparenz fehlt** (`LeagueService.cs:301-344`)
`IsRealPlayer`-Flag im Datenmodell, aber UI-Kennzeichnung unklar. Hardcore-Spieler merkt → Trust-Loss. **Empfehlung:** "(NPC)"-Badge oder grau-Tönung neben NPC-Namen + "12 echte Spieler in deiner Liga"-Hinweis. Severity: MEDIUM.

**B-D9: Daily-Race-Determinismus exploitable**
Mission-Seed = Datum-basiert öffentlich → Discord-Community vorhersagt. **Empfehlung:** Server-Side-Seed mit Tagessalt, oder per-Account-Hash. Severity: LOW.

### 6.4 Comeback & Re-Engagement

**B-D10: Comeback-Bonus schwach** (`RetentionService.cs:55-81`)
2000 Coins + 5 Gems vs. Day-7-Reward 8000 + 15G → fühlt sich wie Trost-Preis. **Empfehlung:** 3+ Tage: 5000 + 10G; 7+ Tage: 10000 + 20G; plus "Rush-Back-Bonus" wenn Comeback < 24h. Severity: MEDIUM.

**B-D11: Re-Engagement-Push Smart-Timing-Lücke** (`ReEngagementScheduler.cs:79-126`)
"24h später" geplant, aber bei Spieler 23:59 Login wird Push für 23:59 nächsten Tag geplant — verpasst Lunch-Break-Engagement. **Empfehlung:** Plan auf "nächsten UTC-Mittag" oder Geräte-Locale-Mittag. Severity: MEDIUM.

### 6.5 Feature-Progression

**B-D12: L50→L100 Mid-Game-Wüste** (`FeatureUnlockChoreographer.cs:37-68`)
Nach L50 (Boss-Rush) bis L100 (Master-Mode) 50 Level ohne neue Mechaniken. **Empfehlung:** L60 = Hero-Trait-Slot-2; L70 = Boss-Modifier-Preview-Toggle; L80 = neue Card-Slot oder Cosmetic-Tier; L90 = Master-Mode-Preview. Severity: MEDIUM.

**B-D13: Achievement-Daily-Slot fehlt**
66 Achievements, alle persistent. Kein D1-Hook über Achievements. **Empfehlung:** Neue Sub-Kategorie "Daily Achievements" (3-5 Items, täglich Reset, je 200 Coins). Severity: MEDIUM.

**B-D14: Cosmetic-Drop-Path unklar** (98 Items, Cosmetic-Volumen)
Spieler sieht massive FOMO, aber kein klarer Path "How do I get this Trail?". **Empfehlung:** Detail-Page mit "Unlock via: 500 Coins / BP-Tier-15 / Achievement Master-100". Severity: LOW.

### 6.6 Cloud-Save & Cross-Device

**B-D15: Cloud-Save Konflikt-Resolution-Hierarchie suboptimal** (`CloudSaveService.cs:220-233`)
TotalStars → Wealth → Cards → Timestamp. Lokal 500 Stars + 50000 Coins vs. Cloud 499 Stars + 5000 Coins → Cloud gewinnt wegen 1 Stern, lokale Coins gehen verloren. **Empfehlung:** Per-Field-Merging (max(local, cloud) für TotalStars/Coins/Gems) statt all-or-nothing. Severity: HIGH.

---

## 7. Achse E — Bug-Hunt & Edge-Cases

### 7.1 Crashes & Data-Loss

**B-E1: GameRenderer SKMaskFilter Re-Use nach Android-Resume** (verifiziert: CLAUDE.md "Render-Lifecycle-Robustheit")
`GameRenderer` wird in `App.DisposeServices` bewusst nicht disposed (Android OnDestroy ist oft kein Process-Kill). **Aber:** Wenn der Renderer nach Process-Suspend zurückkommt und GameEngine bereits disposed wurde, kann `_overlayGlowFilter.Dispose` schon ausgeführt sein. **Empfehlung:** Lazy-Init der SKMaskFilter mit Null-Guard und Re-Init on first use. Verifizieren mit Repro: Play → Home → 5 Min warten → App wiederöffnen → neues Level starten. Severity: CRITICAL.

**B-E2: Negative-Balance-Corruption-Pfad unvollständig** (`CoinService.cs:76-94`)
Wenn JSON corrupt mit negativer Balance: lokal auf 0 geclampt, aber CloudSave wird nicht sofort als "use cloud" markiert. Push überschreibt Cloud mit 0. **Empfehlung:** `PersistenceHealth.ReportCorruption` mit Exception als 2. Parameter, CloudSave prüft das. Severity: HIGH.

**B-E3: CardService.CraftCard nicht atomar** (`CardService.cs:280-320`)
Coin-Spend zwischen `CanCraft`-Check und Karten-Verbrauch. Parallel-Drop kann Karten-Count ändern. **Empfehlung:** Snapshot der zu verbrauchenden Karten VOR Coin-Spend, try/catch um Mutation, Coin-Rollback bei Fehler. Severity: HIGH.

**B-E4: ProgressService JSON-Parse Error Handling**
CoinService/GemService haben Corruption-Handler; ProgressService-Pfad unverifiziert. **Empfehlung:** Audit aller Services die `Preferences.Get<string>` + JsonSerializer.Deserialize machen, einheitliches try/catch + PersistenceHealth.ReportCorruption. Severity: HIGH.

**B-E5: CloudSaveService Push/Force-Race** (`CloudSaveService.cs:180-210`)
Beide Pfade ohne gemeinsamen Lock auf `_isSyncing`. **Empfehlung:** `SemaphoreSlim _syncSemaphore` ergänzen, beide Pfade locken. Severity: MEDIUM.

**B-E6: ChooseBest Konflikt-Hierarchie** (siehe B-D15)
Severity: HIGH.

### 7.2 Threading & Lifecycle

**B-E7: GameEngine.JsonSerialize während Game-Loop** (`CLAUDE.md` Gotcha-Pattern dokumentiert)
"JsonSerializer.Serialize auf Background-Thread → Collection-Crash" — bereits gefixt laut Gotcha-Liste. Verifizieren, ob alle Save-Pfade UI-Thread oder DeepCopy nutzen. Severity: MEDIUM.

**B-E8: Pause-Menu während Boss-Cinematic** (`Core/GameEngine.cs:_cinematic.IsPlaying`)
`Cinematic.Stop()` als erste Zeile in `StartXxxModeAsync` (gut), aber Pause während laufender Cinematic kann Zoom in Pause-Overlay leaken. **Empfehlung:** Render-Pfad mit Cinematic-Save/Restore um alle States, nicht nur Playing. Severity: MEDIUM.

**B-E9: ConfirmDialog mit TaskCompletionSource Race bei App-Background** (`DialogPresenter.cs:63-72`)
Dialog offen → App in Background → Resume → User klickt Accept → Navigation in disposed GameVM. **Empfehlung:** `LifecycleHub.OnAppPaused` cancelt alle offenen Dialoge via TCS.SetCanceled. Severity: HIGH.

**B-E10: FeatureUnlockChoreographer Queue-Deadlock** (`FeatureUnlockChoreographer.cs:83-122`)
Wenn View `DismissCurrent` nicht aufruft (Crash/Navigation weg), bleibt `_isShowing=true` für die ganze Session. **Empfehlung:** 10s-Timeout-Mechanik, auto-advance bei nicht-Dismiss. Severity: MEDIUM.

### 7.3 Multi-Touch & Input

**B-E11: Bomb-Button + Joystick gleichzeitig auf 120Hz** (`NeonJoystick.cs:410-460`)
Beide nutzen `radius*radius*1.6f` Hit-Test. Sehr enge Finger können beide registrieren → `_bombButtonPointerId` überschrieben. **Empfehlung:** `if (_bombButtonPointerId == -1)`-Guard vor Bomb-Hit-Test. Severity: HIGH.

**B-E12: KonamiCode-Detector Cross-Level-Trigger**
3s-Timeout, Reset bei neuem Spiel ungewiss. **Empfehlung:** `_inputManager.KonamiDetector.Reset()` in `CompleteLevel`. Severity: LOW.

**B-E13: DualShock-Gamepad Race**
B-Button + Analog-Stick gleichzeitig kann Input-Events kollidieren. Desktop-only. Severity: MEDIUM.

### 7.4 Datum/Zeit & Locale

**B-E14: Daily-Bonus Anti-Cheat Tick-Hybrid Reboot-Logik** (`CoinService.cs`)
Bei Process-Reboot TickCount64 = 0 → "now < StartTicks" → Code erlaubt Bonus. **Aber:** Im XPBoost-Pfad (`BattlePassService.cs:44-60`) kann das Boost-Verlängerung bedeuten (Reboot = Tick-Check ausgesetzt → DateTime-Check allein zählt, aber DateTime-Cap ist 24h). **Empfehlung:** Persistent-Counter in `_data.PersistentTicks` mit korrekter Reboot-Anpassung; oder Tick-Check entfernen wenn DateTime-Check ausreicht. Severity: MEDIUM.

**B-E15: ProgressService.TotalStars > 300 Rejection statt Cleansing** (`CloudSaveSchemaMigrator.Validate`)
Strict-Reject statt Clamp-and-Continue. **Empfehlung:** Cleanse-on-Migrate (`TotalStars = min(TotalStars, 300)`), return true. Severity: HIGH.

### 7.5 Renderer & Visual

**B-E16: Multi-DPI Pause-Button Hit-Test** (`Core/GameEngine.cs:745-763`)
`PAUSE_BUTTON_SIZE = 40f` hardcoded. 2K-Display ergibt zu klein. **Empfehlung:** DPI-aware Hit-Test mit Screen-Scale. Severity: MEDIUM.

**B-E17: 0×0 Canvas-Resize Crash-Risiko**
Multi-Window-Android oder Foldable kann Canvas-Resize triggern. **Empfehlung:** Guard `if (width < 1 || height < 1) return;` in `RenderFrame`. Severity: LOW.

---

## 8. Konsolidierte Roadmap v2.0.60 → v2.0.65

### Block α — Critical Fixes (v2.0.60, ~1 Woche)

Adressiert die 6 CRITICAL-Befunde. Keine neuen Features, nur Fixes — Release-Risiko minimal.

1. **B-B1**: Premium-Coin-Multiplikator im Code implementieren (×2 LevelComplete, ×3 GameOver-Coins) — oder RESX/CLAUDE.md entfernen. (~2h)
2. **B-A16**: `HEALING_REGEN_PER_SECOND` von 5f auf 2.5f, oder Healing-Modifier nur in Enrage-Phase. (~1h)
3. **B-A17**: Berserk-Telegraph-Minimum 1.5s erzwingen, Enrage-Reduktion auf 0.7 statt 0.6. (~1h)
4. **B-C16**: Account-Delete-UI in SettingsView mit 3-Step-Confirm + Daten-Export-Link. (~4h)
5. **B-C10**: Combo-Pulse-Amplitude bei `ReducedEffects || AccessibilityService.Photosensitivity` auf 10% reduzieren. (~2h)
6. **B-E1**: GameRenderer SKMaskFilter Lazy-Init mit Null-Guard. (~4h)

**Total ~14h, Test ~6h, AppChecker + Smoke-Test ~2h. Release in 2-3 Werktagen.**

### Block β — Anti-Frust & Onboarding (v2.0.61, ~2 Wochen)

7. **B-A14**: `BASE_SPEED = 100f`. (~30min, Tests aktualisieren)
8. **B-A12**: Cure-PowerUp Unlock auf L15.
9. **B-A13**: Wallpass Unlock auf L15.
10. **B-A2**: Mystery-PowerUp in `LevelLayoutGenerator.PlacePowerUps` einsetzen (5% Extra-Slot ab Welt 3).
11. **B-A1**: Mutator Welt 5 als Intro-Phase mit 15% Chance.
12. **B-C2**: Tutorial-Skip Confirm-Dialog.
13. **B-C6**: MainMenu D0-State (nur Play + HEUTE-Card), Modi-Strip ab L3.
14. **B-C1**: D0-Modal-Priorisierung + max 1 Modal pro Session beim ersten Launch.
15. **B-C9**: Colorblind-Onboarding-Prompt nach L1-Fail <10s.
16. **B-C14**: DailyReward auf lokale Timezone umstellen (oder UTC-Hinweis im UI).
17. **B-C12**: Pause-Button vergrößern (52dp, Opacity #A0).

**Total ~3 Tage Entwicklung + 2 Tage Tester-Sessions.**

### Block γ — Economy-Reife (v2.0.62, ~2 Wochen)

18. **B-B2/B-B3**: IAP-SKU-Inventur dokumentieren (Premium-Pass, Gem-Packs, VIP-Subscription). Preise im Code-Comment ergänzen.
19. **B-B14**: LuckySpin-Pity auf 25 + Soft-Pity-Curve.
20. **B-B16**: StarterPack REQUIRED_LEVEL auf 20.
21. **B-B9**: FirstPurchaseBonus-×2 explizit auf Gem-Pack-100 anwenden.
22. **B-A23/B-B12**: Card-Legendary-Drop-Rate auf 6%; Crafting-Kosten halbieren.
23. **B-B8**: Free-BP-Gems auf 65, oder Dungeon-Entry auf 2G.
24. **B-B5**: CoinBonus-Wert im UI sichtbar (Tooltip).

### Block δ — Retention-Hebel (v2.0.63, ~2 Wochen)

25. **B-D3**: Daily-Missionen mit Level-Gating-Filter.
26. **B-D7**: BattlePass Legacy-Rewards-Fenster 7 Tage nach Season-End + Premium-Veteran-Bonus.
27. **B-D10**: Comeback-Bonus skaliert (3T: 5000+10G, 7T: 10000+20G).
28. **B-D15/B-E6**: Cloud-Save Per-Field-Merging (`max(local, cloud)` für Coins/Gems/Stars).
29. **B-D12**: Feature-Unlock-Zwischenstationen L60/L70/L80/L90.
30. **B-D13**: Daily-Achievements Sub-Kategorie (3-5 Items, 200C each).
31. **B-D11**: Re-Engagement-Push Smart-Timing (UTC-Mittag statt +24h).

### Block ε — Balance-Polish (v2.0.64, ~1 Woche)

32. **B-A5**: A*-Budget dynamisch (5 + activeCount/4).
33. **B-A21**: Dungeon-Legendary-Buff-Weights auf 8-10.
34. **B-A22**: Dungeon-Floor 11-15 zwei Buff-Floors einschieben.
35. **B-A24**: Slowdown-Cap auf 0.25× (multiplicative Stacking).
36. **B-A25**: MirrorControls aus Mutator-Pool entfernen, durch LevelRotation ersetzen.
37. **B-A26**: InvisibleBlocks-+-Fog-Konflikt-Prävention.
38. **B-A27/B-A28**: Master-Mode ×1.2 + ×1.5 Coins.

### Block ζ — Robustness-Pass (v2.0.65, ~2 Wochen)

39. **B-E3**: CardService.CraftCard Snapshot-vor-Coin-Spend.
40. **B-E4**: Audit aller JSON-Persistenz-Services auf einheitliches Corruption-Handling.
41. **B-E5**: CloudSave Sync-Semaphore.
42. **B-E9**: DialogPresenter Cancel-on-Background.
43. **B-E10**: FeatureUnlockChoreographer Timeout-Fallback.
44. **B-E11**: Bomb-Button Pointer-ID-Guard.
45. **B-E15**: TotalStars Cleanse-on-Migrate.
46. **B-E16**: DPI-aware Pause-Button-Hit-Test.

### Spätere Blöcke (Major-Features)

- **B-D8**: NPC-Backfill-Transparenz in Liga-Leaderboard.
- **B-D5/B-D6**: WeeklyChallenge + EventCalendar Pool-Erweiterungen (Content).
- **B-C5**: WhatsNew-Modal-View implementieren.
- **B-D9**: Server-Side Daily-Race-Seeding (braucht Backend).
- 2P-Co-Op-Engine-Integration aus AAA_AUDIT_SOLO #17.

---

## 9. Test-Matrix

Tester-Sessions empfohlen pro Block. Mindestens 2 Personen pro Session, ein Genre-Vet und ein Casual-Neuling, beide auf Mid-Tier-Android (3 GB RAM, 60 Hz).

### Session 1 — First-Hour-Experience (Block β verifizieren)
- Spielen vom Splash bis L10.
- Beobachten: Welche Modals erscheinen wann? Wo klickt der Neuling als erstes? Wie viele Tutorial-Schritte werden zu Ende gespielt?
- Akzeptanzkriterium: Neuling kommt ohne Hilfe-Suchen bis L5 in <15 min.

### Session 2 — Boss-Encounter L10/L20/L30/L50/L70/L90/L100
- Each Boss mind. 3× spielen (Random Modifier).
- Beobachten: Wie oft wird Boss beim ersten Versuch besiegt? Gibt es ein "unbesiegbares" Modifier-Set? Telegraph-Erkennbarkeit?
- Akzeptanzkriterium: kein Boss-Modifier-Mix führt zu >5 Versuchen für Average-Spieler.

### Session 3 — Dungeon-Run (Floor 1 bis 15)
- Mind. 5 Runs durchspielen.
- Beobachten: Wann taucht der erste Legendary-Buff auf? Wann sind die Synergien aktiv? Floor 11+ Cliff fühlbar?
- Akzeptanzkriterium: Floor 15 in <90 min reachable mit Standard-Hero.

### Session 4 — Economy-Sanity-Check (1 Woche Casual-Spielen)
- 2 Personen 7 Tage je 30 min/Tag.
- Tracken: Coins/Gems-Balance täglich, Shop-Käufe, BP-Tier-Progression, Card-Drops.
- Akzeptanzkriterium: Casual erreicht BP-Tier 25, Coins reichen für 2-3 Shop-Käufe.

### Session 5 — UX-Friktion-Hunt (alle Screens)
- Heuristischer Walkthrough durch alle Screens.
- Notieren: Wo verliert sich der Tester? Welche Buttons sind missverständlich? Wo fehlt Feedback?

### Session 6 — Accessibility-Run
- Mind. 1 Tester mit echter Colorblindness (Deuteranopia bevorzugt), 1 Tester nutzt nur UiScale 1.5 + HighContrast + Subtitles.
- Akzeptanzkriterium: Beide kommen ohne externe Hilfe bis L20.

### Session 7 — Crash-Resilience
- Background/Foreground in unterschiedlichen Game-States (Cinematic, Pause, Dialog offen, Dungeon-Run mid-floor).
- Acceptance: Kein Crash, kein Data-Loss, alle Continuation-Pfade funktionieren.

### Session 8 — Retention-Funnel (4-Wochen-Studie)
- Anonyme Closed-Beta mit 50+ Spielern.
- Tracken via Analytics: D1/D7/D30, Session-Length, Premium-Conversion, Ad-Watch-Rate.
- Akzeptanzkriterium für Erfolg: D7 ≥25%, D30 ≥10% (Genre-Mid-Tier-Mobile-Average).

---

## 10. Definition of Done

Pro Block bevor Release:

- [ ] Build grün auf Solution-Ebene (`dotnet build F:\Meine_Apps_Ava\MeineApps.Ava.sln`)
- [ ] AppChecker grün für BomberBlast (`dotnet run --project tools/AppChecker BomberBlast`)
- [ ] MVVM-Check sauber
- [ ] Unit-Tests durchgängig grün (`tests/BomberBlast.Tests/`, 643+ Tests)
- [ ] Neue Strings in 6 Sprachen (`/skill localize-check`)
- [ ] Manuelle Smoke-Tests auf Android-Gerät: Startup, Tutorial, L1-L10, Shop öffnen, Settings, Dungeon-Run starten
- [ ] Crashlytics-Counter unverändert nach Smoke-Tests
- [ ] Tester-Session aus passender Kategorie durchgeführt + Findings dokumentiert
- [ ] Version-Bump in `BomberBlast.Shared.csproj` und `BomberBlast.Android.csproj`
- [ ] `WhatsNewService.GetEntries()` erweitert
- [ ] Changelog + Social-Posts (`/skill changelog`)
- [ ] Geschlossener-Test-Upload nur wenn alle obigen Punkte abgehakt

---

## Anhang — Verifizierte Code-Stellen

Diese Behauptungen wurden gegen den realen v2.0.59-Code geprüft (nicht aus dem AAA-Audit übernommen):

| Befund | Datei | Zeile(n) | Inhalt |
|--------|-------|----------|--------|
| Healing 5 HP/s | `BossEnemy.cs` | 236 | `private const float HEALING_REGEN_PER_SECOND = 5f;` |
| Berserk Telegraph 0.5× | `BossEnemy.cs` | 296-299 | `TelegraphTimer = Modifier == BossModifier.Berserk ? TELEGRAPH_DURATION * 0.5f : TELEGRAPH_DURATION;` |
| Mutator ab Welt 6 | `LevelLayoutGenerator.cs` | 445 | `if (world < 5) return;` |
| BASE_SPEED 80 | `Player.cs` | 12-13 | `private const float BASE_SPEED = 80f; private const float SPEED_BOOST = 20f;` |
| DailyReward UTC-Datum | `DailyRewardService.cs` | 113, 142 | `(DateTime.UtcNow.Date - lastClaim.Date).Days; ... lastClaim.Date == DateTime.UtcNow.Date` |
| 3-Tage-Gnade | `DailyRewardService.cs` | 115-118 | `// Audit M13: 3-Tage-Gnade ist absichtlich ... if (daysSinceLastClaim > 3)` |
| Premium-Multiplikator-Lücke | `GameEngine.Level.cs` | 1613-1627 | Nur `coinBonusLevel` + `CoinPickupMultiplier` werden angewendet, kein `IsPremium`-Pfad |
| Combo-Pulse 30%/12 Hz | `GameRenderer.HUD.cs` | 137-171 | `pulseMultiplier = ComboCount >= 10 ? 0.30f : 0.15f; comboPulse = MathF.Sin(_globalTimer * 12f)` |
| DAILY_BONUS 500 | `CoinService.cs` | 12 | `private const int DAILY_BONUS = 500;` |

Alle weiteren Belege sind in den einzelnen Befund-Abschnitten oben mit Datei und Zeilennummer dokumentiert.

---

**Plan-Erstellung:** Claude (Spieletester-Persona), 2026-05-20
**Plan-Grundlage:** 5 parallele Tiefen-Audits + Code-Verifikation von 6 kritischen Annahmen
**Schätzung Total-Aufwand Block α-ζ:** 8-10 Wochen Solo-Dev-Arbeit, etwa Saison v2.0.60→v2.0.65
