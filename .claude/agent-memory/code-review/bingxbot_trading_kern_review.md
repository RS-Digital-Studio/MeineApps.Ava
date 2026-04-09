---
name: BingXBot Trading-Kern Logik-Review
description: Tiefgruendige Analyse des Trading-Kerns (TradingServiceBase, Paper/LiveTradingService, RiskManager, SimulatedExchange) - 12 Findings, 3 kritisch
type: project
---

Review vom 03.04.2026. 12 Findings (3 kritisch, 4 hoch, 4 mittel, 1 niedrig).

**Kritische Findings:**
1. Doppelter SL/TP-Trigger: Native BingX Orders + Bot-seitiger PriceTickerLoop pruefen dasselbe Level. Position wird auf BingX nativ geschlossen, Bot versucht erneut zu schliessen -> falscher CompletedTrade + doppelte PnL im RiskManager.
2. Race Condition Trailing-Stop: Read-Modify-Write auf ConcurrentDictionary (TryGetValue -> with { StopLoss = newSl } -> Indexer-Set) ist nicht atomar. User-SL/TP-Aenderungen via UpdatePositionSignal werden ueberschrieben.
3. StartBase disposed CTS ohne Cancel: Bei versehentlichem Doppel-Start laufen potentiell 4 Loops parallel auf denselben ConcurrentDictionaries.

**Hohe Findings:**
- Non-ATI-Pfad aktualisiert positions-Liste nicht nach Close-Signal (ATI-Pfad tut es)
- CalculatePositionSize ignoriert StopLoss-Parameter komplett (kein risiko-basiertes Sizing)
- EmergencyStop Race mit laufendem ScanAndTradeAsync (Nachlaefer-Positionen bleiben offen)
- PnL-Berechnung divergiert zwischen SimulatedExchange (mit Slippage+Opening-Fee) und Live (ohne)

**Why:** Trading-Bot mit echtem Geld. Jeder dieser Bugs kann zu Geldverlust fuehren.

**How to apply:** Diese Findings muessen vor dem naechsten Live-Trading-Einsatz gefixt werden. Bei kuenftigen Aenderungen an TradingServiceBase besonders auf die Parallelitaet RunLoop/PriceTickerLoop achten.
