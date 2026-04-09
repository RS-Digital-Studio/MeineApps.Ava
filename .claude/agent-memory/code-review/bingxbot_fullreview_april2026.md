---
name: BingXBot Vollstaendiges Code-Review April 2026
description: 10 Findings (2 kritisch, 3 hoch) - MultiModeOrchestrator ATI-Loop, SimulatedExchange Thread-Safety, EquityHistory Race, Fire-and-Forget WS, ExitOptimizer Neg-TP, LiveTradingManager Stop, PeriodicTimer Leak, RiskManager Rolling-Reads
type: project
---

Review vom 07.04.2026. 53 CS-Dateien, 9134 Zeilen. 10 Findings.

**Kritisch (2):**
1. MultiModeOrchestrator.StartPaper/StartLive ruft _ati.RegisterStrategies() IN der 3-Modi-Schleife auf. RegisterStrategies() cleared intern die Strategien -- nach dem Loop ist nur die letzte Registrierung aktiv. Alle 3 Services teilen dieselbe ATI-Instanz.
2. SimulatedExchange.SetMarketConditions() schreibt auf normale Dictionary<> ohne Lock, waehrend ApplySlippage() unter _rwLock liest. PaperTradingService ruft SetMarketConditions ausserhalb des Locks auf -- Dictionary-Corruption bei parallelem Zugriff.

**Hoch (3):**
3. _equityHistory (List<decimal>) in TradingServiceBase wird von ProcessCompletedTrade (2 Threads) und GetEquityCurveScaleFactor (ScanLoop) ohne Lock gleichzeitig zugegriffen.
4. LiveTradingService.Start() startet WebSocket-Tasks als fire-and-forget ohne Exception-Logging. Tote WS ohne User-Feedback.
5. ExitOptimizer Verlierer-TP-Formel kann bei extremen AvgLosingTp-Werten negative Multiplikatoren ergeben -- invertiert Long-TP unter Entry.

**Mittel (3):**
6. LiveTradingService.CleanupUserDataStreamAsync: _listenKeyRenewTimer nie Dispose'd, DeleteListenKey fire-and-forget ohne try/catch
7. MultiModeOrchestrator._services ist normales Dictionary, IsAnyRunning iteriert waehrend StopModeAsync Remove'd
8. FeatureEngine statische Felder (SetCrossMarketData) sind bei Multi-Mode-Betrieb Race Condition

**Niedrig (1):**
9. RiskManager Rolling-Properties (WinRate, ProfitFactor, Sharpe) lesen _rollingTrades ohne Lock waehrend UpdateDailyStats unter Lock schreibt

**Positiv:**
- TradingServiceBase-Architektur sauber (Hook-Pattern, kein Code-Duplication)
- ConcurrentDictionary + AddOrUpdate fuer Trailing-Stop korrekt
- Multi-Stage Pyramid Exit (30/30/40) vollstaendig

**Why:** Vollstaendiges Review ueber alle 6 Libraries. Mehrere Thread-Safety-Probleme v.a. im Multi-Mode-Betrieb (3 parallele Trading-Services).

**How to apply:** Kritische + hohe Findings vor dem naechsten Einsatz fixen. Bei kuenftigen Aenderungen am MultiModeOrchestrator besonders auf shared ATI-Instanz achten.
