---
name: HandwerkerImperium Deep-Review 2026-04-06
description: 7 Findings (1 kritisch async void, 1 hoch Premium-Offline-Bonus unklar, Duplikation GetPrestigeIncomeBonus). Manager-Boni jetzt verdrahtet.
type: project
---

Deep-Review 06.04.2026: 7 Findings, davon 1 kritisch.

**Kritisch**: EconomyFeatureViewModel.ActivateRush() ist async void aber kein Event-Handler — Crash bei Ad-Fehler.

**Hoch**: OfflineProgressService beruecksichtigt Premium +50% Einkommensbonus moeglicherweise nicht (muss verifiziert werden ob Workshop.GrossIncomePerSecond Premium inkludiert).

**Manager-Boni**: Frueheres Finding (hi_manager_boni_nicht_verdrahtet.md) ist BEHOBEN — IncomeCalculatorService.CalculateGrossIncome() ruft jetzt GetManagerBonusForWorkshop + GetGlobalManagerBonus auf.

**Duplikation**: GetPrestigeIncomeBonus() in 3 Dateien identisch (OfflineProgressService, CraftingService, GameLoopService.PrestigeCache).

**How to apply:** Bei naechstem Review async void in EconomyFeatureViewModel pruefen. Bei Offline-Earnings-Bugs Premium-Multiplikator-Kette nachverfolgen.
