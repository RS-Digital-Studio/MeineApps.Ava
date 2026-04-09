---
name: BingXBot vollstaendiges Code-Review April 2026
description: Umfassende Analyse aller 4 Libraries + Shared (10 Findings, 3 kritisch). SemaphoreSlim-Leak, _tradesToday Race, SimExchange Lock-Gap, Backtest ohne Trailing.
type: project
---

Review vom 05.04.2026 ueber die gesamte BingXBot-Codebasis (Core, Exchange, Engine, Backtest, Shared).

**Kritische Findings:**
1. SemaphoreSlim pro Scan-Durchlauf allokiert (TradingServiceBase:562) - wird nie disposed, OS-Handle-Leak
2. _tradesToday++ nicht atomar (TradingServiceBase:672+747) - Race mit StopBase von UI-Thread
3. SimulatedExchange.ReducePositionAsync gibt WriteLock frei vor ClosePositionAsync (SimulatedExchange:286-289) - Zeitfenster fuer Datenkorruption

**Wichtige Findings:**
4. Typo "ConflueceScore" in 7 Stellen (SignalResult, PositionExitState, CryptoTrendPro, TradingServiceBase)
5. BacktestEngine hat keine Trailing-Stop/Multi-Stage-Exit Logik - Backtest nicht repraesentativ
6. Parallele Kline-Ladelogik nutzt normales Dictionary + lock statt ConcurrentDictionary

**Verbesserungen:**
7. SimulatedExchange nicht IDisposable (ReaderWriterLockSlim-Leak)
8. Funding-Rate Schwellwerte in MarketFilter unklar (Dezimal vs Prozent)
9. BTC-Kontext Scoring in CryptoTrendPro weicht von Dokumentation ab
10. Fire-and-Forget Tasks in StartBase ohne Fehler-Monitoring

**Positiv:**
- TradingServiceBase Architektur: Abstrakt + Hooks ist exzellent
- IndicatorHelper Struct-Cache: Keine Heap-Allokationen
- Trailing-Stop mit atomarem CAS via AddOrUpdate
- RiskManager mit unrealisierten Verlusten + Worst-Case neuer Position
- WebSocket mit ArrayPool + wiederverwendbaren MemoryStreams
- LiveTradingManager: HttpClient/RateLimiter Wiederverwendung

**Why:** Trading-Bot mit echtem Geld. SemaphoreSlim-Leak und Race Conditions koennen zu unerwartetem Verhalten fuehren. Backtest-Divergenz gibt falsche Erwartungen.

**How to apply:** 3 kritische Findings vor naechstem Live-Einsatz fixen. BacktestEngine-Logik mittelfristig mit TradingServiceBase harmonisieren.
