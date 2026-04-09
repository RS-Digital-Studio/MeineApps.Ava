---
name: HandwerkerImperium - Manager-Boni nicht verdrahtet
description: 14 Manager-Definitionen mit 6 Faehigkeiten existieren, aber GetManagerBonusForWorkshop/GetGlobalManagerBonus werden nirgendwo in GameLoopService/IncomeCalculatorService/WorkerService aufgerufen
type: project
---

Manager-System hat NULL Gameplay-Effekt. Nur CheckAndUnlockManagers() wird in GameLoopService aufgerufen (alle 120 Ticks).

**Why:** Die Manager-Boni-Methoden existieren im ManagerService, werden aber nur im ManagerViewModel fuer die Anzeige genutzt. Weder IncomeCalculatorService noch WorkerService wenden sie an.

**How to apply:** Bei jeder Aenderung am Einkommens- oder Worker-System pruefen ob Manager-Boni beruecksichtigt werden. Die 6 Faehigkeiten muessen verdrahtet werden: IncomeBoost + EfficiencyBoost in IncomeCalculatorService, FatigueReduction + MoodBoost in WorkerService.UpdateWorking, TrainingSpeedUp in WorkerService.UpdateTraining, AutoCollectOrders in GameLoopService.
