---
name: BingXBot Architektur-Refactoring Review
description: Review des LiveTradingManager-Extrahierens, SimulatedExchange-Verschiebung, DB-Init-Fix - 3 Findings gefunden und gefixt
type: project
---

Review vom 03.04.2026. Architektur-Refactoring: LiveTradingManager extrahiert, SimulatedExchange nach Backtest verschoben.

**Gefixte Findings:**
1. DashboardViewModel.Dispose() disposed LiveTradingManager nicht -> HttpClient/Service-Leak bei App-Schliessung
2. Doppelter SaveAtiStateAsync: StopBot() rief Save auf, dann StopAsync() intern nochmal -> Fix: Paper-Save im VM, Live-Save nur in Manager.StopAsync()
3. RateLimiter bei jedem ConnectAsync() neu erstellt -> als Feld wiederverwendet (analog zu HttpClient)

**Kein Problem:**
- SimulatedExchange-Verschiebung: Alle 6 Referenzen korrekt aktualisiert
- DB-Init: Synchroner Aufruf fuer Desktop angemessen
- Alte _liveClient/_liveService Referenzen: Vollstaendig entfernt, 0 Treffer
- Fire-and-forget Timer: Intern durch try-catch + OperationCanceledException abgesichert
