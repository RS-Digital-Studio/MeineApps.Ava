---
name: HandwerkerImperium Health 2026-04-18 (Abend, nach 11 Fixes)
description: Post-Fix Makro-Analyse v2.0.30 - CLAUDE.md/Splash gefixt, SaveGame-Threading sauber, 1 neuer Bug in RebirthService-Rollback
type: project
---

# HandwerkerImperium Health 2026-04-18 Abend

**Version:** 2.0.30 (csproj+CLAUDE.md synchron) | **Score:** 89 (von 87)

## Delta-Bericht
- CLAUDE.md-Drift gefixt (v2.0.30 in Header)
- App.axaml.cs Splash-Fallback "v2.0.22" entfernt (keine Grep-Treffer mehr)
- MainViewModel.cs 2161 (+12 durch PauseStateChanged-Event)
- MainViewModel.Missions.cs: 28 Zeilen bewusst gelassen
- 58 Services unverändert, 8 StateLoaded-Handler (1 weniger als vorher - vermutlich Zählfehler morgens, 8 ist korrekt)
- Hotspot-Files unverändert: GuildVM 1719, GuildService 1531, EconomyFeatureVM 1328

## Neue Architektur-Bewertungen

### SaveGameService Task.Run (POSITIV)
`SaveInternalAsync` nutzt `Task.Run(() => ExecuteWithLock(...))` für JSON-Serialisierung.
Kommentare dokumentieren: UI-Thread wird nicht mehr blockiert, GameLoop wartet max 50-100ms.
Korrekte Layer-Trennung, State-Lock verhindert Race mit GameLoop.

### MainView PauseStateChanged (POSITIV)
View abonniert Event im Code-Behind mit sauberem Dispose-Pattern (-= in Detach).
Event ist ein Battery-Fix (Render-Timer Stop bei Pause).

### IncomeCalculator Premium-Bonus Duplikation (BEWUSSTE AUSNAHME)
CalculateGrossIncome: Premium +50% auf Einkommen.
CalculateCraftingSellMultiplier: Premium +50% separat, weil Crafting-Verkaeufe NICHT durch
CalculateGrossIncome laufen. Kommentar-Zeile 283 dokumentiert das. Kein DRY-Violation.

### Firebase Rules (POSITIV)
auth_to_player als Single-Source-of-Truth, alle Schreibrechte gehen ueber
`root.child('auth_to_player/' + auth.uid).val() === $playerId`. Korrekt verschaerft.
indexOn auf guilds/level fuer orderBy-Queries vorhanden.

## NEUES FINDING (Hoch)

**[BUG-1] RebirthService.cs:105 — Rollback-Bonus-Exploit**
Datei: Services/RebirthService.cs Zeile 105
Code: `_gameStateService.AddGoldenScrews(cost.goldenScrews);`
Problem: `AddGoldenScrews(amount, fromPurchase=false)` wendet bei Default
`fromPurchase=false` BEIDE Boni an: Prestige-GS-Bonus + Premium-Verdopplung.
Bei failed TrySpendMoney bekommt ein Premium-Spieler also 2x die Kosten zurueck.
Feuert auch `GoldenScrewsChanged`-Event mit verfaelschter Differenz.
Fix: `AddGoldenScrews(cost.goldenScrews, fromPurchase: true)` — Rollback ist
keine Gameplay-Quelle.
