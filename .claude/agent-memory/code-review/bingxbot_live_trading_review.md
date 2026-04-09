---
name: BingXBot Live-Trading Review April 2026
description: 10 Findings (3 kritisch, 3 hoch) - Trailing-SL nicht nativ synchronisiert, doppelte Order bei Limit-Fill, TP-Limit Ghost-Orders, Emergency-Stop nullt RestClient
type: project
---

Review vom 07.04.2026. Fokus: Live-Trading-Modus (echtes Geld).

**Kritisch (3):**
1. Trailing-Stop wird nur bot-seitig nachgezogen (_positionSignals), nativer SL auf BingX bleibt beim alten Wert. App-Crash = Gewinnverlust. OnTrailingStopMoved loggt nur.
2. EmergencyStopAsync/StopAsync setzen _restClient=null waehrend parallele Tasks noch laufen koennen (Close-Tasks, letzter PriceTicker-Loop).
3. Kein _positionSignals-Check vor Order-Platzierung. Limit-Orders aus Scan1 die spaet fillen fuehren zu doppelter Position bei Scan2.

**Hoch (3):**
4. TP1/TP2 Limit-Orders (native auf BingX) werden bei Trailing-Phase nicht gecancelt -> BingX schliesst Rest ungewollt.
5. Recovery setzt EntryTime=jetzt statt echte Oeffnungszeit -> Time-Exit tickt falsch (BingX liefert kein OpenTime).
6. RateLimiter SemaphoreSlim-Leaks (kein Dispose in LiveTradingManager).

**Mittel (3):**
7. WebSocket TickerPriceReceived Handler Leak bei Start/Stop/Start (UserDataReceived wird abgemeldet, Ticker nicht).
8. OriginalQuantity nach Precision-Truncation stale -> Partial-Close kann zu gross sein.
9. Funding-Rate nur fuer erstes Symbol abgefragt, fuer alle verwendet.

**Niedrig (1):**
10. ListenKey-Renewal ohne Reconnect bei Ablauf.

**Positiv:**
- K-2 Fix (Signal-zuerst-entfernen bei SL/TP-Hit) korrekt implementiert
- AddOrUpdate fuer Trailing-Stop-Updates atomar
- StartBase Cancel-vor-Dispose korrekt
- Emergency-Stop mit Ghost-Order-Bereinigung + parallellem Close

**Why:** Live-Trading mit echtem Geld. Trailing-SL-Desync ist der kritischste Bug - bei Crash gehen nachgezogene Gewinne verloren.

**How to apply:** 3 kritische Findings vor naechstem Live-Einsatz fixen. OnTrailingStopMoved muss nativen SL auf BingX setzen (Throttle beachten).
