---
name: BingXBot Live-Trading-System Review April 2026
description: 11 Findings (3 krit): Multi-Mode EmergencyStop doppelte Position, CancellationToken EmergencyStop, TP2-Quantity auf OriginalQuantity
type: project
---

Review vom 10.04.2026. 17 Dateien, ~7000 Zeilen. Exchange-Layer + Trading-Services + Risk + Models + Dashboard.

**3 Kritische Findings:**
1. MultiModeOrchestrator.EmergencyStopAllAsync ruft EmergencyStop auf ALLEN 3 Services parallel -- jeder liest Positionen separat und schliesst dieselbe Position. Zweiter/dritter Close kann Gegen-Position eroeffnen (SELL auf geschlossene LONG = neue SHORT).
2. TP2-Quantity berechnet auf exitState.OriginalQuantity statt pos.Quantity -- bei truncated/partial-filled Quantities schliesst TP2 falsche Menge.
3. EmergencyStop API-Calls ohne CancellationToken (SendSignedRequestAsync Default-Ueberladung nutzt CT.None) -- blockiert bis zu 90s bei Rate-Limit.

**3 Hohe Findings:**
4. User-Data-Stream WebSocket hat keinen Auto-Reconnect (Market-WS hat es, User-Data nicht).
5. Retry-Delay in SendSignedRequestAsync ohne CT in 2 von 3 catch-Bloecken (Zeile 310, 318).
6. MultiModeOrchestrator.Dispose: GetAwaiter().GetResult() kann Deadlock verursachen.
7. LiveTradingService.Start: ContinueWith statt async Wrapper fuer WebSocket-Tasks.

**3 Mittlere Findings:**
8. FeatureEngine statische Felder: 3 Multi-Mode-Services setzen parallel (logisch inkonsistent, praktisch unkritisch).
9. Min-Order-Check: checkPrice=0 bei Market-Orders, Notional-Check wird uebersprungen.
10. Auto-Breakeven 0.15% zu eng fuer volatile Coins (kein ATR-Puffer).

**1 Niedriges Finding:**
11. RateLimiter.WaitForSlotAsync: Bei disposed return statt throw -- nachlaufende Tasks umgehen Rate-Limiting.

**Gefixt gegenueber vorherigem Review:**
- RateLimiter IDisposable implementiert
- OnBreakevenSetAsync 3 Retries
- _reconnectAttempts volatile
- MultiMode RecoverOpenPositionsAsync setzt jetzt Standard-SL

**Why:** Live-Trading mit echtem Geld. Die 3 kritischen Findings koennen direkt zu Geldverlust fuehren.

**How to apply:**
1. Multi-Mode Emergency: Nur einen Service EmergencyStop, Rest nur StopBase, oder direkt CloseAllPositions
2. TP2: pos.Quantity statt OriginalQuantity verwenden
3. CT durch gesamte Chain: Dedizierter Emergency-CTS mit kurzem Timeout
