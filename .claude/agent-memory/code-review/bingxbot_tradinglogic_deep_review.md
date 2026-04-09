---
name: BingXBot Trading-Logik Deep Review
description: Tiefes Review TradingServiceBase + LiveTradingService + LiveTradingManager + PaperTradingService. 7 Findings (3 krit): OriginalQuantity falsch, BTC-Trend=0, ATI Score-Scaling fehlt
type: project
---

Review vom 07.04.2026. Fokus auf Logik in PriceTickerLoop, Multi-Stage Exit, Cross-Market Features.

**3 Kritische Findings:**
1. GetHigherTimeframeTrend(null) gibt IMMER 0 zurueck -- BTC-Trend-Feature im ATI immer Neutral. BTC-HTF-Klines werden nicht uebergeben obwohl in klineResults verfuegbar.
2. OriginalQuantity = riskCheck.AdjustedPositionSize, aber tatsaechlich platziert wird positionSize (mit Equity- und Score-Skalierung). TP2-Mengenberechnung deshalb falsch. Math.Min faengt Overflow ab, aber Proportionen stimmen nicht.
3. ATI-Pfad wendet KEIN Score-basiertes Position-Scaling an (GetPositionScaleFactor). Standard-Pfad tut es. ATI riskiert bei ConfluenceScore=6 volle Positionsgroesse statt 75%.

**3 Verbesserungen:**
4. WebSocket-Ticker-Preise (_wsTickerPrices) nur fuer Dashboard exponiert, nicht fuer SL/TP-Pruefung im PriceTickerLoop. 5s REST-Latenz statt sub-100ms.
5. Open Interest: Sequenzielle API-Calls pro Kandidat ohne Semaphore, blockiert Scan-Zyklus. OperationCanceledException wird verschluckt.
6. TP1 auf pos.Quantity-Basis, TP2 auf OriginalQuantity-Basis -- inkonsistent. BingX Precision-Truncation kann Proportionen verschieben.

**1 Hinweis:**
7. ADX-Exit (ADX < 10 schliesst Position) in CLAUDE.md dokumentiert, im Code nicht implementiert.

**How to apply:** Bei OriginalQuantity IMMER die tatsaechlich platzierte Menge speichern, nicht die Pre-Scaling-Groesse. Cross-Market-Features auf korrekte Datenuebergabe pruefen. ATI- und Standard-Pfad muessen identische Scaling-Logik haben.
