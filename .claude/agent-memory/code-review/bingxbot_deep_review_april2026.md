---
name: BingXBot Tiefes ATI+DB+Exchange Review April 2026
description: 10 Findings (3 kritisch): CheckAutoTraining Race, TryClaimAutoSave Guard, ExitOptimizer TP-Formel No-Op, Limit-Order Fee-Tracking, LoadFromExchange Endlos-Schleife
type: project
---

Tiefes Review vom 07.04.2026 ueber ATI-Pipeline, DB, Exchange, WebSocket, SimExchange, Backtest.

**3 Kritische Findings:**
1. CheckAutoTraining in AdaptiveTradingIntelligence hat keinen Atomic Guard -- 3 parallele TradingServiceBase-Instanzen koennen gleichzeitig LightGBM.Train() starten. MLContext ist nicht thread-safe.
2. TryClaimAutoSave: _autoSaveGuard wird VOR dem eigentlichen DB-Save zurueckgesetzt -- zweiter Thread kann sofort wieder gewinnen.
3. ExitOptimizer TP-Anpassungsformel (Zeile 141) ist ein No-Op im haeufigsten Fall (AvgLosingTp ~ tp). Intention "TP enger machen bei Verlusten" wird nicht umgesetzt.

**2 Hohe Findings:**
4. Backtest LoadFromExchangeClientAsync: Pagination-Loop ohne CancellationToken, kein Fortschritts-Check, Batch-Offset wird nie veraendert.
5. WebSocket _reconnectAttempts: Normales int-Feld, nicht volatile/atomar -- Race bei parallelem Connect/Reconnect.
6. Backtest TP2 Quantity: Division durch (1-Tp1CloseRatio) ohne Guard gegen Tp1CloseRatio=1.0.

**3 Mittlere Findings:**
7. ConfidenceGate: Berechnet Posterior pro Bucket statt Likelihood-Ratio -- Kommentar "Naive Bayes" irrefuehrend.
8. BotDatabaseService: catch {} in Migrationen verschluckt alle Fehler, setzt aber SchemaVersion hoch.
9. SimulatedExchange.ExecuteOrderLocked: Opening-Fee wird nicht in _positionOpenFees gespeichert -- Limit-Order-Fills haben im CompletedTrade zu niedrige Fee.

**Why:** Tiefes Review ueber 6 Subsysteme nach dem Vollreview und Fixes. Fokus auf mathematische Korrektheit, Thread-Safety im Multi-Mode, und Backtest-Verzerrungen.

**How to apply:** 
- CheckAutoTraining: Interlocked.CompareExchange Guard (INNERHALB des Task.Run, nicht aussen)
- ExitOptimizer: Formel vereinfachen auf Lernen von Gewinnern + Verlierer-Clamp
- Limit-Order Fee: _positionOpenFees auch in ExecuteOrderLocked setzen
