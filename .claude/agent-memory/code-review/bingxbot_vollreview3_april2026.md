---
name: BingXBot Vollreview 3 Apr 2026
description: 9 Findings (2 krit): SK-System Clone() ohne State, _tradesToday nicht volatile, SemaphoreSlim Leak
type: project
---

Review des gesamten BingXBot Trading-Kerns am 10.04.2026.

**Kritisch (2):**
1. SK-System Clone() kopiert State-Felder nicht — Deduplizierung/Richtungssperre wirkungslos bei Multi-Symbol
2. _tradesToday (int) wird ohne volatile/Interlocked gelesen in Scan-Guard — MaxTradesPerDay kann umgangen werden

**Hoch (3):**
3. SemaphoreSlim _klineSemaphore nie disposed — Memory Leak bei Start/Stop-Zyklen
4. ContinueWith ohne TaskScheduler in LiveTradingService.Start() (bekannt, noch offen)
5. DailyPnl (Dictionary) + CorrelationMatrix ohne Synchronisation — Concurrent Read/Write Crash

**Mittel (3):**
6. SK-System Tp1CloseRatioOverride=0.3 vs CLAUDE.md "50%" — Doku-Inkonsistenz
7. OpenInterest sequenziell pro Kandidat — Rate-Limit-Risiko bei 20+ Symbolen
8. PaperTradingService.StopAsync() cancelt CTS vor CloseAll (inkonsistent mit Live)

**Niedrig (1):**
9. Static HttpClient _fearGreedClient — DNS-Caching (geringes Risiko)

**Why:** Trading-Bot mit echtem Geld. Clone()-Bug kann doppelte SK-Trades ausloesen.
**How to apply:** Bei SK-System-Aenderungen: Clone() Vollstaendigkeit pruefen. Bei _tradesToday: volatile oder Interlocked.Read.
